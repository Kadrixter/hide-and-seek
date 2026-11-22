using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Adrenaline syringe pickup on the map: the same model as in the hand and on the floor after Q (the item's
    /// <see cref="ItemDef.Model"/>), lying on its side, blue glow, slow bob + spin.
    /// </summary>
    public class SyringeView : MonoBehaviour
    {
        private Vector3 _base;
        private Transform _body;

        public static GameObject Create(Vector3 position)
        {
            var root = new GameObject("AdrenalineSyringe");
            root.transform.position = position;
            var view = root.AddComponent<SyringeView>();
            view._base = position;
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            // Axis along the model's Z (horizontal): lying flat, the plunger plate is the widest part (~5 cm).
            body.localPosition = new Vector3(0f, 0.05f, 0f);
            body.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            view._body = body;
            ItemDefs.Get(HotbarItem.Syringe)?.Model?.Invoke(body);

            var lg = new GameObject("Glow");
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.3f, 0.65f, 1f);
            l.range = 2.2f;
            l.intensity = 0.8f;
            l.shadows = LightShadows.None;
            return root;
        }

        private void Update()
        {
            float t = Time.time;
            transform.position = _base + Vector3.up * (0.03f + 0.03f * Mathf.Sin(t * 2.1f));
            if (_body != null) _body.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
        }
    }
}
