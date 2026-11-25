using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public static partial class CueModel
    {
        /// <summary>
        /// Builder of one cue: shared materials (cached per template), and turned parts stacked along +Z from a cursor
        /// (<see cref="Z"/>) so a butt layout reads like the cue maker's drawing: bumper, cap, rings, sleeve, wrap...
        /// </summary>
        internal sealed class Kit
        {
            public readonly Material Template;
            public readonly int Variant;
            public readonly bool Split;
            public Transform Parent;
            /// <summary>Stacking cursor along the cue (metres from the butt end).</summary>
            public float Z;
            public const int Seg = 40;
            /// <summary>v scale of plain rings / metal (metres per texture tile).</summary>
            const float RingTile = 0.05f;
            /// <summary>Chamfer of metal rings and collars (a fine bright line at each edge).</summary>
            const float Chamfer = 0.00012f;

            static readonly Dictionary<string, Material> Mats = new();

            public Kit(Material template, int variant, bool split)
            {
                Template = template;
                Variant = variant;
                Split = split;
            }

            // ================================================================ materials

            public Material Mat(string key, Color c, float metallic, float gloss, Texture tex = null, Texture normal = null)
            {
                key = key + "_" + (Template != null ? Template.GetHashCode() : 0);
                if (Mats.TryGetValue(key, out var m) && m != null) return m;
                m = Template != null ? new Material(Template) : new Material(Shader.Find("Standard"));
                m.name = "Cue_" + key;
                m.color = c;
                m.mainTexture = tex;
                m.mainTextureScale = Vector2.one;
                m.mainTextureOffset = Vector2.zero;
                m.DisableKeyword("_EMISSION");
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Glossiness", gloss);
                m.SetTexture("_BumpMap", normal);
                if (normal != null) { m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
                else m.DisableKeyword("_NORMALMAP");
                Mats[key] = m;
                return m;
            }

            public Material Steel => Mat("steel", new Color(0.80f, 0.81f, 0.82f), 1f, 0.86f);
            public Material Nickel => Mat("nickel", new Color(0.86f, 0.85f, 0.82f), 1f, 0.8f);
            public Material Brass => Mat("brass", new Color(0.86f, 0.68f, 0.37f), 1f, 0.82f);
            public Material Ebony => Mat("ebony", new Color(0.035f, 0.03f, 0.028f), 0f, 0.86f);
            public Material Bone => Mat("bone", new Color(0.91f, 0.87f, 0.77f), 0f, 0.76f);
            public Material Pearl => Mat("pearl", Color.white, 0f, 0.9f, CueArt.Pearl);
            public Material Phenolic => Mat("phenolic", new Color(0.03f, 0.03f, 0.032f), 0f, 0.9f);
            public Material IvoryPhenolic => Mat("ivoryph", new Color(0.93f, 0.90f, 0.81f), 0f, 0.82f);
            public Material Rubber => Mat("rubber", new Color(0.07f, 0.07f, 0.075f), 0f, 0.28f);
            public Material Ferrule => Mat("ferrule", new Color(0.88f, 0.87f, 0.83f), 0f, 0.8f);
            public Material Red => Mat("red", new Color(0.55f, 0.06f, 0.05f), 0f, 0.85f);
            public Material Green => Mat("green", new Color(0.06f, 0.28f, 0.15f), 0f, 0.85f);

            public Material Dash(string key, Color a, Color b) => Mat("dash_" + key, Color.white, 0f, 0.86f, CueArt.Dashes(key, 24, a, b, new Color(0.02f, 0.02f, 0.02f)));

            // ================================================================ parts

            public GameObject Part(string name, Mesh mesh, Material mat, bool shadows = true)
            {
                var g = new GameObject(name) { layer = Parent.gameObject.layer };
                g.transform.SetParent(Parent, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                return g;
            }

            public static float R(float z) => z < PoolDims.JointZ - 1e-5f ? ButtRadius(z) : ShaftRadius(z);

            /// <summary>
            /// Open tube following the cue's radius over z0..z1 (<paramref name="rows"/> slices), v = (z - vz0) / (vz1 - vz0).
            /// <paramref name="chamfer"/> &gt; 0 gives both ends a small 45 degree bevel (metal parts).
            /// </summary>
            public void Tube(string name, float z0, float z1, Material mat, float vz0, float vz1, int rows = 1, float chamfer = 0f, bool shaftSide = false)
            {
                Func<float, float> rad = shaftSide ? ShaftRadius : (Func<float, float>)ButtRadius;
                float V(float z) => (z - vz0) / (vz1 - vz0);
                var p = new List<Vector3>();
                if (chamfer > 0f)
                {
                    p.Add(new Vector3(rad(z0), z0, V(z0)));
                    p.Add(new Vector3(rad(z0) - chamfer, z0, V(z0)));
                    p.Add(new Vector3(rad(z0) - chamfer, z0, V(z0)));
                    p.Add(new Vector3(rad(z0 + chamfer), z0 + chamfer, V(z0 + chamfer)));
                    p.Add(new Vector3(rad(z0 + chamfer), z0 + chamfer, V(z0 + chamfer)));
                }
                float a = chamfer > 0f ? z0 + chamfer : z0, b = chamfer > 0f ? z1 - chamfer : z1;
                for (int i = 0; i <= rows; i++)
                {
                    float z = Mathf.Lerp(a, b, i / (float)rows);
                    p.Add(new Vector3(rad(z), z, V(z)));
                }
                if (chamfer > 0f)
                {
                    p.Add(new Vector3(rad(z1 - chamfer), z1 - chamfer, V(z1 - chamfer)));
                    p.Add(new Vector3(rad(z1) - chamfer, z1, V(z1)));
                    p.Add(new Vector3(rad(z1) - chamfer, z1, V(z1)));
                    p.Add(new Vector3(rad(z1), z1, V(z1)));
                }
                string key = $"{name}_{z0:F5}_{z1:F5}_{vz0:F5}_{vz1:F5}_{rows}_{chamfer:F5}_{shaftSide}";
                Part(name, CueMesh.Lathe(key, p, Seg), mat);
            }

            /// <summary>Butt section of length <paramref name="len"/> at the cursor; v over z0..z1 (whole texture) or metric tiles.</summary>
            public (float z0, float z1) Section(string name, float len, Material mat, float tile = 0f)
            {
                float z0 = Z, z1 = Z + len;
                if (tile > 0f) Tube(name, z0, z1, mat, 0f, tile, 2);
                else Tube(name, z0, z1, mat, z0, z1, 2);
                Z = z1;
                return (z0, z1);
            }

            /// <summary>Ring stack at the cursor: (length, material, metal?) each; dash rings map their texture over their own length.</summary>
            public void Rings(params (float len, Material mat, bool metal)[] rings)
            {
                foreach (var (len, mat, metal) in rings)
                {
                    bool dash = mat.mainTexture != null && mat.name.StartsWith("Cue_dash_");
                    if (dash) Tube("RingDash", Z, Z + len, mat, Z, Z + len);
                    else Tube(metal ? "RingMetal" : "Ring", Z, Z + len, mat, 0f, RingTile, 1, metal ? Chamfer : 0f);
                    Z += len;
                }
            }

            /// <summary>Metal collar (butt side) from the cursor to the joint, the joint face and the threaded pin.</summary>
            public void JointCollar(Material collar, Material pin)
            {
                float J = PoolDims.JointZ, z0 = Z;
                var p = new List<Vector3>
                {
                    new(ButtRadius(z0), z0, 0f), new(ButtRadius(z0) - Chamfer, z0, 0f), new(ButtRadius(z0) - Chamfer, z0, 0f),
                    new(ButtRadius(z0 + Chamfer), z0 + Chamfer, 0f), new(ButtRadius(z0 + Chamfer), z0 + Chamfer, 0f),
                    new(ButtRadius(J - 0.0004f), J - 0.0004f, (J - z0) / RingTile), new(ButtRadius(J - 0.0004f), J - 0.0004f, (J - z0) / RingTile),
                    new(JointR - 0.0004f, J, (J - z0) / RingTile), new(JointR - 0.0004f, J, 0.3f),
                    new(PinR + 0.0006f, J, 0.32f), new(PinR + 0.0006f, J, 0.32f),
                };
                for (int i = 0; i < p.Count; i++) p[i] = new Vector3(p[i].x, p[i].y, (p[i].y - z0) / RingTile);
                p.Add(new Vector3(0f, J, 0.33f));
                Part("JointCollar", CueMesh.Lathe("jointcollar_" + z0.ToString("F5"), p, Seg), collar);
                Z = J;
                Pin(pin);
            }

            /// <summary>3/8"-10 threaded pin standing on the joint face (real thread ridges, chamfered end).</summary>
            void Pin(Material mat)
            {
                float J = PoolDims.JointZ;
                var p = new List<Vector3> { new(PinR - 0.0005f, J - 0.0005f, 0f), new(PinR - 0.0004f, J + 0.0012f, 0.02f) };
                int turns = Mathf.FloorToInt((PinLen - 0.004f) / ThreadPitch);
                // V threads: sharp roots and narrow flat crests (creases at every corner).
                for (int i = 0; i < turns; i++)
                {
                    float z = J + 0.0018f + i * ThreadPitch, v = (z - J) / 0.01f;
                    var root = new Vector3(PinR - 0.0007f, z, v);
                    var c0 = new Vector3(PinR, z + ThreadPitch * 0.42f, v + 0.1f);
                    var c1 = new Vector3(PinR, z + ThreadPitch * 0.58f, v + 0.12f);
                    p.Add(root); p.Add(root); p.Add(c0); p.Add(c0); p.Add(c1); p.Add(c1);
                }
                float zt = J + PinLen;
                p.Add(new Vector3(PinR - 0.0006f, zt - 0.0012f, 1.8f));
                p.Add(new Vector3(PinR - 0.0012f, zt, 1.9f));
                p.Add(new Vector3(PinR - 0.0012f, zt, 1.9f));
                p.Add(new Vector3(0f, zt, 2f));
                Part("JointPin", CueMesh.Lathe("jointpin", p, 28), mat);
            }
        }
    }
}
