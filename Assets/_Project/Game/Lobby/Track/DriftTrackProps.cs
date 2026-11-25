using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Small shared pieces of the drift track: a tyre, a traffic cone, flat or upright lettering.</summary>
    public static class DriftTrackProps
    {
        public static Material Rubber => TrackMats.Get("TrackRubber", new Color(0.05f, 0.05f, 0.055f), 0.12f);
        private static Mesh _cone;
        private static Material[] _coneMats;

        /// <summary>A lying tyre (outer radius 0.3 m, 0.2 m high) centred at <paramref name="c"/>.</summary>
        public static void Tyre(ProcMesh m, Vector3 c)
        {
            m.At(c).Torus(0.22f, 0.1f, 18, 8);
            m.At(c + Vector3.down * 0.04f).Lathe(new[] { new Vector2(0.14f, 0f), new Vector2(0f, 0f) }, 14); // dark inside
        }

        /// <summary>Traffic cone (orange, white reflective band, black square foot) with a small collider.</summary>
        public static GameObject Cone(Transform parent, Vector3 pos)
        {
            if (_cone == null)
            {
                var orange = new ProcMesh();
                orange.At(Vector3.zero).Lathe(new[] { new Vector2(0.15f, 0.04f), new Vector2(0.12f, 0.2f) }, 16);
                orange.At(Vector3.zero).Lathe(new[] { new Vector2(0.095f, 0.33f), new Vector2(0.025f, 0.7f), new Vector2(0f, 0.71f) }, 16);
                var band = new ProcMesh();
                band.At(Vector3.zero).Lathe(new[] { new Vector2(0.12f, 0.2f), new Vector2(0.095f, 0.33f) }, 16);
                var foot = new ProcMesh();
                foot.At(new Vector3(0f, 0.02f, 0f)).RoundBox(new Vector3(0.38f, 0.04f, 0.38f), 0.01f, 2);
                _cone = Combine(orange.ToMesh("ConeOrange"), band.ToMesh("ConeBand"), foot.ToMesh("ConeFoot"));
                _coneMats = new[]
                {
                    TrackMats.Get("TrackConeOrange", new Color(1f, 0.35f, 0.04f), 0.45f),
                    TrackMats.Glow("TrackConeBand", new Color(0.92f, 0.92f, 0.9f), new Color(0.12f, 0.12f, 0.12f)),
                    TrackMats.Get("TrackConeFoot", new Color(0.05f, 0.05f, 0.05f), 0.2f),
                };
            }
            var go = new GameObject("Cone");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Mathf.Repeat(pos.x * 37f + pos.z * 91f, 90f), 0f); // same on every client
            go.AddComponent<MeshFilter>().sharedMesh = _cone;
            go.AddComponent<MeshRenderer>().sharedMaterials = _coneMats;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.3f, 0f);
            bc.size = new Vector3(0.3f, 0.6f, 0.3f);
            return go;
        }

        private static Mesh Combine(params Mesh[] parts)
        {
            var ci = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++) ci[i] = new CombineInstance { mesh = parts[i], transform = Matrix4x4.identity };
            var m = new Mesh { name = "TrackCone" };
            m.CombineMeshes(ci, false, false);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Lettering (TextMesh, depth-tested) at <paramref name="pos"/>, readable along <paramref name="rot"/>'s forward.</summary>
        public static TextMesh Letters(Transform parent, string text, Vector3 pos, Quaternion rot, float size, Color color, Vector2 fit)
        {
            var go = new GameObject("Letters_" + text);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = size;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            WorldText.Setup(tm, fit);
            return tm;
        }
    }
}
