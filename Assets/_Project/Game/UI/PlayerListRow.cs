using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// One row of <see cref="PlayerListHud"/>: role marker, state column, nick (+ "(ty)"), voice bars on the right.
    /// Dead players are greyed out and struck through; the speaking player gets a green tint, a bar on the left and
    /// bouncing level bars.
    /// </summary>
    public sealed class PlayerListRow
    {
        public const float Height = 32f;
        private const float StateX = 32f, StateW = 104f, NickX = 138f, VoiceW = 30f;

        public struct Data
        {
            public string Nick;
            public bool Me, Seeker, Dead, Pulse, Speaking;
            public string State;
            public Color StateColor, NickColor;
            public float Level; // 0..1 voice level (own mic), < 0 = animate
        }

        public readonly RectTransform Root;
        private readonly Image _bg, _speakBar, _marker, _strike;
        private readonly Text _state, _nick;
        private readonly RectTransform[] _bars = new RectTransform[3];
        private string _lastNick, _lastState;
        private bool _lastSeeker;
        private float _speakFx;

        public PlayerListRow(Transform parent)
        {
            Root = UiKit.Rect("Row", parent);
            _bg = UiKit.Image("Bg", Root, Color.clear).Rounded(true);
            _bg.rectTransform.Stretch();
            _bg.raycastTarget = false;

            _speakBar = UiKit.Image("Speak", Root, new Color(0.45f, 1f, 0.5f, 1f)).Rounded(true);
            _speakBar.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(1, 0), new Vector2(4, 22));

            _marker = UiKit.Image("Marker", Root, Color.white);
            _marker.raycastTarget = false;
            _marker.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18, 0), new Vector2(13, 13));

            _state = UiKit.Label("State", Root, "", 18, TextAnchor.MiddleLeft, UiKit.TextDim, FontStyle.Bold);
            _state.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(StateX, 0), new Vector2(StateW, 0));
            _state.horizontalOverflow = HorizontalWrapMode.Overflow;
            _state.AddOutline(1f, 0.6f);

            _nick = UiKit.Label("Nick", Root, "", 20, TextAnchor.MiddleLeft);
            _nick.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(NickX, 0), new Vector2(-NickX - VoiceW, 0));
            _nick.supportRichText = true;
            _nick.verticalOverflow = VerticalWrapMode.Truncate;
            _nick.AddOutline(1f, 0.6f);

            _strike = UiKit.Image("Strike", _nick.transform, new Color(0.75f, 0.75f, 0.75f, 0.9f));
            _strike.raycastTarget = false;
            _strike.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(10, 2));

            for (int i = 0; i < 3; i++)
            {
                var b = UiKit.Image("VoiceBar" + i, Root, new Color(0.45f, 1f, 0.5f, 1f));
                b.raycastTarget = false;
                _bars[i] = b.rectTransform;
                _bars[i].Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24 + i * 7, 0), new Vector2(4, 6));
            }
        }

        public void SetActive(bool on)
        {
            if (Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on);
        }

        public void Set(in Data d, float time)
        {
            if (_lastNick != d.Nick || _lastSeeker != d.Seeker)
            {
                _lastNick = d.Nick;
                _lastSeeker = d.Seeker;
                _nick.text = d.Me ? d.Nick + " <color=#8f969e>(ty)</color>" : d.Nick;
                // Seeker = square, hider / lobby = round marker.
                _marker.sprite = d.Seeker ? UiStyle.RoundSmall : UiStyle.Circle;
                _marker.type = Image.Type.Simple;
                _marker.rectTransform.sizeDelta = d.Seeker ? new Vector2(14, 14) : new Vector2(13, 13);
            }
            if (_lastState != d.State)
            {
                _lastState = d.State;
                _state.text = d.State;
            }
            _state.color = d.StateColor;
            _nick.color = d.NickColor;

            var mc = d.StateColor;
            if (d.Pulse) mc.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 4f));
            _marker.color = mc;

            _strike.enabled = d.Dead;
            if (d.Dead)
            {
                float w = Mathf.Min(_nick.preferredWidth, _nick.rectTransform.rect.width);
                _strike.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, w), 2f);
            }

            // Speaking: fade the tint in / out so short gaps between words do not blink.
            _speakFx = Mathf.MoveTowards(_speakFx, d.Speaking ? 1f : 0f, Time.unscaledDeltaTime * (d.Speaking ? 10f : 3f));
            var bg = d.Me ? new Color(1f, 1f, 1f, 0.07f) : new Color(1f, 1f, 1f, 0f);
            _bg.color = Color.Lerp(bg, new Color(0.3f, 0.9f, 0.4f, 0.22f), _speakFx);
            _speakBar.color = new Color(0.45f, 1f, 0.5f, _speakFx);
            for (int i = 0; i < 3; i++)
            {
                float lv = d.Level >= 0f ? d.Level : 0.5f + 0.5f * Mathf.Sin(time * (9f + i * 3.3f) + i * 1.7f);
                float h = Mathf.Lerp(4f, 18f, Mathf.Clamp01(lv)) * _speakFx;
                _bars[i].sizeDelta = new Vector2(4f, Mathf.Max(0f, h));
                _bars[i].gameObject.SetActive(_speakFx > 0.01f);
            }
        }
    }
}
