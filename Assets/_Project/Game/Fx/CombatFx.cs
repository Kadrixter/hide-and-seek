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

        private static Material _soft;

        /// <summary>Alpha-blended soft round dot (blood droplets, smoke).</summary>
        public static Material Soft
        {
            get
            {
                if (_soft != null) return _soft;
                const int S = 32;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FxSoftDot" };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((1f - r) * 2.2f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply();
                _soft = new Material(Shader.Find("Sprites/Default")) { name = "FxSoft", mainTexture = tex };
                return _soft;
            }
        }

        private class FxRunner : MonoBehaviour { }
    }

    /// <summary>Dust clouds (hider throws dust into the seeker's eyes).</summary>
    public static class DustFx
    {
        private static readonly Color Tan = new Color(0.78f, 0.68f, 0.5f, 0.9f);
        private static readonly Color TanDark = new Color(0.58f, 0.5f, 0.36f, 0.8f);

        public static void Puff(Vector3 position, Vector3 direction, int count, float speed, float size)
        {
            var go = new GameObject("DustPuff");
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.forward);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(Tan, TanDark);
            main.gravityModifier = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.04f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Unlit;
            ps.Play();
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
