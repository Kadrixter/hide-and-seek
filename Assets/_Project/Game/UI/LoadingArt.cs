using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Procedural art of the <see cref="LoadingScreen"/> (no assets): a dark, foggy parking level seen down the driving
    /// lane - wet concrete with worn bay lines, pillars with hazard stripes, ceiling tubes (one dead), a green exit sign
    /// at the far wall - plus the soft glow, the two-lobed beacon beam and the bottom shade sprites. The nearest ceiling
    /// tube is not in the picture: the screen draws it on top (<see cref="NearTubeV"/>) so it can flicker.
    /// </summary>
    public static class LoadingArt
    {
        public const int W = 640, H = 360;
        private const float Aspect = W / (float)H;
        private const float Horizon = 0.46f;           // v of the vanishing point
        private const float CamH = 1.55f, CeilH = 1.05f; // eye above the floor, ceiling above the eye
        private const float WallZ = 38f;
        private const float PillarX = 3.1f, PillarW = 0.55f;
        private static readonly float[] LightZ = { 3.4f, 10.4f, 17.4f, 24.4f, 31.4f };
        private static readonly float[] LightI = { 0.55f, 0.85f, 0f, 0.6f, 0.45f }; // the third one is dead
        private static readonly float[] PillarZ = { 6.9f, 13.9f, 20.9f, 27.9f, 34.9f };
        private static readonly Color Fog = new Color(0.010f, 0.013f, 0.018f);

        /// <summary>Screen v (0 bottom .. 1 top) of the nearest ceiling tube and of its light pool on the floor.</summary>
        public static float NearTubeV => Horizon + CeilH / LightZ[0];
        public static float NearPoolV => Mathf.Max(0.02f, Horizon - CamH / LightZ[0]);
        /// <summary>Half width of the nearest tube as a fraction of the picture width.</summary>
        public static float NearTubeHalfU => 0.6f / (LightZ[0] * Aspect);

        private static Texture2D _garage;
        private static Sprite _glow, _beam, _shade;

        public static Texture2D Garage => _garage != null ? _garage : (_garage = MakeGarage());
        public static Sprite Glow => _glow != null ? _glow : (_glow = MakeGlow());
        public static Sprite Beam => _beam != null ? _beam : (_beam = MakeBeam());
        public static Sprite BottomShade => _shade != null ? _shade : (_shade = MakeShade());

        // ================================================================ garage

        private static Texture2D MakeGarage()
        {
            float t0 = Time.realtimeSinceStartup;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float yp = (y + 0.5f) / H - Horizon;
                for (int x = 0; x < W; x++)
                {
                    float xp = ((x + 0.5f) / W - 0.5f) * Aspect;
                    Color c = Shade(xp, yp, out float z);
                    c = Color.Lerp(Fog, c, Mathf.Exp(-z / 11f));
                    c += ExitHalo(xp, yp);
                    float d = (Hash(x, y) - 0.5f) * (2f / 255f); // dither: no banding in the dark gradients
                    px[y * W + x] = new Color32(To8(c.r + d), To8(c.g + d), To8(c.b + d), 255);
                }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "LoadingGarage",
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            Debug.Log($"[Loading] backdrop built in {(Time.realtimeSinceStartup - t0) * 1000f:0} ms");
            return tex;
        }

        /// <summary>Colour seen along the ray through (xp, yp); <paramref name="z"/> = depth of the hit (fog).</summary>
        private static Color Shade(float xp, float yp, out float z)
        {
            float zFloor = yp < 0f ? CamH / -yp : float.MaxValue;
            float zCeil = yp > 0f ? CeilH / yp : float.MaxValue;
            z = Mathf.Min(WallZ, Mathf.Min(zFloor, zCeil));
            int kind = z == zFloor ? 0 : z == zCeil ? 1 : 2; // floor, ceiling, far wall
            float lx = 0f;

            // Pillars: the front face and the side facing the lane (whichever the ray meets first).
            foreach (float pz in PillarZ)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float zf = pz - PillarW * 0.5f;
                    if (zf < z)
                    {
                        float wx = xp * zf, h = CamH + yp * zf;
                        if (Mathf.Abs(wx - s * PillarX) < PillarW * 0.5f && h > 0f && h < CamH + CeilH)
                        {
                            z = zf; kind = 3; lx = wx - (s * PillarX - PillarW * 0.5f);
                        }
                    }
                    float sideX = s * (PillarX - PillarW * 0.5f);
                    if (xp * s > 0.0001f)
                    {
                        float zs = sideX / xp;
                        float h = CamH + yp * zs;
                        if (zs < z && zs > pz - PillarW * 0.5f && zs < pz + PillarW * 0.5f && h > 0f && h < CamH + CeilH)
                        {
                            z = zs; kind = 4; lx = zs - (pz - PillarW * 0.5f);
                        }
                    }
                }
            }

            float wx2 = xp * z, wy = CamH + yp * z, wz = z;
            float light = Light(wx2, wy, wz);
            switch (kind)
            {
                case 0: return Floor(wx2, wz, z, light);
                case 1: return Ceiling(wx2, wz, light);
                case 3:
                case 4: return Pillar(lx, wy, wz, light, kind == 4);
                default: return Wall(wx2, wy, light);
            }
        }

        private static float Light(float wx, float wy, float wz)
        {
            float sum = 0f, top = CamH + CeilH;
            for (int i = 0; i < LightZ.Length; i++)
            {
                if (LightI[i] <= 0f) continue;
                float dz = wz - LightZ[i], dy = wy - top;
                sum += LightI[i] / (1f + (wx * wx + dz * dz + dy * dy * 0.5f) * 0.1f);
            }
            return sum;
        }

        private static Color Floor(float wx, float wz, float z, float light)
        {
            float n = Noise(wx * 1.3f, wz * 1.3f), fine = Noise(wx * 7f + 31f, wz * 7f);
            float alb = 0.16f * (0.75f + 0.5f * n) * (0.85f + 0.3f * fine);
            Color c = new Color(alb, alb * 0.98f, alb * 0.93f);

            // Paint: yellow dashed lane edges, white bay dividers (worn), anti-aliased by the pixel footprint.
            float footZ = z * z / CamH / H, footX = z * Aspect / W;
            float wear = Noise(wx * 3f + 7f, wz * 3f) > 0.32f ? 1f : 0.25f;
            float ax = Mathf.Abs(wx);
            float lane = Cover(Mathf.Abs(ax - 2.3f), 0.06f, footX) * (Mathf.Repeat(wz, 3f) < 1.8f ? 1f : 0f);
            float bayZ = Mathf.Repeat(wz + 1.35f, 2.7f);
            float bay = ax > 2.6f && ax < 7.5f ? Cover(Mathf.Min(bayZ, 2.7f - bayZ), 0.05f, footZ) : 0f;
            c = Color.Lerp(c, new Color(0.55f, 0.42f, 0.1f), lane * wear * 0.85f);
            c = Color.Lerp(c, new Color(0.44f, 0.44f, 0.42f), bay * wear * 0.8f);
            c *= 0.06f + light * 2.2f;

            // Wet patches: darker, with the ceiling tubes mirrored in them.
            float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.7f, Noise(wx * 0.45f + 13f, wz * 0.3f)));
            if (wet > 0f)
            {
                float refl = 0f;
                for (int i = 0; i < LightZ.Length; i++)
                {
                    float dz = (wz - LightZ[i]) * 0.55f;
                    refl += LightI[i] * Mathf.Exp(-(wx * wx * 4f + dz * dz));
                }
                c = c * (1f - 0.35f * wet) + new Color(0.75f, 0.85f, 1f) * (refl * 0.55f * wet);
            }
            return c;
        }

        private static Color Ceiling(float wx, float wz, float light)
        {
            float n = Noise(wx * 1.7f + 50f, wz * 1.7f);
            float alb = 0.07f * (0.8f + 0.4f * n);
            float beam = Mathf.Repeat(wz, 7f);
            if (beam < 0.45f) alb *= 0.55f; // concrete beams across the lane
            Color c = new Color(alb, alb, alb * 1.04f) * (0.05f + light * 1.4f);
            for (int i = 1; i < LightZ.Length; i++) // tube 0 is drawn by the screen (it flickers)
            {
                if (Mathf.Abs(wx) < 0.6f && Mathf.Abs(wz - LightZ[i]) < 0.07f)
                    return LightI[i] > 0f ? new Color(0.85f, 0.92f, 1f) * (0.75f + LightI[i] * 0.4f) : new Color(0.09f, 0.09f, 0.1f);
                if (Mathf.Abs(wx) < 0.68f && Mathf.Abs(wz - LightZ[i]) < 0.12f) return new Color(0.05f, 0.05f, 0.055f); // housing
            }
            return c;
        }

        private static Color Pillar(float lx, float h, float wz, float light, bool side)
        {
            float n = Noise(lx * 6f + wz, h * 6f);
            float alb = 0.2f * (0.8f + 0.4f * n);
            Color c = new Color(alb, alb * 0.99f, alb * 0.96f);
            if (h < 0.75f) // hazard stripes at the foot
                c = Mathf.Repeat((lx + h) / 0.28f, 1f) < 0.5f ? new Color(0.62f, 0.48f, 0.07f) * (0.8f + 0.2f * n) : new Color(0.03f, 0.03f, 0.03f);
            else if (h > 1.55f && h < 1.62f) c = new Color(0.5f, 0.12f, 0.1f); // red band (level colour)
            c *= (0.05f + light * 2f) * (side ? 0.55f : 1f);
            return c;
        }

        private static Color Wall(float wx, float h, float light)
        {
            if (Mathf.Abs(wx - 1.6f) < 0.45f && h > 2.05f && h < 2.3f) return new Color(0.2f, 0.95f, 0.42f); // exit sign
            float alb = 0.05f;
            return new Color(alb, alb, alb) * (0.08f + light * 1.5f);
        }

        /// <summary>Green glow of the exit sign (bloom), added over everything near it.</summary>
        private static Color ExitHalo(float xp, float yp)
        {
            float sx = 1.6f / WallZ, sy = (2.175f - CamH) / WallZ;
            float dx = xp - sx, dy = yp - sy;
            float g = Mathf.Exp(-(dx * dx * 0.6f + dy * dy * 2f) / 0.00035f);
            return new Color(0.04f, 0.22f, 0.1f) * g;
        }

        private static float Cover(float dist, float halfWidth, float foot)
        {
            foot = Mathf.Max(foot, 0.0005f);
            float edge = Mathf.Clamp01((halfWidth - dist) / foot + 0.5f);
            return edge * Mathf.Min(1f, halfWidth * 2f / foot);
        }

        private static byte To8(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        private static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ================================================================ sprites

        private static Texture2D NewTex(int w, int h, string name) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = name,
        };

        /// <summary>Soft round glow (white, alpha falls off to the edge).</summary>
        private static Sprite MakeGlow()
        {
            const int n = 64;
            var tex = NewTex(n, n, "LoadingGlow");
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Rotating beacon light: two opposite soft beams (rotated by the screen).</summary>
        private static Sprite MakeBeam()
        {
            const int n = 128;
            var tex = NewTex(n, n, "LoadingBeam");
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d >= 1f) { px[y * n + x] = new Color32(255, 255, 255, 0); continue; }
                float cos = d > 0.0001f ? Mathf.Abs(dx / d) : 1f;
                float lobe = Mathf.Pow(cos, 12f) * Mathf.Pow(1f - d, 1.3f);
                float core = Mathf.Clamp01(1f - d * 4f);
                float a = Mathf.Clamp01(lobe + core * core * 0.6f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Vertical shade: opaque at the bottom, clear at the top (the status / tip lines stay readable).</summary>
        private static Sprite MakeShade()
        {
            const int n = 128;
            var tex = NewTex(1, n, "LoadingShade");
            var px = new Color32[n];
            for (int y = 0; y < n; y++)
            {
                float v = (y + 0.5f) / n;
                px[y] = new Color32(255, 255, 255, (byte)(Mathf.Pow(1f - v, 1.6f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, n), new Vector2(0.5f, 0.5f));
        }
    }
}
