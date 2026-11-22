using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// What the boombox shows: the woofers pump with the bass of the music actually playing on this machine (bass
    /// envelope from <see cref="BoomboxAudio.Kick"/>: fast push out, slower return with a little rebound), the whole box
    /// thumps on strong kicks, VU LEDs, turning cassette reels, the PLAY key pressed, knobs at the volume / bass setting,
    /// and the LCD (scrolling title, "ŁADOWANIE..." while the track loads). Every copy plays the same moment, so the
    /// speakers move together on every screen.
    /// </summary>
    public partial class Boombox
    {
        private float _cone, _coneVel, _thump, _reelAngle, _lcdScroll;
        private Vector3 _shake;

        /// <summary>Current cone excursion (m, forward), for tests.</summary>
        public float ConeExcursion => _cone;

        private void UpdateVisuals(in RadioState r, string url)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool playing = _stream != null && _stream.Playing;
            float vol = r.Volume / 100f;

            // Speaker cones: a springy follower of the bass envelope (stiff = fast attack, under-damped = "bum" rebound).
            float drive = playing ? _out.Kick * Mathf.Sqrt(vol) * (0.75f + 0.5f * r.Bass / 100f) : 0f;
            float target = Mathf.Min(drive, 1.5f) * 0.012f;                  // up to 1.8 cm out
            const float w = 62f, zeta = 0.32f;
            for (int i = 0; i < 4; i++)
            {
                float h = dt / 4f;
                float acc = w * w * (target - _cone) - 2f * zeta * w * _coneVel;
                _coneVel += acc * h;
                _cone += _coneVel * h;
            }
            _cone = Mathf.Clamp(_cone, -0.007f, 0.02f);
            float k = _cone / 0.012f;                                         // ~0..1.5
            foreach (var (dia, sur, cap) in new[] { (_diaL, _surL, _capL), (_diaR, _surR, _capR) })
            {
                if (dia == null) continue;
                dia.localPosition = new Vector3(0f, 0f, 0.019f + _cone);
                float sxy = 1f + 0.08f * k;                                    // the cone swells a bit too (visible head-on)
                dia.localScale = new Vector3(sxy, sxy, 1f + 0.25f * k);
                sur.localScale = new Vector3(1f, 1f, Mathf.Max(0.2f, 1f + _cone / 0.0065f));
                cap.localScale = new Vector3(1f, 1f, 1f + 0.5f * Mathf.Max(0f, k));
            }

            // Whole box: a short thump on the strong kicks, settles at once.
            float kick = playing ? _out.Kick : 0f;
            _thump = Mathf.Max(_thump * Mathf.Exp(-dt * 18f), Mathf.Clamp01((kick - 0.95f) * 2f) * vol);
            Vector3 jitter = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.0012f;
            _shake = Vector3.Lerp(_shake, jitter * _thump, 0.5f);
            if (_visual != null)
            {
                _visual.localPosition = _shake + Vector3.up * (0.0018f * _thump);
                _visual.localRotation = Quaternion.Euler(-0.5f * _thump, 0f, _shake.x * 300f);
            }

            // VU LEDs: 5 green, 2 amber, 1 red.
            float level = playing ? _out.Level * (0.5f + 0.5f * vol) : 0f;
            for (int i = 0; i < _leds.Length; i++)
            {
                bool on = level * 9f > i + 0.5f;
                Color c = i < 5 ? new Color(0.3f, 1f, 0.35f) : i < 7 ? new Color(1f, 0.72f, 0.15f) : new Color(1f, 0.22f, 0.12f);
                _mpb.SetColor("_Color", on ? c : c * 0.12f);
                _leds[i].SetPropertyBlock(_mpb);
            }

            // Cassette reels turn while music plays; PLAY key pressed; knobs follow the settings.
            if (playing) _reelAngle -= dt * 220f;
            if (_reelL != null)
            {
                _reelL.localRotation = Quaternion.Euler(0f, 0f, _reelAngle);
                _reelR.localRotation = Quaternion.Euler(0f, 0f, _reelAngle * 1.3f);
                _playKey.localPosition = new Vector3(_playKey.localPosition.x, H + (r.Playing && url != null ? 0.0005f : 0.004f), _playKey.localPosition.z);
                _knobVol.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 135f - 270f * vol);
                _knobBass.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 135f - 270f * r.Bass / 100f);
            }

            // LCD.
            string lcd;
            if (url == null) lcd = "-- RADIO --";
            else if (r.Playing && IsLoading)
            {
                int dots = (int)(Time.time * 3f) % 4;
                lcd = "ŁADOWANIE" + new string('.', dots) + new string(' ', 3 - dots) + (r.Loading && r.Peers > 1 ? $" {r.Ready}/{r.Peers}" : "");
            }
            else
            {
                string title = TryInfo(url, out var inf) ? inf.title : url;
                lcd = (r.Playing ? "> " : "|| ") + title;
            }
            if (lcd.Length > 16)
            {
                _lcdScroll += dt * 4f;
                int off = (int)_lcdScroll % (lcd.Length + 4);
                lcd = (lcd + "    " + lcd).Substring(off, 16);
            }
            else _lcdScroll = 0f;
            _lcd.text = lcd;
        }
    }
}
