using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Steam (Facepunch.Steamworks, AppID 480 "Spacewar" for testing): client init once per process, Steam lobbies
    /// (create / search / join / leave / data), friend invites and invite acceptance. The game keeps working without
    /// Steam (plain UDP / LAN): every call here is safe when <see cref="Ready"/> is false.
    /// </summary>
    public static class SteamService
    {
        public const uint AppId = 480;
        public const int MaxMembers = 16;
        private const string GameKey = "hns_game";

        public static bool Ready { get; private set; }
        public static string Error { get; private set; }
        public static Lobby? CurrentLobby { get; private set; }
        public static bool InLobby => CurrentLobby.HasValue;
        public static string PersonaName => Ready ? SteamClient.Name : "";
        public static ulong MySteamId => Ready ? SteamClient.SteamId.Value : 0;

        /// <summary>A friend invited us (or we clicked "Join game" in the friends list): lobby id to join.</summary>
        public static event Action<ulong> JoinRequested;

        private static bool _initTried;

        public static void Init()
        {
            if (_initTried) return;
            _initTried = true;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-nosteam") >= 0)
            {
                Error = "wyłączony (-nosteam)";
                return;
            }
            try
            {
                SteamClient.Init(AppId, false);
                Ready = SteamClient.IsValid;
                if (!Ready)
                {
                    Error = "Steam nie odpowiada";
                    return;
                }
                SteamNetworkingUtils.InitRelayNetworkAccess();
                SteamFriends.OnGameLobbyJoinRequested += (lobby, friend) =>
                {
                    Debug.Log($"[Steam] join request lobby={lobby.Id} from {friend.Value}");
                    JoinRequested?.Invoke(lobby.Id.Value);
                };
                SteamFriends.OnGameRichPresenceJoinRequested += (friend, connect) =>
                {
                    // "+connect_lobby <id>" style strings from the friends list.
                    var id = ParseLobbyArg(connect);
                    if (id != 0) JoinRequested?.Invoke(id);
                };
                Debug.Log($"[Steam] ready: {SteamClient.Name} ({SteamClient.SteamId}), app {AppId}");
            }
            catch (Exception e)
            {
                Ready = false;
                Error = "Steam niedostępny (" + e.Message + ")";
                Debug.LogWarning("[Steam] init failed: " + e.Message);
            }
        }

        /// <summary>Pumps Steam callbacks (call every frame).</summary>
        public static void Tick()
        {
            if (!Ready) return;
            try { SteamClient.RunCallbacks(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static void Shutdown()
        {
            if (!Ready) return;
            LeaveLobby();
            try { SteamClient.Shutdown(); } catch { /* ignore */ }
            Ready = false;
        }

        /// <summary>Lobby id from a "+connect_lobby 1234" argument string (0 = none).</summary>
        public static ulong ParseLobbyArg(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
                if (parts[i] == "+connect_lobby" && ulong.TryParse(parts[i + 1], out ulong id)) return id;
            return 0;
        }

        // ------------------------------------------------------------------ lobbies

        public static async Task<bool> CreateLobby(string name)
        {
            if (!Ready) return false;
            LeaveLobby();
            var lobby = await SteamMatchmaking.CreateLobbyAsync(MaxMembers);
            if (!lobby.HasValue) return false;
            var l = lobby.Value;
            l.SetPublic();
            l.SetJoinable(true);
            l.SetData(GameKey, "1");
            l.SetData("name", name);
            l.SetData("ver", Application.version);
            l.SetData("phase", "lobby");
            l.SetData("players", "1");
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + l.Id.Value);
            SteamFriends.SetRichPresence("status", "Hide and Seek - lobby");
            CurrentLobby = l;
            Debug.Log($"[Steam] lobby created {l.Id}");
            return true;
        }

        public struct LobbyInfo
        {
            public ulong Id;
            public string Name;
            public string Version;
            public bool InLobby;
            public int Members;
            public int Max;
            public bool Friend;
        }

        /// <summary>Hide and Seek lobbies: friends' first, then the public list (worldwide, AppID 480 is shared).</summary>
        public static async Task<List<LobbyInfo>> FindLobbies()
        {
            var result = new List<LobbyInfo>();
            if (!Ready) return result;
            var seen = new HashSet<ulong>();
            try
            {
                var list = await SteamMatchmaking.LobbyList.WithKeyValue(GameKey, "1").FilterDistanceWorldwide().WithMaxResults(30).RequestAsync();
                var friendLobbies = new HashSet<ulong>();
                foreach (var f in SteamFriends.GetFriends())
                {
                    try
                    {
                        var gi = f.GameInfo;
                        if (gi.HasValue && gi.Value.Lobby.HasValue) friendLobbies.Add(gi.Value.Lobby.Value.Id.Value);
                    }
                    catch { /* friend without game info */ }
                }
                if (list != null)
                    foreach (var l in list)
                    {
                        if (!seen.Add(l.Id.Value)) continue;
                        result.Add(new LobbyInfo
                        {
                            Id = l.Id.Value,
                            Name = l.GetData("name"),
                            Version = l.GetData("ver"),
                            InLobby = l.GetData("phase") != "game",
                            Members = l.MemberCount,
                            Max = l.MaxMembers,
                            Friend = friendLobbies.Contains(l.Id.Value),
                        });
                    }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Steam] lobby search failed: " + e.Message);
            }
            return result.OrderByDescending(r => r.Friend).ThenByDescending(r => r.InLobby).ToList();
        }

        public enum JoinResult { Ok, NoSteam, Failed, Full, WrongVersion, InGame }

        /// <summary>Enters the Steam lobby; on success returns the host's SteamID (lobby owner).</summary>
        public static async Task<(JoinResult result, ulong hostId, string message)> JoinLobby(ulong lobbyId)
        {
            if (!Ready) return (JoinResult.NoSteam, 0, "Steam nie jest uruchomiony.");
            LeaveLobby();
            var lobby = new Lobby(lobbyId);
            var enter = await lobby.Join();
            if (enter != RoomEnter.Success)
                return (enter == RoomEnter.Full ? JoinResult.Full : JoinResult.Failed, 0, enter == RoomEnter.Full ? "Lobby jest pełne." : "Nie udało się wejść do lobby Steam (" + enter + ").");
            string ver = lobby.GetData("ver");
            if (!string.IsNullOrEmpty(ver) && ver != Application.version)
            {
                lobby.Leave();
                return (JoinResult.WrongVersion, 0, $"Inna wersja gry: masz {Application.version}, serwer ma {ver}. " + (UpdateChecker.Compare(ver, Application.version) > 0 ? "Zaktualizuj grę (przycisk w menu)." : "Host musi zaktualizować grę."));
            }
            if (lobby.GetData("phase") == "game")
            {
                lobby.Leave();
                return (JoinResult.InGame, 0, "Runda już trwa. Spróbuj, gdy wrócą do lobby.");
            }
            CurrentLobby = lobby;
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + lobbyId);
            return (JoinResult.Ok, lobby.Owner.Id.Value, null);
        }

        public static void LeaveLobby()
        {
            if (!CurrentLobby.HasValue) return;
            try { CurrentLobby.Value.Leave(); } catch { /* ignore */ }
            CurrentLobby = null;
            if (Ready)
            {
                try { SteamFriends.ClearRichPresence(); } catch { /* ignore */ }
            }
        }

        /// <summary>Host: keeps the lobby data in sync (phase, player count).</summary>
        public static void UpdateLobby(bool inLobbyPhase, int players)
        {
            if (!CurrentLobby.HasValue || !Ready) return;
            var l = CurrentLobby.Value;
            if (l.Owner.Id != SteamClient.SteamId) return;
            string phase = inLobbyPhase ? "lobby" : "game";
            if (l.GetData("phase") != phase)
            {
                l.SetData("phase", phase);
                l.SetJoinable(inLobbyPhase);
                SteamFriends.SetRichPresence("status", inLobbyPhase ? "Hide and Seek - lobby" : "Hide and Seek - runda trwa");
            }
            string p = players.ToString();
            if (l.GetData("players") != p) l.SetData("players", p);
        }

        // ------------------------------------------------------------------ friends / invites

        public struct FriendInfo
        {
            public ulong Id;
            public string Name;
            public bool Online;
            public bool Playing;
        }

        public static List<FriendInfo> Friends()
        {
            var list = new List<FriendInfo>();
            if (!Ready) return list;
            try
            {
                foreach (var f in SteamFriends.GetFriends())
                {
                    bool playing = false;
                    try { playing = f.IsPlayingThisGame; } catch { /* ignore */ }
                    list.Add(new FriendInfo { Id = f.Id.Value, Name = f.Name, Online = f.IsOnline || playing, Playing = playing });
                }
            }
            catch (Exception e) { Debug.LogWarning("[Steam] friends: " + e.Message); }
            return list.OrderByDescending(f => f.Playing).ThenByDescending(f => f.Online).ThenBy(f => f.Name).ToList();
        }

        public static bool Invite(ulong friendId)
        {
            if (!CurrentLobby.HasValue) return false;
            return CurrentLobby.Value.InviteFriend(new SteamId { Value = friendId });
        }

        /// <summary>Steam overlay invite dialog for the current lobby.</summary>
        public static void OpenInviteOverlay()
        {
            if (CurrentLobby.HasValue) SteamFriends.OpenGameInviteOverlay(CurrentLobby.Value.Id);
        }
    }
}
