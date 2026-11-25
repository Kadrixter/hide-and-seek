using System;
using HideAndSeek.Game.MiniGames;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Rules tests of checkers at the chess table (batchmode: -executeMethod HideAndSeek.Game.Editor.CheckersRulesTests.RunFromCommandLine):
    /// the fixed positions of <see cref="CheckersRulesCheck"/> (maximum capture, a man capturing backwards, flying
    /// kings, promotion only at the end of a move, endings) plus the scripted game of the two-instance test
    /// (<see cref="CheckersScript"/>). Logs "[CheckersTest]"; exit code 0 = all passed.
    /// </summary>
    public static class CheckersRulesTests
    {
        [MenuItem("HideAndSeek/Mini games/Run checkers rules tests")]
        public static void RunMenu() => Run();

        public static void RunFromCommandLine()
        {
            bool ok = false;
            try { ok = Run(); }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run()
        {
            int pass = 0;
            int fail = CheckersRulesCheck.Run((name, ok, detail) =>
            {
                if (ok) pass++;
                Debug.Log($"[CheckersTest] {(ok ? "OK  " : "FAIL")} {name} {detail}");
            });
            var script = CheckersScript.Find();
            bool scriptOk = script != null;
            if (scriptOk) pass++; else fail++;
            Debug.Log($"[CheckersTest] {(scriptOk ? "OK  " : "FAIL")} test game script: {(script != null ? script.ToString() : "none found")}");
            Debug.Log($"[CheckersTest] SUMMARY passed={pass} failed={fail}");
            return fail == 0;
        }
    }
}
