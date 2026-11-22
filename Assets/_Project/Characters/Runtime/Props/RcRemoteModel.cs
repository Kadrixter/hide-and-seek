using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Two-stick RC transmitter (the pilot of the rally cars) in the cars' livery: charcoal case with rounded edges,
    /// rally-blue face plate with a white and a yellow stripe, yellow bumper and grip caps, rubber handles with ribs,
    /// two gimbal sticks with yellow knobs and trim sliders, a backlit LCD, a red power LED, two toggle switches on
    /// the top edge and a telescopic chrome antenna with a red tip. Built from primitives (no colliders).
    /// <para>
    /// Prop space: origin = the right fist round the right handle, +X = towards the left handle (the holder's left),
    /// +Y = top edge (antenna end), +Z = the face (sticks, screen) turned to the holder's eyes. Sized for the 22 cm
    /// blocky fist: the fists wrap the handles under the case, the sticks stay free above them.
    /// </para>
    /// </summary>
    public static partial class RcRemoteModel
    {
        /// <summary>Left fist (left handle) in prop space.</summary>
        public static readonly Vector3 LeftGrip = new Vector3(0.3f, 0f, 0f);
        /// <summary>Stick pivots (right stick = steering, left stick = throttle).</summary>
        public static readonly Vector3 StickRLocal = new Vector3(0.07f, 0.165f, 0.033f), StickLLocal = new Vector3(0.23f, 0.165f, 0.033f);
        /// <summary>Middle of the case (what flies / lands).</summary>
        public static readonly Vector3 CenterLocal = new Vector3(0.15f, 0.12f, -0.01f);

        internal static readonly Color Case = new Color(0.13f, 0.135f, 0.15f);
        internal static readonly Color CaseLight = new Color(0.21f, 0.215f, 0.235f);
        internal static readonly Color Blue = new Color(0.08f, 0.3f, 0.82f);
        internal static readonly Color White = new Color(0.95f, 0.95f, 0.96f);
        internal static readonly Color Yellow = new Color(1f, 0.78f, 0.08f);
        internal static readonly Color Rubber = new Color(0.055f, 0.055f, 0.06f);
        internal static readonly Color Black = new Color(0.025f, 0.025f, 0.03f);
        internal static readonly Color Chrome = new Color(0.78f, 0.8f, 0.84f);
        internal static readonly Color TipRed = new Color(0.85f, 0.07f, 0.05f);

        static readonly Dictionary<Color, Material> Mats = new();
        static Material _lcd, _ledOn, _ledOff, _chrome;

        public sealed class Parts
        {
            public Transform Root, StickR, StickL, Antenna;
            public Renderer Led;
            public Renderer[] Renderers;
        }

        public static Parts Create(Transform parent, int layer)
        {
            var root = new GameObject("RcTransmitter").transform;
            root.gameObject.layer = layer;
            if (parent != null) root.SetParent(parent, false);
            var p = new Parts { Root = root };
            var cs = Mat(Case, 0.35f);
            var blue = Mat(Blue, 0.62f);
            var yellow = Mat(Yellow, 0.5f);
            var white = Mat(White, 0.5f);
            var rubber = Mat(Rubber, 0.08f);
            var black = Mat(Black, 0.25f);

            // ---- case: main block, rounded top / ends, yellow bumper, battery cover on the back.
            B(root, new Vector3(-0.065f, 0.085f, -0.04f), new Vector3(0.365f, 0.225f, 0.02f), cs, layer);
            B(root, new Vector3(-0.05f, 0.225f, -0.034f), new Vector3(0.35f, 0.24f, 0.014f), cs, layer);
            B(root, new Vector3(-0.076f, 0.095f, -0.033f), new Vector3(-0.065f, 0.215f, 0.013f), cs, layer);
            B(root, new Vector3(0.365f, 0.095f, -0.033f), new Vector3(0.376f, 0.215f, 0.013f), cs, layer);
            B(root, new Vector3(-0.067f, 0.07f, -0.039f), new Vector3(0.367f, 0.087f, 0.019f), yellow, layer);
            B(root, new Vector3(0.06f, 0.11f, -0.045f), new Vector3(0.24f, 0.2f, -0.039f), Mat(CaseLight, 0.3f), layer);
            B(root, new Vector3(0.135f, 0.19f, -0.048f), new Vector3(0.165f, 0.198f, -0.044f), black, layer); // cover latch
            B(root, new Vector3(0.144f, 0.2f, -0.052f), new Vector3(0.156f, 0.218f, -0.04f), ChromeMat, layer); // strap hook

            // ---- face: blue plate, stripes, gimbal housings, LCD, LED, trims.
            B(root, new Vector3(-0.047f, 0.1f, 0.02f), new Vector3(0.347f, 0.216f, 0.027f), blue, layer);
            B(root, new Vector3(-0.047f, 0.107f, 0.027f), new Vector3(0.347f, 0.115f, 0.029f), white, layer);
            B(root, new Vector3(-0.047f, 0.117f, 0.027f), new Vector3(0.347f, 0.121f, 0.029f), yellow, layer);
            foreach (var c in new[] { StickRLocal, StickLLocal })
            {
                B(root, new Vector3(c.x - 0.036f, c.y - 0.036f, 0.027f), new Vector3(c.x + 0.036f, c.y + 0.036f, 0.031f), black, layer);
                B(root, new Vector3(c.x - 0.026f, c.y - 0.026f, 0.031f), new Vector3(c.x + 0.026f, c.y + 0.026f, 0.033f), Mat(CaseLight, 0.4f), layer);
                // Trim slider under the stick.
                B(root, new Vector3(c.x - 0.022f, 0.123f, 0.029f), new Vector3(c.x + 0.022f, 0.129f, 0.032f), black, layer);
                B(root, new Vector3(c.x - 0.004f, 0.121f, 0.031f), new Vector3(c.x + 0.004f, 0.131f, 0.035f), white, layer);
            }
            B(root, new Vector3(0.108f, 0.186f, 0.027f), new Vector3(0.192f, 0.213f, 0.03f), black, layer);
            B(root, new Vector3(0.115f, 0.19f, 0.03f), new Vector3(0.185f, 0.209f, 0.032f), LcdMat, layer);
            B(root, new Vector3(0.122f, 0.196f, 0.032f), new Vector3(0.15f, 0.2f, 0.0325f), Mat(new Color(0.12f, 0.2f, 0.12f), 0.3f), layer); // LCD digits
            B(root, new Vector3(0.156f, 0.196f, 0.032f), new Vector3(0.176f, 0.2f, 0.0325f), Mat(new Color(0.12f, 0.2f, 0.12f), 0.3f), layer);
            p.Led = B(root, new Vector3(0.143f, 0.135f, 0.027f), new Vector3(0.157f, 0.149f, 0.035f), LedOn, layer).GetComponent<Renderer>();
            // White "7" disc (the cars' number) between the sticks.
            var disc = Cyl(root, new Vector3(0.15f, 0.165f, 0.029f), 0.032f, 0.003f, white, layer);
            disc.localRotation = Quaternion.Euler(90f, 0f, 0f);
            B(root, new Vector3(0.141f, 0.174f, 0.0305f), new Vector3(0.159f, 0.178f, 0.032f), black, layer);
            var seven = B(root, new Vector3(0.149f, 0.153f, 0.0305f), new Vector3(0.155f, 0.176f, 0.032f), black, layer);
            seven.localRotation = Quaternion.Euler(0f, 0f, -18f);

            // ---- handles under the case ends (the fists wrap these), ribbed rubber, yellow caps.
            foreach (float gx in new[] { 0f, LeftGrip.x })
            {
                B(root, new Vector3(gx - 0.04f, -0.045f, -0.042f), new Vector3(gx + 0.04f, 0.09f, 0.012f), rubber, layer);
                for (int i = 0; i < 3; i++)
                    B(root, new Vector3(gx - 0.043f, -0.032f + i * 0.03f, -0.045f), new Vector3(gx + 0.043f, -0.024f + i * 0.03f, 0.015f), black, layer);
                B(root, new Vector3(gx - 0.036f, -0.056f, -0.038f), new Vector3(gx + 0.036f, -0.045f, 0.008f), yellow, layer);
            }

            // ---- toggle switches on the top edge.
            foreach (float sx in new[] { 0.02f, 0.28f })
            {
                B(root, new Vector3(sx - 0.013f, 0.24f, -0.024f), new Vector3(sx + 0.013f, 0.25f, 0.002f), black, layer);
                var lever = B(root, new Vector3(sx - 0.003f, 0.248f, -0.014f), new Vector3(sx + 0.003f, 0.274f, -0.008f), ChromeMat, layer);
                lever.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            }

            // ---- sticks (pivot = gimbal centre; the shaft points out of the face, +Z).
            p.StickR = Stick(root, "StickR", StickRLocal, layer);
            p.StickL = Stick(root, "StickL", StickLLocal, layer);

            // ---- telescopic antenna on the top-left corner, leaning a little outwards and back.
            var ant = new GameObject("Antenna").transform;
            ant.gameObject.layer = layer;
            ant.SetParent(root, false);
            ant.localPosition = new Vector3(0.335f, 0.238f, -0.012f);
            ant.localRotation = Quaternion.Euler(-8f, 0f, -9f);
            p.Antenna = ant;
            BuildAntenna(ant, layer);

            p.Renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in p.Renderers)
            {
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return p;
        }

        /// <summary>Socket, three chrome segments and the red tip ball along +Y of <paramref name="ant"/>.</summary>
        internal static void BuildAntenna(Transform ant, int layer)
        {
            B(ant, new Vector3(-0.016f, -0.004f, -0.016f), new Vector3(0.016f, 0.02f, 0.016f), Mat(Black, 0.25f), layer);
            B(ant, new Vector3(-0.006f, 0.02f, -0.006f), new Vector3(0.006f, 0.12f, 0.006f), ChromeMat, layer);
            B(ant, new Vector3(-0.008f, 0.115f, -0.008f), new Vector3(0.008f, 0.123f, 0.008f), ChromeMat, layer);
            B(ant, new Vector3(-0.0045f, 0.123f, -0.0045f), new Vector3(0.0045f, 0.21f, 0.0045f), ChromeMat, layer);
            B(ant, new Vector3(-0.003f, 0.21f, -0.003f), new Vector3(0.003f, 0.288f, 0.003f), ChromeMat, layer);
            Ball(ant, new Vector3(0f, 0.294f, 0f), 0.02f, Mat(TipRed, 0.6f), layer);
        }

        static Transform Stick(Transform root, string name, Vector3 at, int layer)
        {
            var pivot = new GameObject(name).transform;
            pivot.gameObject.layer = layer;
            pivot.SetParent(root, false);
            pivot.localPosition = at;
            var boot = Cyl(pivot, new Vector3(0f, 0f, 0.004f), 0.026f, 0.008f, Mat(Black, 0.2f), layer);
            boot.localRotation = Quaternion.Euler(90f, 0f, 0f);
            B(pivot, new Vector3(-0.0045f, -0.0045f, 0f), new Vector3(0.0045f, 0.0045f, 0.04f), ChromeMat, layer);
            var knob = Cyl(pivot, new Vector3(0f, 0f, 0.044f), 0.03f, 0.012f, Mat(Yellow, 0.55f), layer);
            knob.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var cap = Cyl(pivot, new Vector3(0f, 0f, 0.051f), 0.022f, 0.003f, Mat(Black, 0.2f), layer);
            cap.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return pivot;
        }

        /// <summary>Sticks: x = steering (right stick sideways), y = throttle (left stick away from the holder).</summary>
        public static void SetSticks(Parts p, Vector2 input)
        {
            if (p == null) return;
            if (p.StickL != null) p.StickL.localRotation = Quaternion.Euler(-22f * input.y, 0f, 0f);
            if (p.StickR != null) p.StickR.localRotation = Quaternion.Euler(0f, -22f * input.x, 0f);
        }

        /// <summary>Power LED lit / dark (dead transmitter).</summary>
        public static void SetLed(Parts p, bool on)
        {
            if (p?.Led == null) return;
            var m = on ? LedOn : LedOff;
            if (p.Led.sharedMaterial != m) p.Led.sharedMaterial = m;
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

        // ================================================================ primitives

        internal static Transform B(Transform parent, Vector3 min, Vector3 max, Material mat, int layer)
            => Prim(PrimitiveType.Cube, parent, (min + max) * 0.5f, max - min, mat, layer);

        internal static Transform Cyl(Transform parent, Vector3 at, float diameter, float height, Material mat, int layer)
            => Prim(PrimitiveType.Cylinder, parent, at, new Vector3(diameter, height * 0.5f, diameter), mat, layer);

        internal static Transform Ball(Transform parent, Vector3 at, float diameter, Material mat, int layer)
            => Prim(PrimitiveType.Sphere, parent, at, Vector3.one * diameter, mat, layer);

        static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, int layer)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = "Part";
            var col = g.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            g.layer = layer;
            var t = g.transform;
            if (parent != null) t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return t;
        }

        internal static Material ChromeMat => _chrome != null ? _chrome : _chrome = MakeMat("RcRemoteChrome", Chrome, 0.85f, 0.75f, null);
        static Material LcdMat => _lcd != null ? _lcd : _lcd = MakeMat("RcRemoteLcd", new Color(0.62f, 0.86f, 0.58f), 0.75f, 0f, new Color(0.14f, 0.26f, 0.12f));
        static Material LedOn => _ledOn != null ? _ledOn : _ledOn = MakeMat("RcRemoteLedOn", new Color(1f, 0.15f, 0.1f), 0.8f, 0f, new Color(0.9f, 0.08f, 0.04f));
        internal static Material LedOff => _ledOff != null ? _ledOff : _ledOff = MakeMat("RcRemoteLedOff", new Color(0.35f, 0.06f, 0.05f), 0.8f, 0f, null);

        internal static Material Mat(Color c, float gloss)
        {
            if (Mats.TryGetValue(c, out var m) && m != null) return m;
            return Mats[c] = MakeMat("RcRemotePlastic", c, gloss, 0f, null);
        }

        static Material MakeMat(string name, Color c, float gloss, float metal, Color? emission)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse")) { name = name + " (runtime)", color = c, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Glossiness", gloss);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return m;
        }
    }
}
