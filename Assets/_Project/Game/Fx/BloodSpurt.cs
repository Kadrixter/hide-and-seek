using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Pulsing arterial spurt from a stump (heartbeat: strong squirt, pause) or a steady drip from a cut-off limb,
    /// weakening until it stops after the given time.
    /// </summary>
    public class BloodSpurt : MonoBehaviour
    {
        private ParticleSystem _ps;
        private float _duration, _t;
        private bool _pulse;
        private BloodDroplets _drops;
        private float _nextBudget;

        public void Init(ParticleSystem ps, float duration, bool pulse = true)
        {
            _ps = ps;
            _duration = Mathf.Max(0.5f, duration);
            _pulse = pulse;
            _drops = GetComponent<BloodDroplets>();
        }

        private void Update()
        {
            if (_ps == null) { Destroy(gameObject); return; }
            _t += Time.deltaTime;
            float life = Mathf.Clamp01(1f - _t / _duration);
            var em = _ps.emission;
            var main = _ps.main;
            if (_pulse)
            {
                const float beat = 0.62f;
                float ph = Mathf.Repeat(_t, beat) / beat;
                float squirt = ph < 0.28f ? Mathf.Sin(ph / 0.28f * Mathf.PI) : 0.08f;
                em.rateOverTime = 260f * squirt * life * life + 12f * life;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f + 2.6f * squirt * life, 1.2f + 3.4f * squirt * life);
            }
            else
            {
                em.rateOverTime = 26f * life;
            }
            if (_drops != null && Time.time >= _nextBudget)
            {
                _nextBudget = Time.time + 0.5f;
                _drops.AddBudget(_pulse ? 3 : 1);
            }
            if (_t >= _duration)
            {
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(gameObject, 2f);
                enabled = false;
            }
        }
    }
}
