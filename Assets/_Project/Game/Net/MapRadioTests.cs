using System;
using System.Collections;
using System.IO;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests of the radio found on the parking (own component, started from the command line):
    ///  -testmapradio (host with -forcehider -autostart -hidetime N, + a seeker client): the radio on its spot, the E
    ///      prompt, lift (first person shots), carrying (third person shots from 4 sides), speed scale, music through
    ///      the panel (-radiourl, default <see cref="BoomboxTests.DefaultUrl"/>; -nomapmusic skips it), walking with it,
    ///      put down (Q), lift again and knocked down (falls), logs [MapRadioTest].
    ///  -mapradiowatch (the other instance): films the carrier as the others see him, logs what it hears
    ///      (volume factor between floors, level).
    ///  -mapradioshots &lt;folder&gt;: PNGs.
    /// </summary>
    public class MapRadioTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;

        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : def;
        }

        private static string Dir => ArgOr("-mapradioshots", "");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Has("-testmapradio") && !Has("-mapradiowatch")) return;
            var go = new GameObject("[MapRadioTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<MapRadioTests>();
            if (Has("-testmapradio")) t.StartCoroutine(t.HostTest());
            if (Has("-mapradiowatch")) t.StartCoroutine(t.Watch());
        }

        private static void Log(string s) => Debug.Log("[MapRadioTest] " + s);

        private static IEnumerator WaitSeeking()
        {
            while (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(1.5f);
        }

        private static void Screen(string name)
        {
            if (string.IsNullOrEmpty(Dir)) return;
            Directory.CreateDirectory(Dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png"));
        }

        private static string State(MatchManager mm, int i)
        {
            var s = mm.MapRadios[i];
            return $"mode={s.Mode} carrier={s.Carrier} move={s.Move} since={(mm.Now - s.MoveAt):0.00} pos={s.Pos} yaw={s.Yaw:0}";
        }

        // ================================================================ host (carrier)

        private IEnumerator HostTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            Log($"radios {mm.MapRadios.Count}, candidates {MapRadioSpots.Candidates().Count}, me hider={me.IsHider}");
            if (mm.MapRadios.Count == 0) { Log("FAIL no radio on the map"); yield break; }
            for (int k = 0; k < mm.MapRadios.Count; k++) Log($"radio {k}: {State(mm, k)}");
            var s = mm.MapRadios[0];
            Vector3 fwd = s.Rot * Vector3.forward;
            MapRadioLitShot("lying_overview", s.Pos + fwd * 2.6f + Vector3.Cross(Vector3.up, fwd) * 1.4f + Vector3.up * 1.4f, s.Pos + Vector3.up * 0.2f);
            MapRadioLitShot("lying_close", s.Pos + fwd * 0.9f + Vector3.Cross(Vector3.up, fwd) * 0.5f + Vector3.up * 0.5f, s.Pos + Vector3.up * 0.15f);

            // In front of it, looking at it.
            Vector3 stand = new Vector3(s.Pos.x, ItemPlacement.FloorHeight(s.Pos + fwd * 0.95f), s.Pos.z) + fwd * 0.95f;
            Look(me, stand, s.Pos + Vector3.up * 0.15f);
            yield return new WaitForSeconds(1.2f);
            var lpc = LocalPlayerController.Instance;
            Log($"prompt: {lpc.Prompt} '{lpc.PromptText}'");
            Screen("fpp_prompt");

            // Lift.
            LocalPlayerController.TestMapRadioE = true;
            yield return new WaitForSeconds(0.3f);
            Log($"after E: {State(mm, 0)} carries={me.CarriesMapRadio}");
            for (int k = 0; k < 4; k++)
            {
                yield return new WaitForSeconds(0.3f);
                Screen($"fpp_lift_{k}");
            }
            yield return new WaitForSeconds(0.8f);
            Log($"carrying: {State(mm, 0)} speedScale={me.Motor.SpeedScale:0.00} hud='{MapRadioSystem.HudText}'");
            Screen("fpp_carry");
            Look(me, me.transform.position, me.transform.position + me.transform.forward * 4f + Vector3.up * 1.2f);
            yield return new WaitForSeconds(0.5f);
            Screen("fpp_carry_level");

            // Music.
            if (!Has("-nomapmusic"))
            {
                LocalPlayerController.TestMapRadioR = true;
                yield return null;
                yield return null;
                Log($"panel open={Boombox.PanelOpen}");
                Boombox.TestUrl = ArgOr("-radiourl", BoomboxTests.DefaultUrl);
                yield return new WaitForSeconds(0.5f);
                Screen("panel");
                Boombox.ClosePanel();
                var box = MapRadioSystem.Instance.ViewOf(0);
                float until = Time.time + 100f;
                while (Time.time < until && !(box != null && box.MusicAudible)) yield return new WaitForSeconds(0.5f);
                Log($"music: audible={box != null && box.MusicAudible} {box?.DebugState}");
                yield return new WaitForSeconds(4f);
                Screen("fpp_music");
            }

            // Walk with it.
            yield return Walk(me, 4.5f, me.Motor.WalkSpeed * MapRadioSystem.WalkScale);
            Log($"walked: {State(mm, 0)} speedScale={me.Motor.SpeedScale:0.00}");

            // Put down.
            LocalPlayerController.TestMapRadioQ = true;
            yield return new WaitForSeconds(0.25f);
            Log($"after Q: {State(mm, 0)}");
            for (int k = 0; k < 4; k++)
            {
                Screen($"fpp_put_{k}");
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(0.6f);
            s = mm.MapRadios[0];
            Log($"put down: {State(mm, 0)} dist={Vector3.Distance(me.transform.position, s.Pos):0.00} collider={(MapRadioSystem.Instance.ViewOf(0)?.Collider?.enabled)}");
            MapRadioLitShot("put_result", s.Pos + (s.Rot * Vector3.forward) * 1.1f + Vector3.up * 0.7f, s.Pos + Vector3.up * 0.15f);

            // Again, then knocked down: it falls.
            stand = s.Pos + (s.Rot * Vector3.forward) * 0.9f;
            Look(me, stand, s.Pos + Vector3.up * 0.15f);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestMapRadioE = true;
            yield return new WaitForSeconds(ShoulderRadioRig.LiftTime + 0.5f);
            Log($"lifted again: {State(mm, 0)}");
            mm.TestDownForMapRadio(me);
            yield return new WaitForSeconds(0.15f);
            Log($"downed: life={me.Life.Value} {State(mm, 0)}");
            yield return new WaitForSeconds(1.8f);
            s = mm.MapRadios[0];
            MapRadioLitShot("fall_result", s.Pos + (s.Rot * Vector3.forward) * 1.2f + Vector3.up * 0.8f, s.Pos + Vector3.up * 0.15f);
            Log($"fell: {State(mm, 0)}");
            Log("done");
        }

        private static void Look(NetPlayer me, Vector3 stand, Vector3 target)
        {
            Vector3 eye = stand + Vector3.up * 1.62f;
            Vector3 d = target - eye;
            me.TeleportLocal(stand, Quaternion.LookRotation(new Vector3(d.x, 0f, d.z)).eulerAngles.y);
            me.Motor.SetLook(me.Motor.LookYaw, -Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
        }

        private static IEnumerator Walk(NetPlayer me, float seconds, float speed)
        {
            Vector3 best = me.transform.forward;
            float bestD = 0f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                float free = Physics.SphereCast(me.transform.position + Vector3.up * 0.9f, 0.35f, d, out var h, 12f, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore) ? h.distance : 12f;
                if (free > bestD) { bestD = free; best = d; }
            }
            me.TeleportLocal(me.transform.position, Quaternion.LookRotation(best).eulerAngles.y);
            yield return new WaitForSeconds(0.4f);
            var cc = me.GetComponent<CharacterController>();
            float end = Time.time + Mathf.Min(seconds, (bestD - 1f) / Mathf.Max(0.5f, speed));
            while (Time.time < end)
            {
                if (cc != null && cc.enabled) cc.Move(best * speed * Time.deltaTime);
                yield return null;
            }
        }

        // ================================================================ the other player

        private IEnumerator Watch()
        {
            yield return WaitSeeking();
            int n = 0, music = 0;
            float nextLog = 0f, angle = 30f, next = 0f;
            bool placed = false;
            while (n < 460 && Time.time < 900f)
            {
                yield return null;
                var mm = MatchManager.Instance;
                var me = NetPlayer.Local;
                if (mm == null || mm.MapRadios == null || mm.MapRadios.Count == 0 || me == null) continue;
                var s = mm.MapRadios[0];
                var box = MapRadioSystem.Instance != null ? MapRadioSystem.Instance.ViewOf(0) : null;
                if (!placed && box != null)
                {
                    // Stand ~7 m from the radio (same level) to hear it.
                    placed = true;
                    Vector3 best = s.Pos;
                    float bestFree = 0f;
                    for (int i = 0; i < 12; i++)
                    {
                        Vector3 d = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
                        float free = Physics.SphereCast(s.Pos + Vector3.up * 1f, 0.35f, d, out var h, 7f, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore) ? h.distance : 7f;
                        if (free > bestFree) { bestFree = free; best = s.Pos + d * Mathf.Max(1.5f, free - 0.6f); }
                    }
                    me.TeleportLocal(new Vector3(best.x, ItemPlacement.FloorHeight(best), best.z), Quaternion.LookRotation(s.Pos - best).eulerAngles.y);
                }
                if (Time.time >= nextLog && box != null)
                {
                    nextLog = Time.time + 2f;
                    Log($"watch: {State(mm, 0)} audible={box.MusicAudible} level={(box.Output != null ? box.Output.Level : 0f):0.00} volScale={box.VolumeScale:0.00} " +
                        $"dist={Vector3.Distance(me.EyePosition, box.transform.position):0.0} floors me={HideAndSeek.Core.FloorAcoustics.FloorOf(me.transform.position.y)} " +
                        $"radio={HideAndSeek.Core.FloorAcoustics.FloorOf(box.transform.position.y)}");
                }
                if (Time.time < next) continue;
                NetPlayer c = NetPlayer.Find(s.Mode == MapRadioState.Carried ? s.Carrier : s.LastCarrier);
                float since = (float)(mm.Now - s.MoveAt);
                bool anim = since < 2.2f;
                bool playing = box != null && box.MusicAudible;
                if (c == null || c == me || (s.Mode != MapRadioState.Carried && !anim)) { next = Time.time + 0.1f; continue; }
                bool fast = anim || (playing && music < 90);
                if (playing && !anim) music++;
                next = Time.time + (fast ? 0.1f : 0.8f);
                angle += fast ? 2.2f : 12f;
                Vector3 at = c.transform.position + Vector3.up * (s.Mode == MapRadioState.Carried ? 1.15f : 0.8f);
                Vector3 off = Quaternion.Euler(0f, angle, 0f) * (c.transform.forward * 2.3f) + Vector3.up * 0.35f;
                MapRadioLitShot($"watch_{n++:000}_m{s.Mode}_v{s.Move}{(playing ? "_music" : "")}", at + off, at);
            }
            Log($"watch done, {n} shots");
        }

        /// <summary>Temporary camera with its own key and fill light (the garage is dark) rendered into a PNG.</summary>
        private static void MapRadioLitShot(string name, Vector3 pos, Vector3 at)
        {
            if (string.IsNullOrEmpty(Dir)) return;
            Directory.CreateDirectory(Dir);
            var go = new GameObject("MapRadioShot");
            go.transform.position = pos;
            go.transform.LookAt(at);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~((1 << 9) | (1 << PlayerAvatarBinder.LocalBodyLayer));
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(go.transform, false);
            key.transform.localPosition = new Vector3(0.6f, 0.5f, 0f);
            key.type = LightType.Spot;
            key.spotAngle = 70f;
            key.range = 10f;
            key.intensity = 2.4f;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.position = at + Vector3.up * 1.8f;
            fill.type = LightType.Point;
            fill.range = 5f;
            fill.intensity = 1.1f;
            var rt = new RenderTexture(960, 540, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(tex);
            Destroy(fill.gameObject);
            Destroy(go);
        }
    }
}
