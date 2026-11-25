using System.Collections.Generic;
using HideAndSeek.Game.Gfx;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Real planar mirror for Built-in RP: a hidden camera renders the scene reflected about the mirror plane
    /// (oblique near plane so nothing behind the glass shows up) into a render texture that the Mirror shader
    /// samples by projecting the glass through that camera (so a reflection kept for a frame or two stays in place
    /// instead of sliding with the view). The local player's own first-person body is shown during that render, so
    /// you can see yourself.
    /// Quality follows the "Lustro" graphics option (<see cref="MirrorSettings"/>): only the glass's part of the
    /// screen is rendered, at a resolution / MSAA / refresh rate / lighting set by the level; "wyłączone" releases the
    /// render texture, never renders the camera and shows a static dark glass instead. High is the original look:
    /// full screen resolution with MSAA, refreshed every frame while the glass is on screen.
    /// Flashlight bounce and glare: PlanarMirror.Bounce.cs; maths and the lighting scope: PlanarMirror.View.cs.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public partial class PlanarMirror : MonoBehaviour
    {
        [SerializeField] private float renderDistance = 40f; // renamed: old scenes stored 18 m
        [SerializeField, Range(0.25f, 1.5f)] private float renderScale = 1f; // renamed: old scenes stored 0.6
        [SerializeField] private LayerMask reflectLayers = ~(1 << 9);

        private const string OffKeyword = "MIRROR_OFF";

        private MeshRenderer _renderer;
        private Camera _mirrorCam;
        private RenderTexture _rt;
        private Material _material;
        private int _level = -1;
        private int _lastRender = -1000;
        private bool _hasImage;
        private Vector2Int _viewport;
        private static bool _rendering;
        private static readonly int ReflTex = Shader.PropertyToID("_ReflTex");
        private static readonly int MirrorVp = Shader.PropertyToID("_MirrorVP");
        private static readonly int ReflRect = Shader.PropertyToID("_ReflRect");

        /// <summary>Test statistics: reflection renders so far and the CPU time they took, ms.</summary>
        public static int RenderCount;
        public static double RenderMsTotal;

        /// <summary>One-line state for test logs: level, camera, render texture and the rendered part of it.</summary>
        public string Describe() =>
            $"level={MirrorSettings.Name(_level)} rt={(_rt != null ? $"{_rt.width}x{_rt.height} msaa{_rt.antiAliasing} created={_rt.IsCreated()}" : "none")} " +
            $"viewport={_viewport.x}x{_viewport.y} lastRenderFrame={_lastRender} camEnabled={(_mirrorCam != null && _mirrorCam.enabled)} " +
            $"hdr={(_mirrorCam != null && _mirrorCam.allowHDR)} offLook={_material.IsKeywordEnabled(OffKeyword)}";

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _material = _renderer.material;
            _material.EnableKeyword(OffKeyword); // plain glass until the first reflection exists
            var go = new GameObject("MirrorCamera") { hideFlags = HideFlags.HideAndDontSave };
            _mirrorCam = go.AddComponent<Camera>();
            _mirrorCam.enabled = false;
        }

        private void OnDestroy()
        {
            if (_mirrorCam != null) Destroy(_mirrorCam.gameObject);
            FreeTexture();
            DestroyBounces();
        }

        private Vector3 Normal => -transform.forward; // Unity quads face -Z

        private void LateUpdate()
        {
            UpdateBounces();

            int level = MirrorSettings.Effective;
            if (level != _level)
            {
                _level = level;
                _lastRender = -1000;
                if (level == MirrorSettings.Off) TurnOff();
            }
            if (level == MirrorSettings.Off) return;
            var q = MirrorSettings.Levels[level];

            var src = Camera.main;
            if (src == null || _rendering || !_renderer.isVisible) return;
            Vector3 pos = transform.position;
            Vector3 normal = Normal;
            if (Vector3.Distance(src.transform.position, pos) > Mathf.Min(renderDistance, q.RenderDistance)) return;
            if (Vector3.Dot(src.transform.position - pos, normal) <= 0f) return; // behind the mirror
            if (Time.frameCount - _lastRender < q.Interval) return;              // reuse the last reflection
            if (!ScreenRect(src, q.Interval > 1 ? 0.12f : 0.02f, out Rect ndc)) return;

            EnsureTexture(src, q);
            RenderReflection(src, q, ndc, pos, normal);
            _lastRender = Time.frameCount;
            if (!_hasImage)
            {
                _hasImage = true;
                _material.DisableKeyword(OffKeyword);
            }
        }

        /// <summary>"Wyłączone": no texture, no renders, the shader's static glass look.</summary>
        private void TurnOff()
        {
            FreeTexture();
            _hasImage = false;
            _viewport = Vector2Int.zero;
            _material.EnableKeyword(OffKeyword);
        }

        private void FreeTexture()
        {
            if (_rt == null) return;
            if (_mirrorCam != null) _mirrorCam.targetTexture = null;
            if (_material != null) _material.SetTexture(ReflTex, null);
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        private void EnsureTexture(Camera src, in MirrorSettings.Level q)
        {
            float scale = q.Scale * renderScale;
            int w = Mathf.Max(64, (int)(src.pixelWidth * scale));
            int h = Mathf.Max(64, (int)(src.pixelHeight * scale));
            int aa = q.Msaa >= 4 ? Mathf.Clamp(Mathf.Max(4, QualitySettings.antiAliasing), 1, 8) : Mathf.Max(1, q.Msaa);
            if (_rt != null && _rt.width == w && _rt.height == h && _rt.antiAliasing == aa) return;
            FreeTexture(); // the render right after this fills the new texture
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default)
            {
                name = "MirrorRT", antiAliasing = aa, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            _material.SetTexture(ReflTex, _rt);
        }

        private void RenderReflection(Camera src, in MirrorSettings.Level q, Rect ndc, Vector3 pos, Vector3 normal)
        {
            _mirrorCam.CopyFrom(src);
            _mirrorCam.enabled = false;
            _mirrorCam.targetTexture = _rt;
            _mirrorCam.cullingMask = reflectLayers & ~q.SkipLayers;
            _mirrorCam.clearFlags = CameraClearFlags.SolidColor;
            _mirrorCam.backgroundColor = Color.black;
            _mirrorCam.allowMSAA = true;
            _mirrorCam.allowHDR = src.allowHDR && q.Hdr;

            // Only the glass's part of the screen, into the bottom-left corner of the texture.
            int pw = Mathf.Clamp(Mathf.CeilToInt(ndc.width * 0.5f * _rt.width), 8, _rt.width);
            int ph = Mathf.Clamp(Mathf.CeilToInt(ndc.height * 0.5f * _rt.height), 8, _rt.height);
            _viewport = new Vector2Int(pw, ph);
            _mirrorCam.rect = new Rect(0f, 0f, pw / (float)_rt.width, ph / (float)_rt.height);

            float d = -Vector3.Dot(normal, pos);
            Matrix4x4 reflection = ReflectionMatrix(new Vector4(normal.x, normal.y, normal.z, d));
            _mirrorCam.worldToCameraMatrix = src.worldToCameraMatrix * reflection;
            Vector3 camPos = reflection.MultiplyPoint(src.transform.position);
            _mirrorCam.transform.position = camPos;

            float far = q.FarClip > 0f ? Mathf.Min(q.FarClip, src.farClipPlane) : src.farClipPlane;
            _mirrorCam.projectionMatrix = far < src.farClipPlane
                ? Matrix4x4.Perspective(src.fieldOfView, src.aspect, src.nearClipPlane, far)
                : src.projectionMatrix;
            Matrix4x4 oblique = _mirrorCam.CalculateObliqueMatrix(CameraSpacePlane(_mirrorCam, pos, normal));
            _mirrorCam.projectionMatrix = Crop(ndc) * oblique;
            _material.SetMatrix(MirrorVp, _mirrorCam.projectionMatrix * _mirrorCam.worldToCameraMatrix);
            _material.SetVector(ReflRect, new Vector4(pw / (float)_rt.width, ph / (float)_rt.height, 0.5f / _rt.width, 0.5f / _rt.height));

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            ShowOwnBody();
            ShowGlares(camPos, true); // glare quads exist only for this render (the main camera never sees them)
            var lighting = LowerLighting(q);

            _rendering = true;
            GL.invertCulling = true;
            _mirrorCam.Render();
            GL.invertCulling = false;
            _rendering = false;

            RestoreLighting(lighting);
            ShowGlares(camPos, false);
            RestoreOwnBody();
            RenderCount++;
            RenderMsTotal += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        // ================================================================ own body in the reflection

        private static readonly List<Renderer> _ownRenderers = new();
        private static readonly List<ShadowCastingMode> _ownModes = new();
        private static NetPlayer _ownPlayer;
        private static ShoulderLamp _ownLamp;
        private static bool _restoreFp, _restoreHead;

        /// <summary>Shows the local player's own body (normally shadow-only in first person) for the reflection render.</summary>
        private static void ShowOwnBody()
        {
            var local = NetPlayer.Local;
            _ownPlayer = local != null && local.Character != null ? local : null;
            _ownRenderers.Clear();
            _ownModes.Clear();
            _restoreFp = _restoreHead = false;
            _ownLamp = null;
            if (_ownPlayer == null) return;
            var ch = _ownPlayer.Character;
            ch.GetComponentsInChildren(true, _ownRenderers);
            foreach (var r in _ownRenderers) _ownModes.Add(r != null ? r.shadowCastingMode : ShadowCastingMode.On);
            _restoreFp = ch.IsFirstPerson;
            _ownLamp = _restoreFp ? ch.GetComponentInChildren<ShoulderLamp>() : null;
            if (_restoreFp) ch.SetFirstPersonMode(false);
            _ownLamp?.SetFirstPerson(false);
            // Seated: the body is already drawn, only the head is hidden from the own camera.
            _restoreHead = ch.HeadHidden;
            if (_restoreHead) ch.SetHeadHidden(false);
        }

        /// <summary>
        /// Puts the render modes back exactly as they were (a board-game table hides the body its own way, the
        /// setters would undo it).
        /// </summary>
        private static void RestoreOwnBody()
        {
            if (_ownPlayer == null || _ownPlayer.Character == null) return;
            var ch = _ownPlayer.Character;
            if (_restoreFp) ch.SetFirstPersonMode(true);
            _ownLamp?.SetFirstPerson(true);
            if (_restoreHead) ch.SetHeadHidden(true);
            for (int i = 0; i < _ownRenderers.Count; i++)
                if (_ownRenderers[i] != null) _ownRenderers[i].shadowCastingMode = _ownModes[i];
            _ownPlayer = null;
        }
    }
}
