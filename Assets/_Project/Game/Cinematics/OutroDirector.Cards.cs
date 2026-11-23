using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.Cinematics
{
    public sealed partial class OutroDirector
    {
        const float CardW = 540f, CardH = 112f, GapX = 22f, GapY = 14f;

        /// <summary>Award cards in a grid (3 columns, centred, under the title): ring icon, name, nick, result.</summary>
        void BuildCards(RectTransform root)
        {
            var awards = _pack.Awards ?? System.Array.Empty<AwardPack>();
            int n = awards.Length;
            if (n == 0) return;
            // Two columns at the sides of the screen, the winners stay visible in the middle.
            int per = (n + 1) / 2;
            float scale = per > 6 ? 6f / per : 1f;
            float w = CardW * scale * 0.86f, h = CardH * scale;
            for (int i = 0; i < n; i++)
            {
                var a = awards[i];
                int c = i < per ? 0 : 1, r = i < per ? i : i - per;
                float x = (c == 0 ? -1f : 1f) * (960f - 36f - w * 0.5f);
                float y = 250f - h * 0.5f - r * (h + GapY);
                var card = Card(root, (AwardId)a.Id, a.Nicks.ToString(), a.Value.ToString(), w, h);
                var home = new Vector2(x, y);
                card.anchoredPosition = home;
                var g = card.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                _cards.Add((g, card, home));
            }
        }

        static RectTransform Card(RectTransform root, AwardId id, string nicks, string value, float w, float h)
        {
            bool seeker = AwardBook.SeekerAward(id);
            Color accent = seeker ? CineStyle.SeekerAccent : CineStyle.Accent;
            var rt = UiKit.Rect("Award_" + id, root);
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            float k = h / CardH;
            var bg = UiKit.Image("Bg", rt, new Color(0.02f, 0.03f, 0.035f, 0.78f));
            bg.rectTransform.Stretch();
            bg.sprite = UiStyle.RoundSmall;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var bar = UiKit.Image("Accent", rt, accent);
            bar.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(4f, -16f * k));
            Icon(rt, id, accent, 64f * k, new Vector2(18f * k + 32f * k, 0f));
            // TODO(menuui): Montserrat (UiFonts) after the merge; LegacyRuntime here.
            var name = UiKit.Label("Name", rt, AwardBook.Name(id), Mathf.RoundToInt(25 * k), TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(104f * k, -12f * k), new Vector2(-116f * k, 30f * k));
            var who = UiKit.Label("Nick", rt, nicks, Mathf.RoundToInt(27 * k), TextAnchor.UpperLeft, accent, FontStyle.Bold);
            who.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(104f * k, -42f * k), new Vector2(-116f * k, 32f * k));
            who.horizontalOverflow = HorizontalWrapMode.Overflow;
            var res = UiKit.Label("Value", rt, value, Mathf.RoundToInt(20 * k), TextAnchor.UpperLeft, CineStyle.Dim);
            res.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(104f * k, -78f * k), new Vector2(-116f * k, 26f * k));
            res.horizontalOverflow = HorizontalWrapMode.Overflow;
            return rt;
        }

        /// <summary>Award icon: the letter in a thin ring (no emoji, no images).</summary>
        static RectTransform Icon(RectTransform parent, AwardId id, Color accent, float size, Vector2 centreFromLeft)
        {
            var rt = UiKit.Rect("Icon", parent);
            rt.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), centreFromLeft, new Vector2(size, size));
            var outer = UiKit.Image("Ring", rt, accent);
            outer.sprite = UiStyle.Circle;
            outer.rectTransform.Stretch();
            outer.raycastTarget = false;
            var inner = UiKit.Image("Hole", rt, new Color(0.03f, 0.04f, 0.045f, 1f));
            inner.sprite = UiStyle.Circle;
            inner.rectTransform.Stretch(size * 0.06f);
            inner.raycastTarget = false;
            var l = UiKit.Label("L", rt, AwardBook.Letter(id), Mathf.RoundToInt(size * 0.5f), TextAnchor.MiddleCenter, accent, FontStyle.Bold);
            l.rectTransform.Stretch();
            return rt;
        }

        /// <summary>Small award rings over the heads of the figures that won something.</summary>
        void BuildBadges(RectTransform root)
        {
            var awards = _pack.Awards ?? System.Array.Empty<AwardPack>();
            for (int i = 0; i < _puppets.Count; i++)
            {
                var p = _puppets[i];
                if (p == null) continue;
                var mine = new System.Collections.Generic.List<AwardId>();
                foreach (var a in awards) if (a.Holder == p.PlayerId) mine.Add((AwardId)a.Id);
                if (mine.Count == 0) continue;
                var rt = UiKit.Rect("Badges" + i, root);
                rt.sizeDelta = new Vector2(mine.Count * 40f, 40f);
                rt.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
                for (int k = 0; k < mine.Count; k++)
                {
                    var ic = Icon(rt, mine[k], AwardBook.SeekerAward(mine[k]) ? CineStyle.SeekerAccent : CineStyle.Accent, 34f, new Vector2(20f + k * 40f, 0f));
                    ic.anchorMin = ic.anchorMax = new Vector2(0f, 0.5f);
                }
                _badges.Add((rt, i));
            }
        }
    }
}
