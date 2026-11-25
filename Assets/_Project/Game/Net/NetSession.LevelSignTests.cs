using System.Collections;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Level markings vs the HUD (-testlevelsigns &lt;dir&gt; on the host, seeker via -forceseeker): on each of the three
    /// levels takes player-view shots (minimap "POZIOM" label in the corner) of the view out of the central elevator,
    /// a pillar sign close up, the elevator doors with their level sign and the cabin's button panel. Logs the elevator
    /// floor names and the level the HUD reports. Logs: [LevelSignTest] ...
    /// </summary>
    public partial class NetSession
    {
        private static void LsLog(string s) => Debug.Log("[LevelSignTest] " + s);

        private IEnumerator LevelSignTest(string dir)
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            if (!Manager.IsServer || !me.IsSeeker) { LsLog("need the host as seeker (-forceseeker) - FAIL"); yield break; }
            var es = ElevatorSystem.Instance;
            if (es != null && es.Cars.Count > 0)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var fl in es.Cars[0].Floors) names.Add($"{fl.Name}@{fl.Y:0.0}");
                LsLog("elevator floors (top first): " + string.Join(", ", names));
            }
            const float ex = -2.6f;
            for (int lv = 0; lv < 3; lv++)
            {
                float y = -LevelLighting.LevelHeight * lv;
                string tag = "lv" + (lv == 0 ? "0" : "m" + lv);
                var lamp = LsLamp(new Vector3(-4f, y + 2.5f, -6.5f), 11f, 1.6f);
                yield return LsSnap(dir, me, tag + "_a_elevator_exit", new Vector3(ex, y, -2.6f), new Vector3(ex - 1.5f, y + 1.6f, -8.1f));
                yield return LsSnap(dir, me, tag + "_b_pillar", new Vector3(-7.5f, y, -10.9f), new Vector3(-7.5f, y + 1.75f, -8.4f));
                yield return LsSnap(dir, me, tag + "_c_elevator_doors", new Vector3(ex, y, -4.8f), new Vector3(ex, y + 2.1f, -1.4f));
                Destroy(lamp.gameObject);
                var cabin = LsLamp(new Vector3(ex, y + 2.1f, 0f), 3.5f, 1.2f);
                yield return LsSnap(dir, me, tag + "_d_elevator_panel", new Vector3(ex - 0.45f, y, 0.35f), new Vector3(ex + 0.99f, y + 1.25f, 0f));
                Destroy(cabin.gameObject);
                LsLog($"{tag}: player y {me.transform.position.y:0.00} -> level index {LevelLighting.LevelAt(me.transform.position.y)} (HUD POZIOM {(lv == 0 ? "0" : "-" + lv)})");
            }
            LsLog("done");
        }

        private static Light LsLamp(Vector3 pos, float range, float intensity)
        {
            var l = new GameObject("TestLight").AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.transform.position = pos;
            return l;
        }

        private IEnumerator LsSnap(string dir, NetPlayer me, string name, Vector3 at, Vector3 look)
        {
            me.TeleportLocal(at + Vector3.up * 0.05f, 0f);
            yield return new WaitForSeconds(0.9f);
            FlAim(me, look);
            if (me.HasFlashlight && me.Flashlight != null && !me.Flashlight.IsOn) me.Flashlight.SetOn(true, false);
            yield return new WaitForSeconds(0.8f);
            Shot(dir, name);
            yield return new WaitForSeconds(0.4f);
        }
    }
}
