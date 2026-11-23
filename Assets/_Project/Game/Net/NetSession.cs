using System;
using System.Collections.Generic;
using System.Text;
using HideAndSeek.Characters;
using HideAndSeek.Game.UI;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lives next to the NetworkManager in the Menu scene (persistent). Starts host / client either over Steam
    /// (Facepunch transport, P2P through the Steam relay, Steam lobby + friend invites) or over plain UDP
    /// (direct IP, LAN and virtual LANs - the fallback when Steam is not available), registers prefabs, approves
    /// connections and drives scene changes (Menu -> Lobby -> Game -> Lobby ...).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public partial class NetSession : MonoBehaviour
    {
        public const ushort GamePort = 7777;
        public const string MenuScene = "Menu";
        public const string LobbyScene = "Lobby";
        public const string GameScene = "Game_ParkingGarage";

        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject matchManagerPrefab;

        public static NetSession Instance { get; private set; }

        /// <summary>Nick / skin sent by each client in the connection payload (server side).</summary>
        private readonly Dictionary<ulong, (string nick, int skin, string mc)> _pending = new();

        /// <summary>Message shown in the main menu after returning (e.g. why we were disconnected).</summary>
        public static string LastMessage;

        public NetworkManager Manager { get; private set; }

        private bool _quitting;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Manager = GetComponent<NetworkManager>();
            _steamTransport = GetComponent<FacepunchTransport>();
            if (_steamTransport == null) _steamTransport = gameObject.AddComponent<FacepunchTransport>();
            SteamService.Init();
            DiscordPresence.Start();
            _stateSince = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SteamService.JoinRequested += OnSteamJoinRequested;
            LocalProfile.ApplyAudio();
            Application.runInBackground = true;
        }

        private void Start()
        {
            Manager.NetworkConfig.PlayerPrefab = playerPrefab;
            TryAddPrefab(playerPrefab);
            TryAddPrefab(matchManagerPrefab);
            Manager.NetworkConfig.ConnectionApproval = true;
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientDisconnectCallback += OnClientDisconnect;
            Manager.OnClientConnectedCallback += id =>
            {
                // Late joiners get the skin files of everyone already here.
                if (!Manager.IsServer || id == NetworkManager.ServerClientId) return;
                foreach (var p in NetPlayer.All) if (p != null && p.OwnerClientId != id) p.SendSkinTo(id);
            };
            Manager.OnServerStopped += _ => LanDiscovery.StopResponder();
            GameHud.EnsureExists();
            Voice.VoiceChat.EnsureExists();
            HandleCommandLine();
        }

        /// <summary>Test helpers: -nick X, -host, -join IP, -autostart (host starts the round once 2 players are in).</summary>
        private void HandleCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Arg(string name)
            {
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
                return null;
            }
            bool Has(string name) => Array.IndexOf(args, name) >= 0;

            if (Arg("-port") is string ps && ushort.TryParse(ps, out ushort port)) _port = port;
            _noDiscovery = Has("-nodiscovery");
            string nick = Arg("-nick");
            if (nick != null) LocalProfile.SessionNick = LocalProfile.Sanitize(nick);
            if (Arg("-mcnick") is string mcn) LocalProfile.SessionMcNick = mcn;
            Debug.Log($"[NetSession] args nick={nick} profile={LocalProfile.Nick}");
            ulong inviteLobby = SteamService.ParseLobbyArg(string.Join(" ", args));
            if (Has("-host")) Host();
            else if (Has("-steamhost")) HostSteam();
            else if (inviteLobby != 0) JoinSteamLobby(inviteLobby);
            else if (Arg("-join") is string ip) Join(ip, _port);
            if (Has("-autostart")) StartCoroutine(AutoStart());
            if (Arg("-hostdelay") is string hd && float.TryParse(hd, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hdel))
                StartCoroutine(HostLater(hdel));
            if (Has("-steamlist")) SteamListTest();
            if (Has("-lobbytour")) StartCoroutine(Tour(MatchPhase.Lobby, System.Array.ConvertAll(LobbyShotsProbe.Views,
                v => (new Vector3(v.eye.x, 0f, v.eye.z), v.at)))); // views: Game/Lobby/LobbyShotsProbe.cs
            if (Has("-elevgap")) StartCoroutine(ElevatorGapTest());
            if (Has("-testalarm")) StartCoroutine(AlarmTest());
            if (Has("-testrevive")) StartCoroutine(ReviveTest());
            if (Has("-testinject")) StartCoroutine(InjectTest());
            if (Has("-testlobbyfun")) StartCoroutine(LobbyFunTest());
            if (Has("-testwound")) StartCoroutine(WoundTest());
            if (Has("-testsawleg")) StartCoroutine(SawLegTest());
            if (Has("-testsecurity")) StartCoroutine(SecurityTest());
            if (Has("-testspectate")) StartCoroutine(SpectateTest());
            if (Has("-testrc")) StartCoroutine(RcTest());
            if (Has("-testrcwatch")) StartCoroutine(RcWatch());
            if (Has("-testbricks")) StartCoroutine(BricksTest());
            if (Has("-testexit")) StartCoroutine(ExitTest());
            if (Has("-testpouf")) StartCoroutine(PoufTest());
            if (Has("-testradio")) StartCoroutine(RadioTest());
            if (Has("-seekertest")) StartCoroutine(SeekerTest());
            if (Has("-elevtour")) StartCoroutine(Tour(MatchPhase.Seeking, new[]
            {
                (new Vector3(-1.2f, 0f, 3.0f), new Vector3(-2.6f, 1.2f, 1.45f)),
                (new Vector3(-4.0f, 0f, 2.9f), new Vector3(-2.6f, 1.2f, 1.45f)),
                (new Vector3(-0.9f, 0f, 2.0f), new Vector3(-1.6f, 1.4f, 1.45f)),
                (new Vector3(4.2f, 0f, -2.8f), new Vector3(2.6f, 1.2f, -1.45f)),
            }));
            if (Arg("-shots") is string dir) StartCoroutine(Shots(dir));
            StartUpdateTests(Has);
            StartAnimTests(Has);
            StartCarryTests(Has);
            StartCombatTests(Has);
            StartItemTests(Has);
            StartFlashToyTests(Has);
            StartPhoneTests(Has);
            StartRcRemoteTests(Has);
            StartPaTests(Has);
            StartExtTests(Has);
            StartEmoteTests(Has);
            StartSeekerTests(Has);
            StartLobbyReturnTests(Arg);
            StartMapVizTests();
            StartGfxTests();
            if (Arg("-testheroes") is string heroDir) StartCoroutine(HeroCarTour(heroDir));
        }

        private System.Collections.IEnumerator Shots(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            int count = ShotCount();
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(i == 0 ? 2.5f : ShotEvery());
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{LocalProfile.Nick}_{i:00}.png"));
            }
        }

        private System.Collections.IEnumerator AutoStart()
        {
            int need = int.TryParse(ArgOr("-autostartplayers", "2"), out int np) ? np : 2; // -autostartplayers N: wait for N
            while (MatchManager.Instance == null || NetPlayer.All.Count < need) yield return new WaitForSeconds(0.5f);
            yield return new WaitForSeconds(3f);
            Debug.Log("[NetSession] autostart");
            foreach (var p in NetPlayer.All) if (p != null) p.Ready.Value = true; // test helper: everyone ready
            string[] a = Environment.GetCommandLineArgs();
            int hi = Array.IndexOf(a, "-hidetime");
            if (hi >= 0 && hi + 1 < a.Length && float.TryParse(a[hi + 1], out float ht))
            {
                var st = MatchManager.Instance.Settings.Value;
                st.HideTime = ht;
                MatchManager.Instance.Settings.Value = st;
            }
            MatchManager.Instance.RequestStartRpc();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-testcombat") >= 0) StartCoroutine(TestCombat());
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-testsaw") >= 0) StartCoroutine(TestSaw());
        }

        /// <summary>Host-only scripted test: seeker knifes the first hider, then shoots another / the downed one.</summary>
        private System.Collections.IEnumerator TestCombat()
        {
            var mm = MatchManager.Instance;
            while (mm.Phase.Value != MatchPhase.Seeking) yield return null;
            yield return new WaitForSeconds(1f);
            NetPlayer seeker = null, h1 = null, h2 = null;
            foreach (var p in NetPlayer.All)
            {
                if (p.IsSeeker) seeker = p;
                else if (h1 == null) h1 = p;
                else h2 = p;
            }
            if (seeker == null || h1 == null) yield break;
            void Log(string s) => Debug.Log($"[Test] {s}: h1={h1.Life.Value}/w{h1.Wounded.Value} h2={(h2 != null ? h2.Life.Value + "/w" + h2.Wounded.Value : "-")} phase={mm.Phase.Value} winner={mm.Winner.Value} ammo={seeker.Ammo.Value}+{seeker.SpareMags.Value}");

            Vector3 hp = h1.transform.position;
            seeker.TeleportRpc(hp + h1.transform.forward * 2f, Quaternion.LookRotation(-h1.transform.forward).eulerAngles.y);
            if (h2 != null) h2.TeleportRpc(hp - h1.transform.right * 1.2f, Quaternion.LookRotation(h1.transform.right).eulerAngles.y);
            yield return new WaitForSeconds(1.5f);
            mm.ServerKnife(seeker, h1);
            yield return new WaitForSeconds(HideAndSeek.Characters.KillMoves.SequenceDuration(HideAndSeek.Characters.KillMove.Finisher) + 1f);
            Log("after knife");
            if (h2 != null)
            {
                mm.ServerRescue(h2, h1);
                yield return new WaitForSeconds(1.5f);
                Log("after rescue");
            }
            mm.TestGiveGlock();
            yield return new WaitForSeconds(2f);
            mm.ServerShoot(seeker, seeker.EyePosition, (h1.ChestPosition - seeker.EyePosition).normalized, h1, h1.ChestPosition, -1);
            yield return new WaitForSeconds(2f);
            Log("after shot (wounded -> dead)");
            mm.ServerReload(seeker);
            yield return new WaitForSeconds(Tuning.ReloadTime + 0.5f);
            Log("after reload");
            if (h2 != null)
            {
                seeker.TeleportRpc(h2.transform.position + h2.transform.forward * 2f, Quaternion.LookRotation(-h2.transform.forward).eulerAngles.y);
                yield return new WaitForSeconds(1.5f);
                mm.ServerKnife(seeker, h2);
                yield return new WaitForSeconds(HideAndSeek.Characters.KillMoves.SequenceDuration(HideAndSeek.Characters.KillMove.Finisher) + 1f);
                Log("after knife 2");
            }
        }
        /// <summary>Host-only scripted test: seeker walks to the generator-room shelf, takes the chainsaw, kills a hider with it.</summary>
        private System.Collections.IEnumerator TestSaw()
        {
            var mm = MatchManager.Instance;
            while (mm.Phase.Value != MatchPhase.Seeking) yield return null;
            yield return new WaitForSeconds(1f);
            NetPlayer seeker = null, h1 = null;
            foreach (var p in NetPlayer.All)
            {
                if (p.IsSeeker) seeker = p;
                else if (h1 == null) h1 = p;
            }
            if (seeker == null || h1 == null) yield break;
            // In front of the shelf, looking at it.
            seeker.TeleportRpc(ChainsawShelf.Position + new Vector3(0f, 0.05f, -1.6f), 0f);
            yield return new WaitForSeconds(2.5f);
            Debug.Log($"[Test] shelf={(ChainsawShelf.Instance != null)} taken={mm.ChainsawTaken.Value}");
            mm.ServerPickupChainsaw(seeker);
            yield return new WaitForSeconds(2.5f);
            Debug.Log($"[Test] after pickup: taken={mm.ChainsawTaken.Value} has={seeker.HasChainsaw.Value} held={seeker.Held.Value}");
            Vector3 hp = h1.transform.position;
            seeker.TeleportRpc(hp + h1.transform.forward * 1.0f, Quaternion.LookRotation(-h1.transform.forward).eulerAngles.y);
            yield return new WaitForSeconds(1.5f);
            mm.ServerChainsaw(seeker, h1);
            yield return new WaitForSeconds(HideAndSeek.Characters.KillMoves.SequenceDuration(HideAndSeek.Characters.KillMove.Chainsaw) + 1.5f);
            Debug.Log($"[Test] after chainsaw: h1={h1.Life.Value} phase={mm.Phase.Value} winner={mm.Winner.Value} held={seeker.Held.Value}");
        }

        private void TryAddPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            try { Manager.AddNetworkPrefab(prefab); }
            catch (Exception e) { Debug.Log($"[NetSession] prefab {prefab.name}: {e.Message}"); }
        }

        private float _nextLobbySync;

        private long _stateSince;
        private string _stateKey;
        private bool _discordOn = true;

        /// <summary>Discord status from the current state: menu / lobby with N people / in game (role, phase, time left).</summary>
        private void UpdateDiscord()
        {
            if (!LocalProfile.DiscordStatus)
            {
                if (_discordOn) DiscordPresence.Clear();
                _discordOn = false;
                return;
            }
            _discordOn = true;
            var a = new DiscordPresence.Activity { LargeText = "Hide and Seek - podziemny parking" };
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string key;
            if (Manager == null || !Manager.IsListening)
            {
                key = "menu";
                a.Details = "W menu";
                a.State = SteamService.Ready ? "Wybiera serwer (Steam)" : "Wybiera serwer";
            }
            else
            {
                var mm = MatchManager.Instance;
                var phase = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
                int n = NetPlayer.All.Count;
                var me = NetPlayer.Local;
                a.SmallImage = UsingSteam ? "steam" : "lan";
                a.SmallText = UsingSteam ? "Przez Steam" : "LAN / UDP";
                key = phase.ToString();
                switch (phase)
                {
                    case MatchPhase.Lobby:
                        a.Details = $"W lobby {DiscordPresence.WithPeople(n)}";
                        a.State = Manager.IsServer ? "Właściciel pokoju" : me != null && me.Ready.Value ? "Gotowy do gry" : "Czeka na start";
                        break;
                    case MatchPhase.Loading:
                        a.Details = $"W grze {DiscordPresence.WithPeople(n)}";
                        a.State = "Ładuje mapę";
                        break;
                    default:
                    {
                        a.Details = $"W grze {DiscordPresence.WithPeople(n)}";
                        string role = me == null ? "" :
                            me.IsSeeker ? "Szuka" :
                            me.Life.Value == HideAndSeek.Core.LifeState.Dead ? "Nie żyje, ogląda" :
                            me.Life.Value == HideAndSeek.Core.LifeState.Downed ? "Powalony, wykrwawia się" : "Chowa się";
                        string ph = phase == MatchPhase.Hiding ? "faza chowania" : phase == MatchPhase.Seeking ? "" : "koniec rundy"; // seeking: the role says it all
                        if (phase == MatchPhase.Ended && mm != null)
                            ph = mm.Winner.Value == MatchWinner.Seeker ? "wygrał szukający" : "wygrali chowający";
                        a.State = string.IsNullOrEmpty(role) ? (string.IsNullOrEmpty(ph) ? "Szukanie" : ph) : string.IsNullOrEmpty(ph) ? role : $"{role} ({ph})";
                        if (mm != null && (phase == MatchPhase.Hiding || phase == MatchPhase.Seeking))
                            a.End = now + (long)Math.Max(0, mm.Remaining); // Discord shows "xx:xx left"
                        break;
                    }
                }
            }
            if (key != _stateKey)
            {
                _stateKey = key;
                _stateSince = now;
            }
            if (a.End == 0) a.Start = _stateSince; // "xx:xx elapsed" in menu / lobby
            DiscordPresence.Set(a);
        }

        private void Update()
        {
            SteamService.Tick();
            UpdateDiscord();
            if (Manager != null && Manager.IsServer && UsingSteam && Time.unscaledTime >= _nextLobbySync)
            {
                _nextLobbySync = Time.unscaledTime + 1f;
                var mm2 = MatchManager.Instance;
                SteamService.UpdateLobby(mm2 == null || mm2.Phase.Value == MatchPhase.Lobby, Manager.ConnectedClientsIds.Count);
            }
            if (Manager != null && Manager.IsServer)
            {
                var mm = MatchManager.Instance;
                LanDiscovery.UpdatePayload(ServerName, Manager.ConnectedClientsIds.Count, _port,
                    mm == null || mm.Phase.Value == MatchPhase.Lobby);
            }
        }

        public string ServerName { get; private set; } = "Serwer";
        private ushort _port = GamePort;
        private bool _noDiscovery;

        private UnityTransport Transport => GetComponent<UnityTransport>();

        private byte[] Payload() => Encoding.UTF8.GetBytes($"{LocalProfile.Nick}|{LocalProfile.Skin}|{Application.version}|{LocalProfile.McNick}");

        // ---------------------------------------------------------------- start / stop

        /// <summary>Test switch -steamlist: searches Steam lobbies every 5 s and logs them.</summary>
        private async void SteamListTest()
        {
            for (int i = 0; i < 4; i++)
            {
                await System.Threading.Tasks.Task.Delay(5000);
                var list = await SteamService.FindLobbies();
                Debug.Log($"[SteamTest] ready={SteamService.Ready} lobbies={list.Count}: " + string.Join(", ", list.ConvertAll(l => $"{l.Name}({l.Members}/{l.Max},{(l.InLobby ? "lobby" : "game")},v{l.Version},friend={l.Friend})")));
            }
        }

        /// <summary>Test switches -lobbytour / -elevtour: moves the local player to look at given points (for screenshots).</summary>
        private System.Collections.IEnumerator Tour(MatchPhase phase, (Vector3 from, Vector3 at)[] stops)
        {
            while (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != phase || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(2f);
            var me = NetPlayer.Local;
            foreach (var (from, at) in stops)
            {
                Vector3 eye = from + Vector3.up * 1.62f;
                Vector3 d = at - eye;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                me.Motor.Teleport(from, yaw, pitch);
                me.Motor.SetLook(yaw, pitch);
                Debug.Log($"[Tour] {from} -> {at}");
                yield return new WaitForSeconds(2.2f);
            }
        }

        /// <summary>
        /// Test switch -elevgap: with all elevator doors closed, casts rays from the lobby towards every door frame
        /// (x across the jambs, several heights) and logs any ray that gets past the door plane into the cabin.
        /// </summary>
        private System.Collections.IEnumerator ElevatorGapTest()
        {
            while (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != MatchPhase.Seeking) yield return null;
            yield return new WaitForSeconds(1.5f);
            int rays = 0, leaks = 0;
            foreach (float ex in new[] { -2.6f, 2.6f })
            foreach (float y0 in new[] { 0f, -3.4f, -6.8f })
            foreach (float s in new[] { -1f, 1f })
            {
                for (float dx = -1.12f; dx <= 1.12f; dx += 0.01f)
                foreach (float h in new[] { 0.3f, 1.2f, 2.1f, 2.3f })
                {
                    Vector3 from = new Vector3(ex + dx, y0 + h, s * 2.6f);
                    rays++;
                    if (!Physics.Raycast(from, new Vector3(0f, 0f, -s), out var hit, 2.0f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (s * hit.point.z < 1.37f) // got past the door / jamb plane of this face
                    {
                        leaks++;
                        if (leaks <= 12) Debug.Log($"[ElevGap] leak car x={ex} level={y0} face={s} dx={dx:0.00} h={h} -> {hit.collider.name} at z={hit.point.z:0.00}");
                    }
                }
            }
            Debug.Log($"[ElevGap] rays={rays} leaks={leaks}");
        }

        /// <summary>
        /// Test switch -testalarm (host): 1) forces the alarm of the car nearest to the host (effects / screenshot),
        /// 2) "runs" the first hider along the side of another car (teleports in small steps = ~5 m/s, 0.3 m from the
        /// body) and logs whether the server's trigger logic set its alarm off.
        /// </summary>
        private System.Collections.IEnumerator AlarmTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || CarAlarms.Instance == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            NetPlayer host = NetPlayer.Local, hider = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsHider) { hider = p; break; }
            int forced = CarAlarms.Instance.ServerForceNearest(mm, host.transform.position, mm.Now);
            Vector3 car = CarAlarms.Instance.CarPosition(forced);
            Debug.Log($"[AlarmTest] forced car {forced} at {car}, cars={CarAlarms.Instance.CarCount}");
            // Look at it from 6 m away (screenshot).
            var (cf, cr, cfw, ch) = CarAlarms.Instance.CarFrame(forced);
            // In the aisle in front of the car (cars park nose / tail to the aisle), slightly to the side.
            Vector3 from = cf - cfw * (ch.z + 3.5f) + cr * 1.2f + Vector3.up * 0.05f;
            if (Physics.CheckSphere(from + Vector3.up, 0.4f)) from = cf + cfw * (ch.z + 3.5f) + cr * 1.2f + Vector3.up * 0.05f;
            Vector3 look = cf - from;
            host.Motor.Teleport(from, Quaternion.LookRotation(new Vector3(look.x, 0, look.z)).eulerAngles.y, -10f);
            host.Motor.SetLook(Quaternion.LookRotation(new Vector3(look.x, 0, look.z)).eulerAngles.y, -10f);
            yield return new WaitForSeconds(0.5f);
            var lightsGo = GameObject.Find("AlarmLights");
            Debug.Log($"[AlarmTest] host at {host.transform.position} yaw {host.transform.eulerAngles.y:0}, car floor {cf}, lights {(lightsGo != null ? lightsGo.transform.position.ToString() + " active=" + lightsGo.activeInHierarchy : "none")}");
            if (lightsGo != null)
            {
                var cam = host.Camera;
                foreach (Transform lt in lightsGo.transform)
                {
                    var r = lt.GetComponent<Renderer>();
                    var l = lt.GetComponent<Light>();
                    Vector3 vp = cam != null ? cam.WorldToViewportPoint(lt.position) : Vector3.zero;
                    Debug.Log($"[AlarmTest]  {lt.name} {lt.position} vp={vp} {(r != null ? r.sharedMaterial.name + " en=" + r.enabled : "")} {(l != null ? "light " + l.intensity + " range " + l.range : "")}");
                }
                if (cam != null) Debug.Log($"[AlarmTest]  camera {cam.transform.position} fwd {cam.transform.forward} enabled={cam.enabled} main={(Camera.main != null ? Camera.main.name : "-")}");
            }
            yield return new WaitForSeconds(4.5f);
            if (hider == null) yield break;
            // Run test: pick another car, run along its side several times.
            int target = (forced + 7) % CarAlarms.Instance.CarCount;
            var (floor, right, fwd, half) = CarAlarms.Instance.CarFrame(target);
            float yaw = Quaternion.LookRotation(fwd).eulerAngles.y;
            for (int pass = 0; pass < 6; pass++)
            {
                for (int i = 0; i < 12; i++)
                {
                    // 0.3 m beside the body (+ capsule radius), ~5 m/s along the car.
                    Vector3 p = floor + right * (half.x + 0.3f + 0.3f) + fwd * (-half.z - 0.5f + i * 0.45f) + Vector3.up * 0.05f;
                    hider.TeleportRpc(p, yaw);
                    yield return new WaitForSeconds(0.09f);
                }
                yield return new WaitForSeconds(0.3f);
            }
            Debug.Log($"[AlarmTest] run test near car {target} done");
        }

        /// <summary>-seekertest: 12 picks with the players in the lobby, logs the order (each one once per turn).</summary>
        private System.Collections.IEnumerator SeekerTest()
        {
            while (MatchManager.Instance == null || NetPlayer.All.Count < 3) yield return null;
            yield return new WaitForSeconds(1f);
            var list = new List<NetPlayer>(NetPlayer.All);
            var order = new List<string>();
            int per = int.TryParse(ArgOr("-seekers", "1"), out int sn) ? Mathf.Clamp(sn, 1, list.Count) : 1; // -seekers N: N per round
            for (int i = 0; i < 12; i++) order.Add(string.Join("+", MatchManager.Instance.PickSeekers(list, per).ConvertAll(p => p.Nick)));
            Debug.Log("[SeekerTest] order: " + string.Join(" ", order));
        }

        /// <summary>
        /// -testrevive (host = seeker, 2 hiders): hider 1 is downed with a medkit and gets up alone; then killed and
        /// brought back by hider 2 with adrenaline (seeker's minimap then pings him blue).
        /// </summary>
        private System.Collections.IEnumerator ReviveTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            NetPlayer h1 = null, h2 = null;
            foreach (var p in NetPlayer.All) if (p.IsHider) { if (h1 == null) h1 = p; else h2 = p; }
            if (h1 == null || h2 == null) { Debug.Log("[ReviveTest] need 2 hiders"); yield break; }
            Debug.Log($"[ReviveTest] syringes on map: {mm.Syringes.Count}, medkits: {mm.Medkits.Count}");
            h1.MedkitCount.Value = 1;
            mm.TestDown(h1);
            yield return new WaitForSeconds(1f);
            mm.ServerSelfRevive(h1);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[ReviveTest] self revive: life={h1.Life.Value} wounded={h1.Wounded.Value} medkits={h1.MedkitCount.Value}");
            mm.TestKill(h1);
            yield return new WaitForSeconds(1.5f);
            h2.SyringeCount.Value = 1;
            h2.TeleportRpc(h1.transform.position + h1.transform.right * 1.2f, 0f);
            yield return new WaitForSeconds(1.5f);
            mm.ServerResurrect(h2, h1);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[ReviveTest] resurrect: life={h1.Life.Value} resurrected={h1.Resurrected.Value} wounded={h1.Wounded.Value} syringes(h2)={h2.SyringeCount.Value}");
        }

        /// <summary>
        /// -testinject (host must be a hider: use -forcehider; 2 hiders): kills the other hider, kneels next to the body and
        /// plays the full adrenaline hold (4 s) before resurrecting; then gets downed and plays the 5 s self-heal hold.
        /// The holds are driven like the real ones (HoldAnim + progress), so every client animates them.
        /// </summary>
        private System.Collections.IEnumerator InjectTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            var me = NetPlayer.Local;
            NetPlayer other = null;
            foreach (var p in NetPlayer.All) if (p != me && p.IsHider) { other = p; break; }
            if (!me.IsHider || other == null) { Debug.Log("[InjectTest] need host as hider + another hider"); yield break; }
            // Body lies 0.9 m ahead of us.
            mm.TestKill(other);
            yield return new WaitForSeconds(1.5f);
            Vector3 body = other.transform.position;
            Vector3 stand = body - other.transform.forward * 0.05f + other.transform.right * 0.9f;
            float yaw = Quaternion.LookRotation(body - stand).eulerAngles.y;
            me.Motor.Teleport(stand, yaw, -40f);
            // Test light so the screenshots show the animation in the dark garage.
            var tl = new GameObject("InjectTestLight").AddComponent<Light>();
            tl.type = LightType.Point; tl.range = 7f; tl.intensity = 2.2f;
            tl.transform.position = body + Vector3.up * 2.2f;
            var shotDir = Environment.GetCommandLineArgs();
            int si = Array.IndexOf(shotDir, "-injshots");
            if (si >= 0 && si + 1 < shotDir.Length) StartCoroutine(InjectBurst(shotDir[si + 1], body, me));
            me.Motor.SetLook(yaw, -45f);
            yield return new WaitForSeconds(0.8f);
            me.ReviveTarget.Value = other.NetworkObjectId;
            me.HoldAnimVar.Value = (byte)HoldAnim.Inject;
            for (float t = 0f; t < HealTimeline.InjectDuration; t += Time.deltaTime)
            {
                me.ReviveProgress.Value = (byte)Mathf.Clamp(Mathf.RoundToInt(t / HealTimeline.InjectDuration * 255f), 0, 255);
                yield return null;
            }
            me.SyringeCount.Value = 1;
            mm.ServerResurrect(me, other);
            me.HoldAnimVar.Value = 0;
            me.ReviveProgress.Value = 0;
            yield return new WaitForSeconds(2.5f);
            Debug.Log($"[InjectTest] after inject: other={other.Life.Value} resurrected={other.Resurrected.Value}");
            // Self heal.
            me.MedkitCount.Value = 1;
            mm.TestDown(me);
            yield return new WaitForSeconds(1.5f);
            me.Motor.SetLook(me.Motor.LookYaw, -55f);
            me.HoldAnimVar.Value = (byte)HoldAnim.SelfHeal;
            for (float t = 0f; t < HealTimeline.SelfHealDuration; t += Time.deltaTime)
            {
                me.ReviveProgress.Value = (byte)Mathf.Clamp(Mathf.RoundToInt(t / HealTimeline.SelfHealDuration * 255f), 0, 255);
                yield return null;
            }
            mm.ServerSelfRevive(me);
            me.HoldAnimVar.Value = 0;
            me.ReviveProgress.Value = 0;
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[InjectTest] after self heal: me={me.Life.Value} wounded={me.Wounded.Value}");
        }

