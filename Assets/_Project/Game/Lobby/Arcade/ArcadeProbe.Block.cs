using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    public sealed partial class ArcadeProbe
    {
        /// <summary>Client: tries to get in front of the playing host, then photographs the game from the side.</summary>
        private IEnumerator Block(MatchManager mm)
        {
            var me = NetPlayer.Local;
            var cc = me.GetComponent<CharacterController>();
            for (float w = 0f; w < 60f && (mm.ArcadePlayer.Value == ulong.MaxValue || mm.ArcadePlayer.Value == me.NetworkObjectId); w += Time.deltaTime)
                yield return null;
            Log($"someone plays: {mm.ArcadePlayer.Value}");
            yield return new WaitForSeconds(3f);
            Vector3 scr = MatchManager.ArcadeScreen;

            yield return Walk(me, cc, "behind", new Vector3(6.0f, 0f, 3.0f), Vector3.forward);
            yield return Walk(me, cc, "left", new Vector3(4.5f, 0f, 4.75f), Vector3.right);
            yield return Walk(me, cc, "right", new Vector3(7.5f, 0f, 4.75f), Vector3.left);
            yield return Walk(me, cc, "diag", new Vector3(4.9f, 0f, 5.1f), new Vector3(1f, 0f, -0.4f).normalized);

            // Already standing in the zone when it switches on: slid out.
            me.TeleportLocal(new Vector3(6.55f, 0f, 4.2f), 0f);
            for (int k = 0; k < 6; k++)
            {
                yield return new WaitForSeconds(0.25f);
                Log($"inside t={k * 0.25f + 0.25f:0.00} pos={me.transform.position} cc={cc.enabled} blocker={ArcadeKeepOut.Instance.BlockerOn}");
            }
            {
                Vector3 pp = me.transform.position;
                var hits = Physics.OverlapCapsule(pp + Vector3.up * 0.35f, pp + Vector3.up * 1.5f, cc.radius + 0.12f, ~0, QueryTriggerInteraction.Ignore);
                var names = new System.Text.StringBuilder();
                foreach (var h in hits) names.Append(h.name).Append('@').Append(h.transform.parent != null ? h.transform.parent.name : "-").Append(' ').Append(h.bounds.center).Append("; ");
                var flags = cc.Move(Vector3.right * 0.2f);
                Log($"near colliders: {names} | move +x -> {flags} pos={me.transform.position}");
            }
            bool still = ArcadeKeepOut.Overlaps(me.transform.position, cc.radius - 0.01f, out _);
            Log($"put inside -> after 1.5 s pos={me.transform.position} radius={cc.radius:0.00} stillInside={still} -> {(still ? "FAIL" : "OK")}");

            // Forced into the player's view (push off): the host must not draw this body.
            ArcadeKeepOut.DisablePush = true;
            Vector3 spot = new Vector3(6.28f, 0f, 4.95f);
            for (float w = 0f; w < 3.5f; w += Time.deltaTime)
            {
                me.TeleportLocal(spot, 200f);
                yield return null;
            }
            ArcadeKeepOut.DisablePush = false;
            me.TeleportLocal(new Vector3(7.3f, 0f, 3.4f), 0f);
            yield return new WaitForSeconds(0.8f);

            // What everybody else sees: the game on the cabinet screen.
            yield return Watch(me, "watch_left", new Vector3(4.8f, 0f, 4.3f), scr);
            yield return Watch(me, "watch_right", new Vector3(7.2f, 0f, 4.3f), scr);
            yield return Watch(me, "watch_behind", new Vector3(6.15f, 0f, 3.1f), scr);

            for (float w = 0f; w < 60f && mm.ArcadePlayer.Value != ulong.MaxValue; w += Time.deltaTime) yield return null;
            Log($"host left the arcade, blocker={(ArcadeKeepOut.Instance != null && ArcadeKeepOut.Instance.BlockerOn)}");
            // The zone is open again.
            yield return Walk(me, cc, "free", new Vector3(6.0f, 0f, 3.0f), Vector3.forward, expectBlocked: false);
            yield return new WaitForSeconds(1f);
            Log("done");
            Application.Quit();
        }

        private IEnumerator Walk(NetPlayer me, CharacterController cc, string name, Vector3 from, Vector3 dir, bool expectBlocked = true)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            me.TeleportLocal(from, yaw);
            yield return new WaitForSeconds(0.3f);
            for (float w = 0f; w < 2.5f; w += Time.deltaTime)
            {
                if (cc.enabled) cc.Move((dir * 3f + Vector3.down * 2f) * Time.deltaTime);
                yield return null;
            }
            Vector3 p = me.transform.position;
            bool inZone = ArcadeKeepOut.Overlaps(p, cc.radius - 0.03f, out _);
            float toStand = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(MatchManager.ArcadeStand.x, MatchManager.ArcadeStand.z));
            bool ok = expectBlocked ? !inZone : inZone || toStand < 1.2f;
            Log($"walk {name}: end={p} distToPlayer={toStand:0.00} inZone={inZone} -> {(ok ? "OK" : "FAIL")}");
            Vector3 look = MatchManager.ArcadeScreen - (p + Vector3.up * 1.62f);
            me.Motor.SetLook(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.25f);
            Shot("block_" + name);
            yield return null;
        }

        private IEnumerator Watch(NetPlayer me, string name, Vector3 at, Vector3 target)
        {
            me.TeleportLocal(at, 0f);
            Vector3 look = target - (at + Vector3.up * 1.62f);
            for (float w = 0f; w < 0.7f; w += Time.deltaTime)
            {
                me.Motor.SetLook(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
                yield return null;
            }
            Shot(name);
            Log($"{name}: arcade state={ArcadeState.Cab[0].State} score={ArcadeState.Cab[0].Score} len={ArcadeState.Cab[0].Body.Count}");
            yield return new WaitForSeconds(0.2f);
        }
    }
}
