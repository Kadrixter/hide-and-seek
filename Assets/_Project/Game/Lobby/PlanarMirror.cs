using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Real planar mirror for Built-in RP: a hidden camera renders the scene reflected about the mirror plane
    /// (oblique near plane so nothing behind the glass shows up) into a render texture that the Mirror shader
    /// samples in screen space. The local player's own first-person body is shown during that render, so you
    /// can see yourself. Only renders while the mirror is on screen and near.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PlanarMirror : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 18f;
        [SerializeField, Range(0.25f, 1f)] private float resolutionScale = 0.6f;
        [SerializeField] private LayerMask reflectLayers = ~(1 << 9);

        private MeshRenderer _renderer;
        private Camera _mirrorCam;
        private RenderTexture _rt;
        private Material _material;
        private static bool _rendering;
        private static readonly int ReflTex = Shader.PropertyToID("_ReflTex");

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
        }

        private void LateUpdate()
        {
            var src = Camera.main;
            if (src == null || _rendering || !_renderer.isVisible) return;
            Vector3 pos = transform.position;
            Vector3 normal = -transform.forward; // Unity quads face -Z
            if (Vector3.Distance(src.transform.position, pos) > maxDistance) return;
            if (Vector3.Dot(src.transform.position - pos, normal) <= 0f) return; // behind the mirror

            int w = Mathf.Max(64, (int)(Screen.width * resolutionScale));
            int h = Mathf.Max(64, (int)(Screen.height * resolutionScale));
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "MirrorRT", antiAliasing = 2 };
                _material.SetTexture(ReflTex, _rt);
            }

            _mirrorCam.CopyFrom(src);
            _mirrorCam.enabled = false;
            _mirrorCam.targetTexture = _rt;
            _mirrorCam.cullingMask = reflectLayers;
            _mirrorCam.clearFlags = CameraClearFlags.SolidColor;
            _mirrorCam.backgroundColor = Color.black;

            float d = -Vector3.Dot(normal, pos);
            Matrix4x4 reflection = ReflectionMatrix(new Vector4(normal.x, normal.y, normal.z, d));
            _mirrorCam.worldToCameraMatrix = src.worldToCameraMatrix * reflection;
            _mirrorCam.transform.position = reflection.MultiplyPoint(src.transform.position);

            Vector4 clipPlane = CameraSpacePlane(_mirrorCam, pos, normal);
            _mirrorCam.projectionMatrix = src.CalculateObliqueMatrix(clipPlane);

            // Show the local player's own body (normally shadow-only in first person).
            var local = NetPlayer.Local;
            bool restoreFp = local != null && local.Character != null && local.Character.IsFirstPerson;
            var lamp = restoreFp ? local.Character.GetComponentInChildren<ShoulderLamp>() : null;
            if (restoreFp) local.Character.SetFirstPersonMode(false);
            lamp?.SetFirstPerson(false);

            _rendering = true;
            GL.invertCulling = true;
            _mirrorCam.Render();
            GL.invertCulling = false;
            _rendering = false;

            if (restoreFp) local.Character.SetFirstPersonMode(true);
            lamp?.SetFirstPerson(true);
        }

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
