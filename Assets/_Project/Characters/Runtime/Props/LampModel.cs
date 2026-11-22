using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// The blocky shoulder flashlight (Dying Light style): body, wider head, lens, clip and (optionally) the strap that
    /// goes over the shoulder. Shared by the lamp on the character, the lamps in the lobby box and the first-person
    /// hand during the take animation. Lamp space: +Z = where the light shines, origin = middle of the body.
    /// </summary>
    public static class LampModel
    {
        static Material _body, _lens, _strap;

        /// <summary>Length of the lamp from the back of the body to the lens.</summary>
        public const float Length = 0.115f;

        static Material Body => _body != null ? _body : _body = MakeBody();
        static Material Lens => _lens != null ? _lens : _lens = MakeLens();
        static Material Strap => _strap != null ? _strap : _strap = new Material(Shader.Find("Standard")) { color = new Color(0.05f, 0.05f, 0.05f), name = "LampStrap" };

        static Material MakeBody()
        {
            var m = new Material(Shader.Find("Standard")) { color = new Color(0.13f, 0.13f, 0.14f), name = "LampBody" };
            m.SetFloat("_Metallic", 0.6f);
            m.SetFloat("_Glossiness", 0.45f);
            return m;
        }

        static Material MakeLens()
        {
            var m = new Material(Shader.Find("Standard")) { color = new Color(0.85f, 0.85f, 0.8f), name = "LampLens" };
            m.EnableKeyword("_EMISSION");
            return m;
        }

        /// <summary>Builds the lamp under <paramref name="root"/>; returns the lens renderer (its emission shows the light).</summary>
        public static Renderer Build(Transform root, bool strap, int layer, bool shadows = true)
        {
            Part(root, "Body", new Vector3(0f, 0f, 0.02f), new Vector3(0.06f, 0.07f, 0.11f), Body, layer, shadows);
            Part(root, "Head", new Vector3(0f, 0f, 0.085f), new Vector3(0.08f, 0.09f, 0.03f), Body, layer, shadows);
            var lens = Part(root, "Lens", new Vector3(0f, 0f, 0.102f), new Vector3(0.06f, 0.07f, 0.006f), Lens, layer, shadows);
            Part(root, "Clip", new Vector3(0f, 0.045f, 0f), new Vector3(0.03f, 0.02f, 0.08f), Strap, layer, shadows);
            if (strap)
            {
                // Strap over the shoulder to the back.
                Part(root, "StrapTop", new Vector3(0f, 0.11f, -0.11f), new Vector3(0.035f, 0.012f, 0.27f), Strap, layer, shadows);
                Part(root, "StrapFront", new Vector3(0f, 0.06f, -0.0f), new Vector3(0.035f, 0.1f, 0.012f), Strap, layer, shadows);
            }
            return lens;
        }

        static Renderer Part(Transform parent, string name, Vector3 pos, Vector3 size, Material m, int layer, bool shadows)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            if (!shadows)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return r;
        }
    }
}
