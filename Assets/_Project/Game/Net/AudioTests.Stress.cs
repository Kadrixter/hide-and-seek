using System.Collections;
using System.IO;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public partial class AudioTests
    {
        /// <summary>
        /// -teststress: host = seeker (-forceseeker -autostart), client = hider; both with -teststress (+ -stresswav dir).
        /// The seeker walks up to the hider in steps (30, 16, 10, 6, 3 m), then stands 3 m away but one floor below,
        /// then 30 m away. The hider logs stress, heart rate and breathing every second; the seeker logs the hider's
        /// breathing it hears (3D, only within a few metres). With -stresswav both record what they hear (WAV) and the
        /// hider also saves the synthesised breath / heartbeat clips.
        /// </summary>
        private IEnumerator StressTest()
        {
            string dir = ArgOr("-stresswav", "");
            yield return WaitPhase(MatchPhase.Seeking);
            var me = NetPlayer.Local;
            GameAudio.MusicVolume = 0f;
            GameAudio.AmbientVolume = 0f;
            var tap = Tap();
            if (!string.IsNullOrEmpty(dir) && tap != null) tap.StartRecording(62f);
            if (!string.IsNullOrEmpty(dir) && !me.IsSeeker) DumpStressClips(dir);

            NetPlayer other = null;
            foreach (var p in NetPlayer.All) if (p != null && !p.IsOwner) other = p;
            var sb = StressBreathing.Instance;
            if (me.IsSeeker && other != null)
            {
                yield return new WaitForSeconds(1f);
                Vector3 h = other.transform.position;
                Vector3 dirToCentre = new Vector3(-h.x, 0f, -h.z).normalized;
                if (dirToCentre.sqrMagnitude < 0.5f) dirToCentre = Vector3.right;
                float yaw = Quaternion.LookRotation(-dirToCentre).eulerAngles.y;
                var stages = new (string name, Vector3 pos, float time)[]
                {
                    ("30 m", h + dirToCentre * 30f, 6f), ("16 m", h + dirToCentre * 16f, 6f), ("10 m", h + dirToCentre * 10f, 6f),
                    ("6 m", h + dirToCentre * 6f, 6f), ("3 m", h + dirToCentre * 3f, 8f),
                    ("3 m, floor below", FloorBelow(h + dirToCentre * 3f), 9f), ("30 m again", h + dirToCentre * 30f, 14f),
                };
                foreach (var s in stages)
                {
                    me.Motor.Teleport(s.pos + Vector3.up * 0.02f, yaw);
                    int before = sb.RemoteBreaths;
                    yield return new WaitForSeconds(s.time * 0.5f);
                    tap?.BeginWindow();
                    yield return new WaitForSeconds(s.time * 0.5f);
                    var rl = sb.LoopsOf(other);
                    Debug.Log($"[Stress] seeker at {s.name}: real dist {Vector3.Distance(me.transform.position, other.transform.position):0.0} m, " +
                              $"hider stress here {sb.StressOf(other):0.00}, hider breaths heard {sb.RemoteBreaths - before}, " +
                              $"recorded loop {(rl != null ? $"level {rl.Level:0.00} floor {rl.FloorGain:0.00} playing {rl.Playing}" : "none")}, " +
                              $"what the seeker hears {(tap != null ? tap.WindowDb() : -200f):0.0} dBFS");
                }
            }
            else
            {
                for (int i = 0; i < 60; i++)
                {
                    yield return new WaitForSeconds(1f);
                    NetPlayer seeker = null;
                    foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = p;
                    float d = seeker != null ? Vector3.Distance(seeker.transform.position, me.transform.position) : -1f;
                    Debug.Log($"[Stress] t={i + 1:00}s seeker {d:0.0} m (dy {(seeker != null ? seeker.transform.position.y - me.transform.position.y : 0f):0.0}): " +
                              $"stress {sb.StressOf(me):0.00}, heart {sb.LastBpm:0} bpm vol {sb.LastHeartVolume:0.00} beats {sb.Beats}, " +
                              $"breath vol {sb.LastBreathVolume:0.00} breaths {sb.Breaths}, {LoopInfo(sb.LoopsOf(me))}, output {(tap != null ? tap.WindowDb() : -200f):0.0} dBFS");
                    tap?.BeginWindow();
                }
            }
            if (!string.IsNullOrEmpty(dir) && tap != null) tap.SaveRecording(Path.Combine(dir, (me.IsSeeker ? "seeker" : "hider") + "_hears.wav"));
        }

        /// <summary>The floor surface one level below <paramref name="p"/> (ray down from under the slab).</summary>
        private static Vector3 FloorBelow(Vector3 p)
        {
            return Physics.Raycast(p + Vector3.down * 0.7f, Vector3.down, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point : p + Vector3.down * 3.4f;
        }

        private static string LoopInfo(BreathLoops l) => l == null ? "loops none"
            : $"loops calm {l.CalmWeight:0.00} panic {l.PanicWeight:0.00} playing {l.Playing} seams {l.Seams} accents {l.Accents}";

        private static void DumpStressClips(string dir)
        {
            Directory.CreateDirectory(dir);
            Save(dir, StressSounds.Inhale(false, 0)); Save(dir, StressSounds.Exhale(false, 0));
            Save(dir, StressSounds.Inhale(true, 1)); Save(dir, StressSounds.Exhale(true, 1));
            Save(dir, StressSounds.Heartbeat(80f)); Save(dir, StressSounds.Heartbeat(140f));

            static void Save(string d, AudioClip c)
            {
                var data = new float[c.samples];
                c.GetData(data, 0);
                using var w = new BinaryWriter(File.Create(Path.Combine(d, c.name + ".wav")));
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + data.Length * 2);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(c.frequency); w.Write(c.frequency * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(data.Length * 2);
                foreach (var s in data) w.Write((short)Mathf.Clamp(s * 32767f, -32768f, 32767f));
            }
        }
    }
}
