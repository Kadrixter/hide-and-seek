using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Cut-off leg / knock-out / body marker tests in the built game:
    ///  -testlegthrow (host -forcehider -autostart, + a seeker client and a second hider client = the victim): the saw
    ///      takes the victim's leg (a dropped item), the host picks it up, winds up (Hotbar test hold) and throws it at
    ///      the seeker (stun: Busy, knife rejected, back after 6 s), throws it again (immune: hit without a stun), then a
    ///      saw finisher on the victim (leg items from the kill) and the body marker on the minimap (same level / another).
    ///  -teststunview (the seeker client): logs the black screen during its own knock-out.
    ///  -legshots &lt;folder&gt;: screenshots of both (wind-up, throw, the seeker falling / lying / getting up, his black screen).
    /// Log lines: [LegTest], [Legs], [Stun].
    /// </summary>
    public partial class NetSession
    {
        private void StartLegTests(System.Func<string, bool> has)
        {
            if (has("-testlegthrow")) StartCoroutine(LegThrowTest());
            if (has("-teststunview")) StartCoroutine(StunViewTest());
        }

        private static string LegShots => ArgOr("-legshots", "");
        private static int _legShotN;

        private static void LegShot(string what)
        {
            string dir = LegShots;
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{LocalProfile.Nick}_{_legShotN++:000}_{what}.png"));
        }

        private static int LegDrops(MatchManager mm, out int lastId, out Vector3 lastPos)
        {
            int n = 0;
            lastId = 0;
            lastPos = default;
            for (int i = 0; i < mm.Drops.Count; i++)
            {
                var d = mm.Drops[i];
                if (d.Kind != HotbarItem.SeveredLeg) continue;
                n++;
                lastId = d.Id;
                lastPos = mm.DropPosition(d);
            }
            return n;
        }

        private static void LegLookAt(NetPlayer me, Vector3 target)
        {
            Vector3 d = target - me.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            me.Motor.SetLook(yaw, pitch);
        }

        private IEnumerator LegThrowTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer seeker = null, victim = null;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                if (p.IsSeeker) seeker = p;
                else if (p != me) victim = p;
            }
            if (!me.IsHider || seeker == null || victim == null) { Debug.Log("[LegTest] need host hider + a seeker + a second hider"); yield break; }
            Vector3 spot = me.transform.position;
            Vector3 fw = OpenDirection(spot, out float room);
            Vector3 rt = Vector3.Cross(Vector3.up, fw);
            float yawF = Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg;
            me.TeleportRpc(spot, yawF);
            Debug.Log($"[LegTest] test lane from {spot} towards {fw} ({room:0.0} m free)");
            TestLamp(spot + Vector3.up * 2.6f + fw * 3f, 2.2f);
            TestLamp(spot + Vector3.up * 2.6f + fw * 6.5f, 2.2f);

            // 1. The saw's first hit: the victim's leg falls as an item.
            victim.TeleportRpc(spot + fw * 2f, yawF + 180f);
            seeker.TeleportRpc(spot + fw * 3.1f, yawF + 180f);
            yield return new WaitForSeconds(1f);
            seeker.HasChainsaw.Value = true;
            seeker.Held.Value = HeldItem.Chainsaw;
            seeker.AttackReadyAt.Value = 0;
            int before = LegDrops(mm, out _, out _);
            yield return new WaitForSeconds(0.5f);
            LegLookAt(me, victim.ChestPosition);
            for (int tries = 0; tries < 6 && victim.Severed.Value == 0; tries++)
            {
                float w = 0f;
                while (w < 3f && Vector3.Distance(seeker.transform.position, victim.transform.position) > 1.6f) { w += Time.deltaTime; yield return null; }
                seeker.AttackReadyAt.Value = 0;
                Debug.Log($"[LegTest] saw try {tries}: dist={Vector3.Distance(seeker.transform.position, victim.transform.position):0.00} seekerBusy={seeker.Busy.Value} victimBusy={victim.Busy.Value} held={seeker.Held.Value}");
                mm.ServerChainsaw(seeker, victim);
                yield return new WaitForSeconds(BlockyCharacterActions.SawSlashHitTime + 0.15f);
                if (victim.Severed.Value == 0)
                {
                    victim.TeleportRpc(spot + fw * 2f, yawF + 180f);
                    seeker.TeleportRpc(spot + fw * 3.1f, yawF + 180f);
                    yield return new WaitForSeconds(1f);
                }
            }
            LegShot("cut");
            yield return new WaitForSeconds(2.4f);
            int legs = LegDrops(mm, out int legId, out Vector3 legPos);
            Debug.Log($"[LegTest] saw hit: victim severed={victim.Severed.Value} leg drops {before} -> {legs} (expected +1) at {legPos} " +
                      $"(victim at {victim.transform.position}) looseParts={GameObject.Find("SeveredLimb") != null} (expected False)");
            LegLookAt(me, legPos);
            yield return new WaitForSeconds(0.5f);
            LegShot("leg_on_floor");

            // 2. Pick it up.
            me.TeleportRpc(legPos + (spot - legPos).normalized * 0.8f, 0f);
            yield return new WaitForSeconds(0.4f);
            mm.ServerPickupDrop(me, legId);
            yield return new WaitForSeconds(0.4f);
            int slot = Inventory.SlotOf(me, HotbarItem.SeveredLeg);
            Debug.Log($"[LegTest] picked up: LegCode={me.LegCode.Value} slot={slot} legDrops={LegDrops(mm, out _, out _)} (expected 0)");
            if (slot < 0) yield break;
            Hotbar.TestSelect = slot;

            // 3. Wind up and throw at the seeker 6 m away.
            seeker.Held.Value = HeldItem.Knife;
            seeker.AttackReadyAt.Value = 0;
            me.TeleportRpc(spot, yawF);
            seeker.TeleportRpc(spot + fw * 6f, yawF + 180f);
            victim.TeleportRpc(spot + rt * 2.5f + fw * 1f, yawF);
            yield return new WaitForSeconds(1.2f);
            LegLookAt(me, seeker.ChestPosition + Vector3.up * 0.3f);
            yield return new WaitForSeconds(0.4f);
            LegShot("hold");
            Debug.Log($"[LegTest] in hand: selected={me.SelectedItem} handItem={me.HandItem}");
            Hotbar.TestHold = true;
            yield return new WaitForSeconds(0.45f);
            LegShot("windup");
            yield return new WaitForSeconds(0.9f);
            LegShot("cocked");
            Debug.Log($"[LegTest] charging={me.LegCharging} power={me.LegPowerShown:0.00}");
            NetPlayer.TestLegDir = (seeker.ChestPosition + Vector3.up * 0.35f - me.EyePosition).normalized;
            Hotbar.TestHold = false;
            int hits0 = MatchManager.LegHits, stuns0 = MatchManager.LegStuns;
            yield return new WaitForSeconds(0.1f);
            LegShot("release");
            float t0 = Time.time;
            bool knifeTried = false;
            while (Time.time - t0 < 7.5f)
            {
                yield return new WaitForSeconds(0.3f);
                LegShot($"after_{Time.time - t0:0.0}");
                if (!knifeTried && seeker.Stunned)
                {
                    knifeTried = true;
                    Debug.Log($"[LegTest] stunned: busy={seeker.Busy.Value} cannotAttack={seeker.CannotAttack} stunT={seeker.StunTime:0.00}");
                    me.TeleportRpc(seeker.transform.position + seeker.transform.forward * 1f, 180f);
                    yield return new WaitForSeconds(0.2f);
                    bool limp0 = me.Limping.Value;
                    seeker.AttackReadyAt.Value = 0;
                    mm.ServerKnife(seeker, me);
                    yield return new WaitForSeconds(0.2f);
                    Debug.Log($"[LegTest] knife while stunned: limping {limp0} -> {me.Limping.Value}, busy={me.Busy.Value} (expected no change)");
                    me.TeleportRpc(spot, yawF);
                    yield return null;
                    LegLookAt(me, seeker.ChestPosition);
                }
            }
            Debug.Log($"[LegTest] throw 1: hits {MatchManager.LegHits - hits0} stuns {MatchManager.LegStuns - stuns0} (expected 1/1), " +
                      $"seeker busy={seeker.Busy.Value} stunned={seeker.Stunned} (expected False/False) immune={seeker.StunImmuneLeft:0.0} s");

            // 4. Again while he is immune: a hit without a knock-out.
            legs = LegDrops(mm, out legId, out legPos);
            me.TeleportRpc(legPos + rt * 0.8f, 0f);
            yield return new WaitForSeconds(0.4f);
            mm.ServerPickupDrop(me, legId);
            yield return new WaitForSeconds(0.4f);
            slot = Inventory.SlotOf(me, HotbarItem.SeveredLeg);
            Hotbar.TestSelect = slot;
            me.TeleportRpc(spot + fw * 1.5f, yawF);
            yield return new WaitForSeconds(0.8f);
            LegLookAt(me, seeker.ChestPosition);
            Hotbar.TestHold = true;
            yield return new WaitForSeconds(1.2f);
            NetPlayer.TestLegDir = (seeker.ChestPosition + Vector3.up * 0.3f - me.EyePosition).normalized;
            Hotbar.TestHold = false;
            hits0 = MatchManager.LegHits;
            stuns0 = MatchManager.LegStuns;
            yield return new WaitForSeconds(2f);
            Debug.Log($"[LegTest] throw 2 (immune): slot={slot} hits {MatchManager.LegHits - hits0} stuns {MatchManager.LegStuns - stuns0} (expected 1/0) seeker busy={seeker.Busy.Value}");

            // 5. A saw kill takes the other leg too; the body shows on the hiders' minimap on its level only.
            before = LegDrops(mm, out _, out _);
            victim.TeleportRpc(spot + fw * 3f, yawF + 180f);
            seeker.TeleportRpc(spot + fw * 4.2f, yawF + 180f);
            me.TeleportRpc(spot, yawF);
            yield return new WaitForSeconds(0.8f);
            seeker.AttackReadyAt.Value = 0;
            LegLookAt(me, victim.ChestPosition);
            mm.TestKillMove(seeker, victim, KillMove.SawFinisher);
            yield return new WaitForSeconds(KillMoves.SequenceDuration(KillMove.SawFinisher) + 3f);
            Debug.Log($"[LegTest] saw finisher: victim life={victim.Life.Value} leg drops {before} -> {LegDrops(mm, out _, out _)} (expected +1)");
            yield return new WaitForSeconds(1f);
            LegShot("body_minimap");
            Debug.Log($"[LegTest] body dots same level: {Minimap.BodyDots} (expected 1) body at {victim.ChestPosition}");
            Vector3 other = spot + (spot.y < -5f ? Vector3.up : Vector3.down) * 3.4f;
            me.TeleportRpc(other, 0f);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[LegTest] body dots from y={me.transform.position.y:0.0}: {Minimap.BodyDots} (expected 0 if another level)");
            LegShot("other_level");
            Debug.Log("[LegTest] done");
        }

        /// <summary>Flat direction from <paramref name="spot"/> with the most free room at chest height (a lane for the test).</summary>
        private static Vector3 OpenDirection(Vector3 spot, out float best)
        {
            best = 0f;
            Vector3 dir = Vector3.forward;
            for (int i = 0; i < 24; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
                float free = 12f;
                foreach (var h in Physics.RaycastAll(spot + Vector3.up * 1.2f, d, 12f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.GetComponentInParent<NetPlayer>() == null && h.distance < free) free = h.distance;
                foreach (var h in Physics.RaycastAll(spot + Vector3.up * 0.3f, d, 12f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.GetComponentInParent<NetPlayer>() == null && h.distance < free) free = h.distance;
                if (free > best) { best = free; dir = d; }
            }
            return dir;
        }

        private IEnumerator StunViewTest()
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            while (me != null && !me.Stunned) yield return null;
            if (me == null) yield break;
            float[] marks = { 0.1f, 0.5f, 0.9f, 1.4f, 2.5f, 3.9f, 4.4f, 4.9f, 5.4f, 5.9f, 6.3f };
            foreach (float m in marks)
            {
                while (me.Stunned && me.StunTime < m) yield return null;
                if (!me.Stunned && m < StunTimeline.Duration) Debug.Log("[LegTest] stun ended early");
                LegShot($"stun_{m:0.0}");
                Debug.Log($"[LegTest] my stun t={me.StunTime:0.00} black={HideAndSeek.Game.UI.StunScreen.Black:0.00} busy={me.Busy.Value} input={me.InputReader.InputEnabled}");
                if (!me.Stunned) yield return new WaitForSeconds(0.4f);
            }
        }
    }
}
