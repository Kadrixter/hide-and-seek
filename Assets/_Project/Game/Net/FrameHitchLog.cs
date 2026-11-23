using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Diagnostics for "the game froze" reports (Player.log has no clock): every scene change is logged with the time
    /// since start, a frame longer than <see cref="HitchSeconds"/> logs "[Hitch]", and a background watchdog logs when
    /// the main thread did not finish a frame for <see cref="StallSeconds"/> s (a real hang - the log line comes while it
    /// still hangs, so a killed process leaves it behind). Tells a hung game apart from a picture without a camera.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class FrameHitchLog : MonoBehaviour
    {
        public const float HitchSeconds = 0.6f, StallSeconds = 4f;

        private static volatile int _frame;
        private static volatile string _where = "";
        private static Thread _watch;
        private float _nextBodies;
        private static volatile bool _run;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("FrameHitchLog");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<FrameHitchLog>();
            go.AddComponent<FrameHitchLogLate>();
        }

        // Where the time of a frame goes: fixed steps (scripts + physics), Update, LateUpdate, rendering.
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static double _frameStart, _fixedFirst, _updateStart, _updateEnd, _lateEnd, _renderEnd = -1;
        private static int _fixedSteps;
        private static string _lastBreakdown = "";
        internal static double Now => Clock.Elapsed.TotalMilliseconds;
        private static double _cpuUpdateStart, _cpuUpdate;
        internal static void MarkUpdateEnd()
        {
            _updateEnd = Now;
            _cpuUpdate = ThreadCpuMs() - _cpuUpdateStart;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern System.IntPtr GetCurrentThread();
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool GetThreadTimes(System.IntPtr h, out long create, out long exit, out long kernel, out long user);

        /// <summary>CPU time of the calling thread (ms; 0 where the call is not available).</summary>
        private static double ThreadCpuMs()
        {
            try { return GetThreadTimes(GetCurrentThread(), out _, out _, out long k, out long u) ? (k + u) / 10000.0 : 0; }
            catch { return 0; }
        }
        internal static void MarkLateEnd() => _lateEnd = Now;
        internal static void MarkRenderEnd()
        {
            _renderEnd = Now;
            double start = _fixedSteps > 0 ? _fixedFirst : _updateStart;
            _lastBreakdown = $"fixed x{_fixedSteps} {(_fixedSteps > 0 ? _updateStart - _fixedFirst : 0):0} ms, update {_updateEnd - _updateStart:0} ms (cpu {_cpuUpdate:0}), " +
                             $"late {_lateEnd - _updateEnd:0} ms, render {_renderEnd - _lateEnd:0} ms, before {start - _frameStart:0} ms";
            _frameStart = _renderEnd;
            _fixedSteps = 0;
        }

        private void FixedUpdate()
        {
            if (_fixedSteps++ == 0) _fixedFirst = Now;
        }

        private System.Collections.IEnumerator Start()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                MarkRenderEnd();
            }
        }

        private void Awake()
        {
            SceneManager.activeSceneChanged += (a, b) =>
                Debug.Log($"[Scene] active {a.name} -> {b.name} at {Time.realtimeSinceStartup:0.00} s, phase {PhaseText()}");
            _run = true;
            _watch = new Thread(Watch) { IsBackground = true, Name = "HnsFrameWatch" };
            _watch.Start();
        }

        private void OnApplicationQuit() => _run = false;

        private static string PhaseText()
        {
            var mm = MatchManager.Instance;
            return mm != null && mm.IsSpawned ? mm.Phase.Value.ToString() : "-";
        }

        private void Update()
        {
            _updateStart = Now;
            _cpuUpdateStart = ThreadCpuMs();
            _frame++;
            float dt = Time.unscaledDeltaTime;
            string scene = SceneManager.GetActiveScene().name;
            _where = $"scene {scene}, phase {PhaseText()}, t {Time.realtimeSinceStartup:0.0} s";
            if (dt > HitchSeconds && Time.frameCount > 30)
            {
                string extra = "";
                if (Time.realtimeSinceStartup >= _nextBodies)
                {
                    _nextBodies = Time.realtimeSinceStartup + 5f;
                    int bodies = 0, awake = 0, dyn = 0;
                    foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude))
                    {
                        bodies++;
                        if (!rb.isKinematic) dyn++;
                        if (!rb.IsSleeping()) awake++;
                    }
                    extra = $"; rigidbodies {bodies} (dynamic {dyn}, awake {awake}), fixed dt {Time.fixedDeltaTime:0.000}";
                }
                Debug.LogWarning($"[Hitch] frame took {dt:0.00} s ({_where}); last frame: {_lastBreakdown}{extra}");
            }
        }

        /// <summary>Background thread: the frame counter stands still for <see cref="StallSeconds"/> s = the main thread hangs.</summary>
        private static void Watch()
        {
            int last = _frame;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            double since = 0, reported = 0;
            while (_run)
            {
                Thread.Sleep(250);
                int f = _frame;
                double now = clock.Elapsed.TotalSeconds;
                if (f != last)
                {
                    if (reported > 0) Debug.LogWarning($"[Hitch] main thread back after {now - since:0.0} s stuck (last seen: {_where})");
                    last = f;
                    since = now;
                    reported = 0;
                    continue;
                }
                if (since == 0) since = now;
                double stuck = now - since;
                if (stuck >= StallSeconds && (reported == 0 || stuck - reported >= 10))
                {
                    reported = stuck;
                    Debug.LogWarning($"[Hitch] MAIN THREAD STUCK {stuck:0} s (last frame: {_where})");
                }
            }
        }
    }

    /// <summary>Runs after every other script: marks the end of Update and LateUpdate for <see cref="FrameHitchLog"/>.</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class FrameHitchLogLate : MonoBehaviour
    {
        private void Update() => FrameHitchLog.MarkUpdateEnd();
        private void LateUpdate() => FrameHitchLog.MarkLateEnd();
    }
}
