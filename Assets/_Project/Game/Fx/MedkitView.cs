using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Procedural medkit pickup: white box with a red cross, faint glow, slow bob.</summary>
    public class MedkitView : MonoBehaviour
    {
        private Vector3 _base;

        public static GameObject Create(Vector3 position)
        {
            var root = new GameObject("Medkit");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var view = root.AddComponent<MedkitView>();
            view._base = position;

            var white = new Color(0.86f, 0.86f, 0.82f);
            var red = new Color(0.75f, 0.06f, 0.05f);
            Box(root.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.42f, 0.22f, 0.3f), white);
            Box(root.transform, new Vector3(0f, 0.225f, 0f), new Vector3(0.22f, 0.012f, 0.06f), red);
            Box(root.transform, new Vector3(0f, 0.225f, 0f), new Vector3(0.06f, 0.012f, 0.22f), red);
            Box(root.transform, new Vector3(0f, 0.11f, 0.152f), new Vector3(0.16f, 0.05f, 0.006f), red);
            Box(root.transform, new Vector3(0f, 0.245f, 0f), new Vector3(0.16f, 0.03f, 0.03f), new Color(0.2f, 0.2f, 0.2f));

            var lg = new GameObject("Glow");
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.35f, 0.3f);
            l.range = 2.2f;
            l.intensity = 0.6f;
            l.shadows = LightShadows.None;
            return root;
        }

        private static void Box(Transform parent, Vector3 pos, Vector3 size, Color color)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            var mr = b.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(color);
        }

        private static Material _matCache;
        private static readonly System.Collections.Generic.Dictionary<Color, Material> Mats = new();

        private static Material Mat(Color c)
        {
            if (Mats.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", 0.35f);
            Mats[c] = m;
            return m;
        }

        private void Update()
        {
            transform.position = _base + Vector3.up * (0.02f + 0.02f * Mathf.Sin(Time.time * 2f));
            transform.Rotate(0f, 20f * Time.deltaTime, 0f);
        }
    }
}
