using System;
using System.Collections;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The GT86 hero car on level -1 as the HUD names it (-testgt86 &lt;dir&gt; on the host, seeker via -forceseeker): finds the car, checks it
    /// is a solid alarm car (box colliders, a ray from the aisle stops on it, a player capsule cannot stand inside it,
    /// hazard-light anchors present) and takes player-view shots lit only by the flashlight (front, 3/4, side, rear, the big wing)
    /// plus a few close-ups with a test light. Logs: [Gt86Test] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static void G86Log(string s) => Debug.Log("[Gt86Test] " + s);

        private void StartGt86Tests(Func<string, bool> has)
        {
            if (has("-testgt86")) StartCoroutine(Gt86Test(ArgOr("-testgt86", "")));
            if (has("-testlevelsigns")) StartCoroutine(LevelSignTest(ArgOr("-testlevelsigns", ""))); // NetSession.LevelSignTests.cs
        }

        private IEnumerator Gt86Test(string dir)
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            if (!Manager.IsServer || !me.IsSeeker) { G86Log("need the host as seeker (-forceseeker) - FAIL"); yield break; }
            Transform car = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                if (t.name.StartsWith("Car_") && t.name.Contains("_ToyotaGt86_")) { car = t; break; }
            if (car == null) { G86Log("no Car_###_ToyotaGt86 in the scene - FAIL"); yield break; }
            var boxes = car.GetComponents<BoxCollider>();
            int blinkers = 0;
            foreach (Transform c in car) if (c.name == "Blinker") blinkers++;
            Vector3 p = car.position, f = car.forward, r = car.right;
            G86Log($"{car.name} at {p} yaw {car.eulerAngles.y:0} level {LevelLighting.LevelAt(p.y)}, {boxes.Length} box colliders, {blinkers} blinkers, CarAlarms cars {(CarAlarms.Instance != null ? CarAlarms.Instance.CarCount : -1)} -> {Pf(boxes.Length > 0 && blinkers >= 4 && LevelLighting.LevelAt(p.y) == 1)} (expect HUD level -1)");

            // solid: a ray from the aisle hits the car's own collider, a standing capsule overlaps it
            bool rayHit = Physics.Raycast(p + f * 4f + Vector3.up * 0.6f, -f, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore) && hit.transform == car;
            bool capsule = Physics.CheckCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.4f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
            G86Log($"ray from the aisle hits {(hit.transform != null ? hit.transform.name : "nothing")} at {hit.distance:0.00} m, capsule inside blocked {capsule} -> {Pf(rayHit && capsule)}");

            if (me.HasFlashlight && me.Flashlight != null) me.Flashlight.SetOn(true, false);
            var views = new (string name, Vector3 at, Vector3 look)[]
            {
                ("a_front", p + f * 5.5f, p + Vector3.up * 0.5f),
                ("b_front34", p + f * 4.2f + r * 3.2f, p + Vector3.up * 0.5f),
                ("c_side", p - r * 4.5f + f * 0.3f, p + Vector3.up * 0.5f),
                ("d_rear34", p - f * 1.8f - r * 2.6f, p - f * 1.6f + Vector3.up * 0.6f),
                ("e_front_close", p + f * 3.0f - r * 1.0f, p + f * 1.8f + Vector3.up * 0.55f),
                ("f_cabin", p - r * 1.9f - f * 0.3f, p + Vector3.up * 0.8f - f * 0.3f),
                ("g_wing", p - f * 4.6f - r * 2.2f, p - f * 2.1f + Vector3.up * 1.0f),
            };
            foreach (var v in views)
            {
                me.TeleportLocal(new Vector3(v.at.x, p.y + 0.05f, v.at.z), 0f);
                yield return new WaitForSeconds(0.8f);
                FlAim(me, v.look);
                if (me.HasFlashlight && me.Flashlight != null && !me.Flashlight.IsOn) me.Flashlight.SetOn(true, false);
                yield return new WaitForSeconds(0.7f);
                Shot(dir, "gt86_" + v.name);
                yield return new WaitForSeconds(0.4f);
            }
            // close-ups with a soft test light (no flashlight dependency)
            ItemLitShot(dir, "gt86_x_nose", p + f * 3.2f + r * 1.4f + Vector3.up * 1.0f, p + f * 1.9f + Vector3.up * 0.5f);
            ItemLitShot(dir, "gt86_x_tail", p - f * 3.6f - r * 1.6f + Vector3.up * 1.1f, p - f * 2.0f + Vector3.up * 0.6f);
            ItemLitShot(dir, "gt86_x_wing", p - f * 3.9f + r * 1.6f + Vector3.up * 1.7f, p - f * 2.2f + Vector3.up * 1.0f);
            ItemLitShot(dir, "gt86_x_vent", p + f * 0.4f - r * 2.0f + Vector3.up * 0.8f, p + f * 0.7f - r * 0.85f + Vector3.up * 0.55f);
            // the other winged hero cars, rear 3/4 with a test light
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.name.StartsWith("Car_")) continue;
                string model = null;
                foreach (var m in new[] { "MazdaRx7", "MazdaRx8", "LamborghiniDiablo", "BmwE30" })
                    if (t.name.Contains("_" + m + "_")) model = m;
                if (model == null) continue;
                Vector3 q = t.position, qf = t.forward, qr = t.right;
                ItemLitShot(dir, "wing_" + t.name, q - qf * 4.4f + qr * 2.0f + Vector3.up * 1.7f, q - qf * 1.6f + Vector3.up * 0.8f);
                G86Log($"wing shot {t.name} at {q}");
            }
            yield return new WaitForSeconds(1f);
            G86Log("done");
        }
    }
}
