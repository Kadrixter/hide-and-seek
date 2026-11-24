using System;
using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testrccargo &lt;dir&gt; (host -forcehider -autostart -autostartplayers 2 -hidetime N, + a seeker client with
    /// -rccargowatch): minimap markers of the fuse box / fuses (in and out of the 10 m range), the RC car driving into a
    /// medkit (onto the roof, a second item is ignored), E next to the car takes it, a car picked up with a load hands the
    /// load over. "[RcCargoTest] PASS / FAIL".
    /// </summary>
    public partial class NetSession
    {
        private int _cargoFails;

        private void StartRcCargoTests(Func<string, bool> has)
        {
            if (has("-testrccargo")) StartCoroutine(RcCargoTest(ArgOr("-testrccargo", "")));
            if (has("-rccargowatch")) StartCoroutine(RcCargoWatch());
        }

        private void CargoCheck(bool ok, string what)
        {
            if (!ok) _cargoFails++;
            Debug.Log($"[RcCargoTest] {(ok ? "ok" : "FAIL")}: {what}");
        }

        private IEnumerator RcCargoTest(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (!me.IsHider || Manager == null || !Manager.IsServer) { Debug.Log("[RcCargoTest] need host hider"); yield break; }

            // ---- minimap: stand 6 m from a fuse, then far from all of them
            int fuseIdx = -1;
            for (int i = 0; i < mm.Drops.Count; i++) if (FuseItems.Is(mm.Drops[i].Kind)) { fuseIdx = i; break; }
            CargoCheck(fuseIdx >= 0, "a fuse lies on the map");
            if (fuseIdx >= 0)
            {
                Vector3 f = mm.DropPosition(mm.Drops[fuseIdx]);
                Vector3 from = f + new Vector3(0.9f, 0f, 0.3f);
                from.y = MapInfo.FloorUnder(f.y) + 0.05f;
                me.Motor.Teleport(from, 0f, 0f);
                yield return new WaitForSeconds(1.2f);
                int expectF = 0, level = HideAndSeek.Map.LevelLighting.LevelAt(from.y + 0.3f), expectB = 0;
                foreach (var d in mm.Drops)
                {
                    if (!FuseItems.Is(d.Kind)) continue;
                    Vector3 w = mm.DropPosition(d);
                    if (Mathf.Abs(w.y - me.transform.position.y) <= 1.7f && new Vector2(w.x - me.transform.position.x, w.z - me.transform.position.z).magnitude <= Minimap.FuseRange) expectF++;
                }
                for (int b = 0; b < FuseTuning.Boxes; b++) if (mm.FuseBoxes.Value[b].Placed && mm.FuseBoxes.Value[b].Level == level) expectB++;
                CargoCheck(Minimap.FuseMarks == expectF && expectF >= 1, $"near a fuse: {Minimap.FuseMarks} fuse marker(s), expected {expectF}");
                CargoCheck(Minimap.BoxMarks == expectB && expectB >= 1, $"fuse box markers on level {level}: {Minimap.BoxMarks}, expected {expectB}");
                // Screenshot from ~4 m (right next to it the player's arrow covers the marker).
                for (int k = 0; k < 8; k++)
                {
                    Vector3 dir4 = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                    Vector3 p4 = new Vector3(f.x, from.y, f.z) + dir4 * 4f;
                    if (Physics.CheckCapsule(p4 + Vector3.up * 0.4f, p4 + Vector3.up * 1.5f, 0.3f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Linecast(new Vector3(f.x, from.y + 1f, f.z), p4 + Vector3.up, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) continue;
                    me.Motor.Teleport(p4, Quaternion.LookRotation(-dir4).eulerAngles.y, 0f);
                    break;
                }
                yield return new WaitForSeconds(1.2f);
                Debug.Log($"[RcCargoTest] at 4 m: fuse markers {Minimap.FuseMarks}, box markers {Minimap.BoxMarks}");
                Shot(dir, "minimap_near_fuse");
                yield return new WaitForSeconds(0.4f);
            }

            // ---- RC car: a medkit on the floor ahead
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(-0.3f, 0f, -1.15f), RcCarShelf.Yaw);
            yield return new WaitForSeconds(0.8f);
            mm.ServerRcPickup(me, 0);
            if (!FindRun(0, out var spot, out float yaw, out float len)) { Debug.Log("[RcCargoTest] no straight found"); yield break; }
            TestLight(spot + Vector3.up * 2.6f, 9f, 0.9f);
            yield return PlaceCar(me, spot, yaw);
            var car = RcCar.Get(0);
            Vector3 Fwd() => Quaternion.Euler(0f, car.Yaw, 0f) * Vector3.forward;
            Vector3 Floor(Vector3 p) { p.y = MapInfo.FloorUnder(p.y + 0.3f); return p; }
            Vector3 medAt = Floor(car.transform.position + Fwd() * 1.6f);
            mm.TestSpawnDrop(HotbarItem.Medkit, medAt);
            yield return new WaitForSeconds(2f);
            int drops0 = mm.Drops.Count;
            Debug.Log($"[RcCargoTest] car at {car.transform.position} yaw {car.Yaw:0}, medkit at {medAt}, drops {drops0}, ctrl={me.RcControlling}");
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.45f);
            float t0 = Time.time;
            while (mm.RcCargoOf(0) == HotbarItem.None && Time.time - t0 < 5f) yield return null;
            CargoCheck(mm.RcCargoOf(0) == HotbarItem.Medkit && mm.Drops.Count == drops0 - 1, $"car drove into the medkit: cargo={mm.RcCargoOf(0)} after {Time.time - t0:0.00}s, drops {drops0}->{mm.Drops.Count}");
            // A second item in the way is left alone (one load per car).
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            Vector3 syrAt = Floor(car.transform.position + Fwd() * 1.3f);
            mm.TestSpawnDrop(HotbarItem.Syringe, syrAt);
            yield return new WaitForSeconds(2f);
            int drops1 = mm.Drops.Count;
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.45f);
            yield return new WaitForSeconds(1.6f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            CargoCheck(mm.RcCargoOf(0) == HotbarItem.Medkit && mm.Drops.Count == drops1, $"full car ignores the syringe: cargo={mm.RcCargoOf(0)}, drops {drops1}->{mm.Drops.Count}");
            var cargoModel = car.Parts.Root.Find("Cargo");
            CargoCheck(cargoModel != null && cargoModel.gameObject.activeInHierarchy, "medkit model rides on the roof");
            Vector3 side = Quaternion.Euler(0f, 90f, 0f) * Fwd();
            CamShot(dir, "cargo_side", car.transform.position + side * 0.75f + Fwd() * 0.35f + Vector3.up * 0.38f, car.transform.position + Vector3.up * 0.1f);

            // ---- park, walk up to it, E takes the medkit
            LocalPlayerController.TestRcActive = false;
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.8f);
            Vector3 c = car.transform.position;
            me.Motor.Teleport(c - Fwd() * 1.1f, car.Yaw);
            yield return new WaitForSeconds(0.3f);
            Vector3 look = c - (me.transform.position + Vector3.up * 1.62f);
            me.Motor.SetLook(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.8f);
            var lpc = LocalPlayerController.Instance;
            CargoCheck(lpc.Prompt == PromptKind.RcCargo, $"prompt next to the car: {lpc.Prompt} '{lpc.PromptText}'");
            Shot(dir, "cargo_prompt");
            yield return new WaitForSeconds(0.3f);
            int med0 = me.MedkitCount.Value;
            LocalPlayerController.TestPressE = true;
            yield return new WaitForSeconds(0.8f);
            CargoCheck(mm.RcCargoOf(0) == HotbarItem.None && me.MedkitCount.Value == med0 + 1, $"E took it: cargo={mm.RcCargoOf(0)}, medkits {med0}->{me.MedkitCount.Value}");
            CargoCheck(lpc.Prompt != PromptKind.RcCargo, $"no load prompt any more: {lpc.Prompt} '{lpc.PromptText}'");

            // ---- a syringe ahead: drive into it, park, pick the car up -> the syringe comes with it
            mm.TestSpawnDrop(HotbarItem.Syringe, Floor(car.transform.position + Fwd() * 1.4f));
            yield return new WaitForSeconds(2f);
            mm.ServerRcResume(me, 0);
            yield return new WaitForSeconds(1.2f);
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.45f);
            t0 = Time.time;
            while (mm.RcCargoOf(0) == HotbarItem.None && Time.time - t0 < 5f) yield return null;
            LocalPlayerController.TestRcInput = Vector2.zero;
            CargoCheck(mm.RcCargoOf(0) == HotbarItem.Syringe, $"second run picked the syringe: {mm.RcCargoOf(0)}");
            yield return new WaitForSeconds(0.5f);
            LocalPlayerController.TestRcActive = false;
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.8f);
            me.Motor.Teleport(car.transform.position - Fwd() * 0.9f, car.Yaw);
            yield return new WaitForSeconds(0.5f);
            int syr0 = me.SyringeCount.Value;
            mm.ServerRcPickup(me, 0);
            yield return new WaitForSeconds(0.6f);
            CargoCheck(mm.RcSlot(0).Mode == RcMode.Carried && mm.RcCargoOf(0) == HotbarItem.None && me.SyringeCount.Value == syr0 + 1,
                $"car picked up with its load: mode={mm.RcSlot(0).Mode} cargo={mm.RcCargoOf(0)} syringes {syr0}->{me.SyringeCount.Value}");
            Debug.Log($"[RcCargoTest] {(_cargoFails == 0 ? "PASS" : "FAIL")}: {_cargoFails} fail(s), grabs {mm.RcCargoGrabs}");
        }

        private IEnumerator RcCargoWatch()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var last = HotbarItem.None;
            float t0 = Time.time;
            while (Time.time - t0 < 90f)
            {
                var now = mm.RcCargoOf(0);
                if (now != last)
                {
                    yield return new WaitForSeconds(0.4f);
                    var car = RcCar.Peek(0);
                    var model = car != null && car.Parts != null ? car.Parts.Root.Find("Cargo") : null;
                    Debug.Log($"[RcCargoTest] watch: cargo {last} -> {now}, car view {(car != null ? car.View.ToString() : "-")}, model {(model != null ? model.gameObject.activeInHierarchy.ToString() : "none")}");
                    last = now;
                }
                yield return null;
            }
        }
    }
}
