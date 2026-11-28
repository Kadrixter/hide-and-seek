using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Commands of the debug console (<see cref="DevConsole"/>). Player targets: @a (everybody), @s (me), @p (the nearest
    /// other player), @r (a random other player), a nick (or its start, case ignored) or #id (NetworkObjectId).
    /// Commands that move other players run on the host only (the host is the server and sends the teleports).
    /// </summary>
    public static class DevConsoleCommands
    {
        private static readonly string[] Names = { "help", "clear", "tp", "list", "pos", "fps" };

        public static void Execute(string line)
        {
            var args = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            string cmd = args[0].ToLowerInvariant().TrimStart('/');
            switch (cmd)
            {
                case "help": case "?": Help(); break;
                case "clear": case "cls": DevConsole.Clear(); break;
                case "tp": case "teleport": Tp(args); break;
                case "list": case "players": List(); break;
                case "pos": Pos(); break;
                case "fps": Fps(args); break;
                default: Err($"Nie znam komendy '{cmd}'. Wpisz help."); break;
            }
        }

        private static void Help()
        {
            Out("<b>Komendy</b> (Tab uzupełnia nick, strzałki - historia):");
            Out("  <b>tp @a</b> - wszyscy do mnie");
            Out("  <b>tp [cel]</b> - ja do gracza");
            Out("  <b>tp [kto] [cel]</b> - gracz(e) do gracza, np. tp @a Kadrix");
            Out("  <b>tp [kto] x y z</b> - gracz(e) na współrzędne (~ = obecna wartość, ~2 = +2)");
            Out("  <b>list</b> - gracze: id, rola, stan, ping, pozycja");
            Out("  <b>pos</b> - moja pozycja");
            Out("  <b>fps [n]</b> - limit klatek (0 = bez limitu)");
            Out("  <b>clear</b> - wyczyść konsolę");
            Out("Cele: @a wszyscy, @s ja, @p najbliższy, @r losowy, nick (początek wystarczy), #id. tp działa tylko u hosta.");
        }

        // ================================================================ tp

        private static void Tp(string[] a)
        {
            var me = NetPlayer.Local;
            if (me == null) { Err("Najpierw wejdź do gry."); return; }
            if (!IsHost) { Err("tp działa tylko u hosta (on jest serwerem)."); return; }
            if (a.Length < 2) { Err("Użycie: tp @a | tp [cel] | tp [kto] [cel] | tp [kto] x y z"); return; }

            // tp x y z = me to coordinates
            if (a.Length == 4 && LooksLikeCoord(a[1]))
            {
                if (!Coords(a, 1, me.transform.position, out var at)) return;
                Move(new List<NetPlayer> { me }, at, me.transform.eulerAngles.y, false);
                return;
            }
            if (a.Length == 2)
            {
                // tp @a = everybody to me; tp <player> = me to him.
                if (a[1] == "@a" || a[1] == "@e")
                {
                    var who = NetPlayer.All.Where(p => p != null && p.IsSpawned && p != me).ToList();
                    if (who.Count == 0) { Out("Nikogo poza tobą."); return; }
                    Move(who, me.transform.position, me.transform.eulerAngles.y, true);
                    return;
                }
                var dest = One(a[1], me);
                if (dest == null) return;
                if (dest == me) { Out("Już tu jesteś :p"); return; }
                Move(new List<NetPlayer> { me }, dest.transform.position, dest.transform.eulerAngles.y, false);
                return;
            }
            var targets = Many(a[1], me);
            if (targets == null) return;
            if (a.Length == 3)
            {
                var dest = One(a[2], me);
                if (dest == null) return;
                targets.Remove(dest);
                if (targets.Count == 0) { Out("Nie ma kogo przenieść."); return; }
                Move(targets, dest.transform.position, dest.transform.eulerAngles.y, true);
                return;
            }
            if (a.Length == 5)
            {
                if (!Coords(a, 2, targets[0].transform.position, out var at)) return;
                Move(targets, at, me.transform.eulerAngles.y, targets.Count > 1);
                return;
            }
            Err("Użycie: tp @a | tp [cel] | tp [kto] [cel] | tp [kto] x y z");
        }

        /// <summary>Teleports through the owner (server -> owner RPC); a group is spread on a small ring round the spot.</summary>
        private static void Move(List<NetPlayer> who, Vector3 at, float yaw, bool ring)
        {
            for (int i = 0; i < who.Count; i++)
            {
                Vector3 pos = at;
                if (ring)
                {
                    float ang = (i / (float)Mathf.Max(1, who.Count)) * Mathf.PI * 2f;
                    float r = who.Count == 1 ? 1.1f : 1.2f + 0.08f * who.Count;
                    pos += new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                }
                var p = who[i];
                if (p.IsOwner) p.TeleportLocal(pos, yaw);
                else p.TeleportRpc(pos, yaw);
            }
            Out($"Przeniesiono: {string.Join(", ", who.Select(p => p.Nick))} -> ({at.x:0.0}, {at.y:0.0}, {at.z:0.0})");
        }

        // ================================================================ list / pos / fps

        private static void List()
        {
            if (NetPlayer.All.Count == 0) { Out("Brak graczy (nie jesteś w grze)."); return; }
            var mm = MatchManager.Instance;
            Out($"Gracze: {NetPlayer.All.Count}{(mm != null ? $", faza {mm.Phase.Value}" : "")}");
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                bool host = p.OwnerClientId == NetworkManager.ServerClientId;
                Vector3 q = p.transform.position;
                Out($"  #{p.NetworkObjectId}  <b>{p.Nick}</b>{(p.IsOwner ? " (ty)" : "")}  {p.Role.Value} / {p.Life.Value}  " +
                    $"{(host ? "host" : p.PingMs.Value + " ms")}  ({q.x:0.0}, {q.y:0.0}, {q.z:0.0})");
            }
        }

        private static void Pos()
        {
            var me = NetPlayer.Local;
            if (me == null) { Err("Najpierw wejdź do gry."); return; }
            Vector3 q = me.transform.position;
            Out($"Pozycja: {q.x:0.00} {q.y:0.00} {q.z:0.00}, obrót {me.transform.eulerAngles.y:0}°");
        }

        private static void Fps(string[] a)
        {
            if (a.Length < 2 || !int.TryParse(a[1], out int n) || n < 0) { Out($"Limit: {(Application.targetFrameRate <= 0 ? "brak" : Application.targetFrameRate.ToString())}. Użycie: fps [n]"); return; }
            if (n > 0) QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = n == 0 ? -1 : Mathf.Max(10, n);
            Out(n == 0 ? "Bez limitu klatek." : $"Limit klatek: {Application.targetFrameRate}.");
        }

        // ================================================================ targets

        private static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private static List<NetPlayer> Many(string sel, NetPlayer me)
        {
            if (sel == "@a" || sel == "@e") return NetPlayer.All.Where(p => p != null && p.IsSpawned).ToList();
            var one = One(sel, me);
            return one != null ? new List<NetPlayer> { one } : null;
        }

        private static NetPlayer One(string sel, NetPlayer me)
        {
            var all = NetPlayer.All.Where(p => p != null && p.IsSpawned).ToList();
            var others = all.Where(p => p != me).ToList();
            switch (sel)
            {
                case "@s": return me;
                case "@p":
                    if (others.Count == 0) { Err("Nikogo poza tobą."); return null; }
                    return others.OrderBy(p => (p.transform.position - me.transform.position).sqrMagnitude).First();
                case "@r":
                    if (others.Count == 0) { Err("Nikogo poza tobą."); return null; }
                    return others[Random.Range(0, others.Count)];
                case "@a": case "@e":
                    Err("Tu potrzebny jeden gracz (nie @a)."); return null;
            }
            if (sel.StartsWith("#") && ulong.TryParse(sel.Substring(1), out ulong id))
            {
                var byId = all.FirstOrDefault(p => p.NetworkObjectId == id);
                if (byId == null) Err($"Nie ma gracza #{id}.");
                return byId;
            }
            var exact = all.FirstOrDefault(p => string.Equals(p.Nick, sel, System.StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            var starts = all.Where(p => p.Nick.StartsWith(sel, System.StringComparison.OrdinalIgnoreCase)).ToList();
            if (starts.Count == 1) return starts[0];
            if (starts.Count > 1) { Err($"Pasuje kilku: {string.Join(", ", starts.Select(p => p.Nick))}."); return null; }
            var contains = all.Where(p => p.Nick.IndexOf(sel, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (contains.Count == 1) return contains[0];
            Err($"Nie ma gracza '{sel}'. Wpisz list.");
            return null;
        }

        private static bool LooksLikeCoord(string s) => s.StartsWith("~") || float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        private static bool Coords(string[] a, int from, Vector3 cur, out Vector3 v)
        {
            v = cur;
            for (int i = 0; i < 3; i++)
            {
                string s = a[from + i].Replace(',', '.');
                float baseV = cur[i], val;
                if (s.StartsWith("~"))
                {
                    string rest = s.Substring(1);
                    val = baseV + (rest.Length == 0 ? 0f : float.Parse(rest, NumberStyles.Float, CultureInfo.InvariantCulture));
                }
                else if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                {
                    Err($"Zła współrzędna '{a[from + i]}'.");
                    return false;
                }
                v[i] = val;
            }
            return true;
        }

        /// <summary>Tab: completes the command name or the last word as a nick.</summary>
        public static string Complete(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            int sp = line.LastIndexOf(' ');
            string head = sp >= 0 ? line.Substring(0, sp + 1) : "", word = sp >= 0 ? line.Substring(sp + 1) : line;
            if (word.Length == 0) return line;
            IEnumerable<string> pool = sp < 0 ? Names : NetPlayer.All.Where(p => p != null).Select(p => p.Nick).Concat(new[] { "@a", "@s", "@p", "@r" });
            var hits = pool.Where(n => n.StartsWith(word, System.StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count == 1) return head + hits[0] + " ";
            if (hits.Count > 1) Out(string.Join("   ", hits));
            return line;
        }

        private static void Out(string s) => DevConsole.Print(s);
        private static void Err(string s) => DevConsole.Print("<color=#ff7a70>" + s + "</color>");
    }
}
