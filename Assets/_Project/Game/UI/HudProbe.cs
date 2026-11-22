using System;
using System.Collections;
using System.IO;
using System.Text;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Test of the HUD (player list, key hints, dust badge): -testhud [-hudshots &lt;dir&gt;] [-hudplayers N] [-hidetime N].
    /// Every copy with -testhud saves &lt;dir&gt;/&lt;nick&gt;_&lt;step&gt;.png at the same moments; the host drives the round:
    /// lobby (one client ready, the other waiting), lobby after 20 s (hints gone), start, hiding, seeking, then one hider
    /// gets zraniony -> ranny -> powalony (bleed-out bar full, then its last seconds: deep red, pulsing) -> martwy,
    /// last shot 25 s into seeking (no key hints any more), then quit.
    /// A client with -voicetest sends a tone all the time (= speaking in the list).
    /// </summary>
    public sealed class HudProbe : MonoBehaviour
    {
        private string _dir;

        private static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i < 0 ? null : i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Arg("-testhud") == null) return;
            var go = new GameObject("[HudProbe]");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<HudProbe>();
            string d = Arg("-hudshots");
            p._dir = string.IsNullOrEmpty(d) ? "hudshots" : d;
            Directory.CreateDirectory(p._dir);
        }

        private IEnumerator Start()
        {
            while (NetPlayer.Local == null || MatchManager.Instance == null) yield return null;
            var mm = MatchManager.Instance;
            bool host = NetworkManager.Singleton.IsServer;
            int need = int.TryParse(Arg("-hudplayers"), out int n) ? n : 3;
            for (float w = 0f; NetPlayer.All.Count < need && w < 120f; w += Time.unscaledDeltaTime) yield return null;
            Log("players in: " + NetPlayer.All.Count);
            yield return new WaitForSecondsRealtime(2f);

            if (host)
            {
                NetPlayer first = null;
                foreach (var p in NetPlayer.All)
                    if (p != null && p.OwnerClientId != NetworkManager.ServerClientId && (first == null || p.OwnerClientId < first.OwnerClientId)) first = p;
                if (first != null) first.Ready.Value = true;
            }
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("1_lobby");
            yield return new WaitForSecondsRealtime(18f);
            yield return Shot("2_lobby_po20s");

            if (host)
            {
                foreach (var p in NetPlayer.All) if (p != null) p.Ready.Value = true;
                var st = mm.Settings.Value;
                st.HideTime = float.TryParse(Arg("-hidetime"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ht) ? ht : 12f;
                mm.Settings.Value = st;
                yield return new WaitForSecondsRealtime(0.5f);
                mm.RequestStartRpc();
            }

            while (mm.Phase.Value != MatchPhase.Hiding) yield return null;
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("3_chowanie");

            while (mm.Phase.Value != MatchPhase.Seeking) yield return null;
            float t0 = Time.unscaledTime;
            NetPlayer victim = null;
            foreach (var p in NetPlayer.All)
                if (p != null && p.IsHider && p.OwnerClientId != NetworkManager.ServerClientId) victim = p;

            yield return Until(t0 + 3f);
            yield return Shot("4_szukanie");
            if (host && victim != null) victim.Limping.Value = true;
            yield return Until(t0 + 6.5f);
            yield return Shot("5_zraniony");
            if (host && victim != null) { victim.Limping.Value = false; victim.Wounded.Value = true; }
            if (host && NetPlayer.Local.IsHider) NetPlayer.Local.Limping.Value = true;
            yield return Until(t0 + 9.5f);
            yield return Shot("6_ranny");
            if (host && victim != null)
            {
                victim.Wounded.Value = false;
                victim.DownedUntil.Value = mm.Now + mm.BleedOut; // like MatchManager.Down: the bar starts full
                victim.Life.Value = LifeState.Downed;
            }
            yield return Until(t0 + 12.5f);
            yield return Shot("7_powalony");
            if (host && victim != null) victim.DownedUntil.Value = mm.Now + 7d; // the last seconds of the bleed-out
            yield return Until(t0 + 15f);
            yield return Shot("7b_powalony_koncowka");
            yield return Until(t0 + 15.6f);
            yield return Shot("7c_powalony_puls");
            if (host && victim != null) victim.Life.Value = LifeState.Dead;
            yield return Until(t0 + 18.5f);
            yield return Shot("8_martwy");
            yield return Until(t0 + 25f);
            yield return Shot("9_szukanie_po25s");
            Log("done");
            yield return new WaitForSecondsRealtime(host ? 4f : 1f);
            Application.Quit();
        }

        private static IEnumerator Until(float t)
        {
            while (Time.unscaledTime < t) yield return null;
        }

        private IEnumerator Shot(string step)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"{LocalProfile.Nick}_{step}.png"));
            Log(step);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private static void Log(string step)
        {
            var sb = new StringBuilder($"[HudProbe] {LocalProfile.Nick} {step}: phase={MatchManager.Instance?.Phase.Value} hints={KeyHints.Remaining:0.0}s players=");
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                bool speaking = p == NetPlayer.Local ? Voice.VoiceChat.Instance != null && Voice.VoiceChat.Instance.Transmitting : Voice.VoiceChat.IsSpeaking(p);
                sb.Append($"{p.Nick}({p.Role.Value},{p.Life.Value},ready={p.Ready.Value},w={p.Wounded.Value},l={p.Limping.Value},mic={speaking}) ");
            }
            sb.Append($" screen={Screen.width}x{Screen.height}");
            Debug.Log(sb.ToString());
        }
    }
}
