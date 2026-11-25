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
    /// </summary>
    public partial class NetPlayer
    {
        private double _sawStartSeen = -1d;
        private int _sawYanksPlayed;
        private bool _sawFullPlayed;
        private AudioSource _sawSputter;
        private float _sawSputterStop;

        /// <summary>Called from UpdateChainsaw: drives the animations and the start sounds.</summary>
        private void UpdateSawStartView(bool sawOut, bool stalled, bool killing)
        {
            int tries = Mathf.Max(1, SawAttempts.Value);
            float t = !sawOut || !IsSeeker ? float.NaN : killing ? 99f : stalled ? -1f : SawStartElapsed;
            (Actions as BlockyCharacterActions)?.SetSawStart(t, tries);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetSawStart(t, tries);

            if (_sawSputter != null && Time.time >= _sawSputterStop)
            {
                GameAudio.StopLoop(_sawSputter, 0.06f);
                _sawSputter = null;
            }
            if (float.IsNaN(t) || t < 0f || t > 30f) return;
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
    }
}
