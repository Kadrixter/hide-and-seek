using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// KABELKI cabinet test in the exe (opt-in): -testarclink &lt;dir&gt; on the host and on one client, both in the lobby.
    /// Host: steps up (a new puzzle is dealt), draws half the lines with the virtual mouse through the real input path,
    /// leaves with Esc (the board stays), steps up again (must get a DIFFERENT puzzle), solves it (time and record set,
    /// let go after the flash). Client: watches the cabinet from beside it and logs what its own screen shows (pairs,
    /// solved, the user) - the progress must arrive live. Logs "[ArcLinkTest]" (OK / FAIL). Screens: fp_* = own screen,
    /// side_* = a free camera at the cabinets.
    /// </summary>
    public sealed class ArcLinkProbe : MonoBehaviour
    {
        string _dir, _role = "?";
        int _fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testarclink") < 0) return;
            var g = new GameObject("ArcLinkProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<ArcLinkProbe>();
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : null;
        }

        void Log(string s) => Debug.Log($"[ArcLinkTest] {_role}: {s}");

        void Check(string what, bool ok, string detail = "")
        {
            if (!ok) _fails++;
            Log($"{(ok ? "OK  " : "FAIL")} {what} {detail}");
        }

        IEnumerator Start()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || DotLinkArcade.Instance == null || NetPlayer.All.Count < 2)
            {
                mm = MatchManager.Instance;
                yield return null;
            }
            yield return new WaitForSeconds(2f);
            _dir = Arg("-testarclink");
            _role = mm.IsServer ? "host" : "client";
            var cab = DotLinkArcade.Instance;
            Log($"cabinet at {cab.transform.position}, screen {(cab.ScreenQuad != null ? cab.ScreenQuad.position.ToString() : "none")}, demo {cab.ShowingDemo}");
            if (mm.IsServer) yield return Host(mm, cab);
            else yield return Client(mm, cab);
            DotLinkLocal.TestActive = false;
            Log($"DONE fails={_fails}");
        }

        IEnumerator Host(MatchManager mm, DotLinkArcade cab)
        {
            var me = NetPlayer.Local;
            Vector3 o = MatchManager.ArcLinkOrigin;
            Render("00_cabinets", o + new Vector3(1.0f, 1.7f, -3.0f), o + new Vector3(1.0f, 1.1f, 0f), 50f);
            yield return new WaitForSeconds(4f); // the demo runs for a while
            Render("01_demo", o + new Vector3(0f, 1.45f, -1.2f), cab.ScreenQuad.position, 40f);
            Check("demo on the screen while nobody plays", cab.ShowingDemo);

            // Round 1: half the lines, Esc.
            me.TeleportLocal(MatchManager.ArcLinkStand, MatchManager.ArcLinkYaw);
            yield return new WaitForSeconds(0.5f);
            me.ArcLinkUseRpc(true);
            for (float w = 0f; w < 10f && (!me.AtArcLink || cab.NetBoard == null); w += Time.deltaTime) yield return null;
            Check("at the cabinet with a puzzle", me.AtArcLink && cab.NetBoard != null && DotLinkLocal.Target == (IDotLinkTarget)cab, Line(cab));
            string first = mm.ArcLinkPuzzleNet.Value.ToString();
            yield return new WaitForSeconds(1.2f); // camera glides in
            var sol = Solve(cab.Puzzle);
            Check("puzzle solvable", sol != null);
            if (sol == null) yield break;
            for (int c = 0; c < sol.Length / 2; c++) yield return DrawLine(cab, sol[c]);
            yield return new WaitForSeconds(0.6f);
            Check("half drawn on the server", cab.NetBoard.CompleteCount >= sol.Length / 2, Line(cab));
            Shot("02_half");
            Log($"status: {DotLinkHud.LastStatus}");
            yield return new WaitForSeconds(3f); // the client looks
            DotLinkLocal.TestEsc = true;
            yield return new WaitForSeconds(1f);
            Check("Esc left, board kept", !me.AtArcLink && cab.NetBoard != null && cab.NetBoard.CompleteCount >= sol.Length / 2, Line(cab));

            // Round 2: a new puzzle, solved.
            me.TeleportLocal(MatchManager.ArcLinkStand, MatchManager.ArcLinkYaw);
            yield return new WaitForSeconds(0.5f);
            me.ArcLinkUseRpc(true);
            for (float w = 0f; w < 10f && (!me.AtArcLink || cab.NetBoard == null || mm.ArcLinkPuzzleNet.Value.ToString() == first); w += Time.deltaTime) yield return null;
            yield return null; // the cabinet decodes the new puzzle in its own Update
            yield return null;
            Check("new random puzzle on the second go", me.AtArcLink && cab.NetBoard != null && mm.ArcLinkPuzzleNet.Value.ToString() != first && cab.NetBoard.CompleteCount == 0, Line(cab));
            yield return new WaitForSeconds(1.2f);
            sol = Solve(cab.Puzzle);
            if (sol == null) { Check("puzzle 2 solvable", false); yield break; }
            for (int c = 0; c < sol.Length; c++) yield return DrawLine(cab, sol[c]);
            yield return new WaitForSeconds(0.5f);
            Check("solved, time and record set", cab.NetBoard.SolvedFlag && mm.ArcLinkSolvedTime.Value > 0f && mm.ArcLinkBest.Value > 0f,
                $"time {mm.ArcLinkSolvedTime.Value:0.0} best {mm.ArcLinkBest.Value:0.0} ({mm.ArcLinkBestNick.Value}) flash '{cab.SolvedFlash}'");
            Shot("03_solved");
            Log($"status: {DotLinkHud.LastStatus}");
            yield return new WaitForSeconds(3.5f);
            Check("let go after the flash", !me.AtArcLink && !DotLinkLocal.Active);
            Render("04_after", o + new Vector3(0f, 1.45f, -1.2f), cab.ScreenQuad.position, 40f);
            yield return new WaitForSeconds(4f);
        }

        IEnumerator Client(MatchManager mm, DotLinkArcade cab)
        {
            var me = NetPlayer.Local;
            Vector3 o = MatchManager.ArcLinkOrigin;
            // Beside the cabinet, looking at its screen (a spectator).
            Vector3 pos = o + new Vector3(-1.1f, 0f, -1.3f);
            Vector3 to = cab.ScreenQuad.position - (pos + Vector3.up * 1.62f);
            me.TeleportLocal(pos, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(1.2f);
            Shot("c0_prompt"); // the lobby prompt over the cabinet (plain text, the dead lobby's look)
            Log($"prompt: {LocalPlayerController.Instance?.Prompt} '{LocalPlayerController.Instance?.PromptText}'");
            int maxPairs = 0, puzzles = 0, shots = 0;
            string lastPuzzle = "";
            bool sawSolved = false, sawUser = false;
            for (float t = 0f; t < 70f; t += 0.25f)
            {
                string p = mm.ArcLinkPuzzleNet.Value.ToString();
                if (p.Length > 0 && p != lastPuzzle) { lastPuzzle = p; puzzles++; maxPairs = 0; }
                var b = cab.NetBoard;
                var u = mm.ArcLinkUserPlayer;
                if (u != null) sawUser = true;
                if (b != null && b.CompleteCount > maxPairs)
                {
                    maxPairs = b.CompleteCount;
                    Log($"watch: puzzle #{puzzles} pairs {b.CompleteCount}/{b.Puzzle.Colors} user {(u != null ? u.Nick : "-")}");
                    if (shots < 2 && maxPairs >= b.Puzzle.Colors / 2) { shots++; Render($"c{shots}_watch", o + new Vector3(0f, 1.45f, -1.2f), cab.ScreenQuad.position, 40f); }
                }
                if (b != null && b.SolvedFlag && !sawSolved)
                {
                    sawSolved = true;
                    Log($"watch: solved, flash '{cab.SolvedFlash}'");
                    Render("c3_solved", o + new Vector3(0f, 1.45f, -1.2f), cab.ScreenQuad.position, 40f);
                }
                yield return new WaitForSeconds(0.25f);
            }
            Check("client saw the player at the cabinet", sawUser);
            Check("client saw two different puzzles", puzzles >= 2, $"puzzles {puzzles}");
            Check("client saw the solve", sawSolved);
        }

        static string Line(DotLinkArcade cab) =>
            cab.NetBoard == null ? "(no board)" : $"pairs {cab.NetBoard.CompleteCount}/{cab.NetBoard.Puzzle.Colors}, filled {cab.NetBoard.FilledCount}/{cab.NetBoard.Puzzle.Cells}";

        static int[][] Solve(DotLinkPuzzle p)
        {
            if (p == null) return null;
            var s = new DotLinkSolver(p);
            s.Count(1, 5_000_000);
            return s.First;
        }

        IEnumerator DrawLine(DotLinkArcade cab, int[] cells)
        {
            DotLinkLocal.TestActive = true;
            DotLinkLocal.TestLmb = false;
            Aim(cab, cells[0]);
            yield return null;
            DotLinkLocal.TestLmb = true;
            yield return null;
            for (int i = 1; i < cells.Length; i++)
            {
                Aim(cab, cells[i]);
                yield return null;
                yield return null;
            }
            DotLinkLocal.TestLmb = false;
            yield return null;
            yield return new WaitForSeconds(0.15f);
        }

        static void Aim(DotLinkArcade cab, int cell)
        {
            if (DotLinkCamera.RayTo(cab.CellWorld(cell), out var ray)) DotLinkLocal.TestRay = ray;
        }

        void Shot(string name)
        {
            if (_dir == null) return;
            System.IO.Directory.CreateDirectory(_dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, $"fp_{_role}_{name}.png"));
        }

        void Render(string name, Vector3 from, Vector3 at, float fov)
        {
            if (_dir == null) return;
            System.IO.Directory.CreateDirectory(_dir);
            var cam = new GameObject("ProbeCam").AddComponent<Camera>();
            cam.enabled = false;
            var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9);
            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(_dir, $"side_{_role}_{name}.png"), tex.EncodeToPNG());
            Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(cam.gameObject);
        }
    }
}
