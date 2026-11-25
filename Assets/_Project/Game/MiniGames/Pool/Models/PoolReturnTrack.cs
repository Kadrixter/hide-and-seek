using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Ball return under the pool table (2026-11-25), shared by the model and the view. Every pocket drops its balls
    /// through a metal funnel and a short rod cage onto a gully of two parallel rods; there are two gullies, one per
    /// long side (side 0 = -Z: pockets 0, 1, 2; side 1 = +Z: pockets 3, 4, 5). A gully starts under the head corner
    /// pocket, runs along its long side under the apron with a gentle slope towards the foot, passes under the side
    /// and the foot corner pocket, turns round the foot leg and ends in the collection tray hung under the foot rail.
    /// Both gullies run into the same tray from opposite ends and stop at a rubber-faced divider in its middle.
    /// Everything here is the BALL-CENTRE line in table space; the rods lie <see cref="RodDrop"/> under it,
    /// <see cref="RodHalfGap"/> to each side. U = distance along a gully from its upstream end.
    /// </summary>
    public static class PoolReturnTrack
    {
        /// <summary>Rod radius, half the distance between the two rods, ball centre above the rods' centres.</summary>
        public const float RodR = 0.0042f, RodHalfGap = 0.0165f;
        public static readonly float RodDrop = Mathf.Sqrt((PoolDims.BallR + RodR) * (PoolDims.BallR + RodR) - RodHalfGap * RodHalfGap);
        /// <summary>|z| of the straight gully, ball height at the head drop and at the divider, x of the tray's middle line.</summary>
        public const float GullyZ = 0.565f, StartY = 0.515f, EndY = 0.468f, TrayX = 1.19f;
        /// <summary>Half the divider's thickness (rubber faces): the first ball of each side stops this far plus a radius from z = 0.</summary>
        public const float DividerHalf = 0.006f;
        public const float StopZ = PoolDims.BallR + DividerHalf;
        /// <summary>The gully runs on this far upstream of the head pocket's drop (the hairpin end behind the drop).</summary>
        public const float Tail = 0.036f;
        /// <summary>Half-length of the tray (z) and its inner half-width (x).</summary>
        public const float TrayHalfL = 0.335f, TrayHalfW = 0.034f;
        /// <summary>Ball centre in a pocket's mouth, where it leaves the cloth and starts to fall.</summary>
        public const float MouthY = PoolDims.ClothY - PoolDims.BallR * 0.5f;

        /// <summary>One gully: ball-centre points, U at each, plan heading at each (unit, y = 0).</summary>
        public sealed class Line
        {
            public Vector3[] P, H;
            public float[] U;
            public float Length;
            /// <summary>U under the drop of each of the side's three pockets (head corner, side, foot corner).</summary>
            public float[] Land = new float[3];
            /// <summary>Turn round the foot leg: centre (plan) and radius; U where the turn starts and ends.</summary>
            public Vector2 TurnC;
            public float TurnR, TurnU0, TurnU1;
        }

        static Line[] _lines;

        public static int SideOf(int pocket) => pocket < 3 ? 0 : 1;
        static int Slot(int pocket) => pocket % 3 == 0 ? 0 : pocket % 3 == 1 ? 1 : 2;

        public static Line Gully(int side)
        {
            _lines ??= new[] { Build(-1), Build(1) };
            return _lines[side & 1];
        }

        /// <summary>U on its gully under pocket <paramref name="pocket"/>'s drop.</summary>
        public static float LandU(int pocket) => Gully(SideOf(pocket)).Land[Slot(pocket)];

        /// <summary>Ball centre at U on a gully (clamped to its ends) and the plan heading there.</summary>
        public static Vector3 At(Line l, float u, out Vector3 heading)
        {
            u = Mathf.Clamp(u, 0f, l.Length);
            int lo = 0, hi = l.U.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (l.U[mid] <= u) lo = mid; else hi = mid;
            }
            float k = l.U[hi] > l.U[lo] ? (u - l.U[lo]) / (l.U[hi] - l.U[lo]) : 0f;
            heading = Vector3.Slerp(l.H[lo], l.H[hi], k);
            return Vector3.Lerp(l.P[lo], l.P[hi], k);
        }

        public static Vector3 At(Line l, float u) => At(l, u, out _);

        /// <summary>Ball centre where a ball from <paramref name="pocket"/> lands on the rods (under the cage).</summary>
        public static Vector3 Landing(int pocket) => At(Gully(SideOf(pocket)), LandU(pocket));

        /// <summary>Plan centre of the drop (funnel and cage) under pocket <paramref name="pocket"/>: right over its landing.</summary>
        public static Vector2 DropPlan(int pocket)
        {
            var p = Landing(pocket);
            return new Vector2(p.x, p.z);
        }

        // ================================================================ construction

        /// <summary>
        /// Radius of the foot turn: the arc from the straight gully (|z| = <see cref="GullyZ"/>) to the tray line
        /// (x = <see cref="TrayX"/>) that passes right under the foot corner pocket's centre.
        /// </summary>
        static float TurnRadius()
        {
            Vector2 c = G.Centre(2);
            float px = c.x, pz = Mathf.Abs(c.y);
            // Centre (TrayX - R, GullyZ - R): (R - (TrayX - px))^2 + (R - (GullyZ - pz))^2 = R^2.
            float a = TrayX - px, b = GullyZ - pz;
            float sum = a + b, disc = sum * sum - (a * a + b * b);
            return sum + Mathf.Sqrt(Mathf.Max(0f, disc));
        }

        static Line Build(int sz)
        {
            var plan = new List<Vector2>();
            Vector2 head = G.Centre(sz < 0 ? 0 : 3);
            float x0 = head.x - Tail, z0 = Mathf.Abs(head.y);
            float r = TurnRadius(), xt = TrayX - r, cz = GullyZ - r;
            // Straight part: eases from the head pocket's line out to GullyZ over the first 35 cm.
            for (float x = x0; x < xt; x += 0.012f)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(head.x, head.x + 0.35f, x));
                plan.Add(new Vector2(x, sz * Mathf.Lerp(z0, GullyZ, k)));
            }
            int turn0 = plan.Count;
            const int arcN = 36;
            for (int i = 0; i <= arcN; i++)
            {
                float th = i / (float)arcN * Mathf.PI * 0.5f;
                plan.Add(new Vector2(xt + r * Mathf.Sin(th), sz * (cz + r * Mathf.Cos(th))));
            }
            int turn1 = plan.Count - 1;
            for (float z = cz - 0.012f; z > StopZ + 0.006f; z -= 0.012f) plan.Add(new Vector2(TrayX, sz * z));
            plan.Add(new Vector2(TrayX, sz * StopZ));

            var l = new Line { TurnC = new Vector2(xt, sz * cz), TurnR = r };
            int n = plan.Count;
            l.P = new Vector3[n];
            l.H = new Vector3[n];
            l.U = new float[n];
            for (int i = 1; i < n; i++) l.U[i] = l.U[i - 1] + Vector2.Distance(plan[i], plan[i - 1]);
            l.Length = l.U[n - 1];
            l.TurnU0 = l.U[turn0];
            l.TurnU1 = l.U[turn1];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = plan[Mathf.Min(i + 1, n - 1)] - plan[Mathf.Max(i - 1, 0)];
                d.Normalize();
                l.H[i] = new Vector3(d.x, 0f, d.y);
                // Even slope from the head drop to the divider (the short tail behind the drop a touch higher).
                float y = Mathf.LerpUnclamped(StartY, EndY, (l.U[i] - Tail) / (l.Length - Tail));
                l.P[i] = new Vector3(plan[i].x, y, plan[i].y);
            }
            int[] pockets = sz < 0 ? new[] { 0, 1, 2 } : new[] { 3, 4, 5 };
            for (int k = 0; k < 3; k++) l.Land[k] = Closest(l, plan, G.Centre(pockets[k]));
            return l;
        }

        /// <summary>U of the gully point nearest (in plan) to <paramref name="p"/>.</summary>
        static float Closest(Line l, List<Vector2> plan, Vector2 p)
        {
            float best = float.MaxValue, bu = 0f;
            for (int i = 0; i + 1 < plan.Count; i++)
            {
                Vector2 a = plan[i], d = plan[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(1e-9f, d.sqrMagnitude));
                float dist = (a + d * t - p).sqrMagnitude;
                if (dist < best) { best = dist; bu = Mathf.Lerp(l.U[i], l.U[i + 1], t); }
            }
            return bu;
        }

        // ================================================================ rods

        /// <summary>Horizontal unit vector to the left of a plan heading.</summary>
        public static Vector3 Left(Vector3 heading) => new Vector3(-heading.z, 0f, heading.x);

        /// <summary>Centre of rod <paramref name="s"/> (-1 / +1) under a ball centre <paramref name="c"/> heading <paramref name="h"/>.</summary>
        public static Vector3 Rod(Vector3 c, Vector3 h, int s) => c + Left(h) * (s * RodHalfGap) + Vector3.down * RodDrop;
    }
}
