using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Coffee corner against the south wall (counter with cupboards and a mini fridge; drip coffee maker with its jug,
    /// kettle, microwave, mugs, sugar, coffee tin, paper towels) and the break table (open pizza, chips, cans, napkins),
    /// pizza boxes stacked by the bin.
    /// </summary>
    public partial class SecurityRooms
    {
        private static Texture2D _microTex, _chipsTex, _tinTex;

        private void BuildCoffeeCorner(Transform root, int lv, float y0)
        {
            var white = new Color(0.86f, 0.86f, 0.84f);
            var lam = Surf.T(SecurityTextures.Kind.Laminate, 0.9f, 0.3f);
            float x0 = X0 + 0.75f, x1 = X0 + 3.55f, zBack = Z0 + Wall * 0.5f, depth = 0.6f;
            float cx = (x0 + x1) * 0.5f, cz = zBack + depth * 0.5f, h = 0.88f;
            // Carcass on a dark plinth, worktop with a small overhang.
            Box(root, "CounterPlinth", new Vector3(cx, y0 + 0.07f, cz - 0.03f), new Vector3(x1 - x0 - 0.02f, 0.06f, depth - 0.08f), new Color(0.12f, 0.12f, 0.13f), false);
            Box(root, "Counter", new Vector3(cx, y0 + 0.51f, cz), new Vector3(x1 - x0, 0.82f, depth), white, true, null, Surf.Shiny(0.3f));
            float top = y0 + 0.04f + h + 0.03f;
            Box(root, "Worktop", new Vector3(cx, top - 0.015f, cz + 0.015f), new Vector3(x1 - x0 + 0.04f, 0.03f, depth + 0.03f), new Color(0.55f, 0.42f, 0.3f), true, null, lam);
            Box(root, "WorktopEdge", new Vector3(cx, top - 0.015f, cz + depth * 0.5f + 0.031f), new Vector3(x1 - x0 + 0.04f, 0.026f, 0.003f), new Color(0.2f, 0.16f, 0.12f), false);
            float fz = cz + depth * 0.5f + 0.006f;
            // Mini fridge at the west end, three cupboard doors, two drawers.
            Box(root, "FridgeDoor", new Vector3(x0 + 0.31f, y0 + 0.5f, fz), new Vector3(0.58f, 0.74f, 0.012f), new Color(0.93f, 0.93f, 0.92f), false, null, Surf.Shiny(0.55f));
            Box(root, "FridgeHandle", new Vector3(x0 + 0.56f, y0 + 0.62f, fz + 0.02f), new Vector3(0.02f, 0.28f, 0.02f), new Color(0.7f, 0.7f, 0.72f), false, null, Surf.Shiny(0.7f, 0.9f));
            Box(root, "FridgeGrille", new Vector3(x0 + 0.31f, y0 + 0.155f, fz), new Vector3(0.5f, 0.05f, 0.013f), new Color(0.15f, 0.15f, 0.16f), false);
            for (int d = 0; d < 3; d++)
            {
                float dx = x0 + 0.92f + d * 0.6f;
                Box(root, "CupboardDoor", new Vector3(dx, y0 + 0.44f, fz), new Vector3(0.57f, 0.62f, 0.014f), white * 1.03f, false, null, Surf.Shiny(0.35f));
                Box(root, "CupboardHandle", new Vector3(dx + (d % 2 == 0 ? 0.22f : -0.22f), y0 + 0.66f, fz + 0.018f), new Vector3(0.014f, 0.12f, 0.016f), new Color(0.6f, 0.6f, 0.62f), false, null, Surf.Shiny(0.7f, 0.9f));
            }
            Box(root, "Drawer", new Vector3(x1 - 0.195f, y0 + 0.8f, fz), new Vector3(0.37f, 0.12f, 0.014f), white * 1.03f, false, null, Surf.Shiny(0.35f));
            Box(root, "DrawerHandle", new Vector3(x1 - 0.195f, y0 + 0.8f, fz + 0.016f), new Vector3(0.16f, 0.014f, 0.014f), new Color(0.6f, 0.6f, 0.62f), false, null, Surf.Shiny(0.7f, 0.9f));
            Box(root, "CupboardSide", new Vector3(x1 - 0.195f, y0 + 0.42f, fz), new Vector3(0.37f, 0.58f, 0.014f), white * 1.03f, false, null, Surf.Shiny(0.35f));

            BuildCoffeeMaker(root, new Vector3(x0 + 0.3f, top, zBack + 0.22f));
            BuildKettle(root, new Vector3(x0 + 0.78f, top, zBack + 0.28f), lv);
            BuildMicrowave(root, new Vector3(x0 + 1.55f, top, zBack + 0.24f));
            // Mugs upside down on a tea towel, one in use, sugar jar, coffee tin.
            Box(root, "TeaTowel", new Vector3(x0 + 2.25f, top + 0.002f, zBack + 0.34f), new Vector3(0.34f, 0.004f, 0.24f), new Color(0.75f, 0.2f, 0.18f), false, new Vector3(0f, 6f, 0f), Surf.T(SecurityTextures.Kind.Fabric, 0.15f));
            Color[] mugs = { new(0.92f, 0.92f, 0.9f), new(0.15f, 0.3f, 0.6f), new(0.95f, 0.75f, 0.2f) };
            for (int i = 0; i < 3; i++)
            {
                Vector3 mp = new Vector3(x0 + 2.15f + (i % 2) * 0.11f, top + 0.004f, zBack + 0.3f + (i / 2) * 0.1f);
                if (i == 2) MugModel(root, mp + new Vector3(0.12f, 0f, -0.02f), mugs[i], new Color(0.2f, 0.2f, 0.2f), 200f + lv * 30f);
                else
                {
                    // Upside down: the same mug turned over.
                    var g = new GameObject("MugUpsideDown").transform;
                    g.SetParent(root, false);
                    g.SetPositionAndRotation(mp + Vector3.up * 0.098f, Quaternion.Euler(180f, i * 70f, 0f));
                    MugModel(g, g.position, mugs[i], mugs[(i + 1) % 3], i * 70f, false);
                    foreach (Transform t in g) t.rotation = g.rotation * Quaternion.Euler(0f, i * 70f, 0f);
                }
            }
            var glass = new Color(0.75f, 0.8f, 0.82f);
            Vector3 jar = new Vector3(x0 + 2.55f, top, zBack + 0.16f);
            MeshObj(root, "SugarJar", CachedMesh("Jar", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.045f, 0f), new Vector2(0.048f, 0.01f), new Vector2(0.048f, 0.12f), new Vector2(0.04f, 0.13f), new Vector2(0f, 0.13f) }, 18, 40f)),
                jar, Quaternion.identity, glass, Surf.Shiny(0.9f));
            MeshObj(root, "Sugar", CachedMesh("JarFill", p => p.Lathe(new[] { new Vector2(0f, 0.004f), new Vector2(0.044f, 0.004f), new Vector2(0.044f, 0.07f), new Vector2(0f, 0.075f) }, 18, 40f)),
                jar, Quaternion.identity, new Color(0.95f, 0.94f, 0.9f), Surf.Shiny(0.2f), false, false);
            MeshObj(root, "JarLid", CachedMesh("JarLid", p => p.Lathe(new[] { new Vector2(0.042f, 0.125f), new Vector2(0.046f, 0.13f), new Vector2(0.046f, 0.142f), new Vector2(0f, 0.145f) }, 18, 40f)),
                jar, Quaternion.identity, new Color(0.15f, 0.15f, 0.16f), Surf.Shiny(0.4f, 0.4f), false, false);
            if (_tinTex == null) _tinTex = MakeTinTexture();
            Vector3 tin = new Vector3(x0 + 2.72f, top, zBack + 0.18f);
            MeshObj(root, "CoffeeTin", CachedMesh("Tin", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.055f, 0f), new Vector2(0.056f, 0.004f), new Vector2(0.056f, 0.16f), new Vector2(0.05f, 0.165f), new Vector2(0f, 0.166f) }, 22, 40f)),
                tin, Quaternion.Euler(0f, 200f, 0f), new Color(0.65f, 0.08f, 0.06f), Surf.Shiny(0.6f, 0.5f));
            var label = Sign(root, "TinLabel", tin + new Vector3(0f, 0.08f, 0.0575f), new Vector2(0.085f, 0.09f), Vector3.forward, _tinTex, 0.3f);
            label.transform.localScale = new Vector3(0.085f, 0.09f, 1f);

            BuildBreakTable(root, lv, new Vector3(X0 + 2.6f, y0 + 0.04f, Z0 + 2.15f));
            // Two empty pizza boxes on the floor by the bin end of the counter.
            Vector3 stack = new Vector3(x1 + 0.3f, y0 + 0.04f, zBack + 0.32f);
            PizzaBox(root, stack, 8f + lv * 13f, false);
            PizzaBox(root, stack + new Vector3(0.02f, 0.045f, -0.01f), -6f + lv * 9f, false);
        }

        private void BuildCoffeeMaker(Transform root, Vector3 at)
        {
            var body = new Color(0.08f, 0.08f, 0.09f);
            Box(root, "CoffeeMakerBase", at + new Vector3(0f, 0.025f, 0.02f), new Vector3(0.2f, 0.05f, 0.24f), body, false, null, Surf.Shiny(0.4f));
            Box(root, "CoffeeMakerTower", at + new Vector3(0f, 0.19f, -0.06f), new Vector3(0.2f, 0.33f, 0.09f), body, false, null, Surf.Shiny(0.4f));
            Box(root, "CoffeeMakerHead", at + new Vector3(0f, 0.36f, 0.02f), new Vector3(0.2f, 0.07f, 0.24f), body, false, null, Surf.Shiny(0.4f));
            Box(root, "FilterHolder", at + new Vector3(0f, 0.31f, 0.04f), new Vector3(0.14f, 0.04f, 0.13f), new Color(0.12f, 0.12f, 0.13f), false, null, Surf.Shiny(0.35f));
            Led(root, "CoffeeLed", at + new Vector3(0.06f, 0.035f, 0.141f), new Vector3(0.012f, 0.008f, 0.004f), new Color(2f, 0.25f, 0.1f));
            // Glass jug, a third full, on the warming plate.
            Vector3 jug = at + new Vector3(0f, 0.05f, 0.045f);
            MeshObj(root, "Jug", CachedMesh("JugGlass", p => p
                .Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.058f, 0f), new Vector2(0.065f, 0.05f), new Vector2(0.06f, 0.12f), new Vector2(0.048f, 0.17f), new Vector2(0.05f, 0.18f) }, 22, 45f)
                .At(new Vector3(-0.075f, 0.1f, 0f), Quaternion.Euler(90f, 0f, 0f)).Torus(0.035f, 0.008f, 12, 6, 80f, 280f)),
                jug, Quaternion.Euler(0f, 90f, 0f), new Color(0.3f, 0.32f, 0.33f), Surf.Shiny(0.95f));
            MeshObj(root, "JugCoffee", CachedMesh("JugCoffee", p => p.Lathe(new[] { new Vector2(0f, 0.004f), new Vector2(0.056f, 0.004f), new Vector2(0.062f, 0.045f), new Vector2(0f, 0.046f) }, 22, 45f)),
                jug, Quaternion.identity, Coffee, Surf.Shiny(0.9f), false, false);
            MeshObj(root, "JugCollar", CachedMesh("JugCollar", p => p.Lathe(new[] { new Vector2(0.051f, 0.15f), new Vector2(0.053f, 0.185f) }, 22)), jug, Quaternion.identity, body, Surf.Shiny(0.4f), false, false);
        }

        private void BuildKettle(Transform root, Vector3 at, int lv)
        {
            var body = lv == 1 ? new Color(0.75f, 0.76f, 0.78f) : new Color(0.9f, 0.9f, 0.88f);
            MeshObj(root, "KettleBase", CachedMesh("KettleBase", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.085f, 0f), new Vector2(0.088f, 0.012f), new Vector2(0.08f, 0.02f), new Vector2(0f, 0.02f) }, 22, 40f)),
                at, Quaternion.identity, new Color(0.12f, 0.12f, 0.13f), Surf.Shiny(0.4f));
            Vector3 k = at + Vector3.up * 0.02f;
            MeshObj(root, "Kettle", CachedMesh("Kettle", p => p
                .Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.075f, 0f), new Vector2(0.082f, 0.03f), new Vector2(0.078f, 0.15f), new Vector2(0.062f, 0.2f), new Vector2(0.04f, 0.215f), new Vector2(0f, 0.218f) }, 24, 40f)
                .Tube(new[] { new Vector3(-0.06f, 0.2f, 0f), new Vector3(-0.1f, 0.21f, 0f), new Vector3(-0.125f, 0.17f, 0f), new Vector3(-0.115f, 0.06f, 0f), new Vector3(-0.078f, 0.04f, 0f) }, 0.014f, 8)
                .Tube(new[] { new Vector3(0.07f, 0.12f, 0f), new Vector3(0.1f, 0.15f, 0f), new Vector3(0.115f, 0.17f, 0f) }, 0.016f, 8)),
                k, Quaternion.Euler(0f, 160f + lv * 25f, 0f), body, Surf.Shiny(lv == 1 ? 0.8f : 0.55f, lv == 1 ? 0.9f : 0f));
            Led(root, "KettleLed", k + Quaternion.Euler(0f, 160f + lv * 25f, 0f) * new Vector3(-0.11f, 0.19f, 0f), new Vector3(0.01f, 0.006f, 0.01f), new Color(0.2f, 0.5f, 2f));
        }

        private void BuildMicrowave(Transform root, Vector3 at)
        {
            Box(root, "Microwave", at + new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.3f, 0.37f), new Color(0.82f, 0.82f, 0.8f), true, null, Surf.Shiny(0.4f, 0.3f));
            if (_microTex == null) _microTex = MakeMicrowaveTexture();
            Sign(root, "MicrowaveFront", at + new Vector3(0f, 0.15f, 0.188f), new Vector2(0.49f, 0.29f), Vector3.forward, _microTex, 0.6f);
            Box(root, "MicrowaveHandle", at + new Vector3(0.095f, 0.15f, 0.2f), new Vector3(0.018f, 0.2f, 0.02f), new Color(0.3f, 0.3f, 0.32f), false, null, Surf.Shiny(0.6f, 0.6f));
            for (int i = 0; i < 4; i++)
                Box(root, "MicrowaveFoot", at + new Vector3(i % 2 == 0 ? -0.21f : 0.21f, 0.005f, i < 2 ? -0.14f : 0.14f), new Vector3(0.03f, 0.01f, 0.03f), Color.black, false);
            // Paper towels standing on top.
            Vector3 roll = at + new Vector3(-0.1f, 0.3f, -0.02f);
            MeshObj(root, "PaperTowel", CachedMesh("TowelRoll", p => p.Lathe(new[] { new Vector2(0.02f, 0.22f), new Vector2(0.02f, 0.0f), new Vector2(0.055f, 0f), new Vector2(0.056f, 0.11f), new Vector2(0.055f, 0.22f), new Vector2(0.02f, 0.22f) }, 20, 30f)),
                roll, Quaternion.identity, new Color(0.95f, 0.95f, 0.93f), Surf.Shiny(0.05f));
        }

        private void BuildBreakTable(Transform root, int lv, Vector3 floor)
        {
            var legCol = new Color(0.2f, 0.2f, 0.22f);
            float top = floor.y + 0.74f;
            Box(root, "TableTop", new Vector3(floor.x, top - 0.015f, floor.z), new Vector3(0.85f, 0.03f, 0.85f), new Color(0.78f, 0.76f, 0.7f), true, null, Surf.T(SecurityTextures.Kind.Laminate, 0.8f, 0.3f));
            Box(root, "TableEdge", new Vector3(floor.x, top - 0.015f, floor.z), new Vector3(0.856f, 0.024f, 0.856f), new Color(0.15f, 0.15f, 0.16f), false);
            var leg = CachedMesh("TableLeg", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.018f, 0f), new Vector2(0.016f, 0.012f), new Vector2(0.016f, 0.71f), new Vector2(0f, 0.71f) }, 10, 40f));
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                MeshObj(root, "TableLeg", leg, floor + new Vector3(sx * 0.37f, 0f, sz * 0.37f), Quaternion.identity, legCol, Surf.Shiny(0.6f, 0.8f));
            Box(root, "TableApron", new Vector3(floor.x, top - 0.06f, floor.z), new Vector3(0.76f, 0.06f, 0.76f), legCol, false, null, Surf.Shiny(0.5f, 0.7f));

            Vector3 t = new Vector3(floor.x, top, floor.z);
            Vector3 inside = PizzaBox(root, t + new Vector3(-0.12f, 0f, 0.1f), -20f + lv * 15f, true);
            PizzaModel(root, inside, 30f + lv * 40f, 5 - lv);
            CanModel(root, t + new Vector3(0.25f, 0f, -0.18f), new Color(0.12f, 0.5f, 0.18f), 30f);
            CanModel(root, t + new Vector3(0.2f, 0f, -0.3f), new Color(0.75f, 0.1f, 0.08f), 110f, true);
            // Chips bag (crumpled pillow), a few chips spilt, a pile of napkins.
            if (_chipsTex == null) _chipsTex = MakeChipsTexture();
            Vector3 bag = t + new Vector3(0.22f, 0.028f, 0.18f);
            var bagGo = MeshObj(root, "ChipsBag", CachedMesh("ChipsBag", p => p.At(Vector3.zero, Quaternion.identity, new Vector3(1f, 0.32f, 1f)).RoundBox(new Vector3(0.17f, 0.17f, 0.24f), 0.07f, 5)),
                bag, Quaternion.Euler(0f, 35f, 4f), new Color(0.95f, 0.75f, 0.15f), Surf.Shiny(0.75f, 0.4f));
            var print = Sign(root, "ChipsPrint", bag + Vector3.up * 0.0305f, new Vector2(0.13f, 0.19f), Vector3.up, _chipsTex, 0.7f);
            print.transform.rotation = Quaternion.Euler(0f, 35f, 0f) * print.transform.rotation;
            print.transform.SetParent(bagGo.transform, true);
            for (int i = 0; i < 6; i++)
                Prim(root, PrimitiveType.Sphere, "Chip", t + new Vector3(0.12f + i * 0.025f, 0.003f, 0.05f + (i % 3) * 0.03f), new Vector3(0.035f, 0.004f, 0.028f), new Color(0.95f, 0.78f, 0.35f), new Vector3(0f, i * 47f, i * 9f));
            for (int i = 0; i < 4; i++)
                Box(root, "Napkin", t + new Vector3(-0.28f, 0.001f + i * 0.0015f, -0.25f), new Vector3(0.14f, 0.0012f, 0.14f), new Color(0.95f, 0.95f, 0.95f), false, new Vector3(0f, i * 11f, 0f));
        }

        private static Texture2D MakeMicrowaveTexture()
        {
            var tb = new TextureBaker(392, 232, new Color(0.8f, 0.8f, 0.78f));
            tb.Rect(0.36f, 0.5f, 0.66f, 0.84f, new Color(0.05f, 0.05f, 0.06f));
            tb.Rect(0.36f, 0.5f, 0.6f, 0.74f, new Color(0.1f, 0.11f, 0.11f));
            tb.Rect(0.84f, 0.82f, 0.2f, 0.12f, new Color(0.05f, 0.12f, 0.08f));
            tb.Text("12:47", 0.84f, 0.82f, 0.08f, new Color(0.3f, 1f, 0.5f));
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 3; c++)
                    tb.Rect(0.78f + c * 0.06f, 0.6f - r * 0.1f, 0.045f, 0.07f, new Color(0.6f, 0.6f, 0.6f));
            tb.Circle(0.84f, 0.15f, 0.05f, new Color(0.3f, 0.3f, 0.32f));
            return tb.Finish("SecMicrowave", 0.2f, 22);
        }

        private static Texture2D MakeChipsTexture()
        {
            var tb = new TextureBaker(160, 232, new Color(0.95f, 0.75f, 0.15f));
            tb.Rect(0.5f, 0.85f, 1f, 0.18f, new Color(0.8f, 0.1f, 0.08f));
            tb.Text("CHRUPKI", 0.5f, 0.62f, 0.13f, new Color(0.75f, 0.08f, 0.06f), TextAnchor.MiddleCenter, true, 0.95f);
            tb.Text("PAPRYKA", 0.5f, 0.47f, 0.08f, new Color(0.2f, 0.2f, 0.2f));
            tb.Circle(0.5f, 0.22f, 0.22f, new Color(0.97f, 0.85f, 0.45f));
            return tb.Finish("SecChips", 0.15f, 23);
        }

        private static Texture2D MakeTinTexture()
        {
            var tb = new TextureBaker(128, 136, new Color(0.65f, 0.08f, 0.06f));
            tb.Rect(0.5f, 0.55f, 1f, 0.5f, new Color(0.92f, 0.85f, 0.65f));
            tb.Text("KAWA", 0.5f, 0.6f, 0.16f, new Color(0.35f, 0.18f, 0.08f), TextAnchor.MiddleCenter, true, 0.9f);
            tb.Text("MIELONA", 0.5f, 0.42f, 0.08f, new Color(0.35f, 0.18f, 0.08f));
            return tb.Finish("SecTin", 0.25f, 24);
        }
    }
}
