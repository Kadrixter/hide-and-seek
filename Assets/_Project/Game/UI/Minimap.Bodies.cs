using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Bodies of dead hiders on the hiders' minimap (to find them for the adrenaline): a dark maroon dot with a light
    /// rim at the body's chest (follows the ragdoll as it slides), only on the viewer's level (|dy| &lt;= 1.7 m, like
    /// the downed markers), kept faded at the edge when outside the view. Under the teammate dots.
    /// The seeker does not get them: he knows where he killed, and a marker on the body would only help him wait at
    /// it for the one who comes to bring it back.
    /// </summary>
    public partial class Minimap
    {
        private readonly List<Image> _bodyDots = new();
        private static Sprite _bodySprite;

        /// <summary>Test: body markers shown in the last frame.</summary>
        public static int BodyDots { get; private set; }

        private void UpdateBodyDots(NetPlayer me, Vector3 pos, float pxPerM, float size)
        {
            int used = 0;
            if (me != null && me.IsHider)
            {
                float edge = size * 0.5f - 8f;
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p == me || !p.IsHider || p.Life.Value != LifeState.Dead) continue;
                    Vector3 w = p.ChestPosition;
                    if (Mathf.Abs(w.y - 0.3f - pos.y) > 1.7f) continue; // the chest of a body lies ~0.3 m above its floor
                    Vector2 off = new Vector2(w.x - pos.x, w.z - pos.z) * pxPerM;
                    bool outside = Mathf.Abs(off.x) > edge || Mathf.Abs(off.y) > edge;
                    off = new Vector2(Mathf.Clamp(off.x, -edge, edge), Mathf.Clamp(off.y, -edge, edge));
                    var img = BodyDot(used++);
                    if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                    img.rectTransform.anchoredPosition = off;
                    img.rectTransform.sizeDelta = Vector2.one * (outside ? 10f : 14f);
                    img.color = new Color(1f, 1f, 1f, outside ? 0.55f : 1f);
                }
            }
            for (int i = used; i < _bodyDots.Count; i++)
                if (_bodyDots[i].gameObject.activeSelf) _bodyDots[i].gameObject.SetActive(false);
            BodyDots = used;
        }

        private Image BodyDot(int index)
        {
            while (_bodyDots.Count <= index)
            {
                var img = UiKit.Image("BodyDot", _clip, Color.white);
                img.sprite = BodySprite();
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                // Above the map (and its fade), below the team dots, pins and the player's arrow.
                var fade = _clip.Find("Fade");
                rt.SetSiblingIndex(fade != null ? fade.GetSiblingIndex() + 1 : 1);
                _bodyDots.Add(img);
            }
            return _bodyDots[index];
        }

        /// <summary>Dark maroon disc, a thin light rim and a dark outer edge (reads on dark floors and light walls alike).</summary>
        private static Sprite BodySprite() => _bodySprite != null ? _bodySprite : _bodySprite = Make(48, (x, y) =>
        {
            float d = new Vector2(x - 23.5f, y - 23.5f).magnitude / 23.5f;
            float a = Mathf.Clamp01((1f - d) * 10f);
            Color core = new Color(0.33f, 0.07f, 0.08f);
            Color rim = new Color(0.93f, 0.88f, 0.85f);
            Color dark = new Color(0.05f, 0.03f, 0.03f);
            Color c = d < 0.62f ? Color.Lerp(core * 1.25f, core, d / 0.62f)
                : d < 0.82f ? Color.Lerp(core, rim, Mathf.Clamp01((d - 0.62f) * 18f))
                : Color.Lerp(rim, dark, Mathf.Clamp01((d - 0.82f) * 14f));
            return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(a * 255));
        });
    }
}
