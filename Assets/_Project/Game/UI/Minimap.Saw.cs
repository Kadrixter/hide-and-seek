using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Chainsaw nerf (2026-11-25): the saw at full throttle is loud - hiders on the same level see where it runs, as a
    /// red chainsaw icon with a pulsing red ring (pinned to the edge when outside the view), kept for
    /// <see cref="SawTuning.MapLinger"/> s after the throttle is let go. Other levels never show it.
    /// </summary>
    public partial class Minimap
    {
        private static readonly Color SawRed = new Color(1f, 0.22f, 0.16f, 1f);
        private readonly List<Image> _sawIcons = new(), _sawRings = new();
        private readonly Dictionary<ulong, float> _sawHeardAt = new();

        /// <summary>Tests: running saws shown in the last frame.</summary>
        public static int SawAlarmMarks { get; private set; }

        private void UpdateSawAlarm(NetPlayer me, Vector3 pos, float pxPerM, float size)
        {
            int n = 0;
            var mm = MatchManager.Instance;
            if (me != null && me.IsHider && mm != null && mm.Phase.Value == MatchPhase.Seeking)
            {
                int myFloor = NearestFloor(pos.y);
                float edge = size * 0.5f - 12f;
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || !p.IsSeeker) continue;
                    if (p.SawRunning) _sawHeardAt[p.NetworkObjectId] = Time.time;
                    if (!_sawHeardAt.TryGetValue(p.NetworkObjectId, out float at)) continue;
                    float since = Time.time - at;
                    if (since > SawTuning.MapLinger || p.Life.Value != LifeState.Alive) continue;
                    Vector3 w = p.transform.position;
                    if (NearestFloor(w.y) != myFloor) continue; // the same level only
                    Vector2 off = new Vector2(w.x - pos.x, w.z - pos.z) * pxPerM;
                    bool outside = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                    off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                    float fade = since <= 0.05f ? 1f : 1f - since / SawTuning.MapLinger;
                    EnsureSawMark(n);
                    var icon = _sawIcons[n];
                    var ring = _sawRings[n];
                    icon.sprite = SawMapSprite();
                    icon.rectTransform.anchoredPosition = off;
                    icon.rectTransform.sizeDelta = Vector2.one * (outside ? 26f : 34f);
                    icon.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 1f, fade));
                    float ripple = Mathf.Repeat(Time.time * 2.2f + n * 0.37f, 1f);
                    ring.rectTransform.anchoredPosition = off;
                    ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(26f, 64f, ripple);
                    ring.color = new Color(SawRed.r, SawRed.g, SawRed.b, 0.9f * fade * (1f - ripple));
                    SetSawActive(n, true);
                    n++;
                }
            }
            SawAlarmMarks = n;
            for (int i = n; i < _sawIcons.Count; i++) SetSawActive(i, false);
        }

        private static Sprite _sawMap;

        /// <summary>The hotbar chainsaw redrawn in alarm red (bright body, dark red outline) so it stands out on the dark map.</summary>
        private static Sprite SawMapSprite()
        {
            if (_sawMap != null) return _sawMap;
            var src = HotbarIcons.Get(HotbarItem.Chainsaw).texture;
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 0) continue;
                float lum = (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
                px[i] = lum < 0.12f ? new Color32(70, 0, 0, 255) // the outline
                    : new Color32(255, (byte)(40 + 150 * lum), (byte)(25 + 120 * lum), 255);
            }
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            return _sawMap = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32f);
        }

        private void SetSawActive(int i, bool on)
        {
            if (_sawIcons[i].gameObject.activeSelf != on) _sawIcons[i].gameObject.SetActive(on);
            if (_sawRings[i].gameObject.activeSelf != on) _sawRings[i].gameObject.SetActive(on);
        }

        private void EnsureSawMark(int i)
        {
            while (_sawIcons.Count <= i)
            {
                var ring = UiKit.Image("SawRing", _clip, SawRed);
                ring.sprite = RingSprite();
                ring.raycastTarget = false;
                ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                var icon = UiKit.Image("SawIcon", _clip, Color.white);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _sawRings.Add(ring);
                _sawIcons.Add(icon);
            }
        }
    }
}
