using System.Collections.Generic;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Several seekers (killers) per round: the lobby setting <see cref="NetSettings.Seekers"/> (1..players-1), the fair
    /// pick (everybody gets a turn before anybody repeats, last round's seekers not again when avoidable), test switches
    /// (-forceseeker / -forcehider still steer the host, -seekers N presets the setting), spawning side by side at the
    /// seeker spawn, and the one chainsaw returning to its shelf when the seeker carrying it leaves the game.
    /// Every seeker has his own knife, Glock, cooldowns, goo / dust state and inventory (all per NetPlayer).
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Upper bound of the lobby setting (Steam lobby is 16 players, at least one has to hide).</summary>
        public const int MaxSeekersSetting = 15;

        private readonly HashSet<string> _seekerHistory = new();
        private readonly HashSet<ulong> _lastSeekers = new();

        /// <summary>Most seekers a round with <paramref name="players"/> players allows (always at least one hider).</summary>
        public static int MaxSeekersFor(int players) => Mathf.Max(1, players - 1);

        /// <summary>Seekers in the running round as seen on this peer.</summary>
        public static int SeekersNow()
        {
            int n = 0;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) n++;
            return n;
        }

        /// <summary>Nicks of the other seekers (for "you hunt together with ...").</summary>
        public static string OtherSeekerNames(NetPlayer me)
        {
            var names = new List<string>();
            foreach (var p in NetPlayer.All) if (p != null && p != me && p.IsSeeker) names.Add(p.Nick);
            string all = string.Join(", ", names);
            return all.Length > 60 ? all.Substring(0, 57) + "..." : all; // fits a toast (FixedString128Bytes)
        }

        /// <summary>Test switch -seekers N (host): presets the lobby setting.</summary>
        private void ApplySeekerArg()
        {
            if (!IsServer) return;
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-seekers");
            if (i < 0 || i + 1 >= args.Length || !int.TryParse(args[i + 1], out int n)) return;
            var s = Settings.Value;
            s.Seekers = (byte)Mathf.Clamp(n, 1, MaxSeekersSetting);
            Settings.Value = s;
            Debug.Log($"[Seekers] -seekers {n}: setting = {s.Seekers}");
        }

        /// <summary>Server: the reason the round cannot start with this many seekers, or null.</summary>
        private string SeekerCountProblem()
        {
            if (RaceMode) return null;
            int players = 0;
            foreach (var p in NetPlayer.All) if (p != null) players++;
            int want = Settings.Value.SeekerCount;
            if (want <= MaxSeekersFor(players)) return null;
            return $"za dużo zabójców ({want}) na {players} graczy, max {MaxSeekersFor(players)}. Zmień na tablicy (E).";
        }

        /// <summary>
        /// Fair rotation for <paramref name="count"/> seekers: players who have not been the seeker in this turn first,
        /// never last round's seekers when somebody else is available; a new turn starts when everybody had one.
        /// </summary>
        public List<NetPlayer> PickSeekers(List<NetPlayer> players, int count)
        {
            var pool = new List<NetPlayer>(players);
            pool.RemoveAll(x => x == null);
            count = Mathf.Clamp(count, 1, Mathf.Max(1, pool.Count));
            var picked = new List<NetPlayer>();
            while (picked.Count < count && pool.Count > 0)
            {
                var c = pool.FindAll(x => !_seekerHistory.Contains(x.Nick) && !_lastSeekers.Contains(x.OwnerClientId));
                if (c.Count == 0 && !pool.Exists(x => !_seekerHistory.Contains(x.Nick)))
                {
                    _seekerHistory.Clear(); // everybody had a turn: a new one starts
                    c = pool.FindAll(x => !_lastSeekers.Contains(x.OwnerClientId));
                }
                if (c.Count == 0) c = pool.FindAll(x => !_lastSeekers.Contains(x.OwnerClientId));
                if (c.Count == 0) c = pool;
                var pick = c[Random.Range(0, c.Count)];
                picked.Add(pick);
                pool.Remove(pick);
                _seekerHistory.Add(pick.Nick);
            }
            RememberSeekers(picked);
            return picked;
        }

        private void RememberSeekers(List<NetPlayer> seekers)
        {
            _lastSeekers.Clear();
            foreach (var s in seekers) _lastSeekers.Add(s.OwnerClientId);
        }

        /// <summary>Server, round start: who seeks this round (setting, test switches, fair rotation).</summary>
        private List<NetPlayer> ChooseSeekers(List<NetPlayer> players)
        {
            var args = System.Environment.GetCommandLineArgs();
            bool forceHider = System.Array.IndexOf(args, "-forcehider") >= 0 && players.Count > 1;
            bool forceSeeker = System.Array.IndexOf(args, "-forceseeker") >= 0;
            var pickFrom = forceHider ? players.FindAll(x => x.OwnerClientId != NetworkManager.ServerClientId) : players;
            int count = Mathf.Min(Settings.Value.SeekerCount, MaxSeekersFor(players.Count), Mathf.Max(1, pickFrom.Count));
            var seekers = PickSeekers(pickFrom, count);
            var host = NetPlayer.FindByClient(NetworkManager.ServerClientId);
            if (forceSeeker && host != null && !seekers.Contains(host))
            {
                if (seekers.Count > 0)
                {
                    _seekerHistory.Remove(seekers[seekers.Count - 1].Nick); // replaced: still has a turn to come
                    seekers[seekers.Count - 1] = host;
                }
                else seekers.Add(host);
                _seekerHistory.Add(host.Nick);
                RememberSeekers(seekers);
            }
            var nicks = new List<string>();
            foreach (var s in seekers) nicks.Add(s.Nick);
            Debug.Log($"[Match] seekers ({seekers.Count} of {players.Count} players, setting {Settings.Value.SeekerCount}): {string.Join(", ", nicks)}" +
                      $" (this turn so far: {string.Join(", ", _seekerHistory)})");
            return seekers;
        }

        /// <summary>Spot number <paramref name="k"/> of <paramref name="n"/> seekers around the seeker spawn (side by side, free of walls).</summary>
        private static Vector3 SeekerSpot(Transform sp, int k, int n)
        {
            if (n <= 1 || sp == null) return sp != null ? sp.position : Vector3.zero;
            Vector3 right = sp.right, back = -sp.forward;
            var tries = new List<Vector3>();
            float side = (k - (n - 1) * 0.5f) * 1.2f;
            tries.Add(sp.position + right * side);
            tries.Add(sp.position + back * 1.3f + right * side);
            tries.Add(sp.position - right * side);
            tries.Add(sp.position + back * 2.6f + right * side * 0.5f);
            int mask = ~((1 << 2) | (1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9));
            foreach (var p in tries)
            {
                bool blocked = false;
                foreach (var c in Physics.OverlapCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.6f, 0.36f, mask, QueryTriggerInteraction.Ignore))
                    if (c.GetComponentInParent<NetPlayer>() == null) { blocked = true; break; }
                if (!blocked) return p;
            }
            return sp.position;
        }

        /// <summary>
        /// Server, after a player left mid-round: the only chainsaw is gone with him when nobody holds it and it does not
        /// lie on the floor - it goes back on the shelf for the remaining seekers.
        /// </summary>
        private void ServerChainsawLostCheck()
        {
            if (!ChainsawTaken.Value) return;
            foreach (var p in NetPlayer.All) if (p != null && p.HasChainsaw.Value) return;
            for (int i = 0; i < Drops.Count; i++) if (Drops[i].Kind == HotbarItem.Chainsaw) return;
            ChainsawTaken.Value = false;
            Debug.Log("[Seekers] the chainsaw left with its holder - back on the shelf");
        }
    }
}
