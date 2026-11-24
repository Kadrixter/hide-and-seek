using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Map;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The punishment of a wrong fuse box (MatchManager.Fuses): every light of that level comes on - the dead ceiling
    /// tubes strike (flicker, then steady), the broken flickering lamps burn steady, a grid of mains lights fills the
    /// aisles, and for a camera on that level the dark garage ambient and fog give way to a lit car park (hiders can be
    /// seen from far). Lasts while <see cref="FuseBoxSet.LevelLit"/> says so (until the box is empty again), then all of it
    /// goes back. Local, driven every frame by the synced state on every peer.
    /// </summary>
    public sealed class FloorLights : MonoBehaviour
    {
        private static FloorLights _inst;
        private static readonly float[] AisleZ = { -44.75f, -28.25f, -11.75f, 11.75f, 28.25f, 44.75f };

        private sealed class Level
        {
            public bool Want, Built;
            public float Since = -99f, Shown;
            public readonly List<(Renderer r, Material[] orig, Material[] lit)> Tubes = new();
            public readonly List<(Light l, Behaviour flicker, float intensity)> Lamps = new();
            public readonly List<Light> Fill = new();
        }

        private readonly Level[] _lv = { new Level(), new Level(), new Level() };
        private Scene _scene;
        private Material _litTube;
        private bool _ambientSaved;
        private Color _ambient, _fogColor;
        private float _fogDensity, _amb;

        /// <summary>Level lights now on (tests).</summary>
        public static bool IsLit(int level) => _inst != null && level >= 0 && level < 3 && _inst._lv[level].Shown > 0.5f;
        /// <summary>0..1 how much the local camera's level is lit (tests).</summary>
        public static float CameraLit => _inst != null ? _inst._amb : 0f;

        public static void Sync(FuseBoxSet set, bool show)
        {
            if (_inst == null)
            {
                if (!show || !set.Placed) return;
                var go = new GameObject("[FloorLights]");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<FloorLights>();
            }
            for (int i = 0; i < 3; i++)
            {
                bool want = show && set.LevelLit(i);
                var l = _inst._lv[i];
                if (want != l.Want) { l.Want = want; l.Since = Time.time; }
            }
        }

        private void Update()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene != _scene)
            {
                // A new map: everything built for the old one is gone with it.
                _scene = scene;
                foreach (var l in _lv) { l.Built = false; l.Tubes.Clear(); l.Lamps.Clear(); l.Fill.Clear(); l.Shown = 0f; }
                _ambientSaved = false;
                _amb = 0f;
            }
            bool garage = MapInfo.HasGarage;
            for (int i = 0; i < 3; i++)
            {
                var l = _lv[i];
                float k = garage ? Strike(l) : 0f;
                if (k > 0f && !l.Built) Build(i, l);
                if (l.Built && Mathf.Abs(k - l.Shown) > 0.001f) Show(l, k);
                l.Shown = k;
            }
            Ambient(garage);
        }

        /// <summary>Tubes striking: a few flickers in the first second, then steady; off at once.</summary>
        private static float Strike(Level l)
        {
            if (!l.Want) return 0f;
            float t = Time.time - l.Since;
            if (t < 0.05f) return 0f;
            if (t < 0.12f) return 1f;
            if (t < 0.22f) return 0f;
            if (t < 0.3f) return 0.7f;
            if (t < 0.48f) return 0.1f;
            return Mathf.Clamp01(0.6f + (t - 0.48f) * 2f);
        }

        private void Build(int level, Level l)
        {
            l.Built = true;
            if (_litTube == null)
            {
                _litTube = new Material(Shader.Find("Standard")) { name = "FixtureLit", color = new Color(0.95f, 0.95f, 0.9f) };
                _litTube.EnableKeyword("_EMISSION");
                _litTube.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                _litTube.SetColor("_EmissionColor", new Color(1f, 0.96f, 0.86f) * 2.6f);
            }
            string tubes = "CeilingServices_L" + (level + 1) + "_", lamps = "FlickeringLamp_L" + (level + 1) + "_";
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (!r.name.StartsWith(tubes)) continue;
                var orig = r.sharedMaterials;
                var lit = (Material[])orig.Clone();
                bool any = false;
                for (int m = 0; m < lit.Length; m++)
                    if (lit[m] != null && lit[m].name.StartsWith("Fixture_Dead")) { lit[m] = _litTube; any = true; }
                if (any) l.Tubes.Add((r, orig, lit));
            }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.name.StartsWith(lamps)) continue;
                foreach (var lt in t.GetComponentsInChildren<Light>())
                {
                    var fl = lt.GetComponent<FlickeringLight>();
                    l.Lamps.Add((lt, fl, lt.intensity));
                }
            }
            float y = MapInfo.FloorY(level) + 2.75f;
            foreach (float z in AisleZ)
                foreach (float x in new[] { -66f, -40f, -14f, 14f, 40f, 66f })
                {
                    var go = new GameObject("FloorFill");
                    go.transform.SetParent(transform, false);
                    go.transform.position = new Vector3(x, y, z);
                    var lt = go.AddComponent<Light>();
                    lt.type = LightType.Point;
                    lt.range = 17f;
                    lt.intensity = 0f;
                    lt.color = new Color(1f, 0.95f, 0.85f);
                    lt.shadows = LightShadows.None;
                    lt.renderMode = LightRenderMode.ForceVertex;
                    lt.cullingMask = LevelLighting.MaskForLevel(level);
                    lt.enabled = false;
                    l.Fill.Add(lt);
                }
            Debug.Log($"[Fuses] floor lights of level {level}: {l.Tubes.Count} tube chunks, {l.Lamps.Count} lamps, {l.Fill.Count} fill lights");
        }

        private static void Show(Level l, float k)
        {
            bool on = k > 0.05f;
            foreach (var (r, orig, lit) in l.Tubes) if (r != null) r.sharedMaterials = on ? lit : orig;
            foreach (var (lt, fl, intensity) in l.Lamps)
            {
                if (lt == null) continue;
                if (fl != null) fl.enabled = !on;
                if (on) { lt.enabled = true; lt.intensity = 1.3f * k; }
                else lt.intensity = intensity;
            }
            foreach (var lt in l.Fill)
            {
                if (lt == null) continue;
                lt.enabled = on;
                lt.intensity = 1.5f * k;
            }
        }

        /// <summary>The local view's level lit: brighter ambient, thinner and lighter fog (eased); else the scene's own.</summary>
        private void Ambient(bool garage)
        {
            var cam = Camera.main != null ? Camera.main : NetPlayer.Local != null ? NetPlayer.Local.Camera : null;
            float want = 0f;
            if (garage && cam != null && MapInfo.InsideGarage(cam.transform.position, -2f))
                want = _lv[LevelLighting.LevelAt(cam.transform.position.y)].Shown;
            if (!_ambientSaved)
            {
                if (want <= 0f) return;
                _ambientSaved = true;
                _ambient = RenderSettings.ambientLight;
                _fogColor = RenderSettings.fogColor;
                _fogDensity = RenderSettings.fogDensity;
            }
            _amb = Mathf.MoveTowards(_amb, want, Time.deltaTime * (want > _amb ? 6f : 3f));
            RenderSettings.ambientLight = Color.Lerp(_ambient, new Color(0.4f, 0.39f, 0.36f), _amb);
            RenderSettings.fogColor = Color.Lerp(_fogColor, new Color(0.1f, 0.1f, 0.095f), _amb);
            RenderSettings.fogDensity = Mathf.Lerp(_fogDensity, 0.008f, _amb);
            if (_amb <= 0f && want <= 0f)
            {
                RenderSettings.ambientLight = _ambient;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogDensity = _fogDensity;
                _ambientSaved = false;
            }
        }
    }
}
