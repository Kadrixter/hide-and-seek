using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>Main menu panel (top right) shown when GitHub has a newer build: version, notes, "Zaktualizuj".</summary>
    public class UpdateBanner : MonoBehaviour
    {
        private RectTransform _panel;
        private Text _title;
        private Button _button;

        public static void Create(Transform canvasRoot)
        {
            var rt = UiKit.Rect("UpdateBanner", canvasRoot);
            // Bottom right, just above the "wersja ..." label.
            rt.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 118), new Vector2(440, 64)); // above the menu weapon picker
            rt.gameObject.AddComponent<UpdateBanner>().Build(rt);
        }

        private void Build(RectTransform rt)
        {
            _panel = rt;
            var bg = UiKit.Image("Bg", rt, new Color(0.03f, 0.05f, 0.04f, 0.88f));
            bg.rectTransform.Stretch();
            var bar = UiKit.Image("Bar", rt, UiKit.Accent);
            bar.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 0));

            _title = UiKit.Label("Title", rt, "", 20, TextAnchor.MiddleLeft, null, FontStyle.Bold);
            _title.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(-200, 0));
            _button = UiKit.BoxButton("Update", rt, "Zaktualizuj", OnClick, 20, new Color(0.13f, 0.32f, 0.16f));
            ((RectTransform)_button.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(170, 44));

            UpdateChecker.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy() => UpdateChecker.Changed -= Refresh;

        private void Refresh()
        {
            if (_panel == null) return;
            bool show = UpdateChecker.Status == UpdateChecker.State.Available;
            _panel.gameObject.SetActive(show);
            if (!show) return;
            _title.text = $"Dostępna aktualizacja\n<size=15><color=#8a948f>wersja {UpdateChecker.Latest}</color></size>";
            _button.GetComponentInChildren<Text>().text = UpdateChecker.CanUpdate ? "Zaktualizuj" : "Pobierz";
        }

        private void OnClick()
        {
            if (UpdateChecker.RunUpdater()) return;
            Application.OpenURL($"https://github.com/{UpdateChecker.Repo}/releases/latest");
        }
    }
}