/// <summary>Test only: first-person screenshots + a side camera render (third person) every 0.4 s.</summary>
        private System.Collections.IEnumerator InjectBurst(string dir, Vector3 focus, NetPlayer me)
        {
            System.IO.Directory.CreateDirectory(dir);
            var camGo = new GameObject("InjectSideCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 45f;
            cam.cullingMask = ~(1 << 9); // no first-person viewmodel layer
            var rt = new RenderTexture(640, 400, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(640, 400, TextureFormat.RGB24, false);
            for (int i = 0; i < 45; i++)
            {
                yield return new WaitForSeconds(0.4f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"fp_{i:00}.png"));
                Vector3 c = me != null ? Vector3.Lerp(me.transform.position, focus, 0.5f) : focus;
                Vector3 side = me != null ? me.transform.right : Vector3.right;
                camGo.transform.position = c + side * 2.4f + Vector3.up * 1.3f;
                camGo.transform.LookAt(c + Vector3.up * 0.45f);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 640, 400), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"tp_{i:00}.png"), tex.EncodeToPNG());
            }
            Destroy(camGo);
        }

        private static int ShotCount()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-shotcount");
            return i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out int v) ? v : 20;
        }

        /// <summary>
        /// -testlobbyfun (host + 1 client, in the lobby): the client takes the fork, pushes it into socket 0
        /// (host watches), then plays SNAKE with scripted moves.
        /// </summary>
        private System.Collections.IEnumerator LobbyFunTest()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || NetPlayer.All.Count < 2) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(2f);
            var me = NetPlayer.Local;
            NetPlayer other = null;
            foreach (var p in NetPlayer.All) if (p != me) { other = p; break; }
            yield return new WaitForSeconds(4f); // the client is placed at a lobby spawn when its scene is ready
            other.TeleportRpc(MatchManager.ForkTablePos + new Vector3(0.75f, -MatchManager.ForkTablePos.y, 0.75f), 225f); // between the table and the poufs
            yield return new WaitForSeconds(1.2f);
            mm.ServerPickupFork(other);
            Debug.Log($"[LobbyFun] fork holder={mm.ForkHolder.Value} other={other.NetworkObjectId} held={other.Held.Value}");
            Vector3 o = MatchManager.OutletPos[0], n = MatchManager.OutletNormal[0];
            other.TeleportRpc(o + n * 1.2f - Vector3.up * o.y, 180f);
            Vector3 watch = o + n * 2.4f + Vector3.right * 1.9f;
            watch.y = 0f;
            Vector3 look = o + n * 0.5f + Vector3.up * 0.4f - (watch + Vector3.up * 1.62f);
            me.Motor.Teleport(watch, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
            me.Motor.SetLook(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.8f);
            mm.ServerForkOutlet(other, 0);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[LobbyFun] shocking busy={other.Busy.Value}");
            yield return new WaitForSeconds(ShockTimeline.Respawn + 0.5f);
            Debug.Log($"[LobbyFun] after respawn busy={other.Busy.Value} fork={mm.ForkHolder.Value} held={other.Held.Value} pos={other.transform.position}");

            other.TeleportRpc(MatchManager.ArcadeStand + Vector3.back * 0.5f, 0f);
            Vector3 w2 = MatchManager.ArcadeStand + new Vector3(-1.2f, 0f, 0.15f);
            Vector3 l2 = (MatchManager.ArcadeButtons + MatchManager.ArcadeScreen) * 0.5f - (w2 + Vector3.up * 1.62f);
            me.Motor.Teleport(w2, Mathf.Atan2(l2.x, l2.z) * Mathf.Rad2Deg);
            me.Motor.SetLook(Mathf.Atan2(l2.x, l2.z) * Mathf.Rad2Deg, Mathf.Atan2(l2.y, new Vector2(l2.x, l2.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.6f);
            mm.ServerArcade(other, true);
            Debug.Log($"[LobbyFun] arcade player={mm.ArcadePlayer.Value}");
            byte[] moves = { 1, 0, 1, 2, 3, 2, 1, 0, 1, 2, 1, 0 };
            foreach (var mv in moves)
            {
                yield return new WaitForSeconds(0.9f);
                mm.ServerArcadeDir(other, mv);
            }
            Debug.Log($"[LobbyFun] snake live={ArcadeState.Cab[0].Live} len={ArcadeState.Cab[0].Body.Count} score={ArcadeState.Cab[0].Score} crashed={ArcadeState.Cab[0].Crashed}");
            mm.ServerArcade(other, false);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[LobbyFun] arcade stopped player={mm.ArcadePlayer.Value} live={ArcadeState.Cab[0].Live}");
        }

        // ================================================================ tests: RC car, bricks, exit door, poufs

        private static string ArgOr(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
        }

        /// <summary>Third-person side camera renders every <paramref name="every"/> s (test screenshots).</summary>
        private System.Collections.IEnumerator SideBurst(string dir, string prefix, Func<Vector3> focus, Func<Vector3> offset, int count, float every)
        {
            if (string.IsNullOrEmpty(dir)) yield break;
            System.IO.Directory.CreateDirectory(dir);
            var camGo = new GameObject("TestSideCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9);
            var rt = new RenderTexture(800, 450, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(every);
                yield return new WaitForEndOfFrame();
                Vector3 f = focus();
                camGo.transform.position = f + offset();
                camGo.transform.LookAt(f);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"{prefix}_{i:00}.png"), tex.EncodeToPNG());
            }
            Destroy(camGo);
            rt.Release();
        }

        private static void TestLight(Vector3 pos, float range, float intensity)
        {
            var tl = new GameObject("TestLight").AddComponent<Light>();
            tl.type = LightType.Point;
            tl.range = range;
            tl.intensity = intensity;
            tl.shadows = LightShadows.Soft;
            tl.transform.position = pos;
        }

        private static string RcState(MatchManager mm, int i)
        {
            var s = mm.RcSlot(i);
            var car = RcCar.Peek(i);
            return $"slot{i}: {s.Mode} holder={s.Holder} pos={s.Pos} | view={(car != null ? car.View.ToString() : "-")} carPos={(car != null ? car.transform.position.ToString("F2") : "-")} speed={(car != null ? car.Speed : 0f):0.00}";
        }

        /// <summary>-testrc (host hider via -forcehider + seeker client): pick up, put down, drive, horn, park, resume, stomp.</summary>
        private System.Collections.IEnumerator RcTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            var me = NetPlayer.Local;
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p.IsSeeker) seeker = p;
            if (!me.IsHider || seeker == null) { Debug.Log("[RcTest] need host hider + seeker"); yield break; }
            string dir = ArgOr("-rcshots", "");
            TestLight(RcCarShelf.Position + new Vector3(-1.5f, 2.6f, 0f), 9f, 1.6f);
            TestLight(new Vector3(-18f, -1f, 54.5f), 12f, 1.8f);

            // 1. Shelf: prompt + pickup.
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(-0.3f, 0f, -1.15f), RcCarShelf.Yaw);
            Vector3 aim = RcCarShelf.CarPos(0) + Vector3.up * 0.1f - (me.transform.position + Vector3.up * 1.62f);
            me.Motor.SetLook(RcCarShelf.Yaw, Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(1.2f);
            var lpc = LocalPlayerController.Instance;
            Debug.Log($"[RcTest] shelf prompt={lpc.Prompt} '{lpc.PromptText}' | {RcState(mm, 0)} | {RcState(mm, 1)}");
            if (!string.IsNullOrEmpty(dir)) StartCoroutine(SideBurst(dir, "shelf", () => RcCarShelf.CarPos(0), () => shelfRot * new Vector3(0.6f, 0.3f, -1.2f), 1, 0.2f));
            yield return new WaitForSeconds(0.5f);
            mm.ServerRcPickup(me, 0);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[RcTest] picked up | {RcState(mm, 0)} slotOf={mm.RcSlotOf(me)}");

            // 2. Put it down (key 6 path) on open floor, facing west.
            me.Motor.Teleport(new Vector3(-13.6f, -3.4f, 54.6f), 270f);
            yield return new WaitForSeconds(0.8f);
            if (!string.IsNullOrEmpty(dir))
                StartCoroutine(SideBurst(dir, "tp", () => { var c = RcCar.Peek(0); return c != null && c.View != RcCar.ViewMode.Hidden ? Vector3.Lerp(c.transform.position, me.transform.position, 0.35f) + Vector3.up * 0.3f : me.transform.position + Vector3.up * 0.6f; },
                    () => new Vector3(0f, 1.2f, -2.6f), 60, 0.35f));
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[RcTest] after 6: {RcState(mm, 0)} anim={me.RcAnimating} inHand={me.RcCarInHand}");
            yield return new WaitForSeconds(2.0f);
            Debug.Log($"[RcTest] seated: cam={me.RcCameraOn} ctrl={me.RcControlling} view={(RcCarView.Active != null)} | {RcState(mm, 0)}");

            // 3. Drive: straight, turn, horn, boost, coast.
            LocalPlayerController.TestRcActive = true;
            Vector3 start = RcCar.Peek(0).transform.position;
            LocalPlayerController.TestRcInput = new Vector2(0f, 1f);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[RcTest] straight 1.5 s: moved={Vector3.Distance(start, RcCar.Peek(0).transform.position):0.00} | {RcState(mm, 0)}");
            LocalPlayerController.TestRcInput = new Vector2(0.7f, 1f);
            yield return new WaitForSeconds(1.0f);
            LocalPlayerController.TestRcHorn = true;
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestRcHorn = false;
            LocalPlayerController.TestRcInput = new Vector2(-0.7f, 1f);
            LocalPlayerController.TestRcBoost = true;
            yield return new WaitForSeconds(1.0f);
            Debug.Log($"[RcTest] boost: {RcState(mm, 0)} yaw={RcCar.Peek(0).Yaw:0}");
            LocalPlayerController.TestRcBoost = false;
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(1.0f);
            var pk = me.RcDrive.Value;
            Debug.Log($"[RcTest] coast: {RcState(mm, 0)} netPack={pk.Pos} yaw={pk.YawDeg:0}");
            // Into the west wall / tank: bump.
            LocalPlayerController.TestRcInput = new Vector2(0f, 1f);
            yield return new WaitForSeconds(4.5f);
            Debug.Log($"[RcTest] into wall: {RcState(mm, 0)}");
            LocalPlayerController.TestRcInput = new Vector2(0f, -1f);
            yield return new WaitForSeconds(1.0f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.6f);

            // 4. Park, stand up, resume.
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[RcTest] parked: {RcState(mm, 0)} cam={me.RcCameraOn} anim={me.RcAnimating}");
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[RcTest] stood up: anim={me.RcAnimating} move={me.Motor.MovementEnabled} cam={me.Camera.enabled}");
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[RcTest] resumed: {RcState(mm, 0)} cam={me.RcCameraOn} ctrl={me.RcControlling}");
            LocalPlayerController.TestRcInput = new Vector2(0.4f, 1f);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.8f);

            // 5. The seeker stomps on it.
            var car = RcCar.Peek(0);
            Vector3 cp = car.transform.position;
            seeker.TeleportRpc(cp + new Vector3(1.2f, 0f, 0.3f), Quaternion.LookRotation(cp - (cp + new Vector3(1.2f, 0f, 0.3f))).eulerAngles.y);
            yield return new WaitForSeconds(0.6f);
            mm.ServerRcStomp(seeker, 0);
            yield return new WaitForSeconds(0.2f);
            Debug.Log($"[RcTest] stomp start: {RcState(mm, 0)} seekerBusy={seeker.Busy.Value}");
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[RcTest] stomp hit: {RcState(mm, 0)} cam={me.RcCameraOn}");
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[RcTest] after stomp: {RcState(mm, 0)} cam={me.RcCameraOn} anim={me.RcAnimating} slotOf={mm.RcSlotOf(me)} seekerBusy={seeker.Busy.Value} move={me.Motor.MovementEnabled}");
            LocalPlayerController.TestRcActive = false;
            // 6. The other car is still on the shelf; take it and get knifed while driving.
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(0.3f, 0f, -1.1f), RcCarShelf.Yaw);
            yield return new WaitForSeconds(0.5f);
            mm.ServerRcPickup(me, 1);
            me.Motor.Teleport(new Vector3(-13.6f, -3.4f, 53.2f), 270f);
            yield return new WaitForSeconds(0.6f);
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(2.5f);
            Debug.Log($"[RcTest] car 2 driving: {RcState(mm, 1)}");
            seeker.TeleportRpc(me.transform.position + me.transform.forward * -1.2f, me.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.5f);
            mm.ServerKnife(seeker, me);
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[RcTest] knifed while driving: {RcState(mm, 1)} limping={me.Limping.Value} cam={me.RcCameraOn}");
            yield return new WaitForSeconds(2f);
            Debug.Log($"[RcTest] done: anim={me.RcAnimating} move={me.Motor.MovementEnabled}");
        }

        /// <summary>-testrcwatch (other peer): logs how the cars look here once a second.</summary>
        private System.Collections.IEnumerator RcWatch()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking) { mm = MatchManager.Instance; yield return null; }
            for (int i = 0; i < 45; i++)
            {
                yield return new WaitForSeconds(1f);
                NetPlayer holder = NetPlayer.Find(mm.RcSlot(0).Holder);
                Debug.Log($"[RcWatch] {RcState(mm, 0)} | {RcState(mm, 1)} | holderAnim={(holder != null ? holder.RcAnimating.ToString() : "-")} pack={(holder != null ? holder.RcDrive.Value.Pos.ToString("F2") : "-")}");
            }
        }

        /// <summary>-testbricks (host + client in the lobby): host builds by aiming + clicking, changes colours, undoes.</summary>
        private System.Collections.IEnumerator BricksTest()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || LobbyBricks.Instance == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(2.5f);
            var me = NetPlayer.Local;
            if (!NetworkManager.Singleton.IsServer)
            {
                for (int i = 0; i < 12; i++)
                {
                    yield return new WaitForSeconds(2f);
                    Debug.Log($"[BrickWatch] bricks={mm.Bricks.Count} views={LobbyBricks.Instance.transform.childCount}");
                }
                yield break;
            }
            string dir = ArgOr("-brickshots", "");
            me.Motor.Teleport(new Vector3(LobbyBricks.Origin.x + 0.1f, 0f, LobbyBricks.Origin.z + 0.15f), 0f);
            yield return new WaitForSeconds(0.8f);
            var lpc = LocalPlayerController.Instance;
            Vector3 tubAim = LobbyBricks.TubAim - (me.transform.position + Vector3.up * 1.62f);
            me.Motor.SetLook(Mathf.Atan2(tubAim.x, tubAim.z) * Mathf.Rad2Deg, Mathf.Atan2(tubAim.y, new Vector2(tubAim.x, tubAim.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[BrickTest] tub prompt={lpc.Prompt} '{lpc.PromptText}'");
            me.Motor.Teleport(new Vector3(LobbyBricks.Origin.x + 1.1f, 0f, LobbyBricks.Origin.z - 0.5f), 0f);
            yield return new WaitForSeconds(0.3f);
            LobbyBricks.BeginBuilding();
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[BrickTest] building={LobbyBricks.Building} handColor={me.BrickColor.Value}");

            void Look(Vector3 p)
            {
                Vector3 d = p - me.Camera.transform.position;
                me.Motor.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            }
            float S = LobbyBricks.Cell;
            // (x, z, aim point offset from the cell's bottom centre, colour): a row, a tower on the middle one, a side add.
            var steps = new (int x, int y, int z, Vector3 aimOff, int c)[]
            {
                (3, 0, 4, Vector3.zero, 0), (4, 0, 4, Vector3.zero, 1), (5, 0, 4, Vector3.zero, 2), (6, 0, 4, Vector3.zero, 3),
                (4, 0, 4, new Vector3(0f, S, 0f), 4), (4, 1, 4, new Vector3(0f, S, 0f), 0), (4, 2, 4, new Vector3(0f, S, 0f), 2),
                (5, 0, 4, new Vector3(0f, S * 0.5f, -S * 0.5f), 1), (6, 0, 4, new Vector3(0f, S, 0f), 3),
            };
            if (!string.IsNullOrEmpty(dir)) StartCoroutine(SideBurst(dir, "build", () => LobbyBricks.CellCenter(4, 1, 4), () => new Vector3(1.4f, 0.9f, -1.6f), 14, 0.5f));
            foreach (var st in steps)
            {
                Look(LobbyBricks.CellBottom(st.x, st.y, st.z) + st.aimOff);
                LobbyBricks.TestColor = st.c;
                yield return new WaitForSeconds(0.25f);
                LobbyBricks.TestClick = true;
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[BrickTest] placed: bricks={mm.Bricks.Count}");
            // Occupied / outside the plate: no brick.
            Look(LobbyBricks.CellBottom(4, 3, 4) + Vector3.up * S * 0.5f + Vector3.forward * 0.6f);
            yield return new WaitForSeconds(0.25f);
            LobbyBricks.TestClick = true;
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[BrickTest] after a miss: bricks={mm.Bricks.Count}");
            LobbyBricks.TestUndo = true;
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[BrickTest] after undo: bricks={mm.Bricks.Count}");
            if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "build_fp.png"));
            yield return new WaitForSeconds(1.5f);
            LobbyBricks.StopBuilding();
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[BrickTest] stopped: building={LobbyBricks.Building} handColor={me.BrickColor.Value} bricks={mm.Bricks.Count}");
        }

        /// <summary>-testexit (host + client in the lobby): the client opens the exit door (host watches), then the host does.</summary>
        private System.Collections.IEnumerator ExitTest()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || NetPlayer.All.Count < 2 || LobbyExitDoor.Instance == null) { mm = MatchManager.Instance; yield return null; }
            if (!NetworkManager.Singleton.IsServer) yield break;
            yield return new WaitForSeconds(2.5f);
            var me = NetPlayer.Local;
            NetPlayer other = null;
            foreach (var p in NetPlayer.All) if (p != me) { other = p; break; }
            string dir = ArgOr("-exitshots", "");
            var door = LobbyExitDoor.Instance;
            // Host watches from the side.
            Vector3 watch = new Vector3(2.0f + (LobbyLayout.ExitDoorX - 4.3f), 0f, -3.4f);
            me.Motor.Teleport(watch, 0f);
            Vector3 l = new Vector3(4.3f + (LobbyLayout.ExitDoorX - 4.3f), 1.1f, -5.5f) - (watch + Vector3.up * 1.62f);
            me.Motor.SetLook(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg);
            other.TeleportRpc(new Vector3(4.4f + (LobbyLayout.ExitDoorX - 4.3f), 0f, -4.6f), 180f);
            yield return new WaitForSeconds(1f);
            if (!string.IsNullOrEmpty(dir)) StartCoroutine(SideBurst(dir, "side", () => new Vector3(4.3f + (LobbyLayout.ExitDoorX - 4.3f), 1.0f, -5.6f), () => new Vector3(1.5f, 0.5f, 2.9f), 26, 0.4f));
            mm.ServerExitDoor(other);
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(1.6f);
                Debug.Log($"[ExitTest] t={1.6f * (i + 1):0.0} running={door.Running} busy={other.Busy.Value} doorYaw={door.transform.localEulerAngles.y:0}");
            }
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[ExitTest] after respawn: busy={other.Busy.Value} pos={other.transform.position} running={door.Running}");
            // Blocked doorway: try to walk through the closed door position.
            Vector3 blockTest = new Vector3(4.3f + (LobbyLayout.ExitDoorX - 4.3f), 1f, -5.4f);
            bool blocked = Physics.Raycast(blockTest, Vector3.back, out var bh, 1.5f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            Debug.Log($"[ExitTest] doorway blocked={blocked} by={(blocked ? bh.collider.name + " @" + bh.distance.ToString("0.00") : "-")}");
            // Now the host himself (first-person screenshots with -shots).
            me.Motor.Teleport(new Vector3(4.4f + (LobbyLayout.ExitDoorX - 4.3f), 0f, -4.6f), 180f);
            me.Motor.SetLook(180f, -8f);
            yield return new WaitForSeconds(1f);
            var lpc = LocalPlayerController.Instance;
            Debug.Log($"[ExitTest] host prompt={lpc.Prompt} '{lpc.PromptText}'");
            mm.ServerExitDoor(me);
            for (int i = 0; i < 24 && !string.IsNullOrEmpty(dir); i++)
            {
                yield return new WaitForSeconds(0.4f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"fp_{i:00}.png"));
            }
            yield return new WaitForSeconds(Mathf.Max(0.5f, ExitTimeline.Respawn + 1f - 9.6f));
            Debug.Log($"[ExitTest] host after: busy={me.Busy.Value} move={me.Motor.MovementEnabled} pos={me.transform.position}");
        }

        /// <summary>-testradio (host + client in the lobby): queue two links via the panel, sync, pause, volume, bass, next.</summary>
        private System.Collections.IEnumerator RadioTest()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || Boombox.Instance == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(2.5f);
            var me = NetPlayer.Local;
            string dir = ArgOr("-radioshots", "");
            void Log(string what) => Debug.Log($"[RadioTest] {what}: {Boombox.Instance.DebugState}");
            if (!NetworkManager.Singleton.IsServer)
            {
                for (int i = 0; i < 14; i++) { yield return new WaitForSeconds(3f); Log($"client t={3 * (i + 1)}"); }
                yield break;
            }
            string url = ArgOr("-radiourl", "https://www.youtube.com/watch?v=7-QK_1oHoiQ");
            Vector3 radioStand = Boombox.Position + new Vector3(1.2f, -0.7f, 1.3f);
            Vector3 l = Boombox.Instance.AimPoint - (radioStand + Vector3.up * 1.62f);
            me.Motor.Teleport(radioStand, 0f);
            me.Motor.SetLook(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[RadioTest] prompt={LocalPlayerController.Instance.Prompt} '{LocalPlayerController.Instance.PromptText}'");
            Boombox.OpenPanel();
            yield return new WaitForSeconds(0.3f);
            Boombox.TestUrl = url;
            yield return new WaitForSeconds(0.5f);
            Boombox.TestUrl = url; // the same song twice: tests next
            yield return new WaitForSeconds(9f);
            Log("playing");
            if (!string.IsNullOrEmpty(dir)) { System.IO.Directory.CreateDirectory(dir); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "panel.png")); }
            yield return new WaitForSeconds(3f);
            me.RequestRadioRpc(RadioOp.VolUp, default);
            me.RequestRadioRpc(RadioOp.BassUp, default);
            me.RequestRadioRpc(RadioOp.BassUp, default);
            yield return new WaitForSeconds(1f);
            Log("vol/bass up");
            me.RequestRadioRpc(RadioOp.Toggle, default);
            yield return new WaitForSeconds(2f);
            Log("paused");
            me.RequestRadioRpc(RadioOp.Toggle, default);
            yield return new WaitForSeconds(9f);
            Log("resumed");
            me.RequestRadioRpc(RadioOp.Next, default);
            yield return new WaitForSeconds(9f);
            Log("next");
            Boombox.ClosePanel();
            if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "boombox.png"));
            yield return new WaitForSeconds(1f);
        }

        /// <summary>-testpouf (lobby): drop onto every pouf / a pot / the lamp base and log the standing height.</summary>
        private System.Collections.IEnumerator PoufTest()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(2.5f);
            var me = NetPlayer.Local;
            var spots = new (string name, Vector3 p)[]
            {
                ("pouf1", LobbyLayout.Poufs[0].pos + Vector3.up * 1.2f), ("pouf2", LobbyLayout.Poufs[1].pos + Vector3.up * 1.2f),
                ("pot", LobbyLayout.Plants[1] + Vector3.up * 1.4f), ("globe", LobbyLayout.Globe + Vector3.up * 1.4f), ("floor", new Vector3(1.5f, 1.2f, -1f)),
            };
            foreach (var (name, p) in spots)
            {
                me.Motor.Teleport(p, 0f);
                yield return new WaitForSeconds(1.6f);
                Debug.Log($"[PoufTest] {name}: feet y={me.transform.position.y:0.000} grounded={me.Motor.IsGrounded}");
            }
        }

