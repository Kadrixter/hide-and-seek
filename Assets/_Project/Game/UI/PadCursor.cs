using System;
using System.Collections.Generic;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Virtual mouse cursor driven by the pad: the universal way to use every menu / panel / board without a mouse.
    /// Shown while the pad is the last used device and a menu or cursor UI is open (the cursor is free).
    ///
    /// How it reaches the UI:
    ///  - uGUI (main menu, Esc menu, settings, dialogs, HUD panels) and world clicks (chess, tic-tac-toe, pool, dot
    ///    link): a second <see cref="Mouse"/> device ("PadVirtualMouse", <see cref="InputSystem.AddDevice{T}"/>) whose
    ///    position / buttons / scroll are written with <see cref="InputState.Change{TState}"/> every frame and which is
    ///    made <see cref="Mouse.current"/>. InputSystemUIInputModule binds "&lt;Mouse&gt;/..." for every mouse, and the board
    ///    code ray-casts from Mouse.current.position, so both work unchanged.
    ///  - IMGUI (radio, TV, Claude survey panels): IMGUI events in a standalone player come from the OS message queue,
    ///    not from the Input System, so a virtual device never reaches them and there is no public API to queue an
    ///    event. Those OnGUI methods call <see cref="ImguiPass"/> as their first line: it moves the pointer of every
    ///    event to the cursor (hover) and, in the Repaint pass, re-runs the OnGUI once per synthetic event of this
    ///    frame (MouseDown / MouseDrag / MouseUp / ScrollWheel) with <see cref="Event.current"/> swapped for it, so
    ///    GUI.Button, text fields and the hand-made sliders get a normal press / drag / release.
    ///
    /// Cross = left button, Square / Triangle = right button, sticks move (accelerating), D-pad jumps to the next
    /// control in that direction (or steps a slider under the cursor), L1 / R1 switch settings pages
    /// (<see cref="PadUi.SwitchTab"/>) or scroll, L2 / R2 scroll smoothly.
    /// </summary>
    [DefaultExecutionOrder(-950)]
    public sealed class PadCursor : MonoBehaviour
    {
        /// <summary>The pad is the last used device (<see cref="PadInput.UsingPad"/>).</summary>
        public static bool PadMode { get; private set; }

        /// <summary>The cursor is shown and drives the UI this frame.</summary>
        public static bool Active { get; private set; }

        /// <summary>Cursor position in screen pixels, origin bottom-left (Input System convention).</summary>
        public static Vector2 Position { get; set; }

        /// <summary>Cursor position in IMGUI coordinates (origin top-left).</summary>
        public static Vector2 GuiPosition => new(Position.x, Screen.height - Position.y);

        /// <summary>The virtual mouse device (null until the pad was used); input code that tracks "last device" can skip it.</summary>
        public static Mouse VirtualMouse { get; private set; }

        /// <summary>Set by <see cref="PadKeyboard"/> while it is open: the sticks and Cross belong to the keyboard.</summary>
        public static bool Frozen;

        /// <summary>Left button presses sent so far (tests / logs).</summary>
        public static int Clicks { get; private set; }

        private static int _navClaimFrame = -10;

        /// <summary>A page with its own D-pad handling (the "Pad" settings page) calls this every frame it uses the D-pad: the cursor then does not jump.</summary>
        public static void ClaimNavigation() => _navClaimFrame = Time.frameCount;

        public static bool NavigationClaimed => _navClaimFrame >= Time.frameCount - 1;

        private struct Synth
        {
            public EventType Type;
            public int Button;
            public Vector2 Delta;
            public bool Used;
        }

        private static readonly List<Synth> _synth = new();
        private static bool _inPass;
        private static Texture2D _arrow;

        private bool _left, _right, _hidOs, _moved;
        private float _accel, _scrollAcc;
        private Vector2 _fedPos;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("PadUi");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<PadCursor>();
            go.AddComponent<PadKeyboard>();
            go.AddComponent<PadUi>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-testpadui") >= 0) go.AddComponent<PadUiTest>();
        }

        private void Update()
        {
            _synth.Clear();
            if (!PadUiInput.Connected)
            {
                if (PadMode) Debug.Log("[PadCursor] pad disconnected");
                PadMode = false;
                SetActive(false);
                return;
            }
            bool using_ = PadInput.UsingPad; // last used device (PadInput ignores this virtual mouse and the fallback devices)
            if (using_ != PadMode) Debug.Log(using_ ? "[PadCursor] pad is the active device" : "[PadCursor] mouse / keyboard used - pad cursor off");
            PadMode = using_;

            SetActive(PadMode && UiWantsCursor());
            if (!Active) return;
            if (Frozen)
            {
                // On-screen keyboard: release and park the pointer (a release over the clicked field must not click it again).
                if (_left || _right || _fedPos.x > -1000f)
                {
                    _left = _right = false;
                    Park();
                }
                return;
            }

            UiModuleNavigation(false);
            Move();
            Buttons();
            Feed();
            if (CursorPolicy.Active) { /* CursorPolicy hides the OS cursor while Active */ }
            else if (Cursor.visible) { Cursor.visible = false; _hidOs = true; }
        }

        /// <summary>A menu, panel or board that needs a pointer is open.</summary>
        private static bool UiWantsCursor()
        {
            if (CursorPolicy.Active) return CursorPolicy.WantsFree;
            return Cursor.lockState != CursorLockMode.Locked; // main menu / loading: the menus free the cursor themselves
        }

        private void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;
            if (on)
            {
                if (VirtualMouse == null || !VirtualMouse.added)
                {
                    VirtualMouse = InputSystem.AddDevice<Mouse>("PadVirtualMouse");
                    Debug.Log("[PadCursor] virtual mouse added");
                }
                if (Position == Vector2.zero || Position.x > Screen.width || Position.y > Screen.height)
                    Position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                _fedPos = Position;
                _accel = 0f;
                Debug.Log($"[PadCursor] on at {Position}");
            }
            else
            {
                _left = _right = false;
                Park(); // no hover left on a button
                UiModuleNavigation(true);
                if (_hidOs && !CursorPolicy.Active && Cursor.lockState != CursorLockMode.Locked) Cursor.visible = true;
                _hidOs = false;
                Debug.Log("[PadCursor] off");
            }
        }

        /// <summary>
        /// The uGUI module's own submit / move / cancel actions also listen to the pad (Cross = submit the selected
        /// button): with the cursor that would click twice, so they are off while the cursor is on.
        /// </summary>
        private static void UiModuleNavigation(bool on)
        {
            var es = EventSystem.current;
            var m = es != null ? es.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule : null;
            if (m == null && es != null) m = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (m == null) return;
            Toggle(m.submit, on);
            Toggle(m.cancel, on);
            Toggle(m.move, on);

            static void Toggle(InputActionReference r, bool enable)
            {
                var a = r != null ? r.action : null;
                if (a == null || a.enabled == enable) return;
                if (enable) a.Enable();
                else a.Disable();
            }
        }

        // ================================================================ movement

        private void Move()
        {
            _moved = false;
            if (Frozen) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector2 st = PadUiInput.RightStick;
            if (st.magnitude < 0.05f) st = PadUiInput.LeftStick; // values already past the Input System stick dead zone
            float m = Mathf.Min(1f, st.magnitude);
            if (m < 0.05f)
            {
                _accel = 0f;
            }
            else
            {
                // Response curve for precision near the centre, acceleration while the stick stays pushed out.
                // (half-pushed stick ~ 160 px/s, full ~ 500 px/s rising to ~1700 px/s at 1080p; a light push still creeps for pixel aiming)
                float k = Mathf.Max(0.04f, Mathf.Pow(Mathf.InverseLerp(0.05f, 1f, m), 1.5f));
                _accel = m > 0.85f ? Mathf.MoveTowards(_accel, 1f, dt / 0.7f) : Mathf.MoveTowards(_accel, 0f, dt / 0.25f);
                float speed = (500f + 1200f * _accel * _accel) * Screen.height / 1080f;
                Position += st.normalized * (k * speed * dt);
                _moved = true;
            }

            var dp = PadUiInput.Dpad;
            if (dp != Vector2Int.zero && !NavigationClaimed) SnapOrStep(dp);
            Position = new Vector2(Mathf.Clamp(Position.x, 0f, Screen.width - 1), Mathf.Clamp(Position.y, 0f, Screen.height - 1));
        }

        /// <summary>D-pad: steps a slider under the cursor left / right, otherwise jumps to the nearest control in that direction.</summary>
        private void SnapOrStep(Vector2Int dir)
        {
            var under = SelectableUnder(Position);
            if (under is Slider sl && dir.x != 0 && sl.IsInteractable())
            {
                float step = sl.wholeNumbers ? 1f : (sl.maxValue - sl.minValue) / 20f;
                sl.value = Mathf.Clamp(sl.value + dir.x * step, sl.minValue, sl.maxValue);
                return;
            }
            var next = NextSelectable(Position, dir, under);
            if (next == null) return;
            Position = ScreenRect(next).center;
            _moved = true;
        }

        private static readonly List<RaycastResult> _hits = new();

        /// <summary>The uGUI control (Button, Slider, Toggle...) under a screen point, or null.</summary>
        public static Selectable SelectableUnder(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return null;
            var data = new PointerEventData(es) { position = screen };
            _hits.Clear();
            es.RaycastAll(data, _hits);
            foreach (var h in _hits)
            {
                if (h.gameObject == null) continue;
                var s = h.gameObject.GetComponentInParent<Selectable>();
                if (s != null) return s;
                return null; // the top hit is not a control (a dialog veil): nothing under the cursor
            }
            return null;
        }

        /// <summary>The closest interactable control of the top-most canvas in a direction (y up), or null.</summary>
        public static Selectable NextSelectable(Vector2 from, Vector2Int dir, Selectable skip)
        {
            var all = Selectable.allSelectablesArray;
            int top = int.MinValue;
            foreach (var s in all)
                if (Usable(s, out var root)) top = Mathf.Max(top, root.sortingOrder);
            Selectable best = null;
            float bestScore = float.MaxValue;
            Vector2 d = dir;
            foreach (var s in all)
            {
                if (s == skip || !Usable(s, out var root) || root.sortingOrder != top) continue;
                var r = ScreenRect(s);
                if (r.width < 2f || r.height < 2f) continue;
                Vector2 v = r.center - from;
                float along = Vector2.Dot(v, d);
                if (along < 6f) continue;
                float side = Mathf.Abs(v.x * d.y - v.y * d.x);
                float score = along + side * 2.2f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        private static bool Usable(Selectable s, out Canvas root)
        {
            root = null;
            if (s == null || !s.isActiveAndEnabled || !s.IsInteractable()) return false;
            var c = s.GetComponentInParent<Canvas>();
            if (c == null || !c.isActiveAndEnabled) return false;
            root = c.rootCanvas;
            var r = ScreenRect(s);
            return r.xMax > 0 && r.yMax > 0 && r.xMin < Screen.width && r.yMin < Screen.height;
        }

        private static readonly Vector3[] _corners = new Vector3[4];

        /// <summary>Screen rectangle (origin bottom-left) of a uGUI element.</summary>
        public static Rect ScreenRect(Component c)
        {
            var rt = c.transform as RectTransform;
            if (rt == null) return default;
            rt.GetWorldCorners(_corners);
            var canvas = c.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // ================================================================ buttons / scroll

        private void Buttons()
        {
            bool wasLeft = _left, wasRight = _right;
            if (Frozen)
            {
                _left = _right = false;
            }
            else
            {
                if (!_left && PadUiInput.Cross) _left = true;
                else if (_left && !PadUiInput.CrossHeld) _left = false;
                if (!_right && (PadUiInput.Square || PadUiInput.Triangle)) _right = true;
                else if (_right && !PadUiInput.SquareHeld && !PadUiInput.TriangleHeld) _right = false;
            }
            Edge(wasLeft, _left, 0);
            Edge(wasRight, _right, 1);
            if (_left && !wasLeft)
            {
                Clicks++;
                var s = SelectableUnder(Position);
                Debug.Log($"[PadCursor] click at {Position} on {(s != null ? s.name : "-")}");
            }

            float scroll = 0f;
            if (!Frozen)
            {
                if (PadUiInput.L1 && !PadUi.SwitchTab(-1)) scroll += 1f;
                if (PadUiInput.R1 && !PadUi.SwitchTab(1)) scroll -= 1f;
                _scrollAcc += (PadUiInput.L2 - PadUiInput.R2) * Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 10f;
                if (Mathf.Abs(_scrollAcc) >= 1f)
                {
                    float whole = Mathf.Sign(_scrollAcc) * Mathf.Floor(Mathf.Abs(_scrollAcc));
                    scroll += whole;
                    _scrollAcc -= whole;
                }
            }
            _pendingScroll = scroll;
            if (scroll != 0f) _synth.Add(new Synth { Type = EventType.ScrollWheel, Delta = new Vector2(0f, -3f * scroll) });
        }

        private float _pendingScroll;

        private void Edge(bool was, bool now, int button)
        {
            if (now && !was) _synth.Add(new Synth { Type = EventType.MouseDown, Button = button });
            else if (now && _moved) _synth.Add(new Synth { Type = EventType.MouseDrag, Button = button });
            else if (!now && was) _synth.Add(new Synth { Type = EventType.MouseUp, Button = button });
        }

        /// <summary>Buttons up, pointer off-screen.</summary>
        private void Park()
        {
            var m = VirtualMouse;
            _fedPos = new Vector2(-5000f, -5000f);
            if (m == null || !m.added) return;
            InputState.Change(m, new MouseState { position = _fedPos });
        }

        /// <summary>Writes the virtual mouse state (uGUI pointer + Mouse.current for the board / world code).</summary>
        private void Feed()
        {
            var m = VirtualMouse;
            if (m == null || !m.added) return;
            if (_fedPos.x < -1000f) _fedPos = Position; // back from parking: no jump in delta
            var st = new MouseState
            {
                position = Position,
                delta = Position - _fedPos,
                scroll = new Vector2(0f, _pendingScroll * 120f), // one wheel notch of a Windows mouse
            };
            st = st.WithButton(MouseButton.Left, _left).WithButton(MouseButton.Right, _right);
            InputState.Change(m, st);
            if (Mouse.current != m) m.MakeCurrent();
            _fedPos = Position;
        }

        // ================================================================ IMGUI

        /// <summary>
        /// First line of an interactive OnGUI: <c>if (PadCursor.ImguiPass(OnGUI)) return;</c>. Points every event at the pad
        /// cursor and, in the Repaint pass, runs <paramref name="again"/> once per pad mouse event of this frame. Always
        /// returns false today (the caller then draws normally); the bool leaves room for swallowing an event later.
        /// </summary>
        public static bool ImguiPass(Action again)
        {
            if (!Active || _inPass) return false;
            var e = Event.current;
            if (e == null) return false;
            Vector2 gp = GuiPosition;
            if (e.type == EventType.Repaint || e.type == EventType.Layout || e.isMouse || e.isScrollWheel) e.mousePosition = gp;
            if (e.type != EventType.Repaint || _synth.Count == 0 || again == null) return false;

            var matrix = GUI.matrix;
            Color color = GUI.color, bg = GUI.backgroundColor, content = GUI.contentColor;
            bool enabled = GUI.enabled;
            int depth = GUI.depth;
            _inPass = true;
            try
            {
                for (int i = 0; i < _synth.Count; i++)
                {
                    var s = _synth[i];
                    if (s.Used) continue;
                    var ev = new Event
                    {
                        type = s.Type,
                        button = s.Button,
                        mousePosition = gp,
                        delta = s.Delta,
                        clickCount = s.Type == EventType.MouseDown ? 1 : 0,
                    };
                    Event.current = ev;
                    try { again(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    if (ev.type == EventType.Used)
                    {
                        s.Used = true; // like IMGUI: a used event does not reach the other panels
                        _synth[i] = s;
                    }
                    GUI.matrix = matrix;
                    GUI.color = color;
                    GUI.backgroundColor = bg;
                    GUI.contentColor = content;
                    GUI.enabled = enabled;
                    GUI.depth = depth;
                }
            }
            finally
            {
                _inPass = false;
                Event.current = e;
            }
            return false;
        }

        /// <summary>True inside a synthetic pass of <see cref="ImguiPass"/> (the event is a pad click / drag).</summary>
        public static bool InPadPass => _inPass;

        private void OnGUI()
        {
            if (!Active || Frozen || Event.current.type != EventType.Repaint) return;
            GUI.depth = -10000;
            if (_arrow == null) _arrow = BuildArrow();
            float s = Mathf.Max(1f, Screen.height / 1080f);
            var p = GuiPosition;
            var c = GUI.color;
            GUI.color = _left ? new Color(1f, 0.85f, 0.55f) : Color.white;
            GUI.DrawTexture(new Rect(p.x - 2f * s, p.y - 2f * s, 24f * s, 36f * s), _arrow);
            GUI.color = c;
        }

        /// <summary>Arrow pointer (white with a dark outline), built once.</summary>
        private static Texture2D BuildArrow()
        {
            const int w = 24, h = 36;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            // Classic arrow outline in texture pixels (y down), scaled from a 12 x 19 design.
            var poly = new[] { new Vector2(2, 2), new Vector2(2, 30), new Vector2(9, 23), new Vector2(14, 34), new Vector2(18, 32), new Vector2(13, 22), new Vector2(22, 22) };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var pt = new Vector2(x + 0.5f, y + 0.5f);
                float dist = EdgeDistance(poly, pt);
                bool inside = Inside(poly, pt);
                Color32 col = new(0, 0, 0, 0);
                if (inside) col = dist > 1.6f ? new Color32(255, 255, 255, 255) : new Color32(20, 20, 24, 255);
                else if (dist < 1.2f) col = new Color32(20, 20, 24, (byte)(255 * (1.2f - dist) / 1.2f));
                px[(h - 1 - y) * w + x] = col;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        private static float EdgeDistance(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }
    }
}
