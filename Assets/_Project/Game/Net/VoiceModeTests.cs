using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HideAndSeek.Game.UI;
using HideAndSeek.Game.Voice;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testvoicemode: A/B of the voice chat modes by ear and by numbers. Host and client both get
    /// <c>-testvoicemode -voicefeed speech.wav -vmvariants c160,l80,l80j,... -vmout dir</c> (same list on both).
    /// The host sends the recording (16 kHz mono WAV) instead of the microphone once per variant, after switching the
    /// round setting (c = classic, l = low latency, number = delay ms, j = simulated jitter / loss on the host's link);
    /// it logs the UTC tick when the first sample was "captured". The client records the host's voice source (after
    /// echo / low-pass, before the mix) for the whole run into one WAV with UTC stamps of the audio blocks
    /// (<c>vm_client.wav</c> + <c>vm_client_stamps.txt</c>) and logs the stream counters every 0.5 s; an offline script
    /// cuts the variants out and measures delay, gaps and level. -vmshots dir: screenshots of the round settings panel.
    /// </summary>
    public class VoiceModeTests : MonoBehaviour
    {
        private const float Settle = 2.5f, Tail = 2.5f;

        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;
        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : def;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Has("-testvoicemode")) return;
            var go = new GameObject("[VoiceModeTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<VoiceModeTests>();
            t.StartCoroutine(t.Run());
        }

        private struct Variant
        {
            public string Name;
            public byte Mode;
            public int Delay;
            public bool Jitter;
            public int SwitchTo;
        }

        private static List<Variant> ParseVariants(string list)
        {
            var r = new List<Variant>();
            foreach (var raw in list.Split(','))
            {
                string t = raw.Trim().ToLowerInvariant();
                if (t.Length < 2) continue;
                bool j = t.EndsWith("j");
                string num = t.Substring(1, t.Length - 1 - (j ? 1 : 0));
                if (!int.TryParse(num, out int ms)) continue;
                // s<ms>: starts classic 160 ms, switches to low latency <ms> after a third of the sentence and back after two thirds
                r.Add(new Variant { Name = t, Mode = t[0] == 'l' ? VoiceMode.LowLatency : VoiceMode.Classic, Delay = t[0] == 's' ? VoiceMode.ClassicDefaultMs : ms,
                                    Jitter = j, SwitchTo = t[0] == 's' ? ms : 0 });
            }
            return r;
        }

        private IEnumerator Run()
        {
            VoiceChat.EnsureExists();
            VoiceChat.TestNoMic = true; // only the feed talks
            string outDir = ArgOr("-vmout", "Logs/voicemode");
            string shots = ArgOr("-vmshots", "");
            var feed = LoadWav(ArgOr("-voicefeed", ""));
            var variants = ParseVariants(ArgOr("-vmvariants", "c160,l80,l60,l130,c160j,l80j,l130j"));
            float jitter = float.TryParse(ArgOr("-vmjitter", "60"), out float jv) ? jv : 60f;
            float loss = float.TryParse(ArgOr("-vmloss", "2"), out float lv) ? lv : 2f;
            if (feed == null) { Debug.LogError("[VoiceModeTest] no -voicefeed wav"); yield break; }
            float feedSec = feed.Length / (float)VoiceCodec.SampleRate;

            while (MatchManager.Instance == null || NetPlayer.Local == null || NetPlayer.All.Count < 2) yield return null;
            yield return new WaitForSeconds(4f);
            bool host = NetworkManager.Singleton.IsServer;
            var mm = MatchManager.Instance;
            Debug.Log($"[VoiceModeTest] start host={host} variants={variants.Count} feed={feedSec:0.0}s jitter={jitter}ms loss={loss}% " +
                      $"out={AudioSettings.outputSampleRate}Hz legacy22={VoiceStream.Legacy22}");

            float total = variants.Count * (Settle + feedSec + Tail) + 12f;
            if (!host) StartCoroutine(Record(outDir, total)); // from now on: the host's first variant may start any moment
            if (!string.IsNullOrEmpty(shots)) yield return Shots(shots, host, mm);
            if (!host) yield break;

            foreach (var v in variants)
            {
                var s = mm.Settings.Value;
                s.VoiceMode = v.Mode;
                s.VoiceDelayMs = (ushort)v.Delay;
                mm.SetSettingsRpc(s);
                VoiceChat.SimReset(v.Jitter ? jitter : 0f, v.Jitter ? loss : 0f);
                yield return new WaitForSeconds(Settle);
                VoiceChat.Instance.StartFeed(feed);
                Debug.Log($"[VoiceModeTest] variant {v.Name} mode={VoiceMode.ModeName(v.Mode)} delay={v.Delay} jitter={v.Jitter} " +
                          $"t0={VoiceChat.Instance.FeedStartTicks} len={feed.Length}");
                if (v.SwitchTo > 0)
                {
                    // Mode changes while the voice plays (must be clean: fade, no click, no exception).
                    yield return new WaitForSeconds(feedSec / 3f);
                    SetVoice(mm, VoiceMode.LowLatency, v.SwitchTo);
                    Debug.Log($"[VoiceModeTest] switch t={DateTime.UtcNow.Ticks} -> low {v.SwitchTo}");
                    yield return new WaitForSeconds(feedSec / 3f);
                    SetVoice(mm, VoiceMode.Classic, VoiceMode.ClassicDefaultMs);
                    Debug.Log($"[VoiceModeTest] switch t={DateTime.UtcNow.Ticks} -> classic {VoiceMode.ClassicDefaultMs}");
                }
                while (VoiceChat.Instance.Feeding) yield return null;
                yield return new WaitForSeconds(Tail);
                Debug.Log($"[VoiceModeTest] variant {v.Name} done simDropped={VoiceChat.Instance.SimDropped}");
                VoiceChat.SimReset(0f, 0f);
            }
            var d = mm.Settings.Value;
            d.VoiceMode = VoiceMode.Classic;
            d.VoiceDelayMs = VoiceMode.ClassicDefaultMs;
            mm.SetSettingsRpc(d);
            Debug.Log("[VoiceModeTest] host done");
        }

        private static void SetVoice(MatchManager mm, byte mode, int ms)
        {
            var s = mm.Settings.Value;
            s.VoiceMode = mode;
            s.VoiceDelayMs = (ushort)ms;
            mm.SetSettingsRpc(s);
        }

        /// <summary>Client: records the host's voice source for the whole run, logs the stream counters.</summary>
        private IEnumerator Record(string outDir, float seconds)
        {
            NetPlayer hostPlayer = null;
            foreach (var p in NetPlayer.All) if (p != null && p.OwnerClientId == NetworkManager.ServerClientId) hostPlayer = p;
            if (hostPlayer == null) { Debug.LogError("[VoiceModeTest] host player not found"); yield break; }
            var vp = VoicePlayback.For(hostPlayer);
            var tap = vp.gameObject.AddComponent<VoiceTap>();
            tap.Begin(seconds);
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                var st = vp.Stream;
                var src = vp.GetComponent<AudioSource>();
                Debug.Log($"[VoiceModeTest] stat t={DateTime.UtcNow.Ticks} mode={VoiceMode.ModeName(VoiceMode.Mode)} delay={VoiceMode.DelayMs} " +
                          $"low={st.LowLatency} target={st.TargetMs} buffered={st.BufferedMs} gaps={st.Gaps} gapMs={st.GapMs:0} " +
                          $"underruns={st.Underruns} dropouts={st.Dropouts} lost={st.Lost} late={st.Late} packets={vp.Packets} " +
                          $"vol={(src != null ? src.volume : -1f):0.00} dist={Vector3.Distance(hostPlayer.transform.position, NetPlayer.Local.transform.position):0.0}");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            tap.Save(Path.Combine(outDir, "vm_client.wav"), Path.Combine(outDir, "vm_client_stamps.txt"));
            Debug.Log("[VoiceModeTest] client done");
        }

        private static IEnumerator Shots(string dir, bool host, MatchManager mm)
        {
            Directory.CreateDirectory(dir);
            GameHud.OpenSettingsPanel();
            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, (host ? "host" : "client") + "_settings_classic.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            GameHud.CloseAllMenus();
            if (host)
            {
                var s = mm.Settings.Value;
                s.VoiceMode = VoiceMode.LowLatency;
                s.VoiceDelayMs = VoiceMode.LowDefaultMs;
                mm.SetSettingsRpc(s);
            }
            yield return new WaitForSecondsRealtime(1f);
            GameHud.OpenSettingsPanel();
            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, (host ? "host" : "client") + "_settings_low.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            GameHud.CloseAllMenus();
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>16-bit PCM WAV (mono 16 kHz expected) -> floats.</summary>
        private static float[] LoadWav(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var b = File.ReadAllBytes(path);
            int pos = 12, channels = 1, bits = 16;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                int size = BitConverter.ToInt32(b, pos + 4);
                if (id == "fmt ") { channels = BitConverter.ToInt16(b, pos + 10); bits = BitConverter.ToInt16(b, pos + 22); }
                if (id == "data" && bits == 16)
                {
                    int n = Math.Min(size, b.Length - pos - 8) / 2 / channels;
                    var r = new float[n];
                    for (int i = 0; i < n; i++) r[i] = BitConverter.ToInt16(b, pos + 8 + i * 2 * channels) / 32768f;
                    return r;
                }
                pos += 8 + size + (size & 1);
            }
            return null;
        }
    }

    /// <summary>Records what one AudioSource outputs (filters before it included), with UTC stamps of the audio blocks.</summary>
    public class VoiceTap : MonoBehaviour
    {
        private readonly object _lock = new object();
        private float[] _rec;
        private int _pos, _channels = 2;
        private readonly List<(long sample, long ticks)> _stamps = new();
        private long _frames;

        public void Begin(float seconds)
        {
            lock (_lock)
            {
                _rec = new float[(int)(seconds * AudioSettings.outputSampleRate) * 2];
                _pos = 0;
                _frames = 0;
                _stamps.Clear();
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            long now = DateTime.UtcNow.Ticks;
            lock (_lock)
            {
                if (_rec == null || channels != 2) return;
                _channels = channels;
                if (_stamps.Count < 200000) _stamps.Add((_frames, now));
                int m = Mathf.Min(data.Length, _rec.Length - _pos);
                Array.Copy(data, 0, _rec, _pos, m);
                _pos += m;
                _frames += data.Length / channels;
            }
        }

        public void Save(string wav, string stamps)
        {
            float[] r; int n; List<(long, long)> st;
            lock (_lock) { r = _rec; n = _pos; st = new List<(long, long)>(_stamps); _rec = null; }
            if (r == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(wav)));
            int rate = AudioSettings.outputSampleRate;
            using (var w = new BinaryWriter(File.Create(wav)))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)_channels);
                w.Write(rate); w.Write(rate * _channels * 2); w.Write((short)(_channels * 2)); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
                for (int i = 0; i < n; i++) w.Write((short)Mathf.Clamp(r[i] * 32767f, -32768f, 32767f));
            }
            var sb = new StringBuilder();
            foreach (var (s, t) in st) sb.Append(s).Append(' ').Append(t).Append('\n');
            File.WriteAllText(stamps, sb.ToString());
            Debug.Log($"[VoiceModeTest] saved {wav} ({n / _channels / (float)rate:0.0} s, {st.Count} stamps)");
        }
    }
}
