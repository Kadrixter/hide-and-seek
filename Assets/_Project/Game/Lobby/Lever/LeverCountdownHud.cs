using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Big countdown in the middle of the screen after the start lever is pulled (5, 4, 3, 2, 1, 0), seen by everyone
    /// in the lobby; each number pops in and shrinks. Shows "PRZERWANE" with the reason when the countdown is cancelled.
    /// </summary>
    public sealed class LeverCountdownHud : MonoBehaviour
    {
        Text _number, _caption;
        CanvasGroup _group;
        int _shown = -2;
        float _abortT = 9f;

        public static LeverCountdownHud Create(Transform parent)
        {
            var canvas = UiKit.CreateCanvas("LeverCountdownCanvas", 30, parent);
            var hud = canvas.gameObject.AddComponent<LeverCountdownHud>();
            hud._group = canvas.gameObject.AddComponent<CanvasGroup>();
            hud._group.blocksRaycasts = false;
            hud._group.interactable = false;
            hud._number = UiKit.Label("Number", canvas.transform, "", 300, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            hud._number.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1100f, 400f));
            hud._number.AddOutline(5f, 0.9f);
            hud._caption = UiKit.Label("Caption", canvas.transform, "", 46, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            hud._caption.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1400f, 80f));
            hud._caption.AddOutline(3f, 0.9f);
            hud._group.alpha = 0f;
            return hud;
        }

        /// <summary><paramref name="number"/> 5..0 (or -1 to hide); <paramref name="frac"/> = time within that second (0..1).</summary>
        public void Show(int number, float frac)
        {
            if (_abortT < 2.2f) return;
            if (number < 0) { Hide(); return; }
            _group.alpha = 1f;
            if (number != _shown)
            {
                _shown = number;
                _number.text = number.ToString();
                _caption.text = number > 0 ? "START RUNDY ZA..." : "START!";
            }
            // Pop in big, settle, fade a little before the next one ("0" stays bright).
            float pop = 1f + 0.45f * Mathf.Exp(-frac * 9f);
            _number.rectTransform.localScale = Vector3.one * pop;
            Color c = number == 0 ? new Color(0.36f, 1f, 0.45f) : number <= 2 ? new Color(1f, 0.75f, 0.3f) : Color.white;
            c.a = number == 0 ? 1f : Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((frac - 0.6f) / 0.4f));
            _number.color = c;
            _caption.color = new Color(UiKit.Text.r, UiKit.Text.g, UiKit.Text.b, 0.9f);
        }

        public void Hide()
        {
            if (_abortT < 2.2f) return;
            _group.alpha = 0f;
            _shown = -2;
        }

        public void Abort(string why)
        {
            _abortT = 0f;
            _shown = -2;
            _group.alpha = 1f;
            _number.text = "PRZERWANE";
            _number.fontSize = 120;
            _number.rectTransform.localScale = Vector3.one;
            _number.color = UiKit.Danger;
            _caption.text = why;
            _caption.color = UiKit.Text;
        }

        void Update()
        {
            if (_abortT >= 2.2f) return;
            _abortT += Time.deltaTime;
            _group.alpha = Mathf.Clamp01((2.2f - _abortT) / 0.4f);
            if (_abortT >= 2.2f)
            {
                _number.fontSize = 300;
                _group.alpha = 0f;
            }
        }
    }
}
