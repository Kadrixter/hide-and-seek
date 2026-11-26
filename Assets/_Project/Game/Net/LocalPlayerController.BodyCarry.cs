using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using HideAndSeek.Player;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Body carry keys of the local player.
    /// <list type="bullet">
    /// <item>B held <see cref="CarryHoldTime"/> s while looking at an injured hider (downed, a leg cut off, wounded):
    /// asks him (B because H is rescue / healing and E picks things up next to bodies; B is free and next to the
    /// movement keys).</item>
    /// <item>Carrying: B puts down; walking (slowly, no run / jump / crouch), looking round, T (flashlight) and E on
    /// elevator buttons work; no items, no other interactions.</item>
    /// <item>Being carried: look round, talk (V as always), Space asks to be put down; nothing else.</item>
    /// </list>
    /// </summary>
    public partial class LocalPlayerController
    {
        public const float CarryHoldTime = 0.45f;

        /// <summary>Test hook: one B press / B "held long enough" on the current candidate.</summary>
        public static bool TestCarryB, TestCarrySpace;

        private float _carryHold;
        private bool _carrySent;
        private NetPlayer _carryCandidate;

        /// <summary>Hider that B would ask right now (null = none).</summary>
        public NetPlayer CarryCandidate => _carryCandidate;

        /// <summary>Returns true while the carry owns the input (carrying / being carried).</summary>
        private bool CarryTick(Keyboard kb, MatchPhase phase)
        {
            CarryConsentPrompt.Ensure();
            CarryConsentPrompt.Hint = "";
            CarryConsentPrompt.HintProgress = 0f;
            bool b = KeyOrPad.Down(Key.B) || TestCarryB;
            bool space = KeyOrPad.Down(Key.Space) || TestCarrySpace;
            TestCarryB = TestCarrySpace = false;

            if ((_p.IsCarryingBody || _p.BodyCarriedNow) && _p.HealingCarried) StopCarryHeal();
            if (_p.IsCarryingBody)
            {
                _carryCandidate = null;
                var v = _p.CarryVar.Value;
                bool holding = v.PhaseValue == HideAndSeek.Characters.BodyCarryPhase.Hold;
                _p.Motor.LookEnabled = _p.Motor.LookEnabled && holding; // lift / put down: the body does the turning
                _p.InputReader.ResetStance();
                if (b && holding) _p.RequestCarryPutDownRpc();
                FlashlightKey(kb);
                // Elevators keep working; every other prompt is off while the arms are full.
                FindPrompt(phase);
                if (Prompt != PromptKind.ElevatorCall && Prompt != PromptKind.ElevatorPanel) Prompt = PromptKind.None;
                if (Prompt != PromptKind.None) HandlePromptInput(kb);
                return true;
            }
            if (_p.BodyCarriedNow)
            {
                _carryCandidate = null;
                if (space) _p.RequestCarryPutDownRpc();
                if (_p.Life.Value == LifeState.Alive) FlashlightKey(kb);
                return true;
            }

            // H held on a carried player (medkit): treat him in the carrier's arms.
            if (CarryHealTick(kb)) { _carryCandidate = null; return true; }
            bool healHint = !string.IsNullOrEmpty(CarryConsentPrompt.Hint);

            // B held on an injured hider: ask him.
            _carryCandidate = FindCarryCandidate(phase);
            if (_carryCandidate == null) { _carryHold = 0f; _carrySent = false; return false; }
            bool held = KeyOrPad.Held(Key.B);
            if (b && !held) { _carryHold = CarryHoldTime; } // test press
            else if (held) _carryHold += Time.deltaTime;
            else { _carryHold = 0f; _carrySent = false; }
            if (!healHint || _carryHold > 0f)
            {
                CarryConsentPrompt.Hint = _carryCandidate.Life.Value == LifeState.Dead
                    ? $"<color=#ffd27a>B</color>  przytrzymaj - podnieś ciało ({_carryCandidate.Nick})"
                    : $"<color=#ffd27a>B</color>  przytrzymaj - podnieś na ręce ({_carryCandidate.Nick})";
                CarryConsentPrompt.HintProgress = _carryHold / CarryHoldTime;
            }
            if (_carryHold >= CarryHoldTime && !_carrySent)
            {
                _carrySent = true;
                _p.RequestCarryRpc(_carryCandidate.NetworkObjectId);
                if (!held) _carryHold = 0f;
            }
            return false;
        }

        private void FlashlightKey(Keyboard kb)
        {
            if (KeyOrPad.Down(Key.T) && _p.Flashlight != null && _p.HasFlashlight) _p.Flashlight.Toggle();
        }

        /// <summary>Injured hider in front of the crosshair, close enough to be picked up.</summary>
        private NetPlayer FindCarryCandidate(MatchPhase phase)
        {
            if (phase != MatchPhase.Seeking && phase != MatchPhase.Hiding) return null;
            if (_p.Life.Value != LifeState.Alive || _p.Busy.Value || _p.Seat.Value >= 0 || _p.Motor.IsProne) return null;
            bool hider = _p.IsHider && !_p.Limping.Value && _p.Severed.Value == 0;
            if (!hider && !_p.IsSeeker) return null; // the seeker picks up dead bodies only
            Vector3 eye = _p.EyePosition;
            Vector3 fwd = _p.Camera != null ? _p.Camera.transform.forward : transform.forward;
            NetPlayer best = null;
            float bestScore = float.MaxValue;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == _p || !NetPlayer.NeedsCarry(o) || o.Busy.Value || o.BodyCarryBusy) continue;
                if (!hider && o.Life.Value != LifeState.Dead) continue;
                Vector3 chest = o.ChestPosition;
                float d = Vector3.Distance(eye, chest);
                if (d > Tuning.RescueRange + 0.6f || !InView(eye, fwd, chest, 60f, o.transform)) continue;
                float score = d + Vector3.Angle(fwd, chest - eye) * 0.02f;
                if (score < bestScore) { bestScore = score; best = o; }
            }
            return best;
        }
    }
}
