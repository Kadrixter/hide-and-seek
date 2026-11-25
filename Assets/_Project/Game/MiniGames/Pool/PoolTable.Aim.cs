using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Local aiming at the pool table (own turn only). Behind-the-ball view: horizontal mouse turns the cue around the
    /// cue ball; top view (Space): the cue points at the cursor on the cloth. Shift = fine aim, Q / E = fine turn.
    /// Hold LMB and pull the mouse back (down) = power, release = shot (back to zero or RMB = cancel). RMB + mouse or
    /// W A S D / arrows = where the tip hits the cue ball (spin). Ball in hand: the cue ball follows the cursor, LMB puts
    /// it down, B picks it up again. The aim goes to the others through <see cref="NetPlayer.SetPoolAim"/>.
    /// </summary>
    public sealed partial class PoolTable
    {
        /// <summary>Mouse pull (pixels, reference 1080p) for full power.</summary>
        const float DragFull = 300f;
        const float TurnPerPx = 0.0022f, FineK = 0.08f;

        float _aimAngle, _pull, _dragPx, _syncPull, _awaitT;
        bool _dragging, _awaitShot, _aimInit, _topView, _fineOn, _sendNow, _handOk, _seenHand;
        Vector2 _spin, _handPos;
        byte _handMode;
        float _ptrOffset, _fineAnchorPtr, _fineAnchorAim;
        bool _rmbWas;
        int _ptrFrame = -1, _seenShot = int.MinValue, _seenTurn = -1, _seenRound = int.MinValue, _seenPhase = -1;
        readonly Vector2[] _scratch = new Vector2[16];

        /// <summary>Power 0..1 while pulling back (HUD).</summary>
        public float LocalPower => _dragging ? _pull : 0f;
        public bool LocalDragging => _dragging;
        public Vector2 LocalSpin => _spin;
        public bool TopView => _topView;
        /// <summary>The local player holds the cue ball in his hand (it follows the cursor).</summary>
        public bool LocalHolding => LocalTurn && State.BallInHand && _handMode == 1;

        /// <summary>
        /// Cue ball in the shooter's hand, for the ball view: true while the player to shoot has ball in hand and holds
        /// it / put it down somewhere (local player: live; others: from his synced aim).
        /// </summary>
        public bool HandBall(out Vector2 pos)
        {
            pos = default;
            if (!Playing || !State.BallInHand || Rolling) return false;
            int s = State.Shooter;
            if (s < 0 || s > 3) return false;
            var v = _view[s];
            if (v.Player != SeatPlayer(s) || v.HandMode == 0) return false;
            pos = v.Hand;
            return true;
        }

        /// <summary>Called by MiniGameLocal every frame while the local player is at this table (mouse ray from the camera, LMB pressed this frame).</summary>
        public void LocalPointer(NetPlayer me, Ray ray, bool click)
        {
            if (_ptrFrame == Time.frameCount) return;
            _ptrFrame = Time.frameCount;
            LocalInput(me, ray, click);
        }

        /// <summary>When MiniGameLocal does not feed us this frame (not wired, a dialog is open), read the mouse here.</summary>
        void TickLocalFallback()
        {
            if (_ptrFrame == Time.frameCount || LocalSeat < 0) return;
            var me = NetPlayer.Local;
            var mouse = Mouse.current;
            if (me == null || mouse == null || ConfirmDialog.IsOpen || LocalPlayerController.MenuOpen || !MiniGameCamera.MouseRay(out var ray))
            {
                CancelDrag();
                return;
            }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool click = mouse.leftButton.wasPressedThisFrame && !overUi && MiniGameCamera.Weight > 0.8f;
            _ptrFrame = Time.frameCount;
            LocalInput(me, ray, click);
        }

        void CancelDrag()
        {
            if (_dragging) _sendNow = true;
            _dragging = false;
            _pull = 0f;
        }

        /// <summary>Cue ball the local player shoots from.</summary>
        Vector2 AimCue() => State.BallInHand && _handMode >= 1 ? _handPos : State.Pos[0];

        void LocalInput(NetPlayer me, Ray ray, bool click)
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (!LocalTurn || _awaitShot || mouse == null)
            {
                CancelDrag();
                return;
            }
            float dt = Time.unscaledDeltaTime;
            Vector2 delta = mouse.delta.ReadValue() * (1080f / Mathf.Max(200f, Screen.height));
            bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                _topView = !_topView;
                _ptrOffset = 0f;
                _fineOn = false;
            }

            // Ball in hand: carry the cue ball, LMB puts it down (if the spot is legal), B picks it up again.
            if (State.BallInHand)
            {
                if (_handMode == 0) { _handMode = 1; _handPos = StartHandPos(); _sendNow = true; }
                if (_handMode == 1)
                {
                    CancelDrag();
                    PosWithout(_handPos);
                    bool got = HitCloth(ray, out var p);
                    got |= TestHandPoint(ref p, ref click);
                    if (got) _handPos = PoolSim.ClampCueBall(_scratch, (ushort)(State.Down | 1), p, State.Kitchen);
                    _handOk = PoolSim.CanPlaceCueBall(_scratch, (ushort)(State.Down | 1), _handPos, State.Kitchen);
                    if (click)
                    {
                        if (_handOk) { _handMode = 2; _sendNow = true; _aimInit = false; MiniGameSounds.Select(this); }
                        else MiniGameSounds.Deny(this);
                    }
                    return;
                }
                if (kb != null && kb.bKey.wasPressedThisFrame && !_dragging) { _handMode = 1; _sendNow = true; return; }
            }
            EnsureAimInit();

            bool rmb = mouse.rightButton.isPressed;
            // Top view: the pointer moved while setting the spin; carry on from the current aim, no jump.
            if (_rmbWas && !rmb && _topView && HitCloth(ray, out var rp))
            {
                Vector2 to = rp - AimCue();
                if (to.sqrMagnitude > 0.0004f) _ptrOffset = Mathf.DeltaAngle(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, _aimAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            }
            _rmbWas = rmb;
            if (!_dragging)
            {
                if (rmb) _spin += delta * 0.0045f;
                else if (_topView) PointAim(ray, delta, shift);
                else _aimAngle -= delta.x * TurnPerPx * (shift ? FineK : 1f);
                if (kb != null)
                {
                    float q = (kb.qKey.isPressed ? 1f : 0f) - (kb.eKey.isPressed ? 1f : 0f);
                    _aimAngle += q * dt * (shift ? 0.017f : 0.1f);
                    float sx = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                    float sy = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                    _spin += new Vector2(sx, sy) * (dt * 1.1f);
                }
                _spin = Vector2.ClampMagnitude(_spin, 0.8f);
                if (click && !rmb)
                {
                    _dragging = true;
                    _dragPx = 0f;
                    _pull = 0f;
                    _sendNow = true;
                }
            }
            else
            {
                _dragPx = Mathf.Clamp(_dragPx - delta.y, 0f, DragFull);
                _pull = _dragPx / DragFull;
                if (mouse.rightButton.wasPressedThisFrame) CancelDrag(); // RMB = cancel
                else if (!mouse.leftButton.isPressed && !_testLmb)
                {
                    float pull = _pull;
                    CancelDrag();
                    if (pull > 0.02f) Shoot(me, pull);
                }
            }
            _aimAngle = Mathf.Repeat(_aimAngle + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
        }

        /// <summary>Top view: the cue points at the cursor; Shift slows it down (no jump when Shift goes up again).</summary>
        void PointAim(Ray ray, Vector2 delta, bool shift)
        {
            if (!HitCloth(ray, out var p)) return;
            Vector2 to = p - AimCue();
            if (to.sqrMagnitude < 0.0004f) return;
            float pa = Mathf.Atan2(to.y, to.x);
            if (shift)
            {
                if (!_fineOn) { _fineOn = true; _fineAnchorPtr = pa; _fineAnchorAim = _aimAngle; }
                _aimAngle = _fineAnchorAim + Mathf.DeltaAngle(_fineAnchorPtr * Mathf.Rad2Deg, pa * Mathf.Rad2Deg) * Mathf.Deg2Rad * FineK;
                return;
            }
            if (_fineOn)
            {
                _fineOn = false;
                _ptrOffset = Mathf.DeltaAngle(pa * Mathf.Rad2Deg, _aimAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            }
            if (delta.sqrMagnitude > 0.01f) _aimAngle = pa + _ptrOffset;
        }

        void Shoot(NetPlayer me, float pull)
        {
            int seat = LocalSeat;
            if (seat < 0 || me == null) return;
            Vector2 cue = AimCue();
            float power = Mathf.Clamp01(pull);
            _syncPull = pull;
            _sendNow = true;
            _awaitShot = true;
            _awaitT = 0f;
            // The stroke first: on the host the RPC runs (and starts the shot, OnShotStarted) right inside the call.
            StartLocalStroke(seat, cue, _aimAngle, pull, power, _spin);
            me.PoolShotRpc(_aimAngle, power, _spin.x, _spin.y, cue.x, cue.y);
        }

        /// <summary>Our shot started on this peer (OnShotStarted).</summary>
        void LocalShotStarted()
        {
            _awaitShot = false;
            _dragging = false;
            _pull = 0f;
        }

        /// <summary>
        /// Own turn: the cursor is hidden and locked in the behind-the-ball view (the mouse turns the cue, no screen edge),
        /// hidden while pulling / setting the spin in the top view; shown again for dialogs, ball in hand and waiting.
        /// </summary>
        void TickCursor()
        {
            var mouse = Mouse.current;
            bool busy = _dragging || (mouse != null && mouse.rightButton.isPressed);
            bool hide = LocalSeat >= 0 && LocalTurn && !_awaitShot && !LocalHolding && (!_topView || busy)
                        && !ConfirmDialog.IsOpen && !LocalPlayerController.MenuOpen && MiniGameCamera.Weight > 0.8f;
            bool was = CursorHidden;
            CursorHidden = hide;
            if (!hide)
            {
                if (was && LocalSeat >= 0)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }
            if (Cursor.visible) Cursor.visible = false;
            var want = _topView ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != want) Cursor.lockState = want;
        }

        /// <summary>Per frame: give up on a shot the server never started, send the aim to the others.</summary>
        void TickLocalAim(float dt)
        {
            int seat = LocalSeat;
            if (_awaitShot)
            {
                _awaitT += dt;
                if (_awaitT > 3f || seat < 0)
                {
                    _awaitShot = false;
                    CancelLocalStroke(seat);
                }
            }
            var me = NetPlayer.Local;
            if (seat < 0 || me == null)
            {
                _dragging = false;
                return;
            }
            byte hand = LocalTurn && State.BallInHand ? _handMode : (byte)0;
            float pull = _dragging ? _pull : _syncPull;
            me.SetPoolAim(new Vector4(_aimAngle, pull, _spin.x, _spin.y), new Vector3(_handPos.x, _handPos.y, hand), _sendNow);
            _sendNow = false;
        }

        protected override void PoseInputs(int seat, out Vector3 reach, out float reachW, out float think, out Vector3 look, out float lean)
        {
            // Not used by the cue stance (ApplyPose is overridden in PoolTable.Pose.cs); kept for the base contract.
            reach = Centre;
            reachW = 0f;
            think = 0f;
            look = Centre;
            lean = 0f;
        }
    }
}
