using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Bookshelf on the west wall: carcass with a crown and plinth, back panel, four shelves filled with books of
    /// different heights / widths (some leaning, some lying in stacks, gold bands on the spines), a plant, a photo
    /// frame, a vase and a box. Built facing +Z, turned to face the room.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static readonly Color[] BookColours =
        {
            new(0.45f, 0.1f, 0.08f), new(0.12f, 0.25f, 0.4f), new(0.15f, 0.33f, 0.18f), new(0.5f, 0.38f, 0.2f), new(0.75f, 0.68f, 0.52f),
            new(0.2f, 0.16f, 0.13f), new(0.55f, 0.22f, 0.3f), new(0.1f, 0.35f, 0.38f), new(0.7f, 0.45f, 0.12f), new(0.32f, 0.12f, 0.35f),
        };

        private static void Bookshelf(Transform parent, Vector3 pos, Material darkWood)
        {
            var t = Frame(parent, "Bookshelf", pos, 90f); // west wall, faces +X
            const float w = 2.2f, h = 2.2f, d = 0.38f, side = 0.04f;
            RBox(t, "SideL", new Vector3(-w * 0.5f + side * 0.5f, h * 0.5f, 0f), new Vector3(side, h, d), darkWood, 0.008f);
            RBox(t, "SideR", new Vector3(w * 0.5f - side * 0.5f, h * 0.5f, 0f), new Vector3(side, h, d), darkWood, 0.008f);
            RBox(t, "Top", new Vector3(0f, h - 0.02f, 0.005f), new Vector3(w + 0.06f, 0.04f, d + 0.03f), darkWood, 0.01f);
            RBox(t, "Crown", new Vector3(0f, h + 0.02f, 0.01f), new Vector3(w + 0.1f, 0.04f, d + 0.05f), darkWood, 0.012f);
            RBox(t, "Plinth", new Vector3(0f, 0.05f, 0.01f), new Vector3(w, 0.1f, d - 0.02f), darkWood, 0.008f);
            LocalBox(t, "Back", new Vector3(0f, h * 0.5f, -d * 0.5f + 0.005f), new Vector3(w - 0.02f, h - 0.02f, 0.01f), Mat("LobbyShelfBack", new Color(0.3f, 0.2f, 0.13f), Tex("lobby_wood", TexKind.Wood), 0.3f, new Vector2(1f, 1f)));
            float[] shelves = { 0.1f, 0.62f, 1.12f, 1.62f };
            foreach (float y in shelves)
                RBox(t, "Shelf", new Vector3(0f, y + 0.012f, 0.01f), new Vector3(w - side * 2f, 0.024f, d - 0.03f), darkWood, 0.006f);
            var bookMats = new Material[BookColours.Length];
            for (int i = 0; i < bookMats.Length; i++) bookMats[i] = Mat("LobbyBook" + i, BookColours[i], Tex("lobby_fabric", TexKind.Fabric), 0.25f, new Vector2(0.3f, 0.6f));
            var gold = Mat("LobbyBookGold", new Color(0.8f, 0.62f, 0.25f), null, 0.6f, Vector2.one, 0.8f);
            var rnd = new System.Random(21);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float Q(float v) => Mathf.Round(v * 200f) / 200f; // 5 mm steps: fewer rounded-box meshes
            for (int s = 0; s < shelves.Length; s++)
            {
                float y = shelves[s] + 0.024f, maxH = (s < shelves.Length - 1 ? shelves[s + 1] : h - 0.04f) - y - 0.04f;
                float x = -w * 0.5f + side + 0.02f, end = w * 0.5f - side - 0.02f;
                int run = 0;
                while (x < end - 0.05f)
                {
                    float gap = rnd.NextDouble() < 0.12 ? R(0.12f, 0.3f) : 0f;
                    if (gap > 0f && s > 0 && rnd.NextDouble() < 0.5)
                    {
                        // ornament in the gap: plant, vase, photo frame or a box
                        float cx = x + gap * 0.5f;
                        int k = rnd.Next(4);
                        if (k == 0) Plantlet(t, new Vector3(cx, y, 0.02f));
                        else if (k == 1) MeshObj("Vase", t, LatheSaved("ShelfVase", new[] { new Vector2(0.035f, 0f), new Vector2(0.05f, 0.06f), new Vector2(0.03f, 0.16f), new Vector2(0.035f, 0.19f) }, 20, true, false), Mat("LobbyVaseCream", new Color(0.9f, 0.86f, 0.76f), null, 0.75f, Vector2.one), new Vector3(cx, y, 0.03f));
                        else if (k == 2)
                        {
                            var fr = RBox(t, "PhotoFrame", new Vector3(cx, y + 0.09f, 0.0f), new Vector3(0.14f, 0.18f, 0.015f), Mat("LobbyFrameGilt", new Color(0.62f, 0.46f, 0.2f), null, 0.65f, Vector2.one, 0.7f), 0.004f);
                            fr.transform.localRotation = Quaternion.Euler(-10f, R(-15f, 15f), 0f);
                            LocalBox(fr.transform, "Photo", new Vector3(0f, 0f, 0.0085f), new Vector3(0.11f, 0.15f, 0.002f), Mat("LobbyPhotoInside", new Color(0.5f, 0.6f, 0.75f), null, 0.4f, Vector2.one));
                        }
                        else RBox(t, "Box", new Vector3(cx, y + 0.05f, 0.02f), new Vector3(gap * 0.7f, 0.1f, 0.2f), Mat("LobbyBoxRed", new Color(0.55f, 0.12f, 0.1f), null, 0.2f, Vector2.one), 0.008f);
                        x += gap;
                        continue;
                    }
                    if (rnd.NextDouble() < 0.1 && end - x > 0.3f)
                    {
                        // a lying stack
                        float sy = y;
                        int n = rnd.Next(2, 5);
                        for (int i = 0; i < n; i++)
                        {
                            float bt = Q(R(0.03f, 0.05f)), bw = Q(R(0.2f, 0.26f)), bd = Q(R(0.15f, 0.2f));
                            var b = RBox(t, "Book", new Vector3(x + 0.14f, sy + bt * 0.5f, R(-0.03f, 0.03f)), new Vector3(bw, bt, bd), bookMats[rnd.Next(bookMats.Length)], 0.004f);
                            b.transform.localRotation = Quaternion.Euler(0f, R(-8f, 8f), 0f);
                            sy += bt;
                        }
                        x += 0.3f;
                        continue;
                    }
                    float bw2 = Q(R(0.025f, 0.055f)), bh = Q(Mathf.Min(maxH, R(0.2f, 0.32f))), bd2 = Q(R(0.17f, 0.25f));
                    var mat = bookMats[rnd.Next(bookMats.Length)];
                    float lean = run > 6 && rnd.NextDouble() < 0.15 ? R(8f, 16f) : 0f;
                    var book = RBox(t, "Book", new Vector3(x + bw2 * 0.5f, y + bh * 0.5f, d * 0.5f - 0.03f - bd2 * 0.5f), new Vector3(bw2, bh, bd2), mat, 0.004f);
                    if (lean > 0f)
                    {
                        book.transform.localRotation = Quaternion.Euler(0f, 0f, -lean);
                        book.transform.localPosition += new Vector3(Mathf.Sin(lean * Mathf.Deg2Rad) * bh * 0.5f, -bh * 0.5f * (1f - Mathf.Cos(lean * Mathf.Deg2Rad)), 0f);
                        x += Mathf.Sin(lean * Mathf.Deg2Rad) * bh;
                        run = 0;
                    }
                    if (rnd.NextDouble() < 0.4)
                        foreach (float by in new[] { 0.3f, 0.36f })
                            LocalBox(book.transform, "Band", new Vector3(0f, by * bh, bd2 * 0.5f + 0.0012f), new Vector3(bw2 - 0.004f, 0.01f, 0.002f), gold);
                    x += bw2 + 0.002f;
                    run++;
                }
            }
            // top: a big plant and a globe-ish lamp? -> two stacked boxes and a trailing plant
            Plantlet(t, new Vector3(0.75f, h + 0.04f, 0.02f));
            RBox(t, "TopBox", new Vector3(-0.6f, h + 0.1f, 0.0f), new Vector3(0.36f, 0.12f, 0.26f), Mat("LobbyBoxRed", new Color(0.55f, 0.12f, 0.1f), null, 0.2f, Vector2.one), 0.01f);
            RBox(t, "TopBox2", new Vector3(-0.58f, h + 0.205f, 0.0f), new Vector3(0.28f, 0.09f, 0.22f), Mat("LobbyBoxTeal", new Color(0.15f, 0.4f, 0.42f), null, 0.2f, Vector2.one), 0.01f);
            BoxCollider(t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d));
        }

        /// <summary>Small potted plant for shelves (pot + a tuft of leaves).</summary>
        private static void Plantlet(Transform parent, Vector3 local)
        {
            var p = new GameObject("Plantlet").transform;
            p.SetParent(parent, false);
            p.localPosition = local;
            MeshObj("Pot", p, LatheSaved("ShelfPot", new[] { new Vector2(0.04f, 0f), new Vector2(0.055f, 0.1f), new Vector2(0.06f, 0.11f), new Vector2(0.05f, 0.105f) }, 20, true, false), Mat("LobbyPotTerracotta", new Color(0.66f, 0.33f, 0.2f), null, 0.2f, Vector2.one), Vector3.zero);
            var leaf = Mat("LobbyLeaf", new Color(0.16f, 0.42f, 0.14f), Tex("lobby_fabric", TexKind.Fabric), 0.2f, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 9; i++)
            {
                var g = new GameObject("Leaf");
                g.transform.SetParent(p, false);
                g.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                g.transform.localRotation = Quaternion.Euler(0f, i * 40f, 0f);
                g.AddComponent<MeshFilter>().sharedMesh = LeafMesh(0.16f + (i % 3) * 0.04f, 0.05f, 50f + (i % 4) * 8f, 70f);
                g.AddComponent<MeshRenderer>().sharedMaterial = leaf;
            }
        }
    }
}
