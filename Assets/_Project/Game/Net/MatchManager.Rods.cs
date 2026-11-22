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
    /// <summary>Metal rod lying on the map (server-written).</summary>
    public struct RodState : INetworkSerializable, IEquatable<RodState>
    {
        public int Id;
        public Vector3 Base;   // foot on the floor
        public Vector3 Top;    // top end leaning on the rack / fence
        public bool Taken;

        public Vector3 Middle => (Base + Top) * 0.5f;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Base);
            s.SerializeValue(ref Top);
            s.SerializeValue(ref Taken);
        }

        public bool Equals(RodState o) => Id == o.Id && Taken == o.Taken && Base == o.Base && Top == o.Top;
    }

    /// <summary>Tuning of the rod / peg leg and the item markers.</summary>
    public static class ItemTuning
    {
        public const int MinRods = 1, MaxRods = 3;       // per round, random
        public const int MaxRodsCarried = 1;
        public const float RodSpeedScale = 0.62f;        // walking on a rod (hopping 0.34, limping 0.8, healthy 1)
        public const float RodRunScale = 0.5f;           // running on a rod (of the normal run speed)
        public const float MinimapItemRange = 45f;       // medkits / syringes shown on the minimap (same level)
    }

    /// <summary>
    /// Rods on the map: 1-3 per round next to shelving / fences (<see cref="RodSpots"/>), picked up with E into the
    /// inventory (alive hiders), fitted onto a cut-off leg from the hotbar. Server: spawn, pickup, fitting with a
    /// timer (interrupted when the hider is hit). All clients: rod views, per-player <see cref="RodLegDriver"/>, hotbar.
    /// </summary>
    public partial class MatchManager
    {
        public NetworkList<RodState> Rods;

        private readonly Dictionary<int, GameObject> _rodViews = new();
        private readonly Dictionary<ulong, Coroutine> _rodAttach = new();
        private bool _rodsSpawned;

        private void InitRods() => Rods = new NetworkList<RodState>();

        /// <summary>Called from Update on every instance.</summary>
        private void UpdateRods()
        {
            HotbarHud.Ensure();
            foreach (var pl in NetPlayer.All)
                if (pl != null && pl.GetComponent<RodLegDriver>() == null) pl.gameObject.AddComponent<RodLegDriver>();
            UpdateRodViews();
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
                Rods.Add(new RodState { Id = 200 + i, Base = spots[i].Base, Top = spots[i].Top, Taken = false });
                Debug.Log($"[Rods] rod {200 + i} at {spots[i].Base} next to {spots[i].Near}");
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
                if (!_rodViews.TryGetValue(r.Id, out var go) || go == null) _rodViews[r.Id] = RodView.Create(r.Base, r.Top);
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
                Vector3 mp = r.Middle;
                float d = Vector3.Distance(eye, mp);
                if (d >= dist || d > Tuning.PickupRange + 1f || Vector3.Angle(fwd, mp - eye) > 55f) continue;
                id = r.Id;
                pos = mp;
                dist = d;
            }
            if (dist == float.MaxValue) return false;
            text = me.RodCount.Value >= ItemTuning.MaxRodsCarried ? "E  (masz już pręt)" : "E  podnieś metalowy pręt";
            return true;
        }

        public void ServerPickupRod(NetPlayer pl, int id)
        {
            if (pl == null || !pl.IsAliveHider) return;
            if (Phase.Value != MatchPhase.Seeking && Phase.Value != MatchPhase.Hiding) return;
            if (pl.RodCount.Value >= ItemTuning.MaxRodsCarried)
            {
                pl.ToastRpc("Masz już pręt.", 2f);
                return;
            }
            for (int i = 0; i < Rods.Count; i++)
            {
                var r = Rods[i];
                if (r.Id != id || r.Taken) continue;
                if (Vector3.Distance(pl.transform.position, r.Middle) > Tuning.PickupRange + 1.5f) return;
                r.Taken = true;
                Rods[i] = r;
                pl.RodCount.Value++;
                pl.SfxRpc(GameSfx.MedkitPickup, 0.7f);
                pl.ToastRpc(pl.FreeStump != 0 ? "Masz pręt: wybierz go (4) i kliknij LPM, żeby zrobić z niego nogę."
                    : "Masz pręt (slot 4). Przyda się, jeśli stracisz nogę.", 4f);
                return;
            }
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
