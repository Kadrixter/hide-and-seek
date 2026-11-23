using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// The big lobby mirror (south wall, <see cref="PlanarMirror"/>) with a choice of frames:
    /// Gilded (carved gold moulding, corner rosettes, crest), Modern (slim black frame + warm LED backlight halo),
    /// Hollywood (wooden frame with bulbs, the old look). Two soft lights in front and a key light from above keep the
    /// person in front of it lit in the reflection, whatever the frame.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        public enum MirrorFrame { Gilded, Modern, Hollywood }

        /// <summary>Frame used by the lobby build (the -mirrorframes editor render shows all three).</summary>
        public static MirrorFrame MirrorStyle = MirrorFrame.Gilded;

        private static Transform LobbyMirror(Transform root, Vector3 wallCentre, Vector2 size, MirrorFrame style)
        {
            var p = new GameObject("Mirror").transform;
            p.SetParent(root, false);
            p.SetPositionAndRotation(wallCentre, Quaternion.identity); // south wall: local +Z = into the room
            const float glassZ = 0.03f;
            LocalBox(p, "MirrorBacking", new Vector3(0f, 0f, 0.012f), new Vector3(size.x + 0.02f, size.y + 0.02f, 0.02f), Mat("LobbyMirrorBack", new Color(0.05f, 0.05f, 0.05f), null, 0.2f, Vector2.one));
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "MirrorGlass";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(p, false);
            q.transform.localPosition = new Vector3(0f, 0f, glassZ);
            q.transform.localRotation = Quaternion.Euler(0, 180, 0); // face into the room (+Z)
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            string path = $"{LobbyArtDir}/MirrorSurface.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("HideAndSeek/Mirror"));
                AssetDatabase.CreateAsset(m, path);
            }
            q.GetComponent<MeshRenderer>().sharedMaterial = m;
            q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            q.AddComponent<PlanarMirror>();

            float frameW;
            switch (style)
            {
                case MirrorFrame.Modern: frameW = ModernFrame(p, size); break;
                case MirrorFrame.Hollywood: frameW = HollywoodFrame(p, size); break;
                default: frameW = GildedFrame(p, size); break;
            }
            var col = p.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0f, 0.03f);
            col.size = new Vector3(size.x + frameW * 2f, size.y + frameW * 2f, 0.06f);

            // lights for the person in front (and therefore the reflection)
            float hx = size.x * 0.5f + frameW, hy = size.y * 0.5f + frameW;
            void Soft(string n, Vector3 lp)
            {
                var l = new GameObject(n).AddComponent<Light>();
                l.transform.SetParent(p, false);
                l.transform.localPosition = lp;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.86f, 0.68f);
                l.range = 3.6f;
                l.intensity = style == MirrorFrame.Hollywood ? 1.3f : 1.0f;
            }
            Soft("MirrorLightL", new Vector3(-hx, 0.2f, 0.4f));
            Soft("MirrorLightR", new Vector3(hx, 0.2f, 0.4f));
            var key = new GameObject("MirrorKeyLight").AddComponent<Light>();
            key.transform.SetParent(p, false);
            key.transform.localPosition = new Vector3(0, hy + 0.12f, 0.3f);
            key.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.55f, 1f));
            key.type = LightType.Spot;
            key.spotAngle = 85f;
            key.range = 6f;
            key.intensity = 1.5f;
            key.color = new Color(1f, 0.92f, 0.82f);
            key.shadows = LightShadows.Soft;
            return p;
        }

        private static float GildedFrame(Transform p, Vector2 size)
        {
            const float w = 0.13f;
            var gilt = Mat("LobbyGilt", new Color(0.78f, 0.58f, 0.24f), SmoothTex("lobby2_gilt", 64, 64, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                return Color.Lerp(new Color(0.55f, 0.4f, 0.18f), Color.white, Mathf.Clamp01(n * 1.3f)); // darker wash in the recesses
            }), 0.68f, new Vector2(4f, 1f), 0.85f);
            // carved profile: outer bead, cove, raised ridge with pearls (separate), inner sight edge
            var prof = new List<Vector2> { new(0f, 0f), new(0.03f, 0f) };
            for (int i = 1; i <= 4; i++) { float a = -Mathf.PI * 0.5f + i / 4f * Mathf.PI; prof.Add(new Vector2(0.03f + Mathf.Cos(a) * 0.012f, 0.012f + Mathf.Sin(a) * 0.012f)); }
            prof.Add(new Vector2(0.032f, 0.03f));
            for (int i = 1; i <= 4; i++) { float a = i / 4f * Mathf.PI * 0.5f; prof.Add(new Vector2(0.032f + Mathf.Sin(a) * 0.025f, 0.03f + (1f - Mathf.Cos(a)) * 0.05f)); }
            prof.Add(new Vector2(0.055f, 0.1f));
            prof.Add(new Vector2(0.042f, 0.115f));
            prof.Add(new Vector2(0.036f, w));
            prof.Add(new Vector2(0f, w));
            FrameRing(p, new Vector3(0f, 0f, 0f), size - new Vector2(0.01f, 0.01f), w, prof, gilt, "Gilt");
            // pearls along the ridge, rosettes in the corners, a crest on top
            float hx = size.x * 0.5f - 0.005f + w * 0.62f, hy = size.y * 0.5f - 0.005f + w * 0.62f;
            var pearl = LatheSaved("GiltPearl", new[] { new Vector2(0f, 0f), new Vector2(0.008f, 0.004f), new Vector2(0.009f, 0.009f), new Vector2(0.006f, 0.014f), new Vector2(0f, 0.016f) }, 10, false, false);
            void Pearls(Vector3 a, Vector3 b)
            {
                int n = Mathf.RoundToInt((b - a).magnitude / 0.045f);
                for (int i = 0; i <= n; i++) MeshObj("Pearl", p, pearl, gilt, Vector3.Lerp(a, b, i / (float)n) + new Vector3(0f, 0f, 0.05f), Quaternion.Euler(90f, 0f, 0f));
            }
            Pearls(new Vector3(-hx + 0.06f, hy, 0f), new Vector3(hx - 0.06f, hy, 0f));
            Pearls(new Vector3(-hx + 0.06f, -hy, 0f), new Vector3(hx - 0.06f, -hy, 0f));
            Pearls(new Vector3(-hx, -hy + 0.06f, 0f), new Vector3(-hx, hy - 0.06f, 0f));
            Pearls(new Vector3(hx, -hy + 0.06f, 0f), new Vector3(hx, hy - 0.06f, 0f));
            var rosette = LatheSaved("GiltRosette", new[] { new Vector2(0f, 0.035f), new Vector2(0.03f, 0.03f), new Vector2(0.05f, 0.015f), new Vector2(0.06f, 0f) }, 12, false, false);
            foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                MeshObj("Rosette", p, rosette, gilt, new Vector3(sx * hx, sy * hy, 0.03f), Quaternion.Euler(90f, 0f, 0f));
            // crest: shell-like fan of five lobes above the top rail
            float top = size.y * 0.5f + w;
            for (int i = -2; i <= 2; i++)
            {
                var lobe = Ball("CrestLobe", p, Vector3.zero, new Vector3(0.07f, 0.16f - Mathf.Abs(i) * 0.025f, 0.04f), gilt);
                lobe.transform.localPosition = new Vector3(i * 0.055f, top + 0.04f - Mathf.Abs(i) * 0.012f, 0.025f);
                lobe.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 16f);
            }
            Ball("CrestKnot", p, Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), gilt).transform.localPosition = new Vector3(0f, top - 0.005f, 0.045f);
            return w;
        }

        private static float ModernFrame(Transform p, Vector2 size)
        {
            const float w = 0.028f;
            var black = Mat("LobbyFrameBlack", new Color(0.03f, 0.03f, 0.035f), null, 0.35f, Vector2.one, 0.6f);
            var prof = new List<Vector2> { new(0f, 0f), new(0.034f, 0f), new(0.04f, 0.006f), new(0.04f, w - 0.006f), new(0.034f, w), new(0f, w) };
            FrameRing(p, Vector3.zero, size - new Vector2(0.01f, 0.01f), w, prof, black, "Slim");
            // LED halo: an emissive strip on the wall around the backing, peeking out
            var led = Emissive("LobbyMirrorLed", new Color(1f, 0.85f, 0.6f), new Color(1f, 0.78f, 0.5f) * 2.4f);
            float hx = size.x * 0.5f + w + 0.035f, hy = size.y * 0.5f + w + 0.035f;
            LocalBox(p, "LedTop", new Vector3(0f, hy, 0.002f), new Vector3(hx * 2f, 0.012f, 0.004f), led);
            LocalBox(p, "LedBottom", new Vector3(0f, -hy, 0.002f), new Vector3(hx * 2f, 0.012f, 0.004f), led);
            LocalBox(p, "LedL", new Vector3(-hx, 0f, 0.002f), new Vector3(0.012f, hy * 2f, 0.004f), led);
            LocalBox(p, "LedR", new Vector3(hx, 0f, 0.002f), new Vector3(0.012f, hy * 2f, 0.004f), led);
            var halo = new GameObject("MirrorHalo").AddComponent<Light>();
            halo.transform.SetParent(p, false);
            halo.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            halo.type = LightType.Point;
            halo.color = new Color(1f, 0.8f, 0.55f);
            halo.range = 2.4f;
            halo.intensity = 0.7f;
            return w;
        }

        private static float HollywoodFrame(Transform p, Vector2 size)
        {
            const float w = 0.1f;
            var wood = Mat("LobbyDarkWood", new Color(0.55f, 0.4f, 0.3f), Tex("lobby_wood", TexKind.Wood), 0.5f, new Vector2(1, 1));
            FrameRing(p, Vector3.zero, size - new Vector2(0.01f, 0.01f), w, LobbyMesh.MouldingProfile(0.045f, w, 0.012f), wood, "Wood");
            var bulbMat = Mat("MirrorBulb", new Color(1f, 0.93f, 0.8f), null, 0.9f, Vector2.one);
            bulbMat.EnableKeyword("_EMISSION");
            bulbMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 2.2f);
            float hx = size.x * 0.5f + w * 0.5f, hy = size.y * 0.5f + w * 0.5f;
            void Bulb(Vector3 lp)
            {
                var b = Ball("MirrorBulb", p, Vector3.zero, Vector3.one * 0.07f, bulbMat);
                b.transform.localPosition = lp + new Vector3(0f, 0f, 0.07f);
                b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = 0; i < 5; i++) Bulb(new Vector3(Mathf.Lerp(-hx + 0.15f, hx - 0.15f, i / 4f), hy, 0));
            for (int i = 0; i < 6; i++)
            {
                float y = Mathf.Lerp(-hy + 0.3f, hy - 0.2f, i / 5f);
                Bulb(new Vector3(-hx, y, 0));
                Bulb(new Vector3(hx, y, 0));
            }
            return w;
        }
    }
}
