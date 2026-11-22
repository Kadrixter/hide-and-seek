using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Plays one remote player's voice from their head: low-latency jitter buffer (<see cref="VoiceStream"/>), loudness
    /// boost x chat volume x this player's own volume (<see cref="PlayerVoiceVolumes"/>), garage echo that grows with
    /// distance, muffling behind walls, silence from other floors (<see cref="FloorAcoustics"/>).
    /// A dead talker heard by a dead listener (observer channel) is played flat in 2D, without range or effects.
    /// </summary>
    public class VoicePlayback : MonoBehaviour
    {
        private const float BaseGain = 2.4f;

        private AudioSource _source;
        private VoiceStream _stream;
        private AudioLowPassFilter _lowPass;
        private AudioEchoFilter _echo;
        private NetPlayer _owner;
        private float _floorGain = 1f, _floorTarget = 1f, _floorCutoff = 22000f;
        private float _nextFloorCheck, _nextOcclusion, _occlusion;
        private float _shoutUntil = -10f;
        private float _range;
        private string _nick;
        private float _nextNick;
        private bool _wasSpeaking;

        /// <summary>True while this player is shouting (heard across the garage).</summary>
        public bool Shouting => Time.time < _shoutUntil;

        /// <summary>Time.time of the last received packet (speaking indicator).</summary>
        public float LastPacketTime { get; private set; } = -10f;
        public bool IsSpeaking => Time.time - LastPacketTime < 0.4f;

        /// <summary>Current target delay of the jitter buffer in ms (debug).</summary>
        public int BufferMs => _stream != null ? _stream.TargetMs : 0;
        public VoiceStream Stream => _stream;
        /// <summary>True while played on the observer channel (2D, dead to dead).</summary>
        public bool DeadChannel { get; private set; }
        public int Packets { get; private set; }
        /// <summary>Current volume factor from other floors (1 = same floor).</summary>
        public float FloorGain => _floorGain;

        public static VoicePlayback For(NetPlayer p)
        {
            var vp = p.GetComponentInChildren<VoicePlayback>();
            if (vp != null) return vp;
            var go = new GameObject("Voice");
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            return go.AddComponent<VoicePlayback>();
        }

        private void Awake()
        {
            _owner = GetComponentInParent<NetPlayer>();
            // Component order matters: the stream filter first, then echo and low-pass act on the voice.
            _source = gameObject.AddComponent<AudioSource>();
            _stream = gameObject.AddComponent<VoiceStream>();
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Custom;
            _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.1f, 0.95f), new Keyframe(0.4f, 0.55f), new Keyframe(0.75f, 0.2f), new Keyframe(1f, 0f)));
            _source.minDistance = 1f;
            _source.maxDistance = VoiceChat.HearRange;
            _source.dopplerLevel = 0f;
            _source.spread = 30f;
            _source.volume = 1f;
            _source.priority = 8;
            _source.Play();

            _echo = gameObject.AddComponent<AudioEchoFilter>();
            _echo.delay = 320f;
            _echo.decayRatio = 0.35f;
            _echo.dryMix = 1f;
            _echo.wetMix = 0f;

            _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 22000f;
        }

        public void Push(ushort seq, byte[] data, int length, float level, long stamp)
        {
            if (!IsSpeaking) _nextFloorCheck = 0f; // a new sentence: check the floor before it starts playing
            LastPacketTime = Time.time;
            Packets++;
            if (level >= VoiceChat.ShoutLevel) _shoutUntil = Time.time + 0.8f;
            _stream.Push(seq, data, length, stamp);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextNick && _owner != null) { _nick = _owner.Nick; _nextNick = Time.unscaledTime + 1f; }
            _stream.Gain = BaseGain * Mathf.Max(0f, LocalProfile.ChatVolume) * PlayerVoiceVolumes.Get(_nick);
            bool speaking = IsSpeaking;
            _stream.Tick(speaking);

            DeadChannel = VoiceRules.OnDeadChannel(_owner) && VoiceRules.OnDeadChannel(NetPlayer.Local);
            if (DeadChannel)
            {
                // observer channel: flat, the same everywhere
                _source.spatialBlend = 0f;
                _source.volume = 1f;
                _echo.enabled = false;
                _lowPass.cutoffFrequency = 22000f;
                _floorGain = _floorTarget = 1f;
                _wasSpeaking = speaking;
                return;
            }
            _source.spatialBlend = 1f;

            // Shouts carry across the whole garage; the range eases back after the shout.
            bool lobby = VoiceChat.CurrentHearRange > VoiceChat.HearRange;
            float targetRange = Shouting && !lobby ? VoiceChat.ShoutRange : VoiceChat.CurrentHearRange;
            _range = targetRange > _range ? targetRange : Mathf.MoveTowards(_range, targetRange, Time.deltaTime * 120f);
            _source.maxDistance = Mathf.Max(5f, _range);

            var listener = Camera.main;
            Vector3 lp = listener != null ? listener.transform.position : transform.position;
            float dist = Vector3.Distance(lp, transform.position);

            // Another floor: the slab stops the voice; next to a ramp / stairwell only a muffled rest gets through.
            if (listener != null && Time.time >= _nextFloorCheck)
            {
                _nextFloorCheck = Time.time + 0.15f;
                _floorTarget = 1f;
                _floorCutoff = 22000f;
                if (FloorAcoustics.DifferentFloor(lp, transform.position))
                {
                    bool clear = AudioOcclusion.Sample(lp, transform.position, listener.transform.root, transform.parent, GameAudio.OcclusionMask) <= 0f;
                    _floorTarget = FloorAcoustics.Transfer(lp, transform.position, clear, out _floorCutoff);
                }
                if (!_wasSpeaking) _floorGain = _floorTarget; // never start a sentence loud and fade it out
            }
            _floorGain = Mathf.MoveTowards(_floorGain, _floorTarget, Time.deltaTime * 4f);
            float muffle = 1f - _floorGain;

            // Echo: only in the big concrete garage, stronger and longer the farther away the speaker is.
            bool garage = AudioEnvironment.Current == AudioEnvironmentKind.Garage;
            float far = Mathf.InverseLerp(4f, Shouting ? VoiceChat.ShoutRange * 0.6f : VoiceChat.HearRange, dist);
            _echo.wetMix = (garage ? Mathf.Lerp(0.18f, Shouting ? 0.85f : 0.6f, far) : 0f) * Mathf.Lerp(1f, 0.2f, muffle);
            _echo.delay = Mathf.Lerp(220f, Shouting ? 650f : 420f, far);
            _echo.decayRatio = garage ? Mathf.Lerp(0.3f, Shouting ? 0.55f : 0.45f, far) : 0f;
            _echo.enabled = garage;

            if (Time.time >= _nextOcclusion)
            {
                _nextOcclusion = Time.time + 0.15f;
                float target = 0f;
                if (listener != null && speaking)
                {
                    Vector3 d = transform.position - lp;
                    if (Physics.Raycast(lp, d.normalized, out var hit, d.magnitude, ~((1 << 8) | (1 << 9) | GameLayers.RagdollMask), QueryTriggerInteraction.Ignore)
                        && !hit.transform.IsChildOf(transform.parent) && !hit.transform.IsChildOf(listener.transform.root))
                        target = 1f;
                }
                _occlusion = target;
            }
            _source.volume = (1f - 0.4f * _occlusion) * _floorGain;
            float cutoff = Mathf.Min(_occlusion > 0.5f ? 1200f : 22000f, _floorGain < 0.99f ? _floorCutoff : 22000f);
            _lowPass.cutoffFrequency = Mathf.Lerp(_lowPass.cutoffFrequency, cutoff, 0.3f);
            _wasSpeaking = speaking;
        }
    }
}
