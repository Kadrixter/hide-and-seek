using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Test switch -testheroes &lt;folder&gt; (host, round running): walks the host up to every detailed hero car and to
    /// the most crooked ordinary cars, takes the player's view (dark garage, use -flashlight) and a lit close-up,
    /// then sets the car's alarm off and photographs the hazard lights while they are lit. Quits when done.
    /// </summary>
    public partial class NetSession
    {
        private static readonly string[] HeroModels = { "LamborghiniDiablo", "Peugeot306Cabrio", "BmwE30", "MazdaRx7", "VwGolf4", "MazdaRx8" };

        private System.Collections.IEnumerator HeroCarTour(string dir)
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value == MatchPhase.Lobby || mm.Phase.Value == MatchPhase.Loading ||
                   CarAlarms.Instance == null || CarAlarms.Instance.CarCount == 0 || NetPlayer.Local == null)
            {
                mm = MatchManager.Instance;
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(2f);
            var ca = CarAlarms.Instance;
            var host = NetPlayer.Local;
            var heroes = Enumerable.Range(0, ca.CarCount).Where(i => HeroModels.Any(m => ca.CarTransform(i).name.Contains("_" + m + "_"))).ToList();
            float Dev(int i) { float y = Mathf.Repeat(ca.CarTransform(i).eulerAngles.y + 45f, 90f) - 45f; return Mathf.Abs(y); }
            var crooked = Enumerable.Range(0, ca.CarCount).Where(i => !heroes.Contains(i)).OrderByDescending(Dev).Take(4).ToList();
            Debug.Log($"[HeroTour] {heroes.Count} hero cars, crooked picks: {string.Join(", ", crooked.Select(i => ca.CarTransform(i).name + " " + Dev(i).ToString("0.0")))}");
            int n = 0;
            foreach (int idx in heroes.Concat(crooked))
            {
                var t = ca.CarTransform(idx);
                string name = $"{n++:00}_{t.name}";
                var (floor, right, fwd, half) = ca.CarFrame(idx);
                Vector3 centre = floor + Vector3.up * 0.55f;
                Vector3 stand = floor + fwd * (half.z + 3f);
                foreach (var (a, b) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f), (0f, 1f), (0f, -1f) })
                {
                    Vector3 p = floor + fwd * (a * (half.z + 1.6f)) + right * (b * (half.x + 1.7f)) + Vector3.up * 0.05f;
                    if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.35f)) continue;
                    if (!Physics.Raycast(p + Vector3.up, Vector3.down, out var g, 1.5f) || Mathf.Abs(g.point.y - floor.y) > 0.3f) continue;
                    // clear line of sight to the car (a pillar in between is no use)
                    Vector3 e = p + Vector3.up * 1.6f, to = centre - e;
                    if (Physics.Raycast(e, to.normalized, out var block, to.magnitude, ~0, QueryTriggerInteraction.Ignore) &&
                        block.transform != t && !block.transform.IsChildOf(t)) continue;
                    stand = p;
                    break;
                }
                Vector3 eye = stand + Vector3.up * 1.6f;
                Vector3 look = centre - eye;
                float yaw = Quaternion.LookRotation(new Vector3(look.x, 0, look.z)).eulerAngles.y;
                float pitch = Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
                host.Motor.Teleport(stand, yaw, pitch);
                host.Motor.SetLook(yaw, pitch);
                yield return new WaitForSeconds(1.2f);
                Shot(dir, name + "_a_player");
                yield return null;
                LitShot(dir, name + "_b_lit", eye, centre);
                ca.ServerForce(mm, idx, mm.Now, 6f);
                yield return new WaitForSeconds(0.3f);
                while (Mathf.Repeat((float)Time.timeAsDouble * 1.6f, 1f) > 0.3f) yield return null; // wait for "lamps on"
                Shot(dir, name + "_c_alarm");
                yield return null;
                yield return null;
                while (Mathf.Repeat((float)Time.timeAsDouble * 1.6f, 1f) > 0.3f) yield return null;
                Vector3 corner = floor + fwd * (half.z + 0.9f) + right * (half.x + 0.9f) + Vector3.up * 1.0f;
                LitShot(dir, name + "_d_alarm_close", corner, floor + fwd * half.z + right * half.x * 0.6f + Vector3.up * 0.55f);
                // from high up (just under the ceiling): the car against the bay lines
                LitShot(dir, name + "_e_top", floor - fwd * 0.5f - right * (half.x + 2.2f) + Vector3.up * 2.75f, floor + fwd * 0.3f);
                yield return new WaitForSeconds(0.5f);

                Debug.Log($"[HeroTour] {name} at {t.position} yaw {t.eulerAngles.y:0.0}, stood at {stand}");
            }
            Debug.Log("[HeroTour] done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        /// <summary>Close-up from a temporary camera with a flashlight-like spot on it.</summary>
        private static void LitShot(string dir, string name, Vector3 pos, Vector3 at)
        {
            var lg = new GameObject("TestLitShotLight").AddComponent<Light>();
            lg.type = LightType.Spot;
            lg.spotAngle = 70f;
            lg.range = 25f;
            lg.intensity = 2.6f;
            lg.color = new Color(1f, 0.95f, 0.85f);
            lg.shadows = LightShadows.Soft;
            lg.transform.position = pos + Vector3.up * 0.15f;
            lg.transform.LookAt(at);
            CamShot(dir, name, pos, at);
            Object.Destroy(lg.gameObject);
        }
    }
}
