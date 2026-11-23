using System.Collections;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby tests:
    ///  -testseats (host, -seatshots dir): sits down 4x at random (logs the poses, never the same twice in a row),
    ///      then every pose on a sofa seat and on a pouf with side / front shots of the own body, and head turning
    ///      (yaw clamp, body stays still).
    ///  -seatwatch (client, -seatshots dir): photographs the other player while seated (synced pose and head).
    ///  -testseatfit (host, -seatshots dir): every seat x every pose: first-person shots (ahead, down, down-left,
    ///      down-right, left, right; the look is clamped by the pose), the camera checked against the model's eyes
    ///      (body drawn, head hidden, view model hidden), third-person side / front shots; then sitting down and
    ///      standing up frame by frame on a sofa and a pouf (first-person shots every 0.1 s, largest camera step per frame).
    ///      The pin-up poses (4-11, only where the seat offers them, looks relative to the pose's neutral look) are
    ///      included; at the end every pose change without standing up on a sofa and a pouf (log of the largest camera
    ///      step, shots of the changes from pose 0 to each pin-up pose).
    ///  -testbrickarea (host, -brickshots dir): bricks in the four corners of the bigger plate, towers at the corners
    ///      next to the furniture, overview shots.
    /// </summary>
    public partial class NetSession
    {
        private void StartSeatTests(System.Func<string, bool> has)
        {
            if (has("-testseats")) StartCoroutine(SeatsTest());
            if (has("-seatwatch")) StartCoroutine(SeatWatch());
            if (has("-testseatfit")) StartCoroutine(SeatFitTest());
            if (has("-testbrickarea")) StartCoroutine(BrickAreaTest());
            StartWheelTests(has);
        }

        private static IEnumerator WaitLobby()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || LobbySeat.All.Count == 0) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(2.5f);
        }

        private static void TestSitOn(NetPlayer me, LobbySeat seat)
        {
            me.PrepareSit();
            var cc = me.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            me.TeleportLocal(seat.transform.position, seat.transform.eulerAngles.y);
            me.Seat.Value = (sbyte)seat.Index;
        }

        private static void TestStandUp(NetPlayer me, LobbySeat seat)
        {
            me.Seat.Value = -1;
            var cc = me.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            me.TeleportLocal(seat.transform.position + seat.transform.forward * 0.6f, me.transform.eulerAngles.y);
        }

        private IEnumerator SeatsTest()
        {
            yield return WaitLobby();
            var me = NetPlayer.Local;
            string dir = ArgOr("-seatshots", "");
            LobbySeat sofa = null, pouf = null;
            foreach (var s in LobbySeat.All)
            {
                bool isPouf = s.transform.parent != null && s.transform.parent.name.StartsWith("Pouf");
                if (isPouf && pouf == null) pouf = s;
                if (!isPouf && sofa == null) sofa = s;
            }
            if (sofa == null) sofa = LobbySeat.All[0];
            if (pouf == null) pouf = sofa;

            // Random poses: never the same one twice in a row.
            string seq = "";
            for (int i = 0; i < 6; i++)
            {
                TestSitOn(me, sofa);
                yield return new WaitForSeconds(0.4f);
                seq += me.SitPoseVar.Value + " ";
                TestStandUp(me, sofa);
                yield return new WaitForSeconds(0.3f);
            }
            Debug.Log($"[SeatTest] random poses: {seq}");

            foreach (var seat in new[] { sofa, pouf })
            {
                string where = seat == sofa ? "sofa" : "pouf";
                for (int v = 0; v < BlockyCharacterActions.SitPoseCount; v++)
                {
                    if (!SitPoseCatalog.Allowed(seat.Shape, (byte)v)) continue;
                    NetPlayer.TestSitPose = v;
                    TestSitOn(me, seat);
                    yield return new WaitForSeconds(SeatPoses.Lying(v) ? 1.3f : 0.9f);
                    me.SetCinematicBody(true);
                    var t = seat.transform;
                    Vector3 at = t.position + Vector3.up * 0.75f - t.forward * 0.05f;
                    CamShot(dir, $"sit_{where}_{v}_side", t.position + t.right * 2.0f + t.forward * 1.1f + Vector3.up * 1.25f, at);
                    CamShot(dir, $"sit_{where}_{v}_front", t.position + t.forward * 2.3f + t.right * 0.8f + Vector3.up * 1.3f, at);
                    me.SetCinematicBody(false);
                    Debug.Log($"[SeatTest] {where} pose {me.SitPoseVar.Value} body yaw={me.transform.eulerAngles.y:0.0} seat yaw={t.eulerAngles.y:0.0}");
                    TestStandUp(me, seat);
                    yield return new WaitForSeconds(0.5f);
                }
            }

            // Head turning: the mouse turns the head (clamped), the body stays.
            NetPlayer.TestSitPose = 0;
            TestSitOn(me, sofa);
            yield return new WaitForSeconds(0.8f);
            float seatYaw = sofa.transform.eulerAngles.y;
            var st = sofa.transform;
            Vector3 hat = st.position + Vector3.up * 1.1f;
            var turns = new (string name, float yaw, float pitch)[] { ("r50", 50f, 0f), ("r_clamp", 70f, 10f), ("l_clamp", -150f, -30f), ("down", 60f, -70f) };
            foreach (var (name, yaw, pitch) in turns)
            {
                me.Motor.SetLook(me.transform.eulerAngles.y + yaw, pitch);
                yield return new WaitForSeconds(0.5f);
                me.SetCinematicBody(true);
                CamShot(dir, $"head_{name}", st.position + st.forward * 2.2f + Vector3.up * 1.4f, hat);
                me.SetCinematicBody(false);
                Shot(dir, $"head_{name}_fpp");
                yield return new WaitForSeconds(0.3f);
                Debug.Log($"[SeatTest] turn {name}: headYaw={me.SeatHeadYawNow:0.0} synced={me.SitHeadYaw.Value} pitch={me.Motor.LookPitch:0.0} body yaw delta={Mathf.DeltaAngle(seatYaw, me.transform.eulerAngles.y):0.0}");
                yield return new WaitForSeconds(1.2f);
            }
            float before = me.SeatHeadYawNow;
            TestStandUp(me, sofa);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[SeatTest] stood up: look yaw delta from seat={Mathf.DeltaAngle(seatYaw, me.transform.eulerAngles.y):0.0} (head was {before:0.0})");
            Debug.Log("[SeatTest] done");
        }

        private IEnumerator SeatFitTest()
        {
            yield return WaitLobby();
            var me = NetPlayer.Local;
            string dir = ArgOr("-seatshots", "");
            var seats = new System.Collections.Generic.List<LobbySeat>(LobbySeat.All);
            var looks = new (string name, float yaw, float pitch)[]
            {
                ("ahead", 0f, 0f), ("down", 0f, -90f), ("downleft", -90f, -90f), ("downright", 90f, -90f), ("left", -90f, 0f), ("right", 90f, 0f)
            };
            // Idle motion must not move the own first-person camera: sit still 8 s without touching the look
            // (the head would start looking round after 2.5 s if the own body were not recognised as first person).
            foreach (int v in new[] { 0, 4 })
            {
                var s0 = seats.Find(x => SitPoseCatalog.Allowed(x.Shape, (byte)v));
                if (s0 == null) continue;
                NetPlayer.TestSitPose = v;
                TestSitOn(me, s0);
                yield return new WaitForSeconds(SeatPoses.Lying(v) ? 1.8f : 1.3f);
                Vector3 p0 = me.Camera.transform.position;
                Quaternion r0 = me.Camera.transform.rotation;
                float maxMove = 0f, maxTurn = 0f, t0 = Time.time;
                while (Time.time - t0 < 8f)
                {
                    yield return null;
                    maxMove = Mathf.Max(maxMove, (me.Camera.transform.position - p0).magnitude);
                    maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(r0, me.Camera.transform.rotation));
                }
                Debug.Log($"[SeatFit] idle camera pose {v}: moved {maxMove * 100f:0.00} cm, turned {maxTurn:0.00} deg over 8 s (head hidden {me.Character.HeadHidden})");
                TestStandUp(me, s0);
                yield return new WaitForSeconds(0.8f);
            }

            foreach (var seat in seats)
            {
                for (int v = 0; v < BlockyCharacterActions.SitPoseCount; v++)
                {
                    if (!SitPoseCatalog.Allowed(seat.Shape, (byte)v)) continue;
                    NetPlayer.TestSitPose = v;
                    TestSitOn(me, seat);
                    yield return new WaitForSeconds(SeatPoses.Lying(v) ? 1.8f : 1.3f);
                    var lim = SeatPoses.Limits(v, seat.Shape);
                    var act = me.Actions as BlockyCharacterActions;
                    foreach (var (name, yaw, pitch) in looks)
                    {
                        // Looks relative to the pose's neutral look (lying poses look along the sofa).
                        me.Motor.SetLook(me.transform.eulerAngles.y + lim.Center + yaw - me.SeatHeadYawNow, pitch);
                        yield return new WaitForSeconds(0.4f);
                        var cam = me.Camera.transform;
                        Vector3 eye = me.Character.Head.position + cam.rotation * Quaternion.Euler(0f, 0f, act != null ? act.SitHeadRoll : 0f) * SeatPoses.EyeOffset;
                        Debug.Log($"[SeatFit] {seat.name} ({seat.Shape.Kind}) pose {v} look {name}: head yaw {me.SeatHeadYawNow:0} pitch {me.Motor.LookPitch:0} " +
                                  $"(limits yaw +-{lim.Yaw:0} up {lim.Up:0} down {lim.Down:0}), camera-eye {(cam.position - eye).magnitude * 100f:0.0} cm, " +
                                  $"body shown {me.SeatBodyShown}, head hidden {me.Character.HeadHidden}, first person {me.Character.IsFirstPerson}");
                        Shot(dir, $"fit_{seat.name}_p{v}_{name}");
                        yield return null;
                    }
                    me.Motor.SetLook(me.transform.eulerAngles.y - me.SeatHeadYawNow, 0f);
                    yield return new WaitForSeconds(0.3f);
                    me.SetCinematicBody(true);
                    var t = seat.transform;
                    Vector3 at = t.position + Vector3.up * 0.75f - t.forward * 0.1f;
                    CamShot(dir, $"fit_{seat.name}_p{v}_tpp_side", t.position + t.right * 1.9f + t.forward * 0.9f + Vector3.up * 1.2f, at);
                    CamShot(dir, $"fit_{seat.name}_p{v}_tpp_front", t.position + t.forward * 2.2f - t.right * 0.6f + Vector3.up * 1.35f, at);
                    CamShot(dir, $"fit_{seat.name}_p{v}_tpp_low", t.position + t.forward * 1.4f + t.right * 0.5f + Vector3.up * 0.35f, at - Vector3.up * 0.3f);
                    me.SetCinematicBody(false);
                    TestStandUp(me, seat);
                    yield return new WaitForSeconds(0.8f);
                }
            }

            // Sitting down / standing up: the camera follows the model's eyes, no jumps.
            LobbySeat sofa = null, pouf = null;
            foreach (var s in seats)
            {
                if (s.Shape.Kind == SeatKind.Pouf) { if (pouf == null) pouf = s; }
                else if (sofa == null) sofa = s;
            }
            foreach (var seat in new[] { sofa, pouf })
            {
                if (seat == null) continue;
                var t = seat.transform;
                var cc = me.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
                me.TeleportLocal(t.position + t.forward * 1.5f + t.right * 0.3f, t.eulerAngles.y + 180f);
                me.Motor.SetLook(t.eulerAngles.y + 180f, -25f);
                yield return new WaitForSeconds(1f);
                NetPlayer.TestSitPose = 0;
                Vector3 last = me.Camera.transform.position;
                float maxStep = 0f, start = Time.time;
                int shot = 0;
                float nextShot = 0f;
                TestSitOn(me, seat);
                while (Time.time - start < 1.2f)
                {
                    yield return null;
                    Vector3 c = me.Camera.transform.position;
                    maxStep = Mathf.Max(maxStep, (c - last).magnitude);
                    last = c;
                    if (Time.time - start >= nextShot) { nextShot += 0.1f; Shot(dir, $"fit_trans_{seat.name}_sit_{shot++:00}"); }
                }
                Debug.Log($"[SeatFit] sit down on {seat.name}: largest camera step per frame {maxStep * 100f:0.0} cm");
                yield return new WaitForSeconds(0.5f);
                maxStep = 0f;
                start = Time.time;
                shot = 0;
                nextShot = 0f;
                last = me.Camera.transform.position;
                TestStandUp(me, seat);
                while (Time.time - start < 1f)
                {
                    yield return null;
                    Vector3 c = me.Camera.transform.position;
                    maxStep = Mathf.Max(maxStep, (c - last).magnitude);
                    last = c;
                    if (Time.time - start >= nextShot) { nextShot += 0.1f; Shot(dir, $"fit_trans_{seat.name}_stand_{shot++:00}"); }
                }
                Debug.Log($"[SeatFit] stand up from {seat.name}: largest camera step per frame {maxStep * 100f:0.0} cm, body shown after {me.SeatBodyShown}");
                yield return new WaitForSeconds(0.5f);
            }
            // Changing poses without standing up (pose wheel route: SitPoseVar written by the owner), every pose of
            // the seat from every other one: first-person shots during the change, largest camera step per frame.
            foreach (var seat in new[] { sofa, pouf })
            {
                if (seat == null) continue;
                var poses = SitPoseCatalog.For(seat.Shape);
                NetPlayer.TestSitPose = 0;
                TestSitOn(me, seat);
                yield return new WaitForSeconds(1.3f);
                foreach (var from in poses)
                foreach (var to in poses)
                {
                    if (from.Id == to.Id) continue;
                    me.SitPoseVar.Value = from.Id;
                    yield return new WaitForSeconds(1.3f);
                    Vector3 last = me.Camera.transform.position;
                    float maxStep = 0f, start = Time.time, nextShot = 0f;
                    int shot = 0;
                    me.SitPoseVar.Value = to.Id;
                    while (Time.time - start < 1.3f)
                    {
                        yield return null;
                        Vector3 c = me.Camera.transform.position;
                        maxStep = Mathf.Max(maxStep, (c - last).magnitude);
                        last = c;
                        if (to.IsNew && from.Id == 0 && Time.time - start >= nextShot) { nextShot += 0.15f; Shot(dir, $"fit_switch_{seat.name}_{from.Id}to{to.Id}_{shot++:00}"); }
                    }
                    Debug.Log($"[SeatFit] switch on {seat.name}: {from.Id} {from.Name} -> {to.Id} {to.Name}: largest camera step per frame {maxStep * 100f:0.0} cm, " +
                              $"head yaw {me.SeatHeadYawNow:0} (neutral {SeatPoses.Limits(to.Id, seat.Shape).Center:0})");
                }
                me.SitPoseVar.Value = 0;
                yield return new WaitForSeconds(1f);
                TestStandUp(me, seat);
                yield return new WaitForSeconds(0.8f);
            }
            Debug.Log("[SeatFit] done");
        }

        private IEnumerator SeatWatch()
        {
            yield return WaitLobby();
            string dir = ArgOr("-seatshots", "");
            var me = NetPlayer.Local;
            int n = 0, lastPose = -1, lastYaw = 999;
            float lastBodyYaw = 0f;
            for (int i = 0; i < 900 && n < 30; i++)
            {
                yield return new WaitForSeconds(0.1f);
                NetPlayer o = null;
                foreach (var p in NetPlayer.All) if (p != null && p != me && p.Seat.Value >= 0) o = p;
                if (o == null) { lastPose = -1; continue; }
                if (Mathf.Abs(Mathf.DeltaAngle(lastBodyYaw, o.transform.eulerAngles.y)) > 1f && lastPose >= 0)
                    Debug.Log($"[SeatWatch] body yaw changed while seated: {lastBodyYaw:0.0} -> {o.transform.eulerAngles.y:0.0}");
                lastBodyYaw = o.transform.eulerAngles.y;
                int pose = o.SitPoseVar.Value, yaw = o.SitHeadYaw.Value;
                if (pose == lastPose && Mathf.Abs(yaw - lastYaw) < 15) continue;
                lastPose = pose;
                lastYaw = yaw;
                yield return new WaitForSeconds(0.6f);
                var t = o.transform;
                CamShot(dir, $"watch_{n++:00}_pose{pose}_yaw{o.SitHeadYaw.Value}", t.position + t.forward * 2.2f + t.right * 1.2f + Vector3.up * 1.3f, t.position + Vector3.up * 0.8f);
                Debug.Log($"[SeatWatch] frame {n}: pose={o.SitPoseVar.Value} headYaw={o.SitHeadYaw.Value} shown={o.SeatHeadYawNow:0.0}");
            }
        }

        private IEnumerator BrickAreaTest()
        {
            yield return WaitLobby();
            while (LobbyBricks.Instance == null) yield return null;
            var me = NetPlayer.Local;
            string dir = ArgOr("-brickshots", "");
            int X = LobbyBricks.SizeX - 1, Z = LobbyBricks.SizeZ - 1;
            Vector3 lo = LobbyBricks.CellBottom(0, 0, 0), hi = LobbyBricks.CellBottom(X, 0, Z);
            Debug.Log($"[BrickArea] grid {LobbyBricks.SizeX}x{LobbyBricks.SizeZ}x{LobbyBricks.SizeY}, plate x {lo.x - LobbyBricks.Cell / 2:0.00}..{hi.x + LobbyBricks.Cell / 2:0.00} z {lo.z - LobbyBricks.Cell / 2:0.00}..{hi.z + LobbyBricks.Cell / 2:0.00}");
            me.Motor.Teleport(LobbyBricks.PlateCenter + Vector3.up * 0.05f, 0f);
            yield return new WaitForSeconds(0.5f);
            int c = 0;
            foreach (var (x, z) in new[] { (0, 0), (X, 0), (0, Z), (X, Z) })
            {
                for (int y = 0; y < LobbyBricks.SizeY; y++)
                {
                    me.RequestPlaceBrickRpc(LobbyBricks.Pack(x, y, z, c % LobbyBricks.Colors.Length));
                    yield return null;
                }
                c++;
            }
            for (int x = 1; x < X; x++) { me.RequestPlaceBrickRpc(LobbyBricks.Pack(x, 0, 0, 4)); me.RequestPlaceBrickRpc(LobbyBricks.Pack(x, 0, Z, 4)); yield return null; }
            for (int z = 1; z < Z; z++) { me.RequestPlaceBrickRpc(LobbyBricks.Pack(0, 0, z, 2)); me.RequestPlaceBrickRpc(LobbyBricks.Pack(X, 0, z, 2)); yield return null; }
            yield return new WaitForSeconds(1f);
            Debug.Log($"[BrickArea] bricks={MatchManager.Instance.Bricks.Count}");
            me.Motor.Teleport(new Vector3(-2.2f, 0.05f, 1.2f), 0f);
            Vector3 pc = LobbyBricks.PlateCenter;
            CamShot(dir, "area_from_room", new Vector3(-2.0f, 2.6f, 0.6f), pc + Vector3.up * 0.6f);
            CamShot(dir, "area_from_above", pc + new Vector3(0.3f, 3.3f, -2.4f), pc);
            CamShot(dir, "area_west_corner", new Vector3(-4.0f, 1.6f, 2.0f), new Vector3(-6.6f, 0.8f, 4.6f));
            CamShot(dir, "area_east_corner", new Vector3(-5.0f, 1.6f, 2.2f), new Vector3(-3.2f, 0.9f, 5.2f));
            Debug.Log("[BrickArea] done");
        }
    }
}
