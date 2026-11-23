using System;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>Who sits on each swivel chair of the security rooms and which way every chair is turned (synced to all, late joiners included).</summary>
    public struct ChairsState : INetworkSerializable, IEquatable<ChairsState>
    {
        public const ulong Free = ulong.MaxValue;
        public ulong S0, S1, S2, S3, S4, S5;
        public short Y0, Y1, Y2, Y3, Y4, Y5;

        public static ChairsState Empty => new ChairsState { S0 = Free, S1 = Free, S2 = Free, S3 = Free, S4 = Free, S5 = Free, Y0 = short.MinValue, Y1 = short.MinValue, Y2 = short.MinValue, Y3 = short.MinValue, Y4 = short.MinValue, Y5 = short.MinValue };

        public ulong Sitter(int i) => i switch { 0 => S0, 1 => S1, 2 => S2, 3 => S3, 4 => S4, 5 => S5, _ => Free };

        /// <summary>Yaw a free chair was left at (short.MinValue = never moved: the built yaw).</summary>
        public short Yaw(int i) => i switch { 0 => Y0, 1 => Y1, 2 => Y2, 3 => Y3, 4 => Y4, 5 => Y5, _ => short.MinValue };

        public ChairsState With(int i, ulong sitter, short yaw)
        {
            var s = this;
            switch (i)
            {
                case 0: s.S0 = sitter; s.Y0 = yaw; break;
                case 1: s.S1 = sitter; s.Y1 = yaw; break;
                case 2: s.S2 = sitter; s.Y2 = yaw; break;
                case 3: s.S3 = sitter; s.Y3 = yaw; break;
                case 4: s.S4 = sitter; s.Y4 = yaw; break;
                case 5: s.S5 = sitter; s.Y5 = yaw; break;
            }
            return s;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> b) where T : IReaderWriter
        {
            b.SerializeValue(ref S0); b.SerializeValue(ref S1); b.SerializeValue(ref S2);
            b.SerializeValue(ref S3); b.SerializeValue(ref S4); b.SerializeValue(ref S5);
            b.SerializeValue(ref Y0); b.SerializeValue(ref Y1); b.SerializeValue(ref Y2);
            b.SerializeValue(ref Y3); b.SerializeValue(ref Y4); b.SerializeValue(ref Y5);
        }

        public bool Equals(ChairsState o) => S0 == o.S0 && S1 == o.S1 && S2 == o.S2 && S3 == o.S3 && S4 == o.S4 && S5 == o.S5 &&
                                             Y0 == o.Y0 && Y1 == o.Y1 && Y2 == o.Y2 && Y3 == o.Y3 && Y4 == o.Y4 && Y5 == o.Y5;
    }

    /// <summary>
    /// Server side of the security-room swivel chairs (<see cref="SecurityRooms.Chairs"/>): a living player in the
    /// round sits on a free chair within reach (E), gets up with E / Space / a movement key, and is put off it when he
    /// goes down, dies, gets busy (kill animation, crate...), leaves, walks away (teleport) or the round ends. The chair
    /// stays turned the way it was left.
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<ChairsState> SecurityChairs = new(ChairsState.Empty);

        public bool ChairRoundPhase
        {
            get
            {
                var ph = Phase.Value;
                return !RaceMode && (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) && SceneManager.GetActiveScene().name == NetSession.GameScene;
            }
        }

        public bool ChairCanSit(NetPlayer p) => p != null && ChairRoundPhase && p.Life.Value == LifeState.Alive && !p.Busy.Value && p.Seat.Value < 0;

        /// <summary>Index of the chair <paramref name="p"/> sits on (-1 = none).</summary>
        public int ChairOf(NetPlayer p)
        {
            if (p == null) return -1;
            var s = SecurityChairs.Value;
            for (int i = 0; i < SecurityRooms.ChairCount; i++)
                if (s.Sitter(i) == p.NetworkObjectId) return i;
            return -1;
        }

        [Rpc(SendTo.Server)]
        public void ChairRequestRpc(byte chair, bool sit, short yaw, RpcParams rpc = default)
        {
            var p = NetPlayer.FindByClient(rpc.Receive.SenderClientId);
            var sr = SecurityRooms.Instance;
            if (p == null || sr == null || chair >= SecurityRooms.ChairCount || chair >= sr.Chairs.Count) return;
            var s = SecurityChairs.Value;
            if (!sit)
            {
                if (s.Sitter(chair) == p.NetworkObjectId) SecurityChairs.Value = s.With(chair, ChairsState.Free, yaw);
                return;
            }
            if (s.Sitter(chair) != ChairsState.Free || ChairOf(p) >= 0 || !ChairCanSit(p)) return;
            Vector3 d = p.transform.position - sr.Chairs[chair].Anchor.position;
            if (new Vector2(d.x, d.z).magnitude > 2.4f || Mathf.Abs(d.y) > 1.2f) return;
            SecurityChairs.Value = s.With(chair, p.NetworkObjectId, yaw);
            Debug.Log($"[Chair] {p.Nick} sat on chair {chair}");
        }

        /// <summary>Server, every frame (from SecurityRooms): frees chairs whose sitter may no longer sit.</summary>
        public void ServerChairTick()
        {
            if (!IsServer) return;
            var s = SecurityChairs.Value;
            var sr = SecurityRooms.Instance;
            bool changed = false;
            for (int i = 0; i < SecurityRooms.ChairCount; i++)
            {
                ulong id = s.Sitter(i);
                if (id == ChairsState.Free) continue;
                var p = NetPlayer.Find(id);
                string why = p == null ? "left" : !ChairRoundPhase ? "round over" : p.Life.Value != LifeState.Alive ? "down / dead" : p.Busy.Value ? "busy" : null;
                if (why == null && sr != null && i < sr.Chairs.Count && (p.transform.position - sr.Chairs[i].Anchor.position).sqrMagnitude > 1.5f * 1.5f) why = "moved away";
                if (why == null) continue;
                short yaw = p != null ? (short)Mathf.RoundToInt(Mathf.Repeat(p.transform.eulerAngles.y, 360f)) : s.Yaw(i);
                s = s.With(i, ChairsState.Free, yaw);
                changed = true;
                Debug.Log($"[Chair] chair {i} freed ({why})");
            }
            if (changed) SecurityChairs.Value = s;
            if (!ChairRoundPhase && !s.Equals(ChairsState.Empty) && !AnySitter(s)) SecurityChairs.Value = ChairsState.Empty;
        }

        private static bool AnySitter(ChairsState s)
        {
            for (int i = 0; i < SecurityRooms.ChairCount; i++)
                if (s.Sitter(i) != ChairsState.Free) return true;
            return false;
        }
    }
}
