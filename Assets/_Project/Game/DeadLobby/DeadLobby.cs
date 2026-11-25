using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>Where a dead / escaped player is in the dead-lobby flow (see <see cref="DeadLobby"/>).</summary>
    public enum DeadLobbyStage : byte { None, FadeOut, Loading, WalkIn, Lobby, Watching }

    /// <summary>
    /// Killed or escaped during a round: after a short fade the player walks in through the lobby door (first person)
    /// and spends the rest of the round in the lobby. The round goes on in the garage for the living.
    /// <para>Architecture: only this peer loads a copy of the lobby (scene <see cref="SceneName"/>, built by
    /// MultiplayerBuilder.DeadLobby.cs at <see cref="Offset"/>, far from the garage) additively and without NGO - the
    /// living never load it, nothing networked lives in it. Physics, lights and 3D sound are kept apart by the
    /// distance; the lobby's own look (no fog, warm ambient) is applied only to cameras inside it
    /// (<see cref="DeadLobbyWorld"/>). The body is a local first-person controller (<see cref="DeadLobbyBody"/>); its
    /// pose goes out in <see cref="NetPlayer.LobbyGhost"/> and the other dead players' copies draw it
    /// (<see cref="DeadLobbyAvatars"/>). The corpse stays where it fell. The wall TV shows the round through the eyes of
    /// a living player (<see cref="DeadLobbyTv"/>), the living players' voices come out of its speaker
    /// (<see cref="DeadLobbyVoice"/>); F at the TV switches to the old full-screen spectator and back.</para>
    /// <para>The copy is dropped when the round ends (phase Ended: the outro takes the screen as before), when the player is
    /// brought back (adrenaline) or when the session ends.</para>
    /// </summary>
    public sealed class DeadLobby : MonoBehaviour
    {
        public const string SceneName = "LobbyDead";
        /// <summary>Where the lobby copy stands (the real lobby is centred on the origin, the garage around it).</summary>
        public static readonly Vector3 Offset = new(0f, 0f, 2000f);
        /// <summary>Seconds after the death (or the escape) before the fade to the lobby starts.</summary>
        public const float EnterDelay = 3f;
        private const float FadeOutTime = 0.8f;

        private static DeadLobby _inst;
        private DeadLobbyStage _stage;
        private float _observerSince = -1f;
        private bool _failedThisRound;
        private DeadLobbyWorld _world;
        private DeadLobbyBody _body;
        private DeadLobbyHud _hud;

        public static DeadLobbyStage Stage => _inst != null ? _inst._stage : DeadLobbyStage.None;
        /// <summary>The own view is the lobby body (walking in or around).</summary>
        public static bool InLobby => Stage == DeadLobbyStage.WalkIn || Stage == DeadLobbyStage.Lobby;
        /// <summary>Full-screen spectating from the lobby TV (old <see cref="SpectatorCamera"/>).</summary>
        public static bool Watching => Stage == DeadLobbyStage.Watching;
        /// <summary>The lobby copy is loaded and in use (body in it, or watching from its TV).</summary>
        public static bool InWorld => InLobby || Watching;
        /// <summary>May GameHud run the old spectator camera? Before the walk-in (death view under the fade) and while watching.</summary>
        public static bool AllowsSpectator => !InLobby;
        public static DeadLobbyWorld World => _inst != null ? _inst._world : null;
        public static DeadLobbyBody Body => _inst != null ? _inst._body : null;
        /// <summary>Test hook: F as if pressed at the TV / while watching.</summary>
        public static bool TestToggleWatch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_inst != null) return;
            var go = new GameObject("DeadLobby");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<DeadLobby>();
        }

        private void Update()
        {
            GuardSceneSync();
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            var phase = mm != null && mm.IsSpawned ? mm.Phase.Value : MatchPhase.Lobby;
            bool round = me != null && me.IsSpawned && mm != null && mm.IsSpawned && !mm.RaceMode &&
                         (phase == MatchPhase.Hiding || phase == MatchPhase.Seeking) && SceneManager.GetActiveScene().name == NetSession.GameScene;
            if (!round) _failedThisRound = false;
            bool observer = round && me.IsObserver;
            if (!observer) _observerSince = -1f;
            else if (_observerSince < 0f) _observerSince = Time.time;

            if (_stage != DeadLobbyStage.None && !observer)
            {
                Exit(me, phase, me == null ? "session over" : !round ? $"phase {phase}" : "back in the round");
                return;
            }
            var kb = Keyboard.current;
            bool f = TestToggleWatch || (kb != null && kb.fKey.wasPressedThisFrame && !LocalPlayerController.MenuOpen);
            TestToggleWatch = false;
            switch (_stage)
            {
                case DeadLobbyStage.None:
                    if (observer && !_failedThisRound && Time.time - _observerSince >= EnterDelay && !KillCam.Active)
                    {
                        Hud.FadeTo(Color.white, 1f, FadeOutTime); // into the white light behind the lobby door
                        SetStage(DeadLobbyStage.FadeOut, me.IsObserver && me.Escaped.Value ? "escaped" : "dead");
                    }
                    break;
                case DeadLobbyStage.FadeOut:
                    if (!Hud.Fading)
                    {
                        _world = new DeadLobbyWorld();
                        _world.Load();
                        SetStage(DeadLobbyStage.Loading, "fade done, loading " + SceneName);
                    }
                    break;
                case DeadLobbyStage.Loading:
                    if (_world.Failed)
                    {
                        _failedThisRound = true; // the old spectator stays for this round
                        Exit(me, phase, "lobby copy could not be loaded");
                    }
                    else if (_world.Ready)
                    {
                        _body = DeadLobbyBody.Create(_world, me);
                        DeadLobbyTv.Create(_world, me);
                        DeadLobbyAvatars.Create(_world);
                        Hud.FadeFrom(Color.white, 1.2f);
                        SetStage(DeadLobbyStage.WalkIn, "lobby copy ready");
                    }
                    break;
                case DeadLobbyStage.WalkIn:
                    if (_body == null) { Exit(me, phase, "body lost"); break; }
                    if (_body.WalkInDone)
                    {
                        SetStage(DeadLobbyStage.Lobby, "walk-in done");
                        GameHud.Banner("LOBBY", "Runda trwa dalej. Na telewizorze oglądasz grę na żywo.", 4f, new Color(0.8f, 0.8f, 0.82f));
                    }
                    break;
                case DeadLobbyStage.Lobby:
                    if (f && DeadLobbyTv.Instance != null && DeadLobbyTv.Instance.InReach)
                        SetStage(DeadLobbyStage.Watching, "F at the TV -> full screen");
                    break;
                case DeadLobbyStage.Watching:
                    if (f) SetStage(DeadLobbyStage.Lobby, "F -> back to the lobby body");
                    break;
            }
        }

        private void LateUpdate()
        {
            if (_body != null) _body.ApplyView(InLobby);
        }

        private DeadLobbyHud Hud => _hud != null ? _hud : (_hud = DeadLobbyHud.Create(transform));

        private void SetStage(DeadLobbyStage s, string why)
        {
            Debug.Log($"[DeadLobby] {_stage} -> {s} ({why})");
            var tv = DeadLobbyTv.Instance;
            if (tv != null && s == DeadLobbyStage.Watching) tv.HandOverToSpectator();
            else if (tv != null && _stage == DeadLobbyStage.Watching) tv.TakeBackFromSpectator();
            // The ears follow the view: garage acoustics and bed while watching full screen, the lobby's again after.
            if (s == DeadLobbyStage.Watching) GarageSound();
            else if (_stage == DeadLobbyStage.Watching && s == DeadLobbyStage.Lobby) AudioEnvironment.Apply(AudioEnvironmentKind.SmallRoom, true);
            _stage = s;
        }

        /// <summary>Leaves the lobby copy: own camera back, the copy unloaded (not when NGO is loading the real lobby), sound back.</summary>
        private void Exit(NetPlayer me, MatchPhase phase, string why)
        {
            Debug.Log($"[DeadLobby] {_stage} -> None ({why})");
            bool hadWorld = _world != null && _world.Ready;
            _stage = DeadLobbyStage.None;
            DeadLobbyVoice.ResetAll();
            if (_body != null) { _body.Dispose(); _body = null; }
            DeadLobbyTv.DestroyInstance();
            DeadLobbyAvatars.DestroyInstance();
            // Phase Lobby: NGO's single-mode lobby load replaces every scene anyway - an own unload would race it.
            _world?.Unload(phase != MatchPhase.Lobby);
            _world = null;
            Hud.Clear();
            if (me != null && me.IsSpawned)
            {
                if (me.IsOwner && me.LobbyGhost.Value.Mode != LobbyGhostState.Off)
                    me.LobbyGhost.Value = new LobbyGhostState { Mode = LobbyGhostState.Off, Seat = -1 };
                if (me.Camera != null)
                {
                    me.Camera.enabled = true;
                    var l = me.Camera.GetComponent<AudioListener>();
                    if (l != null) l.enabled = true;
                }
            }
            if (!hadWorld) return;
            if (phase == MatchPhase.Hiding || phase == MatchPhase.Seeking)
            {
                GarageSound();
                Hud.FadeFrom(Color.black, 0.6f);
            }
            else if (phase == MatchPhase.Ended) AudioEnvironment.Apply(AudioEnvironmentKind.Garage, false); // the outro sets its own bed
        }

        /// <summary>Garage acoustics and bed (tension music in the Glock phase), as the round has them.</summary>
        private static void GarageSound()
        {
            AudioEnvironment.Apply(AudioEnvironmentKind.Garage, true);
            var mm = MatchManager.Instance;
            if (mm != null && mm.GlockPhase.Value) GameAudio.SetBed(AmbientId.TensionMusic, 1f);
        }

        /// <summary>
        /// Never hand the lobby copy to anybody through NGO (a synchronisation of a joining client would list every loaded
        /// scene of the host; joins are refused during a round, this is the second lock).
        /// </summary>
        private static void GuardSceneSync()
        {
            var nm = NetworkManager.Singleton;
            var sm = nm != null && nm.IsListening ? nm.SceneManager : null;
            if (sm == null || sm.VerifySceneBeforeLoading != null) return;
            sm.VerifySceneBeforeLoading = (index, name, mode) => name != SceneName;
        }

        private void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }
    }
}
