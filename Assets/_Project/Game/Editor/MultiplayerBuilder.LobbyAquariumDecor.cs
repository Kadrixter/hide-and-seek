using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>Aquarium inside (driftwood, castle, plants, filter, air stone) and the shell (water, surface, glass, lid).</summary>
    public static partial class MultiplayerBuilder
    {
        private static void TankDecor(Transform tank, Material black)
        {
            // ---- driftwood root: a trunk with branches, lying on the left
            var wood = Mat("LobbyDriftwood", new Color(0.55f, 0.4f, 0.27f), Tex("lobby_wood", TexKind.Wood), 0.15f, new Vector2(0.3f, 1.5f));
            var root = new GameObject("Driftwood").transform;
            root.SetParent(tank, false);
            root.localPosition = new Vector3(-0.2f, 0.07f, -0.08f);
            root.localRotation = Quaternion.Euler(0f, 25f, 0f);
            void Branch(Vector3 from, Vector3 to, float d0)
            {
                var b = MeshObj("Branch", root, LatheSaved($"Branch_{d0:0.000}".Replace(',', '.'), new[] { new Vector2(d0 * 0.5f, 0f), new Vector2(d0 * 0.45f, 0.5f), new Vector2(d0 * 0.25f, 0.95f), new Vector2(0f, 1f) }, 10, true, false), wood, from);
                Vector3 dir = to - from;
                b.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                b.transform.localScale = new Vector3(1f, dir.magnitude, 1f);
            }
            Branch(new Vector3(-0.18f, 0f, 0f), new Vector3(0.2f, 0.06f, 0.02f), 0.05f);
            Branch(new Vector3(0.05f, 0.03f, 0f), new Vector3(0.12f, 0.24f, -0.04f), 0.03f);
            Branch(new Vector3(0.12f, 0.05f, 0.01f), new Vector3(0.26f, 0.2f, 0.06f), 0.022f);
            Branch(new Vector3(-0.08f, 0.01f, 0f), new Vector3(-0.16f, 0.17f, -0.06f), 0.025f);
            Branch(new Vector3(0.12f, 0.19f, -0.03f), new Vector3(0.05f, 0.3f, -0.05f), 0.014f);

            // ---- castle with a bubbler on the right
            var stone = Mat("LobbyCastleStone", new Color(0.8f, 0.76f, 0.68f), Tex("lobby_bricks", TexKind.Bricks), 0.2f, new Vector2(0.5f, 0.5f));
            var roof = Mat("LobbyCastleRoof", new Color(0.55f, 0.15f, 0.1f), null, 0.4f, Vector2.one);
            var castle = new GameObject("Castle").transform;
            castle.SetParent(tank, false);
            castle.localPosition = new Vector3(0.3f, 0.065f, -0.1f);
            castle.localRotation = Quaternion.Euler(0f, -18f, 0f);
            foreach (float x in new[] { -0.065f, 0.065f })
            {
                MeshObj("Tower", castle, LatheSaved("CastleTower", new[] { new Vector2(0.034f, 0f), new Vector2(0.032f, 0.15f), new Vector2(0.038f, 0.155f), new Vector2(0.038f, 0.17f) }, 16, false, true), stone, new Vector3(x, 0f, 0f));
                MeshObj("Roof", castle, LatheSaved("CastleRoof", new[] { new Vector2(0.042f, 0f), new Vector2(0f, 0.07f) }, 16, true, false), roof, new Vector3(x, 0.17f, 0f));
            }
            RBox(castle, "Wall", new Vector3(0f, 0.05f, 0f), new Vector3(0.12f, 0.1f, 0.05f), stone, 0.004f);
            for (int i = 0; i < 4; i++) RBox(castle, "Merlon", new Vector3(-0.045f + i * 0.03f, 0.108f, 0.018f), new Vector3(0.016f, 0.018f, 0.014f), stone, 0.002f);
            RBox(castle, "Gate", new Vector3(0f, 0.025f, 0.024f), new Vector3(0.034f, 0.05f, 0.006f), black, 0.003f);
            Marker(castle, "BubbleSource0", new Vector3(0.065f, 0.24f, 0f));

            // ---- plants (named Weed: they sway)
            var valis = Mat("LobbyAquaWeed", new Color(0.2f, 0.58f, 0.22f), null, 0.4f, Vector2.one);
            var sword = Mat("LobbyAquaSword", new Color(0.12f, 0.42f, 0.16f), null, 0.45f, Vector2.one);
            var red = Mat("LobbyAquaRed", new Color(0.55f, 0.18f, 0.12f), null, 0.4f, Vector2.one);
            var rnd = new System.Random(3);
            void Clump(float px, float pz, int n, Material m, float len, float wid, float elev, float droop, float spread)
            {
                for (int i = 0; i < n; i++)
                {
                    var g = new GameObject("Weed");
                    g.transform.SetParent(tank, false);
                    g.transform.localPosition = new Vector3(px + ((float)rnd.NextDouble() - 0.5f) * spread, 0.06f, pz + ((float)rnd.NextDouble() - 0.5f) * spread * 0.6f);
                    g.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    g.AddComponent<MeshFilter>().sharedMesh = LeafMesh(len * (0.75f + 0.5f * (float)rnd.NextDouble()), wid, elev, droop);
                    g.AddComponent<MeshRenderer>().sharedMaterial = m;
                }
            }
            Clump(-0.5f, -0.15f, 10, valis, 0.36f, 0.018f, 86f, 20f, 0.08f);   // tall strips, back left
            Clump(-0.38f, -0.16f, 7, valis, 0.3f, 0.016f, 84f, 25f, 0.07f);
            Clump(0.48f, -0.15f, 9, valis, 0.34f, 0.018f, 86f, 22f, 0.07f);   // back right
            Clump(0.05f, -0.14f, 8, sword, 0.18f, 0.06f, 62f, 40f, 0.05f);   // sword plant, back middle
            Clump(-0.05f, 0.08f, 5, red, 0.1f, 0.045f, 50f, 50f, 0.04f);     // small red plant in front
            Clump(0.18f, 0.1f, 6, sword, 0.09f, 0.04f, 45f, 45f, 0.04f);
            var moss = Mat("LobbyMossBall", new Color(0.14f, 0.36f, 0.12f), Tex("lobby_fabric", TexKind.Fabric), 0.1f, new Vector2(2f, 2f));
            foreach (var (x, z, s) in new[] { (-0.1f, 0.11f, 0.05f), (0.27f, 0.12f, 0.04f) })
                Ball("MossBall", tank, Vector3.zero, Vector3.one * s, moss).transform.localPosition = new Vector3(x, 0.06f + s * 0.4f, z);

            // ---- internal filter in the back right corner, air stone with its tube
            RBox(tank, "Filter", new Vector3(0.53f, 0.3f, -0.17f), new Vector3(0.07f, 0.28f, 0.05f), black, 0.008f);
            for (int i = 0; i < 6; i++) LocalBox(tank, "FilterSlot", new Vector3(0.53f, 0.2f + i * 0.02f, -0.144f), new Vector3(0.05f, 0.006f, 0.002f), Mat("LobbyFilterSlot", new Color(0.12f, 0.12f, 0.13f), null, 0.3f, Vector2.one));
            MeshObj("FilterOutlet", tank, LatheSaved("FilterOutlet", new[] { new Vector2(0.012f, 0f), new Vector2(0.012f, 0.03f) }, 10, false, true), black, new Vector3(0.53f, 0.44f, -0.135f), Quaternion.Euler(90f, 0f, 0f));
            Marker(tank, "Ripple", new Vector3(0.5f, 0.47f, -0.08f));
            Cyl("AirStone", tank, Vector3.zero, 0.035f, 0.025f, Mat("LobbyAirStone", new Color(0.7f, 0.68f, 0.62f), null, 0.1f, Vector2.one)).transform.localPosition = new Vector3(-0.45f, 0.072f, 0.06f);
            var tube = Mat("LobbyAirTube", new Color(0.75f, 0.85f, 0.8f, 1f), null, 0.8f, Vector2.one);
            var tb = MeshObj("AirTube", tank, LatheSaved("AirTube", new[] { new Vector2(0.003f, 0f), new Vector2(0.003f, 1f) }, 6, false, false), tube, new Vector3(-0.45f, 0.08f, 0.06f));
            tb.transform.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(-0.08f, 0.42f, -0.2f).normalized);
            tb.transform.localScale = new Vector3(1f, 0.47f, 1f);
            Marker(tank, "BubbleSource1", new Vector3(-0.45f, 0.09f, 0.06f));
        }

        private static void Marker(Transform parent, string name, Vector3 local)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = local;
        }

        private static void TankShell(Transform tank, Material black)
        {
            const float waterTop = TankH - 0.05f;
            LocalBox(tank, "Water", new Vector3(0f, waterTop * 0.5f + 0.01f, 0f), new Vector3(TankW - 0.016f, waterTop - 0.02f, TankD - 0.016f), Transparent("LobbyWater", new Color(0.35f, 0.75f, 0.8f, 0.16f)))
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // moving water surface seen from below / through the front glass (scrolled by LobbyAquarium)
            var surf = Transparent("LobbyWaterSurface", new Color(0.75f, 0.95f, 1f, 0.35f));
            surf.mainTexture = SmoothTex("lobby2_ripples", 128, 128, (x, y) =>
            {
                float a = Mathf.Sin((x + Mathf.Sin(y * 0.098f) * 8f) * 0.098f) * Mathf.Sin((y + Mathf.Sin(x * 0.049f) * 10f) * 0.147f);
                float v = 0.55f + 0.45f * a;
                return new Color(v, v, v, 0.35f + 0.5f * a * a);
            });
            surf.mainTextureScale = new Vector2(3f, 1.2f);
            foreach (float flip in new[] { 90f, -90f })
                QuadAt("WaterSurface", tank, tank.TransformPoint(new Vector3(0f, waterTop + 0.0005f * Mathf.Sign(flip), 0f)), tank.rotation * Quaternion.Euler(flip, 0f, 0f), new Vector2(TankW - 0.016f, TankD - 0.016f), surf);
            LocalBox(tank, "Glass", new Vector3(0f, TankH * 0.5f, 0f), new Vector3(TankW, TankH, TankD), GlassMat()).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            foreach (float sx in new[] { -TankW * 0.5f, TankW * 0.5f })
            foreach (float sz in new[] { -TankD * 0.5f, TankD * 0.5f })
                LocalBox(tank, "Post", new Vector3(sx, TankH * 0.5f, sz), new Vector3(0.016f, TankH + 0.004f, 0.016f), black);
            RBox(tank, "RimBottom", new Vector3(0f, 0.012f, 0f), new Vector3(TankW + 0.02f, 0.024f, TankD + 0.02f), black, 0.004f);
            RBox(tank, "RimTop", new Vector3(0f, TankH - 0.008f, 0f), new Vector3(TankW + 0.02f, 0.016f, TankD + 0.02f), black, 0.003f);
            RBox(tank, "Lid", new Vector3(0f, TankH + 0.022f, 0f), new Vector3(TankW + 0.02f, 0.04f, TankD + 0.02f), black, 0.008f);
            RBox(tank, "LidHatch", new Vector3(0f, TankH + 0.043f, 0.1f), new Vector3(0.3f, 0.004f, 0.14f), Mat("LobbyPlasticBlack", new Color(0.05f, 0.05f, 0.06f), null, 0.4f, Vector2.one), 0.002f);
            LocalBox(tank, "LidLight", new Vector3(0f, TankH - 0.017f, 0f), new Vector3(TankW - 0.1f, 0.003f, 0.06f), Emissive("LobbyAquaLed", new Color(0.8f, 0.95f, 1f), new Color(0.6f, 0.85f, 1f) * 2f));
            var lamp = new GameObject("TankLight").AddComponent<Light>();
            lamp.transform.SetParent(tank, false);
            lamp.transform.localPosition = new Vector3(0f, TankH - 0.06f, 0.05f);
            lamp.type = LightType.Point;
            lamp.color = new Color(0.6f, 0.85f, 1f);
            lamp.range = 1.4f;
            lamp.intensity = 0.9f;
        }
    }
}
