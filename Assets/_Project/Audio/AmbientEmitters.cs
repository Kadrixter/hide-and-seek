using System.Collections.Generic;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Audio
{
    /// <summary>
    /// 3D ambient loops scattered over a map at runtime: ventilation hum (objects named vent/duct/fan, or a sparse grid
    /// under the ceiling when the map has none), water drips in Water <see cref="FootstepSurface"/> zones, fluorescent
    /// buzz near lamps (<see cref="Light"/> components) and muffled city noise near ramps/entrances.
    /// Call <see cref="Populate"/> once after the map is loaded (on every client - audio is local only).
    /// Emitters far from the listener are paused, the rest get the same wall occlusion as <see cref="GameAudio"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbientEmitters : MonoBehaviour
    {
        public const string ContainerName = "[AmbientEmitters]";

        [Tooltip("Max lamp-buzz emitters (closest lamps are not prioritised - spread over the map with min spacing).")]
        public int maxLamps = 24;
        public float lampSpacing = 7f;
        public int maxDrips = 16;
        public int maxVents = 12;
        public float ventGridSpacing = 28f;
        public int maxCity = 4;

        private class Emitter
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public AmbientId Id;
            public float BaseVolume;
            public float Occlusion, OcclusionTarget;
        }

        private readonly List<Emitter> _emitters = new List<Emitter>();
        private int _cursor;
        private Transform _listener;
        private float _nextListenerSearch;

        public int Count => _emitters.Count;

        // ------------------------------------------------------------------ static API

        /// <summary>
        /// (Re)creates ambient emitters for a map. Safe to call again (old emitters under the map root are removed).
        /// Returns the component that owns them (child "[AmbientEmitters]" of <paramref name="mapRoot"/>).
        /// </summary>
        public static AmbientEmitters Populate(Transform mapRoot)
        {
            if (mapRoot == null) return null;
            Clear(mapRoot);
            var go = new GameObject(ContainerName);
            go.transform.SetParent(mapRoot, false);
            var ae = go.AddComponent<AmbientEmitters>();
            ae.Build(mapRoot);
            return ae;
        }

        /// <summary>Removes emitters created by <see cref="Populate"/>.</summary>
        public static void Clear(Transform mapRoot)
        {
            if (mapRoot == null) return;
            for (int i = mapRoot.childCount - 1; i >= 0; i--)
            {
                var c = mapRoot.GetChild(i);
                if (c.name != ContainerName) continue;
                if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
            }
        }

        /// <summary>Adds one emitter by hand (e.g. a generator prop). Returns its AudioSource (null when the clip is missing).</summary>
        public AudioSource AddEmitter(AmbientId id, Vector3 position, float volumeScale = 1f)
        {
            var entry = GameAudio.Library?.Get(id);
            if (entry == null || entry.clip == null) return null;

            var go = new GameObject(id.ToString());
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var s = go.AddComponent<AudioSource>();
            var lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000f;
            lp.enabled = false;

            s.clip = entry.clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.dopplerLevel = 0f;
            s.spread = 90f;
            s.priority = 200;
            s.rolloffMode = AudioRolloffMode.Custom;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            s.minDistance = 1f;
            s.maxDistance = Mathf.Max(2f, entry.range);
            s.reverbZoneMix = 1f;
            s.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);

            var e = new Emitter { Source = s, LowPass = lp, Id = id, BaseVolume = entry.volume * volumeScale };
            s.volume = 0f;
            _emitters.Add(e);
            return s;
        }

        // ------------------------------------------------------------------ build

        private void Build(Transform root)
        {
            var bounds = MapBounds(root);

            // lamps
            var lampPoints = new List<Vector3>();
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional) continue;
                Vector3 p = l.transform.position;
                if (TooClose(lampPoints, p, lampSpacing)) continue;
                lampPoints.Add(p);
                if (lampPoints.Count >= maxLamps) break;
            }
            foreach (var p in lampPoints) AddEmitter(AmbientId.LampBuzz, p, Random.Range(0.6f, 1f));

            // drips in water zones
            int drips = 0;
            foreach (var fs in root.GetComponentsInChildren<FootstepSurface>(true))
            {
                if (fs.Type != SurfaceType.Water || drips >= maxDrips) continue;
                Vector3 size = fs.IsZone ? fs.ZoneSize : ColliderSize(fs);
                int n = Mathf.Clamp(Mathf.RoundToInt(size.x * size.z / 60f), 1, 3);
                for (int i = 0; i < n && drips < maxDrips; i++, drips++)
                {
                    Vector3 local = new Vector3(Random.Range(-0.4f, 0.4f) * size.x, 0f, Random.Range(-0.4f, 0.4f) * size.z);
                    Vector3 p = fs.transform.position + fs.transform.rotation * local;
                    AddEmitter(AmbientId.Drips, p + Vector3.up * 0.2f, Random.Range(0.7f, 1f));
                }
            }

            // vents: named objects first
            var ventPoints = new List<Vector3>();
            var cityPoints = new List<Vector3>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (ventPoints.Count < maxVents && (n.Contains("vent") || n.Contains("duct") || n.Contains("fan") || n.Contains("hvac") || n.Contains("exhaust")))
                {
                    if (!TooClose(ventPoints, t.position, 8f)) ventPoints.Add(t.position);
                }
                else if (cityPoints.Count < maxCity && (n.Contains("ramp") || n.Contains("entrance") || n.Contains("exit") || n.Contains("gate")))
                {
                    if (!TooClose(cityPoints, t.position, 20f)) cityPoints.Add(t.position);
                }
            }
            // fallback: sparse grid just below the ceiling of each level
            if (ventPoints.Count == 0 && bounds.size.sqrMagnitude > 1f)
                ventPoints.AddRange(GridUnderCeilings(bounds, ventGridSpacing, maxVents));

            foreach (var p in ventPoints) AddEmitter(AmbientId.Ventilation, p, Random.Range(0.6f, 1f));
            foreach (var p in cityPoints) AddEmitter(AmbientId.DistantCity, p, 1f);
        }

        private static List<Vector3> GridUnderCeilings(Bounds b, float spacing, int max)
        {
            var pts = new List<Vector3>();
            int nx = Mathf.Max(1, Mathf.FloorToInt(b.size.x / spacing));
            int nz = Mathf.Max(1, Mathf.FloorToInt(b.size.z / spacing));
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                if (pts.Count >= max) return pts;
                var top = new Vector3(b.min.x + (ix + 0.5f) * b.size.x / nx, b.max.y + 1f, b.min.z + (iz + 0.5f) * b.size.z / nz);
                // walk down through the levels: hit a ceiling, step below it
                Vector3 origin = top;
                for (int level = 0; level < 4 && pts.Count < max; level++)
                {
                    if (!Physics.Raycast(origin, Vector3.down, out var ceil, origin.y - b.min.y + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) break;
                    Vector3 below = ceil.point + Vector3.down * 0.05f;
                    if (Physics.Raycast(below, Vector3.down, out var floor, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        float h = below.y - floor.point.y;
                        if (h > 2f && level > 0) pts.Add(below + Vector3.down * 0.4f); // level 0 hit is the roof top
                        origin = floor.point + Vector3.down * 0.05f;
                    }
                    else break;
                }
            }
            return pts;
        }

        private static Vector3 ColliderSize(Component c)
        {
            var col = c.GetComponent<Collider>();
            return col != null ? col.bounds.size : new Vector3(3f, 0f, 3f);
        }

        private static Bounds MapBounds(Transform root)
        {
            bool any = false;
            var b = new Bounds(root.position, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static bool TooClose(List<Vector3> pts, Vector3 p, float d)
        {
            float d2 = d * d;
            foreach (var q in pts) if ((q - p).sqrMagnitude < d2) return true;
            return false;
        }

        private static AnimationCurve _rolloff;
        private static AnimationCurve Rolloff => _rolloff ??= new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.1f, 0.8f), new Keyframe(0.35f, 0.3f), new Keyframe(0.7f, 0.08f), new Keyframe(1f, 0f));

        // ------------------------------------------------------------------ runtime

        private void Update()
        {
            if (_emitters.Count == 0) return;
            float now = Time.unscaledTime;
            if ((_listener == null || !_listener.gameObject.activeInHierarchy) && now >= _nextListenerSearch)
            {
                _nextListenerSearch = now + 0.5f;
                var l = FindAnyObjectByType<AudioListener>();
                _listener = l != null ? l.transform : null;
            }
            if (_listener == null) return;
            Vector3 lp = _listener.position;
            float dt = Time.unscaledDeltaTime;

            // a few occlusion probes per frame (round robin)
            int probes = Mathf.Min(3, _emitters.Count);
            for (int k = 0; k < probes; k++)
            {
                _cursor = (_cursor + 1) % _emitters.Count;
                var e = _emitters[_cursor];
                if (e.Source == null || !e.Source.isPlaying) continue;
                e.OcclusionTarget = GameAudio.OcclusionEnabled
                    ? AudioOcclusion.Sample(lp, e.Source.transform.position, _listener.root, transform, GameAudio.OcclusionMask)
                    : 0f;
            }

            for (int i = 0; i < _emitters.Count; i++)
            {
                var e = _emitters[i];
                var s = e.Source;
                if (s == null) continue;
                float d = Vector3.Distance(lp, s.transform.position);
                bool inRange = d < s.maxDistance + 2f;
                if (inRange && !s.isPlaying)
                {
                    s.time = Random.Range(0f, s.clip.length * 0.95f);
                    s.Play();
                }
                else if (!inRange && s.isPlaying)
                {
                    s.Stop();
                    continue;
                }
                if (!s.isPlaying) continue;

                e.Occlusion = Mathf.MoveTowards(e.Occlusion, e.OcclusionTarget, dt * 3f);
                e.LowPass.enabled = e.Occlusion > 0.01f;
                if (e.LowPass.enabled) e.LowPass.cutoffFrequency = Mathf.Lerp(22000f, 700f, Mathf.Sqrt(e.Occlusion));
                float target = e.BaseVolume * GameAudio.AmbientVolume * Mathf.Lerp(1f, 0.45f, e.Occlusion);
                s.volume = Mathf.MoveTowards(s.volume, target, dt * 2f);
            }
        }
    }
}
