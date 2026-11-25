using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The lobby go-kart as the other players see it: the driver's last pose extrapolated along its velocity and yaw
    /// rate (by the time since it arrived, at most 0.25 s) and smoothed, so a 30 Hz stream drifts smoothly; a pose
    /// from an older epoch (before a reset) or a stale one far away right after the driver got in is ignored. Also
    /// hangs the driver's seated pose (<see cref="KartRider"/>) on whoever drives, on every client.
    /// </summary>
    public partial class LobbyKart
    {
        private KartPack _pack;
        private float _packAt, _remoteSince;
        private KartRider _rider;
        private NetPlayer _riderPlayer;

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
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, _yaw, 0f));

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
