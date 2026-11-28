using System.Collections.Generic;
using System.Linq;
using System.Text;
using HideAndSeek.Game.Net;

namespace HideAndSeek.Game.UI
{
    /// <summary>Console commands give / spawn / world and the item names they understand (Polish, without accents too).</summary>
    public static partial class DevConsoleCommands
    {
        private static readonly string[] ObjectNames = { "gokart", "gokart2" };

        /// <summary>Typed name (accents and case ignored) -> item. The first name of each item is shown in help / Tab.</summary>
        private static readonly (string name, HotbarItem item)[] ItemTable =
        {
            ("apteczka", HotbarItem.Medkit), ("medkit", HotbarItem.Medkit),
            ("strzykawka", HotbarItem.Syringe), ("adrenalina", HotbarItem.Syringe), ("syringe", HotbarItem.Syringe),
            ("pret", HotbarItem.Rod), ("rod", HotbarItem.Rod),
            ("pila", HotbarItem.Chainsaw), ("chainsaw", HotbarItem.Chainsaw),
            ("glock", HotbarItem.Glock), ("pistolet", HotbarItem.Glock),
            ("pistolecik", HotbarItem.ToyGun), ("glut", HotbarItem.ToyGun), ("toygun", HotbarItem.ToyGun),
            ("noga", HotbarItem.SeveredLeg), ("leg", HotbarItem.SeveredLeg),
            ("drut", HotbarItem.Wire), ("wire", HotbarItem.Wire),
            ("kanister", HotbarItem.FuelCan), ("paliwo", HotbarItem.FuelCan), ("fuelcan", HotbarItem.FuelCan),
            ("akumulator", HotbarItem.CarBattery), ("bateria", HotbarItem.CarBattery), ("battery", HotbarItem.CarBattery),
            ("bezpiecznik0", HotbarItem.Fuse0), ("bezpiecznik1", HotbarItem.Fuse1), ("bezpiecznik2", HotbarItem.Fuse2),
            ("bezpiecznik3", HotbarItem.Fuse3), ("bezpiecznik4", HotbarItem.Fuse4), ("bezpiecznik5", HotbarItem.Fuse5),
            ("bezpiecznik6", HotbarItem.Fuse6), ("bezpiecznik7", HotbarItem.Fuse7),
        };

        private static IEnumerable<string> ItemNames()
        {
            var seen = new HashSet<HotbarItem>();
            foreach (var (name, item) in ItemTable)
                if (seen.Add(item)) yield return name;
        }

        private static string Plain(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant())
                sb.Append(c switch { 'ą' => 'a', 'ć' => 'c', 'ę' => 'e', 'ł' => 'l', 'ń' => 'n', 'ó' => 'o', 'ś' => 's', 'ź' => 'z', 'ż' => 'z', '_' => '\0', '-' => '\0', _ => c });
            return sb.ToString().Replace("\0", "");
        }

        private static bool ParseItem(string s, out HotbarItem item)
        {
            string p = Plain(s);
            foreach (var (name, it) in ItemTable) if (name == p) { item = it; return true; }
            var starts = ItemTable.Where(t => t.name.StartsWith(p)).Select(t => t.item).Distinct().ToList();
            if (starts.Count == 1) { item = starts[0]; return true; }
            item = HotbarItem.None;
            Err(starts.Count > 1 ? $"'{s}' pasuje do kilku przedmiotów - dopisz więcej liter." : $"Nie znam przedmiotu '{s}'. Przedmioty: {string.Join(", ", ItemNames())}");
            return false;
        }

        private static bool Server(out MatchManager mm, out NetPlayer me)
        {
            mm = MatchManager.Instance;
            me = NetPlayer.Local;
            if (mm == null || me == null || !DevConsole.IsHost) { Err("Najpierw załóż grę (konsola działa u hosta)."); return false; }
            return true;
        }

        /// <summary>give [item] [uses] [who]</summary>
        private static void Give(string[] a)
        {
            if (!Server(out var mm, out var me)) return;
            if (a.Length < 2) { Err("Użycie: give [przedmiot] [użycia] [kto]"); return; }
            if (!ParseItem(a[1], out var item)) return;
            int uses = 0;
            int whoAt = 2;
            if (a.Length > 2 && int.TryParse(a[2], out uses)) whoAt = 3;
            var who = a.Length > whoAt ? Many(a[whoAt], me) : new List<NetPlayer> { me };
            if (who == null) return;
            foreach (var p in who) Out(mm.ServerConsoleGive(p, item, uses));
        }

        /// <summary>spawn [item/object] [count] (in front of me)</summary>
        private static void Spawn(string[] a)
        {
            if (!Server(out var mm, out var me)) return;
            if (a.Length < 2) { Err("Użycie: spawn [przedmiot/gokart/gokart2] [ilość]"); return; }
            string what = Plain(a[1]);
            int count = a.Length > 2 && int.TryParse(a[2], out int c) ? c : 1;
            if (what == "gokart" || what == "kart" || what == "gokart2" || what == "dwuosobowy" || what == "gokartdwuosobowy")
            {
                bool duo = what != "gokart" && what != "kart";
                for (int i = 0; i < System.Math.Max(1, System.Math.Min(count, 3)); i++) Out(mm.ServerConsoleSpawnKart(me, duo));
                return;
            }
            if (!ParseItem(a[1], out var item)) return;
            Out(mm.ServerConsoleSpawnItem(me, item, 0, count));
        }

        /// <summary>world tp [lobby/garaz] [@a]</summary>
        private static void World(string[] a)
        {
            if (!Server(out var mm, out var me)) return;
            if (a.Length < 3 || a[1].ToLowerInvariant() != "tp") { Err("Użycie: world tp [lobby/garaz] [@a]"); return; }
            string map = Plain(a[2]);
            bool garage = map.StartsWith("gara") || map.StartsWith("park") || map == "mapa" || map == "gra" || map.StartsWith("podziem");
            bool lobby = map.StartsWith("lob");
            if (!garage && !lobby) { Err($"Nie znam mapy '{a[2]}'. Mapy: lobby, garaz."); return; }
            if (a.Length > 3 && !IsAll(a[3]))
            {
                Err("Wszyscy gracze są zawsze na tej samej mapie (sieć ładuje jedną mapę dla wszystkich) - użyj @a / wszyscy, a jedną osobę przenieś przez tp.");
                return;
            }
            Out(mm.ServerConsoleWorld(garage));
        }
    }
}
