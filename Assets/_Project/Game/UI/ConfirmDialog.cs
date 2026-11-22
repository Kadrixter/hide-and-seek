using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// "Are you sure?" box on top of everything (own canvas): question, optional detail line, a red "yes" and a "no"
    /// button. Esc = no. Used before quitting the game and before leaving / closing a session.
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        private static ConfirmDialog _open;
        private static int _closedFrame = -1;
        private Action _onYes;

        public static bool IsOpen => _open != null;

        /// <summary>True while a dialog is open or was closed with Esc in this frame (other Esc handlers should skip it).</summary>
        public static bool ConsumesEscape => _open != null || _closedFrame == Time.frameCount;

        /// <summary>Opens the dialog (replaces one that is already open).</summary>
        public static void Show(string question, string detail, string yes, string no, Action onYes)
        {
            if (_open != null) _open.Close();
            var canvas = UiKit.CreateCanvas("ConfirmDialog", 500);
            var d = canvas.gameObject.AddComponent<ConfirmDialog>();
            d._onYes = onYes;
            _open = d;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var dim = UiKit.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            var panel = UiStyle.Panel("Panel", dim.transform, UiKit.Panel);
            float h = string.IsNullOrEmpty(detail) ? 250f : 290f;
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, h));
            UiKit.VStack(panel.rectTransform, 14, 34).padding.top = 36;
            UiKit.Label("Question", panel.transform, question, 32, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(50);
            if (!string.IsNullOrEmpty(detail))
                UiKit.Label("Detail", panel.transform, detail, 21, TextAnchor.MiddleCenter, UiKit.TextDim).Height(28);

            var row = UiKit.Rect("Buttons", panel.transform);
            row.Height(64);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 18;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;
            UiKit.Button("Yes", row, yes, () => { var a = d._onYes; d.Close(); a?.Invoke(); }, 28, new Color(0.42f, 0.11f, 0.1f));
            UiKit.Button("No", row, no, d.Close, 28);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }

        private void Close()
        {
            if (_open == this) _open = null;
            _closedFrame = Time.frameCount;
            Destroy(gameObject);
        }

        // ================================================================ the questions used by the menus

        public static void ConfirmQuit() =>
            Show("Na pewno chcesz wyjść z gry?", null, "Tak, wyjdź", "Nie", Application.Quit);

        /// <summary>Leaving a session: the host closes the server for everybody, a client only disconnects.</summary>
        public static void ConfirmLeave(bool host, Action leave)
        {
            if (host) Show("Na pewno chcesz zamknąć serwer?", "Wszyscy gracze zostaną rozłączeni.", "Tak, zamknij", "Nie", leave);
            else Show("Na pewno chcesz opuścić grę?", "Wrócisz do menu głównego.", "Tak, wyjdź", "Nie", leave);
        }
    }
}
