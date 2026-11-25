using System.Collections;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Helpers of the flashlight-between-levels test (<see cref="FlashLevelTest"/>): spots stacked on the three levels,
    /// spots at a slab opening, aiming, what a camera really rendered (light / beam state seen in onPreRender, i.e. after
    /// <see cref="FlashlightLevelCulling"/> did its work for that camera) and the screen brightness with the holder's
    /// light switched on and off locally.
    /// </summary>
    public partial class NetSession
    {
        private static Camera _flWatch;
        private static Light _flLight;
        private static Renderer _flBeam;
        private static int _flFrames, _flLit, _flBeamLit;
        private static bool _flHooked;

        private static void FlOnPreRender(Camera c)
        {
            if (c != _flWatch || _flLight == null) return;
            _flFrames++;
            if (_flLight.enabled) _flLit++;
            if (_flBeam != null && _flBeam.enabled) _flBeamLit++;
        }

        /// <summary>Counts, for <paramref name="secs"/>, the frames the holder's light / beam were on while <paramref name="cam"/> rendered.</summary>
        private static IEnumerator FlRecord(Camera cam, NetPlayer holder, float secs)
        {
            if (!_flHooked) { Camera.onPreRender += FlOnPreRender; _flHooked = true; }
            _flWatch = cam;
            _flLight = holder.Flashlight != null ? holder.Flashlight.SpotLight : null;
            _flBeam = holder.Flashlight != null ? holder.Flashlight.GetComponentInChildren<MeshRenderer>(true) : null;
            _flFrames = _flLit = _flBeamLit = 0;
            yield return new WaitForSeconds(secs);
            _flWatch = null;
        }

        /// <summary>
        /// Mean brightness (0-255) of a 240 x 320 px box around <paramref name="center"/> (screen px, e.g. the target player)
        /// with the holder's light on [i] and switched off locally [i + 1].
        /// </summary>
        private static IEnumerator FlLum(NetPlayer holder, float[] lum, int i, Vector2 center)
        {
            yield return FlScreen(lum, i, center);
            var fl = holder.Flashlight;
            if (fl == null) yield break;
            fl.SetOn(false, false);
            yield return new WaitForSeconds(0.25f);
            yield return FlScreen(lum, i + 1, center);
            fl.SetOn(holder.FlashOn.Value, false);
            yield return new WaitForSeconds(0.25f);
        }

        private static IEnumerator FlScreen(float[] lum, int i, Vector2 center)
        {
            double sum = 0;
            int n = 0;
            for (int f = 0; f < 3; f++)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                int x0 = Mathf.Clamp((int)center.x - 120, 0, w - 1), x1 = Mathf.Clamp((int)center.x + 120, 0, w);
                int y0 = Mathf.Clamp((int)center.y - 160, 0, h - 1), y1 = Mathf.Clamp((int)center.y + 160, 0, h);
                for (int y = y0; y < y1; y += 2)
                    for (int x = x0; x < x1; x += 2)
                    {
                        var c = px[y * w + x];
                        sum += 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
                        n++;
                    }
                Destroy(tex);
            }
            lum[i] = n > 0 ? (float)(sum / n) : 0f;
        }

        private static void FlAim(NetPlayer p, Vector3 at)
        {
            Vector3 d = at - p.EyePosition;
            float flat = new Vector2(d.x, d.z).magnitude;
            p.Motor.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Atan2(d.y, Mathf.Max(flat, 0.01f)) * Mathf.Rad2Deg);
        }

        /// <summary>Road spot of <paramref name="level"/> nearest to <paramref name="p"/> (x, z).</summary>
        private static Vector3 FlNear(int level, Vector3 p)
        {
            var spots = FuseSpots.RoadSpots(level);
            Vector3 best = p;
            float bd = float.MaxValue;
            foreach (var s in spots)
            {
                float d = (s.x - p.x) * (s.x - p.x) + (s.z - p.z) * (s.z - p.z);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Road spot of <paramref name="level"/> whose distance to the opening is closest to <paramref name="dist"/> (outside it).</summary>
        private static Vector3 FlNearRect(int level, Rect r, float dist)
        {
            Vector3 best = Vector3.zero;
            float bd = float.MaxValue;
            foreach (var s in FuseSpots.RoadSpots(level))
            {
                float dx = Mathf.Max(r.xMin - s.x, 0f, s.x - r.xMax), dz = Mathf.Max(r.yMin - s.z, 0f, s.z - r.yMax);
                float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dz * dz) - dist);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>A road spot on level 0 with spots right under it on levels 1 and 2, far from every slab opening.</summary>
        private static bool FlColumn(LevelLighting ll, out Vector3 p0, out Vector3 p1, out Vector3 p2)
        {
            p0 = p1 = p2 = default;
            float bestScore = -1f;
            foreach (var s in FuseSpots.RoadSpots(0))
            {
                Vector3 a = FlNear(1, s), b = FlNear(2, s);
                if (Flat(a, s) > 1.2f || Flat(b, s) > 1.2f || Flat(FlNear(0, s + new Vector3(5f, 0f, 0f)), s + new Vector3(5f, 0f, 0f)) > 1.2f) continue;
                float score = float.MaxValue;
                foreach (var h in ll.Holes)
                {
                    float dx = Mathf.Max(h.xMin - s.x, 0f, s.x - h.xMax), dz = Mathf.Max(h.yMin - s.z, 0f, s.z - h.yMax);
                    score = Mathf.Min(score, Mathf.Sqrt(dx * dx + dz * dz));
                }
                if (score > 40f) score = 40f + Mathf.Max(0f, 30f - Mathf.Abs(s.x)); // far enough: prefer the middle
                if (score > bestScore) { bestScore = score; p0 = s; p1 = a; p2 = b; }
            }
            return bestScore > 0f;

            static float Flat(Vector3 u, Vector3 v) => Vector2.Distance(new Vector2(u.x, u.z), new Vector2(v.x, v.z));
        }
    }
}
