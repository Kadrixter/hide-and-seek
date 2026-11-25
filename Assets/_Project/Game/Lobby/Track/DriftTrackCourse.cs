using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The drift course painted on the pad: a 22 m wide loop (two 170 m straights for ~100 km/h, wide 42 m corners) (rounded rectangle, white edge lines, dashed centre line),
    /// a checkered start line, red / white kerbs round the four inner corners with tyre stacks and cones at the
    /// apexes, a yellow donut circle with "DRIFT" in the infield, a cone slalom, five numbered parking boxes for the
    /// karts by the gate, and dark rubber left by earlier drifts in the corners (soft decals). Paint lies 4 mm over
    /// the asphalt, rubber 5 mm, the karts' fresh skid marks 6 mm.
    /// </summary>
    public static class DriftTrackCourse
    {
        public static readonly Vector3 Center = DriftTrackLayout.PadCenter + new Vector3(-3f, 0f, 0f);
        public static readonly Vector2 OuterHalf = new(128f, 82f), InnerHalf = new(106f, 60f);
        public const float OuterR = 42f, InnerR = 22f, Paint = 0.004f;

        public static void Build(Transform parent)
        {
            var root = new GameObject("Course").transform;
            root.SetParent(parent, false);
            var white = new ProcMesh();
            var yellow = new ProcMesh();
            var black = new ProcMesh();

            Ribbon(white, Loop(OuterHalf, OuterR), 0.25f, 0f);
            Ribbon(white, Loop(InnerHalf, InnerR), 0.25f, 0f);
            Ribbon(white, Loop((OuterHalf + InnerHalf) * 0.5f, (OuterR + InnerR) * 0.5f), 0.15f, 4f); // dashed centre line

            // Checkered start line across the south straight.
            float z0 = Center.z - OuterHalf.y, z1 = Center.z - InnerHalf.y;
            for (int row = 0; row < 3; row++)
                for (float z = z0; z < z1 - 0.01f; z += 0.5f)
                {
                    int cell = Mathf.RoundToInt((z - z0) / 0.5f) + row;
                    ((cell & 1) == 0 ? white : black).At(new Vector3(Center.x - 0.5f + row * 0.5f, Paint, z + 0.25f)).Box(Vector3.zero, new Vector3(0.5f, 0.002f, 0.5f));
                }

            // Donut circle and slalom cones in the infield.
            var donut = Center + new Vector3(-40f, 0f, 0f);
            Ribbon(yellow, Circle(donut, 14f), 0.3f, 0f);
            Ribbon(yellow, Circle(donut, 3f), 0.2f, 0f);
            DriftTrackProps.Letters(root, "DRIFT", donut + new Vector3(0f, Paint + 0.002f, -8.5f), Quaternion.Euler(90f, 0f, 0f), 0.45f, new Color(0.95f, 0.72f, 0.08f), new Vector2(14f, 4f));
            for (int i = 0; i < 9; i++) DriftTrackProps.Cone(root, Center + new Vector3(10f + i * 8f, 0f, 0f));

            // Kerbs, tyre stacks and apex cones round the inner corners.
            var red = new ProcMesh();
            var kerbWhite = new ProcMesh();
            var tyres = new ProcMesh();
            foreach (var (sx, sz) in new[] { (1f, 1f), (-1f, 1f), (-1f, -1f), (1f, -1f) })
            {
                var c = Center + new Vector3(sx * (InnerHalf.x - InnerR), 0f, sz * (InnerHalf.y - InnerR));
                float a0 = Mathf.Atan2(0f, sx);
                float a1 = a0 + Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, Mathf.Atan2(sz, 0f) * Mathf.Rad2Deg) * Mathf.Deg2Rad; // the quarter towards the corner
                int n = Mathf.CeilToInt(InnerR * Mathf.PI * 0.5f / 0.9f);
                for (int i = 0; i < n; i++)
                {
                    float a = Mathf.Lerp(a0, a1, (i + 0.5f) / n);
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var p = c + dir * (InnerR + 0.32f);
                    ((i & 1) == 0 ? red : kerbWhite).At(p + Vector3.up * 0.02f, Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir))).RoundBox(new Vector3(0.5f, 0.05f, InnerR * Mathf.Abs(a1 - a0) / n + 0.02f), 0.012f, 2);
                }
                var apex = c + new Vector3(sx, 0f, sz).normalized * (InnerR - 0.9f);
                for (int h = 0; h < 3; h++) DriftTrackProps.Tyre(tyres, apex + Vector3.up * (0.12f + h * 0.22f));
                var col = new GameObject("TyreStackCol").AddComponent<CapsuleCollider>();
                col.transform.SetParent(root, false);
                col.center = apex + Vector3.up * 0.35f;
                col.radius = 0.33f;
                col.height = 0.8f;
                DriftTrackProps.Cone(root, c + new Vector3(sx, 0f, sz).normalized * (InnerR + 0.9f));
            }
            TrackMats.Piece(root, "Kerbs", red.ToMesh("TrackKerbRed"), TrackMats.Get("TrackPaintRed", new Color(0.75f, 0.06f, 0.05f), 0.35f), false);
            TrackMats.Piece(root, "KerbsWhite", kerbWhite.ToMesh("TrackKerbWhite"), TrackMats.Get("TrackPaintWhite", new Color(0.9f, 0.9f, 0.88f), 0.35f), false);
            TrackMats.Piece(root, "TyreStacks", tyres.ToMesh("TrackTyreStacks"), DriftTrackProps.Rubber);

            // Parking boxes for the karts.
            for (int i = 0; i < DriftTrackLayout.KartCount; i++)
            {
                var s = DriftTrackLayout.KartSlot(i);
                foreach (float dz in new[] { -1.15f, 1.15f }) white.At(s + new Vector3(0f, Paint, dz)).Box(Vector3.zero, new Vector3(2.4f, 0.002f, 0.1f));
                white.At(s + new Vector3(1.15f, Paint, 0f)).Box(Vector3.zero, new Vector3(0.1f, 0.002f, 2.4f));
            }

            var paintW = TrackMats.Get("TrackLineWhite", new Color(0.86f, 0.86f, 0.84f), 0.25f);
            TrackMats.Piece(root, "LinesWhite", white.ToMesh("TrackLinesWhite"), paintW, false);
            TrackMats.Piece(root, "LinesYellow", yellow.ToMesh("TrackLinesYellow"), TrackMats.Get("TrackLineYellow", new Color(0.95f, 0.72f, 0.08f), 0.25f), false);
            TrackMats.Piece(root, "Checker", black.ToMesh("TrackChecker"), TrackMats.Get("TrackLineBlack", new Color(0.03f, 0.03f, 0.03f), 0.2f), false);
            Rubber(root, donut);
        }

        /// <summary>Dark, soft rubber patches in the corners and round the donut circle.</summary>
        private static void Rubber(Transform root, Vector3 donut)
        {
            var m = new ProcMesh();
            var rng = new System.Random(311);
            var spots = new List<(Vector3 p, float r)>();
            foreach (var (sx, sz) in new[] { (1f, 1f), (-1f, 1f), (-1f, -1f), (1f, -1f) })
            {
                var c = Center + new Vector3(sx * (InnerHalf.x - InnerR), 0f, sz * (InnerHalf.y - InnerR));
                for (int i = 0; i < 16; i++)
                {
                    float a = Mathf.Atan2(sz, sx) + (float)(rng.NextDouble() - 0.5) * 1.6f;
                    spots.Add((c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (InnerR + 3f + (float)rng.NextDouble() * 14f), 3f + (float)rng.NextDouble() * 5f));
                }
            }
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                spots.Add((donut + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 13f, 3f + (float)rng.NextDouble() * 2f));
            }
            foreach (var (p, r) in spots)
                m.At(p + Vector3.up * 0.005f, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f)).Box(Vector3.zero, new Vector3(r * 2f, 0f, r * (1.2f + (float)rng.NextDouble())));
            var mat = new Material(Shader.Find("Sprites/Default")) { name = "TrackRubberDecal", mainTexture = FxUtil.Soft.mainTexture, color = new Color(0.02f, 0.02f, 0.02f, 0.26f), renderQueue = 2980 };
            var mesh = m.ToMesh("TrackRubber");
            // Box UVs are in metres: remap every top face to 0..1 for the soft dot.
            var uv = mesh.uv;
            for (int i = 0; i + 3 < uv.Length; i += 4) { uv[i] = new Vector2(0f, 0f); uv[i + 1] = new Vector2(1f, 0f); uv[i + 2] = new Vector2(1f, 1f); uv[i + 3] = new Vector2(0f, 1f); }
            mesh.uv = uv;
            TrackMats.Piece(root, "RubberPatches", mesh, mat, false);
        }

        /// <summary>Rounded rectangle round <see cref="Center"/> (half sizes, corner radius), points every ~0.5 m.</summary>
        private static List<Vector3> Loop(Vector2 half, float r)
        {
            var pts = new List<Vector3>();
            var corners = new[] { (1f, 1f, 0f), (-1f, 1f, 90f), (-1f, -1f, 180f), (1f, -1f, 270f) };
            foreach (var (sx, sz, start) in corners)
            {
                var c = Center + new Vector3(sx * (half.x - r), 0f, sz * (half.y - r));
                int n = Mathf.Max(4, Mathf.CeilToInt(r * Mathf.PI * 0.5f / 0.5f));
                for (int i = 0; i <= n; i++)
                {
                    float a = (start + 90f * i / n) * Mathf.Deg2Rad;
                    pts.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                }
            }
            pts.Add(pts[0]);
            return pts;
        }

        private static List<Vector3> Circle(Vector3 c, float r)
        {
            var pts = new List<Vector3>();
            int n = Mathf.Max(16, Mathf.CeilToInt(r * Mathf.PI * 2f / 0.4f));
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                pts.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            }
            return pts;
        }

        /// <summary>Flat paint strip along <paramref name="pts"/> (<paramref name="dash"/> > 0 = dashed, that long).</summary>
        private static void Ribbon(ProcMesh m, List<Vector3> pts, float width, float dash)
        {
            float run = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1], d = b - a;
                float len = d.magnitude;
                if (len < 1e-4f) continue;
                bool on = dash <= 0f || Mathf.Repeat(run, dash * 2f) < dash;
                run += len;
                if (!on) continue;
                m.At((a + b) * 0.5f + Vector3.up * Paint, Quaternion.LookRotation(d / len)).Box(Vector3.zero, new Vector3(width, 0.002f, len + 0.02f));
            }
        }
    }
}
