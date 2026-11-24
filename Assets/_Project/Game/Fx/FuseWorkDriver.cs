using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Per-player glue of the fuse box work on every client (added by MatchManager.Fuses): plays putting a fuse in /
    /// taking one out from the synced <see cref="NetPlayer.FuseWork"/> - the third-person arms
    /// (<see cref="BlockyCharacterActions.SetFuseWork"/>), the fuse itself (in the fist, then sliding along the socket
    /// axis, or the other way round), the sounds; and for the worker himself: steps up to the box and faces it, movement
    /// stops, the first-person fists follow the body's fists, letting go of E stops pulling a fuse out.
    /// </summary>
    [DefaultExecutionOrder(-24)]
    public sealed class FuseWorkDriver : MonoBehaviour
    {
        private NetPlayer _p;
        private BlockyCharacterActions _bca;
        private Transform _fuse;
        private int _fuseSymbol = -1;
        private float _last = -1f;
        private bool _cancelSent, _fppOn;
        private Vector3 _startPos;
        private float _startYaw;

        /// <summary>Tests: hold E without a keyboard.</summary>
        public static bool TestHoldE;

        /// <summary>Work time on this peer (-1 = none) and the moving fuse (tests).</summary>
        public float WorkTime => _last;
        public Transform MovingFuse => _fuse != null && _fuse.gameObject.activeSelf ? _fuse : null;

        private void Awake() => _p = GetComponent<NetPlayer>();

        private void OnDestroy()
        {
            if (_fuse != null) Destroy(_fuse.gameObject);
        }

        private void Update()
        {
            var mm = MatchManager.Instance;
            if (_p == null || !_p.IsSpawned || mm == null) return;
            if (_bca == null) _bca = _p.Actions as BlockyCharacterActions;
            if (_bca == null) return;
            var w = _p.FuseWork.Value;
            var view = w.Active ? FuseBoxViews.Get(w.Box) : null;
            float t = w.Active && view != null && _p.Life.Value == LifeState.Alive ? _p.FuseWorkTime : -1f;
            if (t >= FuseTimeline.Duration(w.Remove)) t = -1f;
            if (t < 0f)
            {
                if (_last >= 0f) End();
                _last = -1f;
                return;
            }
            if (_last < 0f) Begin(view, w);
            _bca.SetFuseWork(t, w.Remove, view.SeatedWorld(w.Socket), view.Axis, view.BraceWorld);
            Sounds(view, w, _last, t);
            if (_p.IsOwner) OwnerTick(view, w, t);
            _last = t;
        }

        private void LateUpdate()
        {
            var w = _p != null ? _p.FuseWork.Value : FuseWorkState.None;
            var view = w.Active ? FuseBoxViews.Get(w.Box) : null;
            if (_last < 0f || view == null || _bca == null) { if (_fuse != null && _fuse.gameObject.activeSelf) _fuse.gameObject.SetActive(false); return; }
            PoseFuse(view, w, _last);
            if (_p.IsOwner) OwnerView(view, w, _last);
        }

        private void Begin(FuseBoxView view, FuseWorkState w)
        {
            _cancelSent = false;
            _startPos = transform.position;
            _startYaw = transform.eulerAngles.y;
            if (_p.IsOwner) _p.InputReader?.ResetStance();
        }

        private void End()
        {
            _bca.SetFuseWork(-1f, false, Vector3.zero, Vector3.forward, Vector3.zero);
            if (_fuse != null) _fuse.gameObject.SetActive(false);
            if (_p.IsOwner && _fppOn) (_p.ViewModel as FirstPersonViewModel)?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
            _fppOn = false;
        }

        /// <summary>The fuse: in the fist on the way in / out, on the socket axis while it slides; hidden once the box shows it.</summary>
        private void PoseFuse(FuseBoxView view, FuseWorkState w, float t)
        {
            bool show = w.Remove ? t < FuseTimeline.OutEnd - 0.12f : !(t >= FuseTimeline.InClick && view.Box.Has(w.Socket)) && t < FuseTimeline.InEnd - 0.1f;
            if (_fuse == null || _fuseSymbol != w.Symbol)
            {
                if (_fuse != null) Destroy(_fuse.gameObject);
                _fuse = FuseModel.Create(null, w.Symbol);
                _fuse.name = "FuseInHands";
                _fuseSymbol = w.Symbol;
            }
            if (_fuse.gameObject.activeSelf != show) _fuse.gameObject.SetActive(show);
            if (!show) return;
            Vector3 axis = view.Axis;
            Quaternion rot = Quaternion.LookRotation(axis, Vector3.up);
            Vector3 pos = FuseTimeline.OnSocketLine(t, w.Remove)
                ? view.SeatedWorld(w.Socket) + axis * FuseTimeline.Out(t, w.Remove)
                : _bca.RightHandWorld - axis * FuseTimeline.FistOnCap;
            _fuse.SetPositionAndRotation(pos, rot);
        }

        private void Sounds(FuseBoxView view, FuseWorkState w, float prev, float t)
        {
            Vector3 at = view.SeatedWorld(w.Socket) + view.Axis * 0.1f;
            if (!w.Remove)
            {
                if (prev < FuseTimeline.InReach && t >= FuseTimeline.InReach) FuseSounds.Slide(at);
                if (prev < FuseTimeline.InClick && t >= FuseTimeline.InClick) FuseSounds.Click(at);
                return;
            }
            foreach (float k in FuseTimeline.Tugs)
                if (prev < k - 0.05f && t >= k - 0.05f) FuseSounds.Tug(at);
            if (prev < FuseTimeline.OutPop && t >= FuseTimeline.OutPop) FuseSounds.Pop(at);
        }

        // ================================================================ the worker himself

        private void OwnerTick(FuseBoxView view, FuseWorkState w, float t)
        {
            var motor = _p.Motor;
            if (motor == null) return;
            motor.SpeedScale = 0f;
            float yaw = Quaternion.LookRotation(-view.Axis).eulerAngles.y;
            if (t < 0.35f)
            {
                // Step up to the box (the arms need to reach the socket) and face it.
                float g = FuseTimeline.S(t, 0f, 0.3f);
                Vector3 stand = view.transform.TransformPoint(FuseBoxModel.WorkStand(w.Socket));
                Vector3 pos = Vector3.Lerp(_startPos, new Vector3(stand.x, _startPos.y, stand.z), g);
                motor.Teleport(pos, Mathf.LerpAngle(_startYaw, yaw, g), motor.LookPitch);
            }
            // Pulling a fuse out: let go of E (or walk / jump) before it pops = stop, the fuse stays in.
            if (w.Remove && !_cancelSent && t > 0.15f && t < FuseTimeline.OutPop)
            {
                var kb = Keyboard.current;
                var input = _p.InputReader;
                bool holding = TestHoldE || (kb != null && kb.eKey.isPressed);
                bool moved = input != null && (input.Move.sqrMagnitude > 0.2f || input.JumpHeld);
                if (!holding || moved)
                {
                    _cancelSent = true;
                    _p.RequestFuseCancelRpc();
                }
            }
        }

        /// <summary>The view on the socket, first-person fists where the body's fists are.</summary>
        private void OwnerView(FuseBoxView view, FuseWorkState w, float t)
        {
            var motor = _p.Motor;
            var cam = _p.Camera != null ? _p.Camera.transform : null;
            if (motor == null || cam == null) return;
            Vector3 to = view.SeatedWorld(w.Socket) - cam.position;
            float want = Mathf.Clamp(Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -55f, 25f);
            float body = FuseTimeline.Body(t, w.Remove);
            motor.SetLook(motor.LookYaw, Mathf.LerpAngle(motor.LookPitch, Mathf.LerpAngle(motor.LookPitch, want - 3f, body), 1f - Mathf.Exp(-6f * Time.deltaTime)));

            var fpv = _p.ViewModel as FirstPersonViewModel;
            var ch = _p.Character;
            float reach = FuseTimeline.Reach(t, w.Remove);
            bool on = fpv != null && ch != null && ch.IsBuilt && ch.IsFirstPerson && reach > 0.02f;
            if (!on)
            {
                if (_fppOn) fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                _fppOn = false;
                return;
            }
            _fppOn = true;
            Vector3 r = OnScreen(cam, _bca.RightHandWorld);
            float brace = FuseTimeline.Brace(t, w.Remove);
            Vector3 restL = cam.TransformPoint(new Vector3(-0.3f, -0.7f, 0.3f));
            Vector3 l = Vector3.Lerp(restL, OnScreen(cam, _bca.LeftHandWorld), brace);
            fpv.SetOverrideHands(true, r, Quaternion.LookRotation(-view.Axis, Vector3.up), l, HeldItem.None);
        }

        /// <summary>World point at arm's length on the view ray to <paramref name="world"/>, corrected for the view-model FOV.</summary>
        private Vector3 OnScreen(Transform cam, Vector3 world)
        {
            Vector3 d = cam.InverseTransformPoint(world);
            if (d.z < 0.05f) d.z = 0.05f;
            float k = Mathf.Tan(FirstPersonViewModel.ViewModelFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(_p.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 s = new Vector3(d.x / d.z * k, d.y / d.z * k, 1f);
            return cam.TransformPoint(s.normalized * 0.85f);
        }
    }
}
