using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public partial class FuseTests
    {
        private IEnumerator HostTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (!me.IsHider || !mm.IsServer) { Log("need the host as a hider (-forcehider) + a seeker client"); yield break; }
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = p;
            Log($"boxes:{Boxes(mm)} stage={mm.Escape.Value}");
            int good = mm.FuseGoodBox, bad = -1;
            for (int i = 0; i < FuseTuning.Boxes; i++) if (mm.FuseBoxes.Value[i].Placed && i != good && bad < 0) bad = i;
            if (good < 0 || bad < 0) { Fail("no right / wrong box"); yield break; }
            CheckFuses(mm);

            // 1. Close-ups of every box (test light), one in the real dark; the HUD line.
            for (int i = 0; i < FuseTuning.Boxes; i++)
            {
                var b = mm.FuseBoxes.Value[i];
                if (!b.Placed) continue;
                yield return StandAt(me, i, 1, 0f);
                if (i == 0) { Screen($"01_box{i}_dark_fp"); yield return new WaitForSeconds(0.3f); }
                var l = TestLight(b.World(new Vector3(0.4f, 2.2f, 1.6f)), 6f, 1.6f);
                yield return new WaitForSeconds(0.3f);
                Screen($"01_box{i}_fp");
                yield return Side($"02_box{i}_side", () => b.World(new Vector3(0f, 1.3f, 0.1f)), () => b.Rot * new Vector3(-0.55f, 0.15f, 1.25f), 1, 0.2f);
                Destroy(l);
            }

            // 2. The four fuses of a wrong box from the floor (the first one filmed lying there).
            var wb = mm.FuseBoxes.Value[bad];
            int[] wrong = { wb.S0, wb.S1, wb.S2, wb.S3 };
            for (int k = 0; k < 4; k++)
            {
                GameObject l = null;
                if (k == 0)
                {
                    Vector3 fp = FuseDropPos(mm, wrong[k]);
                    l = TestLight(fp + Vector3.up * 1.8f, 5f, 1.4f);
                    yield return Side("03_fuse_floor_close", () => fp, () => new Vector3(0.35f, 0.45f, -0.3f), 1, 0.3f, 40f);
                }
                yield return TakeFuse(me, wrong[k], k == 0 ? "03_fuse_floor_fp" : null);
                if (l != null) Destroy(l);
            }
            Log($"carrying mask={me.FuseMask.Value} inv={me.Inv.Value} hud='{EscapeHud.LineText}'");
            Hotbar.TestSelect = Inventory.SlotOf(me, FuseItems.Of(wrong[0]));
            var hl = TestLight(me.transform.position + Vector3.up * 2.4f, 5f, 1.2f);
            yield return new WaitForSeconds(1.2f);
            Log($"in hand: {me.HandItem} hint='{Hotbar.Hint(me, me.HandItem)}'");
            Screen("04_fuse_in_hand_hud");
            yield return new WaitForSeconds(0.3f);
            Destroy(hl);

            // 3. Fill the wrong box: the level's lights come on.
            int lv = wb.Level;
            Vector3 vantage = new Vector3(-30f, MapInfo.FloorY(lv) + 0.05f, -11.75f);
            me.Motor.Teleport(vantage, 90f, -4f);
            yield return new WaitForSeconds(1.2f);
            Screen($"05_level{lv}_dark");
            yield return new WaitForSeconds(0.3f);
            var bl = TestLight(wb.World(new Vector3(0.3f, 2.4f, 1.4f)), 6f, 1.3f);
            yield return FillBox(me, bad, "06_bad");
            Destroy(bl);
            yield return new WaitForSeconds(0.8f);
            wb = mm.FuseBoxes.Value[bad];
            Log($"wrong box full: {Boxes(mm)} lit={wb.Lit} floorLights={FloorLights.IsLit(lv)} stage={mm.Escape.Value}");
            if (!wb.Lit || !FloorLights.IsLit(lv)) Fail("wrong box full but the lights are not on");
            yield return Side("07_bad_full", () => wb.World(new Vector3(0f, 1.3f, 0.1f)), () => wb.Rot * new Vector3(0.6f, 0.2f, 1.4f), 1, 0.2f);
            me.Motor.Teleport(vantage, 90f, -4f);
            yield return new WaitForSeconds(1.5f);
            Screen($"08_level{lv}_lit");
            Log($"camera lit={FloorLights.CameraLit:0.00} ambient={RenderSettings.ambientLight}");
            yield return new WaitForSeconds(0.3f);

            // 4. A pull let go after 0.6 s does nothing; then all four out one by one.
            yield return PullOut(me, bad, 0, 0.6f);
            if (!mm.FuseBoxes.Value[bad].Has(0)) Fail("let-go pull took the fuse out");
            for (int s = 0; s < 4; s++)
            {
                yield return PullOut(me, bad, s, FuseTimeline.OutPop + 0.3f);
                wb = mm.FuseBoxes.Value[bad];
                Log($"pulled socket {s}: {Boxes(mm)} mask={me.FuseMask.Value} floorLights={FloorLights.IsLit(lv)}");
                if (wb.Has(s)) Fail($"socket {s} still full after the pull");
                if (s < 3 && !wb.Lit) Fail("lights off before the box is empty");
            }
            yield return new WaitForSeconds(0.6f);
            if (mm.FuseBoxes.Value[bad].Lit || FloorLights.IsLit(lv)) Fail("box empty but the lights stay on");
            me.Motor.Teleport(vantage, 90f, -4f);
            yield return new WaitForSeconds(1.2f);
            Screen($"09_level{lv}_dark_again");

            // 5. The seeker pulls one out: it falls on the floor in front of the box.
            yield return FillOne(me, bad);
            int sk = -1;
            for (int s = 0; s < 4; s++) if (mm.FuseBoxes.Value[bad].Has(s)) sk = s;
            if (seeker != null && sk >= 0)
            {
                me.Motor.Teleport(wb.World(new Vector3(2.2f, 0.05f, 1.6f)), 0f, 0f);
                var b = mm.FuseBoxes.Value[bad];
                seeker.TeleportRpc(b.World(FuseBoxModel.StandLocal + new Vector3(0f, 0.05f, 0.05f)), Quaternion.LookRotation(-b.Normal).eulerAngles.y);
                yield return new WaitForSeconds(1.2f);
                int before = mm.Drops.Count;
                mm.ServerFuseRemove(seeker, (byte)bad, (byte)sk);
                yield return new WaitForSeconds(FuseTimeline.OutEnd + 2.2f);
                int sym = b.Symbol(sk);
                Vector3 fp = FuseDropPos(mm, sym);
                Log($"seeker pull: socket {sk} full={mm.FuseBoxes.Value[bad].Has(sk)} drops {before}->{mm.Drops.Count} fuse at {fp} ({Vector3.Distance(fp, b.Pos):0.0} m from the box)");
                if (mm.FuseBoxes.Value[bad].Has(sk) || fp == Vector3.zero) Fail("the seeker's pull did not put the fuse on the floor");
                seeker.TeleportRpc(b.World(new Vector3(-6f, 0.05f, 6f)), 0f);
            }
            else Fail("no seeker or nothing to pull");

            // 6. Q: drop the fuses that do not belong in the right box.
            var gb = mm.FuseBoxes.Value[good];
            for (int s = 0; s < FuseSymbols.Count; s++)
            {
                if (!me.HasFuse(s) || gb.SocketOf(s) >= 0) continue;
                Hotbar.TestSelect = Inventory.SlotOf(me, FuseItems.Of(s));
                yield return new WaitForSeconds(0.4f);
                Hotbar.TestDrop = true;
                yield return new WaitForSeconds(1.2f);
                Log($"Q on {FuseSymbols.Name(s)}: has={me.HasFuse(s)} floor={FuseDropPos(mm, s)}");
                if (me.HasFuse(s)) Fail($"Q did not drop {FuseSymbols.Name(s)}");
            }

            // 7. The right box: collect its four, fill it -> locked, stage DotLink.
            int[] right = { gb.S0, gb.S1, gb.S2, gb.S3 };
            foreach (int s in right) if (!me.HasFuse(s)) yield return TakeFuse(me, s);
            var gl = TestLight(gb.World(new Vector3(0.3f, 2.4f, 1.4f)), 6f, 1.4f);
            yield return FillBox(me, good, null);
            yield return new WaitForSeconds(0.6f);
            gb = mm.FuseBoxes.Value[good];
            Log($"right box full: {Boxes(mm)} stage={mm.Escape.Value} hud='{EscapeHud.LineText}'");
            if (!gb.Locked || mm.Escape.Value != EscapeStage.DotLink) Fail("right box full but not locked / stage not DotLink");
            yield return Side("10_good_locked", () => gb.World(new Vector3(0f, 1.3f, 0.1f)), () => gb.Rot * new Vector3(-0.5f, 0.25f, 1.3f), 1, 0.3f);
            yield return StandAt(me, good, 2, 0.4f);
            Log($"at the locked box: prompt {PromptNow()}");
            Screen("11_good_locked_fp_hud");
            yield return new WaitForSeconds(0.3f);
            me.Motor.SetLook(me.Motor.LookYaw + 150f, 0f);
            yield return new WaitForSeconds(0.8f);
            Screen("12_hud_dotlink");
            Destroy(gl);
            if (seeker != null)
            {
                seeker.TeleportRpc(gb.World(FuseBoxModel.StandLocal + new Vector3(0f, 0.05f, 0.05f)), Quaternion.LookRotation(-gb.Normal).eulerAngles.y);
                me.Motor.Teleport(gb.World(new Vector3(2.2f, 0.05f, 1.6f)), 0f, 0f);
                yield return new WaitForSeconds(1.2f);
                mm.ServerFuseRemove(seeker, (byte)good, 0);
                yield return new WaitForSeconds(2.2f);
                Log($"seeker at the locked box: full={mm.FuseBoxes.Value[good].FilledCount()}/4 locked={mm.FuseBoxes.Value[good].Locked}");
                if (mm.FuseBoxes.Value[good].FilledCount() != 4) Fail("seeker took a fuse out of the locked box");
                seeker.TeleportRpc(gb.World(new Vector3(-6f, 0.05f, 6f)), 0f);
            }

            // 8. Death with a full inventory: everything falls out round the body.
            yield return DeathTest(mm, me);
            Log($"DONE, {_fails} fail(s)");
        }

        private static Vector3 FuseDropPos(MatchManager mm, int s)
        {
            for (int i = 0; i < mm.Drops.Count; i++) if (mm.Drops[i].Kind == FuseItems.Of(s)) return mm.DropPosition(mm.Drops[i]);
            return Vector3.zero;
        }

        private static IEnumerator FillOne(NetPlayer me, int box)
        {
            var b = MatchManager.Instance.FuseBoxes.Value[box];
            for (int s = 0; s < 4; s++)
            {
                if (b.Has(s) || !me.HasFuse(b.Symbol(s))) continue;
                yield return StandAt(me, box, s, FuseBoxModel.SocketX(s) * 0.6f);
                LocalPlayerController.TestPressE = true;
                yield return new WaitForSeconds(FuseTimeline.InEnd + 0.4f);
                yield break;
            }
        }

        /// <summary>Every fuse: on the level's floor, nothing solid round it, inside the garage, room for a player.</summary>
        private static void CheckFuses(MatchManager mm)
        {
            int n = 0;
            var perLevel = new int[3];
            var all = new List<Vector3>();
            for (int i = 0; i < mm.Drops.Count; i++)
            {
                var d = mm.Drops[i];
                if (!FuseItems.Is(d.Kind)) continue;
                n++;
                Vector3 p = mm.DropPosition(d);
                int lv = MapInfo.LevelOf(p.y - 0.05f);
                perLevel[lv]++;
                bool floor = Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out var hit, 1f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
                float gap = floor ? p.y - hit.point.y : -1f;
                bool free = !Physics.CheckBox(p + Vector3.up * 0.01f, new Vector3(0.045f, 0.03f, 0.09f), d.Rot, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
                bool room = !Physics.CheckCapsule(hit.point + Vector3.up * 0.35f, hit.point + Vector3.up * 1.3f, 0.25f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
                bool inside = MapInfo.InsideGarage(p, 0.3f) && Mathf.Abs(hit.point.y - MapInfo.FloorY(lv)) < 0.3f;
                float near = float.MaxValue;
                foreach (var o in all) near = Mathf.Min(near, Vector3.Distance(o, p));
                all.Add(p);
                Log($"fuse {FuseSymbols.Name(FuseItems.SymbolOf(d.Kind))}: {p} level {lv} gap {gap:0.000} free={free} room={room} inside={inside} cover={FuseSpots.Cover(hit.point)} nearest other {near:0.0} m");
                if (!floor || gap < 0f || gap > 0.08f || !free || !room || !inside) Fail($"fuse {FuseSymbols.Name(FuseItems.SymbolOf(d.Kind))} badly placed");
            }
            Log($"fuses on the map: {n} (levels {perLevel[0]}/{perLevel[1]}/{perLevel[2]})");
            if (n != FuseTuning.Fuses) Fail($"{n} fuses instead of {FuseTuning.Fuses}");
        }

        private IEnumerator DeathTest(MatchManager mm, NetPlayer me)
        {
            // Two fuses back from the floor, a medkit, adrenaline and a piece of wire.
            int got = 0;
            for (int i = 0; i < mm.Drops.Count && got < 2; i++)
                if (FuseItems.Is(mm.Drops[i].Kind)) { int s = FuseItems.SymbolOf(mm.Drops[i].Kind); yield return TakeFuse(me, s); got++; i = -1; if (got >= 2) break; }
            me.MedkitCount.Value = 2;
            me.SyringeCount.Value = 1;
            me.WireCount.Value = 1;
            yield return new WaitForSeconds(0.5f);
            Vector3 at = new Vector3(-30f, MapInfo.FloorY(1) + 0.05f, -11.75f);
            me.Motor.Teleport(at, 90f, 0f);
            yield return new WaitForSeconds(1f);
            int drops0 = mm.Drops.Count;
            Log($"before death: inv={me.Inv.Value} fuses={me.FuseMask.Value} medkits={me.MedkitCount.Value} syringes={me.SyringeCount.Value} wire={me.WireCount.Value}");
            var l = TestLight(at + Vector3.up * 2.6f, 7f, 1.6f);
            Vector3 body = me.transform.position;
            me.Life.Value = LifeState.Dead;
            StartCoroutine(Side("13_death_drops", () => body + Vector3.up * 0.3f, () => new Vector3(1.6f, 2.4f, -1.9f), 4, 0.7f, 55f));
            yield return new WaitForSeconds(4f);
            var spots = new List<Vector3>();
            for (int i = 0; i < mm.Drops.Count; i++)
            {
                var d = mm.Drops[i];
                Vector3 p = mm.DropPosition(d);
                if (Vector3.Distance(p, body) > 5f) continue;
                bool floor = Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out var hit, 1.2f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
                Log($"dropped {d.Kind} settled={d.Settled} at {p} ({Vector3.Distance(p, body):0.00} m from the body, floor gap {(floor ? p.y - hit.point.y : -1f):0.00})");
                spots.Add(p);
            }
            float minGap = float.MaxValue;
            for (int i = 0; i < spots.Count; i++) for (int j = i + 1; j < spots.Count; j++) minGap = Mathf.Min(minGap, Vector3.Distance(spots[i], spots[j]));
            Log($"after death: drops {drops0}->{mm.Drops.Count}, around the body {spots.Count}, closest pair {minGap:0.00} m, inv={me.Inv.Value} fuses={me.FuseMask.Value} medkits={me.MedkitCount.Value} deathDrops={mm.DeathDropCount}");
            if (spots.Count < 6) Fail($"only {spots.Count} items round the body (expected 6)");
            if (me.FuseMask.Value != 0 || me.MedkitCount.Value != 0) Fail("the dead player still carries items");
            Destroy(l);
        }
    }
}
