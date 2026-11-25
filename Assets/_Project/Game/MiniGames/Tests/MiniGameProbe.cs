using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Board-game tests in the exe (opt-in). Run on the host AND on a client:
    /// <para>-testchess [-chessshots &lt;dir&gt;]: the host sits on seat A, the client on seat B; they play a scripted
    /// scholar's mate through the real click path (virtual mouse rays from the board camera), agree to a rematch
    /// (colours swap), then a game with en passant, castling on both sides and a promotion to a knight chosen in the
    /// promotion box, and the white player resigns with Esc's action. Logs "[ChessTest]".</para>
    /// <para>-testttt [-tttshots &lt;dir&gt;]: X wins with the diagonal, R for the next round (O starts), a draw, then
    /// the host gets up. Logs "[TttTest]".</para>
    /// <para>-testpool &lt;dir&gt;: 8-ball at the lobby pool table (MiniGameProbe.Pool.cs). Logs "[PoolTest]".</para>
    /// <para>-testcheckers &lt;dir&gt;: checkers and the lamp at the chess table (MiniGameProbe.Checkers.cs). Logs "[CheckersTest]".</para>
    /// <para>-chesswatch &lt;dir&gt;: a late joining spectator logs the board it got and photographs both tables.</para>
    /// Screens: fp_* = the player's real screen, side_* = a free camera next to the table (what others see).
    /// </summary>
    public sealed partial class MiniGameProbe : MonoBehaviour
    {
        string _dir;
        bool _host;
        string _tag = "ChessTest";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Has("-testchess") && !Has("-testttt") && !Has("-chesswatch") && !Has("-testpool") && !Has("-testcheckers")) return;
            var g = new GameObject("MiniGameProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<MiniGameProbe>();
        }

        static bool Has(string a) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), a) >= 0;

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        void Log(string s) => Debug.Log($"[{_tag}] {(_host ? "host" : "client")}: {s}");

        IEnumerator Start()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || MiniGameTables.Chess == null || MiniGameTables.Ttt == null)
            {
                mm = MatchManager.Instance;
                yield return null;
            }
            _host = mm.IsServer;
            yield return new WaitForSeconds(1.5f);
            if (Has("-chesswatch"))
            {
                _dir = Arg("-chesswatch");
                yield return Watch();
                yield break;
            }
            for (float w = 0f; w < 40f && NetPlayer.All.Count < 2; w += Time.deltaTime) yield return null;
            Log($"players={NetPlayer.All.Count}");
            string video = Arg("-mgvideo");
            if (video != null) StartCoroutine(Record(video));
            if (Has("-testchess"))
            {
                _dir = Arg("-chessshots");
                yield return ChessRun(mm);
            }
            if (Has("-testcheckers"))
            {
                _dir = Arg("-testcheckers");
                yield return CheckersRun(mm);
            }
            if (Has("-testttt"))
            {
                _tag = "TttTest";
                _dir = Arg("-tttshots");
                yield return TttRun(mm);
            }
            if (Has("-testpool"))
            {
                _tag = "PoolTest";
                _dir = Arg("-testpool");
                yield return PoolRun();
            }
            _recording = false;
            Log("DONE");
        }

        bool _recording;

        /// <summary>-mgvideo &lt;dir&gt;: the player's screen as numbered frames at 15 fps during the first game (ffmpeg makes the mp4).</summary>
        IEnumerator Record(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            _recording = true;
            int n = 0;
            float next = 0f;
            while (_recording && n < 4000)
            {
                if (Time.unscaledTime >= next)
                {
                    next = Time.unscaledTime + 1f / 15f;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"f_{n++:00000}.png"));
                }
                yield return null;
            }
        }

        // ================================================================ helpers

        void Shot(string name)
        {
            if (_dir == null) return;
            System.IO.Directory.CreateDirectory(_dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, "fp_" + name + ".png"));
        }

        /// <summary>
        /// Spectator video (watcher with -mgvideo): numbered frames of the chess table from the side until the game
        /// ends, then of the tic-tac-toe table until its second round ends (12 fps).
        /// </summary>
        IEnumerator WatchVideo(string dir, ChessTable chess, TttTable ttt)
        {
            string keep = _dir;
            _dir = dir;
            int n = 0;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 70f && !(chess.State.Phase == ChessState.Over && !chess.Animating && chess.State.Game >= 2)
                   && !(Time.unscaledTime - t0 > 4f && chess.State.Game == 0))
            {
                Render($"w_{n++:00000}", chess.transform.TransformPoint(new Vector3(2.0f, 2.05f, -1.15f)), chess.transform.TransformPoint(new Vector3(0f, 0.85f, 0f)), 46f);
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            for (float w = 0f; w < 20f && ttt.State.Phase != TttState.Playing; w += Time.unscaledDeltaTime) yield return null;
            t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 70f && !(ttt.State.Phase == TttState.Over && !ttt.Animating && ttt.State.Round >= 2))
            {
                Render($"w_{n++:00000}", ttt.transform.TransformPoint(new Vector3(-1.05f, 0.3f, 2.7f)), ttt.transform.TransformPoint(new Vector3(0.1f, -0.35f, 0.3f)), 52f);
                yield return new WaitForSecondsRealtime(1f / 12f);
            }
            Log($"watcher video frames={n}");
            _dir = keep;
        }

        /// <summary>Free camera render (what other players see) into side_NAME.png.</summary>
        /// <summary>Frame of the last free-camera render (it shows the shadow-only bodies again for one frame).</summary>
        int _renderFrame = -10;

        void Render(string name, Vector3 from, Vector3 at, float fov = 45f)
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
            // Bodies hidden from the own board camera (shadow only) are drawn for this outside view.
            foreach (var p in NetPlayer.All) if (p != null && !p.IsOwner) p.SetGhostBody(false);
            _renderFrame = Time.frameCount;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(_dir, "side_" + name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(cam.gameObject);
        }

        /// <summary>Side view of a table from a corner (local offset from the table).</summary>
        void RenderTable(MiniTable t, string name, Vector3 localFrom, float fov = 50f)
        {
            Render(name, t.transform.TransformPoint(localFrom), t.Centre + Vector3.down * 0.15f, fov);
        }

        /// <summary>Walks (teleports) next to a seat and presses E there (the real request).</summary>
        IEnumerator SitDown(MiniTable t, int seat)
        {
            var me = NetPlayer.Local;
            Vector3 stand = t.LeavePoint(seat);
            me.TeleportLocal(stand, t.SeatYaw(seat));
            me.Motor.SetLook(t.SeatYaw(seat), 25f);
            for (int attempt = 0; attempt < 4 && t.SeatOf(me.NetworkObjectId) < 0; attempt++)
            {
                yield return new WaitForSeconds(0.8f);
                Log($"sit request {attempt + 1}: pos={me.transform.position} stand={t.SeatStand(seat)}");
                me.MiniSitRpc(t.Game, (byte)seat);
                for (float w = 0f; w < 2.5f && t.SeatOf(me.NetworkObjectId) < 0; w += Time.deltaTime) yield return null;
            }
            Log($"sat at {t.Title} seat {seat}: {(t.SeatOf(me.NetworkObjectId) == seat ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(1.0f); // camera glides to the board
        }

        /// <summary>Ray from the board camera to a world point (a virtual mouse click there).</summary>
        static Ray RayTo(MiniTable t, int seat, Vector3 world)
        {
            t.CameraPose(seat, 1f, out var pos, out _);
            return new Ray(pos, (world - pos).normalized);
        }

        IEnumerator Watch()
        {
            _tag = "ChessTest";
            var chess = MiniGameTables.Chess;
            var ttt = MiniGameTables.Ttt;
            yield return new WaitForSeconds(2f);
            Log($"watcher joined: chess phase={chess.State.Phase} ply={chess.State.Ply} fen={chess.Target.ToFen()} shown={chess.Shown.ToFen()}");
            Log($"watcher: ttt phase={ttt.State.Phase} cells={ttt.State.Cells} round={ttt.State.Round}");
            string video = Arg("-mgvideo");
            if (video != null) yield return WatchVideo(video, chess, ttt);
            RenderTable(chess, "watch_chess", new Vector3(1.4f, 1.45f, -1.2f));
            RenderTable(chess, "watch_chess_top", new Vector3(0.05f, 1.6f, -0.35f), 55f);
            RenderTable(ttt, "watch_ttt", new Vector3(-1.0f, 0.15f, 2.6f));
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(2.5f);
                RenderTable(chess, "watch_chess_" + i, new Vector3(1.4f, 1.45f, -1.2f));
                Log($"watcher: ply={chess.State.Ply} fen={chess.Shown.ToFen()}");
            }
            Log("DONE");
        }
    }
}
