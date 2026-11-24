using System.Collections;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// The intro lever handle tearing off: a short spray of small sparks from the torn stub (along the wall, away
    /// from the killer, falling), a warm flicker that does not blow out the close-up, and a few metal chips that drop
    /// and bounce off the floor. Local only (everybody plays it at the same intro time).
    /// </summary>
    public static class StubSnapFx
    {
        static Material _chip, _chipDark;

        /// <param name="at">Torn end of the stub (world).</param>
        /// <param name="outward">Main spray direction (world).</param>
        public static void Play(Vector3 at, Vector3 outward)
        {
            Sparks(at, outward);
            CineFx.Flash(null, at + outward.normalized * 0.12f, new Color(1f, 0.8f, 0.55f), 1.6f, 1.3f, 0.16f);
            _chip ??= CineKit.Mat("SnapChip", new Color(0.8f, 0.82f, 0.85f), 0.95f, 0.8f);
            _chipDark ??= CineKit.Mat("SnapChipDark", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.4f);
            for (int i = 0; i < 4; i++) Chip(at, outward, i);
        }

        static void Sparks(Vector3 at, Vector3 outward)
        {
            var go = new GameObject("StubSparks");
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(outward));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.012f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.65f, 0.25f));
            main.gravityModifier = 1.8f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            // The crack, then the stub crackling a few times.
            em.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 26), new ParticleSystem.Burst(0.09f, 8),
                new ParticleSystem.Burst(0.2f, 5), new ParticleSystem.Burst(0.31f, 4),
            });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.006f;
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.bounce = 0.35f;
            col.dampen = 0.4f;
            var life = ps.colorOverLifetime;
            life.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.2f), 0.35f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            life.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Unlit;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.03f;
            r.lengthScale = 1.2f;
            ps.Play();
        }

        /// <summary>A small bent piece of the rod / the bracket paint, flung out, falling, bouncing, gone after a while.</summary>
        static void Chip(Vector3 at, Vector3 outward, int i)
        {
            var go = new GameObject("SnapChip");
            go.transform.position = at;
            Vector3 size = i == 0 ? new Vector3(0.014f, 0.036f, 0.014f) : new Vector3(0.012f + 0.005f * (i % 2), 0.008f, 0.014f);
            CineKit.Box("Mesh", go.transform, Vector3.zero, size, i % 2 == 0 ? _chip : _chipDark, 0.002f);
            go.AddComponent<BoxCollider>().size = size;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = (outward.normalized + Random.insideUnitSphere * 0.6f + Vector3.up * 0.4f).normalized * Random.Range(0.9f, 1.8f);
            rb.angularVelocity = Random.insideUnitSphere * 25f;
            FxUtil.Runner.StartCoroutine(Remove(go, 2.5f));
        }

        static IEnumerator Remove(GameObject go, float after)
        {
            yield return new WaitForSeconds(after);
            if (go != null) Object.Destroy(go);
        }
    }
}
