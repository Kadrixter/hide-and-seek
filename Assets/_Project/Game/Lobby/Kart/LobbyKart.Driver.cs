using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The local driver's side of the lobby go-kart: getting in (character controller off, own body shown, chase
    /// camera on, the player root rides along under the seat), the controls (set every frame by
    /// LocalPlayerController.Kart.cs), sending the pose, the room / stuck checks that put the kart back on its spot,
    /// and getting out (a free spot beside the kart, checked against walls and furniture).
    /// </summary>
    public partial class LobbyKart
    {
        // ---- controls (frame-stamped: no input this frame = nothing pressed)
        private static float _inThrottle, _inSteer;
        private static bool _inHandbrake;
        private static int _inFrame = -10;

        /// <summary>LocalPlayerController: this frame's controls (W/S, A/D, Space).</summary>
        public static void SetInput(float throttle, float steer, bool handbrake)
        {
            _inThrottle = throttle;
            _inSteer = steer;
            _inHandbrake = handbrake;
            _inFrame = Time.frameCount;
        }

        /// <summary>The kart the local player drives right now (null = none).</summary>
        public static LobbyKart LocalKart
        {
            get
            {
                foreach (var k in All) if (k != null && k.View == Mode.Local) return k;
                return null;
            }
        }

        /// <summary>The local player drives a kart right now.</summary>
        public static bool LocalDriving => LocalKart != null;

        private NetPlayer _localDriver;
        private KartCamera _cam;
        private float _leftUntil, _leftYaw, _stuckT;
        private Vector3 _leftPos, _stuckFrom;
        private bool LeftJustNow => Time.time < _leftUntil;

        private void BeginLocalDrive(NetPlayer me)
        {
            _localDriver = me;
            _stuckT = 0f;
            _stuckFrom = transform.position;
            var cc = me.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                if (_col != null) Physics.IgnoreCollision(cc, _col, true);
            }
            me.Motor.Seated = true;
            me.SetCinematicBody(true);
            _cam = KartCamera.Begin(me, this);
            GameHud.Toast("W / S - gaz i hamulec, A / D - skręt, Spacja - ręczny (drift), R - na start, E - wysiądź", 5f);
            Debug.Log($"[Kart] {me.Nick} drives (local) from {transform.position}");
        }

        /// <summary>Back on foot: controller on, first person, the camera back in the eyes; beside the kart when asked.</summary>
        private void EndLocalDrive(bool placeOutside)
        {
            var me = _localDriver;
            _localDriver = null;
            if (_cam != null) _cam.End();
            _cam = null;
            if (me == null) return;
            me.Motor.Seated = false;
            me.SetCinematicBody(false);
            var cc = me.GetComponent<CharacterController>();
            bool free = me.Life.Value == LifeState.Alive && !me.Busy.Value;
            if (placeOutside && free && MatchManager.Instance != null && MatchManager.Instance.Phase.Value == MatchPhase.Lobby)
            {
                Vector3 exit = FindExit();
                me.TeleportLocal(exit, _yaw);
                Debug.Log($"[Kart] {me.Nick} gets out at {exit}");
            }
            if (cc != null)
            {
                if (_col != null) Physics.IgnoreCollision(cc, _col, false);
                if (free) cc.enabled = true;
            }
        }

        /// <summary>Kart gone (scene unloaded) while driving: give the player his body back where he is.</summary>
        private void DriverDestroyed()
        {
            if (View == Mode.Local) EndLocalDrive(false);
            if (_rider != null) _rider.Release();
        }

        /// <summary>E while driving: get out (the server frees the kart; the kart stays where it stopped).</summary>
        public void RequestExit()
        {
            if (View != Mode.Local) return;
            _leftUntil = Time.time + 1.5f;
            _leftPos = Phys.Pos;
            _leftYaw = Phys.Yaw;
            MatchManager.Instance?.KartRequestRpc((byte)Index, false, Phys.Pos, Phys.Yaw);
            View = Mode.Parked;
            EndLocalDrive(true);
            Debug.Log($"[Kart] exit requested at {Phys.Pos} yaw {Phys.Yaw:0}");
        }

        /// <summary>R / stuck / out of the room: the server puts the kart back on its spot (the driver stays in).</summary>
        public void RequestReset(string why)
        {
            Debug.Log($"[Kart] reset requested ({why}) at {transform.position}");
            MatchManager.Instance?.KartResetRpc((byte)Index);
            _stuckT = 0f;
        }

        private void TickLocal(NetPlayer me, KartState st, float dt)
        {
            if (st.Epoch != _epochSeen)
            {
                Phys.Reset(st.Pos, st.Yaw);
                Debug.Log($"[Kart] put back on the spot (epoch {st.Epoch})");
            }
            bool fresh = Time.frameCount - _inFrame <= 1;
            Phys.ThrottleIn = fresh ? _inThrottle : 0f;
            Phys.SteerIn = fresh ? _inSteer : 0f;
            Phys.Handbrake = fresh && _inHandbrake;
            Phys.Collide.Driver = me;
            Phys.Step(dt, transform);

            if (!OnPad(Phys.Pos))
            {
                Debug.Log($"[Kart] kart {Index} off the pad at {Phys.Pos}");
                Phys.Reset(Spot, SpotYaw);
                RequestReset("off the pad");
            }
            // Stuck: full throttle for 4 s without getting anywhere.
            if (Mathf.Abs(Phys.ThrottleIn) > 0.5f && (Phys.Pos - _stuckFrom).sqrMagnitude < 0.3f * 0.3f) _stuckT += dt;
            else { _stuckT = 0f; _stuckFrom = Phys.Pos; }
            if (_stuckT > 4f)
            {
                GameHud.Toast("Gokart utknął - wraca na swoje miejsce.", 3f);
                RequestReset("stuck");
                _stuckFrom = Phys.Pos;
            }

            _yaw = Phys.Yaw;
            transform.SetPositionAndRotation(Phys.Pos, Quaternion.Euler(0f, _yaw, 0f));
            ShownSpeed = Phys.ForwardSpeed;
            ShownVelocity = Phys.Vel;
            ShownSteer = Phys.Steer;
            ShownYawRate = Phys.YawRate;
            Rpm01 = Phys.Rpm01;
            Slip01 = Phys.Slip01;
            Throttle = Phys.ThrottleIn;
            HandbrakeOn = Phys.Handbrake;
            BrakeOn = Phys.Braking;
            SlidingNow = Phys.Sliding;
            SlipAngleShown = Phys.SlipAngle;
            me.SendKartPack(Pack(st.Epoch));
        }

        private KartPack Pack(byte epoch) => new KartPack
        {
            Pos = Phys.Pos,
            Yaw = (short)Mathf.RoundToInt(Mathf.Repeat(Phys.Yaw, 360f) * 50f),
            VelX = (short)Mathf.Clamp(Mathf.RoundToInt(Phys.Vel.x * 200f), short.MinValue, short.MaxValue),
            VelZ = (short)Mathf.Clamp(Mathf.RoundToInt(Phys.Vel.z * 200f), short.MinValue, short.MaxValue),
            YawRate = (short)Mathf.Clamp(Mathf.RoundToInt(Phys.YawRate * 10f), short.MinValue, short.MaxValue),
            Steer = (sbyte)Mathf.Clamp(Mathf.RoundToInt(Phys.Steer), -40, 40),
            Throttle = (sbyte)Mathf.RoundToInt(Mathf.Clamp(Phys.ThrottleIn, -1f, 1f) * 100f),
            Rpm = (byte)Mathf.RoundToInt(Mathf.Clamp01(Phys.Rpm01) * 255f),
            Slip = (byte)Mathf.RoundToInt(Mathf.Clamp01(Phys.Slip01) * 255f),
            Flags = (byte)((Phys.Handbrake ? KartPack.FlagHandbrake : 0) | (Phys.Sliding ? KartPack.FlagSliding : 0) |
                           (Phys.Braking ? KartPack.FlagBraking : 0) | (Phys.ForwardSpeed < -0.2f ? KartPack.FlagReverse : 0)),
            Epoch = epoch,
            Index = (byte)Index,
        };

        /// <summary>Keeps the driver's root under the seat (after every Update moved things).</summary>
        private void LateUpdate()
        {
            if (View != Mode.Local || _localDriver == null) return;
            var me = _localDriver;
            var cc = me.GetComponent<CharacterController>();
            if (cc != null && cc.enabled) cc.enabled = false; // the controller is switched back on by the generic lock code
            Vector3 seat = transform.TransformPoint(new Vector3(KartModel.SeatPos.x, 0f, KartModel.SeatPos.z));
            me.transform.SetPositionAndRotation(seat, Quaternion.Euler(0f, _yaw, 0f));
        }

        /// <summary>A free standing spot beside the kart (left, right, behind, in front), inside the room.</summary>
        public Vector3 FindExit()
        {
            Vector3[] local =
            {
                new(-1.05f, 0f, -0.25f), new(1.05f, 0f, -0.25f), new(0f, 0f, -1.4f), new(0f, 0f, 1.45f),
                new(-1.05f, 0f, 0.6f), new(1.05f, 0f, 0.6f), new(-1.1f, 0f, -1.2f), new(1.1f, 0f, -1.2f),
                new(-1.6f, 0f, -0.25f), new(1.6f, 0f, -0.25f),
            };
            int mask = KartCollide.LevelMask;
            Vector3 from = transform.TransformPoint(new Vector3(0f, 0.6f, KartModel.SeatPos.z));
            foreach (var l in local)
            {
                Vector3 p = transform.TransformPoint(l);
                p.y = transform.position.y + 0.02f;
                if (!OnPad(p)) continue;
                if (Physics.CheckCapsule(p + Vector3.up * 0.36f, p + Vector3.up * 1.5f, 0.31f, mask, QueryTriggerInteraction.Ignore)) continue;
                Vector3 to = p + Vector3.up * 0.6f - from;
                if (Physics.Raycast(from, to.normalized, to.magnitude, mask, QueryTriggerInteraction.Ignore)) continue;
                return p;
            }
            Vector3 spot = Spot + Quaternion.Euler(0f, SpotYaw, 0f) * new Vector3(-1.1f, 0.02f, 0f);
            Debug.Log($"[Kart] no free exit beside the kart at {transform.position}, using {spot}");
            return spot;
        }
    }
}
