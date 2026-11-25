using System;
using System.Collections;
using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The "Muzyka, radio i tło" setting turns the radio down on this copy only, logs [MusicVolTest]:
    ///  -testmusicvol [dir] (host, best alone in the lobby): queues a track on the lobby boombox (-radiourl), waits for
    ///  the sound, then sets the music volume to 70 / 0 / 35 / 100 / 70 % and measures the source volume and the level of
    ///  the own mix (3 s each). In the game scene (with -autostart) every radio found on the map is checked too (source
    ///  volume at 70 % and 0 %).
    /// </summary>
    public class RadioVolumeTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;
        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length && !Args[i + 1].StartsWith("-") ? Args[i + 1] : def;
        }

        private static int _fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Has("-testmusicvol")) return;
            var go = new GameObject("[RadioVolumeTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<RadioVolumeTests>();
            t.StartCoroutine(Has("-autostart") ? t.MapRadios() : t.Lobby());
        }

        private static void Log(string s) => Debug.Log("[MusicVolTest] " + s);
        private static void Fail(string s) { _fails++; Debug.LogWarning("[MusicVolTest] FAIL: " + s); }

        private IEnumerator Lobby()
        {
            MatchManager mm = null;
            while ((mm = MatchManager.Instance) == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || Boombox.Instance == null) yield return null;
            yield return new WaitForSeconds(2f);
            var me = NetPlayer.Local;
            var bb = Boombox.Instance;
            Vector3 stand = new Vector3(4.75f, 0f, -3.75f);
            Vector3 l = bb.AimPoint - (stand + Vector3.up * 1.62f);
            me.Motor.Teleport(stand, 0f);
            me.Motor.SetLook(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < 4; i++) mm.ServerRadio(me, RadioOp.VolUp, null);
            mm.ServerRadio(me, RadioOp.Add, ArgOr("-radiourl", BoomboxTests.DefaultUrl));
            float t0 = Time.realtimeSinceStartup;
            while (!bb.MusicAudible && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            if (!bb.MusicAudible) { Fail($"no radio sound after 60 s ({bb.DebugState})"); yield break; }
            Log($"radio audible after {Time.realtimeSinceStartup - t0:0.0} s, radio volume {mm.Radio.Value.Volume}");
            foreach (var vp in FindObjectsByType<HideAndSeek.Game.Voice.VoicePlayback>(FindObjectsInactive.Exclude))
                if (vp.TryGetComponent<AudioSource>(out var vs)) vs.mute = true;
            var lis = FindAnyObjectByType<AudioListener>();
            var tap = lis.GetComponent<ListenerTap>() ?? lis.gameObject.AddComponent<ListenerTap>();
            float baseVol = -1f, baseDb = 0f;
            foreach (float v in new[] { 0.7f, 0f, 0.35f, 1f, 0.7f })
            {
                GameAudio.MusicVolume = v;
                yield return new WaitForSeconds(0.5f);
                tap.BeginWindow();
                yield return new WaitForSeconds(3f);
                float vol = bb.Source.volume, db = tap.WindowDb();
                Log($"music setting {v * 100f:0}%: radio source volume {vol:0.000}, own mix {db:0.0} dBFS (peak {tap.WindowPeakDb():0.0})");
                if (baseVol < 0f) { baseVol = vol; baseDb = db; continue; }
                float want = baseVol * Mathf.Clamp01(v / 0.7f);
                if (Mathf.Abs(vol - want) > 0.002f) Fail($"setting {v * 100f:0}%: source volume {vol:0.000}, expected {want:0.000}");
                if (v == 0f && db > -70f) Fail($"setting 0%: the radio is still heard ({db:0.0} dBFS)");
                if (v >= 0.7f && Mathf.Abs(db - baseDb) > 6f) Fail($"setting {v * 100f:0}%: level {db:0.0} dBFS vs {baseDb:0.0} at 70%");
            }
            Log($"lobby done, fails={_fails}");
        }

        private IEnumerator MapRadios()
        {
            MatchManager mm = null;
            while ((mm = MatchManager.Instance) == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(2f);
            var boxes = FindObjectsByType<Boombox>(FindObjectsInactive.Exclude);
            Log($"game scene: {boxes.Length} radios");
            foreach (var b in boxes)
            {
                if (b == null || b.Source == null) continue;
                // Stand next to it (same level, in the open): the floor / occlusion gain must not be 0.
                Vector3 at = b.transform.position;
                float floor = HideAndSeek.Map.MapInfo.FloorUnder(at.y);
                NetPlayer.Local.Motor.Teleport(ItemPlacement.Scatter(new Vector3(at.x, floor, at.z), ItemPlacement.Small) + Vector3.up * 0.05f, 0f);
                GameAudio.MusicVolume = 0.7f;
                yield return new WaitForSeconds(1.5f);
                float full = b.Source.volume, scale = b.VolumeScale;
                GameAudio.MusicVolume = 0f;
                yield return new WaitForSeconds(0.3f);
                float muted = b.Source.volume;
                Log($"radio {b.name} at {b.transform.position}: 70% -> {full:0.000} (scale {scale:0.00}), 0% -> {muted:0.000}");
                if (muted > 0.0001f) Fail($"radio {b.name} not muted at 0%");
                if (full <= 0.0001f) Fail($"radio {b.name} silent at 70% next to it (scale {scale:0.00})");
            }
            GameAudio.MusicVolume = 0.7f;
            Log($"map done, fails={_fails}");
        }
    }
}
