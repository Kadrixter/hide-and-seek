using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The driver's body in the lobby go-kart, on every client: added to the driver's character while he drives and
    /// run after the character animator (order 106), it poses the rig anchored to the kart seat (not to the player
    /// root, so the body sits still in the kart however the network moves the root): hips on the seat pan, back a
    /// little reclined, legs stretched to the pedals (two-bone IK, knees up), both fists on the steering wheel rim
    /// at a quarter to three (they turn with it), the head turned a little into the corner and the upper body leaning
    /// into it. Blends in over ~0.4 s when he gets in and out again when he gets out (then removes itself). Remote
    /// drivers' footsteps are muted meanwhile (the root moves at driving speed).
    /// </summary>
    [DefaultExecutionOrder(106)]
    public sealed class KartRider : MonoBehaviour
    {
        private const float BlendTime = 0.4f;
        private LobbyKart _kart;
        private NetPlayer _p;
        private BlockyCharacter _c;
        private float _w, _lean, _look;
        private bool _wanted = true;
        /// <summary>Two-seater passenger: the right seat, hands on the grab bar.</summary>
        private bool _passenger;
        private FootstepAudio _steps;
        private float _stepsVolume = -1f;

        /// <summary>Seated weight now (0..1), for tests.</summary>
        public float Weight => _w;

        public void Init(LobbyKart kart, NetPlayer p, bool passenger = false)
        {
            _kart = kart;
            _p = p;
            _passenger = passenger;
            _c = GetComponent<BlockyCharacter>();
            if (!p.IsOwner)
            {
                _steps = p.GetComponent<FootstepAudio>();
                if (_steps != null) { _stepsVolume = _steps.MasterVolume; _steps.MasterVolume = 0f; }
            }
        }

        /// <summary>The driver got out: blend back to the animated body, then go.</summary>
        public void Release() => _wanted = false;

        private void Finish()
        {
            if (_steps != null && _stepsVolume >= 0f) _steps.MasterVolume = _stepsVolume;
            _steps = null;
            Destroy(this);
        }

        private void LateUpdate()
        {
            if (_kart == null || _kart.Parts == null || _c == null || _p == null || !_c.IsBuilt || _c.IsRagdoll) { Finish(); return; }
            float dt = Time.deltaTime;
            _w = Mathf.MoveTowards(_w, _wanted ? 1f : 0f, dt / BlendTime);
            if (!_wanted && _w <= 0f) { Finish(); return; }

            var seat = _passenger ? _kart.Parts.PassengerSeat : _kart.Parts.Seat;
            if (seat == null) { Finish(); return; }
            var pose = SeatPose(seat, dt);
            // Seat space -> character root space (the joints below the pelvis are local and stay as they are).
            var root = _c.transform;
            Quaternion toRoot = Quaternion.Inverse(root.rotation) * seat.rotation;
            pose.PelvisPos = root.InverseTransformPoint(seat.position) + toRoot * pose.PelvisPos;
            pose.Pelvis = toRoot * pose.Pelvis;

            var cur = CharacterPose.Capture(_c);
            pose.PropR = cur.PropR;
            float k = _w * _w * (3f - 2f * _w);
            CharacterPose.Lerp(cur, pose, k).Apply(_c);
        }

        /// <summary>The driving pose in seat space (origin on the seat pan, +Z forward).</summary>
        private CharacterPose SeatPose(Transform seat, float dt)
        {
            float lat = _kart.ShownSpeed * _kart.ShownYawRate * Mathf.Deg2Rad;
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(lat * 1.6f, -9f, 9f), 1f - Mathf.Exp(-6f * dt));
            _look = Mathf.Lerp(_look, Mathf.Clamp(_kart.ShownSteer * 0.9f + _kart.SlipAngleShown * 0.35f, -30f, 30f), 1f - Mathf.Exp(-5f * dt));

            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.115f, 0.0f);
            p.Pelvis = Quaternion.Euler(-7f, 0f, 0f);
            p.Torso = Quaternion.Euler(-2f, _look * 0.15f, -_lean);
            p.Head = Quaternion.Euler(6f, _look, _lean * 0.5f);

            // Legs out to the pedals, knees up and a little apart.
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? 1f : -1f;
                p.SolveLeg(leg, new Vector3(side * 0.14f, 0.04f, 0.64f), new Vector3(side * 0.25f, 1f, 0.3f));
            }

            if (_passenger && _kart.Parts.Grab != null)
            {
                // Passenger: both hands on the grab bar's grips, the head free to look round a little more.
                p.Head = Quaternion.Euler(4f, _look * 1.3f, _lean * 0.5f);
                for (int arm = 0; arm < 2; arm++)
                {
                    float side = arm == 0 ? 1f : -1f;
                    Vector3 grip = seat.InverseTransformPoint(_kart.Parts.Grab.TransformPoint(new Vector3(side * 0.09f, 0f, 0f)));
                    p.SolveArm(arm, grip, new Vector3(side * 1f, -0.7f, -0.4f));
                }
                return p;
            }
            // Fists on the rim at a quarter to three, turning with the wheel.
            var wheel = _kart.Parts.SteeringWheel;
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? 1f : -1f;
                Vector3 grip = seat.InverseTransformPoint(wheel.TransformPoint(new Vector3(side * KartModel.WheelRimRadius, 0.015f, -0.01f)));
                p.SolveArm(arm, grip, new Vector3(side * 1f, -0.7f, -0.4f));
            }
            return p;
        }
    }
}
