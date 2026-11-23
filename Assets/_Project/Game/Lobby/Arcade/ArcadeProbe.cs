using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// SNAKE arcade tests (opt-in, lobby).
    /// <para>-testsnake (host): the host walks to the cabinet, starts SNAKE and plays with an autopilot (shortest path,
    /// double-turn buffer check, bonus, levels), crashes on purpose, restarts and leaves; logs "[SnakeTest]".
    /// -snakeshots &lt;dir&gt; also saves the player's real screen (fp_*.png) and cabinet renders from the side (cab_*.png).
    /// -snakebest N seeds the in-memory record (tests never touch the saved one).</para>
    /// <para>-snakeblock &lt;dir&gt; (client): tries to walk in front of the playing host from behind and both sides, is
    /// put inside the zone, then stands in the host's view on purpose (host checks it is hidden), and photographs
    /// the game from the side (what other players see).</para>
    /// </summary>
    public sealed partial class ArcadeProbe : MonoBehaviour
    {
        private string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(a, "-testsnake") < 0 && System.Array.IndexOf(a, "-snakeblock") < 0) return;
            var g = new GameObject("ArcadeProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<ArcadeProbe>();
        }

        private static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        private IEnumerator Start()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || LobbyArcade.Instance == null)
            {
                mm = MatchManager.Instance;
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            _dir = Arg("-snakeshots") ?? Arg("-snakeblock");
            if (_dir != null) System.IO.Directory.CreateDirectory(_dir);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-snakeblock") >= 0) yield return Block(mm);
            else yield return Host(mm);
        }

        private void Log(string s) => Debug.Log("[SnakeTest] " + s);

        private void Shot(string name)
        {
            if (_dir != null) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, name + ".png"));
        }

        /// <summary>Renders the lobby from a free camera (other players' point of view) into cab_NAME.png.</summary>
        private void Render(string name, Vector3 from, Vector3 at, float fov = 45f)
        {
            if (_dir == null) return;
            var cam = new GameObject("ProbeCam").AddComponent<Camera>();
            cam.enabled = false;
            var rt = new RenderTexture(960, 720, 24) { antiAliasing = 2 };
            cam.targetTexture = rt;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9); // no first-person view-model arms (they float at the player's camera)
            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(_dir, "cab_" + name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(cam.gameObject);
        }

        // ================================================================ host: plays

        private IEnumerator Host(MatchManager mm)
        {
            var me = NetPlayer.Local;
            for (float w = 0f; w < 25f && NetPlayer.All.Count < 2; w += Time.deltaTime) yield return null;
            NetPlayer other = null;
            foreach (var p in NetPlayer.All) if (p != me) other = p;
            Log($"players={NetPlayer.All.Count} best={ArcadeState.Best}");
            Vector3 scr = MatchManager.ArcadeScreen;

            // Attract mode + the cabinet from around the room.
            me.Motor.Teleport(MatchManager.ArcadeStand + Vector3.back * 1.3f, 0f);
            me.Motor.SetLook(0f, -14f);
            yield return new WaitForSeconds(1.2f);
            Shot("fp_0_attract");
            Render("front", new Vector3(6.0f, 1.55f, 3.6f), scr + Vector3.down * 0.25f);
            Render("left34", new Vector3(4.9f, 1.5f, 3.9f), scr + Vector3.down * 0.2f);
            Render("right34", new Vector3(7.1f, 1.5f, 3.9f), scr + Vector3.down * 0.2f);
            Render("close", new Vector3(6.0f, 1.5f, 4.55f), scr, 50f);
            Render("graze_left", new Vector3(5.15f, 1.3f, 4.75f), scr, 40f);
            Render("low", new Vector3(6.2f, 0.9f, 3.8f), scr, 45f);
            Render("detail_panel", new Vector3(6.05f, 1.42f, 4.62f), new Vector3(5.98f, 1.03f, 5.06f), 42f);
            Render("detail_stick", new Vector3(5.62f, 1.26f, 4.8f), MatchManager.ArcadeStickBall + Vector3.down * 0.05f, 30f);
            Render("detail_coin", new Vector3(6.25f, 0.75f, 4.35f), new Vector3(6.0f, 0.5f, 5.2f), 45f);
            Render("detail_side", new Vector3(4.75f, 1.3f, 4.1f), new Vector3(5.62f, 1.0f, 5.5f), 55f);
            Render("detail_marquee", new Vector3(6.0f, 1.62f, 4.6f), new Vector3(6.0f, 1.76f, 5.16f), 45f);

            mm.ServerArcade(me, true);
            yield return new WaitForSeconds(1.2f);
            Log($"arcade player={mm.ArcadePlayer.Value} me={me.NetworkObjectId} state={ArcadeState.Cab[0].State} pitch={me.Motor.LookPitch:0.0}");
            Shot("fp_1_ready");
            Render("ready_side", new Vector3(5.0f, 1.6f, 3.7f), scr + Vector3.down * 0.3f, 50f);
            yield return new WaitForSeconds(0.4f);

            mm.ServerArcadeDir(me, 1);
            yield return Play(mm, me, other, 42f);

            // Crash on purpose for the GAME OVER screen.
            Log($"crash on purpose score={ArcadeState.Cab[0].Score} level={ArcadeState.Cab[0].Level}");
            for (float w = 0f; w < 12f && ArcadeState.Cab[0].State == ArcadeState.Playing; w += Time.deltaTime)
            {
                var b = ArcadeState.Cab[0].Body;
                if (b.Count > 1)
                {
                    var head = b[0];
                    // turn towards the nearest wall and keep going
                    int d = head.y < SnakeRules.H / 2 ? 2 : 0;
                    mm.ServerArcadeDir(me, (byte)d);
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.35f);
            Shot("fp_7_over");
            Log($"over state={ArcadeState.Cab[0].State} score={ArcadeState.Cab[0].Score} best={ArcadeState.Best} newRecord={ArcadeState.Cab[0].NewRecord}");
            yield return new WaitForSeconds(1.4f);
            Shot("fp_8_over_late");
            Render("over_side", new Vector3(7.0f, 1.6f, 3.8f), scr + Vector3.down * 0.3f, 50f);
            mm.ServerArcadeDir(me, 0);
            yield return new WaitForSeconds(0.6f);
            Log($"restart state={ArcadeState.Cab[0].State} len={ArcadeState.Cab[0].Body.Count}");
            Shot("fp_9_restart");
            yield return new WaitForSeconds(0.5f);
            mm.ServerArcade(me, false);
            yield return new WaitForSeconds(1.0f);
            Log($"stopped player={mm.ArcadePlayer.Value} live={ArcadeState.Cab[0].Live} blocker={(ArcadeKeepOut.Instance != null && ArcadeKeepOut.Instance.BlockerOn)}");
            yield return new WaitForSeconds(4f);
            Log("done");
            Application.Quit();
        }
    }
}
