using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Mesh of one ceiling CCTV camera in the camera's own frame (lens centre at the origin looking along +z):
    /// bullet housing with a sun hood, dark front bezel with a ring of IR emitters around the glass lens, back cap
    /// with a cable gland, ball-joint bracket and a pole to the ceiling plate, and a cable looping from the back of
    /// the housing up along the pole. One mesh, four sub-meshes (housing, dark parts, glass, LED) - one draw call
    /// set per camera instead of a pile of primitives.
    /// </summary>
    public static class CctvModel
    {
        public const int SubHousing = 0, SubDark = 1, SubGlass = 2, SubLed = 3;
        /// <summary>LED position in the camera frame (for the halo).</summary>
        public static readonly Vector3 LedLocal = new Vector3(0.036f, 0.032f, -0.008f);

        private sealed class B
        {
            public readonly List<Vector3> V = new();
            public readonly List<Vector3> N = new();
            public readonly List<int>[] T = { new(), new(), new(), new() };

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, int sub)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(na); N.Add(nb); N.Add(nc); N.Add(nd);
                Vector3 fn = Vector3.Cross(b - a, c - a);
                bool flip = Vector3.Dot(fn, na + nb + nc + nd) < 0f;
                var t = T[sub];
                if (flip) { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
                else { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            }

            public void Box(Vector3 c, Vector3 size, Quaternion r, int sub)
            {
                Vector3 h = size * 0.5f;
                Vector3[] ax = { r * Vector3.right, r * Vector3.up, r * Vector3.forward };
                float[] e = { h.x, h.y, h.z };
                for (int a = 0; a < 3; a++)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 n = ax[a] * s;
                    Vector3 u = ax[(a + 1) % 3] * e[(a + 1) % 3], v = ax[(a + 2) % 3] * e[(a + 2) % 3];
                    Vector3 f = c + n * e[a];
                    Quad(f - u - v, f + u - v, f + u + v, f - u + v, n, n, n, n, sub);
                }
            }

            /// <summary>Surface of revolution around axis (profile x = radius, y = offset along the axis).</summary>
            public void Lathe(Vector3 c, Vector3 axis, Vector2[] prof, int sides, int sub, float arc0 = 0f, float arc1 = 360f)
            {
                axis.Normalize();
                Vector3 e1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 e2 = Vector3.Cross(axis, e1);
                for (int s = 0; s < sides; s++)
                {
                    float a0 = Mathf.Lerp(arc0, arc1, s / (float)sides) * Mathf.Deg2Rad, a1 = Mathf.Lerp(arc0, arc1, (s + 1) / (float)sides) * Mathf.Deg2Rad;
                    Vector3 d0 = Mathf.Cos(a0) * e1 + Mathf.Sin(a0) * e2, d1 = Mathf.Cos(a1) * e1 + Mathf.Sin(a1) * e2;
                    for (int k = 0; k < prof.Length - 1; k++)
                    {
                        Vector2 p = prof[k], q = prof[k + 1], t = q - p;
                        Vector2 n2 = new Vector2(t.y, -t.x).normalized;
                        Vector3 n0 = d0 * n2.x + axis * n2.y, n1 = d1 * n2.x + axis * n2.y;
                        Quad(c + d0 * p.x + axis * p.y, c + d1 * p.x + axis * p.y, c + d1 * q.x + axis * q.y, c + d0 * q.x + axis * q.y, n0, n1, n1, n0, sub);
                    }
                }
            }

            /// <summary>Tube through points (cable).</summary>
            public void Tube(IList<Vector3> pts, float r, int sides, int sub)
            {
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector3 d = pts[i + 1] - pts[i];
                    if (d.sqrMagnitude < 1e-8f) continue;
                    Lathe(pts[i], d, new[] { new Vector2(r, 0f), new Vector2(r, d.magnitude) }, sides, sub);
                }
            }
        }

        /// <summary>
        /// Builds the mesh for a camera at <paramref name="pos"/> with rotation <paramref name="rot"/> hanging from a
        /// ceiling at height <paramref name="ceilingY"/> (all world space; the mesh is in the camera frame).
        /// </summary>
        public static Mesh Build(Vector3 pos, Quaternion rot, float ceilingY)
        {
            var b = new B();
            var inv = Quaternion.Inverse(rot);
            Vector3 L(Vector3 world) => inv * (world - pos);
            Vector3 fwd = Vector3.forward;

            // bullet housing (rounded back), sun hood over the top half sticking out in front of the lens
            b.Lathe(new Vector3(0f, 0f, -0.33f), fwd, new[]
            {
                new Vector2(0f, -0.004f), new Vector2(0.03f, -0.002f), new Vector2(0.046f, 0.008f), new Vector2(0.053f, 0.03f),
                new Vector2(0.054f, 0.3f), new Vector2(0.05f, 0.31f),
            }, 16, SubHousing);
            b.Lathe(new Vector3(0f, 0f, -0.28f), fwd, new[] { new Vector2(0.066f, 0f), new Vector2(0.066f, 0.3f), new Vector2(0.06f, 0.302f), new Vector2(0.06f, 0f) }, 10, SubHousing, 170f, 370f);
            // dark bezel ring and the recessed front with the glass and IR emitters
            b.Lathe(new Vector3(0f, 0f, -0.03f), fwd, new[] { new Vector2(0.05f, 0f), new Vector2(0.05f, 0.012f), new Vector2(0.042f, 0.016f), new Vector2(0.036f, 0.008f) }, 16, SubDark);
            b.Lathe(new Vector3(0f, 0f, -0.022f), fwd, new[] { new Vector2(0.036f, 0f), new Vector2(0.024f, 0.001f), new Vector2(0.023f, 0.006f) }, 16, SubDark);
            b.Lathe(new Vector3(0f, 0f, -0.016f), fwd, new[] { new Vector2(0.023f, 0f), new Vector2(0.012f, 0.004f), new Vector2(0f, 0.005f) }, 16, SubGlass);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f + 0.2f;
                b.Box(new Vector3(Mathf.Cos(a) * 0.03f, Mathf.Sin(a) * 0.03f, -0.0175f), new Vector3(0.006f, 0.006f, 0.003f), Quaternion.identity, SubGlass);
            }
            // back cap with the cable gland
            b.Lathe(new Vector3(0f, 0f, -0.338f), -fwd, new[] { new Vector2(0.012f, -0.004f), new Vector2(0.012f, 0.03f), new Vector2(0f, 0.032f) }, 8, SubDark);
            // status LED under the hood edge, facing forward
            b.Box(LedLocal, new Vector3(0.012f, 0.012f, 0.006f), Quaternion.identity, SubLed);

            // bracket: saddle on top of the housing, ball joint, pole to the ceiling, ceiling plate
            Vector3 saddle = new Vector3(0f, 0.062f, -0.19f);
            b.Box(saddle, new Vector3(0.05f, 0.016f, 0.1f), Quaternion.identity, SubDark);
            Vector3 jointW = rot * (saddle + new Vector3(0f, 0.035f, 0f)) + pos;
            Vector3 joint = L(jointW);
            b.Lathe(joint, Vector3.up, new[] { new Vector2(0f, -0.026f), new Vector2(0.02f, -0.018f), new Vector2(0.026f, 0f), new Vector2(0.02f, 0.018f), new Vector2(0f, 0.026f) }, 10, SubDark);
            b.Tube(new[] { saddle, joint }, 0.012f, 8, SubDark);
            Vector3 topW = new Vector3(jointW.x, ceilingY, jointW.z);
            b.Tube(new[] { joint, L(topW - Vector3.up * 0.012f) }, 0.016f, 10, SubHousing);
            b.Lathe(L(topW - Vector3.up * 0.014f), inv * Vector3.up, new[] { new Vector2(0.065f, 0f), new Vector2(0.065f, 0.012f), new Vector2(0.06f, 0.014f), new Vector2(0f, 0.014f) }, 14, SubHousing);
            b.Lathe(L(topW - Vector3.up * 0.015f), -(inv * Vector3.up), new[] { new Vector2(0.065f, 0f), new Vector2(0f, 0f) }, 14, SubHousing);

            // cable: out of the gland, a loose loop down and back, then up beside the pole into the ceiling
            Vector3 gland = new Vector3(0f, 0f, -0.37f);
            Vector3 c0 = L(pos + rot * gland), c3 = L(topW + rot * new Vector3(0.03f, 0f, -0.05f) - Vector3.up * 0.02f);
            Vector3 c1 = L(pos + rot * new Vector3(0f, -0.05f, -0.46f)), c2 = L(new Vector3(topW.x, (topW.y + jointW.y) * 0.5f - 0.08f, topW.z) + rot * new Vector3(0.03f, 0f, -0.12f));
            var cable = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f, u = 1f - t;
                cable.Add(u * u * u * c0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * c3);
            }
            b.Tube(cable, 0.0055f, 6, SubDark);

            var mesh = new Mesh { name = "CctvCamera" };
            mesh.SetVertices(b.V);
            mesh.SetNormals(b.N);
            mesh.subMeshCount = 4;
            for (int s = 0; s < 4; s++) mesh.SetTriangles(b.T[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Materials for the four sub-meshes (shared by all cameras; the LED glows through a property block).</summary>
        public static Material[] Materials()
        {
            var sh = Shader.Find("Standard");
            Material M(string n, Color c, float smooth, float metal = 0f)
            {
                var m = new Material(sh) { name = n, color = c };
                m.SetFloat("_Glossiness", smooth);
                m.SetFloat("_Metallic", metal);
                return m;
            }
            var housing = M("CctvHousing", new Color(0.74f, 0.74f, 0.72f), 0.42f);
            var dark = M("CctvDark", new Color(0.07f, 0.07f, 0.075f), 0.35f);
            var glass = M("CctvGlass", new Color(0.02f, 0.025f, 0.04f), 0.96f, 0.3f);
            var led = M("CctvLed", new Color(0.25f, 0.01f, 0.01f), 0.9f);
            led.EnableKeyword("_EMISSION");
            led.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            led.SetColor("_EmissionColor", Color.black);
            return new[] { housing, dark, glass, led };
        }

        public static void Setup(MeshRenderer r)
        {
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.shadowCastingMode = ShadowCastingMode.On;
        }
    }
}
