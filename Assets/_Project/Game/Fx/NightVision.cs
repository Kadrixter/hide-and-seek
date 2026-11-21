using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Grey night vision for dead players (spectators): a dim light on the camera lets them see in the dark, a
    /// post effect turns the picture into amplified grey with a little grain.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class NightVision : MonoBehaviour
    {
        private Material _mat;
        private Light _light;

        private void OnEnable()
        {
            var sh = Resources.Load<Shader>("Shaders/HnsNightVision");
            if (sh != null) _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            _mat?.SetFloat("_Gain", 2.4f);
            _mat?.SetFloat("_Noise", 0.07f);
            if (_light == null)
            {
                _light = new GameObject("NightVisionLight").AddComponent<Light>();
                _light.transform.SetParent(transform, false);
                _light.type = LightType.Point;
                _light.range = 42f;
                _light.intensity = 0.55f;
                _light.shadows = LightShadows.None;
                _light.color = new Color(0.9f, 1f, 0.9f);
            }
            _light.enabled = true;
        }

        private void OnDisable()
        {
            if (_light != null) _light.enabled = false;
            if (_mat != null) Destroy(_mat);
        }

        private void OnDestroy()
        {
            if (_light != null) Destroy(_light.gameObject);
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat != null) Graphics.Blit(src, dst, _mat);
            else Graphics.Blit(src, dst);
        }
    }
}
