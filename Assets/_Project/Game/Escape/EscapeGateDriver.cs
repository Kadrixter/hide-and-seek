using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Cinematics;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Escape
{
    /// <summary>
    /// Every peer, on the exit gate object: plays the gate opening from the shared server time
    /// (<c>MatchManager.EscapeGateAt</c>): the shutter height, the motor with the chain and a clunk per link, the stop
    /// at the top, the siren (a loud 3D source at the gate plus a quiet, muffled copy heard everywhere - on other floors
    /// too, so the seeker knows where to run) and the spinning orange beacons. Switches the seekers' wall on for a
    /// local seeker. When the escape is reset (new round in the same scene) the gate is shut again; when the round
    /// ends the motor stops and the siren fades. The intro and the outro keep driving the gate themselves.
    /// </summary>
    public sealed class EscapeGateDriver : MonoBehaviour
    {
        ExitGate _gate;
        EscapeGateProps _props;
        AudioSource _motor, _siren3D, _sirenFar;
        AudioLowPassFilter _farFilter;
        bool _driving, _stopped, _stopPlayed, _outroShut;
        float _shownF;
        int _lastLink = -1;
        float _sirenVol;
        bool _loggedAlarm;

        public static EscapeGateDriver Attach(ExitGate gate)
        {
            var d = gate.GetComponent<EscapeGateDriver>();
            if (d != null) return d;
            d = gate.gameObject.AddComponent<EscapeGateDriver>();
            d._gate = gate;
            d._props = EscapeGateProps.Build(gate);
            Debug.Log($"[EscapeGate] driveway, boundary line (z {EscapeTimeline.LineZ}) and beacons built; stand at {EscapeTimeline.StandAt:0.0} s, full at {EscapeTimeline.FullAt:0.0} s");
            return d;
        }

        AudioSource Source(string name, Transform at, AudioClip clip, bool loop, float spatial, float min, float max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(at, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = min;
            s.maxDistance = max;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            return s;
        }

        void EnsureAudio()
        {
            if (_motor != null) return;
            var at = _props.SirenPoint != null ? _props.SirenPoint : transform;
            _motor = Source("EscGateMotor", at, EscapeGateSounds.Motor, true, 1f, 3f, 38f);
            _siren3D = Source("EscGateSiren", at, EscapeGateSounds.Siren, true, 1f, 8f, 75f);
            _sirenFar = Source("EscGateSirenFar", at, EscapeGateSounds.Siren, true, 0f, 1f, 2f);
            _farFilter = _sirenFar.gameObject.AddComponent<AudioLowPassFilter>();
            _farFilter.cutoffFrequency = 2500f;
        }

        /// <summary>Called every frame by <see cref="EscapeDirector"/> with the current match state.</summary>
        public void Tick(MatchManager mm)
        {
            var me = NetPlayer.Local;
            bool seekerWall = me != null && me.IsSeeker && (mm.Phase.Value == MatchPhase.Seeking || mm.Phase.Value == MatchPhase.Hiding);
            if (_props.SeekerWall.enabled != seekerWall) _props.SeekerWall.enabled = seekerWall;

            bool started = mm.EscapeGateStarted(_gate.Index); // each gate opens on its own (its own dot link panel)
            if (!started)
            {
                if (_driving) ResetGate();
                return;
            }
            // The outro owns the gate (it stages the winners in front of it, shut) - after its fade to black: until
            // then the open gate stays as it was, it does not snap shut under the end banner.
            var outro = OutroDirector.Current;
            if (outro != null && outro.T < OutroTimeline.FadeOut && _driving) { _gate.SetOpen(_shownF); Silence(false); return; }
            if (outro != null || IntroDirector.Current != null)
            {
                if (outro != null && !_outroShut) { _outroShut = true; _gate.SetOpen(0f); }
                Silence(true); Beacons(false, 0f); Sign(false);
                return;
            }

            double t = mm.Now - mm.EscapeGateAtOf(_gate.Index).Value;
            bool ended = mm.Phase.Value != MatchPhase.Seeking && mm.Phase.Value != MatchPhase.Hiding;
            if (!_driving)
            {
                _driving = true;
                _stopped = false;
                _outroShut = false;
                _stopPlayed = t >= EscapeTimeline.FullAt + 0.5f; // joined late / already up: no stop bang
                _lastLink = -1;
                _gate.SetLamp(false);
                Debug.Log($"[EscapeGate] gate opening shown (t={t:0.0} s)");
            }
            if (ended)
            {
                // Round over while opening: the motor stops where it is, the siren dies away.
                Silence(false);
                Beacons(false, 0f);
                return;
            }

            float f = EscapeTimeline.Fraction(t);
            _gate.SetOpen(f);
            _shownF = f;
            Sign(true);
            bool moving = t < EscapeTimeline.FullAt;
            bool siren = t < EscapeTimeline.SirenEnd;
            EnsureAudio();
            float sfx = GameAudio.SfxVolume;

            // Motor + chain while moving (wind-up over the first second).
            float motorVol = moving ? Mathf.Clamp01((float)t / 0.8f) * 0.85f : 0f;
            Play(_motor, motorVol * sfx, Mathf.Lerp(0.85f, 1f, Mathf.Clamp01((float)t)));
            if (moving && t > EscapeTimeline.SpinUp)
            {
                int link = Mathf.FloorToInt(((float)t - EscapeTimeline.SpinUp) / EscapeTimeline.LinkPeriod);
                if (link != _lastLink)
                {
                    if (_lastLink >= 0) _motor.PlayOneShot(EscapeGateSounds.Clunk, 0.75f * sfx);
                    _lastLink = link;
                }
            }
            if (!moving && !_stopPlayed)
            {
                _stopPlayed = true;
                _motor.PlayOneShot(EscapeGateSounds.Stop, 1f * sfx);
                Debug.Log($"[EscapeGate] shutter fully up at t={t:0.0} s");
            }

            // Siren: full while opening, fades over the last 2 s.
            float target = siren ? Mathf.Clamp01(((float)EscapeTimeline.SirenEnd - (float)t) / 2f) : 0f;
            _sirenVol = Mathf.MoveTowards(_sirenVol, target, Time.deltaTime * 2f);
            Play(_siren3D, _sirenVol * 1f * sfx, 1f);
            FarSiren(_sirenVol * sfx);
            if (siren && !_loggedAlarm)
            {
                _loggedAlarm = true;
                Debug.Log($"[EscapeGate] ALARM on (siren + beacons), heard on every level, t={t:0.0} s");
            }
            Beacons(siren || moving, (float)t);
        }

        /// <summary>The copy of the siren everybody hears: louder on level 0 far from the gate, muffled below.</summary>
        void FarSiren(float vol)
        {
            var lis = GameAudio.Listener;
            // One far copy for the whole map (the gate that opened first), not one per gate.
            var mm = MatchManager.Instance;
            if (lis == null || vol <= 0.001f || mm == null || _gate.Index != mm.FirstOpenGate()) { Play(_sirenFar, 0f, 1f); return; }
            Vector3 lp = lis.position;
            Vector3 gp = _siren3D.transform.position;
            int floors = Mathf.Abs(FloorAcoustics.FloorOf(lp.y) - FloorAcoustics.FloorOf(gp.y));
            float d = Vector3.Distance(lp, gp);
            float near = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, 45f, d)); // the 3D source covers the near part
            float level = floors == 0 ? 0.38f * near : floors == 1 ? 0.3f : 0.22f;
            _farFilter.cutoffFrequency = floors == 0 ? Mathf.Lerp(5000f, 2200f, Mathf.InverseLerp(40f, 180f, d)) : floors == 1 ? 1300f : 800f;
            Play(_sirenFar, vol * level, 1f);
        }

        static void Play(AudioSource s, float vol, float pitch)
        {
            if (s == null) return;
            s.volume = vol;
            s.pitch = pitch;
            if (vol > 0.001f && !s.isPlaying) s.Play();
            else if (vol <= 0.001f && s.isPlaying) s.Stop();
        }

        void Beacons(bool on, float t)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_props.BeaconSpin[i] == null) continue;
                // ~1.3 turns per second, the two out of phase.
                _props.BeaconSpin[i].localRotation = Quaternion.Euler(0f, t * 470f + i * 180f, 0f);
                _props.BeaconSpot[i].intensity = on ? 7f : 0f;
                float pulse = 0.5f + 0.5f * Mathf.Cos((t * 470f + i * 180f) * Mathf.Deg2Rad);
                _props.BeaconGlow[i].intensity = on ? 0.6f + 1.6f * pulse : 0f;
            }
            CineKit.SetGlow(_props.BeaconMat, new Color(1f, 0.5f, 0.05f), on ? 1.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 8.2f)) : 0.3f);
        }

        void Sign(bool on)
        {
            if (_props.OpenSign != null && _props.OpenSign.activeSelf != on) _props.OpenSign.SetActive(on);
        }

        /// <summary>Stops the sounds (at once, or the siren fading).</summary>
        void Silence(bool hard)
        {
            if (_motor == null) return;
            if (!_stopped)
            {
                _stopped = true;
                Debug.Log($"[EscapeGate] round over / cinematic: gate motor stopped, siren off");
            }
            Play(_motor, 0f, 1f);
            _sirenVol = hard ? 0f : Mathf.MoveTowards(_sirenVol, 0f, Time.deltaTime * 1.2f);
            Play(_siren3D, _sirenVol * GameAudio.SfxVolume, 1f);
            Play(_sirenFar, 0f, 1f);
        }

        void ResetGate()
        {
            _driving = false;
            _loggedAlarm = false;
            _sirenVol = 0f;
            Silence(true);
            Beacons(false, 0f);
            _gate.SetOpen(0f);
            _gate.SetLamp(true);
            Sign(false);
            Debug.Log("[EscapeGate] escape reset: gate shut");
        }
    }
}
