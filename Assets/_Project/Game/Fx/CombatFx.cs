using System.Collections;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Shared runtime helpers for small effects (no assets needed).</summary>
    internal static class FxUtil
    {
        private static Material _unlit;
        private static MonoBehaviour _runner;

        public static Material Unlit
        {
            get
            {
                if (_unlit == null)
                {
                    var sh = Shader.Find("Sprites/Default");
                    _unlit = new Material(sh) { name = "FxUnlit" };
                }
                return _unlit;
            }
        }

        public static MonoBehaviour Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("[Fx]");
                    Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<FxRunner>();
                }
                return _runner;
            }
        }

        private class FxRunner : MonoBehaviour { }
    }

    /// <summary>Blood spray particles and floor decals.</summary>
    public static class BloodFx
    {
        private static readonly Color Blood = new Color(0.42f, 0.02f, 0.02f, 1f);
        private static readonly Color BloodDark = new Color(0.22f, 0.01f, 0.01f, 0.92f);

        public static void Spray(Vector3 position, Vector3 direction)
        {
            var go = new GameObject("BloodSpray");
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.forward);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(Blood, BloodDark);
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.03f;
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.dampen = 0.9f;
            col.bounce = 0.05f;
            col.lifetimeLoss = 0.4f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Unlit;
            ps.Play();

            if (Physics.Raycast(position, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                Decal(hit.point + direction * Random.Range(0.1f, 0.5f), Random.Range(0.25f, 0.5f));
        }

        /// <summary>Flat blood pool on the floor below <paramref name="position"/>.</summary>
        public static void Decal(Vector3 position, float size = 0.35f, float life = 90f)
        {
            if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(q.GetComponent<Collider>());
            q.name = "BloodDecal";
            q.transform.position = position + Vector3.up * (0.006f + Random.value * 0.003f);
            q.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            q.transform.localScale = new Vector3(size * Random.Range(0.7f, 1.3f), size * Random.Range(0.7f, 1.3f), 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = FxUtil.Unlit;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", BloodDark);
            mr.SetPropertyBlock(mpb);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(q, life);
        }
    }

    /// <summary>Muzzle flash (bright short light visible far away in the dark), tracer and impact.</summary>
    public static class ShotFx
    {
        public static void Play(Vector3 from, Vector3 to, Transform muzzle, bool firstPerson, bool flash = true)
        {
            FxUtil.Runner.StartCoroutine(Run(from, to, muzzle, firstPerson, flash));
        }

        private static IEnumerator Run(Vector3 from, Vector3 to, Transform muzzle, bool firstPerson, bool withFlash)
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.position = from;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.45f);
            light.range = 14f;
            light.intensity = 7f;
            light.shadows = LightShadows.None;
            light.enabled = withFlash;

            var flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(flash.GetComponent<Collider>());
            flash.transform.SetParent(go.transform, false);
            flash.transform.localScale = Vector3.one * (firstPerson ? 0.12f : 0.3f) * (withFlash ? 1f : 0f);
            var fr = flash.GetComponent<MeshRenderer>();
            fr.sharedMaterial = FxUtil.Unlit;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(1f, 0.85f, 0.5f, 1f));
            fr.SetPropertyBlock(mpb);
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = FxUtil.Unlit;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.012f;
            line.endWidth = 0.006f;
            line.startColor = new Color(1f, 0.9f, 0.6f, 0.7f);
            line.endColor = new Color(1f, 0.9f, 0.6f, 0f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var cam = Camera.main;
            float t = 0f;
            while (t < 0.07f)
            {
                if (muzzle != null) go.transform.position = muzzle.position;
                if (cam != null) flash.transform.rotation = Quaternion.LookRotation(flash.transform.position - cam.transform.position);
                flash.transform.Rotate(0f, 0f, Random.Range(0f, 360f));
                light.intensity = Mathf.Lerp(7f, 0f, t / 0.07f);
                t += Time.deltaTime;
                yield return null;
            }
            Object.Destroy(flash);
            light.enabled = false;
            line.enabled = false;

            // Impact spark + small dust puff light.
            var spark = new GameObject("Impact");
            spark.transform.position = to;
            var sl = spark.AddComponent<Light>();
            sl.type = LightType.Point;
            sl.color = new Color(1f, 0.7f, 0.4f);
            sl.range = 2.5f;
            sl.intensity = 3f;
            Object.Destroy(spark, 0.05f);
            Object.Destroy(go, 0.1f);
        }
    }
}
