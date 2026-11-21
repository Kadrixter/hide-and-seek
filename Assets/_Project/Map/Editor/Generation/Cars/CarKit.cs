using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Small exterior parts shared by the detailed cars: lamps, plates, mirrors, wipers, handles, exhausts.</summary>
    internal static class CarKit
    {
        // ------------------------------------------------------------------ outlines (2D, u = right, v = up)

        public static Vector2[] Rect(float w, float h, float r = 0.01f, int seg = 3)
        {
            var pts = new List<Vector2>();
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f - 1e-4f);
            Vector2[] cs = { new Vector2(w * 0.5f - r, h * 0.5f - r), new Vector2(-w * 0.5f + r, h * 0.5f - r),
                new Vector2(-w * 0.5f + r, -h * 0.5f + r), new Vector2(w * 0.5f - r, -h * 0.5f + r) };
            for (int c = 0; c < 4; c++)
            for (int i = 0; i <= seg; i++)
            {
                float a = (c + i / (float)seg) * Mathf.PI * 0.5f;
                pts.Add(cs[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return pts.ToArray();
        }

        public static Vector2[] Circle(float r, int n = 20)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++) pts[i] = new Vector2(Mathf.Cos(i * Mathf.PI * 2f / n), Mathf.Sin(i * Mathf.PI * 2f / n)) * r;
            return pts;
        }

        /// <summary>Convex polygon from corner points (counter-clockwise in u/v), corners slightly rounded.</summary>
        public static Vector2[] Poly(params Vector2[] corners) => corners;

        // ------------------------------------------------------------------ lamps

        /// <summary>
        /// Lamp unit: a convex lens face (outline in the plane at <paramref name="c"/>, facing <paramref name="n"/>)
        /// standing <paramref name="proud"/> out of the body, with side walls going <paramref name="depth"/> into it.
        /// Outline u runs along <paramref name="u"/> (pass the car's outward x for front/rear lamps so the left lamp
        /// mirrors itself), v is perpendicular and points along <paramref name="vHint"/> (default up).
        /// </summary>
        public static void Lamp(MeshBuilder mb, Vector3 c, Vector3 n, Vector3 u, Vector2[] outline, Material lens,
            Material wall, float proud = 0.006f, float depth = 0.03f, float bulge = 0f, Vector3? vHint = null)
        {
            n.Normalize();
            Vector3 right = (u - n * Vector3.Dot(u, n)).normalized;
            Vector3 up = Vector3.Cross(n, right).normalized;
            if (Vector3.Dot(up, vHint ?? Vector3.up) < 0f) up = -up;
            Vector3 W(Vector2 p, float o) => c + right * p.x + up * p.y + n * o;
            Vector2 centroid = Vector2.zero;
            foreach (var p in outline) centroid += p;
            centroid /= outline.Length;
            Vector3 cc = W(centroid, proud + bulge);
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                mb.Tri(cc, W(a, proud), W(b, proud), n, n, n, lens);
                Vector3 side = (right * (a.y - b.y) - up * (a.x - b.x)).normalized;
                Vector3 mid = W((a + b) * 0.5f, 0f) - W(centroid, 0f);
                if (Vector3.Dot(side, mid) < 0) side = -side;
                mb.Quad(W(a, proud), W(b, proud), W(b, -depth), W(a, -depth), side, side, side, side, wall);
            }
        }

        /// <summary>Round headlamp: chrome bezel ring, glass lens with a reflector look and a bulb cap.</summary>
        public static void RoundLamp(MeshBuilder mb, Vector3 c, Vector3 n, float r, Material ring, Material lens)
        {
            n.Normalize();
            CarWheels.Lathe(mb, c, n, new[] { new Vector2(r + 0.012f, -0.02f), new Vector2(r + 0.012f, 0.004f), new Vector2(r + 0.004f, 0.012f), new Vector2(r - 0.002f, 0.008f) }, 24, ring);
            CarWheels.Lathe(mb, c, n, new[] { new Vector2(r - 0.001f, 0.006f), new Vector2(r * 0.6f, 0.012f), new Vector2(0f, 0.015f) }, 24, lens);
            CarWheels.Lathe(mb, c + n * 0.0158f, n, new[] { new Vector2(r * 0.18f, 0f), new Vector2(0f, 0.002f) }, 12, ring);
        }

        // ------------------------------------------------------------------ plates, badges

        /// <summary>Polish plate (520 x 114 mm): white plate, blue EU band, black characters, dark frame.</summary>
        public static void Plate(MeshBuilder mb, Vector3 c, Vector3 n, string text, float scale = 1f)
        {
            var lib = mb.Lib;
            n.Normalize();
            Vector3 up = Vector3.up - n * Vector3.Dot(Vector3.up, n);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
            up.Normalize();
            var rot = Quaternion.LookRotation(n, up);
            Vector3 right = rot * Vector3.left;
            float w = 0.52f * scale, h = 0.114f * scale;
            mb.Box(c - n * 0.004f, new Vector3(w + 0.02f, h + 0.02f, 0.008f), lib["Car_Trim"], rot);
            mb.Box(c, new Vector3(w, h, 0.004f), lib["Car_Plate"], rot);
            mb.Box(c - right * (w * 0.5f - 0.022f * scale) + n * 0.0022f, new Vector3(0.04f * scale, h - 0.006f, 0.001f), lib["Car_PlateBlue"], rot);
            mb.Text(text, c + right * (0.022f * scale) + n * 0.0026f, n, up, 0.072f * scale, lib["Text_Black"]);
        }

        // ------------------------------------------------------------------ mirrors, wipers, handles

        /// <summary>Door mirror rooted at <paramref name="root"/> (on the door at the belt, front of the side glass).</summary>
        public static void Mirror(MeshBuilder mb, Vector3 root, float side, Material housing, float reach = 0.14f, float w = 0.2f,
            float h = 0.11f, float stalkUp = 0.04f)
        {
            var lib = mb.Lib;
            var trim = lib["Car_Trim"];
            Vector3 head = root + new Vector3(side * reach, stalkUp, -0.01f);
            CarBlob.Blob(mb, root + new Vector3(side * 0.008f, 0.005f, 0.03f), Quaternion.identity, new Vector3(0.018f, 0.04f, 0.075f), 0.5f, trim, 0.5f);
            CarBlob.Bar(mb, root + new Vector3(side * 0.01f, 0, 0.02f), head + new Vector3(side * 0.03f, 0, 0), Vector3.up, 0.04f, 0.024f, housing, 0.5f, 0.8f);
            var rot = Quaternion.Euler(0, side * -7f, 0);
            Vector3 hc = head + new Vector3(side * w * 0.45f, 0.01f, 0.005f);
            CarBlob.Blob(mb, hc, rot, new Vector3(w * 0.5f, h * 0.5f, 0.055f), 0.42f, housing, 0.55f, 1f, 18, 10);
            Vector3 back = rot * Vector3.back;
            Lamp(mb, hc + back * 0.03f, back, rot * Vector3.right * side, Rect(w - 0.026f, h - 0.024f, 0.03f), lib["Car_MirrorGlass"], trim, 0.004f, 0.01f);
        }

        /// <summary>Wiper lying on the glass from <paramref name="pivot"/> along <paramref name="dir"/>.</summary>
        public static void Wiper(MeshBuilder mb, Vector3 pivot, Vector3 dir, Vector3 glassN, float len)
        {
            var m = mb.Lib["Car_Trim"];
            dir.Normalize();
            var rot = Quaternion.LookRotation(dir, glassN);
            mb.Cylinder(pivot - glassN * 0.01f, pivot + glassN * 0.022f, 0.018f, 10, m);
            mb.Box(pivot + dir * (len * 0.5f) + glassN * 0.022f, new Vector3(0.016f, 0.01f, len), m, rot);
            mb.Box(pivot + dir * (len * 0.55f) + glassN * 0.011f, new Vector3(0.022f, 0.014f, len * 0.95f), m, rot);
        }

        /// <summary>Door handle on the surface point p (outward normal n), bar along z.</summary>
        public static void Handle(MeshBuilder mb, Vector3 p, Vector3 n, Material bar, float len = 0.13f)
        {
            var rot = Quaternion.LookRotation(Vector3.forward, n);
            mb.Box(p + n * 0.002f, new Vector3(0.05f, 0.004f, len + 0.025f), mb.Lib["Car_Seam"], rot);
            mb.Box(p + n * 0.01f, new Vector3(0.026f, 0.014f, len), bar, rot);
        }

        /// <summary>Round exhaust tip pointing back from p.</summary>
        public static void Exhaust(MeshBuilder mb, Vector3 p, float r, float len = 0.14f)
        {
            var lib = mb.Lib;
            CarWheels.Lathe(mb, p + Vector3.back * 0.0f, Vector3.back, new[]
            {
                new Vector2(r * 0.8f, -len), new Vector2(r, -len * 0.6f), new Vector2(r, 0f), new Vector2(r * 0.82f, 0.003f),
                new Vector2(r * 0.8f, -0.02f),
            }, 18, lib["Car_ChromeBright"]);
            CarWheels.Lathe(mb, p + Vector3.back * -0.02f, Vector3.back, new[] { new Vector2(r * 0.8f, 0f), new Vector2(0f, 0f) }, 14, lib["Car_Well"]);
        }

        /// <summary>Thin dark seam line between two surface points (door gaps on flat-ish areas).</summary>
        public static void Strip(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 n, float width, Material m, float thick = 0.003f)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            mb.Box((a + b) * 0.5f + n * (thick * 0.5f), new Vector3(width, thick, d.magnitude), m, Quaternion.LookRotation(d, n));
        }
    }
}
