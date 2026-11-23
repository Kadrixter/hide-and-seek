using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Toy bricks in the lobby corner by the floor lamp and the plant: a tub full of bricks (E = start building) and a
    /// green baseplate (18 x 16 studs). While building, the crosshair shows a ghost brick snapped to the grid (like Minecraft);
    /// LMB puts it down, RMB / Z takes back the own last brick, 1-5 / wheel picks one of 5 colours, E stops.
    /// Everybody sees the bricks (network list on the match manager); they stay until the server shuts down.
    /// </summary>
    public class LobbyBricks : MonoBehaviour
    {
        // ---- grid
        // 18 x 16 cells (2.7 x 2.4 m) between the floor lamp / brick tub (west) and the sofa (east), clear of the north wall.
        public const int SizeX = 18, SizeZ = 16, SizeY = 14;
        public const float Cell = BlockyProps.BrickSize;
        public const float PlateTop = 0.02f;
        /// <summary>Grid corner (x0, plate top, z0).</summary>
        public static readonly Vector3 Origin = new Vector3(LobbyLayout.BrickOrigin.x, PlateTop, LobbyLayout.BrickOrigin.z);
        public static readonly Vector3 TubPos = LobbyLayout.BrickTub;
        public const float TubYaw = LobbyLayout.BrickTubYaw;
        public const float Reach = 4.5f, ReachServer = 5.5f, LeaveDistance = 4.2f;

        public static readonly Color[] Colors =
        {
            new Color(0.86f, 0.1f, 0.08f),  // red
            new Color(0.1f, 0.36f, 0.9f),   // blue
            new Color(1f, 0.9f, 0.18f),     // yellow
            new Color(0.12f, 0.64f, 0.2f),  // green
            new Color(0.95f, 0.95f, 0.93f), // white
        };
        public static readonly string[] ColorNames = { "czerwony", "niebieski", "żółty", "zielony", "biały" };

        public static readonly Vector3Int[] Neighbours =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        public static int Pack(int x, int y, int z, int color) => x | (y << 8) | (z << 16) | (color << 24);
        public static void Unpack(int p, out int x, out int y, out int z, out int color)
        {
            x = p & 0xFF;
            y = (p >> 8) & 0xFF;
            z = (p >> 16) & 0xFF;
            color = (p >> 24) & 0xFF;
        }
        public static bool InGrid(int x, int y, int z) => x >= 0 && x < SizeX && z >= 0 && z < SizeZ && y >= 0 && y < SizeY;
        /// <summary>Bottom centre of a cell (world).</summary>
        public static Vector3 CellBottom(int x, int y, int z) => Origin + new Vector3((x + 0.5f) * Cell, y * Cell, (z + 0.5f) * Cell);
        public static Vector3 CellCenter(int x, int y, int z) => CellBottom(x, y, z) + Vector3.up * (Cell * 0.5f);
        public static Vector3 PlateCenter => Origin + new Vector3(SizeX * Cell * 0.5f, 0f, SizeZ * Cell * 0.5f);
        public static Vector3 TubAim => TubPos + Vector3.up * 0.25f;
        public static Transform Tub { get; private set; }

        public static LobbyBricks Instance { get; private set; }

        /// <summary>Local player is building right now.</summary>
        public static bool Building { get; private set; }
        public static int SelectedColor { get; private set; }

        private readonly Dictionary<int, GameObject> _views = new();
        private Material[] _mats;
        private Collider _plateCollider;
        private GameObject _ghost;
        private Renderer _ghostRenderer;
        private MaterialPropertyBlock _ghostBlock;
        private bool _dirty = true, _hooked;
        private bool _hasTarget, _targetValid;
        private int _tx, _ty, _tz;
        private GUIStyle _hint;
        private Texture2D _white;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("LobbyBricks");
            Instance = go.AddComponent<LobbyBricks>();
            Instance.Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var mm = MatchManager.Instance;
            if (_hooked && mm != null && mm.Bricks != null) mm.Bricks.OnListChanged -= OnListChanged;
            if (Building) StopBuildingLocal();
        }

        private void OnListChanged(Unity.Netcode.NetworkListEvent<int> e) => _dirty = true;

        // ================================================================ scenery

        private Material Mat(Color c, float gloss)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        private void Build()
        {
            _mats = new Material[Colors.Length];
            for (int i = 0; i < Colors.Length; i++) _mats[i] = Mat(Colors[i], 0.55f);

            // Baseplate: dark green with a stud on every cell.
            var plate = new GameObject("Baseplate");
            plate.transform.SetParent(transform, false);
            plate.transform.position = Origin;
            var studs = new List<CombineInstance>();
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            studs.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(new Vector3(SizeX * Cell * 0.5f, -PlateTop * 0.5f, SizeZ * Cell * 0.5f), Quaternion.identity, new Vector3(SizeX * Cell + 0.04f, PlateTop, SizeZ * Cell + 0.04f)) });
            for (int x = 0; x < SizeX; x++)
            for (int z = 0; z < SizeZ; z++)
                studs.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(new Vector3((x + 0.5f) * Cell, 0.008f, (z + 0.5f) * Cell), Quaternion.identity, new Vector3(Cell * 0.42f, 0.016f, Cell * 0.42f)) });
            var mesh = new Mesh { name = "Baseplate", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(studs.ToArray(), true, true);
            plate.AddComponent<MeshFilter>().sharedMesh = mesh;
            plate.AddComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.16f, 0.45f, 0.18f), 0.35f);
            var pc = plate.AddComponent<BoxCollider>();
            pc.center = new Vector3(SizeX * Cell * 0.5f, -PlateTop * 0.5f, SizeZ * Cell * 0.5f);
            pc.size = new Vector3(SizeX * Cell + 0.04f, PlateTop, SizeZ * Cell + 0.04f);
            _plateCollider = pc;

            // Toy tub: bright blue open box full of bricks, a lid leaning on it, a few bricks on the floor.
            var tub = new GameObject("BrickTub").transform;
            tub.SetParent(transform, false);
            tub.SetPositionAndRotation(TubPos, Quaternion.Euler(0f, TubYaw, 0f));
            Tub = tub;
            var tubMat = Mat(new Color(0.12f, 0.5f, 0.85f), 0.5f);
            const float w = 0.52f, d = 0.38f, h = 0.26f, t = 0.02f;
            Part(tub, new Vector3(0f, t * 0.5f, 0f), new Vector3(w, t, d), tubMat, false);
            Part(tub, new Vector3(0f, h * 0.5f, d * 0.5f - t * 0.5f), new Vector3(w, h, t), tubMat, false);
            Part(tub, new Vector3(0f, h * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(w, h, t), tubMat, false);
            Part(tub, new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d - 2f * t), tubMat, false);
            Part(tub, new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d - 2f * t), tubMat, false);
            Part(tub, new Vector3(0f, h - 0.012f, -d * 0.5f - 0.004f), new Vector3(w * 0.6f, 0.05f, 0.006f), Mat(new Color(1f, 0.85f, 0.1f), 0.4f), false); // label
            var col = tub.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, d);
            var rnd = new System.Random(5);
            var brickMesh = BlockyProps.GetBrickMesh();
            for (int i = 0; i < 16; i++)
            {
                var b = new GameObject("TubBrick").transform;
                b.SetParent(tub, false);
                b.localPosition = new Vector3((float)(rnd.NextDouble() - 0.5) * (w - 0.14f), 0.03f + (i / 6) * 0.07f, (float)(rnd.NextDouble() - 0.5) * (d - 0.14f));
                b.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 40f - 20f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 40f - 20f);
                b.localScale = Vector3.one * 0.6f;
                b.gameObject.AddComponent<MeshFilter>().sharedMesh = brickMesh;
                b.gameObject.AddComponent<MeshRenderer>().sharedMaterial = _mats[i % _mats.Length];
            }
            foreach (var (pos, yaw, ci) in new[] { (new Vector3(0.38f, 0f, -0.12f), 30f, 0), (new Vector3(0.45f, 0f, 0.1f), -15f, 2), (new Vector3(-0.1f, 0f, -0.36f), 70f, 1) })
            {
                var b = new GameObject("FloorBrick").transform;
                b.SetParent(tub, false);
                b.localPosition = pos;
                b.localRotation = Quaternion.Euler(0f, yaw, 0f);
                b.localScale = Vector3.one * 0.6f;
                b.gameObject.AddComponent<MeshFilter>().sharedMesh = brickMesh;
                b.gameObject.AddComponent<MeshRenderer>().sharedMaterial = _mats[ci];
            }

            // Ghost brick (preview).
            _ghost = new GameObject("BrickGhost");
            _ghost.transform.SetParent(transform, false);
            _ghost.AddComponent<MeshFilter>().sharedMesh = brickMesh;
            _ghostRenderer = _ghost.AddComponent<MeshRenderer>();
            _ghostRenderer.sharedMaterial = FxUtil.Unlit;
            _ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghostBlock = new MaterialPropertyBlock();
            _ghost.SetActive(false);
        }

        private static void Part(Transform parent, Vector3 lp, Vector3 size, Material m, bool collider)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Destroy(g.GetComponent<Collider>());
        }

        // ================================================================ sync with the network list

        private void Update()
        {
            var mm = MatchManager.Instance;
            if (mm == null || mm.Bricks == null) return;
            if (!_hooked)
            {
                _hooked = true;
                mm.Bricks.OnListChanged += OnListChanged;
                _dirty = true;
            }
            if (_dirty) Sync(mm);
        }

        private void Sync(MatchManager mm)
        {
            _dirty = false;
            var want = new Dictionary<int, int>();
            for (int i = 0; i < mm.Bricks.Count; i++) want[mm.Bricks[i] & 0xFFFFFF] = mm.Bricks[i];
            var remove = new List<int>();
            foreach (var kv in _views)
                if (!want.TryGetValue(kv.Key, out int p) || kv.Value == null || (int)kv.Value.GetComponent<BrickCell>().Packed != p) remove.Add(kv.Key);
            foreach (int k in remove)
            {
                if (_views[k] != null) Destroy(_views[k]);
                _views.Remove(k);
            }
            var mesh = BlockyProps.GetBrickMesh();
            foreach (var kv in want)
            {
                if (_views.ContainsKey(kv.Key)) continue;
                Unpack(kv.Value, out int x, out int y, out int z, out int c);
                var g = new GameObject("Brick");
                g.transform.SetParent(transform, false);
                g.transform.position = CellBottom(x, y, z);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                g.AddComponent<MeshRenderer>().sharedMaterial = _mats[Mathf.Clamp(c, 0, _mats.Length - 1)];
                var bc = g.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, Cell * 0.5f, 0f);
                bc.size = Vector3.one * Cell;
                g.AddComponent<BrickCell>().Packed = kv.Value;
                _views[kv.Key] = g;
            }
        }

        // ================================================================ local building

        public static void BeginBuilding()
        {
            if (Instance == null || NetPlayer.Local == null) return;
            Building = true;
            var me = NetPlayer.Local;
            me.BrickColor.Value = (byte)(SelectedColor + 1);
            GameHud.Toast("Budujesz z klocków. LPM - postaw, PPM / Z - cofnij, 1-5 - kolor, E - koniec.", 4f);
        }

        public static void StopBuilding()
        {
            if (!Building) return;
            StopBuildingLocal();
        }

        private static void StopBuildingLocal()
        {
            Building = false;
            var me = NetPlayer.Local;
            if (me != null && me.IsSpawned && me.BrickColor.Value != 0) me.BrickColor.Value = 0;
            if (Instance != null && Instance._ghost != null) Instance._ghost.SetActive(false);
        }

        /// <summary>Owner input while building. Returns true while building (other prompts are off).</summary>
        public static bool TestClick, TestUndo;
        public static int TestColor = -1;

        public static bool LocalTick(NetPlayer me, Keyboard kb, Mouse mouse, bool ignoreE)
        {
            if (!Building) return false;
            var inst = Instance;
            if (inst == null || me == null || me.Camera == null) { StopBuildingLocal(); return false; }
            Vector3 flat = me.transform.position - PlateCenter;
            flat.y = 0f;
            if (flat.magnitude > LeaveDistance || me.Busy.Value || me.Seat.Value >= 0)
            {
                StopBuildingLocal();
                return false;
            }
            if (kb.eKey.wasPressedThisFrame && !ignoreE) { StopBuildingLocal(); return true; }

            // Colour: 1-5 or the wheel.
            int sel = SelectedColor;
            if (kb.digit1Key.wasPressedThisFrame) sel = 0;
            if (kb.digit2Key.wasPressedThisFrame) sel = 1;
            if (kb.digit3Key.wasPressedThisFrame) sel = 2;
            if (kb.digit4Key.wasPressedThisFrame) sel = 3;
            if (kb.digit5Key.wasPressedThisFrame) sel = 4;
            if (TestColor >= 0) { sel = TestColor; TestColor = -1; }
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0.01f) sel = (sel + Colors.Length - 1) % Colors.Length;
            else if (wheel < -0.01f) sel = (sel + 1) % Colors.Length;
            if (sel != SelectedColor)
            {
                SelectedColor = sel;
                me.BrickColor.Value = (byte)(sel + 1);
                HideAndSeek.Audio.GameAudio.Play2D(HideAndSeek.Audio.GameSfx.UiHover, 0.6f);
            }

            inst.Aim(me);
            if ((mouse.leftButton.wasPressedThisFrame && me.InputReader.CursorLocked) || TestClick)
            {
                if (TestClick) Debug.Log($"[BrickTest] click target={inst._hasTarget} valid={inst._targetValid} cell=({inst._tx},{inst._ty},{inst._tz})");
                TestClick = false;
                if (inst._hasTarget && inst._targetValid) me.RequestPlaceBrickRpc(Pack(inst._tx, inst._ty, inst._tz, SelectedColor));
                else HideAndSeek.Audio.GameAudio.Play2D(HideAndSeek.Audio.GameSfx.UiError, 0.4f);
            }
            if ((mouse.rightButton.wasPressedThisFrame && me.InputReader.CursorLocked) || kb.zKey.wasPressedThisFrame || TestUndo)
            {
                TestUndo = false;
                me.RequestUndoBrickRpc();
            }
            return true;
        }

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Crosshair ray: a brick face = the cell next to it, the plate = the cell on it.</summary>
        private void Aim(NetPlayer me)
        {
            _hasTarget = false;
            var cam = me.Camera.transform;
            int mask = ~((1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9) | (1 << 2));
            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, _hits, Reach, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit hit = default;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].transform.IsChildOf(me.transform)) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; hit = _hits[i]; }
            }
            if (best < float.MaxValue)
            {
                var bc = hit.collider.GetComponent<BrickCell>();
                if (bc != null)
                {
                    Unpack(bc.Packed, out int x, out int y, out int z, out _);
                    Vector3 nrm = hit.normal;
                    var d = new Vector3Int(Mathf.RoundToInt(nrm.x), Mathf.RoundToInt(nrm.y), Mathf.RoundToInt(nrm.z));
                    _tx = x + d.x; _ty = y + d.y; _tz = z + d.z;
                    _hasTarget = true;
                }
                else if (hit.collider == _plateCollider && hit.normal.y > 0.5f)
                {
                    Vector3 l = hit.point - Origin;
                    _tx = Mathf.FloorToInt(l.x / Cell);
                    _tz = Mathf.FloorToInt(l.z / Cell);
                    _ty = 0;
                    _hasTarget = true;
                }
            }
            _targetValid = _hasTarget && InGrid(_tx, _ty, _tz) && !Occupied(_tx, _ty, _tz) && !InsidePlayer(_tx, _ty, _tz);
            if (_hasTarget && !InGrid(_tx, _ty, _tz)) _hasTarget = false;
            _ghost.SetActive(_hasTarget);
            if (!_hasTarget) return;
            _ghost.transform.position = CellBottom(_tx, _ty, _tz) + Vector3.up * 0.001f;
            _ghost.transform.localScale = Vector3.one * 1.01f;
            var c = _targetValid ? Colors[SelectedColor] : new Color(1f, 0.15f, 0.1f);
            c.a = _targetValid ? 0.5f + 0.12f * Mathf.Sin(Time.time * 6f) : 0.35f;
            _ghostBlock.SetColor("_Color", c);
            _ghostRenderer.SetPropertyBlock(_ghostBlock);
        }

        private bool Occupied(int x, int y, int z) => _views.ContainsKey(Pack(x, y, z, 0));

        /// <summary>Never build into somebody standing there (the cell overlaps a player capsule).</summary>
        private static bool InsidePlayer(int x, int y, int z)
        {
            Vector3 c = CellCenter(x, y, z);
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                Vector3 d = c - p.transform.position;
                if (d.y < -Cell || d.y > 1.85f) continue;
                d.y = 0f;
                if (d.magnitude < 0.3f + Cell * 0.5f) return true;
            }
            return false;
        }

        private void OnGUI()
        {
            if (!Building || Event.current.type != EventType.Repaint) return;
            if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            if (_hint == null) _hint = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 46f), alignment = TextAnchor.MiddleCenter };
            float h = Screen.height, w = Screen.width;
            float sw = h * 0.055f, gap = h * 0.012f;
            float total = Colors.Length * sw + (Colors.Length - 1) * gap;
            float x0 = (w - total) * 0.5f, y0 = h - h * 0.17f;
            for (int i = 0; i < Colors.Length; i++)
            {
                float x = x0 + i * (sw + gap);
                bool sel = i == SelectedColor;
                GUI.color = sel ? Color.white : new Color(0f, 0f, 0f, 0.6f);
                float pad = sel ? h * 0.006f : h * 0.003f;
                GUI.DrawTexture(new Rect(x - pad, y0 - pad, sw + pad * 2f, sw + pad * 2f), _white);
                GUI.color = Colors[i];
                GUI.DrawTexture(new Rect(x, y0, sw, sw), _white);
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(x, y0 + sw * 0.05f, sw, sw), (i + 1).ToString(), _hint);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(0, y0 - h * 0.05f, w, h * 0.04f), $"Kolor: {ColorNames[SelectedColor]}     LPM - postaw    PPM / Z - cofnij    1-5 / kółko - kolor    E - koniec", _hint);
        }
    }

    /// <summary>Marks a placed brick (packed cell + colour) for the aiming ray.</summary>
    public class BrickCell : MonoBehaviour
    {
        public int Packed;
    }
}
