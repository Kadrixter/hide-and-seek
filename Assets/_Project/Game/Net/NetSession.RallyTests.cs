using System;
using System.Collections;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testrally &lt;dir&gt; (host -forcehider -autostart -autostartplayers 2 -hidetime N + a client): the rally RC car
    /// from six sides and on the shelf, the cockpit camera (straight / steering / a medkit on the roof) and the
    /// minimap following the car. "[RallyTest] PASS / FAIL".
    /// </summary>
    public partial class NetSession
    {
        private int _rallyFails;

        private void StartRallyTests(Func<string, bool> has)
        {
            if (has("-testrally")) StartCoroutine(RallyTest(ArgOr("-testrally", "")));
        }

        private void RallyCheck(bool ok, string what)
        {
            if (!ok) _rallyFails++;
            Debug.Log($"[RallyTest] {(ok ? "ok" : "FAIL")}: {what}");
        }

        private IEnumerator RallyTest(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (!me.IsHider || Manager == null || !Manager.IsServer) { Debug.Log("[RallyTest] need host hider"); yield break; }
            mm.YarisRound.Value = false; // the rally body even when the round rolled the Yaris easter egg
            yield return new WaitForSeconds(0.5f);

            // ---- the shelf
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            Vector3 shelfCar = RcCarShelf.CarPos(0);
            TestLight(shelfCar + Vector3.up * 1.2f, 4f, 1.2f);
            yield return new WaitForSeconds(0.5f);
            CamShot(dir, "rally_shelf", shelfCar + shelfRot * new Vector3(0.25f, 0.25f, -0.6f), shelfCar + Vector3.up * 0.08f);

            // ---- on the floor: six sides
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(-0.3f, 0f, -1.15f), RcCarShelf.Yaw);
            yield return new WaitForSeconds(0.8f);
            mm.ServerRcPickup(me, 0);
            if (!FindRun(0, out var spot, out float yaw, out float len)) { Debug.Log("[RallyTest] no straight found"); yield break; }
            TestLight(spot + Vector3.up * 1.6f, 6f, 1.4f);
            yield return PlaceCar(me, spot, yaw);
            var car = RcCar.Get(0);
            Vector3 c = car.transform.position + Vector3.up * 0.09f;
            Quaternion r = Quaternion.Euler(0f, car.Yaw, 0f);
            var views = new (string name, Vector3 off)[]
            {
                ("rally_front34", new Vector3(0.45f, 0.22f, 0.55f)), ("rally_rear34", new Vector3(-0.45f, 0.24f, -0.55f)),
                ("rally_side", new Vector3(0.75f, 0.06f, 0f)), ("rally_front", new Vector3(0f, 0.1f, 0.7f)),
                ("rally_rear", new Vector3(0f, 0.14f, -0.7f)), ("rally_top", new Vector3(0.2f, 0.75f, 0.1f)),
            };
            foreach (var v in views)
            {
                CamShot(dir, v.name, c + r * v.off, c);
                yield return null;
            }
            RallyCheck(car.Parts.SteeringWheel != null && car.Parts.Body.GetComponent<MeshFilter>().sharedMesh.name == "Rally_Body", "rally body + steering wheel on the car");

            // ---- cockpit camera
            yield return new WaitForSeconds(0.5f);
            RallyCheck(RcCarView.Active != null, $"car camera active (eye {(RcCarView.Active != null ? car.Parts.Root.InverseTransformPoint(RcCarView.Active.transform.position).ToString("F3") : "-")})");
            Shot(dir, "cockpit_still");
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.5f);
            yield return new WaitForSeconds(0.8f);
            Shot(dir, "cockpit_driving");
            LocalPlayerController.TestRcInput = new Vector2(1f, 0.35f);
            yield return new WaitForSeconds(0.5f);
            float wheelAng = car.Parts.SteeringWheel.localEulerAngles.z;
            Shot(dir, "cockpit_steering");
            RallyCheck(Mathf.Abs(Mathf.DeltaAngle(0f, wheelAng)) > 20f, $"steering wheel turned {Mathf.DeltaAngle(0f, wheelAng):0} deg (front wheels {car.SteerAngle:0.0})");
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.8f);

            // ---- the minimap follows the car
            Vector3 body = me.transform.position;
            RallyCheck(Minimap.FollowsCar && Vector3.Distance(car.transform.position, body) > 0.5f, $"minimap follows the car (car {Vector3.Distance(car.transform.position, body):0.0} m from the player)");

            // ---- lights (RcCarLights): the test lamps off, driving (low beams + pod), braking, reversing
            foreach (var tl in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (tl != null && tl.name == "TestLight") tl.enabled = false;
            Quaternion R() => Quaternion.Euler(0f, car.Yaw, 0f);
            Vector3 C() => car.transform.position + Vector3.up * 0.08f;
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.22f);
            yield return new WaitForSeconds(0.7f);
            CamShot(dir, "lights_front", C() + R() * new Vector3(0.28f, 0.13f, 0.75f), C() + R() * new Vector3(0f, 0f, 0.1f));
            CamShot(dir, "lights_beam", C() + R() * new Vector3(0.5f, 0.55f, -0.9f), C() + R() * new Vector3(0f, -0.08f, 1.6f));
            LocalPlayerController.TestRcInput = new Vector2(0f, -1f);
            yield return new WaitForSeconds(0.12f);
            CamShot(dir, "lights_brake", C() + R() * new Vector3(-0.3f, 0.14f, -0.75f), C());
            yield return new WaitForSeconds(0.9f);
            CamShot(dir, "lights_reverse", C() + R() * new Vector3(-0.3f, 0.14f, -0.75f), C());
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.22f);
            yield return new WaitForSeconds(0.5f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            foreach (var tl in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tl != null && tl.name == "TestLight") tl.enabled = true;
            CamShot(dir, "detail_front", C() + R() * new Vector3(0.16f, 0.06f, 0.42f), C() + R() * new Vector3(0f, 0f, 0.15f));
            CamShot(dir, "detail_rear", C() + R() * new Vector3(-0.16f, 0.07f, -0.42f), C() + R() * new Vector3(0f, 0f, -0.15f));
            CamShot(dir, "detail_side", C() + R() * new Vector3(0.42f, 0.05f, 0.02f), C());

            // ---- a medkit on the roof does not block the view
            Vector3 Fwd() => Quaternion.Euler(0f, car.Yaw, 0f) * Vector3.forward;
            Vector3 at = car.transform.position + Fwd() * 1.3f;
            at.y = MapInfo.FloorUnder(at.y + 0.3f);
            mm.TestSpawnDrop(HotbarItem.Medkit, at);
            yield return new WaitForSeconds(2f);
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.45f);
            float t0 = Time.time;
            while (mm.RcCargoOf(0) == HotbarItem.None && Time.time - t0 < 5f) yield return null;
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            RallyCheck(mm.RcCargoOf(0) == HotbarItem.Medkit, $"medkit on the roof: {mm.RcCargoOf(0)}");
            Shot(dir, "cockpit_with_medkit");
            var cr = car.transform.position + Vector3.up * 0.09f;
            CamShot(dir, "rally_with_medkit", cr + Quaternion.Euler(0f, car.Yaw, 0f) * new Vector3(0.45f, 0.25f, 0.45f), cr);
            LocalPlayerController.TestRcActive = false;
            Debug.Log($"[RallyTest] {(_rallyFails == 0 ? "PASS" : "FAIL")}: {_rallyFails} fail(s)");
        }
    }
}
