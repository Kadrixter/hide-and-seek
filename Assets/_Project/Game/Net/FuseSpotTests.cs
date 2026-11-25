using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HideAndSeek.Map;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Where the fuses may lie (own component, started from the command line), logs [FuseSpotTest]:
    ///  -testfusespots &lt;dir&gt; (host -forcehider -autostart -hidetime N, + a client): every road spot of every level is
    ///  checked on its own (on the map, flat floor of its level, not near a ramp / stair opening, nothing low overhead,
    ///  room for a player, not inside a solid - rays that hit back faces), reachable on foot (flood fill over a 0.5 m
    ///  grid from the hider spawns), then -fuseseeds N (default 300) rounds are dealt like the server does (boxes +
    ///  8 fuses) and every fuse checked, the 8 real fuses of this round too; the minimap fuse markers are compared with
    ///  the fuses within <see cref="Minimap.FuseRange"/> m at a few spots. PNGs: top-down map of every level with the
    ///  spots, close-ups of the real fuses, the minimap.
    /// </summary>
    public partial class FuseSpotTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;
        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length && !Args[i + 1].StartsWith("-") ? Args[i + 1] : def;
        }

        private static string _dir;
        private static int _fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Has("-testfusespots")) return;
            _dir = ArgOr("-testfusespots", "");
            var go = new GameObject("[FuseSpotTests]");
            DontDestroyOnLoad(go);
            go.AddComponent<FuseSpotTests>().StartCoroutine(go.GetComponent<FuseSpotTests>().Run());
        }

        private static void Log(string s) => Debug.Log("[FuseSpotTest] " + s);
        private static void Fail(string s) { _fails++; Debug.LogWarning("[FuseSpotTest] FAIL: " + s); }

        private IEnumerator Run()
        {
            while (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(2f);
            var mm = MatchManager.Instance;
            if (!NetworkManager.Singleton.IsServer) { Log("host only"); yield break; }
            int levels = MapInfo.Levels;

            // 1. Every road spot on its own.
            var roads = new List<Vector3>[levels];
            var bad = new List<Vector3>[levels];
            for (int lv = 0; lv < levels; lv++)
            {
                roads[lv] = new List<Vector3>(FuseSpots.RoadSpots(lv));
                bad[lv] = new List<Vector3>();
                int shown = 0;
                foreach (var p in roads[lv])
                {
                    string why = Why(p, lv);
                    if (why == null) continue;
                    bad[lv].Add(p);
                    if (shown++ < 25) Fail($"road spot L{lv} {p}: {why}");
                }
                Log($"level {lv}: {roads[lv].Count} road spots, {bad[lv].Count} bad");
                if (roads[lv].Count < 100) Fail($"level {lv}: only {roads[lv].Count} road spots");
            }

            // 2. Reachable on foot.
            var reach = new bool[levels][,];
            for (int lv = 0; lv < levels; lv++)
            {
                yield return Flood(lv, g => reach[lv] = g);
                int lost = 0;
                foreach (var p in roads[lv])
                    if (!Reached(reach[lv], p)) { if (lost++ < 15) Fail($"road spot L{lv} {p} not reachable on foot from the hider spawns"); bad[lv].Add(p); }
                Log($"level {lv}: {roads[lv].Count - lost}/{roads[lv].Count} road spots reachable on foot");
            }

            // 3. Many rounds dealt like the server does (MatchManager.Fuses DealFuses).
            int seeds = int.TryParse(ArgOr("-fuseseeds", "300"), out int ns) ? ns : 300;
            var picks = new List<Vector3>[levels];
            for (int lv = 0; lv < levels; lv++) picks[lv] = new List<Vector3>();
            int road = 0, fallback = 0, missing = 0, badPicks = 0, closePairs = 0;
            float minSpacing = float.MaxValue;
            for (int s = 0; s < seeds; s++)
            {
                var rnd = new System.Random(s * 7919 + 17);
                var walls = FuseSpots.PickBoxes(rnd);
                var boxPos = new List<Vector3>();
                foreach (var w in walls) if (w != null) boxPos.Add(w.Value.Pos);
                for (int k = 0; k < 6; k++) rnd.Next(); // marks + the right box (not needed here)
                int few = rnd.Next(levels);
                var lvList = new List<int>();
                for (int lv = 0; lv < levels; lv++) for (int k = 0; k < (lv == few ? 2 : 3); k++) lvList.Add(lv);
                for (int i = lvList.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (lvList[i], lvList[j]) = (lvList[j], lvList[i]); }
                var placed = new List<Vector3>();
                for (int f = 0; f < FuseTuning.Fuses; f++)
                {
                    int lv = lvList[f % lvList.Count];
                    var p = FuseSpots.FuseSpot(lv, rnd, placed, boxPos);
                    if (p == null) { missing++; Fail($"seed {s}: no spot for fuse {f} on level {lv}"); continue; }
                    bool onRoad = roads[lv].Exists(r => (r - p.Value).sqrMagnitude < 1e-4f);
                    if (onRoad) road++; else fallback++;
                    string why = onRoad ? (bad[lv].Exists(r => (r - p.Value).sqrMagnitude < 1e-4f) ? "a bad road spot" : null) : Why(p.Value, lv);
                    if (why != null) { badPicks++; Fail($"seed {s}: fuse {f} L{lv} {p.Value}: {why}"); }
                    foreach (var o in placed)
                        if (Mathf.Abs(o.y - p.Value.y) < 1.5f)
                        {
                            float d = new Vector2(o.x - p.Value.x, o.z - p.Value.z).magnitude;
                            minSpacing = Mathf.Min(minSpacing, d);
                            if (d < 3f) closePairs++;
                        }
                    placed.Add(p.Value);
                    picks[lv].Add(p.Value);
                }
                if (s % 4 == 3) yield return null;
            }
            Log($"{seeds} rounds: {road} fuses on the road, {fallback} fallback, {missing} missing, {badPicks} bad, min spacing on a level {minSpacing:0.0} m, pairs < 3 m: {closePairs}");

            // 4. The real fuses of this round.
            var real = new List<Vector3>();
            for (int i = 0; i < mm.Drops.Count; i++)
            {
                var d = mm.Drops[i];
                if (!FuseItems.Is(d.Kind)) continue;
                Vector3 w = mm.DropPosition(d);
                Vector3 floor = new Vector3(w.x, MapInfo.FloorUnder(w.y), w.z);
                real.Add(floor);
                string why = Why(floor, MapInfo.LevelOf(w.y));
                Log($"real fuse {d.Kind} at {w} level {MapInfo.LevelOf(w.y)}: {(why ?? "ok")}");
                if (why != null) Fail($"real fuse {d.Kind}: {why}");
            }
            if (real.Count != FuseTuning.Fuses) Fail($"{real.Count} fuses on the map this round (expected {FuseTuning.Fuses})");

            // 5. Pictures: top-down map of every level, close-ups of the real fuses.
            for (int lv = 0; lv < levels; lv++)
            {
                TopDown(lv, roads[lv], picks[lv], bad[lv], real);
                yield return null;
            }
            for (int i = 0; i < real.Count && i < 8; i++) { CloseUp(real[i], $"fuse_{i}"); yield return null; }

            // 6. Minimap markers (the host is a hider).
            yield return MinimapCheck(mm, roads);
            Log($"done, fails={_fails}");
        }

        /// <summary>Why a fuse must not lie at the floor point <paramref name="p"/> of <paramref name="lv"/> (null = fine).</summary>
        public static string Why(Vector3 p, int lv)
        {
            if (!FuseSpots.OnMap(p)) return "OnMap = false";
            if (MapInfo.LevelOf(p.y) != lv) return $"level {MapInfo.LevelOf(p.y)} instead of {lv}";
            if (Mathf.Abs(p.y - MapInfo.FloorY(lv)) > 0.12f) return $"{p.y - MapInfo.FloorY(lv):0.00} m off the floor";
            if (MapInfo.InSlabOpening(p, 0.6f)) return "within 0.6 m of a ramp / stair opening";
            if (!StaticHit(p + Vector3.up * 0.5f, Vector3.down, 1f, out var fh)) return "no floor";
            if (fh.normal.y < 0.97f) return $"sloped floor ({fh.collider.name})";
            if (fh.collider.name.IndexOf("Ramp", StringComparison.OrdinalIgnoreCase) >= 0) return $"on a ramp ({fh.collider.name})";
            if (StaticHit(p + Vector3.up * 0.1f, Vector3.up, 1.9f, out var uh)) return $"low overhead: {uh.collider.name} {uh.distance + 0.1f:0.00} m";
            var c = StaticOverlapCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.5f, 0.3f);
            if (c != null) return $"no room for a player: {c.name}";
            int back = 0;
            bool old = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up })
                if (StaticHit(p + Vector3.up * 0.3f, dir, 8f, out var bh) && Vector3.Dot(bh.normal, dir) > 0.1f) back++;
            Physics.queriesHitBackfaces = old;
            if (back >= 3) return $"inside a solid ({back}/5 rays hit back faces)";
            for (int g = 0; g < Cinematics.ExitGate.Count; g++)
                if (lv == 0 && Escape.EscapeTimeline.ToLocal(p, g).z < 0.5f && Mathf.Abs(Escape.EscapeTimeline.ToLocal(p, g).x) < 5f) return $"in exit gate {g}";
            return null;
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[24];
        private static readonly Collider[] Cols = new Collider[24];

        private static bool IsStatic(Collider c) => c != null && c.attachedRigidbody == null && c.GetComponentInParent<NetworkObject>() == null;

        public static bool StaticHit(Vector3 from, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(from, dir, Hits, dist, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (IsStatic(Hits[i].collider) && Hits[i].distance < bd) { bd = Hits[i].distance; best = Hits[i]; }
            return bd < float.MaxValue;
        }

        public static Collider StaticOverlapCapsule(Vector3 a, Vector3 b, float r)
        {
            int n = Physics.OverlapCapsuleNonAlloc(a, b, r, Cols, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (IsStatic(Cols[i])) return Cols[i];
            return null;
        }
    }
}
