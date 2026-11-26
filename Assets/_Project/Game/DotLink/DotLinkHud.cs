using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Screen texts while the local player works on the dot link panel: a line at the top (what to do, joined pairs,
    /// filled cells), the controls at the bottom, and "Połączono - brama się otwiera" once it is solved.
    /// </summary>
    public sealed class DotLinkHud : MonoBehaviour
    {
        Canvas _canvas;
        Text _status, _hints, _flash;
        Image _panel;
        float _flashUntil;
        bool _wasSolved;
        string _flashText = "";

        public static DotLinkHud Create(Transform parent)
        {
            var canvas = UiKit.CreateCanvas("DotLinkHud", 40, parent);
            var h = canvas.gameObject.AddComponent<DotLinkHud>();
            h._canvas = canvas;
            h.Build();
            return h;
        }

        void Build()
        {
            var root = _canvas.transform;
            _panel = UiStyle.Panel("Panel", root, new Color(0.04f, 0.05f, 0.045f, 0.82f));
            _panel.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(1040f, 56f));
            _status = UiKit.Label("Status", _panel.transform, "", 24, TextAnchor.MiddleCenter, new Color(0.78f, 0.86f, 0.8f, 1f));
            _status.rectTransform.Stretch(6f);
            _hints = UiKit.Label("Hints", root, "LPM - rysuj (przeciągnij od kropki)     PPM - wyczyść kolor     Kółko - przybliż     Esc - wyjdź",
                22, TextAnchor.LowerCenter, new Color(0.85f, 0.86f, 0.84f, 0.92f));
            _hints.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 132), new Vector2(1500, 34)); // above the hotbar and the voice meter (in a round, unlike the lobby boards)
            _hints.AddOutline(2f, 0.8f);
            _flash = UiKit.Label("Flash", root, "", 52, TextAnchor.MiddleCenter, new Color(0.7f, 0.92f, 0.72f, 1f), FontStyle.Bold);
            _flash.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1400, 80));
            _flash.AddOutline(3f, 0.9f);
            Show(false);
        }

        void Show(bool on)
        {
            if (_panel.gameObject.activeSelf != on) _panel.gameObject.SetActive(on);
            if (_hints.gameObject.activeSelf != on) _hints.gameObject.SetActive(on);
        }

        public static string StatusLine(DotLinkBoard b)
        {
            if (b == null) return "Panel się uruchamia...";
            if (b.SolvedFlag) return "Wszystkie obwody połączone";
            return $"Połącz pary kolorów i wypełnij całą siatkę     Pary: {b.CompleteCount}/{b.Puzzle.Colors}     Pola: {b.FilledCount}/{b.Puzzle.Cells}";
        }

        /// <summary>Last status line shown (tests log it).</summary>
        public static string LastStatus { get; private set; } = "";

        public void Tick(bool active, IDotLinkTarget panel, MatchManager mm)
        {
            Show(active);
            var board = DotLinkLocal.Draft ?? (panel != null ? panel.NetBoard : null);
            bool solved = panel != null && panel.NetBoard != null && panel.NetBoard.SolvedFlag;
            if (solved && !_wasSolved && active)
            {
                _flashUntil = Time.unscaledTime + 2.2f;
                _flashText = panel.SolvedFlash;
            }
            _wasSolved = solved;
            _flash.text = Time.unscaledTime < _flashUntil ? _flashText : "";
            if (!active) return;
            LastStatus = StatusLine(board) + (panel != null ? panel.StatusExtra : "");
            _status.text = LastStatus;
        }
    }
}
