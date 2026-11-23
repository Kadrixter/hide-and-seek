using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HideAndSeek.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Renders the seated poses in the real lobby scene without play mode (fast loop for judging poses and seats):
    /// every pose on the first sofa seat and the first pouf, three camera angles, several skins, per-pose strips and
    /// sheets of four poses; optionally an idle clip (30 fps frames) of chosen poses.
    /// Batch (no -nographics):
    ///   set HNS_SEAT_OUT=C:\out  (default Logs/seatstudio)
    ///   set HNS_SEAT_POSES=all | 0,4,8   (default all)
    ///   set HNS_SEAT_IDLE=8:6,4:6        (pose:seconds clips, optional; HNS_SEAT_IDLE_VIEW=0..2 camera)
    ///   set HNS_SEAT_TIME=1.3            (idle time of the still shots)
    ///   -executeMethod HideAndSeek.Game.Editor.SeatPoseStudio.Shots
    /// </summary>
    public static class SeatPoseStudio
    {
        const int W = 560, H = 420;

        [MenuItem("Hide and Seek/Lobby/Seat pose shots")]
        public static void Shots()
        {
            bool ok = false;
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Run();
                ok = true;
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { ShaderUtil.allowAsyncCompilation = prevAsync; }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static string Env(string k, string def) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? def : v; }

        static void Run()
        {
            string outDir = Env("HNS_SEAT_OUT", Path.Combine(Directory.GetCurrentDirectory(), "Logs", "seatstudio"));
            Directory.CreateDirectory(outDir);
            float still = float.Parse(Env("HNS_SEAT_TIME", "1.3"), System.Globalization.CultureInfo.InvariantCulture);
            EditorSceneManager.OpenScene(MultiplayerBuilder.LobbyScenePath, OpenSceneMode.Single);
            var seats = UnityEngine.Object.FindObjectsByType<LobbySeat>(FindObjectsInactive.Exclude).OrderBy(s => s.name, StringComparer.Ordinal).ToList();
            LobbySeat sofa = seats.FirstOrDefault(s => s.Shape.Kind == SeatKind.Sofa && s.Shape.MirrorSource == null);
            LobbySeat pouf = seats.FirstOrDefault(s => s.Shape.Kind == SeatKind.Pouf);
            var cam = new GameObject("StudioCam").AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            int skins = Mathf.Max(1, SkinLibrary.Count);

            string mode = Env("HNS_SEAT_MODE", "poses");
            SeatPoses.UseBaked = false; // always solve afresh (the baked file may be older)
            SeatPoses.ClearCache();
            var trace = new System.Text.StringBuilder();
            SeatPoses.Trace = trace;
            if (mode == "poufs") { PoufVariants(cam, outDir); return; }
            if (Env("HNS_SEAT_REBUILD", "1") == "1")
                foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Where(x => x.name == "Sofa" || x.name == "Pouf").ToList())
                    MultiplayerBuilder.RebuildSeatModel(t, MultiplayerBuilder.PoufStyle);

            string poses = Env("HNS_SEAT_POSES", "all");
            var want = poses == "all" ? null : new HashSet<int>(poses.Split(',').Select(int.Parse));
            var strips = new Dictionary<string, List<Texture2D>>();
            foreach (var (seat, where) in new[] { (sofa, "sofa"), (pouf, "pouf") })
            {
                if (seat == null) continue;
                strips[where] = new List<Texture2D>();
                for (int v = 0; v < SeatPoses.Count; v++)
                {
                    if (want != null && !want.Contains(v)) continue;
                    if (!SeatPoses.Supports(seat.Shape, v)) continue;
                    var go = Pose(seat, v, (v + (where == "pouf" ? 2 : 0)) % skins, still);
                    var views = new List<Texture2D>();
                    for (int k = 0; k < 3; k++)
                    {
                        Aim(cam, seat, v, k);
                        views.Add(Render(cam, W, H));
                    }
                    UnityEngine.Object.DestroyImmediate(go);
                    var strip = Join(views, true);
                    File.WriteAllBytes(Path.Combine(outDir, $"{where}_{v:00}.png"), strip.EncodeToPNG());
                    strips[where].Add(strip);
                    Debug.Log($"[SeatStudio] {where} pose {v} done");
                }
            }
            foreach (var kv in strips)
                for (int i = 0; i < kv.Value.Count; i += 4)
                {
                    var sheet = Join(kv.Value.Skip(i).Take(4).ToList(), false);
                    File.WriteAllBytes(Path.Combine(outDir, $"sheet_{kv.Key}_{i / 4}.png"), sheet.EncodeToPNG());
                }

            // Idle clip: the listed poses in a 2-column grid, 30 fps frames (HNS_SEAT_IDLE = "0,8,3,10:8" = poses : seconds).
            string idle = Env("HNS_SEAT_IDLE", "");
            if (idle.Length > 0)
            {
                var parts = idle.Split(':');
                var list = parts[0].Split(',').Select(int.Parse).ToList();
                float secs = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 6f;
                int view = int.Parse(Env("HNS_SEAT_IDLE_VIEW", "0"));
                string dir = Path.Combine(outDir, "idle");
                Directory.CreateDirectory(dir);
                foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
                int frames = Mathf.RoundToInt(secs * 30f);
                for (int i = 0; i < frames; i++)
                {
                    var tiles = new List<Texture2D>();
                    foreach (int v in list)
                    {
                        var seat = v >= 8 || (v < 4 && tiles.Count % 2 == 1) ? pouf : sofa;
                        if (seat == null || !SeatPoses.Supports(seat.Shape, v)) seat = seat == sofa ? pouf : sofa;
                        var go = Pose(seat, v, v % skins, i / 30f, 100 + v * 31);
                        Aim(cam, seat, v, view);
                        tiles.Add(Render(cam, 480, 360));
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                    var rows = new List<Texture2D>();
                    for (int r = 0; r < tiles.Count; r += 2) rows.Add(Join(tiles.Skip(r).Take(2).ToList(), true));
                    File.WriteAllBytes(Path.Combine(dir, $"f_{i:0000}.png"), Join(rows, false).EncodeToPNG());
                    foreach (var t in tiles) UnityEngine.Object.DestroyImmediate(t);
                }
                Debug.Log($"[SeatStudio] idle clip {idle}: {frames} frames");
            }
            SeatPoses.Trace = null;
            File.WriteAllText(Path.Combine(outDir, "trace.txt"), trace.ToString());
            Debug.Log($"[SeatStudio] done -> {outDir}");
        }

        /// <summary>The pouf styles side by side (a standing character for scale), sheet of two views per style.</summary>
        static void PoufVariants(Camera cam, string outDir)
        {
            var pouf = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).First(x => x.name == "Pouf");
            var rows = new List<Texture2D>();
            for (int style = 0; style < 4; style++)
            {
                MultiplayerBuilder.RebuildSeatModel(pouf, style);
                foreach (var mf in pouf.GetComponentsInChildren<MeshFilter>())
                {
                    var m = mf.sharedMesh; var n = m.normals; var v = m.vertices; var tr = m.triangles;
                    int nan = n.Count(x => float.IsNaN(x.x)); 
                    Vector3 c0 = m.bounds.center; int outward = 0;
                    for (int i = 0; i < tr.Length; i += 3) { Vector3 fn = Vector3.Cross(v[tr[i+1]] - v[tr[i]], v[tr[i+2]] - v[tr[i]]); Vector3 mid = (v[tr[i]] + v[tr[i+1]] + v[tr[i+2]]) / 3f; if (Vector3.Dot(fn, mid - c0) > 0) outward++; }
                    int nOut = 0; for (int i = 0; i < v.Length; i++) if (Vector3.Dot(n[i], v[i] - c0) > 0) nOut++;
                    Debug.Log($"[SeatStudio] style {style} {mf.name}: verts {v.Length} nan normals {nan}, faces out {outward}/{tr.Length / 3}, normals out {nOut}/{v.Length}, mat {mf.GetComponent<MeshRenderer>().sharedMaterial?.name} static {mf.gameObject.isStatic} lm {mf.GetComponent<MeshRenderer>().lightmapIndex}");
                }
                var stand = new GameObject("StudioSitter");
                stand.transform.SetPositionAndRotation(pouf.position + pouf.right * 0.85f - pouf.forward * 0.25f, pouf.rotation * Quaternion.Euler(0f, -25f, 0f));
                BlockyCharacterFactory.Create(stand.transform, style + 1);
                var views = new List<Texture2D>();
                Vector3 c = pouf.position + Vector3.up * 0.25f;
                foreach (var (dir, up, dist, fov) in new[] { (new Vector3(0.35f, 0f, 1f), 0.9f, 3.2f, 36f), (new Vector3(-0.9f, 0f, 0.7f), 0.35f, 1.15f, 40f) })
                {
                    Vector3 d = pouf.TransformDirection(dir.normalized);
                    Vector3 pos = c + d * dist + Vector3.up * up;
                    Vector3 look = up > 0.5f ? c + pouf.right * 0.42f + Vector3.up * 0.55f : c;
                    cam.fieldOfView = fov;
                    cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                    foreach (var r in Hidden) if (r != null) r.enabled = true;
                    Hidden.Clear();
                    HideBetween(pos, c, pouf);
                    views.Add(Render(cam, 700, 520));
                }
                UnityEngine.Object.DestroyImmediate(stand);
                var row = Join(views, true);
                File.WriteAllBytes(Path.Combine(outDir, $"pouf_style_{style}.png"), row.EncodeToPNG());
                rows.Add(row);
            }
            File.WriteAllBytes(Path.Combine(outDir, "pouf_styles_ab.png"), Join(rows.Take(2).ToList(), false).EncodeToPNG());
            File.WriteAllBytes(Path.Combine(outDir, "pouf_styles_cd.png"), Join(rows.Skip(2).ToList(), false).EncodeToPNG());
            Debug.Log($"[SeatStudio] pouf styles done -> {outDir}");
        }

        static void HideBetween(Vector3 pos, Vector3 target, Transform keep)
        {
            var ray = new Ray(pos, (target - pos).normalized);
            float len = (target - pos).magnitude - 0.5f;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || r.transform.root.name == "StudioSitter" || (keep != null && r.transform.IsChildOf(keep))) continue;
                var b = r.bounds;
                if (b.size.x > 5f || b.size.z > 5f || b.size.y < 0.03f && b.max.y < 0.05f) continue; // floor, walls, rugs
                b.Expand(0.05f);
                if (b.IntersectRay(ray, out float d) && d < len) { r.enabled = false; Hidden.Add(r); }
            }
        }

        /// <summary>A character sitting on <paramref name="seat"/> in pose <paramref name="v"/> at idle time <paramref name="t"/>.</summary>
        static GameObject Pose(LobbySeat seat, int v, int skin, float t, int idleSeed = 0)
        {
            var root = new GameObject("StudioSitter");
            root.transform.SetPositionAndRotation(seat.transform.position, seat.transform.rotation);
            var c = BlockyCharacterFactory.Create(root.transform, skin);
            var lim = SeatPoses.Limits(v, seat.Shape);
            var p = idleSeed != 0 ? SeatPoses.EvaluateIdle(v, seat.Shape, t, idleSeed) : SeatPoses.Evaluate(v, seat.Shape, lim.Center, lim.Pitch, t);
            p.Apply(c);
            return root;
        }

        static void Aim(Camera cam, LobbySeat seat, int v, int k)
        {
            var t = seat.transform;
            bool lying = SeatPoses.Lying(v);
            Vector3 target = t.position + Vector3.up * (lying ? 0.66f : 0.8f) - t.forward * 0.12f - t.right * (lying ? 0.45f : 0f);
            float dist = lying ? 1.55f : 1.3f;
            Vector3 pos = k switch
            {
                0 => target + (t.forward * 2.3f + t.right * 1.2f) * dist + Vector3.up * 0.55f,
                1 => target + (t.forward * 1.2f - t.right * 2.3f) * dist + Vector3.up * 0.35f,
                _ => target + t.forward * 2.5f * dist + Vector3.up * -0.25f,
            };
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
            // Nothing between the camera and the sitter (coffee table snacks, plants, the other furniture).
            foreach (var r in Hidden) if (r != null) r.enabled = true;
            Hidden.Clear();
            Transform furniture = t.parent;
            foreach (float dy in new[] { -0.7f, -0.35f, 0f, 0.35f })
            foreach (float dx in new[] { -0.4f, 0f, 0.4f })
                HideBetween(pos, target + Vector3.up * dy + cam.transform.right * dx, furniture);
        }

        static readonly List<Renderer> Hidden = new List<Renderer>();

        static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 8 });
            var prev = RenderTexture.active;
            cam.targetTexture = rt;
            cam.aspect = w / (float)h;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        /// <summary>Joins images side by side (<paramref name="across"/>) or top to bottom (first on top).</summary>
        static Texture2D Join(List<Texture2D> list, bool across)
        {
            int w = across ? list.Sum(x => x.width) : list.Max(x => x.width);
            int h = across ? list.Max(x => x.height) : list.Sum(x => x.height);
            var r = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var fill = new Color32[w * h];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(20, 20, 24, 255);
            r.SetPixels32(fill);
            int o = across ? 0 : h;
            foreach (var x in list)
            {
                if (across) { r.SetPixels(o, 0, x.width, x.height, x.GetPixels()); o += x.width + 0; }
                else { o -= x.height; r.SetPixels(0, o, x.width, x.height, x.GetPixels()); }
            }
            r.Apply();
            return r;
        }
    }
}
