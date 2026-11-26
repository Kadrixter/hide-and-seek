using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Detailed lobby plant (dracaena-like): a lathed pot (terracotta with saucer, or a galvanised bucket with ribs and a
    /// wire handle in the garage style), soil with pebbles, three ringed canes topped with leaf rosettes and a rosette of
    /// long leaves at the base. Replaces the simple cylinder pot + blades built by <c>Plant</c>; the pot collider stays.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void DetailedPlant(Transform t, Material leaf, Material leafDark, int seed)
        {
            // drop the old look, keep the pot collider
            var old = new List<GameObject>();
            foreach (Transform c in t)
            {
                if (c.name == "Pot")
                {
                    var mr = c.GetComponent<MeshRenderer>();
                    if (mr != null) Object.DestroyImmediate(mr);
                    var mf = c.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf);
                }
                else old.Add(c.gameObject);
            }
            foreach (var g in old) Object.DestroyImmediate(g);

            var m = new GameObject("PlantModel").transform;
            m.SetParent(t, false);
            m.localRotation = Quaternion.Euler(0f, seed * 47f, 0f);
            var rnd = new System.Random(seed * 131 + 17);
            float R() => (float)rnd.NextDouble();

            bool garage = LobbyLayout.Garage;
            float soilY;
            if (garage)
            {
                var zinc = Mat("GarageBucket", new Color(0.56f, 0.58f, 0.57f), Tex("garage_ceiling", TexKind.Concrete), 0.5f, new Vector2(1f, 0.5f), 0.65f);
                MeshObj("Bucket", m, LatheSaved("PlantBucket2", new[]
                {
                    new Vector2(0.17f, 0f), new Vector2(0.176f, 0.012f), new Vector2(0.196f, 0.12f), new Vector2(0.203f, 0.124f), new Vector2(0.199f, 0.132f),
                    new Vector2(0.214f, 0.3f), new Vector2(0.221f, 0.304f), new Vector2(0.217f, 0.312f), new Vector2(0.233f, 0.462f),
                    new Vector2(0.246f, 0.47f), new Vector2(0.247f, 0.484f), new Vector2(0.236f, 0.492f), new Vector2(0.226f, 0.48f),
                    new Vector2(0.222f, 0.46f), new Vector2(0.205f, 0.3f),
                }, 40, true, false), zinc, Vector3.zero);
                // ears + wire handle resting down against the side
                var wire = GarageSteel;
                foreach (float sx in new[] { -1f, 1f })
                    RBox(m, "Ear", new Vector3(sx * 0.238f, 0.43f, 0f), new Vector3(0.012f, 0.05f, 0.04f), zinc, 0.004f);
                var h = new GameObject("Handle").transform;
                h.SetParent(m, false);
                h.localPosition = new Vector3(0f, 0.44f, 0f);
                h.localRotation = Quaternion.Euler(-100f, 0f, 0f); // hangs down against the bucket side
                const int segs = 12;
                for (int i = 0; i < segs; i++)
                {
                    float a0 = i / (float)segs * Mathf.PI, a1 = (i + 1) / (float)segs * Mathf.PI;
                    var p0 = new Vector3(Mathf.Cos(a0) * 0.245f, Mathf.Sin(a0) * 0.3f, 0f);
                    var p1 = new Vector3(Mathf.Cos(a1) * 0.245f, Mathf.Sin(a1) * 0.3f, 0f);
                    var d = p1 - p0;
                    var s = new GameObject("Wire").transform;
                    s.SetParent(h, false);
                    s.localPosition = (p0 + p1) * 0.5f;
                    s.localRotation = Quaternion.LookRotation(d.normalized, Vector3.forward);
                    MeshObj("W", s, LatheSaved("PlantWire", new[] { new Vector2(0.004f, -0.5f), new Vector2(0.004f, 0.5f) }, 6, false, false), wire, Vector3.zero, Quaternion.Euler(90f, 0f, 0f)).transform.localScale = new Vector3(1f, d.magnitude * 1.05f, 1f);
                }
                // grip
                MeshObj("Grip", h, LatheSaved("PlantGrip", new[] { new Vector2(0.009f, -0.05f), new Vector2(0.011f, -0.04f), new Vector2(0.011f, 0.04f), new Vector2(0.009f, 0.05f) }, 10, true, true),
                    Mat("GarageGripBlack", new Color(0.05f, 0.05f, 0.05f), null, 0.3f, Vector2.one), new Vector3(0f, 0.3f, 0f), Quaternion.Euler(0f, 0f, 90f));
                soilY = 0.44f;
            }
            else
            {
                var clay = Mat("LobbyPotTerracotta", new Color(0.66f, 0.33f, 0.2f), null, 0.2f, Vector2.one);
                var clayDark = Mat("LobbyPotTerracottaDark", new Color(0.5f, 0.24f, 0.15f), null, 0.15f, Vector2.one);
                MeshObj("Saucer", m, LatheSaved("PlantSaucer2", new[]
                {
                    new Vector2(0.2f, 0f), new Vector2(0.232f, 0.008f), new Vector2(0.236f, 0.032f), new Vector2(0.228f, 0.034f), new Vector2(0.222f, 0.012f), new Vector2(0.18f, 0.008f),
                }, 40, true, true), clayDark, Vector3.zero);
                MeshObj("Pot", m, LatheSaved("PlantPot2", new[]
                {
                    new Vector2(0.15f, 0.01f), new Vector2(0.158f, 0.02f), new Vector2(0.155f, 0.03f), new Vector2(0.2f, 0.37f),
                    new Vector2(0.235f, 0.375f), new Vector2(0.242f, 0.39f), new Vector2(0.242f, 0.468f), new Vector2(0.236f, 0.482f),
                    new Vector2(0.222f, 0.486f), new Vector2(0.21f, 0.475f), new Vector2(0.192f, 0.37f),
                }, 40, true, false), clay, Vector3.zero);
                soilY = 0.43f;
            }

            // soil + pebbles + a little moss
            var soil = Mat("LobbySoil", new Color(0.12f, 0.08f, 0.05f), null, 0f, Vector2.one);
            MeshObj("Soil", m, LatheSaved(garage ? "PlantSoilB" : "PlantSoilP", new[]
            {
                new Vector2(garage ? 0.228f : 0.205f, 0f), new Vector2(0.15f, 0.012f), new Vector2(0.06f, 0.02f), new Vector2(0f, 0.022f),
            }, 32, false, false), soil, new Vector3(0f, soilY, 0f));
            var pebble = Mat("LobbyPebble", new Color(0.62f, 0.6f, 0.56f), null, 0.35f, Vector2.one);
            var pebbleDark = Mat("LobbyPebbleDark", new Color(0.35f, 0.34f, 0.32f), null, 0.3f, Vector2.one);
            var peb = LatheSaved("PlantPebble", new[] { new Vector2(0.018f, 0f), new Vector2(0.017f, 0.006f), new Vector2(0.011f, 0.011f), new Vector2(0f, 0.013f) }, 10, true, false);
            var moss = Mat("LobbyMoss", new Color(0.2f, 0.32f, 0.1f), null, 0f, Vector2.one);
            for (int i = 0; i < 11; i++)
            {
                float a = R() * Mathf.PI * 2f, r = 0.08f + R() * 0.1f;
                var p = MeshObj(i < 8 ? "Pebble" : "Moss", m, peb, i < 8 ? (i % 3 == 0 ? pebbleDark : pebble) : moss,
                    new Vector3(Mathf.Cos(a) * r, soilY + 0.006f + (0.2f - r) * 0.06f, Mathf.Sin(a) * r), Quaternion.Euler(0f, R() * 360f, 0f), false);
                p.transform.localScale = i < 8 ? new Vector3(0.7f + R() * 0.8f, 0.8f + R() * 0.5f, 0.7f + R() * 0.6f) : new Vector3(2.2f, 0.5f, 1.8f);
            }

            float baseY = soilY + 0.02f;
            var leafLight = Mat("LobbyLeafLight", new Color(0.3f, 0.5f, 0.16f), null, 0.4f, Vector2.one);
            Material LeafMat(int i) => (i % 3) switch { 0 => leaf, 1 => leafDark, _ => leafLight };

            // canes with node rings, a rosette on each top
            var bark = Mat("LobbyCane", new Color(0.36f, 0.27f, 0.18f), Tex("lobby_fabric", TexKind.Fabric), 0.15f, new Vector2(0.5f, 3f));
            float[] heights = { 0.62f + R() * 0.12f, 0.42f + R() * 0.1f, 0.25f + R() * 0.08f };
            for (int c = 0; c < heights.Length; c++)
            {
                float hgt = heights[c];
                float ca = c * 120f + R() * 30f;
                var cane = new GameObject("Cane").transform;
                cane.SetParent(m, false);
                cane.localPosition = new Vector3(0f, baseY - 0.02f, 0f) + Quaternion.Euler(0f, ca, 0f) * new Vector3(0f, 0f, 0.035f);
                cane.localRotation = Quaternion.Euler(0f, ca, 0f) * Quaternion.Euler(5f + R() * 6f, 0f, 0f);
                var prof = new List<Vector2>();
                int nodes = Mathf.RoundToInt(hgt / 0.07f);
                for (int k = 0; k <= nodes; k++)
                {
                    float y = hgt * k / nodes;
                    float r = Mathf.Lerp(0.022f, 0.013f, k / (float)nodes);
                    prof.Add(new Vector2(r, y));
                    if (k < nodes) { prof.Add(new Vector2(r + 0.003f, y + 0.004f)); prof.Add(new Vector2(r, y + 0.008f)); }
                }
                MeshObj("Stem", cane, LatheSaved($"PlantCane_{c}_{hgt:0.000}".Replace(',', '.'), prof.ToArray(), 10, true, true), bark, Vector3.zero);
                // rosette: drooping outer ring, upright young leaves in the middle
                int n = 13;
                for (int i = 0; i < n; i++)
                {
                    bool young = i >= 9;
                    float yaw = i * 137.5f + R() * 15f;
                    float len = young ? 0.2f + R() * 0.08f : 0.3f + R() * 0.16f;
                    float elev = young ? 70f + R() * 12f : 25f + R() * 30f;
                    float droop = young ? 15f : 55f + R() * 35f;
                    var lf = new GameObject("Leaf");
                    lf.transform.SetParent(cane, false);
                    lf.transform.localPosition = new Vector3(0f, hgt - 0.01f - (young ? 0f : (i % 3) * 0.012f), 0f);
                    lf.transform.localRotation = Quaternion.Euler(0f, yaw, (R() - 0.5f) * 14f);
                    lf.AddComponent<MeshFilter>().sharedMesh = LeafMesh(Mathf.Round(len * 20f) / 20f, young ? 0.05f : 0.07f, Mathf.Round(elev / 5f) * 5f, Mathf.Round(droop / 5f) * 5f);
                    lf.AddComponent<MeshRenderer>().sharedMaterial = young ? leafLight : LeafMat(i);
                    lf.isStatic = true;
                }
            }

            // base rosette of long arching leaves, a couple of dry ones at the rim
            var dry = Mat("LobbyLeafDry", new Color(0.45f, 0.36f, 0.16f), null, 0.15f, Vector2.one);
            for (int i = 0; i < 16; i++)
            {
                bool dead = i < 2;
                float yaw = i * 137.5f + R() * 20f;
                float len = dead ? 0.3f : 0.4f + R() * 0.3f;
                float elev = dead ? 15f : 40f + R() * 35f;
                float droop = dead ? 40f : 50f + R() * 40f;
                var lf = new GameObject("Leaf");
                lf.transform.SetParent(m, false);
                lf.transform.localPosition = new Vector3(0f, baseY - 0.01f, 0f) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 0.02f + R() * 0.04f);
                lf.transform.localRotation = Quaternion.Euler(0f, yaw, (R() - 0.5f) * 20f);
                lf.AddComponent<MeshFilter>().sharedMesh = LeafMesh(Mathf.Round(len * 20f) / 20f, dead ? 0.05f : 0.08f, Mathf.Round(elev / 5f) * 5f, Mathf.Round(droop / 5f) * 5f);
                lf.AddComponent<MeshRenderer>().sharedMaterial = dead ? dry : LeafMat(i);
                lf.isStatic = true;
            }

            foreach (var tr in m.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = true;
        }
    }
}
