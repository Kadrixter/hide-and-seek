using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Make badges built from simple shapes (rings, bars, discs) - no copied artwork.</summary>
    internal static class CarBadges
    {
        private static Quaternion Frame(Vector3 n, out Vector3 right, out Vector3 up)
        {
            n.Normalize();
            up = Vector3.up - n * Vector3.Dot(Vector3.up, n);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
            up.Normalize();
            right = Vector3.Cross(up, n).normalized;
            return Quaternion.LookRotation(n, up);
        }

        private static void Ring(MeshBuilder mb, Vector3 c, Vector3 n, float r, float w, Material m) =>
            CarWheels.Lathe(mb, c, n, new[] { new Vector2(r, 0f), new Vector2(r, w * 0.5f), new Vector2(r - w, w * 0.6f), new Vector2(r - w, 0f) }, 24, m);

        private static void Disc(MeshBuilder mb, Vector3 c, Vector3 n, float r, Material m) =>
            CarWheels.Lathe(mb, c, n, new[] { new Vector2(r, 0f), new Vector2(0f, 0f) }, 24, m);

        private static void Stroke(MeshBuilder mb, Vector3 c, Vector3 right, Vector3 up, Vector3 n, Vector2 a, Vector2 b, float w, Material m) =>
            CarBlob.Bar(mb, c + right * a.x + up * a.y, c + right * b.x + up * b.y, n, w, w * 0.6f, m, 0.5f);

        /// <summary>VW: chrome ring, dark blue field, chrome V over W.</summary>
        public static void Vw(MeshBuilder mb, Vector3 c, Vector3 n, float r)
        {
            var lib = mb.Lib;
            Frame(n, out var right, out var up);
            var chrome = lib["Car_ChromeBright"];
            Disc(mb, c + n * 0.002f, n, r * 0.93f, lib["Car_PlateBlue"]);
            Ring(mb, c, n, r, r * 0.12f, chrome);
            Vector3 cc = c + n * 0.006f;
            float w = r * 0.13f;
            Stroke(mb, cc, right, up, n, new Vector2(-0.48f, 0.62f) * r, new Vector2(0f, -0.08f) * r, w, chrome);
            Stroke(mb, cc, right, up, n, new Vector2(0.48f, 0.62f) * r, new Vector2(0f, -0.08f) * r, w, chrome);
            Vector2[] wpts = { new Vector2(-0.78f, 0.3f), new Vector2(-0.38f, -0.72f), new Vector2(0f, -0.12f), new Vector2(0.38f, -0.72f), new Vector2(0.78f, 0.3f) };
            for (int i = 0; i < wpts.Length - 1; i++) Stroke(mb, cc, right, up, n, wpts[i] * r, wpts[i + 1] * r, w, chrome);
        }

        /// <summary>Mazda: chrome oval with the winged V inside.</summary>
        public static void Mazda(MeshBuilder mb, Vector3 c, Vector3 n, float r)
        {
            var lib = mb.Lib;
            Frame(n, out var right, out var up);
            var chrome = lib["Car_ChromeBright"];
            // oval = ring squashed by building it from strokes
            int seg = 20;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Stroke(mb, c, right, up, n, new Vector2(Mathf.Cos(a0) * 1.25f, Mathf.Sin(a0) * 0.82f) * r,
                    new Vector2(Mathf.Cos(a1) * 1.25f, Mathf.Sin(a1) * 0.82f) * r, r * 0.12f, chrome);
            }
            Vector3 cc = c + n * 0.004f;
            float w = r * 0.12f;
            Stroke(mb, cc, right, up, n, new Vector2(-0.95f, 0.25f) * r, new Vector2(-0.35f, -0.05f) * r, w, chrome);
            Stroke(mb, cc, right, up, n, new Vector2(-0.35f, -0.05f) * r, new Vector2(0f, -0.5f) * r, w, chrome);
            Stroke(mb, cc, right, up, n, new Vector2(0.95f, 0.25f) * r, new Vector2(0.35f, -0.05f) * r, w, chrome);
            Stroke(mb, cc, right, up, n, new Vector2(0.35f, -0.05f) * r, new Vector2(0f, -0.5f) * r, w, chrome);
            Stroke(mb, cc, right, up, n, new Vector2(0f, -0.5f) * r, new Vector2(0f, 0.35f) * r, w, chrome);
        }

        /// <summary>BMW roundel: chrome rim, black ring, blue / white quarters.</summary>
        public static void Bmw(MeshBuilder mb, Vector3 c, Vector3 n, float r)
        {
            var lib = mb.Lib;
            var rot = Frame(n, out _, out _);
            Ring(mb, c, n, r, r * 0.18f, lib["Car_ChromeBright"]);
            Disc(mb, c + n * 0.002f, n, r * 0.83f, lib["Plastic_Black"]);
            float q = r * 0.28f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 off = rot * new Vector3((i % 2 == 0 ? 1 : -1) * q, (i < 2 ? 1 : -1) * q, 0.0035f);
                mb.Box(c + off, new Vector3(q * 2f, q * 2f, 0.001f), (i == 0 || i == 3) ? lib["Car_BadgeBlue"] : lib["Car_Plate"], rot);
            }
        }

        /// <summary>Peugeot: chrome lion standing on its hind legs, simplified to a few strokes on a small plinth.</summary>
        public static void Peugeot(MeshBuilder mb, Vector3 c, Vector3 n, float h)
        {
            var lib = mb.Lib;
            Frame(n, out var right, out var up);
            var chrome = lib["Car_ChromeBright"];
            float w = h * 0.14f;
            Vector2[] body = { new Vector2(-0.05f, -0.5f), new Vector2(0.05f, -0.05f), new Vector2(0.0f, 0.3f), new Vector2(0.12f, 0.48f) }; // leg, torso, neck, head
            for (int i = 0; i < body.Length - 1; i++) Stroke(mb, c, right, up, n, body[i] * h, body[i + 1] * h, w * 1.6f, chrome);
            Stroke(mb, c, right, up, n, new Vector2(0.03f, 0.2f) * h, new Vector2(0.26f, 0.36f) * h, w, chrome);  // raised paw
            Stroke(mb, c, right, up, n, new Vector2(0.0f, 0.05f) * h, new Vector2(0.22f, 0.12f) * h, w, chrome);  // paw
            Stroke(mb, c, right, up, n, new Vector2(-0.05f, -0.05f) * h, new Vector2(-0.25f, 0.2f) * h, w * 0.8f, chrome); // tail
            Stroke(mb, c, right, up, n, new Vector2(0.05f, -0.15f) * h, new Vector2(0.2f, -0.5f) * h, w, chrome);  // second leg
        }

        /// <summary>Lamborghini: gold-rimmed black shield with a gold bar for the bull.</summary>
        public static void Lambo(MeshBuilder mb, Vector3 c, Vector3 n, float h)
        {
            var lib = mb.Lib;
            Frame(n, out var right, out _);
            var shield = new[] { new Vector2(-0.4f, 0.5f), new Vector2(0.4f, 0.5f), new Vector2(0.4f, -0.1f), new Vector2(0f, -0.5f), new Vector2(-0.4f, -0.1f) };
            for (int i = 0; i < shield.Length; i++) shield[i] *= h;
            CarKit.Lamp(mb, c, n, right, shield, lib["Car_BadgeGold"], lib["Car_BadgeGold"], 0.003f, 0.002f);
            var inner = new Vector2[shield.Length];
            for (int i = 0; i < shield.Length; i++) inner[i] = shield[i] * 0.78f;
            CarKit.Lamp(mb, c + n * 0.0032f, n, right, inner, lib["Plastic_Black"], lib["Car_BadgeGold"], 0.001f, 0f);
            CarKit.Lamp(mb, c + n * 0.0045f, n, right, CarKit.Rect(h * 0.36f, h * 0.14f, h * 0.05f), lib["Car_BadgeGold"], lib["Car_BadgeGold"], 0.001f, 0f);
        }
    }
}
