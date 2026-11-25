using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Collisions of the driven lobby go-kart (driver's client). The kart is a box (<see cref="Half"/>, lifted
    /// <see cref="BoxBottom"/> off the floor so rugs and toy bricks pass under it): every overlapping level collider
    /// pushes it out sideways (Physics.ComputePenetration), the velocity into the wall bounces back a little
    /// (<see cref="Restitution"/>) and the hit corner spins it. Other players (no colliders on this client) are upright
    /// circles: the kart stops against them instead of shoving them, their own client nudges them gently out of the way
    /// (<see cref="LobbyKart"/> push). Bumps are counted for the sound and the camera shake.
    /// </summary>
    public sealed class KartCollide
    {
        public static readonly Vector3 Half = new(0.6f, 0.21f, 0.83f);
        public const float BoxBottom = 0.1f;
        public static readonly Vector3 Center = new(0f, BoxBottom + 0.21f, -0.02f);
        public const float Restitution = 0.25f, PlayerRadius = 0.32f;

        /// <summary>Level layers: not the own body (8), view models (9), Ignore Raycast (2, the kart itself), ragdolls.</summary>
        public static int LevelMask => ~((1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9) | (1 << 2) | GameLayers.RagdollMask);

        /// <summary>The driver (skipped by the player check) and the own kart (skipped by the kart check).</summary>
        public NetPlayer Driver;
        public LobbyKart Self;
        /// <summary>Strongest impact speed since the last read (m/s), read and cleared by the kart for sound / shake.</summary>
        public float Impact;
        public int Hits;

        private readonly Collider[] _buf = new Collider[24];
        private BoxCollider _probe;

        /// <summary>Pushes the kart out of walls / furniture / players at its current pose.</summary>
        public void Resolve(KartPhysics k, Transform self)
        {
            if (_probe == null && self != null) _probe = self.GetComponent<BoxCollider>();
            var rot = Quaternion.Euler(0f, k.Yaw, 0f);
            for (int pass = 0; pass < 3; pass++)
            {
                Vector3 c = k.Pos + rot * Center;
                int n = Physics.OverlapBoxNonAlloc(c, Half, _buf, rot, LevelMask, QueryTriggerInteraction.Ignore);
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    var col = _buf[i];
                    if (col == null || col is CharacterController || (self != null && col.transform.IsChildOf(self))) continue;
                    if (_probe == null) continue;
                    if (!Physics.ComputePenetration(_probe, k.Pos, rot, col, col.transform.position, col.transform.rotation, out var dir, out float dist)) continue;
                    dir.y = 0f;
                    float flat = dir.magnitude;
                    if (flat < 0.3f || dist <= 1e-4f) continue; // floor / ceiling contact, not a wall
                    dir /= flat;
                    k.Pos += dir * (dist + 0.002f);
                    Hit(k, dir, col.ClosestPoint(c) - c);
                    moved = true;
                }
                if (!moved) break;
            }
            Karts(k, rot);
            Players(k, rot);
        }

        /// <summary>
        /// Other karts (their colliders sit on the Ignore Raycast layer, outside the level mask): pushed apart and bounced
        /// off each other's relative velocity (equal masses: half the exchange here, the other driver's client does the
        /// other half); a parked kart is a heavy obstacle (the full bounce here).
        /// </summary>
        private void Karts(KartPhysics k, Quaternion rot)
        {
            if (_probe == null) return;
            foreach (var o in LobbyKart.All)
            {
                if (o == null || o == Self || o.Body == null) continue;
                Vector3 to = o.transform.position - k.Pos;
                if (to.sqrMagnitude > 4f * 4f) continue;
                if (!Physics.ComputePenetration(_probe, k.Pos, rot, o.Body, o.transform.position, o.transform.rotation, out var dir, out float dist)) continue;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f) continue;
                dir.Normalize();
                bool parked = o.View == LobbyKart.Mode.Parked;
                k.Pos += dir * (dist * (parked ? 1f : 0.5f) + 0.002f);
                float vn = Vector3.Dot(k.Vel - o.ShownVelocity, dir);
                if (vn >= 0f) continue;
                float j = -(1f + 0.35f) * vn * (parked ? 0.8f : 0.5f);
                k.Vel += dir * j;
                if (!parked && Self != null) Self.ReportBump(o, -dir * j); // the other kart's half, on its driver's client
                Vector3 arm = (o.transform.position - k.Pos) * 0.5f;
                arm.y = 0f;
                float spin = Vector3.Cross(arm, dir * j * 0.5f).y * KartPhysics.Mass / KartPhysics.Inertia;
                k.YawRate = k.YawRate * 0.8f + Mathf.Clamp(spin * Mathf.Rad2Deg, -90f, 90f);
                if (-vn > Impact) Impact = -vn;
                if (-vn > 0.8f) { Hits++; KartHits++; }
            }
        }

        /// <summary>Kart-to-kart contacts so far (tests).</summary>
        public int KartHits;

        private void Hit(KartPhysics k, Vector3 n, Vector3 arm)
        {
            float vn = Vector3.Dot(k.Vel, n);
            if (vn >= 0f) return;
            float j = -(1f + Restitution) * vn;
            k.Vel += n * j;
            // Wall friction along the surface, and the corner that hit turns the kart.
            Vector3 tang = k.Vel - n * Vector3.Dot(k.Vel, n);
            k.Vel -= tang * Mathf.Clamp01(0.25f * j / Mathf.Max(0.5f, tang.magnitude));
            arm.y = 0f;
            float spin = Vector3.Cross(arm, n * j * 0.5f).y * KartPhysics.Mass / KartPhysics.Inertia;
            k.YawRate = k.YawRate * 0.6f + Mathf.Clamp(spin * Mathf.Rad2Deg, -160f, 160f);
            if (-vn > Impact) Impact = -vn;
            if (-vn > 0.8f) Hits++;
        }

        /// <summary>Other players: the kart stops against them (never pushes them on this client).</summary>
        private void Players(KartPhysics k, Quaternion rot)
        {
            var inv = Quaternion.Inverse(rot);
            var mm = MatchManager.Instance;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == Driver || !o.IsSpawned || o.Life.Value == LifeState.Dead) continue;
                if (mm != null && mm.KartsVar.Value.KartOf(o.NetworkObjectId) >= 0) continue; // sits in a kart: the kart check handles it
                Vector3 local = inv * (o.transform.position - (k.Pos + rot * Center));
                if (Mathf.Abs(o.transform.position.y - k.Pos.y) > 1.2f) continue;
                Vector3 clamped = new Vector3(Mathf.Clamp(local.x, -Half.x, Half.x), 0f, Mathf.Clamp(local.z, -Half.z, Half.z));
                Vector3 d = new Vector3(local.x, 0f, local.z) - clamped;
                float dist = d.magnitude;
                if (dist >= PlayerRadius) continue;
                Vector3 nLocal = dist > 1e-4f ? d / dist : (Mathf.Abs(local.x) / Half.x > Mathf.Abs(local.z) / Half.z ? new Vector3(Mathf.Sign(local.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z)));
                Vector3 nWorld = rot * nLocal; // from the kart towards the player
                float pen = PlayerRadius - dist;
                k.Pos -= nWorld * pen;
                float vn = Vector3.Dot(k.Vel, nWorld);
                if (vn > 0f)
                {
                    k.Vel -= nWorld * vn;
                    k.Vel *= 0.92f;
                    if (vn > Impact) Impact = vn;
                    if (vn > 1.2f) Hits++;
                }
            }
        }

        /// <summary>Keeps the kart on the floor / rugs under it (no gravity: the lobby floor is flat).</summary>
        public void Ground(KartPhysics k, float dt)
        {
            if (Physics.Raycast(k.Pos + Vector3.up * 0.5f, Vector3.down, out var hit, 1.2f, LevelMask, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f)
                k.Pos.y = Mathf.MoveTowards(k.Pos.y, hit.point.y, dt * 1.5f);
        }
    }
}
