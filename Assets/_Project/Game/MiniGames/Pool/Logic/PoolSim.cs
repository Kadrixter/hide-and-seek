using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Ball physics of the pool table (2D on the cloth, deterministic: the server and every client run the same shot).
    /// The work is done by <see cref="PoolWorld"/> (steps, friction, contacts) on the geometry of <see cref="PoolRails"/>.
    /// Input positions and the shot are snapped to the network grid first, so a peer that got them through
    /// <see cref="PoolState.Encode"/> simulates exactly the same numbers as the server.
    /// </summary>
    public static class PoolSim
    {
        /// <summary>Cue ball speed for power 0 and 1 (m/s).</summary>
        public const float MinSpeed = 0.5f, MaxSpeed = 7.5f;
        /// <summary>Hard limit of a simulated shot (s): everything is stopped there.</summary>
        public const float MaxTime = 25f;

        /// <summary>Cue ball speed (m/s) for a power 0..1: quadratic, so soft shots are easy to dose (0.5 -> 2.25 m/s).</summary>
        public static float ShotSpeed(float power)
        {
            if (float.IsNaN(power) || float.IsInfinity(power)) power = 0f;
            power = Mathf.Clamp01(power);
            return MinSpeed + (MaxSpeed - MinSpeed) * power * power;
        }

        /// <summary>The shot snapped to the grid the simulation uses (angle 1e-5 rad in -pi..pi, power / spin 1e-4); NaN -> 0.</summary>
        public static PoolShot Quantize(PoolShot s)
        {
            float a = Mathf.Repeat(Fin(s.Angle) + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
            return new PoolShot(Mathf.Round(a * 1e5f) / 1e5f, Mathf.Round(Mathf.Clamp01(Fin(s.Power)) * 1e4f) / 1e4f,
                Mathf.Round(Mathf.Clamp(Fin(s.SpinX), -1f, 1f) * 1e4f) / 1e4f, Mathf.Round(Mathf.Clamp(Fin(s.SpinY), -1f, 1f) * 1e4f) / 1e4f);
        }

        static float Fin(float x) => float.IsNaN(x) || float.IsInfinity(x) ? 0f : x;

        /// <summary>Simulates a whole shot from <paramref name="pos"/> / <paramref name="down"/> until every ball stops.</summary>
        public static PoolSimResult Run(Vector2[] pos, ushort down, PoolShot shot)
        {
            var r = new PoolSimResult();
            var w = new PoolWorld(r, pos, down);
            w.Strike(Quantize(shot));
            w.AddFrame();
            int step = 0, maxSteps = Mathf.RoundToInt(MaxTime / PoolWorld.Dt);
            while (w.AnyMoving())
            {
                if (step >= maxSteps)
                {
                    w.StopAll();
                    break;
                }
                w.Step();
                step++;
                if (step % PoolWorld.StepsPerFrame == 0) w.AddFrame();
            }
            if (step % PoolWorld.StepsPerFrame != 0) w.AddFrame();
            w.Finish();
            return r;
        }

        /// <summary>
        /// Aim guide: the cue ball at <paramref name="cue"/> sent at <paramref name="angle"/>. Returns false when it
        /// would hit nothing before a cushion (then <paramref name="ghost"/> = where it touches the cushion). Else
        /// <paramref name="ghost"/> = cue ball centre at contact, <paramref name="hit"/> = object ball, <paramref name="objDir"/>
        /// = where that ball goes, <paramref name="cueDir"/> = where the cue ball goes on (stun shot, tangent line).
        /// A pocket mouth in the way counts as a cushion (ghost = where the centre reaches the hole).
        /// </summary>
        public static bool Predict(Vector2[] pos, ushort down, Vector2 cue, float angle, out Vector2 ghost, out int hit, out Vector2 objDir, out Vector2 cueDir)
        {
            ghost = cue;
            hit = -1;
            objDir = cueDir = Vector2.zero;
            if (PoolRails.Bad(cue) || float.IsNaN(angle) || float.IsInfinity(angle)) return false;
            Vector2 d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float best = 6f;
            if (pos != null)
                for (int j = 1; j < 16 && j < pos.Length; j++)
                {
                    if ((down & (1 << j)) != 0 || PoolRails.Bad(pos[j])) continue;
                    float t = PoolRails.Toi(pos[j] - cue, -d, PoolDims.BallD, best);
                    if (t < 0f || (hit >= 0 && t >= best)) continue;
                    best = t;
                    hit = j;
                }
            bool rail = PoolRails.Hit(cue, d, ref best, out _, out _);
            for (int k = 0; k < 6; k++)
            {
                float t = PoolRails.Toi(cue - PoolRails.Hole[k], d, PoolRails.HoleR[k], best);
                if (t >= 0f && t < best)
                {
                    best = t;
                    rail = true;
                }
            }
            ghost = cue + d * best;
            if (rail || hit < 0)
            {
                hit = -1;
                return false;
            }
            Vector2 n = pos[hit] - ghost;
            objDir = n.sqrMagnitude > 1e-12f ? n.normalized : d;
            Vector2 tan = d - objDir * Vector2.Dot(d, objDir);
            cueDir = tan.sqrMagnitude > 1e-8f ? tan.normalized : Vector2.zero;
            return true;
        }

        /// <summary>Ball in hand: may the cue ball be put down at <paramref name="p"/>? (on the cloth, not touching a ball, in the kitchen when asked).</summary>
        public static bool CanPlaceCueBall(Vector2[] pos, ushort down, Vector2 p, bool kitchen)
        {
            if (PoolRails.Bad(p) || !PoolRails.OnField(p)) return false;
            if (kitchen && p.x > PoolState.KitchenX + 5e-5f) return false; // the head spot itself is in the kitchen
            const float min = PoolDims.BallD + 0.0001f;
            if (pos != null)
                for (int j = 1; j < 16 && j < pos.Length; j++)
                    if ((down & (1 << j)) == 0 && !PoolRails.Bad(pos[j]) && (pos[j] - p).sqrMagnitude < min * min) return false;
            return true;
        }

        /// <summary>Nearest legal spot to <paramref name="p"/> for the cue ball (for dragging it around).</summary>
        public static Vector2 ClampCueBall(Vector2[] pos, ushort down, Vector2 p, bool kitchen)
        {
            if (PoolRails.Bad(p)) p = new Vector2(PoolState.KitchenX, 0f);
            Vector2 q = ClampArea(p, kitchen);
            const float push = PoolDims.BallD + 0.0005f;
            for (int it = 0; it < 8 && pos != null; it++)
            {
                bool moved = false;
                for (int j = 1; j < 16 && j < pos.Length; j++)
                {
                    if ((down & (1 << j)) != 0 || PoolRails.Bad(pos[j])) continue;
                    Vector2 d = q - pos[j];
                    if (d.sqrMagnitude >= push * push) continue;
                    float l = d.magnitude;
                    Vector2 n = l > 1e-6f ? d / l : Vector2.left;
                    q = ClampArea(pos[j] + n * (push + 1e-5f), kitchen);
                    moved = true;
                }
                if (!moved) break;
            }
            if (CanPlaceCueBall(pos, down, q, kitchen)) return q;
            // Boxed in: the nearest free spot on growing rings round the wish (deterministic order).
            Vector2 c = ClampArea(p, kitchen);
            for (int ring = 1; ring <= 120; ring++)
            {
                float rad = ring * 0.005f;
                for (int k = 0; k < 32; k++)
                {
                    float a = k * (Mathf.PI * 2f / 32f);
                    Vector2 s = ClampArea(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad, kitchen);
                    if (CanPlaceCueBall(pos, down, s, kitchen)) return s;
                }
            }
            return q;
        }

        /// <summary>Clamps a cue ball centre onto the open cloth (and behind the head string when <paramref name="kitchen"/>).</summary>
        static Vector2 ClampArea(Vector2 p, bool kitchen)
        {
            // 0.1 mm inside the limits, so the spot stays legal after the 0.05 mm network rounding.
            const float m = 1e-4f;
            float fx = PoolRails.FieldX - m, fy = PoolRails.FieldY - m;
            float maxX = kitchen ? Mathf.Min(PoolState.KitchenX - m, fx) : fx;
            return new Vector2(Mathf.Clamp(p.x, -fx, maxX), Mathf.Clamp(p.y, -fy, fy));
        }
    }
}
