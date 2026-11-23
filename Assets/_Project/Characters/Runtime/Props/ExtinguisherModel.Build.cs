using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Mesh construction of <see cref="ExtinguisherModel"/> (cached per style).</summary>
    public static partial class ExtinguisherModel
    {
        const int SubPaint = 0, SubBlack = 1, SubChrome = 2, SubBrass = 3, SubRubber = 4, SubDark = 5;
        const float LeverOpen = 16f;

        sealed class MeshSet
        {
            public Mesh Body, Label, Lever, Dial, Needle, Pin, Seal, Nozzle;
            public Material[] BodyMats;
            public Material LabelMat, DialMat, NeedleMat, SealMat;
        }

        static readonly Dictionary<Style, MeshSet> Sets = new();

        static Vector3 LeverPivot(Style s) => new(0f, HeadY(s) + 0.026f * s.Head, 0.012f * s.Head);
        static Vector3 GaugeCentre(Style s) => new(0f, HeadY(s) - 0.004f * s.Head, 0.03f * s.Head);

        static MeshSet MeshesFor(Style s)
        {
            if (Sets.TryGetValue(s, out var set)) return set;
            set = new MeshSet
            {
                BodyMats = new[]
                {
                    PaMesh.Mat("ExtPaint", s.Paint, 0.78f, 0.15f),
                    PaMesh.Mat("ExtBlack", new Color(0.05f, 0.05f, 0.055f), 0.42f),
                    PaMesh.Mat("ExtChrome", new Color(0.8f, 0.8f, 0.82f), 0.85f, 0.92f),
                    PaMesh.Mat("ExtBrass", new Color(0.78f, 0.6f, 0.28f), 0.7f, 0.85f),
                    PaMesh.Mat("ExtRubber", new Color(0.03f, 0.03f, 0.032f), 0.25f),
                    PaMesh.Mat("ExtDark", new Color(0.2f, 0.2f, 0.21f), 0.5f, 0.7f),
                },
                LabelMat = PaMesh.Mat("ExtLabel", Color.white, 0.45f),
                DialMat = PaMesh.Mat("ExtDial", Color.white, 0.88f),
                NeedleMat = PaMesh.Mat("ExtNeedle", new Color(0.85f, 0.06f, 0.04f), 0.5f),
                SealMat = PaMesh.Mat("ExtSeal", new Color(0.95f, 0.78f, 0.08f), 0.55f),
            };
            set.LabelMat.mainTexture = ExtinguisherArt.Label;
            set.DialMat.mainTexture = ExtinguisherArt.Gauge;
            BuildBody(s, set);
            BuildSmallParts(s, set);
            Sets[s] = set;
            return set;
        }

        static void BuildBody(Style s, MeshSet set)
        {
            float R = s.TankR, top = s.TankTop, neck = s.NeckY, h = s.Head;
            var m = new PaMesh(6);
            // Tank: rounded bottom edge just above the foot, straight wall, elliptic shoulder up to the neck.
            var prof = new List<Vector2> { new(0f, 0.022f), new(R * 0.6f, 0.022f), new(R * 0.86f, 0.026f) };
            for (int i = 0; i <= 5; i++)
            {
                float a = Mathf.PI * 0.5f * i / 5f;
                prof.Add(new Vector2(R - 0.02f + 0.02f * Mathf.Sin(a), 0.046f - 0.02f * Mathf.Cos(a)));
            }
            prof.Add(new Vector2(R, top * 0.5f));
            prof.Add(new Vector2(R, top));
            for (int i = 1; i <= 9; i++)
            {
                float a = Mathf.PI * 0.5f * i / 9f;
                prof.Add(new Vector2(Mathf.Max(0.026f * h, R * Mathf.Cos(a)), top + (neck - top) * Mathf.Sin(a)));
            }
            m.Lathe(Vector3.zero, Vector3.up, prof, 40, SubPaint);
            // Seam beads at both ends of the straight wall.
            foreach (float y in new[] { 0.058f, top - 0.004f })
                m.Lathe(new Vector3(0f, y, 0f), Vector3.up, new[] { new Vector2(R, -0.004f), new Vector2(R + 0.0018f, -0.002f), new Vector2(R + 0.0018f, 0.002f), new Vector2(R, 0.004f) }, 40, SubPaint);
            // Black plastic foot.
            m.Lathe(Vector3.zero, Vector3.up, new[]
            {
                new Vector2(0f, 0.002f), new Vector2(R - 0.004f, 0.002f), new Vector2(R + 0.003f, 0.006f), new Vector2(R + 0.004f, 0.03f),
                new Vector2(R + 0.001f, 0.036f), new Vector2(R - 0.006f, 0.034f),
            }, 40, SubBlack);
            // Brass neck with a hex nut.
            m.Lathe(new Vector3(0f, neck - 0.004f, 0f), Vector3.up, new[] { new Vector2(0.028f * h, 0f), new Vector2(0.029f * h, 0.004f), new Vector2(0.029f * h, 0.012f * h), new Vector2(0.024f * h, 0.014f * h) }, 24, SubBrass);
            m.Lathe(new Vector3(0f, neck + 0.01f * h, 0f), Vector3.up, new[] { new Vector2(0.024f * h, 0f), new Vector2(0.024f * h, 0.017f * h) }, 6, SubBrass);

            // Valve head: rounded cast block, the front bulges for the gauge.
            float hy = HeadY(s);
            var outline = PaMesh.RoundRect(0.024f * h, 0.036f * h, 0.011f * h, 4);
            m.Pebble(outline, PaMesh.SlabRings(-0.026f * h, 0.026f * h, 0.006f * h, 3), new Vector3(0f, hy, -0.004f), Vector3.right, Vector3.back, Vector3.up, SubChrome);
            Vector3 g = GaugeCentre(s);
            float gr = s.GaugeR;
            // Gauge housing: chrome cup facing +Z with a rolled rim around the dial.
            m.Lathe(g + Vector3.back * 0.004f * h, Vector3.forward, new[]
            {
                new Vector2(0.008f, 0f), new Vector2(gr * 0.9f, 0.002f), new Vector2(gr + 0.0025f, 0.006f * h), new Vector2(gr + 0.003f, 0.012f * h),
                new Vector2(gr + 0.0015f, 0.0148f * h), new Vector2(gr - 0.0008f, 0.0152f * h), new Vector2(gr - 0.0012f, 0.0142f * h),
            }, 28, SubChrome);
            // Hose outlet stub on the +X side.
            Vector3 hout = HoseOutLocal(s);
            m.Lathe(hout - Vector3.right * 0.012f * h, Vector3.right, new[] { new Vector2(0.007f * h, 0f), new Vector2(0.008f * h, 0.006f), new Vector2(0.0095f * h, 0.008f), new Vector2(0.0095f * h, 0.016f * h), new Vector2(0.008f * h, 0.017f * h) }, 6, SubBrass);

            // Carry handle: steel bar out of the back of the head, black grip on the free end.
            Vector3 hb = new(0f, hy + 0.004f * h, -0.03f * h), he = new(0f, hy - 0.004f * h, -0.15f * h);
            Quaternion hr = Quaternion.LookRotation(he - hb, Vector3.up);
            float hl = Vector3.Distance(hb, he);
            m.Box((hb + he) * 0.5f, new Vector3(0.02f * h, 0.006f * h, hl), hr, SubChrome);
            Vector3 gripC = Vector3.Lerp(hb, he, 0.66f);
            var gripOutline = PaMesh.RoundRect(0.0125f * h, 0.008f * h, 0.006f * h, 3);
            m.Pebble(gripOutline, PaMesh.SlabRings(-hl * 0.34f, hl * 0.34f, 0.005f, 2), gripC + Vector3.down * 0.002f, hr * Vector3.right, hr * Vector3.up, hr * Vector3.forward, SubBlack);
            for (int i = 0; i < 5; i++)
                m.Box(gripC + hr * new Vector3(0f, -0.0095f * h, (i - 2) * 0.014f), new Vector3(0.02f * h, 0.003f, 0.006f), hr, SubBlack);

            // Hose clip on the tank side.
            Vector3 clip = ClipLocal(s);
            m.Pebble(PaMesh.RoundRect(0.012f, 0.03f, 0.005f, 2), PaMesh.SlabRings(-0.006f, 0.004f, 0.002f, 1), new Vector3(R + 0.002f, clip.y, 0f), Vector3.forward, Vector3.up, Vector3.right, SubBlack);
            m.Lathe(clip + new Vector3(0.006f, 0f, 0f), Vector3.up, new[] { new Vector2(0.013f, -0.01f), new Vector2(0.013f, 0.01f) }, 14, SubBlack, false, -110f, 200f);
            set.Body = m.ToMesh("ExtBody");
            set.Label = LabelMesh(R + 0.0009f, 0.1f, 0.1f + (top - 0.13f), 70f);
        }

        /// <summary>Wrap-around label: partial cylinder centred on +Z with UVs (u to the viewer's right).</summary>
        static Mesh LabelMesh(float r, float y0, float y1, float halfDeg)
        {
            const int cols = 24;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i <= cols; i++)
            {
                float u = i / (float)cols;
                float a = Mathf.Lerp(halfDeg, -halfDeg, u) * Mathf.Deg2Rad;   // +X (viewer's left) first
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v.Add(d * r + Vector3.up * y0); n.Add(d); uv.Add(new Vector2(u, 0f));
                v.Add(d * r + Vector3.up * y1); n.Add(d); uv.Add(new Vector2(u, 1f));
            }
            for (int i = 0; i < cols; i++)
            {
                int a = i * 2;
                t.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
            }
            var m = new Mesh { name = "ExtLabel" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            FixWinding(m);
            return m;
        }

        /// <summary>Flips triangles whose winding disagrees with the vertex normals (front faces outwards).</summary>
        static void FixWinding(Mesh m)
        {
            var v = m.vertices; var n = m.normals; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(fn, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            m.triangles = t;
            m.RecalculateBounds();
        }

        static Mesh Disc(float r, int seg, string name)
        {
            var v = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { new(0.5f, 0.5f) };
            var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float x = Mathf.Cos(a), y = Mathf.Sin(a);
                v.Add(new Vector3(x * r, y * r, 0f));
                uv.Add(new Vector2(0.5f - x * 0.5f, 0.5f + y * 0.5f));   // seen from +Z the texture is not mirrored
                if (i > 0) t.AddRange(new[] { 0, i, i + 1 });
            }
            var m = new Mesh { name = name };
            m.SetVertices(v);
            var n = new List<Vector3>();
            foreach (var _ in v) n.Add(Vector3.forward);
            m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            FixWinding(m);
            return m;
        }
    }
}
