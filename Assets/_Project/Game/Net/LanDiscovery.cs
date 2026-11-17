using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>A server found on the local network(s).</summary>
    public class FoundServer
    {
        public string Address;
        public ushort Port;
        public string Name;
        public int Players;
        public bool InLobby;
        public float LastSeen;
        public int PingMs;
    }

    /// <summary>
    /// UDP broadcast discovery. The host answers "HNS?" queries on <see cref="DiscoveryPort"/>;
    /// clients broadcast the query on every IPv4 interface (directed subnet broadcast + 255.255.255.255),
    /// so it works on plain LAN and on any virtual LAN adapter.
    /// </summary>
    public static class LanDiscovery
    {
        public const int DiscoveryPort = 47777;
        private const string Query = "HNS?1";
        private const string Reply = "HNS!1";

        // ---------------------------------------------------------------- host side

        private static UdpClient _responder;
        private static Thread _responderThread;
        private static volatile bool _responding;

        /// <summary>Provides "name|players|port|inLobby" for replies. Called from a worker thread: return cached data only.</summary>
        public static Func<string> ReplyPayload;
        private static volatile string _cachedPayload = "";

        public static void UpdatePayload(string name, int players, ushort port, bool inLobby)
            => _cachedPayload = $"{name.Replace("|", "")}|{players}|{port}|{(inLobby ? 1 : 0)}";

        public static void StartResponder()
        {
            StopResponder();
            try
            {
                _responder = new UdpClient(AddressFamily.InterNetwork);
                _responder.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _responder.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _responding = true;
                _responderThread = new Thread(ResponderLoop) { IsBackground = true, Name = "HNS discovery" };
                _responderThread.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LanDiscovery] responder failed: {e.Message}");
            }
        }

        public static void StopResponder()
        {
            _responding = false;
            try { _responder?.Close(); } catch { /* ignore */ }
            _responder = null;
            _responderThread = null;
        }

        private static void ResponderLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (_responding)
            {
                try
                {
                    byte[] data = _responder.Receive(ref any);
                    if (Encoding.UTF8.GetString(data) != Query) continue;
                    byte[] reply = Encoding.UTF8.GetBytes(Reply + "|" + _cachedPayload);
                    _responder.Send(reply, reply.Length, any);
                }
                catch (Exception)
                {
                    if (!_responding) return;
                }
            }
        }

        // ---------------------------------------------------------------- client side

        private static UdpClient _searcher;
        private static Thread _searchThread;
        private static volatile bool _searching;
        private static readonly ConcurrentQueue<(string ip, string payload, long ticks)> Incoming = new();
        private static readonly Dictionary<string, FoundServer> Found = new();
        private static readonly Dictionary<string, long> SentAt = new();
        private static float _nextQuery;

        public static IReadOnlyCollection<FoundServer> Servers => Found.Values;

        public static void StartSearch()
        {
            StopSearch();
            Found.Clear();
            try
            {
                _searcher = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
                _searching = true;
                _searchThread = new Thread(SearchLoop) { IsBackground = true, Name = "HNS search" };
                _searchThread.Start();
                _nextQuery = 0f;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LanDiscovery] search failed: {e.Message}");
            }
        }

        public static void StopSearch()
        {
            _searching = false;
            try { _searcher?.Close(); } catch { /* ignore */ }
            _searcher = null;
        }

        /// <summary>Call every frame from the menu: sends queries every 1.5 s, collects replies, drops stale servers.</summary>
        public static void Tick()
        {
            if (_searcher == null) return;
            float now = Time.unscaledTime;
            if (now >= _nextQuery)
            {
                _nextQuery = now + 1.5f;
                SendQueries();
            }

            while (Incoming.TryDequeue(out var msg))
            {
                string[] p = msg.payload.Split('|');
                if (p.Length < 5 || p[0] != Reply) continue;
                if (!ushort.TryParse(p[3], out ushort port)) continue;
                // One server answers on every interface: merge by name + port, prefer loopback (same PC).
                string key = p[1] + ":" + port;
                if (!Found.TryGetValue(key, out var s)) Found[key] = s = new FoundServer { Address = msg.ip, Port = port };
                else if (msg.ip == "127.0.0.1") s.Address = msg.ip;
                s.Name = p[1];
                int.TryParse(p[2], out s.Players);
                s.InLobby = p[4] == "1";
                s.LastSeen = now;
                if (SentAt.TryGetValue("q", out long sent)) s.PingMs = (int)((msg.ticks - sent) / TimeSpan.TicksPerMillisecond);
            }

            var stale = new List<string>();
            foreach (var kv in Found) if (now - kv.Value.LastSeen > 5f) stale.Add(kv.Key);
            foreach (var k in stale) Found.Remove(k);
        }

        private static void SendQueries()
        {
            byte[] q = Encoding.UTF8.GetBytes(Query);
            SentAt["q"] = DateTime.UtcNow.Ticks;
            foreach (var target in BroadcastTargets())
            {
                try { _searcher.Send(q, q.Length, new IPEndPoint(target, DiscoveryPort)); }
                catch { /* interface may be down */ }
            }
        }

        private static void SearchLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (_searching)
            {
                try
                {
                    byte[] data = _searcher.Receive(ref any);
                    Incoming.Enqueue((any.Address.ToString(), Encoding.UTF8.GetString(data), DateTime.UtcNow.Ticks));
                }
                catch (Exception)
                {
                    if (!_searching) return;
                }
            }
        }

        /// <summary>255.255.255.255, loopback and the directed broadcast address of every up IPv4 interface.</summary>
        public static List<IPAddress> BroadcastTargets()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            foreach (var (ip, mask) in LocalIPv4())
            {
                byte[] a = ip.GetAddressBytes(), m = mask.GetAddressBytes();
                var b = new byte[4];
                for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
                var bc = new IPAddress(b);
                if (!list.Contains(bc)) list.Add(bc);
            }
            return list;
        }

        /// <summary>IPv4 addresses of all active non-loopback interfaces (shown to the host so friends can join by IP).</summary>
        public static List<(IPAddress ip, IPAddress mask)> LocalIPv4()
        {
            var result = new List<(IPAddress, IPAddress)>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var mask = ua.IPv4Mask ?? IPAddress.Parse("255.255.255.0");
                        if (mask.Equals(IPAddress.Any)) mask = IPAddress.Parse("255.255.255.0");
                        result.Add((ua.Address, mask));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LanDiscovery] interfaces: {e.Message}");
            }
            return result;
        }
    }
}
