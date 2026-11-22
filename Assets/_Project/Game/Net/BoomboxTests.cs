using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HideAndSeek.Game.Voice;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Boombox tests (own component, started from the command line):
    /// -testvcmusic: host + client, both with -voicetest (tone, always transmitting). The host stays silent 25 s, then
    /// plays the radio 60 s, then pauses it. Every copy sorts each second into "music off" / "music on" (by what its own
    /// boombox plays) and logs voice dropouts, lost / late frames, latency, main-thread hitches and audio-thread stats.
    /// </summary>
    public partial class BoomboxTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;

        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : def;
        }

        public const string DefaultUrl = "https://www.youtube.com/watch?v=7-QK_1oHoiQ";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Has("-testvcmusic") && !Has("-testboombox") && !Has("-testvcstall")) return;
            var go = new GameObject("[BoomboxTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<BoomboxTests>();
            if (Has("-testvcmusic") || Has("-testvcstall")) t.StartCoroutine(t.VoiceMusicTest());
            if (Has("-testboombox")) t.StartCoroutine(t.BoomboxTest());
        }

        // ---- main-thread hitches
        private float _maxDt;
        private int _hitches;

        private static readonly int StallMs = int.TryParse(ArgOr("-vcstall", "0"), out int ms) ? ms : 0;
        private float _nextStall = 10f, _evMax, _evUntil = -1f;
        private string _evName;
        private RadioState _lastRadio;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > _maxDt) _maxDt = dt;
            if (dt > 0.04f) _hitches++;
            // -vcstall N (host): the main thread stops for N ms every 4 s - a controlled hitch for the jitter buffer.
            if (StallMs > 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && Time.unscaledTime >= _nextStall)
            {
                _nextStall = Time.unscaledTime + 4f;
                System.Threading.Thread.Sleep(StallMs);
            }
            // Main-thread hitches right after every boombox state change (start, loading, next, pause, resume).
            var mm = MatchManager.Instance;
            if (mm != null && mm.RadioQueue != null)
            {
                var r = mm.Radio.Value;
                if (r.Seq != _lastRadio.Seq || r.Playing != _lastRadio.Playing || r.Loading != _lastRadio.Loading)
                {
                    if (_evUntil > 0f) Debug.Log($"[VcMusic] after '{_evName}': frame dt max {_evMax * 1000f:0} ms (cut short)");
                    _evName = $"seq {r.Seq} playing={r.Playing} loading={r.Loading}";
                    _evMax = 0f;
                    _evUntil = Time.unscaledTime + 3f;
                }
                _lastRadio = r;
            }
            if (_evUntil > 0f)
            {
                if (dt > _evMax) _evMax = dt;
                if (Time.unscaledTime > _evUntil)
                {
                    Debug.Log($"[VcMusic] after '{_evName}': frame dt max over 3 s = {_evMax * 1000f:0} ms");
                    _evUntil = -1f;
                }
            }
        }

        private static IEnumerator WaitLobby(int players)
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || Boombox.Instance == null || NetPlayer.All.Count < players)
            {
                mm = MatchManager.Instance;
                yield return null;
            }
        }

        private class Bucket
        {
            public int Seconds, Underruns, Lost, Late, Hitches, LatN;
            public double GapMs, OutMs, LatSum, LatMax, DtMax, CbGapMax, CbCostMax, JitterMax;
        }

        private IEnumerator VoiceMusicTest()
        {
            yield return WaitLobby(2);
            bool host = NetworkManager.Singleton.IsServer;
            string url = ArgOr("-radiourl", DefaultUrl);
            var buckets = new Dictionary<string, Bucket> { ["off"] = new Bucket(), ["on"] = new Bucket() };
            var prev = new Dictionary<VoiceStream, (int u, int lost, int late, double gap, double outMs, double latSum, int latN)>();
            yield return new WaitForSeconds(4f);
            Debug.Log($"[VcMusic] start host={host} delivery={(Has("-voicereliable") ? "reliable" : "unreliable")}");
            float t0 = Time.unscaledTime;
            bool added = false, paused = false;
            _maxDt = 0; _hitches = 0;
            bool radio = !Has("-testvcstall");
            bool switched = false, paused2 = false, resumed = false;
            while (Time.unscaledTime - t0 < (radio ? 105f : 60f))
            {
                yield return new WaitForSecondsRealtime(1f);
                float t = Time.unscaledTime - t0;
                if (host && radio && !switched && t >= 55f)
                {
                    switched = true;
                    MatchManager.Instance.ServerRadio(NetPlayer.Local, RadioOp.Add, url);
                    MatchManager.Instance.ServerRadio(NetPlayer.Local, RadioOp.Next, null);
                    Debug.Log($"[VcMusic] t={t:0} next track");
                }
                if (host && radio && !paused2 && t >= 78f)
                {
                    paused2 = true;
                    MatchManager.Instance.ServerRadio(NetPlayer.Local, RadioOp.Toggle, null);
                    Debug.Log($"[VcMusic] t={t:0} pause");
                }
                if (host && radio && !resumed && t >= 83f)
                {
                    resumed = true;
                    MatchManager.Instance.ServerRadio(NetPlayer.Local, RadioOp.Toggle, null);
                    Debug.Log($"[VcMusic] t={t:0} resume");
                }
                if (host && radio && !added && t >= 25f)
                {
                    added = true;
                    MatchManager.Instance.ServerRadio(NetPlayer.Local, RadioOp.Add, url);
                    Debug.Log($"[VcMusic] t={t:0} radio added");
                }
                if (host && radio && !paused && t >= 95f)
                {
                    paused = true;
                    MatchManager.Instance.ServerRadio(NetPlayer.Local, RadioOp.Toggle, null);
                    Debug.Log($"[VcMusic] t={t:0} radio paused");
                }
                var b = buckets[Boombox.Instance != null && Boombox.Instance.MusicAudible ? "on" : "off"];
                b.Seconds++;
                foreach (var vp in FindObjectsByType<VoicePlayback>(FindObjectsInactive.Exclude))
                {
                    var st = vp.Stream;
                    if (st == null) continue;
                    var lat = st.LatencyTotals;
                    var now = (st.Underruns, st.Lost, st.Late, st.GapMs, st.OutputMs, lat.sum, lat.n);
                    if (prev.TryGetValue(st, out var p))
                    {
                        b.Underruns += now.Item1 - p.u; b.Lost += now.Item2 - p.lost; b.Late += now.Item3 - p.late;
                        b.GapMs += now.Item4 - p.gap; b.OutMs += now.Item5 - p.outMs; b.LatSum += now.Item6 - p.latSum; b.LatN += now.Item7 - p.latN;
                    }
                    prev[st] = now;
                    if (lat.max > b.LatMax && b.LatN > 0) b.LatMax = lat.max;
                    b.JitterMax = Math.Max(b.JitterMax, st.JitterMs);
                    var cb = st.TakeCallbackStats();
                    b.CbGapMax = Math.Max(b.CbGapMax, cb.gapMax);
                    b.CbCostMax = Math.Max(b.CbCostMax, cb.costMax);
                }
                b.DtMax = Math.Max(b.DtMax, _maxDt * 1000.0);
                b.Hitches += _hitches;
                _maxDt = 0; _hitches = 0;
                if ((int)t % 5 == 0) Debug.Log($"[VcMusic] t={t:0} music={(Boombox.Instance != null && Boombox.Instance.MusicAudible)} {Boombox.Instance?.DebugState}");
            }
            var sb = new StringBuilder("[VcMusic] SUMMARY " + (host ? "host" : "client"));
            foreach (var kv in buckets)
            {
                var b = kv.Value;
                double min = Math.Max(1, b.Seconds) / 60.0;
                sb.Append($"\n[VcMusic]   music {kv.Key}: {b.Seconds}s underruns={b.Underruns} ({b.Underruns / min:0.0}/min) dropout={b.GapMs:0}ms " +
                          $"({(b.OutMs > 0 ? 100.0 * b.GapMs / b.OutMs : 0):0.00}% of output) lost={b.Lost} late={b.Late} " +
                          $"latency avg={(b.LatN > 0 ? b.LatSum / b.LatN : 0):0.0}ms max={b.LatMax:0.0} | frame dt max={b.DtMax:0}ms hitches>40ms={b.Hitches} " +
                          $"| voice cb gap max={b.CbGapMax:0.0}ms cost max={b.CbCostMax:0.00}ms | arrival jitter max={b.JitterMax:0}ms");
            }
            Debug.Log(sb.ToString());
        }
    }
}
