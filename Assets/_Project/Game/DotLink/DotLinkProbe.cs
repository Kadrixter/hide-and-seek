using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Dot link test in the exe (opt-in). -testdotlink &lt;dir&gt; on 3 copies: host -forcehider -autostart
    /// -autostartplayers 3 -hidetime N (hider 1), two clients (one becomes hider 2, one the seeker). Timeline from the
    /// start of seeking: the dark box before the stage, the stage forced to DotLink, the lit panel from afar and near,
    /// hider 1 takes the panel and draws 40% of the lines through the real input path (virtual mouse rays from the
    /// close-up camera: press on a dot, cell by cell, release; RMB wipes one line, it is drawn again), leaves with Esc;
    /// hider 2 sees the progress, takes the panel, hider 1 is refused ("Panel zajęty"), hider 2 draws more, the seeker
    /// knifes him at the panel (and he is killed if still alive); the panel is free, the lines kept; hider 1 finishes
    /// -> stage GateOpening. Logs "[DotLinkTest]" (OK / FAIL lines). Screens: fp_* = the player's screen, side_* = a
    /// free camera. (Joining in the middle of a round is refused by the game; a reconnect gets the network variables.)
    /// </summary>
    public sealed partial class DotLinkProbe : MonoBehaviour
    {
        string _dir, _role = "?";
        double _t0;
        int _fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Has("-testdotlink")) return;
            var g = new GameObject("DotLinkProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<DotLinkProbe>();
        }

        static bool Has(string a) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), a) >= 0;

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : null;
        }

        void Log(string s) => Debug.Log($"[DotLinkTest] {_role}: {s}");

        void Check(string what, bool ok, string detail = "")
        {
            if (!ok) _fails++;
            Log($"{(ok ? "OK  " : "FAIL")} {what} {detail}");
        }

        IEnumerator At(float sec)
        {
            while (MatchManager.Instance != null && MatchManager.Instance.Now < _t0 + sec) yield return null;
        }

        IEnumerator Start()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null || DotLinkPanel.Instance == null)
            {
                mm = MatchManager.Instance;
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            _t0 = mm.Now;
            _dir = Arg("-testdotlink");
            var me = NetPlayer.Local;
            _role = me.IsSeeker ? "seeker" : mm.IsServer ? "hider1" : "hider2";
            Log($"players={NetPlayer.All.Count} stage={mm.Escape.Value} puzzle='{mm.DotLinkPuzzleNet.Value}'");
            yield return Run(mm, me);
            DotLinkLocal.TestActive = false;
            Log($"DONE fails={_fails}");
        }

        // ================================================================ helpers

        static NetPlayer Find(System.Func<NetPlayer, bool> f)
        {
            foreach (var p in NetPlayer.All) if (p != null && f(p)) return p;
            return null;
        }

        static int[][] Solution(DotLinkPuzzle p)
        {
            var s = new DotLinkSolver(p);
            s.Count(1, 5_000_000);
            return s.First;
        }

        void Shot(string name)
        {
            if (_dir == null) return;
            System.IO.Directory.CreateDirectory(_dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, "fp_" + name + ".png"));
        }

        /// <summary>Free camera render (what others see) into side_NAME.png, with an optional fill light.</summary>
        void Render(string name, Vector3 from, Vector3 at, float fov = 45f, float fill = 0f)
        {
            if (_dir == null) return;
            System.IO.Directory.CreateDirectory(_dir);
            Light l = null;
            if (fill > 0f)
            {
                l = new GameObject("ProbeFill").AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 6f;
                l.intensity = fill;
                l.transform.position = from + Vector3.up * 0.3f;
            }
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
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(_dir, "side_" + name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(cam.gameObject);
            if (l != null) Destroy(l.gameObject);
        }

        /// <summary>Puts the local player <paramref name="back"/> m in front of the box (and <paramref name="side"/> m aside), looking at the screen.</summary>
        static void StandAtBox(NetPlayer me, float back, float side)
        {
            if (!DotLinkLayout.TryGet(out var lay)) return;
            Vector3 flat = new Vector3(lay.Normal.x, 0f, lay.Normal.z).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            Vector3 pos = lay.Stand + flat * (back - 0.62f) + right * side;
            Vector3 to = lay.Centre - (pos + Vector3.up * 1.62f);
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            me.TeleportLocal(pos, yaw);
            me.Motor.SetLook(yaw, Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg);
        }

        void Aim(int cell)
        {
            var panel = DotLinkPanel.Instance;
            if (panel != null && DotLinkCamera.RayTo(panel.CellWorld(cell), out var ray)) DotLinkLocal.TestRay = ray;
        }

        /// <summary>One line drawn with the virtual mouse: press on its first cell, then cell by cell, release.</summary>
        IEnumerator DrawLine(int[] cells)
        {
            DotLinkLocal.TestActive = true;
            DotLinkLocal.TestLmb = false;
            Aim(cells[0]);
            yield return null;
            DotLinkLocal.TestLmb = true;
            yield return null;
            for (int i = 1; i < cells.Length; i++)
            {
                Aim(cells[i]);
                yield return null;
                yield return null;
            }
            DotLinkLocal.TestLmb = false;
            yield return null;
            yield return new WaitForSeconds(0.15f);
        }

        /// <summary>Right click on a cell.</summary>
        IEnumerator RightClick(int cell)
        {
            DotLinkLocal.TestActive = true;
            Aim(cell);
            yield return null;
            DotLinkLocal.TestRmb = true;
            yield return null;
            yield return new WaitForSeconds(0.4f);
        }
    }
}
