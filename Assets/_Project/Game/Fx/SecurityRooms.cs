using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Hidden "security" rooms on levels 0, -1 and -2 (north-east open area, against the north wall). The doorway is
    /// blocked by a heavy crate (SecurityRooms.Crate*.cs): hiders push it aside or back, the seeker can only cut it
    /// apart with the chainsaw. Each room has 6 monitors with live feeds of the garage cameras (aisles, cross aisles,
    /// stairwell doors, elevator doors) and three buttons that choose which level the cameras show. The chosen level is
    /// synced by the server (<see cref="MatchManager.SecurityFloor"/>), so every player sees the same monitors. A monitor
    /// switches to a camera that detects motion, with a click and old-TV snow. Feeds render only while somebody is near
    /// a room. The room itself is dark like the garage: the light comes from the monitors and a desk lamp.
    /// Parts: .Parts (materials, primitives), .Shell (walls, monitor wall, console, lights), .Furniture, .Desk, .Walls,
    /// .Cameras (CCTV points, motion), .Monitors (feeds, OSD), .Crate / .CrateBuild / .CrateFx (the crate).
    /// </summary>
    public partial class SecurityRooms : MonoBehaviour
    {
        public static SecurityRooms Instance { get; private set; }

        // Room footprint (same on every level): x [66, 78], z [52, 59.55] (north-east open area); door in the south wall.
        private const float X0 = 66f, X1 = 78f, Z0 = 52f, Z1 = 59.55f, Wall = 0.2f, Height = 2.95f;
        private const float GarageWallZ = 60f;   // garage north wall; its pilasters (x 67.5, 75) reach z 59.65, hidden behind the room's north lining
        private const float DoorA = X0 + 10.2f, DoorB = X0 + 11.4f;   // entrance gap (x) near the east end
        private const int Levels = 3, Monitors = 6;

        private readonly List<(Vector3 pos, int floor)> _buttons = new();
        private readonly List<Renderer> _buttonRenderers = new();
        private readonly List<(Transform hour, Transform minute)> _clocks = new();
        private readonly List<Transform> _fans = new();
        private int _floor;                 // level whose cameras the monitors show (0, 1, 2 = levels 0, -1, -2)

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("SecurityRooms");
            Instance = go.AddComponent<SecurityRooms>();
            Instance.StartCoroutine(Instance.BuildRoutine());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var s in _slots)
            {
                if (s == null) continue;
                if (s.Rt != null) s.Rt.Release();
                foreach (var m in s.Mats) if (m != null) Destroy(m);   // per-round screen materials (the shared ones are cached)
            }
        }

        public static string LevelName(int lv) => lv == 0 ? "0" : "-" + lv;
        private static float FloorY(int lv) => -3.4f * lv;
        public static Vector3 RoomFloorPoint(int level) => new Vector3((X0 + X1) * 0.5f + 1.5f, FloorY(level), 56.6f);

        /// <summary>
        /// Built over several frames (shared resources, cameras + feeds, then one level per frame) so the round start
        /// does not stall; textures, materials, meshes and sounds are cached for the session and normally already
        /// prepared in the menu / lobby (SecurityRooms.Warm.cs). <see cref="Ready"/> once everything stands.
        /// </summary>
        private System.Collections.IEnumerator BuildRoutine()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var parts = new System.Text.StringBuilder();
            float worst = 0f;
            void Lap(string what)
            {
                float ms = (float)sw.Elapsed.TotalMilliseconds;
                worst = Mathf.Max(worst, ms);
                parts.Append($" {what} {ms:0}");
                sw.Restart();
            }
            float total0 = Time.realtimeSinceStartup;
            _monitorShader = Resources.Load<Shader>("Shaders/HnsMonitor");
            if (_switchClip == null) _switchClip = Keep(MakeStaticClip("MonitorSwitch", 0.5f, 11));
            if (_floorClip == null) _floorClip = Keep(MakeStaticClip("MonitorFloor", 0.95f, 23));
            BuildCrateSounds();
            for (int lv = 0; lv < Levels; lv++)
            {
                BuildCameraPoints(lv);
                Lap("cams" + LevelName(lv));
                yield return null;
                sw.Restart();
            }
            for (int i = 0; i < Monitors; i++) _slots[i] = MakeSlot(i);
            for (int lv = 0; lv < Levels; lv++)
            {
                BuildRoom(lv);
                Lap("level" + LevelName(lv));
                yield return null;
                sw.Restart();
            }
            var mm = MatchManager.Instance;
            _floor = mm != null ? Mathf.Clamp(mm.SecurityFloor.Value, 0, Levels - 1) : 0;
            AssignAll();
            for (int i = 0; i < _buttonRenderers.Count; i++) PaintButton(i);
            Lap("assign");
            Ready = true;
            Debug.Log($"[Security] rooms built on 3 levels, {_cams.Count} cameras, {_chairs.Count} chairs: longest frame {worst:0} ms, " +
                      $"total {(Time.realtimeSinceStartup - total0) * 1000f:0} ms over {2 * Levels + 1} frames (ms:{parts}; prewarmed {WarmDone})");
        }

        /// <summary>Everything built (the rooms appear over the first few frames of a round).</summary>
        public bool Ready { get; private set; }

        private void BuildRoom(int lv)
        {
            float y0 = FloorY(lv);
            var root = new GameObject("SecurityRoom_L" + LevelName(lv)).transform;
            root.SetParent(transform, false);
            float cx = (X0 + X1) * 0.5f, cz = (Z0 + Z1) * 0.5f;

            // Probe the footprint first: log anything of the garage that is in the way.
            var probe = Physics.OverlapBox(new Vector3(cx, y0 + 1.5f, cz), new Vector3((X1 - X0) * 0.5f - 0.1f, 1.2f, (Z1 - Z0) * 0.5f - 0.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in probe)
                if (c.GetComponentInParent<NetPlayer>() == null) Debug.Log($"[Security] level {LevelName(lv)}: footprint overlaps '{c.name}' at {c.transform.position}");
            ClearProps(lv, cx, cz, y0);

            BuildShell(root, lv, cx, cz, y0);
            BuildCrate(root, lv);
            BuildDoorSign(root, lv, y0);
            BuildMonitorWall(root, lv, cx, y0);
            BuildConsole(root, lv, cx, y0);
            BuildDeskClutter(root, lv, cx, y0 + 0.77f);
            BuildFurniture(root, lv, cx, cz, y0);
            BuildWallDetails(root, lv, cx, cz, y0);
            BuildPower(root, lv, y0);   // camera power: battery tray + switch (SecurityRooms.Power.cs)
            BuildLights(root, lv, cx, y0);
            HideAndSeek.Map.LevelLighting.SetLevelLayer(root, lv);   // lights of the other levels skip the room
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

        /// <summary>Server: a living player stands inside one of the security rooms (the cameras are being watched).</summary>
        public bool AnyoneWatching()
        {
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned || p.Life.Value == LifeState.Dead) continue;
                Vector3 v = p.transform.position;
                if (v.x < X0 || v.x > X1 || v.z < Z0 || v.z > Z1) continue;
                for (int lv = 0; lv < Levels; lv++)
                    if (v.y > FloorY(lv) - 0.5f && v.y < FloorY(lv) + 2.5f && RoomPowered(lv)) return true; // dark monitors watch nothing
            }
            return false;
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

        // ================================================================ runtime

        private void Update()
        {
            if (!Ready) return;
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
            UpdateOsd();
            RenderFeeds();
            UpdateScreenLights();
            UpdatePower();   // rooms without battery power go dark (SecurityRooms.Power.cs)
            AnimateProps();
            UpdateLeds();
            UpdateCrates();
            UpdateChips();
            UpdateBlinkLeds();
        }

        private void AnimateProps()
        {
            var now = System.DateTime.Now;
            float minAngle = (now.Minute + now.Second / 60f) * 6f;
            float hourAngle = (now.Hour % 12 + now.Minute / 60f) * 30f;
            foreach (var (h, m) in _clocks)
            {
                if (h != null) h.localRotation = Quaternion.Euler(0f, 0f, -hourAngle);
                if (m != null) m.localRotation = Quaternion.Euler(0f, 0f, -minAngle);
            }
            foreach (var f in _fans)
                if (f != null) f.Rotate(0f, 0f, 900f * Time.deltaTime, Space.Self);
        }
    }
}
