using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>Effects of the intro: dust blown out along the sill, sparks at the rails, lamps flickering, a bounce light.</summary>
    public static class CineFx
    {
        /// <summary>Thick dust rolling out from under the shutter along <paramref name="along"/> (half length <paramref name="half"/>).</summary>
        public static GameObject DustBurst(Vector3 centre, Vector3 along, Vector3 outward, float half)
        {
            var go = new GameObject("GateDust");
            go.transform.SetPositionAndRotation(centre + Vector3.up * 0.08f, Quaternion.LookRotation(outward, Vector3.up));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.58f, 0.52f, 0.32f), new Color(0.45f, 0.42f, 0.38f, 0.22f));
            main.gravityModifier = -0.02f;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 90), new ParticleSystem.Burst(0.1f, 50), new ParticleSystem.Burst(0.25f, 25) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(half * 2f, 0.3f, 0.2f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.7f);
            vel.z = new ParticleSystem.MinMaxCurve(0.3f, 2.2f);
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.08f;
            limit.limit = 0.25f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.08f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Soft;
            r.sortingFudge = -10f;
            ps.Play();
            return go;
        }

        /// <summary>Sparks along the bottom bar / rails at the impact.</summary>
        public static void ImpactSparks(ExitGate gate)
        {
            Vector3 n = gate.LocalDir(Vector3.forward);
            for (int s = -1; s <= 1; s += 2)
                LeverSparks.Burst(gate.Local(new Vector3(s * (ExitGate.HalfW - 0.05f), 0.06f, ExitGate.CurtainZ + 0.05f)), n - gate.LocalDir(Vector3.right) * s * 0.5f);
            LeverSparks.Burst(gate.Local(new Vector3(-1.2f, 0.04f, ExitGate.CurtainZ + 0.06f)), n);
        }

        /// <summary>Map lamps within <paramref name="radius"/> m flicker for <paramref name="time"/> s (restored after).</summary>
        public static void FlickerLamps(Vector3 at, float radius, float time)
        {
            var list = new List<(Light l, float i)>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l == null || !l.enabled || l.type == LightType.Directional) continue;
                if (l.GetComponentInParent<ExitGate>() != null || l.GetComponentInParent<CineCamera>() != null) continue;
                if (l.GetComponentInParent<HideAndSeek.Game.Net.NetPlayer>() != null) continue; // flashlights
                if ((l.transform.position - at).sqrMagnitude > radius * radius) continue;
                list.Add((l, l.intensity));
            }
            if (list.Count > 0) FxUtil.Runner.StartCoroutine(Flicker(list, time));
        }

        static IEnumerator Flicker(List<(Light l, float i)> lights, float time)
        {
            var rng = new System.Random(77);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                foreach (var (l, i) in lights)
                {
                    if (l == null) continue;
                    bool off = rng.NextDouble() < Mathf.Lerp(0.55f, 0.08f, k);
                    l.intensity = off ? i * 0.05f : i * (1f + 0.35f * (float)rng.NextDouble() * (1f - k));
                }
                yield return null;
            }
            foreach (var (l, i) in lights) if (l != null) l.intensity = i;
        }

        /// <summary>A light that goes on hard and fades out (spark / impact bounce).</summary>
        public static Light Flash(Transform parent, Vector3 pos, Color c, float range, float peak, float time)
        {
            var go = new GameObject("CineFlash");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = peak;
            l.shadows = LightShadows.None;
            FxUtil.Runner.StartCoroutine(FadeLight(l, peak, time));
            return l;
        }

        static IEnumerator FadeLight(Light l, float peak, float time)
        {
            for (float t = 0f; t < time && l != null; t += Time.deltaTime)
            {
                l.intensity = peak * (1f - t / time) * (0.8f + 0.2f * Random.value);
                yield return null;
            }
            if (l != null) Object.Destroy(l.gameObject);
        }
    }
}
