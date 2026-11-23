using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>Metal rod on the map (server-written): stuck in a rubble heap, or (old maps) leaning on a rack.</summary>
    public struct RodState : INetworkSerializable, IEquatable<RodState>
    {
        public const ulong NoPuller = ulong.MaxValue;

        public int Id;
        public Vector3 Base;   // foot (in the rubble: the buried end)
        public Vector3 Top;    // top end
        public bool Taken;
        /// <summary>Stuck in rubble (pulled out in 5 s) instead of lying loose.</summary>
        public bool Embedded;
        /// <summary>Floor point where the puller stands.</summary>
        public Vector3 Stand;
        /// <summary>NetworkObjectId of the player pulling it now, <see cref="NoPuller"/> when nobody.</summary>
        public ulong Puller;
        /// <summary>Server time the current pull started.</summary>
        public double PullStart;
        /// <summary>Yanks already done (an interrupted pull keeps them).</summary>
        public byte Done;

        public Vector3 Middle => (Base + Top) * 0.5f;
        public Vector3 Axis => (Top - Base).normalized;
        /// <summary>Where the rod enters the rubble (embedded rods).</summary>
        public Vector3 Entry => Base + Axis * RodPullTimeline.Buried;
        /// <summary>Middle of the part sticking out (prompt point).</summary>
        public Vector3 Exposed => Embedded ? (Entry + Top) * 0.5f : Middle;
        public bool BeingPulled => Puller != NoPuller;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Base);
            s.SerializeValue(ref Top);
            s.SerializeValue(ref Taken);
            s.SerializeValue(ref Embedded);
            s.SerializeValue(ref Stand);
            s.SerializeValue(ref Puller);
            s.SerializeValue(ref PullStart);
            s.SerializeValue(ref Done);
        }

        public bool Equals(RodState o) => Id == o.Id && Taken == o.Taken && Base == o.Base && Top == o.Top && Embedded == o.Embedded &&
                                          Stand == o.Stand && Puller == o.Puller && PullStart == o.PullStart && Done == o.Done;
    }

    /// <summary>Tuning of the rod / peg leg and the item markers.</summary>
    public static class ItemTuning
    {
        public const int MinRods = 4, MaxRods = 6;       // per round, random (stuck in rubble heaps)
        public const int MaxRodsCarried = 1;
        public const float RodSpeedScale = 0.62f;        // walking on a rod (hopping 0.34, limping 0.8, healthy 1)
        public const float RodRunScale = 0.5f;           // running on a rod (of the normal run speed)
        public const float MinimapItemRange = 45f;       // medkits / syringes shown on the minimap (same level)
    }

    /// <summary>
    /// Rods on the map: 4-6 per round stuck in rubble heaps (<see cref="RodSpots"/>), pulled out with E (5 s, see
    /// MatchManager.RodPull) into the inventory (alive hiders), fitted onto a cut-off leg from the hotbar. Server: spawn, pickup, fitting with a
    /// timer (interrupted when the hider is hit). All clients: rod views, per-player <see cref="RodLegDriver"/>, hotbar.
    /// </summary>
    public partial class MatchManager
    {
        public NetworkList<RodState> Rods;

        private readonly Dictionary<int, GameObject> _rodViews = new();

        /// <summary>View of rod <paramref name="id"/> on this instance (null when taken / not in the game scene).</summary>
        public RodView RodViewOf(int id) => _rodViews.TryGetValue(id, out var go) && go != null ? go.GetComponent<RodView>() : null;
        private readonly Dictionary<ulong, Coroutine> _rodAttach = new();
        private bool _rodsSpawned;

        private void InitRods() => Rods = new NetworkList<RodState>();

        /// <summary>Called from Update on every instance.</summary>
        private void UpdateRods()
        {
            HotbarHud.Ensure();
            foreach (var pl in NetPlayer.All)
            {
                if (pl != null && pl.GetComponent<RodLegDriver>() == null) pl.gameObject.AddComponent<RodLegDriver>();
                if (pl != null && pl.GetComponent<RodPullDriver>() == null) pl.gameObject.AddComponent<RodPullDriver>();
            }
            UpdateRodViews();
            UpdateLeaks();
            if (!IsServer) return;

            var ph = Phase.Value;
            if (ph == MatchPhase.Lobby || ph == MatchPhase.Loading)
            {
                _rodsSpawned = false;
                if (Rods.Count > 0) Rods.Clear();
                foreach (var pl in NetPlayer.All)
                {
                    if (pl == null) continue;
                    if (pl.RodCount.Value != 0) pl.RodCount.Value = 0;
                    if (pl.RodLegs.Value != 0) pl.RodLegs.Value = 0;
                }
                return;
            }
            // Spawn once the game map and the security rooms (which clear some racks away) are there.
            if (!_rodsSpawned && ph == MatchPhase.Hiding && !RaceMode && SecurityRooms.Instance != null &&
                SceneManager.GetActiveScene().name == NetSession.GameScene)
            {
                _rodsSpawned = true;
                SpawnRods(UnityEngine.Random.Range(ItemTuning.MinRods, ItemTuning.MaxRods + 1));
            }
        }

        /// <summary>Server: places <paramref name="count"/> rods (also used by tests).</summary>
        public void SpawnRods(int count)
        {
            Rods.Clear();
            var spots = RodSpots.Pick(count);
            for (int i = 0; i < spots.Count; i++)
            {
                var sp = spots[i];
                Rods.Add(new RodState { Id = 200 + i, Base = sp.Base, Top = sp.Top, Taken = false, Embedded = sp.Embedded, Stand = sp.Stand, Puller = RodState.NoPuller });
                Debug.Log($"[Rods] rod {200 + i} at {sp.Base} {(sp.Embedded ? "stuck in" : "next to")} {sp.Near}");
            }
        }

        private void UpdateRodViews()
        {
            var alive = new HashSet<int>();
            bool inGame = SceneManager.GetActiveScene().name == NetSession.GameScene;
            for (int i = 0; inGame && i < Rods.Count; i++)
            {
                var r = Rods[i];
                if (r.Taken) continue;
                alive.Add(r.Id);
                if (!_rodViews.TryGetValue(r.Id, out var go) || go == null) _rodViews[r.Id] = go = RodView.Create(r);
                if (go.TryGetComponent<RodView>(out var rv)) rv.Sync(r);
            }
            List<int> remove = null;
            foreach (var kv in _rodViews)
                if (!alive.Contains(kv.Key)) { if (kv.Value != null) Destroy(kv.Value); (remove ??= new List<int>()).Add(kv.Key); }
            if (remove != null) foreach (int id in remove) _rodViews.Remove(id);
        }

        /// <summary>Client: nearest rod in front of the eyes within reach (E prompt).</summary>
        public bool TryRodPrompt(Vector3 eye, Vector3 fwd, NetPlayer me, out int id, out Vector3 pos, out string text, out float dist)
        {
            id = 0;
            pos = default;
            text = null;
            dist = float.MaxValue;
            for (int i = 0; i < Rods.Count; i++)
            {
                var r = Rods[i];
                if (r.Taken) continue;
                Vector3 mp = r.Exposed;
                float d = Vector3.Distance(eye, mp);
                if (d >= dist || d > Tuning.PickupRange + 1f || Vector3.Angle(fwd, mp - eye) > 55f) continue;
                id = r.Id;
                pos = mp;
                dist = d;
            }
            if (dist == float.MaxValue) return false;
            RodState rod = default;
            for (int i = 0; i < Rods.Count; i++) if (Rods[i].Id == id) rod = Rods[i];
            if (rod.BeingPulled)
            {
                if (rod.Puller == me.NetworkObjectId) return false; // pulling it now: no prompt
                text = "(ktoś wyciąga ten pręt)";
                return true;
            }
            text = me.RodCount.Value >= ItemTuning.MaxRodsCarried ? "E  (masz już pręt)"
                : Inventory.PickupText(me, HotbarItem.Rod, rod.Embedded ? "E  wyciągnij pręt z gruzu" : "E  podnieś metalowy pręt");
            return true;
        }

        public void ServerPickupRod(NetPlayer pl, int id)
        {
            if (!Inventory.CanPickUp(pl, HotbarItem.Rod, Phase.Value)) return;
            if (pl.RodCount.Value >= ItemTuning.MaxRodsCarried)
            {
                pl.ToastRpc("Masz już pręt.", 2f);
                return;
            }
            if (!Inventory.HasRoomFor(pl, HotbarItem.Rod)) { pl.ToastRpc(Inventory.FullText, 2.5f); return; }
            for (int i = 0; i < Rods.Count; i++)
            {
                var r = Rods[i];
                if (r.Id != id || r.Taken) continue;
                if (Vector3.Distance(pl.transform.position, r.Exposed) > Tuning.PickupRange + 1.5f) return;
                if (r.Embedded)
                {
                    ServerStartRodPull(pl, i);
                    return;
                }
                r.Taken = true;
                Rods[i] = r;
                GiveRod(pl, true);
                return;
            }
        }

        /// <summary>Server: the rod goes into the inventory (picked up or pulled out of the rubble).</summary>
        private void GiveRod(NetPlayer pl, bool sound)
        {
            int slot = Inventory.PredictSlot(pl, HotbarItem.Rod) + 1;
            pl.RodCount.Value++;
            if (sound) pl.SfxRpc(GameSfx.MedkitPickup, 0.7f);
            pl.ToastRpc(pl.IsSeeker ? "Zabrałeś chowającym pręt (Q - wyrzuć)."
                : pl.FreeStump != 0 ? $"Masz pręt: wybierz go ({slot}) i kliknij LPM, żeby zrobić z niego nogę."
                : $"Masz pręt (slot {slot}). Przyda się, jeśli stracisz nogę.", 4f);
        }

        /// <summary>Hider with a free stump and a rod: fitting takes <see cref="RodTimeline.Duration"/> (can be interrupted).</summary>
        public void ServerRodAttach(NetPlayer pl)
        {
            if (pl == null || !pl.IsAliveHider || pl.Busy.Value || pl.RodCount.Value == 0) return;
            if (Phase.Value != MatchPhase.Seeking && Phase.Value != MatchPhase.Hiding) return;
            if (_rodAttach.ContainsKey(pl.NetworkObjectId)) return;
            int limb = pl.FreeStump;
            if (limb == 0) { pl.ToastRpc("Masz obie nogi - pręt przyda się później.", 2.5f); return; }
            pl.RodCount.Value--;
            pl.RodAttachRpc((byte)limb);
            _rodAttach[pl.NetworkObjectId] = StartCoroutine(RodAttachRoutine(pl, limb));
        }

        private IEnumerator RodAttachRoutine(NetPlayer pl, int limb)
        {
            ulong id = pl.NetworkObjectId;
            float end = Time.time + RodTimeline.Duration;
            while (Time.time < end)
            {
                yield return null;
                if (pl == null) { _rodAttach.Remove(id); yield break; }
                bool ok = pl.IsAliveHider && !pl.Busy.Value && (pl.Severed.Value & limb) != 0 &&
                          (Phase.Value == MatchPhase.Seeking || Phase.Value == MatchPhase.Hiding);
                if (!ok)
                {
                    // Interrupted: the rod goes back to the inventory (if the round still runs).
                    if (Phase.Value == MatchPhase.Seeking || Phase.Value == MatchPhase.Hiding) pl.RodCount.Value = (byte)Mathf.Min(255, pl.RodCount.Value + 1);
                    pl.RodAttachCancelRpc();
                    _rodAttach.Remove(id);
                    Debug.Log($"[Rods] fitting interrupted for {pl.Nick}");
                    yield break;
                }
            }
            pl.RodLegs.Value = (byte)(pl.RodLegs.Value | limb);
            _rodAttach.Remove(id);
            Debug.Log($"[Rods] {pl.Nick} walks on a rod now (legs {pl.RodLegs.Value})");
        }
    }
}
