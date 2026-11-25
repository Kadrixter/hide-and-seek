using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Go-kart effects on every client (from what the kart shows, so remote drifts smoke too): white tyre smoke rolling
    /// off both rear wheels while they slide (handbrake, drift, hard braking), black skid marks laid on the floor under
    /// them (<see cref="KartSkids"/>), and little blue-grey puffs from the exhaust with the revs.
    /// </summary>
    public sealed class KartFx : MonoBehaviour
    {
        private LobbyKart _kart;
        private readonly ParticleSystem[] _smoke = new ParticleSystem[2];
        private ParticleSystem _exhaust;
        private KartSkids _skids;

        /// <summary>Skid mark quads laid so far (tests).</summary>
        public int SkidCount => _skids != null ? _skids.Count : 0;

        public void Init(LobbyKart kart)
        {
            _kart = kart;
            for (int i = 0; i < 2; i++) _smoke[i] = MakeSmoke("TyreSmoke" + i);
            _exhaust = MakeExhaust();
            _skids = KartSkids.Create();
        }

        private void OnDestroy()
        {
            if (_skids != null) Destroy(_skids.gameObject);
        }

        private ParticleSystem MakeSmoke(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 0.92f, 0.94f, 0.55f), new Color(0.8f, 0.8f, 0.84f, 0.4f));
            main.gravityModifier = -0.035f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.4f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.08f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.6f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Soft;
            r.sortingFudge = 2f;
            ps.Play();
            return ps;
        }

        private ParticleSystem MakeExhaust()
        {
            var go = new GameObject("ExhaustPuffs");
            go.transform.SetParent(_kart.Parts.Exhaust, false);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.48f, 0.55f, 0.35f), new Color(0.3f, 0.3f, 0.34f, 0.25f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.01f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 3f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            go.GetComponent<ParticleSystemRenderer>().material = FxUtil.Soft;
            ps.Play();
            return ps;
        }

        private void LateUpdate()
        {
            if (_kart == null || _kart.Parts == null) return;
            float slip = _kart.Slip01;
            bool moving = _kart.View != LobbyKart.Mode.Parked || Mathf.Abs(_kart.ShownSpeed) > 0.2f;
            for (int i = 0; i < 2; i++)
            {
                var w = _kart.Parts.RearWheel[i];
                Vector3 contact = w.position;
                contact.y = _kart.transform.position.y;
                _smoke[i].transform.position = contact + Vector3.up * 0.08f - _kart.transform.forward * 0.05f;
                var em = _smoke[i].emission;
                em.rateOverTime = slip > 0.18f ? Mathf.Lerp(8f, 70f, (slip - 0.18f) / 0.82f) : 0f;
                _skids.Track(i, contact, _kart.transform.right, slip > 0.22f && moving, Mathf.Clamp01(0.35f + slip * 0.65f));
            }
            var ex = _exhaust.emission;
            ex.rateOverTime = _kart.Rpm01 > 0.02f ? 3f + 22f * _kart.Rpm01 * Mathf.Max(0.3f, Mathf.Abs(_kart.Throttle)) : 0f;
        }
    }
}
