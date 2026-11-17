using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// One-shot muzzle flash: a bright short point light (lights the dark map ~12 m around, 0.06 s), a small additive
    /// star burst at the barrel and a few puffs of smoke. Self-destroys. Spawn with <see cref="Spawn"/>.
    /// </summary>
    public class MuzzleFlash : MonoBehaviour
    {
        public const string FlashMaterialResource = "Fx/MuzzleFlash";
        public const string SmokeMaterialResource = "Fx/GunSmoke";

        public const float LightDuration = 0.06f;
        public const float FlashDuration = 0.05f;
        public const float LightRange = 12f;
        public const float LightIntensity = 6f;

        static Material flashMat, smokeMat;
        static Texture2D flashTex, smokeTex;
        static MaterialPropertyBlock mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        struct Puff
        {
            public Transform T;
            public Renderer R;
            public Vector3 Velocity;
            public float Size, Grow, Life, Spin, Angle, Alpha;
        }

        Light flashLight;
        Transform muzzle;
        Transform flashRoot;
        readonly List<Renderer> flashRenderers = new List<Renderer>();
        readonly List<Puff> puffs = new List<Puff>();
        float age;
        bool firstPerson;

        /// <summary>
        /// Spawns a flash at <paramref name="muzzle"/> (forward = barrel direction). With <paramref name="firstPerson"/>
        /// the visible quads go to the view-model layer (rendered by the view-model camera, never clip into walls)
        /// and are smaller; the light is the same either way. Returns null when <paramref name="muzzle"/> is null.
        /// </summary>
        public static MuzzleFlash Spawn(Transform muzzle, bool firstPerson)
        {
            if (muzzle == null) return null;
            var go = new GameObject("MuzzleFlash");
            go.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
            var fx = go.AddComponent<MuzzleFlash>();
            fx.Init(muzzle, firstPerson);
            return fx;
        }

        void Init(Transform muzzleT, bool fp)
        {
            muzzle = muzzleT;
            firstPerson = fp;
            int layer = fp ? FirstPersonViewModel.Layer : muzzleT.gameObject.layer;
            gameObject.layer = layer;
            if (mpb == null) mpb = new MaterialPropertyBlock();

            // Light: pushed slightly forward so the gun itself is not blown out.
            var lightGo = new GameObject("FlashLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            flashLight = lightGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = LightRange;
            flashLight.intensity = LightIntensity;
            flashLight.color = new Color(1f, 0.78f, 0.45f);
            flashLight.shadows = LightShadows.None;
            flashLight.renderMode = LightRenderMode.ForcePixel;

            // Flash: two crossed planes along the barrel + one facing forward, random roll.
            flashRoot = new GameObject("Flash").transform;
            flashRoot.SetParent(transform, false);
            flashRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            float s = fp ? 0.16f : 0.26f;
            AddFlashQuad(new Vector3(0f, 0f, s * 0.45f), Quaternion.Euler(0f, 90f, 0f), new Vector3(s * 1.1f, s * 0.55f, 1f), layer);
            AddFlashQuad(new Vector3(0f, 0f, s * 0.45f), Quaternion.Euler(0f, 90f, 90f), new Vector3(s * 1.1f, s * 0.55f, 1f), layer);
            AddFlashQuad(new Vector3(0f, 0f, 0.01f), Quaternion.identity, new Vector3(s * 0.75f, s * 0.75f, 1f), layer);

            // Smoke puffs drift out of the barrel, rise and spread.
            int count = fp ? 3 : 4;
            Vector3 fwd = muzzleT.forward;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Smoke");
                go.layer = layer;
                go.transform.position = muzzleT.position + fwd * (0.03f + 0.04f * i);
                go.AddComponent<MeshFilter>().sharedMesh = BlockyMeshBuilder.GetFaceQuad();
                var r = go.AddComponent<MeshRenderer>();
                ConfigureFxRenderer(r, GetSmokeMaterial());
                puffs.Add(new Puff
                {
                    T = go.transform,
                    R = r,
                    Velocity = fwd * Random.Range(0.4f, 0.9f) + Random.insideUnitSphere * 0.12f + Vector3.up * 0.15f,
                    Size = (fp ? 0.05f : 0.08f) * Random.Range(0.8f, 1.2f),
                    Grow = (fp ? 0.25f : 0.45f) * Random.Range(0.8f, 1.2f),
                    Life = Random.Range(0.7f, 1.1f),
                    Spin = Random.Range(-90f, 90f),
                    Angle = Random.Range(0f, 360f),
                    Alpha = Random.Range(0.22f, 0.32f)
                });
            }

            Step(0f);
        }

        void AddFlashQuad(Vector3 pos, Quaternion rot, Vector3 scale, int layer)
        {
            var go = new GameObject("FlashQuad");
            go.layer = layer;
            go.transform.SetParent(flashRoot, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = BlockyMeshBuilder.GetFaceQuad();
            var r = go.AddComponent<MeshRenderer>();
            ConfigureFxRenderer(r, GetFlashMaterial());
            flashRenderers.Add(r);
        }

        static void ConfigureFxRenderer(Renderer r, Material m)
        {
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void LateUpdate()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances the effect (public so editor previews can drive it).</summary>
        public void Step(float dt)
        {
            age += dt;

            // Flash and light stay glued to the barrel while visible (gun recoils / view model sways).
            if (muzzle != null && age < LightDuration + 0.02f)
                transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);

            float lightK = age < 0.02f ? 1f : Mathf.Clamp01(1f - (age - 0.02f) / (LightDuration - 0.02f));
            if (flashLight != null)
            {
                flashLight.intensity = LightIntensity * lightK;
                flashLight.enabled = lightK > 0f;
            }

            float flashK = Mathf.Clamp01(1f - age / FlashDuration);
            if (flashRoot != null)
            {
                bool on = flashK > 0f;
                if (flashRoot.gameObject.activeSelf != on) flashRoot.gameObject.SetActive(on);
                flashRoot.localScale = Vector3.one * (0.8f + 0.5f * (1f - flashK));
                mpb.SetColor(ColorId, new Color(1f, 0.85f, 0.6f, flashK));
                foreach (var r in flashRenderers) r.SetPropertyBlock(mpb);
            }

            var cam = Camera.main;
            bool alive = false;
            for (int i = 0; i < puffs.Count; i++)
            {
                var p = puffs[i];
                if (p.T == null) continue;
                float k = age / p.Life;
                if (k >= 1f)
                {
                    SafeDestroy(p.T.gameObject);
                    p.T = null;
                    puffs[i] = p;
                    continue;
                }

                alive = true;
                p.Velocity *= Mathf.Exp(-3.5f * dt);
                p.Velocity += Vector3.up * (0.25f * dt);
                p.T.position += p.Velocity * dt;
                p.Angle += p.Spin * dt;
                float size = p.Size + p.Grow * Mathf.Sqrt(k);
                p.T.localScale = new Vector3(size, size, 1f);
                Quaternion face = cam != null ? Quaternion.LookRotation(p.T.position - cam.transform.position) : transform.rotation;
                p.T.rotation = face * Quaternion.Euler(0f, 0f, p.Angle);
                float a = p.Alpha * Mathf.Clamp01(k / 0.08f) * (1f - k) * (1f - k);
                mpb.SetColor(ColorId, new Color(0.62f, 0.6f, 0.58f, a));
                p.R.SetPropertyBlock(mpb);
                puffs[i] = p;
            }

            if (!alive && age > LightDuration + 0.05f && Application.isPlaying) Destroy(gameObject);
        }

        static void SafeDestroy(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void OnDestroy()
        {
            foreach (var p in puffs)
                if (p.T != null) SafeDestroy(p.T.gameObject);
        }

        // ------------------------------------------------------------------ materials

        static Material GetFlashMaterial()
        {
            if (flashMat != null) return flashMat;
            flashMat = CreateMaterial(FlashMaterialResource, "HideAndSeek/FxAdditive", "Legacy Shaders/Particles/Additive");
            flashMat.mainTexture = GetFlashTexture();
            return flashMat;
        }

        static Material GetSmokeMaterial()
        {
            if (smokeMat != null) return smokeMat;
            smokeMat = CreateMaterial(SmokeMaterialResource, "HideAndSeek/FxAlpha", "Legacy Shaders/Particles/Alpha Blended");
            smokeMat.mainTexture = GetSmokeTexture();
            return smokeMat;
        }

        static Material CreateMaterial(string resource, string shaderName, string fallback)
        {
            var template = Resources.Load<Material>(resource);
            Material m;
            if (template != null) m = new Material(template);
            else
            {
                var sh = Shader.Find(shaderName);
                if (sh == null) sh = Shader.Find(fallback);
                if (sh == null) sh = Shader.Find("Sprites/Default");
                m = new Material(sh);
            }

            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        static Texture2D GetFlashTexture()
        {
            if (flashTex != null) return flashTex;
            const int n = 32;
            flashTex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "MuzzleFlashTex", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Blocky star: bright core, 4 spikes + 4 shorter diagonal spikes, quantised to 2x2 "pixels".
                float fx = ((x / 2) * 2 + 1f - n * 0.5f) / (n * 0.5f);
                float fy = ((y / 2) * 2 + 1f - n * 0.5f) / (n * 0.5f);
                float r = Mathf.Sqrt(fx * fx + fy * fy);
                float ang = Mathf.Atan2(fy, fx);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2f)), 6f) * 0.75f + Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 2f)), 10f) * 0.4f;
                float reach = 0.32f + spikes * 0.68f;
                float v = Mathf.Clamp01(1f - r / reach);
                v = Mathf.Round(v * 4f) / 4f;
                Color c = Color.Lerp(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.97f, 0.85f), v * v);
                c.a = v > 0f ? Mathf.Clamp01(v * 1.6f) : 0f;
                px[y * n + x] = c;
            }

            flashTex.SetPixels(px);
            flashTex.Apply(false, false);
            return flashTex;
        }

        static Texture2D GetSmokeTexture()
        {
            if (smokeTex != null) return smokeTex;
            const int n = 32;
            smokeTex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "GunSmokeTex", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = (x + 0.5f - n * 0.5f) / (n * 0.5f);
                float fy = (y + 0.5f - n * 0.5f) / (n * 0.5f);
                float r = Mathf.Sqrt(fx * fx + fy * fy);
                float noise = Mathf.PerlinNoise(x * 0.23f + 3.1f, y * 0.23f + 7.7f);
                float a = Mathf.Clamp01(1f - r) * (0.55f + 0.45f * noise);
                px[y * n + x] = new Color(1f, 1f, 1f, a * a * 1.6f);
            }

            smokeTex.SetPixels(px);
            smokeTex.Apply(false, false);
            return smokeTex;
        }
    }
}
