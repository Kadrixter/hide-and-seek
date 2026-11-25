using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Screen layer of the dead players' lobby: the fade (to white into the lobby door's light, from white out of it),
    /// a small dot in the middle, the E prompt of <see cref="DeadLobbyInteract"/>, the TV line
    /// ("LPM / PPM - zmień gracza   F - oglądaj na pełnym ekranie" + volume) and "F - wróć do lobby" while watching.
    /// </summary>
    public sealed class DeadLobbyHud : MonoBehaviour
    {
        private static DeadLobbyHud _inst;

        private Image _fade, _dot;
        private Text _prompt, _tv, _back;
        private float _from, _to, _t, _dur;
        private string _promptText;
        private Vector3 _promptWorld;

        /// <summary>True while a fade is still running.</summary>
        public bool Fading => _t < _dur;

        public static DeadLobbyHud Create(Transform parent)
        {
            var canvas = UiKit.CreateCanvas("DeadLobbyHud", 950, parent);
            var h = canvas.gameObject.AddComponent<DeadLobbyHud>();
            h._dot = UiKit.Image("Dot", canvas.transform, new Color(1f, 1f, 1f, 0.75f));
            h._dot.raycastTarget = false;
            h._dot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));
            h._prompt = UiKit.Label("Prompt", canvas.transform, "", 28, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            h._prompt.rectTransform.sizeDelta = new Vector2(900f, 44f);
            h._prompt.AddOutline(2f);
            h._tv = UiKit.Label("TvLine", canvas.transform, "", 30, TextAnchor.LowerCenter, Color.white, FontStyle.Bold);
            h._tv.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1400f, 90f));
            h._tv.supportRichText = true;
            h._tv.AddOutline(2f);
            h._back = UiKit.Label("BackLine", canvas.transform, "", 28, TextAnchor.LowerCenter, new Color(0.9f, 0.9f, 0.92f), FontStyle.Bold);
            h._back.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 236f), new Vector2(900f, 44f));
            h._back.AddOutline(2f);
            h._fade = UiKit.Image("Fade", canvas.transform, new Color(1f, 1f, 1f, 0f));
            h._fade.raycastTarget = false;
            h._fade.rectTransform.Stretch();
            _inst = h;
            return h;
        }

        /// <summary>Fades the screen to <paramref name="c"/> at <paramref name="alpha"/>.</summary>
        public void FadeTo(Color c, float alpha, float seconds)
        {
            _from = _fade.color.a;
            _fade.color = new Color(c.r, c.g, c.b, _from);
            _to = alpha;
            _t = 0f;
            _dur = Mathf.Max(0.01f, seconds);
        }

        /// <summary>Starts fully covered with <paramref name="c"/> and clears.</summary>
        public void FadeFrom(Color c, float seconds)
        {
            _fade.color = new Color(c.r, c.g, c.b, 1f);
            FadeTo(c, 0f, seconds);
        }

        public void Clear()
        {
            _promptText = null;
            _tv.text = _back.text = _prompt.text = "";
            _t = _dur = 0f;
            _fade.color = new Color(_fade.color.r, _fade.color.g, _fade.color.b, 0f);
        }

        /// <summary>The E prompt at a world point (null = none), set every frame by <see cref="DeadLobbyInteract"/>.</summary>
        public static void SetPrompt(string text, Vector3 world)
        {
            if (_inst == null) return;
            _inst._promptText = text;
            _inst._promptWorld = world;
        }

        private void LateUpdate()
        {
            if (_t < _dur)
            {
                _t += Time.unscaledDeltaTime;
                var c = _fade.color;
                c.a = Mathf.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / _dur)));
                _fade.color = c;
            }
            bool lobby = DeadLobby.Stage == DeadLobbyStage.Lobby;
            bool menu = LocalPlayerController.MenuOpen;
            _dot.enabled = lobby && !menu;
            var tv = DeadLobbyTv.Instance;
            bool atTv = lobby && tv != null && tv.InReach && !menu;
            if (atTv)
            {
                string who = tv.Target != null ? $"Na telewizorze: <color=#ffffff>{tv.Target.Nick}</color>\n" : "Na telewizorze: brak sygnału\n";
                string vol = tv.ShowVolume ? $"   <color=#ffd34d>Głośność: {Mathf.RoundToInt(DeadLobbyTv.Volume * 100f)}%</color>" : "   Kółko - głośność";
                _tv.text = who + "LPM / PPM - zmień gracza   F - oglądaj na pełnym ekranie" + vol;
            }
            else _tv.text = "";
            _back.text = DeadLobby.Watching && !menu ? "F - wróć do lobby" : "";
            UpdatePrompt(lobby && !atTv && !menu);
        }

        private void UpdatePrompt(bool show)
        {
            var body = DeadLobby.Body;
            if (!show || string.IsNullOrEmpty(_promptText) || body == null || body.Cam == null)
            {
                _prompt.text = "";
                return;
            }
            Vector3 sp = body.Cam.WorldToScreenPoint(_promptWorld);
            if (sp.z <= 0f) { _prompt.text = ""; return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, sp, null, out Vector2 local);
            _prompt.rectTransform.anchorMin = _prompt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _prompt.rectTransform.anchoredPosition = local + new Vector2(0f, 30f);
            _prompt.text = _promptText;
        }

        private void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }
    }
}
