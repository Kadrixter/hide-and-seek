using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Emote wheel under K (hold), for a player who is not sitting (sitting: K opens the <see cref="PoseWheel"/>).
    /// Same look and handling as the pose wheel: mouse = virtual cursor picking a slice by direction, release K / left
    /// click = play, right click / Esc = cancel, a short tap keeps it open. Two pages, "Solo" and "Wspólne", switched
    /// with the mouse wheel (tabs above the ring). Slices show only pictures of the own character doing the emote (pair
    /// emotes with the partner - the looked-at player's look when there is one), the middle a live loop of the
    /// highlighted one. Pair emotes need a player in front within <see cref="Emotes.AskRange"/>; otherwise the page is
    /// greyed out and a short line under the wheel says so.
    /// </summary>
    public sealed partial class EmoteWheel : MonoBehaviour
    {
        private const float OpenTime = 0.16f, CloseTime = 0.12f, TapTime = 0.22f;
        private const float TravelPx = 115f, DeadZone = 0.3f;

        private static EmoteWheel _inst;
        private static int _closedFrame = -10;
        private static int _page; // 0 solo, 1 pair (remembered between openings)

        // Test hooks.
        public static bool TestHold;
        public static int TestHighlight = -1;
        public static bool TestClick;
        public static int TestPage = -1;
        /// <summary>Acts as one right click (cancel).</summary>
        public static bool TestCancel;

        public static bool IsOpen => _inst != null && _inst._open;
        public static bool ConsumesEscape => IsOpen || _closedFrame >= Time.frameCount - 1;
        public static int SliceCount => _inst != null ? _inst._slices.Count : 0;
        public static int Page => _page;
        public static NetPlayer Target => _inst != null && _inst._hasTarget ? _inst._target : null;

        private bool _open, _toggle, _testHeldBefore;
        private float _anim, _openedAt, _pageAnim = 1f;
        private Vector2 _cursor;
        private int _hl = -1, _relock, _builtPage = -1;
        private EmoteId _shown = (EmoteId)255;
        private NetPlayer _target;
        private float _targetCheck;
        private bool _hasTarget;

        public static void Tick()
        {
            if (_inst != null) _inst.Step();
        }

        private static bool CanUse(NetPlayer me)
        {
            if (me == null || !me.IsSpawned || me.Seat.Value >= 0 || me.Life.Value != LifeState.Alive) return false;
            var mm = MatchManager.Instance;
            if (mm == null || mm.Phase.Value == MatchPhase.Loading || mm.Phase.Value == MatchPhase.Race || mm.RaceMode) return false;
            if (LocalPlayerController.MenuOpen || ConfirmDialog.ConsumesEscape || KillCam.Active || PoseWheel.IsOpen) return false;
            return me.InputReader == null || me.InputReader.InputEnabled;
        }

        private void Open(NetPlayer me)
        {
            _open = true;
            _toggle = false;
            _openedAt = Time.unscaledTime;
            _cursor = Vector2.zero;
            _hl = -1;
            _target = EmoteRules.LookTarget(me);
            _hasTarget = _target != null;
            _targetCheck = Time.unscaledTime + 0.25f;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            CreateStages(me);
            BuildPage(_page);
            GameAudio.Play2D(GameSfx.UiHover, 0.35f);
            Debug.Log($"[EmoteWheel] open: page {_page}, target {(_target != null ? _target.Nick : "-")}");
        }

        private void Close()
        {
            if (!_open) return;
            _open = false;
            _toggle = false;
            _closedFrame = Time.frameCount;
            TestHighlight = -1;
        }

        private void SetPage(int page)
        {
            page = Mathf.Clamp(page, 0, 1);
            if (page == _page && _builtPage == page) return;
            _page = page;
            _hl = -1;
            _cursor = Vector2.zero;
            _pageAnim = 0f;
            BuildPage(page);
            GameAudio.Play2D(GameSfx.UiClick, 0.4f);
        }

        private bool Available(int i, NetPlayer me)
        {
            if (i < 0 || i >= _slices.Count) return false;
            if (!EmoteRules.Free(me, out _)) return false;
            return _page == 0 || (_hasTarget && _target != null);
        }

        private void Pick(NetPlayer me, int index, bool closeAnyway)
        {
            if (index < 0 || index >= _slices.Count) { Close(); return; }
            var id = _slices[index].Id;
            if (!Available(index, me))
            {
                GameAudio.Play2D(GameSfx.UiError, 0.5f);
                if (_page == 1 && !_hasTarget) GameHud.Toast("Spójrz na gracza obok (do 2.5 m).", 2f);
                if (closeAnyway) Close();
                return;
            }
            GameAudio.Play2D(GameSfx.UiClick, 0.7f);
            bool ok = Emotes.IsPair(id) ? me.AskPairEmote(id, _target) : me.StartSoloEmote(id);
            if (!ok) GameAudio.Play2D(GameSfx.UiError, 0.5f);
            Debug.Log($"[EmoteWheel] picked {id} ({(ok ? "ok" : "refused")})");
            Close();
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            DestroyStages();
        }

        private void OnDestroy()
        {
            DestroyStages();
            if (_inst == this) _inst = null;
        }

        private void OnSceneChanged(Scene a, Scene b)
        {
            _open = false;
            _anim = 0f;
            DestroyStages();
            _root.gameObject.SetActive(false);
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
            else if (!can) Close();
            else StepOpen(me, kb, mouse);

            float target = _open ? 1f : 0f;
            _anim = Mathf.MoveTowards(_anim, target, Time.unscaledDeltaTime / (_open ? OpenTime : CloseTime));
            if (!_open && _anim <= 0f)
            {
                if (_root.gameObject.activeSelf)
                {
                    _root.gameObject.SetActive(false);
                    DestroyStages();
                }
                return;
            }
            _pageAnim = Mathf.MoveTowards(_pageAnim, 1f, Time.unscaledDeltaTime / 0.2f);
            ApplyOpenAnim(_anim);
            StepThumbs();
            AnimateSlices(me);
        }

        private void StepOpen(NetPlayer me, Keyboard kb, Mouse mouse)
        {
            // Page: mouse wheel (or test).
            if (TestPage >= 0) { SetPage(TestPage); TestPage = -1; }
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (wheel < -0.01f) SetPage(1);
            else if (wheel > 0.01f) SetPage(0);

            if (Time.unscaledTime >= _targetCheck)
            {
                _targetCheck = Time.unscaledTime + 0.25f;
                var t = EmoteRules.LookTarget(me);
                // Sticky: a new player re-renders the partner pictures, looking away keeps the last look (no flicker).
                if (t != null && t != _target)
                {
                    bool newLook = _target == null || _target.SkinVar.Value != t.SkinVar.Value || _target.McNickVar.Value != t.McNickVar.Value;
                    _target = t;
                    if (newLook) RebuildPartner();
                }
                _hasTarget = t != null;
            }

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
            else if (_cursor.magnitude > DeadZone && n > 0)
            {
                float ang = Mathf.Atan2(_cursor.x, _cursor.y) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                hl = Mathf.RoundToInt(ang / (360f / n)) % n;
            }
            if (hl != _hl)
            {
                _hl = hl;
                if (hl >= 0) GameAudio.Play2D(GameSfx.UiHover, Available(hl, me) ? 0.55f : 0.3f);
            }
            var show = _hl >= 0 ? _slices[_hl].Id : EmoteId.None;
            if (show != _shown && _stage != null)
            {
                _shown = show;
                _stage.Show(show);
            }
            UpdateTexts(me);
            UpdatePointer();

            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
            bool rmb = (mouse != null && mouse.rightButton.wasPressedThisFrame) || TestCancel;
            TestCancel = false;
            bool lmb = (mouse != null && mouse.leftButton.wasPressedThisFrame) || TestClick;
            TestClick = false;
            bool held = (kb != null && kb.kKey.isPressed) || TestHold;
            bool again = kb != null && kb.kKey.wasPressedThisFrame && _toggle;
            if (esc || rmb)
            {
                Close();
                if (esc) _relock = 2;
                GameAudio.Play2D(GameSfx.UiHover, 0.3f);
            }
            else if (lmb || again)
            {
                if (_hl >= 0) Pick(me, _hl, false);
                else if (again) Close();
            }
            else if (!held && !_toggle)
            {
                if (_hl < 0 && Time.unscaledTime - _openedAt < TapTime) _toggle = true;
                else if (_hl >= 0) Pick(me, _hl, true);
                else Close();
            }
        }
    }
}
