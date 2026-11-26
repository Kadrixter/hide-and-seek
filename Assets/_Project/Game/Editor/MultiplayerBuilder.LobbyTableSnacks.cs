using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Snacks on the lounge table (built for the old table centre (-1.5, 0.8), then moved by LobbyLayout.TableShift):
    /// pizza box with a printed lid, a pizza slice (dough, cheese, pepperoni, crust), real soda cans (SodaCan model,
    /// spawned by <see cref="LobbyTableCans"/>), a TV remote with a button grid and a puffy chips bag with spilled chips.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void TableSnacks(Transform p)
        {
            const float top = 0.45f;
            var t = new GameObject("TableSnacks").transform;
            t.SetParent(p, false);

            // ---- pizza box: tray + lid (slightly lifted at the front), printed top, grease spot
            var card = Mat("LobbyCardboard2", new Color(0.8f, 0.64f, 0.43f), Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(3f, 3f));
            var box = new GameObject("PizzaBox").transform;
            box.SetParent(t, false);
            box.localPosition = new Vector3(-1.9f, top, 0.7f);
            box.localRotation = Quaternion.Euler(0f, 12f, 0f);
            GMeshObj("Tray", box, new ProcMesh().At(new Vector3(0f, 0.02f, 0f)).RoundBox(new Vector3(0.42f, 0.04f, 0.42f), 0.004f, 3), "SnackPizzaTray", card, Vector3.zero);
            var lid = new GameObject("LidHinge").transform;
            lid.SetParent(box, false);
            lid.localPosition = new Vector3(0f, 0.04f, 0.212f);
            lid.localRotation = Quaternion.Euler(-3f, 0f, 0f);
            GMeshObj("Lid", lid, new ProcMesh().At(new Vector3(0f, 0.004f, -0.214f)).RoundBox(new Vector3(0.43f, 0.008f, 0.43f), 0.003f, 2), "SnackPizzaLid", card, Vector3.zero);
            GMeshObj("LidLip", lid, new ProcMesh().At(new Vector3(0f, -0.012f, -0.426f)).RoundBox(new Vector3(0.43f, 0.03f, 0.006f), 0.002f, 2), "SnackPizzaLidLip", card, Vector3.zero);
            var print = SmoothTex("lobby_pizza_box2", 256, 256, (x, y) =>
            {
                var c = new Color(0.8f, 0.64f, 0.43f) * (0.94f + 0.06f * Hash01(x * 7 + y * 13));
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(128f, 140f));
                if (d < 70f && d > 62f) return new Color(0.72f, 0.1f, 0.07f);
                if (d < 54f)
                {
                    // chef hat + checker band
                    if (y > 150 && y < 186 && Mathf.Abs(x - 128) < 30 - (y - 150) * 0.2f) return new Color(0.97f, 0.95f, 0.9f);
                    if (y > 120 && y <= 150 && Mathf.Abs(x - 128) < 22) return ((x / 6 + y / 6) % 2 == 0) ? new Color(0.72f, 0.1f, 0.07f) : new Color(0.15f, 0.45f, 0.18f);
                    return new Color(0.95f, 0.88f, 0.62f);
                }
                if (y > 40 && y < 58 && x > 50 && x < 206 && ((x / 7) % 2 == 0 || y % 6 < 3)) return new Color(0.72f, 0.1f, 0.07f);
                if (y > 26 && y < 32 && x > 70 && x < 186) return new Color(0.15f, 0.45f, 0.18f);
                if (Vector2.Distance(new Vector2(x, y), new Vector2(205f, 210f)) < 18f + 6f * Mathf.PerlinNoise(x * 0.1f, y * 0.1f)) return c * 0.72f; // grease
                return c;
            });
            QuadAt("PizzaPrint", lid, Vector3.zero, Quaternion.identity, new Vector2(0.41f, 0.41f), Mat("LobbyPizzaPrint2", Color.white, print, 0.05f, Vector2.one));
            var pq = lid.Find("PizzaPrint");
            pq.localPosition = new Vector3(0f, 0.0085f, -0.214f);
            pq.localRotation = Quaternion.Euler(90f, 0f, 0f);
            box.localPosition = new Vector3(-1.9f, top, 0.7f);
            var bc = box.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.025f, 0f);
            bc.size = new Vector3(0.42f, 0.05f, 0.42f);

            // ---- pizza slice: dough wedge, cheese on top, crust arc, pepperoni, a cheese string
            var slice = new GameObject("PizzaSlice").transform;
            slice.SetParent(t, false);
            slice.localPosition = new Vector3(-1.62f, top, 1.0f);
            slice.localRotation = Quaternion.Euler(0f, 52f, 0f);
            const float R = 0.2f, half = 20f;
            var dough = Mat("LobbyPizzaDough", new Color(0.86f, 0.68f, 0.4f), null, 0.15f, Vector2.one);
            var cheese = Mat("LobbyPizzaCheese", new Color(1f, 0.8f, 0.32f), null, 0.45f, Vector2.one);
            var crust = Mat("LobbyPizzaCrust", new Color(0.72f, 0.45f, 0.2f), null, 0.15f, Vector2.one);
            var pep = Mat("LobbyPizzaPepperoni", new Color(0.62f, 0.12f, 0.08f), null, 0.5f, Vector2.one);
            ProcMesh Wedge(float r, float y0, float y1)
            {
                var m = new ProcMesh();
                m.At(Vector3.zero).Lathe(new[] { new Vector2(0.001f, y0), new Vector2(r, y0), new Vector2(r, y1), new Vector2(0.001f, y1) }, 10, 30f, -half, half);
                foreach (float a in new[] { -half, half })
                    m.At(Vector3.zero, Quaternion.Euler(0f, -a, 0f)).Box(new Vector3(r * 0.5f, (y0 + y1) * 0.5f, 0f), new Vector3(r, y1 - y0, 0.001f));
                return m;
            }
            GMeshObj("Dough", slice, Wedge(R, 0f, 0.008f), "SnackSliceDough", dough, Vector3.zero);
            GMeshObj("Cheese", slice, Wedge(R - 0.012f, 0.008f, 0.013f), "SnackSliceCheese", cheese, Vector3.zero);
            GMeshObj("Crust", slice, new ProcMesh().At(new Vector3(0f, 0.012f, 0f)).Torus(R - 0.004f, 0.013f, 10, 8, -half - 1f, half + 1f), "SnackSliceCrust", crust, Vector3.zero);
            var pepMesh = new ProcMesh();
            foreach (var (pr, pa) in new[] { (0.06f, -4f), (0.12f, 9f), (0.13f, -11f), (0.165f, 2f) })
                pepMesh.At(new Vector3(Mathf.Cos(pa * Mathf.Deg2Rad) * pr, 0.013f, Mathf.Sin(pa * Mathf.Deg2Rad) * pr))
                    .Lathe(new[] { new Vector2(0.019f, 0f), new Vector2(0.019f, 0.002f), new Vector2(0.014f, 0.0035f), new Vector2(0f, 0.004f) }, 14, 50f);
            GMeshObj("Pepperoni", slice, pepMesh, "SnackSlicePepperoni", pep, Vector3.zero);

            // ---- soda cans (one tipped over, one opened)
            var cans = new GameObject("TableCans");
            cans.transform.SetParent(t, false);
            const float canHalf = 0.061f * 1.4f, canR = 0.033f * 1.4f;
            cans.AddComponent<LobbyTableCans>().Set(
                new[] { new Vector3(-1.08f, top + canHalf, 0.58f), new Vector3(-0.98f, top + canR, 0.98f), new Vector3(-2.15f, top + canHalf, 1.12f) },
                new[] { new Vector3(0f, 40f, 0f), new Vector3(0f, 30f, 90f), new Vector3(0f, 200f, 0f) },
                new[] { 0, 5, 1 }, new[] { true, true, false }, 1.4f);
            var spill = Mat("LobbySodaSpill", new Color(0.25f, 0.12f, 0.06f), null, 0.95f, Vector2.one);
            var sp = GMeshObj("Spill", t, new ProcMesh().At(Vector3.zero).Lathe(new[] { new Vector2(0.075f, 0f), new Vector2(0.07f, 0.0008f), new Vector2(0f, 0.0012f) }, 18, 60f), "SnackSpill", spill,
                new Vector3(-0.9f, top, 1.06f), null, false);
            sp.transform.localScale = new Vector3(1.4f, 1f, 0.8f);

            // ---- TV remote: rounded body, IR window, power key, d-pad ring, 3 x 4 number keys, volume rocker
            var remote = new GameObject("Remote").transform;
            remote.SetParent(t, false);
            remote.localPosition = new Vector3(-1.32f, top, 0.6f);
            remote.localRotation = Quaternion.Euler(0f, 28f, 0f);
            var black = Mat("LobbyRemote", new Color(0.05f, 0.05f, 0.06f), null, 0.4f, Vector2.one);
            var grey = Mat("LobbyRemoteKey", new Color(0.32f, 0.33f, 0.35f), null, 0.35f, Vector2.one);
            var red = Mat("LobbyRemoteRed", new Color(0.8f, 0.1f, 0.1f), null, 0.4f, Vector2.one);
            var ir = Mat("LobbyRemoteIr", new Color(0.15f, 0.02f, 0.03f), null, 0.9f, Vector2.one);
            GMeshObj("Body", remote, new ProcMesh().At(new Vector3(0f, 0.011f, 0f)).RoundBox(new Vector3(0.05f, 0.022f, 0.18f), 0.008f, 4), "SnackRemoteBody", black, Vector3.zero);
            GMeshObj("IrWindow", remote, new ProcMesh().At(new Vector3(0f, 0.011f, 0.089f)).RoundBox(new Vector3(0.03f, 0.01f, 0.006f), 0.002f, 2), "SnackRemoteIr", ir, Vector3.zero);
            var key = new ProcMesh().At(Vector3.zero).Lathe(new[] { new Vector2(0.0055f, 0f), new Vector2(0.0055f, 0.002f), new Vector2(0.004f, 0.0032f), new Vector2(0f, 0.0035f) }, 12, 50f);
            var keyMesh = SaveMesh(key.ToMesh("SnackRemoteKey"), "SnackRemoteKey");
            MeshObj("Power", remote, keyMesh, red, new Vector3(0.013f, 0.0215f, 0.07f));
            GMeshObj("DPad", remote, new ProcMesh().At(new Vector3(0f, 0.022f, 0.038f)).Torus(0.011f, 0.0035f, 20, 6), "SnackRemoteDPad", grey, Vector3.zero);
            MeshObj("Ok", remote, keyMesh, grey, new Vector3(0f, 0.0215f, 0.038f));
            for (int r = 0; r < 4; r++)
            for (int c = 0; c < 3; c++)
                MeshObj("Key", remote, keyMesh, grey, new Vector3((c - 1) * 0.014f, 0.0215f, 0.008f - r * 0.016f), null, false).transform.localScale = new Vector3(0.9f, 1f, 0.8f);
            GMeshObj("Rocker", remote, new ProcMesh().At(new Vector3(-0.016f, 0.0225f, -0.062f)).RoundBox(new Vector3(0.01f, 0.004f, 0.03f), 0.0018f, 2), "SnackRemoteRocker", grey, Vector3.zero);

            // ---- chips bag: puffy pillow with crimped seals, open end, chips spilling out
            var bag = new GameObject("ChipsBag").transform;
            bag.SetParent(t, false);
            bag.localPosition = new Vector3(-1.62f, top, 0.42f);
            bag.localRotation = Quaternion.Euler(0f, -70f, 0f);
            var chipsTex = SmoothTex("lobby_chips2", 128, 192, (x, y) =>
            {
                if (y < 14 || y > 176) return new Color(0.78f, 0.78f, 0.8f) * (0.85f + 0.15f * ((x / 3) % 2));
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(64f, 104f));
                if (d < 30f) return new Color(0.98f, 0.82f, 0.25f) * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f)); // chip picture
                if (d < 34f) return new Color(1f, 0.95f, 0.85f);
                if (y > 40 && y < 60 && x > 22 && x < 106) return (x / 5 + y / 5) % 2 == 0 ? new Color(1f, 0.95f, 0.85f) : new Color(0.98f, 0.85f, 0.2f);
                return new Color(0.82f, 0.1f, 0.08f) * (0.92f + 0.08f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f));
            });
            var foil = Mat("LobbyChips2", Color.white, chipsTex, 0.75f, Vector2.one, 0.2f);
            var seal = Mat("LobbyChipsSeal", new Color(0.78f, 0.78f, 0.8f), null, 0.8f, Vector2.one, 0.6f);
            var bagBody = GMeshObj("Bag", bag, new ProcMesh().At(new Vector3(0f, 0.032f, 0f)).RoundBox(new Vector3(0.2f, 0.064f, 0.26f), 0.03f, 6), "SnackChipsBag", foil, Vector3.zero);
            bagBody.transform.localRotation = Quaternion.Euler(3f, 0f, 4f);
            QuadAt("BagPrint", bag, Vector3.zero, Quaternion.identity, new Vector2(0.17f, 0.24f), foil);
            var bp = bag.Find("BagPrint");
            bp.localPosition = new Vector3(0f, 0.0655f, 0f);
            bp.localRotation = Quaternion.Euler(90f, 0f, 4f);
            for (int i = 0; i < 9; i++) // crimp teeth on the closed end
                GMeshObj("Crimp", bag, new ProcMesh().At(Vector3.zero).Box(Vector3.zero, new Vector3(0.022f, 0.004f, 0.03f)), "SnackChipsCrimp", seal,
                    new Vector3(-0.088f + i * 0.022f, 0.018f + (i % 2) * 0.003f, -0.142f), Quaternion.Euler(i % 2 == 0 ? 8f : -8f, 0f, 0f));
            var mouth = GMeshObj("Mouth", bag, new ProcMesh().At(Vector3.zero).Torus(0.06f, 0.006f, 20, 6), "SnackChipsMouth", seal, new Vector3(0f, 0.034f, 0.13f), Quaternion.Euler(90f, 0f, 0f));
            mouth.transform.localScale = new Vector3(1.4f, 1f, 0.45f);
            var chip = Mat("LobbyChip", new Color(0.95f, 0.78f, 0.35f), null, 0.3f, Vector2.one);
            var chipMesh = SaveMesh(new ProcMesh().At(Vector3.zero).Lathe(new[] { new Vector2(0f, 0.003f), new Vector2(0.011f, 0.0012f), new Vector2(0.021f, -0.0035f), new Vector2(0.022f, -0.003f), new Vector2(0.012f, 0.002f), new Vector2(0f, 0.004f) }, 14, 50f).ToMesh("SnackChip"), "SnackChip");
            var rnd = new System.Random(5);
            for (int i = 0; i < 7; i++)
            {
                float a = (float)rnd.NextDouble() * 1.6f - 0.8f, d = 0.16f + (float)rnd.NextDouble() * 0.12f;
                var c = MeshObj("Chip", bag, chipMesh, chip, new Vector3(Mathf.Sin(a) * d, 0.004f + (i == 2 ? 0.012f : 0f), Mathf.Cos(a) * d * 0.9f),
                    Quaternion.Euler((float)rnd.NextDouble() * 24f - 12f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 24f - 12f), false);
                c.transform.localScale = new Vector3(1f, 1f, 0.75f + (float)rnd.NextDouble() * 0.3f);
            }
        }
    }
}
