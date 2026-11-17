using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
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
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        public readonly NetworkVariable<MatchPhase> Phase = new(MatchPhase.Lobby);
        public readonly NetworkVariable<double> PhaseEnd = new();
        public readonly NetworkVariable<NetSettings> Settings = new(NetSettings.Default);
        public readonly NetworkVariable<MatchWinner> Winner = new();
        public readonly NetworkVariable<bool> GlockPhase = new();
        public NetworkList<MedkitState> Medkits;

        /// <summary>Raised on every client for hint pings (seeker minimap). Args: world position.</summary>
        public static event System.Action<Vector3> HintPinged;

        private double _nextHint;
        private ulong _lastSeekerClient = ulong.MaxValue;
        private readonly Dictionary<ulong, double> _lastShot = new();
        private readonly HashSet<ulong> _reloading = new();

        public double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;
        public double Remaining => PhaseEnd.Value - Now;

        private void Awake()
        {
            Medkits = new NetworkList<MedkitState>();
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

        [Rpc(SendTo.Everyone)]
        private void GlockGivenRpc()
        {
            GameAudio.SetBed(AmbientId.TensionMusic, 3f);
            var local = NetPlayer.Local;
            if (local != null && local.IsSeeker)
                GameHud.Banner("GLOCK", "Ostatnie 2 minuty. Q - wyciągnij / schowaj, LPM - strzał, R - przeładuj.", 5f, Color.yellow);
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
            Settings.Value = s;
        }

        [Rpc(SendTo.Server)]
        public void RequestStartRpc(RpcParams p = default)
        {
            if (Phase.Value != MatchPhase.Lobby) return;
            var starter = NetPlayer.FindByClient(p.Receive.SenderClientId);
            if (NetPlayer.All.Count < GameSettings.MinPlayers)
            {
                starter?.ToastRpc("Potrzeba co najmniej 2 graczy.", 3f);
                return;
            }
            StartMatch();
        }

        [Rpc(SendTo.Everyone)]
        public void ButtonPressedRpc(Vector3 pos)
        {
            GameAudio.PlayAt(GameSfx.LobbyButtonPress, pos);
            if (LobbyButton.Instance != null) LobbyButton.Instance.Animate();
        }

        private void StartMatch()
        {
            var players = new List<NetPlayer>(NetPlayer.All);
            players.RemoveAll(x => x == null);
            // Random seeker, avoiding the previous one when possible.
            var candidates = players.FindAll(x => x.OwnerClientId != _lastSeekerClient);
            if (candidates.Count == 0) candidates = players;
            var seeker = candidates[Random.Range(0, candidates.Count)];
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
            Phase.Value = MatchPhase.Loading;
            NetworkManager.SceneManager.LoadScene(NetSession.GameScene, LoadSceneMode.Single);
        }

        private void ResetPlayer(NetPlayer pl)
        {
            pl.Life.Value = LifeState.Alive;
            pl.Wounded.Value = false;
            pl.MedkitCount.Value = 0;
            pl.Held.Value = HeldItem.None;
            pl.HasGlock.Value = false;
            pl.Ammo.Value = 0;
            pl.SpareMags.Value = 0;
            pl.Busy.Value = false;
            pl.DownedUntil.Value = 0;
        }

        private void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == NetSession.GameScene && Phase.Value == MatchPhase.Loading)
            {
                PlacePlayersInMap();
                SpawnMedkits();
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
            if (!IsServer) return;
            double now = Now;
            var s = Settings.Value;
            switch (Phase.Value)
            {
                case MatchPhase.Hiding:
                    if (now >= PhaseEnd.Value)
                    {
                        Phase.Value = MatchPhase.Seeking;
                        PhaseEnd.Value = now + s.SeekTime;
                        _nextHint = now + s.HintStart;
                        foreach (var pl in NetPlayer.All) if (pl != null && pl.IsSeeker) pl.Held.Value = HeldItem.Knife;
                    }
                    break;

                case MatchPhase.Seeking:
                    UpdateDowned(now);
                    if (s.Glock && !GlockPhase.Value && PhaseEnd.Value - now <= Tuning.GlockAtRemaining) GiveGlock();
                    if (s.HintMode != 0 && now >= _nextHint)
                    {
                        // Glock phase (last 2 minutes): hints come much more often.
                        float interval = GlockPhase.Value ? Mathf.Min(s.HintInterval, Tuning.GlockHintInterval) : s.HintInterval;
                        _nextHint = now + Mathf.Max(5f, interval);
                        SendHints(s);
                    }
                    if (CheckWin(out var w)) EndMatch(w);
                    else if (now >= PhaseEnd.Value) EndMatch(MatchWinner.Hiders);
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

        private void SendHints(NetSettings s)
        {
            bool sound = (s.HintMode & (byte)HintMode.Sound) != 0;
            bool pin = (s.HintMode & (byte)HintMode.Minimap) != 0;
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || !pl.IsHider || pl.Life.Value == LifeState.Dead) continue;
                HintRpc(pl.ChestPosition, pl.NetworkObjectId, Random.Range(0, 1000), sound, pin);
            }
        }

        public void TestGiveGlock() => GiveGlock();

        private void GiveGlock()
        {
            GlockPhase.Value = true;
            _nextHint = System.Math.Min(_nextHint, Now + 5);
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
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null) continue;
                pl.Role.Value = PlayerRole.None;
                ResetPlayer(pl);
            }
            Medkits.Clear();
            GlockPhase.Value = false;
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
            if (Vector3.Distance(seeker.transform.position, victim.transform.position) > Tuning.KnifeServerRange) return;
            StartCoroutine(KnifeRoutine(seeker, victim));
        }

        private IEnumerator KnifeRoutine(NetPlayer seeker, NetPlayer victim)
        {
            seeker.Busy.Value = true;
            victim.Busy.Value = true;
            HeldItem prevHeld = seeker.Held.Value;
            seeker.Held.Value = HeldItem.Knife;

            // Face each other: the seeker steps in front of the victim.
            Vector3 vp = victim.transform.position;
            Vector3 dir = seeker.transform.position - vp;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -victim.transform.forward;
            dir.Normalize();
            Vector3 seekerPos = vp + dir * Tuning.KnifeStandOff;
            float seekerYaw = Quaternion.LookRotation(-dir).eulerAngles.y;
            float victimYaw = Quaternion.LookRotation(dir).eulerAngles.y;
            seeker.TeleportRpc(seekerPos, seekerYaw);
            victim.TeleportRpc(vp, victimYaw);
            yield return new WaitForSeconds(0.1f);

            seeker.KnifeSequenceRpc(victim.NetworkObjectId);
            yield return new WaitForSeconds(Tuning.KnifeSequenceDuration);

            if (victim != null && victim.IsSpawned)
            {
                if (victim.Wounded.Value) Kill(victim);
                else Down(victim);
                victim.Busy.Value = false;
            }
            if (seeker != null && seeker.IsSpawned)
            {
                seeker.Busy.Value = false;
                seeker.Held.Value = prevHeld == HeldItem.Glock && seeker.HasGlock.Value ? HeldItem.Glock : HeldItem.Knife;
                seeker.Kills.Value++;
            }
        }

        private void Down(NetPlayer pl)
        {
            pl.Life.Value = LifeState.Downed;
            pl.DownedUntil.Value = Now + Tuning.BleedOutTime;
            pl.Held.Value = HeldItem.None;
            pl.ToastRpc("Wykrwawiasz się. Inny chowający może cię uratować (H).", 5f);
        }

        private void Kill(NetPlayer pl)
        {
            pl.Life.Value = LifeState.Dead;
            pl.Held.Value = HeldItem.None;
            pl.Wounded.Value = false;
            pl.ToastRpc("Nie żyjesz. Oglądasz grę (LPM / PPM - zmiana gracza).", 5f);
        }

        public void ServerShoot(NetPlayer shooter, Vector3 origin, Vector3 dir, NetPlayer victim, Vector3 hitPoint)
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
            shooter.ShotFxRpc(hitPoint, valid ? victim.NetworkObjectId : ulong.MaxValue);
            if (!valid) return;
            if (victim.Life.Value == LifeState.Downed || victim.Wounded.Value) Kill(victim);
            else Down(victim);
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
            target.Life.Value = LifeState.Alive;
            target.Wounded.Value = true;
            target.SfxRpc(GameSfx.ReviveDone, 1f);
            target.ToastRpc($"{helper.Nick} cię uratował. Kolejne złapanie = śmierć, chyba że użyjesz apteczki.", 5f);
        }

        public void ServerHeal(NetPlayer helper, NetPlayer target)
        {
            if (helper == null || target == null || helper.MedkitCount.Value == 0 || !helper.IsAliveHider) return;
            if (!target.IsAliveHider || !target.Wounded.Value) return;
            if (helper != target && Vector3.Distance(helper.transform.position, target.transform.position) > Tuning.RescueRange + 1f) return;
            helper.MedkitCount.Value--;
            target.Wounded.Value = false;
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
