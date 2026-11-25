using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>Geometry of the cue at address (shooter's spot, bridge, elevation) and of the stroke.</summary>
    public sealed partial class PoolTable
    {
        /// <summary>
        /// Address for a shot from <paramref name="cue"/> at <paramref name="angle"/>: where the shooter's root stands
        /// (table 2D: behind the ball on the line, outside the rails, the cue line on his right), how far behind the ball
        /// the bridge hand is (on the rail when the ball is close to it) and the cue elevation that clears the rail.
        /// </summary>
        static void Address(Vector2 cue, float angle, out Vector2 stand, out float bridgeLen, out bool rail, out float elev)
        {
            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var right = new Vector2(d.y, -d.x);
            float nose = RayOut(cue, -d, PoolTableGeom.HalfL, PoolTableGeom.HalfW);
            float outer = RayOut(cue, -d, PoolTableGeom.OutX, PoolTableGeom.OutZ);
            // A little closer than an arm's length plus the cue, but the hips 30 cm off the rail (the chest clears it).
            float back = Mathf.Max(1.0f, outer + 0.3f);
            stand = KeepOut(cue - d * back - right * CueSide, BodyClear);
            bridgeLen = Mathf.Lerp(0.25f, 0.16f, Mathf.InverseLerp(1.0f, 1.5f, back));
            rail = nose < bridgeLen + 0.03f;
            if (rail) bridgeLen = nose + PoolTableGeom.CushionW + PoolTableGeom.RailW * 0.45f;
            float need = (PoolDims.RailTopY + 0.022f - (PoolDims.ClothY + PoolDims.BallR)) / Mathf.Max(0.03f, nose - PoolDims.BallR);
            // The right fist (as thick as a blocky arm) on the butt must pass over the rail too when it is above the table.
            float grip = TipGap + PoolDims.CueLength - GripZ;
            Vector2 gp = cue - d * grip;
            if (Mathf.Abs(gp.x) < PoolTableGeom.OutX + 0.3f && Mathf.Abs(gp.y) < PoolTableGeom.OutZ + 0.3f)
                need = Mathf.Max(need, (PoolDims.RailTopY + FistHalf + 0.03f - (PoolDims.ClothY + PoolDims.BallR)) / grip);
            elev = Mathf.Clamp(Mathf.Atan(need), 5.5f * Mathf.Deg2Rad, 35f * Mathf.Deg2Rad);
        }

        /// <summary>Half the thickness of a blocky fist / forearm (4 px): a hand rests this far above what it lies on.</summary>
        const float FistHalf = 0.115f;

        /// <summary>How far the shooter's root (hips) stays outside the rails' outer edge: the belly and the thighs clear the rail.</summary>
        const float BodyClear = 0.34f;

        /// <summary>
        /// <paramref name="p"/> moved out of the table's outline grown by <paramref name="margin"/> (to the nearest side), so
        /// a body standing there never overlaps the rails - an oblique shot or the cue line's side offset could put the
        /// root half inside the table.
        /// </summary>
        public static Vector2 KeepOut(Vector2 p, float margin)
        {
            float hx = PoolTableGeom.OutX + margin, hz = PoolTableGeom.OutZ + margin;
            float dx = hx - Mathf.Abs(p.x), dz = hz - Mathf.Abs(p.y);
            if (dx <= 0f || dz <= 0f) return p;
            if (dx < dz) p.x = Mathf.Sign(p.x == 0f ? 1f : p.x) * hx;
            else p.y = Mathf.Sign(p.y == 0f ? 1f : p.y) * hz;
            return p;
        }

        /// <summary>Distance from <paramref name="p"/> (inside) along <paramref name="d"/> to the box edge (half sizes hx, hz).</summary>
        static float RayOut(Vector2 p, Vector2 d, float hx, float hz)
        {
            float t = 99f;
            if (d.x > 1e-5f) t = Mathf.Min(t, (hx - p.x) / d.x);
            else if (d.x < -1e-5f) t = Mathf.Min(t, (-hx - p.x) / d.x);
            if (d.y > 1e-5f) t = Mathf.Min(t, (hz - p.y) / d.y);
            else if (d.y < -1e-5f) t = Mathf.Min(t, (-hz - p.y) / d.y);
            return Mathf.Max(0f, t);
        }

        /// <summary>Cue on the shot line (table space): butt position and rotation (local +Z = butt to tip).</summary>
        static void CuePose(Vector2 cue, float angle, Vector2 spin, float elev, float tip, out Vector3 butt, out Quaternion rot)
        {
            var d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var r = new Vector3(d.z, 0f, -d.x);
            var ball = new Vector3(cue.x, PoolDims.ClothY + PoolDims.BallR, cue.y);
            Vector3 contact = ball - d * (PoolDims.BallR * 0.97f) + r * (spin.x * PoolDims.BallR * 0.5f) + Vector3.up * (spin.y * PoolDims.BallR * 0.5f);
            Vector3 axis = d * Mathf.Cos(elev) - Vector3.up * Mathf.Sin(elev);
            butt = contact - axis * (tip + PoolDims.CueLength);
            rot = Quaternion.LookRotation(axis, Vector3.up);
        }

        /// <summary>Left fist of the stance (table space): on the cloth (or the rail) under the cue, beside it on the left.</summary>
        static Vector3 BridgeFist(Vector2 cue, float angle, float elev, float bridgeLen, bool rail)
        {
            var d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var r = new Vector3(d.z, 0f, -d.x);
            float h = PoolDims.ClothY + PoolDims.BallR + Mathf.Tan(elev) * Mathf.Max(0f, bridgeLen - PoolDims.BallR);
            float surf = rail ? PoolDims.RailTopY : PoolDims.ClothY;
            Vector3 p = new Vector3(cue.x, 0f, cue.y) - d * bridgeLen - r * 0.1f;
            // The fist rests ON the cloth / the rail: its centre half a (blocky, thick) fist above it.
            p.y = Mathf.Max(surf + FistHalf + 0.04f, h - 0.03f);
            return p;
        }

        /// <summary>Tip distance behind the contact point during a stroke (negative = through where the ball was).</summary>
        static float StrokeTip(SeatView v)
        {
            StrokeShape(v, out float back, out float follow, out float tf);
            float t = v.StrokeT;
            if (t < tf)
            {
                float k = t / tf;
                return back - (back + follow) * k * k;
            }
            if (t < StrokeHold) return -follow;
            return Mathf.Lerp(-follow, TipGap + 0.05f, Mathf.SmoothStep(0f, 1f, (t - StrokeHold) / 0.45f));
        }

        /// <summary>Stroke time at which the tip reaches the ball.</summary>
        static float StrokeContactT(SeatView v)
        {
            StrokeShape(v, out float back, out float follow, out float tf);
            return tf * Mathf.Sqrt(back / (back + follow));
        }

        static void StrokeShape(SeatView v, out float back, out float follow, out float tf)
        {
            back = TipGap + Mathf.Clamp01(v.StrokePull) * MaxPull;
            follow = 0.03f + 0.17f * v.StrokePower;
            // Accelerating stroke whose tip speed at the end is about the cue ball's start speed.
            tf = Mathf.Clamp(1.7f * (back + follow) / Mathf.Max(0.3f, PoolSim.ShotSpeed(v.StrokePower)), 0.06f, 0.3f);
        }
    }
}
