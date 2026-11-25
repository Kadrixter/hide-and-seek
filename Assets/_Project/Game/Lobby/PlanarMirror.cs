using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Real planar mirror for Built-in RP: a hidden camera renders the scene reflected about the mirror plane
    /// (oblique near plane so nothing behind the glass shows up) into a render texture that the Mirror shader
    /// samples in screen space. The local player's own first-person body is shown during that render, so you
    /// can see yourself.
    /// Quality: full screen resolution with MSAA (a scaled-down texture made the edges crawl from far away) and the
    /// reflection is refreshed every frame while the glass is on screen (a stale texture tore when walking away).
    /// Flashlights: a beam hitting the glass bounces off as a mirrored spot light, and a lamp pointed at the
    /// viewer's reflection shows a bright glare in the mirror.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PlanarMirror : MonoBehaviour
    {
        [SerializeField] private float renderDistance = 40f; // renamed: old scenes stored 18 m
        [SerializeField, Range(0.25f, 1.5f)] private float renderScale = 1f; // renamed: old scenes stored 0.6
        [SerializeField] private LayerMask reflectLayers = ~(1 << 9);

        private const float BounceGain = 0.8f;   // a mirror reflects ~80 % of the light
        private const int MaxBounces = 4;

        private MeshRenderer _renderer;
        private Camera _mirrorCam;
        private RenderTexture _rt;
        private Material _material;
        private static bool _rendering;
        private static readonly int ReflTex = Shader.PropertyToID("_ReflTex");

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

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _material = _renderer.material;
            var go = new GameObject("MirrorCamera") { hideFlags = HideFlags.HideAndDontSave };
            _mirrorCam = go.AddComponent<Camera>();
            _mirrorCam.enabled = false;
        }

        private void OnDestroy()
        {
            if (_mirrorCam != null) Destroy(_mirrorCam.gameObject);
            if (_rt != null) _rt.Release();
            foreach (var b in _bounces)
            {
                if (b.Light != null) Destroy(b.Light.gameObject);
                if (b.Glare != null) Destroy(b.Glare.gameObject);
            }
        }

        private Vector3 Normal => -transform.forward; // Unity quads face -Z

        private void LateUpdate()
        {
            UpdateBounces();

            var src = Camera.main;
            if (src == null || _rendering || !_renderer.isVisible) return;
            Vector3 pos = transform.position;
            Vector3 normal = Normal;
            if (Vector3.Distance(src.transform.position, pos) > renderDistance) return;
            if (Vector3.Dot(src.transform.position - pos, normal) <= 0f) return; // behind the mirror

            int w = Mathf.Max(64, (int)(src.pixelWidth * renderScale));
            int h = Mathf.Max(64, (int)(src.pixelHeight * renderScale));
            int aa = Mathf.Clamp(QualitySettings.antiAliasing > 0 ? Mathf.Max(4, QualitySettings.antiAliasing) : 4, 1, 8);
            if (_rt == null || _rt.width != w || _rt.height != h || _rt.antiAliasing != aa)
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default)
                {
                    name = "MirrorRT", antiAliasing = aa, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                _material.SetTexture(ReflTex, _rt);
            }

            _mirrorCam.CopyFrom(src);
            _mirrorCam.enabled = false;
            _mirrorCam.targetTexture = _rt;
            _mirrorCam.cullingMask = reflectLayers;
            _mirrorCam.clearFlags = CameraClearFlags.SolidColor;
            _mirrorCam.backgroundColor = Color.black;
            _mirrorCam.allowMSAA = true;

            float d = -Vector3.Dot(normal, pos);
            Matrix4x4 reflection = ReflectionMatrix(new Vector4(normal.x, normal.y, normal.z, d));
            _mirrorCam.worldToCameraMatrix = src.worldToCameraMatrix * reflection;
            Vector3 camPos = reflection.MultiplyPoint(src.transform.position);
            _mirrorCam.transform.position = camPos;

            Vector4 clipPlane = CameraSpacePlane(_mirrorCam, pos, normal);
            _mirrorCam.projectionMatrix = src.CalculateObliqueMatrix(clipPlane);

            // Show the local player's own body (normally shadow-only in first person). The render modes are put back
            // exactly as they were afterwards (a board-game table hides the body its own way, the setters would undo it).
            var local = NetPlayer.Local;
            _ownRenderers.Clear();
            _ownModes.Clear();
            if (local != null && local.Character != null)
            {
                local.Character.GetComponentsInChildren(true, _ownRenderers);
                foreach (var r in _ownRenderers) _ownModes.Add(r != null ? r.shadowCastingMode : ShadowCastingMode.On);
            }
            bool restoreFp = local != null && local.Character != null && local.Character.IsFirstPerson;
            var lamp = restoreFp ? local.Character.GetComponentInChildren<ShoulderLamp>() : null;
            if (restoreFp) local.Character.SetFirstPersonMode(false);
            lamp?.SetFirstPerson(false);
            // Seated: the body is already drawn, only the head is hidden from the own camera.
            bool restoreHead = local != null && local.Character != null && local.Character.HeadHidden;
            if (restoreHead) local.Character.SetHeadHidden(false);

            // Glare quads exist only for this render (the main camera never sees them).
            ShowGlares(camPos, true);

            _rendering = true;
            GL.invertCulling = true;
            _mirrorCam.Render();
            GL.invertCulling = false;
            _rendering = false;

            ShowGlares(camPos, false);
            if (restoreFp) local.Character.SetFirstPersonMode(true);
            lamp?.SetFirstPerson(true);
            if (restoreHead) local.Character.SetHeadHidden(true);
            for (int i = 0; i < _ownRenderers.Count; i++)
                if (_ownRenderers[i] != null) _ownRenderers[i].shadowCastingMode = _ownModes[i];
        }

        private static readonly List<Renderer> _ownRenderers = new();
        private static readonly List<ShadowCastingMode> _ownModes = new();

        // ================================================================ flashlight bounce

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

        // ================================================================ math

        private static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal)
        {
            const float offset = 0.02f;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(pos + normal * offset);
            Vector3 cnormal = m.MultiplyVector(normal).normalized;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        private static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y; m.m02 = -2f * p.x * p.z; m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x; m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z; m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x; m.m21 = -2f * p.z * p.y; m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }
    }
}
