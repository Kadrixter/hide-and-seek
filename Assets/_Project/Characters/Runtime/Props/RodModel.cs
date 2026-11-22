using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Blocky metal rod (square galvanised pipe with rust spots and a rubber foot) used as a peg leg, plus the grey
    /// duct tape sleeve that holds it on the stump and a strap round the waist. Pixel textures are generated at runtime
    /// (point filtered, same chunky look as the skins). Rod space: origin = top end, the pipe hangs along -Y.
    /// </summary>
    public static class RodModel
    {
        const float Px = BlockyCharacter.PixelSize;
        /// <summary>Full rod length = leg length (hip to sole).</summary>
        public const float Length = BlockyCharacter.HipHeight;
        public const float PipeWidth = 0.06f;
        public const float FootSize = 0.08f, FootHeight = 0.045f;
        /// <summary>Tape sleeve round the stump and the top of the rod.</summary>
        public const float SleeveHeight = 0.13f;
        public static readonly float SleeveWidth = 4f * Px + 0.018f;

        static Material _metal, _tape, _rubber;

        /// <summary>Parts of one rod; Root is the top end.</summary>
        public sealed class Parts
        {
            public Transform Root, Pipe, Sleeve, Strap, Clamp;
            public Renderer[] Renderers;
        }

        /// <summary>Builds the rod under <paramref name="parent"/>. <paramref name="withTape"/> adds the sleeve and the strap.</summary>
        public static Parts Create(Transform parent, int layer, bool withTape, float outerSide = 1f)
        {
            var root = new GameObject("PegRod").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = layer;
            var parts = new Parts { Root = root };

            float pipeLen = Length - FootHeight + 0.01f;
            parts.Pipe = Box(root, "Pipe", new Vector3(0f, -pipeLen * 0.5f, 0f), new Vector3(PipeWidth, pipeLen, PipeWidth), Metal, layer);
            Box(root, "Foot", new Vector3(0f, -Length + FootHeight * 0.5f, 0f), new Vector3(FootSize, FootHeight, FootSize), Rubber, layer);
            // Hose clamp just under the sleeve.
            parts.Clamp = Box(root, "Clamp", new Vector3(0f, -SleeveHeight - 0.03f, 0f), new Vector3(PipeWidth + 0.022f, 0.022f, PipeWidth + 0.022f), Metal, layer);
            if (withTape)
            {
                parts.Sleeve = Box(root, "TapeSleeve", new Vector3(0f, -SleeveHeight * 0.5f + 0.01f, 0f), new Vector3(SleeveWidth, SleeveHeight, SleeveWidth), Tape, layer);
                // Strap from the sleeve up the outside of the hip.
                parts.Strap = Box(root, "TapeStrap", new Vector3(outerSide * (SleeveWidth * 0.5f + 0.004f), 0.03f, 0f), new Vector3(0.012f, 0.2f, 0.075f), Tape, layer);
            }
            parts.Renderers = root.GetComponentsInChildren<Renderer>(true);
            return parts;
        }

        /// <summary>Loose rod for the map (no tape), origin at the top end, hanging along -Y.</summary>
        public static Parts CreateLoose(Transform parent, int layer) => Create(parent, layer, false);

        /// <summary>Sets how much of the tape sleeve is wrapped (0 = none, 1 = finished).</summary>
        public static void SetTape(Parts parts, float amount)
        {
            if (parts?.Sleeve == null) return;
            amount = Mathf.Clamp01(amount);
            bool on = amount > 0.01f;
            if (parts.Sleeve.gameObject.activeSelf != on) parts.Sleeve.gameObject.SetActive(on);
            float h = SleeveHeight * Mathf.Max(0.05f, amount);
            float w = Mathf.Lerp(PipeWidth + 0.02f, SleeveWidth, Mathf.Clamp01(amount * 1.6f));
            parts.Sleeve.localScale = new Vector3(w, h, w);
            parts.Sleeve.localPosition = new Vector3(0f, -h * 0.5f + 0.01f, 0f);
            if (parts.Strap != null)
            {
                bool strap = amount > 0.75f;
                if (parts.Strap.gameObject.activeSelf != strap) parts.Strap.gameObject.SetActive(strap);
            }
        }

        /// <summary>Copies layer and shadow mode to every part (first person: shadows only).</summary>
        public static void SetRenderMode(Parts parts, int layer, ShadowCastingMode mode)
        {
            if (parts == null) return;
            foreach (var r in parts.Renderers)
            {
                if (r == null) continue;
                if (r.gameObject.layer != layer) r.gameObject.layer = layer;
                if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            }
            if (parts.Root.gameObject.layer != layer) parts.Root.gameObject.layer = layer;
        }

        static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, int layer)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            g.layer = layer;
            var t = g.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return t;
        }

        // ------------------------------------------------------------------ materials (pixel textures)

        public static Material Metal => _metal != null ? _metal : _metal = MakeMat("RodMetal", MetalTexture(), 0.65f, 0.42f);
        static Material Tape => _tape != null ? _tape : _tape = MakeMat("RodTape", TapeTexture(), 0.25f, 0.55f);
        static Material Rubber => _rubber != null ? _rubber : _rubber = MakeMat("RodRubber", Solid(new Color(0.07f, 0.07f, 0.075f)), 0f, 0.18f);

        static Material MakeMat(string name, Texture2D tex, float metallic, float gloss)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse")) { name = name + " (runtime)", mainTexture = tex, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave
        };

        static Texture2D Solid(Color c)
        {
            var t = NewTex(4, 4);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = c * (0.92f + 0.08f * (i % 3) / 2f);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>8 x 32 galvanised steel: mottled grey, darker edges, a few rust patches and scratches.</summary>
        static Texture2D MetalTexture()
        {
            const int w = 8, h = 32;
            var t = NewTex(w, h);
            var rng = new System.Random(4242);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0.56f + (float)rng.NextDouble() * 0.07f;
                if (x == 0 || x == w - 1) v -= 0.1f;
                var c = new Color(v, v * 1.02f, v * 1.07f);
                // Rust: two patches and a drip line.
                bool rust = (y >= 6 && y <= 8 && x >= 2 && x <= 5) || (y >= 21 && y <= 22 && x >= 1 && x <= 3) || (x == 5 && y >= 8 && y <= 12);
                if (rust || rng.NextDouble() < 0.04) c = Color.Lerp(c, new Color(0.47f, 0.25f, 0.12f), rust ? 0.85f : 0.5f);
                if (y == 15 && x >= 2 && x <= 6) c *= 1.18f; // scratch
                c.a = 1f;
                px[y * w + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>16 x 16 duct tape: silver grey with darker wrap edges and a lighter sheen.</summary>
        static Texture2D TapeTexture()
        {
            const int n = 16;
            var t = NewTex(n, n);
            var rng = new System.Random(77);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = 0.6f + (float)rng.NextDouble() * 0.05f;
                int band = (y + x / 5) % 5;
                if (band == 0) v -= 0.14f;
                else if (band == 2) v += 0.06f;
                px[y * n + x] = new Color(v, v, v * 1.04f, 1f);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
