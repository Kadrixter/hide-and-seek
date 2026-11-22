using System.Collections;
using System.Text;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public partial class AudioTests
    {
        /// <summary>
        /// -testsawsound on both copies (host -autostart, -forceseeker or -forcehider; optional -sawwav &lt;dir&gt;): the
        /// seeker (host or client) takes the chainsaw from the shelf (pull start), idles, revs (F), stops revving, puts
        /// it away and takes it out again, then hits the hider. Each copy logs what it hears (RMS / peak of the mixed
        /// output), every chainsaw voice (volume, floor gain, occlusion, low-pass, virtual) and which AudioListener the
        /// sound code uses. The hider waits 4 m from the shelf, or with -sawfar on level 0 far from any opening.
        /// </summary>
        private IEnumerator SawSoundTest()
        {
            yield return WaitPhase(MatchPhase.Seeking);
            float t0 = Time.time;
            var me = NetPlayer.Local;
            if (!me.IsSeeker) { yield return SawListen(t0); yield break; }
            string wav = ArgOr("-sawwav", "");
            me.TeleportLocal(ChainsawShelf.Position + new Vector3(0f, 0.05f, -1.6f), 0f);
            yield return new WaitForSeconds(3f);
            var tap = Tap();
            if (!string.IsNullOrEmpty(wav)) tap.StartRecording(30f);
            float T() => Time.time - t0;

            tap.BeginWindow();
            yield return new WaitForSeconds(1f);
            Debug.Log($"[SawSnd] {T():0.0}s background {tap.WindowDb():0.0} dBFS {Census()} {ListenerInfo()}");

            Debug.Log($"[SawSnd] {T():0.0}s pickup");
            me.RequestPickupChainsawRpc();
            tap.BeginWindow();
            yield return new WaitForSeconds(0.9f);
            Debug.Log($"[SawSnd] {T():0.0}s pull start: {tap.WindowDb():0.0} dBFS, peak {tap.WindowPeakDb():0.0}{GameAudio.DebugVoices("Chainsaw")}");
            yield return new WaitForSeconds(0.8f);
            tap.BeginWindow();
            yield return new WaitForSeconds(2f);
            Debug.Log($"[SawSnd] {T():0.0}s idle: {tap.WindowDb():0.0} dBFS, peak {tap.WindowPeakDb():0.0}{GameAudio.DebugVoices("Chainsaw")} {Census()}");

            LocalPlayerController.TestSawRev = true;
            yield return new WaitForSeconds(0.6f);
            tap.BeginWindow();
            yield return new WaitForSeconds(2f);
            Debug.Log($"[SawSnd] {T():0.0}s rev (F): {tap.WindowDb():0.0} dBFS, peak {tap.WindowPeakDb():0.0} SawRev={me.SawRev.Value}{GameAudio.DebugVoices("Chainsaw")}");
            LocalPlayerController.TestSawRev = false;
            yield return new WaitForSeconds(0.8f);
            tap.BeginWindow();
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[SawSnd] {T():0.0}s idle after rev: {tap.WindowDb():0.0} dBFS{GameAudio.DebugVoices("Chainsaw")}");

            me.RequestEquipRpc(HeldItem.Knife);
            yield return new WaitForSeconds(1.5f);
            tap.BeginWindow();
            yield return new WaitForSeconds(1f);
            Debug.Log($"[SawSnd] {T():0.0}s knife out: {tap.WindowDb():0.0} dBFS{GameAudio.DebugVoices("Chainsaw")}");
            me.RequestEquipRpc(HeldItem.Chainsaw);
            tap.BeginWindow();
            yield return new WaitForSeconds(0.9f);
            Debug.Log($"[SawSnd] {T():0.0}s saw out again (pull start): {tap.WindowDb():0.0} dBFS, peak {tap.WindowPeakDb():0.0}{GameAudio.DebugVoices("Chainsaw")}");
            yield return new WaitForSeconds(0.8f);
            tap.BeginWindow();
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[SawSnd] {T():0.0}s idle again: {tap.WindowDb():0.0} dBFS{GameAudio.DebugVoices("Chainsaw")}");

            NetPlayer hider = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsAliveHider) hider = p;
            if (hider != null)
            {
                Vector3 hp = hider.transform.position;
                me.TeleportLocal(hp + hider.transform.forward * 1.0f, Quaternion.LookRotation(-hider.transform.forward).eulerAngles.y);
                yield return new WaitForSeconds(1.5f);
                tap.BeginWindow();
                Debug.Log($"[SawSnd] {T():0.0}s cut");
                me.RequestChainsawRpc(hider.NetworkObjectId);
                yield return new WaitForSeconds(0.5f);
                Debug.Log($"[SawSnd] {T():0.0}s cut +0.5{GameAudio.DebugVoices("Chainsaw")}");
                yield return new WaitForSeconds(1.5f);
                Debug.Log($"[SawSnd] {T():0.0}s cut: {tap.WindowDb():0.0} dBFS, peak {tap.WindowPeakDb():0.0} severed={hider.Severed.Value}");
            }
            if (!string.IsNullOrEmpty(wav)) tap.SaveRecording(System.IO.Path.Combine(wav, "saw_seeker.wav"));
            Debug.Log("[SawSnd] done " + ListenerInfo());
        }

        /// <summary>Hider side: waits near the shelf (or far away on level 0 with -sawfar) and logs what it hears.</summary>
        private IEnumerator SawListen(float t0)
        {
            var me = NetPlayer.Local;
            yield return new WaitForSeconds(0.5f);
            bool far = Has("-sawfar");
            me.TeleportLocal(far ? new Vector3(10f, 0.05f, 30f) : ChainsawShelf.Position + new Vector3(2.5f, 0.05f, -3.2f), 0f);
            var tap = Tap();
            string wav = ArgOr("-sawwav", "");
            yield return new WaitForSeconds(2f);
            if (!string.IsNullOrEmpty(wav)) tap.StartRecording(30f);
            float end = Time.time + 28f;
            int n = 0;
            while (Time.time < end)
            {
                tap.BeginWindow();
                yield return new WaitForSeconds(0.5f);
                string voices = n++ % 2 == 0 ? GameAudio.DebugVoices("Chainsaw") : "";
                Debug.Log($"[SawSnd] hider {Time.time - t0:0.0}s {tap.WindowDb():0.0} dBFS, peak {tap.WindowPeakDb():0.0}{voices}");
            }
            if (!string.IsNullOrEmpty(wav)) tap.SaveRecording(System.IO.Path.Combine(wav, "saw_hider.wav"));
            Debug.Log("[SawSnd] hider done " + Census() + " " + ListenerInfo());
        }

        /// <summary>Which AudioListener the pooled sfx use, and whether it is the one that really hears (enabled).</summary>
        private static string ListenerInfo()
        {
            var l = GameAudio.Listener;
            var me = NetPlayer.Local;
            if (l == null) return "[listener: none]";
            var al = l.GetComponent<AudioListener>();
            bool mine = me != null && l.IsChildOf(me.transform);
            return $"[listener: {l.root.name}/{l.name} enabled={(al != null && al.isActiveAndEnabled)} mine={mine}]";
        }

        /// <summary>All playing AudioSources: how many, how many virtual (Unity plays 32 real voices) and which.</summary>
        private static string Census()
        {
            int playing = 0, virt = 0;
            var names = new StringBuilder();
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            {
                if (!s.isPlaying) continue;
                playing++;
                if (!s.isVirtual) continue;
                virt++;
                if (virt <= 12) names.Append(s.clip != null ? s.clip.name : s.name).Append(' ');
            }
            return $"[sources playing {playing}, virtual {virt}: {names}]";
        }
    }
}
