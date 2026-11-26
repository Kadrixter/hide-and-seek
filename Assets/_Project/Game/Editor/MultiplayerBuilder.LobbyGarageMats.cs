using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Garage floor coverings and the lounge table (<see cref="LobbyLayout.Garage"/>): black interlocking rubber tiles
    /// under the lounge (in place of the Persian rug), a ribbed rubber runner at the exit (kilim), a studded rubber mat
    /// with a yellow edge in front of the arcades (arcade carpet); the chess spot stands on bare concrete. The coffee
    /// table is three pallets with a plywood top (top at 0.45 m like the old table, the snacks keep their place).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void BuildGarageMats(Transform root)
        {
            var t = new GameObject("Rugs").transform;
            t.SetParent(root, false);
            Rug(t, "MatLounge", new Vector3(-4.15f, 0f, 3.1f), new Vector2(3.6f, 3.0f), 0f, Mat("GarageMatPuzzle", Color.white, PuzzleMatTex(), 0.18f, Vector2.one), false);
            Rug(t, "MatRunner", new Vector3(LobbyLayout.ExitDoorX, 0f, -LobbyLayout.HalfD + 1.6f), new Vector2(0.9f, 1.5f), 0f, Mat("GarageMatRibbed", Color.white, RibbedMatTex(), 0.22f, Vector2.one), false);
            float ax = (LobbyLayout.Arcades[0].pos.x + LobbyLayout.Arcades[1].pos.x) * 0.5f;
            Rug(t, "MatArcade", new Vector3(ax, 0f, LobbyLayout.Arcades[0].pos.z - 0.95f), new Vector2(2.2f, 1.0f), 0f, Mat("GarageMatStud", Color.white, StudMatTex(), 0.25f, Vector2.one), false);
        }

        /// <summary>3.6 x 3.0 m of 0.6 m EVA puzzle tiles (160 px each): toothed seams, a few faded tiles, dust and scuffs.</summary>
        private static Texture2D PuzzleMatTex() => SmoothTex("garage_mat_puzzle", 960, 800, (x, y) =>
        {
            float fu = x / 160f, fv = y / 160f;
            int tx = Mathf.FloorToInt(fu), ty = Mathf.FloorToInt(fv);
            // per-tile shade: most black, a couple faded to charcoal
            float h = Hash01(tx * 31 + ty * 17 + 5);
            float shade = h > 0.82f ? 0.2f : 0.1f + h * 0.03f;
            // seams: a square wave (teeth 1/8 of a tile wide, 0.05 deep) along every tile edge
            float Seam(float along, float across)
            {
                float k = Mathf.Repeat(along * 8f, 2f) < 1f ? 0.05f : -0.05f;
                float d = Mathf.Abs(across - Mathf.Round(across) - k);
                float step = Mathf.Abs(Mathf.Repeat(along * 8f + 0.5f, 1f) - 0.5f) / 8f; // distance to a tooth flank
                if (Mathf.Abs(across - Mathf.Round(across)) < 0.05f) d = Mathf.Min(d, step);
                return d;
            }
            float seam = Mathf.Min(Seam(fv, fu), Seam(fu, fv));
            float line = 1f - Step01(0.006f, 0.016f, seam);
            // fine diamond texture of the top face
            float dia = Mathf.Abs(Mathf.Sin((x + y) * 0.9f)) * Mathf.Abs(Mathf.Sin((x - y) * 0.9f));
            float c = shade * (0.9f + 0.18f * dia);
            // dust towards the outer edge, scuffs where people walk
            float u = x / 960f, v = y / 800f;
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float dust = (1f - Step01(0f, 0.12f, edge)) * 0.5f + Fbm(u * 6f + 3f, v * 6f) * 0.35f;
            dust = Step01(0.3f, 0.75f, dust) * 0.12f;
            float scuff = Step01(0.62f, 0.8f, Fbm(u * 18f, v * 3f + 7f)) * 0.05f;
            c = c + dust + scuff;
            c *= 1f - 0.65f * line;
            return new Color(c, c, c * 1.03f);
        });

        /// <summary>Ribbed rubber runner: ribs along its length, flat border, grit near the door end.</summary>
        private static Texture2D RibbedMatTex() => SmoothTex("garage_mat_ribbed", 192, 320, (x, y) =>
        {
            float u = x / 192f, v = y / 320f;
            float border = Mathf.Min(Mathf.Min(u, 1f - u) * 0.9f, Mathf.Min(v, 1f - v) * 1.5f);
            float c = 0.08f;
            if (border > 0.05f)
            {
                float rib = Mathf.Sin(x * 0.75f);
                c = 0.07f + 0.05f * Mathf.Max(0f, rib) - 0.03f * Mathf.Max(0f, -rib);
            }
            float grit = Step01(0.55f, 0.8f, Fbm(u * 8f, v * 8f + 2f) + (1f - v) * 0.25f) * 0.1f;
            c += grit * (0.6f + 0.4f * Hash01(x * 7 + y * 13));
            return new Color(c, c, c);
        });

        /// <summary>Studded (coin) rubber with a bevelled yellow edge: 2.2 x 1.0 m in 512 x 232 px.</summary>
        private static Texture2D StudMatTex() => SmoothTex("garage_mat_stud", 512, 232, (x, y) =>
        {
            float u = x / 512f, v = y / 232f;
            float e = Mathf.Min(Mathf.Min(u, 1f - u) * 2.2f, Mathf.Min(v, 1f - v)); // metres to the edge
            float wear = Fbm(u * 10f, v * 5f + 4f);
            if (e < 0.05f)
            {
                var yel = new Color(0.85f, 0.65f, 0.08f) * (0.8f + 0.25f * wear);
                float worn = Step01(0.55f, 0.7f, wear);
                return Color.Lerp(yel, new Color(0.12f, 0.12f, 0.11f), worn * 0.6f);
            }
            float su = Mathf.Repeat(x / 5.8f, 1f) - 0.5f, sv = Mathf.Repeat(y / 5.8f, 1f) - 0.5f;
            float r = Mathf.Sqrt(su * su + sv * sv);
            float stud = r < 0.32f ? 1f : 0f;
            float c = 0.06f + stud * (0.04f + 0.04f * Mathf.Clamp01(0.5f - (su + sv))) ;
            c += Step01(0.5f, 0.78f, wear) * 0.08f;
            return new Color(c, c, c * 1.04f);
        });

        /// <summary>Three stacked pallets (1.6 x 0.9 x 0.144 m each) with a plywood top: top face at 0.45 m.</summary>
        private static void GaragePalletTable(Transform root, Vector3 centre)
        {
            var t = new GameObject("PalletTable").transform;
            t.SetParent(root, false);
            t.position = centre;
            var wood = Mat("GaragePalletWood", new Color(0.78f, 0.64f, 0.47f), Tex("lobby_wood", TexKind.Wood), 0.12f, new Vector2(0.6f, 0.6f));
            var pallet = SaveMesh(PalletMesh().ToMesh("GaragePallet"), "GaragePallet");
            for (int i = 0; i < 3; i++)
            {
                var off = new Vector3((Hash01(i * 5 + 2) - 0.5f) * 0.05f, i * 0.144f, (Hash01(i * 9 + 1) - 0.5f) * 0.04f);
                MeshObj("Pallet", t, pallet, wood, off, Quaternion.Euler(0f, (Hash01(i * 3 + 7) - 0.5f) * 3f, 0f));
            }
            var ply = Mat("GaragePlywood", new Color(0.86f, 0.74f, 0.56f), Tex("lobby_wood", TexKind.Wood), 0.3f, new Vector2(0.5f, 0.5f));
            var top = LocalBox(t, "Plywood", new Vector3(0f, 0.432f + 0.009f, 0f), new Vector3(1.62f, 0.018f, 0.92f), ply);
            top.transform.localRotation = Quaternion.Euler(0f, 1.2f, 0f);
            // a ring stain and a burn mark on the top
            var stain = Mat("GarageMugRing", new Color(0.3f, 0.2f, 0.12f), null, 0.3f, Vector2.one);
            var ring = MeshObj("MugRing", t, LobbyMeshSaved("GarageMugRingMesh", () => LobbyMesh.Torus(0.04f, 0.003f, 24, 4)), stain, new Vector3(0.58f, 0.4505f, 0.28f), null, false);
            ring.transform.localScale = new Vector3(1f, 0.15f, 1f);
            BoxCollider(t, new Vector3(0f, 0.225f, 0f), new Vector3(1.62f, 0.45f, 0.92f));
        }

        /// <summary>One pallet: three bottom boards, nine blocks, three stringer boards, five top boards (UVs in metres).</summary>
        private static ProcMesh PalletMesh()
        {
            const float L = 1.6f, Wd = 0.9f, b = 0.022f, blk = 0.078f;
            var m = new ProcMesh();
            m.At(Vector3.zero);
            foreach (float z in new[] { -0.4f, 0f, 0.4f })
                m.Box(new Vector3(0f, b * 0.5f, z), new Vector3(L, b, 0.1f));
            foreach (float x in new[] { -0.74f, 0f, 0.74f })
            {
                foreach (float z in new[] { -0.4f, 0f, 0.4f })
                    m.Box(new Vector3(x, b + blk * 0.5f, z), new Vector3(0.12f, blk, z == 0f ? 0.1f : 0.1f));
                m.Box(new Vector3(x, b + blk + b * 0.5f, 0f), new Vector3(0.12f, b, Wd));
            }
            float y = b + blk + b + b * 0.5f;
            foreach (float z in new[] { -0.39f, -0.2f, 0f, 0.2f, 0.39f })
                m.Box(new Vector3(0f, y, z), new Vector3(L, b, z == 0f || Mathf.Abs(z) > 0.3f ? 0.12f : 0.09f));
            return m;
        }
    }
}
