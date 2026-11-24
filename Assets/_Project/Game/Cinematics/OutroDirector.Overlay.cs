using System.Collections.Generic;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.Cinematics
{
    public sealed partial class OutroDirector
    {
        Image _preFade, _dimmer;
        readonly List<Text> _nicks = new();
        readonly List<(RectTransform rt, int puppet)> _badges = new();
        readonly List<(CanvasGroup g, RectTransform rt, Vector2 home)> _cards = new();

        void BuildOverlay()
        {
            // Fade out of the game view before the cut to the stage.
            var c = UiKit.CreateCanvas("OutroPreFade", 418, transform);
            _preFade = UiKit.Image("Black", c.transform, new Color(0f, 0f, 0f, 0f));
            _preFade.rectTransform.Stretch();
            _preFade.raycastTarget = false;
        }

        void AttachOverlay()
        {
            var root = _cam.Overlay;
            // Title at the top: WYGRYWAJĄ ... + how the round went.
            var tr = (RectTransform)_cam.Title.transform.parent;
            tr.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -138), new Vector2(1700, 200));
            bool seekers = _pack.SeekersWon;
            int seekerCount = 0;
            foreach (var id in Row) if (HideAndSeek.Game.Net.NetPlayer.Find(id) is var p && p != null && p.IsSeeker) seekerCount++;
            _cam.Title.text = seekers ? (seekerCount > 1 ? "WYGRYWAJĄ SZUKAJĄCY" : "WYGRYWA SZUKAJĄCY") : "WYGRYWAJĄ CHOWAJĄCY";
            _cam.Title.fontSize = 70;
            _cam.Sub.text = seekers
                ? (_pack.Hiders > 0 ? $"Złapani wszyscy chowający ({_pack.Hiders})" : "Złapani wszyscy")
                : $"Przetrwali do końca: {_pack.Survivors} z {_pack.Hiders}";
            _cam.Sub.color = seekers ? CineStyle.SeekerAccent : CineStyle.Accent;

            _dimmer = UiKit.Image("Dim", root, new Color(0f, 0f, 0f, 0f));
            _dimmer.rectTransform.Stretch();
            _dimmer.raycastTarget = false;
            _dimmer.transform.SetSiblingIndex(3); // over the bars, under the title and the cards

            for (int i = 0; i < _puppets.Count; i++)
            {
                var p = _puppets[i];
                var lbl = UiKit.Label("Nick" + i, root, p != null ? p.Nick : "", 26, TextAnchor.UpperCenter, Color.white, FontStyle.Bold);
                lbl.rectTransform.sizeDelta = new Vector2(300, 40);
                lbl.AddOutline(1.5f, 0.75f);
                lbl.color = new Color(1f, 1f, 1f, 0f);
                _nicks.Add(lbl);
            }
            BuildBadges(root);
            BuildCards(root);
        }

        void UpdateOverlay(float t)
        {
            if (_preFade != null)
            {
                _preFade.color = new Color(0f, 0f, 0f, CineKit.S(t, 0f, OutroTimeline.FadeOut));
                if (_cam != null && t > OutroTimeline.StageIn + 0.05f) { Destroy(_preFade.canvas.gameObject); _preFade = null; }
            }
            if (_cam == null) return;
            float title = CineKit.S(t, OutroTimeline.TitleIn, OutroTimeline.TitleIn + 0.45f);
            _cam.TitleGroup.alpha = title;
            var titleRt = (RectTransform)_cam.Title.transform.parent;
            titleRt.anchoredPosition = new Vector2(0, -138 + 18f * (1f - title));

            float awards = CineKit.S(t, OutroTimeline.Awards, OutroTimeline.Awards + 0.5f);
            if (_dimmer != null) _dimmer.color = new Color(0f, 0f, 0f, 0.5f * awards);
            float nicks = CineKit.S(t, OutroTimeline.Travel - 0.8f, OutroTimeline.Travel) * (1f - awards);
            var root = _cam.Overlay;
            // Feet out of the frame (close shots): the nick stays just above the bottom bar.
            float ch = root.rect.height, floorY = -ch * 0.5f + ch * 0.105f * _cam.Bars + 46f;
            for (int i = 0; i < _nicks.Count; i++)
            {
                var p = _puppets[i];
                if (p == null) continue;
                var nrt = _nicks[i].rectTransform;
                Place(nrt, p.transform.position + Vector3.down * 0.02f, new Vector2(0, -16), out bool vis);
                if (nrt.anchoredPosition.y < floorY) nrt.anchoredPosition = new Vector2(nrt.anchoredPosition.x, floorY);
                _nicks[i].color = new Color(1f, 1f, 1f, vis ? nicks : 0f);
            }
            foreach (var (rt, pi) in _badges)
            {
                var p = _puppets[pi];
                if (p == null) continue;
                Place(rt, p.Body.Head.position + Vector3.up * 0.45f, Vector2.zero, out bool vis);
                var g = rt.GetComponent<CanvasGroup>();
                g.alpha = vis ? CineKit.S(t, OutroTimeline.Travel, OutroTimeline.Travel + 0.6f) : 0f;
            }
            for (int i = 0; i < _cards.Count; i++)
            {
                var (g, rt, home) = _cards[i];
                float k = CineKit.S(t, OutroTimeline.Awards + 0.4f + i * OutroTimeline.CardStep, OutroTimeline.Awards + 0.75f + i * OutroTimeline.CardStep);
                float outK = CineKit.S(t, OutroTimeline.End - 0.45f, OutroTimeline.End);
                g.alpha = k * (1f - outK);
                rt.anchoredPosition = home + new Vector2(0f, -26f * (1f - k));
            }
            if (t > OutroTimeline.End - 0.5f) _cam.Fade = CineKit.S(t, OutroTimeline.End - 0.5f, OutroTimeline.End);
        }

        /// <summary>Puts an overlay element at the screen point of <paramref name="world"/>.</summary>
        void Place(RectTransform rt, Vector3 world, Vector2 offset, out bool visible)
        {
            var cam = _cam.Cam;
            Vector3 sp = cam.WorldToScreenPoint(world);
            visible = sp.z > 0.1f;
            var root = _cam.Overlay;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = local + offset;
        }
    }
}
