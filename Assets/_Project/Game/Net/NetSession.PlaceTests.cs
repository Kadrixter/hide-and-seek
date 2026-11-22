using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testplace [N] (host in the garage, e.g. -host -autostart -autostartplayers 1): checks where items end up.
    /// Draws the medkits / syringes N times (default 100), the phones, toy guns and rods N / 10 times, puts items
    /// down next to every hider spawn point in 8 directions and right against the outer walls (Q drops, a dead
    /// holder's phone), then audits every result on its own: inside the walls, flat floor of the level right under it,
    /// not inside anything, reachable. Logs "[PlaceTest]" lines: the rejected candidates by reason, fallbacks and every
    /// bad spot. -placeverbose: every rejected candidate too. Does not quit.
    /// </summary>
    public partial class NetSession
    {
        private void StartPlaceTests(System.Func<string, bool> has)
        {
            if (has("-testplace")) StartCoroutine(PlaceTest());
        }

        private IEnumerator PlaceTest()
        {
            while (MatchManager.Instance == null || NetPlayer.Local == null ||
                   (MatchManager.Instance.Phase.Value != MatchPhase.Hiding && MatchManager.Instance.Phase.Value != MatchPhase.Seeking))
                yield return null;
            yield return new WaitForSeconds(2f);
            var mm = MatchManager.Instance;
            if (!mm.IsServer) { Debug.Log("[PlaceTest] needs the host"); yield break; }
            int rounds = int.TryParse(ArgOr("-testplace", "100"), out int n) && n > 0 ? n : 100;
            ItemPlacement.Verbose = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-placeverbose") >= 0;
            Debug.Log($"[PlaceTest] garage={MapInfo.HasGarage} inner={MapInfo.InnerHalfSize} rounds={rounds}");

            // Medkits and syringes near the hider spawn points.
            ItemPlacement.ResetStats();
            int checkedN = 0, bad = 0;
            for (int r = 0; r < rounds; r++)
            {
                mm.TestRespawnPickups();
                for (int i = 0; i < mm.Medkits.Count; i++) { checkedN++; if (!Audit("medkit", mm.Medkits[i].Position, true)) bad++; }
                for (int i = 0; i < mm.Syringes.Count; i++) { checkedN++; if (!Audit("syringe", mm.Syringes[i].Position, true)) bad++; }
                if (r % 10 == 9) yield return null;
            }
            Debug.Log($"[PlaceTest] medkits+syringes: {checkedN} placed, {bad} bad | {ItemPlacement.StatsText()}");

            // Phones, toy guns, rods (spots next to racks / shelves / fences).
            ItemPlacement.ResetStats();
            checkedN = bad = 0;
            for (int r = 0; r < Mathf.Max(1, rounds / 10); r++)
            {
                mm.SpawnRods(4);
                mm.SpawnPhones(3);
                mm.SpawnToyGuns(3);
                for (int i = 0; i < mm.Rods.Count; i++) { checkedN++; if (!Audit("rod", mm.Rods[i].Base, false)) bad++; }
                for (int i = 0; i < mm.Phones.Count; i++) { checkedN++; if (!Audit("phone", mm.Phones[i].Pos, false)) bad++; }
                for (int i = 0; i < mm.ToyGuns.Count; i++) { checkedN++; if (!Audit("toy gun", mm.ToyGuns[i].Pos, false)) bad++; }
                yield return null;
            }
            Debug.Log($"[PlaceTest] rods+phones+toy guns: {checkedN} placed, {bad} bad | {ItemPlacement.StatsText()}");

            // Putting down / throwing next to a player: every spawn point in 8 directions, and pressed against the walls.
            ItemPlacement.ResetStats();
            checkedN = bad = 0;
            int noThrow = 0;
            var feetList = new List<Vector3>();
            foreach (var t in SpawnPoints.Hider()) feetList.Add(t.position);
            if (MapInfo.HasGarage)
            {
                var h = MapInfo.InnerHalfSize;
                for (int lv = 0; lv < MapInfo.Levels; lv++)
                for (int k = 0; k < 12; k++)
                {
                    float y = MapInfo.FloorY(lv) + 0.05f;
                    float a = Random.Range(-0.9f, 0.9f);
                    feetList.Add(new Vector3(h.x - 0.35f, y, a * h.y));
                    feetList.Add(new Vector3(-h.x + 0.35f, y, a * h.y));
                    feetList.Add(new Vector3(a * h.x, y, h.y - 0.35f));
                    feetList.Add(new Vector3(a * h.x, y, -h.y + 0.35f));
                }
            }
            foreach (var feet in feetList)
            {
                for (int d = 0; d < 8; d++)
                {
                    float ang = d * Mathf.PI * 0.25f;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    var put = ItemPlacement.PutDown(feet, feet + dir * 0.8f, ItemPlacement.Small, "put down");
                    checkedN++;
                    if (!Audit("put down", put, false, feet)) bad++;
                    if (!ItemPlacement.ThrowStart(feet + Vector3.up * 1.15f, dir, 0.5f, 0.2f, out var start, out _)) noThrow++;
                    else if (MapInfo.HasGarage && !MapInfo.InsideGarage(start, 0.15f))
                    {
                        bad++;
                        Debug.Log($"[PlaceTest] BAD throw start {start} from {feet} dir {dir}");
                    }
                }
                yield return null;
            }
            Debug.Log($"[PlaceTest] put down / throw: {checkedN} spots, {bad} bad, {noThrow} throws without room (let go at the feet) | {ItemPlacement.StatsText()}");

            ItemPlacement.Verbose = false;
            mm.TestRespawnPickups();
            mm.SpawnRods(Random.Range(ItemTuning.MinRods, ItemTuning.MaxRods + 1));
            mm.SpawnPhones(2);
            mm.SpawnToyGuns(2);
            Debug.Log("[PlaceTest] done");
        }

        /// <summary>Independent check of a placed item (floor point + the small lift the item gets).</summary>
        private static bool Audit(string what, Vector3 p, bool headroom, Vector3? from = null)
        {
            string why = null;
            if (MapInfo.HasGarage && !MapInfo.InsideGarage(p, 0.1f)) why = "outside the garage";
            else if (MapInfo.HasGarage && Mathf.Abs(p.y - MapInfo.FloorUnder(p.y)) > 0.3f && (from == null || Mathf.Abs(p.y - from.Value.y) > 1.7f))
                why = $"not on the level floor (y {p.y:0.00}, floor {MapInfo.FloorUnder(p.y):0.00})";
            else if (!ItemPlacement.FirstHit(p + Vector3.up * 0.3f, Vector3.down, 0.6f, 0f, out var hit) || hit.normal.y < 0.85f)
                why = "no flat floor under it";
            else if (!ItemPlacement.SphereFree(p + Vector3.up * 0.1f, 0.06f)) why = "inside something";
            else if (headroom && !ItemPlacement.CapsuleFree(p + Vector3.up * 0.4f, p + Vector3.up * 1.2f, 0.2f)) why = "no room for a player";
            else if (from.HasValue && ItemPlacement.Blocked(from.Value + Vector3.up * 0.45f, p + Vector3.up * 0.45f, 0f)) why = "behind a wall";
            if (why == null) return true;
            Debug.Log($"[PlaceTest] BAD {what} at {p}: {why}");
            return false;
        }
    }
}
