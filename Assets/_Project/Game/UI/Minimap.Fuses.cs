using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Minimap markers of the escape objective (hiders only, like the medkits): the fuse box of the viewer's level - a
    /// small box with its 4 sockets (a lit socket = a fuse in it; yellow frame = a wrong box lighting the floor, green =
    /// the right box locked), pinned faded to the edge when outside the view - and fuses lying on the floor of the same
    /// level within <see cref="FuseRange"/> m (small amber fuse icons).
    /// </summary>
    public partial class Minimap
    {
        public const float FuseRange = 10f;
        private readonly List<Image> _fuseMarks = new();
        private static readonly Dictionary<int, Sprite> BoxSprites = new();
        private static Sprite _fuseSprite;

        /// <summary>Tests: box / fuse markers shown in the last frame.</summary>
        public static int BoxMarks { get; private set; }
        public static int FuseMarks { get; private set; }
        /// <summary>Tests: the map followed the local RC car in the last frame.</summary>
        public static bool FollowsCar { get; private set; }

        private Image FuseMark(int i)
        {
            while (_fuseMarks.Count <= i)
            {
                var img = UiKit.Image("FuseMark", _clip, Color.white);
                img.raycastTarget = false;
                img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var fade = _clip.Find("Fade");
                if (fade != null) rt.SetSiblingIndex(fade.GetSiblingIndex() + 1);
                _fuseMarks.Add(img);
            }
            return _fuseMarks[i];
        }

        private void UpdateFuseMarkers(NetPlayer me, Vector3 pos, float pxPerM, float size)
        {
            int used = 0, boxes = 0, fuses = 0;
            var mm = MatchManager.Instance;
            var ph = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            if (me != null && me.IsHider && mm != null && !mm.RaceMode && (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) && mm.FuseBoxes.Value.Placed)
            {
                float edge = size * 0.5f - 9f;
                int myLevel = HideAndSeek.Map.LevelLighting.LevelAt(pos.y + 0.3f);
                var set = mm.FuseBoxes.Value;
                for (int b = 0; b < FuseTuning.Boxes; b++)
                {
                    var box = set[b];
                    if (!box.Placed || box.Level != myLevel) continue;
                    Vector2 off = new Vector2(box.Pos.x - pos.x, box.Pos.z - pos.z) * pxPerM;
                    bool outside = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                    off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                    var img = FuseMark(used++);
                    if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                    int state = box.Locked ? 2 : box.Lit ? 1 : 0;
                    img.sprite = BoxSprite(box.Filled & 15, state);
                    img.rectTransform.anchoredPosition = off;
                    img.rectTransform.sizeDelta = Vector2.one * (outside ? 16f : 21f);
                    img.color = new Color(1f, 1f, 1f, outside ? 0.8f : 1f);
                    boxes++;
                }
                for (int i = 0; i < mm.Drops.Count; i++)
                {
                    var d = mm.Drops[i];
                    if (!FuseItems.Is(d.Kind)) continue;
                    Vector3 w = mm.DropPosition(d);
                    if (Mathf.Abs(w.y - pos.y) > 1.7f) continue; // same level only
                    Vector2 flat = new Vector2(w.x - pos.x, w.z - pos.z);
                    if (flat.magnitude > FuseRange) continue;
                    Vector2 off = flat * pxPerM;
                    bool outside = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                    off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                    var img = FuseMark(used++);
                    if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                    img.sprite = FuseSprite();
                    img.rectTransform.anchoredPosition = off;
                    img.rectTransform.sizeDelta = Vector2.one * (outside ? 10f : 15f);
                    img.color = new Color(1f, 1f, 1f, outside ? 0.45f : 0.95f);
                    fuses++;
                }
            }
            BoxMarks = boxes;
            FuseMarks = fuses;
            for (int i = used; i < _fuseMarks.Count; i++)
                if (_fuseMarks[i].gameObject.activeSelf) _fuseMarks[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Fuse box (17 px): dark grey box with a frame (grey / yellow = lighting the floor / green = locked) and its 4
        /// sockets in a row, amber when a fuse is in.
        /// </summary>
        private static Sprite BoxSprite(int filled, int state)
        {
            int key = filled | (state << 4);
            if (BoxSprites.TryGetValue(key, out var sp)) return sp;
            var frame = state == 2 ? new Color32(90, 220, 110, 255) : state == 1 ? new Color32(255, 215, 70, 255) : new Color32(175, 180, 186, 255);
            sp = Pixel(17, (x, y) =>
            {
                if (x == 0 || y == 0 || x == 16 || y == 16) return new Color32(15, 15, 17, 255);
                if (x == 1 || y == 1 || x == 15 || y == 15) return frame;
                // 4 sockets in a row (left to right = sockets 0..3, as on the box): 2 px wide, rows 4..12
                int sx = x - 3;
                if (y >= 4 && y <= 12 && sx >= 0 && sx < 11 && sx % 3 != 2)
                {
                    int socket = sx / 3;
                    bool edgeRow = y == 4 || y == 12;
                    if ((filled & (1 << socket)) != 0) return edgeRow ? new Color32(150, 90, 20, 255) : new Color32(255, 180, 50, 255);
                    return edgeRow ? new Color32(70, 72, 78, 255) : new Color32(22, 23, 27, 255);
                }
                return new Color32(48, 52, 58, 255);
            });
            BoxSprites[key] = sp;
            return sp;
        }

        /// <summary>Fuse (11 px): amber ceramic body with metal caps, lying diagonally.</summary>
        private static Sprite FuseSprite() => _fuseSprite != null ? _fuseSprite : _fuseSprite = Pixel(11, (x, y) =>
        {
            int d = x - y, along = x + y; // along the diagonal 0..20
            if (Mathf.Abs(d) > 2 || along < 2 || along > 18) return new Color32(0, 0, 0, 0);
            bool rim = Mathf.Abs(d) == 2;
            bool cap = along <= 5 || along >= 15;
            if (rim) return new Color32(20, 20, 22, 255);
            if (cap) return Mathf.Abs(d) == 0 ? new Color32(235, 238, 242, 255) : new Color32(165, 170, 178, 255);
            return Mathf.Abs(d) == 0 ? new Color32(255, 205, 95, 255) : new Color32(230, 140, 35, 255);
        });
    }
}
