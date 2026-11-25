using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Shooter's widgets at the pool table, bottom right (above the key hints): the power bar (fills while the mouse
    /// is pulled back, green -> yellow -> red) and the cue ball with the red dot where the tip will hit it (spin).
    /// Shown only on the local player's own turn. Nothing here takes clicks.
    /// </summary>
    public sealed class PoolAimHud : MonoBehaviour
    {
        const float BarH = 230f, BallD = 112f;

        Canvas _canvas;
        RectTransform _root, _fill, _dot;
        Image _fillImg, _ballImg;
        Text _pct, _spinLabel;
        float _shownPower;

        public static PoolAimHud Create(Transform parent)
        {
            var canvas = UiKit.CreateCanvas("PoolAimHud", 41, parent);
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) Destroy(raycaster);
            var h = canvas.gameObject.AddComponent<PoolAimHud>();
            h._canvas = canvas;
            h.Build();
            return h;
        }

        void Build()
        {
            _root = UiKit.Rect("Root", _canvas.transform);
            _root.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 78f), new Vector2(250f, BarH + 44f));

            // Power bar.
            var bg = NoRay(UiKit.Image("PowerBg", _root, new Color(0.05f, 0.055f, 0.06f, 0.78f)));
            bg.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, BarH));
            _fillImg = NoRay(UiKit.Image("PowerFill", bg.transform, Color.green));
            _fill = _fillImg.rectTransform;
            _fill.Place(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(-6f, 0f));
            for (int i = 1; i < 4; i++)
            {
                var tick = NoRay(UiKit.Image("Tick" + i, bg.transform, new Color(1f, 1f, 1f, 0.18f)));
                tick.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, BarH * i / 4f), new Vector2(0f, 2f));
            }
            var powerLabel = UiKit.Label("PowerLabel", _root, "SIŁA", 18, TextAnchor.LowerCenter, UiKit.TextDim, FontStyle.Bold);
            powerLabel.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(17f, BarH + 4f), new Vector2(90f, 22f));
            powerLabel.AddOutline(1.5f, 0.8f);
            _pct = UiKit.Label("PowerPct", _root, "", 20, TextAnchor.LowerCenter, UiKit.Text, FontStyle.Bold);
            _pct.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(17f, BarH + 24f), new Vector2(90f, 24f));
            _pct.AddOutline(1.5f, 0.85f);

            // Cue ball with the tip spot.
            var ballBg = NoRay(UiKit.Image("SpinBg", _root, new Color(0.05f, 0.055f, 0.06f, 0.7f)));
            ballBg.sprite = UiStyle.Circle;
            ballBg.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(BallD + 14f, BallD + 14f));
            _ballImg = NoRay(UiKit.Image("CueBall", ballBg.transform, new Color(0.95f, 0.94f, 0.9f, 1f)));
            _ballImg.sprite = UiStyle.Circle;
            _ballImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BallD, BallD));
            var h = NoRay(UiKit.Image("CrossH", _ballImg.transform, new Color(0f, 0f, 0f, 0.16f)));
            h.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BallD * 0.9f, 2f));
            var v = NoRay(UiKit.Image("CrossV", _ballImg.transform, new Color(0f, 0f, 0f, 0.16f)));
            v.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, BallD * 0.9f));
            var dot = NoRay(UiKit.Image("Tip", _ballImg.transform, new Color(0.86f, 0.12f, 0.1f, 1f)));
            dot.sprite = UiStyle.Circle;
            _dot = dot.rectTransform;
            _dot.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            _spinLabel = UiKit.Label("SpinLabel", _root, "PODKRĘCENIE", 18, TextAnchor.LowerCenter, UiKit.TextDim, FontStyle.Bold);
            _spinLabel.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-(BallD + 14f) * 0.5f, BallD + 20f), new Vector2(180f, 22f));
            _spinLabel.AddOutline(1.5f, 0.8f);
            _root.gameObject.SetActive(false);
        }

        static Image NoRay(Image img)
        {
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Every frame: <paramref name="show"/> = the own turn; power 0..1, spin -1..1.</summary>
        public void Tick(bool show, float power, bool pulling, Vector2 spin)
        {
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) { _shownPower = 0f; return; }
            _shownPower = pulling ? power : Mathf.MoveTowards(_shownPower, 0f, Time.unscaledDeltaTime * 2.5f);
            float p = Mathf.Clamp01(_shownPower);
            _fill.sizeDelta = new Vector2(-6f, Mathf.Max(0f, (BarH - 6f) * p));
            _fillImg.color = p < 0.5f ? Color.Lerp(new Color(0.35f, 0.85f, 0.35f), new Color(0.98f, 0.85f, 0.25f), p * 2f)
                                      : Color.Lerp(new Color(0.98f, 0.85f, 0.25f), new Color(0.95f, 0.25f, 0.18f), (p - 0.5f) * 2f);
            _pct.text = pulling ? Mathf.RoundToInt(p * 100f) + "%" : "";
            _dot.anchoredPosition = spin * (BallD * 0.5f);
        }
    }
}
