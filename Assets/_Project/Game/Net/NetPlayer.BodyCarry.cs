using System;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Server-written carry state of a carrier (who, which stage since when, where the body ends up).</summary>
    public struct BodyCarryPack : INetworkSerializable, IEquatable<BodyCarryPack>
    {
        public ulong Target;    // NetworkObjectId of the carried player
        public byte Phase;      // BodyCarryPhase
        public byte Flags;      // 1 mirror, 2 lift from the floor, 4 put down onto the feet, 8 a dead player's body
        public double Start;    // server time the phase began
        public Vector3 EndPos;  // root frame of the carried after a put down / drop
        public float EndYaw;

        public BodyCarryPhase PhaseValue => (BodyCarryPhase)Phase;
        public bool Active => Phase != 0;
        public bool Mirror => (Flags & 1) != 0;
        public bool FromFloor => (Flags & 2) != 0;
        public bool EndStanding => (Flags & 4) != 0;
        public bool DeadBody => (Flags & 8) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Target);
            s.SerializeValue(ref Phase);
            s.SerializeValue(ref Flags);
            s.SerializeValue(ref Start);
            s.SerializeValue(ref EndPos);
            s.SerializeValue(ref EndYaw);
        }

        public bool Equals(BodyCarryPack o) => Target == o.Target && Phase == o.Phase && Flags == o.Flags && Start.Equals(o.Start) && EndPos == o.EndPos && EndYaw.Equals(o.EndYaw);
    }

    /// <summary>
    /// Carrying an injured hider in the arms (B, after the other player agreed - NetPlayer.CarryConsent.cs). The server
    /// owns the state (<see cref="CarryVar"/> on the carrier); every peer links the two action layers
    /// (<see cref="BodyCarryState"/>), so the carried body is posed in the carrier's root space and rides on him without
    /// jitter. The carried player's owner keeps his root on the carrier (controller off, look relative to the carrier)
    /// and moves it to the end spot when he is put down. Server rules: MatchManager-independent polling in
    /// NetPlayer.BodyCarryServer.cs.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Who this player carries and how far the carry is (server writes).</summary>
        public readonly NetworkVariable<BodyCarryPack> CarryVar = new();

        private BodyCarryState _carryLink;
        private NetPlayer _carryLinkTarget;
        private BodyCarryPhase _carryPhaseSeen;
        private bool _carryEffortHooked;
        private bool _carriedFollow, _carriedPlaced, _stepsMuted;
        private float _carrierYawPrev;
        private bool _carrierYawValid;
        private Vector3 _approachFrom, _approachTo;
        private Quaternion _approachFromRot, _approachToRot;
        private bool _approach;
        private CarryView _carryView;
        private CarryTurn _carryTurn;

        /// <summary>The player this one carries (null = none).</summary>
        public NetPlayer CarriedPlayer => CarryVar.Value.Active ? Find(CarryVar.Value.Target) : null;

        /// <summary>The player carrying this one (null = none).</summary>
        public NetPlayer CarriedBy
        {
            get
            {
                // No per-frame cache: the server may start a carry in the middle of a frame (host).
                foreach (var o in All)
                    if (o != null && o != this && o.CarryVar.Value.Active && o.CarryVar.Value.Target == NetworkObjectId) return o;
                return null;
            }
        }

        public bool IsCarryingBody => CarryVar.Value.Active;
        public bool BodyCarriedNow => CarriedBy != null;
        /// <summary>Hands / body busy with a carry (no items, no attacks, no other interactions).</summary>
        public bool BodyCarryBusy => IsCarryingBody || BodyCarriedNow;

        /// <summary>Motor speed factor for the carrier: slow while holding, standing still while lifting / putting down.</summary>
        public float CarrySpeedScale
        {
            get
            {
                var v = CarryVar.Value;
                if (!v.Active) return 1f;
                return v.PhaseValue == BodyCarryPhase.Hold && !CarryTurnLocked ? BodyCarryTimeline.SpeedScale : 0f;
            }
        }

        /// <summary>Seconds into the carry phase of <paramref name="pack"/> (server clock).</summary>
        public static float CarryPhaseTime(in BodyCarryPack pack)
        {
            var mm = MatchManager.Instance;
            double now = mm != null ? mm.Now : 0d;
            return Mathf.Max(0f, (float)(now - pack.Start));
        }

        /// <summary>Where this player's body lies (pelvis, direction to the head), from the posed / ragdolled bones.</summary>
        public void BodyLieFrame(out Vector3 pelvis, out Vector3 headDir)
        {
            if (Character != null && Character.Pelvis != null && Character.Torso != null)
            {
                pelvis = Character.Pelvis.position;
                headDir = Character.Torso.up;
                pelvis.y = transform.position.y;
                return;
            }
            pelvis = transform.position - transform.forward * BodyCarryTimeline.LyingPelvisBack;
            headDir = transform.forward;
        }

        /// <summary>Every peer, every frame (NetPlayer.Update).</summary>
        private void UpdateBodyCarry()
        {
            if (IsServer)
            {
                ServerCarryTick();
                ServerBleedPause();
            }
            UpdateCarryHeal();
            UpdateCarryHealPatient();
            var bca = Actions as BlockyCharacterActions;
            var pack = CarryVar.Value;
            var target = pack.Active ? Find(pack.Target) : null;
            var tb = target != null ? target.Actions as BlockyCharacterActions : null;

            // ---- carrier side: keep the two layers linked
            if (bca != null && tb != null)
            {
                if (_carryLink == null || _carryLinkTarget != target)
                {
                    EndCarryLink();
                    _carryLink = new BodyCarryState();
                    _carryLinkTarget = target;
                    FillCarryState(pack, target);
                    bca.BeginBodyCarry(tb, _carryLink);
                    if (!_carryEffortHooked)
                    {
                        _carryEffortHooked = true;
                        bca.BodyLiftEffort += () => GameAudio.PlayAttached(GameSfx.HiderPain, transform, 0.32f);
                    }
                    if (IsOwner && pack.PhaseValue == BodyCarryPhase.Lift) BeginApproach(target, pack);
                }
                FillCarryState(pack, target);
                if (!tb.IsCarriedBody) bca.BeginBodyCarry(tb, _carryLink); // the carried let go meanwhile (e.g. its own update ran first)
                bca.SetCarried(0, null); // the inventory item goes into the pocket
                if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetCarried(0, null);
            }
            else EndCarryLink();
            if (pack.PhaseValue != _carryPhaseSeen)
            {
                OnCarryPhaseChanged(_carryPhaseSeen, pack.PhaseValue, target);
                _carryPhaseSeen = pack.PhaseValue;
            }

            if (IsOwner)
            {
                if (_carryView == null) _carryView = gameObject.AddComponent<CarryView>();
                if (_carryTurn == null) _carryTurn = gameObject.AddComponent<CarryTurn>();
                if (Motor != null) Motor.Burdened = pack.Active;
                UpdateApproach(pack);
            }

            // ---- carried side
            var carrier = CarriedBy;
            bool carried = carrier != null;
            if (!carried && bca != null && bca.IsCarriedBody) bca.ForceEndCarried();
            bool settling = (Actions as BlockyCharacterActions)?.IsSettlingAfterCarry ?? false;
            bool mute = carried || settling;
            if (_steps != null && mute != _stepsMuted)
            {
                _stepsMuted = mute;
                _steps.enabled = !mute; // the remote copy "moves" with the carrier: no footsteps of its own
            }
            if (IsOwner) UpdateCarriedOwner(carrier);
        }

        private void FillCarryState(in BodyCarryPack pack, NetPlayer target)
        {
            var st = _carryLink;
            st.Phase = pack.PhaseValue;
            st.Time = CarryPhaseTime(pack);
            st.Mirror = pack.Mirror;
            st.FromFloor = pack.FromFloor;
            st.EndStanding = pack.EndStanding;
            st.Conscious = target.Life.Value == LifeState.Alive;
            st.Dead = pack.DeadBody;
            bool end = st.Phase == BodyCarryPhase.PutDown || st.Phase == BodyCarryPhase.Drop;
            st.HasEnd = end;
            if (end)
            {
                st.EndPos = pack.EndPos;
                st.EndRot = Quaternion.Euler(0f, pack.EndYaw, 0f);
            }
            float pitch = target.Motor != null ? target.Motor.LookPitch : 0f;
            var healer = CarryHealerOf(target);
            st.HealT = healer != null ? healer.CarryHealShownT : -1f;
            st.CarriedLook = Quaternion.Euler(-pitch, target.transform.eulerAngles.y, 0f) * Vector3.forward;
        }

        private void EndCarryLink()
        {
            if (_carryLink == null) return;
            (Actions as BlockyCharacterActions)?.EndBodyCarry();
            _carryLink = null;
            _carryLinkTarget = null;
            _approach = false;
        }

        private void OnCarryPhaseChanged(BodyCarryPhase from, BodyCarryPhase to, NetPlayer target)
        {
            if (to == BodyCarryPhase.Drop && target != null)
            {
                GameAudio.PlayAttached(GameSfx.BodyFall, target.transform, 0.9f);
                if (target.Life.Value != LifeState.Dead) GameAudio.PlayAttached(GameSfx.HiderPain, target.transform, 0.6f);
            }
            else if (to == BodyCarryPhase.PutDown && target != null) GameAudio.PlayAttached(GameSfx.BodyFall, target.transform, 0.22f);
        }

        // ================================================================ carrier owner: step next to the body

        private void BeginApproach(NetPlayer target, in BodyCarryPack pack)
        {
            Vector3 pos;
            Quaternion rot;
            if (pack.FromFloor)
            {
                target.BodyLieFrame(out var pelvis, out var head);
                BodyCarryTimeline.CarrierSpotLying(pelvis, head, pack.Mirror, out pos, out rot);
            }
            else BodyCarryTimeline.CarrierSpot(target.transform.position, target.transform.rotation, false, pack.Mirror, out pos, out rot);
            Vector3 d = pos - transform.position;
            d.y = 0f;
            if (d.magnitude > 2.2f) return; // too far for a step: lift from where we are
            _approachFrom = transform.position;
            _approachFromRot = transform.rotation;
            _approachTo = new Vector3(pos.x, transform.position.y, pos.z);
            _approachToRot = rot;
            _approach = true;
        }

        private void UpdateApproach(in BodyCarryPack pack)
        {
            if (!_approach) return;
            float t = pack.PhaseValue == BodyCarryPhase.Lift ? CarryPhaseTime(pack) / BodyCarryTimeline.ApproachTime : 1f;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            var rot = Quaternion.Slerp(_approachFromRot, _approachToRot, s);
            Motor.Teleport(Vector3.Lerp(_approachFrom, _approachTo, s), rot.eulerAngles.y, Motor.LookPitch);
            if (t >= 1f) _approach = false;
        }

        // ================================================================ carried owner: ride along, then the end spot

        private void UpdateCarriedOwner(NetPlayer carrier)
        {
            var bca = Actions as BlockyCharacterActions;
            if (carrier != null)
            {
                var pack = carrier.CarryVar.Value;
                float t = CarryPhaseTime(pack);
                bool ending = pack.PhaseValue == BodyCarryPhase.PutDown || pack.PhaseValue == BodyCarryPhase.Drop;
                bool done = ending && t >= BodyCarryTimeline.Duration(pack.PhaseValue, pack.FromFloor, pack.EndStanding);
                if (!done)
                {
                    _carriedFollow = true;
                    _carriedPlaced = false;
                    // Look turns with the carrier (relative look), root rides on the carrier's feet.
                    float cy = carrier.transform.eulerAngles.y;
                    float yaw = transform.eulerAngles.y;
                    if (_carrierYawValid) yaw += Mathf.DeltaAngle(_carrierYawPrev, cy);
                    _carrierYawPrev = cy;
                    _carrierYawValid = true;
                    float pitch = Mathf.Clamp(Motor.LookPitch, -65f, 60f);
                    var cc = GetComponent<CharacterController>();
                    if (cc != null && cc.enabled) cc.enabled = false;
                    transform.SetPositionAndRotation(carrier.transform.position, Quaternion.Euler(0f, yaw, 0f));
                    Motor.SetLook(yaw, pitch);
                    return;
                }
                if (!_carriedPlaced)
                {
                    // A dead body's root goes along too: it is where late joiners see the body lie.
                    _carriedPlaced = true;
                    TeleportLocal(pack.EndPos, pack.EndYaw);
                }
                _carrierYawValid = false;
                return;
            }
            _carrierYawValid = false;
            if (!_carriedFollow) return;
            // The carry just ended without the end spot (carrier gone, round over): onto the floor where the body falls.
            _carriedFollow = false;
            if (!_carriedPlaced && bca != null && bca.TryGetCarryEnd(out var pos, out var rot))
                TeleportLocal(pos, rot.eulerAngles.y);
            _carriedPlaced = false;
        }
    }
}
