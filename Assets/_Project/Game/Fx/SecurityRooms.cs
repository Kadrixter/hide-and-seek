using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Hidden "security" rooms on levels 0, -1 and -2 (north-east open area, against the north wall). The doorway is
    /// blocked by a heavy crate: E pushes it aside (synced, <see cref="MatchManager.SecurityCrates"/>), then the room
    /// can be entered; a SECURITY sign hangs over the door. Each has 6 monitors with live feeds of the garage cameras (aisles, cross aisles, stairwell doors,
    /// elevator doors) and three buttons that choose which level the cameras show. The chosen level is synced by the
    /// server (<see cref="MatchManager.SecurityFloor"/>), so every player sees the same monitors. A monitor switches to
    /// a camera that detects motion, with a click and old-TV snow. Feeds render only while somebody is near a room.
    /// </summary>
    public class SecurityRooms : MonoBehaviour
    {
        public static SecurityRooms Instance { get; private set; }

        // Room footprint (same on every level): x [66, 78], z [52, 59.8] (north-east open area); door in the south wall.
        private const float X0 = 66f, X1 = 78f, Z0 = 52f, Z1 = 59.8f, Wall = 0.2f, Height = 2.95f;
        private const float DoorA = X0 + 10.2f, DoorB = X0 + 11.4f;   // entrance gap (x) near the east end

        // Crate in front of the door (outside): pushed 1.6 m west by a player standing east of it.
        public const float CratePushTime = 1.9f;      // whole animation (hands on, push, let go)
        private const float CrateMoveFrom = 0.35f, CrateMoveTo = 1.6f;
        private static readonly Vector3 CrateSize = new Vector3(1.45f, 1.5f, 1.05f);
        private const float CrateShift = 1.65f;
        private readonly Transform[] _crates = new Transform[Levels];
        private readonly float[] _crateT = { 0f, 0f, 0f };   // 0 closed .. 1 pushed aside
        private readonly float[] _pushStart = { -1f, -1f, -1f };
        private AudioClip _scrapeClip;
        private const int Levels = 3, Monitors = 6;
        private const float FeedFps = 30f, MotionRange = 36f, MotionHalfAngle = 33f, MotionMinSpeed = 0.5f;
        private const float SnowTime = 0.45f;

        private struct CamPoint
        {
            public Vector3 Pos;      // world
            public Quaternion Rot;
            public int Level;
            public string Name;
            public float LastMotion;
            public Renderer Led;     // red LED on the physical camera
        }

        private sealed class Slot
        {
            public RenderTexture Rt;
            public Camera Cam;
            public Light Ir;
            public int CamIndex = -1;       // index into _cams
            public float Since = -99f;
            public float NextRender;
            public readonly List<Material> Mats = new();
            public readonly List<TextMesh> Labels = new();
            public readonly Vector3[] ScreenPos = new Vector3[Levels]; // monitor centre in the room of each level
        }

        private readonly List<CamPoint> _cams = new();
        private readonly List<int>[] _levelCams = { new(), new(), new() };
        private readonly Slot[] _slots = new Slot[Monitors];
        private readonly List<(Vector3 pos, int floor)> _buttons = new();
        private readonly List<Renderer> _buttonRenderers = new();
        private readonly List<TextMesh> _headers = new();
        private readonly Dictionary<ulong, Vector3> _prev = new();
        private readonly List<(Transform hour, Transform minute)> _clocks = new();
        private readonly List<Transform> _fans = new();
        private readonly MaterialPropertyBlock _ledMpb = new();
        private Material _ledMat;
        private int _floor;                 // level whose cameras the monitors show (0, 1, 2 = levels 0, -1, -2)
        private float _nextSense, _lastSense;
        private Shader _monitorShader;
        private Font _font;
        private Material _fontMat;
        private AudioClip _switchClip, _floorClip;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("SecurityRooms");
            Instance = go.AddComponent<SecurityRooms>();
            Instance.Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var s in _slots)
                if (s != null && s.Rt != null) s.Rt.Release();
        }

        public static string LevelName(int lv) => lv == 0 ? "0" : "-" + lv;
        private static float FloorY(int lv) => -3.4f * lv;
        public static Vector3 RoomFloorPoint(int level) => new Vector3((X0 + X1) * 0.5f + 1.5f, FloorY(level), 56.6f);

        // ================================================================ crate

        private static Vector3 CrateClosed(int lv) => new Vector3((DoorA + DoorB) * 0.5f, FloorY(lv) + CrateSize.y * 0.5f, Z0 - Wall * 0.5f - CrateSize.z * 0.5f - 0.03f);
        private static Vector3 CrateOpen(int lv) => CrateClosed(lv) + Vector3.left * CrateShift;

        /// <summary>Where the pusher stands (east of the crate, facing west) and how far he walks.</summary>
        public static Vector3 PushStand(int lv) => CrateClosed(lv) + new Vector3(CrateSize.x * 0.5f + 0.42f, -CrateSize.y * 0.5f, 0f);
        public static Vector3 PushEnd(int lv) => PushStand(lv) + Vector3.left * CrateShift;
        public const float PushYaw = 270f;

        public bool CrateOpenNow(int lv) => _crateT[lv] >= 0.999f;

        /// <summary>Closed crate in reach and in view, for the interaction prompt.</summary>
        public bool TryFindCrate(Vector3 eye, Vector3 fwd, out Vector3 world, out int level, out float dist)
        {
            world = default; level = -1; dist = float.MaxValue;
            var mm = MatchManager.Instance;
            for (int lv = 0; lv < Levels; lv++)
            {
                if (_crates[lv] == null || (mm != null && (mm.SecurityCrates.Value & (1 << lv)) != 0)) continue;
                Vector3 c = _crates[lv].position;
                float d = Vector3.Distance(eye, c);
                if (d > 2.6f || d >= dist || Mathf.Abs(eye.y - 1.6f - FloorY(lv)) > 1.5f) continue;
                if (Vector3.Angle(fwd, c - eye) > 40f) continue;
                dist = d; world = c; level = lv;
            }
            return level >= 0;
        }

        /// <summary>Server check: <paramref name="feet"/> is next to the crate of level <paramref name="lv"/>.</summary>
        public static bool IsNearCrate(Vector3 feet, int lv)
        {
            if (lv < 0 || lv >= Levels) return false;
            Vector3 c = CrateClosed(lv);
            return Mathf.Abs(feet.y - FloorY(lv)) < 1.2f && Vector3.Distance(new Vector3(feet.x, 0f, feet.z), new Vector3(c.x, 0f, c.z)) < 3.2f;
        }

        /// <summary>Every client: the crate of <paramref name="lv"/> starts sliding now (push animation).</summary>
        public void BeginPush(int lv)
        {
            if (lv < 0 || lv >= Levels || _crateT[lv] > 0f) return;
            _pushStart[lv] = Time.time;
        }

        private void UpdateCrates()
        {
            var mm = MatchManager.Instance;
            int mask = mm != null ? mm.SecurityCrates.Value : 0;
            for (int lv = 0; lv < Levels; lv++)
            {
                if (_crates[lv] == null) continue;
                bool open = (mask & (1 << lv)) != 0;
                float target = _crateT[lv];
                if (!open) { target = 0f; _pushStart[lv] = -1f; }
                else if (_pushStart[lv] < 0f) target = 1f; // joined late / missed the push: just open
                else
                {
                    float k = Mathf.InverseLerp(CrateMoveFrom, CrateMoveTo, Time.time - _pushStart[lv]);
                    if (_crateT[lv] <= 0f && k > 0f && _scrapeClip != null) GameAudio.PlayClipAt(_scrapeClip, _crates[lv].position, 1f, 25f);
                    target = k;
                }
                if (Mathf.Abs(target - _crateT[lv]) < 1e-4f && target != 0f && target != 1f) continue;
                _crateT[lv] = Mathf.Clamp01(target);
                // Eased slide with a little wobble (heavy wooden crate on concrete).
                float e = _crateT[lv] * _crateT[lv] * (3f - 2f * _crateT[lv]);
                float wob = Mathf.Sin(_crateT[lv] * Mathf.PI * 7f) * 1.2f * (1f - Mathf.Abs(_crateT[lv] * 2f - 1f));
                _crates[lv].SetPositionAndRotation(Vector3.Lerp(CrateClosed(lv), CrateOpen(lv), e), Quaternion.Euler(0f, wob, wob * 0.3f));
            }
        }

        /// <summary>Wood scraping over concrete: low rumbling noise bursts, synthesised once.</summary>
        private static AudioClip MakeScrapeClip()
        {
            const int rate = 44100;
            float length = CrateMoveTo - CrateMoveFrom + 0.15f;
            int n = Mathf.CeilToInt(length * rate);
            var d = new float[n];
            var rnd = new System.Random(77);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.2f;
                float judder = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * (11f + 3f * Mathf.Sin(t * 5f)) * t));
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((length - t) / 0.25f);
                float creak = 0.18f * Mathf.Sin(2f * Mathf.PI * (180f + 40f * Mathf.Sin(t * 9f)) * t) * Mathf.Clamp01(Mathf.Sin(t * 13f));
                d[i] = Mathf.Clamp((lp2 * 5.5f * judder + creak) * env, -1f, 1f);
            }
            var clip = AudioClip.Create("CrateScrape", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        // ================================================================ interaction

        /// <summary>Floor button in reach and in view, for the interaction prompt.</summary>
        public bool TryFindButton(Vector3 eye, Vector3 fwd, out Vector3 world, out int floor, out float dist)
        {
            world = default; floor = 0; dist = float.MaxValue;
            foreach (var (pos, f) in _buttons)
            {
                float d = Vector3.Distance(eye, pos);
                if (d > 2.4f || d >= dist) continue;
                if (Vector3.Angle(fwd, pos - eye) > 32f) continue;
                dist = d; world = pos; floor = f;
            }
            return dist < float.MaxValue;
        }

        /// <summary>Server check: a button of this level is within reach of <paramref name="feet"/>.</summary>
        public bool IsNearButton(Vector3 feet, int floor)
        {
            foreach (var (pos, f) in _buttons)
                if (f == floor && Vector3.Distance(feet + Vector3.up * 1.2f, pos) < 3.2f) return true;
            return false;
        }

        public int SelectedFloor => _floor;

        /// <summary>Applies the synced level (called when <see cref="MatchManager.SecurityFloor"/> changes).</summary>
        private void ApplyFloor(int floor)
        {
            floor = Mathf.Clamp(floor, 0, Levels - 1);
            if (floor == _floor) return;
            _floor = floor;
            Debug.Log($"[Security] monitors now show level {LevelName(floor)}");
            AssignAll();
            foreach (var s in _slots) s.Since = Time.time;   // every screen snows
            PlayFeedSound(-1);
            for (int i = 0; i < _buttonRenderers.Count; i++) PaintButton(i);
            foreach (var h in _headers) if (h != null) h.text = "KAMERY  POZIOM " + LevelName(_floor);
        }

        /// <summary>Test helper: pretend camera <paramref name="index"/> of <paramref name="level"/> saw motion just now.</summary>
        public void ForceMotion(int level, int index)
        {
            var list = _levelCams[level];
            if (index < 0 || index >= list.Count) return;
            var c = _cams[list[index]];
            c.LastMotion = Time.time;
            _cams[list[index]] = c;
        }

        /// <summary>Test helper: saves what monitor <paramref name="slot"/> currently shows.</summary>
        public void DumpFeed(int slot, string path)
        {
            var s = _slots[slot];
            var prev = RenderTexture.active;
            RenderTexture.active = s.Rt;
            var tex = new Texture2D(s.Rt.width, s.Rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, s.Rt.width, s.Rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
        }

        // ================================================================ build

        private void Build()
        {
            _monitorShader = Resources.Load<Shader>("Shaders/HnsMonitor");
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textShader = Resources.Load<Shader>("Shaders/HnsSecurityText");
            if (_font != null && textShader != null)
            {
                _fontMat = new Material(textShader);
                _fontMat.mainTexture = _font.material.mainTexture;
            }
            else if (_font != null) _fontMat = _font.material;
            _switchClip = MakeStaticClip("MonitorSwitch", 0.5f, 11);
            _floorClip = MakeStaticClip("MonitorFloor", 0.95f, 23);
            _scrapeClip = MakeScrapeClip();

            BuildCameraPoints();
            for (int i = 0; i < Monitors; i++) _slots[i] = MakeSlot(i);
            for (int lv = 0; lv < Levels; lv++) BuildRoom(lv);
            var mm = MatchManager.Instance;
            _floor = mm != null ? Mathf.Clamp(mm.SecurityFloor.Value, 0, Levels - 1) : 0;
            AssignAll();
            foreach (var h in _headers) h.text = "KAMERY  POZIOM " + LevelName(_floor);
            for (int i = 0; i < _buttonRenderers.Count; i++) PaintButton(i);
            Debug.Log($"[Security] rooms built on 3 levels, {_cams.Count} cameras");
        }

        private void AddCam(int lv, string name, Vector3 pos, Vector3 dir)
        {
            _levelCams[lv].Add(_cams.Count);
            var rot = Quaternion.LookRotation(dir);
            _cams.Add(new CamPoint { Pos = pos, Rot = rot, Level = lv, Name = name, LastMotion = -99f, Led = BuildCameraModel(pos, rot, lv) });
        }

        /// <summary>Small CCTV camera hanging from the ceiling: bracket, housing, lens, sun hood and a red LED.</summary>
        private Renderer BuildCameraModel(Vector3 pos, Quaternion rot, int lv)
        {
            if (_ledMat == null)
            {
                var sh = Shader.Find("Sprites/Default");
                _ledMat = sh != null ? new Material(sh) { name = "CctvLed" } : null;
            }
            var root = new GameObject("CCTV").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(pos, rot);
            var housing = new Color(0.82f, 0.82f, 0.8f);
            var dark = new Color(0.12f, 0.12f, 0.13f);
            float ceiling = FloorY(lv) + 3.0f;
            // Housing behind the lens (the feed camera sits at the lens, its near plane hides the model).
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.2f), new Vector3(0.13f, 0.12f, 0.3f), housing);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.075f, -0.17f), new Vector3(0.16f, 0.025f, 0.36f), housing); // sun hood
            var lens = Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.04f), new Vector3(0.08f, 0.012f, 0.08f), dark);
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Bracket up to the ceiling.
            var arm = new GameObject("Bracket").transform;
            arm.SetParent(transform, false);
            Vector3 top = new Vector3(pos.x, ceiling, pos.z) - rot * Vector3.forward * 0.25f;
            Vector3 bottom = pos - rot * Vector3.forward * 0.25f + Vector3.up * 0.06f;
            arm.position = (top + bottom) * 0.5f;
            Part(arm, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.035f, Mathf.Max(0.05f, (top.y - bottom.y) * 0.5f), 0.035f), dark);
            Part(arm, PrimitiveType.Cube, new Vector3(0f, (top.y - bottom.y) * 0.5f, 0f), new Vector3(0.14f, 0.02f, 0.14f), dark);
            // LED on the front, next to the lens.
            var led = Part(root, PrimitiveType.Cube, new Vector3(0.045f, 0.04f, -0.045f), new Vector3(0.018f, 0.018f, 0.006f), new Color(0.15f, 0f, 0f));
            var lr = led.GetComponent<Renderer>();
            if (_ledMat != null) lr.sharedMaterial = _ledMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        private static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color c)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", c);
            g.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
            return g;
        }

        /// <summary>Server: a living player stands inside one of the security rooms (the cameras are being watched).</summary>
        public bool AnyoneWatching()
        {
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned || p.Life.Value == LifeState.Dead) continue;
                Vector3 v = p.transform.position;
                if (v.x < X0 || v.x > X1 || v.z < Z0 || v.z > Z1) continue;
                for (int lv = 0; lv < Levels; lv++)
                    if (v.y > FloorY(lv) - 0.5f && v.y < FloorY(lv) + 2.5f) return true;
            }
            return false;
        }

        /// <summary>LEDs blink softly red on the cameras shown on the monitors while somebody watches them.</summary>
        private void UpdateLeds()
        {
            var mm = MatchManager.Instance;
            bool watched = mm != null && mm.SecurityWatched.Value;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2f * Mathf.PI * 0.9f);
            for (int i = 0; i < _cams.Count; i++)
            {
                var led = _cams[i].Led;
                if (led == null) continue;
                bool on = false;
                if (watched)
                    foreach (var s in _slots) if (s.CamIndex == i) { on = true; break; }
                var col = on ? new Color(1f * pulse, 0.08f * pulse, 0.05f * pulse, 1f) : new Color(0.15f, 0f, 0f, 1f);
                _ledMpb.SetColor("_Color", col);
                led.SetPropertyBlock(_ledMpb);
            }
        }

        /// <summary>Test helper: is the LED of camera <paramref name="index"/> on level <paramref name="level"/> lit right now.</summary>
        public bool LedOn(int level, int index)
        {
            var mm = MatchManager.Instance;
            if (mm == null || !mm.SecurityWatched.Value) return false;
            int ci = _levelCams[level][index];
            foreach (var s in _slots) if (s.CamIndex == ci) return true;
            return false;
        }

        /// <summary>Test helper: world position of a camera (to look at it from the garage).</summary>
        public Vector3 CameraPosition(int level, int index) => _cams[_levelCams[level][index]].Pos;

        private void BuildCameraPoints()
        {
            float[] aisleZ = { -44.75f, -28.25f, -11.75f, 11.75f, 28.25f, 44.75f };
            // Stairwells (Map ParkingGarageBuilder.Stairs): door on the south face, x0 + 1 on level 0, x0 + 3 below.
            var stairs = new[] { (x0: -62f, zf: 50.8f), (x0: 46f, zf: 50.8f), (x0: -34f, zf: -4.6f), (x0: 30f, zf: -4.6f) };
            // Elevators: the scene has "ElevatorCar_<n>_..." markers, one per level; doors face z = +-1.4.
            var elevX = new SortedDictionary<int, float>();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.name.StartsWith("ElevatorCar_")) continue;
                var p = t.name.Split('_');
                if (p.Length >= 2 && int.TryParse(p[1], out int car)) elevX[car] = t.position.x;
            }

            for (int lv = 0; lv < Levels; lv++)
            {
                float y0 = FloorY(lv), y = y0 + 2.3f;
                for (int i = 0; i < 6; i++)
                {
                    bool fromWest = i % 2 == 0;
                    AddCam(lv, "ALEJA " + (i + 1), new Vector3(fromWest ? -72f : 72f, y, aisleZ[i]), new Vector3(fromWest ? 1f : -1f, -0.07f, 0f));
                }
                AddCam(lv, "WSCHÓD", new Vector3(79.5f, y, -44f), new Vector3(0f, -0.07f, 1f));
                AddCam(lv, "ZACHÓD", new Vector3(-79.5f, y, 44f), new Vector3(0f, -0.07f, -1f));
                for (int s = 0; s < stairs.Length; s++)
                {
                    float door = lv == 0 ? stairs[s].x0 + 1f : stairs[s].x0 + 3f;
                    var pos = new Vector3(door + 2.5f, y0 + 2.4f, stairs[s].zf - 7f);
                    var look = new Vector3(door, y0 + 1.1f, stairs[s].zf) - pos;
                    AddCam(lv, "SCHODY " + (s + 1), pos, look);
                }
                int e = 0;
                foreach (var kv in elevX)
                {
                    if (e >= 2) break;
                    var pos = new Vector3(kv.Value + 2.5f, y0 + 2.4f, -7.5f);
                    var look = new Vector3(kv.Value, y0 + 1.1f, -1.4f) - pos;
                    AddCam(lv, "WINDA " + (++e), pos, look);
                }
            }
        }

        private Slot MakeSlot(int i)
        {
            var s = new Slot();
            s.Rt = new RenderTexture(320, 180, 16, RenderTextureFormat.ARGB32) { name = "SecurityFeed" + i, filterMode = FilterMode.Bilinear };
            s.Rt.Create();
            var go = new GameObject("FeedCam" + i);
            go.transform.SetParent(transform, false);
            s.Cam = go.AddComponent<Camera>();
            s.Cam.enabled = false;
            s.Cam.targetTexture = s.Rt;
            s.Cam.fieldOfView = 62f;
            s.Cam.nearClipPlane = 0.25f;
            s.Cam.farClipPlane = 70f;
            s.Cam.clearFlags = CameraClearFlags.SolidColor;
            s.Cam.backgroundColor = Color.black;
            var lg = new GameObject("IR").AddComponent<Light>();
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0f, -0.3f, 1.5f);
            lg.type = LightType.Point;
            lg.range = 30f;
            lg.intensity = 0.9f;
            lg.shadows = LightShadows.None;
            lg.enabled = false;
            s.Ir = lg;
            return s;
        }

        private void BuildRoom(int lv)
        {
            float y0 = FloorY(lv);
            var root = new GameObject("SecurityRoom_L" + LevelName(lv)).transform;
            root.SetParent(transform, false);
            var concrete = new Color(0.34f, 0.35f, 0.36f);
            var dark = new Color(0.12f, 0.13f, 0.14f);
            float cx = (X0 + X1) * 0.5f, cz = (Z0 + Z1) * 0.5f;

            // Probe the footprint first: log anything of the garage that is in the way.
            var probe = Physics.OverlapBox(new Vector3(cx, y0 + 1.5f, cz), new Vector3((X1 - X0) * 0.5f - 0.1f, 1.2f, (Z1 - Z0) * 0.5f - 0.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in probe)
                if (c.GetComponentInParent<NetPlayer>() == null) Debug.Log($"[Security] level {LevelName(lv)}: footprint overlaps '{c.name}' at {c.transform.position}");
            ClearProps(lv, cx, cz, y0);

            // ---- shell
            Box(root, "WallW", new Vector3(X0, y0 + Height * 0.5f, cz), new Vector3(Wall, Height, Z1 - Z0), concrete);
            Box(root, "WallE", new Vector3(X1, y0 + Height * 0.5f, cz), new Vector3(Wall, Height, Z1 - Z0), concrete);
            Box(root, "WallS1", new Vector3((X0 + DoorA) * 0.5f, y0 + Height * 0.5f, Z0), new Vector3(DoorA - X0, Height, Wall), concrete);
            Box(root, "WallS2", new Vector3((DoorB + X1) * 0.5f, y0 + Height * 0.5f, Z0), new Vector3(X1 - DoorB, Height, Wall), concrete);
            Box(root, "Lintel", new Vector3((DoorA + DoorB) * 0.5f, y0 + 2.1f + (Height - 2.1f) * 0.5f, Z0), new Vector3(DoorB - DoorA, Height - 2.1f, Wall), concrete);
            Box(root, "NorthLining", new Vector3(cx, y0 + Height * 0.5f, Z1 - 0.05f), new Vector3(X1 - X0, Height, 0.1f), dark);
            Box(root, "Floor", new Vector3(cx, y0 + 0.02f, cz), new Vector3(X1 - X0, 0.04f, Z1 - Z0), new Color(0.2f, 0.21f, 0.22f));
            Box(root, "Baffle", new Vector3(DoorA - 0.1f, y0 + Height * 0.5f, Z0 + 1.4f), new Vector3(Wall, Height, 2.4f), concrete);
            BuildCrateAndSign(root, lv, y0);
            Box(root, "Rug", new Vector3(cx + 0.4f, y0 + 0.045f, Z1 - 2.2f), new Vector3(2.4f, 0.01f, 1.6f), new Color(0.22f, 0.12f, 0.1f), false);

            // ---- desk + chair
            float deskTop = y0 + 0.77f, dz = Z1 - 0.65f;
            Box(root, "Desk", new Vector3(cx, y0 + 0.74f, dz), new Vector3(9.4f, 0.06f, 1.0f), new Color(0.22f, 0.22f, 0.24f));
            foreach (float dx in new[] { -4.5f, 0f, 4.5f })
                Box(root, "DeskLeg", new Vector3(cx + dx, y0 + 0.36f, dz), new Vector3(0.08f, 0.72f, 0.9f), dark);
            Box(root, "ChairSeat", new Vector3(cx + 0.4f, y0 + 0.5f, Z1 - 2.1f), new Vector3(0.5f, 0.08f, 0.5f), new Color(0.1f, 0.1f, 0.12f));
            Box(root, "ChairBack", new Vector3(cx + 0.4f, y0 + 0.85f, Z1 - 2.4f), new Vector3(0.5f, 0.6f, 0.08f), new Color(0.1f, 0.1f, 0.12f));
            Box(root, "ChairStem", new Vector3(cx + 0.4f, y0 + 0.25f, Z1 - 2.1f), new Vector3(0.08f, 0.5f, 0.08f), dark);
            Prim(root, PrimitiveType.Cylinder, "ChairBase", new Vector3(cx + 0.4f, y0 + 0.04f, Z1 - 2.1f), new Vector3(0.5f, 0.02f, 0.5f), dark);

            // ---- server rack
            Box(root, "Rack", new Vector3(X0 + 0.8f, y0 + 1.0f, Z1 - 0.7f), new Vector3(0.9f, 2.0f, 0.8f), new Color(0.08f, 0.08f, 0.1f));
            for (int i = 0; i < 6; i++)
                Box(root, "RackLed", new Vector3(X0 + 0.8f, y0 + 0.5f + i * 0.25f, Z1 - 1.12f), new Vector3(0.7f, 0.04f, 0.02f), i % 2 == 0 ? new Color(0.1f, 0.9f, 0.3f) : new Color(0.9f, 0.5f, 0.1f), false);
            // Pizza box left on top of the rack.
            Box(root, "PizzaBox", new Vector3(X0 + 0.8f, y0 + 2.03f, Z1 - 0.7f), new Vector3(0.42f, 0.05f, 0.42f), new Color(0.75f, 0.62f, 0.42f), false);

            // ---- monitors
            int m = 0;
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++, m++)
            {
                Vector3 c = new Vector3(cx - 1.8f + col * 1.8f + 0.3f, y0 + (row == 0 ? 1.28f : 2.14f), Z1 - 0.14f);
                Box(root, "Bezel", c + new Vector3(0f, 0f, 0.03f), new Vector3(1.62f, 0.9f, 0.08f), new Color(0.04f, 0.04f, 0.05f), false);
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Screen" + m;
                Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(root, false);
                q.transform.position = c + new Vector3(0f, 0f, -0.02f);
                q.transform.localScale = new Vector3(1.5f, 0.82f, 1f);
                var r = q.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mat = _monitorShader != null ? new Material(_monitorShader) : new Material(Shader.Find("Unlit/Texture"));
                mat.mainTexture = _slots[m].Rt;
                r.sharedMaterial = mat;
                _slots[m].Mats.Add(mat);
                _slots[m].ScreenPos[lv] = c;
                _slots[m].Labels.Add(MakeText(root, "Label" + m, c + new Vector3(-0.72f, -0.36f, -0.03f), "", 0.0167f, new Color(0.6f, 1f, 0.7f), TextAnchor.LowerLeft));
                // Sticky notes on some bezels.
                if ((m + lv) % 3 == 0)
                    Box(root, "StickyNote", c + new Vector3(0.68f, 0.36f, -0.02f), new Vector3(0.09f, 0.09f, 0.005f), (m % 2 == 0) ? new Color(1f, 0.92f, 0.3f) : new Color(1f, 0.55f, 0.75f), false);
            }
            _headers.Add(MakeText(root, "Header", new Vector3(cx, y0 + 2.72f, Z1 - 0.15f), "", 0.034f, new Color(0.6f, 1f, 0.7f), TextAnchor.MiddleCenter));

            // ---- floor buttons (levels 0, -1, -2)
            Box(root, "ButtonPanel", new Vector3(cx, deskTop + 0.015f, Z1 - 1.0f), new Vector3(2.6f, 0.03f, 0.36f), new Color(0.3f, 0.3f, 0.32f), false);
            for (int f = 0; f < Levels; f++)
            {
                Vector3 bp = new Vector3(cx - 1.0f + f * 1.0f, y0 + 0.8f, Z1 - 1.0f);
                var b = Box(root, "Button" + f, bp, new Vector3(0.22f, 0.06f, 0.22f), new Color(0.15f, 0.15f, 0.16f));
                _buttons.Add((bp + Vector3.up * 0.05f, f));
                _buttonRenderers.Add(b.GetComponent<Renderer>());
                var t = MakeText(root, "ButtonLabel" + f, bp + new Vector3(0f, 0.04f, -0.3f), "POZIOM " + LevelName(f), 0.011f, new Color(0.85f, 0.9f, 0.85f), TextAnchor.MiddleCenter);
                t.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
            }

            BuildDeskClutter(root, lv, cx, deskTop);
            BuildRoomDetails(root, lv, cx, cz, y0);

            // ---- lighting
            var lamp = new GameObject("RoomLamp").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.position = new Vector3(cx, y0 + 2.6f, cz - 0.5f);
            lamp.type = LightType.Point;
            lamp.range = 9f;
            lamp.intensity = 0.55f;
            lamp.color = new Color(0.6f, 0.9f, 0.75f);
            lamp.shadows = LightShadows.None;
            var glow = new GameObject("MonitorGlow").AddComponent<Light>();
            glow.transform.SetParent(root, false);
            glow.transform.position = new Vector3(cx, y0 + 1.7f, Z1 - 1.6f);
            glow.type = LightType.Point;
            glow.range = 6f;
            glow.intensity = 0.8f;
            glow.color = new Color(0.45f, 1f, 0.6f);
            glow.shadows = LightShadows.None;
        }

        /// <summary>
        /// Map props standing where the room and the crate go (pallets, scaffolding, a tarp car...) are switched off.
        /// Only objects under the map's "Props" group: walls and floors are never touched.
        /// </summary>
        private void ClearProps(int lv, float cx, float cz, float y0)
        {
            float zMin = Z0 - 3f;
            var hits = Physics.OverlapBox(new Vector3(cx + 0.6f, y0 + 1.2f, (zMin + Z1) * 0.5f),
                new Vector3((X1 - X0) * 0.5f + 1.4f, 1.0f, (Z1 - zMin) * 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            var done = new HashSet<Transform>();
            foreach (var c in hits)
            {
                Transform t = c.transform;
                while (t.parent != null && !(t.parent.name.StartsWith("Level-") && t.parent.parent != null && t.parent.parent.name == "Props")) t = t.parent;
                if (t.parent == null || !done.Add(t)) continue;
                Debug.Log($"[Security] level {LevelName(lv)}: removed prop '{t.name}' at {t.position}");
                t.gameObject.SetActive(false);
            }
        }

        private void BuildCrateAndSign(Transform root, int lv, float y0)
        {
            // Heavy wooden crate with planks, battens and stencils.
            var crate = new GameObject("SecurityCrate_L" + LevelName(lv)).transform;
            crate.SetParent(root, false);
            crate.SetPositionAndRotation(CrateClosed(lv), Quaternion.identity);
            var wood = new Color(0.52f, 0.37f, 0.2f);
            var batten = new Color(0.38f, 0.26f, 0.13f);
            var body = Box(crate, "Body", crate.position, CrateSize, wood, true);
            body.transform.localPosition = Vector3.zero;
            Vector3 h = CrateSize * 0.5f;
            foreach (float sz in new[] { -1f, 1f })
            {
                float z = sz * (h.z + 0.012f);
                Box(crate, "BattenTop", crate.position + new Vector3(0f, h.y - 0.06f, z), new Vector3(CrateSize.x, 0.1f, 0.03f), batten, false);
                Box(crate, "BattenBot", crate.position + new Vector3(0f, -h.y + 0.06f, z), new Vector3(CrateSize.x, 0.1f, 0.03f), batten, false);
                Box(crate, "BattenL", crate.position + new Vector3(-h.x + 0.06f, 0f, z), new Vector3(0.1f, CrateSize.y, 0.03f), batten, false);
                Box(crate, "BattenR", crate.position + new Vector3(h.x - 0.06f, 0f, z), new Vector3(0.1f, CrateSize.y, 0.03f), batten, false);
                var diag = Box(crate, "BattenX", crate.position + new Vector3(0f, 0f, z), new Vector3(0.09f, Mathf.Sqrt(CrateSize.x * CrateSize.x + CrateSize.y * CrateSize.y) - 0.2f, 0.028f), batten, false);
                diag.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(CrateSize.x, CrateSize.y) * Mathf.Rad2Deg);
            }
            for (int i = 0; i < 4; i++)
                Box(crate, "Gap", crate.position + new Vector3(0f, -h.y + 0.26f * (i + 1), -h.z - 0.006f), new Vector3(CrateSize.x - 0.02f, 0.012f, 0.01f), new Color(0.2f, 0.13f, 0.07f), false);
            var stencil = MakeText(crate, "Stencil", crate.position + new Vector3(0.2f, 0.1f, -h.z - 0.03f), "ZŁOM\nNIE RUSZAĆ", 0.03f, new Color(0.12f, 0.08f, 0.05f), TextAnchor.MiddleCenter);
            stencil.fontStyle = FontStyle.Bold;
            _crates[lv] = crate;

            // SECURITY sign over the door, facing the garage.
            float doorX = (DoorA + DoorB) * 0.5f;
            Vector3 sp = new Vector3(doorX, y0 + 2.42f, Z0 - Wall * 0.5f - 0.025f);
            Box(root, "SignPlate", sp, new Vector3(1.7f, 0.42f, 0.04f), new Color(0.07f, 0.12f, 0.3f), false);
            Box(root, "SignEdge", sp + new Vector3(0f, 0f, 0.004f), new Vector3(1.78f, 0.5f, 0.03f), new Color(0.85f, 0.85f, 0.88f), false);
            var sign = MakeText(root, "SignText", sp + new Vector3(0f, 0.02f, -0.03f), "SECURITY", 0.075f, Color.white, TextAnchor.MiddleCenter);
            sign.fontStyle = FontStyle.Bold;
            var sub = MakeText(root, "SignSub", sp + new Vector3(0f, -0.14f, -0.03f), "TYLKO PERSONEL", 0.026f, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);
            sub.fontStyle = FontStyle.Bold;
            var lampL = new GameObject("SignLamp").AddComponent<Light>();
            lampL.transform.SetParent(root, false);
            lampL.transform.position = sp + new Vector3(0f, 0.45f, -0.5f);
            lampL.type = LightType.Point;
            lampL.range = 3.2f;
            lampL.intensity = 0.7f;
            lampL.color = new Color(0.85f, 0.9f, 1f);
            lampL.shadows = LightShadows.None;
        }

        /// <summary>Things on the desk: a fast-food drink with a straw, coffee mug, donuts, keyboard, phone, radio, fan, lamp, papers.</summary>
        private void BuildDeskClutter(Transform root, int lv, float cx, float top)
        {
            float zFront = Z1 - 1.0f, zMid = Z1 - 0.75f, zBack = Z1 - 0.5f;

            // Fast-food cup with a lid and a bent straw.
            Vector3 cup = new Vector3(cx + 2.2f, top, zFront + 0.05f);
            Prim(root, PrimitiveType.Cylinder, "Cup", cup + Vector3.up * 0.09f, new Vector3(0.09f, 0.09f, 0.09f), new Color(0.85f, 0.12f, 0.1f));
            Prim(root, PrimitiveType.Cylinder, "CupStripe", cup + Vector3.up * 0.11f, new Vector3(0.092f, 0.02f, 0.092f), Color.white);
            Prim(root, PrimitiveType.Cylinder, "CupLid", cup + Vector3.up * 0.185f, new Vector3(0.1f, 0.008f, 0.1f), new Color(0.9f, 0.9f, 0.92f));
            Prim(root, PrimitiveType.Cylinder, "Straw", cup + new Vector3(0.012f, 0.25f, 0f), new Vector3(0.008f, 0.07f, 0.008f), new Color(1f, 0.85f, 0.2f), new Vector3(0f, 0f, -12f));
            Prim(root, PrimitiveType.Cylinder, "StrawBend", cup + new Vector3(0.03f, 0.325f, 0f), new Vector3(0.008f, 0.025f, 0.008f), new Color(1f, 0.85f, 0.2f), new Vector3(0f, 0f, -60f));

            // Coffee mug with a handle and a coaster.
            Vector3 mug = new Vector3(cx - 3.4f, top, zFront + 0.1f);
            Prim(root, PrimitiveType.Cylinder, "Coaster", mug + Vector3.up * 0.004f, new Vector3(0.11f, 0.004f, 0.11f), new Color(0.35f, 0.24f, 0.15f));
            Prim(root, PrimitiveType.Cylinder, "Mug", mug + Vector3.up * 0.055f, new Vector3(0.08f, 0.05f, 0.08f), new Color(0.92f, 0.92f, 0.9f));
            Prim(root, PrimitiveType.Cylinder, "Coffee", mug + Vector3.up * 0.103f, new Vector3(0.07f, 0.003f, 0.07f), new Color(0.22f, 0.12f, 0.06f));
            Box(root, "MugHandle", mug + new Vector3(0.05f, 0.055f, 0f), new Vector3(0.02f, 0.05f, 0.012f), new Color(0.92f, 0.92f, 0.9f), false);

            // Open donut box.
            Vector3 dbox = new Vector3(cx + 3.4f, top, zMid);
            Box(root, "DonutBox", dbox + Vector3.up * 0.025f, new Vector3(0.38f, 0.05f, 0.3f), new Color(0.95f, 0.55f, 0.7f), false);
            Box(root, "DonutLid", dbox + new Vector3(0f, 0.17f, 0.17f), new Vector3(0.38f, 0.3f, 0.01f), new Color(0.95f, 0.55f, 0.7f), false, new Vector3(-12f, 0f, 0f));
            Color[] glaze = { new Color(0.45f, 0.25f, 0.12f), new Color(1f, 0.6f, 0.75f), new Color(0.95f, 0.9f, 0.8f) };
            for (int i = 0; i < 3; i++)
            {
                Vector3 d = dbox + new Vector3(-0.11f + i * 0.11f, 0.06f, -0.04f + (i % 2) * 0.07f);
                Prim(root, PrimitiveType.Cylinder, "Donut", d, new Vector3(0.09f, 0.015f, 0.09f), new Color(0.8f, 0.55f, 0.3f));
                Prim(root, PrimitiveType.Cylinder, "Glaze", d + Vector3.up * 0.012f, new Vector3(0.08f, 0.004f, 0.08f), glaze[i]);
                Prim(root, PrimitiveType.Cylinder, "Hole", d + Vector3.up * 0.016f, new Vector3(0.025f, 0.004f, 0.025f), new Color(0.3f, 0.2f, 0.12f));
            }
            // Half-eaten donut next to the keyboard.
            Prim(root, PrimitiveType.Cylinder, "Donut", new Vector3(cx - 1.6f, top + 0.012f, zFront), new Vector3(0.085f, 0.012f, 0.085f), new Color(0.8f, 0.55f, 0.3f));

            // Keyboard (key rows), mouse on a pad.
            Vector3 kb = new Vector3(cx - 2.5f, top, zFront + 0.02f);
            Box(root, "Keyboard", kb + Vector3.up * 0.012f, new Vector3(0.46f, 0.02f, 0.16f), new Color(0.1f, 0.1f, 0.11f), false);
            for (int r = 0; r < 4; r++)
                Box(root, "Keys", kb + new Vector3(0f, 0.024f, -0.055f + r * 0.036f), new Vector3(0.42f, 0.006f, 0.026f), new Color(0.25f, 0.25f, 0.27f), false);
            Box(root, "MousePad", new Vector3(cx - 2.0f, top + 0.002f, zFront + 0.02f), new Vector3(0.24f, 0.004f, 0.2f), new Color(0.12f, 0.15f, 0.3f), false);
            Box(root, "Mouse", new Vector3(cx - 2.0f, top + 0.016f, zFront + 0.03f), new Vector3(0.06f, 0.025f, 0.1f), new Color(0.15f, 0.15f, 0.16f), false);

            // Old desk phone with the receiver.
            Vector3 ph = new Vector3(cx + 1.5f, top, zBack);
            Box(root, "Phone", ph + Vector3.up * 0.04f, new Vector3(0.22f, 0.08f, 0.2f), new Color(0.75f, 0.72f, 0.62f), false, new Vector3(-10f, 0f, 0f));
            Box(root, "Receiver", ph + new Vector3(0f, 0.1f, 0.02f), new Vector3(0.22f, 0.04f, 0.05f), new Color(0.75f, 0.72f, 0.62f), false);

            // Walkie-talkie with an antenna and a red LED.
            Vector3 wt = new Vector3(cx - 1.4f, top, zBack);
            Box(root, "Radio", wt + Vector3.up * 0.09f, new Vector3(0.06f, 0.18f, 0.035f), new Color(0.08f, 0.08f, 0.09f), false);
            Prim(root, PrimitiveType.Cylinder, "Antenna", wt + new Vector3(0.018f, 0.24f, 0f), new Vector3(0.008f, 0.06f, 0.008f), new Color(0.05f, 0.05f, 0.05f));
            Box(root, "RadioLed", wt + new Vector3(-0.015f, 0.185f, -0.018f), new Vector3(0.008f, 0.008f, 0.004f), new Color(1f, 0.1f, 0.1f), false);

            // Desk fan (blades spin).
            Vector3 fan = new Vector3(cx - 3.0f, top, zBack + 0.05f);
            Prim(root, PrimitiveType.Cylinder, "FanBase", fan + Vector3.up * 0.01f, new Vector3(0.14f, 0.01f, 0.14f), new Color(0.85f, 0.85f, 0.85f));
            Prim(root, PrimitiveType.Cylinder, "FanStem", fan + Vector3.up * 0.12f, new Vector3(0.02f, 0.11f, 0.02f), new Color(0.85f, 0.85f, 0.85f));
            var blades = new GameObject("FanBlades").transform;
            blades.SetParent(root, false);
            blades.position = fan + new Vector3(0f, 0.25f, -0.03f);
            for (int i = 0; i < 3; i++)
            {
                var b = Box(blades, "Blade", blades.position, new Vector3(0.035f, 0.11f, 0.004f), new Color(0.6f, 0.75f, 0.9f), false);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                b.transform.position = blades.position + b.transform.rotation * new Vector3(0f, 0.055f, 0f);
            }
            _fans.Add(blades);
            Prim(root, PrimitiveType.Cylinder, "FanCage", fan + new Vector3(0f, 0.25f, -0.025f), new Vector3(0.25f, 0.005f, 0.25f), new Color(0.75f, 0.75f, 0.75f), new Vector3(90f, 0f, 0f));

            // Desk lamp with a warm light.
            Vector3 dl = new Vector3(cx + 4.3f, top, zBack);
            Prim(root, PrimitiveType.Cylinder, "LampBase", dl + Vector3.up * 0.01f, new Vector3(0.14f, 0.01f, 0.14f), new Color(0.1f, 0.25f, 0.15f));
            Prim(root, PrimitiveType.Cylinder, "LampArm", dl + new Vector3(-0.05f, 0.2f, 0f), new Vector3(0.015f, 0.2f, 0.015f), new Color(0.1f, 0.25f, 0.15f), new Vector3(0f, 0f, 20f));
            Prim(root, PrimitiveType.Cylinder, "LampHead", dl + new Vector3(-0.18f, 0.37f, 0f), new Vector3(0.12f, 0.06f, 0.12f), new Color(0.1f, 0.25f, 0.15f), new Vector3(0f, 0f, 35f));
            var ll = new GameObject("DeskLampLight").AddComponent<Light>();
            ll.transform.SetParent(root, false);
            ll.transform.position = dl + new Vector3(-0.22f, 0.32f, 0f);
            ll.transform.rotation = Quaternion.Euler(80f, -90f, 0f);
            ll.type = LightType.Spot;
            ll.spotAngle = 80f;
            ll.range = 2.2f;
            ll.intensity = 1.2f;
            ll.color = new Color(1f, 0.8f, 0.5f);
            ll.shadows = LightShadows.None;

            // Papers, clipboard with a pen, a logbook.
            for (int i = 0; i < 5; i++)
                Box(root, "Paper", new Vector3(cx - 4.2f + i * 0.01f, top + 0.003f + i * 0.003f, zMid + i * 0.012f), new Vector3(0.21f, 0.002f, 0.297f), new Color(0.95f, 0.95f, 0.92f), false, new Vector3(0f, i * 7f - 10f, 0f));
            Box(root, "Clipboard", new Vector3(cx - 0.6f, top + 0.006f, zBack + 0.05f), new Vector3(0.23f, 0.008f, 0.32f), new Color(0.55f, 0.38f, 0.2f), false, new Vector3(0f, 18f, 0f));
            Box(root, "ClipSheet", new Vector3(cx - 0.6f, top + 0.012f, zBack + 0.04f), new Vector3(0.2f, 0.002f, 0.27f), new Color(0.95f, 0.95f, 0.92f), false, new Vector3(0f, 18f, 0f));
            Prim(root, PrimitiveType.Cylinder, "Pen", new Vector3(cx - 0.45f, top + 0.02f, zBack - 0.05f), new Vector3(0.008f, 0.07f, 0.008f), new Color(0.1f, 0.2f, 0.7f), new Vector3(90f, 30f, 0f));
            Box(root, "Logbook", new Vector3(cx + 0.7f, top + 0.015f, zBack + 0.05f), new Vector3(0.3f, 0.03f, 0.22f), new Color(0.15f, 0.2f, 0.45f), false, new Vector3(0f, -8f, 0f));
            // Energy-drink cans (one crushed, lying).
            Prim(root, PrimitiveType.Cylinder, "Can", new Vector3(cx + 2.6f, top + 0.06f, zBack), new Vector3(0.065f, 0.06f, 0.065f), new Color(0.15f, 0.65f, 0.2f));
            Prim(root, PrimitiveType.Cylinder, "CanLying", new Vector3(cx + 2.85f, top + 0.033f, zBack + 0.08f), new Vector3(0.065f, 0.05f, 0.065f), new Color(0.15f, 0.65f, 0.2f), new Vector3(0f, 40f, 90f));
        }

        /// <summary>Room furniture and wall details: clock, garage map, calendar, coat rack, water cooler, bin, extinguisher, vent, cables.</summary>
        private void BuildRoomDetails(Transform root, int lv, float cx, float cz, float y0)
        {
            // Wall clock on the west wall (hands follow the real time).
            Vector3 clock = new Vector3(X0 + 0.12f, y0 + 2.25f, cz + 0.5f);
            Prim(root, PrimitiveType.Cylinder, "Clock", clock, new Vector3(0.36f, 0.02f, 0.36f), new Color(0.95f, 0.95f, 0.92f), new Vector3(0f, 0f, 90f));
            Prim(root, PrimitiveType.Cylinder, "ClockRim", clock + new Vector3(-0.003f, 0f, 0f), new Vector3(0.39f, 0.015f, 0.39f), new Color(0.1f, 0.1f, 0.1f), new Vector3(0f, 0f, 90f));
            var hour = new GameObject("HourHand").transform;
            hour.SetParent(root, false);
            hour.position = clock + new Vector3(0.025f, 0f, 0f);
            hour.rotation = Quaternion.LookRotation(Vector3.right);
            var hb = Box(hour, "H", hour.position, new Vector3(0.012f, 0.09f, 0.004f), Color.black, false);
            hb.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            var minute = new GameObject("MinuteHand").transform;
            minute.SetParent(root, false);
            minute.position = clock + new Vector3(0.03f, 0f, 0f);
            minute.rotation = Quaternion.LookRotation(Vector3.right);
            var mb = Box(minute, "M", minute.position, new Vector3(0.008f, 0.14f, 0.004f), Color.black, false);
            mb.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            _clocks.Add((hour, minute));

            // Garage plan on the east wall: three level outlines with red pins at the cameras.
            Vector3 plan = new Vector3(X1 - 0.12f, y0 + 1.55f, cz + 1.6f);
            Box(root, "PlanBoard", plan, new Vector3(0.02f, 0.8f, 1.3f), new Color(0.85f, 0.85f, 0.8f), false);
            for (int i = 0; i < 3; i++)
            {
                Box(root, "PlanLevel", plan + new Vector3(-0.012f, 0.25f - i * 0.25f, 0f), new Vector3(0.004f, 0.18f, 1.1f), new Color(0.6f, 0.65f, 0.75f), false);
                for (int p = 0; p < 6; p++)
                    Box(root, "Pin", plan + new Vector3(-0.018f, 0.25f - i * 0.25f + ((p % 2 == 0) ? 0.05f : -0.05f), -0.45f + p * 0.18f), new Vector3(0.01f, 0.02f, 0.02f), new Color(0.9f, 0.1f, 0.1f), false);
            }
            // Calendar and a shift schedule.
            Box(root, "Calendar", new Vector3(X1 - 0.12f, y0 + 1.7f, cz - 0.2f), new Vector3(0.01f, 0.45f, 0.32f), new Color(0.95f, 0.95f, 0.95f), false);
            Box(root, "CalendarPic", new Vector3(X1 - 0.128f, y0 + 1.82f, cz - 0.2f), new Vector3(0.004f, 0.18f, 0.28f), new Color(0.3f, 0.55f, 0.8f), false);
            Box(root, "Schedule", new Vector3(X0 + 0.12f, y0 + 1.5f, cz - 1.2f), new Vector3(0.01f, 0.3f, 0.21f), new Color(0.97f, 0.97f, 0.9f), false);

            // Coat rack with a hi-vis "security" vest and a cap.
            Vector3 cr = new Vector3(X0 + 0.45f, y0, Z0 + 0.6f);
            Prim(root, PrimitiveType.Cylinder, "RackPole", cr + Vector3.up * 0.9f, new Vector3(0.04f, 0.9f, 0.04f), new Color(0.3f, 0.2f, 0.12f));
            Prim(root, PrimitiveType.Cylinder, "RackFoot", cr + Vector3.up * 0.02f, new Vector3(0.4f, 0.02f, 0.4f), new Color(0.3f, 0.2f, 0.12f));
            Box(root, "Vest", cr + new Vector3(0.12f, 1.35f, 0f), new Vector3(0.06f, 0.55f, 0.4f), new Color(0.95f, 0.85f, 0.1f), false);
            Box(root, "VestStripe", cr + new Vector3(0.155f, 1.3f, 0f), new Vector3(0.005f, 0.05f, 0.41f), new Color(0.85f, 0.85f, 0.85f), false);
            Prim(root, PrimitiveType.Cylinder, "Cap", cr + new Vector3(0f, 1.82f, 0f), new Vector3(0.2f, 0.05f, 0.2f), new Color(0.08f, 0.1f, 0.2f));

            // Water cooler.
            Vector3 wc = new Vector3(X0 + 0.45f, y0, Z0 + 1.9f);
            Box(root, "Cooler", wc + Vector3.up * 0.5f, new Vector3(0.32f, 1.0f, 0.32f), new Color(0.88f, 0.88f, 0.86f));
            Prim(root, PrimitiveType.Cylinder, "Bottle", wc + Vector3.up * 1.2f, new Vector3(0.26f, 0.2f, 0.26f), new Color(0.35f, 0.6f, 0.9f));
            Box(root, "Tap", wc + new Vector3(0.17f, 0.85f, 0f), new Vector3(0.03f, 0.04f, 0.05f), new Color(0.2f, 0.4f, 0.9f), false);

            // Bin with crumpled paper.
            Vector3 bin = new Vector3(cx - 4.4f, y0, Z1 - 1.6f);
            Prim(root, PrimitiveType.Cylinder, "Bin", bin + Vector3.up * 0.18f, new Vector3(0.28f, 0.18f, 0.28f), new Color(0.2f, 0.22f, 0.25f));
            for (int i = 0; i < 4; i++)
                Prim(root, PrimitiveType.Sphere, "PaperBall", bin + new Vector3(-0.06f + i * 0.04f, 0.38f + (i % 2) * 0.03f, (i % 2 == 0 ? 0.04f : -0.03f)), Vector3.one * 0.07f, new Color(0.92f, 0.92f, 0.88f));
            Prim(root, PrimitiveType.Sphere, "PaperBallFloor", bin + new Vector3(0.35f, 0.035f, -0.2f), Vector3.one * 0.07f, new Color(0.92f, 0.92f, 0.88f));

            // Fire extinguisher by the entrance.
            Vector3 ex = new Vector3(DoorA - 0.35f, y0, Z0 + 2.9f);
            Prim(root, PrimitiveType.Cylinder, "Extinguisher", ex + Vector3.up * 0.3f, new Vector3(0.15f, 0.25f, 0.15f), new Color(0.8f, 0.08f, 0.06f));
            Box(root, "ExtHead", ex + Vector3.up * 0.6f, new Vector3(0.06f, 0.08f, 0.1f), new Color(0.1f, 0.1f, 0.1f), false);

            // Ceiling vent and a cable tray to the rack.
            Box(root, "Vent", new Vector3(cx - 2f, y0 + Height - 0.03f, cz), new Vector3(0.6f, 0.04f, 0.6f), new Color(0.55f, 0.56f, 0.58f), false);
            for (int i = 0; i < 4; i++)
                Box(root, "VentSlat", new Vector3(cx - 2f, y0 + Height - 0.055f, cz - 0.22f + i * 0.15f), new Vector3(0.56f, 0.01f, 0.03f), new Color(0.3f, 0.3f, 0.32f), false);
            Box(root, "CableTray", new Vector3(cx - 1.5f, y0 + 2.75f, Z1 - 0.3f), new Vector3(8.5f, 0.05f, 0.25f), new Color(0.4f, 0.42f, 0.45f), false);
            for (int i = 0; i < 6; i++)
                Prim(root, PrimitiveType.Cylinder, "Cable", new Vector3(cx - 1.8f + i * 0.6f, y0 + 2.45f, Z1 - 0.2f), new Vector3(0.015f, 0.28f, 0.015f), new Color(0.05f, 0.05f, 0.05f));
        }

        private GameObject Box(Transform parent, string n, Vector3 worldPos, Vector3 size, Color c, bool collider = true, Vector3? euler = null)
            => Prim(parent, PrimitiveType.Cube, n, worldPos, size, c, euler, collider);

        private GameObject Prim(Transform parent, PrimitiveType type, string n, Vector3 worldPos, Vector3 scale, Color c, Vector3? euler = null, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = n;
            if (!collider) Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = worldPos;
            if (euler.HasValue) g.transform.rotation = Quaternion.Euler(euler.Value);
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
            return g;
        }

        private TextMesh MakeText(Transform parent, string n, Vector3 worldPos, string text, float charSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.text = text;
            tm.characterSize = charSize;
            tm.fontSize = 48;
            tm.anchor = anchor;
            tm.color = color;
            tm.richText = true;
            var mr = go.GetComponent<MeshRenderer>();
            if (_fontMat != null) mr.sharedMaterial = _fontMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }

        private void PaintButton(int i)
        {
            if (i < 0 || i >= _buttonRenderers.Count || _buttonRenderers[i] == null) return;
            bool on = i % Levels == _floor;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", on ? new Color(0.2f, 0.9f, 0.35f) : new Color(0.55f, 0.1f, 0.08f));
            _buttonRenderers[i].SetPropertyBlock(mpb);
        }

        // ================================================================ sound

        /// <summary>Relay click followed by old-TV snow (white noise, crackle), synthesised once.</summary>
        private static AudioClip MakeStaticClip(string name, float length, int seed)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(length * rate);
            var d = new float[n];
            var rnd = new System.Random(seed);
            float lp = 0f, hpPrev = 0f, hpOut = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // Click: sharp metallic tick + a small low thump.
                float click = 0.9f * Mathf.Exp(-t / 0.0025f) * Mathf.Sin(2f * Mathf.PI * 2300f * t)
                              + 0.45f * Mathf.Exp(-t / 0.012f) * Mathf.Sin(2f * Mathf.PI * 140f * t);
                // Snow: white noise, high-passed (hiss) and slightly low-passed, with random crackles.
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.55f;
                hpOut = 0.92f * (hpOut + lp - hpPrev);
                hpPrev = lp;
                float crackle = rnd.NextDouble() < 0.0015 ? (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.9f : 0f;
                float env = Mathf.Clamp01((t - 0.006f) / 0.015f) * Mathf.Clamp01((length - t) / (length * 0.55f));
                float wobble = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 50f * t); // mains hum in the noise
                d[i] = Mathf.Clamp(click + (0.42f * hpOut * wobble + crackle) * env, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Plays the switch sound at the monitor (or the whole wall for -1) in the room on the listener's level.</summary>
        private void PlayFeedSound(int slot)
        {
            if (!AnyRoomNear(out var viewer)) return;
            int lv = Mathf.Clamp(Mathf.RoundToInt(-(viewer.y - 1f) / 3.4f), 0, Levels - 1);
            Vector3 pos = slot >= 0 ? _slots[slot].ScreenPos[lv] : new Vector3((X0 + X1) * 0.5f, FloorY(lv) + 1.7f, Z1 - 0.2f);
            GameAudio.PlayClipAt(slot >= 0 ? _switchClip : _floorClip, pos, slot >= 0 ? 0.55f : 0.75f, 14f);
        }

        // ================================================================ runtime

        private void Update()
        {
            var mm = MatchManager.Instance;
            if (mm != null && mm.SecurityFloor.Value != _floor) ApplyFloor(mm.SecurityFloor.Value);
            if (Time.time >= _nextSense)
            {
                _nextSense = Time.time + 0.2f;
                Sense(Time.time - _lastSense);
                _lastSense = Time.time;
                Reassign();
            }
            UpdateSnow();
            RenderFeeds();
            AnimateProps();
            UpdateLeds();
            UpdateCrates();
        }

        private void AnimateProps()
        {
            var now = System.DateTime.Now;
            float minAngle = (now.Minute + now.Second / 60f) * 6f;
            float hourAngle = (now.Hour % 12 + now.Minute / 60f) * 30f;
            foreach (var (h, m) in _clocks)
            {
                if (h != null) h.rotation = Quaternion.LookRotation(Vector3.right) * Quaternion.Euler(0f, 0f, -hourAngle);
                if (m != null) m.rotation = Quaternion.LookRotation(Vector3.right) * Quaternion.Euler(0f, 0f, -minAngle);
            }
            foreach (var f in _fans)
                if (f != null) f.Rotate(0f, 0f, 900f * Time.deltaTime, Space.Self);
        }

        /// <summary>Motion detection: a moving, living player inside the view cone of a camera (line of sight) on its level.</summary>
        private void Sense(float dt)
        {
            if (dt <= 0f) return;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                Vector3 pos = p.transform.position;
                float speed = _prev.TryGetValue(p.NetworkObjectId, out var prev) ? Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(prev.x, 0f, prev.z)) / dt : 0f;
                _prev[p.NetworkObjectId] = pos;
                if (p.Life.Value == LifeState.Dead || speed < MotionMinSpeed || speed > 40f) continue;
                int lv = Mathf.RoundToInt(-pos.y / 3.4f);
                if (lv < 0 || lv >= Levels) continue;
                Vector3 target = pos + Vector3.up * 1.0f;
                foreach (int ci in _levelCams[lv])
                {
                    var c = _cams[ci];
                    Vector3 to = target - c.Pos;
                    float d = to.magnitude;
                    if (d > MotionRange || d < 1f) continue;
                    if (Vector3.Angle(c.Rot * Vector3.forward, to) > MotionHalfAngle) continue;
                    if (Physics.Linecast(c.Pos, target, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<NetPlayer>() != p) continue;
                    c.LastMotion = Time.time;
                    _cams[ci] = c;
                }
            }
        }

        /// <summary>All monitors: cameras with the freshest motion first, then the rest in fixed order.</summary>
        private void AssignAll()
        {
            var order = new List<int>(_levelCams[_floor]);
            order.Sort((a, b) =>
            {
                int cmp = _cams[b].LastMotion.CompareTo(_cams[a].LastMotion);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });
            for (int s = 0; s < Monitors; s++)
            {
                _slots[s].CamIndex = s < order.Count ? order[s] : -1;
                _slots[s].NextRender = 0f;
            }
            UpdateLabels();
        }

        /// <summary>A camera that just saw motion and is not on a monitor takes over the monitor with the oldest picture.</summary>
        private void Reassign()
        {
            bool changed = false;
            foreach (int ci in _levelCams[_floor])
            {
                if (Time.time - _cams[ci].LastMotion > 1f) continue;
                bool shown = false;
                foreach (var s in _slots) if (s.CamIndex == ci) { shown = true; break; }
                if (shown) continue;
                Slot best = null;
                int bestIndex = -1;
                for (int i = 0; i < Monitors; i++)
                {
                    var s = _slots[i];
                    if (Time.time - s.Since < 3f) continue;
                    float age = s.CamIndex >= 0 ? _cams[s.CamIndex].LastMotion : -999f;
                    if (best == null || age < (best.CamIndex >= 0 ? _cams[best.CamIndex].LastMotion : -999f)) { best = s; bestIndex = i; }
                }
                if (best == null || (best.CamIndex >= 0 && Time.time - _cams[best.CamIndex].LastMotion < 2f)) continue;
                best.CamIndex = ci;
                best.Since = Time.time;
                best.NextRender = 0f;
                PlayFeedSound(bestIndex);
                changed = true;
            }
            if (changed || Time.frameCount % 6 == 0) UpdateLabels();
        }

        private void UpdateLabels()
        {
            for (int s = 0; s < Monitors; s++)
            {
                var sl = _slots[s];
                string txt = "BRAK SYGNAŁU";
                bool motion = false;
                if (sl.CamIndex >= 0)
                {
                    var cam = _cams[sl.CamIndex];
                    motion = Time.time - cam.LastMotion < 4f;
                    txt = $"{cam.Name}  P{LevelName(cam.Level)}" + (motion ? "  <color=#ff4040>RUCH</color>" : "");
                }
                foreach (var l in sl.Labels) if (l != null) l.text = txt;
                foreach (var mat in sl.Mats) mat.SetFloat("_Alarm", motion ? 1f : 0f);
            }
        }

        /// <summary>Old-TV snow on a monitor right after it switches.</summary>
        private void UpdateSnow()
        {
            foreach (var s in _slots)
            {
                float k = Mathf.Clamp01(1f - (Time.time - s.Since) / SnowTime);
                if (s.CamIndex < 0) k = 1f;
                foreach (var mat in s.Mats)
                {
                    mat.SetFloat("_Snow", k);
                    mat.SetFloat("_Glitch", k > 0.01f ? 1f : 0f);
                }
            }
        }

        private bool AnyRoomNear(out Vector3 viewer)
        {
            viewer = default;
            var cam = Camera.main;
            if (cam == null) return false;
            viewer = cam.transform.position;
            if (viewer.x < X0 - 6f || viewer.x > X1 + 6f || viewer.z < Z0 - 6f || viewer.z > Z1 + 2f) return false;
            for (int lv = 0; lv < Levels; lv++)
                if (viewer.y > FloorY(lv) - 0.5f && viewer.y < FloorY(lv) + 3.2f) return true;
            return false;
        }

        private void RenderFeeds()
        {
            if (!AnyRoomNear(out _)) return;
            // Every monitor at a steady 30 fps; the ticks of the monitors are staggered so the work is spread over frames.
            var prevShadows = QualitySettings.shadows;
            bool any = false;
            float step = 1f / FeedFps;
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s.CamIndex < 0 || Time.time < s.NextRender) continue;
                s.NextRender = Time.time - s.NextRender > step ? Time.time + step * i / _slots.Length : s.NextRender + step;
                if (!any) { QualitySettings.shadows = ShadowQuality.Disable; any = true; }
                var c = _cams[s.CamIndex];
                s.Cam.transform.SetPositionAndRotation(c.Pos, c.Rot);
                s.Ir.enabled = true;
                s.Cam.Render();
                s.Ir.enabled = false;
            }
            if (any) QualitySettings.shadows = prevShadows;
        }
    }
}
