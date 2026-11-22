using System;
using System.Collections;
using System.IO;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testquitconfirm &lt;folder&gt;: screenshots of the "are you sure" dialogs. In the main menu (no -host / -join):
    /// the quit question. In the lobby (-host): Esc menu + quit question, then "Nie" (the menu must stay open), then the
    /// "close the server" question. Nothing is confirmed, the copy keeps running.
    /// </summary>
    public class QuitConfirmTest : MonoBehaviour
    {
        private string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-testquitconfirm");
            if (i < 0) return;
            var go = new GameObject("[QuitConfirmTest]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<QuitConfirmTest>();
            t._dir = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "quitconfirm";
            Directory.CreateDirectory(t._dir);
            t.StartCoroutine(t.Run(Array.IndexOf(args, "-host") >= 0 || Array.IndexOf(args, "-join") >= 0));
        }

        private IEnumerator Run(bool session)
        {
            if (!session)
            {
                yield return new WaitForSeconds(4f);
                ConfirmDialog.ConfirmQuit();
                yield return Shot("menu_quit");
                Log("main menu");
                yield break;
            }
            while (MatchManager.Instance == null || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(4f);
            var hud = FindAnyObjectByType<GameHud>();
            typeof(GameHud).GetMethod("ShowEscMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(hud, null);
            yield return new WaitForSeconds(0.5f);
            Click("Quit");
            yield return Shot("lobby_quit");
            Log("after clicking Wyjdz z gry");
            Click("No");
            yield return new WaitForSeconds(0.5f);
            Log("after clicking Nie");
            Click("Leave");
            yield return Shot("lobby_leave_host");
            Log("after clicking Zamknij serwer (host=" + NetworkManager.Singleton.IsServer + ")");
            Click("No");
            yield return new WaitForSeconds(0.5f);
            Log("end");
        }

        private static void Click(string name)
        {
            // the newest button with that name (the dialog is created after the menu)
            Button found = null;
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude))
                if (b.name == name) found = b;
            if (found != null) found.onClick.Invoke();
            else Debug.Log("[QuitConfirm] button not found: " + name);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(0.8f);
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
            yield return new WaitForSeconds(0.3f);
        }

        private static void Log(string when) =>
            Debug.Log($"[QuitConfirm] {when}: dialog open={ConfirmDialog.IsOpen}, esc menu open={GameObject.Find("EscMenu") != null}, app running");
    }
}
