using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Pose wheel (like an emote wheel) for a player sitting in the lobby. Hold K: the wheel opens in the middle of the
    /// screen, the camera stops turning and moving the mouse picks a slice by direction; release G = take the
    /// highlighted pose, left click = take it too, Esc / right click = cancel. A short tap of K leaves the wheel open
    /// (then a click or K again picks). One slice per pose of the seat (<see cref="SitPoseRules.Poses"/>, i.e.
    /// <see cref="SitPoseCatalog"/>), shown only by its look - no names: each slice holds a small picture of the own
    /// character (own skin) in that pose on the same seat, rendered once per pose when the wheel opens by a still
    /// thumbnail stage (<see cref="PosePreviewStage.CreateThumbs"/>, same posing code as the game). States are drawn,
    /// not written: current pose = green strip and green rim, unavailable = greyed out and darkened, new = small gold dot.
    /// In the middle a live preview of the own character shows the highlighted pose (<see cref="PosePreviewStage"/>).
    /// The pick goes to the server (<see cref="NetPlayer.RequestSitPose"/>).
    /// Standing, K opens the <see cref="EmoteWheel"/> instead; G is only the medkit.
    /// </summary>
    public sealed class PoseWheel : MonoBehaviour
    {
        private const float OuterR = 330f, InnerR = 172f, SliceIn = InnerR + 8f, Gap = 8f;
        private const float TravelPx = 115f;   // mouse travel (1080p pixels) from the middle to the rim of the virtual cursor
        private const float DeadZone = 0.3f;
        private const float OpenTime = 0.16f, CloseTime = 0.12f, TapTime = 0.22f;
        private const float ThumbD = 124f;     // slice picture diameter (fits 8 slices)
        private const int ThumbsPerFrame = 2;  // pictures rendered per frame after opening

        private static PoseWheel _inst;
        private static int _closedFrame = -10;

        // Test hooks (scripted tests drive the same code paths as the keyboard / mouse).
        /// <summary>Acts as K held down.</summary>
        public static bool TestHold;
        /// <summary>&gt;= 0: highlights this slice (as if the mouse pointed at it). -1 = the mouse decides.</summary>
        public static int TestHighlight = -1;
        /// <summary>Acts as one left click.</summary>
        public static bool TestClick;

        /// <summary>True while the wheel is open (also while it fades out): look and flashlight input are off.</summary>
        public static bool IsOpen => _inst != null && _inst._open;

        /// <summary>Esc this frame belongs to the wheel (it was open or just closed by it): no Esc menu.</summary>
        public static bool ConsumesEscape => IsOpen || _closedFrame >= Time.frameCount - 1;

        /// <summary>Slices on the open wheel (tests).</summary>
        public static int SliceCount => _inst != null ? _inst._slices.Count : 0;

        /// <summary>Highlighted slice (-1 = none, tests).</summary>
        public static int Highlighted => _inst != null ? _inst._hl : -1;

        private sealed class Slice
        {
            public SitPoseInfo Info;
            public bool Available, Current;
            public RectTransform Rt;
            public RingSector Fill, Glow, Edge, Mark, ThumbRim;
            public RawImage Thumb;
            public Image Grey, NewDot;
            public RenderTexture Picture;
            public float Scale = 1f, Hot;
        }

        private RectTransform _root, _ring;
        private CanvasGroup _group;
        private Image _dim;
        private RawImage _preview;
        private RingSector _pointer;
        private Text _hint;
        private readonly List<Slice> _slices = new();
        private PosePreviewStage _stage, _thumbStage;
        private int _thumbNext, _thumbPx = 128;

        private bool _open, _toggle;
        private float _anim, _openedAt;
        private Vector2 _cursor;
        private int _hl = -1, _seat = -1, _relock;
        private byte _shownPose = 255;
        private bool _testHeldBefore;

        public static PoseWheel Create(Transform canvasRoot)
        {
            var rt = UiKit.Rect("PoseWheel", canvasRoot);
            rt.Stretch();
            var c = rt.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 30;
            var w = rt.gameObject.AddComponent<PoseWheel>();
            w._root = rt;
            w._group = rt.gameObject.AddComponent<CanvasGroup>();
            w._group.alpha = 0f;
            w._group.blocksRaycasts = false;
            w._group.interactable = false;
            w.Build();
            rt.gameObject.SetActive(false);
            _inst = w;
            return w;
        }

        // ================================================================ build

        private void Build()
        {
            _dim = UiKit.Image("Dim", _root, new Color(0f, 0f, 0f, 0.28f));
            _dim.rectTransform.Stretch();
            _dim.raycastTarget = false;

            _ring = UiKit.Rect("Ring", _root);
            _ring.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (OuterR * 2f + 60f));

            // Dark translucent ring with a faint rim on both edges.
            RingSector.Create("Back", _ring, InnerR - 14f, OuterR + 14f, 0f, 360f, 0f, new Color(0.035f, 0.04f, 0.045f, 0.62f), 2f);
            RingSector.Create("RimOut", _ring, OuterR + 13f, OuterR + 14.5f, 0f, 360f, 0f, new Color(1f, 1f, 1f, 0.1f));
            RingSector.Create("RimIn", _ring, InnerR - 14.5f, InnerR - 13f, 0f, 360f, 0f, new Color(1f, 1f, 1f, 0.1f));

            // Preview disc (masked render texture) with a thin border.
            float pr = InnerR - 20f;
            var disc = UiKit.Image("PreviewDisc", _ring, new Color(0.075f, 0.082f, 0.095f, 1f));
            disc.sprite = UiStyle.Circle;
            disc.raycastTarget = false;
            disc.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * pr * 2f);
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _preview = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            _preview.transform.SetParent(disc.transform, false);
            _preview.rectTransform.Stretch();
            _preview.raycastTarget = false;
            RingSector.Create("PreviewRim", _ring, pr - 1f, pr + 1.5f, 0f, 360f, 0f, new Color(1f, 1f, 1f, 0.16f));

            _pointer = RingSector.Create("Pointer", _ring, InnerR - 11f, InnerR - 4f, 0f, 30f, 0f, UiKit.ButtonHover, 1.5f);

            _hint = UiKit.Label("Hint", _root, "", 19, TextAnchor.MiddleCenter, UiKit.TextDim);
            _hint.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -OuterR - 30f), new Vector2(1000f, 30f));
            _hint.AddOutline(1f, 0.6f);
        }

        private void BuildSlices(NetPlayer me)
        {
            ReleaseThumbs();
            foreach (var s in _slices) if (s.Rt != null) Destroy(s.Rt.gameObject);
            _slices.Clear();
            var seat = LobbySeat.Get(me.Seat.Value);
            var poses = SitPoseRules.Poses(seat);
            int n = poses.Count;
            if (n == 0) return;
            float step = 360f / n;
            float mid = (SliceIn + OuterR) * 0.5f;
            var dark = new Color(0.075f, 0.082f, 0.095f, 1f);
            for (int i = 0; i < n; i++)
            {
                float a = i * step;
                var s = new Slice { Info = poses[i] };
                s.Rt = UiKit.Rect("Slice" + i, _ring);
                s.Rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (OuterR * 2f + 60f));
                s.Glow = RingSector.Create("Glow", s.Rt, SliceIn - 4f, OuterR + 6f, a, step, Gap, new Color(UiKit.ButtonHover.r, UiKit.ButtonHover.g, UiKit.ButtonHover.b, 0f), 16f);
                s.Fill = RingSector.Create("Fill", s.Rt, SliceIn, OuterR, a, step, Gap, Color.white);
                s.Edge = RingSector.Create("Edge", s.Rt, OuterR - 5f, OuterR, a, step, Gap, UiKit.ButtonHover);
                s.Mark = RingSector.Create("Current", s.Rt, SliceIn, SliceIn + 5f, a, step, Gap, UiKit.ButtonHover);

                // Picture of the pose: a round window (masked render texture) in the middle of the slice.
                Vector2 c = RingSector.Dir(a) * mid;
                var holder = UiKit.Image("Thumb", s.Rt, dark);
                holder.sprite = UiStyle.Circle;
                holder.raycastTarget = false;
                holder.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), c, Vector2.one * ThumbD);
                holder.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                s.Thumb = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
                s.Thumb.transform.SetParent(holder.transform, false);
                s.Thumb.rectTransform.Stretch();
                s.Thumb.raycastTarget = false;
                s.Thumb.enabled = false;
                s.Grey = UiKit.Image("Grey", holder.transform, new Color(0.17f, 0.18f, 0.2f, 0f));
                s.Grey.rectTransform.Stretch();
                s.Grey.raycastTarget = false;
                s.ThumbRim = RingSector.Create("Rim", s.Rt, ThumbD * 0.5f - 1.5f, ThumbD * 0.5f + 1f, 0f, 360f, 0f, new Color(1f, 1f, 1f, 0.14f));
                s.ThumbRim.rectTransform.anchoredPosition = c;

                // New pose: small gold dot at the picture's upper outer edge.
                s.NewDot = UiKit.Image("New", s.Rt, new Color(1f, 0.78f, 0.25f, 1f));
                s.NewDot.sprite = UiStyle.Circle;
                s.NewDot.raycastTarget = false;
                s.NewDot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), c + new Vector2(0.36f, 0.36f) * ThumbD, Vector2.one * 15f);
                s.NewDot.AddDrop(1.5f, 0.6f);
                s.NewDot.gameObject.SetActive(s.Info.IsNew);
                _slices.Add(s);
            }
        }

        /// <summary>Renders the next pose pictures (a couple per frame after opening), then drops the thumbnail stage.</summary>
        private void StepThumbs()
        {
            if (_thumbStage == null) return;
            for (int k = 0; k < ThumbsPerFrame && _thumbNext < _slices.Count; k++, _thumbNext++)
            {
                var s = _slices[_thumbNext];
                if (s.Picture == null)
                {
                    s.Picture = new RenderTexture(_thumbPx, _thumbPx, 24, RenderTextureFormat.ARGB32) { name = "PoseThumb", antiAliasing = 4 };
                    s.Picture.Create();
                }
                _thumbStage.RenderPose(s.Info.Id, s.Picture);
                s.Thumb.texture = s.Picture;
                s.Thumb.enabled = true;
            }
            if (_thumbNext >= _slices.Count)
            {
                Destroy(_thumbStage.gameObject);
                _thumbStage = null;
            }
        }

        private void ReleaseThumbs()
        {
            if (_thumbStage != null) Destroy(_thumbStage.gameObject);
            _thumbStage = null;
            foreach (var s in _slices)
            {
                if (s.Thumb != null) { s.Thumb.texture = null; s.Thumb.enabled = false; }
                if (s.Picture != null)
                {
                    s.Picture.Release();
                    Destroy(s.Picture);
                    s.Picture = null;
                }
            }
        }

        // ================================================================ open / close

        private static bool CanUse(NetPlayer me)
        {
            if (me == null || !me.IsSpawned || me.Seat.Value < 0 || me.Life.Value != LifeState.Alive) return false;
            var mm = MatchManager.Instance;
            if (mm == null || mm.Phase.Value != MatchPhase.Lobby) return false;
            if (LocalPlayerController.MenuOpen || ConfirmDialog.ConsumesEscape || KillCam.Active) return false;
            return me.InputReader == null || me.InputReader.InputEnabled;
        }

        private void Open(NetPlayer me)
        {
            _seat = me.Seat.Value;
            BuildSlices(me);
            if (_slices.Count == 0) return;
            _open = true;
            _toggle = false;
            _openedAt = Time.unscaledTime;
            _cursor = Vector2.zero;
            _hl = -1;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            RefreshStates(me);
            foreach (var s in _slices) { s.Scale = 1f; s.Hot = 0f; }

            if (_stage != null) Destroy(_stage.gameObject);
            var canvas = GetComponentInParent<Canvas>();
            float k = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            int size = Mathf.Clamp(Mathf.RoundToInt((InnerR - 20f) * 2f * k / 32f) * 32, 256, 1024);
            _stage = PosePreviewStage.Create(LobbySeat.Get(_seat), me.Character, me.SitPoseVar.Value, size);
            _shownPose = me.SitPoseVar.Value;
            _preview.texture = _stage.Texture;
            _thumbPx = Mathf.Clamp(Mathf.RoundToInt(ThumbD * k / 16f) * 16, 96, 256);
            _thumbStage = PosePreviewStage.CreateThumbs(LobbySeat.Get(_seat), me.Character, me.SitPoseVar.Value);
            _thumbNext = 0;
            GameAudio.Play2D(GameSfx.UiHover, 0.35f);
            Debug.Log($"[PoseWheel] open: seat {_seat}, {_slices.Count} poses, current {me.SitPoseVar.Value}");
        }

        /// <summary>Starts closing (fade out); the preview goes away once the wheel is gone.</summary>
        private void Close()
        {
            if (!_open) return;
            _open = false;
            _toggle = false;
            _closedFrame = Time.frameCount;
            TestHighlight = -1;
        }

        /// <summary>Takes the pose of slice <paramref name="index"/>; an unavailable one only buzzes (and closes when <paramref name="closeAnyway"/>).</summary>
        private void Pick(NetPlayer me, int index, bool closeAnyway)
        {
            var s = index >= 0 && index < _slices.Count ? _slices[index] : null;
            if (s == null) { Close(); return; }
            if (!s.Available)
            {
                GameAudio.Play2D(GameSfx.UiError, 0.5f);
                if (closeAnyway) Close();
                return;
            }
            GameAudio.Play2D(GameSfx.UiClick, 0.7f);
            if (!s.Current && !me.RequestSitPose(s.Info.Id)) GameAudio.Play2D(GameSfx.UiError, 0.5f);
            Debug.Log($"[PoseWheel] picked {s.Info.Id} '{s.Info.Name}'");
            Close();
        }

        private void DestroyStage()
        {
            ReleaseThumbs();
            if (_stage != null) Destroy(_stage.gameObject);
            _stage = null;
            if (_preview != null) _preview.texture = null;
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            DestroyStage();
        }

        private void OnDestroy()
        {
            DestroyStage();
            if (_inst == this) _inst = null;
        }

        private void OnSceneChanged(Scene a, Scene b)
        {
            _open = false;
            _anim = 0f;
            DestroyStage();
            _root.gameObject.SetActive(false);
        }

        // ================================================================ frame

        /// <summary>
        /// Runs on the HUD object (always active), so it also opens the wheel; the wheel's own object is only active
        /// while it is shown.
        /// </summary>
        public static void Tick()
        {
            if (_inst != null) _inst.Step();
        }

        private void Step()
        {
            var me = NetPlayer.Local;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool can = CanUse(me);
            bool testPress = TestHold && !_testHeldBefore;
            _testHeldBefore = TestHold;

            if (_relock > 0 && --_relock == 0 && me != null && me.InputReader != null && me.InputReader.ManageCursor)
                me.InputReader.SetCursorLocked(true);

            if (!_open)
            {
                if (can && ((kb != null && kb.kKey.wasPressedThisFrame) || testPress)) Open(me);
            }
            else if (!can || me.Seat.Value != _seat)
            {
                Close();
            }
            else
            {
                StepOpen(me, kb, mouse);
            }

            // Fade / scale in and out.
            float target = _open ? 1f : 0f;
            _anim = Mathf.MoveTowards(_anim, target, Time.unscaledDeltaTime / (_open ? OpenTime : CloseTime));
            if (!_open && _anim <= 0f)
            {
                if (_root.gameObject.activeSelf)
                {
                    _root.gameObject.SetActive(false);
                    DestroyStage();
                }
                return;
            }
            float e = _open ? EaseOutBack(_anim) : _anim * _anim;
            _group.alpha = Mathf.Clamp01(_anim * 1.4f);
            _ring.localScale = Vector3.one * Mathf.LerpUnclamped(0.84f, 1f, e);
            _dim.color = new Color(0f, 0f, 0f, 0.28f * _anim);
            StepThumbs();
            AnimateSlices();
        }

        private void StepOpen(NetPlayer me, Keyboard kb, Mouse mouse)
        {
            RefreshStates(me);

            // Virtual cursor: mouse movement (the camera does not turn meanwhile, see LocalPlayerController).
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue() * (1080f / Mathf.Max(1, Screen.height));
                _cursor = Vector2.ClampMagnitude(_cursor + d / TravelPx, 1f);
            }
            int n = _slices.Count;
            int hl = -1;
            if (TestHighlight >= 0 && TestHighlight < n)
            {
                _cursor = RingSector.Dir(TestHighlight * 360f / n);
                hl = TestHighlight;
            }
            else if (_cursor.magnitude > DeadZone)
            {
                float ang = Mathf.Atan2(_cursor.x, _cursor.y) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                hl = Mathf.RoundToInt(ang / (360f / n)) % n;
            }
            if (hl != _hl)
            {
                _hl = hl;
                if (hl >= 0) GameAudio.Play2D(GameSfx.UiHover, _slices[hl].Available ? 0.55f : 0.3f);
            }

            // Preview: the highlighted pose (an unavailable one too: you can see what it would be), else the current one.
            byte show = _hl >= 0 ? _slices[_hl].Info.Id : me.SitPoseVar.Value;
            if (show != _shownPose && _stage != null)
            {
                _shownPose = show;
                _stage.ShowPose(show);
            }
            UpdateTexts();

            // Pointer arc follows the cursor direction.
            float m = _cursor.magnitude;
            _pointer.CentreAngle = Mathf.Atan2(_cursor.x, _cursor.y) * Mathf.Rad2Deg;
            var pc = UiKit.ButtonHover;
            pc.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, DeadZone + 0.05f, m));
            _pointer.color = pc;

            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
            bool rmb = mouse != null && mouse.rightButton.wasPressedThisFrame;
            bool lmb = (mouse != null && mouse.leftButton.wasPressedThisFrame) || TestClick;
            TestClick = false;
            bool gHeld = (kb != null && kb.kKey.isPressed) || TestHold;
            bool gAgain = kb != null && kb.kKey.wasPressedThisFrame && _toggle;

            if (esc || rmb)
            {
                Close();
                if (esc) _relock = 2; // Esc also frees the cursor (PlayerInputReader): take it back
                GameAudio.Play2D(GameSfx.UiHover, 0.3f);
            }
            else if (lmb || gAgain)
            {
                if (_hl >= 0) Pick(me, _hl, false);
                else if (gAgain) Close();
            }
            else if (!gHeld && !_toggle)
            {
                // Released: a short tap with nothing picked keeps the wheel open (click / G to pick).
                if (_hl < 0 && Time.unscaledTime - _openedAt < TapTime) _toggle = true;
                else if (_hl >= 0) Pick(me, _hl, true);
                else Close();
            }
        }

        private void RefreshStates(NetPlayer me)
        {
            var seat = LobbySeat.Get(me.Seat.Value);
            byte cur = me.SitPoseVar.Value;
            foreach (var s in _slices)
            {
                s.Current = s.Info.Id == cur;
                s.Available = s.Current || SitPoseRules.Allowed(seat, s.Info.Id, me);
            }
        }

        private void UpdateTexts()
        {
            _hint.text = _toggle
                ? "mysz - wskaż   |   LPM / K - wybierz   |   PPM / Esc - anuluj"
                : "mysz - wskaż   |   puść K / LPM - wybierz   |   PPM / Esc - anuluj";
        }

        private void AnimateSlices()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _slices.Count; i++)
            {
                var s = _slices[i];
                bool hot = i == _hl && _open;
                s.Hot = Mathf.MoveTowards(s.Hot, hot ? 1f : 0f, dt / 0.1f);
                float h = Mathf.SmoothStep(0f, 1f, s.Hot);
                float target = hot ? (s.Available ? 1.065f : 1.025f) : 1f;
                s.Scale = Mathf.Lerp(s.Scale, target, 1f - Mathf.Exp(-22f * dt));
                s.Rt.localScale = Vector3.one * s.Scale;

                Color fill = s.Available
                    ? Color.Lerp(new Color(0.105f, 0.115f, 0.135f, 0.8f), new Color(0.2f, 0.225f, 0.255f, 0.94f), h)
                    : Color.Lerp(new Color(0.07f, 0.072f, 0.08f, 0.55f), new Color(0.11f, 0.11f, 0.12f, 0.7f), h);
                s.Fill.color = fill;
                var acc = UiKit.ButtonHover;
                s.Glow.color = new Color(acc.r, acc.g, acc.b, s.Available ? 0.2f * h : 0f);
                s.Edge.color = new Color(acc.r, acc.g, acc.b, s.Available ? h : 0f);
                s.Mark.color = new Color(acc.r, acc.g, acc.b, s.Current ? 0.95f : 0f);

                // Picture: slightly dimmed at rest, full brightness under the cursor; unavailable = dark and washed out.
                float lum = s.Available ? Mathf.Lerp(0.84f, 1f, h) : 0.5f;
                s.Thumb.color = new Color(lum, lum, lum, 1f);
                s.Grey.color = new Color(0.17f, 0.18f, 0.2f, s.Available ? 0f : 0.55f);
                s.ThumbRim.color = s.Current ? new Color(acc.r, acc.g, acc.b, 0.95f)
                    : s.Available ? new Color(1f, 1f, 1f, Mathf.Lerp(0.14f, 0.4f, h)) : new Color(1f, 1f, 1f, 0.06f);
                s.ThumbRim.Outer = ThumbD * 0.5f + (s.Current ? 3f : 1f);
                s.NewDot.color = new Color(1f, 0.78f, 0.25f, s.Available ? 1f : 0.45f);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
