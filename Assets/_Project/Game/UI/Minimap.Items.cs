using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Minimap markers for medkits and adrenaline syringes lying on the map: small, discreet pixel icons, only for
    /// hiders (the seeker never uses them), only on the viewer's level and within <see cref="ItemTuning.MinimapItemRange"/>
    /// metres. Items in range but outside the current zoom sit faded on the edge of the map.
    /// </summary>
    public partial class Minimap
    {
        private readonly List<Image> _itemMarks = new();
        private static Sprite _medSprite, _syrSprite;

        private Image ItemMark(int i)
        {
            while (_itemMarks.Count <= i)
            {
                var img = UiKit.Image("ItemMark", _clip, Color.white);
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var fade = _clip.Find("Fade");
                if (fade != null) rt.SetSiblingIndex(fade.GetSiblingIndex() + 1);
                _itemMarks.Add(img);
            }
            return _itemMarks[i];
        }

        private void UpdateItemMarkers(NetPlayer me, Vector3 pos, float pxPerM, float size)
        {
            int used = 0;
            var mm = MatchManager.Instance;
            if (me != null && me.IsHider && mm != null)
            {
                used = Place(mm.Medkits, MedSprite(), new Color(1f, 1f, 1f, 0.9f), pos, pxPerM, size, used);
                used = Place(mm.Syringes, SyrSprite(), new Color(1f, 1f, 1f, 0.9f), pos, pxPerM, size, used);
            }
            for (int i = used; i < _itemMarks.Count; i++)
                if (_itemMarks[i].gameObject.activeSelf) _itemMarks[i].gameObject.SetActive(false);
        }

        private int Place(Unity.Netcode.NetworkList<MedkitState> list, Sprite sprite, Color color, Vector3 pos, float pxPerM, float size, int used)
        {
            float edge = size * 0.5f - 8f;
            float range = ItemTuning.MinimapItemRange;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m.Taken) continue;
                Vector3 w = m.Position;
                if (Mathf.Abs(w.y - pos.y) > 1.7f) continue;                  // same level only
                Vector2 flat = new Vector2(w.x - pos.x, w.z - pos.z);
                if (flat.magnitude > range) continue;
                Vector2 off = flat * pxPerM;
                bool outside = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                var img = ItemMark(used++);
                if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                img.sprite = sprite;
                img.rectTransform.anchoredPosition = off;
                img.rectTransform.sizeDelta = Vector2.one * (outside ? 8f : 11f);
                img.color = outside ? new Color(color.r, color.g, color.b, 0.45f) : color;
            }
            return used;
        }

        /// <summary>White box with a red cross (11 px).</summary>
        private static Sprite MedSprite() => _medSprite != null ? _medSprite : _medSprite = Pixel(11, (x, y) =>
        {
            bool border = x == 0 || y == 0 || x == 10 || y == 10;
            bool cross = (x >= 4 && x <= 6 && y >= 2 && y <= 8) || (y >= 4 && y <= 6 && x >= 2 && x <= 8);
            return border ? new Color32(20, 20, 22, 255) : cross ? new Color32(220, 30, 25, 255) : new Color32(240, 240, 235, 255);
        });

        /// <summary>Blue syringe on a diagonal (11 px).</summary>
        private static Sprite SyrSprite() => _syrSprite != null ? _syrSprite : _syrSprite = Pixel(11, (x, y) =>
        {
            int d = x - y;
            bool body = Mathf.Abs(d) <= 1 && x >= 2 && x <= 8;
            bool rim = Mathf.Abs(d) == 2 && x >= 2 && x <= 9;
            bool needle = d == 0 && x <= 1;
            bool plunger = (x == 9 || x == 10) && (y == 9 || y == 10);
            if (body) return Mathf.Abs(d) == 0 ? new Color32(70, 170, 255, 255) : new Color32(210, 235, 250, 255);
            if (rim) return new Color32(20, 20, 22, 255);
            if (needle || plunger) return new Color32(200, 205, 210, 255);
            return new Color32(0, 0, 0, 0);
        });

        private static Sprite Pixel(int n, System.Func<int, int, Color32> f)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) px[y * n + x] = f(x, y);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
