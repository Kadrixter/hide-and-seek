using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Several seekers: each one sees the other seekers on his minimap as small red-orange dots with a dark core
    /// (kept at the edge when out of range, with the up / down arrow when on another level). Hiders never see them.
    /// </summary>
    public partial class Minimap
    {
        private static readonly Color AllyColor = new Color(1f, 0.42f, 0.22f, 1f);

        /// <summary>Adds the ally dots after <paramref name="used"/> team dots; returns the new count.</summary>
        private int UpdateSeekerAllies(NetPlayer me, Vector3 pos, float pxPerM, float edge, int used)
        {
            if (me == null || !me.IsSeeker) return used;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p == me || !p.IsSeeker) continue;
                Vector3 w = p.transform.position;
                Vector2 off = new Vector2(w.x - pos.x, w.z - pos.z) * pxPerM;
                bool outside = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                float dy = w.y - pos.y;
                bool otherFloor = Mathf.Abs(dy) > 1.7f;

                var dot = Dot(used);
                var arrow = _dotArrows[used];
                used++;
                dot.gameObject.SetActive(true);
                var rt = dot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = off;
                rt.sizeDelta = new Vector2(14, 14) * (outside ? 0.8f : 1f);
                dot.color = new Color(AllyColor.r, AllyColor.g, AllyColor.b, otherFloor || outside ? 0.6f : 1f);
                arrow.gameObject.SetActive(otherFloor);
                if (otherFloor)
                {
                    arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    arrow.rectTransform.anchoredPosition = Vector2.zero;
                    arrow.rectTransform.sizeDelta = new Vector2(9, 9);
                    arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, dy > 0f ? 0f : 180f);
                    arrow.color = new Color(0.25f, 0.06f, 0.02f, 0.95f);
                }
            }
            return used;
        }
    }
}
