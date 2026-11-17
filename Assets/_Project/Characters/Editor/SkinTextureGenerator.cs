using System;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>Colour palette of one skin variant.</summary>
    public struct SkinPalette
    {
        public string Name;
        public Color32 Hoodie, Accent, Shirt, Hair, Skin, Eyes, Pants, Shoes, Sole, Laces;
    }

    /// <summary>
    /// Paints 64x64 Minecraft-layout skins: hoodie with hood, drawstrings, kangaroo pocket and ribbed hems,
    /// sporty sleeve stripes, jeans with knee wear and seams, chunky sneakers, side-swept hair and a pixel-art face.
    /// The second layer (hat/jacket/sleeves/pants) adds hair volume, hood, cuffs and shoe soles.
    /// Fully deterministic.
    /// </summary>
    public static class SkinTextureGenerator
    {
        enum Face { Top, Bottom, Right, Front, Left, Back }

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static readonly SkinPalette[] DefaultPalettes =
        {
            new SkinPalette
            {
                Name = "Red", Hoodie = C(196, 44, 48), Accent = C(238, 236, 228), Shirt = C(232, 228, 218),
                Hair = C(96, 60, 36), Skin = C(232, 186, 150), Eyes = C(58, 104, 168),
                Pants = C(54, 76, 124), Shoes = C(232, 232, 226), Sole = C(70, 66, 64), Laces = C(196, 44, 48)
            },
            new SkinPalette
            {
                Name = "Blue", Hoodie = C(44, 100, 196), Accent = C(250, 196, 60), Shirt = C(236, 236, 240),
                Hair = C(222, 180, 92), Skin = C(240, 202, 168), Eyes = C(70, 132, 74),
                Pants = C(64, 64, 74), Shoes = C(186, 44, 44), Sole = C(236, 232, 224), Laces = C(240, 240, 240)
            },
            new SkinPalette
            {
                Name = "Green", Hoodie = C(58, 146, 76), Accent = C(240, 238, 226), Shirt = C(52, 56, 60),
                Hair = C(36, 32, 38), Skin = C(196, 138, 98), Eyes = C(92, 60, 34),
                Pants = C(168, 148, 104), Shoes = C(112, 72, 42), Sole = C(44, 34, 28), Laces = C(200, 180, 140)
            },
            new SkinPalette
            {
                Name = "Yellow", Hoodie = C(234, 186, 48), Accent = C(58, 58, 70), Shirt = C(250, 248, 240),
                Hair = C(188, 84, 40), Skin = C(246, 212, 182), Eyes = C(56, 128, 150),
                Pants = C(42, 42, 50), Shoes = C(236, 236, 236), Sole = C(34, 34, 38), Laces = C(34, 34, 38)
            },
            new SkinPalette
            {
                Name = "Purple", Hoodie = C(128, 70, 188), Accent = C(126, 228, 214), Shirt = C(240, 236, 246),
                Hair = C(58, 40, 32), Skin = C(142, 96, 66), Eyes = C(62, 40, 24),
                Pants = C(36, 46, 82), Shoes = C(242, 242, 242), Sole = C(126, 90, 200), Laces = C(126, 228, 214)
            }
        };

        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        /// <summary>Canvas with Minecraft (top-left origin) coordinates.</summary>
        sealed class Canvas
        {
            public readonly Color32[] Pixels = new Color32[64 * 64];

            public void Set(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= 64 || y >= 64) return;
                Pixels[(63 - y) * 64 + x] = c;
            }

            public Color32 Get(int x, int y) => Pixels[(63 - y) * 64 + x];

            /// <summary>Paints a face; the painter receives local (fx, fy, width, height) and returns null to skip.</summary>
            public void PaintFace(RectInt r, Func<int, int, int, int, Color32?> painter)
            {
                for (int fy = 0; fy < r.height; fy++)
                for (int fx = 0; fx < r.width; fx++)
                {
                    var c = painter(fx, fy, r.width, r.height);
                    if (c.HasValue) Set(r.x + fx, r.y + fy, c.Value);
                }
            }
        }

        static RectInt FaceRect(int u, int v, int w, int h, int d, Face f)
        {
            switch (f)
            {
                case Face.Top: return new RectInt(u + d, v, w, d);
                case Face.Bottom: return new RectInt(u + d + w, v, w, d);
                case Face.Right: return new RectInt(u, v + d, d, h);
                case Face.Front: return new RectInt(u + d, v + d, w, h);
                case Face.Left: return new RectInt(u + d + w, v + d, d, h);
                default: return new RectInt(u + d + w + d, v + d, w, h);
            }
        }

        // ------------------------------------------------------------------ colour helpers

        /// <summary>Hue-shifted shading: shadows drift towards blue/purple, highlights towards warm yellow.</summary>
        static Color32 Shade(Color32 c, float factor)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (factor < 1f)
            {
                float k = 1f - factor;
                h = ShiftHue(h, 0.68f, 0.10f * k);
                s = Mathf.Clamp01(s * (1f + 0.35f * k) + 0.03f * k);
            }
            else
            {
                float k = factor - 1f;
                h = ShiftHue(h, 0.14f, 0.12f * k);
                s = Mathf.Clamp01(s * (1f - 0.35f * k));
            }

            v = Mathf.Clamp01(v * factor);
            var o = Color.HSVToRGB(h, s, v);
            o.a = c.a / 255f;
            return o;
        }

        static float ShiftHue(float h, float target, float amount)
        {
            float delta = Mathf.DeltaAngle(h * 360f, target * 360f) / 360f;
            return Mathf.Repeat(h + delta * Mathf.Clamp01(amount), 1f);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint hsh = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
                hsh = (hsh ^ (hsh >> 13)) * 1274126177u;
                hsh ^= hsh >> 16;
                return (hsh & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Fabric texture: subtle value noise.</summary>
        static Color32 Cloth(Color32 c, int gx, int gy, float baseFactor, float noise = 0.07f, int seed = 1)
        {
            float n = (Hash(gx, gy, seed) - 0.5f) * noise;
            return Shade(c, baseFactor + n);
        }

        // ------------------------------------------------------------------ public entry

        public static Texture2D Generate(SkinPalette p)
        {
            var cv = new Canvas();
            for (int i = 0; i < cv.Pixels.Length; i++) cv.Pixels[i] = Clear;

            PaintHead(cv, p);
            PaintHat(cv, p);
            PaintBody(cv, p);
            PaintJacket(cv, p);
            PaintArm(cv, p, 40, 16, true);
            PaintArm(cv, p, 32, 48, false);
            PaintSleeve(cv, p, 40, 32);
            PaintSleeve(cv, p, 48, 48);
            PaintLeg(cv, p, 0, 16, true);
            PaintLeg(cv, p, 16, 48, false);
            PaintPants(cv, p, 0, 32);
            PaintPants(cv, p, 0, 48);

            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(cv.Pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ head

        static Color32 HairPx(SkinPalette p, int gx, int gy, float baseFactor = 1f)
        {
            float h = Hash(gx, gy, 7);
            // Strand-like streaks: correlate along columns.
            float streak = Hash(gx, gy / 3, 11);
            float f = baseFactor + (streak - 0.5f) * 0.16f;
            if (h < 0.12f) f += 0.18f;
            else if (h > 0.9f) f -= 0.16f;
            return Shade(p.Hair, f);
        }

        static void PaintHead(Canvas cv, SkinPalette p)
        {
            const int u = 0, v = 0, w = 8, h = 8, d = 8;
            Color32 skin = p.Skin;
            Color32 skinShade = Shade(skin, 0.86f);
            Color32 skinDeep = Shade(skin, 0.74f);
            Color32 skinLight = Shade(skin, 1.06f);
            Color32 brow = Shade(p.Hair, 0.62f);
            Color32 eyeWhite = C(246, 246, 244);
            Color32 iris = p.Eyes;
            // Closed smile: soft corners, slightly darker centre (reads as a mouth, not an open "o").
            Color32 lip = Shade(Lerp(skin, C(190, 100, 100), 0.22f), 0.92f);
            Color32 mouth = Shade(Lerp(skin, C(150, 60, 66), 0.5f), 0.82f);
            Color32 blush = Lerp(skin, C(236, 120, 120), 0.28f);

            // Front: pixel-art face.
            string[] face =
            {
                "HHHHHHHH",
                "HHHHHHHH",
                "HfffffhH",
                "HbbsSbbH",
                "sWIssIWs",
                "scsnnscs",
                "sssmmsss",
                "jsssssSj"
            };
            var fr = FaceRect(u, v, w, h, d, Face.Front);
            cv.PaintFace(fr, (fx, fy, fw, fh) =>
            {
                char ch = face[fy][fx];
                int gx = fr.x + fx, gy = fr.y + fy;
                switch (ch)
                {
                    case 'H': return HairPx(p, gx, gy);
                    case 'h': return HairPx(p, gx, gy, 0.85f);
                    case 'f': return skinShade; // shadow under the fringe
                    case 'b': return brow;
                    case 'S': return Shade(skin, 0.95f);
                    case 'W': return eyeWhite;
                    case 'I': return iris;
                    case 'n': return Shade(skin, 0.9f);
                    case 'c': return blush;
                    case 'm': return lip;
                    case 'M': return mouth;
                    case 'j': return skinShade;
                    default: return Cloth(skin, gx, gy, 1f, 0.03f);
                }
            });

            // Sides: hair covers the back/top, ear in the middle. fx 0 = back on the right face.
            int[] hairDepth = { 8, 8, 7, 6, 4, 3, 2, 2 };
            void Side(Face f, bool frontIsRight)
            {
                var r = FaceRect(u, v, w, h, d, f);
                cv.PaintFace(r, (fx, fy, fw, fh) =>
                {
                    int depthIndex = frontIsRight ? fx : fw - 1 - fx; // 0 = back
                    int gx = r.x + fx, gy = r.y + fy;
                    if (fy < hairDepth[depthIndex]) return HairPx(p, gx, gy, fy == hairDepth[depthIndex] - 1 ? 0.85f : 1f);
                    if (depthIndex == 4 && (fy == 4 || fy == 5)) return fy == 4 ? skinShade : skinDeep; // ear
                    if (depthIndex == 3 && fy == 5) return skinShade;
                    if (fy == 7) return skinShade; // jaw shadow
                    return Cloth(skin, gx, gy, depthIndex >= 6 ? 1f : 0.96f, 0.03f);
                });
            }

            Side(Face.Right, true);
            Side(Face.Left, false);

            var back = FaceRect(u, v, w, h, d, Face.Back);
            cv.PaintFace(back, (fx, fy, fw, fh) =>
            {
                int gx = back.x + fx, gy = back.y + fy;
                if (fy == 7) return (fx == 0 || fx == 7) ? skinShade : HairPx(p, gx, gy, 0.78f);
                return HairPx(p, gx, gy, fy >= 5 ? 0.9f : 1f);
            });

            var top = FaceRect(u, v, w, h, d, Face.Top);
            cv.PaintFace(top, (fx, fy, fw, fh) =>
            {
                int gx = top.x + fx, gy = top.y + fy;
                // Parting line on the top, slightly off centre.
                if (fx == 5 && fy >= 3) return HairPx(p, gx, gy, 0.8f);
                return HairPx(p, gx, gy, 1.06f);
            });

            var bottom = FaceRect(u, v, w, h, d, Face.Bottom);
            cv.PaintFace(bottom, (fx, fy, fw, fh) =>
            {
                // Image top row = front (chin), bottom rows = back of the neck (hair).
                if (fy >= 6) return HairPx(p, bottom.x + fx, bottom.y + fy, 0.7f);
                return fy < 2 ? skinShade : skinDeep;
            });
        }

        static void PaintHat(Canvas cv, SkinPalette p)
        {
            const int u = 32, v = 0, w = 8, h = 8, d = 8;

            // Fringe: swept from the character's right (image left) across the forehead.
            string[] front =
            {
                "HHHHHHHH",
                "HHHHHHh.",
                "HHh.....",
                "H.......",
                "........",
                "........",
                "........",
                "........"
            };
            var fr = FaceRect(u, v, w, h, d, Face.Front);
            cv.PaintFace(fr, (fx, fy, fw, fh) =>
            {
                char ch = front[fy][fx];
                if (ch == '.') return null;
                return HairPx(p, fr.x + fx, fr.y + fy, ch == 'h' ? 0.82f : 1.08f);
            });

            // Sides: volume over the back half and a tuft above the ear.
            int[] depth = { 7, 7, 6, 5, 3, 2, 1, 1 };
            void Side(Face f, bool frontIsRight)
            {
                var r = FaceRect(u, v, w, h, d, f);
                cv.PaintFace(r, (fx, fy, fw, fh) =>
                {
                    int di = frontIsRight ? fx : fw - 1 - fx;
                    if (fy >= depth[di]) return null;
                    if (fy == depth[di] - 1 && Hash(r.x + fx, r.y + fy, 3) < 0.35f) return null; // ragged edge
                    return HairPx(p, r.x + fx, r.y + fy, 1.04f);
                });
            }

            Side(Face.Right, true);
            Side(Face.Left, false);

            var back = FaceRect(u, v, w, h, d, Face.Back);
            cv.PaintFace(back, (fx, fy, fw, fh) =>
            {
                if (fy == 7) return null;
                if (fy == 6 && (fx % 3 == 1)) return null; // jagged ends
                return HairPx(p, back.x + fx, back.y + fy, 1.02f);
            });

            var top = FaceRect(u, v, w, h, d, Face.Top);
            cv.PaintFace(top, (fx, fy, fw, fh) =>
            {
                // A small cowlick near the crown plus fringe volume at the front edge.
                bool cowlick = (fy == 1 && (fx == 2 || fx == 3)) || (fy == 2 && fx == 3);
                bool fringe = fy == 7 && fx <= 4;
                return cowlick || fringe ? HairPx(p, top.x + fx, top.y + fy, 1.12f) : (Color32?)null;
            });
        }

        // ------------------------------------------------------------------ body

        static Color32 HoodiePx(SkinPalette p, int gx, int gy, int fy, int fh, float extra = 1f)
        {
            // Light from above: slightly brighter at the shoulders, darker towards the hem.
            float grad = Mathf.Lerp(1.05f, 0.92f, fh <= 1 ? 0f : fy / (float)(fh - 1));
            return Cloth(p.Hoodie, gx, gy, grad * extra, 0.06f, 21);
        }

        static Color32 RibPx(SkinPalette p, int fx, int fy)
        {
            float f = (fx % 2 == 0) ? 0.8f : 0.72f;
            if (fy == 1) f -= 0.04f;
            return Shade(p.Hoodie, f);
        }

        static void PaintBody(Canvas cv, SkinPalette p)
        {
            const int u = 16, v = 16, w = 8, h = 12, d = 4;
            Color32 rim = Shade(p.Hoodie, 1.14f);
            Color32 rimDark = Shade(p.Hoodie, 0.7f);
            Color32 shirt = p.Shirt;
            Color32 shirtShade = Shade(p.Shirt, 0.78f);
            Color32 str = p.Accent;
            Color32 strTip = Shade(p.Accent, 0.75f);
            Color32 pocket = Shade(p.Hoodie, 0.78f);

            var fr = FaceRect(u, v, w, h, d, Face.Front);
            cv.PaintFace(fr, (fx, fy, fw, fh) =>
            {
                int gx = fr.x + fx, gy = fr.y + fy;
                // Hood rim forms a V around the neck with the shirt visible inside.
                if (fy == 0)
                {
                    if (fx == 3 || fx == 4) return shirtShade;
                    if (fx == 2 || fx == 5) return rim;
                    if (fx == 1 || fx == 6) return Shade(p.Hoodie, 1.08f);
                }

                if (fy == 1)
                {
                    if (fx == 3 || fx == 4) return shirt;
                    if (fx == 2 || fx == 5) return rim;
                }

                if (fy == 2 && (fx == 3 || fx == 4)) return fx == 3 ? rim : Shade(p.Hoodie, 1.02f);
                if (fy == 3 && (fx == 3 || fx == 4)) return rimDark; // collar seam shadow

                // Drawstrings hanging from the hood.
                if ((fx == 2 || fx == 5) && fy >= 2 && fy <= 5) return fy == 5 ? strTip : Cloth(str, gx, gy, 1f, 0.05f);

                // Kangaroo pocket.
                if (fy == 7 && fx >= 2 && fx <= 5) return pocket;
                if (fy == 8 && (fx == 1 || fx == 6)) return pocket;
                if (fy >= 8 && fy <= 9 && fx >= 2 && fx <= 5) return HoodiePx(p, gx, gy, fy, fh, 0.9f);

                // Ribbed hem.
                if (fy >= 10) return RibPx(p, fx, fy - 10);

                // Soft fold creases and side shading.
                float edge = (fx == 0 || fx == fw - 1) ? 0.93f : 1f;
                if (fy == 6 && (fx == 1 || fx == 6)) edge *= 0.9f;
                return HoodiePx(p, gx, gy, fy, fh, edge);
            });

            var back = FaceRect(u, v, w, h, d, Face.Back);
            cv.PaintFace(back, (fx, fy, fw, fh) =>
            {
                int gx = back.x + fx, gy = back.y + fy;
                // Hood lying on the back: a rounded shape with darker outline.
                int halfWidth = fy == 0 ? 4 : fy == 1 ? 3 : fy == 2 ? 3 : fy == 3 ? 2 : fy == 4 ? 1 : 0;
                float cx = 3.5f;
                float dx = Mathf.Abs(fx - cx);
                if (halfWidth > 0 && dx < halfWidth)
                {
                    bool outline = dx > halfWidth - 1f || fy == 4;
                    if (fy == 0) return rim;
                    return outline ? rimDark : HoodiePx(p, gx, gy, fy, fh, 1.06f);
                }

                if (fy >= 10) return RibPx(p, fx, fy - 10);
                return HoodiePx(p, gx, gy, fy, fh, 0.97f);
            });

            void Side(Face f)
            {
                var r = FaceRect(u, v, w, h, d, f);
                cv.PaintFace(r, (fx, fy, fw, fh) =>
                {
                    if (fy >= 10) return RibPx(p, fx, fy - 10);
                    float seam = fx == 1 ? 0.92f : 1f;
                    return HoodiePx(p, r.x + fx, r.y + fy, fy, fh, 0.95f * seam);
                });
            }

            Side(Face.Right);
            Side(Face.Left);

            var top = FaceRect(u, v, w, h, d, Face.Top);
            cv.PaintFace(top, (fx, fy, fw, fh) =>
            {
                // Neck opening in the middle (fy 3 = front edge).
                if ((fx == 3 || fx == 4) && (fy == 1 || fy == 2)) return Shade(p.Skin, 0.7f);
                if ((fx == 2 || fx == 5) && fy >= 1 && fy <= 2) return rim;
                if (fy == 0 && fx >= 2 && fx <= 5) return rim; // hood rolled at the back
                return Cloth(p.Hoodie, top.x + fx, top.y + fy, 1.1f, 0.06f, 21);
            });

            var bottom = FaceRect(u, v, w, h, d, Face.Bottom);
            cv.PaintFace(bottom, (fx, fy, fw, fh) => Shade(p.Hoodie, 0.62f));
        }

        static void PaintJacket(Canvas cv, SkinPalette p)
        {
            const int u = 16, v = 32, w = 8, h = 12, d = 4;
            Color32 rim = Shade(p.Hoodie, 1.12f);
            Color32 rimDark = Shade(p.Hoodie, 0.72f);

            var fr = FaceRect(u, v, w, h, d, Face.Front);
            cv.PaintFace(fr, (fx, fy, fw, fh) =>
            {
                // Pocket flap edge and a slightly protruding waistband.
                if (fy == 7 && fx >= 2 && fx <= 5) return Shade(p.Hoodie, 0.96f);
                if (fy == 11) return RibPx(p, fx, 0);
                return null;
            });

            var back = FaceRect(u, v, w, h, d, Face.Back);
            cv.PaintFace(back, (fx, fy, fw, fh) =>
            {
                // Hood volume.
                if (fy == 0 && fx >= 1 && fx <= 6) return rim;
                if (fy == 1 && fx >= 1 && fx <= 6) return fx == 1 || fx == 6 ? rimDark : Shade(p.Hoodie, 1.0f);
                if (fy == 2 && fx >= 2 && fx <= 5) return fx == 2 || fx == 5 ? rimDark : Shade(p.Hoodie, 0.94f);
                if (fy == 11) return RibPx(p, fx, 0);
                return null;
            });

            void Side(Face f)
            {
                var r = FaceRect(u, v, w, h, d, f);
                cv.PaintFace(r, (fx, fy, fw, fh) => fy == 11 ? RibPx(p, fx, 0) : (Color32?)null);
            }

            Side(Face.Right);
            Side(Face.Left);

            var top = FaceRect(u, v, w, h, d, Face.Top);
            cv.PaintFace(top, (fx, fy, fw, fh) => fy == 0 && fx >= 1 && fx <= 6 ? rim : (Color32?)null);
        }

        // ------------------------------------------------------------------ arms

        static void PaintArm(Canvas cv, SkinPalette p, int u, int v, bool right)
        {
            const int w = 4, h = 12, d = 4;
            Color32 skin = p.Skin;
            Face outer = right ? Face.Right : Face.Left;
            Face inner = right ? Face.Left : Face.Right;

            foreach (Face f in new[] { Face.Right, Face.Front, Face.Left, Face.Back })
            {
                var r = FaceRect(u, v, w, h, d, f);
                Face face = f;
                cv.PaintFace(r, (fx, fy, fw, fh) =>
                {
                    int gx = r.x + fx, gy = r.y + fy;
                    if (fy <= 7)
                    {
                        // Sporty stripe down the outer side of the sleeve.
                        if (face == outer && (fx == 1 || fx == 2) && fy <= 7)
                            return Cloth(p.Accent, gx, gy, fx == 1 ? 1f : 0.88f, 0.04f);
                        float f2 = Mathf.Lerp(1.04f, 0.9f, fy / 7f);
                        if (face == inner) f2 *= 0.9f;
                        if (face == Face.Front && fy == 4 && (fx == 1 || fx == 2)) f2 *= 0.9f; // elbow crease
                        if (fy == 0) f2 *= 1.05f; // shoulder seam highlight
                        return Cloth(p.Hoodie, gx, gy, f2, 0.06f, 21);
                    }

                    if (fy == 8) return RibPx(p, fx, 0);

                    // Hand.
                    float hf = fy == 11 ? 0.88f : 1f;
                    if (face == Face.Front && fy == 11 && fx % 2 == 1) hf = 0.8f; // fingers
                    if (face == Face.Back && fy == 9) hf = 1.05f; // knuckles
                    if (face == inner && fy >= 9 && fy <= 10)
                    {
                        // Thumb on the front edge of the inner side.
                        int frontCol = right ? 0 : fw - 1;
                        if (fx == frontCol) return Shade(skin, 1.06f);
                        if (fx == (right ? 1 : fw - 2)) return Shade(skin, 0.86f);
                    }

                    if (face == inner) hf *= 0.95f;
                    return Shade(skin, hf);
                });
            }

            var top = FaceRect(u, v, w, h, d, Face.Top);
            cv.PaintFace(top, (fx, fy, fw, fh) => Cloth(p.Hoodie, top.x + fx, top.y + fy, 1.1f, 0.05f, 21));
            var bottom = FaceRect(u, v, w, h, d, Face.Bottom);
            cv.PaintFace(bottom, (fx, fy, fw, fh) => Shade(skin, (fx + fy) % 3 == 0 ? 0.78f : 0.84f));
        }

        static void PaintSleeve(Canvas cv, SkinPalette p, int u, int v)
        {
            const int w = 4, h = 12, d = 4;
            foreach (Face f in new[] { Face.Right, Face.Front, Face.Left, Face.Back })
            {
                var r = FaceRect(u, v, w, h, d, f);
                cv.PaintFace(r, (fx, fy, fw, fh) =>
                {
                    if (fy == 8) return RibPx(p, fx, 0);
                    if (fy == 7) return Shade(p.Hoodie, 0.86f);
                    return null;
                });
            }
        }

        // ------------------------------------------------------------------ legs

        static Color32 PantsPx(SkinPalette p, int gx, int gy, float f)
        {
            // Denim-like diagonal twill.
            float twill = ((gx + gy) % 3 == 0) ? 0.95f : 1f;
            return Cloth(p.Pants, gx, gy, f * twill, 0.07f, 33);
        }

        static void PaintLeg(Canvas cv, SkinPalette p, int u, int v, bool right)
        {
            const int w = 4, h = 12, d = 4;
            Face outer = right ? Face.Right : Face.Left;
            Face inner = right ? Face.Left : Face.Right;
            Color32 shoe = p.Shoes;
            Color32 sole = p.Sole;

            foreach (Face f in new[] { Face.Right, Face.Front, Face.Left, Face.Back })
            {
                var r = FaceRect(u, v, w, h, d, f);
                Face face = f;
                cv.PaintFace(r, (fx, fy, fw, fh) =>
                {
                    int gx = r.x + fx, gy = r.y + fy;
                    if (fy <= 8)
                    {
                        float pf = Mathf.Lerp(1.0f, 0.9f, fy / 8f);
                        if (face == inner) pf *= 0.88f;
                        if (face == Face.Front && (fy == 4 || fy == 5) && (fx == 1 || fx == 2)) pf *= 1.12f; // worn knees
                        if (face == Face.Back && fy == 5) pf *= 0.88f; // knee-pit crease
                        if (face == outer && fx == 2) pf *= 1.1f; // outer seam
                        if (fy == 0) pf *= 0.85f; // shadow under the hoodie
                        if (fy == 8) pf *= 0.85f; // hem
                        return PantsPx(p, gx, gy, pf);
                    }

                    if (fy == 11) return Shade(sole, fx % 2 == 0 ? 1f : 0.9f);
                    if (face == Face.Front)
                    {
                        if (fy == 9) return (fx == 1 || fx == 2) ? p.Laces : Shade(shoe, 0.92f);
                        return Shade(shoe, 1.04f); // toe cap
                    }

                    if (face == Face.Back && fy == 9 && (fx == 1 || fx == 2)) return Shade(shoe, 0.78f); // heel tab
                    if (face == outer && fy == 10 && (fx == 1 || fx == 2)) return Shade(p.Laces, 0.95f); // side logo
                    return Shade(shoe, face == inner ? 0.88f : 0.96f);
                });
            }

            var top = FaceRect(u, v, w, h, d, Face.Top);
            cv.PaintFace(top, (fx, fy, fw, fh) => Shade(p.Pants, 0.7f));
            var bottom = FaceRect(u, v, w, h, d, Face.Bottom);
            cv.PaintFace(bottom, (fx, fy, fw, fh) => Shade(sole, (fx + fy) % 2 == 0 ? 0.85f : 0.75f));
        }

        static void PaintPants(Canvas cv, SkinPalette p, int u, int v)
        {
            const int w = 4, h = 12, d = 4;
            foreach (Face f in new[] { Face.Right, Face.Front, Face.Left, Face.Back })
            {
                var r = FaceRect(u, v, w, h, d, f);
                Face face = f;
                cv.PaintFace(r, (fx, fy, fw, fh) =>
                {
                    // Chunky sneaker sole wrapping around the foot.
                    if (fy == 11) return Shade(p.Sole, 1.05f);
                    if (fy == 10 && face == Face.Front) return Shade(p.Shoes, 1.06f);
                    return null;
                });
            }

            var bottom = FaceRect(u, v, w, h, d, Face.Bottom);
            cv.PaintFace(bottom, (fx, fy, fw, fh) => Shade(p.Sole, 0.7f));
        }

        static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);
    }
}
