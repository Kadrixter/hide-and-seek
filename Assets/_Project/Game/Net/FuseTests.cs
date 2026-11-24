using System;
using System.Collections;
using System.IO;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests of the fuses / fuse boxes and of the items the dead drop (own component, started from the command line):
    ///  -testfuses &lt;dir&gt; (host -forcehider -autostart -hidetime N, + a seeker client with -fuseswatch): logs the boxes and
    ///      the 8 fuses (floor, free, inside the garage), close-ups of every box, a fuse on the floor and in the hand, the
    ///      HUD; fills a wrong box (the level's lights on), pulls the fuses out again one by one (a let-go first; lights off
    ///      when empty), has the seeker pull one out (falls on the floor), drops two (Q), fills the right box (locked,
    ///      stage DotLink, the seeker cannot pull out of it), then dies with a full inventory; logs [FuseTest].
    ///  -fuseswatch &lt;dir&gt; (the seeker instance): holds E for the scripted pulls, films the hider at the box from the side
    ///      and the items round the body; logs what it sees.
    /// </summary>
    public partial class FuseTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;
        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length && !Args[i + 1].StartsWith("-") ? Args[i + 1] : def;
        }

        private static string _dir;
        private static int _fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Has("-testfuses") && !Has("-fuseswatch")) return;
            var go = new GameObject("[FuseTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<FuseTests>();
            if (Has("-testfuses")) { _dir = ArgOr("-testfuses", ""); t.StartCoroutine(t.HostTest()); }
            if (Has("-fuseswatch")) { _dir = ArgOr("-fuseswatch", ""); t.StartCoroutine(t.Watch()); }
        }

        private static void Log(string s) => Debug.Log("[FuseTest] " + s);
        private static void Fail(string s) { _fails++; Debug.LogWarning("[FuseTest] FAIL: " + s); }

        private static IEnumerator WaitSeeking()
        {
            while (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(1.5f);
        }

        private static void Screen(string name)
        {
            if (string.IsNullOrEmpty(_dir)) return;
            Directory.CreateDirectory(_dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
        }

        private static GameObject TestLight(Vector3 pos, float range, float intensity)
        {
            var l = new GameObject("FuseTestLight").AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.transform.position = pos;
            LevelLighting.Instance?.Track(l);
            return l.gameObject;
        }

        /// <summary>Renders <paramref name="count"/> frames of a side camera (focus + offset recomputed every frame).</summary>
        private IEnumerator Side(string prefix, Func<Vector3> focus, Func<Vector3> offset, int count, float every, float fov = 45f)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            var camGo = new GameObject("FuseSideCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.cullingMask = ~(1 << 9);
            var rt = new RenderTexture(960, 540, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(every);
                yield return new WaitForEndOfFrame();
                Vector3 f = focus();
                camGo.transform.position = f + offset();
                camGo.transform.LookAt(f);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(_dir, $"{prefix}_{i:00}.png"), tex.EncodeToPNG());
            }
            Destroy(camGo);
            rt.Release();
        }

        private static string PromptNow()
        {
            var lpc = LocalPlayerController.Instance;
            return lpc != null ? $"{lpc.Prompt} '{lpc.PromptText}'" : "-";
        }

        private static string Boxes(MatchManager mm)
        {
            var s = "";
            for (int i = 0; i < FuseTuning.Boxes; i++)
            {
                var b = mm.FuseBoxes.Value[i];
                if (!b.Placed) continue;
                s += $" [{i}{(i == mm.FuseGoodBox ? "*" : "")} L{b.Level} {b.FilledCount()}/4{(b.Locked ? " LOCKED" : "")}{(b.Lit ? " LIT" : "")}]";
            }
            return s;
        }

        /// <summary>Stand in front of box <paramref name="box"/> (offset along it) and look at socket <paramref name="socket"/>.</summary>
        private static IEnumerator StandAt(NetPlayer me, int box, int socket, float side = 0f)
        {
            var b = MatchManager.Instance.FuseBoxes.Value[box];
            Vector3 stand = b.World(FuseBoxModel.StandLocal + new Vector3(side, 0f, 0.05f));
            me.Motor.Teleport(stand + Vector3.up * 0.05f, Quaternion.LookRotation(-b.Normal).eulerAngles.y, -20f);
            yield return new WaitForSeconds(0.5f);
            LookAt(me, b.World(FuseBoxModel.FuseLocal(socket, 0.1f)));
            yield return new WaitForSeconds(0.4f);
        }

        private static void LookAt(NetPlayer me, Vector3 p)
        {
            Vector3 d = p - me.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            me.Motor.SetLook(yaw, pitch);
        }

        /// <summary>Walks to the dropped fuse of mark <paramref name="s"/> and takes it (E path on the server).</summary>
        private static IEnumerator TakeFuse(NetPlayer me, int s, string shot = null)
        {
            var mm = MatchManager.Instance;
            for (int i = 0; i < mm.Drops.Count; i++)
            {
                var d = mm.Drops[i];
                if (d.Kind != FuseItems.Of(s)) continue;
                Vector3 p = mm.DropPosition(d);
                Vector3 from = p + new Vector3(0.9f, 0f, 0.3f);
                from.y = MapInfo.FloorUnder(p.y) + 0.05f;
                me.Motor.Teleport(from, 0f, 0f);
                yield return null;
                LookAt(me, p);
                yield return new WaitForSeconds(0.6f);
                if (shot != null) Screen(shot);
                yield return new WaitForSeconds(0.15f);
                Log($"at fuse {FuseSymbols.Name(s)} #{d.Id}: prompt {PromptNow()}");
                mm.ServerPickupDrop(me, d.Id);
                yield return new WaitForSeconds(0.4f);
                if (!me.HasFuse(s)) Fail($"fuse {FuseSymbols.Name(s)} not taken");
                yield break;
            }
            Fail($"fuse {FuseSymbols.Name(s)} not found on the floor");
        }

        /// <summary>Puts every fuse it carries of box <paramref name="box"/>'s marks in (E per fuse).</summary>
        private static IEnumerator FillBox(NetPlayer me, int box, string shotPrefix)
        {
            var mm = MatchManager.Instance;
            for (int k = 0; k < FuseTuning.Sockets; k++)
            {
                var b = mm.FuseBoxes.Value[box];
                int socket = -1;
                for (int s = 0; s < FuseTuning.Sockets; s++) if (!b.Has(s) && me.HasFuse(b.Symbol(s))) { socket = s; break; }
                if (socket < 0) yield break;
                yield return StandAt(me, box, socket, FuseBoxModel.SocketX(socket) * 0.6f);
                Log($"box {box} socket {socket} ({FuseSymbols.Name(b.Symbol(socket))}): prompt {PromptNow()}");
                LocalPlayerController.TestPressE = true;
                float t0 = Time.time;
                if (k == 0 && shotPrefix != null)
                    foreach (float at in new[] { 0.25f, 0.55f, 0.8f, 1.1f })
                    {
                        while (Time.time - t0 < at) yield return null;
                        Screen($"{shotPrefix}_insert_fp_{at:0.00}");
                        var drv = me.GetComponent<FuseWorkDriver>();
                        var bca = me.Actions as BlockyCharacterActions;
                        if (drv != null && drv.MovingFuse != null && bca != null)
                            Log($"insert t={drv.WorkTime:0.00}: right fist {Vector3.Distance(bca.RightHandWorld, drv.MovingFuse.position):0.00} m from the fuse centre");
                    }
                while (Time.time - t0 < FuseTimeline.InEnd + 0.4f) yield return null;
                b = mm.FuseBoxes.Value[box];
                if (!b.Has(socket)) Fail($"socket {socket} of box {box} still empty");
                Log($"after insert: {Boxes(mm)} mask={me.FuseMask.Value} sounds={FuseSounds.Played}");
            }
        }

        /// <summary>Holds E on socket <paramref name="socket"/> for <paramref name="hold"/> s.</summary>
        private static IEnumerator PullOut(NetPlayer me, int box, int socket, float hold)
        {
            yield return StandAt(me, box, socket, FuseBoxModel.SocketX(socket) * 0.6f);
            Log($"pull box {box} socket {socket}: prompt {PromptNow()}");
            FuseWorkDriver.TestHoldE = true;
            LocalPlayerController.TestPressE = true;
            yield return new WaitForSeconds(hold);
            FuseWorkDriver.TestHoldE = false;
            yield return new WaitForSeconds(Mathf.Max(0.5f, FuseTimeline.OutEnd - hold + 0.3f));
        }
    }
}
