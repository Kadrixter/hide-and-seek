using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Per-player glue of the fence wire on every client (added by MatchManager.RcWire): plays tearing a piece of wire
    /// off a fence from the synced <see cref="NetPlayer.WirePull"/> (body, the strand bent between the fists and torn
    /// off, sounds; the puller himself: walks up to the mesh, faces it, movement stops, first-person fists on the
    /// strand, letting go of E or moving stops it) and the antenna swap from <see cref="NetPlayer.AntennaSwapAt"/>
    /// (third / first person, sounds); keeps the antennas of the transmitter / car models this player shows in the
    /// right look (stock / pulled out / wire).
    /// </summary>
    [DefaultExecutionOrder(-24)]
    public class RcWireDriver : MonoBehaviour
    {
        private NetPlayer _p;
        private BlockyCharacterActions _bca;
        private RcWireTracker _look;
        private Transform _strand;
        private float _lastPull = -1f, _lastSwap = -1f;
        private bool _fppOn, _cancelSent, _walked;
        private Vector3 _startPos, _gripR, _gripL, _normalToPlayer;
        private float _startYaw;
        private int _pullSpot = -1;

        /// <summary>Tests: hold E without a keyboard.</summary>
        public static bool TestHoldE;

        /// <summary>Wire-pull time now (-1 = none) / antenna-swap time now (-1 = none), on this peer.</summary>
        public float PullTime => _lastPull;
        public float SwapTime => _lastSwap;
        /// <summary>The torn strand in the hands (tests).</summary>
        public Transform Strand => _strand != null && _strand.gameObject.activeSelf ? _strand : null;

        private void Awake() => _p = GetComponent<NetPlayer>();

        private void OnDestroy()
        {
            if (_strand != null) Destroy(_strand.gameObject);
        }

        private void Update()
        {
            var mm = MatchManager.Instance;
            if (_p == null || !_p.IsSpawned || mm == null) return;
            if (_bca == null) _bca = _p.Actions as BlockyCharacterActions;
            if (_bca == null) return;

            // ---- tearing wire
            var w = _p.WirePull.Value;
            var spots = WireSpots.All;
            float pt = -1f;
            if (w.Spot >= 0 && w.Spot < spots.Count && _p.Life.Value == LifeState.Alive)
                pt = Mathf.Clamp((float)(mm.Now - w.Start), 0f, WirePullTimeline.Duration);
            if (pt >= WirePullTimeline.Duration) pt = -1f;
            if (pt >= 0f)
            {
                var s = spots[w.Spot];
                if (_pullSpot != w.Spot || _lastPull < 0f) BeginPull(s, w);
                _bca.SetWirePull(pt, _gripR, _gripL);
                PullSounds(_lastPull, pt);
                if (_p.IsOwner) OwnerPull(s, w, pt);
            }
            else if (_lastPull >= 0f) EndPull();
            _lastPull = pt;

            // ---- antenna swap
            float st = _p.AntennaSwapTime;
            if (!AntennaSwapTimeline.Running(st)) st = -1f;
            _bca.SetAntennaSwap(st);
            if (_p.IsOwner) (_p.ViewModel as FirstPersonViewModel)?.SetAntennaSwap(st);
            if (st >= 0f && _lastSwap < 0f) _look?.Rescan();
            SwapSounds(_lastSwap, st);
            _lastSwap = st;

            // ---- antenna looks of the transmitter / car this player shows
            _look ??= new RcWireTracker(transform);
            int slot = mm.RcSlotOf(_p);
            bool wired = mm.RcIsWired(slot);
            _look.Apply(AntennaSwapTimeline.Look(st, wired), AntennaSwapTimeline.LiftShown(st), wired);
        }

        // ================================================================ tearing wire

        private void BeginPull(WireSpots.Spot s, WirePullState w)
        {
            _pullSpot = w.Spot;
            _cancelSent = false;
            _walked = false;
            _startPos = transform.position;
            _startYaw = transform.eulerAngles.y;
            _normalToPlayer = s.Normal * w.Side;
            Vector3 c = s.Grip(w.Along, w.Side);
            Vector3 right = Vector3.Cross(Vector3.up, -_normalToPlayer).normalized;
            _gripR = c + right * 0.08f;
            _gripL = c - right * 0.1f + Vector3.up * 0.04f;
            if (_strand == null)
            {
                _strand = WireModel.CreateLoose(null, 0);
                _strand.name = "WireTornOff";
            }
            _strand.gameObject.SetActive(false);
            if (_p.IsOwner) _p.InputReader?.ResetStance();
            Debug.Log($"[RcWire] {_p.Nick} starts tearing wire at {s.Name}");
        }

        private void EndPull()
        {
            _pullSpot = -1;
            _bca.SetWirePull(-1f, Vector3.zero, Vector3.zero);
            if (_strand != null) _strand.gameObject.SetActive(false);
            if (_p.IsOwner && _fppOn) (_p.ViewModel as FirstPersonViewModel)?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
            _fppOn = false;
        }

        private void PullSounds(float prev, float t)
        {
            Vector3 at = (_gripR + _gripL) * 0.5f;
            if (prev < WirePullTimeline.Reach - 0.05f && t >= WirePullTimeline.Reach - 0.05f) WireSounds.Rattle(at);
            foreach (float b in WirePullTimeline.Bends)
                if (prev < b - 0.12f && t >= b - 0.12f) WireSounds.Creak(at);
            if (prev < 1.6f && t >= 1.6f) WireSounds.Rattle(at, 0.5f);
            if (prev < WirePullTimeline.Snap && t >= WirePullTimeline.Snap) WireSounds.Snap(at);
        }

        private void OwnerPull(WireSpots.Spot s, WirePullState w, float t)
        {
            var motor = _p.Motor;
            if (motor == null) return;
            motor.SpeedScale = 0f;
            float yaw = Quaternion.LookRotation(-_normalToPlayer).eulerAngles.y;
            // Steps up to the mesh in the first moment (the hands need to reach it), facing it.
            if (t < 0.4f)
            {
                float g = WirePullTimeline.S(t, 0f, 0.35f);
                Vector3 stand = s.Stand(w.Along, w.Side);
                Vector3 pos = Vector3.Lerp(_startPos, new Vector3(stand.x, _startPos.y, stand.z), g);
                motor.Teleport(pos, Mathf.LerpAngle(_startYaw, yaw, g), motor.LookPitch);
            }
            else if (!_walked)
            {
                _walked = true;
                motor.SetLook(yaw, motor.LookPitch);
            }
            // Let go of E, walk or jump = stop (the piece stays in the fence).
            var input = _p.InputReader;
            var kb = Keyboard.current;
            bool holding = TestHoldE || (kb != null && kb.eKey.isPressed);
            bool moved = input != null && (input.Move.sqrMagnitude > 0.2f || input.JumpHeld);
            if (!_cancelSent && t > 0.15f && t < WirePullTimeline.Snap && (!holding || moved))
            {
                _cancelSent = true;
                _p.RequestWirePullCancelRpc();
            }
        }

        private void LateUpdate()
        {
            if (_p == null || _bca == null) return;
            float t = _lastPull;
            if (t >= 0f && _strand != null) PoseStrand(t);
            if (_p.IsOwner && t >= 0f) OwnerView(t);
        }

        /// <summary>The strand: bent back and forth between the fists, torn off at the snap, then in the right fist.</summary>
        private void PoseStrand(float t)
        {
            bool show = t > WirePullTimeline.Reach - 0.05f && t < WirePullTimeline.Pocket + 0.05f;
            if (_strand.gameObject.activeSelf != show) _strand.gameObject.SetActive(show);
            if (!show) return;
            Vector3 hand = _bca.RightHandWorld;
            Vector3 right = Vector3.Cross(Vector3.up, -_normalToPlayer).normalized;
            if (t < WirePullTimeline.Snap)
            {
                // Still in the mesh: the bottom end at the left fist, through the right fist and out past it, bending.
                float bend = WirePullTimeline.Bend(t);
                Vector3 dir = (hand - _gripL).normalized;
                if (dir.sqrMagnitude < 0.5f) dir = right;
                dir = Quaternion.AngleAxis(25f * bend, Vector3.Cross(dir, _normalToPlayer).normalized) * dir;
                var rot = Quaternion.FromToRotation(Vector3.up, dir);
                _strand.SetPositionAndRotation(_gripL - _normalToPlayer * 0.01f - dir * 0.03f, rot);
            }
            else
            {
                // Torn off: held by the grip point, swinging up into view and then down to the pocket.
                float k = WirePullTimeline.S(t, WirePullTimeline.Snap, WirePullTimeline.Snap + 0.35f);
                Vector3 up = Vector3.Slerp(right, (Vector3.up * 0.8f + _normalToPlayer * -0.4f + right * 0.4f).normalized, k);
                var rot = Quaternion.FromToRotation(Vector3.up, up);
                _strand.SetPositionAndRotation(hand - rot * WireModel.GripPoint, rot);
            }
        }

        /// <summary>Puller: the view on the hands at the mesh, first-person fists where the body's fists are.</summary>
        private void OwnerView(float t)
        {
            var motor = _p.Motor;
            var cam = _p.Camera != null ? _p.Camera.transform : null;
            if (motor == null || cam == null) return;
            Vector3 eye = cam.position;
            Vector3 look = t < WirePullTimeline.Snap + 0.1f ? (_gripR + _gripL) * 0.5f : _bca.RightHandWorld;
            Vector3 to = look - eye;
            float want = Mathf.Clamp(Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg - 4f, -60f, 20f);
            if (t > WirePullTimeline.Pocket) want = Mathf.Lerp(want, -5f, WirePullTimeline.S(t, WirePullTimeline.Pocket, WirePullTimeline.Duration));
            motor.SetLook(motor.LookYaw, Mathf.LerpAngle(motor.LookPitch, want, 1f - Mathf.Exp(-6f * Time.deltaTime)));

            var fpv = _p.ViewModel as FirstPersonViewModel;
            var ch = _p.Character;
            bool on = fpv != null && ch != null && ch.IsBuilt && ch.IsFirstPerson && t > 0.05f && t < WirePullTimeline.Pocket + 0.05f;
            if (!on)
            {
                if (_fppOn) fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                _fppOn = false;
                return;
            }
            _fppOn = true;
            Vector3 r = OnScreen(cam, _bca.RightHandWorld), l = OnScreen(cam, _bca.LeftHandWorld);
            float leftOn = 1f - WirePullTimeline.S(t, WirePullTimeline.Snap, WirePullTimeline.Snap + 0.3f);
            Vector3 restL = cam.TransformPoint(new Vector3(-0.3f, -0.7f, 0.3f));
            fpv.SetOverrideHands(true, r, Quaternion.LookRotation(-_normalToPlayer, Vector3.up), Vector3.Lerp(restL, l, leftOn), HeldItem.None);
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

        // ================================================================ antenna swap

        private void SwapSounds(float prev, float t)
        {
            if (t < 0f) return;
            Vector3 at = transform.position + transform.rotation * new Vector3(-0.1f, 1.2f, 0.35f);
            if (prev < AntennaSwapTimeline.Grab && t >= AntennaSwapTimeline.Grab) WireSounds.Twist(at);
            if (prev < AntennaSwapTimeline.YankEnd - 0.06f && t >= AntennaSwapTimeline.YankEnd - 0.06f) WireSounds.Pop(at);
            if (prev < AntennaSwapTimeline.Release + 0.55f && t >= AntennaSwapTimeline.Release + 0.55f)
                WireSounds.Clink(_bca.ThrownAntenna ?? transform.position + transform.rotation * new Vector3(-1.2f, 0.05f, 0.6f));
            if (prev < AntennaSwapTimeline.Pocket && t >= AntennaSwapTimeline.Pocket) WireSounds.Rattle(at, 0.25f);
            if (prev < AntennaSwapTimeline.InsertEnd - 0.2f && t >= AntennaSwapTimeline.InsertEnd - 0.2f) WireSounds.Seat(at);
            if (prev < AntennaSwapTimeline.InsertEnd + 0.05f && t >= AntennaSwapTimeline.InsertEnd + 0.05f) WireSounds.Twist(at);
        }
    }
}
