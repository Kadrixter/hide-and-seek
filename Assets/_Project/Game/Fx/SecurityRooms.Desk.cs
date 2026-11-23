using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Things on the console desk: keyboards, mice, mugs, phone, lamp (a real warm light), logbook, papers, radio, fan, snacks.</summary>
    public partial class SecurityRooms
    {
        private static Texture2D _keysTex, _phoneTex, _logTex;

        private void BuildDeskClutter(Transform root, int lv, float cx, float top)
        {
            float zFront = Z1 - 0.95f, zMid = Z1 - 0.65f, zBack = Z1 - 0.4f;
            if (_keysTex == null) _keysTex = MakeKeyboardTexture();
            if (_phoneTex == null) _phoneTex = MakePhoneTexture();
            if (_logTex == null) _logTex = MakeLogbookTexture();

            // Two keyboards with mice on pads.
            foreach (var (kx, yaw) in new[] { (cx - 2.4f, 4f), (cx + 2.25f, -6f) })
            {
                Vector3 kb = new Vector3(kx, top, zFront + 0.02f);
                var rot = new Vector3(0f, yaw, 0f);
                Box(root, "Keyboard", kb + Vector3.up * 0.012f, new Vector3(0.46f, 0.022f, 0.16f), new Color(0.12f, 0.12f, 0.13f), false, rot, Surf.Shiny(0.3f));
                var face = Sign(root, "Keys", kb + Vector3.up * 0.026f, new Vector2(0.44f, 0.145f), Vector3.up, _keysTex, 0.25f);
                face.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * face.transform.rotation;
                Vector3 mp = new Vector3(kx + 0.42f, top, zFront + 0.04f);
                Box(root, "MousePad", mp + Vector3.up * 0.002f, new Vector3(0.24f, 0.004f, 0.2f), new Color(0.1f, 0.12f, 0.22f), false, null, Surf.T(SecurityTextures.Kind.Fabric, 0.3f));
                Prim(root, PrimitiveType.Sphere, "Mouse", mp + new Vector3(0.01f, 0.014f, 0.01f), new Vector3(0.06f, 0.03f, 0.1f), new Color(0.13f, 0.13f, 0.14f), new Vector3(0f, yaw + 10f, 0f), false, Surf.Shiny(0.45f));
                Prim(root, PrimitiveType.Cylinder, "MouseCable", mp + new Vector3(0.01f, 0.006f, 0.18f), new Vector3(0.006f, 0.12f, 0.006f), new Color(0.05f, 0.05f, 0.05f), new Vector3(90f, 0f, 0f));
            }

            // Mugs (one with a chipped logo band), coffee inside.
            MugModel(root, new Vector3(cx - 3.3f, top, zFront + 0.12f), new Color(0.92f, 0.92f, 0.9f), new Color(0.15f, 0.3f, 0.6f), 30f + lv * 40f);
            MugModel(root, new Vector3(cx + 1.75f, top, zBack), new Color(0.6f, 0.12f, 0.1f), new Color(0.95f, 0.95f, 0.95f), -70f + lv * 25f);

            // Fast-food cup with a lid and a bent straw.
            CupModel(root, new Vector3(cx - 1.55f, top, zFront + 0.05f), new Color(0.75f, 0.12f, 0.1f), true);

            // Open donut box.
            Vector3 dbox = new Vector3(cx + 1.75f, top, zMid - 0.15f);
            var pink = new Color(0.9f, 0.55f, 0.68f);
            var dq = Quaternion.Euler(0f, 12f, 0f);
            Box(root, "DonutBox", dbox + Vector3.up * 0.002f, new Vector3(0.38f, 0.004f, 0.3f), pink, false, new Vector3(0f, 12f, 0f));
            foreach (var (o, sz) in new[] { (new Vector3(0.188f, 0.025f, 0f), new Vector3(0.004f, 0.05f, 0.3f)), (new Vector3(-0.188f, 0.025f, 0f), new Vector3(0.004f, 0.05f, 0.3f)),
                                             (new Vector3(0f, 0.025f, -0.148f), new Vector3(0.38f, 0.05f, 0.004f)), (new Vector3(0f, 0.025f, 0.148f), new Vector3(0.38f, 0.05f, 0.004f)) })
                Box(root, "DonutBoxWall", dbox + dq * o, sz, pink * 0.95f, false, new Vector3(0f, 12f, 0f));
            Box(root, "DonutLid", dbox + new Vector3(-0.03f, 0.17f, 0.17f), new Vector3(0.38f, 0.3f, 0.01f), new Color(0.9f, 0.55f, 0.68f), false, new Vector3(-12f, 12f, 0f));
            Color[] glaze = { new Color(0.32f, 0.17f, 0.08f), new Color(1f, 0.6f, 0.75f), new Color(0.95f, 0.9f, 0.8f) };
            for (int i = 0; i < 3 - (lv == 2 ? 1 : 0); i++)
                DonutModel(root, dbox + Quaternion.Euler(0f, 12f, 0f) * new Vector3(-0.115f + i * 0.115f, 0.004f, -0.03f + (i % 2) * 0.05f), glaze[i], i * 50f, i == 1);

            // Desk phone: wedge base, keypad, handset on the cradle, curly cord.
            Vector3 ph = new Vector3(cx + 3.0f, top, zBack);
            Box(root, "Phone", ph + Vector3.up * 0.035f, new Vector3(0.22f, 0.07f, 0.2f), new Color(0.2f, 0.2f, 0.21f), false, new Vector3(-12f, -15f, 0f), Surf.Shiny(0.35f));
            var pad = Sign(root, "PhoneKeys", ph + new Vector3(0.02f, 0.073f, -0.03f), new Vector2(0.12f, 0.1f), Vector3.up, _phoneTex, 0.3f);
            pad.transform.rotation = Quaternion.Euler(-12f, -15f, 0f) * pad.transform.rotation;
            Box(root, "Handset", ph + new Vector3(-0.06f, 0.09f, 0.02f), new Vector3(0.05f, 0.035f, 0.22f), new Color(0.2f, 0.2f, 0.21f), false, new Vector3(0f, -15f, 0f), Surf.Shiny(0.35f));
            for (int i = 0; i < 6; i++)
                Prim(root, PrimitiveType.Sphere, "Cord", ph + new Vector3(-0.16f - i * 0.03f, 0.012f, 0.06f - i * 0.02f), Vector3.one * 0.022f, new Color(0.15f, 0.15f, 0.16f));

            // Desk lamp with a warm light falling on the logbook.
            Vector3 dl = new Vector3(cx + 4.25f, top, zBack - 0.05f);
            var lampGreen = new Color(0.1f, 0.22f, 0.14f);
            Prim(root, PrimitiveType.Cylinder, "LampBase", dl + Vector3.up * 0.012f, new Vector3(0.16f, 0.012f, 0.16f), lampGreen, null, false, Surf.Shiny(0.5f, 0.5f));
            Prim(root, PrimitiveType.Cylinder, "LampArm", dl + new Vector3(-0.04f, 0.19f, 0f), new Vector3(0.016f, 0.19f, 0.016f), new Color(0.6f, 0.6f, 0.6f), new Vector3(0f, 0f, 12f), false, Surf.Shiny(0.6f, 0.9f));
            Prim(root, PrimitiveType.Cylinder, "LampArm2", dl + new Vector3(-0.15f, 0.39f, 0f), new Vector3(0.014f, 0.12f, 0.014f), new Color(0.6f, 0.6f, 0.6f), new Vector3(0f, 0f, 60f), false, Surf.Shiny(0.6f, 0.9f));
            Prim(root, PrimitiveType.Cylinder, "LampHead", dl + new Vector3(-0.27f, 0.42f, -0.02f), new Vector3(0.13f, 0.07f, 0.13f), lampGreen, new Vector3(0f, 0f, 38f), false, Surf.Shiny(0.5f, 0.5f));
            Led(root, "LampBulb", dl + new Vector3(-0.3f, 0.38f, -0.02f), new Vector3(0.06f, 0.03f, 0.06f), new Color(2f, 1.6f, 1f), PrimitiveType.Sphere);
            var ll = AddLight(root, "DeskLampLight", dl + new Vector3(-0.31f, 0.36f, -0.02f), LightType.Spot, 2.4f, 1.6f, new Color(1f, 0.78f, 0.5f));
            ll.transform.rotation = Quaternion.LookRotation(new Vector3(-0.45f, -1f, -0.35f));
            ll.spotAngle = 95f;
            ll.innerSpotAngle = 40f;

            // Logbook open under the lamp, pen across it.
            Vector3 lb = new Vector3(cx + 3.7f, top, zFront + 0.02f);
            Box(root, "LogCover", lb + Vector3.up * 0.006f, new Vector3(0.46f, 0.012f, 0.32f), new Color(0.12f, 0.16f, 0.35f), false, new Vector3(0f, -8f, 0f));
            var log = Sign(root, "LogPages", lb + Vector3.up * 0.0135f, new Vector2(0.44f, 0.3f), Vector3.up, _logTex, 0.05f);
            log.transform.rotation = Quaternion.Euler(0f, -8f, 0f) * log.transform.rotation;
            Prim(root, PrimitiveType.Cylinder, "Pen", lb + new Vector3(0.05f, 0.02f, -0.04f), new Vector3(0.008f, 0.07f, 0.008f), new Color(0.1f, 0.2f, 0.7f), new Vector3(90f, 35f, 0f), false, Surf.Shiny(0.6f));

            // Papers (a messy stack), two binders lying flat.
            for (int i = 0; i < 6; i++)
                Box(root, "Paper", new Vector3(cx - 4.05f + i * 0.01f, top + 0.002f + i * 0.0025f, zMid + i * 0.012f), new Vector3(0.21f, 0.002f, 0.297f), new Color(0.9f, 0.9f, 0.86f), false, new Vector3(0f, i * 7f - 12f, 0f));
            Box(root, "BinderFlat", new Vector3(cx - 4.3f, top + 0.03f, zBack), new Vector3(0.32f, 0.06f, 0.29f), new Color(0.1f, 0.25f, 0.5f), false, new Vector3(0f, 5f, 0f), Surf.Shiny(0.35f));
            Box(root, "BinderFlat", new Vector3(cx - 4.28f, top + 0.09f, zBack + 0.01f), new Vector3(0.32f, 0.06f, 0.29f), new Color(0.55f, 0.1f, 0.08f), false, new Vector3(0f, -4f, 0f), Surf.Shiny(0.35f));

            // Walkie-talkie standing in its charger (green LED).
            Vector3 wt = new Vector3(cx - 1.6f, top, zBack);
            Box(root, "Charger", wt + Vector3.up * 0.025f, new Vector3(0.09f, 0.05f, 0.08f), new Color(0.08f, 0.08f, 0.09f), false);
            Box(root, "Radio", wt + Vector3.up * 0.13f, new Vector3(0.06f, 0.17f, 0.035f), new Color(0.06f, 0.06f, 0.07f), false, null, Surf.Shiny(0.3f));
            Prim(root, PrimitiveType.Cylinder, "Antenna", wt + new Vector3(0.018f, 0.27f, 0f), new Vector3(0.01f, 0.06f, 0.01f), new Color(0.05f, 0.05f, 0.05f));
            Led(root, "ChargeLed", wt + new Vector3(0.03f, 0.035f, -0.041f), new Vector3(0.008f, 0.008f, 0.004f), new Color(0.2f, 1.5f, 0.3f));

            // Desk fan (blades spin).
            Vector3 fan = new Vector3(cx - 3.0f, top, zBack + 0.05f);
            Prim(root, PrimitiveType.Cylinder, "FanBase", fan + Vector3.up * 0.01f, new Vector3(0.14f, 0.01f, 0.14f), new Color(0.75f, 0.75f, 0.73f), null, false, Surf.Shiny(0.4f));
            Prim(root, PrimitiveType.Cylinder, "FanStem", fan + Vector3.up * 0.12f, new Vector3(0.02f, 0.11f, 0.02f), new Color(0.75f, 0.75f, 0.73f), null, false, Surf.Shiny(0.4f));
            var blades = new GameObject("FanBlades").transform;
            blades.SetParent(root, false);
            blades.position = fan + new Vector3(0f, 0.25f, -0.03f);
            for (int i = 0; i < 3; i++)
            {
                var b = Box(blades, "Blade", blades.position, new Vector3(0.035f, 0.11f, 0.004f), new Color(0.5f, 0.62f, 0.75f), false);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                b.transform.position = blades.position + b.transform.rotation * new Vector3(0f, 0.055f, 0f);
            }
            _fans.Add(blades);
            Prim(root, PrimitiveType.Cylinder, "FanCage", fan + new Vector3(0f, 0.25f, -0.025f), new Vector3(0.25f, 0.005f, 0.25f), new Color(0.65f, 0.65f, 0.65f), new Vector3(90f, 0f, 0f), false, Surf.Shiny(0.5f, 0.8f));

            // Energy-drink cans (one lying), pen pot.
            CanModel(root, new Vector3(cx + 2.65f, top, zBack), new Color(0.12f, 0.55f, 0.18f), 20f);
            CanModel(root, new Vector3(cx + 2.8f, top, zBack + 0.08f), new Color(0.12f, 0.55f, 0.18f), 40f, true);
            Vector3 pot = new Vector3(cx - 2.85f, top, zBack - 0.15f);
            Prim(root, PrimitiveType.Cylinder, "PenPot", pot + Vector3.up * 0.05f, new Vector3(0.07f, 0.05f, 0.07f), new Color(0.2f, 0.2f, 0.22f), null, false, Surf.T(SecurityTextures.Kind.Metal, 0.1f, 0.4f, 0.8f));
            for (int i = 0; i < 4; i++)
                Prim(root, PrimitiveType.Cylinder, "PotPen", pot + new Vector3(-0.015f + i * 0.01f, 0.12f, (i % 2) * 0.012f), new Vector3(0.007f, 0.07f, 0.007f),
                    i == 0 ? new Color(0.7f, 0.1f, 0.1f) : i == 1 ? new Color(0.1f, 0.2f, 0.7f) : new Color(0.1f, 0.1f, 0.1f), new Vector3(i * 6f - 9f, 0f, i * 5f - 7f));
        }

        private static Texture2D MakeKeyboardTexture()
        {
            var tb = new TextureBaker(512, 168, new Color(0.08f, 0.08f, 0.09f));
            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            var cap = new Color(0.2f, 0.2f, 0.22f);
            var ink = new Color(0.7f, 0.7f, 0.72f);
            for (int r = 0; r < 4; r++)
            {
                float y = 0.82f - r * 0.2f, x0 = 0.06f + r * 0.012f;
                for (int k = 0; k < 13; k++)
                {
                    float x = x0 + k * 0.068f;
                    tb.Rect(x, y, 0.058f, 0.16f, cap);
                    if (k < rows[r].Length) tb.Text(rows[r][k].ToString(), x, y + 0.02f, 0.06f, ink);
                }
            }
            tb.Rect(0.42f, 0.04f + 0.0f, 0.4f, 0.13f, cap);     // space bar
            tb.Rect(0.93f, 0.62f, 0.09f, 0.5f, cap);              // numpad block hint
            return tb.Finish("SecKeys", 0.1f, 9);
        }

        private static Texture2D MakePhoneTexture()
        {
            var tb = new TextureBaker(128, 112, new Color(0.18f, 0.18f, 0.19f));
            string keys = "123456789*0#";
            for (int i = 0; i < 12; i++)
            {
                float x = 0.25f + (i % 3) * 0.25f, y = 0.8f - (i / 3) * 0.2f;
                tb.Rect(x, y, 0.2f, 0.16f, new Color(0.75f, 0.75f, 0.72f));
                tb.Text(keys[i].ToString(), x, y, 0.08f, new Color(0.1f, 0.1f, 0.1f));
            }
            return tb.Finish("SecPhone", 0.1f, 4);
        }

        private static Texture2D MakeLogbookTexture()
        {
            var paper = new Color(0.86f, 0.84f, 0.76f);
            var tb = new TextureBaker(384, 256, paper);
            tb.Rect(0.5f, 0.5f, 0.012f, 1f, new Color(0.55f, 0.52f, 0.45f));        // spine fold
            for (int i = 0; i < 11; i++)
            {
                float y = 0.85f - i * 0.075f;
                tb.Rect(0.25f, y, 0.44f, 0.004f, new Color(0.55f, 0.6f, 0.75f)).Rect(0.75f, y, 0.44f, 0.004f, new Color(0.55f, 0.6f, 0.75f));
            }
            var ink = new Color(0.12f, 0.15f, 0.4f);
            tb.Text("DZIENNIK ZMIANY", 0.25f, 0.93f, 0.045f, new Color(0.2f, 0.2f, 0.2f));
            string[] left = { "22:00 obchód P0 - ok", "23:15 brama zamknięta", "00:40 ruch? P-2 kam.4", "01:05 nic nie widać", "02:30 światło P-1 miga" };
            for (int i = 0; i < left.Length; i++) tb.Text(left[i], 0.04f, 0.8f - i * 0.075f, 0.035f, ink, TextAnchor.MiddleLeft, false, 0.42f);
            string[] right = { "03:10 hałas przy windzie", "03:12 sprawdzić!!", "03:47 ktoś na schodach", "skrzynię zostawić", "NIE WYCHODZIĆ" };
            for (int i = 0; i < right.Length; i++) tb.Text(right[i], 0.54f, 0.8f - i * 0.075f, 0.035f, i == 4 ? new Color(0.6f, 0.08f, 0.06f) : ink, TextAnchor.MiddleLeft, false, 0.42f);
            return tb.Finish("SecLog", 0.3f, 6);
        }
    }
}
