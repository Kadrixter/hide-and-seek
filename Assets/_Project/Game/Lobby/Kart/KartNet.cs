using System;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Driving state of the lobby go-kart, written by the driver's client ~30 times a second (owner-authoritative, like
    /// the RC cars): pose, world velocity and yaw rate (the others extrapolate along them), the controls (wheels, steering
    /// wheel, lights) and the engine / slide state (sound, smoke, skid marks on every client).
    /// </summary>
    public struct KartPack : INetworkSerializable, IEquatable<KartPack>
    {
        public const byte FlagHandbrake = 1, FlagSliding = 2, FlagBraking = 4, FlagReverse = 8;

        public Vector3 Pos;
        public short Yaw;       // degrees * 50
        public short VelX;      // m/s * 200
        public short VelZ;      // m/s * 200
        public short YawRate;   // deg/s * 10
        public sbyte Steer;     // front wheel angle, degrees (-40..40)
        public sbyte Throttle;  // -100..100
        public byte Rpm;        // engine 0..255 (idle .. limiter)
        public byte Slip;       // tyre screech / smoke strength 0..255
        public byte Flags;
        /// <summary>Kart epoch the pose belongs to (a reset to the parking spot bumps it; older packs are ignored).</summary>
        public byte Epoch;
        /// <summary>Which kart (0..<see cref="DriftTrackLayout.KartCount"/>-1).</summary>
        public byte Index;

        public float YawDeg => Yaw / 50f;
        public Vector3 Velocity => new Vector3(VelX / 200f, 0f, VelZ / 200f);
        public float YawRateDeg => YawRate / 10f;
        public bool Valid => Pos != Vector3.zero;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref VelX);
            s.SerializeValue(ref VelZ);
            s.SerializeValue(ref YawRate);
            s.SerializeValue(ref Steer);
            s.SerializeValue(ref Throttle);
            s.SerializeValue(ref Rpm);
            s.SerializeValue(ref Slip);
            s.SerializeValue(ref Flags);
            s.SerializeValue(ref Epoch);
            s.SerializeValue(ref Index);
        }

        public bool Equals(KartPack o) => (Pos - o.Pos).sqrMagnitude < 1e-8f && Yaw == o.Yaw && VelX == o.VelX && VelZ == o.VelZ &&
                                          YawRate == o.YawRate && Steer == o.Steer && Throttle == o.Throttle && Rpm == o.Rpm &&
                                          Slip == o.Slip && Flags == o.Flags && Epoch == o.Epoch && Index == o.Index;
    }

    /// <summary>
    /// Server-owned state of one track go-kart (inside <see cref="KartsState"/>): who drives it (none =
    /// <see cref="Free"/>), where it stands while nobody drives (the spot where the last driver got out) and the epoch,
    /// bumped every time the kart is put back in its parking slot (round start, R, stuck, off the track).
    /// </summary>
    public struct KartState : INetworkSerializable, IEquatable<KartState>
    {
        public const ulong Free = ulong.MaxValue;

        public ulong Driver;
        public Vector3 Pos;
        public float Yaw;
        public byte Epoch;

        public bool Driven => Driver != Free;

        /// <summary>Parked in its slot by the track gate (<see cref="DriftTrackLayout.KartSlot"/>), nobody in it.</summary>
        public static KartState AtSlot(int i, byte epoch) => new KartState { Driver = Free, Pos = DriftTrackLayout.KartSlot(i), Yaw = DriftTrackLayout.KartSlotYaw, Epoch = epoch };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Driver);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Epoch);
        }

        public bool Equals(KartState o) => Driver == o.Driver && (Pos - o.Pos).sqrMagnitude < 1e-8f && Mathf.Approximately(Yaw, o.Yaw) && Epoch == o.Epoch;
    }

    /// <summary>All five track karts' <see cref="KartState"/>s in one synced value (late joiners get them all at once).</summary>
    public struct KartsState : INetworkSerializable, IEquatable<KartsState>
    {
        public KartState K0, K1, K2, K3, K4;

        public static KartsState AtSlots(byte epoch) => new KartsState
        {
            K0 = KartState.AtSlot(0, epoch), K1 = KartState.AtSlot(1, epoch), K2 = KartState.AtSlot(2, epoch),
            K3 = KartState.AtSlot(3, epoch), K4 = KartState.AtSlot(4, epoch),
        };

        public KartState Get(int i) => i switch { 0 => K0, 1 => K1, 2 => K2, 3 => K3, _ => K4 };

        public KartsState With(int i, KartState k)
        {
            var s = this;
            switch (i) { case 0: s.K0 = k; break; case 1: s.K1 = k; break; case 2: s.K2 = k; break; case 3: s.K3 = k; break; default: s.K4 = k; break; }
            return s;
        }

        /// <summary>The kart <paramref name="id"/> drives (-1 = none).</summary>
        public int KartOf(ulong id)
        {
            for (int i = 0; i < DriftTrackLayout.KartCount; i++) if (Get(i).Driver == id) return i;
            return -1;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            K0.NetworkSerialize(s); K1.NetworkSerialize(s); K2.NetworkSerialize(s); K3.NetworkSerialize(s); K4.NetworkSerialize(s);
        }

        public bool Equals(KartsState o) => K0.Equals(o.K0) && K1.Equals(o.K1) && K2.Equals(o.K2) && K3.Equals(o.K3) && K4.Equals(o.K4);
    }
}
