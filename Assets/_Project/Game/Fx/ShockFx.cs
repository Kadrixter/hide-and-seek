using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Short circuit at a wall socket: fork stuck in the socket, a storm of bouncing sparks for the shock duration,
    /// flickering blue-white arc light, black scorch mark afterwards.
    /// </summary>
    public static class ShockFx
    {
        public static void Play(Vector3 outlet, Vector3 normal, float duration, float forkLifetime)
        {
            FxUtil.Runner.StartCoroutine(Run(outlet, normal, duration, forkLifetime));
        }

        private static IEnumerator Run(Vector3 outlet, Vector3 normal, float duration, float forkLifetime)
        {
            // Fork stuck in the socket (tines in the holes, handle sticking out, slightly drooping).
            float len = BlockyProps.ForkTipLocal.z * BlockyProps.ForkScale;
            var fork = BlockyProps.Create(HeldItem.Fork, null, 0);
            var rot = Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(6f, 0f, 0f);
            fork.transform.SetPositionAndRotation(outlet + normal * (len - 0.035f) + Vector3.up * 0.004f, rot);

            var go = new GameObject("ShortCircuitSparks");
            go.transform.position = outlet + normal * 0.03f;
            go.transform.rotation = Quaternion.LookRotation(normal);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.6f, 0.15f));
            main.gravityModifier = 1.6f;
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(220f);
            em.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 90), new ParticleSystem.Burst(0.35f, 40), new ParticleSystem.Burst(0.9f, 50),
                new ParticleSystem.Burst(1.4f, 40), new ParticleSystem.Burst(duration - 0.2f, 70),
            });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.02f;
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.bounce = 0.45f;
            col.dampen = 0.25f;
            col.lifetimeLoss = 0.25f;
            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.2f), 0.4f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colOverLife.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Unlit;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f;
            r.lengthScale = 1.5f;
            ps.Play();

            // Arc light.
            var lg = new GameObject("ArcLight");
            lg.transform.position = outlet + normal * 0.25f;
            var light = lg.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.color = new Color(0.75f, 0.85f, 1f);
            light.shadows = LightShadows.None;

            // Glow at the socket.
            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(glow.GetComponent<Collider>());
            glow.transform.SetPositionAndRotation(outlet + normal * 0.02f, Quaternion.LookRotation(-normal));
            var gr = glow.GetComponent<MeshRenderer>();
            gr.sharedMaterial = FxUtil.Unlit;
            var mpb = new MaterialPropertyBlock();

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float flick = Random.value;
                light.intensity = flick < 0.25f ? 0.4f : 2.5f + 4f * Random.value;
                float s = 0.08f + 0.14f * Random.value;
                glow.transform.localScale = new Vector3(s, s, s);
                mpb.SetColor("_Color", new Color(0.85f, 0.92f, 1f, 0.55f + 0.45f * Random.value));
                gr.SetPropertyBlock(mpb);
                yield return null;
            }
            Object.Destroy(lg);
            Object.Destroy(glow);

            // Scorch mark round the socket; smoking fork stays a bit longer.
            var scorch = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(scorch.GetComponent<Collider>());
            scorch.transform.SetPositionAndRotation(outlet + normal * 0.009f, Quaternion.LookRotation(-normal));
            scorch.transform.localScale = new Vector3(0.2f, 0.17f, 1f);
            var sr = scorch.GetComponent<MeshRenderer>();
            sr.sharedMaterial = FxUtil.Unlit;
            var smpb = new MaterialPropertyBlock();
            smpb.SetColor("_Color", new Color(0.03f, 0.025f, 0.02f, 0.75f));
            sr.SetPropertyBlock(smpb);

            yield return new WaitForSeconds(Mathf.Max(0f, forkLifetime - duration));
            Object.Destroy(fork.gameObject);
            yield return new WaitForSeconds(20f);
            Object.Destroy(scorch);
        }
    }
}
