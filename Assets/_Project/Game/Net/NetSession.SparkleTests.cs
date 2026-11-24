using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Game.Gfx;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testsparkle &lt;dir&gt; (host in a round, -autostart): looks for "glowing white dots on and around the cars".
    /// Close player views of cars on levels 0 / -1 / -2 with and without the flashlight; every frame is grabbed and the
    /// isolated bright pixels counted ([Sparkle] lines: pixel much brighter than its 5x5 surroundings). Then on the
    /// worst view the suspects are switched off one by one (bloom, anti-aliasing, dust shaders, fluids, covers,
    /// particles, reflections) and the count compared. -sparklemove: also a slow strafe past the cars (frame series).
    /// </summary>
    public partial class NetSession
    {
        private void StartSparkleTests()
        {
            if (ArgOr("-testsparkle", null) is string d) StartCoroutine(SparkleTest(d));
            if (ArgOr("-testhdrscan", null) is string h) StartCoroutine(HdrScanTest(h));
        }

        private struct SparkleView
        {
            public string Name;
            public Vector3 Feet, At;
            public bool Lamp;
        }

        private IEnumerator SparkleTest(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            var views = new List<SparkleView>();
            for (int lv = 0; lv >= -2; lv--)
            {
                for (int k = 0; k < 3; k++)
                {
                    if (!CarSpot(lv, k * 5, out var feet, out var target, out var car)) continue;
                    // closer than CarSpot: 1.2 m from the body side, looking at the door / bonnet edge
                    Vector3 to = car - feet;
                    to.y = 0f;
                    Vector3 near = feet + to.normalized * 0.9f;
                    foreach (bool lamp in new[] { true, false })
                        views.Add(new SparkleView { Name = $"L{-lv}_{k}_{(lamp ? "lamp" : "dark")}", Feet = near, At = target + Vector3.up * 0.4f, Lamp = lamp });
                }
            }
            int worstIdx = -1, worstCount = -1;
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                Pose(me, v.Feet, v.At);
                if (me.Flashlight != null) me.Flashlight.SetOn(v.Lamp, false);
                yield return new WaitForSeconds(0.8f);
                int n = 0;
                yield return GrabSparkles(dir, "view_" + v.Name, c => n = c);
                Debug.Log($"[Sparkle] view {v.Name}: {n} sparkles at {v.Feet}");
                if (n > worstCount) { worstCount = n; worstIdx = i; }
            }
            if (worstIdx < 0) { Debug.Log("[Sparkle] no views"); yield break; }

            // bisect on the worst view
            var w = views[worstIdx];
            Pose(me, w.Feet, w.At);
            if (me.Flashlight != null) me.Flashlight.SetOn(w.Lamp, false);
            yield return new WaitForSeconds(0.8f);
            var toggles = new (string name, System.Action off, System.Action on)[]
            {
                ("base", () => { }, () => { }),
                ("nobloom", () => { GfxSettings.TestBloom = false; GfxSettings.Apply(); }, () => { GfxSettings.TestBloom = null; GfxSettings.Apply(); }),
                ("noaa", () => { GfxSettings.TestAa = AaMode.Off; GfxSettings.Apply(); }, () => { GfxSettings.TestAa = null; GfxSettings.Apply(); }),
                ("msaaonly", () => { GfxSettings.TestAa = AaMode.Msaa4; GfxSettings.Apply(); }, () => { GfxSettings.TestAa = null; GfxSettings.Apply(); }),
                ("smaaonly", () => { GfxSettings.TestAa = AaMode.Smaa; GfxSettings.Apply(); }, () => { GfxSettings.TestAa = null; GfxSettings.Apply(); }),
                ("nodust", () => SwapShaders(true, "Hns/CarDust", "Hns/CarDustGlass"), () => SwapShaders(false)),
                ("nofluid", () => HideByShader(true, "Hns/Fluid"), () => HideByShader(false)),
                ("nowreck", () => HideByShader(true, "Hns/WreckDust", "Hns/WreckGlass", "Hns/HnsWreckDust", "Hns/HnsWreckGlass"), () => HideByShader(false)),
                ("notarps", () => HideByName(true, "TarpCar_"), () => HideByName(false, null)),
                ("noparticles", () => HideParticles(true), () => HideParticles(false)),
                ("refl", () => GfxSettings.TestReflections = true, () => GfxSettings.TestReflections = null),
                ("nodarksight", () => { if (DarkSight.Instance != null) { _sparkleDs = DarkSight.Instance.Scale; DarkSight.Instance.Scale = 0f; } }, () => { if (DarkSight.Instance != null) DarkSight.Instance.Scale = _sparkleDs; }),
                ("nospec", () => SpecOff(true), () => SpecOff(false)),
                ("base2", () => { }, () => { }),
            };
            foreach (var t in toggles)
            {
                t.off();
                yield return new WaitForSeconds(0.5f);
                int n = 0;
                yield return GrabSparkles(dir, "bisect_" + t.name, c => n = c);
                Debug.Log($"[Sparkle] bisect {w.Name} {t.name}: {n} sparkles");
                t.on();
                yield return new WaitForSeconds(0.2f);
            }

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sparklemove") >= 0)
            {
                Vector3 side = Vector3.Cross(Vector3.up, (w.At - w.Feet).normalized);
                for (int f = 0; f < 30; f++)
                {
                    Pose(me, w.Feet + side * (f * 0.03f - 0.45f), w.At + side * (f * 0.03f - 0.45f));
                    yield return null;
                    int n = 0;
                    yield return GrabSparkles(dir, $"move_{f:00}", c => n = c);
                    Debug.Log($"[Sparkle] move {f}: {n}");
                }
            }
            Debug.Log("[Sparkle] done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        /// <summary>Grabs the screen, writes the PNG and counts isolated bright pixels.</summary>
        private static IEnumerator GrabSparkles(string dir, string name, System.Action<int> result)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height, n = 0;
            var lum = new float[px.Length];
            for (int i = 0; i < px.Length; i++) lum[i] = (px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f) / 255f;
            var marks = new List<Vector2Int>();
            for (int y = 3; y < h - 3; y++)
            for (int x = 3; x < w - 3; x++)
            {
                float c = lum[y * w + x];
                if (c < 0.55f) continue;
                // ring 2 px away (a dot is at most ~3 px wide)
                float sum = 0f;
                int k = 0;
                for (int d = -3; d <= 3; d++)
                {
                    sum += lum[(y - 3) * w + x + d] + lum[(y + 3) * w + x + d] + lum[(y + d) * w + x - 3] + lum[(y + d) * w + x + 3];
                    k += 4;
                }
                float ring = sum / k;
                if (c - ring > 0.35f && c > ring * 2.2f) { n++; if (marks.Count < 400) marks.Add(new Vector2Int(x, y)); }
            }
            if (!string.IsNullOrEmpty(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, name + ".txt"), marks.ConvertAll(m => $"{m.x} {h - 1 - m.y}"));
            }
            Object.Destroy(tex);
            result(n);
        }

        private readonly Dictionary<Renderer, Material[]> _sparkleMats = new Dictionary<Renderer, Material[]>();
        private float _sparkleDs = 1f;
        private readonly List<Renderer> _sparkleHidden = new List<Renderer>();
        private readonly List<ParticleSystemRenderer> _sparklePs = new List<ParticleSystemRenderer>();

        private void SwapShaders(bool off, params string[] shaders)
        {
            if (!off)
            {
                foreach (var kv in _sparkleMats) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
                _sparkleMats.Clear();
                return;
            }
            var std = Shader.Find("Standard");
            var cache = new Dictionary<Material, Material>();
            foreach (var r in FindObjectsByType<Renderer>())
            {
                var ms = r.sharedMaterials;
                bool hit = false;
                var copy = (Material[])ms.Clone();
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null || System.Array.IndexOf(shaders, ms[i].shader.name) < 0) continue;
                    if (!cache.TryGetValue(ms[i], out var m))
                    {
                        m = new Material(std);
                        m.color = ms[i].color;
                        m.SetFloat("_Metallic", ms[i].GetFloat("_Metallic"));
                        m.SetFloat("_Glossiness", ms[i].GetFloat("_Glossiness"));
                        cache[ms[i]] = m;
                    }
                    copy[i] = m;
                    hit = true;
                }
                if (!hit) continue;
                _sparkleMats[r] = ms;
                r.sharedMaterials = copy;
            }
            Debug.Log($"[Sparkle] swapped {_sparkleMats.Count} renderers ({cache.Count} materials) to Standard");
        }

        private void HideByShader(bool off, params string[] shaders)
        {
            if (!off) { foreach (var r in _sparkleHidden) if (r != null) r.enabled = true; _sparkleHidden.Clear(); return; }
            foreach (var r in FindObjectsByType<Renderer>())
                foreach (var m in r.sharedMaterials)
                    if (m != null && System.Array.IndexOf(shaders, m.shader.name) >= 0 && r.enabled) { r.enabled = false; _sparkleHidden.Add(r); break; }
            Debug.Log($"[Sparkle] hid {_sparkleHidden.Count} renderers");
        }

        private void HideByName(bool off, string prefix)
        {
            if (!off) { foreach (var r in _sparkleHidden) if (r != null) r.enabled = true; _sparkleHidden.Clear(); return; }
            foreach (var r in FindObjectsByType<Renderer>())
            {
                for (var t = r.transform; t != null; t = t.parent)
                    if (t.name.StartsWith(prefix)) { if (r.enabled) { r.enabled = false; _sparkleHidden.Add(r); } break; }
            }
            Debug.Log($"[Sparkle] hid {_sparkleHidden.Count} renderers");
        }

        private void HideParticles(bool off)
        {
            if (!off) { foreach (var r in _sparklePs) if (r != null) r.enabled = true; _sparklePs.Clear(); return; }
            foreach (var r in FindObjectsByType<ParticleSystemRenderer>())
                if (r.enabled) { r.enabled = false; _sparklePs.Add(r); }
            Debug.Log($"[Sparkle] hid {_sparklePs.Count} particle renderers");
        }

        /// <summary>All materials of the cars level smoothness to 0 (rough) - tells specular aliasing from geometry holes.</summary>
        private void SpecOff(bool off)
        {
            if (!off) { SwapShaders(false); return; }
            var cache = new Dictionary<Material, Material>();
            foreach (var r in FindObjectsByType<Renderer>())
            {
                var ms = r.sharedMaterials;
                var copy = (Material[])ms.Clone();
                bool hit = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null || !ms[i].HasProperty("_Glossiness")) continue;
                    if (!cache.TryGetValue(ms[i], out var m))
                    {
                        m = new Material(ms[i]);
                        m.SetFloat("_Glossiness", 0f);
                        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                        if (m.HasProperty("_GlossMapScale")) m.SetFloat("_GlossMapScale", 0f);
                        cache[ms[i]] = m;
                    }
                    copy[i] = m;
                    hit = true;
                }
                if (!hit) continue;
                _sparkleMats[r] = ms;
                r.sharedMaterials = copy;
            }
            Debug.Log($"[Sparkle] rough: {_sparkleMats.Count} renderers");
        }

        // ------------------------------------------------------------------ HDR scan

        /// <summary>
        /// -testhdrscan &lt;dir&gt;: the player's camera (with its flashlight) is placed in front, behind and on both sides
        /// of every parked car; a copy of it renders the world into a half-float target with the game's MSAA (the same
        /// buffer GfxPost gets) and every pixel that is not finite or brighter than 1000 is counted ([HdrScan] lines).
        /// The first hot views are dissected: which renderer and which of its materials make the hot pixel.
        /// </summary>
        private IEnumerator HdrScanTest(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
            HudOff();
            var ca = CarAlarms.Instance;
            int views = 0, hotViews = 0, dissected = 0;
            var hotByMat = new Dictionary<string, int>();
            // -hdrviews a,b,c: views (car name + _v index) always shot, for before / after comparisons
            var wanted = new HashSet<string>((ArgOr("-hdrviews", "") ?? "").Split(','));
            for (int i = 0; i < ca.CarCount; i++)
            {
                var (floor, right, fwd, half) = ca.CarFrame(i);
                var carT = ca.CarTransform(i);
                Vector3 mid = floor + Vector3.up * 0.6f;
                var spots = new[] { fwd * (half.z + 1.4f), -fwd * (half.z + 1.4f), right * (half.x + 1.1f), -right * (half.x + 1.1f), fwd * (half.z + 0.6f) + right * (half.x + 0.9f) };
                for (int s = 0; s < spots.Length; s++)
                {
                    Vector3 feet = floor + spots[s] + Vector3.up * 0.05f;
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.6f, 0.3f)) continue;
                    Pose(me, feet, mid);
                    yield return null;
                    yield return null;
                    views++;
                    var hot = HdrHot(me.Camera, out float max, out int nan);
                    string name = $"{(carT != null ? carT.name : "car" + i)}_v{s}";
                    if (wanted.Contains(name)) { Shot(dir, "view_" + name); yield return null; yield return null; Debug.Log($"[HdrScan] view {name}: {hot.Count} hot px (non-finite {nan}), max finite {max:0}"); }
                    if (hot.Count == 0) continue;
                    hotViews++;
                    Debug.Log($"[HdrScan] HOT {name}: {hot.Count} px (non-finite {nan}), max finite {max:0}, first {hot[0]}");
                    if (hotViews <= 12) { Shot(dir, "hot_" + name); yield return null; yield return null; }
                    if (dissected < 25)
                    {
                        dissected++;
                        string who = null;
                        yield return HdrDissect(me.Camera, hot[0], r => who = r);
                        Debug.Log($"[HdrScan]   culprit of {name} @ {hot[0]}: {who}");
                        if (who != null) { hotByMat.TryGetValue(who, out int c); hotByMat[who] = c + 1; }
                    }
                }
            }
            Debug.Log($"[HdrScan] done: {views} views, {hotViews} with hot pixels");
            foreach (var kv in hotByMat) Debug.Log($"[HdrScan] culprit {kv.Value}x {kv.Key}");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        private static void HudOff()
        {
            HideAndSeek.Game.Cinematics.HudHider.Push();
        }

        private static RenderTexture _hdrRt;
        private static Camera _hdrCam;

        /// <summary>Renders like the player's camera into a half-float MSAA target and returns the hot pixels.</summary>
        private static List<Vector2Int> HdrHot(Camera src, out float maxFinite, out int nonFinite)
        {
            int w = 800, h = 450;
            int aa = Mathf.Max(1, QualitySettings.antiAliasing);
            if (_hdrRt == null || _hdrRt.antiAliasing != aa)
            {
                if (_hdrRt != null) _hdrRt.Release();
                _hdrRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = aa };
            }
            if (_hdrCam == null)
            {
                _hdrCam = new GameObject("HdrScanCam").AddComponent<Camera>();
                _hdrCam.enabled = false;
            }
            _hdrCam.CopyFrom(src);
            _hdrCam.allowHDR = true;
            _hdrCam.allowMSAA = true;
            _hdrCam.targetTexture = _hdrRt;
            _hdrCam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            var prev = RenderTexture.active;
            RenderTexture.active = _hdrRt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels();
            Object.Destroy(tex);
            var hot = new List<Vector2Int>();
            maxFinite = 0f;
            nonFinite = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                bool bad = float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsInfinity(m);
                if (bad) nonFinite++;
                else maxFinite = Mathf.Max(maxFinite, m);
                if (bad || m > 1000f) hot.Add(new Vector2Int(i % w, i / w));
            }
            return hot;
        }

        private static bool StillHot(Camera src, Vector2Int p)
        {
            var hot = HdrHot(src, out _, out _);
            foreach (var q in hot) if (Mathf.Abs(q.x - p.x) <= 2 && Mathf.Abs(q.y - p.y) <= 2) return true;
            return false;
        }

        /// <summary>Finds the renderer and the material that produce the hot pixel (switching them off one by one).</summary>
        private static IEnumerator HdrDissect(Camera src, Vector2Int p, System.Action<string> done)
        {
            _hdrCam.CopyFrom(src);
            _hdrCam.targetTexture = _hdrRt;
            var ray = _hdrCam.ViewportPointToRay(new Vector3((p.x + 0.5f) / 800f, (p.y + 0.5f) / 450f, 0f));
            var cands = new List<(Renderer r, float d)>();
            foreach (var r in FindObjectsByType<Renderer>())
                if (r.enabled && r.gameObject.activeInHierarchy && r.bounds.IntersectRay(ray, out float d) && d < 40f) cands.Add((r, d));
            cands.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var (r, _) in cands)
            {
                r.enabled = false;
                bool hot = StillHot(src, p);
                r.enabled = true;
                if (hot) continue;
                var mats = r.sharedMaterials;
                var black = new Material(Shader.Find("Unlit/Color")) { color = Color.black };
                for (int k = 0; k < mats.Length; k++)
                {
                    var test = (Material[])mats.Clone();
                    test[k] = black;
                    r.sharedMaterials = test;
                    bool h = StillHot(src, p);
                    r.sharedMaterials = mats;
                    if (h) continue;
                    var m = mats[k];
                    string info = m == null ? "null" : $"{m.name} [{m.shader.name}] gloss {(m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : -1f):0.00} metal {(m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : -1f):0.00} kw {string.Join(" ", m.shaderKeywords)}";
                    Object.Destroy(black);
                    done($"{r.name} / {info}");
                    yield break;
                }
                Object.Destroy(black);
                done($"{r.name} / (no single material)");
                yield break;
            }
            done(null);
        }
    }
}
