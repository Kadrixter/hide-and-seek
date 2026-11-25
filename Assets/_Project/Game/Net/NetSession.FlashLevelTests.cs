using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Flashlights between the garage levels (-testflashlevel &lt;dir&gt; on all three: host = seeker -forceseeker
    /// -autostart -autostartplayers 3, clients "Klient" = light holder (keeps the flashlight on and aims it at "Klient2")
    /// and "Klient2" = target standing next to the host). Cases seen from the host camera: same level (shown), holder
    /// directly below / above / two levels below (hidden), at a ramp shaft both near it (shown) and far from it (hidden).
    /// For each: whether the holder's spot light and beam were on while the host camera rendered, and the screen
    /// brightness with the holder's light on minus off, with shadows on and off (the old leak needs no shadows). Then
    /// "Klient2" is killed and, as a spectator, checks the same from the orbit camera of every target.
    /// <c>-noflashcull</c> on the host = the old behaviour, for comparison. Logs: [FlashLevelTest] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static void FlLog(string s) => Debug.Log("[FlashLevelTest] " + s);

        private IEnumerator FlashLevelTest(string dir)
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            if (Manager.IsServer) yield return FlHost(dir, me);
            else if (me.Nick == "Klient") yield return FlHolder();
            else yield return FlSpectator(dir, me);
        }

        private IEnumerator FlHost(string dir, NetPlayer me)
        {
            var holder = ByNick("Klient");
            var target = ByNick("Klient2");
            var ll = LevelLighting.Instance;
            if (!me.IsSeeker || holder == null || target == null || ll == null) { FlLog("need host seeker + clients Klient and Klient2 in the garage"); yield break; }
            FlLog($"culling {(FlashlightLevelCulling.Off ? "OFF (-noflashcull)" : "on")}, shadows {QualitySettings.shadows} dist {QualitySettings.shadowDistance:0}");
            if (!FlColumn(ll, out var p0, out var p1, out var p2)) { FlLog("no stacked road spots on 3 levels - FAIL"); yield break; }
            Vector3 h0 = FlNear(0, p0 + new Vector3(5f, 0f, 0f)), h1 = FlNear(1, p1 + new Vector3(5f, 0f, 0f));
            FlLog($"column {p0} / {p1} / {p2}");

            yield return FlCase(dir, "a_same_level", me, holder, target, h0, p0, FlNear(0, p0 + new Vector3(-4f, 0f, 0f)), true);
            yield return FlCase(dir, "b_below", me, holder, target, h0, p0, p1, false);
            yield return FlCase(dir, "c_above", me, holder, target, h1, p1, p0, false);
            yield return FlCase(dir, "d_two_below", me, holder, target, h0, p0, p2, false);

            // Ramp shaft: host on level 0 at its edge, holder on level -1 next to it (linked) or far along the lane (not).
            var hole = ll.Holes[0];
            Vector3 r0 = FlNearRect(0, hole, 0f), r1 = FlNearRect(1, hole, 0f), rFar = FlNearRect(1, hole, 36f);
            yield return FlCase(dir, "e_ramp_near", me, holder, target, r0, FlNear(0, r0 + new Vector3(0f, 0f, 3f)), r1, true);
            yield return FlCase(dir, "f_ramp_far", me, holder, target, r0, FlNear(0, r0 + new Vector3(0f, 0f, 3f)), rFar, false);

            // Spectator: Klient2 dies and watches the host (level 0, holder below: hidden) and the holder (shown).
            me.TeleportLocal(h0, 0f);
            holder.TeleportRpc(p1, 0f);
            MatchManager.Instance.TestKill(target);
            FlLog("PHASE spectator (Klient2 killed)");
            yield return new WaitForSeconds(16f);
            FlLog("host done");
        }

        private IEnumerator FlCase(string dir, string name, NetPlayer me, NetPlayer holder, NetPlayer target, Vector3 hostAt, Vector3 targetAt, Vector3 lightAt, bool expectShown)
        {
            me.TeleportLocal(hostAt, 0f);
            target.TeleportRpc(targetAt, 0f);
            holder.TeleportRpc(lightAt, 0f);
            yield return new WaitForSeconds(2.4f);
            FlAim(me, target.ChestPosition);
            yield return new WaitForSeconds(0.8f);
            var fl = holder.Flashlight;
            Vector3 lp = fl != null && fl.SpotLight != null ? fl.SpotLight.transform.position : holder.EyePosition;
            Vector3 cp = me.Camera.transform.position;
            yield return FlRecord(me.Camera, holder, 1f);
            bool shown = _flLit * 2 > _flFrames, beam = _flBeamLit * 2 > _flFrames;
            var dust = fl != null ? fl.GetComponentInChildren<ParticleSystemRenderer>(true) : null;
            var mpb = new MaterialPropertyBlock();
            if (dust != null) dust.GetPropertyBlock(mpb);
            Vector4 band = mpb.GetVector("_FlashSlab");
            int llv = LevelLighting.LevelAt(lp.y);
            bool inHole = LevelLighting.Instance.LevelBits(lp) != 1 << LevelLighting.LayerOf(llv);
            bool bandOk = FlashlightLevelCulling.Off || (inHole ? band.y > 1000f : Mathf.Abs(band.x + LevelLighting.LevelHeight * llv) < 0.1f && band.y < -LevelLighting.LevelHeight * llv + 3.1f);
            FlLog($"{name}: dust height band {band.x:0.00}..{band.y:0.00} (light level {llv}, in opening {inHole}) -> {Pf(bandOk)}");
            FlLog($"{name}: cam level {LevelLighting.LevelAt(cp.y)} light level {LevelLighting.LevelAt(lp.y)} (dy {lp.y - cp.y:0.0}, flat {Vector2.Distance(new Vector2(lp.x, lp.z), new Vector2(cp.x, cp.z)):0.0} m) holder on={holder.FlashOn.Value} | rendered light {_flLit}/{_flFrames} beam {_flBeamLit}/{_flFrames} expect {(expectShown ? "shown" : "hidden")} -> {Pf(holder.FlashOn.Value && shown == expectShown && beam == expectShown)}");

            float[] lum = new float[4];
            Vector3 sp = me.Camera.WorldToScreenPoint(target.ChestPosition);
            Vector2 box = sp.z > 0f ? new Vector2(sp.x, sp.y) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            yield return FlLum(holder, lum, 0, box);
            Shot(dir, "h_" + name);
            yield return new WaitForSeconds(0.3f);
            var keep = QualitySettings.shadows;
            QualitySettings.shadows = ShadowQuality.Disable;
            yield return null;
            yield return FlLum(holder, lum, 2, box);
            Shot(dir, "h_" + name + "_noshadow");
            yield return new WaitForSeconds(0.3f);
            QualitySettings.shadows = keep;
            float dOn = lum[0] - lum[1], dOff = lum[2] - lum[3];
            bool leakOk = expectShown || (Mathf.Abs(dOn) < 1f && Mathf.Abs(dOff) < 1f);
            // around the target player (Klient2), where a leaking light shows
            FlLog($"{name}: brightness at the target with holder light - without: shadows {dOn:+0.000;-0.000} / no shadows {dOff:+0.000;-0.000} (0-255 scale) -> {(expectShown ? "info" : Pf(leakOk))}");
        }

        /// <summary>Holder side: flashlight on, aimed at Klient2's chest, until the test ends.</summary>
        private IEnumerator FlHolder()
        {
            var me = NetPlayer.Local;
            float end = Time.time + 150f;
            while (Time.time < end)
            {
                if (me.Flashlight != null && !me.Flashlight.IsOn && me.HasFlashlight && me.Life.Value == LifeState.Alive) me.Flashlight.SetOn(true, false);
                var t = ByNick("Klient2");
                if (t != null) FlAim(me, t.ChestPosition);
                yield return null;
            }
        }

        /// <summary>Klient2 side: once dead, every spectated target for a while; the holder's light is shown only on its level.</summary>
        private IEnumerator FlSpectator(string dir, NetPlayer me)
        {
            float t0 = Time.time;
            while (me.Life.Value != LifeState.Dead && Time.time - t0 < 90f) yield return null;
            if (me.Life.Value != LifeState.Dead) { FlLog("client never died - FAIL"); yield break; }
            var holder = ByNick("Klient");
            yield return new WaitForSeconds(3.5f);
            for (int k = 0; k < 3; k++)
            {
                if (k > 0) { SpectatorCamera.TestNext = true; yield return new WaitForSeconds(1.6f); }
                var cam = Camera.main;
                var who = GameHud.Spectated;
                if (cam == null || holder == null || holder.Flashlight == null) { FlLog("client: no camera / holder - FAIL"); yield break; }
                yield return FlRecord(cam, holder, 1f);
                Vector3 cp = cam.transform.position, lp = holder.Flashlight.SpotLight.transform.position;
                bool shown = _flLit * 2 > _flFrames;
                bool expect = LevelLighting.LevelAt(cp.y) == LevelLighting.LevelAt(lp.y);
                FlLog($"client spectating {(who != null ? who.Nick : "-")}: cam {cam.name} level {LevelLighting.LevelAt(cp.y)}, light level {LevelLighting.LevelAt(lp.y)} on={holder.FlashOn.Value} | rendered {_flLit}/{_flFrames} expect {(expect ? "shown" : "hidden")} -> {Pf(shown == expect)}");
                Shot(dir, "c2_spectate_" + k + "_" + (who != null ? who.Nick : "none"));
                yield return new WaitForSeconds(0.4f);
            }
        }
    }
}
