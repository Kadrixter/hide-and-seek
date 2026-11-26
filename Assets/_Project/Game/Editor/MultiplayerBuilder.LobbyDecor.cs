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
    /// behind the sofas and the TV, cork board (CCTV photos, notes, MISSING posters), cushions, trash bin.
    /// Room and positions: <see cref="LobbyLayout"/>.
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
            deco.Find("TableSnacks").position = LobbyLayout.TableShift; // built for the old table centre
            for (int i = 0; i < LobbyLayout.Arcades.Length; i++)
                Arcade(deco, LobbyLayout.Arcades[i].pos, LobbyLayout.Arcades[i].yaw, i, metal);
            Arcade(deco, LobbyLayout.LinkArcade.pos, LobbyLayout.LinkArcade.yaw, -1, metal); // KABELKI: the dot link puzzle
            // CCTV TV over the fireplace (built facing +Z, turned to face the room from the north wall)
            var tvPos = new Vector3(LobbyLayout.Fireplace.x, LobbyLayout.TvCenterY, -LobbyLayout.HalfD + 0.06f);
            WallTv(deco, tvPos, cctv, cctvLabels, darkWood, false, LobbyLayout.TvScale);
            var tv = deco.Find("WallTv");
            tv.RotateAround(new Vector3(tvPos.x, 0f, 0f), Vector3.up, 180f);
            Vending(deco, LobbyLayout.Vending, metal);
            WallClock(deco, LobbyLayout.WallClock);
            CorkBoard(deco, LobbyLayout.CorkBoard, cctv, trim);
            TrashBin(deco, LobbyLayout.TrashBin); // MultiplayerBuilder.TrashBin
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

        private static readonly Lazy<Material> metalSilver = new(() => Mat("LobbySilver", new Color(0.78f, 0.78f, 0.8f), null, 0.8f, Vector2.one, 0.9f));

        // ================================================================ arcade

        /// <param name="cab">SNAKE cabinet index, or -1 for the KABELKI cabinet (HideAndSeek.Game.DotLink.DotLinkArcade).</param>
        private static void Arcade(Transform p, Vector3 pos, float yaw, int cab, Material metal)
        {
            bool link = cab < 0;
            var a = new GameObject(link ? "ArcadeCabinetLink" : cab == 0 ? "ArcadeCabinet" : "ArcadeCabinet" + (cab + 1)).transform;
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
            var screen = ArcadeScreen(a, pos);
            ArcadeControls(a, pos, metal);   // panel with art, joystick, buttons (MultiplayerBuilder.LobbyArcade.cs)
            ArcadeTrim(a, pos, metal, body); // coin door, bevels, kick plate, marquee frame

            var glowColor = link ? new Color(0.5f, 0.9f, 0.62f) : cab == 0 ? new Color(0.45f, 0.6f, 1f) : new Color(1f, 0.45f, 0.75f);
            var glow = PointLight("ArcadeGlow", a, pos + new Vector3(0, 1.35f, -0.8f), glowColor, 0.7f, 2.4f);
            ArcadeDetails(a, pos, metal);
            if (link)
            {
                var dl = a.gameObject.AddComponent<HideAndSeek.Game.DotLink.DotLinkArcade>();
                dl.screen = screen.GetComponent<Renderer>();
                dl.glow = glow;
            }
            else
            {
                var arc = a.gameObject.AddComponent<LobbyArcade>();
                arc.screen = screen.GetComponent<Renderer>();
                arc.glow = glow;
                arc.cab = cab;
            }
            a.rotation = Quaternion.Euler(0f, yaw, 0f); // built facing -Z around its origin
        }

        // ================================================================ CCTV TV

        /// <param name="k">Size factor on width / height of everything (the depth stays: a thin wall-mounted set).</param>
        private static void WallTv(Transform p, Vector3 pos, Texture2D[] feeds, string[] labels, Material darkWood, bool console = true, float k = 1f)
        {
            var t = new GameObject("WallTv").transform;
            t.SetParent(p, false);
            t.position = pos;
            // On the south wall, facing north (+Z, towards the sofa).
            var face = Quaternion.Euler(0, 180, 0);
            var black = Mat("LobbyTvBody", new Color(0.03f, 0.03f, 0.035f), null, 0.7f, Vector2.one);
            Box("TvBody", t, pos, new Vector3(1.56f * k, 0.92f * k, 0.06f), black);
            var screenMat = Unlit("LobbyTvScreen", feeds.Length > 0 ? feeds[0] : null);
            var screen = QuadAt("Screen", t, pos + new Vector3(0, 0, 0.032f), face, new Vector2(1.48f, 0.84f) * k, screenMat);
            var overlay = Label3D("Osd", t, pos + new Vector3(0.38f * k, 0.33f * k, 0.036f), face, "CAM 01", new Color(1f, 1f, 1f, 0.9f), 0.012f * k, new Vector2(0.62f, 0.14f) * k);
            overlay.anchor = TextAnchor.MiddleCenter;
            // Warm LED glow behind the TV (strips peeking out around the edge + light on the wall).
            var led = Emissive("LobbyLedWarm", new Color(1f, 0.6f, 0.25f), new Color(1f, 0.55f, 0.2f) * 2.2f);
            float hx = 0.79f * k, hy = 0.47f * k;
            Deco("LedTop", t, pos + new Vector3(0, hy, -0.02f), new Vector3(1.5f * k, 0.015f, 0.015f), led);
            Deco("LedBottom", t, pos + new Vector3(0, -hy, -0.02f), new Vector3(1.5f * k, 0.015f, 0.015f), led);
            Deco("LedL", t, pos + new Vector3(-hx, 0, -0.02f), new Vector3(0.015f, 0.88f * k, 0.015f), led);
            Deco("LedR", t, pos + new Vector3(hx, 0, -0.02f), new Vector3(0.015f, 0.88f * k, 0.015f), led);
            PointLight("TvBacklight", t, pos + new Vector3(0, 0, 0.12f), new Color(1f, 0.55f, 0.25f), 0.45f, 1.6f * Mathf.Sqrt(k) + 0.4f * (k - 1f));
            var glow = PointLight("TvGlow", t, pos + new Vector3(0, 0, 0.7f + 0.2f * (k - 1f)), new Color(0.6f, 0.9f, 0.7f), 0.55f, 2.8f * Mathf.Sqrt(k));
            // Media console below
            if (console) Box("MediaConsole", t, new Vector3(pos.x, 0.25f, pos.z + 0.22f), new Vector3(1.8f, 0.5f, 0.4f), darkWood);
            if (console) Deco("ConsoleBox", t, new Vector3(pos.x + 0.5f, 0.55f, pos.z + 0.22f), new Vector3(0.36f, 0.08f, 0.28f), black);
            if (console) Deco("ConsoleLed", t, new Vector3(pos.x + 0.4f, 0.555f, pos.z + 0.362f), new Vector3(0.02f, 0.01f, 0.005f),
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
            // Only the collider stays in the scene; the detailed machine (cabinet, glass door, spirals, cans, keypad,
            // delivery bin) is built at runtime by Game/Lobby/Vending/VendingMachineModel. The body material is the
            // Standard template the runtime model copies.
            var v = new GameObject("VendingMachine").transform;
            v.SetParent(p, false);
            v.position = pos;
            var body = Mat("LobbyVendingBody", new Color(0.75f, 0.1f, 0.12f), null, 0.5f, Vector2.one, 0.2f);
            var box = Box("Body", v, pos + new Vector3(0, 0.98f, 0), new Vector3(0.8f, 1.96f, 1.0f), body);
            box.isStatic = false;
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        // ================================================================ clock

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

        // ================================================================ LEDs behind sofas

        private static void SofaLeds(Transform p, Transform root)
        {
            var led = Emissive("LobbyLedWarm", new Color(1f, 0.6f, 0.25f), new Color(1f, 0.55f, 0.2f) * 2.2f);
            var t = new GameObject("SofaLeds").transform;
            t.SetParent(p, false);
            // warm strip under the back of every sofa (sofa space: backrest at local +Z) and its glow on the floor
            foreach (Transform s in root)
            {
                if (s.name != "Sofa") continue;
                Deco("LedSofa", t, s.TransformPoint(new Vector3(0f, 0.06f, 0.52f)), new Vector3(2.3f, 0.02f, 0.02f), led, s.rotation);
                PointLight("LedGlow", t, s.TransformPoint(new Vector3(0f, 0.3f, 0.85f)), new Color(1f, 0.55f, 0.22f), 0.6f, 2.2f);
            }
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

        // ================================================================ rugs, cushions

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
