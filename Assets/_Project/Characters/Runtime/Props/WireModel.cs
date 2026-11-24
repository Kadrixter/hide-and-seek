using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// A piece of galvanised fence wire torn out of a wire-mesh panel ("Kawałek drutu"): about 46 cm of thin steel
    /// wire with the zig-zag kinks of the mesh it came from, a torn sharp end and a small hook at the other one. The same
    /// wire as a long RC antenna: straightened, wrapped round the old antenna socket, a little loop at the top
    /// (<see cref="CreateAntenna"/>). Built from thin boxes, no colliders.
    /// <para>Space: along +Y from 0 (torn bottom end) to <see cref="Length"/>, the kinks in the X-Y plane.</para>
    /// </summary>
    public static class WireModel
    {
        public const float Length = 0.46f;
        public const float Thick = 0.0045f;
        /// <summary>Where the fist holds the loose piece (a third from the top).</summary>
        public static readonly Vector3 GripPoint = new Vector3(0f, 0.3f, 0f);

        static Material _steel, _steelDark;

        internal static Material Steel => _steel != null ? _steel : _steel = Mat("WireSteel", new Color(0.84f, 0.86f, 0.88f), 0.55f, 0.3f);
        internal static Material SteelDark => _steelDark != null ? _steelDark : _steelDark = Mat("WireSteelDark", new Color(0.5f, 0.51f, 0.53f), 0.4f, 0.3f);

        // Zig-zag of the mesh it was torn out of: straight runs with sharp bends every ~5 cm, a hook at the top.
        static readonly Vector3[] LoosePts =
        {
            new Vector3(0.004f, 0f, 0.002f), new Vector3(0f, 0.012f, 0f), new Vector3(0.016f, 0.062f, 0f), new Vector3(-0.008f, 0.118f, 0.003f),
            new Vector3(0.014f, 0.17f, -0.002f), new Vector3(-0.01f, 0.225f, 0.002f), new Vector3(0.012f, 0.28f, 0f),
            new Vector3(-0.006f, 0.334f, -0.003f), new Vector3(0.01f, 0.388f, 0f), new Vector3(0f, 0.44f, 0f),
            new Vector3(-0.012f, 0.458f, 0.004f), new Vector3(-0.022f, 0.447f, 0.008f), new Vector3(-0.02f, 0.43f, 0.008f),
        };

        /// <summary>The loose piece (inventory item: floor, hand, the antenna swap, torn off the fence).</summary>
        public static Transform CreateLoose(Transform parent, int layer)
        {
            var root = Node("WirePiece", parent, layer);
            Poly(root, LoosePts, Thick, Steel, layer);
            // Torn end: a darker sharp burr.
            Seg(root, LoosePts[0], LoosePts[0] + new Vector3(0.006f, -0.006f, 0.002f), Thick * 0.8f, SteelDark, layer);
            return root;
        }

        /// <summary>
        /// The wire as a long antenna along +Y of <paramref name="parent"/>: three turns round the socket at the bottom,
        /// a slightly wavy mast of <paramref name="length"/> m (world metres, the caller scales), a closed loop on top.
        /// </summary>
        public static Transform CreateAntenna(Transform parent, int layer, float length, float wrapRadius)
        {
            var root = Node("WireAntenna", parent, layer);
            var pts = new List<Vector3>();
            // Wraps round the socket, going up.
            const int turnSteps = 18;
            for (int i = 0; i <= turnSteps; i++)
            {
                float a = i / (float)turnSteps * Mathf.PI * 2f * 1.5f;
                pts.Add(new Vector3(Mathf.Cos(a) * wrapRadius, 0.004f + i * 0.0011f, Mathf.Sin(a) * wrapRadius));
            }
            pts.Add(new Vector3(0f, 0.03f, 0f));
            // Mast with faint kinks left from the mesh.
            int n = Mathf.Max(4, Mathf.RoundToInt(length / 0.055f));
            for (int i = 1; i <= n; i++)
            {
                float y = Mathf.Lerp(0.03f, length - 0.02f, i / (float)n);
                float k = (i % 2 == 0 ? 1f : -1f) * 0.0035f * (i < n ? 1f : 0f);
                pts.Add(new Vector3(k, y, k * 0.3f));
            }
            Poly(root, pts.ToArray(), Thick * 0.9f, Steel, layer);
            // Loop on top (the end bent round so nobody pokes an eye out).
            var loop = new Vector3[9];
            for (int i = 0; i < loop.Length; i++)
            {
                float a = -Mathf.PI * 0.5f + i / 8f * Mathf.PI * 2f;
                loop[i] = new Vector3(Mathf.Cos(a) * 0.012f, length + Mathf.Sin(a) * 0.012f - 0.008f, 0f);
            }
            Poly(root, loop, Thick * 0.9f, Steel, layer);
            return root;
        }

        // ================================================================ primitives

        internal static Transform Node(string name, Transform parent, int layer)
        {
            var t = new GameObject(name).transform;
            t.gameObject.layer = layer;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        internal static void Poly(Transform parent, Vector3[] pts, float thick, Material m, int layer)
        {
            for (int i = 0; i + 1 < pts.Length; i++) Seg(parent, pts[i], pts[i + 1], thick, m, layer);
        }

        /// <summary>A thin box from <paramref name="a"/> to <paramref name="b"/> (overlapping a little at the ends).</summary>
        internal static Transform Seg(Transform parent, Vector3 a, Vector3 b, float thick, Material m, int layer)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Wire";
            var col = g.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            g.layer = layer;
            var t = g.transform;
            t.SetParent(parent, false);
            Vector3 d = b - a;
            t.localPosition = (a + b) * 0.5f;
            t.localRotation = d.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, d) : Quaternion.identity;
            t.localScale = new Vector3(thick, d.magnitude + thick, thick);
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return t;
        }

        static Material Mat(string name, Color c, float gloss, float metal)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse")) { name = name + " (runtime)", color = c, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }
    }
}
