using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Bottom-left minimap. The level is scanned once per loaded map with downward raycasts (one texture per floor),
    /// then shaded (outlined walls, soft cars, hatched stairs and ramps, a 10 m grid). The view is north-up and centred
    /// on the local player (or the watched player when spectating). M cycles the zoom. Markers: view cone, teammates
    /// (with up / down arrows for other floors), pulsing downed hiders, hint pings with ripples, stairs and lifts,
    /// level tabs with teammate counts, compass and scale bar.
    /// </summary>
    public partial class Minimap : MonoBehaviour
    {
        private const float Cell = 0.5f;
        private static readonly float[] Zooms = { 30f, 56f, 100f };
        private const float PanelSize = 320f;

        private float _viewMetres = 56f;
        private float _viewShown = 56f;
        private int _zoomIndex = 1;

        private RectTransform _root, _clip;
        private RawImage _map;
        private RectTransform _arrow, _cone;
        private Text _floorLabel, _northLabel, _scaleLabel;
        private RectTransform _scaleBar;
        // anyFloor: car alarms; hint pings (forced sounds of hiders) only show while the seeker is on their level.
        private readonly List<(RectTransform rt, Vector3 world, float until, float born, bool anyFloor)> _pins = new();

        /// <summary>Test: hint / alarm pins drawn in the last frame.</summary>
        public static int VisiblePins { get; private set; }
        /// <summary>Test: hint pins alive but hidden (pinged on another level).</summary>
        public static int HiddenPins { get; private set; }
        private readonly List<Text> _tabs = new();
        private readonly List<Text> _tabCounts = new();

        private string _scannedScene;
        private Bounds _bounds;
        private readonly List<float> _floors = new();
        private readonly List<Texture2D> _textures = new();
        private bool _ready;

        // Points of interest (world x, z): stairwells and ramps are fixed map features.
        private static readonly Vector2[] StairSpots = { new Vector2(-60f, 56f), new Vector2(48f, 56f), new Vector2(-32f, 0f), new Vector2(32f, 0f) };

        private bool _liftsBuilt;
        private readonly List<RectTransform> _poi = new();
        private readonly List<Vector2> _poiWorld = new();

        public void Build(Transform canvas)
        {
            var frame = UiKit.Image("Minimap", canvas, new Color(0.03f, 0.035f, 0.04f, 0.82f));
            _root = frame.rectTransform;
            _root.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(20, 20), new Vector2(PanelSize, PanelSize));

            var clipGo = UiKit.Rect("Clip", _root);
            clipGo.Stretch(4);
            clipGo.gameObject.AddComponent<RectMask2D>();
            _clip = clipGo;
            var mapGo = UiKit.Rect("Map", _clip);
            mapGo.Stretch();
            _map = mapGo.gameObject.AddComponent<RawImage>();
            _map.color = new Color(1f, 1f, 1f, 0.95f);
            _map.raycastTarget = false;

            // Edge fade inside the map (soft vignette).
            var fade = UiKit.Image("Fade", _clip, Color.white);
            fade.rectTransform.Stretch();
            fade.sprite = EdgeFadeSprite();
            fade.color = new Color(0.02f, 0.025f, 0.03f, 0.75f);
            fade.raycastTarget = false;

            // View cone + arrow of the player.
            var cone = UiKit.Image("Cone", _clip, new Color(1f, 0.95f, 0.55f, 0.22f));
            cone.sprite = ConeSprite();
            cone.raycastTarget = false;
            _cone = cone.rectTransform;
            _cone.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(110, 110));
            var arrow = UiKit.Image("Me", _clip, new Color(1f, 0.95f, 0.5f, 1f));
            arrow.sprite = ArrowSprite();
            arrow.raycastTarget = false;
            _arrow = arrow.rectTransform;
            _arrow.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));

            // Fixed map features: stairwells.
            foreach (var s in StairSpots)
            {
                var icon = UiKit.Image("Stairs", _clip, new Color(0.5f, 0.78f, 1f, 0.95f));
                icon.sprite = StairIconSprite();
                icon.raycastTarget = false;
                icon.rectTransform.sizeDelta = new Vector2(16, 16);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _poi.Add(icon.rectTransform);
                _poiWorld.Add(s);
            }

            // Border: thin accent lines + corner brackets.
            var edge = new Color(0.45f, 0.8f, 0.62f, 0.7f);
            Line(_root, "BT", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), new Vector2(0, 2), edge);
            Line(_root, "BB", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, 2), edge);
            Line(_root, "BL", new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0.5f), new Vector2(2, 0), edge);
            Line(_root, "BR", new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(2, 0), edge);

            // Header: level name; compass N; scale bar.
            _floorLabel = UiKit.Label("Floor", _root, "", 20, TextAnchor.UpperLeft, new Color(0.85f, 0.95f, 0.88f));
            _floorLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -7), new Vector2(200, 26));
            _floorLabel.AddOutline();
            _northLabel = UiKit.Label("North", _root, "N", 20, TextAnchor.UpperCenter, new Color(1f, 0.6f, 0.5f));
            _northLabel.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(40, 26));
            _northLabel.AddOutline();
            _scaleLabel = UiKit.Label("Scale", _root, "", 15, TextAnchor.LowerRight, new Color(0.8f, 0.85f, 0.82f));
            _scaleLabel.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 20), new Vector2(120, 20));
            _scaleLabel.AddOutline();
            var bar = UiKit.Image("ScaleBar", _root, new Color(0.9f, 0.95f, 0.9f, 0.9f));
            bar.raycastTarget = false;
            _scaleBar = bar.rectTransform;
            _scaleBar.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 14), new Vector2(60, 3));
            // "M - skala" is shown with the other key hints at the start of the round (KeyHints).

            // Level tabs on the right edge: 0, -1, -2 with the number of teammates on each level.
            for (int i = 0; i < 3; i++)
            {
                var t = UiKit.Label("Tab" + i, _root, i == 0 ? "0" : "-" + i, 18, TextAnchor.MiddleCenter, UiKit.TextDim);
                t.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -34 - i * 34), new Vector2(34, 22));
                t.AddOutline();
                _tabs.Add(t);
                var c = UiKit.Label("TabCount" + i, _root, "", 12, TextAnchor.MiddleCenter, new Color(0.6f, 0.9f, 1f));
                c.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -52 - i * 34), new Vector2(34, 14));
                c.AddOutline();
                _tabCounts.Add(c);
            }

            MatchManager.HintPinged += OnHint;
            MatchManager.AlarmPinged += OnAlarm;
            _root.gameObject.SetActive(false);
        }

        private static void Line(RectTransform parent, string name, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 size, Color c)
        {
            var img = UiKit.Image(name, parent, c);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            // Horizontal lines stretch in x, vertical lines in y.
            rt.sizeDelta = amin.y == amax.y ? new Vector2(0, size.y) : new Vector2(size.x, 0);
        }

        private void OnDestroy()
        {
            MatchManager.HintPinged -= OnHint;
            MatchManager.AlarmPinged -= OnAlarm;
        }

        private void OnHint(Vector3 world) => AddPin(world, false);

        private void OnAlarm(Vector3 world) => AddPin(world, true);

        private void AddPin(Vector3 world, bool anyFloor)
        {
            var me = NetPlayer.Local;
            if (me == null || !me.IsSeeker) return;
            var pin = UiKit.Image("Pin", _clip, new Color(1f, 0.2f, 0.15f, 1f));
            pin.sprite = RingSprite();
            pin.raycastTarget = false;
            pin.rectTransform.sizeDelta = new Vector2(30, 30);
            _pins.Add((pin.rectTransform, world, Time.time + Tuning.HintPinTime, Time.time, anyFloor));
        }

        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (!visible) return;
            string scene = SceneManager.GetActiveScene().name;
            if (_scannedScene != scene)
            {
                _scannedScene = scene;
                _ready = false;
                _liftsBuilt = false;
                StopAllCoroutines();
                StartCoroutine(Scan());
            }
            HandleZoomKey();
            if (_ready) Refresh();
        }

        private void HandleZoomKey()
        {
            var kb = Keyboard.current;
            var me = NetPlayer.Local;
            if (kb == null || me == null || me.InputReader == null || !me.InputReader.InputEnabled) return;
            if (kb.mKey.wasPressedThisFrame)
            {
                _zoomIndex = (_zoomIndex + 1) % Zooms.Length;
                _viewMetres = Zooms[_zoomIndex];
            }
        }

        // ================================================================ scanning

        private enum Kind : byte { None, Floor, Car, Wall, Down }

        private IEnumerator Scan()
        {
            foreach (var t in _textures) Destroy(t);
            _textures.Clear();
            _floors.Clear();

            // Bounds of the level (scene objects only, players live in DontDestroyOnLoad).
            bool has = false;
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.bounds.size.magnitude > 2000f) continue;
                if (!has) { _bounds = r.bounds; has = true; }
                else _bounds.Encapsulate(r.bounds);
            }
            if (!has) yield break;

            // Floor heights from the spawn points (cluster by 1.5 m).
            var ys = new List<float>();
            foreach (var t in SpawnPoints.Hider()) ys.Add(t.position.y);
            var seeker = SpawnPoints.Seeker();
            if (seeker != null) ys.Add(seeker.position.y);
            ys.Sort();
            foreach (float y in ys)
                if (_floors.Count == 0 || y - _floors[_floors.Count - 1] > 1.5f) _floors.Add(y);
            if (_floors.Count == 0) _floors.Add(0f);

            int w = Mathf.Clamp(Mathf.CeilToInt(_bounds.size.x / Cell), 8, 1400);
            int h = Mathf.Clamp(Mathf.CeilToInt(_bounds.size.z / Cell), 8, 1400);
            int budget = 0;
            foreach (float floorY in _floors)
            {
                var kinds = new Kind[w * h];
                var heights = new float[w * h];
                for (int z = 0; z < h; z++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        Vector3 p = new Vector3(_bounds.min.x + (x + 0.5f) * Cell, floorY + 2.6f, _bounds.min.z + (z + 0.5f) * Cell);
                        Kind k = Kind.None;
                        float dy = 0f;
                        if (Physics.Raycast(p, Vector3.down, out var hit, 4.5f, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore))
                        {
                            dy = hit.point.y - floorY;
                            if (dy > 1.2f) k = Kind.Wall;
                            else if (dy > 0.35f) k = Kind.Car;
                            else if (dy >= -0.35f) k = Kind.Floor;
                            else k = Kind.Down;
                        }
                        kinds[z * w + x] = k;
                        heights[z * w + x] = dy;
                    }
                    budget += w;
                    if (budget > 7000)
                    {
                        budget = 0;
                        yield return null;
                    }
                }
                _textures.Add(Shade(kinds, heights, w, h));
                yield return null;
            }
            _ready = true;
        }

        /// <summary>Turns the raw classification into a readable picture: outlined walls, soft cars, hatched ramps, faint grid.</summary>
        private Texture2D Shade(Kind[] kinds, float[] heights, int w, int h)
        {
            var px = new Color32[w * h];
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                int i = z * w + x;
                Kind k = kinds[i];
                Color32 c = new Color32(0, 0, 0, 0);
                switch (k)
                {
                    case Kind.Floor:
                    {
                        // 10 m grid, very faint.
                        float wx = _bounds.min.x + x * Cell, wz = _bounds.min.z + z * Cell;
                        bool grid = Mathf.Repeat(wx, 10f) < Cell || Mathf.Repeat(wz, 10f) < Cell;
                        c = grid ? new Color32(48, 52, 58, 240) : new Color32(30, 33, 38, 238);
                        break;
                    }
                    case Kind.Car:
                    {
                        bool edge = IsEdge(kinds, x, z, w, h, Kind.Car);
                        c = edge ? new Color32(118, 124, 132, 255) : new Color32(78, 83, 90, 255);
                        break;
                    }
                    case Kind.Wall:
                    {
                        bool edge = IsEdge(kinds, x, z, w, h, Kind.Wall);
                        c = edge ? new Color32(232, 232, 224, 255) : new Color32(150, 152, 148, 255);
                        break;
                    }
                    case Kind.Down:
                    {
                        bool hatch = ((x + z) & 3) < 2;
                        c = hatch ? new Color32(40, 78, 112, 245) : new Color32(26, 52, 78, 245);
                        break;
                    }
                }
                px[i] = c;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static bool IsEdge(Kind[] kinds, int x, int z, int w, int h, Kind same)
        {
            return (x > 0 && kinds[z * w + x - 1] != same) || (x < w - 1 && kinds[z * w + x + 1] != same) ||
                   (z > 0 && kinds[(z - 1) * w + x] != same) || (z < h - 1 && kinds[(z + 1) * w + x] != same);
        }

        private void BuildPoi()
        {
            // Lifts (from the elevator system) get a small "L" tile; created once the system is known.
            var es = ElevatorSystem.Instance;
            if (_liftsBuilt || es == null || es.Cars.Count == 0) return;
            _liftsBuilt = true;
            foreach (var car in es.Cars)
            {
                var icon = UiKit.Image("Lift", _clip, new Color(1f, 0.85f, 0.4f, 0.95f));
                icon.sprite = LiftIconSprite();
                icon.raycastTarget = false;
                icon.rectTransform.sizeDelta = new Vector2(14, 14);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _poi.Add(icon.rectTransform);
                _poiWorld.Add(new Vector2(car.X, 0f));
            }
        }

        // ================================================================ display

        private void Refresh()
        {
            var me = NetPlayer.Local;
            if (me == null) return;
            // Spectating: the map (and the floor number) follows the watched player.
            var watched = GameHud.Spectated;
            var viewPlayer = watched != null ? watched : me;
            Transform view = viewPlayer.transform;
            Vector3 pos = view.position;
            float yaw = viewPlayer == me ? me.Motor.LookYaw : view.eulerAngles.y;

            int floor = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < _floors.Count; i++)
            {
                float d = Mathf.Abs(pos.y - _floors[i]);
                if (d < bestD) { bestD = d; floor = i; }
            }
            _map.texture = _textures[floor];
            string lvl = floor == _floors.Count - 1 ? "0" : (-(_floors.Count - 1 - floor)).ToString();
            _floorLabel.text = _floors.Count > 1 ? $"POZIOM {lvl}" : "";

            // Smooth zoom.
            _viewShown = Mathf.Lerp(_viewShown, _viewMetres, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));

            float u = (pos.x - _bounds.min.x) / _bounds.size.x;
            float v = (pos.z - _bounds.min.z) / _bounds.size.z;
            float uw = _viewShown / _bounds.size.x, vh = _viewShown / _bounds.size.z;
            _map.uvRect = new Rect(u - uw * 0.5f, v - vh * 0.5f, uw, vh);
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            _cone.localRotation = Quaternion.Euler(0f, 0f, -yaw);

            BuildPoi();
            float size = _clip.rect.width;
            float pxPerM = size / _viewShown;
            UpdateScale(pxPerM);
            UpdatePoi(pos, pxPerM, floor);
            UpdateItemMarkers(me, pos, pxPerM, size);
            UpdateTabs(me, floor);
            UpdateTeamDots(me, pos, pxPerM, size);
            UpdatePins(pos, pxPerM, size, floor);
            UpdateSeekerDrops(me, pos, pxPerM, size);
        }

        private void UpdateScale(float pxPerM)
        {
            float metres = _viewMetres >= 90f ? 20f : _viewMetres >= 50f ? 10f : 5f;
            _scaleBar.sizeDelta = new Vector2(metres * pxPerM, 3f);
            _scaleLabel.text = $"{metres:0} m";
        }

        private void UpdatePoi(Vector3 pos, float pxPerM, int floor)
        {
            float half = _clip.rect.width * 0.5f - 6f;
            for (int i = 0; i < _poi.Count; i++)
            {
                Vector2 off = (_poiWorld[i] - new Vector2(pos.x, pos.z)) * pxPerM;
                bool inside = Mathf.Abs(off.x) < half && Mathf.Abs(off.y) < half;
                _poi[i].gameObject.SetActive(inside);
                if (inside) _poi[i].anchoredPosition = off;
            }
        }

        private void UpdateTabs(NetPlayer me, int floor)
        {
            int count = _floors.Count;
            var counts = new int[3];
            if (me.IsHider)
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p == me || !p.IsHider || p.Life.Value == LifeState.Dead) continue;
                    int f = NearestFloor(p.transform.position.y);
                    if (f >= 0 && f < 3) counts[Mathf.Clamp(f, 0, count - 1)]++;
                }
            for (int i = 0; i < 3; i++)
            {
                bool exists = i < count;
                _tabs[i].gameObject.SetActive(exists);
                _tabCounts[i].gameObject.SetActive(exists);
                if (!exists) continue;
                // Tab i = level -i, which is floor index (count - 1 - i).
                int fi = count - 1 - i;
                bool current = fi == floor;
                _tabs[i].color = current ? new Color(1f, 0.95f, 0.5f) : UiKit.TextDim;
                _tabs[i].fontSize = current ? 22 : 18;
                int n = counts[fi];
                _tabCounts[i].text = n > 0 ? new string('*', Mathf.Min(n, 4)) : "";
            }
        }

        private int NearestFloor(float y)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < _floors.Count; i++)
            {
                float d = Mathf.Abs(y - _floors[i]);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        private void UpdatePins(Vector3 pos, float pxPerM, float size, int floor)
        {
            int visible = 0, hidden = 0;
            for (int i = _pins.Count - 1; i >= 0; i--)
            {
                var (rt, world, until, born, anyFloor) = _pins[i];
                if (rt == null || Time.time > until)
                {
                    if (rt != null) Destroy(rt.gameObject);
                    _pins.RemoveAt(i);
                    continue;
                }
                // A hint ping marks a hider's chest: only shown while the viewer stands on that level.
                bool show = anyFloor || NearestFloor(world.y - 1f) == floor;
                if (rt.gameObject.activeSelf != show) rt.gameObject.SetActive(show);
                if (!show) { hidden++; continue; }
                visible++;
                Vector2 off = new Vector2(world.x - pos.x, world.z - pos.z) * pxPerM;
                float edge = size * 0.5f - 10f;
                bool clamped = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = off;
                float left = until - Time.time;
                // Expanding ripple, repeating.
                float ripple = Mathf.Repeat((Time.time - born) * 1.2f, 1f);
                rt.sizeDelta = Vector2.one * Mathf.Lerp(14f, 46f, ripple);
                var img = rt.GetComponent<Image>();
                img.color = new Color(1f, 0.25f, 0.18f, Mathf.Clamp01(left) * (clamped ? 0.9f : 1f - ripple * 0.8f));
            }
            VisiblePins = visible;
            HiddenPins = hidden;
        }

        // ================================================================ team dots

        private readonly List<Image> _dots = new();
        private readonly List<Image> _dotArrows = new();

        private Image Dot(int index)
        {
            while (_dots.Count <= index)
            {
                var img = UiKit.Image("TeamDot", _clip, Color.white);
                img.sprite = DotSprite();
                img.raycastTarget = false;
                _dots.Add(img);
                var ar = UiKit.Image("DotArrow", img.transform, Color.white);
                ar.sprite = ArrowSprite();
                ar.raycastTarget = false;
                _dotArrows.Add(ar);
            }
            return _dots[index];
        }

        /// <summary>Hiders only: teammates as dots (arrows when on another level), downed ones on the viewer's level as pulsing red markers.</summary>
        private void UpdateTeamDots(NetPlayer me, Vector3 pos, float pxPerM, float size)
        {
            int used = 0;
            float edge = size * 0.5f - 9f;
            if (me.IsHider)
            {
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p == me || !p.IsHider || p.Life.Value == LifeState.Dead) continue;
                    Vector3 w = p.transform.position;
                    float dy = w.y - pos.y; // pos = the watched player while spectating (the map shows his level)
                    bool otherFloor = Mathf.Abs(dy) > 1.7f;
                    // The downed marker (red, pulsing, kept at the edge) only for a teammate on the viewer's level;
                    // one on another level is an ordinary teammate dot (dim, with the up / down arrow, only in range).
                    bool downed = p.Life.Value == LifeState.Downed && !otherFloor;
                    Vector2 off = new Vector2(w.x - pos.x, w.z - pos.z) * pxPerM;
                    bool outside = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                    if (outside && !downed) continue;
                    off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));

                    var dot = Dot(used);
                    var arrow = _dotArrows[used];
                    used++;
                    dot.gameObject.SetActive(true);
                    var rt = dot.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = off;
                    if (downed)
                    {
                        float pulse = 1f + 0.35f * Mathf.Sin(Time.time * 7f);
                        rt.sizeDelta = new Vector2(18, 18) * pulse;
                        dot.color = new Color(1f, 0.15f, 0.12f, otherFloor ? 0.65f : 1f);
                    }
                    else
                    {
                        rt.sizeDelta = new Vector2(12, 12);
                        dot.color = otherFloor ? new Color(0.6f, 0.85f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.97f);
                    }
                    // Other level: a little arrow above the dot (up = higher level, down = lower).
                    arrow.gameObject.SetActive(otherFloor);
                    if (otherFloor)
                    {
                        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        arrow.rectTransform.anchoredPosition = new Vector2(0f, 0f);
                        arrow.rectTransform.sizeDelta = new Vector2(9, 9);
                        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, dy > 0f ? 0f : 180f);
                        arrow.color = downed ? new Color(0.3f, 0f, 0f, 0.95f) : new Color(0.1f, 0.2f, 0.3f, 0.95f);
                    }
                }
            }
            used = UpdateSeekerAllies(me, pos, pxPerM, edge, used); // several seekers: the others (Minimap.Allies)
            // Seeker: hiders brought back with adrenaline blink blue every few seconds (clamped to the edge).
            if (me.IsSeeker)
            {
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || !p.IsHider || !p.Resurrected.Value || p.Life.Value == LifeState.Dead) continue;
                    float phase = Mathf.Repeat(Time.time + (p.NetworkObjectId % 7) * 0.37f, Tuning.ResurrectedPingEvery);
                    if (phase > 1.1f) continue;
                    Vector3 w = p.transform.position;
                    Vector2 off = new Vector2(w.x - pos.x, w.z - pos.z) * pxPerM;
                    off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                    var dot = Dot(used);
                    _dotArrows[used].gameObject.SetActive(false);
                    used++;
                    dot.gameObject.SetActive(true);
                    var rt = dot.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = off;
                    float k = Mathf.Sin(phase / 1.1f * Mathf.PI);
                    rt.sizeDelta = new Vector2(22, 22) * (0.8f + 0.5f * k);
                    dot.color = new Color(0.25f, 0.6f, 1f, k);
                }
            }
            for (int i = used; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
            _cone.SetAsLastSibling();
            _arrow.SetAsLastSibling();
        }

        // ================================================================ sprites

        private static Sprite _arrowSprite, _dotSprite, _coneSprite, _ringSprite, _fadeSprite, _stairSprite, _liftSprite;

        private static Sprite Make(int n, System.Func<int, int, Color32> f)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) px[y * n + x] = f(x, y);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private static Sprite ArrowSprite() => _arrowSprite != null ? _arrowSprite : _arrowSprite = Make(48, (x, y) =>
        {
            // Arrow head pointing up with a notch at the back.
            float fx = x / 47f, fy = y / 47f;
            float half = (1f - fy) * 0.5f * 1.0f;
            bool inside = fy > 0.06f && Mathf.Abs(fx - 0.5f) <= half * (fy < 0.32f ? Mathf.Lerp(0.3f, 1f, fy / 0.32f) : 1f);
            return inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        });

        private static Sprite DotSprite() => _dotSprite != null ? _dotSprite : _dotSprite = Make(48, (x, y) =>
        {
            float d = new Vector2(x - 23.5f, y - 23.5f).magnitude / 23.5f;
            // Dark rim for contrast on any background, bright body.
            float body = Mathf.Clamp01((0.78f - d) * 8f);
            float rim = Mathf.Clamp01((1f - d) * 8f);
            byte c = (byte)(255 * body + 25 * (1f - body));
            return new Color32(c, c, c, (byte)(rim * 255));
        });

        private static Sprite ConeSprite() => _coneSprite != null ? _coneSprite : _coneSprite = Make(64, (x, y) =>
        {
            // Wedge opening upwards from the bottom centre, +-32 degrees, fading with distance.
            Vector2 p = new Vector2(x - 31.5f, y);
            float dist = p.magnitude / 63f;
            float ang = Mathf.Abs(Mathf.Atan2(p.x, p.y)) * Mathf.Rad2Deg;
            float a = ang < 32f && dist < 1f ? (1f - dist) * Mathf.Clamp01((32f - ang) / 8f) : 0f;
            return new Color32(255, 255, 255, (byte)(a * 255));
        });

        private static Sprite RingSprite() => _ringSprite != null ? _ringSprite : _ringSprite = Make(64, (x, y) =>
        {
            float d = new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f;
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) * 10f);
            float core = Mathf.Clamp01((0.22f - d) * 8f);
            return new Color32(255, 255, 255, (byte)(Mathf.Max(a, core) * 255));
        });

        private static Sprite EdgeFadeSprite() => _fadeSprite != null ? _fadeSprite : _fadeSprite = Make(64, (x, y) =>
        {
            Vector2 d = new Vector2(x - 31.5f, y - 31.5f) / 31.5f;
            float r = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
            float round = d.magnitude;
            float a = Mathf.Clamp01((Mathf.Max(r, round * 0.85f) - 0.62f) / 0.38f);
            return new Color32(255, 255, 255, (byte)(a * a * 255));
        });

        private static Sprite StairIconSprite() => _stairSprite != null ? _stairSprite : _stairSprite = Make(32, (x, y) =>
        {
            // Rounded tile with three ascending steps.
            bool tile = x > 1 && x < 30 && y > 1 && y < 30;
            bool step = (x > 5 && x < 12 && y > 5 && y < 12) || (x >= 12 && x < 19 && y > 5 && y < 18) || (x >= 19 && x < 27 && y > 5 && y < 25);
            if (!tile) return new Color32(0, 0, 0, 0);
            return step ? new Color32(255, 255, 255, 255) : new Color32(20, 40, 62, 235);
        });

        private static Sprite LiftIconSprite() => _liftSprite != null ? _liftSprite : _liftSprite = Make(32, (x, y) =>
        {
            // Tile with a lift door split and up / down arrows.
            bool tile = x > 1 && x < 30 && y > 1 && y < 30;
            if (!tile) return new Color32(0, 0, 0, 0);
            bool split = x == 15 || x == 16;
            bool up = y > 17 && y < 26 && Mathf.Abs(x - 15.5f) < (26 - y) * 0.9f;
            bool down = y > 5 && y < 14 && Mathf.Abs(x - 15.5f) < (y - 5) * 0.9f;
            return (up || down) && !split ? new Color32(60, 40, 5, 255) : new Color32(255, 255, 255, 235);
        });
    }
}
