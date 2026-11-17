using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Slowly orbits a point with a gentle height bob. No input required.</summary>
    public class ShowcaseOrbitCamera : MonoBehaviour
    {
        public Vector3 target = new Vector3(0f, 1f, 0f);
        public float radius = 10f;
        public float height = 2.6f;
        public float degreesPerSecond = 9f;
        public float startAngle = 0f;
        public float heightBob = 0.6f;

        float angle;

        void OnEnable()
        {
            angle = startAngle;
            Apply();
        }

        void LateUpdate()
        {
            angle += degreesPerSecond * Time.deltaTime;
            Apply();
        }

        void Apply()
        {
            float a = angle * Mathf.Deg2Rad;
            float h = height + heightBob * Mathf.Sin(a * 0.5f);
            transform.position = target + new Vector3(Mathf.Sin(a) * radius, h, Mathf.Cos(a) * radius);
            transform.LookAt(target);
        }
    }
}
