using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Pulling a rod out of rubble (server): E starts a 5 s pull (<see cref="RodPullTimeline"/>) written into the
    /// rod's state (puller + start time), so every client plays it from the same clock, late joiners too
    /// (<see cref="RodPullDriver"/>). Moving away, attacking, being attacked or downed, or the round ending stops it;
    /// the yanks already done stay done - the rod stays half out and the next pull is shorter. At the end the rod
    /// goes into the inventory. Also: which cars leak fluid this round (<see cref="LeakSeed"/>).
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Seed of the fluid leaks under the cars for this round (0 = default picture).</summary>
        public readonly NetworkVariable<int> LeakSeed = new();

        private readonly Dictionary<int, Coroutine> _rodPulls = new();
        private readonly HashSet<ulong> _rodPullCancel = new();
        private int _leakSeedShown = int.MinValue;

        /// <summary>Server: starts pulling rod index <paramref name="i"/> (checks already done by ServerPickupRod).</summary>
        private void ServerStartRodPull(NetPlayer pl, int i)
        {
            var r = Rods[i];
            if (r.BeingPulled) { pl.ToastRpc("Ktoś już wyciąga ten pręt.", 2f); return; }
            if (pl.Life.Value != LifeState.Alive || pl.Busy.Value) return;
            foreach (var o in Rods) if (o.Puller == pl.NetworkObjectId) return;
            if (Vector3.Distance(Flat(pl.transform.position), Flat(r.Stand)) > 2.2f) return;
            r.Puller = pl.NetworkObjectId;
            r.PullStart = Now;
            Rods[i] = r;
            _rodPullCancel.Remove(pl.NetworkObjectId);
            if (_rodPulls.TryGetValue(r.Id, out var old) && old != null) StopCoroutine(old);
            _rodPulls[r.Id] = StartCoroutine(RodPullRoutine(pl, r.Id, r.Done));
            Debug.Log($"[RodPull] {pl.Nick} pulls rod {r.Id} (yanks done before: {r.Done})");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Owner asks to stop (moved, jumped, attacked).</summary>
        public void ServerCancelRodPull(NetPlayer pl)
        {
            if (pl != null) _rodPullCancel.Add(pl.NetworkObjectId);
        }

        private int RodIndex(int id)
        {
            for (int i = 0; i < Rods.Count; i++) if (Rods[i].Id == id) return i;
            return -1;
        }

        private IEnumerator RodPullRoutine(NetPlayer pl, int id, int done0)
        {
            ulong pid = pl.NetworkObjectId;
            double start = Now;
            float length = RodPullTimeline.LengthFrom(done0);
            Vector3 stand = Rods[RodIndex(id)].Stand;
            string why = null;
            bool wounded0 = pl.Wounded.Value;
            byte severed0 = pl.Severed.Value;
            while (Now - start < length)
            {
                yield return null;
                float t = (float)(Now - start);
                if (pl == null || !pl.IsSpawned) why = "player gone";
                else if (pl.Life.Value != LifeState.Alive) why = "downed";
                else if (pl.Busy.Value || pl.Severed.Value != severed0 || (pl.Wounded.Value && !wounded0)) why = "attacked";
                else if (Phase.Value != MatchPhase.Seeking && Phase.Value != MatchPhase.Hiding) why = "round over";
                else if (_rodPullCancel.Remove(pid)) why = "moved / cancelled";
                else if (t > 0.7f && Vector3.Distance(Flat(pl.transform.position), Flat(stand)) > 0.9f) why = "walked away";
                else if (RodIndex(id) < 0) why = "rod gone";
                if (why != null) break;
            }
            _rodPulls.Remove(id);
            int i = RodIndex(id);
            if (i < 0) yield break;
            var r = Rods[i];
            if (why != null)
            {
                float tl = RodPullTimeline.Map((float)(Now - start), done0);
                r.Done = (byte)Mathf.Clamp(Mathf.Max(done0, RodPullTimeline.JerksDone(tl)), 0, RodPullTimeline.Jerks.Length);
                r.Puller = RodState.NoPuller;
                Rods[i] = r;
                if (pl != null && pl.IsSpawned) pl.ToastRpc("Przerwane - pręt dalej tkwi w gruzie.", 2.5f);
                Debug.Log($"[RodPull] rod {id} interrupted ({why}), yanks done {r.Done}");
                yield break;
            }
            r.Puller = RodState.NoPuller;
            r.Taken = true;
            r.Done = (byte)RodPullTimeline.Jerks.Length;
            Rods[i] = r;
            if (pl.RodCount.Value < ItemTuning.MaxRodsCarried && Inventory.HasRoomFor(pl, HotbarItem.Rod)) GiveRod(pl, false);
            else
            {
                // no room any more (picked something up meanwhile): the rod drops where it was pulled out
                r.Taken = false;
                r.Embedded = false;
                Vector3 foot = stand + (r.Entry - stand).normalized * 0.4f;
                r.Base = new Vector3(foot.x, stand.y, foot.z);
                r.Top = r.Base + new Vector3(0.6f, 0.05f, 0.2f);
                Rods[i] = r;
            }
            Debug.Log($"[RodPull] {pl.Nick} pulled rod {id} out (rods={pl.RodCount.Value})");
        }

        // ------------------------------------------------------------------ leaks

        /// <summary>Every instance: shows the leaks of this round; the server picks a new seed once per round.</summary>
        private void UpdateLeaks()
        {
            if (IsServer && Phase.Value == MatchPhase.Hiding && _rodsSpawned && LeakSeed.Value == 0)
                LeakSeed.Value = Random.Range(1, int.MaxValue);
            if (IsServer && (Phase.Value == MatchPhase.Lobby) && LeakSeed.Value != 0) LeakSeed.Value = 0;
            var leaks = HideAndSeek.Map.CarLeaks.Instance;
            if (leaks == null || SceneManager.GetActiveScene().name != NetSession.GameScene) { _leakSeedShown = int.MinValue; return; }
            int seed = LeakSeed.Value != 0 ? LeakSeed.Value : HideAndSeek.Map.CarLeaks.DefaultSeed;
            if (seed == _leakSeedShown) return;
            _leakSeedShown = seed;
            leaks.Apply(seed);
        }
    }
}
