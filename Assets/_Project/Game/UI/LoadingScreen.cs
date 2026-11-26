using System;
using System.Collections;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.UI
{
    /// <summary>What the loading screen waits for (picks the status line and the "ready" rule).</summary>
    public enum LoadingStage : byte { None, CreateServer, Connect, Lobby, Map, BackToLobby }

    /// <summary>
    /// Full-screen loading screen (persistent uGUI canvas above everything else, sorting order <see cref="SortOrder"/>):
    /// from host / join until the lobby is built, from the round start until the map is built and the round intro is
    /// about to start, and from the end of the round until the lobby is back. Purely UI: it reads the session state and
    /// the readiness signals of <see cref="MatchManager"/> (MatchManager.Readiness.cs) and never changes the network flow.
    /// <para>Shown by: the menu buttons (<see cref="Begin"/> + <see cref="RunWhenDrawn"/>: the network start waits until
    /// the screen has been drawn, the scene switch then freezes on it), Steam joins (NetSession.JoinSteamLobby), the phase
    /// changes Lobby -> Loading and * -> Lobby (<see cref="MatchManager.PhaseChangedLocal"/>, same frame as the load
    /// starts) and any NGO scene load of the lobby / map seen here (command-line hosts, the client's initial sync).</para>
    /// <para>Hidden when: lobby scene active + phase Lobby + own player spawned + lobby parts ran
    /// (<see cref="MatchManager.LobbyReadyFrame"/>) + frames smooth again; map scene active + round phase + map parts
    /// ran (<see cref="MatchManager.MapReadyFrame"/>) and then just before the intro (<see cref="MatchManager.IntroAt"/>),
    /// or after smooth frames when there is no intro (race, -nointro). A failed / cancelled / dropped connection hides
    /// it at once (the menu shows the message). Safety: "Anuluj" (mouse, Esc, pad Circle) during connecting and after
    /// <see cref="CancelAfter"/> s, and the screen gives up by itself after <see cref="GiveUpAfter"/> s.</para>
    /// <para>Off in -batchmode runs, with -noloadscreen and in scripted runs (-test*, *shots, *watch, -autostart...: their
    /// screenshots / timings stay as they were); -loadscreen shows it there too, but it never blocks their input.</para>
    /// </summary>
    public sealed partial class LoadingScreen : MonoBehaviour
    {
        public const int SortOrder = 1000;
        public const float CancelAfter = 15f, GiveUpAfter = 60f;
        private const float FadeIn = 0.15f, FadeOut = 0.35f;
        /// <summary>Frames in a row under this frame time count as "smooth" (the heavy builds are over).</summary>
        private const float SmoothFrame = 0.07f;
        private const int SmoothNeeded = 12;
        private const float SettleMin = 0.3f, SettleMax = 8f;

        private static LoadingScreen _inst;
        private static bool _automated;

        private LoadingStage _stage;
        private string _detail;
        private float _shownAt, _stageAt, _readySince = -1f;
        private int _stageFrame, _sceneFrame = -1, _smooth;
        private Scene _startScene;
        private bool _hiding;
        private float _alpha, _alphaTarget;
        private AsyncOperation _op;
        private string _opScene;
        private Coroutine _pending;
        private NetworkManager _hookedNm;
        private NetworkSceneManager _hookedSm;

        /// <summary>The screen exists (not batchmode / -noloadscreen).</summary>
        public static bool Available => _inst != null;

        /// <summary>On screen (also while fading out).</summary>
        public static bool Visible => _inst != null && _inst._stage != LoadingStage.None;

        /// <summary>Covers the game and takes the input (players do not move, Esc menu closed). Never in automated runs.</summary>
        public static bool Blocking => Visible && !_inst._hiding && !_automated;

        /// <summary>The "Anuluj" button is up: the cursor must be free (CursorPolicy).</summary>
        public static bool WantsCursor => Blocking && _inst._cancelShown;

        public static LoadingStage Stage => _inst != null ? _inst._stage : LoadingStage.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_inst != null || Application.isBatchMode) return;
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-noloadscreen") >= 0) return;
            foreach (var a in args)
                if (IsScripted(a)) _automated = true;
            if (_automated && Array.IndexOf(args, "-loadscreen") < 0) return;
            var go = new GameObject("LoadingScreen");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<LoadingScreen>();
        }

        /// <summary>A command-line switch of a scripted test / capture run.</summary>
        private static bool IsScripted(string a)
        {
            if (string.IsNullOrEmpty(a) || a[0] != '-') return false;
            a = a.ToLowerInvariant();
            return a.StartsWith("-test") || a.StartsWith("-lobbytest") || a == "-autostart" || a == "-quitafter" || a == "-shots"
                   || a.EndsWith("shots") || a.EndsWith("watch") || a.EndsWith("tour") || a.EndsWith("film") || a.EndsWith("frames");
        }

        // ================================================================ API

        /// <summary>
        /// Shows the screen for <paramref name="stage"/> (or switches the stage while it is up). <paramref name="fade"/>:
        /// a short fade in (menu buttons); without it the screen is fully up in this very frame (a load already runs).
        /// </summary>
        public static void Begin(LoadingStage stage, string detail = null, bool fade = false)
        {
            if (_inst == null || stage == LoadingStage.None) return;
            _inst.BeginStage(stage, detail, fade);
        }

        /// <summary>Hides the screen (connection failed: the menu shows the message). <paramref name="instant"/>: no fade.</summary>
        public static void Hide(string why, bool instant = false)
        {
            if (_inst == null || _inst._stage == LoadingStage.None) return;
            _inst.StopPending();
            _inst.HideNow(why, instant);
        }

        /// <summary>
        /// Runs <paramref name="action"/> once the screen is fully up and has been drawn (the work after it may freeze
        /// the main thread for seconds: the frozen frame is then the loading screen). Without the screen: right away.
        /// </summary>
        public static void RunWhenDrawn(Action action)
        {
            if (action == null) return;
            if (_inst == null || _inst._stage == LoadingStage.None) { action(); return; }
            _inst.StopPending();
            _inst._pending = _inst.StartCoroutine(_inst.AfterDrawn(action));
        }

        // ================================================================ lifecycle

        private void Awake()
        {
            SceneManager.activeSceneChanged += OnActiveScene;
            MatchManager.PhaseChangedLocal += OnPhase;
        }

        /// <summary>The view (procedural backdrop texture) is built while the menu idles, not on the first click.</summary>
        private void Start() => Invoke(nameof(Prebuild), 1.5f);

        private void Prebuild()
        {
            EnsureView();
            if (_stage == LoadingStage.None) _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveScene;
            MatchManager.PhaseChangedLocal -= OnPhase;
            if (_hookedSm != null) _hookedSm.OnSceneEvent -= OnNgoScene;
            if (_hookedNm != null) { _hookedNm.OnServerStarted -= HookScenes; _hookedNm.OnClientStarted -= HookScenes; }
            if (_inst == this) _inst = null;
        }

        private void BeginStage(LoadingStage stage, string detail, bool fade)
        {
            bool wasUp = _stage != LoadingStage.None && !_hiding;
            if (wasUp && stage == _stage)
            {
                if (string.IsNullOrEmpty(_detail)) _detail = detail; // keep the more precise one (server name)
                return;
            }
            if (!wasUp)
            {
                _shownAt = Time.unscaledTime;
                EnsureView();
                PickTip(true);
            }
            _hiding = false;
            _alphaTarget = 1f;
            if (!fade) _alpha = 1f;
            _stage = stage;
            _detail = detail;
            _stageAt = Time.unscaledTime;
            _stageFrame = Time.frameCount;
            _startScene = SceneManager.GetActiveScene();
            _readySince = -1f;
            _smooth = 0;
            _bar = 0f;
            if (stage == LoadingStage.CreateServer || stage == LoadingStage.Connect) _op = null;
            HookScenes();
            ApplyView(); // drawn in this frame's render already (Update may come after the stall)
            Debug.Log($"[Loading] {(wasUp ? "stage" : "show")} {stage}{(detail != null ? " (" + detail + ")" : "")}, scene {SceneManager.GetActiveScene().name}");
        }

        private void HideNow(string why, bool instant)
        {
            if (_stage == LoadingStage.None) return;
            if (!_hiding) Debug.Log($"[Loading] hide ({why}) after {Time.unscaledTime - _shownAt:0.0} s, stage {_stage}, scene {SceneManager.GetActiveScene().name}");
            bool hadCursor = WantsCursor;
            _hiding = true;
            _alphaTarget = 0f;
            if (instant) _alpha = 0f;
            if (hadCursor) RelockCursor();
            if (_alpha <= 0f) Finish();
        }

        private void Finish()
        {
            _stage = LoadingStage.None;
            _hiding = false;
            _op = null;
            _cancelShown = false;
            if (_canvas != null) _canvas.enabled = false;
        }

        private void StopPending()
        {
            if (_pending != null) StopCoroutine(_pending);
            _pending = null;
        }

        private IEnumerator AfterDrawn(Action action)
        {
            float until = Time.unscaledTime + 0.6f;
            while (_alpha < 1f && Time.unscaledTime < until) yield return null;
            yield return new WaitForEndOfFrame(); // this frame (fully up) reaches the screen
            yield return null;
            _pending = null;
            _stageAt = Time.unscaledTime; // the failure check counts from the real start
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ================================================================ triggers

        private void OnActiveScene(Scene from, Scene to) => _sceneFrame = Time.frameCount;

        private void OnPhase(MatchPhase prev, MatchPhase phase)
        {
            if (phase == MatchPhase.Loading && prev == MatchPhase.Lobby) Begin(LoadingStage.Map);
            else if (phase == MatchPhase.Lobby && prev != MatchPhase.Lobby) Begin(LoadingStage.BackToLobby);
        }

        /// <summary>Subscribes to the scene events of the running session (the host's lobby load fires inside StartHost).</summary>
        private void HookScenes()
        {
            var nm = NetworkManager.Singleton;
            if (nm != _hookedNm)
            {
                if (_hookedNm != null) { _hookedNm.OnServerStarted -= HookScenes; _hookedNm.OnClientStarted -= HookScenes; }
                _hookedNm = nm;
                if (nm != null) { nm.OnServerStarted += HookScenes; nm.OnClientStarted += HookScenes; }
            }
            var sm = nm != null ? nm.SceneManager : null;
            if (sm == _hookedSm) return;
            if (_hookedSm != null) _hookedSm.OnSceneEvent -= OnNgoScene;
            _hookedSm = sm;
            if (sm != null) sm.OnSceneEvent += OnNgoScene;
        }

        private void OnNgoScene(SceneEvent e)
        {
            try
            {
                var nm = NetworkManager.Singleton;
                if (e == null || nm == null || e.ClientId != nm.LocalClientId || e.SceneEventType != SceneEventType.Load) return;
                bool lobby = e.SceneName == NetSession.LobbyScene, map = e.SceneName == NetSession.GameScene;
                if (!lobby && !map) return;
                _op = e.AsyncOperation;
                _opScene = e.SceneName;
                if (map && _stage != LoadingStage.Map) Begin(LoadingStage.Map);
                else if (lobby && _stage != LoadingStage.Lobby && _stage != LoadingStage.BackToLobby)
                    Begin(SceneManager.GetActiveScene().name == NetSession.GameScene ? LoadingStage.BackToLobby : LoadingStage.Lobby, _detail);
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        // ================================================================ every frame

        private void Update()
        {
            if (_stage == LoadingStage.None) { HookScenes(); return; }
            HookScenes();
            float now = Time.unscaledTime;
            _alpha = Mathf.MoveTowards(_alpha, _alphaTarget, Time.unscaledDeltaTime / (_alphaTarget > _alpha ? FadeIn : FadeOut));
            if (_hiding)
            {
                if (_alpha <= 0f) { Finish(); return; }
                ApplyView();
                return;
            }

            if (now - _stageAt > GiveUpAfter)
            {
                Debug.LogWarning($"[Loading] SAFETY TIMEOUT {GiveUpAfter:0} s (stage {_stage}, scene {SceneManager.GetActiveScene().name}) - hiding");
                HideNow("safety timeout", false);
                return;
            }
            if (CheckSession()) return;
            if (CheckReady(now)) return;
            UpdateCancel(now);
            ApplyView();
        }

        /// <summary>The session failed / ended / was cancelled: off at once (the menu shows why). True = hidden.</summary>
        private bool CheckSession()
        {
            if (_pending != null) return false; // the start waits for the first drawn frame
            var nm = NetworkManager.Singleton;
            bool listening = nm != null && nm.IsListening;
            var ns = NetSession.Instance;
            if (_stage == LoadingStage.CreateServer || _stage == LoadingStage.Connect)
            {
                if (!listening)
                {
                    if (ns != null && ns.SteamBusy) return false; // Steam lobby still being created / joined
                    if (Time.unscaledTime - _stageAt < 0.6f) return false;
                    HideNow("not connected", false);
                    return true;
                }
                if (nm.IsServer || nm.IsConnectedClient)
                    BeginStage(LoadingStage.Lobby, _detail, false); // connected: now the lobby load
                return false;
            }
            if (!listening)
            {
                HideNow("session ended", true);
                return true;
            }
            return false;
        }

        private string _status;
        private float _progress = -1f; // < 0: indeterminate

        /// <summary>Status line + progress for the current stage; hides the screen once the target is ready. True = hiding.</summary>
        private bool CheckReady(float now)
        {
            switch (_stage)
            {
                case LoadingStage.CreateServer:
                    _status = "Tworzenie serwera…";
                    _progress = -1f;
                    return false;
                case LoadingStage.Connect:
                    _status = "Łączenie z serwerem…";
                    _progress = -1f;
                    return false;
            }

            bool map = _stage == LoadingStage.Map;
            string target = map ? NetSession.GameScene : NetSession.LobbyScene;
            var active = SceneManager.GetActiveScene();
            var mm = MatchManager.Instance;
            bool mmUp = mm != null && mm.IsSpawned;
            bool opRunning = _op != null && !_op.isDone && _opScene == target;
            // The target scene came after this stage started, or it already was the scene and nothing reloads it.
            bool sceneIn = active.name == target &&
                           (active != _startScene || _sceneFrame >= _stageFrame || (!opRunning && now - _stageAt > 0.5f));
            if (!sceneIn)
            {
                _status = map ? "Wczytywanie mapy…" : _stage == LoadingStage.BackToLobby ? "Powrót do lobby…" : "Wczytywanie lobby…";
                _progress = _op != null && _opScene == target && !_op.isDone
                    ? 0.05f + 0.6f * Mathf.Clamp01(_op.progress / 0.9f)
                    : 0.6f * (1f - Mathf.Exp(-(now - _stageAt) / 5f)); // no load handle (initial sync): a slow creep
                _readySince = -1f;
                return false;
            }

            int since = Mathf.Max(_sceneFrame, _stageFrame);
            var me = NetPlayer.Local;
            bool built;
            if (map)
                built = mmUp && mm.Phase.Value != MatchPhase.Loading && mm.Phase.Value != MatchPhase.Lobby && MatchManager.MapReadyFrame >= since;
            else
                built = mmUp && mm.Phase.Value == MatchPhase.Lobby && me != null && me.IsSpawned && MatchManager.LobbyReadyFrame >= since;
            if (!built)
            {
                _status = map ? "Budowanie parkingu…" : "Przygotowywanie lobby…";
                _progress = 0.7f;
                _readySince = -1f;
                return false;
            }

            if (_readySince < 0f) { _readySince = now; _smooth = 0; }
            _smooth = Time.unscaledDeltaTime < SmoothFrame ? _smooth + 1 : 0;

            if (map && !mm.RaceMode && mm.IntroAt.Value != 0d)
            {
                // Round intro ahead: off just before it starts (its first frames fade in from black, all of it is seen).
                _status = "Za chwilę start…";
                _progress = 0.97f;
                if (mm.IntroAt.Value > 0d && mm.Now >= mm.IntroAt.Value - FadeOut - 0.05)
                {
                    HideNow("intro starts", false);
                    return true;
                }
                return false;
            }

            _status = map ? "Za chwilę start…" : "Prawie gotowe…";
            _progress = 0.75f + 0.25f * Mathf.Clamp01(_smooth / (float)SmoothNeeded);
            float waited = now - _readySince;
            if ((_smooth >= SmoothNeeded && waited >= SettleMin) || waited >= SettleMax)
            {
                HideNow(_smooth >= SmoothNeeded ? "ready" : "ready (frames never settled)", false);
                return true;
            }
            return false;
        }

        // ================================================================ cancel

        private bool _cancelShown;

        private void UpdateCancel(float now)
        {
            bool connecting = _stage == LoadingStage.CreateServer || _stage == LoadingStage.Connect;
            bool show = connecting || now - _shownAt > CancelAfter;
            if (show != _cancelShown)
            {
                _cancelShown = show;
                if (!show) RelockCursor();
            }
            if (!_cancelShown) return;
            // Pad Circle: in the menu PadUi presses the top-most "Cancel" button; in a session it is ours to read.
            bool pad = CursorPolicy.Active && PadUi.Back;
            var kb = Keyboard.current;
            if (pad || (kb != null && kb.escapeKey.wasPressedThisFrame)) Cancel();
        }

        /// <summary>"Anuluj": stop waiting, leave the session, back to the menu.</summary>
        private void Cancel()
        {
            if (_stage == LoadingStage.None || _hiding) return;
            bool connecting = _stage == LoadingStage.CreateServer || _stage == LoadingStage.Connect;
            Debug.Log($"[Loading] cancelled by the player after {Time.unscaledTime - _shownAt:0.0} s (stage {_stage})");
            StopPending();
            HideNow("cancelled", true);
            var nm = NetworkManager.Singleton;
            if (NetSession.Instance != null && (connecting || (nm != null && nm.IsListening)))
                NetSession.Instance.Leave(connecting ? null : "Anulowano wczytywanie.");
        }

        /// <summary>Back in play after the cancel button freed the cursor: CursorPolicy locks it again next frame.</summary>
        private static void RelockCursor()
        {
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null && CursorPolicy.Active && !CursorPolicy.WantsFree) me.InputReader.SetCursorLocked(true);
        }
    }
}
