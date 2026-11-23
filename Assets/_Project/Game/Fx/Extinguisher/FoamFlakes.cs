using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Shared particle system for little bits of foam: flakes falling off a wiped face, the splash when the jet hits a
    /// face, and the puff when a dropped extinguisher hits the floor. One world-space system for everything.
    /// </summary>
    public static class FoamFlakes
    {
        private static ParticleSystem _ps;

        private static ParticleSystem Ps
        {
            get
            {
                if (_ps != null) return _ps;
                var go = new GameObject("[FoamFlakes]");
                _ps = go.AddComponent<ParticleSystem>();
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _ps.main;
                main.loop = true;
                main.playOnAwake = false;
                main.maxParticles = 500;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                main.startColor = new Color(0.97f, 0.97f, 0.95f, 0.95f);
                main.gravityModifier = 0.7f;
                var em = _ps.emission;
                em.rateOverTime = 0f;
                var shape = _ps.shape;
                shape.enabled = false;
                var coll = _ps.collision;
                coll.enabled = true;
                coll.type = ParticleSystemCollisionType.World;
                coll.quality = ParticleSystemCollisionQuality.Low;
                coll.dampen = 1f;
                coll.bounce = 0f;
                coll.collidesWith = ~((1 << 8) | (1 << 9));
                var col = _ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var r = go.GetComponent<ParticleSystemRenderer>();
                var sh = Resources.Load<Shader>("Shaders/HnsPowderCloud");
                r.sharedMaterial = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { mainTexture = ExtinguisherSpray.PuffTexture() };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ps.Play();
                return _ps;
            }
        }

        /// <summary>A couple of flakes thrown off to <paramref name="side"/>.</summary>
        public static void Drop(Vector3 at, Vector3 side)
        {
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 2; i++)
            {
                ep.position = at + Random.insideUnitSphere * 0.04f;
                ep.velocity = side * Random.Range(0.4f, 1.2f) + Vector3.up * Random.Range(0f, 0.5f) + Random.insideUnitSphere * 0.2f;
                Ps.Emit(ep, 1);
            }
        }

        /// <summary>Burst of foam / powder at <paramref name="at"/> (bigger, slower).</summary>
        public static void Burst(Vector3 at, Vector3 dir, int count, float speed, float size)
        {
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.position = at + Random.insideUnitSphere * 0.05f;
                ep.velocity = (dir + Random.insideUnitSphere * 0.8f).normalized * Random.Range(speed * 0.4f, speed);
                ep.startSize = Random.Range(size * 0.5f, size);
                Ps.Emit(ep, 1);
            }
        }
    }
}
