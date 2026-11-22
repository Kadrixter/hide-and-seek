using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server-authoritative round rules, spawned once by the host and kept across scene loads.
    /// Lobby -> (start button) -> Loading -> Hiding (seeker blind) -> Seeking (hints, Glock at the end) -> Ended -> Lobby.
    /// </summary>
    public partial class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        public readonly NetworkVariable<MatchPhase> Phase = new(MatchPhase.Lobby);
        public readonly NetworkVariable<double> PhaseEnd = new();
        public readonly NetworkVariable<NetSettings> Settings = new(NetSettings.Default);
        public readonly NetworkVariable<MatchWinner> Winner = new();
        public readonly NetworkVariable<bool> GlockPhase = new();
        /// <summary>The chainsaw on the generator-room shelf has been picked up this round.</summary>
        public readonly NetworkVariable<bool> ChainsawTaken = new();
        /// <summary>Level shown on the security-room monitors (0, 1, 2 = levels 0, -1, -2), same for every player.</summary>
        public readonly NetworkVariable<byte> SecurityFloor = new();
        /// <summary>Somebody stands in a security room: the cameras on the monitors light their red LED.</summary>
        public readonly NetworkVariable<bool> SecurityWatched = new();
        /// <summary>Bit per level (0, -1, -2): the crate in front of the security room door has been pushed aside.</summary>
        public readonly NetworkVariable<byte> SecurityCrates = new();
        public NetworkList<MedkitState> Medkits;
        public NetworkList<MedkitState> Syringes;
        public NetworkList<ElevatorState> Elevators;

        /// <summary>Raised on every client for hint pings (seeker minimap). Args: world position.</summary>
        public static event System.Action<Vector3> HintPinged;

        private float CurrentHintInterval => GlockPhase.Value
            ? Mathf.Min(Settings.Value.HintInterval, Tuning.GlockHintInterval)
            : Settings.Value.HintInterval;
        private ulong _lastSeekerClient = ulong.MaxValue;
        private readonly HashSet<string> _seekerHistory = new();

        /// <summary>
        /// Fair rotation: everybody is the seeker once (random order), then a new random round of turns starts.
        /// The previous seeker is never picked twice in a row when somebody else is available.
        /// </summary>
        public NetPlayer PickSeeker(List<NetPlayer> players)
        {
            var candidates = players.FindAll(x => !_seekerHistory.Contains(x.Nick) && x.OwnerClientId != _lastSeekerClient);
            if (candidates.Count == 0)
            {
                _seekerHistory.Clear();
                candidates = players.FindAll(x => x.OwnerClientId != _lastSeekerClient);
            }
            if (candidates.Count == 0) candidates = players;
            var seeker = candidates[Random.Range(0, candidates.Count)];
            _seekerHistory.Add(seeker.Nick);
            _lastSeekerClient = seeker.OwnerClientId;
            Debug.Log($"[Match] seeker {seeker.Nick} (this turn so far: {string.Join(", ", _seekerHistory)})");
            return seeker;
        }

        public void TestDown(NetPlayer pl) => Down(pl);
        public void TestKill(NetPlayer pl) => Kill(pl);
        private readonly Dictionary<ulong, double> _lastShot = new();
        private readonly HashSet<ulong> _reloading = new();

        public double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        /// <summary>Bleed-out time of this round (lobby setting).</summary>
        public float BleedOut => Settings.Value.BleedOut > 1f ? Settings.Value.BleedOut : Tuning.BleedOutTime;

        /// <summary>Raised on every client when a hider goes down (minimap / notifications).</summary>
        public static event System.Action<NetPlayer> HelpNeeded;

        [Rpc(SendTo.Everyone)]
        private void HelpNeededRpc(ulong downedId)
        {
            var downed = NetPlayer.Find(downedId);
            if (downed == null) return;
            HelpNeeded?.Invoke(downed);
            var local = NetPlayer.Local;
            if (local == null || local == downed || !local.IsHider) return;
            GameAudio.Play2D(GameSfx.UiError, 0.9f);
            GameHud.Banner("POMOCY!", $"{downed.Nick} potrzebuje pomocy - czerwony znacznik na mapie.", 4f, new Color(1f, 0.35f, 0.3f));
        }
        public double Remaining => PhaseEnd.Value - Now;

        private void Awake()
        {
            Medkits = new NetworkList<MedkitState>();
            Syringes = new NetworkList<MedkitState>();
            Elevators = new NetworkList<ElevatorState>();
            VendingCans = new NetworkList<VendingState>();
            InitBricks();
            InitRadio();
            InitRods();
            InitToyGuns();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Phase.OnValueChanged += OnPhaseChanged;
            if (IsServer)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
                NetworkManager.OnClientDisconnectCallback += OnClientLeft;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (IsServer && NetworkManager != null)
            {
                if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
                NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
            }
        }

        // ================================================================ client side reactions

        private void OnPhaseChanged(MatchPhase prev, MatchPhase phase)
        {
            var local = NetPlayer.Local;
            Debug.Log($"[Match] phase {prev} -> {phase}, players {NetPlayer.All.Count}, me {(local != null ? local.Role.Value.ToString() : "-")}");
            switch (phase)
            {
                case MatchPhase.Hiding:
                    GameAudio.Play2D(GameSfx.RoundStart);
                    if (local != null)
                        GameHud.Banner(local.IsSeeker ? "SZUKASZ" : "CHOWASZ SIĘ",
                            local.IsSeeker ? "Czekaj, aż wszyscy się schowają..." : "Uciekaj i schowaj się, zanim szukający wyruszy!", 4f,
                            local.IsSeeker ? Color.red : UI.UiKit.HiderColor);
                    break;
                case MatchPhase.Seeking:
                    GameAudio.Play2D(GameSfx.SeekerReleased);
                    GameHud.Banner("SZUKAJĄCY WYRUSZYŁ", local != null && local.IsSeeker ? "Znajdź ich wszystkich." : "Nie daj się złapać.", 3f, Color.red);
                    break;
                case MatchPhase.Ended:
                    break;
            }
        }

        /// <summary>Sent after Winner is set, so every client sees the right result (variable order is not guaranteed).</summary>
        [Rpc(SendTo.Everyone)]
        private void MatchEndedRpc(MatchWinner winner)
        {
            if (winner == MatchWinner.Racer) { RaceEndedLocal(); return; }
            var local = NetPlayer.Local;
            bool seekerWon = winner == MatchWinner.Seeker;
            bool iWon = local != null && (local.IsSeeker == seekerWon);
            GameAudio.Play2D(iWon ? GameSfx.WinSting : GameSfx.LoseSting);
            GameAudio.SetBed(AmbientId.None);
            GameHud.Banner(seekerWon ? "WYGRYWA SZUKAJĄCY" : "WYGRYWAJĄ UKRYCI",
                iWon ? "Wygrana :D" : "Przegrana :/", Tuning.EndScreenTime - 1f, iWon ? UI.UiKit.Accent : UI.UiKit.Danger);
        }

        [Rpc(SendTo.Everyone)]
        private void HintRpc(Vector3 position, ulong hiderId, int clipIndex, bool sound, bool pin)
        {
            var hider = NetPlayer.Find(hiderId);
            if (sound)
            {
                var clip = HintVoices.Get(clipIndex);
                if (clip != null) GameAudio.PlayClipAt(clip, hider != null ? hider.ChestPosition : position, 1f, 45f);
                else if (hider != null) GameAudio.PlayAttached(GameSfx.HintWhistle, hider.transform);
                else GameAudio.PlayAt(GameSfx.HintWhistle, position);
            }
            if (pin) HintPinged?.Invoke(position);
        }

        /// <summary>A hider set off a car alarm: horn + hazard lights everywhere, minimap pin for the seeker (no text - the sound says it all).</summary>
        [Rpc(SendTo.Everyone)]
        public void CarAlarmRpc(int car, float duration, ulong hiderId)
        {
            CarAlarms.Ensure();
            var alarms = CarAlarms.Instance;
            alarms.PlayAlarm(car, duration);
            Vector3 pos = alarms.CarPosition(car);
            HintPinged?.Invoke(pos); // minimap shows pins to the seeker only
        }

        [Rpc(SendTo.Everyone)]
        private void GlockGivenRpc()
        {
            GameAudio.SetBed(AmbientId.TensionMusic, 3f);
            var local = NetPlayer.Local;
            if (local != null && local.IsSeeker)
                GameHud.Banner("GLOCK", "Ostatnie 2 minuty. Q - wyciągnij / schowaj, LPM - strzał, PPM - celowanie, R - przeładuj.", 5f, Color.yellow);
            else
                GameHud.Banner("UWAGA", "Szukający dostał pistolet. Ostatnie 2 minuty.", 4f, Color.yellow);
        }

        // ================================================================ settings / start (from lobby)

        [Rpc(SendTo.Server)]
        public void SetSettingsRpc(NetSettings s, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != NetworkManager.ServerClientId || Phase.Value != MatchPhase.Lobby) return;
            s.HideTime = Mathf.Clamp(s.HideTime, 10f, 300f);
            s.SeekTime = Mathf.Clamp(s.SeekTime, 60f, 3600f);
            s.HintStart = Mathf.Clamp(s.HintStart, 0f, s.SeekTime);
            s.HintInterval = Mathf.Clamp(s.HintInterval, 15f, 600f);
            s.Medkits = (byte)Mathf.Clamp(s.Medkits, 0, 10);
            s.Syringes = (byte)Mathf.Clamp(s.Syringes, 0, 6);
            s.BleedOut = Mathf.Clamp(s.BleedOut, 20f, 300f);
            if (s.Mode >= GameModes.All.Length) s.Mode = 0;
            s.Laps = (byte)Mathf.Clamp(s.Laps == 0 ? 3 : s.Laps, 1, 10);
            Settings.Value = s;
        }

        [Rpc(SendTo.Server)]
        public void RequestStartRpc(RpcParams p = default)
        {
            if (Phase.Value != MatchPhase.Lobby) return;
            var starter = NetPlayer.FindByClient(p.Receive.SenderClientId);
            if (p.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                starter?.ToastRpc("Tylko właściciel pokoju może rozpocząć grę.", 3f);
                return;
            }
            if (NetPlayer.All.Count < GameSettings.MinPlayers && !RaceMode)
            {
                starter?.ToastRpc("Potrzeba co najmniej 2 graczy.", 3f);
                return;
            }
            var mode = GameModes.Get(Settings.Value.Mode);
            if (!mode.Ready)
            {
                starter?.ToastRpc($"Tryb \"{mode.Name}\" jeszcze nie jest gotowy. Wybierz inny na tablicy ustawień.", 4f);
                return;
            }
            var notReady = new List<string>();
            foreach (var pl in NetPlayer.All)
                if (pl != null && pl.OwnerClientId != NetworkManager.ServerClientId && !pl.Ready.Value) notReady.Add(pl.Nick);
            if (notReady.Count > 0)
            {
                string list = string.Join(", ", notReady);
                if (list.Length > 70) list = list.Substring(0, 70) + "...";
                starter?.ToastRpc("Nie są gotowi: " + list, 4f);
                return;
            }
            StartMatch();
        }

        /// <summary>Call button / cabin panel: send the car to a level.</summary>
        [Rpc(SendTo.Server)]
        public void ElevatorRequestRpc(byte car, sbyte floor)
        {
            ElevatorSystem.Instance?.ServerRequest(this, car, floor, Now);
        }

        [Rpc(SendTo.Everyone)]
        public void ButtonPressedRpc(Vector3 pos)
        {
            GameAudio.PlayAt(GameSfx.LobbyButtonPress, pos);
            if (LobbyButton.Instance != null) LobbyButton.Instance.Animate();
        }

        private void StartMatch()
        {
            if (RaceMode) { StartRace(); return; }
            var players = new List<NetPlayer>(NetPlayer.All);
            players.RemoveAll(x => x == null);
            var pickFrom = players;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-forcehider") >= 0 && players.Count > 1)
                pickFrom = players.FindAll(x => x.OwnerClientId != NetworkManager.ServerClientId);
            var seeker = PickSeeker(pickFrom);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-forceseeker") >= 0)
                seeker = NetPlayer.FindByClient(NetworkManager.ServerClientId) ?? seeker;
            _lastSeekerClient = seeker.OwnerClientId;

            foreach (var pl in players)
            {
                pl.Role.Value = pl == seeker ? PlayerRole.Seeker : PlayerRole.Hider;
                ResetPlayer(pl);
            }
            Winner.Value = MatchWinner.None;
            GlockPhase.Value = false;
            ChainsawTaken.Value = false;
            SecurityCrates.Value = 0;
            ResetRcCars();
            Phase.Value = MatchPhase.Loading;
            NetworkManager.SceneManager.LoadScene(NetSession.GameScene, LoadSceneMode.Single);
        }

        private void ResetPlayer(NetPlayer pl)
        {
            pl.Life.Value = LifeState.Alive;
            pl.Wounded.Value = false;
            pl.Limping.Value = false;
            pl.MedkitCount.Value = 0;
            pl.SyringeCount.Value = 0;
            pl.Resurrected.Value = false;
            pl.Severed.Value = 0;
            pl.AttackReadyAt.Value = 0;
            pl.BlindUntil.Value = 0;
            pl.DustReadyAt.Value = 0;
            pl.Held.Value = HeldItem.None;
            pl.HasGlock.Value = false;
            pl.HasChainsaw.Value = false;
            pl.Ammo.Value = 0;
            pl.SpareMags.Value = 0;
            pl.Busy.Value = false;
            pl.DownedUntil.Value = 0;
            pl.NextHintAt.Value = 0;
            pl.Ready.Value = false;
        }

        private void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == NetSession.GameScene && Phase.Value == MatchPhase.Loading && RaceMode)
            {
                OnRaceSceneLoaded();
            }
            else if (sceneName == NetSession.GameScene && Phase.Value == MatchPhase.Loading)
            {
                PlacePlayersInMap();
                SpawnMedkits();
                SpawnSyringes();
                Elevators.Clear();
                if (ElevatorSystem.Instance != null)
                    foreach (var e in ElevatorSystem.Instance.InitialStates()) Elevators.Add(e);
                Phase.Value = MatchPhase.Hiding;
                PhaseEnd.Value = Now + Settings.Value.HideTime;
                SceneBedRpc((byte)AmbientId.GarageBed);
            }
            else if (sceneName == NetSession.LobbyScene)
            {
                int i = 0;
                var spawns = SpawnPoints.Lobby();
                foreach (var pl in NetPlayer.All)
                {
                    if (pl == null) continue;
                    var sp = spawns.Count > 0 ? spawns[i++ % spawns.Count] : null;
                    if (sp != null) pl.TeleportRpc(sp.position, sp.eulerAngles.y);
                }
                SceneBedRpc((byte)AmbientId.LobbyRoom);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void SceneBedRpc(byte bed) => GameAudio.SetBed((AmbientId)bed);

        private void PlacePlayersInMap()
        {
            var hiderSpawns = SpawnPoints.Hider();
            Shuffle(hiderSpawns);
            var seekerSpawn = SpawnPoints.Seeker();
            int i = 0;
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null) continue;
                Transform sp = pl.IsSeeker ? seekerSpawn : (hiderSpawns.Count > 0 ? hiderSpawns[i++ % hiderSpawns.Count] : null);
                if (sp == null) sp = seekerSpawn;
                if (sp != null) pl.TeleportRpc(sp.position + Vector3.up * 0.05f, sp.eulerAngles.y);
                pl.ToastRpc(pl.IsSeeker ? "Jesteś SZUKAJĄCYM" : "Jesteś CHOWAJĄCYM SIĘ", 4f);
            }
        }

        /// <summary>Adrenaline syringes at random hider spawn areas (other spots than the medkits).</summary>
        private void SpawnSyringes()
        {
            Syringes.Clear();
            var spots = SpawnPoints.Hider();
            Shuffle(spots);
            int count = Mathf.Min(Settings.Value.Syringes, spots.Count);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = spots[i].position;
                Vector3 off = Random.insideUnitSphere;
                off.y = 0f;
                p += off.normalized * Random.Range(2f, 5f);
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                    p = hit.point;
                Syringes.Add(new MedkitState { Id = 100 + i, Position = p + Vector3.up * 0.02f, Taken = false });
            }
        }

        private void SpawnMedkits()
        {
            Medkits.Clear();
            var spots = SpawnPoints.Hider();
            Shuffle(spots);
            int count = Mathf.Min(Settings.Value.Medkits, spots.Count);
            for (int i = 0; i < count; i++)
            {
                // Offset from the spawn point so medkits are not exactly where hiders start.
                Vector3 p = spots[spots.Count - 1 - i].position;
                Vector3 off = Random.insideUnitSphere;
                off.y = 0f;
                p += off.normalized * Random.Range(1.5f, 4f);
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                    p = hit.point;
                Medkits.Add(new MedkitState { Id = i + 1, Position = p + Vector3.up * 0.02f, Taken = false });
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ================================================================ server loop

        // ================================================================ medkit visuals (all clients)

        private readonly Dictionary<int, GameObject> _medkitViews = new();

        private readonly Dictionary<int, GameObject> _syringeViews = new();

        private void UpdateSyringeViews()
        {
            var alive = new HashSet<int>();
            for (int i = 0; i < Syringes.Count; i++)
            {
                var m = Syringes[i];
                if (m.Taken) continue;
                alive.Add(m.Id);
                if (!_syringeViews.TryGetValue(m.Id, out var go) || go == null)
                    _syringeViews[m.Id] = SyringeView.Create(m.Position);
            }
            var remove = new List<int>();
            foreach (var kv in _syringeViews)
                if (!alive.Contains(kv.Key)) { if (kv.Value != null) Destroy(kv.Value); remove.Add(kv.Key); }
            foreach (int id in remove) _syringeViews.Remove(id);
        }

        private void UpdateMedkitViews()
        {
            var alive = new HashSet<int>();
            for (int i = 0; i < Medkits.Count; i++)
            {
                var m = Medkits[i];
                if (m.Taken) continue;
                alive.Add(m.Id);
                if (!_medkitViews.TryGetValue(m.Id, out var go) || go == null)
                    _medkitViews[m.Id] = MedkitView.Create(m.Position);
            }
            var remove = new List<int>();
            foreach (var kv in _medkitViews)
                if (!alive.Contains(kv.Key)) { if (kv.Value != null) Destroy(kv.Value); remove.Add(kv.Key); }
            foreach (int id in remove) _medkitViews.Remove(id);
        }

        private void Update()
        {
            UpdateMedkitViews();
            UpdateSyringeViews();
            UpdateLobbyFun();
            UpdateRcCars();
            UpdateRods();
            UpdateFlashBox();
            UpdateToyGuns();
            BloodTracks.Ensure();
            var ph = Phase.Value;
            if (RaceMode && (ph == MatchPhase.Race || ph == MatchPhase.Ended) && SceneManager.GetActiveScene().name == NetSession.GameScene)
                RaceSystem.Ensure();
            if ((ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || (ph == MatchPhase.Ended && !RaceMode)) && SceneManager.GetActiveScene().name == NetSession.GameScene)
            {
                ChainsawShelf.Ensure(ChainsawTaken.Value);
                CarAlarms.Ensure();
                SecurityRooms.Ensure();
            }
            if (!IsServer) return;
            bool watched = SecurityRooms.Instance != null && SecurityRooms.Instance.AnyoneWatching();
            if (SecurityWatched.Value != watched) SecurityWatched.Value = watched;
            if (ElevatorSystem.Instance != null && Elevators.Count > 0) ElevatorSystem.Instance.ServerTick(this, Now);
            if (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended) UpdateRcServer();
            UpdateRadioServer();
            double now = Now;
            var s = Settings.Value;
            switch (Phase.Value)
            {
                case MatchPhase.Hiding:
                    if (now >= PhaseEnd.Value)
                    {
                        Phase.Value = MatchPhase.Seeking;
                        PhaseEnd.Value = now + s.SeekTime;
                        // Every hider gets an own, randomly offset schedule so hints never fire all at once.
                        foreach (var pl in NetPlayer.All)
                            if (pl != null && pl.IsHider)
                                pl.NextHintAt.Value = now + s.HintStart + Random.Range(0f, Mathf.Max(5f, s.HintInterval));
                        foreach (var pl in NetPlayer.All) if (pl != null && pl.IsSeeker) pl.Held.Value = HeldItem.Knife;
                    }
                    break;

                case MatchPhase.Seeking:
                    UpdateDowned(now);
                    if (CarAlarms.Instance != null) CarAlarms.Instance.ServerTick(this, now);
                    if (s.Glock && !GlockPhase.Value && PhaseEnd.Value - now <= Tuning.GlockAtRemaining) GiveGlock();
                    if (s.HintMode != 0) UpdateHints(now, s);
                    if (_finisherRunning) break; // let everybody watch the finisher to the end
                    if (CheckWin(out var w)) EndMatch(w);
                    else if (now >= PhaseEnd.Value) EndMatch(MatchWinner.Hiders);
                    break;

                case MatchPhase.Race:
                    UpdateRaceServer(now);
                    break;

                case MatchPhase.Ended:
                    if (now >= PhaseEnd.Value) BackToLobby();
                    break;
            }
        }

        private void UpdateDowned(double now)
        {
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || pl.Life.Value != LifeState.Downed) continue;
                if (now >= pl.DownedUntil.Value) Kill(pl);
            }
        }

        /// <summary>Per-hider hint timers: each fires on its own, next one after the interval +-30 % (Glock phase: ~20 s).</summary>
        private void UpdateHints(double now, NetSettings s)
        {
            bool sound = (s.HintMode & (byte)HintMode.Sound) != 0;
            bool pin = (s.HintMode & (byte)HintMode.Minimap) != 0;
            float interval = Mathf.Max(5f, CurrentHintInterval);
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || !pl.IsHider || pl.Life.Value == LifeState.Dead) continue;
                if (pl.NextHintAt.Value <= 0 || now < pl.NextHintAt.Value) continue;
                HintRpc(pl.ChestPosition, pl.NetworkObjectId, Random.Range(0, 1000), sound, pin);
                pl.NextHintAt.Value = now + interval * Random.Range(0.7f, 1.3f);
            }
        }

        public void TestGiveGlock() => GiveGlock();

        private void GiveGlock()
        {
            GlockPhase.Value = true;
            // Glock phase: everybody's next hint comes soon, still spread out.
            foreach (var pl in NetPlayer.All)
                if (pl != null && pl.IsHider)
                    pl.NextHintAt.Value = System.Math.Min(pl.NextHintAt.Value, Now + Random.Range(3f, Tuning.GlockHintInterval));
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || !pl.IsSeeker) continue;
                pl.HasGlock.Value = true;
                pl.Ammo.Value = Tuning.MagSize;
                pl.SpareMags.Value = Tuning.Mags - 1;
                pl.Held.Value = HeldItem.Glock;
                pl.SfxRpc(GameSfx.GlockEquip, 1f);
            }
            GlockGivenRpc();
        }

        private bool CheckWin(out MatchWinner winner)
        {
            winner = MatchWinner.None;
            bool anySeeker = false, anyHider = false, anyAliveHider = false;
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null) continue;
                if (pl.IsSeeker) anySeeker = true;
                if (pl.IsHider)
                {
                    anyHider = true;
                    if (pl.Life.Value == LifeState.Alive) anyAliveHider = true;
                }
            }
            if (!anySeeker) { winner = MatchWinner.Hiders; return true; }
            if (!anyHider || !anyAliveHider) { winner = MatchWinner.Seeker; return true; }
            return false;
        }

        private void EndMatch(MatchWinner w)
        {
            if (Phase.Value == MatchPhase.Ended) return;
            Winner.Value = w;
            Phase.Value = MatchPhase.Ended;
            PhaseEnd.Value = Now + Tuning.EndScreenTime;
            MatchEndedRpc(w);
        }

        private void BackToLobby()
        {
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
            NetworkManager.SceneManager.LoadScene(NetSession.LobbyScene, LoadSceneMode.Single);
        }

        private void OnClientLeft(ulong clientId)
        {
            if (Phase.Value == MatchPhase.Seeking || Phase.Value == MatchPhase.Hiding)
                StartCoroutine(CheckAfterLeave());
            else if (Phase.Value == MatchPhase.Loading && NetPlayer.All.Count < 2)
                BackToLobby();
        }

        private IEnumerator CheckAfterLeave()
        {
            yield return null; // let the player object despawn
            if (CheckWin(out var w)) EndMatch(w);
        }

        // ================================================================ actions (validated on the server)

        private bool Seeking => Phase.Value == MatchPhase.Seeking;

        public void ServerKnife(NetPlayer seeker, NetPlayer victim)
        {
            if (!Seeking || seeker == null || victim == null || !seeker.IsSeeker || !victim.IsAliveHider) return;
            if (seeker.Busy.Value || victim.Busy.Value) return;
            if (Now < seeker.AttackReadyAt.Value || Now < seeker.BlindUntil.Value || seeker.Gooed) return; // 8 s between attacks, no attacks while blinded / gooed
            if (Vector3.Distance(seeker.transform.position, victim.transform.position) > Tuning.KnifeServerRange) return;
            if (!victim.Limping.Value && !victim.Wounded.Value && victim.Severed.Value == 0)
            {
                // First hit only wounds: quick slash, the hider limps and can still run.
                if (_slashGrace.TryGetValue(victim.NetworkObjectId, out double g) && Now < g) return;
                _slashGrace[victim.NetworkObjectId] = Now + 1.6;
                seeker.AttackReadyAt.Value = Now + Tuning.AttackCooldown;
                seeker.Held.Value = HeldItem.Knife;
                ServerRcForcePark(victim);
                seeker.SlashRpc(victim.NetworkObjectId);
                Wound(victim);
                return;
            }
            if (_slashGrace.TryGetValue(victim.NetworkObjectId, out double g2) && Now < g2) return;
            ServerRcForcePark(victim);
            StartCoroutine(KnifeRoutine(seeker, victim));
        }

        public void ServerChainsaw(NetPlayer seeker, NetPlayer victim)
        {
            if (!Seeking || seeker == null || victim == null || !seeker.IsSeeker || !victim.IsAliveHider) return;
            if (!seeker.HasChainsaw.Value || seeker.Held.Value != HeldItem.Chainsaw) return;
            if (seeker.Busy.Value || victim.Busy.Value) return;
            if (Now < seeker.AttackReadyAt.Value || Now < seeker.BlindUntil.Value || seeker.Gooed) return; // 8 s between attacks, no attacks while blinded / gooed
            if (Vector3.Distance(seeker.transform.position, victim.transform.position) > Tuning.ChainsawServerRange) return;
            ServerRcForcePark(victim);
            bool injured = victim.Limping.Value || victim.Wounded.Value || victim.Severed.Value != 0;
            if (!injured)
            {
                // First hit: the saw takes one leg. The hider can still hop away (slowly); the next hit kills.
                if (_slashGrace.TryGetValue(victim.NetworkObjectId, out double g) && Now < g) return;
                _slashGrace[victim.NetworkObjectId] = Now + 1.6;
                seeker.AttackReadyAt.Value = Now + Tuning.AttackCooldown;
                int limb = Random.value < 0.5f ? Limb.LegR : Limb.LegL;
                seeker.SawSlashRpc(victim.NetworkObjectId, (byte)limb);
                StartCoroutine(SeverLegAfterHit(victim, limb));
                return;
            }
            if (_slashGrace.TryGetValue(victim.NetworkObjectId, out double g2) && Now < g2) return;
            StartCoroutine(KnifeRoutine(seeker, victim, true));
        }

        private IEnumerator SeverLegAfterHit(NetPlayer victim, int limb)
        {
            yield return new WaitForSeconds(BlockyCharacterActions.SawSlashHitTime);
            if (victim == null || !victim.IsSpawned || !victim.IsAliveHider) yield break;
            victim.Severed.Value = (byte)(victim.Severed.Value | limb);
            victim.ToastRpc("Piła odcięła ci nogę! Skaczesz na jednej nodze. Kolejny atak piłą = śmierć.", 6f);
        }

        /// <summary>Hider throws a handful of dust: a seeker standing close in front is blinded for a few seconds.</summary>
        public void ServerDust(NetPlayer hider)
        {
            if (!Seeking || hider == null || !hider.IsAliveHider || hider.Busy.Value) return;
            if (Now < hider.DustReadyAt.Value) return;
            hider.DustReadyAt.Value = Now + Tuning.DustCooldown;
            NetPlayer target = null;
            float best = Tuning.DustRange;
            foreach (var s in NetPlayer.All)
            {
                if (s == null || !s.IsSeeker || s.Life.Value != LifeState.Alive || s.Busy.Value) continue;
                Vector3 d = s.transform.position - hider.transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > best) continue;
                if (dist > 0.4f && Vector3.Angle(hider.transform.forward, d) > 75f) continue;
                best = dist;
                target = s;
            }
            hider.DustRpc(target != null ? target.NetworkObjectId : ulong.MaxValue);
            if (target != null) StartCoroutine(BlindAfterDust(target));
        }

        private IEnumerator BlindAfterDust(NetPlayer seeker)
        {
            yield return new WaitForSeconds(BlockyCharacterActions.DustReleaseTime + 0.12f);
            if (seeker == null || !seeker.IsSpawned) yield break;
            seeker.BlindUntil.Value = Now + Tuning.DustBlindTime;
            seeker.ToastRpc("Kurz w oczach! Nic nie widzisz.", 3f);
        }

        /// <summary>A player pressed a level button in a security room.</summary>
        public void ServerSecurityFloor(NetPlayer pl, byte floor)
        {
            if (pl == null || floor > 2 || SecurityRooms.Instance == null) return;
            if (!SecurityRooms.Instance.IsNearButton(pl.transform.position, floor)) return;
            SecurityFloor.Value = floor;
        }

        public void ServerPickupChainsaw(NetPlayer pl)
        {
            if (!Seeking || pl == null || !pl.IsSeeker || pl.Busy.Value || ChainsawTaken.Value) return;
            if (Vector3.Distance(pl.transform.position, ChainsawShelf.Position) > 3.2f) return;
            ChainsawTaken.Value = true;
            pl.HasChainsaw.Value = true;
            pl.Held.Value = HeldItem.Chainsaw;
            pl.SfxRpc(GameSfx.ChainsawStart, 1f);
            pl.ToastRpc("Masz piłę! F (przytrzymaj) - odpal i wejdź w chowającego. Q - zmiana broni.", 6f);
        }

        /// <summary>Seeker weapon switch (Q cycles knife -> chainsaw -> Glock).</summary>
        public void ServerEquip(NetPlayer pl, HeldItem item)
        {
            if (pl == null || !pl.IsSeeker || pl.Busy.Value || _reloading.Contains(pl.OwnerClientId)) return;
            if (item == HeldItem.Glock && !pl.HasGlock.Value) return;
            if (item == HeldItem.Chainsaw && !pl.HasChainsaw.Value) return;
            if (item != HeldItem.Knife && item != HeldItem.Glock && item != HeldItem.Chainsaw) return;
            if (pl.Held.Value == item) return;
            pl.Held.Value = item;
            pl.SfxRpc(item == HeldItem.Glock ? GameSfx.GlockEquip : item == HeldItem.Chainsaw ? GameSfx.ChainsawStart : GameSfx.KnifeDraw, 0.8f);
        }

        private bool IsLastHider(NetPlayer victim)
        {
            foreach (var pl in NetPlayer.All)
                if (pl != null && pl != victim && pl.IsAliveHider) return false;
            return true;
        }

        /// <summary>Test hook (-testkills): plays one forced kill variant between two players.</summary>
        public void TestKillMove(NetPlayer seeker, NetPlayer victim, KillMove move)
        {
            if (KillMoves.IsSaw(move)) { seeker.HasChainsaw.Value = true; seeker.Held.Value = HeldItem.Chainsaw; }
            StartCoroutine(KnifeRoutine(seeker, victim, KillMoves.IsSaw(move), move == KillMove.GlockFinisher, move));
        }

        private IEnumerator KnifeRoutine(NetPlayer seeker, NetPlayer victim, bool chainsaw = false, bool glock = false, KillMove? forced = null)
        {
            seeker.Busy.Value = true;
            victim.Busy.Value = true;
            HeldItem prevHeld = seeker.Held.Value;
            if (!chainsaw && !glock) seeker.Held.Value = HeldItem.Knife;

            // Face each other: the seeker steps in front of the victim.
            Vector3 vp = victim.transform.position;
            Vector3 dir = seeker.transform.position - vp;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -victim.transform.forward;
            dir.Normalize();
            Vector3 seekerPos = vp + dir * (glock ? 2.3f : Tuning.KnifeStandOff);
            float seekerYaw = Quaternion.LookRotation(-dir).eulerAngles.y;
            float victimYaw = Quaternion.LookRotation(dir).eulerAngles.y;
            seeker.TeleportRpc(seekerPos, seekerYaw);
            victim.TeleportRpc(vp, victimYaw);
            yield return new WaitForSeconds(0.1f);

            // Last hider standing gets the long finisher (everybody watches); otherwise a random variant.
            bool last = true;
            foreach (var pl in NetPlayer.All)
                if (pl != null && pl != victim && pl.IsAliveHider) { last = false; break; }
            KillMove move = forced ?? (glock ? KillMove.GlockFinisher
                : chainsaw ? (last ? KillMove.SawFinisher : PickKillMove(true)) : last ? KillMove.Finisher : PickKillMove(false));
            if (forced.HasValue) last = KillMoves.IsFinisher(move);
            if (last) _finisherRunning = true;
            Debug.Log($"[Match] kill {seeker.Nick} -> {victim.Nick}: {move}{(last ? " (last hider, shown to everybody)" : "")}");
            seeker.KnifeSequenceRpc(victim.NetworkObjectId, (byte)move, last);

            if (KillMoves.EndsDead(move))
            {
                float dead = KillMoves.VictimDuration(move) + 0.1f;
                yield return new WaitForSeconds(dead);
                if (victim != null && victim.IsSpawned)
                {
                    Kill(victim);
                    int cut = KillMoves.SeveredTotal(move);
                    if (cut != 0) victim.Severed.Value = (byte)(victim.Severed.Value | cut);
                    victim.Busy.Value = false;
                }
                yield return new WaitForSeconds(Mathf.Max(0f, KillMoves.SequenceDuration(move) - dead));
                _finisherRunning = false;
            }
            else
            {
                yield return new WaitForSeconds(KillMoves.SequenceDuration(move));
                if (victim != null && victim.IsSpawned)
                {
                    if (victim.Wounded.Value) Kill(victim);
                    else Down(victim);
                    victim.Busy.Value = false;
                }
            }
            if (seeker != null && seeker.IsSpawned)
            {
                seeker.Busy.Value = false;
                seeker.AttackReadyAt.Value = Now + Tuning.AttackCooldown;
                seeker.Held.Value = chainsaw ? HeldItem.Chainsaw
                    : prevHeld == HeldItem.Glock && seeker.HasGlock.Value ? HeldItem.Glock : HeldItem.Knife;
                seeker.Kills.Value++;
            }
        }

        private bool _finisherRunning;
        private int _lastKillMove = -1, _lastSawMove = -1;

        /// <summary>Random normal kill variant of the knife or the chainsaw (5 each), never the same one twice in a row.</summary>
        private KillMove PickKillMove(bool saw)
        {
            var pool = saw ? KillMoves.SawPool : KillMoves.KnifePool;
            // Test switch: -killmove N forces a variant (enum value, must belong to the weapon in use).
            var args = System.Environment.GetCommandLineArgs();
            int ki = System.Array.IndexOf(args, "-killmove");
            if (ki >= 0 && ki + 1 < args.Length && int.TryParse(args[ki + 1], out int forced) && System.Array.IndexOf(pool, (KillMove)forced) >= 0)
                return (KillMove)forced;
            int last = saw ? _lastSawMove : _lastKillMove;
            int idx = Random.Range(0, pool.Length);
            if (idx == last) idx = (idx + 1 + Random.Range(0, pool.Length - 1)) % pool.Length;
            if (saw) _lastSawMove = idx; else _lastKillMove = idx;
            return pool[idx];
        }

        private readonly System.Collections.Generic.Dictionary<ulong, double> _slashGrace = new();
        private readonly System.Collections.Generic.Dictionary<ulong, double> _slashCooldown = new();

        /// <summary>First hit: limping, hand on the belly. The next one downs.</summary>
        private void Wound(NetPlayer pl)
        {
            ServerRcForcePark(pl); // hit while steering: the transmitter drops
            pl.Limping.Value = true;
            pl.ToastRpc("Zostałeś zraniony! Utykasz. Kolejny atak = wykrwawianie. Apteczka opatrzy ranę.", 5f);
        }

        private void Down(NetPlayer pl)
        {
            pl.Limping.Value = false;
            pl.Life.Value = LifeState.Downed;
            pl.DownedUntil.Value = Now + BleedOut;
            HelpNeededRpc(pl.NetworkObjectId);
            pl.Held.Value = HeldItem.None;
            pl.ToastRpc("Wykrwawiasz się. Inny chowający może cię uratować (H).", 5f);
        }

        private void Kill(NetPlayer pl)
        {
            pl.Life.Value = LifeState.Dead;
            pl.Held.Value = HeldItem.None;
            pl.Wounded.Value = false;
            pl.Limping.Value = false;
            pl.ToastRpc("Nie żyjesz. Oglądasz grę (LPM / PPM - zmiana gracza).", 5f);
        }

        public void ServerShoot(NetPlayer shooter, Vector3 origin, Vector3 dir, NetPlayer victim, Vector3 hitPoint, sbyte rcSlot)
        {
            if (!Seeking || shooter == null || !shooter.IsSeeker || !shooter.HasGlock.Value || shooter.Busy.Value) return;
            if (shooter.Held.Value != HeldItem.Glock || _reloading.Contains(shooter.OwnerClientId)) return;
            double now = Now;
            if (_lastShot.TryGetValue(shooter.OwnerClientId, out double last) && now - last < Tuning.FireInterval * 0.85f) return;
            if (shooter.Ammo.Value == 0)
            {
                shooter.DryFireRpc();
                return;
            }
            _lastShot[shooter.OwnerClientId] = now;
            shooter.Ammo.Value--;

            bool valid = victim != null && victim.IsHider && victim.Life.Value != LifeState.Dead && !victim.Busy.Value &&
                         Vector3.Distance(origin, victim.ChestPosition) <= Tuning.GlockRange + 3f;
            // Last hider standing and this shot would finish them (death or downed): the Glock finisher cutscene.
            if (valid && victim.Life.Value == LifeState.Alive && (victim.Limping.Value || victim.Wounded.Value) && IsLastHider(victim) && !_finisherRunning)
            {
                shooter.ShotFxRpc(hitPoint, victim.NetworkObjectId);
                ServerRcForcePark(victim);
                StartCoroutine(KnifeRoutine(shooter, victim, false, true));
                return;
            }
            if (!valid && rcSlot >= 0 && ServerShootRc(shooter, origin, rcSlot, hitPoint))
            {
                shooter.ShotFxRpc(hitPoint, ulong.MaxValue, rcSlot);
                return;
            }
            shooter.ShotFxRpc(hitPoint, valid ? victim.NetworkObjectId : ulong.MaxValue);
            if (!valid) return;
            if (victim.Life.Value == LifeState.Downed || victim.Wounded.Value) Kill(victim);
            else if (victim.Limping.Value) Down(victim);
            else Wound(victim);
            if (victim.Life.Value == LifeState.Dead) shooter.Kills.Value++;
        }

        public void ServerReload(NetPlayer pl)
        {
            if (pl == null || !pl.HasGlock.Value || pl.SpareMags.Value == 0 || pl.Ammo.Value >= Tuning.MagSize) return;
            if (pl.Held.Value != HeldItem.Glock || _reloading.Contains(pl.OwnerClientId)) return;
            StartCoroutine(ReloadRoutine(pl));
        }

        private IEnumerator ReloadRoutine(NetPlayer pl)
        {
            _reloading.Add(pl.OwnerClientId);
            pl.ReloadFxRpc();
            yield return new WaitForSeconds(Tuning.ReloadTime);
            if (pl != null)
            {
                _reloading.Remove(pl.OwnerClientId);
                if (pl.SpareMags.Value > 0)
                {
                    pl.SpareMags.Value--;
                    pl.Ammo.Value = Tuning.MagSize; // a partly used magazine is dropped
                }
            }
        }

        public bool IsReloading(NetPlayer pl) => pl != null && _reloading.Contains(pl.OwnerClientId);

        public void ServerHolster(NetPlayer pl, bool glockOut)
        {
            if (pl == null || !pl.IsSeeker || pl.Busy.Value || _reloading.Contains(pl.OwnerClientId)) return;
            if (glockOut && !pl.HasGlock.Value) return;
            pl.Held.Value = glockOut ? HeldItem.Glock : HeldItem.Knife;
            pl.SfxRpc(glockOut ? GameSfx.GlockEquip : GameSfx.KnifeDraw, 0.7f);
        }

        public void ServerRescue(NetPlayer helper, NetPlayer target)
        {
            if (helper == null || target == null || helper == target) return;
            if (!helper.IsAliveHider || target.Life.Value != LifeState.Downed || !target.IsHider) return;
            if (Vector3.Distance(helper.transform.position, target.transform.position) > Tuning.RescueRange + 1f) return;
            target.Busy.Value = false;
            target.Limping.Value = false;
            target.Held.Value = HeldItem.None;
            target.Life.Value = LifeState.Alive;
            target.Wounded.Value = true;
            target.SfxRpc(GameSfx.ReviveDone, 1f);
            target.ToastRpc($"{helper.Nick} cię uratował. Kolejne złapanie = śmierć, chyba że użyjesz apteczki.", 5f);
        }

        /// <summary>Downed hider with a medkit gets up alone (hold G). Stays wounded: the next catch kills.</summary>
        public void ServerSelfRevive(NetPlayer pl)
        {
            if (pl == null || !pl.IsHider || pl.Life.Value != LifeState.Downed || pl.MedkitCount.Value == 0) return;
            pl.MedkitCount.Value--;
            pl.Life.Value = LifeState.Alive;
            pl.Wounded.Value = true;
            pl.DownedUntil.Value = 0;
            pl.SfxRpc(GameSfx.ReviveDone, 1f);
            pl.ToastRpc("Opatrzyłeś się i wstałeś. Jesteś ranny - kolejne złapanie = śmierć.", 5f);
        }

        public void ServerPickupSyringe(NetPlayer pl, int id)
        {
            if (pl == null || !pl.IsAliveHider) return;
            if (Phase.Value != MatchPhase.Seeking && Phase.Value != MatchPhase.Hiding) return;
            if (pl.SyringeCount.Value >= Tuning.MaxSyringes)
            {
                pl.ToastRpc($"Masz już {Tuning.MaxSyringes} strzykawki.", 2f);
                return;
            }
            for (int i = 0; i < Syringes.Count; i++)
            {
                var m = Syringes[i];
                if (m.Id != id || m.Taken) continue;
                if (Vector3.Distance(pl.transform.position, m.Position) > Tuning.PickupRange + 1.5f) return;
                m.Taken = true;
                Syringes[i] = m;
                pl.SyringeCount.Value++;
                pl.SfxRpc(GameSfx.MedkitPickup, 0.8f);
                pl.ToastRpc("Masz adrenalinę: przytrzymaj H przy martwym chowającym, żeby go wskrzesić.", 4f);
                return;
            }
        }

        /// <summary>Adrenaline: a dead hider comes back (wounded); the seeker sees them blink blue on the minimap.</summary>
        public void ServerResurrect(NetPlayer helper, NetPlayer target)
        {
            if (!Seeking || helper == null || target == null || helper == target) return;
            if (!helper.IsAliveHider || helper.SyringeCount.Value == 0) return;
            if (!target.IsHider || target.Life.Value != LifeState.Dead) return;
            if (Vector3.Distance(helper.transform.position, target.transform.position) > Tuning.ResurrectRange + 1f) return;
            helper.SyringeCount.Value--;
            target.Life.Value = LifeState.Alive;
            target.Wounded.Value = true;
            target.DownedUntil.Value = 0;
            target.Resurrected.Value = true;
            target.SfxRpc(GameSfx.ReviveDone, 1f);
            target.ToastRpc($"{helper.Nick} wskrzesił cię adrenaliną! Szukający co chwilę widzi cię na mapie.", 6f);
            helper.ToastRpc($"Wskrzesiłeś {target.Nick}.", 3f);
        }

        public void ServerHeal(NetPlayer helper, NetPlayer target)
        {
            if (helper == null || target == null || helper.MedkitCount.Value == 0 || !helper.IsAliveHider) return;
            if (!target.IsAliveHider || !(target.Wounded.Value || target.Limping.Value)) return;
            if (helper != target && Vector3.Distance(helper.transform.position, target.transform.position) > Tuning.RescueRange + 1f) return;
            helper.MedkitCount.Value--;
            target.Wounded.Value = false;
            target.Limping.Value = false;
            target.SfxRpc(GameSfx.MedkitUse, 1f);
            if (helper != target) target.ToastRpc($"{helper.Nick} opatrzył twoje rany.", 4f);
            else helper.ToastRpc("Rany opatrzone.", 3f);
        }

        public void ServerPickup(NetPlayer pl, int medkitId)
        {
            if (pl == null || !pl.IsAliveHider) return;
            if (pl.MedkitCount.Value >= Tuning.MaxMedkits)
            {
                pl.ToastRpc($"Masz już {Tuning.MaxMedkits} apteczki.", 2f);
                return;
            }
            if (Phase.Value != MatchPhase.Seeking && Phase.Value != MatchPhase.Hiding) return;
            for (int i = 0; i < Medkits.Count; i++)
            {
                var m = Medkits[i];
                if (m.Id != medkitId || m.Taken) continue;
                if (Vector3.Distance(pl.transform.position, m.Position) > Tuning.PickupRange + 1.5f) return;
                m.Taken = true;
                Medkits[i] = m;
                pl.MedkitCount.Value++;
                pl.SfxRpc(GameSfx.MedkitPickup, 1f);
                pl.ToastRpc($"Apteczki: {pl.MedkitCount.Value}. G - ulecz siebie, H - ulecz rannego.", 4f);
                return;
            }
        }
    }

    /// <summary>Spawn point lookup in the current scene.</summary>
    public static class SpawnPoints
    {
        public static List<Transform> Lobby()
        {
            var list = new List<Transform>();
            foreach (var go in GameObject.FindGameObjectsWithTag(GameLayers.SpawnPointTag)) list.Add(go.transform);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        public static List<Transform> Hider()
        {
            var list = new List<Transform>();
            foreach (var go in GameObject.FindGameObjectsWithTag(GameLayers.SpawnPointTag)) list.Add(go.transform);
            return list;
        }

        public static Transform Seeker()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                if (t.name == "SeekerSpawn") return t;
            var h = Hider();
            return h.Count > 0 ? h[0] : null;
        }
    }
}


