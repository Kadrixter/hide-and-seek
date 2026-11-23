using System;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// "Ustawienia rundy" panel: a row shared by the Glock toggle and the killer count stepper  [-]  ZABÓJCY: 2  [+].
    /// The count goes 1..<see cref="MatchManager.MaxSeekersSetting"/>; the line under it shows the limit for the players
    /// in the room right now (players - 1) and turns red when the setting is too high - the lever then refuses to start.
    /// Only the host changes it; others see the live value.
    /// </summary>
    public class SeekerCountRow : MonoBehaviour
    {
        private Func<int> _get;
        private Text _label;
        private float _next;

        /// <summary>Horizontal row (height 50) for the Glock button + the stepper.</summary>
        public static RectTransform Row(Transform panel)
        {
            var row = UiKit.Rect("GlockSeekersRow", panel);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
            row.Height(50);
            return row;
        }

        /// <summary>Adds the stepper to <paramref name="row"/>; <paramref name="set"/> null = view only (not the host).</summary>
        public static void Build(RectTransform row, Func<int> get, Action<int> set)
        {
            Button minus = null, plus = null;
            minus = UiKit.BoxButton("SeekersMinus", row, "-", () => set?.Invoke(Mathf.Clamp(get() - 1, 1, MatchManager.MaxSeekersSetting)), 34);
            Width(minus, 58f);
            var box = UiKit.Image("SeekersBox", row, new Color(0.11f, 0.05f, 0.05f, 1f)).Rounded();
            Width(box, 214f);
            var label = UiKit.Label("SeekersLabel", box.transform, "", 22, TextAnchor.MiddleCenter, null, FontStyle.Bold);
            label.rectTransform.Stretch(4f);
            label.supportRichText = true;
            label.raycastTarget = false;
            plus = UiKit.BoxButton("SeekersPlus", row, "+", () => set?.Invoke(Mathf.Clamp(get() + 1, 1, MatchManager.MaxSeekersSetting)), 30);
            Width(plus, 58f);
            minus.interactable = plus.interactable = set != null;
            foreach (var b in new[] { minus, plus })
            {
                var chevron = b.transform.Find("Chevron");
                if (chevron != null) chevron.gameObject.SetActive(false);
            }
            var view = box.gameObject.AddComponent<SeekerCountRow>();
            view._get = get;
            view._label = label;
            view.Refresh();
        }

        private static void Width(Component c, float w)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.minWidth = w;
            le.flexibleWidth = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            if (_label == null || _get == null) return;
            int want = _get(), players = 0;
            foreach (var p in NetPlayer.All) if (p != null) players++;
            int max = MatchManager.MaxSeekersFor(players);
            bool tooMany = want > max;
            string sub = tooMany ? $"<color=#ff6a5a>za dużo: max {max} przy {players} graczach</color>"
                : $"<color=#9aa0a6>max {max} przy {players} graczach</color>";
            _label.text = $"<color=#ff7a66>ZABÓJCY: {want}</color>\n<size=15>{sub}</size>";
        }
    }
}
