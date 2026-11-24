using System;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>One security-room fire extinguisher (index = level 0..2), written by the server.</summary>
    public struct ExtState : INetworkSerializable, IEquatable<ExtState>
    {
        public const byte OnWall = 0, InHands = 1, OnFloor = 2;
        public const byte Full = 200;

        public byte Where;
        public ulong Holder;
        public Vector3 Pos;     // on the floor: where it lies
        public float Yaw;
        public bool Unlocked;   // safety pin pulled
        public byte Charge;     // 0..Full

        public float Charge01 => Charge / (float)Full;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Where);
            s.SerializeValue(ref Holder);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Unlocked);
            s.SerializeValue(ref Charge);
        }

        public bool Equals(ExtState o) => Where == o.Where && Holder == o.Holder && Pos == o.Pos && Yaw == o.Yaw && Unlocked == o.Unlocked && Charge == o.Charge;
    }

    /// <summary>Tuning of the extinguishers and the foam.</summary>
    public static class ExtTuning
    {
        public const float SprayTime = 16.5f;      // seconds of continuous spraying from full to empty
        public const float Range = 4.6f;           // jet reach (m)
        public const float ConeDeg = 13f;          // half angle of the jet that counts as a hit
        public const float ExposureToHit = 0f;     // the foam takes effect on the first hit, no delay
        public const float FoamTime = 10f;         // seeker: no attacks, slowed, wiping the face
        public const float FoamMax = 11f;          // more spray during the effect extends it up to this in total
        public const float Immunity = 0f;          // after the effect: no cooldown, a fresh hit foams again at once
        public const float FoamSpeed = 0.45f;      // speed multiplier while foamed (recovers in the last 1.2 s)
        public const float TakeRange = 1.9f, PromptRange = 2.6f, HangBackRange = 1.7f;
    }

    /// <summary>
    /// Fire extinguishers of the security rooms (server side): one per room, back on its wall bracket and full every
    /// round. Alive hiders take one (E, two hands, not an inventory item), pull the pin on the first use, spray it
    /// (owner flag <see cref="NetPlayer.ExtSpray"/>, the server drains the charge and tests the jet against seekers)
    /// and put it down with Q (back on the bracket when close to it, else on the floor, where anybody may pick it up
    /// again). It falls out of the hands when the holder goes down, dies, gets busy, sits or leaves. Late joiners get
    /// everything from <see cref="Extinguishers"/> and the players' foam variables.
    /// </summary>
    public partial class MatchManager
    {
        public NetworkList<ExtState> Extinguishers;
        public const ulong NoExtHolder = ulong.MaxValue;
        public const byte ExtTake = 0, ExtPutDown = 1, ExtUnlock = 2;

        private System.Action _stepExt;
        private bool _extSpawned;
        private readonly float[] _extDrain = new float[3];
        private readonly double[] _extUnlockAt = new double[3];
        private readonly Vector3[] _extLastPos = new Vector3[3];
        private float _extNextHitTest;

        private void InitExtinguishers() => Extinguishers = new NetworkList<ExtState>();

        public bool ExtRoundPhase
        {
            get
            {
                var ph = Phase.Value;
                return !RaceMode && (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) && SceneManager.GetActiveScene().name == NetSession.GameScene;
            }
        }

        /// <summary>Index of the extinguisher held by <paramref name="p"/> (-1 = none).</summary>
        public int ExtHeldBy(NetPlayer p)
        {
            if (p == null || Extinguishers == null) return -1;
            for (int i = 0; i < Extinguishers.Count; i++)
                if (Extinguishers[i].Where == ExtState.InHands && Extinguishers[i].Holder == p.NetworkObjectId) return i;
            return -1;
        }

        /// <summary>May <paramref name="p"/> hold an extinguisher now?</summary>
        public bool ExtCanHold(NetPlayer p) => p != null && ExtRoundPhase && p.IsAliveHider && !p.Busy.Value && p.Seat.Value < 0 && !p.HoldsPaMic && !p.RcControlling;

        /// <summary>Called from Update on every instance.</summary>
        private void UpdateExtinguishers()
        {
            FoamOverlay.Ensure();
            if (SceneManager.GetActiveScene().name == NetSession.GameScene) ExtinguisherSystem.Ensure();
            if (!IsServer) return;
            if (!ExtRoundPhase)
            {
                if (Phase.Value == MatchPhase.Lobby || Phase.Value == MatchPhase.Loading)
                {
                    _extSpawned = false;
                    if (Extinguishers.Count > 0) Extinguishers.Clear();
                    foreach (var pl in NetPlayer.All) if (pl != null) { pl.FoamUntil.Value = 0; pl.FoamStart.Value = 0; }
                }
                return;
            }
            if (!_extSpawned && SecurityRooms.Instance != null)
            {
                _extSpawned = true;
                Extinguishers.Clear();
                for (int i = 0; i < 3; i++)
                {
                    Extinguishers.Add(new ExtState { Where = ExtState.OnWall, Holder = NoExtHolder, Charge = ExtState.Full });
                    _extDrain[i] = 0f;
                }
                Debug.Log("[Ext] 3 extinguishers on their brackets");
            }
            for (int i = 0; i < Extinguishers.Count; i++)
            {
                var e = Extinguishers[i];
                if (e.Where != ExtState.InHands) continue;
                var p = NetPlayer.Find(e.Holder);
                if (p != null) _extLastPos[i] = p.transform.position;
                string why = p == null ? "holder left" : p.Life.Value != LifeState.Alive ? "holder down" : p.Busy.Value ? "holder busy"
                    : p.Seat.Value >= 0 ? "holder sat down" : !p.IsHider ? "not a hider" : null;
                if (why != null) { ExtDrop(i, p, why); continue; }
                ServerExtSpray(i, p);
            }
        }

        /// <summary>Server: requests of a player (take = index of the extinguisher, put down / unlock = the held one).</summary>
        public void ServerExt(NetPlayer p, byte op, byte index)
        {
            if (!IsServer || p == null || Extinguishers.Count == 0) return;
            int held = ExtHeldBy(p);
            if (op == ExtTake)
            {
                if (held >= 0 || index >= Extinguishers.Count || !ExtCanHold(p)) return;
                var e = Extinguishers[index];
                if (e.Where == ExtState.InHands) return;
                Vector3 at = e.Where == ExtState.OnWall ? SecurityRooms.ExtinguisherSpot(index) : e.Pos;
                Vector3 d = p.transform.position - at;
                if (new Vector2(d.x, d.z).magnitude > ExtTuning.TakeRange + 0.4f || Mathf.Abs(d.y) > 1.3f) return;
                e.Where = ExtState.InHands;
                e.Holder = p.NetworkObjectId;
                Extinguishers[index] = e;
                Debug.Log($"[Ext] {p.Nick} took extinguisher {index} (charge {e.Charge}, pin {(e.Unlocked ? "out" : "in")})");
                return;
            }
            if (held < 0) return;
            if (op == ExtPutDown) ExtDrop(held, p, "put down");
            else if (op == ExtUnlock)
            {
                var e = Extinguishers[held];
                if (e.Unlocked) return;
                e.Unlocked = true;
                Extinguishers[held] = e;
                _extUnlockAt[held] = Now;
                p.ExtPinRpc();
                Debug.Log($"[Ext] {p.Nick} pulled the pin of extinguisher {held}");
            }
        }

        /// <summary>Out of the hands: back on its bracket when the holder stands next to it, else on the floor in front of him.</summary>
        private void ExtDrop(int i, NetPlayer p, string why)
        {
            var e = Extinguishers[i];
            Vector3 feet = p != null ? p.transform.position : _extLastPos[i];
            Vector3 spot = SecurityRooms.ExtinguisherSpot(i);
            Vector3 d = feet - spot;
            bool hang = why == "put down" && new Vector2(d.x, d.z).magnitude < ExtTuning.HangBackRange && Mathf.Abs(d.y) < 1.2f;
            e.Holder = NoExtHolder;
            if (hang) e.Where = ExtState.OnWall;
            else
            {
                Vector3 fwd = p != null ? p.transform.forward : Vector3.forward;
                e.Where = ExtState.OnFloor;
                e.Pos = ExtinguisherSystem.FloorSpot(feet, fwd);
                e.Yaw = (p != null ? p.transform.eulerAngles.y : 0f) + UnityEngine.Random.Range(-40f, 40f);
            }
            Extinguishers[i] = e;
            Debug.Log($"[Ext] extinguisher {i} {(hang ? "back on the bracket" : "on the floor at " + e.Pos)} ({why})");
        }
    }
}
