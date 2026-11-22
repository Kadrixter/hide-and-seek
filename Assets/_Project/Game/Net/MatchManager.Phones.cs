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
    public enum PhoneMode : byte { Floor, Carried, Smashed }

    /// <summary>Pink toy phone (server-written): lying on the floor, carried by a hider or smashed.</summary>
    public struct PhoneState : INetworkSerializable, IEquatable<PhoneState>
    {
        public int Id;
        public PhoneMode Mode;
        public ulong Holder;     // NetworkObjectId of the carrying hider
        public Vector3 Pos;      // on the floor (also where it was smashed)
        public float Yaw;
        public double RingAt;    // server time the ringtone started, 0 = silent

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Mode);
            s.SerializeValue(ref Holder);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref RingAt);
        }

        public bool Equals(PhoneState o) => Id == o.Id && Mode == o.Mode && Holder == o.Holder && Pos == o.Pos && Yaw == o.Yaw && RingAt == o.RingAt;
    }

    /// <summary>
    /// Toy phones: ceil(n/2)..n-1 per round (n = players incl. the seeker) next to racks / fences on all levels
    /// (<see cref="PhoneSpots"/>). Hiders pick one up (E, passive hotbar slot); two or more hiders with a phone hear
    /// each other through it anywhere (<see cref="Voice.VoiceRules.PhoneLink"/>). Every 60-150 s a phone rings
    /// (on the floor: 3D + it jumps; in the pocket: from the hider - E smashes it). The seeker stomps on one lying
    /// on the floor (F). Client visuals / sound: <see cref="PhoneWorld"/>.
    /// </summary>
    public partial class MatchManager
    {
        public NetworkList<PhoneState> Phones;

        private readonly Dictionary<int, double> _phoneNextRing = new();
        private readonly Dictionary<int, Vector3> _phoneHolderPos = new();
        private bool _phonesSpawned;
        private float _phoneHolderCheck;

        private void InitPhones() => Phones = new NetworkList<PhoneState>();

        /// <summary>Random count for <paramref name="players"/> players (seeker included): ceil(n/2)..n-1, at least 1.</summary>
        public static int PhoneCountFor(int players) =>
            players < 2 ? 1 : UnityEngine.Random.Range((players + 1) / 2, Mathf.Max((players + 1) / 2, players - 1) + 1);

        public static bool PhoneRinging(in PhoneState s, double now) =>
            s.RingAt > 0d && s.Mode != PhoneMode.Smashed && now - s.RingAt < PhoneTuning.RingLength;

        /// <summary>Index of the phone <paramref name="p"/> carries, -1 = none.</summary>
        public int PhoneOf(NetPlayer p)
        {
            if (p == null || Phones == null) return -1;
            for (int i = 0; i < Phones.Count; i++)
                if (Phones[i].Mode == PhoneMode.Carried && Phones[i].Holder == p.NetworkObjectId) return i;
            return -1;
        }

        public bool HasPhone(NetPlayer p) => PhoneOf(p) >= 0;

        /// <summary>True while the phone in <paramref name="p"/>'s pocket rings.</summary>
        public bool PocketRinging(NetPlayer p)
        {
            int i = PhoneOf(p);
            return i >= 0 && PhoneRinging(Phones[i], Now);
        }

        /// <summary>Called from Update on every instance.</summary>
        private void UpdatePhones()
        {
            PhoneWorld.Ensure();
            PhoneHud.Ensure();
            if (!IsServer) return;
            var ph = Phase.Value;
            if (ph == MatchPhase.Lobby || ph == MatchPhase.Loading)
            {
                _phonesSpawned = false;
                if (Phones.Count > 0) Phones.Clear();
                _phoneNextRing.Clear();
                return;
            }
            if (!_phonesSpawned && ph == MatchPhase.Hiding && !RaceMode && SecurityRooms.Instance != null &&
                SceneManager.GetActiveScene().name == NetSession.GameScene)
            {
                _phonesSpawned = true;
                int players = 0, hiders = 0;
                foreach (var p in NetPlayer.All) if (p != null) { players++; if (p.IsHider) hiders++; }
                int forced = PhoneTuning.ForcedCount;
                // Several seekers: never more phones than hiders (with one seeker n-1 = hiders, as before).
                int count = forced > 0 ? forced : Mathf.Min(PhoneCountFor(players), Mathf.Max(1, hiders));
                SpawnPhones(count);
                Debug.Log($"[Phone] {Phones.Count} phones for {players} players (wanted {count})");
            }
            if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking) return;
            UpdatePhoneHolders();
            UpdatePhoneRings();
        }

        /// <summary>Server: places <paramref name="count"/> phones on the floor (also used by tests).</summary>
        public void SpawnPhones(int count)
        {
            Phones.Clear();
            _phoneNextRing.Clear();
            var avoid = new List<Vector3>();
            for (int i = 0; i < Rods.Count; i++) avoid.Add(Rods[i].Base);
            var spots = PhoneSpots.Pick(count, avoid);
            for (int i = 0; i < spots.Count; i++) AddPhone(300 + i, spots[i].Pos, spots[i].Yaw, spots[i].Near);
        }

        private void AddPhone(int id, Vector3 pos, float yaw, string near)
        {
            Phones.Add(new PhoneState { Id = id, Mode = PhoneMode.Floor, Holder = 0, Pos = pos, Yaw = yaw, RingAt = 0d });
            _phoneNextRing[id] = Now + PhoneTuning.NextGap();
            Debug.Log($"[Phone] phone {id} at {pos} (level {FloorAcoustics.FloorOf(pos.y)}) next to {near}");
        }

        /// <summary>Test: an extra phone lying at <paramref name="pos"/>; returns its id.</summary>
        public int TestAddPhone(Vector3 pos, float yaw)
        {
            int id = 390 + Phones.Count;
            AddPhone(id, pos, yaw, "test");
            return id;
        }

        /// <summary>Test / server: starts the ringtone of phone <paramref name="id"/> now.</summary>
        public void RingPhone(int id)
        {
            for (int i = 0; i < Phones.Count; i++)
            {
                var s = Phones[i];
                if (s.Id != id || s.Mode == PhoneMode.Smashed) continue;
                s.RingAt = Now;
                Phones[i] = s;
                Debug.Log($"[Phone] phone {id} rings ({s.Mode}{(s.Mode == PhoneMode.Carried ? " by " + NetPlayer.Find(s.Holder)?.Nick : "")})");
            }
        }

        private void UpdatePhoneRings()
        {
            double now = Now;
            for (int i = 0; i < Phones.Count; i++)
            {
                var s = Phones[i];
                if (s.Mode == PhoneMode.Smashed) continue;
                bool ringing = PhoneRinging(s, now);
                if (s.RingAt > 0d && !ringing)
                {
                    s.RingAt = 0d;
                    Phones[i] = s;
                    _phoneNextRing[s.Id] = now + PhoneTuning.NextGap();
                    continue;
                }
                if (!ringing && _phoneNextRing.TryGetValue(s.Id, out double next) && now >= next) RingPhone(s.Id);
            }
        }

        /// <summary>A dead / gone / no longer hiding holder drops the phone where they were (anybody can take it).</summary>
        private void UpdatePhoneHolders()
        {
            bool sample = Time.time >= _phoneHolderCheck;
            if (sample) _phoneHolderCheck = Time.time + 0.25f;
            for (int i = 0; i < Phones.Count; i++)
            {
                var s = Phones[i];
                if (s.Mode != PhoneMode.Carried) continue;
                var pl = NetPlayer.Find(s.Holder);
                if (pl != null && pl.IsHider && pl.Life.Value != LifeState.Dead)
                {
                    if (sample) _phoneHolderPos[s.Id] = pl.transform.position;
                    continue;
                }
                Vector3 feet = pl != null ? pl.transform.position : _phoneHolderPos.TryGetValue(s.Id, out var last) ? last : s.Pos;
                Vector3 at = pl != null ? feet + pl.transform.right * 0.45f : feet;
                s.Mode = PhoneMode.Floor;
                s.Holder = 0;
                s.Pos = FloorUnder(feet, at);
                s.Yaw = UnityEngine.Random.Range(0f, 360f);
                Phones[i] = s;
                Debug.Log($"[Phone] phone {s.Id} dropped at {s.Pos} (holder {(pl != null ? pl.Nick + " " + pl.Life.Value : "left")})");
            }
        }

        /// <summary>
        /// A phone put down by somebody standing at <paramref name="feet"/>, wanted at <paramref name="want"/>: on that
        /// floor, never behind a wall or inside a car / column (ItemPlacement.PutDown).
        /// </summary>
        private static Vector3 FloorUnder(Vector3 feet, Vector3 want) =>
            ItemPlacement.PutDown(feet, want, ItemPlacement.Small, "phone") + Vector3.up * 0.005f;
    }
}
