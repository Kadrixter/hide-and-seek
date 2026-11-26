using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using HideAndSeek.Player;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// H held on a player lying in somebody's arms (with a medkit): treat him there (NetPlayer.CarryHeal.cs). The usual
    /// rescue / heal prompts are switched off for carried players (they would kneel next to an empty floor), the
    /// healer steps to the side of the body during the first moment, cannot walk while treating; letting go of H
    /// cancels. Test hook <see cref="TestCarryHeal"/> = H held.
    /// </summary>
    public partial class LocalPlayerController
    {
        public static bool TestCarryHeal;

        private NetPlayer _carryHealCandidate;
        private Vector3 _chFrom, _chTo;
        private Quaternion _chFromRot, _chToRot;
        private bool _chStep;
        private float _chStepTime = 0.7f;

        /// <summary>Carried player that H would treat now (null = none).</summary>
        public NetPlayer CarryHealCandidate => _carryHealCandidate;

        /// <summary>Called right after FindPrompt: no kneeling rescue / heal on a body that lies in somebody's arms.</summary>
        private void FilterCarryPrompt()
        {
            if (Prompt != PromptKind.Rescue && Prompt != PromptKind.Heal && Prompt != PromptKind.Resurrect) return;
            foreach (var o in NetPlayer.All)
                if (o != null && o.BodyCarriedNow && (o.ChestPosition - PromptWorld).sqrMagnitude < 0.01f) { Prompt = PromptKind.None; return; }
        }

        /// <summary>Returns true while treating (the input belongs to it).</summary>
        private bool CarryHealTick(Keyboard kb)
        {
            bool held = KeyOrPad.Held(Key.H) || TestCarryHeal;
            if (_p.HealingCarried)
            {
                var patient = NetPlayer.Find(_p.CarryHealTarget.Value);
                bool ok = held && patient != null && patient.BodyCarriedNow && NetPlayer.NeedsCarry(patient) && patient.Life.Value != LifeState.Dead && _p.MedkitCount.Value > 0 && _p.IsAliveHider && !_p.Busy.Value;
                if (!ok) { StopCarryHeal(); return false; }
                float t = _p.CarryHealLocalT + Time.deltaTime;
                _p.CarryHealLocalT = t;
                byte prog = (byte)Mathf.Clamp(Mathf.RoundToInt(t / BodyCarryTimeline.HealTime * 255f), 0, 255);
                if (_p.CarryHealProgress.Value != prog) _p.CarryHealProgress.Value = prog;
                _p.Motor.MovementEnabled = false;
                _p.InputReader.ResetStance();
                if (_chStep)
                {
                    float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _chStepTime));
                    _p.Motor.Teleport(Vector3.Lerp(_chFrom, _chTo, s), Quaternion.Slerp(_chFromRot, _chToRot, s).eulerAngles.y, Mathf.Lerp(_p.Motor.LookPitch, -28f, s * 0.5f));
                    if (s >= 1f) _chStep = false;
                }
                CarryConsentPrompt.Hint = $"Opatrujesz: {patient.Nick}";
                CarryConsentPrompt.HintProgress = t / BodyCarryTimeline.HealTime;
                if (t >= BodyCarryTimeline.HealTime)
                {
                    _p.RequestCarriedHealRpc(patient.NetworkObjectId);
                    StopCarryHeal();
                }
                return true;
            }

            _carryHealCandidate = FindCarryHealCandidate();
            if (_carryHealCandidate == null) return false;
            CarryConsentPrompt.Hint = $"<color=#ffd27a>H</color>  przytrzymaj - opatrz niesionego ({_carryHealCandidate.Nick})";
            if (!held) return false;
            StartCarryHeal(_carryHealCandidate);
            return true;
        }

        private void StartCarryHeal(NetPlayer patient)
        {
            _p.CarryHealLocalT = 0f;
            _p.CarryHealProgress.Value = 0;
            _p.CarryHealTarget.Value = patient.NetworkObjectId;
            // Step to the side of the body, facing its belly (in front of the carrier, never between his arms).
            var carrier = patient.CarriedBy;
            Vector3 belly = patient.Character != null ? patient.Character.Torso.TransformPoint(new Vector3(0f, 0.2f, 0f)) : patient.ChestPosition;
            Vector3 cpos = carrier != null ? carrier.transform.position : patient.transform.position;
            Vector3 dir = belly - cpos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = carrier != null ? carrier.transform.forward : transform.forward;
            dir.Normalize();
            Vector3 spot = new Vector3(belly.x, transform.position.y, belly.z) + dir * 0.68f;
            _chFrom = transform.position;
            _chFromRot = transform.rotation;
            _chTo = spot;
            _chToRot = Quaternion.LookRotation(-dir, Vector3.up);
            float stepLen = (spot - transform.position).magnitude;
            _chStep = stepLen < 3.8f;
            _chStepTime = Mathf.Clamp(0.5f + 0.3f * stepLen, 0.6f, 1.4f); // a calm step or two, not a jump
            Debug.Log($"[Carry] treating {patient.Nick} in the arms of {(carrier != null ? carrier.Nick : "?")}");
        }

        private void StopCarryHeal()
        {
            _p.CarryHealTarget.Value = ulong.MaxValue;
            _p.CarryHealProgress.Value = 0;
            _p.CarryHealLocalT = 0f;
            _chStep = false;
        }

        private NetPlayer FindCarryHealCandidate()
        {
            if (!_p.IsAliveHider || _p.Busy.Value || _p.MedkitCount.Value == 0 || _p.Seat.Value >= 0) return null;
            Vector3 eye = _p.EyePosition;
            Vector3 fwd = _p.Camera != null ? _p.Camera.transform.forward : transform.forward;
            NetPlayer best = null;
            float bestD = float.MaxValue;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == _p || !o.BodyCarriedNow || !NetPlayer.NeedsCarry(o) || o.Busy.Value || o.Life.Value == LifeState.Dead) continue;
                if (o.CarriedBy == _p || NetPlayer.CarryHealerOf(o) != null) continue;
                var v = o.CarriedBy.CarryVar.Value;
                if (v.PhaseValue != BodyCarryPhase.Hold) continue;
                Vector3 chest = o.ChestPosition;
                float d = Vector3.Distance(eye, chest);
                if (d > Tuning.RescueRange + 0.6f || !InView(eye, fwd, chest, 60f, o.transform)) continue;
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }
    }
}
