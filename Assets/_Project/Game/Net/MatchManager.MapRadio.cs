using System;
using HideAndSeek.Characters;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>A boombox found on the parking (server-written).</summary>
    public struct MapRadioState : INetworkSerializable, IEquatable<MapRadioState>
    {
        public const byte Lying = 1, Carried = 2;
        public const byte Placed = 0, Lifted = 1, PutDown = 2, Fell = 3;

        public byte Mode;          // Lying / Carried
        public ulong Carrier;      // carried: the player's NetworkObjectId
        public ulong LastCarrier;  // put down / fell: who had it (his body animates it)
        public Vector3 Pos;        // lying: bottom centre (also where a lift starts and a put ends)
        public float Yaw;
        public byte Move;          // how it got into this state
        public double MoveAt;      // server time of that

        public Quaternion Rot => Quaternion.Euler(0f, Yaw, 0f);

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Mode);
            s.SerializeValue(ref Carrier);
            s.SerializeValue(ref LastCarrier);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Move);
            s.SerializeValue(ref MoveAt);
        }

        public bool Equals(MapRadioState o) => Mode == o.Mode && Carrier == o.Carrier && LastCarrier == o.LastCarrier && Pos == o.Pos &&
                                               Yaw == o.Yaw && Move == o.Move && MoveAt == o.MoveAt;
    }

    /// <summary>
    /// Radios on the parking: one per round (two from <see cref="MapRadioTwoFrom"/> players) on a pallet / crate
    /// (<see cref="MapRadioSpots"/>). Alive hiders lift it onto the shoulder (E, not into the inventory), carry it
    /// (slower running, no items meanwhile), put it down (Q) and play music on it (R = the same panel as the lobby
    /// boombox, own queue per radio: <see cref="RadioChannel"/>). Knocked down / killed / lying down = it falls. The
    /// music is heard by everybody around it - seekers too. Views, sound and animation: <see cref="MapRadioSystem"/>.
    /// </summary>
    public partial class MatchManager
    {
        public const int MapRadioMax = 2, MapRadioTwoFrom = 8;
        public const float MapRadioReach = 2.4f, MapRadioPanelReach = 3.2f;

        public NetworkList<MapRadioState> MapRadios;
        public NetworkList<FixedString128Bytes> MapRadioQueue0, MapRadioQueue1;
        public readonly NetworkVariable<RadioState> MapRadioMusic0 = new(RadioState.Default), MapRadioMusic1 = new(RadioState.Default);

        private RadioChannel[] _mapChannels;
        private bool _mapRadioSpawned;
        private readonly Vector3[] _mapRadioCarrierAt = new Vector3[MapRadioMax]; // last feet of the carrier (he may leave)

        public static readonly ItemPlacement.Profile RadioPlace = new ItemPlacement.Profile
        {
            HalfWidth = 0.2f, Height = 0.3f, Headroom = 0f, LosRadius = 0.1f,
            MinRadius = 0.35f, MaxRadius = 1.1f, Attempts = 12, Spacing = 0f,
        };

        public RadioChannel MapChannel(int i) => _mapChannels != null && i >= 0 && i < _mapChannels.Length ? _mapChannels[i] : null;

        private void InitMapRadio()
        {
            MapRadios = new NetworkList<MapRadioState>();
            MapRadioQueue0 = new NetworkList<FixedString128Bytes>();
            MapRadioQueue1 = new NetworkList<FixedString128Bytes>();
            bool CanPlay() => Phase.Value == MatchPhase.Hiding || Phase.Value == MatchPhase.Seeking || Phase.Value == MatchPhase.Ended;
            _mapChannels = new[]
            {
                new RadioChannel(this, "map0", MapRadioMusic0, MapRadioQueue0, "hns.mapradio0.ready", CanPlay, p => MayUseMapRadio(p, 0),
                    (me, op, url) => me.RequestMapRadioRpc(0, op, url)),
                new RadioChannel(this, "map1", MapRadioMusic1, MapRadioQueue1, "hns.mapradio1.ready", CanPlay, p => MayUseMapRadio(p, 1),
                    (me, op, url) => me.RequestMapRadioRpc(1, op, url)),
            };
            if (GetComponent<MapRadioSystem>() == null) gameObject.AddComponent<MapRadioSystem>();
        }

        /// <summary>World position of radio <paramref name="i"/> now (the carrier's shoulder while carried).</summary>
        public Vector3 MapRadioWhere(int i)
        {
            if (i < 0 || i >= MapRadios.Count) return Vector3.zero;
            var s = MapRadios[i];
            if (s.Mode == MapRadioState.Carried)
            {
                var c = NetPlayer.Find(s.Carrier);
                if (c != null) return c.transform.position + Vector3.up * 1.5f;
            }
            return s.Pos;
        }

        /// <summary>Index of the radio <paramref name="pl"/> carries (-1 = none).</summary>
        public int MapRadioOf(NetPlayer pl)
        {
            if (pl == null || MapRadios == null) return -1;
            for (int i = 0; i < MapRadios.Count; i++)
                if (MapRadios[i].Mode == MapRadioState.Carried && MapRadios[i].Carrier == pl.NetworkObjectId) return i;
            return -1;
        }

        /// <summary>Server: may <paramref name="pl"/> change radio <paramref name="i"/>'s music (alive hider next to it or carrying it).</summary>
        private bool MayUseMapRadio(NetPlayer pl, int i)
        {
            if (pl == null || !pl.IsAliveHider || i >= MapRadios.Count) return false;
            var s = MapRadios[i];
            if (s.Mode == MapRadioState.Carried && s.Carrier == pl.NetworkObjectId) return true;
            return Vector3.Distance(pl.transform.position + Vector3.up * 1f, MapRadioWhere(i)) <= MapRadioPanelReach + 0.8f;
        }

        // ================================================================ server

        private void UpdateMapRadioServer()
        {
            if (_mapChannels == null) return;
            var ph = Phase.Value;
            if (ph == MatchPhase.Lobby || ph == MatchPhase.Loading)
            {
                if (MapRadios.Count > 0)
                {
                    MapRadios.Clear();
                    foreach (var c in _mapChannels) c.ServerReset();
                }
                _mapRadioSpawned = false;
            }
            else if (!_mapRadioSpawned && ph == MatchPhase.Hiding && !RaceMode && SecurityRooms.Instance != null &&
                     SceneManager.GetActiveScene().name == NetSession.GameScene)
            {
                _mapRadioSpawned = true;
                int players = NetPlayer.All.Count;
                SpawnMapRadios(players >= MapRadioTwoFrom ? 2 : 1);
            }

            // A carrier that cannot hold it any more drops it.
            for (int i = 0; i < MapRadios.Count; i++)
            {
                var s = MapRadios[i];
                if (s.Mode != MapRadioState.Carried) continue;
                var c = NetPlayer.Find(s.Carrier);
                if (c != null) _mapRadioCarrierAt[i] = c.transform.position;
                bool round = ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended;
                if (c == null || !c.IsAliveHider || c.Motion.Value.IsProne || c.Seat.Value >= 0 || c.RcControlling || !round)
                    ServerMapRadioFall(i, c);
            }
            foreach (var c in _mapChannels) c.ServerUpdate();
        }

        /// <summary>Server: puts <paramref name="count"/> radios on pallets / crates (also used by tests).</summary>
        public void SpawnMapRadios(int count)
        {
            MapRadios.Clear();
            foreach (var c in _mapChannels) c.ServerReset();
            var spots = MapRadioSpots.Pick(Mathf.Clamp(count, 0, MapRadioMax));
            for (int i = 0; i < spots.Count; i++)
            {
                MapRadios.Add(new MapRadioState { Mode = MapRadioState.Lying, Pos = spots[i].Pos, Yaw = spots[i].Yaw, Move = MapRadioState.Placed, MoveAt = Now });
                Debug.Log($"[MapRadio] radio {i} at {spots[i].Pos} yaw {spots[i].Yaw:0} on {spots[i].Near} (of {spots[i].Candidates} spots)");
            }
        }

        /// <summary>Seconds the radio's last move still animates (no pickup meanwhile).</summary>
        private static float MoveTime(byte move) =>
            move == MapRadioState.Lifted ? ShoulderRadioRig.LiftTime : move == MapRadioState.PutDown ? ShoulderRadioRig.PutTime
            : move == MapRadioState.Fell ? ShoulderRadioRig.FallTime : 0f;

        public void ServerMapRadioPick(NetPlayer pl, int i)
        {
            var ph = Phase.Value;
            if (pl == null || i < 0 || i >= MapRadios.Count || (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking)) return;
            var s = MapRadios[i];
            if (s.Mode != MapRadioState.Lying || Now - s.MoveAt < MoveTime(s.Move) - 0.05) return;
            if (!pl.IsAliveHider || pl.Busy.Value || pl.Seat.Value >= 0 || pl.RcControlling || pl.HoldsPaMic || pl.Motion.Value.IsProne) return;
            if (MapRadioOf(pl) >= 0) return;
            Vector3 d = s.Pos - pl.transform.position;
            if (new Vector2(d.x, d.z).magnitude > MapRadioReach || d.y > 1.7f || d.y < -0.6f) return;
            s.Mode = MapRadioState.Carried;
            s.Carrier = pl.NetworkObjectId;
            s.Move = MapRadioState.Lifted;
            s.MoveAt = Now;
            MapRadios[i] = s;
            Debug.Log($"[MapRadio] {pl.Nick} lifts radio {i}");
        }

        public void ServerMapRadioPut(NetPlayer pl)
        {
            int i = MapRadioOf(pl);
            if (i < 0) return;
            var s = MapRadios[i];
            if (Now - s.MoveAt < ShoulderRadioRig.LiftTime - 0.05) return;
            Vector3 feet = pl.transform.position;
            Vector3 fwd = pl.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            s.Pos = ItemPlacement.PutDown(feet, feet + fwd * 0.6f, RadioPlace, "radio", pl.transform);
            s.Yaw = Quaternion.LookRotation(-fwd).eulerAngles.y; // the speakers face the player who put it down
            s.Mode = MapRadioState.Lying;
            s.LastCarrier = pl.NetworkObjectId;
            s.Carrier = 0;
            s.Move = MapRadioState.PutDown;
            s.MoveAt = Now;
            MapRadios[i] = s;
            Debug.Log($"[MapRadio] {pl.Nick} puts radio {i} down at {s.Pos}");
        }

        /// <summary>Server: the radio slips off <paramref name="pl"/>'s shoulder (down / dead / left / lying).</summary>
        private void ServerMapRadioFall(int i, NetPlayer pl)
        {
            var s = MapRadios[i];
            Vector3 feet = pl != null ? pl.transform.position : _mapRadioCarrierAt[i];
            Vector3 right = pl != null ? pl.transform.right : Vector3.right;
            Vector3 fwd = pl != null ? pl.transform.forward : Vector3.forward;
            right.y = fwd.y = 0f;
            if (pl != null || feet != Vector3.zero)
                s.Pos = ItemPlacement.PutDown(feet, feet + right.normalized * 0.55f + fwd.normalized * 0.15f, RadioPlace, "radio", pl != null ? pl.transform : null);
            s.Yaw = (pl != null ? pl.transform.eulerAngles.y + 90f : s.Yaw) + UnityEngine.Random.Range(-35f, 35f);
            s.Mode = MapRadioState.Lying;
            s.LastCarrier = s.Carrier;
            s.Carrier = 0;
            s.Move = MapRadioState.Fell;
            s.MoveAt = Now;
            MapRadios[i] = s;
            Debug.Log($"[MapRadio] radio {i} fell from {(pl != null ? pl.Nick : "a player who left")} at {s.Pos}");
        }

        /// <summary>Test hook (-testmapradio): knocks <paramref name="pl"/> down the normal way (the radio must fall).</summary>
        public void TestDownForMapRadio(NetPlayer pl)
        {
            if (IsServer && pl != null) Down(pl);
        }

        public void ServerMapRadioOp(NetPlayer pl, int i, RadioOp op, string url) => MapChannel(i)?.ServerOp(pl, op, url);
    }
}
