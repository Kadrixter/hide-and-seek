using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Moving parts of <see cref="ExtinguisherModel"/>: lever, gauge dial and needle, pin + seal, nozzle, hose.</summary>
    public static partial class ExtinguisherModel
    {
        static void BuildSmallParts(Style s, MeshSet set)
        {
            float h = s.Head, hy = HeadY(s);
            Vector3 pivot = LeverPivot(s);

            // Lever (lever space = pivot): chrome bar going back and slightly down, black cap on the free end, pivot bolt.
            var lv = new PaMesh(2);
            lv.Box(new Vector3(0f, 0f, -0.075f * h), new Vector3(0.022f * h, 0.006f * h, 0.15f * h), Quaternion.identity, 1);
            lv.Pebble(PaMesh.RoundRect(0.0135f * h, 0.0075f * h, 0.006f * h, 3), PaMesh.SlabRings(-0.035f * h, 0.035f * h, 0.006f, 2),
                new Vector3(0f, 0.002f, -0.115f * h), Vector3.right, Vector3.up, Vector3.forward, 0);
            lv.Lathe(new Vector3(-0.017f * h, 0f, 0f), Vector3.right, new[] { new Vector2(0f, 0f), new Vector2(0.0055f * h, 0f), new Vector2(0.0055f * h, 0.034f * h), new Vector2(0f, 0.034f * h) }, 12, 1);
            set.Lever = lv.ToMesh("ExtLever");

            // Gauge dial + needle (dial space: centre of the face, +Z towards the viewer).
            set.Dial = Disc(s.GaugeR - 0.0013f, 32, "ExtDialFace");
            var nd = new PaMesh(1);
            float L = s.GaugeR * 0.82f;
            nd.Pebble(new List<Vector2> { new(-0.0012f, 0f), new(0f, -0.001f), new(0.0012f, 0f), new(0.0004f, L), new(-0.0004f, L) },
                PaMesh.SlabRings(0f, 0.0008f, 0.0002f, 1), Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, 0);
            nd.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0f, 0f), new Vector2(0.0028f, 0f), new Vector2(0.0028f, 0.0012f), new Vector2(0f, 0.0016f) }, 12, 0);
            set.Needle = nd.ToMesh("ExtNeedle");

            // Safety pin through the handles behind the head, pull ring on its +X end.
            Vector3 ring = RingLocal(s);
            float rr = 0.013f * h;
            var pn = new PaMesh(1);
            Vector3 pinA = new(-0.028f * h, ring.y, ring.z), pinB = new(ring.x - rr, ring.y, ring.z);
            pn.Tube(new[] { pinA, pinB }, 0.0018f * h, 8, 0, true);
            var loop = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                loop.Add(ring + new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0f));
            }
            pn.Tube(loop, 0.0019f * h, 8, 0);
            set.Pin = pn.ToMesh("ExtPin");

            // Seal: thin plastic tie from the ring round the carry handle, with a little round tag.
            var sl = new PaMesh(1);
            Vector3 r0 = ring + Vector3.down * rr;
            sl.Tube(new[] { r0, r0 + new Vector3(-0.008f, -0.01f, -0.006f), new Vector3(0.016f * h, hy - 0.01f * h, -0.06f * h), new Vector3(0f, hy - 0.014f * h, -0.066f * h), new Vector3(-0.014f * h, hy - 0.006f * h, -0.06f * h) }, 0.0011f, 6, 0, true);
            Vector3 tag = r0 + new Vector3(-0.004f, -0.016f, -0.004f);
            sl.Lathe(tag, Vector3.right, new[] { new Vector2(0f, -0.0016f), new Vector2(0.0075f, -0.0016f), new Vector2(0.008f, 0f), new Vector2(0.0075f, 0.0016f), new Vector2(0f, 0.0016f) }, 14, 0);
            set.Seal = sl.ToMesh("ExtSeal");

            // Nozzle (nozzle space: coupling at the origin, spray along +Z).
            var nz = new PaMesh(3);
            nz.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0.0095f, -0.004f), new Vector2(0.0108f, 0f), new Vector2(0.0108f, 0.012f), new Vector2(0.0098f, 0.016f) }, 6, 1);
            if (!s.Horn)
            {
                nz.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0.0098f, 0.016f), new Vector2(0.0112f, 0.018f), new Vector2(0.0118f, 0.03f), new Vector2(0.0102f, 0.085f), new Vector2(0.0094f, 0.088f) }, 18, 0);
                for (int i = 0; i < 4; i++)
                    nz.Lathe(new Vector3(0f, 0f, 0.034f + i * 0.009f), Vector3.forward, new[] { new Vector2(0.0116f - i * 0.0003f, 0f), new Vector2(0.0126f - i * 0.0003f, 0.002f), new Vector2(0.0116f - i * 0.0003f, 0.004f) }, 18, 2);
                nz.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0.0094f, 0.088f), new Vector2(0.0086f, 0.1f), new Vector2(0.0074f, 0.105f), new Vector2(0.0042f, 0.105f) }, 18, 1);
                nz.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0.0042f, 0.105f), new Vector2(0.0038f, 0.09f), new Vector2(0f, 0.09f) }, 12, 2, true);
            }
            else
            {
                nz.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0.0098f, 0.016f), new Vector2(0.0102f, 0.02f), new Vector2(0.0102f, 0.05f) }, 18, 0);
                var outer = new List<Vector2>();
                var inner = new List<Vector2>();
                for (int i = 0; i <= 8; i++)
                {
                    float k = i / 8f;
                    float r = Mathf.Lerp(0.0105f, 0.042f, k * k * 0.6f + k * 0.4f);
                    outer.Add(new Vector2(r, 0.05f + 0.1f * k));
                    inner.Add(new Vector2(r - 0.0022f, 0.052f + 0.098f * k));
                }
                nz.Lathe(Vector3.zero, Vector3.forward, outer, 24, 0);
                nz.Lathe(Vector3.zero, Vector3.forward, inner, 24, 2, true);
                nz.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0.0418f, 0.15f), new Vector2(0.0432f, 0.1495f), new Vector2(0.0432f, 0.1525f), new Vector2(0.0398f, 0.1525f) }, 24, 2);
            }
            set.Nozzle = nz.ToMesh("ExtNozzle");
        }

        static readonly List<Vector3> HosePts = new();

        /// <summary>Hose from the outlet on the valve head to the nozzle coupling (cubic curve, end tangent = nozzle direction).</summary>
        static void BuildHose(Parts p, Vector3 end, Vector3 dir)
        {
            var s = p.Style;
            Vector3 a = HoseOutLocal(s) + Vector3.right * 0.005f * s.Head;
            float len = Vector3.Distance(a, end);
            float slack = Mathf.Clamp(len * 0.45f, 0.04f, 0.22f);
            Vector3 b = a + new Vector3(0.6f, -0.8f, 0f).normalized * slack;
            Vector3 c = end - dir * slack;
            // Short real hoses sag: pull the middle a bit down when there is length to spare.
            Vector3 sag = Vector3.down * Mathf.Clamp(0.42f - len, 0f, 0.25f) * 0.35f;
            HosePts.Clear();
            const int n = 22;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                Vector3 q = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * end;
                q += sag * Mathf.Sin(Mathf.PI * t);
                HosePts.Add(q);
            }
            var m = new PaMesh(1);
            m.Tube(HosePts, s.HoseR, 10, 0);
            // Crimped ferrule at the valve end.
            m.Tube(new[] { a - Vector3.right * 0.002f, a + Vector3.right * 0.016f }, s.HoseR + 0.0016f, 10, 0, true);
            var mesh = p.HoseMesh;
            mesh.Clear();
            mesh.SetVertices(m.V);
            mesh.SetNormals(m.N);
            mesh.SetTriangles(m.T[0], 0);
            mesh.RecalculateBounds();
        }
    }
}
