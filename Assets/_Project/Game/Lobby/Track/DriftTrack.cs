using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The drift track behind the lobby (layout: <see cref="DriftTrackLayout"/>), built at runtime in the real lobby
    /// scene only (the dead-lobby copy keeps its passage shutter closed): the asphalt pad (<see cref="DriftTrackPad"/>),
    /// barriers, fence and the entrance wall (<see cref="DriftTrackBounds"/>), the painted course, kerbs, cones and tyres
    /// (<see cref="DriftTrackCourse"/>), floodlight towers and the night sky (<see cref="DriftTrackLights"/>). The asphalt
    /// maps follow the graphics profile (<see cref="AsphaltMaps"/>). Seen from the lobby room the whole track (renderers
    /// and lights, not the colliders) is switched off unless the arch is in the camera's view.
    /// </summary>
    public sealed class DriftTrack : MonoBehaviour
    {
        public static DriftTrack Instance { get; private set; }
        /// <summary>Tests: draw the track whatever the camera sees.</summary>
        public static bool ForceVisible;

        public bool Shown { get; private set; } = true;
        public Material Asphalt { get; private set; }

        private readonly List<Renderer> _renderers = new();
        private readonly List<Light> _lights = new();
        private readonly Plane[] _planes = new Plane[6];

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("DriftTrack");
            Instance = go.AddComponent<DriftTrack>();
            Instance.Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            float t0 = Time.realtimeSinceStartup;
            var floor = FindInLobby("TrackPassageFloor");
            Asphalt = floor != null ? floor.GetComponent<MeshRenderer>().sharedMaterial : new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.2f, 0.21f) };
            AsphaltMaps.Attach(Asphalt);
            var wallSrc = FindInLobby("PassageWall");
            var brick = wallSrc != null ? new Material(wallSrc.GetComponent<MeshRenderer>().sharedMaterial) { mainTextureScale = new Vector2(0.5f, 0.5f) } : TrackMats.Get("TrackBrickPlain", new Color(0.5f, 0.32f, 0.26f), 0.1f);

            DriftTrackPad.Build(transform, Asphalt);
            DriftTrackBounds.Build(transform, brick);
            DriftTrackCourse.Build(transform);
            DriftTrackLights.Build(transform);

            // The passage shutter is up (rolled into its box above the gate).
            var shutter = FindInLobby("TrackShutter");
            if (shutter != null) shutter.SetActive(false);
            _found = null;

            GetComponentsInChildren(true, _renderers);
            GetComponentsInChildren(true, _lights);
            Debug.Log($"[Track] built in {(Time.realtimeSinceStartup - t0) * 1000f:0} ms: {_renderers.Count} renderers, {_lights.Count} lights, asphalt {AsphaltMaps.SizeForSettings()} px wanted");
        }

        private static System.Collections.Generic.Dictionary<string, GameObject> _found;

        /// <summary>A builder object of the real lobby scene (never of the dead-lobby copy, should both be loaded).</summary>
        private static GameObject FindInLobby(string name)
        {
            if (_found == null)
            {
                _found = new System.Collections.Generic.Dictionary<string, GameObject>();
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                foreach (var root in scene.GetRootGameObjects())
                {
                    var passage = root.transform.Find("TrackPassage");
                    if (passage == null) continue;
                    foreach (var t in passage.GetComponentsInChildren<Transform>(true))
                        if (!_found.ContainsKey(t.name)) _found[t.name] = t.gameObject;
                }
            }
            return _found.TryGetValue(name, out var go) ? go : null;
        }

        private void LateUpdate()
        {
            AsphaltMaps.Tick();
            FarClip();
            var cam = Camera.main;
            bool show = ForceVisible || cam == null || SeenFrom(cam);
            if (show == Shown) return;
            Shown = show;
            foreach (var r in _renderers) if (r != null) r.enabled = show;
            foreach (var l in _lights) if (l != null) l.enabled = show;
        }

        private float _farWas = -1f;

        /// <summary>The player's eyes see the whole 300 m pad and the sky from the track side (back to normal in the lobby).</summary>
        private void FarClip()
        {
            var me = Net.NetPlayer.Local;
            if (me == null || me.Camera == null) return;
            bool far = DriftTrackLayout.OnTrackSide(me.transform.position);
            if (far && _farWas < 0f) { _farWas = me.Camera.farClipPlane; me.Camera.farClipPlane = Mathf.Max(_farWas, 1000f); }
            else if (!far && _farWas > 0f) { me.Camera.farClipPlane = _farWas; _farWas = -1f; }
        }

        private void OnDisable()
        {
            var me = Net.NetPlayer.Local;
            if (_farWas > 0f && me != null && me.Camera != null) me.Camera.farClipPlane = _farWas;
            _farWas = -1f;
        }

        /// <summary>On the track side of the lobby wall, or the arch is in view from the room.</summary>
        private bool SeenFrom(Camera cam)
        {
            Vector3 p = cam.transform.position;
            if (DriftTrackLayout.OnTrackSide(p)) return true;
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            return GeometryUtility.TestPlanesAABB(_planes, DriftTrackLayout.ArchBounds);
        }
    }

    /// <summary>Shared runtime materials of the track (Standard, cached by name).</summary>
    public static class TrackMats
    {
        private static readonly Dictionary<string, Material> Cache = new();

        public static Material Get(string name, Color c, float gloss, float metal = 0f)
        {
            if (Cache.TryGetValue(name, out var m) && m != null) return m;
            m = new Material(Shader.Find("Standard")) { name = name, color = c };
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metal);
            Cache[name] = m;
            return m;
        }

        public static Material Glow(string name, Color c, Color emission)
        {
            if (Cache.TryGetValue(name, out var m) && m != null) return m;
            m = Get(name, c, 0.5f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        /// <summary>A mesh object under <paramref name="parent"/> (world space, no collider).</summary>
        public static GameObject Piece(Transform parent, string name, Mesh mesh, Material mat, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static BoxCollider Wall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = center;
            bc.size = size;
            return bc;
        }
    }
}
