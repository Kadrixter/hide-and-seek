using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Screen side of being drunk (local player's camera): a faint second image that slowly drifts apart and back
    /// (double vision), a soft blur that grows toward the edges and a light vignette. One cheap full-screen pass;
    /// the component switches itself off when sober, so it costs nothing then. The hands (view model camera) stay sharp.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class DrunkVision : MonoBehaviour
    {
        /// <summary>0..1 shown amount (already smoothed by the caller).</summary>
        public float Amount { get; set; }
        /// <summary>Clock and seed of the shared sway signal, so the ghost image drifts with the swaying view.</summary>
        public float Clock { get; set; }
        public float Seed { get; set; }
        /// <summary>0..1 fading consciousness (collapse): darker, blurrier edges on top of the drunk look.</summary>
        public float Faint { get; set; }

        static readonly int GhostId = Shader.PropertyToID("_Ghost");
        static readonly int GhostMixId = Shader.PropertyToID("_GhostMix");
        static readonly int BlurId = Shader.PropertyToID("_Blur");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        Material _mat;
        Camera _cam;

        void OnEnable()
        {
            _cam = GetComponent<Camera>();
            if (_mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/HnsDrunk");
                if (sh != null && sh.isSupported) _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            float a = Mathf.Clamp01(Amount);
            float faint = Mathf.Clamp01(Faint);
            if (_mat == null || (a < 0.002f && faint < 0.002f))
            {
                Graphics.Blit(src, dst);
                return;
            }

            // The second image slides out mostly sideways, breathes with the sway and turns slowly.
            float spread = Drunkenness.Fbm(Clock * 0.19f, Seed + 91.7f);
            float turn = Drunkenness.Fbm(Clock * 0.08f, Seed + 103.3f);
            float dist = a * 0.0105f * (0.6f + 0.4f * spread);
            float ang = turn * 0.6f;
            float aspect = _cam != null && _cam.pixelHeight > 0 ? (float)_cam.pixelWidth / _cam.pixelHeight : 16f / 9f;
            _mat.SetVector(GhostId, new Vector4(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist * aspect, 0f, 0f));
            _mat.SetFloat(GhostMixId, 0.4f * Mathf.Clamp01(a * 1.5f));
            _mat.SetFloat(BlurId, 0.0028f * a + 0.0022f * faint);
            _mat.SetFloat(VignetteId, 0.3f * a + 0.42f * faint);
            _mat.SetFloat(AspectId, aspect);
            Graphics.Blit(src, dst, _mat);
        }
    }
}
