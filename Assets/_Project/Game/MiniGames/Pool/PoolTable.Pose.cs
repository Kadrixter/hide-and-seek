using HideAndSeek.Characters;
using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Bodies at the pool table. The local player's root is moved by his own client (it is owner-synced, so everybody
    /// sees him walk): to the address behind the cue ball while it is his turn (around the table, not through it),
    /// staying put through his stroke and the rolling balls, otherwise back to his waiting spot facing the table. The
    /// pose (every peer) is the cue stance of <see cref="BlockyCharacterActions.SetPoolStance"/> fed from the cue.
    /// </summary>
    public sealed partial class PoolTable
    {
        /// <summary>The cue line runs this far right of the shooter's root (under his head when bent over).</summary>
        const float CueSide = 0.2f;
        /// <summary>The walking path around the table: this far outside the rails.</summary>
        const float WalkOut = 0.42f, WalkSpeed = 1.5f;

        Vector2 _rootPos;
        float _rootYaw;
        bool _rootInit;
        Vector3 _rootWritten;
        Quaternion _rootWrittenRot;

        static float PathA => PoolTableGeom.OutX + WalkOut;
        static float PathB => PoolTableGeom.OutZ + WalkOut;

        /// <summary>Own client: walks the own root to where it should be this frame.</summary>
        void TickOwnRoot(float dt)
        {
            int seat = LocalSeat;
            var me = NetPlayer.Local;
            if (seat < 0 || me == null || dt <= 0f)
            {
                _rootInit = false;
                return;
            }
            Transform t = me.transform;
            // First frame, or somebody else moved us (server teleport): continue from there.
            if (!_rootInit || (t.position - _rootWritten).sqrMagnitude > 0.04f)
            {
                _rootPos = ToTable(t.position);
                _rootYaw = LocalYaw(t.rotation);
                _rootInit = true;
            }
            var v = _view[seat];
            Vector2 target = _rootPos;
            float yaw = _rootYaw;
            bool myShot = v.StrokeT >= 0f || (Rolling && _rollSeat == seat) || _awaitShot;
            if (myShot) { }
            else if (LocalTurn)
            {
                if (!(State.BallInHand && _handMode == 1))
                {
                    Address(AimCue(), _aimAngle, out target, out _, out _, out _);
                    yaw = Mathf.Atan2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle)) * Mathf.Rad2Deg;
                }
            }
            else if (!Rolling || _rollSeat != seat)
            {
                target = ToTable(SeatStand(seat));
                yaw = seat == 0 ? 0f : 180f;
            }

            Vector2 cur = _rootPos;
            float dist = (target - cur).magnitude;
            Vector2 next = cur;
            if (dist > 0.0005f)
            {
                float stepLen = WalkSpeed * dt;
                next = dist < 0.45f || SegmentClear(cur, target) ? Vector2.MoveTowards(cur, target, stepLen) : PathStep(cur, target, stepLen);
            }
            Vector2 moved = next - cur;
            float wantYaw = dist > 0.45f && moved.sqrMagnitude > 1e-8f ? Mathf.Atan2(moved.x, moved.y) * Mathf.Rad2Deg : yaw;
            _rootYaw = Mathf.MoveTowardsAngle(_rootYaw, wantYaw, 300f * dt);
            _rootPos = next;

            float y = SeatStand(seat).y;
            Vector3 world = transform.TransformPoint(new Vector3(next.x, 0f, next.y));
            world.y = y;
            Quaternion rot = Quaternion.Euler(0f, transform.eulerAngles.y + _rootYaw, 0f);
            if ((world - _rootWritten).sqrMagnitude > 1e-8f || Quaternion.Angle(rot, _rootWrittenRot) > 0.01f || (t.position - world).sqrMagnitude > 1e-6f)
                t.SetPositionAndRotation(world, rot);
            _rootWritten = world;
            _rootWrittenRot = rot;
        }

        /// <summary>Yaw of a world rotation relative to the table (degrees, 0 = facing table +Z).</summary>
        float LocalYaw(Quaternion world)
        {
            Vector3 f = transform.InverseTransformDirection(world * Vector3.forward);
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        /// <summary>The straight walk from a to b keeps clear of the table.</summary>
        static bool SegmentClear(Vector2 a, Vector2 b)
        {
            float hx = PoolTableGeom.OutX + 0.12f, hz = PoolTableGeom.OutZ + 0.12f;
            Vector2 d = b - a;
            float t0 = 0f, t1 = 1f;
            return Slab(a.x, d.x, hx, ref t0, ref t1) && Slab(a.y, d.y, hz, ref t0, ref t1) ? false : true;
        }

        static bool Slab(float o, float d, float h, ref float t0, ref float t1)
        {
            if (Mathf.Abs(d) < 1e-6f) return o > -h && o < h;
            float a = (-h - o) / d, b = (h - o) / d;
            if (a > b) (a, b) = (b, a);
            t0 = Mathf.Max(t0, a);
            t1 = Mathf.Min(t1, b);
            return t0 < t1;
        }

        /// <summary>One step along the path around the table towards <paramref name="target"/> (the shorter way).</summary>
        static Vector2 PathStep(Vector2 cur, Vector2 target, float stepLen)
        {
            float len = 4f * (PathA + PathB);
            float uc = PathParam(cur), ut = PathParam(target);
            float du = Mathf.Repeat(ut - uc + len * 0.5f, len) - len * 0.5f;
            float ahead = Mathf.Min(Mathf.Abs(du), 0.3f) * Mathf.Sign(du);
            return Vector2.MoveTowards(cur, PathPoint(uc + ahead), stepLen);
        }

        /// <summary>Point on the path rectangle at arc length u (counter-clockwise from (-A, -B)).</summary>
        static Vector2 PathPoint(float u)
        {
            float a = PathA, b = PathB;
            u = Mathf.Repeat(u, 4f * (a + b));
            if (u < 2f * a) return new Vector2(-a + u, -b);
            u -= 2f * a;
            if (u < 2f * b) return new Vector2(a, -b + u);
            u -= 2f * b;
            if (u < 2f * a) return new Vector2(a - u, b);
            u -= 2f * a;
            return new Vector2(-a, b - u);
        }

        /// <summary>Arc length of the path point nearest to <paramref name="p"/>.</summary>
        static float PathParam(Vector2 p)
        {
            float a = PathA, b = PathB;
            float dx = a - Mathf.Abs(p.x), dz = b - Mathf.Abs(p.y);
            if (dz < dx)
            {
                float x = Mathf.Clamp(p.x, -a, a);
                return p.y < 0f ? x + a : 2f * a + 2f * b + (a - x);
            }
            float z = Mathf.Clamp(p.y, -b, b);
            return p.x > 0f ? 2f * a + (z + b) : 4f * a + 2f * b + (b - z);
        }

        protected override float ApplyPose(BlockyCharacterActions act, int seat, float seed)
        {
            var v = _view[seat];
            if (!v.PoseValid)
            {
                Vector3 here = act.transform.position;
                act.SetBoardStand(Centre, 0f, 0f, Centre, here, seed);
                return 0f;
            }
            act.SetPoolStance(v.Grip, v.Bridge, v.Bend, v.Look, v.Walk, seed);
            return 0f;
        }
    }
}
