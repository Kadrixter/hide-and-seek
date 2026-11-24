using System.Collections;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The two RC rally cars on the shelf in the generator room. A hider picks one up (E), key 6 puts it on the
    /// floor and switches to the car camera while the character sits and steers; 6 again parks it. The seeker
    /// stomps on a car with F: it is crushed for the rest of the round and its driver drops out of the car view.
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<RcSlotState> Rc0 = new(RcSlotState.OnShelf);
        public readonly NetworkVariable<RcSlotState> Rc1 = new(RcSlotState.OnShelf);

        public NetworkVariable<RcSlotState> RcVar(int i) => i == 0 ? Rc0 : Rc1;
        public RcSlotState RcSlot(int i) => RcVar(i).Value;

        private bool _rcHooked;

        /// <summary>Slot of the car this player has (carried / on the floor / being driven), -1 = none.</summary>
        public int RcSlotOf(NetPlayer pl)
        {
            if (pl == null) return -1;
            for (int i = 0; i < RcCar.Count; i++)
            {
                var s = RcSlot(i);
                if (s.Holder == pl.NetworkObjectId && s.Mode is RcMode.Carried or RcMode.Driving or RcMode.Parked or RcMode.Doomed) return i;
            }
            return -1;
        }

        private bool RcPhase => Phase.Value == MatchPhase.Hiding || Phase.Value == MatchPhase.Seeking;

        // ================================================================ every peer: car views

        private void UpdateRcCars()
        {
            if (!_rcHooked)
            {
                _rcHooked = true;
                Rc0.OnValueChanged += (o, n) => OnRcChanged(0, o, n);
                Rc1.OnValueChanged += (o, n) => OnRcChanged(1, o, n);
            }
            bool inGame = SceneManager.GetActiveScene().name == NetSession.GameScene &&
                          (Phase.Value == MatchPhase.Hiding || Phase.Value == MatchPhase.Seeking || Phase.Value == MatchPhase.Ended);
            if (!inGame) return;
            RcCarShelf.Ensure();
            RcRemoteFx.Ensure(); // wrecks of smashed transmitters (also for late joiners)
            for (int i = 0; i < RcCar.Count; i++)
            {
                var s = RcSlot(i);
                var car = RcCar.Get(i);
                switch (s.Mode)
                {
                    case RcMode.Shelf:
                        car.ShowOnShelf(RcCarShelf.CarPos(i), RcCarShelf.CarYaw(i));
                        break;
                    case RcMode.Carried:
                        car.Hide();
                        break;
                    case RcMode.Driving:
                    {
                        var holder = NetPlayer.Find(s.Holder);
                        if (holder == null) car.ShowParked(s.Pos, s.Yaw);
                        else if (holder.RcCarInHand) car.Hide();
                        else if (holder.IsOwner) car.EnsureLocal(s.Pos, s.Yaw);
                        else car.EnsureRemote(holder.RcDrive.Value, s.Pos, s.Yaw);
                        break;
                    }
                    case RcMode.Parked:
                        car.ShowParked(s.Pos, s.Yaw);
                        break;
                    case RcMode.Doomed:
                        car.Freeze(s.Pos, s.Yaw);
                        break;
                    case RcMode.Wreck:
                        car.Crush(s.Pos, s.Yaw);
                        break;
                    case RcMode.Shot:
                        car.ShotDown(s.Pos, s.Yaw);
                        break;
                }
            }
        }

        private void OnRcChanged(int i, RcSlotState old, RcSlotState now)
        {
            // The holder animates (sit down / stand up) and the local driver switches cameras.
            var a = NetPlayer.Find(old.Holder);
            var b = NetPlayer.Find(now.Holder);
            a?.OnRcSlotChanged(i, old, now);
            if (b != null && b != a) b.OnRcSlotChanged(i, old, now);
        }

        // ================================================================ server

        private void ResetRcCars()
        {
            Rc0.Value = RcSlotState.OnShelf;
            Rc1.Value = RcSlotState.OnShelf;
        }

        private void SetRc(int i, RcMode mode, ulong holder, Vector3 pos, float yaw)
            => RcVar(i).Value = new RcSlotState { Mode = mode, Holder = holder, Pos = pos, Yaw = yaw };

        /// <summary>Where the car of slot <paramref name="i"/> is right now (the driver's last reported pose while driving).</summary>
        private void RcWhere(int i, out Vector3 pos, out float yaw)
        {
            var s = RcSlot(i);
            pos = s.Pos;
            yaw = s.Yaw;
            if (s.Mode != RcMode.Driving) return;
            var d = NetPlayer.Find(s.Holder);
            if (d == null) return;
            var pk = d.RcDrive.Value;
            if (pk.Pos != Vector3.zero && (pk.Pos - s.Pos).sqrMagnitude < RcRangeOf(i) * RcRangeOf(i) * 4f)
            {
                pos = pk.Pos;
                yaw = pk.YawDeg;
            }
        }

        /// <summary>Drops the driver out of the car (car stays where it is).</summary>
        public void ServerRcForcePark(NetPlayer pl)
        {
            int i = RcSlotOf(pl);
            if (i < 0 || RcSlot(i).Mode != RcMode.Driving) return;
            RcWhere(i, out var pos, out float yaw);
            SetRc(i, RcMode.Parked, pl.NetworkObjectId, pos, yaw);
        }

        private void UpdateRcServer()
        {
            for (int i = 0; i < RcCar.Count; i++)
            {
                var s = RcSlot(i);
                if (s.Mode is RcMode.Shelf or RcMode.Wreck or RcMode.Doomed or RcMode.Shot) continue;
                if (s.Holder == RcDropHolder) continue; // lying on the floor as a dropped item / its transmitter does (MatchManager.Drops)
                if (s.Holder == ulong.MaxValue) continue; // on the floor, nobody has its transmitter (anybody can take it)
                var pl = NetPlayer.Find(s.Holder);
                if (pl == null)
                {
                    // Holder left: a driven / parked car stays on the floor (free for anybody), a carried one goes back on the shelf.
                    if (s.Mode == RcMode.Carried) RcVar(i).Value = RcSlotState.OnShelf;
                    else if (s.Mode is RcMode.Driving or RcMode.Parked) SetRc(i, RcMode.Parked, ulong.MaxValue, s.Pos, s.Yaw);
                    continue;
                }
                bool canDrive = RcPhase && pl.IsAliveHider && !pl.Busy.Value;
                if (s.Mode == RcMode.Driving && !canDrive)
                {
                    RcWhere(i, out var pos, out float yaw);
                    SetRc(i, RcMode.Parked, pl.NetworkObjectId, pos, yaw);
                }
                else if (s.Mode == RcMode.Carried && (pl.Life.Value == LifeState.Dead || !pl.IsHider))
                {
                    // Dead hider drops the car next to the body (anybody can take it).
                    Vector3 p = ItemPlacement.PutDown(pl.transform.position, pl.transform.position + pl.transform.right * 0.5f, ItemPlacement.Small, "rc car");
                    SetRc(i, RcMode.Parked, ulong.MaxValue, p, pl.transform.eulerAngles.y + 90f);
                }
                else if (s.Mode == RcMode.Parked && (pl.Life.Value == LifeState.Dead || !pl.IsHider))
                    ServerSpillRcRemote(pl, i); // the transmitter falls next to the body (MatchManager.RcRemote)
            }
        }

        public void ServerRcPickup(NetPlayer pl, int i)
        {
            if (pl == null || i < 0 || i >= RcCar.Count || !RcPhase || !pl.IsAliveHider || pl.Busy.Value) return;
            var s = RcSlot(i);
            if (RcSlotOf(pl) >= 0 && !(s.Holder == pl.NetworkObjectId && s.Mode == RcMode.Parked))
            {
                pl.ToastRpc("Masz już samochodzik.", 2f);
                return;
            }
            if (RcSlotOf(pl) < 0 && RcRemoteSlotOf(pl) >= 0) return; // still holds the transmitter of a destroyed car (thrown in a moment)
            if (s.Mode == RcMode.Parked && s.Holder != pl.NetworkObjectId && s.Holder != ulong.MaxValue)
            {
                // The car goes with its transmitter: somebody has it / it lies somewhere on the floor.
                pl.ToastRpc(s.Holder == RcDropHolder ? "Pilot do tego samochodzika leży gdzieś na podłodze - bez niego go nie weźmiesz."
                    : "Ktoś ma pilota do tego samochodzika.", 3f);
                return;
            }
            Vector3 at = s.Mode == RcMode.Shelf ? RcCarShelf.CarPos(i) : s.Pos;
            if (s.Mode != RcMode.Shelf && s.Mode != RcMode.Parked) return;
            if (RcSlotOf(pl) < 0 && !Inventory.HasRoomFor(pl, HotbarItem.RcCar)) { pl.ToastRpc(Inventory.FullText, 2.5f); return; }
            if (Vector3.Distance(pl.transform.position + Vector3.up * 0.9f, at) > Tuning.RcPickupRange + 1.2f) return;
            SetRc(i, RcMode.Carried, pl.NetworkObjectId, at, 0f);
            pl.SfxRpc(GameSfx.RcPlace, 0.7f);
            pl.ToastRpc("Masz zdalnie sterowany samochodzik. Naciśnij 6, żeby go postawić i nim sterować.", 5f);
        }

        public void ServerRcDeploy(NetPlayer pl, int i, Vector3 pos, float yaw)
        {
            if (pl == null || i < 0 || i >= RcCar.Count || !RcPhase || !pl.IsAliveHider || pl.Busy.Value || pl.Seat.Value >= 0) return;
            var s = RcSlot(i);
            if (s.Holder != pl.NetworkObjectId || s.Mode != RcMode.Carried) return;
            if (Vector3.Distance(pl.transform.position, pos) > 1.6f) return;
            SetRc(i, RcMode.Driving, pl.NetworkObjectId, pos, yaw);
        }

        public void ServerRcResume(NetPlayer pl, int i)
        {
            if (pl == null || i < 0 || i >= RcCar.Count || !RcPhase || !pl.IsAliveHider || pl.Busy.Value || pl.Seat.Value >= 0) return;
            var s = RcSlot(i);
            if (s.Holder != pl.NetworkObjectId || s.Mode != RcMode.Parked) return;
            if (Vector3.Distance(pl.transform.position, s.Pos) > RcRangeOf(i)) return;
            SetRc(i, RcMode.Driving, pl.NetworkObjectId, s.Pos, s.Yaw);
        }

        public void ServerRcPark(NetPlayer pl, int i, Vector3 pos, float yaw)
        {
            if (pl == null || i < 0 || i >= RcCar.Count) return;
            var s = RcSlot(i);
            if (s.Holder != pl.NetworkObjectId || s.Mode != RcMode.Driving) return;
            if (Vector3.Distance(pl.transform.position, pos) > RcRangeOf(i) + 5f) RcWhere(i, out pos, out yaw);
            SetRc(i, RcMode.Parked, pl.NetworkObjectId, pos, yaw);
            pl.ToastRpc("Pilot został w ekwipunku. LPM z pilotem albo 6 - wróć do sterowania.", 3.5f);
        }

        /// <summary>
        /// Glock hit on a car on the floor (driven or parked): one bullet is enough, the car bursts into a smoking
        /// wreck for the rest of the round and its driver drops back into the body.
        /// </summary>
        private bool ServerShootRc(NetPlayer shooter, Vector3 origin, int i, Vector3 hitPoint)
        {
            if (i < 0 || i >= RcCar.Count) return false;
            var s = RcSlot(i);
            if (s.Mode != RcMode.Driving && s.Mode != RcMode.Parked) return false;
            RcWhere(i, out var pos, out float yaw);
            if (Vector3.Distance(hitPoint, pos) > 1.2f || Vector3.Distance(origin, pos) > Tuning.GlockRange + 3f) return false;
            SetRc(i, RcMode.Shot, s.Holder, pos, yaw);
            ServerRcDestroyed(i);
            Cinematics.RoundStats.ToyBroken(shooter);
            var h = NetPlayer.Find(s.Holder);
            if (h != null) h.ToastRpc("Szukający zestrzelił twój samochodzik!", 4f);
            shooter.ToastRpc("Zestrzeliłeś samochodzik. Kierowca jest gdzieś niedaleko...", 3f);
            return true;
        }

        public void ServerRcStomp(NetPlayer seeker, int i)
        {
            if (!Seeking || seeker == null || !seeker.IsSeeker || seeker.Busy.Value || seeker.Life.Value != LifeState.Alive) return;
            if (i < 0 || i >= RcCar.Count) return;
            var s = RcSlot(i);
            if (s.Mode != RcMode.Driving && s.Mode != RcMode.Parked) return;
            RcWhere(i, out var pos, out float yaw);
            Vector3 d = pos - seeker.transform.position;
            if (Mathf.Abs(d.y) > 1.2f) return;
            d.y = 0f;
            if (d.magnitude > Tuning.RcStompServerRange) return;
            StartCoroutine(StompRoutine(seeker, i, pos, yaw));
        }

        private IEnumerator StompRoutine(NetPlayer seeker, int i, Vector3 pos, float yaw)
        {
            var s = RcSlot(i);
            ulong holder = s.Holder;
            seeker.Busy.Value = true;
            SetRc(i, RcMode.Doomed, holder, pos, yaw);

            // Step up to the car, facing it.
            Vector3 dir = pos - seeker.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = seeker.transform.forward;
            dir.Normalize();
            Vector3 stand = pos - dir * Tuning.RcStompStandOff;
            stand.y = Mathf.Max(pos.y, seeker.transform.position.y - 0.3f);
            seeker.TeleportRpc(stand, Quaternion.LookRotation(dir).eulerAngles.y);
            yield return new WaitForSeconds(0.08f);
            if (seeker == null || !seeker.IsSpawned) { SetRc(i, RcMode.Parked, holder, pos, yaw); yield break; }
            seeker.StompRpc((byte)i, pos);
            yield return new WaitForSeconds(RcTimeline.StompHit);
            SetRc(i, RcMode.Wreck, holder, pos, yaw);
            ServerRcDestroyed(i);
            Cinematics.RoundStats.ToyBroken(seeker);
            var h = NetPlayer.Find(holder);
            if (h != null) h.ToastRpc("Szukający rozdeptał twój samochodzik!", 4f);
            seeker.ToastRpc("Rozdeptałeś samochodzik. Ktoś się chowa niedaleko...", 3f);
            yield return new WaitForSeconds(RcTimeline.Stomp - RcTimeline.StompHit);
            if (seeker != null && seeker.IsSpawned) seeker.Busy.Value = false;
        }
    }
}
