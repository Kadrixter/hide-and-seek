using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Collision geometry of the table for the ball physics (table space 2D): the 6 cushion noses, the 12 jaw facings,
    /// their 24 end points (jaw tips and throats) and the 6 pocket holes. Built once from <see cref="PoolTableGeom"/>.
    /// A ball touches a line when its centre is <see cref="R"/> away from it (the nose line is the edge of the field).
    /// </summary>
    internal static class PoolRails
    {
        public const float R = PoolDims.BallR;
        public const float HalfL = PoolTableGeom.HalfL, HalfW = PoolTableGeom.HalfW;
        public const float BedX = PoolTableGeom.BedX, BedZ = PoolTableGeom.BedZ;
        /// <summary>Limits of a ball centre on the open cloth (touching the cushion noses).</summary>
        public const float FieldX = HalfL - R, FieldY = HalfW - R;

        struct Seg
        {
            public Vector2 A, N, D;
            public float L;
            public bool Jaw;
        }

        static readonly Seg[] segs;
        static readonly Vector2[] verts;
        /// <summary>Hole centres, radii and the zone round each pocket where a ball may be past the nose lines (in the jaws).</summary>
        public static readonly Vector2[] Hole = new Vector2[6];
        public static readonly float[] HoleR = new float[6], Zone = new float[6];

        static PoolRails()
        {
            var cs = PoolTableGeom.Cushions();
            segs = new Seg[cs.Length * 3];
            verts = new Vector2[cs.Length * 4];
            float cw = PoolTableGeom.CushionW;
            for (int k = 0; k < cs.Length; k++)
            {
                var (a, b, o, ka, kb) = cs[k];
                segs[k * 3] = Make(a, b, -o, false);
                Vector2 ua = (a - b).normalized, ub = (b - a).normalized;
                Vector2 ta = a + o * cw + ua * (ka * cw), tb = b + o * cw + ub * (kb * cw);
                segs[k * 3 + 1] = Make(a, ta, (ua - o * ka).normalized, true);
                segs[k * 3 + 2] = Make(b, tb, (ub - o * kb).normalized, true);
                verts[k * 4] = a;
                verts[k * 4 + 1] = b;
                verts[k * 4 + 2] = ta;
                verts[k * 4 + 3] = tb;
            }
            for (int i = 0; i < 6; i++)
            {
                Hole[i] = PoolTableGeom.Centre(i);
                HoleR[i] = PoolTableGeom.HoleR(i);
                float far = HoleR[i];
                for (int k = 0; k < cs.Length; k++)
                    foreach (var tip in new[] { cs[k].a, cs[k].b })
                    {
                        float d = (tip - Hole[i]).magnitude;
                        if (d < 0.25f && d > far) far = d;
                    }
                Zone[i] = far + R + 0.01f;
            }
        }

        static Seg Make(Vector2 a, Vector2 b, Vector2 n, bool jaw)
        {
            Vector2 d = b - a;
            float l = d.magnitude;
            return new Seg { A = a, N = n, D = l > 1e-9f ? d / l : Vector2.right, L = l, Jaw = jaw };
        }

        /// <summary>
        /// Time (0..tmax) when two circles get <paramref name="dist"/> apart: <paramref name="dp"/> = position of B minus A,
        /// <paramref name="dv"/> = velocity of B minus A. -1 = no contact (separating, parallel or too late);
        /// 0 = already touching / overlapping and approaching.
        /// </summary>
        public static float Toi(Vector2 dp, Vector2 dv, float dist, float tmax)
        {
            float b = dp.x * dv.x + dp.y * dv.y;
            if (b >= 0f) return -1f;
            float a = dv.x * dv.x + dv.y * dv.y;
            if (a < 1e-12f) return -1f;
            float c = dp.x * dp.x + dp.y * dp.y - dist * dist;
            if (c <= 0f) return 0f;
            float disc = b * b - a * c;
            if (disc < 0f) return -1f;
            float t = (-b - Mathf.Sqrt(disc)) / a;
            if (t < 0f) t = 0f;
            return t <= tmax ? t : -1f;
        }

        /// <summary>Time a ball centre (p, v) gets R from the segment's line, inside the segment (ends are the vertices).</summary>
        static float SegToi(in Seg s, Vector2 p, Vector2 v, float tmax)
        {
            float vn = v.x * s.N.x + v.y * s.N.y;
            if (vn >= -1e-7f) return -1f;
            Vector2 rel = p - s.A;
            float dist = rel.x * s.N.x + rel.y * s.N.y;
            if (dist < 0f) return -1f; // behind the line: not this segment
            float t = (dist - R) / -vn;
            if (t < 0f) t = 0f;
            if (t > tmax) return -1f;
            Vector2 c = rel + v * t;
            float along = c.x * s.D.x + c.y * s.D.y;
            return along < 0f || along > s.L ? -1f : t;
        }

        /// <summary>May a ball at <paramref name="p"/> moving <paramref name="v"/> reach any cushion within <paramref name="tmax"/>?</summary>
        public static bool NearRail(Vector2 p, Vector2 v, float tmax)
        {
            float m = R + 0.003f + (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * tmax;
            return Mathf.Abs(p.x) > HalfL - m || Mathf.Abs(p.y) > HalfW - m;
        }

        /// <summary>
        /// Earliest cushion / jaw contact of a ball (p, v) not later than <paramref name="best"/> (lowered to it when found).
        /// <paramref name="n"/> = contact normal towards the ball, <paramref name="jaw"/> = a facing or a tip, not a nose.
        /// </summary>
        public static bool Hit(Vector2 p, Vector2 v, ref float best, out Vector2 n, out bool jaw)
        {
            bool found = false;
            n = Vector2.zero;
            jaw = false;
            for (int k = 0; k < segs.Length; k++)
            {
                float t = SegToi(segs[k], p, v, best);
                if (t < 0f || (found && t >= best)) continue;
                best = t;
                found = true;
                n = segs[k].N;
                jaw = segs[k].Jaw;
            }
            for (int k = 0; k < verts.Length; k++)
            {
                float t = Toi(p - verts[k], v, R, best);
                if (t < 0f || (found && t >= best)) continue;
                Vector2 d = p + v * t - verts[k];
                float l = d.magnitude;
                if (l < 1e-7f) continue;
                best = t;
                found = true;
                n = d / l;
                jaw = true;
            }
            return found;
        }

        /// <summary>Pocket whose hole contains the ball centre, else -1.</summary>
        public static int InHole(Vector2 p)
        {
            for (int i = 0; i < 6; i++)
                if ((p - Hole[i]).sqrMagnitude < HoleR[i] * HoleR[i]) return i;
            return -1;
        }

        /// <summary>Nearest pocket (by hole centre) and the distance to it.</summary>
        public static int NearestHole(Vector2 p, out float dist)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                float d = (p - Hole[i]).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            dist = Mathf.Sqrt(bd);
            return best;
        }

        /// <summary>Is a ball centre on the open cloth (between the cushion noses)?</summary>
        public static bool OnField(Vector2 p) => Mathf.Abs(p.x) <= FieldX && Mathf.Abs(p.y) <= FieldY;

        public static bool Bad(Vector2 a) => float.IsNaN(a.x) || float.IsNaN(a.y) || float.IsInfinity(a.x) || float.IsInfinity(a.y);
    }
}
