using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The RC transmitter ("Pilot RC") as an inventory item. A carried car is one slot ("Samochodzik RC"); once it is
    /// put on the floor the same slot holds its transmitter for as long as the car stands there (driven or parked) -
    /// so leaving the car view never needs a free slot. LMB / 6 with the transmitter = steer the car again (within
    /// range), E on the own car = the car back into the hand (slot back to "Samochodzik RC"), Q = the transmitter goes
    /// on the floor as a normal dropped item (Extra = the car's slot; whoever picks it up steers that car).
    /// <para>
    /// When the car is crushed by the seeker or shot to pieces, the holder - first dropped out of the car view and up on
    /// their feet - swings the useless transmitter and smashes it on the floor (<see cref="RcSlotState.Remote"/> =
    /// Thrown: every peer plays the throw, the wreck stays there until the round ends, built from a seed, so late
    /// joiners see the same wreck). A downed holder throws it after getting up; a dead one loses it.
    /// Nobody else may take a car whose transmitter somebody has (or that lies on the floor).
    /// </para>
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Slot of the car this player carries in the hand (not on the floor), -1 = none.</summary>
        public int RcCarriedSlotOf(NetPlayer pl)
        {
            if (pl == null) return -1;
            for (int i = 0; i < RcCar.Count; i++)
            {
                var s = RcSlot(i);
                if (s.Holder == pl.NetworkObjectId && s.Mode == RcMode.Carried) return i;
            }
            return -1;
        }

        /// <summary>
        /// Slot of the car whose transmitter this player has (the car on the floor: driven / parked / under the boot, or
        /// destroyed with the transmitter not thrown away yet), -1 = none.
        /// </summary>
        public int RcRemoteSlotOf(NetPlayer pl)
        {
            if (pl == null) return -1;
            for (int i = 0; i < RcCar.Count; i++)
            {
                var s = RcSlot(i);
                if (s.Holder != pl.NetworkObjectId) continue;
                if (s.Mode is RcMode.Driving or RcMode.Parked or RcMode.Doomed) return i;
                if (s.Mode is RcMode.Wreck or RcMode.Shot)
                {
                    if (s.Remote == RcRemoteFate.InHand) return i;
                    if (s.Remote == RcRemoteFate.Thrown && Now < s.RemoteAt + RcRemoteTimeline.Release) return i; // still in the hand
                }
            }
            return -1;
        }

        /// <summary>The car of slot <paramref name="i"/> is crushed or shot.</summary>
        public bool RcDestroyed(int i) => i >= 0 && i < RcCar.Count && RcSlot(i).Mode is RcMode.Wreck or RcMode.Shot;

        // ================================================================ client: hotbar

        public string RcRemoteHint(NetPlayer p)
        {
            int i = RcRemoteSlotOf(p);
            if (i < 0) return "";
            var s = RcSlot(i);
            if (RcDestroyed(i)) return "samochodzik zniszczony - pilot do niczego";
            if (s.Mode == RcMode.Doomed) return "szukający depcze twój samochodzik!";
            var car = RcCar.Peek(i);
            Vector3 at = car != null && car.View != RcCar.ViewMode.Hidden ? car.transform.position : s.Pos;
            float d = Vector3.Distance(p.transform.position, at);
            if (d > RcRangeOf(i)) return $"samochodzik poza zasięgiem ({d:0} / {RcRangeOf(i):0} m)";
            return $"LPM / 6 - steruj samochodzikiem ({d:0} m)";
        }

        /// <summary>LMB on the selected transmitter: the same as key 6 (take control of the car again).</summary>
        public void UseRcRemote(NetPlayer p)
        {
            int i = RcRemoteSlotOf(p);
            if (i < 0) return;
            if (RcDestroyed(i)) { GameHud.Toast("Samochodzik zniszczony - ten pilot jest już do niczego.", 2.5f); return; }
            HotbarInput.Six = true;
        }

        /// <summary>E prompt over a transmitter lying on the floor.</summary>
        public string RcRemoteFloorText(NetPlayer me, byte slot)
        {
            if (RcDestroyed(slot)) return "E  pilot zniszczonego samochodzika (bezużyteczny)";
            if (RcSlotOf(me) >= 0 || RcRemoteSlotOf(me) >= 0) return "E  (masz już samochodzik)";
            return Inventory.PickupText(me, HotbarItem.RcRemote, "E  podnieś pilota RC (steruje samochodzikiem)");
        }

        // ================================================================ server: inventory

        /// <summary>Q on the transmitter: it goes on the floor (the car stays where it is, nobody can drive it).</summary>
        public bool ServerTakeRcRemote(NetPlayer pl, out byte slot)
        {
            slot = 0;
            int i = RcRemoteSlotOf(pl);
            if (i < 0) return false;
            var s = RcSlot(i);
            switch (s.Mode)
            {
                case RcMode.Parked:
                    SetRc(i, RcMode.Parked, RcDropHolder, s.Pos, s.Yaw);
                    slot = (byte)i;
                    Debug.Log($"[RcRemote] {pl.Nick} dropped the transmitter of car {i}");
                    return true;
                case RcMode.Doomed:
                    pl.ToastRpc("Szukający właśnie depcze twój samochodzik!", 2f);
                    return false;
                default:
                    return false; // driving (no hotbar) or destroyed (it is about to be smashed anyway)
            }
        }

        /// <summary>E on a transmitter on the floor: whoever takes it steers that car.</summary>
        public bool ServerGiveRcRemote(NetPlayer pl, byte slot)
        {
            if (pl == null || slot >= RcCar.Count) return false;
            var s = RcSlot(slot);
            if (RcDestroyed(slot))
            {
                pl.ToastRpc("Ten pilot jest już do niczego - jego samochodzik został zniszczony.", 3f);
                return false;
            }
            if (s.Mode != RcMode.Parked || s.Holder != RcDropHolder) return false;
            if (RcSlotOf(pl) >= 0 || RcRemoteSlotOf(pl) >= 0) { pl.ToastRpc("Masz już samochodzik.", 2f); return false; }
            SetRc(slot, RcMode.Parked, pl.NetworkObjectId, s.Pos, s.Yaw);
            Debug.Log($"[RcRemote] {pl.Nick} picked up the transmitter of car {slot}");
            return true;
        }

        /// <summary>The transmitter of a dead (or no longer hiding) holder falls next to the body.</summary>
        private void ServerSpillRcRemote(NetPlayer pl, int i)
        {
            var s = RcSlot(i);
            SetRc(i, RcMode.Parked, RcDropHolder, s.Pos, s.Yaw);
            Vector3 side = pl.transform.right;
            ThrowDrop(pl, HotbarItem.RcRemote, (byte)i, side, 0.4f, 0.9f, 0.8f, 0.5f,
                Quaternion.Euler(0f, pl.transform.eulerAngles.y + 90f, 0f));
            Debug.Log($"[RcRemote] {pl.Nick}: transmitter of car {i} fell next to the body");
        }

        /// <summary>Test hook (-testremote): a Glock hit on car <paramref name="i"/> at <paramref name="hitPoint"/>.</summary>
        public bool TestShootRc(NetPlayer shooter, int i, Vector3 hitPoint) =>
            IsServer && shooter != null && ServerShootRc(shooter, shooter.transform.position + Vector3.up * 1.5f, i, hitPoint);

        // ================================================================ server: the car is destroyed

        /// <summary>Called right after a car became a wreck (stomped / shot): its holder smashes the transmitter.</summary>
        private void ServerRcDestroyed(int i) => StartCoroutine(RemoteThrowRoutine(i));

        private IEnumerator RemoteThrowRoutine(int i)
        {
            ulong holder = RcSlot(i).Holder;
            if (holder == RcDropHolder || holder == ulong.MaxValue) yield break; // the transmitter lies on the floor / nobody has it
            yield return new WaitForSeconds(0.45f); // a moment to take it in (the driver first watches the wreck from the car)
            NetPlayer pl;
            while (true)
            {
                var s = RcSlot(i);
                if (!(s.Mode is RcMode.Wreck or RcMode.Shot) || s.Holder != holder || s.Remote != RcRemoteFate.InHand) yield break;
                var ph = Phase.Value;
                if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking && ph != MatchPhase.Ended) yield break;
                pl = NetPlayer.Find(holder);
                if (pl == null || !pl.IsSpawned || pl.Life.Value == LifeState.Dead || !pl.IsHider)
                {
                    s.Remote = RcRemoteFate.Lost;
                    RcVar(i).Value = s;
                    Debug.Log($"[RcRemote] transmitter of car {i} lost with its holder");
                    yield break;
                }
                // Out of the car view, standing (a downed holder throws it after getting up), nothing else going on.
                if (pl.Life.Value == LifeState.Alive && !pl.Busy.Value && !pl.RcAnimating && pl.Seat.Value < 0 && !pl.Motor.IsProne) break;
                yield return null;
            }

            Vector3 fwd = pl.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            float reach = 0.55f;
            Vector3 chest = pl.transform.position + Vector3.up * 0.9f;
            if (Physics.Raycast(chest, fwd, out var wall, 0.9f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore) && !wall.transform.IsChildOf(pl.transform))
                reach = Mathf.Clamp(wall.distance - 0.3f, 0.2f, reach);
            Vector3 land = FloorUnder(pl.transform.position, pl.transform.position + fwd * reach + pl.transform.right * 0.1f);
            float yaw = pl.transform.eulerAngles.y + Random.Range(-50f, 50f);
            var st = RcSlot(i);
            st.Remote = RcRemoteFate.Thrown;
            st.RemoteAt = Now;
            st.RemotePos = land;
            st.RemoteYaw = yaw;
            RcVar(i).Value = st;
            pl.Busy.Value = true;
            Debug.Log($"[RcRemote] {pl.Nick} smashes the transmitter of car {i} at {land}");
            yield return new WaitForSeconds(RcRemoteTimeline.End);
            if (pl != null && pl.IsSpawned) pl.Busy.Value = false;
        }
    }
}
