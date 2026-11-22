using System.Collections;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Voice;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public partial class AudioTests
    {
        private const float FloorVoiceStart = 56f; // s after the seeking phase began: voice stage (both copies)

        /// <summary>
        /// -testfloorsnd: host (+ -autostart) and one client, both with -voicetest. The host stands at fixed spots and
        /// measures what it hears (RMS / peak of the mixed output) for a car alarm loop and a glock shot placed on the
        /// same floor, one floor below / above, two floors below, by the ramp shaft and by a stairwell. Then the client's
        /// voice tone from the same floor, from directly below and from below by the ramp shaft.
        /// </summary>
        private IEnumerator FloorSoundTest()
        {
            yield return WaitPhase(MatchPhase.Seeking);
            float t0 = Time.time;
            var me = NetPlayer.Local;
            GameAudio.MusicVolume = 0f;
            GameAudio.AmbientVolume = 0f;
            if (!NetworkManager.Singleton.IsServer)
            {
                // out of the way (floor -2) while the host measures sound effects
                yield return new WaitForSeconds(0.5f);
                me.Motor.Teleport(new Vector3(60f, -6.78f, 40f), 0f);
                yield return VoiceStages(t0, false);
                yield break;
            }
            yield return new WaitForSeconds(2f);
            var tap = Tap();
            float refDb = 0f, refPeak = 0f;
            var cases = new (string name, Vector3 listener, Vector3 source)[]
            {
                ("same floor, 10 m", new Vector3(-30f, 0.02f, 20f), new Vector3(-30f, 1f, 30f)),
                ("floor -1, right below", new Vector3(-30f, 0.02f, 20f), new Vector3(-30f, -2.4f, 21f)),
                ("listener on -1, source on 0 at the floor (rc car)", new Vector3(-30f, -3.38f, 20f), new Vector3(-30f, 0.05f, 21f)),
                ("floor -2", new Vector3(-30f, 0.02f, 20f), new Vector3(-30f, -5.8f, 21f)),
                ("ramp shaft: both next to it, slab between", new Vector3(-41f, 0.02f, -4f), new Vector3(-41f, -2.4f, -4f)),
                ("ramp: down the ramp", new Vector3(-41f, 0.02f, -4f), new Vector3(-68f, -2.4f, -4f)),
                ("stairwell: outside both doors", new Vector3(-33f, 0.02f, -7.5f), new Vector3(-31f, -2.4f, -7.5f)),
                ("floor -1, 25 m from any opening", new Vector3(10f, 0.02f, 30f), new Vector3(12f, -2.4f, 32f)),
            };
            for (int i = 0; i < cases.Length; i++)
            {
                var c = cases[i];
                me.Motor.Teleport(c.listener, 0f);
                yield return new WaitForSeconds(1.2f);
                tap.BeginWindow();
                yield return new WaitForSeconds(0.8f);
                float noise = tap.WindowDb();
                var at = new GameObject("FloorTestSound").transform;
                at.position = c.source;
                var loop = GameAudio.StartLoop(GameSfx.CarAlarm, at, 1f);
                tap.BeginWindow();
                yield return new WaitForSeconds(0.25f);
                float first = tap.WindowPeakDb(); // the very start must already be quiet
                yield return new WaitForSeconds(1.25f);
                float db = tap.WindowDb();
                float vol = loop != null ? loop.volume : -1f;
                GameAudio.StopLoop(loop, 0.05f);
                yield return new WaitForSeconds(0.6f);
                tap.BeginWindow();
                GameAudio.PlayAt(GameSfx.GlockShot, c.source, 1f);
                yield return new WaitForSeconds(1.2f);
                float shot = tap.WindowPeakDb();
                if (i == 0) { refDb = db; refPeak = shot; }
                Vector3 cam = me.Camera.transform.position;
                float transfer = FloorAcoustics.Transfer(cam, c.source, false, out float cutoff);
                Debug.Log($"[FloorSnd] {c.name}: alarm {db:0.0} dBFS ({db - refDb:+0.0;-0.0} dB vs same floor, first 0.25 s peak {first:0.0}), " +
                          $"source vol {vol:0.000}, glock peak {shot:0.0} ({shot - refPeak:+0.0;-0.0} dB), background {noise:0.0} dBFS, " +
                          $"floors L{FloorAcoustics.ListenerFloor(cam.y)}/S{FloorAcoustics.SourceFloor(c.source.y)} transfer(blocked)={transfer:0.000} cutoff={cutoff:0}");
                Destroy(at.gameObject);
                yield return new WaitForSeconds(0.6f);
            }
            yield return VoiceStages(t0, true);
        }

        /// <summary>Voice tone of the client from the same floor, from right below and from below by the ramp shaft.</summary>
        private IEnumerator VoiceStages(float t0, bool host)
        {
            var me = NetPlayer.Local;
            var stages = new (string name, Vector3 host, Vector3 client)[]
            {
                ("voice same floor 7 m", new Vector3(-30f, 0.02f, 20f), new Vector3(-30f, 0.02f, 27f)),
                ("voice floor -1 right below", new Vector3(-30f, 0.02f, 20f), new Vector3(-30f, -3.38f, 21f)),
                ("voice floor -1 by the ramp shaft", new Vector3(-41f, 0.02f, -4f), new Vector3(-41f, -3.38f, -3f)),
                ("voice floor -2", new Vector3(-30f, 0.02f, 20f), new Vector3(-30f, -6.78f, 21f)),
            };
            float refDb = 0f;
            var tap = host ? Tap() : null;
            for (int i = 0; i < stages.Length; i++)
            {
                float start = t0 + FloorVoiceStart + i * 9f;
                while (Time.time < start) yield return null;
                me.Motor.Teleport(host ? stages[i].host : stages[i].client, 0f);
                if (!host) continue;
                // measure only once the client really stands at its spot (the copies' clocks differ a little)
                NetPlayer other = null;
                foreach (var p in NetPlayer.All) if (p != null && !p.IsOwner) other = p;
                float until = Time.time + 8f;
                while (other != null && Time.time < until && (other.transform.position - stages[i].client).sqrMagnitude > 1f) yield return null;
                yield return new WaitForSeconds(1.5f);
                tap.BeginWindow();
                yield return new WaitForSeconds(2f);
                float db = tap.WindowDb();
                if (i == 0) refDb = db;
                var vp = other != null ? other.GetComponentInChildren<VoicePlayback>() : null;
                Debug.Log($"[FloorSnd] {stages[i].name}: {db:0.0} dBFS ({db - refDb:+0.0;-0.0} dB vs same floor), " +
                          $"voice floor gain {(vp != null ? vp.FloorGain : -1f):0.000}, packets {(vp != null ? vp.Packets : 0)}");
            }
            Debug.Log("[FloorSnd] done");
        }
    }
}
