using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Per-player glue of pulling a rod out of rubble, on every client (added by MatchManager.Rods): reads the rod
    /// state (puller + server start time) and plays the same 5 s on everybody's screen - the body
    /// (<see cref="BlockyCharacterActions.SetRodPull"/>), the rod sliding out in jerks and wobbling, then in the hands
    /// and over the shoulder, dust, falling bits of concrete and sounds. For the puller himself: walks up to the
    /// spot, movement stops, the camera rides the head with a jolt on every yank, first-person fists on the rod;
    /// moving, jumping or clicking stops the pull (the server keeps the yanks done).
    /// </summary>
    [DefaultExecutionOrder(-25)]
    public class RodPullDriver : MonoBehaviour
    {
        private NetPlayer _p;
        private BlockyCharacterActions _bca;
        private HeadBob _bob;
        private int _rodId = -1;
        private float _lastTl = -1f, _siftAt;
        private bool _cancelSent, _fppOn, _ripCaptured;
        private Vector3 _foot, _ripBase, _ripAxis, _startPos;
        private float _startYaw;

        /// <summary>Timeline time of the pull now (-1 = not pulling); tests.</summary>
        public float Timeline { get; private set; } = -1f;
        public bool Pulling => _rodId >= 0;

        private static readonly Vector3 HeldCenter = new Vector3(0.04f, 0.98f, 0.4f), HeldDir = new Vector3(0.85f, 0.4f, 0.1f);
        private static readonly Vector3 ShoulderCenter = new Vector3(0.3f, 1.6f, 0.05f);
        private static readonly Vector3 StowCenter = new Vector3(0.16f, 1.28f, -0.24f), StowDir = new Vector3(0.12f, 1f, -0.1f);

        /// <summary>Tests (-rodpullfilm): plays the pull of <see cref="FilmRod"/> on <see cref="FilmPlayer"/> at <see cref="FilmTime"/>, locally.</summary>
        public static NetPlayer FilmPlayer;
        public static int FilmRod = -1;
        public static float FilmTime = -1f;

        private void Awake() => _p = GetComponent<NetPlayer>();

        private void Update()
        {
            var mm = MatchManager.Instance;
            if (_p == null || !_p.IsSpawned || mm == null) return;
            if (_bca == null) _bca = _p.Actions as BlockyCharacterActions;
            if (_bca == null) return;

            int found = -1;
            RodState r = default;
            bool film = FilmPlayer == _p && FilmTime >= 0f;
            for (int i = 0; i < mm.Rods.Count; i++)
                if ((film ? mm.Rods[i].Id == FilmRod : mm.Rods[i].Puller == _p.NetworkObjectId) && mm.Rods[i].Embedded) { found = i; r = mm.Rods[i]; break; }
            float tl = found >= 0 ? (film ? FilmTime : RodPullTimeline.Map((float)(mm.Now - r.PullStart), r.Done)) : -1f;
            if (found < 0 || tl >= RodPullTimeline.Duration || _p.Life.Value != LifeState.Alive)
            {
                if (_rodId >= 0) Stop(mm);
                return;
            }
            var view = mm.RodViewOf(r.Id);
            if (_rodId != r.Id) Begin(r);
            tl = Mathf.Max(0f, tl);
            Timeline = tl;

            // ---- the rod
            Vector3 e = r.Entry, axis0 = r.Axis;
            Vector3 f = r.Stand - e;
            f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : -transform.forward;
            Vector3 gripR, gripL;
            if (view != null)
            {
                if (tl < RodPullTimeline.Rip)
                {
                    float o = RodPullTimeline.Out(tl, r.Done);
                    float strain = RodPullTimeline.S(tl, RodPullTimeline.GripEnd, RodPullTimeline.BraceEnd);
                    float tilt = RodPullTimeline.Wobble(tl) + 7f * RodPullTimeline.Yank(tl) + 4f * strain;
                    Vector3 a = (axis0 + f * Mathf.Tan(tilt * Mathf.Deg2Rad)).normalized;
                    view.SetPose(e - a * (RodPullTimeline.Buried * (1f - o)), a);
                    _ripCaptured = false;
                }
                else
                {
                    if (!_ripCaptured)
                    {
                        _ripCaptured = true;
                        Vector3 a = (axis0 + f * Mathf.Tan(10f * Mathf.Deg2Rad)).normalized;
                        _ripBase = transform.InverseTransformPoint(e + a * 0.06f);
                        _ripAxis = transform.InverseTransformDirection(a);
                    }
                    HeldPose(tl, out var baseRoot, out var dirRoot);
                    view.SetPose(transform.TransformPoint(baseRoot), transform.TransformDirection(dirRoot));
                }
                view.SetRodVisible(tl < RodPullTimeline.Duration - 0.12f);
                gripR = view.PointFromTop(0.06f);
                gripL = view.PointFromTop(0.24f);
            }
            else
            {
                gripR = e + axis0 * 0.4f;
                gripL = e + axis0 * 0.25f;
            }
            _bca.SetRodPull(tl, gripR, gripL, _foot);
            Sounds(tl, e, axis0);
            _lastTl = tl;

            if (_p.IsOwner) OwnerUpdate(r, tl, e);
        }

        /// <summary>Rod pose after the rip, in the puller's root space: up into both hands, then over the shoulder.</summary>
        private void HeldPose(float tl, out Vector3 baseRoot, out Vector3 dirRoot)
        {
            float L = RodModel.Length;
            float k1 = RodPullTimeline.S(tl, RodPullTimeline.Rip, RodPullTimeline.Rip + 0.32f);
            float k2 = RodPullTimeline.S(tl, RodPullTimeline.StowStart, RodPullTimeline.Duration - 0.12f);
            Vector3 ripC = _ripBase + _ripAxis * (L * 0.5f) + _ripAxis * 0.14f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((tl - RodPullTimeline.Rip) / 0.25f));
            Vector3 c = Vector3.Lerp(ripC, HeldCenter, k1);
            Vector3 d = Vector3.Slerp(_ripAxis, HeldDir.normalized, k1);
            if (k2 > 0f)
            {
                // over the right shoulder: quadratic curve through a point above it
                Vector3 m = 2f * ShoulderCenter - 0.5f * (HeldCenter + StowCenter);
                c = (1f - k2) * (1f - k2) * HeldCenter + 2f * k2 * (1f - k2) * m + k2 * k2 * StowCenter;
                d = Vector3.Slerp(HeldDir.normalized, StowDir.normalized, k2);
            }
            dirRoot = d.normalized;
            baseRoot = c - dirRoot * (L * 0.5f);
        }

        private void Begin(RodState r)
        {
            _rodId = r.Id;
            _lastTl = -1f;
            _cancelSent = false;
            _ripCaptured = false;
            _startPos = transform.position;
            _startYaw = transform.eulerAngles.y;
            // braced foot: up on the heap between the stand point and the rod, a little to the right
            Vector3 e = r.Entry;
            Vector3 f = r.Stand - e;
            f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, -f);
            _foot = e + f * 0.3f + right * 0.13f;
            _foot.y = r.Stand.y + Mathf.Clamp(e.y - r.Stand.y - 0.06f, 0.08f, 0.32f);
            if (_p.IsOwner) _p.InputReader?.ResetStance();
            Debug.Log($"[RodPull] {_p.Nick} starts pulling rod {r.Id} (done {r.Done})");
        }

        private void Stop(MatchManager mm)
        {
            var view = mm.RodViewOf(_rodId);
            if (view != null) view.Unsynced();
            _rodId = -1;
            Timeline = -1f;
            _bca.SetRodPull(-1f, Vector3.zero, Vector3.zero, Vector3.zero);
            if (_p.IsOwner)
            {
                var fpv = _p.ViewModel as FirstPersonViewModel;
                if (_fppOn) fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                _fppOn = false;
                if (_bob != null)
                {
                    _bob.ExtraEuler = Vector3.zero;
                    _bob.ExtraPosition = Vector3.zero;
                    _bob.EyeOverrideWeight = 0f;
                }
            }
        }

        private void Sounds(float tl, Vector3 e, Vector3 axis)
        {
            float prev = _lastTl;
            if (prev < 0f) return;
            for (int k = 0; k < RodPullTimeline.Jerks.Length; k++)
                if (prev < RodPullTimeline.Jerks[k] && tl >= RodPullTimeline.Jerks[k]) RubbleFx.Yank(e, axis, (k + 1f) / RodPullTimeline.Jerks.Length);
            if (prev < RodPullTimeline.Rip && tl >= RodPullTimeline.Rip)
            {
                RubbleFx.Rip(e, axis);
                GameAudio.PlayAt(GameSfx.HiderPain, transform.position + Vector3.up * 1.4f, 0.3f);
            }
            if (prev < RodPullTimeline.StowStart + 0.2f && tl >= RodPullTimeline.StowStart + 0.2f) GameAudio.PlayAt(GameSfx.KnifeSwing, transform.position + Vector3.up * 1.4f, 0.3f);
            if (tl > RodPullTimeline.BraceEnd && tl < RodPullTimeline.Rip && Time.time >= _siftAt)
            {
                _siftAt = Time.time + Random.Range(0.18f, 0.35f);
                RubbleFx.Sift(e);
            }
        }

        // ---------------------------------------------------------------- puller only

        private void OwnerUpdate(RodState r, float tl, Vector3 e)
        {
            var motor = _p.Motor;
            if (motor == null) return;
            motor.SpeedScale = 0f;
            Vector3 to = e - r.Stand;
            to.y = 0f;
            float yaw = to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to).eulerAngles.y : transform.eulerAngles.y;
            // walk up to the spot in the first 0.4 s
            float g = RodPullTimeline.S(tl, 0f, 0.4f);
            if (tl < 0.45f)
            {
                Vector3 pos = Vector3.Lerp(_startPos, new Vector3(r.Stand.x, _startPos.y, r.Stand.z), g);
                motor.Teleport(pos, Mathf.LerpAngle(_startYaw, yaw, g), motor.LookPitch);
            }
            else if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, yaw)) > 0.5f) motor.SetLook(yaw, motor.LookPitch);

            // stop: walking, jumping or clicking (after the first moment, so the E press itself does not count)
            var input = _p.InputReader;
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (!_cancelSent && tl > 0.35f && tl < RodPullTimeline.Rip && input != null && (input.Move.sqrMagnitude > 0.2f || input.JumpHeld || click))
            {
                _cancelSent = true;
                _p.RequestRodPullCancelRpc();
            }
        }

        private void LateUpdate()
        {
            if (_p == null || !_p.IsOwner || _rodId < 0 || _bca == null) return;
            float tl = Timeline;
            var mm = MatchManager.Instance;
            var view = mm != null ? mm.RodViewOf(_rodId) : null;
            if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
            float body = RodPullTimeline.S(tl, 0f, 0.35f) * (1f - RodPullTimeline.S(tl, RodPullTimeline.Duration - 0.35f, RodPullTimeline.Duration));
            Vector3 grip = view != null ? view.PointFromTop(0.15f) : transform.position + transform.forward * 0.6f + Vector3.up * 0.6f;
            var ch = _p.Character;
            if (_bob != null && ch != null && ch.Head != null)
            {
                // Eye rides the animated head (crouch, lean back on the yanks), a jolt on each yank and on the rip.
                var parent = _bob.transform.parent;
                Vector3 eyeW = ch.Head.position + transform.rotation * new Vector3(0f, 0.27f, 0.06f);
                Vector3 local = parent != null ? parent.InverseTransformPoint(eyeW) : eyeW - transform.position;
                _bob.EyeOverride = local.y;
                _bob.EyeOverrideWeight = body;
                _bob.ExtraPosition = new Vector3(local.x, 0f, local.z - 0.15f) * body;
                float yank = RodPullTimeline.Yank(tl);
                float rip = tl > RodPullTimeline.Rip ? Mathf.Exp(-(tl - RodPullTimeline.Rip) * 6f) : 0f;
                float strain = RodPullTimeline.S(tl, RodPullTimeline.GripEnd, RodPullTimeline.BraceEnd) * (1f - RodPullTimeline.S(tl, RodPullTimeline.Rip, RodPullTimeline.Rip + 0.1f));
                _bob.ExtraEuler = new Vector3(-3f * yank - 7f * rip + Random.Range(-1f, 1f) * (0.5f * strain + 1.5f * rip), Random.Range(-1f, 1f) * 0.6f * strain, 4f * rip + Random.Range(-1f, 1f) * 0.4f * strain);
                // look at the rod while pulling, back to level after the rip
                Vector3 to = grip - eyeW;
                // (motor pitch: negative = looking down) a little below the hands, so rod and rubble are in view
                float want = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg - 6f;
                float level = RodPullTimeline.S(tl, RodPullTimeline.Rip + 0.2f, RodPullTimeline.Duration);
                want = Mathf.Lerp(want, -5f, level);
                var motor = _p.Motor;
                float pitch = Mathf.LerpAngle(motor.LookPitch, want, 1f - Mathf.Exp(-7f * Time.deltaTime));
                motor.SetLook(transform.eulerAngles.y, pitch);
            }
            FirstPersonHands(tl, view);
        }

        private void FirstPersonHands(float tl, RodView view)
        {
            var fpv = _p.ViewModel as FirstPersonViewModel;
            var ch = _p.Character;
            var camT = _p.Camera != null ? _p.Camera.transform : null;
            bool on = fpv != null && camT != null && view != null && ch != null && ch.IsBuilt && ch.IsFirstPerson && tl > 0.05f && tl < RodPullTimeline.Duration - 0.2f;
            if (!on)
            {
                if (_fppOn) fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                _fppOn = false;
                return;
            }
            _fppOn = true;
            float hands = RodPullTimeline.S(tl, 0.05f, RodPullTimeline.GripEnd) * (1f - RodPullTimeline.S(tl, RodPullTimeline.StowStart + 0.05f, RodPullTimeline.StowStart + 0.35f));
            Vector3 restR = camT.TransformPoint(new Vector3(0.28f, -0.6f, 0.3f)), restL = camT.TransformPoint(new Vector3(-0.28f, -0.6f, 0.3f));
            Vector3 r = Vector3.Lerp(restR, OnScreen(camT, view.PointFromTop(0.06f)), hands);
            Vector3 l = Vector3.Lerp(restL, OnScreen(camT, view.PointFromTop(0.24f)), hands);
            fpv.SetOverrideHands(true, r, Quaternion.LookRotation(view.Axis, -camT.forward), l, HeldItem.None);
        }

        /// <summary>World point at arm's length on the view ray to <paramref name="world"/>, corrected for the viewmodel FOV.</summary>
        private Vector3 OnScreen(Transform cam, Vector3 world)
        {
            Vector3 d = cam.InverseTransformPoint(world);
            if (d.z < 0.05f) d.z = 0.05f;
            float k = Mathf.Tan(FirstPersonViewModel.ViewModelFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(_p.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 s = new Vector3(d.x / d.z * k, d.y / d.z * k, 1f);
            return cam.TransformPoint(s.normalized * 0.74f);
        }
    }
}
