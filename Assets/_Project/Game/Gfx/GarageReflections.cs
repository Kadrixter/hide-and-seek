using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Gfx
{
    /// <summary>
    /// "Odbicia (eksperymentalne)" (<see cref="GfxSettings.Reflections"/>, off by default): box-projected reflection
    /// probes over the garage, 6 x 4 per level (30 x 30 m boxes, floor to ceiling), so puddles, the flooded quarter,
    /// wet floor and car paint reflect the lamps and the level around them. Every probe sees only its own level's
    /// layer (MapLevel0-2, LevelLighting), so nothing of another level and no light from it gets into a reflection.
    /// The probes are rendered once (a few per frame, face by face) when the first round starts and kept for the
    /// session; between rounds they are switched off. The map renderers are built with reflection probes off: while
    /// the option is on they blend the probes, switching it off puts them back.
    /// </summary>
    public class GarageReflections : MonoBehaviour
    {
        private const int PerX = 6, PerZ = 4, Levels = 3, Resolution = 64, AtOnce = 2;
        private const float HX = 90f, HZ = 60f, LevelHeight = 3.4f, Clear = 3.0f;

        private static GarageReflections _instance;
        private readonly List<ReflectionProbe> _probes = new();
        private readonly List<Renderer> _switched = new();
        private Transform _probeRoot;
        private bool _rendered, _rendering;
        private Scene _appliedScene, _renderedScene;

        /// <summary>Probes ready (rendered) - for tests.</summary>
        public static bool Ready => _instance != null && _instance._rendered;
        public static float LastRenderMs { get; private set; }

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[GarageReflections]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GarageReflections>();
        }

        private void Update()
        {
            bool inGarage = SceneManager.GetActiveScene().name == Net.NetSession.GameScene;
            bool want = GfxSettings.EffectiveReflections && inGarage;
            if (_probeRoot != null && _probeRoot.gameObject.activeSelf != want)
            {
                _probeRoot.gameObject.SetActive(want);
                if (want && _renderedScene != SceneManager.GetActiveScene()) _rendered = false;   // a new round: render once more, spread over frames
            }
            var scene = SceneManager.GetActiveScene();
            if (want)
            {
                if (_probeRoot == null) Build();
                if (!_rendered && !_rendering) StartCoroutine(RenderAll());
                if (_appliedScene != scene) ApplyRenderers(scene, true);
            }
            else if (_switched.Count > 0) ApplyRenderers(scene, false);
        }

        private void Build()
        {
            _probeRoot = new GameObject("Probes").transform;
            _probeRoot.SetParent(transform, false);
            float sx = 2f * HX / PerX, sz = 2f * HZ / PerZ;
            for (int lv = 0; lv < Levels; lv++)
            {
                float y0 = -LevelHeight * lv;
                for (int ix = 0; ix < PerX; ix++)
                for (int iz = 0; iz < PerZ; iz++)
                {
                    var go = new GameObject($"Probe_L{lv}_{ix}_{iz}");
                    go.transform.SetParent(_probeRoot, false);
                    go.transform.position = new Vector3(-HX + sx * (ix + 0.5f), y0 + 1.5f, -HZ + sz * (iz + 0.5f));
                    var p = go.AddComponent<ReflectionProbe>();
                    p.mode = ReflectionProbeMode.Realtime;
                    p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                    p.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                    p.resolution = Resolution;
                    p.hdr = true;
                    p.boxProjection = true;
                    p.size = new Vector3(sx + 0.5f, Clear + 0.4f, sz + 0.5f);
                    p.center = new Vector3(0f, y0 + Clear * 0.5f - (y0 + 1.5f), 0f);
                    p.blendDistance = 2f;
                    p.cullingMask = 1 << (HideAndSeek.Map.LevelLighting.FirstLayer + lv);
                    p.clearFlags = ReflectionProbeClearFlags.SolidColor;
                    p.backgroundColor = new Color(0.004f, 0.004f, 0.005f);
                    p.nearClipPlane = 0.1f;
                    p.farClipPlane = 70f;
                    p.shadowDistance = 0f;
                    p.intensity = 1f;
                    _probes.Add(p);
                }
            }
        }

        private IEnumerator RenderAll()
        {
            _rendering = true;
            yield return new WaitForSeconds(1.5f);   // after the round-start work (rooms, props)
            float t0 = Time.realtimeSinceStartup;
            var active = new List<(ReflectionProbe p, int id)>();
            int next = 0;
            while (next < _probes.Count || active.Count > 0)
            {
                if (_probeRoot == null || !_probeRoot.gameObject.activeInHierarchy) { _rendering = false; yield break; }   // switched off / left: later again
                for (int i = active.Count - 1; i >= 0; i--)
                    if (active[i].p == null || active[i].p.IsFinishedRendering(active[i].id)) active.RemoveAt(i);
                while (active.Count < AtOnce && next < _probes.Count)
                {
                    var p = _probes[next++];
                    active.Add((p, p.RenderProbe()));
                }
                yield return null;
            }
            LastRenderMs = (Time.realtimeSinceStartup - t0) * 1000f;
            _rendered = true;
            _renderedScene = SceneManager.GetActiveScene();
            _rendering = false;
            Debug.Log($"[Gfx] reflections: {_probes.Count} probes rendered in {LastRenderMs:0} ms (spread over frames)");
        }

        /// <summary>Map renderers on the level layers blend the probes (on) or go back to no probes (off).</summary>
        private void ApplyRenderers(Scene scene, bool on)
        {
            if (!on)
            {
                foreach (var r in _switched)
                    if (r != null) r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                _switched.Clear();
                _appliedScene = default;
                return;
            }
            _switched.Clear();
            int first = HideAndSeek.Map.LevelLighting.FirstLayer;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                int layer = r.gameObject.layer;
                if (layer < first || layer >= first + Levels || r.reflectionProbeUsage != ReflectionProbeUsage.Off) continue;
                if (r.gameObject.scene != scene || r.GetComponentInParent<SecurityRooms>() != null) continue;   // the dark rooms stay without
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                _switched.Add(r);
            }
            _appliedScene = scene;
            Debug.Log($"[Gfx] reflections on for {_switched.Count} map renderers");
        }
    }
}
