using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Aquarium on a wooden cabinet (south wall). The tank: gravel bed sloping up to the back with pebbles, a driftwood
    /// root, a little castle with a bubbler, swaying plants (vallisneria strips, sword plants, moss balls), an internal
    /// filter with its outlet ripple, an air stone, painted backdrop, water volume with a moving surface and caustics on
    /// the gravel, glass with black trim, lid with an LED lamp. Fish (LobbyAquariumFish) swim with
    /// <see cref="LobbyAquarium"/> (paths, school, tail wag, feeding).
    /// Tank space: origin on the cabinet top in the middle, +Z = front glass.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private const float TankW = 1.2f, TankH = 0.52f, TankD = 0.44f;

        private static void Aquarium(Transform parent, Vector3 pos, Material darkWood)
        {
            var t = Frame(parent, "Aquarium", pos, 0f); // south wall, faces +Z
            var black = Mat("LobbyFireBlack", new Color(0.04f, 0.04f, 0.045f), null, 0.45f, Vector2.one, 0.4f);
            RBox(t, "Cabinet", new Vector3(0f, 0.4f, 0f), new Vector3(1.3f, 0.7f, 0.5f), darkWood, 0.015f);
            RBox(t, "CabinetTop", new Vector3(0f, 0.765f, 0f), new Vector3(1.34f, 0.03f, 0.52f), darkWood, 0.01f);
            RBox(t, "Plinth", new Vector3(0f, 0.025f, -0.01f), new Vector3(1.24f, 0.05f, 0.44f), black, 0.006f);
            foreach (float sx in new[] { -0.32f, 0.32f })
            {
                RBox(t, "Door", new Vector3(sx, 0.4f, 0.255f), new Vector3(0.6f, 0.62f, 0.012f), darkWood, 0.006f);
                RBox(t, "Handle", new Vector3(sx + (sx < 0 ? 0.24f : -0.24f), 0.55f, 0.268f), new Vector3(0.016f, 0.12f, 0.014f), Brass, 0.006f);
            }
            var tank = new GameObject("Tank").transform;
            tank.SetParent(t, false);
            tank.localPosition = new Vector3(0f, 0.78f, 0f);
            TankBed(tank);
            TankDecor(tank, black);
            AquariumFish(tank);
            TankShell(tank, black);
            var aq = tank.gameObject.AddComponent<LobbyAquarium>();
            aq.centre = new Vector3(0f, 0.27f, 0.01f);
            aq.half = new Vector3(TankW * 0.5f - 0.06f, 0.17f, TankD * 0.5f - 0.07f);
            aq.flakeMat = Mat("LobbyFishFood", new Color(0.8f, 0.45f, 0.2f), null, 0.2f, Vector2.one);
            aq.bubbleMat = Transparent("LobbyBubble", new Color(0.9f, 0.97f, 1f, 0.45f));
            BoxCollider(t, new Vector3(0f, 0.66f, 0f), new Vector3(1.34f, 1.32f, 0.52f));
            AddProp(tank.gameObject, LobbyPropKind.Aquarium, new Vector3(0f, TankH + 0.06f, 0.12f), 1.9f);
        }

        /// <summary>Backdrop, gravel sloping to the back, pebbles, caustic light on the gravel.</summary>
        private static void TankBed(Transform tank)
        {
            var backdrop = Mat("LobbyAquaBack", Color.white, SmoothTex("lobby3_aquaback", 256, 128, (x, y) =>
            {
                float v = y / 128f, u = x / 256f;
                var c = Color.Lerp(new Color(0.02f, 0.1f, 0.16f), new Color(0.08f, 0.36f, 0.45f), v);
                float hill = 0.25f + 0.15f * Mathf.PerlinNoise(u * 4f, 1.7f);
                if (v < hill) c = Color.Lerp(c, new Color(0.12f, 0.16f, 0.12f), 0.65f);
                float weed = Mathf.PerlinNoise(u * 22f, 3.3f);
                if (v < hill + weed * 0.45f && Mathf.Repeat(u * 37f, 1f) < 0.35f) c = Color.Lerp(c, new Color(0.06f, 0.28f, 0.12f), 0.55f);
                return c;
            }), 0.2f, Vector2.one);
            LocalBox(tank, "Backdrop", new Vector3(0f, TankH * 0.5f, -TankD * 0.5f + 0.004f), new Vector3(TankW - 0.02f, TankH - 0.02f, 0.002f), backdrop);
            var gravel = Mat("LobbyGravel", Color.white, SmoothTex("lobby2_gravel", 128, 64, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.9f, y * 0.9f), h = Hash01(x / 2 * 37 + y / 2 * 91);
                var c = h > 0.75f ? new Color(0.8f, 0.7f, 0.55f) : h > 0.45f ? new Color(0.55f, 0.5f, 0.45f) : new Color(0.35f, 0.3f, 0.28f);
                return c * (0.75f + 0.35f * n);
            }), 0.3f, new Vector2(2f, 1f));
            // front low, back higher (two puffed slabs)
            RBox(tank, "Gravel", new Vector3(0f, 0.03f, 0.02f), new Vector3(TankW - 0.02f, 0.04f, TankD - 0.05f), gravel, 0.01f, 0.01f);
            RBox(tank, "GravelBack", new Vector3(0f, 0.045f, -0.12f), new Vector3(TankW - 0.03f, 0.05f, 0.18f), gravel, 0.02f, 0.02f);
            var rnd = new System.Random(41);
            var stones = new[]
            {
                Mat("LobbyPebbleGrey", new Color(0.5f, 0.5f, 0.48f), null, 0.45f, Vector2.one), Mat("LobbyPebbleWhite", new Color(0.85f, 0.83f, 0.78f), null, 0.5f, Vector2.one),
                Mat("LobbyPebbleBrown", new Color(0.42f, 0.3f, 0.2f), null, 0.4f, Vector2.one), Mat("LobbyPebbleDark", new Color(0.18f, 0.18f, 0.2f), null, 0.5f, Vector2.one),
            };
            for (int i = 0; i < 34; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * (TankW - 0.1f), z = ((float)rnd.NextDouble() - 0.5f) * (TankD - 0.1f);
                float s = 0.008f + (float)rnd.NextDouble() * 0.02f;
                var p = Ball("Pebble", tank, Vector3.zero, new Vector3(s * 1.3f, s * 0.7f, s), stones[rnd.Next(stones.Length)]);
                p.transform.localPosition = new Vector3(x, (z < -0.03f ? 0.068f : 0.05f) + s * 0.1f, z);
                p.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
            }
            // big rocks
            var rock = Mat("LobbyRock", new Color(0.4f, 0.38f, 0.36f), null, 0.2f, Vector2.one);
            foreach (var (x, z, sx, sy, sz, yaw) in new[] { (-0.33f, -0.1f, 0.17f, 0.11f, 0.12f, 30f), (-0.24f, -0.03f, 0.09f, 0.06f, 0.08f, -20f), (0.38f, 0.04f, 0.1f, 0.05f, 0.08f, 50f) })
            {
                var r = Ball("Rock", tank, Vector3.zero, new Vector3(sx, sy, sz), rock);
                r.transform.localPosition = new Vector3(x, 0.06f + sy * 0.25f, z);
                r.transform.localRotation = Quaternion.Euler(4f, yaw, 6f);
            }
            // caustics: additive light pattern on the gravel and the backdrop (scrolled by LobbyAquarium)
            var caustic = SmoothTex("lobby3_caustics", 128, 128, (x, y) =>
            {
                // tileable wavy light lines (two crossing sine fields, bright where they cross zero)
                float k = Mathf.PI * 2f / 128f;
                float a = Mathf.Sin(x * k * 3f + Mathf.Sin(y * k * 2f) * 1.6f + Mathf.Sin(y * k * 5f) * 0.5f);
                float b = Mathf.Sin(y * k * 3f + Mathf.Sin(x * k * 2f) * 1.6f + Mathf.Sin(x * k * 4f) * 0.6f);
                float l = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(a) * 5f), Mathf.Clamp01(1f - Mathf.Abs(b) * 5f));
                l = l * l;
                return new Color(l * 0.7f, l * 0.9f, l, 1f); // opaque alpha: additive, no colour bleeding
            });
            string cp = $"{LobbyArtDir}/LobbyCaustics.mat";
            var cm = AssetDatabase.LoadAssetAtPath<Material>(cp);
            if (cm == null)
            {
                cm = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                AssetDatabase.CreateAsset(cm, cp);
            }
            cm.mainTexture = caustic;
            cm.mainTextureScale = new Vector2(3f, 1.2f);
            cm.SetColor("_TintColor", new Color(0.16f, 0.21f, 0.24f, 0.5f));
            EditorUtility.SetDirty(cm);
            var c1 = QuadAt("Caustics", tank, tank.TransformPoint(new Vector3(0f, 0.076f, 0f)), tank.rotation * Quaternion.Euler(90f, 0f, 0f), new Vector2(TankW - 0.04f, TankD - 0.04f), cm);
            c1.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            QuadAt("CausticsBack", tank, tank.TransformPoint(new Vector3(0f, 0.3f, -TankD * 0.5f + 0.008f)), tank.rotation * Quaternion.Euler(0f, 180f, 0f), new Vector2(TankW - 0.04f, 0.4f), cm);
        }
    }
}
