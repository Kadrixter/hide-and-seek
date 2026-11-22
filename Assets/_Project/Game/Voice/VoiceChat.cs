using System;
using HideAndSeek.Game.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Proximity voice chat over the game connection (no external service).
    /// Microphone -> 20 ms 16 kHz mu-law frames -> server -> only listeners allowed by <see cref="VoiceRules"/> ->
    /// <see cref="VoicePlayback"/>. Frames go unreliable and carry a sequence number: a lost frame becomes a short
    /// concealed gap instead of stalling everything behind it (reliable delivery waits for the resend).
    /// Lobby: everybody hears everybody. Round: within <see cref="HearRange"/> m; the dead have their own channel and
    /// still hear the living, the living never hear the dead.
    /// </summary>
    public partial class VoiceChat : MonoBehaviour
    {
        public const string Message = "hns.voice";
        public const float HearRange = 60f;
        /// <summary>Shouting (level in the red zone) carries across the whole garage.</summary>
        public const float ShoutRange = 250f;
        /// <summary>Meter level (0..1) from which speech counts as a shout: end of the yellow zone.</summary>
        public const float ShoutLevel = 0.8f;
        public const float YellowLevel = 0.55f;
        public const float LobbyRange = 60f;

        public static VoiceChat Instance { get; private set; }

        /// <summary>True while the local player is transmitting.</summary>
        public bool Transmitting { get; private set; }
        /// <summary>Current microphone level 0..1 (for the HUD meter / settings).</summary>
        public float MicLevel { get; private set; }
        public string ActiveDevice { get; private set; }
        public bool MicAvailable => Microphone.devices.Length > 0;

        public static float CurrentHearRange => VoiceRules.LobbyLike ? LobbyRange : HearRange;

        /// <summary>-voicereliable: old reliable delivery (for comparison / bad routers).</summary>
        private static readonly bool Reliable = Array.IndexOf(Environment.GetCommandLineArgs(), "-voicereliable") >= 0;
        private static NetworkDelivery Delivery => Reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced;

        private AudioClip _mic;
        private int _readPos;
        private readonly float[] _frame = new float[VoiceCodec.FrameSamples];
        private readonly byte[] _encoded = new byte[VoiceCodec.FrameSamples];
        private byte[] _rx = new byte[VoiceCodec.FrameSamples * 2];
        private float _vadHangover;
        private NetworkManager _registeredOn;
        private ushort _seq;
        private bool _wantMic;
        private float _frameLevel;
        private string _deviceArg;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("[VoiceChat]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VoiceChat>();
            go.AddComponent<SpectatorVoiceLabel>();
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

            LogStats();
            if (session && TestTone)
            {
                ToneFrames(nm);
                return;
            }

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

            // Read every complete 20 ms frame the microphone has delivered since the last frame.
            int pos = Microphone.GetPosition(_deviceArg);
            int len = _mic.samples;
            int available = (pos - _readPos + len) % len;
            while (available >= VoiceCodec.FrameSamples)
            {
                _mic.GetData(_frame, _readPos);
                _readPos = (_readPos + VoiceCodec.FrameSamples) % len;
                available -= VoiceCodec.FrameSamples;
                ProcessFrame(nm, false, 0);
            }
        }

        /// <summary>Gain, level meter, push-to-talk / voice activation, then encode and send one frame.</summary>
        private void ProcessFrame(NetworkManager nm, bool forceSend, long stamp)
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
            // dB meter: -50 dBFS = 0, -5 dBFS = 1 (normal speech ~0.4-0.6, shouting > 0.8).
            float db = 20f * Mathf.Log10(Mathf.Max(rms, 1e-5f));
            float level = Mathf.Clamp01((db + 50f) / 45f);
            MicLevel = Mathf.Max(level, MicLevel * 0.92f); // fast attack, slower release for the HUD (20 ms frames)
            _frameLevel = level;

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
            // Public address: the talk bar of the wall microphone sends (also without V / below the open-mic threshold).
            var me = NetPlayer.Local;
            bool pa = me != null && me.PaTransmitting;
            if (pa) send = true;
            Transmitting = send;
            if (!send) return;
            _txPackets++;
            _seq++;

            for (int i = 0; i < _frame.Length; i++) _encoded[i] = VoiceCodec.Encode(_frame[i]);
            byte lvl = (byte)Mathf.RoundToInt(_frameLevel * 255f);

            if (nm.IsServer) Relay(nm, nm.LocalClientId, _seq, lvl, _encoded, _encoded.Length, stamp, pa);
            else
            {
                using var writer = new FastBufferWriter(_encoded.Length + 16, Allocator.Temp);
                WriteFrame(writer, _seq, lvl, _encoded, _encoded.Length, stamp, pa);
                nm.CustomMessagingManager.SendNamedMessage(Message, NetworkManager.ServerClientId, writer, Delivery);
            }
        }

        // ================================================================ network

        /// <summary>Flags byte: 1 = capture stamp follows, 2 = public-address talk bar down (client -> server).</summary>
        private static void WriteFrame(FastBufferWriter w, ushort seq, byte level, byte[] data, int len, long stamp, bool pa = false)
        {
            w.WriteValueSafe(seq);
            w.WriteValueSafe(level);
            w.WriteValueSafe((byte)((stamp != 0 ? 1 : 0) | (pa ? 2 : 0)));
            w.WriteValueSafe((ushort)len);
            w.WriteBytesSafe(data, len);
            if (stamp != 0) w.WriteValueSafe(stamp);
        }

        private bool ReadFrame(FastBufferReader r, out ushort seq, out byte level, out int len, out long stamp, out bool pa)
        {
            r.ReadValueSafe(out seq);
            r.ReadValueSafe(out level);
            r.ReadValueSafe(out byte flags);
            r.ReadValueSafe(out ushort l);
            len = l;
            stamp = 0;
            pa = (flags & 2) != 0;
            if (len > _rx.Length) return false;
            r.ReadBytesSafe(ref _rx, len);
            if ((flags & 1) != 0) r.ReadValueSafe(out stamp);
            return true;
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsServer && sender != NetworkManager.ServerClientId)
            {
                // Client -> server: relay to the listeners allowed to hear it.
                if (!ReadFrame(reader, out ushort seq, out byte lvl, out int len, out long stamp, out bool pa)) return;
                Relay(nm, sender, seq, lvl, _rx, len, stamp, pa);
            }
            else
            {
                // Server -> client: play (channel bits: 1 = proximity, 2 = toy phone, 4 = public address).
                reader.ReadValueSafe(out ulong speaker);
                reader.ReadValueSafe(out byte channel);
                if (!ReadFrame(reader, out ushort seq, out byte lvl, out int len, out long stamp, out _)) return;
                Play(speaker, channel, seq, lvl, _rx, len, stamp);
            }
        }

        private void Relay(NetworkManager nm, ulong speakerId, ushort seq, byte level, byte[] data, int len, long stamp, bool pa)
        {
            var speaker = NetPlayer.FindByClient(speakerId);
            if (speaker == null) return;
            bool shout = level / 255f >= ShoutLevel;
            // Public address: only the current holder of the wall microphone; everybody gets it (own speakers on every level).
            bool paOk = pa && VoiceRules.PaLink(speaker);

            var writers = new FastBufferWriter[8]; // per channel combination, built when first needed
            try
            {
                foreach (var listener in NetPlayer.All)
                {
                    if (listener == null || listener.OwnerClientId == speakerId) continue;
                    // The dead talk on their own channel: their voice is never even sent to the living.
                    bool near = VoiceRules.Hears(listener, speaker, shout) || TestRelayAll;
                    bool phone = VoiceRules.PhoneLink(listener, speaker);
                    if (!near && !phone && !paOk) continue;
                    byte ch = (byte)((near ? 1 : 0) | (phone ? 2 : 0) | (paOk ? 4 : 0));

                    if (listener.OwnerClientId == nm.LocalClientId) { Play(speakerId, ch, seq, level, data, len, stamp); continue; } // host listens locally
                    if (!writers[ch].IsInitialized)
                    {
                        writers[ch] = new FastBufferWriter(len + 26, Allocator.Temp);
                        writers[ch].WriteValueSafe(speakerId);
                        writers[ch].WriteValueSafe(ch);
                        WriteFrame(writers[ch], seq, level, data, len, stamp);
                    }
                    nm.CustomMessagingManager.SendNamedMessage(Message, listener.OwnerClientId, writers[ch], Delivery);
                }
            }
            finally
            {
                foreach (var w in writers) if (w.IsInitialized) w.Dispose();
            }
        }

        private static void Play(ulong speakerId, byte channel, ushort seq, byte level, byte[] data, int len, long stamp)
        {
            var p = NetPlayer.FindByClient(speakerId);
            if (p == null || p.IsOwner) return;
            if ((channel & 4) != 0 && VoiceRules.LocalPaLink(p) && PaVoice.Instance != null)
            {
                if (Instance != null) Instance._rxPa++;
                PaVoice.Instance.Push(p, seq, data, len, stamp);
            }
            if ((channel & 2) != 0 && VoiceRules.LocalPhoneLink(p))
            {
                if (Instance != null) Instance._rxPhone++;
                PhoneVoice.For(p).Push(seq, data, len, stamp);
            }
            if ((channel & 1) == 0) return;
            // Second check on the receiving side (an older / modified server): the living never hear the dead.
            if (!VoiceRules.LocalMayHear(p))
            {
                if (Instance != null) Instance._rejected++;
                return;
            }
            if (Instance != null) Instance._rxPackets++;
            VoicePlayback.For(p).Push(seq, data, len, level / 255f, stamp);
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
