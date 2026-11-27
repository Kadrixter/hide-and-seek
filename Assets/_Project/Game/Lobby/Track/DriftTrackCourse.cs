using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Paint and props of the paddock (the open asphalt by the gate, east of the circuit's start straight - the circuit
    /// itself: <see cref="DriftTrackCircuit"/> / <see cref="DriftTrackRoad"/>): five numbered parking boxes for the
    /// karts by the gate, a yellow donut circle with "DRIFT" and a cone slalom for free practice, and dark rubber left
    /// by earlier drifts round the donut (soft decals). Paint lies 4 mm over the asphalt, rubber 5 mm, the karts' fresh
    /// skid marks 6 mm.
    /// </summary>
    public static class DriftTrackCourse
    {
        /// <summary>Middle of the pad (the floodlights aim round it).</summary>
        public static readonly Vector3 Center = DriftTrackLayout.PadCenter + new Vector3(-3f, 0f, 0f);
        /// <summary>The paddock's donut circle and the slalom line (clear of the karts' run west from their boxes).</summary>
        public static readonly Vector3 Donut = new(-62f, 0f, -58f), SlalomStart = new(-34f, 0f, 34f);
        public const float Paint = 0.004f;

        public static void Build(Transform parent)
        {
            var root = new GameObject("Course").transform;
            root.SetParent(parent, false);
            var white = new ProcMesh();
            var yellow = new ProcMesh();

            Ribbon(yellow, Circle(Donut, 14f), 0.3f, 0f);
            Ribbon(yellow, Circle(Donut, 3f), 0.2f, 0f);
            DriftTrackProps.Letters(root, "DRIFT", Donut + new Vector3(0f, Paint + 0.002f, -8.5f), Quaternion.Euler(90f, 0f, 0f), 0.45f, new Color(0.95f, 0.72f, 0.08f), new Vector2(14f, 4f));
            for (int i = 0; i < 8; i++) DriftTrackProps.Cone(root, SlalomStart + new Vector3(-i * 7f, 0f, 0f));
            Ribbon(white, new List<Vector3> { SlalomStart + new Vector3(4f, 0f, -3f), SlalomStart + new Vector3(-53f, 0f, -3f) }, 0.15f, 2f);
            Ribbon(white, new List<Vector3> { SlalomStart + new Vector3(4f, 0f, 3f), SlalomStart + new Vector3(-53f, 0f, 3f) }, 0.15f, 2f);

            // Parking boxes for the karts.
            for (int i = 0; i < DriftTrackLayout.KartCount; i++)
            {
                var s = DriftTrackLayout.KartSlot(i);
                float hw = DriftTrackLayout.KartSlotWidth(i) * 0.5f;
                foreach (float dz in new[] { -hw, hw }) white.At(s + new Vector3(0f, Paint, dz)).Box(Vector3.zero, new Vector3(2.4f, 0.002f, 0.1f));
                white.At(s + new Vector3(1.15f, Paint, 0f)).Box(Vector3.zero, new Vector3(0.1f, 0.002f, hw * 2f + 0.1f));
            }

            var paintW = TrackMats.Get("TrackLineWhite", new Color(0.86f, 0.86f, 0.84f), 0.25f);
            TrackMats.Piece(root, "LinesWhite", white.ToMesh("TrackLinesWhite"), paintW, false);
            TrackMats.Piece(root, "LinesYellow", yellow.ToMesh("TrackLinesYellow"), TrackMats.Get("TrackLineYellow", new Color(0.95f, 0.72f, 0.08f), 0.25f), false);
            Rubber(root, Donut);
        }

        /// <summary>Dark, soft rubber patches round the donut circle.</summary>
        private static void Rubber(Transform root, Vector3 donut)
        {
            var m = new ProcMesh();
            var rng = new System.Random(311);
            var spots = new List<(Vector3 p, float r)>();
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
