using System.Collections;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Short burst of sparks and a blue-white flash where the lever blades slam into the jaws.</summary>
    public static class LeverSparks
    {
        public static void Burst(Vector3 at, Vector3 outward) => FxUtil.Runner.StartCoroutine(Run(at, outward));

        static IEnumerator Run(Vector3 at, Vector3 outward)
        {
            var go = new GameObject("LeverSparks");
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(outward + Vector3.down * 0.3f));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.6f, 0.2f));
            main.gravityModifier = 1.6f;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 45), new ParticleSystem.Burst(0.08f, 20) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f;
            shape.radius = 0.02f;
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.bounce = 0.4f;
            col.dampen = 0.3f;
            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.2f), 0.4f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colOverLife.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Unlit;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1.4f;
            ps.Play();

            var lg = new GameObject("LeverArc");
            lg.transform.position = at + outward * 0.2f;
            var light = lg.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.5f;
            light.color = new Color(0.75f, 0.85f, 1f);
            light.shadows = LightShadows.None;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                light.intensity = (1f - t / 0.25f) * (2f + 3f * Random.value);
                yield return null;
            }
            Object.Destroy(lg);
        }
    }
}
