using System.Collections.Generic;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Flashlights and the mirror: a beam hitting the glass bounces off as a mirrored spot light, and a lamp pointed at
    /// the viewer's reflection shows a bright glare in the mirror (glare quads exist only for the reflection render).
    /// Independent of the "Lustro" quality level (the bounce lights the room, not the reflection).
    /// </summary>
    public partial class PlanarMirror
    {
        private const float BounceGain = 0.8f;   // a mirror reflects ~80 % of the light
        private const int MaxBounces = 4;

        private class Bounce
        {
            public Light Light;
            public Transform Glare;
            public MeshRenderer GlareRenderer;
            public float Glow;
        }

        private readonly List<Flashlight> _lamps = new List<Flashlight>();
        private readonly List<Bounce> _bounces = new List<Bounce>();
        private float _nextLampSearch;
        private static Material _glareMat;

        private void DestroyBounces()
        {
            foreach (var b in _bounces)
            {
                if (b.Light != null) Destroy(b.Light.gameObject);
                if (b.Glare != null) Destroy(b.Glare.gameObject);
            }
        }

        private void UpdateBounces()
        {
            if (Time.unscaledTime >= _nextLampSearch)
            {
                _nextLampSearch = Time.unscaledTime + 0.5f;
                _lamps.Clear();
                _lamps.AddRange(FindObjectsByType<Flashlight>(FindObjectsSortMode.None));
            }

            Vector3 pos = transform.position;
            Vector3 n = Normal;
            Vector3 right = transform.right * (transform.lossyScale.x * 0.5f);
            Vector3 up = transform.up * (transform.lossyScale.y * 0.5f);
            int used = 0;
            foreach (var fl in _lamps)
            {
                if (used >= MaxBounces) break;
                var L = fl != null ? fl.SpotLight : null;
                if (L == null || !L.enabled || !L.gameObject.activeInHierarchy || L.intensity <= 0.01f) continue;
                Vector3 o = L.transform.position;
                Vector3 f = L.transform.forward;
                float side = Vector3.Dot(o - pos, n);
                if (side <= 0.02f) continue;                // behind the glass
                float fn = Vector3.Dot(f, n);
                if (fn > -0.05f) continue;                   // not pointing at the mirror plane

                // How much of the beam lands on the glass: angle from the beam axis to the nearest point of the glass.
                float t = side / -fn;
                if (t > L.range) continue;
                Vector3 hit = o + f * t;
                Vector3 rel = hit - pos;
                float lx = Mathf.Clamp(Vector3.Dot(rel, right.normalized), -right.magnitude, right.magnitude);
                float ly = Mathf.Clamp(Vector3.Dot(rel, up.normalized), -up.magnitude, up.magnitude);
                Vector3 nearest = pos + right.normalized * lx + up.normalized * ly;
                float ang = Vector3.Angle(f, nearest - o);
                float half = L.spotAngle * 0.5f;
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.35f, half, ang));
                if (k <= 0.01f) continue;
                float distFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(L.range * 0.55f, L.range, t));
                k *= distFade;
                if (k <= 0.01f) continue;

                var b = GetBounce(used++);
                var bl = b.Light;
                bl.enabled = true;
                bl.transform.SetPositionAndRotation(o - 2f * side * n, Quaternion.LookRotation(f - 2f * fn * n, Vector3.up));
                bl.spotAngle = L.spotAngle;
                bl.innerSpotAngle = L.innerSpotAngle;
                bl.range = L.range;
                bl.cookie = L.cookie;
                bl.color = L.color * new Color(0.94f, 0.96f, 1f);
                bl.intensity = L.intensity * BounceGain * k;
                b.Glow = k;
                b.Glare.position = o + f * 0.06f;
            }
            for (int i = used; i < _bounces.Count; i++)
            {
                _bounces[i].Light.enabled = false;
                _bounces[i].Glow = 0f;
            }
        }

        private Bounce GetBounce(int i)
        {
            while (_bounces.Count <= i)
            {
                var lg = new GameObject("MirrorBounceLight");
                lg.transform.SetParent(transform, true);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot;
                l.shadows = LightShadows.None; // starts behind the wall; the wall must not block it
                l.renderMode = LightRenderMode.ForcePixel;
                l.enabled = false;

                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "MirrorGlare";
                Destroy(q.GetComponent<Collider>());
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = GlareMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                _bounces.Add(new Bounce { Light = l, Glare = q.transform, GlareRenderer = r });
            }
            return _bounces[i];
        }

        /// <summary>Bright glare where a lamp points back at the reflected viewer (only drawn into the mirror).</summary>
        private void ShowGlares(Vector3 mirrorCamPos, bool on)
        {
            foreach (var b in _bounces)
            {
                if (!on || b.Glow <= 0.01f || !b.Light.enabled) { b.GlareRenderer.enabled = false; continue; }
                Vector3 lampPos = b.Glare.position;
                // The real lamp shines along the un-mirrored axis; how straight at the (reflected) eye does it point?
                Vector3 f = Vector3.Reflect(b.Light.transform.forward, Normal);
                Vector3 toEye = mirrorCamPos - lampPos;
                float dist = toEye.magnitude;
                float a = Vector3.Angle(f, toEye);
                float facing = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, b.Light.spotAngle * 0.5f + 6f, a));
                float glow = facing * Mathf.Clamp01(b.Glow * 1.3f);
                if (glow <= 0.02f) { b.GlareRenderer.enabled = false; continue; }
                b.GlareRenderer.enabled = true;
                b.Glare.rotation = Quaternion.LookRotation(lampPos - mirrorCamPos, Vector3.up);
                float size = Mathf.Lerp(0.25f, 1.1f, glow) * Mathf.Clamp(dist * 0.12f, 0.6f, 1.8f);
                b.Glare.localScale = new Vector3(size, size, 1f);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", new Color(1f, 0.97f, 0.9f, Mathf.Clamp01(glow)));
                b.GlareRenderer.SetPropertyBlock(mpb);
            }
        }

        private static Material GlareMaterial
        {
            get
            {
                if (_glareMat != null) return _glareMat;
                const int S = 128;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MirrorGlare" };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float core = Mathf.Exp(-r * r * 38f);
                    float halo = Mathf.Exp(-r * r * 5f) * 0.45f;
                    // Four soft star streaks.
                    float streak = (Mathf.Exp(-v * v * 900f) + Mathf.Exp(-u * u * 900f)) * Mathf.Clamp01(1f - r) * 0.55f;
                    float a = Mathf.Clamp01(core + halo + streak) * Mathf.Clamp01((1f - r) * 3f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply();
                _glareMat = new Material(Shader.Find("Sprites/Default")) { name = "MirrorGlare", mainTexture = tex, renderQueue = 3500 };
                return _glareMat;
            }
        }
    }
}
