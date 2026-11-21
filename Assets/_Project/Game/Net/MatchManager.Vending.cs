using HideAndSeek.Core;
using System;
using Unity.Netcode;
using UnityEngine;
namespace HideAndSeek.Game.Net
{
    public struct VendingState : INetworkSerializable, IEquatable<VendingState>
    {
        public int Id;
        public ulong Player;
        public double Started, Expires;
        public Vector3 Position;
        public Quaternion Rotation;
        public byte Stage;
        /// <summary>Which of the <see cref="HideAndSeek.Characters.SodaCanArt"/> brands came out.</summary>
        public byte Brand;
        /// <summary>Shelf slot the can came from (row * 4 + column of <see cref="VendingMachineModel"/>).</summary>
        public byte Slot;
        /// <summary>Hard hits of the thrown can (bounce sounds).</summary>
        public byte Bounces;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Id); s.SerializeValue(ref Player); s.SerializeValue(ref Started); s.SerializeValue(ref Expires); s.SerializeValue(ref Position); s.SerializeValue(ref Rotation); s.SerializeValue(ref Stage); s.SerializeValue(ref Brand); s.SerializeValue(ref Bounces); s.SerializeValue(ref Slot); }
        public bool Equals(VendingState o) => Id==o.Id && Player==o.Player && Started==o.Started && Expires==o.Expires && Position==o.Position && Rotation==o.Rotation && Stage==o.Stage && Brand==o.Brand && Bounces==o.Bounces && Slot==o.Slot;
    }
    public partial class MatchManager
    {
        public NetworkList<VendingState> VendingCans;
        int nextCan;
        public void ServerBuyCan(NetPlayer p)
        {
            var machine=LobbyVending.Instance;
            if(!IsServer || Phase.Value!=MatchPhase.Lobby || !p || p.Busy.Value || p.Life.Value!=LifeState.Alive || !machine) return;
            if(Vector3.Distance(p.transform.position,machine.transform.position)>3f) return;
            foreach(var s in VendingCans) if(s.Stage==0) return;
            p.Busy.Value=true;
            p.TeleportRpc(machine.transform.TransformPoint(new Vector3(-.98f,0,.08f)),90f);
            // A random slot drops one can; the brand is whatever is stocked in that slot.
            int slot=UnityEngine.Random.Range(0, VendingMachineModel.Rows*VendingMachineModel.Cols);
            int brand=VendingMachineModel.SlotBrand(slot/VendingMachineModel.Cols, slot%VendingMachineModel.Cols);
            VendingCans.Add(new VendingState { Id=++nextCan, Player=p.NetworkObjectId, Started=Now, Rotation=Quaternion.identity, Brand=(byte)brand, Slot=(byte)slot });
        }
    }
}


