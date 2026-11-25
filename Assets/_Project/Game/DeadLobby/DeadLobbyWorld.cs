using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The lobby copy of <see cref="DeadLobby"/>: loads scene <see cref="DeadLobby.SceneName"/> additively (local only,
    /// the garage stays the active scene and keeps its lighting), takes the wall TV over from its CCTV loop and gives every
    /// camera that renders inside the copy the lobby's own look (no fog, warm flat ambient, weak reflections - the
    /// values the lobby builder sets) only for that render; the garage cameras keep the garage's settings.
    /// </summary>
    public sealed class DeadLobbyWorld
    {
        /// <summary>Lobby look (MultiplayerBuilder.BuildLobbyScene / BuildLobbyRoom).</summary>
        private static readonly Color LobbyAmbient = new(0.09f, 0.08f, 0.07f);
        private const float LobbyReflections = 0.25f;
        /// <summary>Cameras closer than this to <see cref="DeadLobby.Offset"/> render the lobby.</summary>
        private const float LobbyRadius = 300f;

        public Scene Scene { get; private set; }
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public LobbyTv Tv { get; private set; }
        public LobbyExitDoor Door { get; private set; }

        private AsyncOperation _op;
        private bool _abandoned, _hooked;
        private Camera _swapped;
        private bool _fog;
        private AmbientMode _ambMode;
        private Color _amb;
        private float _refl;

        /// <summary>Lobby-local point to world (the copy's place).</summary>
        public static Vector3 ToWorld(Vector3 local) => local + DeadLobby.Offset;
        public static Vector3 ToLocal(Vector3 world) => world - DeadLobby.Offset;

        public void Load()
        {
            if (!Application.CanStreamedLevelBeLoaded(DeadLobby.SceneName))
            {
                Debug.LogWarning($"[DeadLobby] scene {DeadLobby.SceneName} is not in the build - old spectator instead");
                Failed = true;
                return;
            }
            SceneManager.sceneLoaded += OnLoaded;
            _op = SceneManager.LoadSceneAsync(DeadLobby.SceneName, LoadSceneMode.Additive);
            if (_op == null)
            {
                SceneManager.sceneLoaded -= OnLoaded;
                Failed = true;
            }
        }

        private void OnLoaded(Scene s, LoadSceneMode mode)
        {
            if (s.name != DeadLobby.SceneName || mode != LoadSceneMode.Additive) return;
            SceneManager.sceneLoaded -= OnLoaded;
            Scene = s;
            if (_abandoned)
            {
                Debug.Log("[DeadLobby] copy finished loading after the exit - unloading it");
                SceneManager.UnloadSceneAsync(s);
                return;
            }
            foreach (var root in s.GetRootGameObjects())
            {
                if (Tv == null) Tv = root.GetComponentInChildren<LobbyTv>(true);
                if (Door == null) Door = root.GetComponentInChildren<LobbyExitDoor>(true);
            }
            // The CCTV loop must not start: the screen shows the live feed (DeadLobbyTv).
            if (Tv != null) Tv.enabled = false;
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
            _hooked = true;
            Ready = true;
            Debug.Log($"[DeadLobby] copy loaded: {s.rootCount} roots, tv={(Tv != null)}, door={(Door != null)}, scenes now {SceneManager.sceneCount}");
        }

        /// <summary>Drops the copy. <paramref name="unload"/> false: leave it to NGO's single-mode lobby load.</summary>
        public void Unload(bool unload)
        {
            _abandoned = true;
            if (_hooked)
            {
                Camera.onPreCull -= PreCull;
                Camera.onPostRender -= PostRender;
                _hooked = false;
            }
            RestoreLook();
            if (!Ready) return; // still loading: OnLoaded unloads it
            SceneManager.sceneLoaded -= OnLoaded;
            if (unload && Scene.IsValid() && Scene.isLoaded) SceneManager.UnloadSceneAsync(Scene);
            Ready = false;
        }

        /// <summary>True when a camera at <paramref name="p"/> looks at the lobby copy.</summary>
        public static bool InLobbySpace(Vector3 p) => (p - DeadLobby.Offset).sqrMagnitude < LobbyRadius * LobbyRadius;

        private void PreCull(Camera c)
        {
            if (c == null || _swapped != null || !InLobbySpace(c.transform.position)) return;
            _swapped = c;
            _fog = RenderSettings.fog;
            _ambMode = RenderSettings.ambientMode;
            _amb = RenderSettings.ambientLight;
            _refl = RenderSettings.reflectionIntensity;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = LobbyAmbient;
            RenderSettings.reflectionIntensity = LobbyReflections;
        }

        private void PostRender(Camera c)
        {
            if (c != _swapped) return;
            RestoreLook();
        }

        private void RestoreLook()
        {
            if (_swapped == null) return;
            _swapped = null;
            RenderSettings.fog = _fog;
            RenderSettings.ambientMode = _ambMode;
            RenderSettings.ambientLight = _amb;
            RenderSettings.reflectionIntensity = _refl;
        }
    }
}
