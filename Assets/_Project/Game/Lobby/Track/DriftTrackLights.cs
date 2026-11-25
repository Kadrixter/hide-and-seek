using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Night lighting of the drift track: sixteen 18 m floodlight towers round the pad edge (lattice-ish pole, a lamp bank of
    /// glowing panels aimed at the course, one wide spot light each; two of them cast soft shadows), a warm lamp over
    /// the gate and the kart row, and the sky: a dark blue dome with stars and a faint glow on the horizon (unlit).
    /// </summary>
    public static class DriftTrackLights
    {
        public static void Build(Transform parent)
        {
            var root = new GameObject("Lights").transform;
            root.SetParent(parent, false);
            float w = DriftTrackLayout.PadWest + 0.6f, e = DriftTrackLayout.PadEast - 2.2f, s = DriftTrackLayout.PadSouth + 0.6f, n = DriftTrackLayout.PadNorth - 0.6f;
            float mx = DriftTrackCourse.Center.x;
            var towers = new System.Collections.Generic.List<Vector3>();
            for (int k = 0; k < 7; k++)
            {
                float x = Mathf.Lerp(e - 18f, w + 18f, k / 6f);
                towers.Add(new Vector3(x, 0f, n));
                towers.Add(new Vector3(x, 0f, s));
            }
            towers.Add(new Vector3(w, 0f, DriftTrackCourse.Center.z));
            towers.Add(new Vector3(e, 0f, DriftTrackLayout.ArchZ - 34f));
            var pole = new ProcMesh();
            var head = new ProcMesh();
            var glow = new ProcMesh();
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                const float H = 18f;
                pole.At(t).Lathe(new[] { new Vector2(0.32f, 0f), new Vector2(0.32f, 0.4f), new Vector2(0.17f, 0.45f), new Vector2(0.12f, H), new Vector2(0f, H + 0.05f) }, 10);
                for (float y = 1.5f; y < H - 1f; y += 0.6f) pole.At(t + new Vector3(0f, y, 0f)).Box(new Vector3(0.18f, 0f, 0f), new Vector3(0.24f, 0.03f, 0.03f)); // ladder rungs
                Vector3 target = t + (DriftTrackCourse.Center - t).normalized * 48f;
                Vector3 top = t + Vector3.up * (H + 0.6f);
                var aim = Quaternion.LookRotation(target - top);
                head.At(top, aim).Box(Vector3.zero, new Vector3(2.6f, 1.2f, 0.25f));
                head.At(t + Vector3.up * H, Quaternion.Euler(0f, aim.eulerAngles.y, 0f)).Box(Vector3.zero, new Vector3(2.8f, 0.12f, 0.12f));
                for (int k = 0; k < 8; k++)
                {
                    var lp = new Vector3(-0.96f + (k % 4) * 0.64f, k < 4 ? 0.28f : -0.28f, 0.14f);
                    glow.At(top + aim * lp, aim).Box(Vector3.zero, new Vector3(0.5f, 0.42f, 0.02f));
                }
                var l = new GameObject("Floodlight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.SetPositionAndRotation(top + aim * new Vector3(0f, 0f, 0.4f), aim);
                l.type = LightType.Spot;
                l.spotAngle = 112f;
                l.innerSpotAngle = 30f; // soft edge
                l.range = 110f;
                l.intensity = 1.25f; // a believable night track: lit enough to drive, no blown-out asphalt
                l.color = new Color(1f, 0.9f, 0.76f); // warm sodium-white
                l.shadows = i == 6 || i == 7 ? LightShadows.Soft : LightShadows.None; // two shadow casters (mid north / south)
                l.shadowStrength = 0.8f;
                l.renderMode = LightRenderMode.ForcePixel;
                TrackMats.Wall(root, "TowerCol", t + Vector3.up * 2f, new Vector3(0.6f, 4f, 0.6f));
            }
            TrackMats.Piece(root, "TowerPoles", pole.ToMesh("TrackTowerPoles"), TrackMats.Get("TrackGalvanised", new Color(0.5f, 0.52f, 0.54f), 0.55f, 0.6f));
            TrackMats.Piece(root, "TowerHeads", head.ToMesh("TrackTowerHeads"), TrackMats.Get("TrackSteelDark", new Color(0.22f, 0.23f, 0.25f), 0.5f, 0.7f));
            TrackMats.Piece(root, "TowerGlow", glow.ToMesh("TrackTowerGlow"), TrackMats.Glow("TrackLampGlow", new Color(1f, 0.97f, 0.9f), new Color(1.7f, 1.5f, 1.2f)), false);

            // Warm lamp over the gate and the kart row.
            var gate = new GameObject("GateLight").AddComponent<Light>();
            gate.transform.SetParent(root, false);
            gate.transform.position = new Vector3(DriftTrackLayout.PadEast - 4.5f, 4.2f, DriftTrackLayout.ArchZ + 8.5f); // range kept off the lobby room
            gate.type = LightType.Point;
            gate.range = 9.5f;
            gate.intensity = 1.2f;
            gate.color = new Color(1f, 0.8f, 0.55f);
            var lamp = new ProcMesh();
            lamp.At(new Vector3(DriftTrackLayout.PadEast - 0.15f, 4.4f, DriftTrackLayout.ArchZ + 8.5f)).Box(Vector3.zero, new Vector3(0.3f, 0.2f, 0.5f));
            TrackMats.Piece(root, "GateLamp", lamp.ToMesh("TrackGateLamp"), TrackMats.Glow("TrackGateLampGlow", new Color(1f, 0.85f, 0.6f), new Color(1.4f, 1.0f, 0.6f)), false);

            Sky(root);
        }

        /// <summary>Unlit dome (radius 450 m) round the pad: night blue, stars, a glow on the horizon.</summary>
        private static void Sky(Transform root)
        {
            const int segs = 48, rings = 16;
            const float R = 450f;
            var c = DriftTrackLayout.PadCenter;
            var v = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var t = new System.Collections.Generic.List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float el = Mathf.Lerp(-0.08f, Mathf.PI * 0.5f, j / (float)rings);
                for (int i = 0; i <= segs; i++)
                {
                    float az = i / (float)segs * Mathf.PI * 2f;
                    v.Add(c + new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el)) * R);
                    uv.Add(new Vector2(i / (float)segs * 2f, j / (float)rings));
                }
            }
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < segs; i++)
            {
                int a = j * (segs + 1) + i, b = a + 1, d = a + segs + 1, e = d + 1;
                t.AddRange(new[] { a, b, e, a, e, d }); // faces inwards
            }
            var mesh = new Mesh { name = "TrackSky" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var mat = new Material(Shader.Find("Sprites/Default")) { name = "TrackSky", mainTexture = SkyTexture(), renderQueue = 1990 };
            TrackMats.Piece(root, "Sky", mesh, mat, false).GetComponent<MeshRenderer>().receiveShadows = false;
        }

        private static Texture2D SkyTexture()
        {
            const int W = 1024, H = 512;
            var px = new Color32[W * H];
            var rng = new System.Random(4401);
            for (int y = 0; y < H; y++)
            {
                float k = y / (float)(H - 1);
                Color sky = Color.Lerp(new Color(0.11f, 0.1f, 0.14f), new Color(0.012f, 0.018f, 0.045f), Mathf.Pow(k, 0.45f));
                for (int x = 0; x < W; x++) px[y * W + x] = sky;
            }
            for (int s = 0; s < 1100; s++)
            {
                int x = rng.Next(W), y = 50 + rng.Next(H - 50);
                float b = (float)rng.NextDouble();
                b = 0.25f + 0.6f * b * b * b;
                var col = new Color(b, b * 0.97f, b * (0.9f + 0.2f * (float)rng.NextDouble()));
                px[y * W + x] = col;
                if (b > 0.85f && x + 1 < W) px[y * W + x + 1] = col * 0.6f;
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "TrackSkyTex", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
