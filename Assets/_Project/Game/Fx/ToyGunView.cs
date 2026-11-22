using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Toy slime blaster lying on the floor of the garage: the model on its side with a soft green glow pulsing from
    /// the goo tank, so it can be spotted in the dark.
    /// </summary>
    public class ToyGunView : MonoBehaviour
    {
        private Light _glow;
        private float _phase;

        public static GameObject Create(Vector3 pos, float yaw, float level)
        {
            var root = new GameObject("ToyGunPickup");
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            // Lying on its left side: grip pointing sideways, body resting on the floor.
            holder.localPosition = new Vector3(0f, 0.046f, 0f);
            holder.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, 0f, 0f);
            var parts = ToyGunModel.Create(holder, 0, true);
            parts.Root.localPosition = new Vector3(0f, 0.07f, -0.12f);
            ToyGunModel.SetLevel(parts, level);

            var view = root.AddComponent<ToyGunView>();
            var lg = new GameObject("Glow");
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            view._glow = lg.AddComponent<Light>();
            view._glow.type = LightType.Point;
            view._glow.color = new Color(0.45f, 1f, 0.35f);
            view._glow.range = 1.5f;
            view._glow.intensity = 0.6f;
            view._glow.shadows = LightShadows.None;
            view._phase = Random.Range(0f, 10f);
            return root;
        }

        private void Update()
        {
            if (_glow == null) return;
            float k = 0.5f + 0.5f * Mathf.Sin((Time.time + _phase) * 2.1f);
            _glow.intensity = 0.4f + 0.45f * k * k;
        }
    }
}
