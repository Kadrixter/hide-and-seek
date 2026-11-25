using System;
using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Drift track tests (no -autostart; host + client + optional late client; the host side is in NetSession.TrackTests.cs):
    ///  -testtrack &lt;dir&gt; (host): shots of the arch / passage / track / karts / asphalt on every size, walks through the
    ///      arch, gets into the purple kart, drifts (yaw rate vs the velocity direction, slip angle), waits for the
    ///      client's bump, gets out, walks back into the lobby (track hidden, frame time).
    ///  -testtrackwatch &lt;dir&gt; (client): follows the host onto the track, takes the black kart, shoots the host's
    ///      drift from its kart, then drives into the host's parked kart (kart-to-kart bump), gets out.
    ///  -testtracklate &lt;dir&gt; (late client): every kart is shown where the server says.
    /// Logs: [KartTest] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static void KLog(string s) => Debug.Log("[KartTest] " + s);

        private void StartKartTests(Func<string, bool> has)
        {
            if (has("-testtrack")) StartCoroutine(TrackHostTest(ArgOr("-testtrack", "")));
            if (has("-testtrackwatch")) StartCoroutine(TrackWatchTest(ArgOr("-testtrackwatch", "")));
            if (has("-testtracklate")) StartCoroutine(TrackLateTest(ArgOr("-testtracklate", "")));
        }

        private static IEnumerator WaitKartLobby(int players)
        {
            float t0 = Time.time;
            while (true)
            {
                var mm = MatchManager.Instance;
                if (mm != null && mm.Phase.Value == MatchPhase.Lobby && NetPlayer.Local != null && LobbyKart.All[DriftTrackLayout.KartCount - 1] != null &&
                    DriftTrack.Instance != null && (NetPlayer.All.Count >= players || Time.time - t0 > 40f)) break;
                yield return null;
            }
            yield return new WaitForSeconds(3f);
        }

        /// <summary>Seated pose check: pelvis over the seat, both fists on the rim grips (metres off).</summary>
        private static string KartPoseCheck(NetPlayer p, LobbyKart kart, out bool ok)
        {
            var c = p.Character;
            ok = false;
            if (kart == null || c == null) return "no kart / character";
            var rider = c.GetComponent<KartRider>();
            float pelvis = Vector3.Distance(c.Pelvis.position, kart.Parts.Seat.position + kart.Parts.Seat.up * 0.115f);
            var wheel = kart.Parts.SteeringWheel;
            Vector3 hr = c.ElbowRight.TransformPoint(HideAndSeek.Characters.CharacterPose.HandLocal);
            Vector3 hl = c.ElbowLeft.TransformPoint(HideAndSeek.Characters.CharacterPose.HandLocal);
            float dr = Vector3.Distance(hr, wheel.TransformPoint(new Vector3(KartModel.WheelRimRadius, 0.015f, -0.01f)));
            float dl = Vector3.Distance(hl, wheel.TransformPoint(new Vector3(-KartModel.WheelRimRadius, 0.015f, -0.01f)));
            ok = rider != null && rider.Weight > 0.99f && pelvis < 0.08f && dr < 0.07f && dl < 0.07f;
            return $"rider={(rider != null ? rider.Weight.ToString("0.00") : "none")} pelvis-seat={pelvis:0.000} m handR-rim={dr:0.000} handL-rim={dl:0.000}";
        }

        /// <summary>Walks the local player (scripted input) towards <paramref name="to"/> until within 0.4 m or the time is up.</summary>
        private static IEnumerator WalkTo(NetPlayer me, Vector3 to, float seconds)
        {
            float t = 0f;
            me.Motor.CommandProvider = () =>
            {
                Vector3 d = to - me.transform.position;
                d.y = 0f;
                float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float turn = Mathf.Clamp(Mathf.DeltaAngle(me.transform.eulerAngles.y, want), -12f, 12f);
                return new MotorCommand { Move = new Vector2(0f, d.magnitude > 0.4f ? 1f : 0f), Look = new Vector2(turn, 0f) };
            };
            while (t < seconds)
            {
                Vector3 d = to - me.transform.position;
                d.y = 0f;
                if (d.magnitude < 0.4f) break;
                t += Time.deltaTime;
                yield return null;
            }
            me.Motor.CommandProvider = null;
        }

        /// <summary>Stands beside kart <paramref name="i"/> facing its seat and presses E (through the prompt).</summary>
        private static IEnumerator EnterKart(NetPlayer me, int i, string dir, string shot)
        {
            var kart = LobbyKart.Get(i);
            Vector3 stand = kart.transform.TransformPoint(new Vector3(-1.2f, 0f, -0.2f)) + Vector3.up * 0.02f;
            Vector3 to = kart.Parts.Seat.position - (stand + Vector3.up * 1.62f);
            me.TeleportLocal(stand, YawTo(to));
            me.Motor.SetLook(YawTo(to), PitchTo(to));
            yield return new WaitForSeconds(1f);
            var lpc = LocalPlayerController.Instance;
            KLog($"prompt at kart {i}: {lpc.Prompt} '{lpc.PromptText}' -> {Pf(lpc.Prompt == PromptKind.Kart && lpc.PromptText.Contains("nr " + KartLivery.Of(i).Number))}");
            if (shot != null) Shot(dir, shot);
            yield return new WaitForSeconds(0.2f);
            LocalPlayerController.TestKartE = true;
            yield return new WaitForSeconds(1.6f);
            var st = MatchManager.Instance.KartsVar.Value.Get(i);
            KLog($"in kart {i}: driver={st.Driver} me={me.NetworkObjectId} view={kart.View} -> {Pf(st.Driver == me.NetworkObjectId && kart.View == LobbyKart.Mode.Local && KartCamera.Active != null)}");
            KLog($"seated pose kart {i}: {KartPoseCheck(me, kart, out bool ok)} -> {Pf(ok)}");
        }

        private IEnumerator TrackWatchTest(string dir)
        {
            yield return WaitKartLobby(2);
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer host = null;
            foreach (var o in NetPlayer.All) if (o != null && o != me) host = o;
            // Wait for the host to reach the track and take a kart, then take the black kart (3).
            float t0 = Time.time;
            while (Time.time - t0 < 90f && (host == null || mm.KartsVar.Value.KartOf(host.NetworkObjectId) < 0)) yield return null;
            int hostKart = mm.KartsVar.Value.KartOf(host.NetworkObjectId);
            yield return EnterKart(me, 3, dir, "watch_00_prompt_black");
            var mine = LobbyKart.Get(3);
            var other = LobbyKart.Get(hostKart);
            LocalPlayerController.TestKartActive = true;
            LocalPlayerController.TestKartThrottle = 0f;
            // Shoot the host's drift from here.
            int shots = 0;
            float tw = Time.time, nextShot = 0f;
            bool loggedRemote = false;
            while (Time.time - tw < 25f && shots < 3)
            {
                if (other != null && other.View == LobbyKart.Mode.Remote && Mathf.Abs(other.SlipAngleShown) > 12f && Time.time >= nextShot)
                {
                    nextShot = Time.time + 0.6f;
                    Shot(dir, $"watch_01_host_drift_{shots}");
                    CamShot(dir, $"watch_02_host_drift_close_{shots}", other.transform.position + new Vector3(3.2f, 2.0f, 2.6f), other.transform.position + Vector3.up * 0.4f);
                    KLog($"remote drift seen: kart {hostKart} slip {other.SlipAngleShown:0.0} yawRate {other.ShownYawRate:0.0} v {other.ShownSpeed:0.00} smoke {other.Slip01:0.00}");
                    shots++;
                }
                if (!loggedRemote && other != null && other.View == LobbyKart.Mode.Remote && Mathf.Abs(other.ShownSpeed) < 0.1f && Time.time - tw > 1.5f)
                {
                    KLog($"remote seated pose: {KartPoseCheck(host, other, out bool ok)} -> {Pf(ok)}");
                    loggedRemote = true;
                }
                yield return null;
            }
            KLog($"watch: drift shots {shots} -> {Pf(shots > 0)}");
            // Wait until the host stops after the drift, then drive into his kart.
            float ts = Time.time;
            while (Time.time - ts < 20f && (other.View != LobbyKart.Mode.Remote || Mathf.Abs(other.ShownSpeed) > 0.2f || Time.time - ts < 3f)) yield return null;
            int hits0 = mine.Phys.Collide.KartHits;
            float tb = 0f, maxV = 0f;
            while (tb < 9f && mine.Phys.Collide.KartHits == hits0)
            {
                Vector3 d = other.transform.position - mine.transform.position;
                float err = Mathf.DeltaAngle(mine.transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                LocalPlayerController.TestKartSteer = Mathf.Clamp(err / 25f, -1f, 1f);
                LocalPlayerController.TestKartThrottle = d.magnitude > 6f ? 0.8f : 0.55f;
                maxV = Mathf.Max(maxV, mine.Phys.Speed);
                tb += Time.deltaTime;
                yield return null;
            }
            if (mine.Phys.Collide.KartHits > hits0) Shot(dir, "watch_03_kart_bump");
            LocalPlayerController.TestKartThrottle = 0f;
            yield return new WaitForSeconds(1.2f);
            KLog($"kart bump: hits {mine.Phys.Collide.KartHits - hits0}, speed before {maxV:0.00} -> after {mine.Phys.Speed:0.00}, gap {Vector3.Distance(mine.transform.position, other.transform.position):0.00} m, y {mine.transform.position.y:0.00} -> {Pf(mine.Phys.Collide.KartHits > hits0 && mine.Phys.Speed < Mathf.Max(3f, maxV) && Mathf.Abs(mine.transform.position.y) < 0.2f)}");
            LocalPlayerController.TestKartActive = false;
            LocalPlayerController.TestKartE = true;
            yield return new WaitForSeconds(1.5f);
            KLog($"client out: kart 3 {(mm.KartsVar.Value.Get(3).Driven ? "taken" : "free")} -> {Pf(!mm.KartsVar.Value.Get(3).Driven)}");
        }

        private IEnumerator TrackLateTest(string dir)
        {
            yield return WaitKartLobby(1);
            var mm = MatchManager.Instance;
            yield return new WaitForSeconds(1.5f);
            bool all = true;
            for (int i = 0; i < DriftTrackLayout.KartCount; i++)
            {
                var st = mm.KartsVar.Value.Get(i);
                var k = LobbyKart.Get(i);
                float d = (k.transform.position - st.Pos).magnitude;
                bool ok = d < 0.05f && Mathf.Abs(Mathf.DeltaAngle(st.Yaw, k.transform.eulerAngles.y)) < 1f;
                all &= ok;
                KLog($"late join kart {i}: state {st.Pos} yaw {st.Yaw:0} {(st.Driven ? "driven" : "free")}, shown {k.transform.position} yaw {k.transform.eulerAngles.y:0}, off slot {(st.Pos - k.Spot).magnitude:0.0} m -> {Pf(ok)}");
            }
            KLog($"late join all karts -> {Pf(all)}");
            DriftTrack.ForceVisible = true;
            CamShot(dir, "late_00_karts", DriftTrackLayout.KartSlot(2) + new Vector3(-9f, 4f, -9f), DriftTrackLayout.KartSlot(2));
        }
    }
}
