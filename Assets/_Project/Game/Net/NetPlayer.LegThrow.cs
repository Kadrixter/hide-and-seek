using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// A cut-off leg in the hiders' inventory (<see cref="HotbarItem.SeveredLeg"/>): <see cref="LegCode"/> says whose leg
    /// is carried (-1 = none, see <see cref="SeveredLegs"/>). The owner holds the left mouse button to wind up (power
    /// grows over <see cref="LegThrowTimeline.FullCharge"/>; others see the wind-up through <see cref="LegCharge"/>) and
    /// lets go to throw along the view; the server launches the leg (MatchManager.Legs) and checks the hit.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Carried leg (code from SeveredLegs), -1 = none. Server-written.</summary>
        public readonly NetworkVariable<short> LegCode = new(-1);

        /// <summary>Wind-up shown to everybody: 0 = none, 1..11 = power 0..1 (owner-written).</summary>
        public readonly NetworkVariable<byte> LegCharge = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private int _legHoldFrame = -10;
        private float _legHoldStart = -1f, _nextLegThrow;

        /// <summary>Hotbar: the left mouse button is down with the leg selected (this frame).</summary>
        public void LegUseHeld()
        {
            if (IsOwner) _legHoldFrame = Time.frameCount;
        }

        public bool LegCharging => IsOwner ? _legHoldStart >= 0f : LegCharge.Value > 0;

        /// <summary>0..1 power of the wind-up right now.</summary>
        public float LegPowerShown
        {
            get
            {
                if (IsOwner) return _legHoldStart >= 0f ? LegThrowTimeline.Power(Time.time - _legHoldStart) : 0f;
                return LegCharge.Value > 0 ? (LegCharge.Value - 1) / 10f : 0f;
            }
        }

        /// <summary>Test hook: next throw goes along this direction instead of the view.</summary>
        public static Vector3? TestLegDir;

        /// <summary>Called from Update on every peer.</summary>
        private void UpdateLegThrow()
        {
            if (IsServer) ServerTickLeg();
            if (IsOwner)
            {
                bool selected = SelectedItem == HotbarItem.SeveredLeg && LegCode.Value >= 0;
                bool can = selected && Life.Value == LifeState.Alive && !Busy.Value && InputReader != null && InputReader.InputEnabled
                           && !LocalPlayerController.MenuOpen;
                bool held = _legHoldFrame >= Time.frameCount - 1;
                if (_legHoldStart < 0f)
                {
                    if (can && held && Time.time >= _nextLegThrow) _legHoldStart = Time.time;
                }
                else if (!can) _legHoldStart = -1f; // put away / busy / menu: no throw
                else if (!held)
                {
                    float power = LegThrowTimeline.Power(Time.time - _legHoldStart);
                    _legHoldStart = -1f;
                    ThrowLegLocal(power);
                }
                byte want = _legHoldStart >= 0f ? (byte)(1 + Mathf.RoundToInt(LegPowerShown * 10f)) : (byte)0;
                if (LegCharge.Value != want) LegCharge.Value = want;
            }
            bool aiming = LegCharging && Life.Value == LifeState.Alive;
            float pw = LegPowerShown;
            (Actions as BlockyCharacterActions)?.SetLegAim(aiming, pw);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetLegAim(aiming, pw);
        }

        private void ThrowLegLocal(float power)
        {
            _nextLegThrow = Time.time + 0.8f;
            Vector3 dir = TestLegDir ?? (Camera != null ? Camera.transform.forward : transform.forward);
            TestLegDir = null;
            RequestLegThrowRpc(dir, power);
            PlayLegThrowFx(power);
        }

        private void PlayLegThrowFx(float power)
        {
            (Actions as BlockyCharacterActions)?.PlayLegThrow(power);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.PlayLegThrow(power);
            LegSounds.Swish(transform.position + Vector3.up * 1.6f, power);
        }

        [Rpc(SendTo.Server)]
        private void RequestLegThrowRpc(Vector3 dir, float power) => MatchManager.Instance?.ServerThrowLeg(this, dir, power);

        /// <summary>The others see the throw (the owner played it already).</summary>
        [Rpc(SendTo.NotOwner)]
        public void LegThrowRpc(float power) => PlayLegThrowFx(power);

        /// <summary>
        /// A thrown leg hit this seeker (every client): the wet slap, blood off the leg, a grunt; <paramref name="stunned"/>
        /// = he goes down (the stun layer takes over), otherwise only a flinch (glancing hit / still immune).
        /// </summary>
        [Rpc(SendTo.Everyone)]
        public void LegHitRpc(Vector3 at, Vector3 dir, bool stunned)
        {
            LegSounds.Splat(at, stunned ? 1f : 0.75f);
            BloodFx.Spray(at, (-dir + Vector3.up * 0.4f).normalized, stunned ? 1.1f : 0.7f);
            var grunt = GameAudio.PlayAttached(GameSfx.HiderPain, transform, stunned ? 0.95f : 0.7f);
            if (grunt != null) grunt.pitch = stunned ? 0.82f : 0.9f;
            if (!stunned) Actions?.PlayHitReaction();
            if (IsOwner) GameHud.Kick();
        }

        /// <summary>Server: nothing carried outside a round.</summary>
        private void ServerTickLeg()
        {
            var mm = MatchManager.Instance;
            var ph = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            bool round = ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended;
            if (!round && LegCode.Value != -1) LegCode.Value = -1;
            ServerTickStun(round);
        }
    }
}
