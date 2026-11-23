using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Small round props shared by the desk, the coffee corner and the table: mugs, cans, donuts, cups, a jug, pizza.</summary>
    public partial class SecurityRooms
    {
        private static readonly Color Coffee = new Color(0.1f, 0.05f, 0.025f);

        /// <summary>Ceramic mug: thick rim, inside, coffee, a looped handle. <paramref name="at"/> = on the surface.</summary>
        private void MugModel(Transform parent, Vector3 at, Color body, Color band, float yaw, bool full = true)
        {
            var m = CachedMesh("Mug", p => p
                .Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.036f, 0f), new Vector2(0.04f, 0.006f), new Vector2(0.04f, 0.094f), new Vector2(0.036f, 0.098f),
                               new Vector2(0.034f, 0.094f), new Vector2(0.034f, 0.012f), new Vector2(0f, 0.012f) }, 20, 50f)
                .At(new Vector3(0.042f, 0.05f, 0f), Quaternion.Euler(90f, 0f, 0f)).Torus(0.024f, 0.0065f, 14, 8, -100f, 100f));
            var rot = Quaternion.Euler(0f, yaw, 0f);
            MeshObj(parent, "Mug", m, at, rot, body, Surf.Shiny(0.7f));
            MeshObj(parent, "MugBand", CachedMesh("MugBand", p => p.Lathe(new[] { new Vector2(0.0405f, 0.055f), new Vector2(0.0405f, 0.075f) }, 20)), at, rot, band, Surf.Shiny(0.7f), false, false);
            if (full)
                MeshObj(parent, "Coffee", CachedMesh("MugCoffee", p => p.Lathe(new[] { new Vector2(0.0345f, 0.078f), new Vector2(0f, 0.08f) }, 20)), at, rot, Coffee, Surf.Shiny(0.9f), false, false);
        }

        /// <summary>Drink can (domed bottom, necked top, rim), standing or lying on its side.</summary>
        private void CanModel(Transform parent, Vector3 at, Color paint, float yaw, bool lying = false)
        {
            var m = CachedMesh("Can", p => p.Lathe(new[] { new Vector2(0f, 0.004f), new Vector2(0.022f, 0f), new Vector2(0.032f, 0.008f), new Vector2(0.033f, 0.016f),
                new Vector2(0.033f, 0.104f), new Vector2(0.027f, 0.118f), new Vector2(0.026f, 0.122f), new Vector2(0.023f, 0.12f), new Vector2(0f, 0.119f) }, 20, 35f));
            var lid = CachedMesh("CanLid", p => p.Lathe(new[] { new Vector2(0.0265f, 0.1205f), new Vector2(0.0255f, 0.1225f), new Vector2(0.0232f, 0.1205f), new Vector2(0f, 0.1195f) }, 20));
            var rot = Quaternion.Euler(0f, yaw, 0f) * (lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity);
            Vector3 pos = lying ? at + Vector3.up * 0.033f + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.06f, 0f, 0f) : at;
            MeshObj(parent, "Can", m, pos, rot, paint, Surf.Shiny(0.7f, 0.6f));
            MeshObj(parent, "CanLid", lid, pos, rot, new Color(0.7f, 0.71f, 0.73f), Surf.Shiny(0.8f, 1f), false, false);
        }

        /// <summary>Ring donut with glaze on top (and sprinkles on the pink ones).</summary>
        private void DonutModel(Transform parent, Vector3 at, Color glaze, float yaw, bool sprinkles)
        {
            var dough = CachedMesh("Donut", p => p.At(new Vector3(0f, 0.016f, 0f), Quaternion.identity, new Vector3(1f, 0.85f, 1f)).Torus(0.032f, 0.019f, 22, 12));
            var icing = CachedMesh("DonutGlaze", p => p.At(new Vector3(0f, 0.0215f, 0f), Quaternion.identity, new Vector3(1f, 0.62f, 1f)).Torus(0.032f, 0.0205f, 22, 12));
            var rot = Quaternion.Euler(0f, yaw, 0f);
            MeshObj(parent, "Donut", dough, at, rot, new Color(0.78f, 0.52f, 0.27f), Surf.Shiny(0.15f));
            MeshObj(parent, "Glaze", icing, at, rot, glaze, Surf.Shiny(0.7f), false, false);
            if (!sprinkles) return;
            Color[] sc = { new(1f, 0.95f, 0.3f), new(0.3f, 0.7f, 1f), new(1f, 1f, 1f), new(0.4f, 0.9f, 0.4f) };
            for (int i = 0; i < 10; i++)
            {
                float a = i * 37f * Mathf.Deg2Rad, r = 0.022f + (i % 3) * 0.009f;
                Vector3 sp = at + rot * new Vector3(Mathf.Cos(a) * r, 0.034f + (i % 2) * 0.002f, Mathf.Sin(a) * r);
                Box(parent, "Sprinkle", sp, new Vector3(0.008f, 0.003f, 0.003f), sc[i % sc.Length], false, new Vector3(0f, i * 61f, 0f));
            }
        }

        /// <summary>Paper take-away cup with a plastic lid (and a straw when <paramref name="straw"/>).</summary>
        private void CupModel(Transform parent, Vector3 at, Color paint, bool straw)
        {
            MeshObj(parent, "Cup", CachedMesh("Cup", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.032f, 0f), new Vector2(0.045f, 0.16f), new Vector2(0f, 0.16f) }, 20, 30f)),
                at, Quaternion.identity, paint, Surf.Shiny(0.25f));
            MeshObj(parent, "CupBand", CachedMesh("CupBand", p => p.Lathe(new[] { new Vector2(0.0365f, 0.055f), new Vector2(0.0395f, 0.1f) }, 20)), at, Quaternion.identity, new Color(0.92f, 0.92f, 0.9f), Surf.Shiny(0.25f), false, false);
            MeshObj(parent, "CupLid", CachedMesh("CupLid", p => p.Lathe(new[] { new Vector2(0.047f, 0.158f), new Vector2(0.047f, 0.166f), new Vector2(0.04f, 0.17f), new Vector2(0.038f, 0.176f), new Vector2(0f, 0.177f) }, 20, 40f)),
                at, Quaternion.identity, new Color(0.88f, 0.88f, 0.9f), Surf.Shiny(0.5f), false, false);
            if (straw)
                MeshObj(parent, "Straw", CachedMesh("Straw", p => p.Tube(new[] { new Vector3(0.008f, 0.1f, 0f), new Vector3(0.012f, 0.24f, 0f), new Vector3(0.016f, 0.255f, 0f), new Vector3(0.05f, 0.27f, 0f) }, 0.004f, 6)),
                    at, Quaternion.Euler(0f, 30f, 0f), new Color(1f, 0.85f, 0.2f), Surf.Shiny(0.5f), false, false);
        }

        /// <summary>Round pizza on a box bottom, <paramref name="slicesLeft"/> of 8 slices (the cut faces show crust and cheese).</summary>
        private void PizzaModel(Transform parent, Vector3 at, float yaw, int slicesLeft)
        {
            float a1 = slicesLeft * 45f;
            var crust = CachedMesh("PizzaBase" + slicesLeft, p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.148f, 0f), new Vector2(0.158f, 0.009f), new Vector2(0.153f, 0.019f),
                new Vector2(0.143f, 0.016f), new Vector2(0.14f, 0.01f), new Vector2(0f, 0.01f) }, 40, 40f, 0f, a1));
            var cheese = CachedMesh("PizzaCheese" + slicesLeft, p => p.Lathe(new[] { new Vector2(0.141f, 0.0102f), new Vector2(0.135f, 0.0135f), new Vector2(0f, 0.0145f) }, 40, 60f, 0f, a1));
            var rot = Quaternion.Euler(0f, yaw, 0f);
            MeshObj(parent, "PizzaCrust", crust, at, rot, new Color(0.8f, 0.55f, 0.28f), Surf.Shiny(0.2f));
            MeshObj(parent, "PizzaCheese", cheese, at, rot, new Color(0.95f, 0.72f, 0.3f), Surf.Shiny(0.55f), false, false);
            var pep = CachedMesh("Pepperoni", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.018f, 0f), new Vector2(0.019f, 0.003f), new Vector2(0f, 0.0035f) }, 14));
            var rnd = new System.Random(slicesLeft * 7 + 3);
            for (int i = 0; i < 16; i++)
            {
                float ang = (float)rnd.NextDouble() * a1, r = 0.03f + (float)rnd.NextDouble() * 0.09f;
                if (ang > a1 - 6f || ang < 6f) continue;
                Vector3 pp = at + rot * new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * r, 0.0135f, Mathf.Sin(ang * Mathf.Deg2Rad) * r);
                MeshObj(parent, "Pepperoni", pep, pp, rot, new Color(0.55f, 0.1f, 0.06f), Surf.Shiny(0.5f), false, false);
            }
        }

        /// <summary>Pizza box (closed, or open with the lid leaning back). Returns the inside floor centre.</summary>
        private Vector3 PizzaBox(Transform parent, Vector3 at, float yaw, bool open)
        {
            var card = new Color(0.78f, 0.66f, 0.47f);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 c = at + Vector3.up * 0.0225f;
            if (!open)
            {
                Box(parent, "PizzaBox", c, new Vector3(0.34f, 0.045f, 0.34f), card, false, new Vector3(0f, yaw, 0f), Surf.T(SecurityTextures.Kind.Planks, 0.5f, 0.05f));
                if (_pizzaLid == null) _pizzaLid = MakePizzaLid();
                var top = Sign(parent, "PizzaLid", c + Vector3.up * 0.0255f, new Vector2(0.32f, 0.32f), Vector3.up, _pizzaLid, 0.05f);
                top.transform.rotation = rot * top.transform.rotation;
                return c + Vector3.up * 0.0225f;
            }
            Box(parent, "PizzaBoxBottom", at + Vector3.up * 0.002f, new Vector3(0.34f, 0.004f, 0.34f), card, false, new Vector3(0f, yaw, 0f));
            foreach (var (o, s) in new[] { (new Vector3(0.168f, 0.02f, 0f), new Vector3(0.004f, 0.04f, 0.34f)), (new Vector3(-0.168f, 0.02f, 0f), new Vector3(0.004f, 0.04f, 0.34f)), (new Vector3(0f, 0.02f, -0.168f), new Vector3(0.34f, 0.04f, 0.004f)) })
                Box(parent, "PizzaBoxWall", at + rot * o, s, card * 0.95f, false, new Vector3(0f, yaw, 0f));
            // Lid hinged at the back edge, open past upright.
            var lidRot = rot * Quaternion.Euler(105f, 0f, 0f);
            Vector3 hinge = at + rot * new Vector3(0f, 0.04f, 0.17f);
            Box(parent, "PizzaBoxLid", hinge + lidRot * new Vector3(0f, 0f, -0.17f), new Vector3(0.34f, 0.004f, 0.34f), card, false, lidRot.eulerAngles);
            if (_pizzaLid == null) _pizzaLid = MakePizzaLid();
            var face = Sign(parent, "PizzaLidPrint", hinge + lidRot * new Vector3(0f, 0.0025f, -0.17f), new Vector2(0.32f, 0.32f), lidRot * Vector3.up, _pizzaLid, 0.05f);
            face.transform.rotation = Quaternion.LookRotation(-(lidRot * Vector3.up), lidRot * Vector3.forward);
            Box(parent, "GreasePaper", at + Vector3.up * 0.0045f, new Vector3(0.31f, 0.001f, 0.31f), new Color(0.85f, 0.8f, 0.68f), false, new Vector3(0f, yaw + 7f, 0f));
            return at + Vector3.up * 0.005f;
        }

        private Texture2D _pizzaLid;

        private static Texture2D MakePizzaLid()
        {
            var red = new Color(0.72f, 0.1f, 0.07f);
            var tb = new TextureBaker(256, 256, new Color(0.8f, 0.68f, 0.48f));
            tb.Circle(0.5f, 0.56f, 0.3f, red, 0.26f, new Color(0.8f, 0.68f, 0.48f));
            tb.Text("PIZZA", 0.5f, 0.6f, 0.13f, red);
            tb.Text("DA MARIO", 0.5f, 0.46f, 0.07f, new Color(0.15f, 0.35f, 0.15f));
            tb.Text("tel. 555 123 456", 0.5f, 0.14f, 0.05f, red);
            return tb.Finish("PizzaLid", 0.35f, 21);
        }
    }
}
