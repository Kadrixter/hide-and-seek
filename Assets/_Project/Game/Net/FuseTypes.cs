using System;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Tuning of the fuses and fuse boxes (escape objective, stage Fuses).</summary>
    public static class FuseTuning
    {
        public const int Boxes = 3, Sockets = 4, Fuses = FuseSymbols.Count;
        /// <summary>Server: how far (m, flat) from the box front the player may stand to work on it.</summary>
        public const float Reach = 1.7f;
        /// <summary>Fuses on the floor: from a hider spawn point, this far.</summary>
        public const float SpotMin = 5f, SpotMax = 13f;
        /// <summary>Minimum distance between two fuses / a fuse and a box.</summary>
        public const float FuseSpacing = 16f, BoxClearance = 7f;
    }

    /// <summary>
    /// One wall fuse box: where it hangs (wall point at floor height + yaw, +Z of the box = out of the wall), the mark of
    /// each of its 4 sockets and which sockets hold a fuse. Flags: placed, locked (the right box full: fuses can no longer
    /// be taken out), lit (a wrong box full: the floor's lights are on until it is empty again).
    /// Which box is the right one is NOT synced (only the server knows it).
    /// </summary>
    public struct FuseBox : INetworkSerializable, IEquatable<FuseBox>
    {
        public Vector3 Pos;
        public float Yaw;
        public byte S0, S1, S2, S3;
        public byte Filled;
        public byte Flags;

        public const byte FlagPlaced = 1, FlagLocked = 2, FlagLit = 4;

        public bool Placed => (Flags & FlagPlaced) != 0;
        public bool Locked => (Flags & FlagLocked) != 0;
        public bool Lit => (Flags & FlagLit) != 0;
        public bool Full => (Filled & 15) == 15;
        public bool Empty => (Filled & 15) == 0;
        public int Level => HideAndSeek.Map.LevelLighting.LevelAt(Pos.y + 0.3f);
        public Quaternion Rot => Quaternion.Euler(0f, Yaw, 0f);
        public Vector3 Normal => Rot * Vector3.forward;

        public int Symbol(int socket) => socket switch { 0 => S0, 1 => S1, 2 => S2, _ => S3 };
        public void SetSymbol(int socket, int s)
        {
            byte b = (byte)s;
            switch (socket) { case 0: S0 = b; break; case 1: S1 = b; break; case 2: S2 = b; break; default: S3 = b; break; }
        }
        public bool Has(int socket) => (Filled & (1 << socket)) != 0;
        /// <summary>Socket with mark <paramref name="symbol"/> (-1 = none).</summary>
        public int SocketOf(int symbol)
        {
            for (int i = 0; i < FuseTuning.Sockets; i++) if (Symbol(i) == symbol) return i;
            return -1;
        }
        public int FilledCount()
        {
            int n = 0;
            for (int i = 0; i < FuseTuning.Sockets; i++) if (Has(i)) n++;
            return n;
        }

        public Vector3 World(Vector3 local) => Pos + Rot * local;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref S0);
            s.SerializeValue(ref S1);
            s.SerializeValue(ref S2);
            s.SerializeValue(ref S3);
            s.SerializeValue(ref Filled);
            s.SerializeValue(ref Flags);
        }

        public bool Equals(FuseBox o) => Pos == o.Pos && Yaw == o.Yaw && S0 == o.S0 && S1 == o.S1 && S2 == o.S2 && S3 == o.S3 &&
                                         Filled == o.Filled && Flags == o.Flags;
    }

    /// <summary>The three fuse boxes of the round (one per level), synced as one value; <see cref="Round"/> changes every round.</summary>
    public struct FuseBoxSet : INetworkSerializable, IEquatable<FuseBoxSet>
    {
        public FuseBox A, B, C;
        public int Round;

        public FuseBox this[int i]
        {
            get => i switch { 0 => A, 1 => B, _ => C };
            set { switch (i) { case 0: A = value; break; case 1: B = value; break; default: C = value; break; } }
        }

        public bool Placed => A.Placed || B.Placed || C.Placed;

        /// <summary>Lights of level <paramref name="level"/> forced on (a wrong box on it is full / not yet empty).</summary>
        public bool LevelLit(int level)
        {
            for (int i = 0; i < FuseTuning.Boxes; i++) if (this[i].Placed && this[i].Lit && this[i].Level == level) return true;
            return false;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            A.NetworkSerialize(s);
            B.NetworkSerialize(s);
            C.NetworkSerialize(s);
            s.SerializeValue(ref Round);
        }

        public bool Equals(FuseBoxSet o) => A.Equals(o.A) && B.Equals(o.B) && C.Equals(o.C) && Round == o.Round;
    }

    /// <summary>A player working on a fuse box socket: putting a fuse in (one E press) or taking one out (E held).</summary>
    public struct FuseWorkState : INetworkSerializable, IEquatable<FuseWorkState>
    {
        public sbyte Box;    // -1 = not working
        public byte Socket;
        public bool Remove;
        public byte Symbol;  // the fuse's mark
        public double Start; // server time

        public static FuseWorkState None => new FuseWorkState { Box = -1 };
        public bool Active => Box >= 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Box);
            s.SerializeValue(ref Socket);
            s.SerializeValue(ref Remove);
            s.SerializeValue(ref Symbol);
            s.SerializeValue(ref Start);
        }

        public bool Equals(FuseWorkState o) => Box == o.Box && Socket == o.Socket && Remove == o.Remove && Symbol == o.Symbol && Start == o.Start;
    }

    /// <summary>Fuse marks as inventory items (HotbarItem.Fuse0 .. Fuse7, one kind per mark: each takes its own slot).</summary>
    public static class FuseItems
    {
        public static bool Is(HotbarItem item) => item >= HotbarItem.Fuse0 && item <= HotbarItem.Fuse7;
        public static int SymbolOf(HotbarItem item) => Is(item) ? (int)item - (int)HotbarItem.Fuse0 : -1;
        public static HotbarItem Of(int symbol) => (HotbarItem)((int)HotbarItem.Fuse0 + Mathf.Clamp(symbol, 0, FuseSymbols.Count - 1));
        public static int CountBits(byte mask)
        {
            int n = 0;
            for (int m = mask; m != 0; m &= m - 1) n++;
            return n;
        }
    }
}
