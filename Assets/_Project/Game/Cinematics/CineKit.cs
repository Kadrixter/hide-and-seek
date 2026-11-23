using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>Small helpers for the runtime-built cinematic props (materials, rounded boxes, cylinders).</summary>
    public static class CineKit
    {
        static Shader _std;

        static Shader Std
        {
            get
            {
                if (_std != null) return _std;
                _std = Shader.Find("Standard");
                if (_std == null)
                {
                    var r = Object.FindAnyObjectByType<MeshRenderer>();
                    if (r != null && r.sharedMaterial != null) _std = r.sharedMaterial.shader;
                }
                return _std;
            }
        }

        public static Material Mat(string name, Color c, float metallic, float gloss, Texture tex = null)
        {
            var m = new Material(Std) { name = name, color = c, mainTexture = tex };
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        public static Material Glow(string name, Color c, float intensity)
        {
            var m = Mat(name, c * 0.4f, 0f, 0.8f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        public static void SetGlow(Material m, Color c, float intensity) => m.SetColor("_EmissionColor", c * intensity);

        public static Transform Node(string name, Transform parent, Vector3 pos, Quaternion? rot = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rot ?? Quaternion.identity;
            t.gameObject.layer = parent != null ? parent.gameObject.layer : 0;
            return t;
        }

        /// <summary>Rounded box (bevelled edges), centre and size in the parent's space.</summary>
        public static Transform Box(string name, Transform parent, Vector3 centre, Vector3 size, Material mat, float bevel = 0.01f,
            Quaternion? rot = null, bool shadows = true)
        {
            var t = Node(name, parent, centre, rot);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = VendingMeshes.RoundedBox(size, Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f));
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        /// <summary>Box from two corners (parent space).</summary>
        public static Transform BoxMinMax(string name, Transform parent, Vector3 a, Vector3 b, Material mat, float bevel = 0.006f) =>
            Box(name, parent, (a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z)), mat, bevel);

        /// <summary>Cylinder along local axis 0 = X, 1 = Y, 2 = Z.</summary>
        public static Transform Cyl(string name, Transform parent, Vector3 centre, float diameter, float length, int axis, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.layer = parent != null ? parent.gameObject.layer : 0;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = centre;
            g.transform.localRotation = axis == 0 ? Quaternion.Euler(0f, 0f, 90f) : axis == 2 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            g.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return g.transform;
        }

        public static Transform Sphere(string name, Transform parent, Vector3 centre, float diameter, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.layer = parent != null ? parent.gameObject.layer : 0;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = centre;
            g.transform.localScale = Vector3.one * diameter;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return g.transform;
        }

        /// <summary>Flat quad facing local +Z of the parent (the visible side looks along +Z).</summary>
        public static Transform Quad(string name, Transform parent, Vector3 centre, Vector2 size, Material mat, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.layer = parent != null ? parent.gameObject.layer : 0;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = centre;
            g.transform.localRotation = (rot ?? Quaternion.identity) * Quaternion.Euler(0f, 180f, 0f);
            g.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g.transform;
        }

        public static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
    }
}
