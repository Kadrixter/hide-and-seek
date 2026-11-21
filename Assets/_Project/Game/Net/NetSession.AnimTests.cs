using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Animation tests in the built game:
    ///  -testkills all|0,5,12 (host, with -forcehider, + 2 clients): every listed kill variant between the seeker and the
    ///      other hider; the victim is brought back after each one.
    ///  -testheal (host, -forcehider, + another hider and a seeker): medkit on the own wound (G), then on the other hider (H).
    ///  -animcam &lt;folder&gt; (any instance): a lit side camera films every kill / hold animation it sees (0.2 s per frame).
    /// </summary>
    public partial class NetSession
    {
        private void StartAnimTests(Func<string, bool> has)
        {
            string kills = ArgOr("-testkills", "");
            if (!string.IsNullOrEmpty(kills)) StartCoroutine(KillsTest(kills));
            if (has("-testheal")) StartCoroutine(HealTest());
            string cam = ArgOr("-animcam", "");
            if (!string.IsNullOrEmpty(cam)) StartCoroutine(AnimCam(cam));
        }

        private static IEnumerator WaitSeeking()
        {
            while (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(1.5f);
        }

        private IEnumerator KillsTest(string list)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer seeker = null, victim = null;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                if (p.IsSeeker) seeker = p;
                else if (p != me) victim = p;
            }
            if (seeker == null || victim == null) { Debug.Log("[KillsTest] need a seeker and a second hider"); yield break; }
            var moves = new List<KillMove>();
            if (list == "all") for (int i = 0; i <= (int)KillMove.GlockFinisher; i++) moves.Add((KillMove)i);
            else foreach (var n in list.Split(',')) if (int.TryParse(n, out int v)) moves.Add((KillMove)v);
            Vector3 spot = victim.transform.position;
            me.TeleportRpc(spot + victim.transform.right * 5f, 0f);
            foreach (var move in moves)
            {
                victim.Life.Value = LifeState.Alive;
                victim.Severed.Value = 0;
                victim.Wounded.Value = false;
                victim.Limping.Value = true;
                victim.Busy.Value = false;
                victim.DownedUntil.Value = 0;
                seeker.Busy.Value = false;
                seeker.AttackReadyAt.Value = 0;
                if (move == KillMove.GlockFinisher) { mm.TestGiveGlock(); seeker.Held.Value = HeldItem.Glock; }
                else seeker.Held.Value = KillMoves.IsSaw(move) ? HeldItem.Chainsaw : HeldItem.Knife;
                victim.TeleportRpc(spot, 0f);
                seeker.TeleportRpc(spot + Vector3.forward * 1.4f, 180f);
                yield return new WaitForSeconds(2.2f);
                Debug.Log($"[KillsTest] start {move} ({KillMoves.SequenceDuration(move):0.0} s)");
                mm.TestKillMove(seeker, victim, move);
                yield return new WaitForSeconds(KillMoves.SequenceDuration(move) + 1.2f);
                Debug.Log($"[KillsTest] end {move}: victim life={victim.Life.Value} severed={victim.Severed.Value} busy={victim.Busy.Value}/{seeker.Busy.Value}");
            }
            Debug.Log("[KillsTest] done");
        }

        private IEnumerator HealTest()
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            NetPlayer other = null;
            foreach (var p in NetPlayer.All) if (p != null && p != me && p.IsHider) other = p;
            me.MedkitCount.Value = 2;
            me.Limping.Value = true;
            yield return new WaitForSeconds(1.5f);
            LocalPlayerController.TestHoldG = true;
            yield return new WaitForSeconds(Tuning.HealHoldTime + 0.8f);
            LocalPlayerController.TestHoldG = false;
            Debug.Log($"[HealTest] self: limping={me.Limping.Value} medkits={me.MedkitCount.Value}");
            if (other == null) { Debug.Log("[HealTest] no other hider"); yield break; }
            other.Limping.Value = true;
            Vector3 op = other.transform.position;
            me.TeleportRpc(op + other.transform.forward * 1.0f, other.transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(1.5f);
            LocalPlayerController.TestHoldH = true;
            yield return new WaitForSeconds(Tuning.HealHoldTime + 0.8f);
            LocalPlayerController.TestHoldH = false;
            Debug.Log($"[HealTest] other: limping={other.Limping.Value} medkits={me.MedkitCount.Value}");
        }

        /// <summary>Lit side camera on whatever kill / hold animation is running (own screen, above everything).</summary>
        private IEnumerator AnimCam(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var go = new GameObject("AnimTestCam");
            DontDestroyOnLoad(go);
            var cam = go.AddComponent<Camera>();
            cam.depth = 60f;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.enabled = false;
            var key = new GameObject("AnimTestKey").AddComponent<Light>();
            key.transform.SetParent(go.transform, false);
            key.type = LightType.Spot;
            key.spotAngle = 60f;
            key.range = 12f;
            key.intensity = 2.2f;
            var fill = new GameObject("AnimTestFill").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 6f;
            fill.intensity = 1.2f;
            DontDestroyOnLoad(fill.gameObject);
            int n = 0;
            while (true)
            {
                yield return new WaitForSeconds(0.2f);
                NetPlayer a = null, b = null;
                foreach (var p in NetPlayer.All)
                {
                    if (p == null) continue;
                    bool busy = p.Busy.Value || p.HoldAnimVar.Value != 0;
                    if (!busy) continue;
                    if (a == null) a = p;
                    else if (b == null && p != a) b = p;
                }
                if (a != null && b == null && a.HoldAnimVar.Value != 0)
                {
                    var t = NetPlayer.Find(a.ReviveTarget.Value);
                    if (t != null && t != a) b = t;
                }
                cam.enabled = a != null;
                if (a == null) continue;
                Vector3 pa = a.transform.position, pb = b != null ? b.transform.position : pa + a.transform.forward;
                Vector3 mid = (pa + pb) * 0.5f;
                Vector3 axis = pb - pa;
                axis.y = 0f;
                if (axis.sqrMagnitude < 0.01f) axis = a.transform.forward;
                Vector3 side = Vector3.Cross(Vector3.up, axis.normalized);
                float dist = Mathf.Max(3.2f, axis.magnitude * 1.5f + 1.6f);
                go.transform.position = mid + side * dist + Vector3.up * 1.35f;
                go.transform.LookAt(mid + Vector3.up * 0.75f);
                fill.transform.position = mid + Vector3.up * 2f;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{LocalProfile.Nick}_{n++:0000}.png"));
            }
        }
    }
}
