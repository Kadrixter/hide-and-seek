using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Metal rod lying on the map: the blocky pipe leaning against a rack / fence (foot on the floor, top on the
    /// face), with a faint cold glint so it can be found in the dark garage.
    /// </summary>
    public class RodView : MonoBehaviour
    {
        private Light _glint;
        private float _phase;

        public static GameObject Create(Vector3 basePos, Vector3 top)
        {
            var root = new GameObject("MetalRod");
            Vector3 axis = (basePos - top).normalized;          // top -> foot
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            Vector3 fwd = Vector3.Cross(side.normalized, axis);
            root.transform.SetPositionAndRotation(top, Quaternion.LookRotation(fwd, -axis));
            RodModel.CreateLoose(root.transform, 0);

            var view = root.AddComponent<RodView>();
            var lg = new GameObject("Glint");
            lg.transform.SetParent(root.transform, false);
            lg.transform.position = Vector3.Lerp(top, basePos, 0.4f) + Vector3.up * 0.25f;
            view._glint = lg.AddComponent<Light>();
            view._glint.type = LightType.Point;
            view._glint.color = new Color(0.75f, 0.85f, 1f);
            view._glint.range = 1.6f;
            view._glint.intensity = 0.55f;
            view._glint.shadows = LightShadows.None;
            view._phase = Random.Range(0f, 10f);
            return root;
        }

        private void Update()
        {
            if (_glint == null) return;
            // Slow shimmer, like a reflection of a distant lamp.
            float k = 0.5f + 0.5f * Mathf.Sin((Time.time + _phase) * 1.3f);
            _glint.intensity = 0.35f + 0.3f * k * k;
        }
    }
}
