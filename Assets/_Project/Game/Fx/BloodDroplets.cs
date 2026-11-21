using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Turns blood particles that hit the level into splats (floor, walls, ceiling), up to a budget per spray.</summary>
    public class BloodDroplets : MonoBehaviour
    {
        private ParticleSystem _ps;
        private int _budget;
        private readonly List<ParticleCollisionEvent> _events = new List<ParticleCollisionEvent>();

        public void Init(ParticleSystem ps, int budget)
        {
            _ps = ps;
            _budget = budget;
        }

        /// <summary>Adds more splats (a spurt keeps bleeding for seconds).</summary>
        public void AddBudget(int n) => _budget += n;

        private void OnParticleCollision(GameObject other)
        {
            if (_ps == null || _budget <= 0 || other == null) return;
            int n = _ps.GetCollisionEvents(other, _events);
            for (int i = 0; i < n && _budget > 0; i++)
            {
                var e = _events[i];
                if (e.colliderComponent is Collider c && !BloodFx.IsLevel(c)) continue;
                if (Random.value > 0.55f) continue;
                _budget--;
                float speed = e.velocity.magnitude;
                float size = Random.Range(0.04f, 0.1f) * Mathf.Clamp(0.6f + speed * 0.15f, 0.6f, 1.5f);
                BloodFx.Splat(e.intersection, e.normal, size);
            }
        }
    }
}
