using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Minimap markers for dropped items. Hiders see dropped medkits / syringes like the ones from the map (same level,
    /// in range). The seeker sees his own weapons lying on the floor (e.g. the Glock that did not fit the inventory)
    /// anywhere: the item's icon with a pulsing yellow ring, faded with an up / down hint when it lies on another level,
    /// pinned to the edge when it is outside the view.
    /// </summary>
    public partial class Minimap
    {
        private readonly List<Image> _dropIcons = new(), _dropRings = new();
        private readonly List<Text> _dropArrows = new();

        /// <summary>Test: seeker weapon markers shown in the last frame.</summary>
        public static int SeekerDropMarks { get; private set; }

        private int PlaceDrops(NetPlayer me, MatchManager mm, Vector3 pos, float pxPerM, float size, int used)
        {
            var list = mm.Drops;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d.Kind != HotbarItem.Medkit && d.Kind != HotbarItem.Syringe) continue;
                Vector3 w = mm.DropPosition(d);
                if (Mathf.Abs(w.y - pos.y) > 1.7f) continue;
                Vector2 flat = new Vector2(w.x - pos.x, w.z - pos.z);
                if (flat.magnitude > ItemTuning.MinimapItemRange) continue;
                float edge = size * 0.5f - 8f;
                Vector2 off = flat * pxPerM;
                bool outside = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                var img = ItemMark(used++);
                if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                img.sprite = d.Kind == HotbarItem.Medkit ? MedSprite() : SyrSprite();
                img.rectTransform.anchoredPosition = off;
                img.rectTransform.sizeDelta = Vector2.one * (outside ? 8f : 11f);
                img.color = new Color(1f, 1f, 1f, outside ? 0.45f : 0.9f);
            }
            return used;
        }

        private void UpdateSeekerDrops(NetPlayer me, Vector3 pos, float pxPerM, float size)
        {
            int n = 0;
            var mm = MatchManager.Instance;
            if (me != null && me.IsSeeker && mm != null)
            {
                int myFloor = NearestFloor(pos.y);
                float edge = size * 0.5f - 12f;
                for (int i = 0; i < mm.Drops.Count; i++)
                {
                    var d = mm.Drops[i];
                    var def = ItemDefs.Get(d.Kind);
                    if (def == null || !def.SeekerMapMarker) continue;
                    Vector3 w = mm.DropPosition(d);
                    int f = NearestFloor(w.y - 0.3f);
                    Vector2 off = new Vector2(w.x - pos.x, w.z - pos.z) * pxPerM;
                    off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                    bool other = f != myFloor;
                    EnsureDropMark(n);
                    var icon = _dropIcons[n];
                    var ring = _dropRings[n];
                    var arrow = _dropArrows[n];
                    icon.sprite = HotbarIcons.Get(d.Kind);
                    icon.rectTransform.anchoredPosition = off;
                    icon.color = new Color(1f, 1f, 1f, other ? 0.55f : 1f);
                    float ripple = Mathf.Repeat(Time.time * 0.9f + i * 0.3f, 1f);
                    ring.rectTransform.anchoredPosition = off;
                    ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(18f, 42f, ripple);
                    ring.color = new Color(1f, 0.85f, 0.2f, (other ? 0.5f : 0.95f) * (1f - ripple));
                    arrow.rectTransform.anchoredPosition = off + new Vector2(13f, 9f);
                    arrow.text = other ? (f > myFloor ? "^" : "v") : "";
                    SetActive(n, true);
                    n++;
                }
            }
            SeekerDropMarks = n;
            for (int i = n; i < _dropIcons.Count; i++) SetActive(i, false);
        }

        private void SetActive(int i, bool on)
        {
            if (_dropIcons[i].gameObject.activeSelf != on) _dropIcons[i].gameObject.SetActive(on);
            if (_dropRings[i].gameObject.activeSelf != on) _dropRings[i].gameObject.SetActive(on);
            if (_dropArrows[i].gameObject.activeSelf != on) _dropArrows[i].gameObject.SetActive(on);
        }

        private void EnsureDropMark(int i)
        {
            while (_dropIcons.Count <= i)
            {
                var ring = UiKit.Image("DropRing", _clip, Color.yellow);
                ring.sprite = RingSprite();
                ring.raycastTarget = false;
                ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                var icon = UiKit.Image("DropIcon", _clip, Color.white);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(20f, 20f);
                var arrow = UiKit.Label("DropArrow", _clip, "", 14, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f), FontStyle.Bold);
                arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                arrow.rectTransform.sizeDelta = new Vector2(16f, 16f);
                arrow.AddOutline();
                _dropRings.Add(ring);
                _dropIcons.Add(icon);
                _dropArrows.Add(arrow);
            }
        }
    }
}
