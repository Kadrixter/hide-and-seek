using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// 32 x 32 pixel-art icons for the hotbar (point filtered, dark outline), drawn in code in the blocky style of the
    /// game: medkit, adrenaline syringe, RC car, metal rod, knife, chainsaw, Glock, toy gun, phone (HotbarIcons.Phone.cs).
    /// </summary>
    public static partial class HotbarIcons
    {
        private const int N = 32;
        private static readonly Dictionary<HotbarItem, Sprite> Cache = new();

        public static Sprite Get(HotbarItem item)
        {
            if (item == HotbarItem.None) return null;
            if (item == HotbarItem.Phone) return Phone(); // HotbarIcons.Phone.cs
            if (item == HotbarItem.RcRemote) return RcRemote(); // HotbarIcons.RcRemote.cs
            if (item == HotbarItem.SeveredLeg) return Leg(); // HotbarIcons.Leg.cs
            if (item == HotbarItem.Wire) return Wire(); // HotbarIcons.Wire.cs
            if (item == HotbarItem.FuelCan) return FuelCan(); // HotbarIcons.FuelCan.cs
            if (Net.FuseItems.Is(item)) return Fuse(Net.FuseItems.SymbolOf(item)); // HotbarIcons.Fuse.cs
            if (Cache.TryGetValue(item, out var s) && s != null) return s;
            var px = new Color32[N * N];
            switch (item)
            {
                case HotbarItem.Medkit: Medkit(px); break;
                case HotbarItem.Syringe: Syringe(px); break;
                case HotbarItem.RcCar: Car(px); break;
                case HotbarItem.Rod: Rod(px); break;
                case HotbarItem.Knife: Knife(px); break;
                case HotbarItem.Chainsaw: Saw(px); break;
                case HotbarItem.Glock: Glock(px); break;
                case HotbarItem.ToyGun: ToyGun(px); break;
            }
            Outline(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 32f);
            Cache[item] = s;
            return s;
        }

        // ---------------------------------------------------------------- painting helpers (y = 0 at the bottom)

        private static void Rect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(N - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(N - 1, x1); x++)
                px[y * N + x] = c;
        }

        /// <summary>Thick diagonal / straight stroke from (x0, y0) to (x1, y1).</summary>
        private static void Line(Color32[] px, float x0, float y0, float x1, float y1, float half, Color32 c)
        {
            Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1), ab = b - a;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                Vector2 q = a + ab * t - p;
                if (Mathf.Abs(q.x) <= half && Mathf.Abs(q.y) <= half) px[y * N + x] = c;
            }
        }

        private static void Outline(Color32[] px)
        {
            var dark = new Color32(14, 14, 16, 255);
            var copy = (Color32[])px.Clone();
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                if (copy[y * N + x].a > 0) continue;
                bool near = false;
                for (int dy = -1; dy <= 1 && !near; dy++)
                for (int dx = -1; dx <= 1 && !near; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && yy >= 0 && xx < N && yy < N && copy[yy * N + xx].a > 0 && (dx == 0 || dy == 0)) near = true;
                }
                if (near) px[y * N + x] = dark;
            }
        }

        private static Color32 C(float r, float g, float b) => new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255);

        // ---------------------------------------------------------------- icons

        private static void Medkit(Color32[] px)
        {
            Rect(px, 12, 23, 19, 25, C(0.45f, 0.47f, 0.5f));   // handle
            Rect(px, 12, 21, 13, 23, C(0.45f, 0.47f, 0.5f));
            Rect(px, 18, 21, 19, 23, C(0.45f, 0.47f, 0.5f));
            Rect(px, 5, 7, 26, 21, C(0.93f, 0.93f, 0.9f));     // box
            Rect(px, 5, 7, 26, 8, C(0.75f, 0.75f, 0.72f));
            Rect(px, 14, 9, 17, 19, C(0.82f, 0.1f, 0.08f));    // cross
            Rect(px, 10, 12, 21, 15, C(0.82f, 0.1f, 0.08f));
        }

        private static void Syringe(Color32[] px)
        {
            Line(px, 7f, 7f, 22f, 22f, 2.6f, C(0.86f, 0.92f, 0.96f));   // barrel
            Line(px, 9f, 9f, 18f, 18f, 1.6f, C(0.2f, 0.6f, 1f));         // liquid
            Line(px, 22f, 22f, 27f, 27f, 0.9f, C(0.4f, 0.42f, 0.45f));   // plunger rod
            Line(px, 25f, 29f, 29f, 25f, 1.1f, C(0.15f, 0.15f, 0.16f));  // plunger head
            Line(px, 19f, 25f, 25f, 19f, 0.9f, C(0.92f, 0.92f, 0.92f));  // finger grip
            Line(px, 3f, 3f, 7f, 7f, 0.6f, C(0.8f, 0.82f, 0.85f));       // needle
            Line(px, 11f, 11f, 13f, 13f, 2.7f, C(1f, 0.82f, 0.1f));      // label
        }

        private static void Car(Color32[] px)
        {
            Rect(px, 2, 9, 29, 14, C(0.08f, 0.3f, 0.82f));      // low body
            Rect(px, 26, 13, 29, 14, C(0.06f, 0.22f, 0.62f));   // bonnet slope
            Rect(px, 10, 15, 21, 18, C(0.08f, 0.3f, 0.82f));    // small cabin
            Rect(px, 9, 14, 22, 14, C(0.08f, 0.3f, 0.82f));
            Rect(px, 12, 15, 15, 17, C(0.12f, 0.16f, 0.22f));   // windows
            Rect(px, 17, 15, 20, 17, C(0.12f, 0.16f, 0.22f));
            Rect(px, 1, 15, 6, 16, C(0.08f, 0.3f, 0.82f));      // rear wing
            Rect(px, 2, 11, 29, 11, C(1f, 0.78f, 0.08f));       // stripe
            Rect(px, 28, 12, 29, 13, C(1f, 0.97f, 0.78f));      // headlight
            Rect(px, 4, 5, 10, 11, C(0.06f, 0.06f, 0.07f));     // big wheels
            Rect(px, 21, 5, 27, 11, C(0.06f, 0.06f, 0.07f));
            Rect(px, 6, 7, 8, 9, C(0.78f, 0.8f, 0.84f));
            Rect(px, 23, 7, 25, 9, C(0.78f, 0.8f, 0.84f));
            Line(px, 4f, 15f, 2f, 27f, 0.5f, C(0.2f, 0.2f, 0.22f)); // antenna
        }

        private static void Rod(Color32[] px)
        {
            Line(px, 7f, 5f, 25f, 27f, 1.9f, C(0.6f, 0.62f, 0.66f));     // pipe
            Line(px, 9f, 6f, 25f, 25.5f, 0.6f, C(0.78f, 0.8f, 0.84f));   // highlight
            Line(px, 13f, 12f, 15f, 14.5f, 1.8f, C(0.5f, 0.28f, 0.14f)); // rust
            Line(px, 19f, 20f, 20f, 21f, 1.6f, C(0.5f, 0.28f, 0.14f));
            Line(px, 5f, 2.5f, 8f, 6.5f, 2.6f, C(0.08f, 0.08f, 0.09f));  // rubber foot
            Line(px, 22f, 23.5f, 24f, 26f, 2.4f, C(0.62f, 0.62f, 0.65f)); // tape band
        }

        private static void Knife(Color32[] px)
        {
            Line(px, 12f, 12f, 27f, 27f, 2.1f, C(0.72f, 0.74f, 0.78f));  // blade
            Line(px, 14f, 12f, 27f, 25f, 0.6f, C(0.95f, 0.96f, 0.98f));  // edge
            Line(px, 9f, 15f, 15f, 9f, 1.2f, C(0.3f, 0.31f, 0.34f));     // guard
            Line(px, 4f, 4f, 11f, 11f, 2.2f, C(0.16f, 0.12f, 0.09f));    // handle
        }

        private static void Saw(Color32[] px)
        {
            Rect(px, 3, 8, 14, 19, C(0.93f, 0.45f, 0.08f));     // body
            Rect(px, 5, 19, 12, 22, C(0.16f, 0.16f, 0.17f));    // top handle
            Rect(px, 2, 10, 3, 17, C(0.16f, 0.16f, 0.17f));     // rear handle
            Rect(px, 14, 11, 29, 16, C(0.62f, 0.64f, 0.67f));   // bar
            for (int x = 14; x <= 29; x += 2) { Rect(px, x, 10, x, 10, C(0.16f, 0.16f, 0.17f)); Rect(px, x + 1, 17, x + 1, 17, C(0.16f, 0.16f, 0.17f)); }
            Rect(px, 6, 11, 10, 15, C(0.2f, 0.2f, 0.22f));      // engine cover
        }

        /// <summary>Placeholder: green toy pistol with an orange tip and a blob of slime.</summary>
        private static void ToyGun(Color32[] px)
        {
            Rect(px, 6, 16, 24, 22, C(0.35f, 0.82f, 0.3f));     // body
            Rect(px, 24, 17, 27, 21, C(1f, 0.55f, 0.1f));       // orange tip
            Line(px, 10f, 16f, 7f, 5f, 2.6f, C(0.95f, 0.45f, 0.7f)); // pink grip
            Rect(px, 13, 12, 16, 13, C(0.95f, 0.45f, 0.7f));    // trigger guard
            Rect(px, 8, 22, 14, 25, C(0.25f, 0.65f, 0.22f));    // tank
            Rect(px, 28, 18, 30, 21, C(0.55f, 1f, 0.35f));      // slime
        }

        private static void Glock(Color32[] px)
        {
            Rect(px, 5, 17, 28, 23, C(0.15f, 0.155f, 0.17f));   // slide
            Rect(px, 26, 23, 27, 24, C(0.95f, 0.93f, 0.85f));   // sight
            Rect(px, 6, 15, 24, 17, C(0.075f, 0.075f, 0.085f)); // frame
            Line(px, 9f, 15f, 6f, 4f, 2.6f, C(0.075f, 0.075f, 0.085f)); // grip
            Rect(px, 12, 11, 16, 12, C(0.075f, 0.075f, 0.085f)); // trigger guard
            Rect(px, 16, 12, 17, 14, C(0.075f, 0.075f, 0.085f));
        }
    }
}
