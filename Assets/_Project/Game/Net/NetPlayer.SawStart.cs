using HideAndSeek.Audio;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Chainsaw pull-start on every peer (2026-11-25): the server rolls how many tries the start needs
    /// (<see cref="SawAttempts"/>: 1, 2 or 4) and when it began (<see cref="SawStartAt"/>); here the third- and
    /// first-person animations follow that clock (one yank per try), the failed tries sputter (the start sound cut
    /// short) and the last one plays the whole start sound with the engine catching. Stalled = engine dead, no pull.
    /// 2026-11-26: the engine dying (empty tank / stall) plays <see cref="GameSfx.ChainsawStall"/> and F on an empty
    /// tank a dry pull (<see cref="GameSfx.ChainsawDryPull"/>: one yank, the engine does not catch); both fall back to
    /// the existing clips while the new ones are not in the library.
    /// </summary>
    public partial class NetPlayer
    {
        private double _sawStartSeen = -1d;
        private int _sawYanksPlayed;
        private bool _sawFullPlayed;
        private AudioSource _sawSputter;
        private float _sawSputterStop;
        private float _sawDryPullAt = -99f;
        // Stall fallback (no ChainsawStall clip): the idle loop fading out while its pitch drops.
        private AudioSource _sawStallFake;
        private float _sawStallFakeAt;
        private const float StallFakeTime = 1.5f, StallFakePitch = 0.45f;
        /// <summary>Dry pull fallback (no ChainsawDryPull clip): the start clip cut after this long (cord only, no catch).</summary>
        private const float DryPullFakeCut = 0.6f;

        /// <summary>Called from UpdateChainsaw: drives the animations and the start sounds.</summary>
        private void UpdateSawStartView(bool sawOut, bool stalled, bool killing)
        {
            int tries = Mathf.Max(1, SawAttempts.Value);
            float dryT = Time.time - _sawDryPullAt;
            bool dryPull = stalled && !killing && dryT >= 0f && dryT < SawTuning.DryPullAnim;
            float t = !sawOut || !IsSeeker ? float.NaN : killing ? 99f : dryPull ? dryT : stalled ? -1f : SawStartElapsed;
            // A dry pull animates the priming reach and one yank of a 4-try start (the engine never fires in that window).
            int animTries = dryPull ? 4 : tries;
            (Actions as BlockyCharacterActions)?.SetSawStart(t, animTries);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetSawStart(t, animTries);

            if (_sawSputter != null && Time.time >= _sawSputterStop)
            {
                GameAudio.StopLoop(_sawSputter, 0.06f);
                _sawSputter = null;
            }
            UpdateSawStallFake();
            if (float.IsNaN(t) || t < 0f || t > 30f || stalled) return;
            if (SawStartAt.Value != _sawStartSeen)
            {
                _sawStartSeen = SawStartAt.Value;
                _sawYanksPlayed = 0;
                _sawFullPlayed = false;
            }
            // The start sound (cord + engine catching) is ~0.95 s from its first yank to the engine: it starts one yank
            // before the last; every yank before it is a failed try that only sputters.
            float fullAt = BlockyProps.ChainsawFireAt(tries) - BlockyProps.ChainsawFireTime;
            float vol = IsOwner ? 0.6f : 0.85f;
            while (_sawYanksPlayed <= tries && BlockyProps.ChainsawYankAt(_sawYanksPlayed) < fullAt - 0.05f && t >= BlockyProps.ChainsawYankAt(_sawYanksPlayed))
            {
                bool fresh = t - BlockyProps.ChainsawYankAt(_sawYanksPlayed) < 0.3f; // a late joiner does not get the old ones
                _sawYanksPlayed++;
                if (!fresh) continue;
                if (_sawSputter != null) GameAudio.StopLoop(_sawSputter, 0.03f);
                _sawSputter = GameAudio.PlayAttached(GameSfx.ChainsawStart, transform, vol * 0.8f);
                if (_sawSputter != null) _sawSputter.pitch = Random.Range(0.92f, 1.06f);
                _sawSputterStop = Time.time + 0.34f;
            }
            if (!_sawFullPlayed && t >= fullAt)
            {
                _sawFullPlayed = true;
                if (t - fullAt < 0.4f) GameAudio.PlayAttached(GameSfx.ChainsawStart, transform, vol);
            }
        }

        /// <summary>Every peer (<see cref="SawDryPullRpc"/>): the cord is pulled on an empty tank, the engine does not catch.</summary>
        private void PlaySawDryPull()
        {
            _sawDryPullAt = Time.time;
            float vol = IsOwner ? 0.6f : 0.85f;
            if (GameAudio.HasClip(GameSfx.ChainsawDryPull))
            {
                GameAudio.PlayAttached(GameSfx.ChainsawDryPull, transform, vol);
                return;
            }
            // Fallback: the cord part of the start clip only (cut before the engine catches).
            if (_sawSputter != null) GameAudio.StopLoop(_sawSputter, 0.03f);
            _sawSputter = GameAudio.PlayAttached(GameSfx.ChainsawStart, transform, vol * 0.8f);
            _sawSputterStop = Time.time + DryPullFakeCut;
        }

        /// <summary>The running engine just died (empty tank or a stall): it sputters out where the player is.</summary>
        private void PlaySawStall()
        {
            float vol = IsOwner ? 0.6f : 0.85f;
            if (GameAudio.HasClip(GameSfx.ChainsawStall))
            {
                GameAudio.PlayAttached(GameSfx.ChainsawStall, transform, vol);
                return;
            }
            // Fallback: the idle loop winding down (volume fades out, pitch drops); the runner stops it at the end.
            if (_sawStallFake != null) GameAudio.StopLoop(_sawStallFake, 0.05f);
            _sawStallFake = GameAudio.StartLoop(GameSfx.ChainsawIdle, transform, IsOwner ? SawIdleOwnerVol : SawIdleOtherVol);
            if (_sawStallFake == null) return;
            _sawStallFake.pitch = SawIdlePitch;
            _sawStallFakeAt = Time.unscaledTime;
            GameAudio.StopLoop(_sawStallFake, StallFakeTime);
        }

        /// <summary>Drops the pitch of the fake stall; lets go of the pooled source just before the runner stops it.</summary>
        private void UpdateSawStallFake()
        {
            if (_sawStallFake == null) return;
            float k = (Time.unscaledTime - _sawStallFakeAt) / StallFakeTime;
            if (k >= 0.95f || !_sawStallFake.isPlaying) { _sawStallFake = null; return; }
            _sawStallFake.pitch = Mathf.Lerp(SawIdlePitch, StallFakePitch, k * k * (3f - 2f * k));
        }
    }
}
