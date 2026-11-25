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

        private void FixedUpdate()
        {
            if (View != Mode.Local || Phys.Body == null || Phys.Body.isKinematic) return;
            var rb = Phys.Body;
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
