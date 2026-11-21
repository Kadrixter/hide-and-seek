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
        const int Seg = 48;

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
            transform.localScale = baseScale * new Vector3(1f, 1f - 0.3f * c, 1f);
            if (Mathf.Abs(c - crushDone) < 0.03f && !(c == 0f && crushDone != 0f)) return;
            crushDone = c;
            if (c <= 0f) { body.GetComponent<MeshFilter>().sharedMesh = bodyMesh; return; }
            if (crushed == null) { crushed = Instantiate(bodyMesh); crushed.name = "SodaCanCrushed"; crushSeed = Random.Range(0f, 6.28f); }
            var src = bodyMesh.vertices;
            var dst = new Vector3[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = Crumple(src[i], c, crushSeed);
            crushed.vertices = dst;
            crushed.RecalculateNormals();
            crushed.RecalculateTangents();
            crushed.RecalculateBounds();
            body.GetComponent<MeshFilter>().sharedMesh = crushed;
        }

        Mesh crushed;
        float crushDone, crushSeed;

        void OnDestroy()
        {
            if (crushed != null) Destroy(crushed);
        }

        /// <summary>Squeezed in a fist: pinched waist, irregular dents and folds, squashed oval; ends stay round.</summary>
        static Vector3 Crumple(Vector3 p, float c, float seed)
        {
            float h = Mathf.InverseLerp(-0.061f, 0.061f, p.y);
            float bump = Mathf.Sin(Mathf.PI * Mathf.Clamp01((h - 0.1f) / 0.75f));
            float r = new Vector2(p.x, p.z).magnitude;
            if (r < 1e-5f) return p;
            float a = Mathf.Atan2(p.z, p.x);
            float dent = 0.14f * Mathf.Sin(5f * a + seed + h * 6f) + 0.09f * Mathf.Sin(9f * a - seed * 2f + h * 22f)
                         + 0.05f * Mathf.Sin(17f * a + h * 40f);
            float k = 1f + c * bump * (dent - 0.24f);
            float x = Mathf.Cos(a) * r * k * (1f + 0.22f * c * bump), z = Mathf.Sin(a) * r * k * (1f - 0.28f * c * bump);
            float y = p.y + c * bump * 0.005f * Mathf.Sin(7f * a + seed);
            return new Vector3(x, y, z);
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
            // Printed section in 14 rings, so the body can be crumpled.
            var printed = new Vector2[14];
            for (int i = 0; i < printed.Length; i++) printed[i] = new Vector2(Mathf.Lerp(-0.057f, 0.045f, i / 13f), Radius);
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
                uv.Add(new Vector2(0.5f + x * 0.5f, 0.5f + z * 0.5f)); // lid texture reads upright from the front; opening at -V = -Z
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

        /// <summary>
        /// Pull tab cut from an outline: rounded nose over the opening (-Z), rivet pad at the origin, finger ring with a
        /// rounded hole towards +Z. Sampled on a fine grid and extruded (top, bottom and boundary walls), slightly domed.
        /// </summary>
        static Mesh BuildTab()
        {
            const float cell = 0.0005f, th = 0.0007f;
            const int nx = 30, nz = 60;           // 15 mm x 30 mm
            const float x0 = -0.0075f, z0 = -0.009f;
            static float Capsule(float x, float z, float zA, float zB, float r)
            {
                float cz = Mathf.Clamp(z, zA, zB);
                return Mathf.Sqrt(x * x + (z - cz) * (z - cz)) - r;
            }
            bool Solid(int i, int k)
            {
                if (i < 0 || k < 0 || i >= nx || k >= nz) return false;
                float x = x0 + (i + 0.5f) * cell, z = z0 + (k + 0.5f) * cell;
                bool outer = Capsule(x, z, -0.004f, 0.0155f, 0.0062f) < 0f;
                bool hole = Capsule(x, z, 0.0085f, 0.0145f, 0.0037f) < 0f;
                return outer && !hole;
            }
            var v = new List<Vector3>();
            var t = new List<int>();
            float Dome(float x, float z) => 0.0004f * (1f - Mathf.Clamp01((x * x) / (0.0062f * 0.0062f)));
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int s0 = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                t.Add(s0); t.Add(s0 + 1); t.Add(s0 + 2); t.Add(s0); t.Add(s0 + 2); t.Add(s0 + 3);
            }
            for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                if (!Solid(i, k)) continue;
                float xa = x0 + i * cell, xb = xa + cell, za = z0 + k * cell, zb = za + cell;
                float ya = Dome(xa, za), yb = Dome(xb, za);
                // Top (+Y) and bottom (-Y).
                Quad(new Vector3(xa, th + ya, za), new Vector3(xa, th + ya, zb), new Vector3(xb, th + yb, zb), new Vector3(xb, th + yb, za));
                Quad(new Vector3(xa, ya, za), new Vector3(xb, yb, za), new Vector3(xb, yb, zb), new Vector3(xa, ya, zb));
                // Walls on the outline.
                if (!Solid(i - 1, k)) Quad(new Vector3(xa, ya, za), new Vector3(xa, ya, zb), new Vector3(xa, th + ya, zb), new Vector3(xa, th + ya, za));
                if (!Solid(i + 1, k)) Quad(new Vector3(xb, yb, zb), new Vector3(xb, yb, za), new Vector3(xb, th + yb, za), new Vector3(xb, th + yb, zb));
                if (!Solid(i, k - 1)) Quad(new Vector3(xb, yb, za), new Vector3(xa, ya, za), new Vector3(xa, th + ya, za), new Vector3(xb, th + yb, za));
                if (!Solid(i, k + 1)) Quad(new Vector3(xa, ya, zb), new Vector3(xb, yb, zb), new Vector3(xb, th + yb, zb), new Vector3(xa, th + ya, zb));
            }
            // Rivet head on the pad.
            int c0 = v.Count;
            v.Add(new Vector3(0f, th + 0.0011f, 0f));
            for (int j = 0; j <= 16; j++)
            {
                float a = j * Mathf.PI * 2f / 16f;
                v.Add(new Vector3(Mathf.Cos(a) * 0.0022f, th + 0.0004f, Mathf.Sin(a) * 0.0022f));
                if (j > 0) { t.Add(c0); t.Add(c0 + j + 1); t.Add(c0 + j); }
            }
            var m = new Mesh { name = "SodaCanTab" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
