using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// One row of <see cref="PlayerListHud"/>: role marker, state column, nick (+ "(ty)"), voice bars on the right.
    /// Dead players are greyed out and struck through; the speaking player gets a green tint, a bar on the left and
    /// bouncing level bars. A downed player gets a thin bleed-out bar with the time left under "powalony" (same column,
    /// the row keeps its height): it empties towards zero, turns deeper red near the end and pulses in the last seconds.
    /// </summary>
    public sealed class PlayerListRow
    {
        public const float Height = 32f;
        private const float StateX = 32f, StateW = 104f, NickX = 138f, VoiceW = 30f;
        // Bleed-out bar under the state text: track width, bar height, time label width.
        private const float BleedBarW = 62f, BleedBarH = 6f, BleedTimeW = 38f, BleedY = -8.5f, StateLiftY = 6f;
        private static readonly Color BleedFull = new Color(1f, 0.52f, 0.42f), BleedEnd = new Color(0.93f, 0.07f, 0.05f),
            BleedFlash = new Color(1f, 0.62f, 0.56f), BleedTrack = new Color(0.22f, 0.03f, 0.03f, 0.72f);

        public struct Data
        {
            public string Nick;
            public bool Me, Seeker, Dead, Pulse, Speaking;
            public string State;
            public Color StateColor, NickColor;
            public float Level; // 0..1 voice level (own mic), < 0 = animate
            public float Bleed; // downed: part of the bleed-out time left (1 = just went down, 0 = dies), < 0 = no bar
            public float BleedLeft; // downed: seconds left
            public float BleedTotal; // full bleed-out time of this round (seconds)
            public int Fuses; // fuses in the inventory (0 = no icon, 1 = icon, 2+ = icon + count)
            public int Ping; // lobby: ping to the host in ms, shown small right after the nick (< 0 = none)
        }

        public readonly RectTransform Root;
        private readonly Image _bg, _speakBar, _marker, _strike;
        private readonly Text _state, _nick;
        private readonly RectTransform[] _bars = new RectTransform[3];
        private readonly RectTransform _bleed, _bleedFillRt;
        private readonly Image _bleedTrack, _bleedFill;
        private readonly Text _bleedTime;
        private readonly Image _fuseIcon;
        private readonly Text _fuseCount;
        private int _lastFuses = -1;
        private readonly Text _ping;
        private int _lastPing = int.MinValue;
        private float _pingW;
        private const float PingW = 50f;
        private string _lastNick, _lastState;
        private bool _lastSeeker, _bleedOn;
        private int _lastSecs = -1;
        private float _speakFx, _bleedPhase;

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

            // Bleed-out: dark red track + fill (pill shaped: small corners at 6 px height), time on its right.
            _bleed = UiKit.Rect("Bleed", Root);
            _bleed.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(StateX + 1f, BleedY), new Vector2(StateW - 1f, 14f));
            _bleedTrack = UiKit.Image("Track", _bleed, BleedTrack).Rounded(true);
            _bleedTrack.pixelsPerUnitMultiplier = 2.7f;
            _bleedTrack.raycastTarget = false;
            _bleedTrack.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(BleedBarW, BleedBarH));
            _bleedFill = UiKit.Image("Fill", _bleedTrack.rectTransform, BleedFull).Rounded(true);
            _bleedFill.pixelsPerUnitMultiplier = 2.7f;
            _bleedFill.raycastTarget = false;
            _bleedFillRt = _bleedFill.rectTransform;
            _bleedFillRt.anchorMin = Vector2.zero;
            _bleedFillRt.anchorMax = Vector2.one;
            _bleedFillRt.pivot = new Vector2(0f, 0.5f);
            _bleedFillRt.offsetMin = _bleedFillRt.offsetMax = Vector2.zero;
            _bleedTime = UiKit.Label("Time", _bleed, "", 13, TextAnchor.MiddleLeft, BleedFull, FontStyle.Bold);
            _bleedTime.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(BleedBarW + 5f, 0f), new Vector2(BleedTimeW, 14f));
            _bleedTime.horizontalOverflow = HorizontalWrapMode.Overflow;
            _bleedTime.AddOutline(1f, 0.6f);
            _bleed.gameObject.SetActive(false);

            // Fuses carried: the amber fuse of the minimap, with the number when 2 or more (right, before the voice bars).
            _fuseIcon = UiKit.Image("Fuse", Root, Color.white);
            _fuseIcon.raycastTarget = false;
            _fuseIcon.preserveAspect = true;
            _fuseIcon.sprite = Minimap.FuseSprite();
            _fuseIcon.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-VoiceW - 26f, 0), new Vector2(20, 20));
            _fuseCount = UiKit.Label("FuseCount", Root, "", 16, TextAnchor.MiddleLeft, new Color(1f, 0.78f, 0.3f), FontStyle.Bold);
            _fuseCount.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(-VoiceW - 15f, -1f), new Vector2(16, 20));
            _fuseCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            _fuseCount.AddOutline(1f, 0.7f);
            _fuseIcon.gameObject.SetActive(false);
            _fuseCount.gameObject.SetActive(false);

            // Ping (lobby): small coloured "23 ms" just after the nick.
            _ping = UiKit.Label("Ping", Root, "", 14, TextAnchor.MiddleLeft, new Color(0.55f, 0.9f, 0.55f), FontStyle.Bold);
            _ping.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(NickX, 0f), new Vector2(PingW, 20f));
            _ping.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ping.raycastTarget = false;
            _ping.AddOutline(1f, 0.6f);
            _ping.gameObject.SetActive(false);

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

            UpdateBleed(d);
            UpdateFuses(d.Fuses);
            UpdatePing(d.Ping);

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

        /// <summary>Fuse icon (+ count from 2) on the right; the nick gives up the room while it shows.</summary>
        private void UpdateFuses(int n)
        {
            if (n == _lastFuses) return;
            _lastFuses = n;
            _fuseIcon.gameObject.SetActive(n > 0);
            _fuseCount.gameObject.SetActive(n > 1);
            _fuseCount.text = n > 1 ? n.ToString() : "";
            ResizeNick();
        }

        private void ResizeNick() =>
            _nick.rectTransform.sizeDelta = new Vector2(-NickX - VoiceW - (_lastFuses > 0 ? (_lastFuses > 1 ? 44f : 32f) : 0f) - _pingW, 0);

        /// <summary>Lobby ping after the nick: green below 80 ms, yellow below 150 ms, red above.</summary>
        private void UpdatePing(int ms)
        {
            bool on = ms >= 0;
            if (ms != _lastPing)
            {
                _lastPing = ms;
                if (_ping.gameObject.activeSelf != on) _ping.gameObject.SetActive(on);
                float w = on ? PingW : 0f;
                if (w != _pingW) { _pingW = w; ResizeNick(); }
                if (on)
                {
                    _ping.text = ms + " ms";
                    _ping.color = ms < 80 ? new Color(0.5f, 0.92f, 0.5f) : ms < 150 ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.45f, 0.4f);
                }
            }
            if (!on) return;
            // Right after the visible nick text (it may be cut when the row is narrow).
            float nickW = Mathf.Min(_nick.preferredWidth, Mathf.Max(0f, _nick.rectTransform.rect.width));
            var pos = new Vector2(NickX + nickW + 6f, 0f);
            if (_ping.rectTransform.anchoredPosition != pos) _ping.rectTransform.anchoredPosition = pos;
        }

        /// <summary>Bleed-out bar of a downed player (hidden otherwise); the state text moves up above it.</summary>
        private void UpdateBleed(in Data d)
        {
            bool on = d.Bleed >= 0f;
            if (on != _bleedOn)
            {
                _bleedOn = on;
                _bleed.gameObject.SetActive(on);
                _state.rectTransform.anchoredPosition = new Vector2(StateX, on ? StateLiftY : 0f);
                _state.fontSize = on ? 16 : 18;
                _lastSecs = -1;
            }
            if (!on) return;

            float f = Mathf.Clamp01(d.Bleed);
            _bleedFillRt.anchorMax = new Vector2(f, 1f);
            _bleedFill.enabled = f > 0.002f;

            // Coral at the start, deep red towards the end (most of the change in the last part).
            var col = Color.Lerp(BleedFull, BleedEnd, Mathf.Pow(1f - f, 1.6f));
            // Last seconds (10 s, at most a quarter of the time): a light pulse, a little faster at the very end.
            float window = Mathf.Min(10f, Mathf.Max(3f, d.BleedTotal * 0.25f));
            float pulse = 0f;
            if (d.BleedLeft > 0f && d.BleedLeft <= window)
            {
                float k = 1f - d.BleedLeft / window;
                _bleedPhase = Mathf.Repeat(_bleedPhase + Time.unscaledDeltaTime * Mathf.Lerp(1.4f, 2.6f, k), 1f);
                pulse = (0.5f - 0.5f * Mathf.Cos(_bleedPhase * Mathf.PI * 2f)) * Mathf.Lerp(0.45f, 0.8f, k);
            }
            else _bleedPhase = 0f;
            _bleedFill.color = Color.Lerp(col, BleedFlash, pulse * 0.55f);
            var track = BleedTrack;
            track.r += 0.18f * pulse;
            _bleedTrack.color = track;
            _bleedTime.color = Color.Lerp(col, BleedFlash, pulse * 0.4f);

            int secs = Mathf.CeilToInt(Mathf.Max(0f, d.BleedLeft));
            if (secs != _lastSecs)
            {
                _lastSecs = secs;
                _bleedTime.text = UiKit.FormatTime(secs);
            }
        }
    }
}
