using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The six monitors: which camera each one shows, live feeds (30 fps, only near a room), snow when switching, the
    /// recorder's on-screen display (time, REC, camera name, RUCH) drawn with a pixel font into an overlay texture, and
    /// the light the screens throw into the room (flickers with snow, goes red on motion).
    /// </summary>
    public partial class SecurityRooms
    {
        private const float FeedFps = 30f, SnowTime = 0.45f;
        private const int OsdW = 256, OsdH = 144;

        private sealed class Slot
        {
            public RenderTexture Rt;
            public Camera Cam;
            public Light Ir;
            public int CamIndex = -1;       // index into _cams
            public float Since = -99f;
            public float NextRender;
            public bool Motion;
            public readonly List<Material> Mats = new();
            public readonly Vector3[] ScreenPos = new Vector3[Levels]; // monitor centre in the room of each level
            public Texture2D Osd;
            public Color32[] OsdPx;
            public string OsdKey;
        }

        private readonly Slot[] _slots = new Slot[Monitors];
        private Shader _monitorShader;
        private static AudioClip _switchClip, _floorClip;
        private readonly List<(Light light, float baseIntensity, Color baseColor)> _screenLightBase = new();

        private Slot MakeSlot(int i)
        {
            var s = new Slot();
            s.Rt = new RenderTexture(320, 180, 16, RenderTextureFormat.ARGB32) { name = "SecurityFeed" + i, filterMode = FilterMode.Bilinear };
            s.Rt.Create();
            var go = new GameObject("FeedCam" + i);
            go.transform.SetParent(transform, false);
            s.Cam = go.AddComponent<Camera>();
            s.Cam.enabled = false;
            s.Cam.targetTexture = s.Rt;
            s.Cam.fieldOfView = 62f;
            s.Cam.nearClipPlane = 0.25f;
            s.Cam.farClipPlane = 70f;
            s.Cam.clearFlags = CameraClearFlags.SolidColor;
            s.Cam.backgroundColor = Color.black;
            var lg = new GameObject("IR").AddComponent<Light>();
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0f, -0.3f, 1.5f);
            lg.type = LightType.Point;
            lg.range = 30f;
            lg.intensity = 0.9f;
            lg.shadows = LightShadows.None;
            lg.enabled = false;
            s.Ir = lg;
            s.Osd = new Texture2D(OsdW, OsdH, TextureFormat.RGBA32, false) { name = "Osd" + i, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            s.OsdPx = new Color32[OsdW * OsdH];
            return s;
        }

        /// <summary>Applies the synced level (called when <see cref="MatchManager.SecurityFloor"/> changes).</summary>
        private void ApplyFloor(int floor)
        {
            floor = Mathf.Clamp(floor, 0, Levels - 1);
            if (floor == _floor) return;
            _floor = floor;
            Debug.Log($"[Security] monitors now show level {LevelName(floor)}");
            AssignAll();
            foreach (var s in _slots) s.Since = Time.time;   // every screen snows
            PlayFeedSound(-1);
            for (int i = 0; i < _buttonRenderers.Count; i++) PaintButton(i);
        }

        /// <summary>Test helper: saves what monitor <paramref name="slot"/> currently shows.</summary>
        public void DumpFeed(int slot, string path)
        {
            var s = _slots[slot];
            var prev = RenderTexture.active;
            RenderTexture.active = s.Rt;
            var tex = new Texture2D(s.Rt.width, s.Rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, s.Rt.width, s.Rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
        }

        /// <summary>All monitors: cameras with the freshest motion first, then the rest in fixed order.</summary>
        private void AssignAll()
        {
            var order = new List<int>(_levelCams[_floor]);
            order.Sort((a, b) =>
            {
                int cmp = _cams[b].LastMotion.CompareTo(_cams[a].LastMotion);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });
            for (int s = 0; s < Monitors; s++)
            {
                _slots[s].CamIndex = s < order.Count ? order[s] : -1;
                _slots[s].NextRender = 0f;
            }
            UpdateLabels();
        }

        /// <summary>A camera that just saw motion and is not on a monitor takes over the monitor with the oldest picture.</summary>
        private void Reassign()
        {
            bool changed = false;
            foreach (int ci in _levelCams[_floor])
            {
                if (Time.time - _cams[ci].LastMotion > 1f) continue;
                bool shown = false;
                foreach (var s in _slots) if (s.CamIndex == ci) { shown = true; break; }
                if (shown) continue;
                Slot best = null;
                int bestIndex = -1;
                for (int i = 0; i < Monitors; i++)
                {
                    var s = _slots[i];
                    if (Time.time - s.Since < 3f) continue;
                    float age = s.CamIndex >= 0 ? _cams[s.CamIndex].LastMotion : -999f;
                    if (best == null || age < (best.CamIndex >= 0 ? _cams[best.CamIndex].LastMotion : -999f)) { best = s; bestIndex = i; }
                }
                if (best == null || (best.CamIndex >= 0 && Time.time - _cams[best.CamIndex].LastMotion < 2f)) continue;
                best.CamIndex = ci;
                best.Since = Time.time;
                best.NextRender = 0f;
                PlayFeedSound(bestIndex);
                changed = true;
            }
            if (changed || Time.frameCount % 6 == 0) UpdateLabels();
            CycleIdle();
        }

        private void UpdateLabels()
        {
            foreach (var sl in _slots)
            {
                sl.Motion = sl.CamIndex >= 0 && Time.time - _cams[sl.CamIndex].LastMotion < 4f;
                foreach (var mat in sl.Mats) mat.SetFloat("_Alarm", sl.Motion ? 1f : 0f);
            }
        }

        // ---------------------------------------------------------------- on-screen display

        /// <summary>Redraws an overlay when its content changes (every second for the clock, blinking REC / RUCH).</summary>
        private void UpdateOsd()
        {
            var now = System.DateTime.Now;
            bool blink = Time.time % 1f < 0.6f;
            for (int i = 0; i < Monitors; i++)
            {
                var s = _slots[i];
                string name = s.CamIndex >= 0 ? _cams[s.CamIndex].Name : "";
                string lvl = s.CamIndex >= 0 ? "P " + LevelName(_cams[s.CamIndex].Level) : "";
                string time = now.ToString("dd-MM-yyyy  HH:mm:ss");
                string key = $"{name}|{lvl}|{time}|{blink}|{s.Motion}";
                if (key == s.OsdKey) continue;
                s.OsdKey = key;
                var px = s.OsdPx;
                System.Array.Clear(px, 0, px.Length);
                var white = new Color32(235, 240, 235, 255);
                var red = new Color32(255, 50, 40, 255);
                if (s.CamIndex < 0)
                {
                    OsdText(px, "BRAK SYGNAŁU", (OsdW - CanFont.Width("BRAK SYGNAŁU", 2)) / 2, OsdH / 2 - 7, 2, white);
                }
                else
                {
                    OsdText(px, time, OsdW - CanFont.Width(time, 1) - 6, OsdH - 13, 1, white);
                    if (blink) OsdDot(px, 9, OsdH - 10, 3, red);
                    OsdText(px, "REC", 15, OsdH - 13, 1, white);
                    OsdText(px, $"CAM{CamNumber(s.CamIndex):00}  {name}", 6, 6, 1, white);
                    OsdText(px, lvl, OsdW - CanFont.Width(lvl, 1) - 6, 6, 1, white);
                    if (s.Motion && blink) OsdText(px, "RUCH", (OsdW - CanFont.Width("RUCH", 2)) / 2, 22, 2, red);
                }
                s.Osd.SetPixels32(px);
                s.Osd.Apply(false);
            }
        }

        private static void OsdText(Color32[] px, string text, int x, int y, int scale, Color32 c)
        {
            var shadow = new Color32(0, 0, 0, 200);
            CanFont.Draw(text, x + 1, y - 1, scale, scale, (a, b) => OsdPlot(px, a, b, shadow, false));
            CanFont.Draw(text, x, y, scale, scale, (a, b) => OsdPlot(px, a, b, c, true));
        }

        private static void OsdDot(Color32[] px, int cx, int cy, int r, Color32 c)
        {
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r * r) OsdPlot(px, cx + x, cy + y, c, true);
        }

        private static void OsdPlot(Color32[] px, int x, int y, Color32 c, bool over)
        {
            if (x < 0 || y < 0 || x >= OsdW || y >= OsdH) return;
            int i = y * OsdW + x;
            if (!over && px[i].a > 0) return;
            px[i] = c;
        }

        /// <summary>Old-TV snow on a monitor right after it switches.</summary>
        private void UpdateSnow()
        {
            foreach (var s in _slots)
            {
                float k = Mathf.Clamp01(1f - (Time.time - s.Since) / SnowTime);
                if (s.CamIndex < 0) k = 1f;
                foreach (var mat in s.Mats)
                {
                    mat.SetFloat("_Snow", k);
                    mat.SetFloat("_Glitch", k > 0.01f ? 1f : 0f);
                }
            }
        }

        /// <summary>Screen light: dim green-grey, flickers while monitors snow, pulses red with motion alarms.</summary>
        private void UpdateScreenLights()
        {
            float snow = 0f, alarm = 0f;
            foreach (var s in _slots)
            {
                snow += Mathf.Clamp01(1f - (Time.time - s.Since) / SnowTime) / Monitors;
                if (s.Motion) alarm += 1f / Monitors;
            }
            float flick = 1f + snow * (Random.value - 0.3f) * 1.2f + 0.04f * Mathf.Sin(Time.time * 47f);
            float red = Mathf.Clamp01(alarm * 2f) * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f)) * 0.6f;
            foreach (var (l, bi, bc) in _screenLightBase)
            {
                if (l == null) continue;
                l.intensity = bi * flick * (1f + red * 0.3f);
                l.color = Color.Lerp(bc, new Color(1f, 0.35f, 0.3f), red);
            }
        }

        private void RenderFeeds()
        {
            if (!AnyRoomNear(out _) || !AnyRoomPowered) return; // no battery power: nothing to show (SecurityRooms.Power.cs)
            // Every monitor at a steady 30 fps; the ticks of the monitors are staggered so the work is spread over frames.
            var prevShadows = QualitySettings.shadows;
            bool any = false;
            float step = 1f / FeedFps;
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s.CamIndex < 0 || Time.time < s.NextRender) continue;
                s.NextRender = Time.time - s.NextRender > step ? Time.time + step * i / _slots.Length : s.NextRender + step;
                if (!any) { QualitySettings.shadows = ShadowQuality.Disable; any = true; }
                var c = _cams[s.CamIndex];
                s.Cam.transform.SetPositionAndRotation(c.Pos, c.Rot);
                s.Ir.cullingMask = HideAndSeek.Map.LevelLighting.MaskForLevel(c.Level);   // the IR lamp lights only its own level
                s.Ir.enabled = true;
                s.Cam.Render();
                s.Ir.enabled = false;
            }
            if (any) QualitySettings.shadows = prevShadows;
        }

        // ---------------------------------------------------------------- sound

        /// <summary>Relay click followed by old-TV snow (white noise, crackle), synthesised once.</summary>
        private static AudioClip MakeStaticClip(string name, float length, int seed)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(length * rate);
            var d = new float[n];
            var rnd = new System.Random(seed);
            float lp = 0f, hpPrev = 0f, hpOut = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float click = 0.9f * Mathf.Exp(-t / 0.0025f) * Mathf.Sin(2f * Mathf.PI * 2300f * t)
                              + 0.45f * Mathf.Exp(-t / 0.012f) * Mathf.Sin(2f * Mathf.PI * 140f * t);
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.55f;
                hpOut = 0.92f * (hpOut + lp - hpPrev);
                hpPrev = lp;
                float crackle = rnd.NextDouble() < 0.0015 ? (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.9f : 0f;
                float env = Mathf.Clamp01((t - 0.006f) / 0.015f) * Mathf.Clamp01((length - t) / (length * 0.55f));
                float wobble = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 50f * t);
                d[i] = Mathf.Clamp(click + (0.42f * hpOut * wobble + crackle) * env, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Plays the switch sound at the monitor (or the whole wall for -1) in the room on the listener's level.</summary>
        private void PlayFeedSound(int slot, float volume = 1f)
        {
            if (!AnyRoomNear(out var viewer)) return;
            int lv = Mathf.Clamp(Mathf.RoundToInt(-(viewer.y - 1f) / 3.4f), 0, Levels - 1);
            Vector3 pos = slot >= 0 ? _slots[slot].ScreenPos[lv] : new Vector3((X0 + X1) * 0.5f, FloorY(lv) + 1.7f, Z1 - 0.2f);
            GameAudio.PlayClipAt(slot >= 0 ? _switchClip : _floorClip, pos, (slot >= 0 ? 0.55f : 0.75f) * volume, 14f);
        }
    }
}
