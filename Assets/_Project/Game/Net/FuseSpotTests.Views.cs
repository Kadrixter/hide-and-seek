using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Reachability flood fill, pictures and the minimap check of <see cref="FuseSpotTests"/>.</summary>
    public partial class FuseSpotTests
    {
        private const float Cell = 0.5f, GX0 = -90f, GZ0 = -60f;
        private const int NX = 360, NZ = 240;

        private static bool Reached(bool[,] g, Vector3 p)
        {
            int ix = Mathf.FloorToInt((p.x - GX0) / Cell), iz = Mathf.FloorToInt((p.z - GZ0) / Cell);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int x = ix + dx, z = iz + dz;
                if (x >= 0 && z >= 0 && x < NX && z < NZ && g[x, z]) return true;
            }
            return false;
        }

        /// <summary>Cells of <paramref name="lv"/> a player can walk to from the hider spawns of the level (4 neighbours, steps under 0.35 m).</summary>
        private static IEnumerator Flood(int lv, Action<bool[,]> done)
        {
            float y0 = MapInfo.FloorY(lv);
            var h = new float[NX, NZ];
            var walk = new bool[NX, NZ];
            for (int x = 0; x < NX; x++)
            {
                for (int z = 0; z < NZ; z++)
                {
                    var c = new Vector3(GX0 + (x + 0.5f) * Cell, y0, GZ0 + (z + 0.5f) * Cell);
                    if (!MapInfo.InsideGarage(c, 0.3f) || MapInfo.InSlabOpening(c, 0.2f)) continue;
                    if (!StaticHit(c + Vector3.up * 1.0f, Vector3.down, 1.4f, out var fh) || fh.normal.y < 0.8f || Mathf.Abs(fh.point.y - y0) > 0.35f) continue;
                    if (StaticOverlapCapsule(fh.point + Vector3.up * 0.45f, fh.point + Vector3.up * 1.5f, 0.28f) != null) continue;
                    walk[x, z] = true;
                    h[x, z] = fh.point.y;
                }
                if (x % 24 == 23) yield return null;
            }
            var seen = new bool[NX, NZ];
            var q = new Queue<Vector2Int>();
            int seeds = 0;
            foreach (var t in SpawnPoints.Hider())
            {
                if (t == null || MapInfo.LevelOf(t.position.y) != lv) continue;
                if (!NearestWalk(walk, t.position, out var s) || seen[s.x, s.y]) continue;
                seen[s.x, s.y] = true;
                q.Enqueue(s);
                seeds++;
            }
            if (seeds == 0 && NearestWalk(walk, new Vector3(0f, y0, 11.75f), out var mid)) { seen[mid.x, mid.y] = true; q.Enqueue(mid); Log($"level {lv}: no hider spawn - flood from the middle aisle"); }
            int count = 0, total = 0;
            foreach (bool b in walk) if (b) total++;
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                count++;
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= NX || n.y >= NZ || seen[n.x, n.y] || !walk[n.x, n.y]) continue;
                    if (Mathf.Abs(h[n.x, n.y] - h[c.x, c.y]) > 0.35f) continue;
                    seen[n.x, n.y] = true;
                    q.Enqueue(n);
                }
            }
            Log($"level {lv}: flood from {seeds} spawns reached {count} of {total} walkable cells");
            done(seen);
        }

        private static bool NearestWalk(bool[,] walk, Vector3 p, out Vector2Int best)
        {
            int ix = Mathf.FloorToInt((p.x - GX0) / Cell), iz = Mathf.FloorToInt((p.z - GZ0) / Cell);
            best = default;
            for (int r = 0; r < 6; r++)
            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                int x = ix + dx, z = iz + dz;
                if (x >= 0 && z >= 0 && x < NX && z < NZ && walk[x, z]) { best = new Vector2Int(x, z); return true; }
            }
            return false;
        }

        // ================================================================ pictures

        /// <summary>Top-down view of the level from just under its ceiling: green = road spots, yellow = picks over all
        /// seeds, magenta = bad spots, red rings = the real fuses of this round.</summary>
        private static void TopDown(int lv, List<Vector3> roads, List<Vector3> picks, List<Vector3> bad, List<Vector3> real)
        {
            if (string.IsNullOrEmpty(_dir)) return;
            const int W = 1800, H = 1220;
            float y0 = MapInfo.FloorY(lv);
            var go = new GameObject("FuseSpotTopCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = 61f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 4f;
            cam.cullingMask = (1 << LevelLighting.LayerOf(lv)) | 1;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.transform.SetPositionAndRotation(new Vector3(0f, y0 + 2.1f, 0f), Quaternion.Euler(90f, 0f, 0f));
            var lightGo = new GameObject("FuseSpotTopLight");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.4f;
            l.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(70f, 30f, 0f);
            var tex = Render(cam, W, H);
            cam.aspect = (float)W / H; // the projection of the render, not of the screen
            UnityEngine.Object.Destroy(lightGo);
            void Dot(Vector3 p, int r, Color c, bool ring = false)
            {
                Vector3 v = cam.WorldToViewportPoint(p);
                int cx = Mathf.RoundToInt(v.x * W), cy = Mathf.RoundToInt(v.y * H);
                for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    int d2 = dx * dx + dy * dy;
                    if (d2 > r * r || (ring && d2 < (r - 3) * (r - 3))) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x >= 0 && y >= 0 && x < W && y < H) tex.SetPixel(x, y, c);
                }
            }
            foreach (var p in roads) Dot(p, 2, new Color(0.2f, 1f, 0.3f));
            foreach (var p in picks) Dot(p, 3, new Color(1f, 0.85f, 0.1f));
            foreach (var p in bad) Dot(p, 6, Color.magenta);
            foreach (var p in real) if (MapInfo.LevelOf(p.y) == lv) Dot(p, 12, Color.red, true);
            for (int g = 0; g < Cinematics.ExitGate.Count && lv == 0; g++) Dot(Escape.EscapeTimeline.ToWorld(Vector3.zero, g), 9, Color.cyan, true);
            tex.Apply();
            Directory.CreateDirectory(_dir);
            File.WriteAllBytes(Path.Combine(_dir, $"top_level{lv}.png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(go);
        }

        /// <summary>A fuse on the floor seen from 1.9 m away at eye height (the free side), with a test light.</summary>
        private static void CloseUp(Vector3 p, string name)
        {
            if (string.IsNullOrEmpty(_dir)) return;
            Vector3 eye = p + new Vector3(1.9f, 1.6f, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 e = p + new Vector3(Mathf.Cos(a) * 1.9f, 1.6f, Mathf.Sin(a) * 1.9f);
                if (!StaticHit(e, (p + Vector3.up * 0.05f - e).normalized, Vector3.Distance(e, p) - 0.15f, out _)) { eye = e; break; }
            }
            var lg = new GameObject("FuseSpotLight").AddComponent<Light>();
            lg.type = LightType.Point;
            lg.range = 7f;
            lg.intensity = 1.5f;
            lg.transform.position = p + Vector3.up * 2.3f;
            LevelLighting.Instance?.Track(lg);
            var go = new GameObject("FuseSpotCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9);
            go.transform.position = eye;
            go.transform.LookAt(p);
            var tex = Render(cam, 960, 600);
            Directory.CreateDirectory(_dir);
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(go);
            UnityEngine.Object.Destroy(lg.gameObject);
        }

        public static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            return tex;
        }

        // ================================================================ minimap

        /// <summary>The host (a hider) stands on road spots: the minimap must show exactly the fuses of the level within range.</summary>
        private IEnumerator MinimapCheck(MatchManager mm, List<Vector3>[] roads)
        {
            var me = NetPlayer.Local;
            if (me == null || !me.IsHider) { Log("minimap: the host is not a hider - skipped"); yield break; }
            var rnd = new System.Random(7);
            int checks = 0, bad = 0;
            bool shot = false;
            for (int lv = 0; lv < roads.Length; lv++)
            for (int k = 0; k < 8 && roads[lv].Count > 0; k++)
            {
                Vector3 p = roads[lv][rnd.Next(roads[lv].Count)];
                me.Motor.Teleport(p + Vector3.up * 0.05f, rnd.Next(360), 0f);
                yield return new WaitForSeconds(0.4f);
                Vector3 pos = me.transform.position;
                int want = 0;
                float nearest = float.MaxValue;
                for (int i = 0; i < mm.Drops.Count; i++)
                {
                    var d = mm.Drops[i];
                    if (!FuseItems.Is(d.Kind)) continue;
                    Vector3 w = mm.DropPosition(d);
                    if (Mathf.Abs(w.y - pos.y) > 1.7f) continue;
                    float dist = new Vector2(w.x - pos.x, w.z - pos.z).magnitude;
                    if (dist <= Minimap.FuseRange) { want++; nearest = Mathf.Min(nearest, dist); }
                }
                int got = Minimap.FuseMarks;
                checks++;
                if (got != want) { bad++; Fail($"minimap at {pos}: {got} fuse markers, expected {want}"); }
                if (!shot && want > 0 && nearest > 15f && !string.IsNullOrEmpty(_dir))
                {
                    shot = true;
                    Log($"minimap shot at {pos}: {got} markers, nearest fuse {nearest:0.0} m");
                    ScreenCapture.CaptureScreenshot(Path.Combine(_dir, "minimap_far_fuse.png"));
                    yield return new WaitForSeconds(0.3f);
                }
            }
            Log($"minimap: {checks - bad}/{checks} spots show the right number of fuse markers (range {Minimap.FuseRange} m)");
        }
    }
}
