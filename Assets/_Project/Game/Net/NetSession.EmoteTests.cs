using System;
using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Emote tests (not run automatically). Log lines start with [EmoteTest].
    ///  -testemotes (host, -emoteshots dir, a second copy with -autoconsent joined): K wheel on both pages with a shot
    ///      per highlighted slice, every solo emote started from the wheel code path with a shot from the emote camera
    ///      and a side shot, then every pair emote asked of the other player (who accepts by itself), shots at the
    ///      contact moment, the dance stopped by the test. -emoteonly Wave,Hug limits the list.
    ///  -autoconsent: answers every consent question with yes after ~1.2 s (any copy).
    /// </summary>
    public partial class NetSession
    {
        private void StartEmoteTests(Func<string, bool> has)
        {
            if (has("-autoconsent")) ConsentPrompt.AutoAccept = true;
            if (has("-testemotes")) StartCoroutine(EmotesTest());
            if (has("-emotehold")) StartCoroutine(EmoteHold());
            if (has("-emotewatch")) StartCoroutine(EmoteWatch());
            if (has("-testemotetabs")) StartCoroutine(EmoteTabsTest());
        }

        /// <summary>-testemotetabs (-emoteshots dir): page switch by hovering the tabs with the virtual cursor (dwell 0.18 s).</summary>
        private IEnumerator EmoteTabsTest()
        {
            yield return WaitLobby();
            string dir = ArgOr("-emoteshots", "");
            EmoteWheel.TestPage = 0;
            EmoteWheel.TestHold = true;
            yield return new WaitForSeconds(0.6f);
            var tab1 = new Vector2(78f, 380f);
            var tab0 = new Vector2(-78f, 380f);
            Debug.Log($"[EmoteTabs] open page={EmoteWheel.Page}");
            // Brushing past the tab (0.08 s): no switch.
            EmoteWheel.TestCursor = tab1;
            yield return new WaitForSecondsRealtime(0.08f);
            EmoteWheel.TestCursor = new Vector2(0f, 120f);
            yield return null;
            Debug.Log($"[EmoteTabs] brush past WSPÓLNE: page={EmoteWheel.Page} (expected 0)");
            // Resting on it: switches after the dwell.
            EmoteWheel.TestCursor = tab1;
            yield return new WaitForSecondsRealtime(0.1f);
            Shot(dir, "tabs_dwell");
            Debug.Log($"[EmoteTabs] 0.10 s on WSPÓLNE: page={EmoteWheel.Page} (expected 0)");
            yield return new WaitForSecondsRealtime(0.2f);
            Debug.Log($"[EmoteTabs] 0.30 s on WSPÓLNE: page={EmoteWheel.Page} slices={EmoteWheel.SliceCount} (expected 1, 5)");
            yield return new WaitForSecondsRealtime(0.5f);
            Shot(dir, "tabs_pair");
            Debug.Log($"[EmoteTabs] staying on it 0.8 s: page={EmoteWheel.Page} (expected 1)");
            EmoteWheel.TestCursor = tab0;
            yield return new WaitForSecondsRealtime(0.35f);
            Debug.Log($"[EmoteTabs] 0.35 s on SOLO: page={EmoteWheel.Page} slices={EmoteWheel.SliceCount} (expected 0, 11)");
            EmoteWheel.TestCursor = new Vector2(0f, 250f);
            yield return new WaitForSecondsRealtime(0.4f);
            Debug.Log($"[EmoteTabs] back on a slice: page={EmoteWheel.Page} highlighted slice ok");
            Shot(dir, "tabs_solo_slice");
            EmoteWheel.TestCancel = true;
            yield return null;
            EmoteWheel.TestHold = false;
            Debug.Log("[EmoteTest] done");
        }

        /// <summary>-emotehold Id [-emoteholdfor S]: starts one emote in the lobby and keeps it (late-join check).</summary>
        private IEnumerator EmoteHold()
        {
            yield return WaitLobby();
            var me = NetPlayer.Local;
            if (!System.Enum.TryParse<EmoteId>(ArgOr("-emotehold", "DanceDisco"), out var id)) id = EmoteId.DanceDisco;
            float.TryParse(ArgOr("-emoteholdfor", "40"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float len);
            me.StartSoloEmote(id);
            Debug.Log($"[EmoteTest] holding {id} for {len} s");
            yield return new WaitForSeconds(Mathf.Max(5f, len));
            me.StopEmote();
            Debug.Log("[EmoteTest] done");
        }

        /// <summary>-emotewatch (-emoteshots dir): logs and photographs the other players' emotes every 2 s.</summary>
        private IEnumerator EmoteWatch()
        {
            yield return WaitLobby();
            string dir = ArgOr("-emoteshots", "");
            int k = 0;
            while (true)
            {
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p.IsOwner || p.Character == null) continue;
                    var b = p.Character.Actions;
                    Debug.Log($"[EmoteWatch] {p.Nick}: var={p.EmoteVar.Value.Id} pair={p.PairVar.Value.Id} body={b.Emote} w={b.EmoteWeight:0.00} t={b.EmoteTime:0.00}");
                    if (b.EmoteWeight > 0.5f && k % 2 == 0)
                    {
                        var t = p.transform;
                        CamShot(dir, $"watch_{k:000}_{p.Nick}_{b.Emote}", t.position + t.forward * 2.6f + t.right * 1.2f + Vector3.up * 1.5f, t.position + Vector3.up * 0.95f);
                    }
                }
                k++;
                yield return new WaitForSeconds(2f);
            }
        }

        private IEnumerator EmotesTest()
        {
            yield return WaitLobby();
            var me = NetPlayer.Local;
            string dir = ArgOr("-emoteshots", "");
            string only = ArgOr("-emoteonly", "");
            bool Want(EmoteId id) => string.IsNullOrEmpty(only) || ("," + only + ",").Contains("," + id + ",");
            float wait = 0f;
            while (NetPlayer.All.Count < 2 && wait < 40f) { wait += Time.deltaTime; yield return null; }
            NetPlayer other = null;
            foreach (var p in NetPlayer.All) if (p != me) other = p;
            Debug.Log($"[EmoteTest] start, other={(other != null ? other.Nick : "-")}");
            yield return new WaitForSeconds(6f); // the other one is placed in the lobby first

            // Stand 1.6 m in front of the other player, facing it.
            if (other != null) Face(me, other, 1.6f);
            yield return new WaitForSeconds(0.6f);

            // Wheel: both pages, every slice highlighted.
            for (int page = 0; page < 2; page++)
            {
                EmoteWheel.TestPage = page;
                EmoteWheel.TestHold = true;
                yield return new WaitForSeconds(0.8f);
                EmoteWheel.TestPage = page;
                yield return new WaitForSeconds(0.6f);
                Debug.Log($"[EmoteTest] wheel page {EmoteWheel.Page}: open={EmoteWheel.IsOpen} slices={EmoteWheel.SliceCount} target={(EmoteWheel.Target != null ? EmoteWheel.Target.Nick : "-")} {EmoteRules.Explain(me, other)}");
                Shot(dir, $"wheel_p{page}_open");
                int n = EmoteWheel.SliceCount;
                for (int i = 0; i < n; i++)
                {
                    EmoteWheel.TestHighlight = i;
                    yield return new WaitForSeconds(i < 3 ? 1.1f : 0.7f);
                    Shot(dir, $"wheel_p{page}_s{i}");
                }
                EmoteWheel.TestHighlight = -1;
                EmoteWheel.TestCancel = true;
                yield return null;
                EmoteWheel.TestHold = false;
                yield return new WaitForSeconds(0.4f);
            }

            // Solo emotes.
            foreach (var id in Emotes.Solo)
            {
                if (!Want(id)) continue;
                if (other != null) Face(me, other, 2.2f);
                yield return new WaitForSeconds(0.5f);
                bool ok = me.StartSoloEmote(id);
                var info = Emotes.Get(id);
                if (id == EmoteId.Wave)
                {
                    // The camera going out of the eyes and back in.
                    float[] at = { 0.15f, 0.35f, 0.7f, 1.3f, 2.2f, 3.0f, 3.4f };
                    float last = 0f;
                    foreach (float a in at)
                    {
                        yield return new WaitForSeconds(a - last);
                        last = a;
                        Debug.Log($"[EmoteTest] cam {a:0.00}: view={me.EmoteViewActive} {EmoteCamDebug(me)}");
                        Shot(dir, $"cam_{a:0.00}");
                    }
                    continue;
                }
                yield return new WaitForSeconds(Mathf.Max(0.6f, info.ThumbTime));
                var body = me.Character.Actions;
                Debug.Log($"[EmoteTest] {id}: started={ok} var={me.EmoteVar.Value.Id} body={body.Emote} w={body.EmoteWeight:0.00} view={me.EmoteViewActive}");
                Shot(dir, $"solo_{id}_view");
                SideShot(dir, $"solo_{id}_side", me, null);
                float len = info.Loops ? 3.5f : info.Duration - info.ThumbTime;
                yield return new WaitForSeconds(Mathf.Max(0.5f, len));
                if (info.Loops) me.StopEmote();
                yield return new WaitForSeconds(info.Exit + 0.9f);
                Debug.Log($"[EmoteTest] {id}: after var={me.EmoteVar.Value.Id} body={body.Emote} shown={body.EmoteShown} view={me.EmoteViewActive}");
            }

            // Pair emotes (the other copy runs with -autoconsent).
            if (other != null)
                foreach (var id in Emotes.Pair)
                {
                    if (!Want(id)) continue;
                    Face(me, other, 1.7f);
                    yield return new WaitForSeconds(0.8f);
                    bool asked = me.AskPairEmote(id, other);
                    float t0 = Time.time;
                    while (!me.PairVar.Value.Active && Time.time - t0 < 6f) yield return null;
                    var pv = me.PairVar.Value;
                    Debug.Log($"[EmoteTest] {id}: asked={asked} active={pv.Active} role={pv.Role} after {Time.time - t0:0.0} s");
                    if (!pv.Active) continue;
                    var info = Emotes.Get(id);
                    var mm = MatchManager.Instance;
                    while (mm.Now < pv.StartAt + info.ThumbTime) yield return null;
                    float d = Vector3.Distance(me.transform.position, other.transform.position);
                    Debug.Log($"[EmoteTest] {id}: at contact dist={d:0.00} (want {info.PairDistance:0.00}) other var={other.PairVar.Value.Id} body={me.Character.Actions.Emote}/{other.Character.Actions.Emote}");
                    Shot(dir, $"pair_{id}_view");
                    SideShot(dir, $"pair_{id}_side", me, other);
                    if (info.Loops)
                    {
                        yield return new WaitForSeconds(3.5f);
                        me.StopEmote();
                    }
                    t0 = Time.time;
                    while (me.PairVar.Value.Active && Time.time - t0 < 8f) yield return null;
                    yield return new WaitForSeconds(1f);
                    Debug.Log($"[EmoteTest] {id}: ended active={me.PairVar.Value.Active} view={me.EmoteViewActive}");
                }
            Debug.Log("[EmoteTest] done");
        }

        private static string EmoteCamDebug(NetPlayer me)
        {
            var c = me.GetComponent<EmoteCamera>();
            return c != null ? c.Describe() : "-";
        }

        private static void Face(NetPlayer me, NetPlayer other, float dist)
        {
            Vector3 f = other.transform.forward;
            Vector3 pos = other.transform.position + f * dist;
            me.TeleportLocal(pos, Quaternion.LookRotation(-f).eulerAngles.y);
        }

        private static void SideShot(string dir, string name, NetPlayer me, NetPlayer other)
        {
            Vector3 c = other != null ? (me.transform.position + other.transform.position) * 0.5f : me.transform.position;
            Vector3 axis = other != null ? (other.transform.position - me.transform.position).normalized : me.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            Vector3 eye = c + side * 2.6f + axis * (other != null ? 0.6f : 1.6f) + Vector3.up * 1.4f;
            me.SetCinematicBody(true);
            CamShot(dir, name, eye, c + Vector3.up * 1.0f);
        }
    }
}
