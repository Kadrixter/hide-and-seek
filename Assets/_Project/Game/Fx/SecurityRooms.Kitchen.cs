using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Derelict coffee corner against the south wall: counter with a mini fridge, one cupboard hanging wide open (empty,
    /// dark), another ajar; a dead drip coffee maker with an empty stained jug, a dead kettle, a grimy microwave with its
    /// door ajar, mugs left upside down on a filthy tea towel, an almost used up paper-towel roll. The break table holds
    /// only crushed cans, an old newspaper from the week the room was left and coffee rings. No food anywhere.
    /// </summary>
    public partial class SecurityRooms
    {
        private static Texture2D _microTex, _newsTex;

        private void BuildCoffeeCorner(Transform root, int lv, float y0)
        {
            var white = new Color(0.74f, 0.73f, 0.68f);      // yellowed, grimy laminate
            var lam = Surf.T(SecurityTextures.Kind.Laminate, 0.9f, 0.3f);
            float x0 = X0 + 0.75f, x1 = X0 + 3.55f, zBack = Z0 + Wall * 0.5f, depth = 0.6f;
            float cx = (x0 + x1) * 0.5f, cz = zBack + depth * 0.5f, h = 0.88f;
            // Carcass on a dark plinth, worktop with a small overhang.
            Box(root, "CounterPlinth", new Vector3(cx, y0 + 0.07f, cz - 0.03f), new Vector3(x1 - x0 - 0.02f, 0.06f, depth - 0.08f), new Color(0.1f, 0.1f, 0.1f), false);
            Box(root, "Counter", new Vector3(cx, y0 + 0.51f, cz), new Vector3(x1 - x0, 0.82f, depth), white, true, null, Surf.Shiny(0.2f));
            float top = y0 + 0.04f + h + 0.03f;
            Box(root, "Worktop", new Vector3(cx, top - 0.015f, cz + 0.015f), new Vector3(x1 - x0 + 0.04f, 0.03f, depth + 0.03f), new Color(0.46f, 0.36f, 0.27f), true, null, lam);
            Box(root, "WorktopEdge", new Vector3(cx, top - 0.015f, cz + depth * 0.5f + 0.031f), new Vector3(x1 - x0 + 0.04f, 0.026f, 0.003f), new Color(0.2f, 0.16f, 0.12f), false);
            float fz = cz + depth * 0.5f + 0.006f;
            // Mini fridge at the west end (unplugged long ago, yellowed), cupboard doors, a drawer.
            Box(root, "FridgeDoor", new Vector3(x0 + 0.31f, y0 + 0.5f, fz), new Vector3(0.58f, 0.74f, 0.012f), new Color(0.8f, 0.78f, 0.7f), false, null, Surf.Shiny(0.3f));
            Box(root, "FridgeHandle", new Vector3(x0 + 0.56f, y0 + 0.62f, fz + 0.02f), new Vector3(0.02f, 0.28f, 0.02f), new Color(0.55f, 0.55f, 0.55f), false, null, Surf.Shiny(0.45f, 0.7f));
            Box(root, "FridgeGrille", new Vector3(x0 + 0.31f, y0 + 0.155f, fz), new Vector3(0.5f, 0.05f, 0.013f), new Color(0.12f, 0.12f, 0.12f), false);
            var doorCol = white * 1.03f;
            var handleCol = new Color(0.5f, 0.5f, 0.5f);
            for (int d = 0; d < 3; d++)
            {
                float dx = x0 + 0.92f + d * 0.6f;
                bool hingeRight = d % 2 == 1;                      // the handle is on the other side
                float openDeg = d == 1 ? 100f : d == 2 ? 18f : 0f;
                if (openDeg > 0f)
                {
                    // Dark empty inside behind the open door, a bare shelf edge.
                    Box(root, "CupboardInside", new Vector3(dx, y0 + 0.44f, fz - 0.002f), new Vector3(0.55f, 0.6f, 0.003f), new Color(0.035f, 0.033f, 0.03f), false);
                    Box(root, "CupboardShelf", new Vector3(dx, y0 + 0.46f, fz - 0.0005f), new Vector3(0.53f, 0.016f, 0.003f), new Color(0.2f, 0.19f, 0.17f), false);
                }
                // Door turned about its hinge edge, swinging out into the room (+z).
                float side = hingeRight ? 1f : -1f;
                Vector3 hinge = new Vector3(dx + side * 0.285f, y0 + 0.44f, fz + 0.007f);
                var q = Quaternion.Euler(0f, side * openDeg, 0f);
                Box(root, "CupboardDoor", hinge + q * new Vector3(-side * 0.285f, 0f, 0f), new Vector3(0.57f, 0.62f, 0.014f), doorCol, false, q.eulerAngles, Surf.Shiny(0.25f));
                Box(root, "CupboardHandle", hinge + q * new Vector3(-side * 0.505f, 0.22f, 0.018f), new Vector3(0.014f, 0.12f, 0.016f), handleCol, false, q.eulerAngles, Surf.Shiny(0.45f, 0.7f));
            }
            Box(root, "Drawer", new Vector3(x1 - 0.195f, y0 + 0.8f, fz), new Vector3(0.37f, 0.12f, 0.014f), doorCol, false, null, Surf.Shiny(0.25f));
            Box(root, "DrawerHandle", new Vector3(x1 - 0.195f, y0 + 0.8f, fz + 0.016f), new Vector3(0.16f, 0.014f, 0.014f), handleCol, false, null, Surf.Shiny(0.45f, 0.7f));
            Box(root, "CupboardSide", new Vector3(x1 - 0.195f, y0 + 0.42f, fz), new Vector3(0.37f, 0.58f, 0.014f), doorCol, false, null, Surf.Shiny(0.25f));

            BuildCoffeeMaker(root, new Vector3(x0 + 0.3f, top, zBack + 0.22f));
            BuildKettle(root, new Vector3(x0 + 0.78f, top, zBack + 0.28f), lv);
            BuildMicrowave(root, new Vector3(x0 + 1.55f, top, zBack + 0.24f));
            // Two mugs left upside down on a filthy tea towel, a crushed can.
            Box(root, "TeaTowel", new Vector3(x0 + 2.25f, top + 0.002f, zBack + 0.34f), new Vector3(0.34f, 0.004f, 0.24f), new Color(0.38f, 0.22f, 0.18f), false, new Vector3(0f, 6f, 0f), Surf.T(SecurityTextures.Kind.Fabric, 0.15f));
            Color[] mugs = { new(0.92f, 0.92f, 0.9f), new(0.15f, 0.3f, 0.6f), new(0.95f, 0.75f, 0.2f) };
            for (int i = 0; i < 2; i++)
            {
                var g = new GameObject("MugUpsideDown").transform;
                g.SetParent(root, false);
                MugModel(g, Vector3.zero, mugs[i], mugs[(i + 1) % 3], 0f, false);
                g.SetPositionAndRotation(new Vector3(x0 + 2.15f + i * 0.11f, top + 0.004f + 0.098f, zBack + 0.3f), Quaternion.Euler(0f, i * 70f, 0f) * Quaternion.Euler(180f, 0f, 0f));
            }
            CrushedCan(root, new Vector3(x0 + 2.62f, top, zBack + 0.18f), 30f + lv * 50f, Crush.Stomped, 0, lv + 2);

            BuildBreakTable(root, lv, new Vector3(X0 + 2.6f, y0 + 0.04f, Z0 + 2.15f));
        }

        private void BuildCoffeeMaker(Transform root, Vector3 at)
        {
            var body = new Color(0.08f, 0.08f, 0.09f);
            Box(root, "CoffeeMakerBase", at + new Vector3(0f, 0.025f, 0.02f), new Vector3(0.2f, 0.05f, 0.24f), body, false, null, Surf.Shiny(0.25f));
            Box(root, "CoffeeMakerTower", at + new Vector3(0f, 0.19f, -0.06f), new Vector3(0.2f, 0.33f, 0.09f), body, false, null, Surf.Shiny(0.25f));
            Box(root, "CoffeeMakerHead", at + new Vector3(0f, 0.36f, 0.02f), new Vector3(0.2f, 0.07f, 0.24f), body, false, null, Surf.Shiny(0.25f));
            Box(root, "FilterHolder", at + new Vector3(0f, 0.31f, 0.04f), new Vector3(0.14f, 0.04f, 0.13f), new Color(0.12f, 0.12f, 0.13f), false, null, Surf.Shiny(0.2f));
            // Glass jug, empty, brown dried crust on the bottom, on the cold warming plate.
            Vector3 jug = at + new Vector3(0f, 0.05f, 0.045f);
            MeshObj(root, "Jug", CachedMesh("JugGlass", p => p
                .Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.058f, 0f), new Vector2(0.065f, 0.05f), new Vector2(0.06f, 0.12f), new Vector2(0.048f, 0.17f), new Vector2(0.05f, 0.18f) }, 22, 45f)
                .At(new Vector3(-0.075f, 0.1f, 0f), Quaternion.Euler(90f, 0f, 0f)).Torus(0.035f, 0.008f, 12, 6, 80f, 280f)),
                jug, Quaternion.Euler(0f, 90f, 0f), new Color(0.3f, 0.28f, 0.25f), Surf.Shiny(0.6f));
            MeshObj(root, "JugCrust", CachedMesh("JugCrust", p => p.Lathe(new[] { new Vector2(0f, 0.004f), new Vector2(0.056f, 0.004f), new Vector2(0.058f, 0.008f), new Vector2(0f, 0.0085f) }, 22, 45f)),
                jug, Quaternion.identity, DriedCoffee, Surf.Shiny(0.2f), false, false);
            MeshObj(root, "JugCollar", CachedMesh("JugCollar", p => p.Lathe(new[] { new Vector2(0.051f, 0.15f), new Vector2(0.053f, 0.185f) }, 22)), jug, Quaternion.identity, body, Surf.Shiny(0.3f), false, false);
        }

        private void BuildKettle(Transform root, Vector3 at, int lv)
        {
            var body = lv == 1 ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.72f, 0.7f, 0.64f);
            MeshObj(root, "KettleBase", CachedMesh("KettleBase", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.085f, 0f), new Vector2(0.088f, 0.012f), new Vector2(0.08f, 0.02f), new Vector2(0f, 0.02f) }, 22, 40f)),
                at, Quaternion.identity, new Color(0.12f, 0.12f, 0.13f), Surf.Shiny(0.25f));
            Vector3 k = at + Vector3.up * 0.02f;
            MeshObj(root, "Kettle", CachedMesh("Kettle", p => p
                .Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.075f, 0f), new Vector2(0.082f, 0.03f), new Vector2(0.078f, 0.15f), new Vector2(0.062f, 0.2f), new Vector2(0.04f, 0.215f), new Vector2(0f, 0.218f) }, 24, 40f)
                .Tube(new[] { new Vector3(-0.06f, 0.2f, 0f), new Vector3(-0.1f, 0.21f, 0f), new Vector3(-0.125f, 0.17f, 0f), new Vector3(-0.115f, 0.06f, 0f), new Vector3(-0.078f, 0.04f, 0f) }, 0.014f, 8)
                .Tube(new[] { new Vector3(0.07f, 0.12f, 0f), new Vector3(0.1f, 0.15f, 0f), new Vector3(0.115f, 0.17f, 0f) }, 0.016f, 8)),
                k, Quaternion.Euler(0f, 160f + lv * 25f, 0f), body, Surf.Shiny(lv == 1 ? 0.45f : 0.3f, lv == 1 ? 0.6f : 0f));
        }

        /// <summary>Grimy microwave, door hanging ajar on its left hinge, dead control panel, dark dirty inside.</summary>
        private void BuildMicrowave(Transform root, Vector3 at)
        {
            var shell = new Color(0.62f, 0.6f, 0.55f);
            Box(root, "Microwave", at + new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.3f, 0.37f), shell, true, null, Surf.Shiny(0.2f, 0.2f));
            if (_microTex == null) _microTex = MakeMicrowaveTexture();
            float front = at.z + 0.185f;
            // Control panel on the right (the clock is dead), the cavity behind the open door.
            Sign(root, "MicrowavePanel", new Vector3(at.x + 0.165f, at.y + 0.15f, front + 0.003f), new Vector2(0.16f, 0.28f), Vector3.forward, _microTex, 0.3f);
            Box(root, "MicrowaveCavity", new Vector3(at.x - 0.08f, at.y + 0.155f, front + 0.0015f), new Vector3(0.3f, 0.24f, 0.002f), new Color(0.07f, 0.055f, 0.04f), false);
            // Door: hinge on the left edge, swung ~40 degrees open; smoky window on its front.
            Vector3 hinge = new Vector3(at.x - 0.25f, at.y + 0.15f, front + 0.015f);
            var q = Quaternion.Euler(0f, -40f, 0f);
            Box(root, "MicrowaveDoor", hinge + q * new Vector3(0.165f, 0f, 0f), new Vector3(0.33f, 0.29f, 0.025f), shell * 1.02f, false, q.eulerAngles, Surf.Shiny(0.25f, 0.2f));
            Box(root, "MicrowaveWindow", hinge + q * new Vector3(0.155f, 0.005f, 0.0135f), new Vector3(0.24f, 0.18f, 0.002f), new Color(0.08f, 0.08f, 0.07f), false, q.eulerAngles, Surf.Shiny(0.7f));
            Box(root, "MicrowaveHandle", hinge + q * new Vector3(0.3f, 0f, 0.025f), new Vector3(0.018f, 0.2f, 0.02f), new Color(0.3f, 0.3f, 0.32f), false, q.eulerAngles, Surf.Shiny(0.4f, 0.4f));
            for (int i = 0; i < 4; i++)
                Box(root, "MicrowaveFoot", at + new Vector3(i % 2 == 0 ? -0.21f : 0.21f, 0.005f, i < 2 ? -0.14f : 0.14f), new Vector3(0.03f, 0.01f, 0.03f), Color.black, false);
            // An almost used up paper-towel roll standing on top.
            Vector3 roll = at + new Vector3(-0.1f, 0.3f, -0.02f);
            MeshObj(root, "PaperTowel", CachedMesh("TowelRollOld", p => p.Lathe(new[] { new Vector2(0.02f, 0.22f), new Vector2(0.02f, 0.0f), new Vector2(0.027f, 0f), new Vector2(0.027f, 0.11f), new Vector2(0.027f, 0.22f), new Vector2(0.02f, 0.22f) }, 20, 30f)),
                roll, Quaternion.identity, new Color(0.72f, 0.68f, 0.6f), Surf.Shiny(0.05f));
        }

        private void BuildBreakTable(Transform root, int lv, Vector3 floor)
        {
            var legCol = new Color(0.2f, 0.2f, 0.22f);
            float top = floor.y + 0.74f;
            Box(root, "TableTop", new Vector3(floor.x, top - 0.015f, floor.z), new Vector3(0.85f, 0.03f, 0.85f), new Color(0.62f, 0.6f, 0.54f), true, null, Surf.T(SecurityTextures.Kind.Laminate, 0.8f, 0.2f));
            Box(root, "TableEdge", new Vector3(floor.x, top - 0.015f, floor.z), new Vector3(0.856f, 0.024f, 0.856f), new Color(0.15f, 0.15f, 0.16f), false);
            var leg = CachedMesh("TableLeg", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.018f, 0f), new Vector2(0.016f, 0.012f), new Vector2(0.016f, 0.71f), new Vector2(0f, 0.71f) }, 10, 40f));
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                MeshObj(root, "TableLeg", leg, floor + new Vector3(sx * 0.37f, 0f, sz * 0.37f), Quaternion.identity, legCol, Surf.Shiny(0.35f, 0.6f));
            Box(root, "TableApron", new Vector3(floor.x, top - 0.06f, floor.z), new Vector3(0.76f, 0.06f, 0.76f), legCol, false, null, Surf.Shiny(0.3f, 0.5f));

            Vector3 t = new Vector3(floor.x, top, floor.z);
            // Old newspaper (the week the room was left), crushed cans, a crumpled sheet.
            if (_newsTex == null) _newsTex = MakeNewspaperTexture();
            var news = Sign(root, "Newspaper", t + new Vector3(-0.1f, 0.0015f, 0.08f), new Vector2(0.3f, 0.4f), Vector3.up, _newsTex, 0.05f);
            news.transform.rotation = Quaternion.Euler(0f, -18f + lv * 21f, 0f) * news.transform.rotation;
            CrushedCan(root, t + new Vector3(0.25f, 0f, -0.18f), 30f + lv * 40f, Crush.Squeezed, 2, lv);
            CrushedCan(root, t + new Vector3(0.18f, 0f, -0.3f), 110f, Crush.Flattened, 0, lv);
            TrashBinProp.CreateLitter(root, t + new Vector3(0.24f, 0f, 0.2f), 0f, 73 + lv, 2, 0.06f, false);
        }

        /// <summary>Microwave control panel: dead display, keypad, dial.</summary>
        private static Texture2D MakeMicrowaveTexture()
        {
            var tb = new TextureBaker(96, 168, new Color(0.55f, 0.54f, 0.5f));
            tb.Rect(0.5f, 0.86f, 0.8f, 0.1f, new Color(0.04f, 0.05f, 0.045f));
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 3; c++)
                    tb.Rect(0.22f + c * 0.28f, 0.66f - r * 0.1f, 0.2f, 0.07f, new Color(0.42f, 0.41f, 0.38f));
            tb.Circle(0.5f, 0.17f, 0.07f, new Color(0.25f, 0.25f, 0.26f));
            return tb.Finish("SecMicrowave", 0.6f, 22);
        }

        /// <summary>Yellowed local paper from November 2009 (the room was left that week).</summary>
        private static Texture2D MakeNewspaperTexture()
        {
            var ink = new Color(0.15f, 0.14f, 0.13f);
            var tb = new TextureBaker(240, 320, new Color(0.8f, 0.77f, 0.66f));
            tb.Text("KURIER MIEJSKI", 0.5f, 0.93f, 0.055f, ink, TextAnchor.MiddleCenter, true, 0.92f);
            tb.Rect(0.5f, 0.885f, 0.92f, 0.006f, ink);
            tb.Text("piątek, 13 listopada 2009", 0.5f, 0.865f, 0.022f, ink, TextAnchor.MiddleCenter, false, 0.6f);
            tb.Text("ZAGINĄŁ STRÓŻ\nZ PODZIEMNEGO GARAŻU", 0.5f, 0.77f, 0.04f, ink, TextAnchor.MiddleCenter, true, 0.92f);
            tb.Rect(0.3f, 0.57f, 0.48f, 0.22f, new Color(0.42f, 0.41f, 0.38f));
            for (int c = 0; c < 2; c++)
                for (int i = 0; i < 26; i++)
                {
                    float y = (c == 0 ? 0.43f : 0.67f) - i * 0.022f;
                    if (y < 0.05f) break;
                    tb.Rect(c == 0 ? 0.3f : 0.76f, y, i % 6 == 5 ? 0.3f : 0.42f, 0.008f, new Color(0.45f, 0.43f, 0.39f));
                }
            return tb.Finish("SecNewspaper", 0.8f, 25);
        }
    }
}
