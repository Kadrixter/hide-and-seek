using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Blocky toy slime blaster in loud plastic colours: purple body, orange barrel with a lime muzzle ring, teal grip,
    /// yellow trigger and pump, a see-through-looking tank of glowing green goo on top (its level shows the shots left)
    /// and a few sticker dots. Prop space like the Glock: origin = the fist on the grip, barrel along +Z, up = +Y
    /// (sized for the 22 cm blocky fist).
    /// </summary>
    public static class ToyGunModel
    {
        public static readonly Vector3 MuzzleLocal = new Vector3(0f, 0.15f, 0.41f);
        const float GooBottom = 0.205f, GooHeight = 0.07f;

        static Material _purple, _orange, _lime, _teal, _yellow, _pink, _goo, _dark;

        public sealed class Parts
        {
            public Transform Root, Muzzle, Goo;
            public Renderer[] Renderers;
        }

        static Material Mat(ref Material cache, string name, Color c, float gloss, Color? emission = null)
        {
            if (cache != null) return cache;
            cache = new Material(Shader.Find("Standard")) { color = c, name = name };
            cache.SetFloat("_Glossiness", gloss);
            cache.SetFloat("_Metallic", 0f);
            if (emission.HasValue)
            {
                cache.EnableKeyword("_EMISSION");
                cache.SetColor("_EmissionColor", emission.Value);
            }
            return cache;
        }

        public static Material GooMaterial => Mat(ref _goo, "ToyGoo", new Color(0.35f, 0.95f, 0.2f), 0.92f, new Color(0.12f, 0.45f, 0.05f));

        public static Parts Create(Transform parent, int layer, bool shadows)
        {
            var purple = Mat(ref _purple, "ToyPurple", new Color(0.55f, 0.2f, 0.85f), 0.55f);
            var orange = Mat(ref _orange, "ToyOrange", new Color(1f, 0.48f, 0.08f), 0.55f);
            var lime = Mat(ref _lime, "ToyLime", new Color(0.6f, 1f, 0.15f), 0.5f);
            var teal = Mat(ref _teal, "ToyTeal", new Color(0.1f, 0.75f, 0.8f), 0.45f);
            var yellow = Mat(ref _yellow, "ToyYellow", new Color(1f, 0.86f, 0.1f), 0.5f);
            var pink = Mat(ref _pink, "ToyPink", new Color(1f, 0.35f, 0.65f), 0.5f);
            var dark = Mat(ref _dark, "ToyBore", new Color(0.05f, 0.08f, 0.04f), 0.2f);

            var root = new GameObject("ToyGun").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = layer;
            var parts = new Parts { Root = root };

            void B(string n, Vector3 min, Vector3 max, Material m, float rotX = 0f)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = n;
                go.layer = layer;
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root, false);
                go.transform.localPosition = (min + max) * 0.5f;
                go.transform.localScale = max - min;
                if (rotX != 0f) go.transform.localRotation = Quaternion.Euler(rotX, 0f, 0f);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = m;
                if (!shadows) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            }

            // Grip (raked back), with a pink pommel.
            B("Grip", new Vector3(-0.03f, -0.12f, -0.07f), new Vector3(0.03f, 0.1f, 0.02f), teal, -14f);
            B("Pommel", new Vector3(-0.034f, -0.15f, -0.075f), new Vector3(0.034f, -0.115f, 0.0f), pink, -14f);
            // Body.
            B("Body", new Vector3(-0.042f, 0.09f, -0.11f), new Vector3(0.042f, 0.205f, 0.23f), purple);
            B("BackCap", new Vector3(-0.046f, 0.085f, -0.135f), new Vector3(0.046f, 0.21f, -0.105f), yellow);
            // Trigger guard and trigger.
            B("Guard", new Vector3(-0.016f, 0.0f, 0.03f), new Vector3(0.016f, 0.018f, 0.13f), purple);
            B("GuardFront", new Vector3(-0.016f, 0.0f, 0.112f), new Vector3(0.016f, 0.09f, 0.13f), purple);
            B("Trigger", new Vector3(-0.009f, 0.025f, 0.055f), new Vector3(0.009f, 0.09f, 0.075f), yellow);
            // Barrel, pump under it, muzzle ring with a dark bore.
            B("Barrel", new Vector3(-0.024f, 0.125f, 0.23f), new Vector3(0.024f, 0.175f, 0.38f), orange);
            B("Pump", new Vector3(-0.032f, 0.085f, 0.21f), new Vector3(0.032f, 0.125f, 0.33f), yellow);
            B("Muzzle", new Vector3(-0.036f, 0.112f, 0.38f), new Vector3(0.036f, 0.188f, 0.41f), lime);
            B("Bore", new Vector3(-0.016f, 0.135f, 0.405f), new Vector3(0.016f, 0.165f, 0.412f), dark);
            // Goo tank on top: frame rails + the goo block whose height is the ammo.
            B("TankBase", new Vector3(-0.036f, 0.2f, -0.07f), new Vector3(0.036f, 0.212f, 0.14f), yellow);
            B("TankRailL", new Vector3(-0.04f, 0.2f, -0.075f), new Vector3(-0.03f, 0.29f, -0.06f), yellow);
            B("TankRailR", new Vector3(0.03f, 0.2f, 0.125f), new Vector3(0.04f, 0.29f, 0.14f), yellow);
            B("TankCap", new Vector3(-0.04f, 0.282f, -0.075f), new Vector3(0.04f, 0.296f, 0.14f), orange);
            var gooGo = new GameObject("GooLevel").transform;
            gooGo.SetParent(root, false);
            gooGo.localPosition = new Vector3(0f, GooBottom, 0.033f);
            gooGo.gameObject.layer = layer;
            var goo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goo.name = "Goo";
            goo.layer = layer;
            Object.Destroy(goo.GetComponent<Collider>());
            goo.transform.SetParent(gooGo, false);
            goo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            goo.transform.localScale = new Vector3(0.06f, 1f, 0.19f);
            var gr = goo.GetComponent<MeshRenderer>();
            gr.sharedMaterial = GooMaterial;
            if (!shadows) { gr.shadowCastingMode = ShadowCastingMode.Off; gr.receiveShadows = false; }
            parts.Goo = gooGo;
            // Sticker dots.
            B("DotA", new Vector3(0.042f, 0.13f, 0.02f), new Vector3(0.046f, 0.16f, 0.05f), pink);
            B("DotB", new Vector3(0.042f, 0.15f, 0.09f), new Vector3(0.046f, 0.175f, 0.115f), lime);
            B("DotC", new Vector3(-0.046f, 0.12f, 0.06f), new Vector3(-0.042f, 0.155f, 0.1f), yellow);
            B("DotD", new Vector3(-0.046f, 0.16f, -0.05f), new Vector3(-0.042f, 0.185f, -0.02f), teal);

            var muzzle = new GameObject("MuzzlePoint").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = MuzzleLocal;
            parts.Muzzle = muzzle;
            parts.Renderers = root.GetComponentsInChildren<Renderer>(true);
            SetLevel(parts, 1f);
            return parts;
        }

        /// <summary>Goo in the tank, 0 = empty, 1 = full.</summary>
        public static void SetLevel(Parts parts, float level)
        {
            if (parts?.Goo == null) return;
            float h = Mathf.Max(0.002f, GooHeight * Mathf.Clamp01(level));
            parts.Goo.localScale = new Vector3(1f, h, 1f);
        }
    }
}
