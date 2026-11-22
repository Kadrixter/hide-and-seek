using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Shoulder flashlight in the lobby: nobody has it at first, the lamp is taken from <see cref="FlashlightBox"/>
    /// (= ready, <see cref="Ready"/>) and can be put back (= not ready). Outside the lobby everybody has it.
    /// Drives the lamp model (hidden / in the left hand / on the shoulder), the take / put-back animation on every
    /// client (third person, first person + camera), the sounds and keeps the light off while the lamp is not carried.
    /// </summary>
    public partial class NetPlayer
    {
        private float _flashT = -1f;
        private bool _flashTake;
        private HeadBob _flashBob;

        private static bool LobbyPhase => MatchManager.Instance != null && MatchManager.Instance.Phase.Value == MatchPhase.Lobby;

        /// <summary>The flashlight can be used (T): always in a round, in the lobby only once taken from the box.</summary>
        public bool HasFlashlight => !LobbyPhase || (Ready.Value && _flashT < 0f);

        /// <summary>The lamp is out of the box (on the shoulder or in the hand) - for the lamps shown in the box.</summary>
        public bool LampWithPlayer
        {
            get
            {
                if (!LobbyPhase) return true;
                if (_flashT >= 0f) return FlashTakeTimeline.LampState(_flashTake, _flashT, out _) != 0;
                return Ready.Value;
            }
        }

        /// <summary>Seconds into the box animation (-1 = none) and whether it is a take (tests / box view).</summary>
        public float FlashAnimTime => _flashT;
        public bool FlashAnimTake => _flashTake;

        [Rpc(SendTo.Server)]
        public void RequestFlashBoxRpc(bool take) => MatchManager.Instance?.ServerFlashBox(this, take);

        /// <summary>Everyone: this player takes a lamp out of the box (take) or puts it back.</summary>
        [Rpc(SendTo.Everyone)]
        public void FlashBoxRpc(bool take)
        {
            _flashTake = take;
            _flashT = 0f;
            (Actions as BlockyCharacterActions)?.PlayFlashTake(take);
            if (IsOwner && Flashlight != null && Flashlight.IsOn) Flashlight.SetOn(false, true);
        }

        /// <summary>Called from Update on every client.</summary>
        private void UpdateFlashCarry()
        {
            if (_shoulderLamp == null) return;
            float prev = _flashT;
            if (_flashT >= 0f)
            {
                _flashT += Time.deltaTime;
                bool lobbyOk = LobbyPhase && Life.Value == LifeState.Alive;
                if (_flashT >= FlashTakeTimeline.Duration(_flashTake) || !lobbyOk) EndFlashAnim();
            }

            int state;
            float toShoulder = 1f;
            if (_flashT >= 0f) state = FlashTakeTimeline.LampState(_flashTake, _flashT, out toShoulder);
            else state = !LobbyPhase || Ready.Value ? 2 : 0;
            _shoulderLamp.SetVisible(state != 0);
            _shoulderLamp.SetCarry(state == 1 ? Character.ElbowLeft : null, toShoulder);

            if (_flashT >= 0f) FlashSounds(prev < 0f ? 0f : prev, _flashT);
            if (IsOwner)
            {
                if (!HasFlashlight && Flashlight != null && Flashlight.IsOn) Flashlight.SetOn(false, false);
                if (_flashT >= 0f) OwnerFlashView();
            }
        }

        private void EndFlashAnim()
        {
            _flashT = -1f;
            if (!IsOwner) return;
            (ViewModel as FirstPersonViewModel)?.SetFlashTake(_flashTake, -1f, Vector3.zero);
            if (_flashBob != null) _flashBob.ExtraEuler = Vector3.zero;
        }

        private void FlashSounds(float a, float b)
        {
            bool Hit(float m) => a < m && b >= m;
            Vector3 shoulder = transform.position + Vector3.up * 1.45f;
            if (_flashTake)
            {
                if (Hit(FlashTakeTimeline.TakeGrab)) GameAudio.PlayAt(GameSfx.MedkitPickup, FlashlightBox.AimPoint, 0.6f);
                if (Hit(FlashTakeTimeline.TakeClip - 0.02f)) GameAudio.PlayAt(GameSfx.FlashlightClick, shoulder, 1f);
                if (Hit(1.42f)) GameAudio.PlayAt(GameSfx.FlashlightAdjust, shoulder, 0.6f);
            }
            else
            {
                if (Hit(FlashTakeTimeline.PutUnclip)) GameAudio.PlayAt(GameSfx.FlashlightClick, shoulder, 0.9f);
                if (Hit(FlashTakeTimeline.PutRelease)) GameAudio.PlayAt(GameSfx.MedkitPickup, FlashlightBox.AimPoint, 0.5f);
            }
        }

        /// <summary>Owner: first-person hands and the camera looking into the box, then at the right shoulder.</summary>
        private void OwnerFlashView()
        {
            (ViewModel as FirstPersonViewModel)?.SetFlashTake(_flashTake, _flashT, FlashlightBox.GrabWorld);
            if (_flashBob == null) _flashBob = GetComponentInChildren<HeadBob>();
            float lean = FlashTakeTimeline.Lean(_flashTake, _flashT), look = FlashTakeTimeline.LookAtShoulder(_flashTake, _flashT);
            // The motor pitch is brought level first, the rest is a camera offset (positive x = down).
            Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, 0f, Time.deltaTime * 90f));
            if (_flashBob != null) _flashBob.ExtraEuler = new Vector3(34f * lean + 22f * look, 38f * look, 0f);
        }
    }
}
