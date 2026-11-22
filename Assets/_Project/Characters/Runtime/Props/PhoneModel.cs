using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Pink toy phone / walkie-talkie in the blocky style: chunky pink body with a lighter top cap, a small LCD,
    /// speaker slits, big green / red buttons, a 3 x 3 keypad of colourful buttons, a stubby antenna with a yellow ball
    /// and a heart sticker. Built from cubes (no colliders). Phone space: origin = middle of the back face, front
    /// (screen) towards +Y, top end (antenna) towards +Z, so identity rotation = lying on the floor screen up.
    /// </summary>
    public static partial class PhoneModel
    {
        public const float Width = 0.12f, Length = 0.24f, Thick = 0.055f;
        /// <summary>Top of the front face above the origin (where a sole lands).</summary>
        public const float FrontY = Thick + 0.012f;

        static readonly Color Pink = new Color(1f, 0.42f, 0.72f), PinkLight = new Color(1f, 0.66f, 0.85f), PinkDark = new Color(0.82f, 0.24f, 0.55f);
        static readonly Color[] KeyColors =
        {
            new Color(1f, 0.86f, 0.2f), new Color(0.35f, 0.82f, 1f), new Color(0.55f, 0.95f, 0.4f),
            new Color(0.98f, 0.98f, 0.95f), new Color(0.72f, 0.5f, 1f), new Color(1f, 0.6f, 0.25f),
        };
        static readonly Dictionary<Color, Material> Mats = new();
        static Material _screenOff, _screenOn;

        public sealed class Parts
        {
            public Transform Root, Body;
            public Renderer Screen;
            /// <summary>Buttons, antenna and ball (separate parts on top of the body).</summary>
            public readonly List<Transform> Bits = new();
            public Renderer[] Renderers;
        }

        public static Parts Create(Transform parent, int layer)
        {
            var root = new GameObject("ToyPhone").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = layer;
            var p = new Parts { Root = root };
            const float w = Width, l = Length, t = Thick;

            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.gameObject.layer = layer;
            p.Body = body;
            Box(body, new Vector3(0f, t * 0.5f, -0.01f), new Vector3(w, t, l - 0.02f), Mat(Pink), layer);
            Box(body, new Vector3(0f, t * 0.5f, l * 0.5f - 0.025f), new Vector3(w - 0.012f, t + 0.006f, 0.03f), Mat(PinkLight), layer); // top cap
            Box(body, new Vector3(0f, t * 0.5f, -l * 0.5f + 0.012f), new Vector3(w - 0.01f, t - 0.008f, 0.024f), Mat(PinkDark), layer);  // bottom bumper
            Box(body, new Vector3(w * 0.5f + 0.003f, t * 0.5f, 0f), new Vector3(0.008f, t - 0.012f, l * 0.6f), Mat(PinkDark), layer);  // side grips
            Box(body, new Vector3(-w * 0.5f - 0.003f, t * 0.5f, 0f), new Vector3(0.008f, t - 0.012f, l * 0.6f), Mat(PinkDark), layer);

            // Screen + speaker slits.
            var scr = Box(body, new Vector3(0f, t + 0.002f, 0.052f), new Vector3(w - 0.036f, 0.006f, 0.058f), ScreenOff, layer);
            p.Screen = scr.GetComponent<Renderer>();
            Box(body, new Vector3(0f, t + 0.001f, 0.052f), new Vector3(w - 0.024f, 0.004f, 0.07f), Mat(new Color(0.95f, 0.95f, 0.98f)), layer); // bezel
            for (int i = 0; i < 3; i++)
                Box(body, new Vector3(0f, t + 0.001f, 0.093f + i * 0.0075f - 0.0075f), new Vector3(0.04f, 0.004f, 0.003f), Mat(new Color(0.25f, 0.08f, 0.18f)), layer);

            // Big call / end buttons, then the keypad (they pop off when crushed).
            p.Bits.Add(Box(root, new Vector3(-0.028f, t + 0.006f, 0.006f), new Vector3(0.036f, 0.014f, 0.022f), Mat(new Color(0.3f, 0.9f, 0.35f)), layer));
            p.Bits.Add(Box(root, new Vector3(0.028f, t + 0.006f, 0.006f), new Vector3(0.036f, 0.014f, 0.022f), Mat(new Color(1f, 0.25f, 0.25f)), layer));
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                var col = KeyColors[(r * 3 + c) % KeyColors.Length];
                p.Bits.Add(Box(root, new Vector3((c - 1) * 0.032f, t + 0.006f, -0.026f - r * 0.026f), new Vector3(0.024f, 0.013f, 0.019f), Mat(col), layer));
            }
            // Heart sticker under the keypad.
            var heart = Mat(new Color(0.95f, 0.1f, 0.35f));
            Box(body, new Vector3(-0.0045f, t + 0.001f, -0.099f), new Vector3(0.008f, 0.004f, 0.008f), heart, layer);
            Box(body, new Vector3(0.0045f, t + 0.001f, -0.099f), new Vector3(0.008f, 0.004f, 0.008f), heart, layer);
            Box(body, new Vector3(0f, t + 0.001f, -0.104f), new Vector3(0.008f, 0.004f, 0.006f), heart, layer);

            // Stubby antenna on the top-right with a yellow ball.
            p.Bits.Add(Box(root, new Vector3(0.036f, t * 0.55f, l * 0.5f + 0.03f), new Vector3(0.017f, 0.017f, 0.06f), Mat(PinkDark), layer));
            p.Bits.Add(Box(root, new Vector3(0.036f, t * 0.55f, l * 0.5f + 0.068f), new Vector3(0.028f, 0.028f, 0.028f), Mat(KeyColors[0]), layer));

            p.Renderers = root.GetComponentsInChildren<Renderer>(true);
            return p;
        }

        /// <summary>Screen lit (ringing: blinking pale green) or dark.</summary>
        public static void SetScreen(Parts p, bool lit)
        {
            if (p?.Screen == null) return;
            var m = lit ? ScreenOn : ScreenOff;
            if (p.Screen.sharedMaterial != m) p.Screen.sharedMaterial = m;
        }

        public static void SetRenderMode(Parts p, int layer, ShadowCastingMode mode)
        {
            if (p == null) return;
            foreach (var r in p.Renderers)
            {
                if (r == null) continue;
                if (r.gameObject.layer != layer) r.gameObject.layer = layer;
                if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            }
            if (p.Root.gameObject.layer != layer) p.Root.gameObject.layer = layer;
        }

        static Transform Box(Transform parent, Vector3 pos, Vector3 size, Material mat, int layer)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Part";
            var col = g.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            g.layer = layer;
            var t = g.transform;
            if (parent != null) t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return t;
        }

        static Material ScreenOff => _screenOff != null ? _screenOff : _screenOff = MakeMat("PhoneScreenOff", new Color(0.12f, 0.2f, 0.18f), 0.7f);
        static Material ScreenOn => _screenOn != null ? _screenOn : _screenOn = MakeMat("PhoneScreenOn", new Color(0.75f, 1f, 0.7f), 0.7f);

        static Material Mat(Color c)
        {
            if (Mats.TryGetValue(c, out var m) && m != null) return m;
            return Mats[c] = MakeMat("PhonePlastic", c, 0.55f);
        }

        static Material MakeMat(string name, Color c, float gloss)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse")) { name = name + " (runtime)", color = c, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }
    }
}
