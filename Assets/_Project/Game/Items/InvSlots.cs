using System;
using Unity.Netcode;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Layout of a player's inventory: which item kind sits in each of the 5 hotbar slots (keys 1-5). Server-written
    /// (<see cref="Inventory.ServerReconcile"/>); the amounts come from the item's own state (<see cref="ItemDef.Count"/>).
    /// </summary>
    public struct InvSlots : INetworkSerializable, IEquatable<InvSlots>
    {
        public const int Count = 5;
        public byte S0, S1, S2, S3, S4;

        public HotbarItem this[int i]
        {
            get => (HotbarItem)(i switch { 0 => S0, 1 => S1, 2 => S2, 3 => S3, 4 => S4, _ => 0 });
            set
            {
                byte b = (byte)value;
                switch (i)
                {
                    case 0: S0 = b; break;
                    case 1: S1 = b; break;
                    case 2: S2 = b; break;
                    case 3: S3 = b; break;
                    case 4: S4 = b; break;
                }
            }
        }

        public int IndexOf(HotbarItem item)
        {
            if (item == HotbarItem.None) return -1;
            for (int i = 0; i < Count; i++) if (this[i] == item) return i;
            return -1;
        }

        public int FirstFree()
        {
            for (int i = 0; i < Count; i++) if (this[i] == HotbarItem.None) return i;
            return -1;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref S0);
            s.SerializeValue(ref S1);
            s.SerializeValue(ref S2);
            s.SerializeValue(ref S3);
            s.SerializeValue(ref S4);
        }

        public bool Equals(InvSlots o) => S0 == o.S0 && S1 == o.S1 && S2 == o.S2 && S3 == o.S3 && S4 == o.S4;
        public override bool Equals(object obj) => obj is InvSlots o && Equals(o);
        public override int GetHashCode() => S0 | S1 << 8 | S2 << 16 | S3 << 24 ^ S4 * 31;
        public override string ToString() => $"[{this[0]}, {this[1]}, {this[2]}, {this[3]}, {this[4]}]";
    }
}
