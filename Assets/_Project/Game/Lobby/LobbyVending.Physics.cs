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
            // A real toss over the shoulder, never the same twice: backwards within a cone, a little more often to the right
            // (the throwing hand; a negative yaw turns "back" to the player's right), 24-46 degrees up, 3.6-5.6 m/s, random
            // tumble. The old fixed toss was ~7 degrees right, 31 up, 5 m/s.
            float yaw = Random.Range(-26f, 14f), pitch = Random.Range(24f, 46f), speed = Random.Range(3.6f, 5.6f);
            Vector3 back = Quaternion.AngleAxis(yaw, Vector3.up) * -tr.forward;
            rb.linearVelocity = (back * Mathf.Cos(pitch * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(pitch * Mathf.Deg2Rad)) * speed;
            rb.angularVelocity = tr.right * Random.Range(8f, 18f) + tr.up * Random.Range(-7f, 7f) + tr.forward * Random.Range(-4f, 4f);
            Debug.Log($"[Vending] {player.Nick} tosses the can: yaw {-yaw:0} (right +), up {pitch:0} deg, {speed:0.0} m/s");
            can.gameObject.AddComponent<CanImpact>();
            bodies[s.Id] = rb;
            s.Stage = 1;
            s.Position = can.transform.position;
            s.Rotation = can.transform.rotation;
            s.Expires = mm.Now + 45;
            player.Busy.Value = false;
            // The can is empty: one more in the blood (two or more in a short time make the player drunk).
            player.ServerAddDrink();
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
                if (mm.Now - restSince[s.Id] > 0.5)
                {
                    rb.isKinematic = true;
                    s.Stage = 2;
                    s.Expires = mm.Now + 30;
                    changed = true;
                    Debug.Log($"[Vending] can {s.Id} came to rest at {can.transform.position} after {s.Bounces} bounces");
                }
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
