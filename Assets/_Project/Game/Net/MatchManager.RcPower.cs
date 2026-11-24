using System;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Constants of the RC car "energising" (R while steering) and of the boost.</summary>
    public static class RcPower
    {
        public const float ChargeTime = 8f;          // the car crackles with lightning this long
        public const float Cooldown = 25f;           // after the charge ran out, before the next one
        /// <summary>Electrocution of a player the charged car drove into = the standing part of the fork-in-the-socket shock (2 s).</summary>
        public const float ZapTime = ShockTimeline.ShockEnd - ShockTimeline.Insert;
        public const float HitRadius = 0.6f;         // car centre to the player's feet (horizontal), server side (20 Hz pose)
        public const float HitHeight = 1.0f;
        /// <summary>Boost (Shift) top speed = this times the seeker's sprint speed.</summary>
        public const float BoostOverSeeker = 1.08f;

        /// <summary>Sprint speed of a healthy seeker (his motor's run speed; slowdowns like aiming / foam are not counted).</summary>
        public static float SeekerSprint(NetPlayer fallback)
        {
            foreach (var p in NetPlayer.All)
                if (p != null && p.IsSeeker && p.Motor != null) return p.Motor.RunSpeed;
            return fallback != null && fallback.Motor != null ? fallback.Motor.RunSpeed : 5.8f;
        }

        /// <summary>Top speed of the RC car with Shift held: 8 % faster than the seeker's sprint.</summary>
        public static float BoostSpeed(NetPlayer driver) => SeekerSprint(driver) * BoostOverSeeker;
    }

    /// <summary>Server time each car was energised (0 = not this round).</summary>
    public struct RcPowerState : INetworkSerializable, IEquatable<RcPowerState>
    {
        public double At0, At1, At2, At3;

        public double At(int i) => i switch { 0 => At0, 1 => At1, 2 => At2, 3 => At3, _ => 0d };

        public void Set(int i, double t)
        {
            switch (i) { case 0: At0 = t; break; case 1: At1 = t; break; case 2: At2 = t; break; case 3: At3 = t; break; }
        }

        public bool Any => At0 != 0d || At1 != 0d || At2 != 0d || At3 != 0d;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref At0);
            s.SerializeValue(ref At1);
            s.SerializeValue(ref At2);
            s.SerializeValue(ref At3);
        }

        public bool Equals(RcPowerState o) => At0 == o.At0 && At1 == o.At1 && At2 == o.At2 && At3 == o.At3;
    }

    /// <summary>
    /// Energised RC car: R while steering charges the car for <see cref="RcPower.ChargeTime"/> s (lightning arcs over the
    /// body and to the floor, blue glow, buzzing - <see cref="RcChargeFx"/>), then <see cref="RcPower.Cooldown"/> s
    /// before the next charge. The server checks the driven car against every standing player: whoever it touches while
    /// charged (hider or seeker) is electrocuted (<see cref="NetPlayer.ZapAt"/>), once per player per charge.
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<RcPowerState> RcPowerVar = new();

        private readonly HashSet<ulong>[] _rcZapped = { new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>() };

        /// <summary>Seconds since car <paramref name="i"/> was energised (&lt; 0 = never this round).</summary>
        public float RcChargeElapsed(int i)
        {
            if (i < 0 || i >= RcCar.Count) return -1f;
            double at = RcPowerVar.Value.At(i);
            return at > 0d ? Mathf.Max(0f, (float)(Now - at)) : -1f;
        }

        /// <summary>Seconds of charge left (0 = not charged).</summary>
        public float RcChargeLeft(int i)
        {
            float e = RcChargeElapsed(i);
            return e >= 0f && e < RcPower.ChargeTime ? RcPower.ChargeTime - e : 0f;
        }

        public bool RcCharged(int i) => RcChargeLeft(i) > 0f;

        /// <summary>Seconds until car <paramref name="i"/> can be energised again (0 = ready, also while charged it is &gt; 0).</summary>
        public float RcChargeCooldown(int i)
        {
            float e = RcChargeElapsed(i);
            return e >= 0f ? Mathf.Max(0f, RcPower.ChargeTime + RcPower.Cooldown - e) : 0f;
        }

        // ================================================================ server

        /// <summary>R while steering: the car of slot <paramref name="i"/> is energised.</summary>
        public void ServerRcCharge(NetPlayer pl, int i)
        {
            if (pl == null || i < 0 || i >= RcCar.Count || !RcPhase || !pl.IsAliveHider) return;
            var s = RcSlot(i);
            if (s.Holder != pl.NetworkObjectId || s.Mode != RcMode.Driving) return;
            float cd = RcChargeCooldown(i);
            if (cd > 0f)
            {
                if (!RcCharged(i)) pl.ToastRpc($"Akumulator się ładuje - jeszcze {Mathf.CeilToInt(cd)} s.", 2f);
                return;
            }
            var st = RcPowerVar.Value;
            st.Set(i, Now);
            RcPowerVar.Value = st;
            _rcZapped[i].Clear();
            Debug.Log($"[RcPower] {pl.Nick} energised car {i} at {Now:0.00}");
        }

        /// <summary>Every peer, every frame (from <see cref="UpdateRcCars"/>): server hit checks, then the arcs on the cars.</summary>
        private void UpdateRcPower(bool inGame)
        {
            if (IsServer)
            {
                var st = RcPowerVar.Value;
                if (!RcPhase && st.Any)
                {
                    RcPowerVar.Value = default;
                    foreach (var z in _rcZapped) z.Clear();
                }
                else if (RcPhase) ServerRcZapCheck();
            }
            for (int i = 0; i < RcCar.Count; i++)
            {
                var car = RcCar.Peek(i);
                if (car == null) continue;
                var v = car.View;
                bool show = inGame && RcCharged(i) && v != RcCar.ViewMode.Hidden && v != RcCar.ViewMode.Shelf && v != RcCar.ViewMode.Wreck;
                RcChargeFx.Set(car, show, show ? RcChargeElapsed(i) : 0f);
            }
        }

        private void ServerRcZapCheck()
        {
            for (int i = 0; i < RcCar.Count; i++)
            {
                if (!RcCharged(i)) continue;
                var s = RcSlot(i);
                if (s.Mode != RcMode.Driving) continue;
                RcWhere(i, out var pos, out _);
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || !p.IsSpawned || p.NetworkObjectId == s.Holder || _rcZapped[i].Contains(p.NetworkObjectId)) continue;
                    if (!CanBeZapped(p)) continue;
                    Vector3 d = p.transform.position - pos;
                    if (Mathf.Abs(d.y) > RcPower.HitHeight) continue;
                    d.y = 0f;
                    if (d.sqrMagnitude > RcPower.HitRadius * RcPower.HitRadius) continue;
                    _rcZapped[i].Add(p.NetworkObjectId);
                    p.ServerStartZap(i);
                    var driver = NetPlayer.Find(s.Holder);
                    if (driver != null) driver.ToastRpc($"Poraziłeś prądem: {p.Nick}!", 2.5f);
                    p.ToastRpc("Porażenie prądem! Samochodzik RC był pod napięciem.", 2.5f);
                }
            }
        }

        /// <summary>Standing on the own feet, nothing else going on (no seat, no RC steering, no scripted action).</summary>
        private static bool CanBeZapped(NetPlayer p)
        {
            if (p.Life.Value != LifeState.Alive || p.Busy.Value || p.Zapped || p.Stunned) return false;
            if (p.Seat.Value >= 0 || p.RcAnimating || p.BodyCarriedNow || p.IsCarryingBody) return false;
            return true;
        }
    }
}
