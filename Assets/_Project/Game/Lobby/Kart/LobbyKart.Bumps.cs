using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Contacts of the driven kart (driver's client, Rigidbody physics): players on foot stop it
    /// (<see cref="KartCollide.Players"/>), nothing launches it (upward speed and roll / pitch rates are capped), an
    /// overturned kart goes back in its slot, and a hit on another driven kart sends that kart's driver his share of
    /// the bump (at most 4 m/s, a few times a second; the other karts are kinematic here, so the hit kart would
    /// otherwise not move on its own client).
    /// </summary>
    public partial class LobbyKart
    {
        private readonly Dictionary<int, float> _lastPush = new();
        private float _flipT;

        // ---- ride quality (tests): the chassis' acceleration along its up axis and its pitch / roll rate, per physics step
        private double _rideAccSq, _rideRateSq;
        private float _rideAccMax, _rideRateMax;
        private int _rideN;
        private Vector3 _rideV;
        private bool _rideHave;

        /// <summary>Starts a new ride-quality sample (tests).</summary>
        public void ResetRide()
        {
            _rideAccSq = _rideRateSq = 0;
            _rideAccMax = _rideRateMax = 0f;
            _rideN = 0;
            _rideHave = false;
        }

        /// <summary>Ride quality since <see cref="ResetRide"/>: RMS / max of the vertical (body-up) acceleration (m/s^2, gravity
        /// held by the springs = 0) and of the pitch / roll rate (deg/s); lower = smoother.</summary>
        public string RideReport()
        {
            int n = Mathf.Max(1, _rideN);
            return $"vertical accel rms {System.Math.Sqrt(_rideAccSq / n):0.00} max {_rideAccMax:0.0} m/s^2, pitch/roll rate rms {System.Math.Sqrt(_rideRateSq / n):0.0} max {_rideRateMax:0} deg/s over {_rideN} steps";
        }

        private void SampleRide(Rigidbody rb)
        {
            Vector3 v = rb.linearVelocity;
            if (_rideHave)
            {
                float a = Vector3.Dot(v - _rideV, rb.transform.up) / Time.fixedDeltaTime;
                Vector3 wl = rb.transform.InverseTransformDirection(rb.angularVelocity) * Mathf.Rad2Deg;
                float rate = new Vector2(wl.x, wl.z).magnitude;
                _rideAccSq += a * a;
                _rideRateSq += rate * rate;
                _rideAccMax = Mathf.Max(_rideAccMax, Mathf.Abs(a));
                _rideRateMax = Mathf.Max(_rideRateMax, rate);
                _rideN++;
            }
            _rideV = v;
            _rideHave = true;
        }

        private void FixedUpdate()
        {
            if (View != Mode.Local || Phys.Body == null || Phys.Body.isKinematic) { _rideHave = false; return; }
            var rb = Phys.Body;
            SampleRide(rb);
            Phys.Collide.Players(rb);
            Vector3 v = rb.linearVelocity;
            if (v.y > 2.5f) { v.y = 2.5f; rb.linearVelocity = v; }
            Vector3 w = rb.angularVelocity;
            Vector3 wl = rb.transform.InverseTransformDirection(w);
            if (Mathf.Abs(wl.x) > 2.5f || Mathf.Abs(wl.z) > 2.5f)
            {
                wl.x = Mathf.Clamp(wl.x, -2.5f, 2.5f);
                wl.z = Mathf.Clamp(wl.z, -2.5f, 2.5f);
                rb.angularVelocity = rb.transform.TransformDirection(wl);
            }
            _flipT = Vector3.Dot(rb.transform.up, Vector3.up) < 0.35f ? _flipT + Time.fixedDeltaTime : 0f;
            if (_flipT > 1.5f)
            {
                _flipT = 0f;
                RequestReset("overturned");
            }
        }

        private void OnCollisionEnter(Collision c)
        {
            if (View != Mode.Local || c.contactCount == 0) return;
            Vector3 n = c.GetContact(0).normal;
            float hit = Mathf.Abs(Vector3.Dot(c.relativeVelocity, n));
            if (hit > Phys.Collide.Impact) Phys.Collide.Impact = hit;
            if (hit > 1.5f) Phys.Collide.Hits++;
            var other = c.collider.GetComponentInParent<LobbyKart>();
            if (other == null || other == this) return;
            Phys.Collide.KartHits++;
            if (other.View != Mode.Parked) ReportBump(other, -n * hit * 0.6f);
        }

        /// <summary>Own kart (local) hit kart <paramref name="other"/>: pass its share of the bump to its driver.</summary>
        public void ReportBump(LobbyKart other, Vector3 dv)
        {
            if (other == null || View != Mode.Local) return;
            if (_lastPush.TryGetValue(other.Index, out float at) && Time.time - at < 0.25f) return;
            _lastPush[other.Index] = Time.time;
            var mm = MatchManager.Instance;
            if (mm == null) return;
            var st = mm.KartsVar.Value.Get(other.Index);
            var driver = st.Driven ? NetPlayer.Find(st.Driver) : null;
            if (driver == null) return;
            dv.y = 0f;
            driver.KartPushRpc((byte)other.Index, Vector3.ClampMagnitude(dv, 4f));
        }

        /// <summary>Driver's client: somebody's kart shoved this one.</summary>
        public void ExternalPush(Vector3 dv)
        {
            if (View != Mode.Local || Phys.Body == null || Phys.Body.isKinematic) return;
            dv.y = 0f;
            Phys.Body.AddForce(Vector3.ClampMagnitude(dv, 4f), ForceMode.VelocityChange);
            Phys.Body.AddTorque(Vector3.up * Mathf.Clamp(Vector3.Cross(transform.forward, dv).y * 0.4f, -1f, 1f), ForceMode.VelocityChange);
            Phys.Collide.KartHits++;
            Phys.Collide.Hits++;
            Phys.Collide.Impact = Mathf.Max(Phys.Collide.Impact, dv.magnitude);
            Debug.Log($"[Kart] kart {Index} shoved by another kart: {dv.magnitude:0.00} m/s");
        }
    }
}
