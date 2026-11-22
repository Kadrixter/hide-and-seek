using System.Collections.Generic;
using System.Text;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Round over -> lobby, made robust and traceable (every step logs "[LobbyReturn] ...").
    /// <para>Server: the lobby load is started first and only when NGO accepted it (status Started) the phase turns
    /// Lobby. A refused load (another scene event still running, e.g. a player left during the map load) is retried
    /// every second instead of leaving everybody on the map with a "Lobby" phase forever. Each client is placed at a lobby
    /// spawn as soon as it reports its own lobby load, so one slow copy no longer keeps the others waiting.</para>
    /// <para>Every copy (host too): when the phase is Lobby but the lobby scene does not come within
    /// <see cref="LobbyWaitWarn"/> s, the host re-issues the load; a client whose NGO load never started (or failed)
    /// loads the lobby itself after <see cref="LobbyWaitFallback"/> s (network objects kept in DontDestroyOnLoad, so the
    /// next round's load still works) and leaves to the menu as the last resort after <see cref="LobbyWaitLeave"/> s.</para>
    /// </summary>
    public partial class MatchManager
    {
        public const float LobbyWaitWarn = 15f, LobbyWaitFallback = 25f, LobbyWaitLeave = 70f;

        // ---- server
        private bool _lobbyReturnPending;
        private string _lobbyReturnReason;
        private float _lobbyReturnSince, _lobbyRetryAt, _lobbyLoadIssuedAt = -1f;
        private int _lobbyReturnTries, _lobbySpawnIndex;
        private readonly HashSet<ulong> _lobbyPlaced = new();

        /// <summary>Lobby returns whose load finished on every copy (or timed out), for the tests.</summary>
        public int LobbyReturnsCompleted { get; private set; }
        /// <summary>Seconds from the lobby load to the last copy done, and how many copies timed out (last return).</summary>
        public float LastLobbyAllLoadedSeconds { get; private set; }
        public int LastLobbyTimedOut { get; private set; }

        // ---- every copy
        private bool _lobbyWaiting;
        private float _lobbyWaitSince, _lobbyNextLog;
        private AsyncOperation _lobbyLoadOp;
        private float _lobbyLoadSeenAt = -1f;
        private bool _lobbyFallbackDone;
        private float _lobbyReissueAt;

        private static float RealNow => Time.realtimeSinceStartup;
        private static string ActiveSceneName => SceneManager.GetActiveScene().name;

        private static void LR(string msg) => Debug.Log("[LobbyReturn] " + msg);
        private static void LRWarn(string msg) => Debug.LogWarning("[LobbyReturn] " + msg);

        private void HookLobbyReturn()
        {
            if (NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnSceneEvent += OnLobbySceneEvent;
        }

        private void UnhookLobbyReturn()
        {
            if (NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnSceneEvent -= OnLobbySceneEvent;
        }

        // ================================================================ server: start the return

        /// <summary>Server: back to the lobby (end screen over, race without racers, everybody left during the map load).</summary>
        private void BackToLobby(string reason = "round over")
        {
            if (!_lobbyReturnPending)
            {
                _lobbyReturnPending = true;
                _lobbyReturnReason = reason;
                _lobbyReturnSince = RealNow;
                _lobbyReturnTries = 0;
                _lobbyRetryAt = 0f;
                LR($"server: start ({reason}), phase {Phase.Value}, scene {ActiveSceneName}, players {NetPlayer.All.Count}, clients {NetworkManager.ConnectedClientsIds.Count}");
            }
            TryBackToLobby();
        }

        /// <summary>Server, every frame while a return is pending: asks NGO for the lobby load (at most once a second).</summary>
        private void TryBackToLobby()
        {
            if (!_lobbyReturnPending || RealNow < _lobbyRetryAt) return;
            _lobbyReturnTries++;
            var sm = NetworkManager != null ? NetworkManager.SceneManager : null;
            var status = sm != null ? sm.LoadScene(NetSession.LobbyScene, LoadSceneMode.Single) : SceneEventProgressStatus.None;
            if (status != SceneEventProgressStatus.Started)
            {
                _lobbyRetryAt = RealNow + 1f;
                if (_lobbyReturnTries == 1 || _lobbyReturnTries % 5 == 0)
                    LRWarn($"server: lobby load refused ({status}), try {_lobbyReturnTries}, waiting {RealNow - _lobbyReturnSince:0.0} s - retry in 1 s");
                return;
            }

            _lobbyReturnPending = false;
            _lobbyLoadIssuedAt = RealNow;
            _lobbyPlaced.Clear();
            _lobbySpawnIndex = 0;
            _finisherRunning = false;
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null) continue;
                pl.Role.Value = PlayerRole.None;
                ResetPlayer(pl);
            }
            Medkits.Clear();
            Syringes.Clear();
            Elevators.Clear();
            GlockPhase.Value = false;
            ChainsawTaken.Value = false;
            SecurityCrates.Value = 0;
            ResetRcCars();
            Phase.Value = MatchPhase.Lobby;
            LR($"server: lobby load started (try {_lobbyReturnTries}, {RealNow - _lobbyReturnSince:0.00} s after the start, {_lobbyReturnReason}), phase -> Lobby");
        }

        /// <summary>Server: may this game-scene load finish into a round? Not when a lobby return is waiting for it to end.</summary>
        private bool LobbyReturnBlocksRound(string sceneName)
        {
            if (!_lobbyReturnPending) return false;
            LR($"server: {sceneName} finished loading while a lobby return waits - no round, the lobby load follows");
            return true;
        }

        /// <summary>Server: the round start could not load the map: stay in the lobby instead of "Loading" forever.</summary>
        private bool LoadGameSceneOrStay(string what)
        {
            var status = NetworkManager.SceneManager.LoadScene(NetSession.GameScene, LoadSceneMode.Single);
            if (status == SceneEventProgressStatus.Started) return true;
            LRWarn($"server: {what}: map load refused ({status}) - staying in the lobby");
            foreach (var pl in NetPlayer.All) if (pl != null) pl.Role.Value = PlayerRole.None;
            Phase.Value = MatchPhase.Lobby;
            NetPlayer.FindByClient(NetworkManager.ServerClientId)?.ToastRpc("Nie udało się wczytać mapy - spróbuj jeszcze raz.", 4f);
            return false;
        }

        // ================================================================ scene events (every copy)

        private void OnLobbySceneEvent(SceneEvent e)
        {
            if (e == null || e.SceneName != NetSession.LobbyScene) return;
            ulong me = NetworkManager != null ? NetworkManager.LocalClientId : 0;
            switch (e.SceneEventType)
            {
                case SceneEventType.Load:
                    if (e.ClientId != me) break;
                    _lobbyLoadOp = e.AsyncOperation;
                    _lobbyLoadSeenAt = RealNow;
                    LR($"{Who}: NGO lobby load started (scene {ActiveSceneName}, phase {Phase.Value})");
                    break;
                case SceneEventType.LoadComplete:
                    if (e.ClientId == me)
                        LR($"{Who}: NGO lobby load complete{SinceText(_lobbyLoadSeenAt)}");
                    if (IsServer)
                    {
                        var pl = NetPlayer.FindByClient(e.ClientId);
                        LR($"server: client {e.ClientId} ({(pl != null ? pl.Nick : "?")}) has the lobby{SinceText(_lobbyLoadIssuedAt)}");
                        if (Phase.Value == MatchPhase.Lobby) PlaceInLobby(pl);
                    }
                    break;
                case SceneEventType.LoadEventCompleted:
                    if (!IsServer) break;
                    int timedOut = e.ClientsThatTimedOut != null ? e.ClientsThatTimedOut.Count : 0;
                    LastLobbyAllLoadedSeconds = _lobbyLoadIssuedAt >= 0f ? RealNow - _lobbyLoadIssuedAt : 0f;
                    LastLobbyTimedOut = timedOut;
                    LobbyReturnsCompleted++;
                    if (timedOut > 0)
                        LRWarn($"server: lobby load event done after {LastLobbyAllLoadedSeconds:0.00} s, TIMED OUT: {ClientList(e.ClientsThatTimedOut)}; done: {ClientList(e.ClientsThatCompleted)}");
                    else
                        LR($"server: every copy has the lobby after {LastLobbyAllLoadedSeconds:0.00} s ({ClientList(e.ClientsThatCompleted)})");
                    break;
            }
        }

        private string Who => IsServer ? "host" : "client " + (NetworkManager != null ? NetworkManager.LocalClientId : 0);

        private static string SinceText(float since) => since >= 0f ? $" after {RealNow - since:0.00} s" : "";

        private static string ClientList(List<ulong> ids)
        {
            if (ids == null || ids.Count == 0) return "-";
            var sb = new StringBuilder();
            foreach (var id in ids)
            {
                if (sb.Length > 0) sb.Append(", ");
                var pl = NetPlayer.FindByClient(id);
                sb.Append(id).Append(pl != null ? " " + pl.Nick : "");
            }
            return sb.ToString();
        }

        /// <summary>Server: puts a player at the next lobby spawn (once per return).</summary>
        private void PlaceInLobby(NetPlayer pl)
        {
            if (pl == null || !pl.IsSpawned || !_lobbyPlaced.Add(pl.OwnerClientId)) return;
            var spawns = SpawnPoints.Lobby();
            var sp = spawns.Count > 0 ? spawns[_lobbySpawnIndex++ % spawns.Count] : null;
            if (sp != null) pl.TeleportRpc(sp.position, sp.eulerAngles.y);
        }

        /// <summary>Server, lobby load done on every copy: places whoever was not placed yet (copies that timed out too).</summary>
        private void PlaceRemainingInLobby()
        {
            foreach (var pl in NetPlayer.All) PlaceInLobby(pl);
        }

        // ================================================================ watchdog (every copy)

        /// <summary>Phase change seen on this copy: a new return to the lobby starts the watch.</summary>
        private void OnPhaseLobbyReturn(MatchPhase prev, MatchPhase phase)
        {
            if (phase != MatchPhase.Lobby || prev == MatchPhase.Lobby) return;
            _lobbyWaiting = true;
            _lobbyWaitSince = RealNow;
            _lobbyNextLog = RealNow + 5f;
            _lobbyFallbackDone = false;
            _lobbyReissueAt = 0f;
            if (_lobbyLoadSeenAt >= 0f && RealNow - _lobbyLoadSeenAt > 3f) { _lobbyLoadOp = null; _lobbyLoadSeenAt = -1f; } // from an earlier return
            LR($"{Who}: phase {prev} -> Lobby, scene {ActiveSceneName}, players {NetPlayer.All.Count}");
        }

        private void UpdateLobbyReturnWatch()
        {
            if (!_lobbyWaiting) return;
            float waited = RealNow - _lobbyWaitSince;
            if (Phase.Value != MatchPhase.Lobby)
            {
                _lobbyWaiting = false;
                LR($"{Who}: phase is {Phase.Value} again after {waited:0.00} s - lobby watch stopped");
                return;
            }
            if (ActiveSceneName == NetSession.LobbyScene)
            {
                _lobbyWaiting = false;
                _lobbyLoadOp = null;
                _lobbyLoadSeenAt = -1f;
                LR($"{Who}: lobby scene active {waited:0.00} s after the phase change{(_lobbyFallbackDone ? " (local fallback load)" : "")}");
                return;
            }

            bool ngoLoading = _lobbyLoadOp != null && !_lobbyLoadOp.isDone;
            if (RealNow >= _lobbyNextLog)
            {
                _lobbyNextLog = RealNow + 5f;
                LRWarn($"{Who}: still not in the lobby after {waited:0.0} s: scene {ActiveSceneName}, NGO load " +
                       (_lobbyLoadOp == null ? "not started" : _lobbyLoadOp.isDone ? "done (scene not active)" : $"running {_lobbyLoadOp.progress:0.00}") +
                       (IsServer ? $", server retry pending={_lobbyReturnPending}" : ""));
            }

            if (IsServer)
            {
                // The host's own load is gone (refused / failed): ask NGO again once the previous event is over.
                if (!_lobbyReturnPending && !ngoLoading && waited > LobbyWaitWarn && RealNow >= _lobbyReissueAt)
                {
                    _lobbyReissueAt = RealNow + 5f; // refused (another scene event still running): again in 5 s
                    var status = NetworkManager.SceneManager.LoadScene(NetSession.LobbyScene, LoadSceneMode.Single);
                    LRWarn($"server: TIMEOUT {waited:0.0} s - lobby load issued again: {status}");
                    if (status == SceneEventProgressStatus.Started) _lobbyLoadIssuedAt = RealNow;
                }
                return;
            }

            // Client: NGO never started the lobby load here (or it ended without the scene): load it locally.
            if (!_lobbyFallbackDone && !ngoLoading && waited > LobbyWaitFallback)
            {
                _lobbyFallbackDone = true;
                LocalLobbyFallback(waited);
                return;
            }
            if (waited > LobbyWaitLeave)
            {
                _lobbyWaiting = false;
                LRWarn($"client: TIMEOUT {waited:0.0} s - no lobby even after the fallback: leaving to the menu");
                NetSession.Instance?.Leave("Nie udało się wrócić do lobby (przekroczony czas). Dołącz ponownie.");
            }
        }

        /// <summary>
        /// Client: loads the lobby without NGO. The spawned network objects (players, this manager) go to DontDestroyOnLoad
        /// first: they survive the load, and NGO skips objects already there when the next round's map load starts.
        /// </summary>
        private void LocalLobbyFallback(float waited)
        {
            int kept = 0;
            var spawned = NetworkManager != null && NetworkManager.SpawnManager != null ? NetworkManager.SpawnManager.SpawnedObjectsList : null;
            if (spawned != null)
                foreach (var no in new List<NetworkObject>(spawned))
                {
                    if (no == null || no.transform.parent != null) continue;
                    DontDestroyOnLoad(no.gameObject);
                    kept++;
                }
            LRWarn($"client: TIMEOUT {waited:0.0} s without the lobby (scene {ActiveSceneName}, NGO load {(_lobbyLoadOp == null ? "never started" : "ended")}) - " +
                   $"loading the lobby locally, {kept} network objects kept");
            SceneManager.LoadScene(NetSession.LobbyScene, LoadSceneMode.Single);
        }

        // ================================================================ test hooks (-testlobbyreturn)

        /// <summary>Test: ends the running round at once (hiders win), like the timer running out.</summary>
        public void TestEndRound() => EndMatch(MatchWinner.Hiders);

        /// <summary>Test: items thrown around every player (drops on the floor for the scene change to clean up).</summary>
        public void TestScatter(int perPlayer)
        {
            var kinds = new[] { HotbarItem.Medkit, HotbarItem.Syringe };
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null) continue;
                for (int i = 0; i < perPlayer; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    ThrowDrop(pl, kinds[i % kinds.Length], 0, dir, 0.4f, 1.2f, 2f, 1.5f, Quaternion.LookRotation(dir));
                }
            }
        }
    }
}
