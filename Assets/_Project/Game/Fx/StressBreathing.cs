using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Fear when the seeker comes close (seeking phase, same floor): from <see cref="StartDistance"/> m the hider starts
    /// breathing harder and faster, loudest at <see cref="MaxDistance"/> m; it rises over a couple of seconds and calms
    /// down slowly. The local hider also hears their own heartbeat (2D, nobody else). Other players hear a hider's
    /// breathing quietly in 3D within a few metres (<see cref="OthersRange"/>) - tension, not a radar.
    /// Every client computes this from the synced positions, nothing extra goes over the network.
    /// </summary>
    public class StressBreathing : MonoBehaviour
    {
        public const float StartDistance = 18f, MaxDistance = 3f;
        public const float OthersRange = 6f;
        private const float RiseRate = 0.45f, FallRate = 0.1f; // stress per second

        private class State
        {
            public float Stress, NextBreath, NextBeat, CycleEnd;
            public bool InhaleNext = true;
            public int Variant;
            public Transform Mouth;
        }

        private readonly Dictionary<NetPlayer, State> _states = new Dictionary<NetPlayer, State>();
        private readonly List<NetPlayer> _seekers = new List<NetPlayer>();
        private readonly List<NetPlayer> _gone = new List<NetPlayer>();

        public static StressBreathing Instance { get; private set; }

        /// <summary>Stress 0..1 of a player as computed on this client (for tests / HUD).</summary>
        public float StressOf(NetPlayer p) => p != null && _states.TryGetValue(p, out var s) ? s.Stress : 0f;
        /// <summary>Last heartbeat / breath played locally (tests).</summary>
        public float LastBpm { get; private set; }
        public float LastHeartVolume { get; private set; }
        public float LastBreathVolume { get; private set; }
        public int Breaths { get; private set; }
        public int Beats { get; private set; }
        public int RemoteBreaths { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("[StressBreathing]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<StressBreathing>();
        }

        private void Update()
        {
            float dt = Time.deltaTime, now = Time.time;
            var mm = MatchManager.Instance;
            bool seeking = mm != null && mm.Phase.Value == MatchPhase.Seeking && !mm.RaceMode;

            _seekers.Clear();
            if (seeking)
                foreach (var p in NetPlayer.All)
                    if (p != null && p.IsSeeker && p.Life.Value != LifeState.Dead) _seekers.Add(p);

            var listener = GameAudio.Listener;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                if (!_states.TryGetValue(p, out var st)) _states[p] = st = new State { Variant = Random.Range(0, 3) };
                bool hider = seeking && p.Role.Value == PlayerRole.Hider && p.Life.Value == LifeState.Alive;
                float target = hider ? Target(p) : 0f;
                st.Stress = Mathf.MoveTowards(st.Stress, target, dt * (target > st.Stress ? RiseRate : FallRate));
                if (!hider) { st.Stress = Mathf.MoveTowards(st.Stress, 0f, dt * 0.5f); continue; }
                if (st.Stress < 0.03f) { st.NextBreath = Mathf.Max(st.NextBreath, now); continue; }

                if (p.IsOwner)
                {
                    Heart(st, now);
                    Breathe(p, st, now, true);
                }
                else if (listener != null && (listener.position - p.transform.position).sqrMagnitude < (OthersRange + 2f) * (OthersRange + 2f))
                    Breathe(p, st, now, false);
                else st.NextBreath = now + 0.3f;
            }

            // forget players that left
            _gone.Clear();
            foreach (var kv in _states) if (kv.Key == null) _gone.Add(kv.Key);
            foreach (var g in _gone) _states.Remove(g);
        }

        /// <summary>0 at <see cref="StartDistance"/> m or more, 1 at <see cref="MaxDistance"/> m; only on the same floor.</summary>
        private float Target(NetPlayer hider)
        {
            Vector3 h = hider.transform.position;
            float best = 0f;
            foreach (var s in _seekers)
            {
                Vector3 sp = s.transform.position;
                bool sameFloor = FloorAcoustics.SourceFloor(h.y) == FloorAcoustics.SourceFloor(sp.y) || Mathf.Abs(h.y - sp.y) < 1.6f;
                if (!sameFloor) continue;
                float d = Vector3.Distance(h, sp);
                float k = Mathf.InverseLerp(StartDistance, MaxDistance, d);
                best = Mathf.Max(best, k * k * (3f - 2f * k));
            }
            return best;
        }

        private void Heart(State st, float now)
        {
            float s = st.Stress;
            float bpm = Mathf.Lerp(78f, 150f, s);
            if (now < st.NextBeat) return;
            float vol = Mathf.Lerp(0.06f, 0.7f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.9f, s)));
            var src = GameAudio.PlayClip2D(StressSounds.Heartbeat(bpm), vol, GameSfx.HeartbeatLoop);
            if (src != null) src.priority = 20;
            LastBpm = bpm;
            LastHeartVolume = vol;
            Beats++;
            st.NextBeat = now + 60f / bpm * Random.Range(0.97f, 1.03f);
        }

        private void Breathe(NetPlayer p, State st, float now, bool local)
        {
            if (now < st.NextBreath) return;
            float s = st.Stress;
            float rate = Mathf.Lerp(16f, 46f, Mathf.Pow(s, 0.8f)); // breaths per minute
            float cycle = 60f / rate * Random.Range(0.93f, 1.07f);
            if (st.InhaleNext) st.CycleEnd = now + cycle;
            bool fast = s > 0.5f;
            bool inhale = st.InhaleNext;
            st.InhaleNext = !inhale;
            st.Variant++;
            var clip = inhale ? StressSounds.Inhale(fast, st.Variant) : StressSounds.Exhale(fast, st.Variant);
            float want = cycle * (inhale ? 0.38f : 0.47f);
            float pitch = Mathf.Clamp(clip.length / Mathf.Max(0.2f, want), 0.92f, 1.18f);
            float loud = Mathf.Lerp(0.08f, 0.6f, Mathf.SmoothStep(0f, 1f, s)) * (inhale ? (fast ? 1f : 0.8f) : 1f);

            AudioSource src;
            if (local) src = GameAudio.PlayClip2D(clip, loud, GameSfx.HiderBreathPanic);
            else
            {
                if (st.Mouth == null) st.Mouth = MouthOf(p);
                src = GameAudio.PlayClipAttached(clip, st.Mouth, loud * 0.8f, OthersRange, GameSfx.HiderBreathPanic, 0.5f);
            }
            if (src != null) src.pitch = pitch;
            float len = clip.length / pitch;
            // inhale -> a tiny hold -> exhale -> rest until the cycle is over
            st.NextBreath = inhale ? now + len + cycle * 0.04f : Mathf.Max(now + len + 0.03f, st.CycleEnd);
            if (local) { LastBreathVolume = loud; Breaths++; }
            else RemoteBreaths++;
        }

        private static Transform MouthOf(NetPlayer p)
        {
            var t = p.transform.Find("Breath");
            if (t != null) return t;
            t = new GameObject("Breath").transform;
            t.SetParent(p.transform, false);
            t.localPosition = new Vector3(0f, 1.5f, 0.1f);
            return t;
        }
    }
}
