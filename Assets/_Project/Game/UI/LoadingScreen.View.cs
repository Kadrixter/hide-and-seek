using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Look of the <see cref="LoadingScreen"/>: the procedural garage (<see cref="LoadingArt"/>) with a flickering ceiling
    /// tube over it (the whole level dims with it), vignette, the game title in the middle-top like the main menu,
    /// bottom left a spinning orange hazard beacon + the status line + a detail line + a thin progress bar (real load
    /// progress, a sliding segment while connecting), a rotating tip and bottom right "Anuluj". Everything animates on
    /// unscaled time; a frame frozen by a scene build still looks finished (tube on, beacon lit, bar filled so far).
    /// </summary>
    public sealed partial class LoadingScreen
    {
        private const float TipEvery = 6.5f, BarWidth = 680f;

        private static readonly string[] Tips =
        {
            "Chowający się wygrywają, gdy przetrwają do końca czasu - albo uciekną bramą WYJŚCIE.",
            "Bramę WYJŚCIE otworzą bezpieczniki - znajdź je na parkingu i włóż do pasujących skrzynek.",
            "Szukający przez pierwsze chwile nic nie widzi. Wykorzystaj ten czas na dobrą kryjówkę.",
            "Pod maskami niektórych aut są akumulatory. Podłączony w pokoju ochrony zasila kamery.",
            "Zamka maski nie otworzysz gołymi rękami - urwij kawałek drutu z siatki ogrodzenia albo klatki.",
            "Piła łańcuchowa pali paliwo. Z kanistrem w ekwipunku zatankujesz ją klawiszem R.",
            "Ctrl - kucanie, Z - czołganie. Nisko zmieścisz się tam, gdzie szukający nie zajrzy.",
            "Latarka (T) pomaga w ciemności, ale zdradza, gdzie jesteś.",
            "X - rzucasz kurzem w oczy szukającego. Chwila ślepoty to szansa na ucieczkę.",
            "Powalony? Przytrzymaj G, żeby użyć apteczki - albo czekaj, aż ktoś cię podniesie.",
            "W lobby możesz pojeździć gokartem po torze albo zagrać w szachy, kółko i krzyżyk czy bilard.",
            "Przytrzymaj K, żeby wybrać emotkę. Kółkiem myszy przełączasz solo / wspólne.",
            "Grasz na padzie? PS4 i PS5 działają - przyciski ustawisz w Ustawienia > Pad.",
            "Zginąłeś? Trafiasz do lobby i oglądasz resztę rundy na telewizorze.",
        };

        private Canvas _canvas;
        private CanvasGroup _group;
        private Image _bg, _tube, _tubeGlow, _pool, _dim, _dome;
        private RectTransform _beam, _barFill, _barSeg, _tipBox;
        private CanvasGroup _tipGroup;
        private Text _statusLabel, _detailLabel, _tipLabel, _cancelHint;
        private Button _cancel;
        private float _bar;
        private int _tip = -1;
        private float _tipAt, _nextBurst = -1f, _burstEnd;

        private void EnsureView()
        {
            if (_canvas != null) return;
            _canvas = UiKit.CreateCanvas("LoadingCanvas", SortOrder, transform);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            var root = _canvas.transform;

            _bg = UiKit.Image("Bg", root, new Color(0.008f, 0.01f, 0.013f, 1f));
            _bg.rectTransform.Stretch();
            _bg.raycastTarget = !_automated; // blocks clicks to everything below

            // The garage, covering the screen at any aspect (cropped, never stretched).
            var art = UiKit.Rect("Art", root);
            art.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080));
            var raw = art.gameObject.AddComponent<RawImage>();
            raw.texture = LoadingArt.Garage;
            raw.raycastTarget = false;
            var fit = art.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = LoadingArt.W / (float)LoadingArt.H;

            // The nearest ceiling tube (flickers) with its glow and the pool of light on the floor below it.
            float tv = LoadingArt.NearTubeV, tw = LoadingArt.NearTubeHalfU * 2f * 1920f;
            _pool = Decal("Pool", art, LoadingArt.Glow, new Vector2(0.5f, LoadingArt.NearPoolV), new Vector2(1700, 330));
            _tubeGlow = Decal("TubeGlow", art, LoadingArt.Glow, new Vector2(0.5f, tv), new Vector2(tw * 3.2f, 340));
            var housing = Decal("Housing", art, null, new Vector2(0.5f, tv), new Vector2(tw + 26f, 22f));
            housing.color = new Color(0.05f, 0.05f, 0.055f, 1f);
            _tube = Decal("Tube", art, null, new Vector2(0.5f, tv), new Vector2(tw, 11f));
            _dim = UiKit.Image("Dim", root, new Color(0f, 0f, 0f, 0f));
            _dim.rectTransform.Stretch();
            _dim.raycastTarget = false;

            MenuChrome.VignetteImage(root, 0.8f);
            var shade = UiKit.Image("BottomShade", root, new Color(0f, 0f, 0f, 0.88f));
            shade.sprite = LoadingArt.BottomShade;
            shade.raycastTarget = false;
            shade.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 460f));

            // Title (as in the main menu, centred).
            var title = Label("Title", root, UiFonts.Spaced("Hide and Seek"), 60, UiFonts.Bold, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.92f));
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1700f, 84f));
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            var line = UiKit.Image("TitleLine", root, UiKit.ButtonHover);
            line.raycastTarget = false;
            line.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -246f), new Vector2(56f, 3f));
            var sub = Label("Sub", root, "chowany w ciemnościach", 22, UiFonts.Medium, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.45f));
            sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(900f, 34f));

            // Hazard beacon: base, rotating beams, glowing dome.
            float x = MenuChrome.ColumnX;
            var beacon = UiKit.Rect("Beacon", root);
            beacon.Place(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(x + 16f, 192f), new Vector2(40f, 40f));
            var halo = Decal("Halo", beacon, LoadingArt.Glow, new Vector2(0.5f, 0.5f), new Vector2(110f, 110f));
            halo.color = new Color(1f, 0.5f, 0.1f, 0.35f);
            var beam = Decal("Beam", beacon, LoadingArt.Beam, new Vector2(0.5f, 0.5f), new Vector2(300f, 300f));
            beam.color = new Color(1f, 0.55f, 0.12f, 0.55f);
            _beam = beam.rectTransform;
            var foot = Decal("Base", beacon, null, new Vector2(0.5f, 0.5f), new Vector2(30f, 7f));
            foot.color = new Color(0.12f, 0.12f, 0.13f, 1f);
            foot.rectTransform.anchoredPosition = new Vector2(0f, -11f);
            _dome = Decal("Dome", beacon, UiStyle.Circle, new Vector2(0.5f, 0.5f), new Vector2(20f, 20f));
            _dome.color = new Color(1f, 0.62f, 0.16f, 1f);

            _statusLabel = Label("Status", root, "", 34, UiFonts.SemiBold, TextAnchor.MiddleLeft, UiKit.Text);
            _statusLabel.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(x + 52f, 192f), new Vector2(1300f, 50f));
            _statusLabel.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            _detailLabel = Label("Detail", root, "", 20, UiFonts.Medium, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.5f));
            _detailLabel.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(x + 52f, 156f), new Vector2(1300f, 30f));

            var barBg = UiKit.Image("Bar", root, new Color(1f, 1f, 1f, 0.1f));
            barBg.raycastTarget = false;
            barBg.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(x, 124f), new Vector2(BarWidth, 4f));
            var fill = UiKit.Image("Fill", barBg.transform, UiKit.ButtonHover);
            fill.raycastTarget = false;
            _barFill = fill.rectTransform;
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
            var seg = UiKit.Image("Segment", barBg.transform, UiKit.ButtonHover);
            seg.raycastTarget = false;
            _barSeg = seg.rectTransform;
            _barSeg.Place(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(170f, 0f));
            barBg.gameObject.AddComponent<RectMask2D>(); // the sliding segment stays inside the bar

            // Tip: small spaced heading + the text, cross-faded every few seconds.
            _tipBox = UiKit.Rect("Tip", root);
            _tipBox.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, 34f), new Vector2(1400f, 64f));
            _tipGroup = _tipBox.gameObject.AddComponent<CanvasGroup>();
            var tipHead = Label("Head", _tipBox, UiFonts.Spaced("Wskazówka"), 15, UiFonts.Bold, TextAnchor.MiddleLeft, UiKit.ButtonHover);
            tipHead.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 22f));
            _tipLabel = Label("Text", _tipBox, "", 22, UiFonts.Medium, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.66f));
            _tipLabel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -26f), new Vector2(1400f, 34f));

            // "Anuluj" (name "Cancel": in the menu PadUi presses it on Circle).
            _cancel = UiKit.Button("Cancel", root, "Anuluj", Cancel, 34, UiKit.Danger).Align(TextAnchor.MiddleRight);
            _cancel.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-x, 192f), new Vector2(260f, 60f));
            _cancelHint = Label("CancelHint", root, "", 18, UiFonts.Medium, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.4f));
            _cancelHint.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-x, 154f), new Vector2(500f, 26f));
            _cancel.gameObject.SetActive(false);
            _cancelHint.gameObject.SetActive(false);

            _canvas.enabled = false;
        }

        private static Image Decal(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 size)
        {
            var img = UiKit.Image(name, parent, Color.white);
            img.sprite = sprite;
            img.raycastTarget = false;
            img.rectTransform.Place(anchor, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return img;
        }

        private static Text Label(string name, Transform parent, string text, int size, Font font, TextAnchor anchor, Color color)
        {
            var t = UiKit.Label(name, parent, text, size, anchor, color);
            t.font = font;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        private void PickTip(bool first)
        {
            _tip = first || _tip < 0 ? Random.Range(0, Tips.Length) : (_tip + 1) % Tips.Length;
            _tipAt = Time.unscaledTime;
            if (_tipLabel != null) _tipLabel.text = Tips[_tip];
            if (first) _nextBurst = -1f;
        }

        /// <summary>Brightness of the flickering tube 0..1: a steady hum with short flicker bursts every few seconds.</summary>
        private float TubeLevel(float t)
        {
            if (_nextBurst < 0f) _nextBurst = t + Random.Range(1.4f, 3f); // steady while the screen comes up
            if (t >= _nextBurst)
            {
                _burstEnd = t + Random.Range(0.12f, 0.55f);
                _nextBurst = _burstEnd + Random.Range(1.6f, 5f);
            }
            float hum = 0.93f + 0.07f * Mathf.PerlinNoise(t * 9f, 0.7f);
            if (t < _burstEnd) return Mathf.PerlinNoise(t * 37f, 4.2f) > 0.45f ? hum : 0.12f;
            return hum;
        }

        /// <summary>Pushes the state into the view (alpha, texts, bar, animation). Cheap: runs every frame while shown.</summary>
        private void ApplyView()
        {
            EnsureView();
            bool on = _stage != LoadingStage.None;
            if (_canvas.enabled != on) _canvas.enabled = on;
            if (!on) return;
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(_alpha);
            _group.alpha = a * a * (3f - 2f * a);
            _group.blocksRaycasts = !_automated && !_hiding;

            float lvl = TubeLevel(t);
            _tube.color = new Color(0.86f, 0.93f, 1f, 0.2f + 0.8f * lvl);
            _tubeGlow.color = new Color(0.75f, 0.85f, 1f, 0.24f * lvl);
            _pool.color = new Color(0.7f, 0.8f, 0.9f, 0.11f * lvl);
            _dim.color = new Color(0f, 0f, 0f, (1f - lvl) * 0.3f);

            _beam.localRotation = Quaternion.Euler(0f, 0f, -t * 240f);
            float flash = 0.75f + 0.25f * Mathf.Abs(Mathf.Cos(t * 240f * Mathf.Deg2Rad));
            _dome.color = new Color(1f, 0.45f + 0.2f * flash, 0.12f, 1f);

            string status = _status ?? "";
            if (_statusLabel.text != status) _statusLabel.text = status;
            bool connecting = _stage == LoadingStage.CreateServer || _stage == LoadingStage.Connect;
            string detail = _detail ?? "";
            if (!connecting && t - _shownAt > CancelAfter) detail = "To trwa dłużej niż zwykle…" + (detail.Length > 0 ? "   ·   " + detail : "");
            if (_detailLabel.text != detail) _detailLabel.text = detail;

            // Progress: never backwards; a sliding segment while there is nothing to measure (connecting).
            bool indeterminate = _progress < 0f && !_hiding;
            if (_barSeg.gameObject.activeSelf != indeterminate) _barSeg.gameObject.SetActive(indeterminate);
            if (indeterminate)
            {
                _barSeg.anchoredPosition = new Vector2((Mathf.Repeat(t * 0.55f, 1.3f) - 0.25f) * BarWidth, 0f);
                _bar = 0f;
            }
            else
            {
                float target = _hiding ? 1f : _progress;
                _bar = Mathf.Max(_bar, Mathf.MoveTowards(_bar, target, dt * (_hiding ? 4f : 0.8f)));
            }
            _barFill.anchorMax = new Vector2(indeterminate ? 0f : _bar, 1f);

            if (t - _tipAt >= TipEvery) PickTip(false);
            float tipT = t - _tipAt;
            _tipGroup.alpha = Mathf.Clamp01(tipT / 0.4f) * Mathf.Clamp01((TipEvery - tipT) / 0.4f);

            if (_cancel.gameObject.activeSelf != _cancelShown)
            {
                _cancel.gameObject.SetActive(_cancelShown);
                _cancelHint.gameObject.SetActive(_cancelShown);
            }
            if (_cancelShown) _cancelHint.text = PadUiInput.Connected ? "Esc   ·   kółko na padzie" : "Esc";
        }
    }
}
