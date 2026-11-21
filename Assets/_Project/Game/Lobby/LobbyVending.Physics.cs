using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    public sealed partial class LobbyVending
    {
        /// <summary>Server: the crushed can leaves the hand over the shoulder and becomes a rigidbody.</summary>
        void Throw(MatchManager mm, int i, VendingState s, SodaCan can, NetPlayer player)
        {
            var tr = player.transform;
            can.gameObject.SetActive(true);
            can.Scale = ThrownScale;
            can.SetCrush(1f);
            can.transform.SetPositionAndRotation(tr.TransformPoint(new Vector3(0.22f, 1.78f, -0.3f)), tr.rotation * Quaternion.Euler(70f, 0f, 20f));
            var col = can.gameObject.AddComponent<CapsuleCollider>();
            col.radius = SodaCan.Radius * 0.9f;
            col.height = SodaCan.Height * 0.7f;
            var rb = can.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.03f;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var c in player.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c);
            rb.linearVelocity = -tr.forward * 4.2f + Vector3.up * 2.6f + tr.right * 0.5f;
            rb.angularVelocity = tr.right * 14f + tr.up * 5f;
            can.gameObject.AddComponent<CanImpact>();
            bodies[s.Id] = rb;
            s.Stage = 1;
            s.Position = can.transform.position;
            s.Rotation = can.transform.rotation;
            s.Expires = mm.Now + 45;
            player.Busy.Value = false;
            mm.VendingCans[i] = s;
        }

        /// <summary>Server: publishes the flying can, counts bounces, freezes it once it rests, removes it later.</summary>
        bool Simulate(MatchManager mm, int i, VendingState s, SodaCan can)
        {
            if (mm.Now >= s.Expires) { mm.VendingCans.RemoveAt(i); return false; }
            if (s.Stage != 1 || !bodies.TryGetValue(s.Id, out var rb)) return true;
            var impact = can.GetComponent<CanImpact>();
            bool changed = false;
            if (impact != null && impact.Hits != s.Bounces) { s.Bounces = (byte)Mathf.Min(impact.Hits, 250); changed = true; }
            bool grounded = Physics.Raycast(can.transform.position, Vector3.down, 0.12f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            if (grounded && rb.linearVelocity.sqrMagnitude < 0.05f)
            {
                if (!restSince.ContainsKey(s.Id)) restSince[s.Id] = mm.Now;
                if (mm.Now - restSince[s.Id] > 0.5) { rb.isKinematic = true; s.Stage = 2; s.Expires = mm.Now + 30; changed = true; }
            }
            else restSince.Remove(s.Id);
            s.Position = can.transform.position;
            s.Rotation = can.transform.rotation;
            if (changed || Time.unscaledTime >= nextSync) mm.VendingCans[i] = s;
            return true;
        }
    }

    /// <summary>Counts hard hits of the thrown can (server); the count is replicated so everyone hears the bounces.</summary>
    sealed class CanImpact : MonoBehaviour
    {
        public int Hits;
        float last;
        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude < 1.2f || Time.time - last < 0.15f) return;
            last = Time.time;
            Hits++;
        }
    }
}
