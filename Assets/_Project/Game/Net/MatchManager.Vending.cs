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
        /// <summary>Hard hits of the thrown can (bounce sounds).</summary>
        public byte Bounces;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Id); s.SerializeValue(ref Player); s.SerializeValue(ref Started); s.SerializeValue(ref Expires); s.SerializeValue(ref Position); s.SerializeValue(ref Rotation); s.SerializeValue(ref Stage); s.SerializeValue(ref Brand); s.SerializeValue(ref Bounces); }
        public bool Equals(VendingState o) => Id==o.Id && Player==o.Player && Started==o.Started && Expires==o.Expires && Position==o.Position && Rotation==o.Rotation && Stage==o.Stage && Brand==o.Brand && Bounces==o.Bounces;
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
            VendingCans.Add(new VendingState { Id=++nextCan, Player=p.NetworkObjectId, Started=Now, Rotation=Quaternion.identity, Brand=(byte)UnityEngine.Random.Range(0, HideAndSeek.Characters.SodaCanArt.BrandCount) });
        }
    }
}


