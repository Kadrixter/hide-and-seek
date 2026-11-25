using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Board view of a player at the pool table (used by <see cref="MiniGameCamera"/>, mouse wheel = zoom). Own turn:
    /// low behind the cue ball looking down the shot line (follows the aim at once, stays for the stroke); watching
    /// (rolling balls, the other player's turn): high three-quarter view of the whole table from the own side; Space
    /// (and ball in hand): steep view from the own side, under the lamp. The modes blend smoothly. The other players are
    /// drawn as shadows while they stand in front of or right next to the camera.
    /// </summary>
    public sealed partial class PoolTable
    {
        float _wAim, _wTop;
        readonly System.Collections.Generic.List<NetPlayer> _ghosted = new(), _ghostNow = new();

        public override void CameraPose(int seat, float zoom, out Vector3 pos, out Quaternion rot)
        {
            float s = Side(seat), px = PlaceX(seat) * 0.8f;
            // Watching: three-quarter view of the whole table from behind the own waiting spot (not over a team mate).
            Vector3 p0 = new Vector3(px, PoolDims.ClothY + 0.95f * zoom, s * 1.45f * zoom);
            Vector3 a0 = new Vector3(0f, PoolDims.ClothY, 0f);
            // Top: steep, from the own side (the lamp hangs over the middle, the camera stays in front of it).
            Vector3 p1 = new Vector3(0f, PoolDims.ClothY + 1.3f * zoom, s * 0.85f * zoom);
            Vector3 a1 = new Vector3(0f, PoolDims.ClothY, s * 0.03f);
            float kt = Mathf.SmoothStep(0f, 1f, _wTop);
            Vector3 lp = Vector3.Lerp(p0, p1, kt);
            Vector3 la = Vector3.Lerp(a0, a1, kt);
            pos = transform.TransformPoint(lp);
            rot = Quaternion.LookRotation(transform.TransformPoint(la) - pos, transform.up);
            if (_wAim > 0.001f && seat == LocalSeat && LocalAimFrame(out var cue, out float angle))
            {
                AimCamera(cue, angle, zoom, out var ap, out var ar);
                float k = Mathf.SmoothStep(0f, 1f, _wAim);
                pos = Vector3.Lerp(pos, ap, k);
                rot = Quaternion.Slerp(rot, ar, k);
            }
        }

        /// <summary>Low camera behind the cue ball along the shot (world).</summary>
        void AimCamera(Vector2 cue, float angle, float zoom, out Vector3 pos, out Quaternion rot)
        {
            var d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var ball = new Vector3(cue.x, PoolDims.ClothY + PoolDims.BallR, cue.y);
            Vector3 cam = ball - d * (0.8f * zoom) + Vector3.up * (0.24f * zoom + 0.05f);
            Vector3 at = ball + d * 0.75f - Vector3.up * 0.02f;
            pos = transform.TransformPoint(cam);
            rot = Quaternion.LookRotation(transform.TransformPoint(at) - pos, transform.up);
        }

        /// <summary>Cue ball and angle the own camera follows (own stroke: where it was struck from).</summary>
        bool LocalAimFrame(out Vector2 cue, out float angle)
        {
            int seat = LocalSeat;
            cue = default;
            angle = 0f;
            if (seat < 0) return false;
            var v = _view[seat];
            if (v.StrokeT >= 0f && v.StrokeLocal || v.StrokeT >= 0f && v.StrokeT < 1f)
            {
                cue = v.StrokeCue;
                angle = v.StrokeAngle;
                return true;
            }
            cue = AimCue();
            angle = _aimAngle;
            return true;
        }

        void TickCamera(float dt)
        {
            int seat = LocalSeat;
            if (seat < 0)
            {
                _wAim = _wTop = 0f;
                Unghost();
                return;
            }
            var v = _view[seat];
            bool stroke = v.StrokeT >= 0f && v.StrokeT < 0.9f;
            bool holding = LocalHolding;
            bool aimCam = !_topView && !holding && (LocalTurn || stroke);
            bool topCam = _topView || holding;
            float rate = Mathf.Max(dt, 0f) / 0.6f;
            _wAim = Mathf.MoveTowards(_wAim, aimCam ? 1f : 0f, rate);
            _wTop = Mathf.MoveTowards(_wTop, topCam ? 1f : 0f, rate);
            TickGhost(seat);
        }

        /// <summary>
        /// Every other player (the other team, the own team mate, a spectator) as a shadow while he stands between our
        /// camera and what it looks at, or right next to the camera (walking round the table).
        /// </summary>
        void TickGhost(int seat)
        {
            CameraPose(seat, 1f, out var cam, out _);
            Vector2 c = ToTable(cam), mid = Vector2.zero;
            if (_wAim > 0.5f && LocalAimFrame(out var cue, out _)) mid = cue;
            Vector2 seg = mid - c;
            _ghostNow.Clear();
            foreach (var other in NetPlayer.All)
            {
                if (other == null || other.IsOwner || other.Character == null) continue;
                Vector2 o = ToTable(other.transform.position);
                float t = Mathf.Clamp01(Vector2.Dot(o - c, seg) / Mathf.Max(1e-4f, seg.sqrMagnitude));
                float d = (c + seg * t - o).magnitude;
                // A bent shooter's head is far from his feet: the nearest point of his body counts too.
                bool near = (o - c).magnitude < 1.3f || BodyNear(other, cam, 1.3f);
                if (d < 0.45f && t < 0.75f || near) _ghostNow.Add(other);
            }
            foreach (var g in _ghosted)
                if (g != null && !_ghostNow.Contains(g)) g.SetGhostBody(false);
            foreach (var g in _ghostNow) g.SetGhostBody(true, false);
            _ghosted.Clear();
            _ghosted.AddRange(_ghostNow);
        }

        /// <summary>Some part of the player's body is within <paramref name="dist"/> of the camera.</summary>
        static bool BodyNear(NetPlayer p, Vector3 cam, float dist)
        {
            if (p.Character == null) return false;
            Vector3 root = p.transform.position;
            if ((new Vector2(root.x, root.z) - new Vector2(cam.x, cam.z)).sqrMagnitude > 6.25f) return false;
            foreach (var r in p.Character.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.bounds.SqrDistance(cam) < dist * dist) return true;
            return false;
        }

        void Unghost()
        {
            foreach (var g in _ghosted) if (g != null) g.SetGhostBody(false);
            _ghosted.Clear();
        }

        PoolAimHud _aimHud;

        /// <summary>Power bar and spin ball of the own turn.</summary>
        void TickHud()
        {
            bool seated = LocalSeat >= 0;
            if (_aimHud == null)
            {
                if (!seated) return;
                _aimHud = PoolAimHud.Create(transform);
            }
            _aimHud.Tick(seated && LocalTurn && !LocalHolding && !_awaitShot, _pull, _dragging, _spin);
        }
    }
}
