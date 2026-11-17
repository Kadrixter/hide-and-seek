using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Bottom-left minimap. The level is scanned once per loaded map with downward raycasts (one texture per floor);
    /// the view is north-up, centred on the local player, and shows hint pins for the seeker.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        private const float Cell = 0.6f;
        private const float ViewMetres = 56f;

        private RectTransform _root;
        private RawImage _map;
        private RectTransform _arrow;
        private Text _floorLabel;
        private readonly List<(RectTransform rt, Vector3 world, float until)> _pins = new();

        private string _scannedScene;
        private Bounds _bounds;
        private readonly List<float> _floors = new();
        private readonly List<Texture2D> _textures = new();
        private bool _ready;

        public void Build(Transform canvas)
        {
            var frame = UiKit.Image("Minimap", canvas, new Color(0f, 0f, 0f, 0.6f));
            _root = frame.rectTransform;
            _root.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(20, 20), new Vector2(300, 300));
            frame.gameObject.AddComponent<RectMask2D>();
            var mapGo = UiKit.Rect("Map", _root);
            mapGo.Stretch(4);
            _map = mapGo.gameObject.AddComponent<RawImage>();
            _map.color = new Color(1f, 1f, 1f, 0.92f);

            var arrow = UiKit.Image("Me", _root, new Color(1f, 0.95f, 0.5f, 1f));
            arrow.sprite = ArrowSprite();
            _arrow = arrow.rectTransform;
            _arrow.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));

            _floorLabel = UiKit.Label("Floor", _root, "", 20, TextAnchor.UpperLeft, UiKit.TextDim);
            _floorLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -6), new Vector2(200, 26));
            _floorLabel.AddOutline();

            MatchManager.HintPinged += OnHint;
            _root.gameObject.SetActive(false);
        }

        private void OnDestroy() => MatchManager.HintPinged -= OnHint;

        private void OnHint(Vector3 world)
        {
            var me = NetPlayer.Local;
            if (me == null || !me.IsSeeker) return;
            var pin = UiKit.Image("Pin", _root, new Color(1f, 0.2f, 0.15f, 1f));
            pin.sprite = DotSprite();
            pin.rectTransform.sizeDelta = new Vector2(18, 18);
            _pins.Add((pin.rectTransform, world, Time.time + Tuning.HintPinTime));
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
                StopAllCoroutines();
                StartCoroutine(Scan());
            }
            if (_ready) Refresh();
        }

        // ================================================================ scanning

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

            int w = Mathf.CeilToInt(_bounds.size.x / Cell), h = Mathf.CeilToInt(_bounds.size.z / Cell);
            w = Mathf.Clamp(w, 8, 1024);
            h = Mathf.Clamp(h, 8, 1024);
            int budget = 0;
            foreach (float floorY in _floors)
            {
                var px = new Color32[w * h];
                for (int z = 0; z < h; z++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        Vector3 p = new Vector3(_bounds.min.x + (x + 0.5f) * Cell, floorY + 2.6f, _bounds.min.z + (z + 0.5f) * Cell);
                        Color32 c = new Color32(0, 0, 0, 0);
                        if (Physics.Raycast(p, Vector3.down, out var hit, 4.5f, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore))
                        {
                            float dy = hit.point.y - floorY;
                            if (dy > 1.2f) c = new Color32(150, 150, 145, 255);           // wall / pillar
                            else if (dy > 0.35f) c = new Color32(92, 94, 96, 255);        // car / obstacle
                            else if (dy >= -0.35f) c = new Color32(34, 36, 40, 235);      // floor
                            else c = new Color32(22, 40, 58, 235);                         // ramp / stairs down
                        }
                        px[z * w + x] = c;
                    }
                    budget += w;
                    if (budget > 6000)
                    {
                        budget = 0;
                        yield return null;
                    }
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(px);
                tex.Apply();
                _textures.Add(tex);
            }
            _ready = true;
        }

        // ================================================================ display

        private void Refresh()
        {
            var me = NetPlayer.Local;
            if (me == null) return;
            Transform view = me.transform;
            Vector3 pos = view.position;
            float yaw = me.Motor.LookYaw;

            int floor = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < _floors.Count; i++)
            {
                float d = Mathf.Abs(pos.y - _floors[i]);
                if (d < bestD) { bestD = d; floor = i; }
            }
            _map.texture = _textures[floor];
            _floorLabel.text = _floors.Count > 1 ? $"Poziom {(floor == _floors.Count - 1 ? 0 : -(_floors.Count - 1 - floor))}" : "";

            float u = (pos.x - _bounds.min.x) / _bounds.size.x;
            float v = (pos.z - _bounds.min.z) / _bounds.size.z;
            float uw = ViewMetres / _bounds.size.x, vh = ViewMetres / _bounds.size.z;
            _map.uvRect = new Rect(u - uw * 0.5f, v - vh * 0.5f, uw, vh);
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);

            float size = _root.rect.width - 8f;
            for (int i = _pins.Count - 1; i >= 0; i--)
            {
                var (rt, world, until) = _pins[i];
                if (rt == null || Time.time > until)
                {
                    if (rt != null) Destroy(rt.gameObject);
                    _pins.RemoveAt(i);
                    continue;
                }
                Vector2 off = new Vector2(world.x - pos.x, world.z - pos.z) / ViewMetres * size;
                float edge = size * 0.5f - 10f;
                off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = off;
                float left = until - Time.time;
                rt.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(Time.time * 8f));
                var img = rt.GetComponent<Image>();
                img.color = new Color(1f, 0.2f, 0.15f, Mathf.Clamp01(left));
            }
        }

        // ================================================================ sprites

        private static Sprite _arrowSprite, _dotSprite;

        private static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Triangle pointing up.
                float fy = y / (float)(n - 1);
                float half = (1f - fy) * 0.5f;
                bool inside = Mathf.Abs(x / (float)(n - 1) - 0.5f) <= half && fy > 0.05f;
                px[y * n + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private static Sprite DotSprite()
        {
            if (_dotSprite != null) return _dotSprite;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f;
                byte a = (byte)(Mathf.Clamp01((1f - d) * 4f) * 255);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
