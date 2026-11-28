using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server side of the debug console (<see cref="DevConsoleCommands"/>, host only): give an item straight into a
    /// player's inventory, throw items on the floor in front of a player, bring a free go-kart, and switch the whole
    /// session between the lobby and the garage map.
    /// </summary>
    public partial class MatchManager
    {
        private bool ConsoleRound => Phase.Value == MatchPhase.Hiding || Phase.Value == MatchPhase.Seeking;

        /// <summary>Items the console may not hand out (tied to a physical car / phone on the map, or the seeker's fixed knife).</summary>
        public static bool ConsoleItemAllowed(HotbarItem item, out string why)
        {
            why = null;
            switch (item)
            {
                case HotbarItem.None: why = "brak przedmiotu"; return false;
                case HotbarItem.RcCar: case HotbarItem.RcRemote: why = "auto RC i pilot są związane z autami na mapie"; return false;
                case HotbarItem.Phone: why = "telefony są rozdawane przez mapę"; return false;
                case HotbarItem.Knife: why = "nóż to stała broń szukającego"; return false;
            }
            var def = ItemDefs.Get(item);
            if (def == null || def.Give == null) { why = "tego przedmiotu nie da się dać"; return false; }
            return true;
        }

        /// <summary>The extra byte of an item for <paramref name="uses"/> (0 = the item's default / full).</summary>
        private static byte ConsoleExtra(HotbarItem item, int uses)
        {
            switch (item)
            {
                case HotbarItem.ToyGun: return (byte)Mathf.Clamp(uses <= 0 ? ToyTuning.Shots : uses, 1, 255);
                case HotbarItem.Chainsaw: return uses <= 0 ? (byte)0 : SawFuel.Pack(Mathf.Min(uses, SawFuel.Full));
                case HotbarItem.CarBattery: return (byte)Mathf.Clamp(uses <= 0 ? 100 : uses, 0, 100);
                default: return 0;
            }
        }

        /// <summary>Server: <paramref name="uses"/> = how many (stacking items) or uses / shots / fuel / charge (items that keep one).</summary>
        public string ServerConsoleGive(NetPlayer p, HotbarItem item, int uses)
        {
            if (!IsServer || p == null) return "brak gracza";
            if (!ConsoleRound) return "przedmioty działają tylko w rundzie (na mapie)";
            if (!ConsoleItemAllowed(item, out string why)) return why;
            var def = ItemDefs.Get(item);
            int given = 0;
            if (def.MaxStack > 1)
            {
                int n = Mathf.Clamp(uses <= 0 ? 1 : uses, 1, 50);
                for (int i = 0; i < n && Inventory.HasRoomFor(p, item); i++)
                    if (def.Give(p, 0)) given++;
                if (given == 0) return $"{p.Nick}: brak miejsca na {def.Name}";
                return given < n ? $"{p.Nick}: +{given} {def.Name} (więcej się nie zmieści, max {def.MaxStack})" : $"{p.Nick}: +{given} {def.Name}";
            }
            if (!Inventory.HasRoomFor(p, item)) return $"{p.Nick}: brak miejsca (albo już ma {def.Name})";
            if (!def.Give(p, ConsoleExtra(item, uses))) return $"{p.Nick}: nie udało się dać {def.Name}";
            if (item == HotbarItem.Glock && uses > 0) p.Ammo.Value = (byte)Mathf.Clamp(uses, 0, 255);
            return $"{p.Nick}: {def.Name}{(uses > 0 ? $" ({uses})" : "")}";
        }

        /// <summary>Server: <paramref name="count"/> items thrown on the floor in front of <paramref name="at"/>.</summary>
        public string ServerConsoleSpawnItem(NetPlayer at, HotbarItem item, int uses, int count)
        {
            if (!IsServer || at == null) return "brak gracza";
            if (!ConsoleRound) return "przedmioty na ziemi są tylko w rundzie (na mapie)";
            if (!ConsoleItemAllowed(item, out string why)) return why;
            count = Mathf.Clamp(count, 1, 20);
            Vector3 fwd = at.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, (i - (count - 1) * 0.5f) * 12f, 0f) * fwd;
                ThrowDrop(at, item, ConsoleExtra(item, uses), dir, 0.6f, 1.2f, 2.6f, 1.4f, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f));
            }
            return $"{count} x {ItemDefs.Name(item)} przed {at.Nick}";
        }

        /// <summary>Server: a free go-kart (two-seater with <paramref name="duo"/>) stands 2.5 m in front of <paramref name="at"/>.</summary>
        public string ServerConsoleSpawnKart(NetPlayer at, bool duo)
        {
            if (!IsServer || at == null) return "brak gracza";
            if (Phase.Value != MatchPhase.Lobby || !InLobbyScene) return "gokarty są tylko w lobby (na torze)";
            Vector3 fwd = at.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 pos = at.transform.position + fwd * 2.5f;
            if (!LobbyKart.OnPad(pos)) return "gokart może stać tylko na torze (za bramą w lobby)";
            var all = KartsVar.Value;
            int pick = -1;
            for (int i = 0; i < DriftTrackLayout.KartCount; i++)
            {
                var s = all.Get(i);
                if (s.Driven || s.Carries || DriftTrackLayout.IsDuo(i) != duo) continue;
                pick = i;
                break;
            }
            if (pick < 0) return duo ? "wszystkie dwuosobowe gokarty są zajęte" : "wszystkie gokarty są zajęte";
            var k = all.Get(pick);
            k.Pos = new Vector3(pos.x, at.transform.position.y, pos.z);
            k.Yaw = Mathf.Repeat(at.transform.eulerAngles.y, 360f);
            k.Epoch++;
            KartsVar.Value = all.With(pick, k);
            var l = KartLivery.Of(pick);
            return $"gokart {l.Name} nr {l.Number} przed {at.Nick}";
        }

        /// <summary>Server: the whole session to the lobby or to the garage map (every player is always on the same map).</summary>
        public string ServerConsoleWorld(bool garage)
        {
            if (!IsServer) return "tylko host";
            if (garage)
            {
                if (Phase.Value != MatchPhase.Lobby) return "już jesteście w grze (garaż)";
                StartMatch();
                return "start rundy - wszyscy idą do garażu";
            }
            if (Phase.Value == MatchPhase.Lobby) return "już jesteście w lobby";
            BackToLobby("console");
            return "powrót do lobby";
        }
    }
}
