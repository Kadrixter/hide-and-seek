using System.Collections;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests of the security room and its crate:
    /// -testroomdark (-roomshots dir): walks through the level 0 room, screenshots with and without the flashlight.
    /// -testcrate (host hider, -crateshots dir): push aside, close from inside, open from inside, close from outside.
    /// -testcratesaw (host seeker, -crateshots dir): the seeker cannot push, cuts the crate apart with the chainsaw.
    /// -cratewatch (any client, -crateshots dir): logs the synced crate state, photographs the crate, and after it breaks
    /// forgets the local visuals to check that a client without the animation gets the settled debris.
    /// </summary>
    public partial class NetSession
    {
        private void StartCrateTests(System.Func<string, bool> has)
        {
            if (has("-testroomdark")) StartCoroutine(RoomDarkTest());
            if (has("-testcrate")) StartCoroutine(CrateCycleTest());
            if (has("-testcratesaw")) StartCoroutine(CrateSawTest());
            if (has("-cratewatch")) StartCoroutine(CrateWatch());
        }

        private static IEnumerator WaitSeeking()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null || SecurityRooms.Instance == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1f);
        }

        private static string CrateLog(int lv)
        {
            var mm = MatchManager.Instance;
            var sr = SecurityRooms.Instance;
            return $"state={SecurityRooms.StateOf(mm.SecurityCrates.Value, lv)} open={sr.CrateOpenNow(lv)} broken={sr.CrateBrokenNow(lv)} cratePos={sr.CratePosition(lv):F2}";
        }

        private IEnumerator RoomDarkTest()
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            string dir = ArgOr("-roomshots", "");
            var views = new (string name, Vector3 pos, float yaw, float pitch)[]
            {
                ("corridor", new Vector3(77.0f, 0.05f, 52.5f), 0f, 0f),
                ("across", new Vector3(77.0f, 0.05f, 55.4f), 285f, 4f),
                ("monitors", new Vector3(72.6f, 0.05f, 56.3f), 0f, 6f),
                ("desk", new Vector3(74.2f, 0.05f, 57.9f), 340f, -32f),
                ("west", new Vector3(70.8f, 0.05f, 56.2f), 270f, 4f),
                ("south", new Vector3(71.5f, 0.05f, 57.6f), 180f, 2f),
                ("outside", new Vector3(76.0f, 0.05f, 46.5f), 5f, 6f),
            };
            foreach (var v in views)
            {
                me.Motor.Teleport(v.pos, v.yaw, v.pitch);
                me.Motor.SetLook(v.yaw, v.pitch);
                if (me.Flashlight != null) me.Flashlight.SetOn(false, false);
                yield return new WaitForSeconds(1.2f);
                Shot(dir, $"room_{v.name}_dark");
                yield return new WaitForSeconds(0.4f);
                if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
                yield return new WaitForSeconds(0.6f);
                Shot(dir, $"room_{v.name}_lamp");
                yield return new WaitForSeconds(0.4f);
            }
            if (me.Flashlight != null) me.Flashlight.SetOn(false, false);
            // From the garage behind the west wall: nothing of the room may show through it.
            CamShot(dir, "room_westwall_outside", new Vector3(61.5f, 1.6f, 56f), new Vector3(66f, 1.5f, 56f));
            CamShot(dir, "room_northwest_outside", new Vector3(63f, 1.6f, 50f), new Vector3(70f, 1.4f, 55f));
            Debug.Log("[RoomDark] done");
        }

        private static GameObject CrateTestLight(Vector3 at)
        {
            var l = new GameObject("CrateTestLight").AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 9f;
            l.intensity = 1.3f;
            l.shadows = LightShadows.Soft;
            l.transform.position = at;
            return l.gameObject;
        }

        private IEnumerator CrateFrames(string dir, string prefix, Vector3 cam, Vector3 at, float seconds, float every)
        {
            int i = 0;
            for (float t = 0f; t < seconds; t += every, i++)
            {
                CamShot(dir, $"{prefix}_{i:00}", cam, at);
                yield return new WaitForSeconds(every);
            }
        }

        private IEnumerator CrateCycleTest()
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            var lpc = LocalPlayerController.Instance;
            string dir = ArgOr("-crateshots", "");
            if (!me.IsHider) Debug.Log("[CrateTest] warning: host is not a hider");
            Vector3 stand = SecurityRooms.ActionStand(0, CrateAction.OpenOutside, out _);
            var light = CrateTestLight(stand + new Vector3(-1.2f, 2.5f, -2.6f));

            // 1. Outside: push aside.
            me.Motor.Teleport(stand + new Vector3(0.2f, 0f, -1.3f), 300f);
            me.Motor.SetLook(300f, -6f);
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[CrateTest] outside closed: prompt='{lpc.PromptText}' {CrateLog(0)}");
            Vector3 side = stand + new Vector3(-0.9f, 1.6f, -3.6f), look = stand + new Vector3(-1.2f, 0.8f, 0.4f);
            me.RequestCrateActionRpc(0, (byte)CrateAction.OpenOutside);
            yield return CrateFrames(dir, "open_out", side, look, SecurityRooms.CratePushTime + 0.3f, 0.3f);
            Debug.Log($"[CrateTest] after open outside: {CrateLog(0)} me={me.transform.position:F2} busy={me.Busy.Value}");

            // 2. Walk in, close it from the doorway.
            Vector3 inStand = SecurityRooms.ActionStand(0, CrateAction.CloseInside, out _);
            me.Motor.Teleport(inStand + new Vector3(0.3f, 0f, 1.2f), 200f);
            me.Motor.SetLook(200f, -4f);
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[CrateTest] inside, crate open: prompt='{lpc.PromptText}'");
            Vector3 inCam = new Vector3(77.7f, 1.8f, 54.4f), inLook = new Vector3(76.3f, 0.8f, 51.7f);
            me.RequestCrateActionRpc(0, (byte)CrateAction.CloseInside);
            yield return CrateFrames(dir, "close_in", inCam, inLook, SecurityRooms.CratePushTime + 0.3f, 0.3f);
            Debug.Log($"[CrateTest] after close inside: {CrateLog(0)}");
            CamShot(dir, "close_in_outside_view", stand + new Vector3(-0.8f, 1.6f, -4f), stand + new Vector3(-0.6f, 0.8f, 0.4f));

            // 3. Open it again from the doorway.
            me.Motor.Teleport(SecurityRooms.ActionStand(0, CrateAction.OpenInside, out _) + new Vector3(0f, 0f, 0.6f), 180f);
            me.Motor.SetLook(180f, -4f);
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[CrateTest] inside, crate closed: prompt='{lpc.PromptText}'");
            me.RequestCrateActionRpc(0, (byte)CrateAction.OpenInside);
            yield return CrateFrames(dir, "open_in", inCam, inLook, SecurityRooms.CratePushTime + 0.3f, 0.3f);
            Debug.Log($"[CrateTest] after open inside: {CrateLog(0)}");

            // 4. Outside again, west of the open crate: push it back in front of the door.
            Vector3 cStand = SecurityRooms.ActionStand(0, CrateAction.CloseOutside, out _);
            me.Motor.Teleport(cStand + new Vector3(-0.2f, 0f, -1.3f), 60f);
            me.Motor.SetLook(60f, -6f);
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[CrateTest] outside open: prompt='{lpc.PromptText}'");
            me.RequestCrateActionRpc(0, (byte)CrateAction.CloseOutside);
            yield return CrateFrames(dir, "close_out", side, look, SecurityRooms.CratePushTime + 0.3f, 0.3f);
            Debug.Log($"[CrateTest] after close outside: {CrateLog(0)} me={me.transform.position:F2}");
            Destroy(light);
            Shot(dir, "close_out_player_view");
        }

        private IEnumerator CrateSawTest()
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            var lpc = LocalPlayerController.Instance;
            string dir = ArgOr("-crateshots", "");
            if (!me.IsSeeker) { Debug.Log("[CrateSaw] need host seeker"); yield break; }
            Vector3 stand = SecurityRooms.SawStand(0, false);
            var light = CrateTestLight(stand + new Vector3(2.2f, 2.4f, -1.5f));

            // The seeker cannot push the crate.
            me.Motor.Teleport(SecurityRooms.PushStand(0) + new Vector3(0.2f, 0f, -1.2f), 300f);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[CrateSaw] seeker without saw: prompt='{lpc.PromptText}'");
            me.RequestCrateActionRpc(0, (byte)CrateAction.OpenOutside);
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[CrateSaw] after seeker push attempt: {CrateLog(0)} busy={me.Busy.Value}");

            me.HasChainsaw.Value = true;
            me.Held.Value = HeldItem.Chainsaw;
            me.Motor.Teleport(stand + new Vector3(0.1f, 0f, -0.9f), 0f);
            me.Motor.SetLook(0f, -5f);
            yield return new WaitForSeconds(2.2f);
            Debug.Log($"[CrateSaw] with saw: prompt='{lpc.PromptText}'");
            Vector3 side = stand + new Vector3(2.4f, 1.5f, 0.4f), look = stand + new Vector3(0f, 0.9f, 0.9f);
            CamShot(dir, "saw_before", side, look);
            me.RequestCrateActionRpc(0, (byte)CrateAction.Saw);
            yield return CrateFrames(dir, "saw", side, look, Characters.BlockyCharacterActions.CrateSawTime + 0.4f, 0.2f);
            Debug.Log($"[CrateSaw] after cut: {CrateLog(0)} busy={me.Busy.Value}");
            yield return new WaitForSeconds(4.5f);
            CamShot(dir, "saw_debris", stand + new Vector3(1.8f, 2.2f, -1.6f), stand + new Vector3(0f, 0f, 1.2f));
            Destroy(light);
            Shot(dir, "saw_debris_player_dark");
            if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
            yield return new WaitForSeconds(0.5f);
            Shot(dir, "saw_debris_player_lamp");
            // No more actions on a broken crate.
            me.RequestCrateActionRpc(0, (byte)CrateAction.Saw);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[CrateSaw] second cut attempt: {CrateLog(0)} busy={me.Busy.Value} prompt={lpc.Prompt}");
            // Walk through the open doorway.
            Vector3 door = new Vector3(76.8f, 0.05f, 50.0f);
            for (int i = 0; i < 50; i++)
            {
                me.Motor.Teleport(Vector3.Lerp(door, door + new Vector3(0f, 0f, 4.5f), i / 49f), 0f);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[CrateSaw] walked in: me={me.transform.position:F2} inside={SecurityRooms.IsInside(me.transform.position)}");
            Debug.Log($"[CrateSaw] done, crates={mm.SecurityCrates.Value}");
        }

        private IEnumerator CrateWatch()
        {
            yield return WaitSeeking();
            string dir = ArgOr("-crateshots", "");
            var sr = SecurityRooms.Instance;
            var mm = MatchManager.Instance;
            string last = "";
            bool forgot = false;
            Vector3 cam = new Vector3(78.4f, 1.6f, 48.6f), at = new Vector3(76.0f, 0.7f, 51.3f);
            Vector3 inCam = new Vector3(77.7f, 1.8f, 54.6f), inAt = new Vector3(76.3f, 0.8f, 51.8f);
            int shot = 0;
            CrateTestLight(new Vector3(77.6f, 2.5f, 49.2f));
            CrateTestLight(new Vector3(77.3f, 2.4f, 53.6f)).GetComponent<Light>().intensity = 0.7f;
            for (int i = 0; i < 400; i++)
            {
                string s = CrateLog(0);
                if (s != last) { Debug.Log($"[CrateWatch] t={Time.time:0.0} {s}"); last = s; }
                // Dense frames while somebody else works on the crate (his body is visible from here).
                NetPlayer actor = null;
                foreach (var p in NetPlayer.All) if (p != null && p != NetPlayer.Local && p.Busy.Value) actor = p;
                if (actor != null)
                {
                    bool inside = SecurityRooms.IsInside(actor.transform.position);
                    CamShot(dir, $"watch_{shot++:000}", inside ? inCam : cam, inside ? inAt : at);
                }
                if (!forgot && SecurityRooms.StateOf(mm.SecurityCrates.Value, 0) == CrateState.Broken && sr.CrateBrokenNow(0) && i > 0)
                {
                    yield return new WaitForSeconds(5f);
                    CamShot(dir, "watch_live_debris", cam, at);
                    sr.TestForgetCrate(0);
                    Debug.Log($"[CrateWatch] forgot visuals: {CrateLog(0)}");
                    yield return new WaitForSeconds(1.5f);
                    Debug.Log($"[CrateWatch] after re-sync: {CrateLog(0)}");
                    CamShot(dir, "watch_resync_debris", cam, at);
                    forgot = true;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }
    }
}
