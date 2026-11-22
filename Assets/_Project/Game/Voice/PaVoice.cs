using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>Counter of the public-address channel (tests).</summary>
    public partial class VoiceChat
    {
        private int _rxPa;
        /// <summary>Frames received on the public-address channel.</summary>
        public int ReceivedPa => _rxPa;
    }

    /// <summary>
    /// Plays the public-address channel (the holder of the wall microphone talking with the talk bar down) through the
    /// garage loudspeakers. Every level has its own speakers, so the floor silence between levels does not apply: the
    /// listener hears the speakers of the level he is on. A few emitters (<see cref="EmitterCount"/>) sit on the
    /// speakers nearest to the listener (3D, from the horn mouths); each has its own jitter buffer (<see cref="VoiceStream"/>)
    /// fed with the same frames and its own horn colouring (<see cref="PaFilter"/>) plus a light echo of the concrete
    /// (the garage reverb zone adds the rest). The channel opens with the first frame (crackle in the speakers and the
    /// talk bar's click at the holder's hand) and closes when the frames stop.
    /// </summary>
    public sealed class PaVoice : MonoBehaviour
    {
        public static PaVoice Instance { get; private set; }
        public const int EmitterCount = 4;
        private const float BaseGain = 2.5f;

        private sealed class Emitter
        {
            public Transform T;
            public AudioSource Src;
            public VoiceStream Stream;
            public PaFilter Filter;
            public int Speaker = -1;
        }

        private readonly Emitter[] _em = new Emitter[EmitterCount];
        private readonly List<(Vector3 pos, int floor)> _speakers = new();
        private readonly List<int> _order = new();
        private ulong _talker = ulong.MaxValue;
        private float _lastPacket = -10f, _closedAt = -10f, _nextAssign;
        private bool _open, _playing;
        private string _nick;
        private float _nextNick;
        private AudioListener _listener;

        /// <summary>Frames received on the PA channel (tests).</summary>
        public int Packets { get; private set; }
        /// <summary>How many times the channel opened (tests).</summary>
        public int Opens { get; private set; }
        public bool IsOpen => _open;
        public int SpeakerCount => _speakers.Count;

        /// <summary>True while <paramref name="p"/> is heard through the speakers.</summary>
        public static bool IsTalking(NetPlayer p) => Instance != null && Instance._open && p != null && p.NetworkObjectId == Instance._talker;

        public static PaVoice Create(Transform parent)
        {
            var go = new GameObject("PaVoice");
            go.transform.SetParent(parent, false);
            return go.AddComponent<PaVoice>();
        }

        private void Awake()
        {
            Instance = this;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.08f, 0.8f), new Keyframe(0.3f, 0.42f), new Keyframe(0.6f, 0.16f), new Keyframe(1f, 0f));
            for (int i = 0; i < EmitterCount; i++)
            {
                var go = new GameObject("PaEmitter" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                var stream = go.AddComponent<VoiceStream>();          // the clip (reads the jitter buffer)
                var echo = go.AddComponent<AudioEchoFilter>();        // concrete slap after the horn colouring
                echo.delay = 170f;
                echo.decayRatio = 0.3f;
                echo.wetMix = 0.26f;
                echo.dryMix = 1f;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Custom;
                src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
                src.minDistance = 1f;
                src.maxDistance = 42f;
                src.spread = 80f;
                src.dopplerLevel = 0f;
                src.priority = 4;
                src.reverbZoneMix = 1.05f;
                src.volume = 0f;
                var filter = new PaFilter(9137 + i * 7919) { CaptureThis = i == 0 };
                stream.Post = filter;
                _em[i] = new Emitter { T = go.transform, Src = src, Stream = stream, Filter = filter };
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Speaker horn mouths (world) and their level index (0, 1, 2 = levels 0, -1, -2).</summary>
        public void SetSpeakers(List<(Vector3 pos, int floor)> speakers)
        {
            _speakers.Clear();
            _speakers.AddRange(speakers);
            foreach (var e in _em) e.Speaker = -1;
            _nextAssign = 0f;
        }

        /// <summary>One frame of the PA channel from <paramref name="speaker"/> (already checked: he holds the mic).</summary>
        public void Push(NetPlayer speaker, ushort seq, byte[] data, int length, long stamp)
        {
            if (speaker == null || _speakers.Count == 0) return;
            if (_talker != speaker.NetworkObjectId)
            {
                _talker = speaker.NetworkObjectId;
                _nextNick = 0f;
            }
            _lastPacket = Time.time;
            Packets++;
            if (!_playing) StartPlaying();
            foreach (var e in _em) e.Stream.Push(seq, data, length, stamp);
        }

        private void StartPlaying()
        {
            _playing = true;
            Assign(true);
            foreach (var e in _em)
                if (!e.Src.isPlaying) e.Src.Play();
        }

        private void Update()
        {
            var talker = _talker != ulong.MaxValue ? NetPlayer.Find(_talker) : null;
            if (Time.unscaledTime >= _nextNick && talker != null) { _nick = talker.Nick; _nextNick = Time.unscaledTime + 1f; }
            float gain = BaseGain * Mathf.Max(0f, LocalProfile.ChatVolume) * PlayerVoiceVolumes.Get(_nick);
            bool speaking = Time.time - _lastPacket < 0.3f;
            int buffered = 0;
            foreach (var e in _em)
            {
                e.Stream.Gain = gain;
                e.Stream.Tick(speaking);
                buffered = Mathf.Max(buffered, e.Stream.BufferedMs);
            }

            // Squelch: open with the first frame, close once the frames stopped and the buffer played out.
            if (speaking && !_open)
            {
                _open = true;
                Opens++;
                foreach (var e in _em) e.Filter.Open();
                Click(talker, true);
            }
            else if (_open && !speaking && (buffered < 25 || Time.time - _lastPacket > 1.2f))
            {
                _open = false;
                _closedAt = Time.time;
                foreach (var e in _em) e.Filter.Close();
                Click(talker, false);
            }
            if (_playing && !_open && Time.time - _closedAt > 1.6f)
            {
                _playing = false;
                foreach (var e in _em) { e.Src.Stop(); e.Src.volume = 0f; }
            }

            if (_playing && Time.unscaledTime >= _nextAssign) Assign(false);
        }

        /// <summary>The talk bar clicks at the holder's hand for the people around him (the holder hears his own click).</summary>
        private static void Click(NetPlayer talker, bool down)
        {
            if (talker == null || talker.IsOwner) return;
            var bca = talker.Actions as BlockyCharacterActions;
            var mic = bca != null ? bca.PaMicTransform : null;
            Vector3 at = mic != null ? mic.position : talker.transform.position + Vector3.up * 1.45f;
            GameAudio.PlayClipAt(down ? PaSounds.ClickDown : PaSounds.ClickUp, at, 0.5f, 9f);
        }

        /// <summary>Puts the emitters on the speakers of the listener's level nearest to him (keeps the ones still near).</summary>
        private void Assign(bool force)
        {
            _nextAssign = Time.unscaledTime + 0.25f;
            if (!ActiveListener.Valid(_listener)) _listener = ActiveListener.Find();
            Vector3 lp = _listener != null ? _listener.transform.position : Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            int floor = -FloorAcoustics.ListenerFloor(lp.y); // 0, 1, 2
            _order.Clear();
            for (int i = 0; i < _speakers.Count; i++)
                if (_speakers[i].floor == floor) _order.Add(i);
            _order.Sort((a, b) => (_speakers[a].pos - lp).sqrMagnitude.CompareTo((_speakers[b].pos - lp).sqrMagnitude));
            int keep = Mathf.Min(_order.Count, EmitterCount + 2);
            // Emitters whose speaker is no longer among the nearest are freed.
            foreach (var e in _em)
            {
                if (e.Speaker < 0) continue;
                int at = _order.IndexOf(e.Speaker);
                if (force || at < 0 || at >= keep) e.Speaker = -1;
            }
            int next = 0;
            foreach (var e in _em)
            {
                if (e.Speaker >= 0) continue;
                while (next < _order.Count && IsUsed(_order[next])) next++;
                if (next >= Mathf.Min(_order.Count, EmitterCount)) break;
                e.Speaker = _order[next++];
            }
            foreach (var e in _em)
            {
                bool on = e.Speaker >= 0;
                if (on) e.T.position = _speakers[e.Speaker].pos;
                e.Src.volume = on ? 1f : 0f;
            }
        }

        private bool IsUsed(int speaker)
        {
            foreach (var e in _em) if (e.Speaker == speaker) return true;
            return false;
        }

        /// <summary>Test / log line.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder($"packets={Packets} opens={Opens} open={_open} playing={_playing} talker={_talker} speakers={_speakers.Count}");
            foreach (var e in _em)
                sb.Append($" | sp{e.Speaker} vol={e.Src.volume:0.0} buf={e.Stream.BufferedMs}ms lvl={e.Filter.OutputLevel:0.000}");
            return sb.ToString();
        }
    }
}
