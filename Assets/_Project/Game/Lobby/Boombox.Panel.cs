using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Boombox panel (E): now playing, progress, loading state, transport, volume, bass, queue, adding links.</summary>
    public partial class Boombox
    {
        public static bool PanelOpen { get; private set; }
        private string _urlField = "";
        private string _note;
        private float _noteUntil;
        private GUIStyle _box, _btn, _label, _big, _field, _small;
        private Texture2D _bg, _bar, _barFill;

        // ================================================================ panel

        public static void OpenPanel()
        {
            if (Instance == null) return;
            PanelOpen = true;
            LocalPlayerController.MenuOpen = true;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(false);
            Instance._urlField = "";
        }

        public static void ClosePanel()
        {
            PanelOpen = false;
            LocalPlayerController.MenuOpen = false;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(true);
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void Styles()
        {
            if (_box != null) return;
            float s = Screen.height / 1080f;
            _bg = Tex(new Color(0.06f, 0.07f, 0.09f, 0.94f));
            _bar = Tex(new Color(1f, 1f, 1f, 0.15f));
            _barFill = Tex(new Color(0.35f, 0.95f, 0.5f, 1f));
            _box = new GUIStyle(GUI.skin.box) { normal = { background = _bg } };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * s), fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * s), wordWrap = false, clipping = TextClipping.Clip };
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(19 * s) };
            _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(34 * s), fontStyle = FontStyle.Bold };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(22 * s), alignment = TextAnchor.MiddleLeft };
        }

        private static string Fmt(float t) => t < 0 ? "--:--" : $"{(int)t / 60}:{(int)t % 60:00}";

        private void OnGUI()
        {
            if (!PanelOpen) return;
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (mm == null || me == null) { ClosePanel(); return; }
            Styles();
            float s = Screen.height / 1080f;
            float w = 900 * s, h = 640 * s, x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f, p = 24 * s, row = 46 * s;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _box);
            float cy = y + p;
            GUI.Label(new Rect(x + p, cy, w, 44 * s), "BOOMBOX", _big);
            cy += 54 * s;

            var r = mm.Radio.Value;
            string url = r.Index >= 0 && r.Index < mm.RadioQueue.Count ? mm.RadioQueue[r.Index].ToString() : null;
            (string title, float dur) inf = default;
            bool known = url != null && TryInfo(url, out inf);
            float pos = r.Loading ? 0f : r.Playing ? Mathf.Max(0f, (float)(mm.Now - r.StartTime)) : r.PausedAt;
            string state = !r.Playing ? "Pauza" : IsLoading ? "Ładowanie" : "Gra";
            GUI.Label(new Rect(x + p, cy, w - 2 * p, row), url == null ? "Nic nie gra - wklej link z YouTube poniżej." : $"{state}: {(known ? inf.title : url)}", _label);
            cy += row * 0.8f;
            if (url != null)
            {
                float bw = w - 2 * p - 160 * s;
                GUI.DrawTexture(new Rect(x + p, cy + 10 * s, bw, 10 * s), _bar);
                if (known && inf.dur > 0f) GUI.DrawTexture(new Rect(x + p, cy + 10 * s, bw * Mathf.Clamp01(pos / inf.dur), 10 * s), _barFill);
                GUI.Label(new Rect(x + p + bw + 12 * s, cy, 160 * s, row), $"{Fmt(pos)} / {(known && inf.dur > 0 ? Fmt(inf.dur) : "--:--")}", _small);
            }
            cy += row * 0.8f;
            string note = Time.unscaledTime < _noteUntil ? _note : _status;
            if (!string.IsNullOrEmpty(note))
            {
                GUI.color = IsLoading ? new Color(1f, 0.85f, 0.35f) : Color.white;
                GUI.Label(new Rect(x + p, cy, w - 2 * p, row), note, _small);
                GUI.color = Color.white;
            }
            cy += row * 0.8f;

            // Transport + volume + bass.
            float bx = x + p, bwid = 150 * s, gap = 10 * s;
            if (GUI.Button(new Rect(bx, cy, bwid, row), "<< Poprz.", _btn)) me.RequestRadioRpc(RadioOp.Prev, default);
            if (GUI.Button(new Rect(bx + (bwid + gap), cy, bwid * 1.2f, row), r.Playing ? "|| Pauza" : "> Wznów", _btn)) me.RequestRadioRpc(RadioOp.Toggle, default);
            if (GUI.Button(new Rect(bx + (bwid + gap) * 2.2f, cy, bwid, row), "Nast. >>", _btn)) me.RequestRadioRpc(RadioOp.Next, default);
            cy += row + gap;
            GUI.Label(new Rect(bx, cy + 6 * s, 170 * s, row), $"Głośność {r.Volume}%", _label);
            if (GUI.Button(new Rect(bx + 190 * s, cy, 60 * s, row), "-", _btn)) me.RequestRadioRpc(RadioOp.VolDown, default);
            if (GUI.Button(new Rect(bx + 260 * s, cy, 60 * s, row), "+", _btn)) me.RequestRadioRpc(RadioOp.VolUp, default);
            GUI.Label(new Rect(bx + 380 * s, cy + 6 * s, 170 * s, row), $"Bas {r.Bass}%", _label);
            if (GUI.Button(new Rect(bx + 540 * s, cy, 60 * s, row), "-", _btn)) me.RequestRadioRpc(RadioOp.BassDown, default);
            if (GUI.Button(new Rect(bx + 610 * s, cy, 60 * s, row), "+", _btn)) me.RequestRadioRpc(RadioOp.BassUp, default);
            cy += row + gap * 2f;

            // Queue.
            GUI.Label(new Rect(bx, cy, w, row * 0.7f), $"Kolejka ({mm.RadioQueue.Count}/{MatchManager.RadioMaxQueue}):", _small);
            cy += row * 0.7f;
            int first = Mathf.Clamp(r.Index - 1, 0, Mathf.Max(0, mm.RadioQueue.Count - 5));
            for (int i = first; i < Mathf.Min(mm.RadioQueue.Count, first + 5); i++)
            {
                string u = mm.RadioQueue[i].ToString();
                string t = TryInfo(u, out var qi) ? qi.title : u;
                GUI.color = i == r.Index ? new Color(0.5f, 1f, 0.6f) : i < r.Index ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                GUI.Label(new Rect(bx, cy, w - 2 * p, row * 0.62f), $"{i + 1}. {t}", _small);
                GUI.color = Color.white;
                cy += row * 0.62f;
            }
            cy = y + h - p - row * 2f - gap;

            // Paste a link.
            GUI.Label(new Rect(bx, cy - row * 0.7f, w, row * 0.7f), "Link z YouTube (Ctrl+V):", _small);
            GUI.SetNextControlName("RadioUrl");
            _urlField = GUI.TextField(new Rect(bx, cy, w - 2 * p - 2 * (130 * s + gap), row), _urlField ?? "", 200, _field);
            if (GUI.Button(new Rect(x + w - p - 2 * 130 * s - gap, cy, 130 * s, row), "Wklej", _btn)) _urlField = GUIUtility.systemCopyBuffer?.Trim() ?? "";
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (TestUrl != null) { _urlField = TestUrl; TestUrl = null; enter = true; }
            if (GUI.Button(new Rect(x + w - p - 130 * s, cy, 130 * s, row), "Dodaj", _btn) || enter)
            {
                string u = (_urlField ?? "").Trim();
                if (MatchManager.IsYoutubeUrl(u))
                {
                    me.RequestRadioRpc(RadioOp.Add, new Unity.Collections.FixedString128Bytes(u));
                    _urlField = "";
                }
                else { _note = "To nie jest link z YouTube."; _noteUntil = Time.unscaledTime + 3f; }
            }
            cy += row + gap;
            if (GUI.Button(new Rect(x + w - p - 200 * s, cy, 200 * s, row), "Zamknij (Esc)", _btn)) ClosePanel();
            GUI.Label(new Rect(bx, cy + 6 * s, w - 260 * s, row), RadioTools.YtDlp == null || RadioTools.Ffmpeg == null ? "Brak yt-dlp / ffmpeg (folder Tools obok gry)" : "", _small);
        }
    }
}
