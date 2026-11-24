using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Mouse and keys of the local player at the dot link panel (runs before the HUD so Esc leaves the panel instead of
    /// opening the game menu). LMB on a dot starts its line (on a line: carries on from that cell), dragging draws cell
    /// by cell (back over the line = undo, a jump of several cells is walked through), crossing another line cuts it
    /// for as long as the pointer stays there; RMB wipes the line of the colour under the pointer; Esc leaves (the
    /// lines stay on the server). The drag is shown at once as a local draft and sent to the server ~12 times a second
    /// and on release. Tests set <see cref="TestRay"/> / <see cref="TestLmb"/> / <see cref="TestRmb"/> / <see cref="TestEsc"/>.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class DotLinkLocal : MonoBehaviour
    {
        static DotLinkLocal _inst;

        /// <summary>The local player works on the panel (camera, cursor, input belong to it).</summary>
        public static bool Active { get; private set; }
        /// <summary>The board as the local user sees it while dragging (null = show the server's).</summary>
        public static DotLinkBoard Draft { get; private set; }
        public static int DraftVersion { get; private set; }
        public static int Hover { get; private set; } = -1;
        /// <summary>Esc was used by the panel this frame (the game menu must not open).</summary>
        public static bool ConsumesEscape => _escFrame == Time.frameCount;

        public static bool TestActive;
        public static Ray TestRay;
        public static bool TestLmb, TestRmb, TestEsc;

        static int _escFrame = -1;
        DotLinkHud _hud;
        DotLinkBoard _base;
        readonly List<int> _cur = new();
        int _color = -1;
        byte _dragId;
        bool _dragging, _dirty, _lmbWas;
        float _nextSend, _draftUntil, _nextDeny;
        int _draftSeq;

        public static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("DotLinkLocal");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<DotLinkLocal>();
        }

        void Awake() => _hud = DotLinkHud.Create(transform);

        void Update()
        {
            var me = NetPlayer.Local;
            var panel = DotLinkPanel.Instance;
            var mm = MatchManager.Instance;
            Active = me != null && panel != null && mm != null && me.AtDotLink;
            DotLinkCamera.Attach(me, Active);
            _hud.Tick(Active, panel, mm);
            if (!Active)
            {
                if (_dragging) EndDrag(me, false);
                Draft = null;
                Hover = -1;
                _lmbWas = false;
                return;
            }
            // The draft stays until the server has the released line (or a moment passed).
            if (!_dragging && Draft != null && (panel.NetBoard == null || panel.NetBoard.Seq > _draftSeq || Time.unscaledTime > _draftUntil)) Draft = null;
            var kb = Keyboard.current;
            bool esc = (kb != null && kb.escapeKey.wasPressedThisFrame && !ConfirmDialog.ConsumesEscape) || TestEsc;
            TestEsc = false;
            if (esc && !LocalPlayerController.MenuOpen)
            {
                _escFrame = Time.frameCount;
                if (_dragging) EndDrag(me, true);
                me.DotLinkUseRpc(false);
                return;
            }
            if (ConfirmDialog.IsOpen || LocalPlayerController.MenuOpen || DotLinkCamera.Weight < 0.8f) return;
            var mouse = Mouse.current;
            Ray ray;
            bool lmb, rmbDown;
            if (TestActive)
            {
                ray = TestRay;
                lmb = TestLmb;
                rmbDown = TestRmb;
                TestRmb = false;
            }
            else
            {
                if (mouse == null || !DotLinkCamera.RayAt(mouse.position.ReadValue(), out ray)) return;
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                lmb = mouse.leftButton.isPressed && (!overUi || _dragging);
                rmbDown = mouse.rightButton.wasPressedThisFrame && !overUi;
            }
            int cell = panel.CellUnder(ray);
            Hover = cell;
            var net = panel.NetBoard;
            if (net == null || net.SolvedFlag)
            {
                if (_dragging) EndDrag(me, false);
                return;
            }
            var shown = Draft ?? net; // a released line the server has not echoed yet still counts
            if (lmb && !_lmbWas) BeginDrag(shown, cell);
            else if (lmb && _dragging) DragTo(cell);
            else if (!lmb && _dragging) EndDrag(me, true);
            _lmbWas = lmb;
            if (rmbDown && !_dragging && cell >= 0)
            {
                int c = shown.ColorAt(cell);
                if (c >= 0 && shown.Path(c).Count > 0) me.DotLinkClearRpc((byte)c);
            }
            if (_dragging && _dirty && Time.unscaledTime >= _nextSend) Send(me, false);
        }

        void BeginDrag(DotLinkBoard net, int cell)
        {
            if (cell < 0) return;
            var p = net.Puzzle;
            int dot = p.DotAt(cell);
            _cur.Clear();
            if (dot >= 0)
            {
                _color = dot;
                _cur.Add(cell);
            }
            else
            {
                int c = net.ColorAt(cell);
                if (c < 0) return;
                _color = c;
                var path = net.Path(c);
                for (int i = 0; i < path.Count; i++)
                {
                    _cur.Add(path[i]);
                    if (path[i] == cell) break;
                }
            }
            _base = net.Clone();
            _dragId++;
            _dragging = true;
            Rebuild();
        }

        void DragTo(int cell)
        {
            if (cell < 0 || _cur.Count == 0) return;
            var p = _base.Puzzle;
            for (int guard = 0; guard < 24 && _cur[_cur.Count - 1] != cell; guard++)
            {
                int head = _cur[_cur.Count - 1];
                int hx = head % p.W, hy = head / p.W, tx = cell % p.W, ty = cell / p.W;
                int next = Mathf.Abs(tx - hx) >= Mathf.Abs(ty - hy) ? head + (tx > hx ? 1 : -1) : head + (ty > hy ? p.W : -p.W);
                if (!Step(p, next)) break;
            }
        }

        bool Step(DotLinkPuzzle p, int n)
        {
            int count = _cur.Count;
            if (count >= 2 && n == _cur[count - 2])
            {
                _cur.RemoveAt(count - 1);
                Rebuild();
                return true;
            }
            int idx = _cur.IndexOf(n);
            if (idx >= 0)
            {
                _cur.RemoveRange(idx + 1, count - idx - 1);
                Rebuild();
                return true;
            }
            bool done = count >= 2 && _cur[count - 1] == p.OtherEnd(_color, _cur[0]);
            int dot = p.DotAt(n);
            if (done || (dot >= 0 && dot != _color))
            {
                if (!done && Time.unscaledTime >= _nextDeny)
                {
                    _nextDeny = Time.unscaledTime + 0.4f;
                    DotLinkPanel.Instance?.Play(DotLinkSounds.Deny, 0.3f);
                }
                return false;
            }
            _cur.Add(n);
            Rebuild();
            return true;
        }

        void Rebuild()
        {
            var d = _base.Clone();
            if (!d.SetPath(_color, _cur)) return;
            Draft = d;
            DraftVersion++;
            _dirty = true;
        }

        void EndDrag(NetPlayer me, bool send)
        {
            if (send && me != null) Send(me, true);
            _dragging = false;
            var net = DotLinkPanel.Instance != null ? DotLinkPanel.Instance.NetBoard : null;
            _draftSeq = net != null ? net.Seq : 0;
            _draftUntil = Time.unscaledTime + 0.6f;
            if (!send) Draft = null;
        }

        void Send(NetPlayer me, bool final)
        {
            _dirty = false;
            _nextSend = Time.unscaledTime + 0.08f;
            me.DotLinkPathRpc((byte)_color, new FixedString512Bytes(DotLinkBoard.EncodeCells(_cur)), _dragId, final);
        }
    }
}
