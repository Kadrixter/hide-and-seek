using System.Collections;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public partial class FuseTests
    {
        /// <summary>Seeker instance: holds E for the pulls the host scripts, films the hider at a box and the dead body's items.</summary>
        private IEnumerator Watch()
        {
            FuseWorkDriver.TestHoldE = true;
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer hider = null;
            bool insertShot = false, pullShot = false, ownPull = false, deathShot = false;
            int lastLit = -1;
            for (int k = 0; k < 3000; k++)
            {
                yield return new WaitForSeconds(0.1f);
                if (hider == null) foreach (var p in NetPlayer.All) if (p != null && p.IsHider) hider = p;
                if (hider == null) continue;
                var h = hider;
                int views = 0;
                for (int i = 0; i < FuseTuning.Boxes; i++) if (FuseBoxViews.Get(i) != null) views++;
                int lit = (FloorLights.IsLit(0) ? 1 : 0) | (FloorLights.IsLit(1) ? 2 : 0) | (FloorLights.IsLit(2) ? 4 : 0);
                if (k % 50 == 0 || lit != lastLit)
                    Log($"watch: views={views}{Boxes(mm)} lit={lit} stage={mm.Escape.Value} hider fuses={h.FuseMask.Value} work={h.FuseWork.Value.Box}/{h.FuseWork.Value.Remove} hud='{EscapeHud.LineText}' drops={mm.Drops.Count}");
                lastLit = lit;
                var w = h.FuseWork.Value;
                if (!insertShot && w.Active && !w.Remove)
                {
                    insertShot = true;
                    Log("watch: the hider puts a fuse in");
                    TestLight(h.transform.position + Vector3.up * 2.5f - h.transform.forward * 0.8f, 7f, 2f);
                    StartCoroutine(Side("w1_insert_side", () => h.transform.position + Vector3.up * 1.15f + h.transform.forward * 0.35f,
                        () => h.transform.rotation * new Vector3(0.75f, 0.75f, -1.35f), 6, 0.2f));
                }
                if (!pullShot && w.Active && w.Remove && h.FuseWorkTime > 0.3f)
                {
                    pullShot = true;
                    Log("watch: the hider pulls a fuse out");
                    TestLight(h.transform.position + Vector3.up * 2.5f - h.transform.forward * 0.8f, 7f, 2f);
                    StartCoroutine(Side("w2_pull_side", () => h.transform.position + Vector3.up * 1.15f + h.transform.forward * 0.35f,
                        () => h.transform.rotation * new Vector3(0.95f, 0.6f, -1.2f), 6, 0.25f));
                }
                if (!ownPull && me.FuseWork.Value.Active)
                {
                    ownPull = true;
                    Log($"watch: I (seeker) pull a fuse out of box {me.FuseWork.Value.Box} socket {me.FuseWork.Value.Socket}");
                    var l = TestLight(me.transform.position + Vector3.up * 2.4f + me.transform.forward * 0.6f, 6f, 1.5f);
                    yield return new WaitForSeconds(0.7f);
                    Screen("w3_seeker_pull_fp");
                    yield return new WaitForSeconds(0.6f);
                    Screen("w3_seeker_pull_fp_b");
                    yield return new WaitForSeconds(1.6f);
                    Screen("w4_seeker_pull_after_fp");
                    Destroy(l);
                }
                if (!deathShot && h.Life.Value == LifeState.Dead)
                {
                    deathShot = true;
                    Vector3 body = h.transform.position;
                    Log("watch: the hider is dead");
                    var l = TestLight(body + Vector3.up * 2.6f, 7f, 1.6f);
                    yield return Side("w5_death_drops", () => body + Vector3.up * 0.2f, () => new Vector3(-1.8f, 2.6f, 1.6f), 3, 1.2f, 55f);
                    int near = 0;
                    for (int i = 0; i < mm.Drops.Count; i++) if (Vector3.Distance(mm.DropPosition(mm.Drops[i]), body) < 5f) near++;
                    Log($"watch: {near} item(s) lie round the body");
                    Destroy(l);
                }
            }
        }
    }
}
