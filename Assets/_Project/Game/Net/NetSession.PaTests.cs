using System;
using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Game.Voice;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Public-address tests (prepared, run only with the user's consent). -testpa on every copy, all with -voicetest
    /// (a tone instead of the microphone), host -forcehider -autostart -hidetime N -autostartplayers 3, own -port:
    /// builds (stations, speakers per level), host takes the level 0 microphone (prompt, holder), the other hider is
    /// refused at the level -1 station (busy prompt), host talks for 6 s (PA frames counted on every copy, also two
    /// levels away; emitters / levels logged), first person shot of the host and third person shots from the side,
    /// host walks off (cord pulled out of the hand = released), the other hider takes the level -1 microphone and
    /// hangs it back with E. -pashots &lt;dir&gt; = PNGs, -pawav &lt;dir&gt; = 4 s of the speaker sound on a listener (WAV).
    /// </summary>
    public partial class NetSession
    {
        private double _paT0;

        private void StartPaTests(Func<string, bool> has)
        {
            if (has("-testpa")) StartCoroutine(PaTest());
            if (ArgOr("-pawav", "") is string wav && wav.Length > 0) StartCoroutine(PaWav(wav));
        }

        private IEnumerator PaAt(float sec)
        {
            while (MatchManager.Instance != null && MatchManager.Instance.Now < _paT0 + sec) yield return null;
        }

        private static string PaState(MatchManager mm)
        {
            var h = mm.PaHolderPlayer;
            var pv = PaVoice.Instance;
            var vc = VoiceChat.Instance;
            return $"holder={(h != null ? h.Nick : "-")} station={mm.PaStation.Value} rxPa={(vc != null ? vc.ReceivedPa : -1)} hud='{PaHud.LineText}' voice[{(pv != null ? pv.Describe() : "none")}]";
        }

        private IEnumerator PaTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            _paT0 = mm.Now;
            var me = NetPlayer.Local;
            bool host = NetworkManager.Singleton.IsServer;
            string role = me.IsSeeker ? "seeker" : host ? "hostHider" : "hiderB";
            string dir = ArgOr("-pashots", "");
            NetPlayer hostP = NetPlayer.FindByClient(NetworkManager.ServerClientId);
            var pa = PaSystem.Instance;
            Debug.Log($"[PaTest] {me.Nick} is {role}; system={(pa != null)} speakers={(pa != null ? pa.SpeakerCount : 0)} skipped={(pa != null ? pa.SkippedSpeakers : 0)} " +
                      $"voiceSpeakers={(PaVoice.Instance != null ? PaVoice.Instance.SpeakerCount : 0)}");

            // ---- the empty station, lit (before anybody takes the mic)
            if (role == "hostHider")
            {
                Vector3 hang = PaLayout.MicHangPos(0);
                ItemLitShot(dir, "station_l0", hang + new Vector3(-0.2f, 0.25f, -1.1f), hang + new Vector3(-0.2f, 0.02f, 0f));
                ItemLitShot(dir, "station_l0_close", hang + new Vector3(0.05f, 0.1f, -0.55f), hang + new Vector3(-0.12f, 0f, 0f));
            }

            // ---- host takes the level 0 mic, hider B goes to the level -1 station, the seeker two levels down.
            yield return PaAt(1f);
            if (role == "hostHider") PaStand(me, 0, 0.75f);
            if (role == "hiderB") PaStand(me, 1, 0.75f);
            if (role == "seeker") me.TeleportLocal(new Vector3(-60f, PaLayout.FloorY(2) + 0.05f, -30f), 0f);
            yield return PaAt(2.5f);
            var lpc = LocalPlayerController.Instance;
            Debug.Log($"[PaTest] {role} prompt: {lpc.Prompt} '{lpc.PromptText}'");
            if (role == "hostHider")
            {
                Shot(dir, "prompt_take");
                me.RequestPaRpc(true, 0);
            }
            yield return PaAt(4f);
            Debug.Log($"[PaTest] {role} after take: {PaState(mm)}");
            if (role == "hiderB")
            {
                Debug.Log($"[PaTest] hiderB prompt at busy station: {lpc.Prompt} '{lpc.PromptText}'");
                me.RequestPaRpc(true, 1); // must be refused
            }
            yield return PaAt(5f);
            Debug.Log($"[PaTest] {role} after refused take: {PaState(mm)}");

            // ---- host talks for 6 s; hider B comes up to level 0 to film him from the side.
            if (role == "hostHider") NetPlayer.TestPaTalk = true;
            if (role == "hiderB" && hostP != null)
                me.TeleportLocal(hostP.transform.position + hostP.transform.right * 2.2f + hostP.transform.forward * 0.6f + Vector3.up * 0.05f, hostP.transform.eulerAngles.y - 105f);
            yield return PaAt(7f);
            if (role == "hostHider") Shot(dir, "talk_fpp");
            if (role == "hiderB" && hostP != null)
            {
                Vector3 at = hostP.transform.position + Vector3.up * 1.45f;
                Vector3 eye = me.EyePosition;
                me.Motor.SetLook(YawTo(at - eye), PitchTo(at - eye));
                Shot(dir, "talk_tpp_view");
                ItemLitShot(dir, "talk_tpp_side", at + hostP.transform.right * 1.3f + hostP.transform.forward * 0.9f + Vector3.up * 0.1f, at);
                ItemLitShot(dir, "talk_cord", PaLayout.MicHangPos(0) + new Vector3(0.9f, -0.2f, -1.6f), (PaLayout.GlandPos(0) + at) * 0.5f + Vector3.down * 0.3f);
            }
            yield return PaAt(9f);
            Debug.Log($"[PaTest] {role} while talking: {PaState(mm)} floor={FloorAcoustics.FloorOf(me.transform.position.y)}");
            yield return PaAt(11f);
            if (role == "hostHider") NetPlayer.TestPaTalk = false;
            yield return PaAt(12.5f);
            Debug.Log($"[PaTest] {role} after talking: {PaState(mm)}");

            // ---- host walks off with it: the cord pulls it out of the hand.
            if (role == "hostHider") me.TeleportLocal(me.transform.position + new Vector3(0f, 0f, -3.6f), 180f);
            yield return PaAt(14f);
            Debug.Log($"[PaTest] {role} after walking off: {PaState(mm)}");
            if (role == "hostHider") ItemLitShot(dir, "back_on_hook", PaLayout.MicHangPos(0) + new Vector3(0.1f, 0.15f, -0.7f), PaLayout.MicHangPos(0));

            // ---- hider B takes the level -1 mic, then hangs it back (E path = the same request).
            if (role == "hiderB") PaStand(me, 1, 0.75f);
            yield return PaAt(15.5f);
            if (role == "hiderB") me.RequestPaRpc(true, 1);
            yield return PaAt(17.5f);
            Debug.Log($"[PaTest] {role} B holds: {PaState(mm)}");
            if (role == "hiderB") Shot(dir, "b_holding_fpp");
            if (role == "hiderB") me.RequestPaRpc(false, 1);
            yield return PaAt(19.5f);
            Debug.Log($"[PaTest] {role} B hung back: {PaState(mm)}");
            Debug.Log($"[PaTest] {role} done");
        }

        /// <summary>Puts the player in front of the station of <paramref name="lv"/>, looking at the hanging mic.</summary>
        private static void PaStand(NetPlayer me, int lv, float dist)
        {
            Vector3 hang = PaLayout.MicHangPos(lv);
            Vector3 stand = hang + PaLayout.StationRot * new Vector3(0.12f, 0f, dist);
            stand.y = PaLayout.FloorY(lv) + 0.05f;
            Vector3 eye = stand + Vector3.up * 1.62f;
            me.TeleportLocal(stand, YawTo(hang - eye));
            me.Motor.SetLook(YawTo(hang - eye), PitchTo(hang - eye));
        }

        /// <summary>-pawav &lt;dir&gt;: 4 s of the horn-speaker sound (first emitter) once PA frames arrive.</summary>
        private IEnumerator PaWav(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            WriteWav(System.IO.Path.Combine(dir, "pa_click_down.wav"), PaSounds.ClickDown);
            WriteWav(System.IO.Path.Combine(dir, "pa_click_up.wav"), PaSounds.ClickUp);
            while (VoiceChat.Instance == null || VoiceChat.Instance.ReceivedPa < 10) yield return null;
            PaFilter.TestCaptureN = 0;
            PaFilter.TestCapture = new float[VoiceStream.Rate * 4];
            while (PaFilter.TestCaptureN < PaFilter.TestCapture.Length) yield return null;
            var cap = PaFilter.TestCapture;
            PaFilter.TestCapture = null;
            WriteWav(System.IO.Path.Combine(dir, "pa_speaker.wav"), cap, VoiceStream.Rate);
            Debug.Log($"[PaTest] wavs written to {dir}");
        }
    }
}
