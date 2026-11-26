using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Kart circuit test (-testcircuit &lt;dir&gt;, host alone: -host -nosteam -nodiscord -nodiscovery -port N): checks the
    /// geometry of <see cref="DriftTrackCircuit"/> in the built lobby with raycasts (the road is under every row at
    /// the centre and near both edges at the expected height, no gaps or lips, barrier colliders along both sides - tall
    /// ones on the raised road -, the paddock gap open, at least 2.2 m of head room in the tunnel and under the bridge,
    /// the kart slots on the paddock clear of the road, everything inside the pad), takes pictures (overview, the climb,
    /// the crossover from below and above, inside the tunnel, a hairpin, the start gantry), then drives one lap in kart 0
    /// on a simple autopilot (follows the centre line, slows for corners) with chase-camera pictures on the way.
    /// Logs: [TrackTest] ... PASS / FAIL, a RESULT line, then quits.
    /// </summary>
    public partial class NetSession
    {
        private static int _circuitFails;

        private static void TLog(string s) => Debug.Log("[TrackTest] " + s);
        private static string Cf(bool ok) { if (!ok) _circuitFails++; return Pf(ok); }

        private IEnumerator CircuitTest(string dir)
        {
            yield return WaitKartLobby(1);
            // -batchmode has no input devices: LocalPlayerController skips its prompts while Keyboard.current is null.
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            _circuitFails = 0;
            Physics.SyncTransforms();
            var c = DriftTrackCircuit.Center;
            var nrm = DriftTrackCircuit.Normal;
            int n = c.Length;
            int mask = KartCollide.LevelMask;
            float maxH = 0f;
            foreach (var p in c) maxH = Mathf.Max(maxH, p.y);
            TLog($"circuit: {DriftTrackCircuit.Length:0} m, {n} rows, {DriftTrackCircuit.CornerCount} corners, top {maxH:0.00} m -> {Cf(DriftTrackCircuit.CornerCount >= 14 && maxH > 4f)}");

            // 1. The road under every row: centre and both edges, expected height, no lips.
            int gaps = 0, off = 0, tilt = 0;
            float worstOff = 0f, maxGrade = 0f, maxGradeStep = 0f, prevGrade = float.NaN;
            Vector3 prevHit = Vector3.zero;
            for (int i = 0; i <= n; i++)
            {
                int r = i % n;
                foreach (float lat in new[] { -5.5f, 0f, 5.5f })
                {
                    // a third of the way to the next row: inside the triangles, off the chunk seams
                    Vector3 want = Vector3.Lerp(DriftTrackCircuit.Point(r, lat), DriftTrackCircuit.Point(r + 1, lat), 0.3f);
                    if (!Physics.Raycast(want + nrm[r] * 1.5f, -nrm[r], out var hit, 3f, mask, QueryTriggerInteraction.Ignore))
                    {
                        if (gaps++ < 5) TLog($"  no road under row {r} lat {lat} at {want}");
                        continue;
                    }
                    float d = Vector3.Dot(hit.point - want, nrm[r]);
                    worstOff = Mathf.Max(worstOff, Mathf.Abs(d));
                    if (Mathf.Abs(d) > 0.05f && off++ < 5) TLog($"  road off by {d:0.000} m at row {r} lat {lat} ({hit.collider.name})");
                    if (Vector3.Dot(hit.normal, nrm[r]) < 0.98f && tilt++ < 5) TLog($"  surface tilted at row {r} lat {lat}: normal {hit.normal} want {nrm[r]} ({hit.collider.name})");
                    if (lat != 0f) continue;
                    if (i > 0)
                    {
                        Vector3 dp = hit.point - prevHit;
                        float run = new Vector2(dp.x, dp.z).magnitude;
                        if (run > 0.2f)
                        {
                            float g = dp.y / run;
                            maxGrade = Mathf.Max(maxGrade, Mathf.Abs(g));
                            if (!float.IsNaN(prevGrade)) maxGradeStep = Mathf.Max(maxGradeStep, Mathf.Abs(g - prevGrade));
                            prevGrade = g;
                        }
                    }
                    prevHit = hit.point;
                }
            }
            TLog($"road surface: {gaps} gaps, {off} rows off height (worst {worstOff:0.000} m), {tilt} tilted -> {Cf(gaps == 0 && off == 0 && tilt == 0)}");
            TLog($"smoothness: max grade {maxGrade * 100f:0.0} %, max grade change per row {maxGradeStep * 100f:0.00} % (a lip would show here) -> {Cf(maxGrade < 0.09f && maxGradeStep < 0.03f)}");

            // 2. Barriers along both sides (tall ones on the raised road), the paddock gap open.
            int missing = 0, missingHigh = 0, low = 0, gapRows = 0, gapBlocked = 0;
            for (int r = 0; r < n; r++)
                foreach (int side in new[] { -1, 1 })
                {
                    var openSide = side < 0 ? DriftTrackCircuit.OpenLeft : DriftTrackCircuit.OpenRight;
                    bool open = openSide[r];
                    if (!open && (openSide[DriftTrackCircuit.Wrap(r - 1)] || openSide[DriftTrackCircuit.Wrap(r + 1)])) continue; // a run's end row
                    Vector3 o = DriftTrackRoad.Flat(DriftTrackCircuit.Right[r]) * side;
                    Vector3 from = DriftTrackCircuit.Point(r, side * 4.8f) + nrm[r] * 0.45f;
                    bool hitLow = Physics.Raycast(from, o, out var h0, 2.5f, mask, QueryTriggerInteraction.Ignore);
                    if (open)
                    {
                        bool inner = openSide[DriftTrackCircuit.Wrap(r - 2)] && openSide[DriftTrackCircuit.Wrap(r + 2)]; // the tyre bundles stand at the ends
                        gapRows++;
                        if (hitLow && inner) gapBlocked++;
                        continue;
                    }
                    if (!hitLow) { if (missing++ < 5) TLog($"  no barrier at row {r} side {side} ({c[r]})"); continue; }
                    if (c[r].y > 0.4f)
                    {
                        bool hitHigh = Physics.Raycast(from + Vector3.up * 0.9f, o, 2.5f, mask, QueryTriggerInteraction.Ignore);
                        if (!hitHigh && low++ < 5) TLog($"  barrier too low on the raised road at row {r} side {side} ({c[r]})");
                        if (!hitHigh) missingHigh++;
                    }
                }
            TLog($"barriers: {missing} rows without, {missingHigh} raised rows without a tall collider; paddock gap {gapRows} rows, {gapBlocked} blocked -> {Cf(missing == 0 && missingHigh == 0 && gapRows > 20 && gapBlocked == 0)}");

            // 3. Head room in the tunnel and under the bridge.
            int covered = 0, tunnelRows = 0, tunnelOpen = 0;
            float minClear = float.MaxValue;
            for (int r = 0; r < n; r++)
            {
                bool tun = DriftTrackCircuit.Tunnel[r];
                if (tun) tunnelRows++;
                if (Physics.Raycast(c[r] + nrm[r] * 0.2f, Vector3.up, out var up, 8f, mask, QueryTriggerInteraction.Ignore))
                {
                    covered++;
                    minClear = Mathf.Min(minClear, up.distance + 0.2f);
                }
                else if (tun) tunnelOpen++;
            }
            TLog($"head room: {covered} covered rows (tunnel {tunnelRows}, {tunnelOpen} of them open), min clearance {minClear:0.00} m -> {Cf(tunnelRows > 50 && tunnelOpen == 0 && covered > tunnelRows + 8 && minClear >= 2.2f)}");

            // 4. Inside the pad; the kart slots on the paddock floor, clear of the road.
            int outside = 0;
            for (int r = 0; r < n; r++)
                foreach (float lat in new[] { -DriftTrackCircuit.DeckHalf, DriftTrackCircuit.DeckHalf })
                    if (!DriftTrackLayout.InsidePad(DriftTrackCircuit.Point(r, lat) + Vector3.up * 0.3f, 0.5f)) outside++;
            bool slotsOk = true;
            for (int k = 0; k < DriftTrackLayout.KartCount; k++)
            {
                Vector3 s = DriftTrackLayout.KartSlot(k);
                int near = DriftTrackCircuit.Nearest(s);
                float lat = Mathf.Abs(DriftTrackCircuit.Lateral(s, near));
                float dist = Vector2.Distance(new Vector2(s.x, s.z), new Vector2(c[near].x, c[near].z));
                bool floor = Physics.Raycast(s + Vector3.up * 2f, Vector3.down, out var fh, 3f, mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(fh.point.y) < 0.05f;
                slotsOk &= floor && dist > DriftTrackCircuit.DeckHalf + 2f && DriftTrackLayout.InsidePad(s);
                TLog($"  kart slot {k} at {s}: floor {floor}, {dist:0.0} m from the circuit (lateral {lat:0.0})");
            }
            TLog($"bounds: {outside} deck edge points outside the pad; kart slots ok {slotsOk} -> {Cf(outside == 0 && slotsOk)}");

            // 5. Pictures.
            DriftTrack.ForceVisible = true;
            // The renderers are switched back on in DriftTrack.LateUpdate (the player still stands in the lobby): let it run first.
            yield return null;
            yield return null;
            var corners = DriftTrackCircuit.Corners;
            int RowAt(float s)
            {
                var S = DriftTrackCircuit.S;
                int best = 0;
                float bd = float.MaxValue;
                for (int r = 0; r < n; r++) { float d = Mathf.Abs(DriftTrackCircuit.DeltaS(S[r], s)); if (d < bd) { bd = d; best = r; } }
                return best;
            }
            Vector3 F(int r) => DriftTrackRoad.Flat(DriftTrackCircuit.Fwd[DriftTrackCircuit.Wrap(r)]);
            Vector3 C(int r) => c[DriftTrackCircuit.Wrap(r)];
            Vector3 mid = DriftTrackLayout.PadCenter;
            CamShot(dir, "circuit_00_overview", mid + new Vector3(0f, 240f, -150f), mid + new Vector3(0f, 0f, 8f));
            CamShot(dir, "circuit_01_overview_low", new Vector3(-30f, 55f, -125f), new Vector3(-190f, 0f, 10f));
            int climb = RowAt(corners[12].S1 + 25f);
            CamShot(dir, "circuit_02_climb", C(climb) - F(climb) * 10f + Vector3.up * 2.4f, C(climb + 70) + Vector3.up * 1.5f);
            int span = -1;
            for (int r = 0; r < n; r++) if (DriftTrackCircuit.Span[r]) { span = r; break; }
            int spanMid = span;
            while (spanMid >= 0 && DriftTrackCircuit.Span[DriftTrackCircuit.Wrap(spanMid + 1)]) spanMid++;
            spanMid = span >= 0 ? (span + spanMid) / 2 : 0;
            Vector3 cross = C(spanMid);
            int under = DriftTrackCircuit.Nearest(new Vector3(cross.x, 0f, cross.z));
            TLog($"crossover: bridge row {spanMid} at {cross}, lower road row {under} at {C(under)}, height difference {cross.y - C(under).y:0.00} m");
            CamShot(dir, "circuit_03_crossover_below", C(under - 30) + Vector3.up * 1.6f, C(under) + Vector3.up * 2.5f);
            CamShot(dir, "circuit_04_crossover_above", cross + new Vector3(22f, 16f, -24f), cross);
            CamShot(dir, "circuit_05_bridge_deck", C(spanMid - 35) + Vector3.up * 1.7f, C(spanMid + 10) + Vector3.up * 1.2f);
            int t0 = -1;
            for (int r = 0; r < n; r++) if (DriftTrackCircuit.Tunnel[r]) { t0 = r; break; }
            if (t0 >= 0)
            {
                CamShot(dir, "circuit_06_tunnel_inside", C(t0 + 25) + Vector3.up * 1.5f, C(t0 + 45) + Vector3.up * 1.3f);
                CamShot(dir, "circuit_07_tunnel_entry", C(t0 - 30) + Vector3.up * 2f, C(t0) + Vector3.up * 2.5f);
            }
            int hp = RowAt(corners[1].S1);
            CamShot(dir, "circuit_08_hairpin", C(hp) - F(hp) * 20f + Vector3.up * 18f, C(hp));
            int hs = RowAt(corners[20].S1);
            CamShot(dir, "circuit_09_hairpin_south", C(hs) + F(hs) * 26f + Vector3.up * 9f, C(hs));
            int sr = DriftTrackCircuit.StartRow;
            CamShot(dir, "circuit_10_start", C(sr - 24) + Vector3.up * 2f + DriftTrackRoad.Flat(DriftTrackCircuit.Right[sr]) * 2f, C(sr) + Vector3.up * 3.5f);
            CamShot(dir, "circuit_11_paddock", DriftTrackLayout.KartSlot(2) + new Vector3(-6f, 5f, -14f), new Vector3(-104f, 0f, 10f));
            CamShot(dir, "circuit_12_esses", C(RowAt(corners[3].S0)) + Vector3.up * 14f - F(RowAt(corners[3].S0)) * 12f, C(RowAt(corners[5].S1)));
            CamShot(dir, "circuit_13_chicane", C(RowAt(corners[17].S0)) + Vector3.up * 12f - F(RowAt(corners[17].S0)) * 14f, C(RowAt(corners[19].S0)));

            // 6. One lap in kart 0 on the autopilot.
            var me = NetPlayer.Local;
            yield return EnterKart(me, 0, dir, null);
            var kart = LobbyKart.Get(0);
            if (kart == null || kart.View != LobbyKart.Mode.Local)
            {
                TLog($"lap: could not get into kart 0 -> {Cf(false)}");
            }
            else
            {
                int row = DriftTrackCircuit.Wrap(sr - 40);
                PlaceKart(kart, row);
                yield return new WaitForSeconds(0.8f);
                LocalPlayerController.TestKartActive = true;
                kart.ResetRide();
                // A hitch (another game / editor busy on the machine) would run up to ~17 physics steps on one stale
                // autopilot input and steer the kart into a barrier: game time slows down in a hitch instead.
                float maxDt = Time.maximumDeltaTime;
                Time.maximumDeltaTime = 0.05f;
                float t = 0f, progress = 0f, nextLog = 0f, maxLat = 0f, maxDy = 0f, top = 0f, prevS = DriftTrackCircuit.S[row];
                int resets = 0, offRoad = 0, idx = row, shot = 0;
                Vector3 last = kart.Phys.Pos;
                var mm = MatchManager.Instance;
                byte epoch = mm.KartsVar.Value.Get(0).Epoch;
                var shotAt = new float[5];
                string[] shotName = { "circuit_20_drive_tunnel", "circuit_21_drive_sweeper", "circuit_22_drive_climb", "circuit_23_drive_bridge", "circuit_24_drive_hairpin" };
                float L = DriftTrackCircuit.Length;
                // chase pictures at: the tunnel, the sweeper, the climb, the bridge, the south hairpin (metres driven from the start)
                for (int k = 0; k < shotAt.Length; k++)
                {
                    float s = k switch { 0 => (DriftTrackCircuit.S[t0 >= 0 ? t0 + 20 : 0]), 1 => corners[11].Apex, 2 => corners[13].S0, 3 => DriftTrackCircuit.S[spanMid], _ => corners[20].Apex };
                    shotAt[k] = Mathf.Repeat(s - DriftTrackCircuit.S[row], L);
                }
                while (t < 200f && progress < L - 2f)
                {
                    Vector3 pos = kart.Phys.Pos;
                    byte e = mm.KartsVar.Value.Get(0).Epoch;
                    if (e != epoch)
                    {
                        epoch = e;
                        resets++;
                        TLog($"  kart reset by the game (flipped / stuck / off the pad?) at {progress:0} m, now at {pos}");
                    }
                    if ((pos - last).magnitude > 8f)
                    {
                        TLog($"  kart jumped from {last} to {pos} at {progress:0} m - back on the road at row {idx}");
                        PlaceKart(kart, idx);
                        yield return new WaitForSeconds(0.5f);
                        pos = kart.Phys.Pos;
                    }
                    last = pos;
                    idx = DriftTrackCircuit.Nearest(pos, idx, 25);
                    float sNow = DriftTrackCircuit.S[idx];
                    float ds = DriftTrackCircuit.DeltaS(prevS, sNow);
                    if (ds > 0f) { progress += ds; prevS = sNow; }
                    else if (ds < -3f) prevS = sNow;
                    float lat = DriftTrackCircuit.Lateral(pos, idx);
                    float dy = pos.y - DriftTrackCircuit.Point(idx, lat).y;
                    maxLat = Mathf.Max(maxLat, Mathf.Abs(lat));
                    maxDy = Mathf.Max(maxDy, Mathf.Abs(dy));
                    if (Mathf.Abs(dy) > 1.2f && offRoad++ < 5) TLog($"  off the road surface by {dy:0.00} m at row {idx} ({pos})");
                    top = Mathf.Max(top, kart.Phys.Speed);
                    AutoDrive(kart, idx);
                    if (t >= nextLog)
                    {
                        nextLog = t + 1f;
                        TLog($"  lap t={t:0.0} s={progress:0} row={idx} v={kart.Phys.Speed * 3.6f:0} km/h lat={lat:0.00} y={pos.y:0.00} dy={dy:0.00} slip={kart.Phys.SlipAngle:0}");
                    }
                    if (shot < shotAt.Length && progress >= shotAt[shot])
                    {
                        Vector3 kf = DriftTrackRoad.Flat(kart.transform.forward);
                        CamShot(dir, shotName[shot], pos - kf * 6.5f + Vector3.up * 2.4f, pos + kf * 5f + Vector3.up * 0.6f);
                        shot++;
                    }
                    yield return null;
                    t += Time.deltaTime;
                }
                string ride = kart.RideReport(); // the lap only (not the stop after it)
                Time.maximumDeltaTime = maxDt;
                LocalPlayerController.TestKartThrottle = -1f;
                LocalPlayerController.TestKartSteer = 0f;
                yield return new WaitForSeconds(2f);
                LocalPlayerController.TestKartActive = false;
                bool lap = progress >= L - 2f;
                TLog($"lap: {progress:0} / {L:0} m in {t:0.0} s (avg {progress / Mathf.Max(1f, t) * 3.6f:0} km/h, top {top * 3.6f:0} km/h), resets {resets}, max |lateral| {maxLat:0.00} m, max off-surface {maxDy:0.00} m, off-road samples {offRoad} -> {Cf(lap && resets == 0 && offRoad == 0 && maxLat < DriftTrackCircuit.BarrierLat)}");
                TLog($"ride{(KartTuning.OldRide ? " (-kartoldride: old suspension)" : "")}: {ride} (info: suspension jitter, lower = smoother)");
                float ms = 0f;
                yield return FrameMs(v => ms = v);
                TLog($"frame time after the lap: {ms:0.0} ms");

                // R on the circuit: back onto the road where it stands, not to the paddock slot.
                Vector3 before = kart.Phys.Pos;
                kart.RequestReset("test");
                yield return new WaitForSeconds(1.5f);
                Vector3 after = kart.Phys.Pos;
                int ar = DriftTrackCircuit.Nearest(after);
                float alat = DriftTrackCircuit.Lateral(after, ar);
                bool onRoad = Mathf.Abs(alat) < 1f && Mathf.Abs(after.y - c[ar].y) < 0.6f;
                TLog($"reset on the circuit: {before} -> {after} (row {ar}, lateral {alat:0.00}), {Vector3.Distance(after, kart.Spot):0} m from the slot, epoch {mm.KartsVar.Value.Get(0).Epoch} -> {Cf(onRoad && Vector3.Distance(after, before) < 15f && Vector3.Distance(after, kart.Spot) > 20f)}");
            }
            DriftTrack.ForceVisible = false;
            TLog(_circuitFails == 0 ? "RESULT: all checks PASS" : $"RESULT: {_circuitFails} check(s) FAIL");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        /// <summary>Puts the (locally driven) kart on row <paramref name="row"/>, facing along the circuit.</summary>
        private static void PlaceKart(LobbyKart kart, int row)
        {
            Vector3 f = DriftTrackRoad.Flat(DriftTrackCircuit.Fwd[row]);
            kart.Phys.Reset(DriftTrackCircuit.Center[row] + Vector3.up * 0.25f, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Test autopilot for the circuit: steers at a point ahead on the centre line (further at speed), speed from the
        /// tightest curve in the next stretch (lateral grip ~5 m/s^2, at most <paramref name="maxSpeed"/>, 80 km/h by default). Sets the test kart inputs.
        /// </summary>
        private static void AutoDrive(LobbyKart kart, int idx, float maxSpeed = 22f)
        {
            var ph = kart.Phys;
            float v = ph.Speed;
            int ahead = Mathf.RoundToInt(5f + v * 0.45f);
            Vector3 target = DriftTrackCircuit.Center[DriftTrackCircuit.Wrap(idx + ahead)] - ph.Pos;
            Vector3 fwd = DriftTrackRoad.Flat(kart.transform.forward);
            float err = Vector3.SignedAngle(fwd, new Vector3(target.x, 0f, target.z), Vector3.up);
            float k = 0f;
            int look = Mathf.RoundToInt(10f + v * 1.8f);
            for (int j = 0; j < look; j++) k = Mathf.Max(k, Mathf.Abs(DriftTrackCircuit.Curvature[DriftTrackCircuit.Wrap(idx + j)]));
            float want = k > 1e-4f ? Mathf.Sqrt(5f / k) : 99f;
            want = Mathf.Clamp(want, 6f, maxSpeed);
            float thr = Mathf.Clamp((want - v) * 0.35f, -1f, 1f);
            if (v < 3f) thr = Mathf.Max(thr, 0.6f);
            LocalPlayerController.TestKartThrottle = thr;
            LocalPlayerController.TestKartSteer = Mathf.Clamp(err / 22f, -1f, 1f);
            LocalPlayerController.TestKartHandbrake = false;
        }
    }
}
