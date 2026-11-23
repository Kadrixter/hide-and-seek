using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Aquarium fish: a school of neon tetras, two goldfish, two angelfish, two corydoras catfish on the bottom.
    /// Bodies are lathed and flattened, every fish has a tail on a pivot ("Tail", wagged by <see cref="LobbyAquarium"/>),
    /// fins and eyes. Names: Fish_&lt;species&gt;_&lt;n&gt; (the runtime reads the species from the name). Forward = +Z.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void AquariumFish(Transform tank)
        {
            var eye = Mat("LobbyFishEye", new Color(0.02f, 0.02f, 0.02f), null, 0.9f, Vector2.one);
            var fin = Transparent("LobbyFishFin", new Color(0.95f, 0.85f, 0.7f, 0.45f), 0.4f);
            var rnd = new System.Random(77);
            Vector3 P(float y0, float y1) => new(((float)rnd.NextDouble() - 0.5f) * 0.9f, y0 + (float)rnd.NextDouble() * (y1 - y0), ((float)rnd.NextDouble() - 0.5f) * 0.26f);

            // neon tetras: small, blue back with a red rear half, a school of 9
            var tetraBlue = Mat("LobbyFishTetra", new Color(0.15f, 0.55f, 1f), null, 0.85f, Vector2.one, 0.3f);
            var tetraRed = Mat("LobbyFishTetraRed", new Color(0.85f, 0.1f, 0.1f), null, 0.7f, Vector2.one);
            var silver = Mat("LobbyFishSilver", new Color(0.8f, 0.82f, 0.85f), null, 0.85f, Vector2.one, 0.5f);
            for (int i = 0; i < 9; i++)
            {
                var f = Fish(tank, $"Fish_tetra_{i}", P(0.22f, 0.36f), 0.034f, 0.009f, 0.004f, silver, fin, eye, 0.6f);
                LocalBox(f, "Stripe", new Vector3(0f, 0.0015f, 0.002f), new Vector3(0.0046f, 0.0025f, 0.024f), tetraBlue);
                LocalBox(f, "Red", new Vector3(0f, -0.0015f, -0.005f), new Vector3(0.0044f, 0.003f, 0.014f), tetraRed);
            }
            // goldfish: round body, big double tail
            var gold = Mat("LobbyFishGold", new Color(1f, 0.5f, 0.08f), null, 0.8f, Vector2.one, 0.2f);
            var goldFin = Transparent("LobbyFishGoldFin", new Color(1f, 0.6f, 0.2f, 0.6f), 0.5f);
            for (int i = 0; i < 2; i++) Fish(tank, $"Fish_gold_{i}", P(0.15f, 0.3f), 0.075f, 0.036f, 0.022f, gold, goldFin, eye, 1.4f);
            // angelfish: tall, silver with dark bars, long fins
            var angel = Mat("LobbyFishAngel", new Color(0.82f, 0.82f, 0.75f), null, 0.8f, Vector2.one, 0.3f);
            var bar = Mat("LobbyFishBar", new Color(0.12f, 0.1f, 0.08f), null, 0.5f, Vector2.one);
            for (int i = 0; i < 2; i++)
            {
                var f = Fish(tank, $"Fish_angel_{i}", P(0.2f, 0.32f), 0.06f, 0.05f, 0.008f, angel, fin, eye, 1.1f);
                foreach (float z in new[] { 0.012f, -0.01f }) LocalBox(f, "Bar", new Vector3(0f, 0f, z), new Vector3(0.0085f, 0.044f, 0.005f), bar);
                FinTri(f, "Dorsal", new Vector3(0f, 0.022f, -0.006f), 0.05f, 0.03f, true, fin);
                FinTri(f, "Anal", new Vector3(0f, -0.022f, -0.006f), 0.05f, 0.03f, false, fin);
            }
            // corydoras: grey, spotted, belly flat, stay on the gravel
            var cory = Mat("LobbyFishCory", new Color(0.55f, 0.52f, 0.45f), null, 0.6f, Vector2.one);
            for (int i = 0; i < 2; i++) Fish(tank, $"Fish_cory_{i}", new Vector3(-0.2f + i * 0.35f, 0.085f, 0.08f), 0.045f, 0.016f, 0.012f, cory, fin, eye, 0.8f);
        }

        /// <summary>Fish of length L (forward +Z), height h, width w; tail on a pivot at the back.</summary>
        private static Transform Fish(Transform tank, string name, Vector3 local, float L, float h, float w, Material body, Material fin, Material eye, float tailScale)
        {
            var f = new GameObject(name).transform;
            f.SetParent(tank, false);
            f.localPosition = local;
            f.localRotation = Quaternion.Euler(0f, Hash01(name.GetHashCode()) * 360f, 0f);
            string key = $"FishBody_{L:0.000}_{h:0.000}_{w:0.000}".Replace(',', '.');
            var mesh = LobbyMeshSaved(key, () => FishBodyMesh(L, h, w));
            MeshObj("Body", f, mesh, body, Vector3.zero);
            // eyes
            foreach (float s in new[] { -1f, 1f })
                Ball("Eye", f, f.position, Vector3.one * Mathf.Max(0.003f, h * 0.18f), eye).transform.localPosition = new Vector3(s * w * 0.42f, h * 0.12f, L * 0.36f);
            // tail: forked fan on a pivot
            var tail = new GameObject("Tail").transform;
            tail.SetParent(f, false);
            tail.localPosition = new Vector3(0f, 0f, -L * 0.44f);
            float tl = L * 0.32f * tailScale, th = h * 0.9f * tailScale;
            var outline = new List<Vector2> { new(0f, 0f), new(-th * 0.6f, -tl), new(-th * 0.2f, -tl * 0.75f), new(th * 0.2f, -tl * 0.75f), new(th * 0.6f, -tl) };
            // outline in (x = height, z = back): extrude 1 mm, then stand it up (x -> y)
            var tmesh = LobbyMeshSaved($"FishTail_{tl:0.000}_{th:0.000}".Replace(',', '.'), () => LobbyMesh.Extrude(Convex(outline), 0.0012f));
            MeshObj("Fan", tail, tmesh, fin, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), false);
            FinTri(f, "DorsalFin", new Vector3(0f, h * 0.45f, -L * 0.05f), L * 0.3f, h * 0.35f, true, fin);
            return f;
        }

        /// <summary>Convex hull order is not needed for the tail (fan); keep the outline but split the fork into a fan.</summary>
        private static List<Vector2> Convex(List<Vector2> o) => new() { o[0], o[1], o[4] };

        /// <summary>Triangular fin standing up (or down) from the body, sweeping back.</summary>
        private static void FinTri(Transform f, string name, Vector3 local, float len, float height, bool up, Material m)
        {
            float s = up ? 1f : -1f;
            var outline = new List<Vector2> { new(0f, len * 0.5f), new(0f, -len * 0.5f), new(height * s, -len * 0.6f) };
            if (!up) outline.Reverse();
            var mesh = LobbyMeshSaved($"Fin_{len:0.000}_{height:0.000}_{(up ? 1 : 0)}".Replace(',', '.'), () => LobbyMesh.Extrude(outline, 0.001f));
            MeshObj(name, f, mesh, m, local, Quaternion.Euler(0f, 0f, 90f), false);
        }

        /// <summary>Lathed fish body along +Z (head at +Z, blunt), flattened to width w.</summary>
        private static Mesh FishBodyMesh(float L, float h, float w)
        {
            var prof = new List<Vector2>();
            const int n = 12;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;                                   // 0 tail .. 1 nose
                float r = h * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 1f, t)), 0.8f) * (t < 0.15f ? 0.5f + t * 3.3f : 1f);
                if (i == n) r = 0f;
                prof.Add(new Vector2(Mathf.Max(r, i == n ? 0f : h * 0.08f), t * L - L * 0.5f));
            }
            var m = LobbyMesh.Lathe(prof, 16, true, false);
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = new Vector3(v[i].x * (w / h), v[i].z, v[i].y); // axis Y -> Z, flatten X
            m.vertices = v;
            m.triangles = Flip(m.triangles); // mirroring (y <-> z) flips the winding
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static int[] Flip(int[] t)
        {
            for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            return t;
        }
    }
}
