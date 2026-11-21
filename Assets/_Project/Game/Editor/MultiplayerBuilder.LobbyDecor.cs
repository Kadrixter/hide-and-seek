using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Lobby decoration: snacks on the coffee table, arcade cabinet (animated screen), CCTV wall TV (images rendered
    /// from the real parking map), drinks vending machine, wall clock with live hands, slow ceiling fan, warm LED strips
    /// behind the sofas and the TV, cork board (CCTV photos, notes, MISSING posters), rugs, cushions, trash bin.
    /// Room: 16 x 12 m (x -8..8, z -6..6), 3.6 m high.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private const float RoomH = 3.6f;

        private static void BuildLobbyDecor(Transform root, Material darkWood, Material metal, Material trim, Material fabric)
        {
            var cctv = CaptureCctv(out var cctvLabels); // cached (rendered at the start of BuildLobbyScene)
            var deco = new GameObject("Decor").transform;
            deco.SetParent(root, false);

            TableSnacks(deco);
            Arcade(deco, new Vector3(6.0f, 0f, 5.5f), metal);
            WallTv(deco, new Vector3(-3.6f, 1.8f, -5.94f), cctv, cctvLabels, darkWood);
            Vending(deco, new Vector3(7.55f, 0f, -2.55f), metal);
            WallClock(deco, new Vector3(0.65f, 2.6f, 5.96f));
            CeilingFan(deco, new Vector3(-1.5f, RoomH, 0.8f), darkWood, metal);
            SofaLeds(deco);
            CorkBoard(deco, new Vector3(7.96f, 1.85f, -4.35f), cctv, trim);
            TrashBin(deco, new Vector3(7.35f, 0f, -1.35f), metal);
            Rugs(deco);
            BuildLobbyDetails(root, darkWood, metal, trim);
        }

        // ================================================================ materials / textures

        private static Material Emissive(string name, Color color, Color emission, Texture2D tex = null)
        {
            var m = Mat(name, color, tex, 0.4f, Vector2.one);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            if (tex != null) m.SetTexture("_EmissionMap", tex);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Unlit(string name, Texture2D tex)
        {
            string path = $"{LobbyArtDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Unlit/Texture"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.mainTexture = tex;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Procedural texture saved once as PNG (point filtered, blocky look).</summary>
        private static Texture2D GenTex(string name, int w, int h, Func<int, int, Color> px)
        {
            string path = $"{LobbyArtDir}/{name}.png";
            if (!File.Exists(path))
            {
                _artGenerated = true;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var c = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    c[y * w + x] = px(x, y);
                t.SetPixels(c);
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject Deco(string name, Transform parent, Vector3 pos, Vector3 size, Material m, Quaternion? rot = null)
        {
            var g = Box(name, parent, pos, size, m);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            if (rot.HasValue) g.transform.rotation = rot.Value;
            g.isStatic = false;
            return g;
        }

        private static GameObject Cyl(string name, Transform parent, Vector3 pos, float diameter, float height, Material m, Quaternion? rot = null, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = new Vector3(diameter, height / 2f, diameter);
            if (rot.HasValue) g.transform.rotation = rot.Value;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        private static GameObject Ball(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        private static GameObject QuadAt(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.SetPositionAndRotation(pos, rot);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = m;
            q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return q;
        }

        private static TextMesh Label3D(string name, Transform parent, Vector3 pos, Quaternion rot, string text, Color color, float charSize, Vector2 fit, FontStyle style = FontStyle.Bold)
        {
            var t = new GameObject(name).AddComponent<TextMesh>();
            t.transform.SetParent(parent, false);
            t.transform.SetPositionAndRotation(pos, rot);
            t.text = text;
            t.characterSize = charSize;
            t.fontSize = 64;
            t.fontStyle = style;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = color;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            AddWorldText(t, fit);
            return t;
        }

        private static Light PointLight(string name, Transform parent, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ================================================================ coffee table

        private static void TableSnacks(Transform p)
        {
            const float top = 0.45f;
            var t = new GameObject("TableSnacks").transform;
            t.SetParent(p, false);

            // Pizza box with a slice next to it.
            var pizzaTop = GenTex("lobby_pizza_box", 32, 32, (x, y) =>
            {
                var card = new Color(0.78f, 0.62f, 0.42f) * (0.92f + 0.08f * Hash01(x * 7 + y * 13));
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                if (d < 9f && d > 6.5f) return new Color(0.75f, 0.12f, 0.08f);
                if (y >= 13 && y <= 18 && x >= 9 && x <= 22 && (x + y) % 3 != 0 && d < 6.5f) return new Color(0.75f, 0.12f, 0.08f);
                return card;
            });
            Deco("PizzaBox", t, new Vector3(-1.9f, top + 0.025f, 0.7f), new Vector3(0.42f, 0.05f, 0.42f), Mat("LobbyCardboard", new Color(0.78f, 0.62f, 0.42f), null, 0.05f, Vector2.one), Quaternion.Euler(0, 12f, 0));
            QuadAt("PizzaPrint", t, new Vector3(-1.9f, top + 0.0505f, 0.7f), Quaternion.Euler(90, 12f, 0), new Vector2(0.4f, 0.4f), Mat("LobbyPizzaPrint", Color.white, pizzaTop, 0.05f, Vector2.one));
            var slice = Mat("LobbyPizzaSlice", new Color(0.95f, 0.62f, 0.2f), GenTex("lobby_pizza", 16, 16, (x, y) =>
                (x * 5 + y * 3) % 11 == 0 ? new Color(0.7f, 0.12f, 0.08f) : (x + y * 2) % 13 == 0 ? new Color(0.25f, 0.45f, 0.15f) : new Color(0.98f, 0.72f, 0.25f)), 0.2f, Vector2.one);
            Deco("PizzaSlice", t, new Vector3(-1.52f, top + 0.008f, 1.08f), new Vector3(0.13f, 0.016f, 0.2f), slice, Quaternion.Euler(0, -38f, 0));
            Deco("Crust", t, new Vector3(-1.46f, top + 0.016f, 1.16f), new Vector3(0.14f, 0.03f, 0.04f), Mat("LobbyCrust", new Color(0.7f, 0.45f, 0.2f), null, 0.1f, Vector2.one), Quaternion.Euler(0, -38f, 0));

            // Drink cans (one tipped over).
            void Can(Vector3 pos, Color c, bool lying)
            {
                var can = Mat("LobbyCan_" + ColorUtility.ToHtmlStringRGB(c), c, GenTex("lobby_can_" + ColorUtility.ToHtmlStringRGB(c), 16, 16, (x, y) =>
                    y > 6 && y < 10 ? Color.white * 0.92f : c * (0.9f + 0.1f * (x % 4 == 0 ? 1f : 0f))), 0.75f, Vector2.one, 0.4f);
                var rot = lying ? Quaternion.Euler(0, 30f, 90f) : Quaternion.identity;
                var body = Cyl("Can", t, pos + (lying ? new Vector3(0, 0.033f, 0) : new Vector3(0, 0.06f, 0)), 0.066f, 0.12f, can, rot);
                var lid = Cyl("CanTop", body.transform, body.transform.position, 1f, 0.04f, metalSilver.Value);
                lid.transform.localPosition = new Vector3(0, 1f, 0);
                lid.transform.localRotation = Quaternion.identity;
                lid.transform.localScale = new Vector3(0.86f, 0.03f, 0.86f);
            }
            Can(new Vector3(-1.08f, top, 0.58f), new Color(0.8f, 0.08f, 0.08f), false);
            Can(new Vector3(-0.98f, top, 0.98f), new Color(0.1f, 0.35f, 0.85f), true);
            Can(new Vector3(-2.15f, top, 1.12f), new Color(0.15f, 0.65f, 0.2f), false);

            // TV remote.
            var black = Mat("LobbyRemote", new Color(0.05f, 0.05f, 0.06f), null, 0.4f, Vector2.one);
            var remote = Deco("Remote", t, new Vector3(-1.32f, top + 0.012f, 0.6f), new Vector3(0.05f, 0.022f, 0.17f), black, Quaternion.Euler(0, 28f, 0));
            Deco("PowerBtn", remote.transform, Vector3.zero, Vector3.one, Mat("LobbyRemoteRed", new Color(0.8f, 0.1f, 0.1f), null, 0.4f, Vector2.one)).transform.localPosition = new Vector3(0, 0.55f, 0.36f);
            remote.transform.Find("PowerBtn").localScale = new Vector3(0.4f, 0.3f, 0.12f);
            for (int i = 0; i < 6; i++)
            {
                var b = Deco("Btn", remote.transform, Vector3.zero, Vector3.one, metalSilver.Value);
                b.transform.localPosition = new Vector3(i % 2 == 0 ? -0.2f : 0.2f, 0.55f, 0.1f - (i / 2) * 0.16f);
                b.transform.localScale = new Vector3(0.28f, 0.3f, 0.08f);
            }

            // Chips bag with a few chips spilled.
            var chipsTex = GenTex("lobby_chips", 16, 24, (x, y) =>
            {
                if (y < 3 || y > 20) return new Color(0.75f, 0.75f, 0.78f);
                if (y > 9 && y < 15 && x > 2 && x < 13) return new Color(0.95f, 0.85f, 0.2f);
                return new Color(0.85f, 0.12f, 0.1f) * (0.9f + 0.1f * ((x + y) % 2));
            });
            Deco("ChipsBag", t, new Vector3(-1.62f, top + 0.03f, 0.45f), new Vector3(0.2f, 0.06f, 0.28f), Mat("LobbyChips", Color.white, chipsTex, 0.7f, Vector2.one), Quaternion.Euler(4f, -70f, 6f));
            var chip = Mat("LobbyChip", new Color(0.95f, 0.78f, 0.35f), null, 0.3f, Vector2.one);
            foreach (var (x, z) in new[] { (-1.4f, 0.42f), (-1.36f, 0.5f), (-1.45f, 0.36f), (-1.3f, 0.4f) })
                Ball("Chip", t, new Vector3(x, top + 0.004f, z), new Vector3(0.04f, 0.008f, 0.035f), chip);
        }

        private static readonly Lazy<Material> metalSilver = new(() => Mat("LobbySilver", new Color(0.78f, 0.78f, 0.8f), null, 0.8f, Vector2.one, 0.9f));

        // ================================================================ arcade

        private static void Arcade(Transform p, Vector3 pos, Material metal)
        {
            var a = new GameObject("ArcadeCabinet").transform;
            a.SetParent(p, false);
            a.position = pos;
            // Front faces -Z (into the room).
            var body = Mat("LobbyArcadeBody", new Color(0.09f, 0.07f, 0.14f), null, 0.4f, Vector2.one);
            var sideArt = GenTex("lobby_arcade_side", 16, 32, (x, y) =>
            {
                float stripe = Mathf.Repeat((x + y) * 0.25f, 1f);
                if (stripe < 0.18f) return new Color(0.9f, 0.2f, 0.6f);
                if (stripe < 0.3f) return new Color(0.2f, 0.7f, 1f);
                return new Color(0.12f, 0.08f, 0.2f);
            });
            var side = Mat("LobbyArcadeSide", Color.white, sideArt, 0.3f, Vector2.one);
            Box("Body", a, pos + new Vector3(0, 0.92f, 0.05f), new Vector3(0.7f, 1.84f, 0.7f), body);
            Deco("SideL", a, pos + new Vector3(-0.37f, 0.95f, 0.02f), new Vector3(0.04f, 1.9f, 0.78f), side);
            Deco("SideR", a, pos + new Vector3(0.37f, 0.95f, 0.02f), new Vector3(0.04f, 1.9f, 0.78f), side);
            // Marquee
            var marq = Emissive("LobbyArcadeMarquee", new Color(1f, 0.85f, 0.3f), new Color(1f, 0.6f, 0.15f) * 1.6f);
            Deco("Marquee", a, pos + new Vector3(0, 1.78f, -0.33f), new Vector3(0.66f, 0.2f, 0.06f), marq);
            Label3D("MarqueeText", a, pos + new Vector3(0, 1.78f, -0.37f), Quaternion.identity, "SNAKE", new Color(0.35f, 0.08f, 0.02f), 0.02f, new Vector2(0.55f, 0.16f));
            // Screen (recessed, tilted back)
            Deco("Bezel", a, pos + new Vector3(0, 1.36f, -0.3f), new Vector3(0.62f, 0.5f, 0.04f), Mat("LobbyArcadeBezel", new Color(0.02f, 0.02f, 0.03f), null, 0.6f, Vector2.one), Quaternion.Euler(-12f, 0, 0));
            var screenMat = Unlit("LobbyArcadeScreen", null);
            var screen = QuadAt("Screen", a, pos + new Vector3(0, 1.36f, -0.325f), Quaternion.Euler(-12f, 0, 0), new Vector2(0.5f, 0.375f), screenMat);
            // Control panel, joystick, buttons
            Deco("Panel", a, pos + new Vector3(0, 1.0f, -0.42f), new Vector3(0.68f, 0.07f, 0.32f), Mat("LobbyArcadePanel", new Color(0.12f, 0.12f, 0.16f), null, 0.5f, Vector2.one), Quaternion.Euler(14f, 0, 0));
            Cyl("Stick", a, pos + new Vector3(-0.15f, 1.1f, -0.45f), 0.025f, 0.12f, metal);
            Ball("StickBall", a, pos + new Vector3(-0.15f, 1.17f, -0.45f), Vector3.one * 0.06f, Mat("LobbyArcadeRed", new Color(0.9f, 0.1f, 0.1f), null, 0.6f, Vector2.one));
            var cols = new[] { new Color(0.95f, 0.15f, 0.15f), new Color(0.2f, 0.5f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.9f, 0.3f) };
            for (int i = 0; i < 4; i++)
                Cyl("Button", a, pos + new Vector3(0.04f + (i % 2) * 0.09f + (i / 2) * 0.04f, 1.08f, -0.47f + (i / 2) * 0.07f), 0.05f, 0.02f,
                    Emissive("LobbyArcadeBtn" + i, cols[i], cols[i] * 0.6f), Quaternion.Euler(14f, 0, 0));
            // Coin door
            Deco("CoinDoor", a, pos + new Vector3(0, 0.55f, -0.31f), new Vector3(0.3f, 0.36f, 0.02f), metal);
            for (int i = 0; i < 2; i++)
                Deco("CoinSlot", a, pos + new Vector3(-0.06f + i * 0.12f, 0.62f, -0.325f), new Vector3(0.06f, 0.08f, 0.01f),
                    Emissive("LobbyCoinSlot", new Color(1f, 0.3f, 0.2f), new Color(1f, 0.2f, 0.1f) * 1.2f));

            var glow = PointLight("ArcadeGlow", a, pos + new Vector3(0, 1.35f, -0.8f), new Color(0.45f, 0.6f, 1f), 0.8f, 2.6f);
            var arc = a.gameObject.AddComponent<LobbyArcade>();
            arc.screen = screen.GetComponent<Renderer>();
            arc.glow = glow;
        }

        // ================================================================ CCTV TV

        private static void WallTv(Transform p, Vector3 pos, Texture2D[] feeds, string[] labels, Material darkWood)
        {
            var t = new GameObject("WallTv").transform;
            t.SetParent(p, false);
            t.position = pos;
            // On the south wall, facing north (+Z, towards the sofa).
            var face = Quaternion.Euler(0, 180, 0);
            var black = Mat("LobbyTvBody", new Color(0.03f, 0.03f, 0.035f), null, 0.7f, Vector2.one);
            Box("TvBody", t, pos, new Vector3(1.56f, 0.92f, 0.06f), black);
            var screenMat = Unlit("LobbyTvScreen", feeds.Length > 0 ? feeds[0] : null);
            var screen = QuadAt("Screen", t, pos + new Vector3(0, 0, 0.032f), face, new Vector2(1.48f, 0.84f), screenMat);
            var overlay = Label3D("Osd", t, pos + new Vector3(0.38f, 0.33f, 0.036f), face, "CAM 01", new Color(1f, 1f, 1f, 0.9f), 0.012f, new Vector2(0.62f, 0.14f));
            overlay.anchor = TextAnchor.MiddleCenter;
            // Warm LED glow behind the TV (strips peeking out around the edge + light on the wall).
            var led = Emissive("LobbyLedWarm", new Color(1f, 0.6f, 0.25f), new Color(1f, 0.55f, 0.2f) * 2.2f);
            Deco("LedTop", t, pos + new Vector3(0, 0.47f, -0.02f), new Vector3(1.5f, 0.015f, 0.015f), led);
            Deco("LedBottom", t, pos + new Vector3(0, -0.47f, -0.02f), new Vector3(1.5f, 0.015f, 0.015f), led);
            Deco("LedL", t, pos + new Vector3(-0.79f, 0, -0.02f), new Vector3(0.015f, 0.88f, 0.015f), led);
            Deco("LedR", t, pos + new Vector3(0.79f, 0, -0.02f), new Vector3(0.015f, 0.88f, 0.015f), led);
            PointLight("TvBacklight", t, pos + new Vector3(0, 0, 0.12f), new Color(1f, 0.55f, 0.25f), 0.45f, 1.6f);
            var glow = PointLight("TvGlow", t, pos + new Vector3(0, 0, 0.7f), new Color(0.6f, 0.9f, 0.7f), 0.55f, 2.8f);
            // Media console below
            Box("MediaConsole", t, new Vector3(pos.x, 0.25f, pos.z + 0.22f), new Vector3(1.8f, 0.5f, 0.4f), darkWood);
            Deco("ConsoleBox", t, new Vector3(pos.x + 0.5f, 0.55f, pos.z + 0.22f), new Vector3(0.36f, 0.08f, 0.28f), black);
            Deco("ConsoleLed", t, new Vector3(pos.x + 0.4f, 0.555f, pos.z + 0.362f), new Vector3(0.02f, 0.01f, 0.005f),
                Emissive("LobbyLedGreen", new Color(0.2f, 1f, 0.3f), new Color(0.2f, 1f, 0.3f) * 2f));

            var tv = t.gameObject.AddComponent<LobbyTv>();
            tv.screen = screen.GetComponent<Renderer>();
            tv.feeds = feeds;
            tv.labels = labels;
            tv.overlay = overlay;
            tv.glow = glow;
        }

        /// <summary>
        /// Renders CCTV-style stills of the parking garage (real map, from spawn points at camera height looking down the
        /// most open direction), brightened + green tinted + vignette. Cached as PNGs; delete them to re-render.
        /// </summary>
        private static Texture2D[] CaptureCctv(out string[] labels)
        {
            const int count = 5, w = 320, h = 180;
            var paths = Enumerable.Range(0, count).Select(i => $"{LobbyArtDir}/lobby_cctv_{i}.png").ToArray();
            var labelPath = $"{LobbyArtDir}/lobby_cctv_labels.txt";
            if (!paths.All(File.Exists) || !File.Exists(labelPath))
            {
                _artGenerated = true;
                var labelList = new List<string>();
                // Rendered in its own (single) scene before the lobby scene is created.
                var opened = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.34f);
                var spawns = opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.CompareTag("Respawn")).OrderBy(t => t.position.y).ThenBy(t => t.position.x).ToList();
                var camGo = new GameObject("CctvCam");
                SceneManager.MoveGameObjectToScene(camGo, opened);
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 70f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.1f;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                for (int i = 0; i < count; i++)
                {
                    var sp = spawns.Count > 0 ? spawns[(int)((i + 0.5f) / count * spawns.Count)] : null;
                    Vector3 pos = (sp != null ? sp.position : new Vector3(i * 10f, 0, 0)) + Vector3.up * 2.75f;
                    // Look along the most open direction, tilted down like a ceiling camera.
                    float bestYaw = 0f, bestDist = -1f;
                    for (int k = 0; k < 16; k++)
                    {
                        float yaw = k * 22.5f;
                        Vector3 dir = Quaternion.Euler(18f, yaw, 0) * Vector3.forward;
                        float d = Physics.Raycast(pos, dir, out var hit, 60f) ? hit.distance : 60f;
                        if (d > bestDist) { bestDist = d; bestYaw = yaw; }
                    }
                    cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(20f, bestYaw, 0));
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    var px = tex.GetPixels();
                    for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[y * w + x];
                        float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                        l = 1f - Mathf.Exp(-l * 5.5f);                         // brighten the dark garage
                        float vx = (x / (float)w - 0.5f) * 2f, vy = (y / (float)h - 0.5f) * 2f;
                        l *= 1f - 0.35f * (vx * vx + vy * vy);                  // vignette
                        l *= y % 3 == 0 ? 0.82f : 1f;                           // scanlines
                        l += (Hash01(x * 31 + y * 17 + i * 999) - 0.5f) * 0.06f; // grain
                        px[y * w + x] = new Color(l * 0.78f, l, l * 0.82f);    // green-ish CCTV tint
                    }
                    tex.SetPixels(px);
                    tex.Apply();
                    File.WriteAllBytes(paths[i], tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    float py = pos.y - 2.75f;
                    string level = py > -1.5f ? "0" : py > -5f ? "-1" : "-2";
                    labelList.Add($"CAM {i + 1:00}  POZIOM {level}");
                }
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
                File.WriteAllLines(labelPath, labelList);
                foreach (var path in paths)
                {
                    AssetDatabase.ImportAsset(path);
                    var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                }
            }
            labels = File.ReadAllLines(labelPath);
            return paths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).ToArray();
        }

        // ================================================================ vending machine

        private static void Vending(Transform p, Vector3 pos, Material metal)
        {
            var v = new GameObject("VendingMachine").transform;
            v.SetParent(p, false);
            v.position = pos;
            // Against the east wall, front facing west (-X). Local layout uses world axes: depth = x, width = z.
            var body = Mat("LobbyVendingBody", new Color(0.75f, 0.1f, 0.12f), null, 0.5f, Vector2.one, 0.2f);
            Box("Body", v, pos + new Vector3(0, 0.98f, 0), new Vector3(0.8f, 1.96f, 1.0f), body);
            float fx = pos.x - 0.405f;
            // Lit window with cans
            var backLit = Emissive("LobbyVendingBack", new Color(0.85f, 0.95f, 1f), new Color(0.7f, 0.85f, 1f) * 1.3f);
            // Lit display case in front of the body: back panel, shelves with cans, frame all around.
            Deco("Window", v, new Vector3(fx - 0.008f, 1.12f, pos.z - 0.12f), new Vector3(0.016f, 1.3f, 0.62f), backLit);
            var canCols = new[] { new Color(0.85f, 0.1f, 0.1f), new Color(0.1f, 0.4f, 0.9f), new Color(0.15f, 0.7f, 0.2f), new Color(1f, 0.6f, 0.1f), new Color(0.6f, 0.2f, 0.8f) };
            for (int r = 0; r < 5; r++)
            {
                Deco("ShelfRow", v, new Vector3(fx - 0.07f, 0.58f + r * 0.25f, pos.z - 0.12f), new Vector3(0.12f, 0.012f, 0.6f), metal);
                for (int c = 0; c < 4; c++)
                {
                    var col = canCols[(r + c) % canCols.Length];
                    Cyl("Can", v, new Vector3(fx - 0.06f, 0.664f + r * 0.25f, pos.z - 0.35f + c * 0.15f), 0.075f, 0.14f,
                        Mat("LobbyVendCan" + ((r + c) % canCols.Length), col, null, 0.7f, Vector2.one, 0.3f));
                }
            }
            // Glass reflection hint + frame
            Deco("FrameTop", v, new Vector3(fx - 0.07f, 1.79f, pos.z - 0.12f), new Vector3(0.14f, 0.04f, 0.68f), metal);
            Deco("FrameBottom", v, new Vector3(fx - 0.07f, 0.45f, pos.z - 0.12f), new Vector3(0.14f, 0.04f, 0.68f), metal);
            Deco("FrameL", v, new Vector3(fx - 0.07f, 1.12f, pos.z - 0.46f), new Vector3(0.14f, 1.38f, 0.03f), metal);
            Deco("FrameR", v, new Vector3(fx - 0.07f, 1.12f, pos.z + 0.22f), new Vector3(0.14f, 1.38f, 0.03f), metal);
            // Logo strip
            var sign = Emissive("LobbyVendingSign", new Color(1f, 1f, 1f), new Color(1f, 0.95f, 0.85f) * 1.5f);
            Deco("Sign", v, new Vector3(fx - 0.005f, 1.9f, pos.z), new Vector3(0.02f, 0.12f, 0.9f), sign);
            Label3D("SignText", v, new Vector3(fx - 0.02f, 1.9f, pos.z), Quaternion.Euler(0, 90, 0), "ZIMNE NAPOJE", new Color(0.75f, 0.05f, 0.05f), 0.016f, new Vector2(0.8f, 0.1f));
            // Keypad + coin slot + tray
            var dark = Mat("LobbyVendingPanel", new Color(0.08f, 0.08f, 0.09f), null, 0.5f, Vector2.one);
            Deco("Keypad", v, new Vector3(fx - 0.005f, 1.25f, pos.z + 0.35f), new Vector3(0.02f, 0.5f, 0.2f), dark);
            for (int i = 0; i < 9; i++)
                Deco("Key", v, new Vector3(fx - 0.018f, 1.38f - (i / 3) * 0.07f, pos.z + 0.29f + (i % 3) * 0.06f), new Vector3(0.012f, 0.045f, 0.045f), metalSilver.Value);
            Deco("Display", v, new Vector3(fx - 0.018f, 1.45f + 0.02f, pos.z + 0.35f), new Vector3(0.01f, 0.05f, 0.15f), Emissive("LobbyVendDisplay", new Color(0.2f, 1f, 0.4f), new Color(0.2f, 1f, 0.4f) * 1.5f));
            Deco("CoinSlot", v, new Vector3(fx - 0.018f, 1.05f, pos.z + 0.35f), new Vector3(0.012f, 0.08f, 0.03f), metalSilver.Value);
            Deco("Tray", v, new Vector3(fx - 0.01f, 0.25f, pos.z - 0.12f), new Vector3(0.04f, 0.2f, 0.55f), dark);
            PointLight("VendingGlow", v, new Vector3(fx - 0.6f, 1.2f, pos.z), new Color(0.65f, 0.85f, 1f), 0.9f, 3f);
        }

        // ================================================================ clock / fan

        private static void WallClock(Transform p, Vector3 pos)
        {
            var c = new GameObject("WallClock").transform;
            c.SetParent(p, false);
            c.position = pos;
            var faceRot = Quaternion.Euler(90, 0, 0); // cylinder axis along Z
            Cyl("Rim", c, pos + new Vector3(0, 0, 0.0f), 0.48f, 0.05f, Mat("LobbyClockRim", new Color(0.08f, 0.08f, 0.09f), null, 0.6f, Vector2.one), faceRot);
            Cyl("Face", c, pos + new Vector3(0, 0, -0.012f), 0.43f, 0.04f, Mat("LobbyClockFace", new Color(0.93f, 0.92f, 0.86f), null, 0.3f, Vector2.one), faceRot);
            var tickMat = Mat("LobbyClockTick", new Color(0.05f, 0.05f, 0.05f), null, 0.2f, Vector2.one);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                bool big = i % 3 == 0;
                Deco("Tick", c, pos + new Vector3(Mathf.Sin(a) * 0.175f, Mathf.Cos(a) * 0.175f, -0.035f), new Vector3(0.012f, big ? 0.045f : 0.025f, 0.005f), tickMat,
                    Quaternion.Euler(0, 0, -i * 30f));
            }
            Transform Hand(string n, float len, float width, float z, Material m)
            {
                var pivot = new GameObject(n).transform;
                pivot.SetParent(c, false);
                pivot.position = pos + new Vector3(0, 0, z);
                var h = Deco(n + "Mesh", pivot, Vector3.zero, Vector3.one, m);
                h.transform.localPosition = new Vector3(0, len * 0.4f, 0);
                h.transform.localScale = new Vector3(width, len, 0.006f);
                return pivot;
            }
            var lc = c.gameObject.AddComponent<LobbyClock>();
            lc.hour = Hand("HourHand", 0.11f, 0.018f, -0.04f, tickMat);
            lc.minute = Hand("MinuteHand", 0.16f, 0.012f, -0.045f, tickMat);
            lc.second = Hand("SecondHand", 0.17f, 0.005f, -0.05f, Mat("LobbyClockRed", new Color(0.85f, 0.1f, 0.08f), null, 0.3f, Vector2.one));
            Cyl("Cap", c, pos + new Vector3(0, 0, -0.055f), 0.025f, 0.01f, tickMat, faceRot);
        }

        private static void CeilingFan(Transform p, Vector3 ceilingPoint, Material wood, Material metal)
        {
            var f = new GameObject("CeilingFan").transform;
            f.SetParent(p, false);
            f.position = ceilingPoint;
            Cyl("Mount", f, ceilingPoint + new Vector3(0, -0.03f, 0), 0.16f, 0.06f, metal);
            Cyl("Rod", f, ceilingPoint + new Vector3(0, -0.25f, 0), 0.03f, 0.4f, metal);
            Cyl("Motor", f, ceilingPoint + new Vector3(0, -0.48f, 0), 0.24f, 0.12f, metal);
            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(f, false);
            rotor.position = ceilingPoint + new Vector3(0, -0.52f, 0);
            for (int i = 0; i < 4; i++)
            {
                var arm = new GameObject("BladeArm").transform;
                arm.SetParent(rotor, false);
                arm.localRotation = Quaternion.Euler(0, i * 90f, 0);
                var blade = Deco("Blade", arm, Vector3.zero, Vector3.one, wood);
                blade.transform.localPosition = new Vector3(0, 0, 0.45f);
                blade.transform.localRotation = Quaternion.Euler(0, 0, 10f);
                blade.transform.localScale = new Vector3(0.15f, 0.014f, 0.62f);
            }
            Cyl("LightBowl", f, ceilingPoint + new Vector3(0, -0.6f, 0), 0.18f, 0.08f,
                Emissive("LobbyFanBowl", new Color(1f, 0.9f, 0.7f), new Color(1f, 0.8f, 0.55f) * 1.2f));
            var sp = rotor.gameObject.AddComponent<LobbySpinner>();
            sp.degreesPerSecond = 75f;
        }

        // ================================================================ LEDs behind sofas

        private static void SofaLeds(Transform p)
        {
            var led = Emissive("LobbyLedWarm", new Color(1f, 0.6f, 0.25f), new Color(1f, 0.55f, 0.2f) * 2.2f);
            var t = new GameObject("SofaLeds").transform;
            t.SetParent(p, false);
            // North sofa: centre (-1.5, 3.6), backrest at z ~4.08
            Deco("LedSofaN", t, new Vector3(-1.5f, 0.06f, 4.12f), new Vector3(2.3f, 0.02f, 0.02f), led);
            PointLight("LedGlowN", t, new Vector3(-1.5f, 0.35f, 4.45f), new Color(1f, 0.55f, 0.22f), 0.75f, 2.4f);
            // West sofa: centre (-5.6, 0.5), backrest at x ~-6.08
            Deco("LedSofaW", t, new Vector3(-6.12f, 0.06f, 0.5f), new Vector3(0.02f, 0.02f, 2.3f), led);
            PointLight("LedGlowW", t, new Vector3(-6.45f, 0.35f, 0.5f), new Color(1f, 0.55f, 0.22f), 0.75f, 2.4f);
        }

        // ================================================================ cork board

        private static void CorkBoard(Transform p, Vector3 pos, Texture2D[] photos, Material trim)
        {
            var b = new GameObject("CorkBoard").transform;
            b.SetParent(p, false);
            b.position = pos;
            var wallFace = Quaternion.Euler(0, 90, 0); // readable when looking +X (east wall, facing west)
            var cork = Mat("LobbyCork", Color.white, GenTex("lobby_cork", 64, 48, (x, y) =>
            {
                float n = Hash01(x * 13 + y * 71);
                var c = new Color(0.62f, 0.42f, 0.24f) * (0.82f + 0.3f * n);
                if (n > 0.93f) c = new Color(0.35f, 0.22f, 0.12f);
                return c;
            }), 0.1f, Vector2.one);
            Box("Frame", b, pos + new Vector3(0.01f, 0, 0), new Vector3(0.04f, 1.12f, 1.72f), trim);
            Deco("Cork", b, pos + new Vector3(-0.012f, 0, 0), new Vector3(0.02f, 1.0f, 1.6f), cork);
            float fx = pos.x - 0.025f;
            var pin = Mat("LobbyPin", new Color(0.85f, 0.1f, 0.1f), null, 0.6f, Vector2.one);
            void Pin(Vector3 at) => Ball("Pin", b, at + new Vector3(-0.012f, 0, 0), Vector3.one * 0.022f, pin);

            // CCTV photos (polaroids)
            var paper = Mat("LobbyPaper", new Color(0.95f, 0.94f, 0.9f), null, 0.1f, Vector2.one);
            var photoSpots = new[] { (0.22f, -0.55f, -6f), (0.3f, -0.15f, 4f), (-0.18f, -0.42f, 3f) };
            for (int i = 0; i < photoSpots.Length && i < photos.Length; i++)
            {
                var (y, z, rot) = photoSpots[i];
                var at = new Vector3(fx, pos.y + y, pos.z + z);
                Deco("Polaroid", b, at, new Vector3(0.006f, 0.26f, 0.3f), paper, Quaternion.Euler(rot, 0, 0));
                QuadAt("Photo", b, at + new Vector3(-0.004f, 0.02f, 0), Quaternion.Euler(rot, 0, 0) * wallFace, new Vector2(0.26f, 0.15f),
                    Mat("LobbyPhoto" + i, Color.white, photos[i], 0.2f, Vector2.one));
                Pin(at + new Vector3(0, 0.11f, 0));
            }
            // Red yarn between the photos
            var yarn = Mat("LobbyYarn", new Color(0.8f, 0.05f, 0.05f), null, 0.2f, Vector2.one);
            void Yarn(Vector3 a, Vector3 c)
            {
                var mid = (a + c) * 0.5f;
                var d = c - a;
                var g = Deco("Yarn", b, mid + new Vector3(-0.016f, 0, 0), new Vector3(0.004f, 0.006f, d.magnitude), yarn);
                g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.right);
            }
            Yarn(new Vector3(fx, pos.y + 0.33f, pos.z - 0.55f), new Vector3(fx, pos.y + 0.41f, pos.z - 0.15f));
            Yarn(new Vector3(fx, pos.y + 0.41f, pos.z - 0.15f), new Vector3(fx, pos.y - 0.07f, pos.z - 0.42f));

            // MISSING posters
            var face = GenTex("lobby_missing_face", 8, 8, (x, y) =>
            {
                if (y >= 6) return new Color(0.25f, 0.16f, 0.08f);                // hair
                if (y == 4 && (x == 2 || x == 5)) return Color.white;            // eyes
                if (y == 4 && (x == 3 || x == 4)) return new Color(0.75f, 0.55f, 0.42f);
                if (y == 2 && x >= 3 && x <= 4) return new Color(0.45f, 0.25f, 0.2f);
                return new Color(0.82f, 0.62f, 0.48f);
            });
            var faceMat = Mat("LobbyMissingFace", Color.white, face, 0.1f, Vector2.one);
            foreach (var (z, nick) in new[] { (0.32f, "cendy"), (0.62f, "Kadrixter") })
            {
                var at = new Vector3(fx, pos.y + 0.05f, pos.z + z);
                Deco("Poster", b, at, new Vector3(0.006f, 0.42f, 0.28f), paper, Quaternion.Euler(z > 0.5f ? 3f : -2f, 0, 0));
                Label3D("Missing", b, at + new Vector3(-0.006f, 0.16f, 0), wallFace, "ZAGINĄŁ", new Color(0.8f, 0.05f, 0.05f), 0.01f, new Vector2(0.24f, 0.06f));
                QuadAt("Face", b, at + new Vector3(-0.005f, 0.03f, 0), wallFace, new Vector2(0.14f, 0.14f), faceMat);
                Label3D("Info", b, at + new Vector3(-0.006f, -0.12f, 0), wallFace, $"{nick}\nostatnio widziany:\nparking, poziom -2", new Color(0.1f, 0.1f, 0.1f), 0.006f, new Vector2(0.24f, 0.12f), FontStyle.Normal);
                Pin(at + new Vector3(0, 0.19f, 0));
            }
            // Sticky notes
            var sticky = Mat("LobbySticky", new Color(1f, 0.9f, 0.3f), null, 0.1f, Vector2.one);
            var notes = new[] { (-0.3f, 0.05f, "winda\nnie działa?!"), (0.36f, 0.25f, "nie chodź\nsam na -2"), (-0.36f, 0.62f, "kto ma\npiłę??") };
            foreach (var (y, z, txt) in notes)
            {
                var at = new Vector3(fx, pos.y + y, pos.z + z);
                Deco("Note", b, at, new Vector3(0.004f, 0.13f, 0.13f), sticky, Quaternion.Euler(Hash01((int)(z * 100)) * 10f - 5f, 0, 0));
                Label3D("NoteText", b, at + new Vector3(-0.005f, 0, 0), wallFace, txt, new Color(0.15f, 0.15f, 0.4f), 0.005f, new Vector2(0.11f, 0.1f), FontStyle.Normal);
            }
        }

        // ================================================================ bin, rugs, cushions

        private static void TrashBin(Transform p, Vector3 pos, Material metal)
        {
            var t = new GameObject("TrashBin").transform;
            t.SetParent(p, false);
            Cyl("Bin", t, pos + new Vector3(0, 0.28f, 0), 0.36f, 0.56f, Mat("LobbyBin", new Color(0.18f, 0.2f, 0.22f), null, 0.5f, Vector2.one, 0.6f), null, true);
            Cyl("BinInside", t, pos + new Vector3(0, 0.555f, 0), 0.32f, 0.01f, Mat("LobbyBinInside", new Color(0.02f, 0.02f, 0.02f), null, 0f, Vector2.one));
            var paper = Mat("LobbyCrumpled", new Color(0.92f, 0.91f, 0.86f), null, 0.05f, Vector2.one);
            var rnd = new System.Random(11);
            foreach (var off in new[] { new Vector3(0.02f, 0.58f, 0.04f), new Vector3(-0.06f, 0.6f, -0.03f), new Vector3(-0.38f, 0.04f, 0.2f), new Vector3(-0.3f, 0.04f, -0.32f), new Vector3(-0.5f, 0.04f, -0.05f), new Vector3(0.08f, 0.04f, 0.38f) })
            {
                float s = 0.07f + (float)rnd.NextDouble() * 0.03f;
                var g = Ball("Paper", t, pos + off, new Vector3(s, s * 0.85f, s * 1.1f), paper);
                g.transform.rotation = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, 0);
            }
        }

        private static void Rugs(Transform p)
        {
            var t = new GameObject("Rugs").transform;
            t.SetParent(p, false);
            var round = Mat("LobbyRugRound", Color.white, GenTex("lobby_rug_round", 32, 32, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                Color a = new Color(0.12f, 0.2f, 0.32f), bcol = new Color(0.7f, 0.55f, 0.25f);
                return (int)(d / 3f) % 2 == 0 ? a : bcol;
            }), 0f, Vector2.one);
            Cyl("RugArcade", t, new Vector3(6.0f, 0.006f, 4.3f), 1.5f, 0.01f, round);
            var runner = Mat("LobbyRugRunner", Color.white, GenTex("lobby_rug_runner", 16, 48, (x, y) =>
            {
                bool border = x < 2 || x > 13 || y < 2 || y > 45;
                if (border) return new Color(0.75f, 0.6f, 0.3f);
                return ((x + y) / 4) % 2 == 0 ? new Color(0.35f, 0.08f, 0.08f) : new Color(0.25f, 0.06f, 0.06f);
            }), 0f, Vector2.one);
            Deco("RugRunner", t, new Vector3(1.2f, 0.006f, -3.6f), new Vector3(1.3f, 0.012f, 3.4f), runner, Quaternion.Euler(0, 90, 0));
        }

        /// <summary>Softens a sofa: rounded arm tops and backrest, split seat cushions, two throw pillows.</summary>
        private static void SofaCushions(Transform sofa, Material fabric, Material pillowA, Material pillowB)
        {
            // Local space of Sofa(): seat 2.4 x 0.4 x 0.9 at y 0.25, back at z 0.38, arms at x +-1.15.
            void LocalCyl(string n, Vector3 lp, float dia, float len, Vector3 euler)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = n;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(sofa, false);
                g.transform.localPosition = lp;
                g.transform.localRotation = Quaternion.Euler(euler);
                g.transform.localScale = new Vector3(dia, len / 2f, dia);
                g.GetComponent<MeshRenderer>().sharedMaterial = fabric;
            }
            LocalCyl("ArmRollL", new Vector3(-1.15f, 0.8f, 0f), 0.22f, 0.9f, new Vector3(90, 0, 0));
            LocalCyl("ArmRollR", new Vector3(1.15f, 0.8f, 0f), 0.22f, 0.9f, new Vector3(90, 0, 0));
            LocalCyl("BackRoll", new Vector3(0f, 1.05f, 0.38f), 0.22f, 2.4f, new Vector3(0, 0, 90));
            for (int i = 0; i < 2; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "SeatCushion";
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.SetParent(sofa, false);
                c.transform.localPosition = new Vector3(i == 0 ? -0.53f : 0.53f, 0.48f, -0.04f);
                c.transform.localScale = new Vector3(1.02f, 0.1f, 0.78f);
                c.GetComponent<MeshRenderer>().sharedMaterial = fabric;
            }
            for (int i = 0; i < 2; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Pillow";
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.SetParent(sofa, false);
                c.transform.localPosition = new Vector3(i == 0 ? -0.82f : 0.82f, 0.72f, 0.2f);
                c.transform.localRotation = Quaternion.Euler(-18f, i == 0 ? 12f : -12f, i == 0 ? 8f : -8f);
                c.transform.localScale = new Vector3(0.42f, 0.4f, 0.13f);
                c.GetComponent<MeshRenderer>().sharedMaterial = i == 0 ? pillowA : pillowB;
            }
        }
    }
}
