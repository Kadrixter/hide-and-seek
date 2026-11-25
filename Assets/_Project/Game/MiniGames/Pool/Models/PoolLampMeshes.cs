using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Meshes of the pool-table lamp: turned profiles of the shades (green cased-glass dome, black metal cone), bulb,
    /// socket, ceiling canopy and finials (lathe around +Y, see <see cref="BoardMeshes.Lathe"/>), plus swept tubes
    /// (cable, chain links) and a whole chain merged into one mesh. Shade space: the rim at y = 0, the neck up.
    /// </summary>
    internal static class PoolLampMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>Height of the shade from the rim to the top of the crown (style 0 / 1).</summary>
        public static float ShadeHeight(int style) => style == 1 ? 0.17f : 0.205f;

        // ================================================================ shades

        /// <summary>Outer skin of the shade (green glass / black metal), from the rim up to the neck.</summary>
        public static Mesh ShadeOuter(int style)
        {
            var p = new List<Vector2>();
            if (style == 1)
            {
                // Straight cone with a slight bell at the bottom, rounded shoulder into the neck.
                p.Add(new Vector2(0.188f, 0.004f));
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    float r = Mathf.Lerp(0.188f, 0.062f, t) - Mathf.Sin(t * Mathf.PI) * 0.006f;
                    p.Add(new Vector2(r, 0.006f + t * 0.128f));
                }
                BoardMeshes.Arc(p, 0.046f, 0.134f, 0.016f, -15f, 85f, 6);
                p.Add(new Vector2(0.036f, 0.152f));
            }
            else
            {
                // Banker's dome: a short flared skirt, then a half-ellipse up to the neck.
                p.Add(new Vector2(0.172f, 0f)); p.Add(new Vector2(0.176f, 0.005f)); p.Add(new Vector2(0.175f, 0.014f));
                for (int i = 0; i <= 16; i++)
                {
                    float a = Mathf.Lerp(0f, 77f, i / 16f) * Mathf.Deg2Rad;
                    p.Add(new Vector2(0.171f * Mathf.Cos(a), 0.022f + 0.165f * Mathf.Sin(a)));
                }
                p.Add(new Vector2(0.034f, 0.19f));
            }
            return BoardMeshes.Lathe("poolshade_out" + style, p, 48, 3f);
        }

        /// <summary>White enamel / opal inside of the shade, from the axis (above the socket) down to the rim; v runs 0..1 from the top.</summary>
        public static Mesh ShadeInner(int style)
        {
            var p = new List<Vector2>();
            if (style == 1)
            {
                p.Add(new Vector2(0f, 0.146f)); p.Add(new Vector2(0.046f, 0.142f));
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    p.Add(new Vector2(Mathf.Lerp(0.058f, 0.184f, t) - Mathf.Sin(t * Mathf.PI) * 0.006f, Mathf.Lerp(0.13f, 0.004f, t)));
                }
            }
            else
            {
                p.Add(new Vector2(0f, 0.186f)); p.Add(new Vector2(0.03f, 0.183f));
                for (int i = 0; i <= 16; i++)
                {
                    float a = Mathf.Lerp(76f, 0f, i / 16f) * Mathf.Deg2Rad;
                    p.Add(new Vector2(0.167f * Mathf.Cos(a), 0.022f + 0.159f * Mathf.Sin(a)));
                }
                p.Add(new Vector2(0.171f, 0.004f));
            }
            float len = 0f;
            for (int i = 1; i < p.Count; i++) len += Vector2.Distance(p[i], p[i - 1]);
            return BoardMeshes.Lathe("poolshade_in" + style, p, 48, 1f / len);
        }

        /// <summary>Rim: a brass U-band round the glass edge (style 0) or a rolled bead (style 1).</summary>
        public static Mesh ShadeRim(int style)
        {
            var p = new List<Vector2>();
            if (style == 1) BoardMeshes.Arc(p, 0.187f, 0.004f, 0.0045f, -90f, 270f, 14);
            else
            {
                p.Add(new Vector2(0.165f, 0.013f)); p.Add(new Vector2(0.165f, 0.002f));
                BoardMeshes.Arc(p, 0.167f, 0.002f, 0.002f, 180f, 270f, 3);
                BoardMeshes.Arc(p, 0.179f, 0.003f, 0.003f, 270f, 360f, 3);
                p.Add(new Vector2(0.182f, 0.012f));
                BoardMeshes.Arc(p, 0.179f, 0.012f, 0.003f, 0f, 90f, 3);
                p.Add(new Vector2(0.176f, 0.015f)); p.Add(new Vector2(0.165f, 0.013f));
            }
            return BoardMeshes.Lathe("poolshade_rim" + style, p, 48, 6f);
        }

        /// <summary>Crown on top of the shade (brass cap with a ribbed collar), seated a little into the shade.</summary>
        public static Mesh ShadeCrown(int style)
        {
            var p = new List<Vector2>();
            if (style == 1)
            {
                p.Add(new Vector2(0.04f, 0.142f));
                p.Add(new Vector2(0.042f, 0.15f)); p.Add(new Vector2(0.04f, 0.156f)); p.Add(new Vector2(0.03f, 0.158f));
                p.Add(new Vector2(0.03f, 0.158f)); p.Add(new Vector2(0.026f, 0.163f)); p.Add(new Vector2(0.026f, 0.17f));
                p.Add(new Vector2(0.026f, 0.17f)); p.Add(new Vector2(0f, 0.172f));
            }
            else
            {
                p.Add(new Vector2(0.054f, 0.175f));
                p.Add(new Vector2(0.058f, 0.18f)); p.Add(new Vector2(0.054f, 0.186f)); p.Add(new Vector2(0.044f, 0.19f));
                p.Add(new Vector2(0.03f, 0.194f)); p.Add(new Vector2(0.026f, 0.197f)); p.Add(new Vector2(0.026f, 0.197f));
                p.Add(new Vector2(0.028f, 0.199f)); p.Add(new Vector2(0.026f, 0.201f)); p.Add(new Vector2(0.018f, 0.203f));
                p.Add(new Vector2(0.018f, 0.203f)); p.Add(new Vector2(0.018f, 0.205f)); p.Add(new Vector2(0f, 0.206f));
            }
            return BoardMeshes.Lathe("poolshade_crown" + style, p, 40, 6f);
        }

        // ================================================================ bulb, socket, stem

        /// <summary>A-shape bulb, base up: glass from the tip (y = 0) to the neck (y = 0.088).</summary>
        public static Mesh BulbGlass()
        {
            var p = new List<Vector2> { new(0f, 0f) };
            BoardMeshes.Arc(p, 0f, 0.03f, 0.03f, -88f, 25f, 12);
            p.Add(new Vector2(0.022f, 0.06f)); p.Add(new Vector2(0.015f, 0.076f)); p.Add(new Vector2(0.0135f, 0.088f)); p.Add(new Vector2(0f, 0.089f));
            return BoardMeshes.Lathe("poolbulb", p, 28, 4f);
        }

        /// <summary>Screw base and socket above the bulb: from y = 0.084 up to <paramref name="top"/>.</summary>
        public static Mesh Socket(float top)
        {
            var p = new List<Vector2> { new(0f, 0.084f), new(0.0138f, 0.084f) };
            for (int i = 0; i < 4; i++)
            {
                p.Add(new Vector2(0.0138f, 0.088f + i * 0.005f)); p.Add(new Vector2(0.0128f, 0.0905f + i * 0.005f));
            }
            p.Add(new Vector2(0.0138f, 0.108f)); p.Add(new Vector2(0.0138f, 0.108f));
            p.Add(new Vector2(0.019f, 0.11f)); p.Add(new Vector2(0.0195f, 0.114f)); p.Add(new Vector2(0.0195f, top - 0.01f));
            p.Add(new Vector2(0.017f, top)); p.Add(new Vector2(0f, top));
            return BoardMeshes.Lathe("poolsocket" + top.ToString("0.000"), p, 24, 4f);
        }

        /// <summary>Round rod of radius <paramref name="r"/> from y = 0 up to <paramref name="len"/> (stems, down-rods).</summary>
        public static Mesh Rod(float r, float len)
        {
            var p = new List<Vector2> { new(0f, 0f), new(r, 0f), new(r, 0f), new(r, len), new(r, len), new(0f, len) };
            return BoardMeshes.Lathe($"poolrod{r:0.0000}_{len:0.000}", p, 16, 4f);
        }

        /// <summary>Ceiling canopy (rosette): a stepped dome hanging down from y = 0, with a bead round the edge.</summary>
        public static Mesh Canopy()
        {
            var p = new List<Vector2>
            {
                new(0f, -0.05f), new(0.011f, -0.05f), new(0.012f, -0.046f), new(0.012f, -0.04f), new(0.012f, -0.04f),
                new(0.022f, -0.038f), new(0.04f, -0.033f), new(0.056f, -0.024f), new(0.064f, -0.014f), new(0.066f, -0.008f),
                new(0.066f, -0.008f), new(0.07f, -0.006f), new(0.071f, -0.003f), new(0.07f, 0f), new(0f, 0f),
            };
            return BoardMeshes.Lathe("poolcanopy", p, 40, 6f);
        }

        /// <summary>Turned drop finial hanging under the beam ends (top at y = 0).</summary>
        public static Mesh Finial()
        {
            var p = new List<Vector2> { new(0f, -0.1f) };
            BoardMeshes.Arc(p, 0f, -0.092f, 0.008f, -90f, 0f, 4);
            p.Add(new Vector2(0.006f, -0.08f)); p.Add(new Vector2(0.014f, -0.07f)); p.Add(new Vector2(0.02f, -0.055f));
            p.Add(new Vector2(0.018f, -0.042f)); p.Add(new Vector2(0.01f, -0.034f)); p.Add(new Vector2(0.008f, -0.028f));
            p.Add(new Vector2(0.015f, -0.024f)); p.Add(new Vector2(0.015f, -0.019f)); p.Add(new Vector2(0.01f, -0.016f));
            p.Add(new Vector2(0.012f, -0.008f)); p.Add(new Vector2(0.026f, -0.004f)); p.Add(new Vector2(0.028f, 0f)); p.Add(new Vector2(0f, 0f));
            return BoardMeshes.Lathe("poolfinial", p, 28, 6f);
        }

        /// <summary>Brass end knob of the tube bar (style 1), along +Y from y = 0.</summary>
        public static Mesh EndKnob()
        {
            var p = new List<Vector2> { new(0f, 0f), new(0.025f, 0f), new(0.025f, 0f), new(0.026f, 0.008f), new(0.02f, 0.014f), new(0.016f, 0.02f) };
            BoardMeshes.Arc(p, 0f, 0.034f, 0.016f, -62f, 90f, 10);
            return BoardMeshes.Lathe("poolendknob", p, 28, 6f);
        }

        // ================================================================ sweeps, chain

        /// <summary>Tube of <paramref name="radius"/> along <paramref name="path"/>(t), t = 0..1, optionally closed (no caps).</summary>
        public static Mesh Sweep(string key, Func<float, Vector3> path, bool closed, float radius, int segs, int sides)
        {
            key = "sweep_" + key;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 prevN = Vector3.zero;
            for (int i = 0; i <= segs; i++)
            {
                float u = closed && i == segs ? 0f : i / (float)segs;
                Vector3 c = path(u);
                Vector3 tan = (path(Mathf.Min(u + 0.002f, closed ? u + 0.002f : 1f)) - path(Mathf.Max(u - 0.002f, closed ? u - 0.002f : 0f))).normalized;
                Vector3 nrm = i == 0 ? Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized
                                     : Vector3.ProjectOnPlane(prevN, tan).normalized;
                prevN = nrm;
                Vector3 bin = Vector3.Cross(tan, nrm);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 d = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    v.Add(c + d * radius); n.Add(d); uv.Add(new Vector2(s / (float)sides, i / (float)segs * 8f));
                }
                if (i == 0) continue;
                int p0 = (i - 1) * (sides + 1), q0 = i * (sides + 1);
                for (int s = 0; s < sides; s++)
                {
                    t.Add(p0 + s); t.Add(q0 + s); t.Add(q0 + s + 1);
                    t.Add(p0 + s); t.Add(q0 + s + 1); t.Add(p0 + s + 1);
                }
            }
            var m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        /// <summary>Chain link: wire radius, half straight length and end radius of the oval centre line (in the XY plane, long along Y).</summary>
        public const float LinkWire = 0.0021f, LinkHalf = 0.008f, LinkEnd = 0.0068f;
        /// <summary>Distance between the centres of two interlocked links.</summary>
        public const float LinkPitch = 2f * (LinkHalf + LinkEnd - LinkWire);

        static Vector3 LinkPath(float u)
        {
            // Stadium: right straight, top arc, left straight, bottom arc (equal parameter share per piece length).
            float straight = LinkHalf * 2f, arc = Mathf.PI * LinkEnd, total = 2f * (straight + arc);
            float s = u * total;
            if (s < straight) return new Vector3(LinkEnd, -LinkHalf + s, 0f);
            s -= straight;
            if (s < arc) { float a = s / LinkEnd; return new Vector3(Mathf.Cos(a) * LinkEnd, LinkHalf + Mathf.Sin(a) * LinkEnd, 0f); }
            s -= arc;
            if (s < straight) return new Vector3(-LinkEnd, LinkHalf - s, 0f);
            s -= straight;
            { float a = Mathf.PI + s / LinkEnd; return new Vector3(Mathf.Cos(a) * LinkEnd, -LinkHalf + Mathf.Sin(a) * LinkEnd, 0f); }
        }

        /// <summary>Number of links of a chain spanning about <paramref name="length"/>.</summary>
        public static int LinkCount(float length) => Mathf.Max(1, Mathf.RoundToInt(length / LinkPitch));

        /// <summary>Real span of a chain of about <paramref name="length"/>: from the hang point (inner top of the first link, y = 0)
        /// to the inner bottom of the last link (where the next part hooks in).</summary>
        public static float ChainSpan(float length) => LinkCount(length) * LinkPitch;

        /// <summary>Chain hanging from y = 0 (inner top of the first link) down to -<see cref="ChainSpan"/>, links alternately turned by 90 degrees; one mesh.</summary>
        public static Mesh Chain(float length)
        {
            int count = LinkCount(length);
            string key = "chain" + count;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var link = Sweep("chainlink", LinkPath, true, LinkWire, 36, 8);
            var ci = new CombineInstance[count];
            float top = -(LinkHalf + LinkEnd - LinkWire);
            for (int i = 0; i < count; i++)
                ci[i] = new CombineInstance
                {
                    mesh = link,
                    transform = Matrix4x4.TRS(new Vector3(0f, top - i * LinkPitch, 0f), Quaternion.Euler(0f, (i & 1) * 90f, 0f), Vector3.one),
                };
            var m = new Mesh { name = key };
            m.CombineMeshes(ci, true, true);
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        /// <summary>Eye bolt / hook loop: a ring of centre radius <paramref name="r"/> in the XY plane (centre at the origin).</summary>
        public static Mesh Ring(float r, float wire)
        {
            return Sweep($"ring{r:0.0000}_{wire:0.0000}", u => new Vector3(Mathf.Cos(u * Mathf.PI * 2f) * r, Mathf.Sin(u * Mathf.PI * 2f) * r, 0f), true, wire, 28, 8);
        }
    }
}
