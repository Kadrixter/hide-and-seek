using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Medkit pickup on the map: the same model as in the hand and on the floor after Q (the item's
    /// <see cref="ItemDef.Model"/>), standing on its bottom with the handle up, faint red glow, slow bob and turn.
    /// </summary>
    public class MedkitView : MonoBehaviour
    {
        private Vector3 _base;

        /// <summary>The model's case hangs this far below its handle (model origin).</summary>
        private const float CaseDepth = 0.37f;

        public static GameObject Create(Vector3 position)
        {
            var root = new GameObject("Medkit");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var view = root.AddComponent<MedkitView>();
            view._base = position;

            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(0f, CaseDepth + 0.005f, 0f);
            ItemDefs.Get(HotbarItem.Medkit)?.Model?.Invoke(holder);

            var lg = new GameObject("Glow");
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.35f, 0.3f);
            l.range = 2.2f;
            l.intensity = 0.6f;
            l.shadows = LightShadows.None;
            return root;
        }

        private void Update()
        {
            transform.position = _base + Vector3.up * (0.02f + 0.02f * Mathf.Sin(Time.time * 2f));
            transform.Rotate(0f, 20f * Time.deltaTime, 0f);
        }
    }
}
