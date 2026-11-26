using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The lobby go-kart as the other players see it: the driver's last pose extrapolated along its velocity and yaw
    /// rate (by the time since it arrived, at most 0.25 s) and smoothed, so a 30 Hz stream drifts smoothly; a pose
    /// from an older epoch (before a reset) or a stale one far away right after the driver got in is ignored. Also
    /// hangs the driver's seated pose (<see cref="KartRider"/>) on whoever drives, on every client. The pack carries
    /// the heading only, so remote and parked karts lean with the ground under them here (climbs, banked corners).
    /// </summary>
    public partial class LobbyKart
    {
        private KartPack _pack;
        private float _packAt, _remoteSince;
        private KartRider _rider;
        private NetPlayer _riderPlayer;
        private Quaternion _lean = Quaternion.identity;
        private Vector3 _leanAt = new(float.NaN, 0f, 0f);
        private float _leanYaw;

        private void TickRemote(NetPlayer driver, KartState st, float dt)
        {
            var pk = driver.KartDrive.Value;
            bool usable = pk.Valid && pk.Index == Index && pk.Epoch == st.Epoch &&
                          !(Time.time - _remoteSince < 0.6f && (pk.Pos - transform.position).sqrMagnitude > 4f);
            if (!usable)
            {
                // Nothing fresh yet: stay where the kart stands (a reset puts it on the spot).
                if (st.Epoch != _epochSeen) transform.SetPositionAndRotation(st.Pos, Quaternion.Euler(0f, _yaw = st.Yaw, 0f));
                ShownSpeed = Mathf.MoveTowards(ShownSpeed, 0f, dt * 6f);
                return;
            }
            if (!pk.Equals(_pack))
            {
                _pack = pk;
                _packAt = Time.time;
            }
            float lead = Mathf.Clamp(Time.time - _packAt, 0f, 0.25f) + 0.02f;
            Vector3 target = pk.Pos + pk.Velocity * lead;
            float targetYaw = pk.YawDeg + pk.YawRateDeg * lead;
            Vector3 before = transform.position;
            bool snap = (target - before).sqrMagnitude > 9f;
            float k = snap ? 1f : 1f - Mathf.Exp(-16f * dt);
            Vector3 pos = Vector3.Lerp(before, target, k);
            _yaw = snap ? targetYaw : Mathf.LerpAngle(_yaw, targetYaw, k);
            transform.SetPositionAndRotation(pos, OnGround(pos, _yaw, dt, snap));

            float prev = ShownSpeed;
            Vector3 vel = pk.Velocity;
            ShownVelocity = vel;
            ShownSpeed = Mathf.Lerp(ShownSpeed, Vector3.Dot(vel, transform.forward), 1f - Mathf.Exp(-12f * dt));
            if (Time.time - _remoteSince > 0.6f) RemoteBump(Mathf.Abs(prev), Mathf.Abs(ShownSpeed));
            ShownSteer = Mathf.Lerp(ShownSteer, pk.Steer, 1f - Mathf.Exp(-14f * dt));
            ShownYawRate = pk.YawRateDeg;
            Rpm01 = Mathf.Lerp(Rpm01, pk.Rpm / 255f, 1f - Mathf.Exp(-10f * dt));
            Slip01 = Mathf.Lerp(Slip01, pk.Slip / 255f, 1f - Mathf.Exp(-12f * dt));
            Throttle = pk.Throttle / 100f;
            HandbrakeOn = (pk.Flags & KartPack.FlagHandbrake) != 0;
            BrakeOn = (pk.Flags & KartPack.FlagBraking) != 0;
            SlidingNow = (pk.Flags & KartPack.FlagSliding) != 0;
            float sp = new Vector2(vel.x, vel.z).magnitude;
            SlipAngleShown = sp > 0.3f ? Vector3.SignedAngle(transform.forward, vel, Vector3.up) : 0f;
        }

        /// <summary>
        /// Heading <paramref name="yaw"/> tilted to the ground under the kart (the plane through the ground below the
        /// front axle and both rear wheels), smoothed; level when the rays miss or the ground is too steep.
        /// </summary>
        private Quaternion OnGround(Vector3 pos, float yaw, float dt, bool snap)
        {
            var flat = Quaternion.Euler(0f, yaw, 0f);
            if ((pos - _leanAt).sqrMagnitude > 1e-6f || Mathf.Abs(Mathf.DeltaAngle(yaw, _leanYaw)) > 0.05f)
            {
                _leanAt = pos;
                _leanYaw = yaw;
                Quaternion want = Quaternion.identity;
                if (Floor(pos + flat * new Vector3(0f, 0f, KartPhysics.FrontAxle), out Vector3 f) &&
                    Floor(pos + flat * new Vector3(-KartModel.RearTrack, 0f, -KartPhysics.RearAxle), out Vector3 l) &&
                    Floor(pos + flat * new Vector3(KartModel.RearTrack, 0f, -KartPhysics.RearAxle), out Vector3 r))
                {
                    Vector3 n = Vector3.Cross(f - l, r - l).normalized;
                    if (n.y > 0.7f) want = Quaternion.FromToRotation(Vector3.up, n);
                }
                _lean = snap ? want : Quaternion.Slerp(_lean, want, 1f - Mathf.Exp(-12f * dt));
            }
            return _lean * flat;
        }

        private static bool Floor(Vector3 p, out Vector3 hit)
        {
            bool ok = Physics.Raycast(p + Vector3.up * 0.8f, Vector3.down, out RaycastHit h, 2f, KartCollide.LevelMask, QueryTriggerInteraction.Ignore);
            hit = ok ? h.point : p;
            return ok;
        }

        /// <summary>Puts the seated pose on the current driver (and lets the previous one stand up).</summary>
        private void UpdateRider(NetPlayer driver)
        {
            if (driver == _riderPlayer && (_rider != null || driver == null)) return;
            if (_rider != null) _rider.Release();
            _rider = null;
            _riderPlayer = driver;
            if (driver == null || driver.Character == null) return;
            _rider = driver.Character.gameObject.AddComponent<KartRider>();
            _rider.Init(this, driver);
        }
    }
}
