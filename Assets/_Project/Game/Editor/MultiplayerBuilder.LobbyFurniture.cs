using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// New lobby furniture with mini interactions: electric fireplace (on / off), aquarium (feed the fish), sideboard
    /// with a record player (play / stop); the rest in LobbyFurniture2 (globe, coat rack, darts, bookshelf, crates,
    /// mini-game spots). Every piece is built in its own frame (local +Z = front) and turned to face the room.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void BuildLobbyFurniture(Transform root, Material darkWood, Material metal)
        {
            var f = new GameObject("Furniture").transform;
            f.SetParent(root, false);
            Fireplace(f, LobbyLayout.Fireplace, darkWood);
            Aquarium(f, LobbyLayout.Aquarium, darkWood);
            RecordCabinet(f, LobbyLayout.RecordPlayer, darkWood);
            FloorGlobe(f, LobbyLayout.Globe, darkWood);
            CoatRack(f, LobbyLayout.CoatRack, darkWood);
            Dartboard(f, darkWood);
            Bookshelf(f, LobbyLayout.Bookshelf, darkWood);
            Crates(f);
            MiniGameSpots(f);
        }

        private static Transform Frame(Transform parent, string name, Vector3 pos, float facingYaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, facingYaw, 0f));
            return t;
        }

        private static GameObject RBox(Transform parent, string name, Vector3 local, Vector3 size, Material m, float r = 0.01f, float puff = 0f)
        {
            var g = LocalBox(parent, name, local, size, m);
            RoundCube(g.transform, r, puff);
            return g;
        }

        private static void BoxCollider(Transform t, Vector3 centre, Vector3 size)
        {
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = centre;
            c.size = size;
        }

        private static Material Transparent(string name, Color c, float smooth = 0.9f)
        {
            var m = Mat(name, c, null, smooth, Vector2.one);
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return m;
        }

        private static Material GlassMat()
        {
            string path = $"{LobbyArtDir}/LobbyGlass.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Hns/Glass"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        // ================================================================ fireplace (north wall, faces -Z)

        private static void Fireplace(Transform parent, Vector3 wallPos, Material darkWood)
        {
            var t = Frame(parent, "Fireplace", wallPos, 180f);
            var stone = Mat("LobbyFireStone", new Color(0.82f, 0.78f, 0.7f), Tex("lobby_concrete", TexKind.Concrete), 0.25f, new Vector2(1f, 1f));
            var black = Mat("LobbyFireBlack", new Color(0.04f, 0.04f, 0.045f), null, 0.45f, Vector2.one, 0.4f);
            // surround: plinth, two pilasters, frieze, mantel shelf
            RBox(t, "Plinth", new Vector3(0f, 0.04f, 0.2f), new Vector3(1.76f, 0.08f, 0.4f), stone, 0.012f);
            RBox(t, "PilasterL", new Vector3(-0.73f, 0.54f, 0.18f), new Vector3(0.26f, 0.92f, 0.34f), stone, 0.012f);
            RBox(t, "PilasterR", new Vector3(0.73f, 0.54f, 0.18f), new Vector3(0.26f, 0.92f, 0.34f), stone, 0.012f);
            RBox(t, "Frieze", new Vector3(0f, 0.9f, 0.18f), new Vector3(1.2f, 0.2f, 0.34f), stone, 0.012f);
            RBox(t, "Mantel", new Vector3(0f, 1.03f, 0.21f), new Vector3(1.9f, 0.06f, 0.42f), darkWood, 0.018f);
            // firebox: back with brick pattern, black sides / top / hearth, black frame around the glass
            var brick = Emissive("LobbyFireBrick", new Color(0.35f, 0.2f, 0.15f), new Color(0.25f, 0.08f, 0.03f) * 0.4f, Tex("lobby_bricks", TexKind.Bricks));
            brick.mainTextureScale = new Vector2(2f, 1.2f);
            LocalBox(t, "FireBack", new Vector3(0f, 0.44f, 0.04f), new Vector3(1.2f, 0.72f, 0.02f), brick);
            LocalBox(t, "FireFloor", new Vector3(0f, 0.09f, 0.18f), new Vector3(1.2f, 0.02f, 0.28f), black);
            LocalBox(t, "FireTop", new Vector3(0f, 0.795f, 0.18f), new Vector3(1.2f, 0.01f, 0.28f), black);
            foreach (float sx in new[] { -0.595f, 0.595f })
                LocalBox(t, "FireSide", new Vector3(sx, 0.44f, 0.18f), new Vector3(0.01f, 0.7f, 0.28f), black);
            RBox(t, "GlassFrameTop", new Vector3(0f, 0.785f, 0.335f), new Vector3(1.22f, 0.03f, 0.02f), black, 0.006f);
            RBox(t, "GlassFrameBottom", new Vector3(0f, 0.105f, 0.335f), new Vector3(1.22f, 0.03f, 0.02f), black, 0.006f);
            foreach (float sx in new[] { -0.6f, 0.6f })
                RBox(t, "GlassFrameSide", new Vector3(sx, 0.445f, 0.335f), new Vector3(0.03f, 0.7f, 0.02f), black, 0.006f);
            QuadAt("FireGlass", t, t.TransformPoint(new Vector3(0f, 0.445f, 0.33f)), t.rotation * Quaternion.Euler(0f, 180f, 0f), new Vector2(1.18f, 0.66f), GlassMat());
            // ember bed + logs
            var ember = Emissive("LobbyEmbers", new Color(0.3f, 0.08f, 0.02f), new Color(1f, 0.35f, 0.08f) * 1.6f,
                SmoothTex("lobby2_embers", 128, 32, (x, y) => { float n = Mathf.PerlinNoise(x * 0.18f, y * 0.4f); return Color.Lerp(new Color(0.1f, 0.02f, 0f), new Color(1f, 0.55f, 0.15f), Mathf.Clamp01(n * 1.8f - 0.5f)); }));
            var bed = LocalBox(t, "EmberBed", new Vector3(0f, 0.11f, 0.17f), new Vector3(1.0f, 0.03f, 0.18f), ember);
            var logMat = Mat("LobbyLog", new Color(0.32f, 0.22f, 0.15f), Tex("lobby_wood", TexKind.Wood), 0.2f, new Vector2(0.4f, 1f));
            foreach (var (x, y, z, yaw) in new[] { (-0.2f, 0.16f, 0.14f, 8f), (0.18f, 0.16f, 0.2f, -6f), (0f, 0.23f, 0.17f, 2f) })
            {
                var log = Cyl("Log", t, Vector3.zero, 0.09f, 0.62f, logMat);
                log.transform.localPosition = new Vector3(x, y, z);
                log.transform.localRotation = Quaternion.Euler(0f, yaw, 90f);
            }
            // flames: three additive cards on pivots at their base (LobbyFireplace scales / scrolls them)
            var flameTex = SmoothTex("lobby2_flame", 64, 128, (x, y) =>
            {
                float u = x / 64f, v = y / 128f;
                float w = 0.5f - Mathf.Abs(u - 0.5f);
                float tongue = Mathf.PerlinNoise(u * 6f, v * 3f) * 0.6f + w * 1.4f - v * 0.9f;
                float a = Mathf.Clamp01(tongue * 2.2f - 0.3f) * Mathf.Clamp01((1f - v) * 3f) * Mathf.Clamp01(w * 6f);
                var c = Color.Lerp(new Color(1f, 0.35f, 0.05f), new Color(1f, 0.85f, 0.45f), Mathf.Clamp01(a * 1.4f - v * 0.3f));
                return new Color(c.r * a, c.g * a, c.b * a, a);
            });
            string fpath = $"{LobbyArtDir}/LobbyFlame.mat";
            var flame = AssetDatabase.LoadAssetAtPath<Material>(fpath);
            if (flame == null)
            {
                flame = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                AssetDatabase.CreateAsset(flame, fpath);
            }
            flame.mainTexture = flameTex;
            flame.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(flame);
            var flames = new List<Renderer>();
            foreach (var (x, w, h, z) in new[] { (-0.24f, 0.42f, 0.34f, 0.15f), (0.02f, 0.5f, 0.44f, 0.19f), (0.26f, 0.4f, 0.3f, 0.16f) })
            {
                var piv = new GameObject("Flame").transform;
                piv.SetParent(t, false);
                piv.localPosition = new Vector3(x, 0.13f, z);
                piv.localScale = new Vector3(w, h, 1f);
                var q = QuadAt("FlameCard", piv, piv.position, piv.rotation * Quaternion.Euler(0f, 180f, 0f), Vector2.one, flame);
                q.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                q.transform.localScale = Vector3.one;
                flames.Add(q.GetComponent<Renderer>());
            }
            // control knob on the right pilaster
            var knobPiv = new GameObject("Knob").transform;
            knobPiv.SetParent(t, false);
            knobPiv.localPosition = new Vector3(0.73f, 0.5f, 0.35f);
            MeshObj("KnobRose", knobPiv, LatheSaved("FireKnobRose", new[] { new Vector2(0.035f, 0f), new Vector2(0.03f, 0.006f), new Vector2(0.0f, 0.008f) }, 24, false, false), Brass, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            MeshObj("KnobCap", knobPiv, LatheSaved("FireKnobCap", new[] { new Vector2(0.022f, 0f), new Vector2(0.022f, 0.022f), new Vector2(0.016f, 0.03f), new Vector2(0f, 0.03f) }, 20, false, true), Brass, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            LocalBox(knobPiv, "KnobMark", new Vector3(0f, 0.012f, 0.031f), new Vector3(0.004f, 0.014f, 0.002f), black);
            // mantel decor: two candles in brass holders, a small vase
            var wax = Emissive("LobbyCandle", new Color(0.95f, 0.92f, 0.84f), new Color(0.25f, 0.18f, 0.1f));
            foreach (float sx in new[] { -0.7f, 0.7f })
            {
                MeshObj("CandleHolder", t, LatheSaved("CandleHolder", new[] { new Vector2(0.045f, 0f), new Vector2(0.04f, 0.01f), new Vector2(0.012f, 0.03f), new Vector2(0.01f, 0.1f), new Vector2(0.03f, 0.12f), new Vector2(0.03f, 0.13f) }, 20, true, true), Brass, new Vector3(sx, 1.06f, 0.2f));
                MeshObj("Candle", t, LatheSaved("Candle", new[] { new Vector2(0.018f, 0f), new Vector2(0.018f, 0.16f), new Vector2(0.012f, 0.17f) }, 16, false, true), wax, new Vector3(sx, 1.19f, 0.2f));
            }
            MeshObj("Vase", t, LatheSaved("MantelVase", new[] { new Vector2(0.04f, 0f), new Vector2(0.06f, 0.05f), new Vector2(0.055f, 0.12f), new Vector2(0.025f, 0.18f), new Vector2(0.03f, 0.21f) }, 24, true, false),
                Mat("LobbyVase", new Color(0.15f, 0.35f, 0.55f), null, 0.8f, Vector2.one), new Vector3(0.45f, 1.06f, 0.22f));
            BoxCollider(t, new Vector3(0f, 0.55f, 0.2f), new Vector3(1.9f, 1.1f, 0.42f));
            var glow = new GameObject("FireGlow").AddComponent<Light>();
            glow.transform.SetParent(t, false);
            glow.transform.localPosition = new Vector3(0f, 0.45f, 0.7f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.2f);
            glow.range = 4.2f;
            glow.intensity = 1.1f;
            var fp = t.gameObject.AddComponent<LobbyFireplace>();
            fp.flames = flames.ToArray();
            fp.embers = bed.GetComponent<Renderer>();
            fp.glow = glow;
            fp.knob = knobPiv;
            AddProp(t.gameObject, LobbyPropKind.Fireplace, new Vector3(0.73f, 0.5f, 0.38f), 2.0f);
        }
    }
}