/// <summary>-testwound (host hider via -forcehider + seeker client): knife twice -> limping, then bleeding.</summary>
        private System.Collections.IEnumerator WoundTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            var me = NetPlayer.Local;
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p.IsSeeker) seeker = p;
            if (seeker == null || !me.IsHider) { Debug.Log("[WoundTest] need host hider + seeker"); yield break; }
            seeker.TeleportRpc(me.transform.position + me.transform.forward * 1.1f, me.transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(0.6f);
            mm.ServerKnife(seeker, me);
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[WoundTest] hit 1: life={me.Life.Value} limping={me.Limping.Value} wounded={me.Wounded.Value}");
            mm.ServerKnife(seeker, me);
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[WoundTest] hit during cooldown: life={me.Life.Value} busy={me.Busy.Value} (cooldown left {seeker.AttackReadyAt.Value - mm.Now:0.0} s)");
            // Progress ring while bandaging the own wound (hold G).
            me.MedkitCount.Value = 1;
            LocalPlayerController.TestHoldG = true;
            yield return new WaitForSeconds(Tuning.HealHoldTime * 0.5f);
            string rd = ArgOr("-ringshot", "");
            if (!string.IsNullOrEmpty(rd)) { System.IO.Directory.CreateDirectory(rd); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(rd, "ring_self.png")); }
            yield return new WaitForSeconds(Tuning.HealHoldTime * 0.5f + 0.6f);
            LocalPlayerController.TestHoldG = false;
            Debug.Log($"[WoundTest] after bandage (G): limping={me.Limping.Value} medkits={me.MedkitCount.Value}");
            while (seeker.AttackReadyAt.Value > mm.Now) yield return null;
            seeker.TeleportRpc(me.transform.position + me.transform.forward * 1.1f, me.transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(0.5f);
            mm.ServerKnife(seeker, me);
            yield return new WaitForSeconds(Tuning.KnifeSequenceDuration + 3f);
            Debug.Log($"[WoundTest] hit 2: life={me.Life.Value} limping={me.Limping.Value} wounded={me.Wounded.Value}");
        }

        /// <summary>-testsawleg (host hider via -forcehider + seeker client): saw cuts a leg, hider hops, dust blinds the seeker, second saw hit kills.</summary>
        private System.Collections.IEnumerator SawLegTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            var me = NetPlayer.Local;
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p.IsSeeker) seeker = p;
            if (seeker == null || !me.IsHider) { Debug.Log("[SawLegTest] need host hider + seeker"); yield break; }
            seeker.HasChainsaw.Value = true;
            seeker.Held.Value = HideAndSeek.Core.HeldItem.Chainsaw;
            seeker.TeleportRpc(me.transform.position + me.transform.forward * 1.1f, me.transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(0.6f);
            string rd = ArgOr("-sawshots", "");
            if (!string.IsNullOrEmpty(rd)) System.IO.Directory.CreateDirectory(rd);
            mm.ServerChainsaw(seeker, me);
            yield return new WaitForSeconds(0.8f);
            if (!string.IsNullOrEmpty(rd)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(rd, "saw_hit1.png"));
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[SawLegTest] hit 1: life={me.Life.Value} severed={me.Severed.Value} busy={me.Busy.Value} cooldown={seeker.AttackReadyAt.Value - mm.Now:0.0}");
            mm.ServerChainsaw(seeker, me);
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[SawLegTest] hit during cooldown: life={me.Life.Value} busy={me.Busy.Value}");
            // Hop away a bit, then dust.
            yield return new WaitForSeconds(1.5f);
            if (!string.IsNullOrEmpty(rd)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(rd, "hop.png"));
            seeker.TeleportRpc(me.transform.position + me.transform.forward * 1.3f, me.transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(0.5f);
            mm.ServerDust(me);
            yield return new WaitForSeconds(0.9f);
            Debug.Log($"[SawLegTest] dust: seeker blind for {seeker.BlindUntil.Value - mm.Now:0.0} s");
            mm.ServerChainsaw(seeker, me);
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[SawLegTest] attack while blinded: busy={me.Busy.Value}");
            while (seeker.AttackReadyAt.Value > mm.Now || seeker.BlindUntil.Value > mm.Now) yield return null;
            seeker.TeleportRpc(me.transform.position + me.transform.forward * 1.1f, me.transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(0.6f);
            mm.ServerChainsaw(seeker, me);
            yield return new WaitForSeconds(2.5f);
            if (!string.IsNullOrEmpty(rd)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(rd, "saw_kill.png"));
            yield return new WaitForSeconds(HideAndSeek.Characters.KillMoves.SequenceDuration(HideAndSeek.Characters.KillMove.SawFinisher) - 1.5f);
            Debug.Log($"[SawLegTest] hit 2: life={me.Life.Value} severed={me.Severed.Value}");
        }

        /// <summary>-testspectate (host hider via -forcehider + seeker client): the host dies and watches in grey night vision (-specshots folder).</summary>
        private System.Collections.IEnumerator SpectateTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(2f);
            string rd = ArgOr("-specshots", "");
            if (!string.IsNullOrEmpty(rd)) System.IO.Directory.CreateDirectory(rd);
            mm.TestKill(NetPlayer.Local);
            yield return new WaitForSeconds(2.5f);
            if (!string.IsNullOrEmpty(rd)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(rd, "spectate.png"));
            Debug.Log("[SpecTest] spectating");
        }

        /// <summary>-testsecurity: walk into the security rooms of all levels, press the floor buttons, screenshot the monitors (-secshots folder).</summary>
        private System.Collections.IEnumerator SecurityTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null || SecurityRooms.Instance == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            var me = NetPlayer.Local;
            string rd = ArgOr("-secshots", "");
            if (!string.IsNullOrEmpty(rd)) System.IO.Directory.CreateDirectory(rd);
            for (int lv = 0; lv < 3; lv++)
            {
                Vector3 p = SecurityRooms.RoomFloorPoint(lv);
                me.TeleportRpc(new Vector3(p.x, p.y + 0.1f, p.z + 0.9f), 0f);
                yield return new WaitForSeconds(0.5f);
                me.RequestSecurityFloorRpc((byte)lv);
                SecurityRooms.Instance.ForceMotion(lv, 9);
                SecurityRooms.Instance.ForceMotion(lv, 12);
                yield return new WaitForSeconds(1.0f);
                int frames = 0; float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 1.5f) { frames++; yield return null; }
                Debug.Log($"[SecTest] level {lv}: player at {me.transform.position}, game fps in the room {frames / 1.5f:0}, watched={mm.SecurityWatched.Value}, led cam1={SecurityRooms.Instance.LedOn(lv, 0)}");
                if (!string.IsNullOrEmpty(rd))
                {
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(rd, $"sec_L{lv}.png"));
                    SecurityRooms.Instance.DumpFeed(0, System.IO.Path.Combine(rd, $"feed_L{lv}_0.png"));
                    SecurityRooms.Instance.DumpFeed(1, System.IO.Path.Combine(rd, $"feed_L{lv}_1.png"));
                }
                if (lv == 0 && !string.IsNullOrEmpty(rd))
                {
                    yield return new WaitForSeconds(0.5f);
                    me.TeleportRpc(new Vector3(p.x - 3.5f, p.y + 0.1f, p.z - 1.6f), 40f);
                    yield return new WaitForSeconds(1.5f);
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(rd, "sec_room_wide.png"));
                    yield return new WaitForSeconds(0.5f);
                }
                yield return new WaitForSeconds(1f);
            }
            // Close look at a camera model in the garage (nobody in the room now: LED dark).
            Vector3 cp = SecurityRooms.Instance.CameraPosition(0, 0);
            me.TeleportRpc(new Vector3(cp.x + 2.2f, 0.1f, cp.z - 0.6f), -70f);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[SecTest] outside: watched={mm.SecurityWatched.Value}");
            if (!string.IsNullOrEmpty(rd)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(rd, "cctv_model.png"));
        }

                private static float ShotEvery()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-shotevery");
            return i >= 0 && i + 1 < a.Length && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 2f;
        }

        /// <summary>Test switch -hostdelay N: starts a UDP host after N seconds in the menu.</summary>
        private System.Collections.IEnumerator HostLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Host();
        }

        private FacepunchTransport _steamTransport;

        /// <summary>True while the current session runs over Steam (not UDP).</summary>
        public bool UsingSteam { get; private set; }

        /// <summary>Busy creating / joining a Steam lobby (menu shows a spinner text).</summary>
        public bool SteamBusy { get; private set; }

        private void UseTransport(bool steam)
        {
            UsingSteam = steam;
            Manager.NetworkConfig.NetworkTransport = steam ? _steamTransport : Transport;
        }

        /// <summary>UDP / LAN server (fallback, also works without Steam).</summary>
        public bool Host()
        {
            ServerName = LocalProfile.Nick;
            UseTransport(false);
            Transport.SetConnectionData("127.0.0.1", _port, "0.0.0.0");
            return StartHostCommon(true);
        }

        /// <summary>Steam server: creates a Steam lobby (friends can join / be invited) and hosts over the Steam relay.</summary>
        public async void HostSteam()
        {
            if (SteamBusy) return;
            if (!SteamService.Ready)
            {
                LastMessage = "Steam nie jest dostępny: " + SteamService.Error + ". Użyj serwera LAN/UDP.";
                return;
            }
            SteamBusy = true;
            try
            {
                ServerName = LocalProfile.Nick;
                bool ok = await SteamService.CreateLobby(LocalProfile.Nick);
                if (!ok)
                {
                    LastMessage = "Nie udało się utworzyć lobby Steam.";
                    return;
                }
                UseTransport(true);
                if (!StartHostCommon(false)) SteamService.LeaveLobby();
            }
            finally { SteamBusy = false; }
        }

        /// <summary>Joins a Steam lobby (from the browser, an invite or the friends list) and connects to its owner.</summary>
        public async void JoinSteamLobby(ulong lobbyId)
        {
            if (SteamBusy || Manager.IsListening) return;
            SteamBusy = true;
            try
            {
                var (result, hostId, message) = await SteamService.JoinLobby(lobbyId);
                if (result != SteamService.JoinResult.Ok)
                {
                    LastMessage = message;
                    MainMenu.ShowMessage(message);
                    return;
                }
                if (hostId == SteamService.MySteamId)
                {
                    SteamService.LeaveLobby();
                    LastMessage = "To twoje własne lobby.";
                    MainMenu.ShowMessage(LastMessage);
                    return;
                }
                UseTransport(true);
                _steamTransport.targetSteamId = hostId;
                Manager.NetworkConfig.ConnectionData = Payload();
                MainMenu.ShowConnecting();
                if (!Manager.StartClient())
                {
                    SteamService.LeaveLobby();
                    LastMessage = "Nie udało się połączyć przez Steam.";
                    MainMenu.ShowMessage(LastMessage);
                }
            }
            finally { SteamBusy = false; }
        }

        private void OnSteamJoinRequested(ulong lobbyId)
        {
            if (Manager.IsListening)
            {
                GameHud.Toast("Najpierw wyjdź z obecnej gry, potem przyjmij zaproszenie.", 4f);
                return;
            }
            JoinSteamLobby(lobbyId);
        }

        private bool StartHostCommon(bool udp)
        {
            Manager.NetworkConfig.ConnectionData = Payload();
            if (!Manager.StartHost())
            {
                LastMessage = udp ? $"Nie udało się uruchomić serwera (port {_port} zajęty - czy gra jest już otwarta?)"
                                  : "Nie udało się uruchomić serwera Steam.";
                return false;
            }
            if (udp && !_noDiscovery) LanDiscovery.StartResponder();
            var mm = Instantiate(matchManagerPrefab);
            DontDestroyOnLoad(mm);
            mm.GetComponent<NetworkObject>().Spawn(false);
            Manager.SceneManager.LoadScene(LobbyScene, LoadSceneMode.Single);
            return true;
        }

        public bool Join(string address, ushort port = GamePort)
        {
            address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            if (address.Equals("localhost", StringComparison.OrdinalIgnoreCase)) address = "127.0.0.1";
            int colon = address.IndexOf(':');
            if (colon > 0 && ushort.TryParse(address.Substring(colon + 1), out ushort p))
            {
                port = p;
                address = address.Substring(0, colon);
            }
            LocalProfile.LastIp = address;
            UseTransport(false);
            Transport.SetConnectionData(address, port);
            Manager.NetworkConfig.ConnectionData = Payload();
            bool ok = Manager.StartClient();
            if (!ok) LastMessage = "Nie udało się połączyć.";
            return ok;
        }

        /// <summary>Leaves the session (host closes the server) and returns to the main menu.</summary>
        public void Leave(string message = null)
        {
            if (message != null) LastMessage = message;
            Debug.Log($"[NetSession] leave to the menu ({message ?? "own choice"}), scene {SceneManager.GetActiveScene().name}, t {Time.realtimeSinceStartup:0.0} s");
            LanDiscovery.StopResponder();
            SteamService.LeaveLobby();
            if (Manager.IsListening) Manager.Shutdown();
            if (SceneManager.GetActiveScene().name != MenuScene) SceneManager.LoadScene(MenuScene);
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
            LanDiscovery.StopResponder();
            LanDiscovery.StopSearch();
            SteamService.Shutdown();
            DiscordPresence.Stop();
        }

        // ---------------------------------------------------------------- server

        private void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse resp)
        {
            string nick = "Gracz";
            int skin = 0;
            string mc = "";
            try
            {
                string[] p = Encoding.UTF8.GetString(req.Payload ?? Array.Empty<byte>()).Split('|');
                if (p.Length > 0) nick = LocalProfile.Sanitize(p[0]);
                if (p.Length > 1) int.TryParse(p[1], out skin);
                if (p.Length > 3 && MinecraftSkins.IsValidNick(p[3])) mc = p[3];
            }
            catch { /* defaults */ }

            // Everybody must run the same build.
            string theirVersion = "";
            try
            {
                string[] pv = Encoding.UTF8.GetString(req.Payload ?? Array.Empty<byte>()).Split('|');
                if (pv.Length > 2) theirVersion = pv[2];
            }
            catch { /* ignore */ }
            if (req.ClientNetworkId != NetworkManager.ServerClientId && theirVersion != Application.version)
            {
                resp.Approved = false;
                resp.Reason = $"Inna wersja gry: masz {theirVersion}, serwer ma {Application.version}. " + (UpdateChecker.Compare(Application.version, theirVersion) > 0 ? "Zaktualizuj grę (przycisk w menu)." : "Host musi zaktualizować grę.");
                return;
            }

            var mm = MatchManager.Instance;
            bool inLobby = mm == null || mm.Phase.Value == MatchPhase.Lobby;
            if (!inLobby && req.ClientNetworkId != NetworkManager.ServerClientId)
            {
                resp.Approved = false;
                resp.Reason = "Runda już trwa. Spróbuj, gdy wrócą do lobby.";
                return;
            }

            nick = UniqueNick(nick);
            Debug.Log($"[NetSession] approve client={req.ClientNetworkId} mc={mc} payload='{Encoding.UTF8.GetString(req.Payload ?? Array.Empty<byte>())}' -> {nick}");
            _pending[req.ClientNetworkId] = (nick, Mathf.Clamp(skin, 0, 4), mc);
            resp.Approved = true;
            resp.CreatePlayerObject = true;
            resp.Pending = false;
        }

        private string UniqueNick(string nick)
        {
            var used = new HashSet<string>();
            foreach (var kv in _pending) used.Add(kv.Value.nick);
            foreach (var p in NetPlayer.All) used.Add(p.Nick);
            string n = nick;
            for (int i = 2; used.Contains(n); i++) n = nick + i;
            return n;
        }

        public (string nick, int skin, string mc) TakeProfile(ulong clientId)
        {
            if (_pending.TryGetValue(clientId, out var v))
            {
                _pending.Remove(clientId);
                return v;
            }
            return clientId == NetworkManager.ServerClientId ? (LocalProfile.Nick, LocalProfile.Skin, LocalProfile.McNick) : ("Gracz" + clientId, 0, "");
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (_quitting) return;
            if (Manager.IsServer)
            {
                _pending.Remove(clientId);
                return;
            }
            // We are a client and got disconnected (or failed to connect).
            string reason = Manager.DisconnectReason;
            Debug.LogWarning($"[NetSession] disconnected from the server (client {clientId}, scene {SceneManager.GetActiveScene().name}, t {Time.realtimeSinceStartup:0.0} s): '{reason}'");
            Leave(string.IsNullOrEmpty(reason) ? "Rozłączono z serwerem." : reason);
        }
    }
}
