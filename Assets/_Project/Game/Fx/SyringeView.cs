using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Procedural adrenaline syringe pickup: barrel with blue liquid, plunger, needle, yellow label, blue glow, slow bob + spin.</summary>
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
            body.localPosition = new Vector3(0f, 0.12f, 0f);
            body.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f); // lying on its side
            view._body = body;

            Cyl(body, new Vector3(0f, 0f, 0f), 0.045f, 0.2f, new Color(0.85f, 0.92f, 0.95f), 0f);          // barrel
            Cyl(body, new Vector3(0f, 0.015f, 0f), 0.036f, 0.15f, new Color(0.15f, 0.55f, 1f), 1.6f);      // liquid (emissive)
            Cyl(body, new Vector3(0f, 0.11f, 0f), 0.07f, 0.012f, new Color(0.9f, 0.9f, 0.9f), 0f);        // finger grip
            Cyl(body, new Vector3(0f, 0.14f, 0f), 0.014f, 0.06f, new Color(0.85f, 0.85f, 0.85f), 0f);     // plunger rod
            Cyl(body, new Vector3(0f, 0.172f, 0f), 0.05f, 0.008f, new Color(0.15f, 0.15f, 0.15f), 0f);    // plunger head
            Cyl(body, new Vector3(0f, -0.108f, 0f), 0.02f, 0.016f, new Color(0.95f, 0.75f, 0.1f), 0f);    // hub
            Cyl(body, new Vector3(0f, -0.15f, 0f), 0.004f, 0.07f, new Color(0.8f, 0.82f, 0.85f), 0f);     // needle
            Cyl(body, new Vector3(0f, 0.04f, 0f), 0.047f, 0.05f, new Color(1f, 0.82f, 0.1f), 0f);         // label

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

        private static void Cyl(Transform parent, Vector3 pos, float dia, float len, Color color, float emission)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(parent, false);
            c.transform.localPosition = pos;
            c.transform.localScale = new Vector3(dia, len * 0.5f, dia);
            var mr = c.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(color, emission);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static readonly Dictionary<(Color, float), Material> Mats = new();

        private static Material Mat(Color c, float emission)
        {
            if (Mats.TryGetValue((c, emission), out var m) && m != null) return m;
            m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", 0.8f);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
            }
            Mats[(c, emission)] = m;
            return m;
        }
    }
}
