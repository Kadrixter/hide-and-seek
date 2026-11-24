using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HideAndSeek.Game.Gfx
{
    /// <summary>
    /// Test tool for <see cref="NanScan"/>: renders the full-screen camera stack like the game does (world camera, then
    /// the first-person view-model camera on top) into one HDR texture with MSAA, post effects off, finds a NaN pixel
    /// and bisects the enabled renderers (switching halves off) down to the one that makes it. Call at the end of a
    /// frame (WaitForEndOfFrame), never while rendering.
    /// </summary>
    public static class NanCulprit
    {
        private static Material _mat;
        private static Texture2D _read;

        public static string Find()
        {
            var cams = Stack();
            if (cams.Count == 0) return "no camera";
            if (_mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/HnsNanScan");
                if (sh == null) return "no shader";
                _mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            int w = cams[0].pixelWidth, h = cams[0].pixelHeight;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) };
            rt.Create();
            var effects = new List<Behaviour>();
            foreach (var c in cams)
                foreach (var b in c.GetComponents<MonoBehaviour>())
                    if (b.enabled && b.GetType().GetMethod("OnRenderImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public) != null)
                    { b.enabled = false; effects.Add(b); }
            bool scan = NanScan.Active;
            NanScan.Active = false;
            var all = new List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>())
                if (r.enabled && r.gameObject.activeInHierarchy) all.Add(r);
            try
            {
                var bad = RenderAndFind(cams, rt, -1, -1);
                if (bad.x < 0) return $"no NaN in a manual render ({cams.Count} cameras)";
                int px = bad.x, py = bad.y;
                var cand = new List<Renderer>(all);
                int steps = 0;
                while (cand.Count > 1 && steps < 40)
                {
                    steps++;
                    int half = cand.Count / 2;
                    var a = cand.GetRange(0, half);
                    foreach (var r in a) r.enabled = false;
                    bool still = RenderAndFind(cams, rt, px, py).x >= 0;
                    foreach (var r in a) r.enabled = true;
                    cand = still ? cand.GetRange(half, cand.Count - half) : a;
                }
                return Describe(cand[0], cams, rt, px, py);
            }
            finally
            {
                foreach (var b in effects) if (b != null) b.enabled = true;
                NanScan.Active = scan;
                rt.Release();
                Object.Destroy(rt);
            }
        }

        /// <summary>Names the renderer, confirms it (only it off), then finds the material slot (one at a time swapped).</summary>
        private static string Describe(Renderer c, List<Camera> cams, RenderTexture rt, int px, int py)
        {
            c.enabled = false;
            bool gone = RenderAndFind(cams, rt, px, py).x < 0;
            c.enabled = true;
            var m = c.sharedMaterials;
            var sb = new StringBuilder();
            sb.Append($"pixel {px},{py} -> {Path(c.transform)} ({c.GetType().Name}, layer {c.gameObject.layer}");
            var mf = c.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : c is SkinnedMeshRenderer sk ? sk.sharedMesh : null;
            if (mesh != null) sb.Append($", mesh {mesh.name} v{mesh.vertexCount}");
            sb.Append(") materials:");
            foreach (var mm in m) sb.Append($" {(mm != null ? mm.name + "/" + mm.shader.name + (mm.IsKeywordEnabled("_NORMALMAP") ? "+normalmap" : "") : "-")}");
            sb.Append(gone ? " [confirmed]" : " [not confirmed]");
            if (gone && m.Length > 1)
            {
                var plain = new Material(Shader.Find("Sprites/Default")) { color = Color.gray, hideFlags = HideFlags.DontSave };
                for (int i = 0; i < m.Length; i++)
                {
                    var swap = (Material[])m.Clone();
                    swap[i] = plain;
                    c.sharedMaterials = swap;
                    bool off = RenderAndFind(cams, rt, px, py).x < 0;
                    c.sharedMaterials = m;
                    if (off) sb.Append($" => slot {i} {(m[i] != null ? m[i].name : "-")}");
                }
                Object.Destroy(plain);
            }
            return sb.ToString();
        }

        /// <summary>Enabled full-screen game cameras without a target texture, lowest depth first (as they draw).</summary>
        private static List<Camera> Stack()
        {
            var list = new List<Camera>();
            foreach (var c in Camera.allCameras)
            {
                if (c.targetTexture != null || c.cameraType != CameraType.Game) continue;
                Rect r = c.rect;
                if (r.x > 0.001f || r.y > 0.001f || r.width < 0.999f || r.height < 0.999f) continue;
                list.Add(c);
            }
            list.Sort((a, b) => a.depth.CompareTo(b.depth));
            return list;
        }

        /// <summary>Renders the stack and returns a NaN pixel (any when px &lt; 0, else within 1 px of px,py), or (-1,-1).</summary>
        private static Vector2Int RenderAndFind(List<Camera> cams, RenderTexture rt, int px, int py)
        {
            foreach (var cam in cams)
            {
                var old = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = old;
            }
            int w = rt.width, h = rt.height;
            var mask = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(rt, mask, _mat, 0);
            if (_read == null || _read.width != w || _read.height != h)
                _read = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.DontSave };
            var prev = RenderTexture.active;
            RenderTexture.active = mask;
            _read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(mask);
            var p = _read.GetPixels32();
            if (px < 0)
            {
                for (int i = 0; i < p.Length; i++) if (p[i].r > 127) return new Vector2Int(i % w, i / w);
                return new Vector2Int(-1, -1);
            }
            for (int y = Mathf.Max(0, py - 1); y <= Mathf.Min(h - 1, py + 1); y++)
            for (int x = Mathf.Max(0, px - 1); x <= Mathf.Min(w - 1, px + 1); x++)
                if (p[y * w + x].r > 127) return new Vector2Int(x, y);
            return new Vector2Int(-1, -1);
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
