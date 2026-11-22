using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Hand microphone of the garage public-address system (a CB-radio style "gruszka"): rounded charcoal shell a bit
    /// wider at the top, slotted metal grille on the upper front with a small badge under it, rubber push-to-talk bar
    /// on the left side (seen from the front; under the right thumb of the holder), a chrome hang tab on the back for
    /// the wall hook and a ribbed rubber strain relief at the bottom where the coiled cord leaves.
    /// Mic space: origin = middle of the shell, +Z = front (grille), +Y = top, the cord leaves towards -Y.
    /// Shared by the wall station (Game), the third-person hand and the first-person view model.
    /// </summary>
    public static class PaMicModel
    {
        public const float Width = 0.095f, Height = 0.135f, Thick = 0.042f;
        /// <summary>Scale in the blocky fist (on the hook it is 1; the fists are 22 cm wide).</summary>
        public const float HandScale = 1.6f;
        /// <summary>End of the strain relief: the coiled cord starts here.</summary>
        public static readonly Vector3 CordLocal = new Vector3(0f, -Height * 0.5f - 0.034f, -0.002f);
        /// <summary>Point of the shell that sits in the middle of the fist (lower part, the grille stays free).</summary>
        public static readonly Vector3 GripLocal = new Vector3(0f, -0.048f, 0f);
        /// <summary>Middle of the grille (what is held to the mouth).</summary>
        public static readonly Vector3 GrilleLocal = new Vector3(0f, 0.026f, Thick * 0.5f);
        /// <summary>Neck of the hang tab on the back (sits in the slot of the wall hook).</summary>
        public static readonly Vector3 HangLocal = new Vector3(0f, 0.036f, -Thick * 0.5f - 0.006f);
        /// <summary>How far the push-to-talk bar moves in when pressed.</summary>
        public const float ButtonTravel = 0.0028f;

        private const int SubBody = 0, SubGrille = 1, SubSlot = 2, SubChrome = 3, SubRubber = 4;
        private static Mesh _body, _button;
        private static Material[] _mats;

        public sealed class Parts
        {
            public Transform Root, Button;
            public Renderer[] Renderers;
        }

        /// <summary>Half width of the shell at height <paramref name="y"/> (the outline tapers towards the bottom).</summary>
        public static float HalfWidthAt(float y) => Width * 0.5f * (1f + 0.1f * Mathf.Clamp(y / (Height * 0.5f), -1f, 1f));

        public static Parts Create(Transform parent, int layer, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            Build();
            var root = new GameObject("PaMic").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = layer;
            var body = new GameObject("Shell");
            body.layer = layer;
            body.transform.SetParent(root, false);
            body.AddComponent<MeshFilter>().sharedMesh = _body;
            var r0 = body.AddComponent<MeshRenderer>();
            r0.sharedMaterials = _mats;
            var btn = new GameObject("TalkButton");
            btn.layer = layer;
            btn.transform.SetParent(root, false);
            btn.AddComponent<MeshFilter>().sharedMesh = _button;
            var r1 = btn.AddComponent<MeshRenderer>();
            r1.sharedMaterial = _mats[SubRubber];
            var p = new Parts { Root = root, Button = btn.transform, Renderers = new Renderer[] { r0, r1 } };
            SetRender(p, layer, shadows);
            return p;
        }

        public static void SetRender(Parts p, int layer, ShadowCastingMode shadows)
        {
            if (p == null || p.Root == null) return;
            p.Root.gameObject.layer = layer;
            foreach (var r in p.Renderers)
            {
                r.gameObject.layer = layer;
                r.shadowCastingMode = shadows;
                r.receiveShadows = shadows != ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        /// <summary>Talk bar pressed in (0..1).</summary>
        public static void SetPressed(Parts p, float pressed)
        {
            if (p == null || p.Button == null) return;
            p.Button.localPosition = new Vector3(ButtonTravel * Mathf.Clamp01(pressed), 0f, 0f);
        }

        private static List<Vector2> Outline(int n = 44)
        {
            var pts = new List<Vector2>(n);
            float a = Width * 0.5f, b = Height * 0.5f, e = 2f / 3.4f;
            for (int i = 0; i < n; i++)
            {
                float t = i * Mathf.PI * 2f / n;
                float c = Mathf.Cos(t), s = Mathf.Sin(t);
                float x = a * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
                float y = b * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e);
                x *= 1f + 0.1f * (y / b);
                pts.Add(new Vector2(x, y));
            }
            return pts;
        }

        private static void Build()
        {
            if (_body != null) return;
            _mats = new[]
            {
                PaMesh.Mat("PaMicShell", new Color(0.085f, 0.088f, 0.095f), 0.5f),
                PaMesh.Mat("PaMicGrille", new Color(0.24f, 0.245f, 0.25f), 0.45f, 0.65f),
                PaMesh.Mat("PaMicSlot", new Color(0.012f, 0.012f, 0.014f), 0.1f),
                PaMesh.Mat("PaMicChrome", new Color(0.78f, 0.79f, 0.8f), 0.82f, 0.9f),
                PaMesh.Mat("PaMicRubber", new Color(0.035f, 0.035f, 0.038f), 0.18f),
            };

            var m = new PaMesh(5);
            const float t = Thick;
            // Shell: flat-ish back, rounded rim, slightly domed front.
            m.Pebble(Outline(), PaMesh.SlabRings(-t * 0.5f, t * 0.5f, 0.0125f, 5, 0.003f), Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, SubBody);
            // Parting line of the two half shells (thin dark band just proud of the rim).
            var band = Outline();
            for (int i = 0; i < band.Count; i++) band[i] *= 1.004f;
            m.Pebble(band, new List<PaMesh.Ring> { new(1f, 0f, -0.0012f), new(1f, 0f, 0.0012f) }, Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, SubSlot);

            // Grille: dark metal plate on the upper front with horizontal slots.
            float gz = t * 0.5f + 0.0028f;
            var gr = PaMesh.RoundRect(0.031f, 0.024f, 0.009f, 3);
            m.Pebble(gr, PaMesh.SlabRings(gz - 0.003f, gz + 0.0012f, 0.0012f, 2), new Vector3(0f, 0.026f, 0f), Vector3.right, Vector3.up, Vector3.forward, SubGrille);
            for (int i = 0; i < 7; i++)
            {
                float y = 0.026f + (i - 3) * 0.0064f;
                float w = i == 0 || i == 6 ? 0.04f : 0.05f;
                m.Box(new Vector3(0f, y, gz + 0.0012f), new Vector3(w, 0.0026f, 0.0008f), Quaternion.identity, SubSlot);
            }
            // Badge under the grille.
            m.Pebble(PaMesh.RoundRect(0.016f, 0.004f, 0.002f, 2), PaMesh.SlabRings(gz - 0.003f, gz + 0.0008f, 0.0007f, 1), new Vector3(0f, -0.006f, 0f), Vector3.right, Vector3.up, Vector3.forward, SubChrome);
            // Small ribbed grip zone on the lower front.
            for (int i = 0; i < 4; i++)
                m.Box(new Vector3(0f, -0.03f - i * 0.0075f, t * 0.5f + 0.0022f), new Vector3(0.036f - i * 0.004f, 0.0022f, 0.0044f), Quaternion.identity, SubBody);

            // Boot around the talk bar on the left side (seen from the front: -X).
            float side = -HalfWidthAt(0.008f);
            m.Pebble(PaMesh.RoundRect(0.0085f, 0.03f, 0.0065f, 3), PaMesh.SlabRings(-0.003f, 0.0015f, 0.0012f, 2),
                new Vector3(side + 0.001f, 0.008f, 0.002f), Vector3.forward, Vector3.up, Vector3.left, SubRubber);

            // Hang tab on the back: neck + head.
            Vector3 tab = new Vector3(0f, HangLocal.y, -t * 0.5f);
            m.Lathe(tab + new Vector3(0f, 0f, 0.002f), Vector3.back, new[] { new Vector2(0.0042f, 0f), new Vector2(0.0042f, 0.0085f) }, 12, SubChrome);
            m.Lathe(tab + new Vector3(0f, 0f, -0.0065f), Vector3.back, new[]
            {
                new Vector2(0f, 0f), new Vector2(0.0085f, 0f), new Vector2(0.009f, 0.0012f), new Vector2(0.009f, 0.0022f), new Vector2(0.0082f, 0.0032f), new Vector2(0f, 0.0034f),
            }, 16, SubChrome);
            // A ring where the tab enters the shell.
            m.Lathe(tab + new Vector3(0f, 0f, 0.0015f), Vector3.back, new[] { new Vector2(0.0075f, -0.0015f), new Vector2(0.0075f, 0.001f), new Vector2(0.0042f, 0.0016f) }, 14, SubBody);

            // Strain relief: ribbed rubber cone out of the bottom.
            Vector3 bottom = new Vector3(0f, -Height * 0.5f + 0.004f, -0.002f);
            var sr = new List<Vector2> { new(0f, -0.001f), new(0.0095f, 0f) };
            for (int i = 0; i < 5; i++)
            {
                float y = 0.004f + i * 0.0062f;
                float r = Mathf.Lerp(0.0092f, 0.0058f, i / 4f);
                sr.Add(new Vector2(r, y));
                sr.Add(new Vector2(r + 0.0011f, y + 0.0016f));
                sr.Add(new Vector2(r, y + 0.0032f));
            }
            sr.Add(new Vector2(0.0042f, 0.0362f));
            sr.Add(new Vector2(0f, 0.037f));
            m.Lathe(bottom, Vector3.down, sr, 14, SubRubber);
            _body = m.ToMesh("PaMic");

            // Talk bar (separate so it can be pressed in): rubber bar with three grip ribs.
            var b = new PaMesh(1);
            b.Pebble(PaMesh.RoundRect(0.0066f, 0.025f, 0.0052f, 3), PaMesh.SlabRings(-0.002f, 0.0032f, 0.0016f, 2),
                new Vector3(side - 0.0015f, 0.008f, 0.002f), Vector3.forward, Vector3.up, Vector3.left, 0);
            for (int i = -1; i <= 1; i++)
                b.Box(new Vector3(side - 0.0015f - 0.0033f, 0.008f + i * 0.012f, 0.002f), new Vector3(0.0014f, 0.0022f, 0.0095f), Quaternion.identity, 0);
            _button = b.ToMesh("PaMicButton");
        }
    }
}
