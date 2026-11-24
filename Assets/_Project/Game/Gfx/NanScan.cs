using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HideAndSeek.Game.Gfx
{
    /// <summary>
    /// Test tool (-nanscan &lt;dir&gt; / -testnan &lt;dir&gt;): reads back the HDR frame inside <see cref="GfxPost"/> (before and
    /// after the bloom) and reports pixels that are NaN, +-Inf or negative - such pixels turn black on screen and the
    /// bloom chain can spread them into black squares. Logs [NanScan] lines with the frame area and the renderers under
    /// it, saves a PNG of the frame with the bad pixels painted (NaN magenta, +Inf green, -Inf cyan, negative yellow).
    /// </summary>
    public static class NanScan
    {
        public static bool Active;
        public static string Dir;
        /// <summary>What the test is looking at (goes into the log line and the file name).</summary>
        public static string Context = "";
        public static int Every = 3, MaxSaves = 60, MaxLogs = 400;

        public static int BadFrames, Saved, Logged, Scans;
        /// <summary>Last frame with bad pixels, their count and centre (bottom-left origin).</summary>
        public static int LastBadFrame = -1, LastBadCount;
        public static Vector2Int LastBadCentre;
        public static readonly Dictionary<string, int> BySource = new();

        private static Material _mat;
        private static Texture2D _read;
        private static int _lastFrame = -1;

        public static void Inspect(RenderTexture src, string stage)
        {
            if (!Active || src == null) return;
            int f = Time.frameCount;
            if (f % Every != 0) return;
            if (_mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/HnsNanScan");
                if (sh == null || !sh.isSupported) { Debug.LogWarning("[NanScan] shader missing"); Active = false; return; }
                _mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            int w = src.width, h = src.height;
            var mask = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, mask, _mat, 0);
            if (_read == null || _read.width != w || _read.height != h)
                _read = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.DontSave };
            var prev = RenderTexture.active;
            RenderTexture.active = mask;
            _read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(mask);
            Scans++;
            var px = _read.GetPixels32();
            int nan = 0, pinf = 0, ninf = 0, neg = 0, x0 = w, y0 = h, x1 = -1, y1 = -1;
            long sx = 0, sy = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                if ((p.r | p.g | p.b | p.a) == 0) continue;
                if (p.r > 127) nan++;
                if (p.g > 127) pinf++;
                if (p.b > 127) ninf++;
                if (p.a > 127) neg++;
                int x = i % w, y = i / w;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
                sx += x;
                sy += y;
            }
            int bad = nan + pinf + ninf + neg;
            if (bad == 0) return;
            if (f != _lastFrame) BadFrames++;
            _lastFrame = f;
            int cx = (int)(sx / bad), cy = (int)(sy / bad);
            LastBadFrame = f;
            LastBadCount = bad;
            LastBadCentre = new Vector2Int(cx, cy);
            string under = Under(cx, cy, w, h);
            string key = stage + " " + under;
            BySource.TryGetValue(key, out int seen);
            BySource[key] = seen + 1;
            if (Logged < MaxLogs)
            {
                Logged++;
                Debug.Log($"[NanScan] {stage} frame {f} [{Context}] nan {nan} +inf {pinf} -inf {ninf} neg {neg} area x {x0}-{x1} y {y0}-{y1} ({w}x{h}) centre {cx},{cy} under: {under}");
            }
            if (Saved < MaxSaves && !string.IsNullOrEmpty(Dir) && seen < 3)
            {
                Saved++;
                SavePicture(src, $"nan_{Saved:00}_{stage}_{Safe(Context)}_f{f}");
            }
        }

        private static void SavePicture(RenderTexture src, string name)
        {
            int w = src.width, h = src.height;
            var vis = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, vis, _mat, 1);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = vis;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(vis);
            System.IO.Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        /// <summary>Renderers whose bounds the view ray through the bad area crosses, nearest first (name / shader).</summary>
        private static string Under(int x, int y, int w, int h)
        {
            var cam = Camera.main;
            if (cam == null) cam = GfxPostDriver.FinalCamera;
            if (cam == null) return "?";
            var ray = cam.ViewportPointToRay(new Vector3((x + 0.5f) / w, (y + 0.5f) / h, 0f));
            var hits = new List<(float d, string s)>();
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !r.isVisible) continue;
                if (r is ParticleSystemRenderer == false && r.bounds.size.sqrMagnitude > 2500f) continue; // floors / walls of a whole level
                if (!r.bounds.IntersectRay(ray, out float d) || d > 60f) continue;
                var m = r.sharedMaterial;
                hits.Add((d, $"{r.name}({(m != null && m.shader != null ? m.shader.name : "-")})"));
            }
            hits.Sort((a, b) => a.d.CompareTo(b.d));
            var sb = new StringBuilder();
            for (int i = 0; i < hits.Count && i < 4; i++) sb.Append(i > 0 ? ", " : "").Append(hits[i].s).Append($" @{hits[i].d:0.0}m");
            return sb.Length > 0 ? sb.ToString() : "(nothing)";
        }

        private static string Safe(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.Length > 40 ? sb.ToString(0, 40) : sb.ToString();
        }

        public static string Summary()
        {
            var sb = new StringBuilder($"[NanScan] summary: scans {Scans}, bad frames {BadFrames}");
            foreach (var kv in BySource) sb.Append($"\n  {kv.Value}x {kv.Key}");
            return sb.ToString();
        }
    }
}
