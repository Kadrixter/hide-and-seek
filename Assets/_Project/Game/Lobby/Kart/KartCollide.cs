using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Contacts of the driven go-kart (driver's client). Walls, barriers and other karts are real Rigidbody contacts
    /// now (the kart's box, <see cref="Half"/>, lifted <see cref="BoxBottom"/> off the ground, frictionless and not
    /// bouncy, against the level and the other karts' kinematic boxes; see LobbyKart.Bumps.cs). Players on foot carry no
    /// colliders on this client: they are upright circles here, and the kart's velocity into them is taken away, so a
    /// kart stops against a player instead of running him over (his own client nudges him out of the way). Counts
    /// hits for the sound / camera shake and the tests.
    /// </summary>
    public sealed class KartCollide
    {
        public static readonly Vector3 Half = new(0.6f, 0.2f, 0.83f);
        /// <summary>Two-seater box: wider (the second seat and the wider axles), the same length.</summary>
        public static readonly Vector3 HalfDuo = new(0.88f, 0.2f, 0.83f);
        public static Vector3 HalfOf(int kart) => DriftTrackLayout.IsDuo(kart) ? HalfDuo : Half;
        public const float BoxBottom = 0.12f;
        public static readonly Vector3 Center = new(0f, BoxBottom + 0.2f, -0.02f);
        public const float PlayerRadius = 0.32f;

        /// <summary>Level layers: not the own body (8), view models (9), Ignore Raycast (2, the karts), ragdolls.</summary>
        public static int LevelMask => ~((1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9) | (1 << 2) | GameLayers.RagdollMask);

        private static PhysicsMaterial _slick;
        /// <summary>Frictionless, not bouncy: scraping a wall does not stop the kart, a hit does not throw it.</summary>
        public static PhysicsMaterial Slick => _slick != null ? _slick : _slick = new PhysicsMaterial("KartSlick")
        {
            dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        /// <summary>The driver (skipped by the player check) and the own kart.</summary>
        public NetPlayer Driver;
        public LobbyKart Self;
        /// <summary>Strongest impact speed since the last read (m/s), read and cleared by the kart for sound / shake.</summary>
        public float Impact;
        public int Hits;
        /// <summary>Kart-to-kart contacts so far (tests).</summary>
        public int KartHits;

        /// <summary>FixedUpdate on the driver's client: stop against players on foot.</summary>
        public void Players(Rigidbody rb)
        {
            var rot = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
            var inv = Quaternion.Inverse(rot);
            var mm = MatchManager.Instance;
            Vector3 c = rb.position + rot * Center;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == Driver || !o.IsSpawned || o.Life.Value == LifeState.Dead) continue;
                if (mm != null && mm.KartsVar.Value.InKart(o.NetworkObjectId)) continue; // sits in a kart: a kart contact
                Vector3 p = o.transform.position;
                if (Mathf.Abs(p.y - rb.position.y) > 1.2f) continue;
                Vector3 local = inv * (p - c);
                Vector3 hb = Self != null ? HalfOf(Self.Index) : KartCollide.Half;
                Vector3 clamped = new Vector3(Mathf.Clamp(local.x, -hb.x, hb.x), 0f, Mathf.Clamp(local.z, -hb.z, hb.z));
                Vector3 d = new Vector3(local.x, 0f, local.z) - clamped;
                float dist = d.magnitude;
                if (dist >= PlayerRadius) continue;
                Vector3 nLocal = dist > 1e-4f ? d / dist : (Mathf.Abs(local.x) / hb.x > Mathf.Abs(local.z) / hb.z ? new Vector3(Mathf.Sign(local.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z)));
                Vector3 n = rot * nLocal; // from the kart towards the player
                rb.position -= n * (PlayerRadius - dist);
                Vector3 v = rb.linearVelocity;
                float vn = Vector3.Dot(v, n);
                if (vn <= 0f) continue;
                rb.linearVelocity = (v - n * vn) * 0.9f;
                if (vn > Impact) Impact = vn;
                if (vn > 1.2f) Hits++;
            }
        }
    }
}
