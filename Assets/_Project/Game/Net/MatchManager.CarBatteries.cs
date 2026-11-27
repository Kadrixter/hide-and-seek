using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Map;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>Tuning of the car batteries / camera power objective (26.11, 27.11: every bonnet opens, no wire needed).</summary>
    public static class BatteryTuning
    {
        /// <summary>Share of the batteries that are flat (0 %) this round; the rest hold <see cref="ChargeMin"/>..<see cref="ChargeMax"/>.</summary>
        public const double DeadShare = 0.5;
        /// <summary>Charge (per cent) of a working battery at the start of the round.</summary>
        public const int ChargeMin = 65, ChargeMax = 100;
        /// <summary>Seconds a full battery runs the cameras of one room (they only drain while switched on).</summary>
        public const float FullLasts = 360f;
        /// <summary>Lifting the bonnet: 3 s for a clean car up to 7 s for a forgotten one (+ a little random).</summary>
        public const float HoodTimeMin = 3f, HoodTimeMax = 7f;
        /// <summary>Server: how far (m, flat) from the latch / the battery the player may stand.</summary>
        public const float HoodReach = 1.9f, BatteryReach = 2.1f;
        /// <summary>Walking with the battery (heavy).</summary>
        public const float CarrySpeedScale = 0.92f;
        /// <summary>Latch mini-game: pins to set (more on dusty cars) and the least time a won try can take (server check).</summary>
        public const int PinsMin = 3, PinsMax = 5;
        public const float MinPinTime = 0.15f;
        /// <summary>Angle the bonnet pops up to once the latch is open.</summary>
        public const float UnlatchedAngle = 2.5f;

        public static int PinsFor(float dust) => Mathf.Clamp(PinsMin + Mathf.RoundToInt(dust * (PinsMax - PinsMin)), PinsMin, PinsMax);
    }

    /// <summary>256 flags, one per bonnet (<see cref="CarHood.Index"/>).</summary>
    public struct HoodMask : IEquatable<HoodMask>
    {
        public ulong A, B, C, D;

        public bool Get(int i)
        {
            if (i < 0 || i >= 256) return false;
            ulong w = (i >> 6) switch { 0 => A, 1 => B, 2 => C, _ => D };
            return (w & (1ul << (i & 63))) != 0;
        }

        public HoodMask With(int i)
        {
            if (i < 0 || i >= 256) return this;
            ulong bit = 1ul << (i & 63);
            switch (i >> 6) { case 0: A |= bit; break; case 1: B |= bit; break; case 2: C |= bit; break; default: D |= bit; break; }
            return this;
        }

        public bool Any => (A | B | C | D) != 0;

        public void Serialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref A);
            s.SerializeValue(ref B);
            s.SerializeValue(ref C);
            s.SerializeValue(ref D);
        }

        public bool Equals(HoodMask o) => A == o.A && B == o.B && C == o.C && D == o.D;
    }

    /// <summary>Per-bonnet state of the round (flag i = <see cref="CarHood.Index"/> i).</summary>
    public struct HoodBits : INetworkSerializable, IEquatable<HoodBits>
    {
        /// <summary>This bonnet can be opened this round (27.11: all of them once the round is dealt).</summary>
        public HoodMask Active;
        /// <summary>The latch is open (lifting it needs no mini-game any more).</summary>
        public HoodMask Unlatched;
        /// <summary>Fully open (stays open for the rest of the round).</summary>
        public HoodMask Open;
        /// <summary>The battery was taken out.</summary>
        public HoodMask Taken;

        public static bool Has(HoodMask mask, int i) => mask.Get(i);
        public static HoodMask With(HoodMask mask, int i) => mask.With(i);

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            Active.Serialize(s);
            Unlatched.Serialize(s);
            Open.Serialize(s);
            Taken.Serialize(s);
        }

        public bool Equals(HoodBits o) => Active.Equals(o.Active) && Unlatched.Equals(o.Unlatched) && Open.Equals(o.Open) && Taken.Equals(o.Taken);
    }

    /// <summary>
    /// Car batteries (26.11): the security cameras start without power; a working car battery has to be found under a
    /// bonnet. Since 27.11 every parked car's bonnet opens (<see cref="CarHood"/>) and half of the batteries are flat
    /// (<see cref="BatteryTuning.DeadShare"/>, shuffled per level). A hider opens the bonnet latch by hand (the local
    /// latch mini-game, LockpickUi; a slip just starts it again - no wire needed any more), then lifts the bonnet in
    /// 3..7 s depending on the car's dust (it creaks up
    /// on every client; walking away, getting hit or E stops it and the bonnet drops back - the latch stays picked),
    /// and takes the battery (the "Akumulator" item, its charge travels with it). The battery powers the cameras of a
    /// security room (MatchManager.CamPower.cs). Synced: <see cref="CarHoods"/>, the players' <see cref="NetPlayer.HoodWork"/>
    /// and <see cref="NetPlayer.CarBattery"/> - late joiners see open bonnets and missing batteries.
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<HoodBits> CarHoods = new();

        private const int MaxHoods = 256;
        private readonly int[] _hoodCharge = new int[MaxHoods];      // server: charge (%) of the battery under each bonnet, 0 = dead
        private readonly float[] _hoodCreakAt = new float[MaxHoods]; // client: next creak sound of a bonnet being lifted
        private readonly HashSet<ulong> _hoodCancel = new();
        private bool _hoodsDealt;
        private Action _stepCarBatteries;

        private bool HoodPhase => (Phase.Value == MatchPhase.Hiding || Phase.Value == MatchPhase.Seeking) && !RaceMode;

        /// <summary>Hood by its index (null when the map has none such).</summary>
        public static CarHood HoodAt(int i)
        {
            var all = CarHood.All;
            if (i < 0) return null;
            if (i < all.Count && all[i].Index == i) return all[i];
            for (int k = 0; k < all.Count; k++) if (all[k] != null && all[k].Index == i) return all[k];
            return null;
        }

        /// <summary>Player working on bonnet <paramref name="i"/> (null = nobody).</summary>
        public NetPlayer HoodWorker(int i)
        {
            foreach (var p in NetPlayer.All)
                if (p != null && p.HoodWork.Value.Active && p.HoodWork.Value.Hood == i) return p;
            return null;
        }

        /// <summary>Server / tests: charge (%) of the battery under bonnet <paramref name="i"/> this round (0 = dead).</summary>
        public int ServerHoodCharge(int i) => i >= 0 && i < MaxHoods ? _hoodCharge[i] : 0;

        /// <summary>Seconds it takes to lift a bonnet of a car with <paramref name="dust"/> (0..1), without the random part.</summary>
        public static float HoodTime(float dust) => Mathf.Lerp(BatteryTuning.HoodTimeMin, BatteryTuning.HoodTimeMax, Mathf.Clamp01(dust));

        // ================================================================ every peer

        /// <summary>Per frame (MatchManager.Update): bonnet angles and batteries in sight, creaks; server: dealing, resets, camera power.</summary>
        private void UpdateCarBatteries()
        {
            if (NetPlayer.Local != null) HideAndSeek.Game.UI.LockpickUi.Ensure();
            var bits = CarHoods.Value;
            var all = CarHood.All;
            double now = Now;
            float dt = Time.deltaTime;
            for (int k = 0; k < all.Count; k++)
            {
                var h = all[k];
                if (h == null) continue;
                int i = h.Index;
                float target = 0f;
                bool lifting = false;
                if (HoodBits.Has(bits.Open, i)) target = h.OpenAngle;
                else
                {
                    var w = HoodWorker(i);
                    if (w != null && w.HoodWork.Value.Mode == HoodWorkMode.Opening)
                    {
                        float t = Mathf.Clamp01((float)(now - w.HoodWork.Value.Start) / Mathf.Max(0.1f, w.HoodWork.Value.Duration));
                        target = HoodLift(t) * h.OpenAngle;
                        lifting = true;
                    }
                    else if (HoodBits.Has(bits.Unlatched, i)) target = BatteryTuning.UnlatchedAngle;
                }
                float cur = h.Angle;
                // follows the lift closely, drops back fast when let go
                float speed = target >= cur ? (lifting ? 60f : 90f) : 140f;
                h.SetOpenAngle(Mathf.MoveTowards(cur, target, speed * dt));
                h.SetBatteryVisible(!HoodBits.Has(bits.Taken, i));
                if (lifting && i < MaxHoods && Time.time >= _hoodCreakAt[i])
                {
                    _hoodCreakAt[i] = Time.time + UnityEngine.Random.Range(0.45f, 0.95f);
                    WireSounds.Creak(h.FrontWorld, 1.3f);
                }
            }

            if (!IsServer) return;
            var ph = Phase.Value;
            if (ph == MatchPhase.Lobby || ph == MatchPhase.Loading)
            {
                _hoodsDealt = false;
                if (!CarHoods.Value.Equals(default(HoodBits))) CarHoods.Value = default;
                foreach (var pl in NetPlayer.All)
                {
                    if (pl == null) continue;
                    if (pl.CarBattery.Value != 0) pl.CarBattery.Value = 0;
                    if (pl.HoodWork.Value.Hood >= 0) pl.HoodWork.Value = HoodWorkState.None;
                }
            }
            else if (HoodPhase && !_hoodsDealt && all.Count > 0 && SceneManager.GetActiveScene().name == NetSession.GameScene)
                DealHoods();
            // a lockpick ends when its player is downed / attacked / the round is over (the lift has its own routine)
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || pl.HoodWork.Value.Mode != HoodWorkMode.Lockpick) continue;
                if (!HoodPhase || !pl.IsAliveHider || pl.Busy.Value) pl.HoodWork.Value = HoodWorkState.None;
            }
            UpdateCamPower(dt); // MatchManager.CamPower.cs
        }

        /// <summary>Bonnet lift curve 0..1: a rusty start (a few degrees, stuck), then it creaks up.</summary>
        public static float HoodLift(float t)
        {
            if (t < 0.18f) return 0.06f * (t / 0.18f) + 0.01f * Mathf.Sin(t * 90f);
            float u = (t - 0.18f) / 0.82f;
            return 0.06f + 0.94f * Mathf.SmoothStep(0f, 1f, u) + 0.012f * Mathf.Sin(u * 37f) * (1f - u);
        }

        // ================================================================ server: the round's bonnets

        private void DealHoods()
        {
            _hoodsDealt = true;
            var rnd = new System.Random(Environment.TickCount ^ 0x5EED);
            var all = CarHood.All;
            var levels = new List<int>[3] { new(), new(), new() };
            foreach (var h in all)
                if (h != null && h.Index < MaxHoods) levels[Mathf.Clamp(h.Level, 0, 2)].Add(h.Index);
            var bits = new HoodBits();
            Array.Clear(_hoodCharge, 0, _hoodCharge.Length);
            int working = 0, dead = 0;
            var sb = new System.Text.StringBuilder();
            for (int lv = 0; lv < 3; lv++)
            {
                var l = levels[lv];
                for (int k = l.Count - 1; k > 0; k--) { int j = rnd.Next(k + 1); (l[k], l[j]) = (l[j], l[k]); }
                // exactly half of each level flat (an odd one out decided by a coin)
                int flat = (int)Math.Floor(l.Count * BatteryTuning.DeadShare) + (l.Count % 2 == 1 && rnd.Next(2) == 0 ? 1 : 0);
                int lvWork = 0;
                for (int k = 0; k < l.Count; k++)
                {
                    int i = l[k];
                    bits.Active = HoodBits.With(bits.Active, i);
                    bool works = k >= flat;
                    _hoodCharge[i] = works ? rnd.Next(BatteryTuning.ChargeMin, BatteryTuning.ChargeMax + 1) : 0;
                    if (works) { working++; lvWork++; } else dead++;
                }
                sb.Append($" P {(lv == 0 ? "0" : "-" + lv)}: {l.Count} ({lvWork} working)");
            }
            CarHoods.Value = bits;
            Debug.Log($"[Battery] round bonnets: all {all.Count} open-able, {working} batteries working, {dead} flat:{sb}");
        }

        /// <summary>Why <paramref name="pl"/> cannot work on bonnet <paramref name="i"/> now (null = can). Also used by the prompt.</summary>
        public string HoodBlock(NetPlayer pl, int i)
        {
            if (pl == null || !HoodPhase || !pl.IsAliveHider) return "phase";
            if (pl.Busy.Value || pl.Seat.Value >= 0 || pl.RcAnimating || pl.WirePull.Value.Spot >= 0 || pl.AntennaSwapAt.Value >= 0d) return "hands";
            var bits = CarHoods.Value;
            if (!HoodBits.Has(bits.Active, i) || HoodAt(i) == null) return "inactive";
            if (HoodBits.Has(bits.Open, i)) return "open";
            var w = HoodWorker(i);
            if (w != null && w != pl) return "busy";
            return null;
        }

        private bool NearHood(NetPlayer pl, CarHood h, float reach, Vector3 point)
        {
            Vector3 at = pl.transform.position;
            return Mathf.Abs(at.y - h.transform.position.y) < 1.4f && Vector3.Distance(Flat(at), Flat(point)) <= reach;
        }

        public void ServerHoodRequest(NetPlayer pl, byte hood)
        {
            string block = HoodBlock(pl, hood);
            if (block != null)
            {
                Refused(pl, "bonnet #" + hood, block);
                if (block == "busy") pl.ToastRpc("Ktoś już grzebie przy tej masce.", 2f);
                return;
            }
            if (pl.HoodWork.Value.Active) { Refused(pl, "bonnet #" + hood, "already working"); return; }
            var h = HoodAt(hood);
            if (!NearHood(pl, h, BatteryTuning.HoodReach, h.FrontWorld)) { Refused(pl, "bonnet #" + hood, $"too far ({Vector3.Distance(Flat(pl.transform.position), Flat(h.FrontWorld)):0.00} m)"); return; }
            if (HoodBits.Has(CarHoods.Value.Unlatched, hood)) { StartHoodOpening(pl, h); return; }
            pl.HoodWork.Value = new HoodWorkState { Hood = (short)hood, Mode = HoodWorkMode.Lockpick, Start = Now };
            Debug.Log($"[Battery] {pl.Nick} works the bonnet latch of {h.name} (#{hood}, dust {h.Dust:0.0}, {BatteryTuning.PinsFor(h.Dust)} pins)");
        }

        public void ServerLockpickResult(NetPlayer pl, bool won, byte pins)
        {
            if (pl == null) return;
            var w = pl.HoodWork.Value;
            if (!w.Active || w.Mode != HoodWorkMode.Lockpick) return;
            var h = HoodAt(w.Hood);
            if (h == null || HoodBlock(pl, w.Hood) != null || !NearHood(pl, h, BatteryTuning.HoodReach + 0.6f, h.FrontWorld))
            {
                pl.HoodWork.Value = HoodWorkState.None;
                return;
            }
            if (!won)
            {
                // 27.11: no wire to lose - the latch just slips and the mini-game starts again
                CarHoodFxRpc(2, h.FrontWorld);
                Debug.Log($"[Battery] {pl.Nick}: the latch of {h.name} slipped, trying again");
                pl.HoodWork.Value = new HoodWorkState { Hood = w.Hood, Mode = HoodWorkMode.Lockpick, Start = Now };
                return;
            }
            int need = BatteryTuning.PinsFor(h.Dust);
            if (pins < need || Now - w.Start < need * BatteryTuning.MinPinTime)
            {
                Debug.LogWarning($"[Battery] {pl.Nick}: lockpick win refused ({pins}/{need} pins after {Now - w.Start:0.00} s)");
                pl.HoodWork.Value = HoodWorkState.None;
                return;
            }
            var bits = CarHoods.Value;
            bits.Unlatched = HoodBits.With(bits.Unlatched, w.Hood);
            CarHoods.Value = bits;
            CarHoodFxRpc(1, h.FrontWorld);
            Debug.Log($"[Battery] {pl.Nick} picked the latch of {h.name} in {Now - w.Start:0.0} s");
            StartHoodOpening(pl, h);
        }

        public void ServerHoodStop(NetPlayer pl)
        {
            if (pl == null) return;
            var w = pl.HoodWork.Value;
            if (!w.Active) return;
            if (w.Mode == HoodWorkMode.Lockpick) pl.HoodWork.Value = HoodWorkState.None;
            else _hoodCancel.Add(pl.NetworkObjectId);
        }

        private void StartHoodOpening(NetPlayer pl, CarHood h)
        {
            float dur = Mathf.Clamp(HoodTime(h.Dust) + UnityEngine.Random.Range(-0.25f, 0.5f), BatteryTuning.HoodTimeMin, BatteryTuning.HoodTimeMax);
            pl.HoodWork.Value = new HoodWorkState { Hood = (short)h.Index, Mode = HoodWorkMode.Opening, Start = Now, Duration = dur };
            _hoodCancel.Remove(pl.NetworkObjectId);
            StartCoroutine(HoodOpenRoutine(pl, h.Index, dur));
            Debug.Log($"[Battery] {pl.Nick} lifts the bonnet of {h.name} ({dur:0.0} s, dust {h.Dust:0.0})");
        }

        private IEnumerator HoodOpenRoutine(NetPlayer pl, int i, float dur)
        {
            ulong id = pl.NetworkObjectId;
            double start = Now;
            Vector3 pos0 = pl.transform.position;
            bool wounded0 = pl.Wounded.Value;
            byte severed0 = pl.Severed.Value;
            string why = null;
            while (Now - start < dur)
            {
                yield return null;
                if (pl == null || !pl.IsSpawned) { why = "player gone"; break; }
                var w = pl.HoodWork.Value;
                if (pl.Life.Value != LifeState.Alive) why = "downed";
                else if (pl.Busy.Value || pl.Severed.Value != severed0 || (pl.Wounded.Value && !wounded0)) why = "attacked";
                else if (!HoodPhase) why = "round over";
                else if (_hoodCancel.Remove(id)) why = "let go";
                else if (Now - start > 0.3 && Vector3.Distance(Flat(pl.transform.position), Flat(pos0)) > 0.8f) why = "walked away";
                else if (w.Hood != i || w.Mode != HoodWorkMode.Opening) why = "state changed";
                if (why != null) break;
            }
            _hoodCancel.Remove(id);
            if (pl == null || !pl.IsSpawned) yield break;
            if (pl.HoodWork.Value.Hood == i) pl.HoodWork.Value = HoodWorkState.None;
            var h = HoodAt(i);
            if (why != null)
            {
                Debug.Log($"[Battery] {pl.Nick}: bonnet #{i} dropped back ({why}) after {Now - start:0.0} s");
                if (why != "let go") pl.ToastRpc("Przerwane - maska opadła.", 1.8f);
                if (h != null) CarHoodFxRpc(2, h.FrontWorld);
                yield break;
            }
            var bits = CarHoods.Value;
            bits.Open = HoodBits.With(bits.Open, i);
            CarHoods.Value = bits;
            if (h != null) CarHoodFxRpc(3, h.FrontWorld);
            Debug.Log($"[Battery] {pl.Nick} opened the bonnet #{i} ({dur:0.0} s)");
        }

        // ================================================================ server: the battery

        public void ServerBatteryTake(NetPlayer pl, byte hood)
        {
            if (pl == null) return;
            if (!HoodPhase || !pl.IsAliveHider || pl.Busy.Value) { Refused(pl, "battery #" + hood, "phase / not an alive hider / busy"); return; }
            var bits = CarHoods.Value;
            var h = HoodAt(hood);
            if (h == null || !HoodBits.Has(bits.Open, hood) || HoodBits.Has(bits.Taken, hood)) { Refused(pl, "battery #" + hood, "bonnet not open / battery gone"); return; }
            if (!NearHood(pl, h, BatteryTuning.BatteryReach, h.BatteryWorld)) { Refused(pl, "battery #" + hood, $"too far ({Vector3.Distance(Flat(pl.transform.position), Flat(h.BatteryWorld)):0.00} m)"); return; }
            if (pl.HasCarBattery) { pl.ToastRpc("Masz już akumulator - drugiego nie uniesiesz.", 2.5f); return; }
            if (!Inventory.HasRoomFor(pl, HotbarItem.CarBattery)) { pl.ToastRpc(Inventory.FullText, 2.5f); return; }
            bits.Taken = HoodBits.With(bits.Taken, hood);
            CarHoods.Value = bits;
            int charge = hood < MaxHoods ? _hoodCharge[hood] : 0;
            pl.CarBattery.Value = (byte)(1 + Mathf.Clamp(charge, 0, 100));
            CarHoodFxRpc(4, h.BatteryWorld);
            Debug.Log($"[Battery] {pl.Nick} took the battery of {h.name} (#{hood}, charge {charge}%)");
        }

        /// <summary>Server log of a refused request (tests, bug reports).</summary>
        private static void Refused(NetPlayer pl, string what, string why) => Debug.Log($"[Battery] {(pl != null ? pl.Nick : "?")}: {what} refused ({why})");

        /// <summary>Sounds of the bonnets / batteries: 0 wire snaps, 1 latch pops, 2 bonnet drops, 3 bonnet open, 4 battery out, 5 battery in, 6 switch.</summary>
        [Rpc(SendTo.Everyone)]
        private void CarHoodFxRpc(byte kind, Vector3 pos)
        {
            switch (kind)
            {
                case 0: WireSounds.Snap(pos); break;
                case 1: WireSounds.Pop(pos); break;
                case 2: WireSounds.Clink(pos); WireSounds.Rattle(pos, 0.6f); break;
                case 3: WireSounds.Creak(pos, 1.5f); WireSounds.Seat(pos); break;
                case 4: WireSounds.Seat(pos); break;
                case 5: WireSounds.Seat(pos); WireSounds.Twist(pos); break;
                case 6: WireSounds.Pop(pos); break;
            }
        }
    }
}
