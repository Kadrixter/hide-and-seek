using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Map;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Legs through walls (-testlegs &lt;dir&gt; on the host = seeker: -forceseeker -autostart -autostartplayers 2, and on a
    /// hider client; -legsoff on both turns the fix off for the "before" pictures). Both peers pick the same thin wall
    /// next to a road spot. The client puts its hider with the back to the wall and crouches / lies down / crawls back /
    /// turns the legs towards the wall while lying parallel to it; the host downs it and the client crawls back again.
    /// The host measures the remote hider's leg and hip boxes against the wall plane every frame and takes pictures from
    /// the other side of the wall and from the side (-legsfoldonly: the motor does not push, the hider lies with the hips
    /// in front of the wall and only the leg fold works). PASS = nothing out the other side and at most a boot tip
    /// (8 cm) in the wall. Logs: [LegsTest] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static void LgLog(string s) => Debug.Log("[LegsTest] " + s);

        private struct LegWall
        {
            public bool Ok;
            public Vector3 Point, Normal, Side, Spot;
            public float Thick, Yaw;
        }

        private void StartLegRoomTests(System.Func<string, bool> has)
        {
            if (!has("-testlegs")) return;
            if (has("-legsoff")) BodyRoom.Enabled = false;
            if (has("-legsfoldonly")) BodyRoom.MotorPush = false;
            StartCoroutine(LegRoomTest(ArgOr("-testlegs", "")));
        }

        private static IEnumerator LgUntil(float t)
        {
            while (Time.time < t) yield return null;
        }

        private IEnumerator LegRoomTest(string dir)
        {
            yield return WaitSeeking();
            float t0 = Time.time;
            var w = FindLegWall();
            LgLog(w.Ok ? $"fix={(BodyRoom.Enabled ? "on" : "off")} push={BodyRoom.MotorPush} wall {w.Point:F2} n={w.Normal:F2} thick={w.Thick:0.00} spot={w.Spot:F2} yaw={w.Yaw:0}"
                       : "no thin wall found - FAIL");
            if (!w.Ok) yield break;
            yield return Manager.IsServer ? LegHost(dir, w, t0) : LegClient(w, t0);
        }

        // ------------------------------------------------------------------ wall search (same on both peers)

        private static LegWall FindLegWall()
        {
            for (int lv = 0; lv < MapInfo.Levels; lv++)
            {
                var spots = FuseSpots.RoadSpots(lv);
                for (int i = 0; i < spots.Count; i++)
                for (int k = 0; k < 8; k++)
                    if (TryLegWall(spots[i], Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward, out var w)) return w;
            }
            return default;
        }

        private static bool TryLegWall(Vector3 spot, Vector3 dir, out LegWall w)
        {
            w = default;
            const int mask = BodyRoom.Mask;
            var q = QueryTriggerInteraction.Ignore;
            Vector3 o = spot + Vector3.up * 0.3f;
            if (!Physics.Raycast(o, dir, out var hit, 7f, mask, q) || hit.distance < 2.2f || hit.rigidbody != null) return false;
            if (Mathf.Abs(hit.normal.y) > 0.1f) return false;
            Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, n);
            // A real wall: tall and wide (1.5 m to each side, the parallel lying test needs it), flat.
            foreach (var off in new[] { side * 1.5f, -side * 1.5f, side * 0.7f, -side * 0.7f, Vector3.up * 1.1f })
            {
                if (!Physics.Raycast(hit.point + n * 1f + off, -n, out var h2, 1.2f, mask, q)) return false;
                if (Mathf.Abs(h2.distance - 1f) > 0.04f || Vector3.Dot(h2.normal, n) < 0.98f) return false;
            }
            // Thin, with an open room behind it for the camera.
            Vector3 behind = hit.point - n * 1.0f;
            if (Physics.CheckSphere(behind, 0.05f, mask, q) || !Physics.Raycast(behind, n, out var far, 1.0f, mask, q)) return false;
            float thick = 1.0f - far.distance;
            if (thick < 0.03f || thick > 0.45f) return false;
            Vector3 cam = hit.point - n * (thick + 1.6f) + Vector3.up * 0.6f;
            if (Physics.CheckSphere(cam, 0.3f, mask, q) || Physics.Linecast(cam, far.point - n * 0.05f, mask, q)) return false;
            if (!Physics.Raycast(behind + Vector3.up * 0.2f, Vector3.down, 0.8f, mask, q)) return false; // floor behind
            Vector3 stand = new Vector3(hit.point.x, spot.y, hit.point.z) + n * 0.45f;
            if (Physics.CheckCapsule(stand + Vector3.up * 0.4f, stand + Vector3.up * 1.5f, 0.3f, mask, q)) return false;
            w = new LegWall
            {
                Ok = true, Point = new Vector3(hit.point.x, spot.y, hit.point.z), Normal = n, Side = side, Spot = stand,
                Thick = thick, Yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg
            };
            return true;
        }

        // ------------------------------------------------------------------ client: the hider

        private IEnumerator LegClient(LegWall w, float t0)
        {
            var me = NetPlayer.Local;
            var m = me.Motor;
            var c = new MotorCommand();
            float rate = 0f;
            yield return LgUntil(t0 + 0.5f);
            me.TeleportLocal(w.Spot, w.Yaw);
            m.SetLook(w.Yaw, 0f);
            m.CommandProvider = () => { var r = c; r.Look = new Vector2(rate * Time.deltaTime, 0f); return r; };
            yield return LgUntil(t0 + 2f);
            c.Crouch = true;
            yield return LgUntil(t0 + 3f);
            c.Move = new Vector2(0f, -1f);
            yield return LgUntil(t0 + 5f);
            c.Move = Vector2.zero;
            LgLog($"client crouched backing up: root {LgRootGap(me, w):0.00} m from the wall");
            yield return LgUntil(t0 + 7f);
            c.Crouch = false;
            c.Prone = true;
            yield return LgUntil(t0 + 8.5f);
            LgLog($"client lay down: root {LgRootGap(me, w):0.00} m from the wall prone={m.ProneAmount:0.00}");
            yield return LgUntil(t0 + 9f);
            c.Move = new Vector2(0f, -1f);
            yield return LgUntil(t0 + (BodyRoom.MotorPush ? 11f : 10f));
            c.Move = Vector2.zero;
            float gap = LgRootGap(me, w);
            LgLog($"client crawled back: root {gap:0.00} m from the wall (body needs 1.36) -> {Pf(!BodyRoom.Enabled || !BodyRoom.MotorPush || gap > 1.3f)}");
            if (!BodyRoom.MotorPush)
            {
                // Fold only: hips in front of the wall, boots would be through it (what the motor normally prevents).
                me.TeleportLocal(w.Spot + w.Normal * 0.55f, w.Yaw);
            }

            // Lying parallel to the wall, side 0.45 m from it, then turning the legs towards it.
            yield return LgUntil(t0 + 12.5f);
            float yawPar = Mathf.Atan2(w.Side.x, w.Side.z) * Mathf.Rad2Deg;
            me.TeleportLocal(w.Spot, yawPar);
            m.SetLook(yawPar, 0f);
            yield return LgUntil(t0 + 13.5f);
            float yaw0 = me.transform.eulerAngles.y;
            rate = Vector3.SignedAngle(w.Side, w.Normal, Vector3.up) > 0f ? 50f : -50f; // head swings away, legs to the wall
            var boxes = new PartBox[BodyBoxes.Count];
            float legMin = 9f, hipMin = 9f;
            while (Time.time < t0 + 15f)
            {
                LgWallDepth(me, w, boxes, ref legMin, ref hipMin);
                yield return null;
            }
            rate = 0f;
            LgLog($"client turned: wanted 75 deg, got {Mathf.Abs(Mathf.DeltaAngle(yaw0, me.transform.eulerAngles.y)):0} deg, root {LgRootGap(me, w):0.00} m " +
                  $"from the wall, own body during the turn: deepest leg {-legMin:0.00} m into the wall -> {Pf(!BodyRoom.Enabled || legMin > -0.08f)}");
            yield return LgUntil(t0 + 18f);
            c.Prone = false;
            yield return LgUntil(t0 + 19.5f);
            me.TeleportLocal(w.Spot, w.Yaw);
            m.SetLook(w.Yaw, 0f);
            yield return LgUntil(t0 + 19.9f); // downed by the host at +20
            string track = "";
            while (Time.time < t0 + 22f)
            {
                track += $" {Time.time - t0:0.00}:{LgRootGap(me, w):0.00}{(m.Downed ? "d" : "")}";
                yield return new WaitForSeconds(0.2f);
            }
            LgLog("client root gap while going down:" + track);
            LgLog($"client downed: root {LgRootGap(me, w):0.00} m from the wall downed={m.Downed}");
            c.Move = new Vector2(0f, -1f);
            yield return LgUntil(t0 + 24f);
            c.Move = Vector2.zero;
            gap = LgRootGap(me, w);
            LgLog($"client downed crawl back: root {gap:0.00} m from the wall -> {Pf(!BodyRoom.Enabled || !BodyRoom.MotorPush || gap > 1.3f)}");
            yield return LgUntil(t0 + 28f);
            m.CommandProvider = null;
            LgLog("client done");
        }

        private static float LgRootGap(NetPlayer p, LegWall w) => Vector3.Dot(p.transform.position - w.Point, w.Normal);

        // ------------------------------------------------------------------ host: the observer

        private IEnumerator LegHost(string dir, LegWall w, float t0)
        {
            var h = SfHider();
            var mm = MatchManager.Instance;
            if (h == null || h.Character == null) { LgLog("need a hider client - FAIL"); yield break; }
            var boxes = new PartBox[BodyBoxes.Count];
            // Measured windows (the teleports and the getting up in between are left out).
            string[] names = { "stand", "crouch", "prone", "prone-turn", "downed" };
            float[] starts = { 1.4f, 2f, BodyRoom.MotorPush ? 7f : 11.2f, 13.4f, 20f };
            float[] ends = { 2f, 6.5f, 12.4f, 18f, 26f };
            float[] mins = { 9f, 9f, 9f, 9f, 9f };
            float[] hipMins = { 9f, 9f, 9f, 9f, 9f };
            string[] worst = new string[5];
            float[] shots = { 1.6f, 5.6f, 11.6f, 15.4f, 24.6f };
            string[] shotNames = { "a_stand", "b_crouch", "c_prone_back", "d_prone_turn", "e_downed_back" };
            int shot = 0, ph = 0;
            bool downed = false;
            while (ph < names.Length)
            {
                float t = Time.time - t0;
                if (t >= ends[ph])
                {
                    bool ok = mins[ph] > -0.08f && mins[ph] > -w.Thick; // never out the other side, at most a boot tip in the wall
                    LgLog($"{names[ph]}: deepest leg {-mins[ph]:0.00} m into the wall (hips {-hipMins[ph]:0.00}), wall {w.Thick:0.00} thick, " +
                          $"out the other side={(mins[ph] < -w.Thick ? "YES" : "no")} (worst {worst[ph]}) -> {Pf(ok)}");
                    ph++;
                    continue;
                }
                if (t >= starts[ph])
                {
                    float before = mins[ph];
                    LgWallDepth(h, w, boxes, ref mins[ph], ref hipMins[ph]);
                    if (mins[ph] < before && mins[ph] < 0f)
                    {
                        var la = h.Actions as BlockyCharacterActions;
                        worst[ph] = $"t={t:0.00} gap={LgRootGap(h, w):0.00} yaw={h.transform.eulerAngles.y:0} prone={h.Motor.ProneAmount:0.00} " +
                                    $"room={(la != null ? la.LegRoomBehind : -1f):0.00} fold={(la != null ? la.LegFoldWeight : -1f):0.00}";
                    }
                }
                if (!downed && t >= 20f)
                {
                    downed = true;
                    h.DownedUntil.Value = mm.Now + 120d;
                    h.Life.Value = LifeState.Downed;
                }
                if (shot < shots.Length && t >= shots[shot])
                {
                    var a = h.Actions as BlockyCharacterActions;
                    LgLog($"shot {shotNames[shot]}: hider root {LgRootGap(h, w):0.00} m from the wall, prone={h.Motor.ProneAmount:0.00} " +
                          $"room={(a != null ? a.LegRoomBehind : -1f):0.00} fold={(a != null ? a.LegFoldWeight : -1f):0.00}");
                    Vector3 sw = h.transform.position + Vector3.up * 0.2f;
                    if (BodyRoom.SweepWall(sw, sw - h.transform.forward * 1.5f, 0.14f, h.transform, out var sh))
                        LgLog($"  sweep back hits '{sh.collider.name}' at {sh.distance:0.00} point {sh.point:F2} normal {sh.normal:F2}");
                    LgShots(dir, (!BodyRoom.Enabled ? "before_" : BodyRoom.MotorPush ? "after_" : "foldonly_") + shotNames[shot], w, h);
                    shot++;
                }
                yield return null;
            }
            h.Life.Value = LifeState.Alive;
            LgLog("done");
        }

        /// <summary>Deepest point of the leg boxes (and of the torso box) behind the near face of the wall (negative = inside).</summary>
        private static void LgWallDepth(NetPlayer h, LegWall w, PartBox[] boxes, ref float legMin, ref float hipMin)
        {
            var ch = h.Character;
            BodyBoxes.Get(CharacterPose.Capture(ch), boxes);
            for (int i = 0; i < BodyBoxes.Count; i++)
            {
                bool leg = i >= (int)BodyPart.ThighR;
                if (!leg && i != (int)BodyPart.Torso) continue;
                var b = boxes[i].Transformed(ch.transform.position, ch.transform.rotation);
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = b.Point((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f);
                    float d = Vector3.Dot(corner - w.Point, w.Normal);
                    if (leg) legMin = Mathf.Min(legMin, d);
                    else hipMin = Mathf.Min(hipMin, d);
                }
            }
        }

        /// <summary>From the other side of the wall at the hider, and from the side along the wall.</summary>
        private static void LgShots(string dir, string name, LegWall w, NetPlayer h)
        {
            Vector3 at = h.transform.position;
            Vector3 onWall = at - w.Normal * Vector3.Dot(at - w.Point, w.Normal);
            Vector3 farFace = onWall - w.Normal * w.Thick;
            ItemLitShot(dir, name + "_behind", farFace - w.Normal * 1.6f + Vector3.up * 0.6f, farFace + Vector3.up * 0.15f);
            ItemLitShot(dir, name + "_side", onWall + w.Normal * 0.8f + w.Side * 2.4f + Vector3.up * 1.0f, onWall + w.Normal * 0.6f + Vector3.up * 0.15f);
        }
    }
}
