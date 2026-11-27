using System.Collections;
using System.Linq;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// 2026-11-27 sofa spring + worn upholstery test (both with -sofaspring so the spring always pops):
    ///  -testspring &lt;dir&gt; (host): close-ups of every sofa and pouf (the torn holes with the foam), waits for the client
    ///      to sit on a sofa, photographs the pop, the throw, the push and the end from the side (the client is a remote
    ///      player here: the RPC, the effect and the animation are checked across the network), then checks the client
    ///      stands by the sofa and sits down again.
    ///  -springsit (client): sits on the first sofa seat, logs its own side (seat, busy, position), sits again after it.
    /// Logs [SpringTest] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static int _springFails;
        private static void SpLog(string s) => Debug.Log("[SpringTest] " + s);
        private static string SpF(bool ok) { if (!ok) _springFails++; return ok ? "PASS" : "FAIL"; }

        private void StartSpringTests(System.Func<string, bool> has)
        {
            if (has("-testspring")) StartCoroutine(SpringHostTest(ArgOr("-testspring", "")));
            if (has("-springsit")) StartCoroutine(SpringSitTest());
        }

        private IEnumerator SpringHostTest(string dir)
        {
            yield return WaitLobby();
            // wear close-ups
            var furn = LobbySeat.All.Where(s => s != null && s.transform.parent != null).Select(s => s.transform.parent).Distinct().ToList();
            int k = 0;
            foreach (var f in furn)
            {
                int holes = 0;
                foreach (Transform c in f) if (c.name.StartsWith("Wear")) holes++;
                bool sofa = f.name.StartsWith("Sofa");
                SpLog($"{f.name} at {f.position}: wear pieces {holes} -> {SpF(holes == 3)}");
                Vector3 fc = f.position + Vector3.up * 0.35f;
                Vector3 front = sofa ? -f.forward : f.forward;
                CamShot(dir, $"wear_{k:00}_{f.name}_front", fc + front * 1.4f + Vector3.up * 0.9f, fc);
                CamShot(dir, $"wear_{k:00}_{f.name}_top", fc + front * 0.5f + Vector3.up * 1.3f + f.right * 0.4f, fc);
                k++;
            }
            // wait for the client to sit and the spring to pop
            NetPlayer other = null;
            float w = 0f;
            while (w < 60f && (other == null || other.Seat.Value < 0))
            {
                other = NetPlayer.All.FirstOrDefault(p => p != null && p != NetPlayer.Local);
                w += Time.deltaTime;
                yield return null;
            }
            if (other == null || other.Seat.Value < 0) { SpLog($"the client never sat down -> {SpF(false)}"); Finish(); yield break; }
            var seat = LobbySeat.Get(other.Seat.Value);
            float satAt = Time.time;
            SpLog($"client {other.Nick} sat on seat {seat.Index} ({seat.transform.parent.name})");
            Vector3 side = seat.transform.right, fwd = seat.transform.forward;
            Vector3 mid = seat.transform.position + Vector3.up * 0.7f + fwd * 0.4f;
            Vector3 cam = mid + side * 2.6f + fwd * 1.2f + Vector3.up * 0.4f;
            CamShot(dir, "spring_00_seated", cam, mid);
            SofaSpringFx fx = null;
            while (Time.time - satAt < 10f && (fx = Object.FindFirstObjectByType<SofaSpringFx>()) == null) yield return null;
            float popAfter = Time.time - satAt;
            SpLog($"spring popped {popAfter:0.0} s after sitting down (4-7 s wanted) -> {SpF(fx != null && popAfter > 3.8f && popAfter < 7.5f)}");
            if (fx == null) { Finish(); yield break; }
            float t0 = Time.time;
            float[] at = { 0.1f, 0.35f, 0.9f, 1.7f, 2.3f, 2.9f, 3.6f, 4.4f };
            float topY = 0f;
            foreach (float a in at)
            {
                while (Time.time - t0 < a) { topY = Mathf.Max(topY, other.transform.position.y); yield return null; }
                CamShot(dir, $"spring_{(int)(a * 100):000}_t", cam, mid);
                Vector3 sp = NetPlayer.SpringSurface(seat);
                CamShot(dir, $"spring_{(int)(a * 100):000}_close", sp + fwd * 0.9f - side * 0.7f + Vector3.up * 0.75f, sp + Vector3.up * 0.08f);
                SpLog($"  t={a:0.00}s seat={other.Seat.Value} pos={other.transform.position} spring fx={(Object.FindFirstObjectByType<SofaSpringFx>() != null)}");
            }
            SpLog($"thrown up: highest {topY - seat.transform.position.y:0.00} m over the floor -> {SpF(topY - seat.transform.position.y > 0.12f)}");
            while (Time.time - t0 < 5f) yield return null;
            float dist = Vector3.Distance(Flat(other.transform.position), Flat(NetPlayer.SpringSurface(seat)));
            bool fxGone = Object.FindFirstObjectByType<SofaSpringFx>() == null;
            SpLog($"after the push: standing {other.Seat.Value < 0}, {dist:0.00} m from the spring, spring gone {fxGone} -> {SpF(other.Seat.Value < 0 && dist < 1.1f && fxGone)}");
            // the client sits down again
            w = 0f;
            while (w < 12f && other.Seat.Value < 0) { w += Time.deltaTime; yield return null; }
            SpLog($"sitting again works: seat {other.Seat.Value} -> {SpF(other.Seat.Value >= 0)}");
            Finish();

            void Finish()
            {
                SpLog(_springFails == 0 ? "RESULT: all checks PASS" : $"RESULT: {_springFails} check(s) FAIL");
                StartCoroutine(QuitSoon());
            }
        }

        private static IEnumerator QuitSoon()
        {
            yield return new WaitForSeconds(2f);
            Application.Quit();
        }


        private IEnumerator SpringSitTest()
        {
            yield return WaitLobby();
            while (NetPlayer.All.Count(p => p != null) < 2) yield return null;
            yield return new WaitForSeconds(4f); // the host takes its wear pictures first
            var me = NetPlayer.Local;
            var seat = LobbySeat.All.FirstOrDefault(s => s != null && s.Shape.Kind == HideAndSeek.Characters.SeatKind.Sofa);
            if (seat == null) { SpLog("no sofa seat"); yield break; }
            TestSitOn(me, seat);
            SpLog($"sat on seat {seat.Index}");
            var lpc = LocalPlayerController.Instance;
            float t = 0f;
            bool popped = false;
            while (t < 16f)
            {
                if (!popped && lpc != null && lpc.SpringBusy) { popped = true; SpLog($"client: spring after {t:0.0} s"); }
                t += Time.deltaTime;
                yield return null;
                if (popped && !lpc.SpringBusy) break;
            }
            SpLog($"client: done, seat {me.Seat.Value}, busy {lpc?.SpringBusy}, at {me.transform.position}");
            yield return new WaitForSeconds(1.5f);
            TestSitOn(me, seat);
            SpLog($"client: sat again on {seat.Index}: {me.Seat.Value}");
            yield return new WaitForSeconds(3f);
            Application.Quit();
        }
    }
}
