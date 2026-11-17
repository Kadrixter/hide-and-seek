using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Sets a sky/ground ambient probe at runtime so the studio looks right without baked lighting data.
    /// </summary>
    [ExecuteAlways]
    public class ShowcaseAmbient : MonoBehaviour
    {
        public Color sky = new Color(0.42f, 0.46f, 0.56f);
        public Color equator = new Color(0.36f, 0.36f, 0.38f);
        public Color ground = new Color(0.20f, 0.18f, 0.17f);

        void OnEnable()
        {
            Apply(sky, equator, ground);
        }

        public static void Apply(Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;

            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(equator);
            // Approximate the trilight gradient: sky from above, ground from below.
            sh.AddDirectionalLight(Vector3.up, (sky - equator) * 1.2f, 1f);
            sh.AddDirectionalLight(Vector3.down, (ground - equator) * 1.2f, 1f);
            RenderSettings.ambientProbe = sh;
        }
    }
}
