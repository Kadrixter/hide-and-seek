using System.Collections;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Pose wheel test (not run automatically):
    ///  -testposewheel (host, -wheelshots dir): sits on a sofa seat, holds G (test hook), highlights every slice in turn
    ///      with a screenshot each (wheel + live preview), releases G on one pose (applied through the server), then
    ///      third-person shots of the own body, picks another pose with a click, checks the tap-to-keep-open mode, and
    ///      does the same on a pouf. Log lines start with [WheelTest].
    ///  For the view from a second copy run the client with -seatwatch -seatshots dir (photographs the other player
    ///  every time its pose changes).
    /// </summary>
    public partial class NetSession
    {
        private void StartWheelTests(System.Func<string, bool> has)
        {
            if (has("-testposewheel")) StartCoroutine(PoseWheelTest());
        }

        private IEnumerator PoseWheelTest()
        {
            yield return WaitLobby();
            var me = NetPlayer.Local;
            string dir = ArgOr("-wheelshots", "");
            LobbySeat sofa = null, pouf = null;
            foreach (var s in LobbySeat.All)
            {
                bool isPouf = s.transform.parent != null && s.transform.parent.name.StartsWith("Pouf");
                if (isPouf && pouf == null) pouf = s;
                if (!isPouf && sofa == null) sofa = s;
            }
            if (sofa == null) sofa = LobbySeat.All[0];
            if (pouf == null) pouf = sofa;

            foreach (var seat in new[] { sofa, pouf })
            {
                string where = seat == sofa ? "sofa" : "pouf";
                NetPlayer.TestSitPose = 0;
                TestSitOn(me, seat);
                yield return new WaitForSeconds(1.2f);

                // Hold G: the wheel opens, every slice highlighted in turn (the preview follows).
                PoseWheel.TestHold = true;
                yield return new WaitForSeconds(0.5f);
                Debug.Log($"[WheelTest] {where}: open={PoseWheel.IsOpen} slices={PoseWheel.SliceCount} pose={me.SitPoseVar.Value}");
                Shot(dir, $"wheel_{where}_open");
                int n = PoseWheel.SliceCount;
                for (int i = 0; i < n; i++)
                {
                    PoseWheel.TestHighlight = i;
                    yield return new WaitForSeconds(1.0f);
                    Shot(dir, $"wheel_{where}_slice{i}");
                    Debug.Log($"[WheelTest] {where}: highlighted {PoseWheel.Highlighted}");
                }

                // Release G on the last slice: applied through the server, the body moves there without standing up.
                int pick = Mathf.Max(0, n - 1);
                PoseWheel.TestHighlight = pick;
                yield return null;
                PoseWheel.TestHold = false;
                yield return new WaitForSeconds(0.25f);
                Debug.Log($"[WheelTest] {where}: released on {pick}: open={PoseWheel.IsOpen} seat={me.Seat.Value} pose={me.SitPoseVar.Value}");
                Shot(dir, $"wheel_{where}_blend_fpp");
                yield return new WaitForSeconds(1.0f);
                Debug.Log($"[WheelTest] {where}: after release pose={me.SitPoseVar.Value} seat={me.Seat.Value}");
                me.SetCinematicBody(true);
                var t = seat.transform;
                Vector3 at = t.position + Vector3.up * 0.75f - t.forward * 0.05f;
                CamShot(dir, $"wheel_{where}_picked_side", t.position + t.right * 2.0f + t.forward * 1.1f + Vector3.up * 1.25f, at);
                CamShot(dir, $"wheel_{where}_picked_front", t.position + t.forward * 2.3f + t.right * 0.8f + Vector3.up * 1.3f, at);
                me.SetCinematicBody(false);

                // Tap G (wheel stays open), then a click on slice 1.
                PoseWheel.TestHold = true;
                yield return null;
                yield return null;
                PoseWheel.TestHold = false;
                yield return new WaitForSeconds(0.4f);
                Debug.Log($"[WheelTest] {where}: after tap open={PoseWheel.IsOpen} (expected True)");
                PoseWheel.TestHighlight = Mathf.Min(1, n - 1);
                yield return new WaitForSeconds(0.5f);
                Shot(dir, $"wheel_{where}_tap_slice1");
                PoseWheel.TestClick = true;
                yield return new WaitForSeconds(1.2f);
                Debug.Log($"[WheelTest] {where}: after click open={PoseWheel.IsOpen} pose={me.SitPoseVar.Value} (expected {SitPoseRules.Poses(seat)[Mathf.Min(1, n - 1)].Id})");
                me.SetCinematicBody(true);
                CamShot(dir, $"wheel_{where}_clicked_front", t.position + t.forward * 2.3f + t.right * 0.8f + Vector3.up * 1.3f, at);
                me.SetCinematicBody(false);

                TestStandUp(me, seat);
                yield return new WaitForSeconds(0.8f);
                Debug.Log($"[WheelTest] {where}: stood up, wheel open={PoseWheel.IsOpen}");
            }
            Debug.Log("[WheelTest] done");
        }
    }
}
