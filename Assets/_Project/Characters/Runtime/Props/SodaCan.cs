using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// 330 ml soda can at real size (origin = centre, +Y = top, logo facing local -Z).
    /// Printed body with an embossed label (<see cref="SodaCanArt"/>), aluminium neck, rim and domed bottom,
    /// engraved lid, pull tab that lifts when opened and the drink opening.
    /// </summary>
    public class SodaCan : MonoBehaviour
    {
        public const float Height = 0.122f, Radius = 0.033f;
        const float LidY = 0.0585f, LidR = 0.0285f;
        const int Seg = 40;

        /// <summary>Optional template material (Built-in Standard); set by the game from a scene material.</summary>
        public static Material Template;

        static Mesh bodyMesh, lidMesh, tabMesh, holeMesh;
        static Material alu, lidMat, dark;
        static readonly Material[] paints = new Material[SodaCanArt.BrandCount];

        MeshRenderer body;
        Transform tab;
        GameObject hole;
        int brand = -1;
        float baseScale = 1f;

        /// <summary>0 = round, 1 = squeezed flat in the fist before the throw.</summary>
        public void SetCrush(float amount)
        {
            float c = Mathf.Clamp01(amount);
            transform.localScale = baseScale * new Vector3(1f + 0.18f * c, 1f - 0.34f * c, 1f + 0.1f * c);
        }

        public int Brand
        {
            get => brand;
            set
            {
                int b = ((value % SodaCanArt.BrandCount) + SodaCanArt.BrandCount) % SodaCanArt.BrandCount;
                if (b == brand) return;
                brand = b;
                body.sharedMaterials = new[] { Paint(b), alu };
            }
        }

        public static SodaCan Create(Transform parent, int brand, int layer, float scale = 1f)
        {
            EnsureShared();
            var go = new GameObject("SodaCan") { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var can = go.AddComponent<SodaCan>();
            can.baseScale = scale;
            can.body = Part(go.transform, "Body", bodyMesh, layer, null);
            Part(go.transform, "Lid", lidMesh, layer, lidMat);
            can.tab = new GameObject("TabHinge") { layer = layer }.transform;
            can.tab.SetParent(go.transform, false);
            can.tab.localPosition = new Vector3(0f, LidY + 0.0012f, 0f);
            Part(can.tab, "Tab", tabMesh, layer, alu);
            can.hole = Part(go.transform, "Opening", holeMesh, layer, dark).gameObject;
            can.hole.SetActive(false);
            can.Brand = brand;
            return can;
        }

        /// <summary>0 = closed, 1 = tab lifted and the opening pushed in.</summary>
        public void SetOpen(float amount)
        {
            amount = Mathf.Clamp01(amount);
            // The tab lifts up to ~80 degrees, then falls back down flat once the opening is pushed in.
            float lift = amount < 0.6f ? amount / 0.6f * 80f : Mathf.Lerp(80f, 12f, (amount - 0.6f) / 0.4f);
            tab.localRotation = Quaternion.Euler(-lift, 0f, 0f);
            bool open = amount > 0.45f;
            if (hole.activeSelf != open) hole.SetActive(open);
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, int layer, Material m)
        {
            var g = new GameObject(name) { layer = layer };
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            if (m != null) r.sharedMaterial = m;
            return r;
        }

        // ------------------------------------------------------------------ materials

        static Material NewMat(Color c, float metallic, float gloss)
        {
            Material m = Template != null ? new Material(Template) : new Material(Shader.Find("Standard"));
            m.color = c;
            m.mainTexture = null;
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        static void SetNormal(Material m, Texture2D n, float scale)
        {
            if (!m.HasProperty("_BumpMap")) return;
            m.SetTexture("_BumpMap", n);
            m.SetFloat("_BumpScale", scale);
            m.EnableKeyword("_NORMALMAP");
        }

        static Material Paint(int b)
        {
            if (paints[b] != null) return paints[b];
            var m = NewMat(Color.white, 0.55f, 0.72f);
            m.mainTexture = SodaCanArt.Label(b);
            SetNormal(m, SodaCanArt.LabelNormal(b), 1f);
            m.name = "SodaCanPaint" + b;
            return paints[b] = m;
        }

        static void EnsureShared()
        {
            if (bodyMesh != null) return;
            alu = NewMat(new Color(0.78f, 0.8f, 0.83f), 0.92f, 0.8f);
            alu.name = "SodaCanAlu";
            lidMat = NewMat(Color.white, 0.9f, 0.75f);
            lidMat.mainTexture = SodaCanArt.Lid;
            SetNormal(lidMat, SodaCanArt.LidNormal, 1f);
            lidMat.name = "SodaCanLid";
            dark = NewMat(new Color(0.03f, 0.03f, 0.035f), 0.2f, 0.4f);
            dark.name = "SodaCanHole";
            bodyMesh = BuildBody();
            lidMesh = BuildDisc(LidY, LidR, true);
            holeMesh = BuildHole();
            tabMesh = BuildTab();
        }

        // ------------------------------------------------------------------ meshes

        static Mesh BuildBody()
        {
            // (y, r) profile rings. Printed section = submesh 0, aluminium (bottom dome, neck, rim) = submesh 1.
            var printed = new[] { new Vector2(-0.057f, Radius), new Vector2(0.045f, Radius) };
            var bottom = new[] { new Vector2(-0.0585f, 0.02f), new Vector2(-0.061f, 0.0255f), new Vector2(-0.0605f, 0.029f), new Vector2(-0.059f, 0.0318f), new Vector2(-0.057f, Radius) };
            var neck = new[] { new Vector2(0.045f, Radius), new Vector2(0.051f, 0.0318f), new Vector2(0.0565f, 0.0292f), new Vector2(0.0595f, 0.0286f), new Vector2(0.0608f, 0.0298f), new Vector2(0.0614f, 0.0291f), new Vector2(0.0605f, 0.0280f), new Vector2(LidY, LidR) };
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t0 = new List<int>();
            var t1 = new List<int>();
            Revolve(printed, true, v, n, uv, t0);
            Revolve(bottom, false, v, n, uv, t1);
            Revolve(neck, false, v, n, uv, t1);
            // Bottom dome cap.
            int c = v.Count;
            v.Add(new Vector3(0f, -0.056f, 0f)); n.Add(Vector3.down); uv.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= Seg; j++)
            {
                float a = j * Mathf.PI * 2f / Seg;
                v.Add(new Vector3(Mathf.Cos(a) * 0.02f, -0.0585f, Mathf.Sin(a) * 0.02f)); n.Add(Vector3.down); uv.Add(Vector2.zero);
                if (j > 0) { t1.Add(c); t1.Add(c + j); t1.Add(c + j + 1); }
            }
            var m = new Mesh { name = "SodaCanBody" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.subMeshCount = 2;
            m.SetTriangles(t0, 0);
            m.SetTriangles(t1, 1);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        static void Revolve(Vector2[] prof, bool label, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris)
        {
            int start = v.Count;
            float y0 = prof[0].x, y1 = prof[prof.Length - 1].x;
            for (int i = 0; i < prof.Length; i++)
            for (int j = 0; j <= Seg; j++)
            {
                // u = 0 at +X going round towards +Z; u = 0.75 faces -Z (front), text reads left to right from outside.
                float u = (float)j / Seg, a = u * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * prof[i].y, prof[i].x, Mathf.Sin(a) * prof[i].y));
                n.Add(Vector3.zero);
                uv.Add(new Vector2(u, label ? Mathf.InverseLerp(y0, y1, prof[i].x) : 0.5f));
            }
            for (int i = 0; i + 1 < prof.Length; i++)
            for (int j = 0; j < Seg; j++)
            {
                int a = start + i * (Seg + 1) + j, b = a + Seg + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }

        static Mesh BuildDisc(float y, float r, bool up)
        {
            var v = new List<Vector3> { new Vector3(0f, y, 0f) };
            var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var t = new List<int>();
            for (int j = 0; j <= Seg; j++)
            {
                float a = j * Mathf.PI * 2f / Seg, x = Mathf.Cos(a), z = Mathf.Sin(a);
                v.Add(new Vector3(x * r, y, z * r));
                uv.Add(new Vector2(0.5f + x * 0.5f, 0.5f - z * 0.5f)); // +V on the lid texture = -Z (front)
                if (j > 0) { if (up) { t.Add(0); t.Add(j + 1); t.Add(j); } else { t.Add(0); t.Add(j); t.Add(j + 1); } }
            }
            var m = new Mesh { name = "SodaCanLid" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static Mesh BuildHole()
        {
            var v = new List<Vector3> { new Vector3(0f, LidY + 0.0004f, -0.012f) };
            var t = new List<int>();
            for (int j = 0; j <= 24; j++)
            {
                float a = j * Mathf.PI * 2f / 24f;
                v.Add(new Vector3(Mathf.Cos(a) * 0.0086f, LidY + 0.0004f, -0.012f + Mathf.Sin(a) * 0.0103f));
                if (j > 0) { t.Add(0); t.Add(j + 1); t.Add(j); }
            }
            var m = new Mesh { name = "SodaCanHole" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Pull tab: nose over the opening (-Z), rivet at the origin, finger ring towards +Z.</summary>
        static Mesh BuildTab()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            void Box(Vector3 min, Vector3 max)
            {
                int s = v.Count;
                for (int i = 0; i < 8; i++) v.Add(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
                int[] q = { 0, 2, 3, 1, 4, 5, 7, 6, 0, 1, 5, 4, 2, 6, 7, 3, 0, 4, 6, 2, 1, 3, 7, 5 };
                for (int f = 0; f < 6; f++)
                {
                    int a = s + q[f * 4], b = s + q[f * 4 + 1], c = s + q[f * 4 + 2], d = s + q[f * 4 + 3];
                    t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d);
                }
            }
            const float th = 0.0008f;
            Box(new Vector3(-0.0055f, 0f, -0.007f), new Vector3(0.0055f, th, 0.003f));   // nose + rivet plate
            Box(new Vector3(-0.0065f, 0f, 0.003f), new Vector3(-0.0035f, th, 0.019f));   // ring sides
            Box(new Vector3(0.0035f, 0f, 0.003f), new Vector3(0.0065f, th, 0.019f));
            Box(new Vector3(-0.0065f, 0f, 0.016f), new Vector3(0.0065f, th, 0.0205f));   // ring end
            var m = new Mesh { name = "SodaCanTab" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
