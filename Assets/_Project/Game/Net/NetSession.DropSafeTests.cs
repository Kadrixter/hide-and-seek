using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testdropsafe &lt;dir&gt; (host -forcehider -autostart -autostartplayers 2 -hidetime N + a seeker client): throws
    /// items the Q way (MatchManager.TestThrowDrop) facing a wall, on the ramps / stairs, from a car roof, while walking,
    /// plus raw drops started inside a wall and very fast at a wall / the floor. Every drop is followed until it rests:
    /// PASS = inside the garage, lying on something (not under the floor), not far from where it was thrown, never
    /// faster than a throw. Logs: [DropSafe] ... PASS / FAIL, summary at the end. PNGs of the resting items in &lt;dir&gt;.
    /// </summary>
    public partial class NetSession
    {
        private int _dsPass, _dsFail;

        private static void DsLog(string s) => Debug.Log("[DropSafe] " + s);

        private IEnumerator DropSafeTest(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (!me.IsHider || !Manager.IsServer) { DsLog("need the host as hider"); yield break; }
            var h = MapInfo.InnerHalfSize;
            HotbarItem[] kinds = { HotbarItem.Medkit, HotbarItem.Syringe, HotbarItem.Rod, HotbarItem.Chainsaw, HotbarItem.Glock };

            // 1. Facing the outer walls (east, north, west) at level 0 and level -2, 0.35 m from the wall.
            var spots = new List<(string name, Vector3 at, float yaw)>
            {
                ("wall_east_L0", new Vector3(h.x - 0.35f, 0.05f, 12f), 90f),
                ("wall_north_L0", new Vector3(20f, 0.05f, h.y - 0.35f), 0f),
                ("wall_west_L2", new Vector3(-h.x + 0.35f, MapInfo.FloorY(2) + 0.05f, -20f), 270f),
            };
            // 2. Ramps and stairs: the middle of every slab opening, on whatever surface is there (level 0 down).
            var ll = LevelLighting.Instance;
            for (int i = 0; ll != null && i < ll.Holes.Count; i++)
            {
                var r = ll.Holes[i];
                Vector3 c = new Vector3(r.center.x, 1.5f, r.center.y);
                if (!Physics.Raycast(c, Vector3.down, out var hit, 4.5f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) continue;
                float yaw = r.width > r.height ? 90f : 0f;
                spots.Add(($"hole{i}_down", hit.point + Vector3.up * 0.05f, yaw));
                spots.Add(($"hole{i}_up", hit.point + Vector3.up * 0.05f, yaw + 180f));
            }
            // 3. On a car roof (searched with rays from above on level 0).
            for (float x = -60f; x < 60f && spots.Count < 40; x += 1.3f)
            {
                Vector3 top = new Vector3(x, 2.8f, 25f);
                if (!Physics.Raycast(top, Vector3.down, out var hit, 3f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.point.y < 1.1f || hit.normal.y < 0.9f) continue;
                spots.Add(("car_roof", hit.point + Vector3.up * 0.05f, 0f));
                break;
            }
            DsLog($"{spots.Count} spots");

            int k = 0;
            foreach (var s in spots)
            {
                me.TeleportLocal(s.at, s.yaw);
                me.Motor.SetLook(s.yaw, 10f);
                yield return new WaitForSeconds(0.5f);
                var kind = kinds[k++ % kinds.Length];
                Vector3 feet = me.transform.position;
                int id = mm.TestThrowDrop(me, kind, 0);
                yield return DsFollow(dir, s.name, id, feet, 12f, mm);
            }

            // 4. While walking towards a wall (the server position moves every frame).
            {
                Vector3 a = new Vector3(h.x - 4f, 0.05f, -12f);
                me.TeleportLocal(a, 90f);
                yield return new WaitForSeconds(0.4f);
                int id = -1;
                Vector3 feet = a;
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    me.TeleportLocal(a + Vector3.right * t * 3f, 90f);
                    if (id < 0 && t > 0.9f) { feet = me.transform.position; id = mm.TestThrowDrop(me, HotbarItem.Rod, 0); }
                    yield return null;
                }
                yield return DsFollow(dir, "walking_to_wall", id, feet, 12f, mm);
            }

            // 5. Raw drops: started half inside the wall (pushed out gently), and very fast at a wall / at the floor.
            {
                Vector3 feet = new Vector3(h.x - 1.5f, 0.05f, 30f);
                me.TeleportLocal(feet, 90f);
                yield return new WaitForSeconds(0.4f);
                int id = mm.TestSpawnDropAt(HotbarItem.Medkit, new Vector3(h.x + 0.05f, 0.6f, 30f), Vector3.zero, feet);
                yield return DsFollow(dir, "inside_wall", id, feet, 15f, mm); // one-step snap out of the wall reads as ~12 m/s, it must not fly off (dist < 4 m)
                id = mm.TestSpawnDropAt(HotbarItem.Rod, feet + Vector3.up * 1.2f, new Vector3(25f, 0f, 0f), feet);
                yield return DsFollow(dir, "fast_at_wall", id, feet, 99f, mm);
                id = mm.TestSpawnDropAt(HotbarItem.Syringe, feet + Vector3.up * 2.5f, new Vector3(0f, -30f, 0f), feet);
                yield return DsFollow(dir, "fast_at_floor", id, feet, 99f, mm);
                Vector3 hole = ll != null && ll.Holes.Count > 0 ? new Vector3(ll.Holes[0].center.x, 0.05f, ll.Holes[0].center.y) : feet;
                id = mm.TestSpawnDropAt(HotbarItem.Medkit, hole + Vector3.up * 1.0f, new Vector3(0f, -30f, 0f), hole);
                yield return DsFollow(dir, "fast_into_hole", id, hole, 99f, mm);
            }
            DsLog($"summary: {_dsPass} PASS, {_dsFail} FAIL");
        }

        /// <summary>Follows drop <paramref name="id"/> on the server until it rests, checks it, photographs it, removes it.</summary>
        private IEnumerator DsFollow(string dir, string name, int id, Vector3 feet, float speedLimit, MatchManager mm)
        {
            float maxSpeed = 0f, minY = float.MaxValue, t0 = Time.time;
            Vector3 last = Vector3.zero;
            bool have = false, settled = false;
            DropState d = default;
            while (Time.time - t0 < 9f)
            {
                yield return null;
                bool found = false;
                for (int i = 0; i < mm.Drops.Count; i++) if (mm.Drops[i].Id == id) { d = mm.Drops[i]; found = true; }
                if (!found) { DsLog($"{name}: drop #{id} vanished - FAIL"); _dsFail++; yield break; }
                var v = mm.DropView(id);
                Vector3 p = v != null ? v.position : d.Pos;
                if (have && Time.deltaTime > 0f) maxSpeed = Mathf.Max(maxSpeed, (p - last).magnitude / Time.deltaTime);
                last = p;
                have = true;
                minY = Mathf.Min(minY, p.y);
                if (d.Settled && Time.time - t0 > 0.5f) { settled = true; break; }
            }
            yield return new WaitForSeconds(0.4f);
            Vector3 rest = d.Pos;
            bool inside = MapInfo.InsideGarage(rest, 0f);
            bool onSomething = Physics.Raycast(rest + Vector3.up * 0.25f, Vector3.down, out var hit, 0.9f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
            bool aboveBottom = rest.y >= MapInfo.FloorY(MapInfo.Levels - 1) - 0.1f;
            Vector3 flat = rest - feet;
            flat.y = 0f;
            bool near = flat.magnitude < 4f;
            bool calm = maxSpeed <= speedLimit;
            bool ok = settled && inside && onSomething && aboveBottom && near && calm;
            if (ok) _dsPass++; else _dsFail++;
            DsLog($"{name}: {d.Kind} #{id} rest={rest} feet={feet} dist={flat.magnitude:0.00} settled={settled} inside={inside} " +
                  $"onSomething={onSomething}{(onSomething ? $" ({hit.collider.name}, {hit.distance - 0.25f:0.00} m)" : "")} minY={minY:0.00} " +
                  $"maxSpeed={maxSpeed:0.0} (limit {speedLimit:0}) -> {(ok ? "PASS" : "FAIL")}");
            if (!string.IsNullOrEmpty(dir))
            {
                Vector3 side = Vector3.Cross(Vector3.up, (rest - feet).sqrMagnitude > 0.01f ? (rest - feet).normalized : Vector3.forward);
                TestLight(rest + Vector3.up * 1.5f, 5f, 1.4f);
                ItemLitShot(dir, "drop_" + name, rest + side * 1.2f + Vector3.up * 0.9f - (rest - feet).normalized * 0.4f, rest);
                yield return new WaitForSeconds(0.2f);
            }
            mm.TestRemoveDrop(id);
            yield return new WaitForSeconds(0.3f);
        }
    }
}
