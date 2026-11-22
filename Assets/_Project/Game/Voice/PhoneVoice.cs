using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Counters of the toy phone channel (tests / HUD).
    /// </summary>
    public partial class VoiceChat
    {
        private int _rxPhone;
        /// <summary>Frames received on the phone channel.</summary>
        public int ReceivedPhone => _rxPhone;
    }

    /// <summary>
    /// One remote talker heard through the local player's toy phone: flat (2D, it is in your hand), own jitter buffer
    /// (<see cref="VoiceStream"/>), then <see cref="RadioFilter"/> - narrow band, light overdrive, hiss and crackle,
    /// squelch clicks at the start and end of every sentence. Distance and floors do not matter here.
    /// </summary>
    public class PhoneVoice : MonoBehaviour
    {
        private const float BaseGain = 2.6f;

        private AudioSource _source;
        private VoiceStream _stream;
        private RadioFilter _radio;
        private NetPlayer _owner;
        private string _nick;
        private float _nextNick;
        private bool _open;

        public float LastPacketTime { get; private set; } = -10f;
        public bool IsSpeaking => Time.time - LastPacketTime < 0.25f;
        public int Packets { get; private set; }
        public VoiceStream Stream => _stream;
        public RadioFilter Radio => _radio;

        public static PhoneVoice For(NetPlayer p)
        {
            var t = p.transform.Find("PhoneVoice");
            if (t != null && t.TryGetComponent(out PhoneVoice pv)) return pv;
            var go = new GameObject("PhoneVoice");
            go.transform.SetParent(p.transform, false);
            return go.AddComponent<PhoneVoice>();
        }

        private void Awake()
        {
            _owner = GetComponentInParent<NetPlayer>();
            // Order matters: the stream writes the voice, the radio filter colours it.
            _source = gameObject.AddComponent<AudioSource>();
            _stream = gameObject.AddComponent<VoiceStream>();
            _radio = gameObject.AddComponent<RadioFilter>();
            _source.spatialBlend = 0f;
            _source.volume = 0.8f;
            _source.priority = 9;
            _source.dopplerLevel = 0f;
            _source.Play();
        }

        public void Push(ushort seq, byte[] data, int length, long stamp)
        {
            LastPacketTime = Time.time;
            Packets++;
            _stream.Push(seq, data, length, stamp);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextNick && _owner != null) { _nick = _owner.Nick; _nextNick = Time.unscaledTime + 1f; }
            _stream.Gain = BaseGain * Mathf.Max(0f, LocalProfile.ChatVolume) * PlayerVoiceVolumes.Get(_nick);
            bool speaking = IsSpeaking;
            _stream.Tick(speaking);
            // Squelch: open when a sentence starts, close (click + noise tail) when it ends or the link drops.
            bool open = speaking && VoiceRules.LocalPhoneLink(_owner);
            if (open && !_open) _radio.Open();
            else if (!open && _open) _radio.Close();
            _open = open;
        }
    }
}
