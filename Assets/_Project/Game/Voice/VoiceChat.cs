using System;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Proximity voice chat over the game connection (no external service).
    /// Microphone -> 16 kHz mu-law frames -> server -> only listeners in range -> 3D <see cref="VoicePlayback"/>.
    /// Lobby: everybody hears everybody. Round: within <see cref="HearRange"/> m; the dead hear everyone,
    /// the living never hear the dead.
    /// </summary>
    public class VoiceChat : MonoBehaviour
    {
        public const string Message = "hns.voice";
        public const float HearRange = 35f;
        public const float LobbyRange = 60f;

        public static VoiceChat Instance { get; private set; }

        /// <summary>True while the local player is transmitting.</summary>
        public bool Transmitting { get; private set; }
        /// <summary>Current microphone level 0..1 (for the HUD meter / settings).</summary>
        public float MicLevel { get; private set; }
        public string ActiveDevice { get; private set; }
        public bool MicAvailable => Microphone.devices.Length > 0;

        public static float CurrentHearRange
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm == null || mm.Phase.Value == MatchPhase.Lobby || mm.Phase.Value == MatchPhase.Loading ? LobbyRange : HearRange;
            }
        }

        private AudioClip _mic;
        private int _readPos;
        private readonly float[] _frame = new float[VoiceCodec.FrameSamples];
        private readonly byte[] _encoded = new byte[VoiceCodec.FrameSamples];
        private byte[] _rx = new byte[VoiceCodec.FrameSamples * 2];
        private float _vadHangover;
        private NetworkManager _registeredOn;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("[VoiceChat]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VoiceChat>();
        }

        // ================================================================ microphone

        private void StartMic()
        {
            StopMic();
            if (!LocalProfile.VoiceEnabled || Microphone.devices.Length == 0) return;
            string device = LocalProfile.MicDevice;
            if (string.IsNullOrEmpty(device) || Array.IndexOf(Microphone.devices, device) < 0) device = null;
            _deviceArg = device;
            ActiveDevice = device ?? Microphone.devices[0];
            _mic = Microphone.Start(device, true, 1, VoiceCodec.SampleRate);
            _readPos = 0;
        }

        public void StopMic()
        {
            if (_mic != null) Microphone.End(_deviceArg);
            _mic = null;
            Transmitting = false;
        }

        /// <summary>Re-open the microphone after a settings change (device / on-off).</summary>
        public void Restart()
        {
            StopMic();
            _wantMic = false;
        }

        private bool _wantMic;
        private string _deviceArg;

        // ================================================================ loop

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            bool session = nm != null && nm.IsListening && NetPlayer.Local != null;

            // (Re-)register the message handler for each new session.
            if (nm != null && nm.IsListening && nm.CustomMessagingManager != null && _registeredOn != nm)
            {
                nm.CustomMessagingManager.RegisterNamedMessageHandler(Message, OnMessage);
                _registeredOn = nm;
            }
            if (nm == null || !nm.IsListening) _registeredOn = null;

            if (session && TestTone)
            {
                // Test mode: synthetic 440 Hz tone at real-time rate instead of the microphone.
                _toneClock += Time.unscaledDeltaTime;
                while (_toneClock >= VoiceCodec.FrameSamples / (float)VoiceCodec.SampleRate)
                {
                    _toneClock -= VoiceCodec.FrameSamples / (float)VoiceCodec.SampleRate;
                    for (int i = 0; i < _frame.Length; i++, _tonePhase++)
                        _frame[i] = 0.3f * Mathf.Sin(_tonePhase * 2f * Mathf.PI * 440f / VoiceCodec.SampleRate);
                    ProcessFrame(nm, true);
                }
                LogStats();
                return;
            }
            LogStats();

            bool want = session && LocalProfile.VoiceEnabled;
            if (want && !_wantMic) StartMic();
            if (!want && _wantMic) StopMic();
            _wantMic = want;
            if (!want || _mic == null)
            {
                Transmitting = false;
                MicLevel = 0f;
                return;
            }

            int pos = Microphone.GetPosition(_deviceArg);
            int len = _mic.samples;
            int available = (pos - _readPos + len) % len;
            while (available >= VoiceCodec.FrameSamples)
            {
                _mic.GetData(_frame, _readPos);
                _readPos = (_readPos + VoiceCodec.FrameSamples) % len;
                available -= VoiceCodec.FrameSamples;
                ProcessFrame(nm, false);
            }
        }

        private static readonly bool TestTone = Array.IndexOf(Environment.GetCommandLineArgs(), "-voicetest") >= 0;
        private float _toneClock;
        private long _tonePhase;
        private int _rxPackets, _txPackets;
        private float _nextStats;

        private void LogStats()
        {
            if (!TestTone && Array.IndexOf(Environment.GetCommandLineArgs(), "-debugpos") < 0) return;
            if (Time.unscaledTime < _nextStats) return;
            _nextStats = Time.unscaledTime + 3f;
            int buf = 0;
            foreach (var vp in FindObjectsByType<VoicePlayback>(FindObjectsInactive.Exclude)) buf = Mathf.Max(buf, vp.BufferMs);
            Debug.Log($"[Voice] tx={_txPackets} rx={_rxPackets} transmitting={Transmitting} bufferMs={buf}");
        }

        private void ProcessFrame(NetworkManager nm, bool forceSend)
        {
            float gain = LocalProfile.MicGain;
            float sum = 0f;
            for (int i = 0; i < _frame.Length; i++)
            {
                float v = Mathf.Clamp(_frame[i] * gain, -1f, 1f);
                _frame[i] = v;
                sum += v * v;
            }
            float rms = Mathf.Sqrt(sum / _frame.Length);
            MicLevel = Mathf.Clamp01(rms * 6f);

            bool menu = LocalPlayerController.MenuOpen;
            bool send;
            if (LocalProfile.VoiceOpenMic)
            {
                if (MicLevel >= LocalProfile.MicThreshold) _vadHangover = 0.35f;
                else _vadHangover -= VoiceCodec.FrameSamples / (float)VoiceCodec.SampleRate;
                send = _vadHangover > 0f;
            }
            else
            {
                var kb = Keyboard.current;
                send = kb != null && kb.vKey.isPressed && !menu;
            }
            if (forceSend) send = true;
            Transmitting = send;
            if (!send) return;
            _txPackets++;

            for (int i = 0; i < _frame.Length; i++) _encoded[i] = VoiceCodec.Encode(_frame[i]);

            if (nm.IsServer) Relay(nm, nm.LocalClientId, _encoded, _encoded.Length);
            else
            {
                using var writer = new FastBufferWriter(_encoded.Length + 8, Allocator.Temp);
                writer.WriteValueSafe((ushort)_encoded.Length);
                writer.WriteBytesSafe(_encoded, _encoded.Length);
                nm.CustomMessagingManager.SendNamedMessage(Message, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        // ================================================================ network

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsServer && sender != NetworkManager.ServerClientId)
            {
                // Client -> server: relay to listeners in range.
                reader.ReadValueSafe(out ushort len);
                if (len > _rx.Length) return;
                reader.ReadBytesSafe(ref _rx, len);
                Relay(nm, sender, _rx, len);
            }
            else
            {
                // Server -> client: play.
                reader.ReadValueSafe(out ulong speaker);
                reader.ReadValueSafe(out ushort len);
                if (len > _rx.Length) return;
                reader.ReadBytesSafe(ref _rx, len);
                Play(speaker, _rx, len);
            }
        }

        private void Relay(NetworkManager nm, ulong speakerId, byte[] data, int len)
        {
            var speaker = NetPlayer.FindByClient(speakerId);
            if (speaker == null) return;
            var mm = MatchManager.Instance;
            bool lobby = mm == null || mm.Phase.Value == MatchPhase.Lobby || mm.Phase.Value == MatchPhase.Loading;
            bool speakerDead = speaker.Life.Value == LifeState.Dead;

            using var writer = new FastBufferWriter(len + 16, Allocator.Temp);
            writer.WriteValueSafe(speakerId);
            writer.WriteValueSafe((ushort)len);
            writer.WriteBytesSafe(data, len);

            foreach (var listener in NetPlayer.All)
            {
                if (listener == null || listener.OwnerClientId == speakerId) continue;
                bool listenerDead = listener.Life.Value == LifeState.Dead;
                bool hears;
                if (lobby) hears = true;
                else if (speakerDead) hears = listenerDead;
                else hears = listenerDead || Vector3.Distance(listener.transform.position, speaker.transform.position) <= HearRange + 5f;
                if (!hears) continue;

                if (listener.OwnerClientId == nm.LocalClientId) Play(speakerId, data, len); // host listens locally
                else nm.CustomMessagingManager.SendNamedMessage(Message, listener.OwnerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private static void Play(ulong speakerId, byte[] data, int len)
        {
            if (Instance != null) Instance._rxPackets++;
            var p = NetPlayer.FindByClient(speakerId);
            if (p == null || p.IsOwner) return;
            VoicePlayback.For(p).Push(data, len);
        }

        /// <summary>True if the given remote player is currently heard talking.</summary>
        public static bool IsSpeaking(NetPlayer p)
        {
            var vp = p != null ? p.GetComponentInChildren<VoicePlayback>() : null;
            return vp != null && vp.IsSpeaking;
        }

        private void OnDestroy() => StopMic();
    }
}
