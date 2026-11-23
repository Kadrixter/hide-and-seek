using System;
using System.Collections;
using System.Globalization;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// -testcursor: logs "[CursorTest]" with the cursor state every time the number of players changes (somebody joined /
    /// left) and 0.5 s later, plus at every phase change. -leaveafter S: this copy leaves the session to the menu after S
    /// seconds in it (a graceful leave, like Esc > Wyjdź); -quitafter S: quits the game (the other side sees a drop).
    /// </summary>
    public sealed class CursorProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var a = Environment.GetCommandLineArgs();
            if (Array.IndexOf(a, "-testcursor") < 0) return;
            var go = new GameObject("CursorProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<CursorProbe>();
        }

        private static float Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length && float.TryParse(a[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : -1f;
        }

        private static string State() =>
            $"{Cursor.lockState}/{(Cursor.visible ? "visible" : "hidden")} wantsFree={CursorPolicy.WantsFree} " +
            $"players={NetPlayer.All.Count} scene={SceneManager.GetActiveScene().name} " +
            $"phase={(MatchManager.Instance != null ? MatchManager.Instance.Phase.Value.ToString() : "-")}";

        private IEnumerator Start()
        {
            float leave = Arg("-leaveafter"), quit = Arg("-quitafter");
            float joinedAt = -1f;
            int lastCount = -1;
            string lastPhase = "";
            while (true)
            {
                yield return null;
                if (NetPlayer.Local == null) { joinedAt = -1f; continue; }
                if (joinedAt < 0f) joinedAt = Time.realtimeSinceStartup;
                int n = NetPlayer.All.Count;
                string ph = MatchManager.Instance != null ? MatchManager.Instance.Phase.Value.ToString() : "-";
                if (n != lastCount || ph != lastPhase)
                {
                    string why = n != lastCount ? $"players {lastCount} -> {n}" : $"phase {lastPhase} -> {ph}";
                    lastCount = n;
                    lastPhase = ph;
                    Debug.Log($"[CursorTest] {NetPlayer.Local.Nick} {why}: {State()}");
                    StartCoroutine(Later(why));
                }
                float inSession = Time.realtimeSinceStartup - joinedAt;
                if (leave > 0f && inSession > leave) { Debug.Log("[CursorTest] leaving to the menu"); leave = -1f; NetSession.Instance?.Leave(); }
                if (quit > 0f && inSession > quit) { Debug.Log("[CursorTest] quitting"); Application.Quit(); quit = -1f; }
            }
        }

        private IEnumerator Later(string why)
        {
            yield return new WaitForSeconds(0.5f);
            if (NetPlayer.Local != null) Debug.Log($"[CursorTest] {NetPlayer.Local.Nick} {why} +0.5 s: {State()}");
        }
    }
}
