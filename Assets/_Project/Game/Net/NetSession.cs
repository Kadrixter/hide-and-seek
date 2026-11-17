using System;
using System.Collections.Generic;
using System.Text;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lives next to the NetworkManager in the Menu scene (persistent). Starts host / client over plain UDP
    /// (direct IP, works on LAN and virtual LANs), registers prefabs, approves connections and drives
    /// scene changes (Menu -> Lobby -> Game -> Lobby ...).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class NetSession : MonoBehaviour
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
            LocalProfile.ApplyAudio();
            if (PlayerPrefs.HasKey("hns.display")) LocalProfile.ApplyDisplay();
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
            if (Has("-host")) Host();
            else if (Arg("-join") is string ip) Join(ip, _port);
            if (Has("-autostart")) StartCoroutine(AutoStart());
            if (Arg("-shots") is string dir) StartCoroutine(Shots(dir));
        }

        private System.Collections.IEnumerator Shots(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSeconds(i == 0 ? 2.5f : 2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{LocalProfile.Nick}_{i:00}.png"));
            }
        }

        private System.Collections.IEnumerator AutoStart()
        {
            while (MatchManager.Instance == null || NetPlayer.All.Count < 2) yield return new WaitForSeconds(0.5f);
            yield return new WaitForSeconds(3f);
            Debug.Log("[NetSession] autostart");
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
            yield return new WaitForSeconds(Tuning.KnifeSequenceDuration + 1f);
            Log("after knife");
            if (h2 != null)
            {
                mm.ServerRescue(h2, h1);
                yield return new WaitForSeconds(1.5f);
                Log("after rescue");
            }
            mm.TestGiveGlock();
            yield return new WaitForSeconds(2f);
            mm.ServerShoot(seeker, seeker.EyePosition, (h1.ChestPosition - seeker.EyePosition).normalized, h1, h1.ChestPosition);
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
                yield return new WaitForSeconds(Tuning.KnifeSequenceDuration + 1f);
                Log("after knife 2");
            }
        }
        private void TryAddPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            try { Manager.AddNetworkPrefab(prefab); }
            catch (Exception e) { Debug.Log($"[NetSession] prefab {prefab.name}: {e.Message}"); }
        }

        private void Update()
        {
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

        public bool Host()
        {
            ServerName = LocalProfile.Nick;
            Transport.SetConnectionData("127.0.0.1", _port, "0.0.0.0");
            Manager.NetworkConfig.ConnectionData = Payload();
            if (!Manager.StartHost())
            {
                LastMessage = $"Nie udało się uruchomić serwera (port {_port} zajęty - czy gra jest już otwarta?)";
                return false;
            }
            if (!_noDiscovery) LanDiscovery.StartResponder();
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
            LanDiscovery.StopResponder();
            if (Manager.IsListening) Manager.Shutdown();
            if (SceneManager.GetActiveScene().name != MenuScene) SceneManager.LoadScene(MenuScene);
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
            LanDiscovery.StopResponder();
            LanDiscovery.StopSearch();
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

            var mm = MatchManager.Instance;
            bool inLobby = mm == null || mm.Phase.Value == MatchPhase.Lobby;
            if (!inLobby && req.ClientNetworkId != NetworkManager.ServerClientId)
            {
                resp.Approved = false;
                resp.Reason = "Runda już trwa. Spróbuj, gdy wrócą do lobby.";
                return;
            }

            nick = UniqueNick(nick);
            Debug.Log($"[NetSession] approve client={req.ClientNetworkId} payload='{Encoding.UTF8.GetString(req.Payload ?? Array.Empty<byte>())}' -> {nick}");
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
            Leave(string.IsNullOrEmpty(reason) ? "Rozłączono z serwerem." : reason);
        }
    }
}
