using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Wall details: safety posters, key board, fire extinguisher, clock, garage plan, calendar, cork board, first-aid box, cables.</summary>
    public partial class SecurityRooms
    {
        private static Texture2D _posterBhp, _posterSmoke, _posterWatch, _posterExit, _extSign, _planTex, _calTex, _keyTagTex, _aidTex;

        private static void BakeWallTextures()
        {
            if (_posterBhp != null) return;
            var paper = new Color(0.9f, 0.88f, 0.82f);
            var dark = new Color(0.12f, 0.12f, 0.12f);
            var red = new Color(0.75f, 0.1f, 0.08f);

            var tb = new TextureBaker(256, 360, paper);
            tb.Rect(0.5f, 0.9f, 1f, 0.2f, new Color(0.1f, 0.35f, 0.65f)).Text("INSTRUKCJA BHP", 0.5f, 0.9f, 0.05f, Color.white, TextAnchor.MiddleCenter, true, 0.9f);
            tb.Text("STANOWISKO\nMONITORINGU", 0.5f, 0.72f, 0.04f, dark);
            for (int i = 0; i < 9; i++) tb.Rect(0.47f, 0.56f - i * 0.05f, 0.8f - (i % 3) * 0.12f, 0.012f, new Color(0.45f, 0.45f, 0.45f));
            tb.Text("1. Nie opuszczać stanowiska.\n2. Każdy ruch zgłaszać.\n3. Nie otwierać skrzyni.", 0.08f, 0.12f, 0.022f, dark, TextAnchor.MiddleLeft, false, 0.86f);
            _posterBhp = tb.Finish("PosterBhp", 0.55f, 11);

            tb = new TextureBaker(256, 320, Color.white);
            tb.Circle(0.5f, 0.6f, 0.3f, red, 0.25f, Color.white);
            tb.Rect(0.5f, 0.6f, 0.5f, 0.06f, new Color(0.2f, 0.2f, 0.2f)).Rect(0.73f, 0.6f, 0.06f, 0.06f, new Color(0.9f, 0.4f, 0.1f));
            tb.Rect(0.5f, 0.6f, 0.08f, 0.6f, red, 45f);
            tb.Text("ZAKAZ PALENIA", 0.5f, 0.14f, 0.07f, red, TextAnchor.MiddleCenter, true, 0.92f);
            _posterSmoke = tb.Finish("PosterSmoke", 0.45f, 12);

            tb = new TextureBaker(256, 300, new Color(0.95f, 0.8f, 0.12f));
            tb.Triangle(0.5f, 0.62f, 0.62f, dark).Triangle(0.5f, 0.6f, 0.46f, new Color(0.95f, 0.8f, 0.12f));
            tb.Text("!", 0.5f, 0.6f, 0.2f, dark);
            tb.Text("UWAGA!\nOBIEKT MONITOROWANY", 0.5f, 0.15f, 0.05f, dark, TextAnchor.MiddleCenter, true, 0.92f);
            _posterWatch = tb.Finish("PosterWatch", 0.5f, 13);

            tb = new TextureBaker(384, 128, new Color(0.05f, 0.5f, 0.22f));
            tb.Rect(0.16f, 0.5f, 0.2f, 0.8f, Color.white).Rect(0.16f, 0.5f, 0.16f, 0.68f, new Color(0.05f, 0.5f, 0.22f));
            tb.Text("WYJŚCIE\nEWAKUACYJNE", 0.62f, 0.5f, 0.17f, Color.white);
            _posterExit = tb.Finish("SignExit", 0.25f, 14);

            tb = new TextureBaker(200, 200, red);
            tb.Rect(0.5f, 0.45f, 0.26f, 0.55f, Color.white).Rect(0.5f, 0.78f, 0.12f, 0.1f, Color.white).Rect(0.66f, 0.8f, 0.2f, 0.05f, Color.white);
            tb.Text("GAŚNICA", 0.5f, 0.1f, 0.1f, Color.white);
            _extSign = tb.Finish("SignExt", 0.3f, 15);

            tb = new TextureBaker(400, 280, new Color(0.88f, 0.88f, 0.84f));
            tb.Text("PLAN GARAŻU  P0 / P-1 / P-2", 0.5f, 0.93f, 0.045f, dark);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.72f - i * 0.28f;
                tb.Frame(0.5f, y, 0.9f, 0.22f, 0.012f, new Color(0.2f, 0.3f, 0.55f));
                for (int a = 0; a < 6; a++) tb.Rect(0.1f + a * 0.16f, y, 0.1f, 0.012f, new Color(0.6f, 0.65f, 0.75f));
                for (int p = 0; p < 6; p++) tb.Circle(0.1f + p * 0.16f, y + (p % 2 == 0 ? 0.06f : -0.06f), 0.018f, red);
                tb.Text("P" + LevelName(i), 0.02f, y + 0.08f, 0.035f, dark, TextAnchor.MiddleLeft);
            }
            _planTex = tb.Finish("PlanGarage", 0.4f, 16);

            tb = new TextureBaker(240, 340, Color.white);
            tb.Rect(0.5f, 0.78f, 0.9f, 0.36f, new Color(0.25f, 0.45f, 0.7f)).Circle(0.3f, 0.75f, 0.12f, new Color(0.95f, 0.85f, 0.4f));
            tb.Triangle(0.62f, 0.68f, 0.35f, new Color(0.2f, 0.35f, 0.25f));
            tb.Text("LISTOPAD", 0.5f, 0.52f, 0.05f, red);
            for (int d = 0; d < 30; d++)
            {
                float x = 0.12f + (d % 7) * 0.127f, y = 0.42f - (d / 7) * 0.085f;
                tb.Text((d + 1).ToString(), x, y, 0.028f, d % 7 == 6 ? red : dark);
                if (d < 20) tb.Rect(x, y, 0.09f, 0.008f, red, 35f);
            }
            _calTex = tb.Finish("Calendar", 0.4f, 17);

            tb = new TextureBaker(64, 32, Color.white);
            tb.Text("P-1", 0.5f, 0.5f, 0.4f, dark);
            _keyTagTex = tb.Finish("KeyTag", 0.3f, 18);

            tb = new TextureBaker(160, 160, new Color(0.1f, 0.55f, 0.25f));
            tb.Rect(0.5f, 0.55f, 0.5f, 0.14f, Color.white).Rect(0.5f, 0.55f, 0.14f, 0.5f, Color.white);
            tb.Text("APTECZKA", 0.5f, 0.14f, 0.1f, Color.white);
            _aidTex = tb.Finish("FirstAid", 0.2f, 19);
        }

        private void BuildWallDetails(Transform root, int lv, float cx, float cz, float y0)
        {
            BakeWallTextures();
            Vector3 north = Vector3.forward, east = Vector3.right, west = Vector3.left;
            float sIn = Z0 + Wall * 0.5f + 0.006f;            // inner face of the south wall
            float wIn = X0 + Wall * 0.5f + 0.006f, eIn = X1 - Wall * 0.5f - 0.006f;

            // Posters on the south wall, a cork board with notes.
            Sign(root, "PosterBhp", new Vector3(X0 + 2.0f, y0 + 1.55f, sIn), new Vector2(0.42f, 0.59f), north, _posterBhp);
            Sign(root, "PosterWatch", new Vector3(X0 + 2.7f, y0 + 1.6f, sIn), new Vector2(0.36f, 0.42f), north, _posterWatch);
            Sign(root, "PosterSmoke", new Vector3(X0 + 7.4f, y0 + 1.6f, sIn), new Vector2(0.3f, 0.38f), north, _posterSmoke);
            Vector3 cork = new Vector3(X0 + 5.0f, y0 + 1.5f, Z0 + Wall * 0.5f + 0.015f);
            Box(root, "CorkBoard", cork, new Vector3(1.2f, 0.8f, 0.025f), new Color(0.9f, 0.9f, 0.9f), false, null, Surf.T(SecurityTextures.Kind.Cork, 0.6f));
            Box(root, "CorkFrame", cork + new Vector3(0f, 0f, -0.002f), new Vector3(1.26f, 0.86f, 0.02f), new Color(0.35f, 0.25f, 0.15f), false, null, Surf.T(SecurityTextures.Kind.Laminate, 0.5f, 0.3f));
            // Garage plan and calendar on the east wall (main room part).
            Sign(root, "Plan", new Vector3(eIn, y0 + 1.6f, cz + 1.6f), new Vector2(1.1f, 0.77f), west, _planTex);
            Box(root, "PlanBack", new Vector3(eIn + 0.006f, y0 + 1.6f, cz + 1.6f), new Vector3(0.006f, 0.81f, 1.14f), new Color(0.2f, 0.2f, 0.22f), false);
            Sign(root, "Calendar", new Vector3(eIn, y0 + 1.65f, cz + 0.4f), new Vector2(0.3f, 0.42f), west, _calTex);

            // Wall clock on the west wall (hands follow the real time).
            Vector3 clock = new Vector3(X0 + 0.13f, y0 + 2.3f, cz + 0.3f);
            Prim(root, PrimitiveType.Cylinder, "Clock", clock, new Vector3(0.36f, 0.02f, 0.36f), new Color(0.92f, 0.92f, 0.88f), new Vector3(0f, 0f, 90f), false, Surf.Shiny(0.6f));
            Prim(root, PrimitiveType.Cylinder, "ClockRim", clock + new Vector3(-0.003f, 0f, 0f), new Vector3(0.39f, 0.015f, 0.39f), new Color(0.1f, 0.1f, 0.1f), new Vector3(0f, 0f, 90f), false, Surf.Shiny(0.5f));
            for (int h = 0; h < 12; h++)
            {
                var q = Quaternion.Euler(-h * 30f, 0f, 0f);
                Box(root, "Tick", clock + new Vector3(0.022f, 0f, 0f) + q * new Vector3(0f, 0.15f, 0f), new Vector3(0.003f, h % 3 == 0 ? 0.03f : 0.015f, 0.008f), Color.black, false, new Vector3(-h * 30f, 0f, 0f));
            }
            var face = new GameObject("ClockFace").transform;
            face.SetParent(root, false);
            face.SetPositionAndRotation(clock + new Vector3(0.025f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            var hour = new GameObject("HourHand").transform;
            hour.SetParent(face, false);
            LocalPart(hour, PrimitiveType.Cube, new Vector3(0f, 0.045f, 0f), new Vector3(0.012f, 0.09f, 0.004f), Color.black, default);
            var minute = new GameObject("MinuteHand").transform;
            minute.SetParent(face, false);
            minute.localPosition = new Vector3(0f, 0f, 0.004f);
            LocalPart(minute, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f), new Vector3(0.008f, 0.14f, 0.004f), Color.black, default);
            _clocks.Add((hour, minute));

            // Entrance corridor: key board on the east wall, extinguisher with its sign, first-aid box, exit sign.
            BuildKeyBoard(root, new Vector3(eIn, y0 + 1.45f, Z0 + 1.35f));
            // The extinguisher itself (interactive, on its wall bracket) is built by ExtinguisherSystem at ExtinguisherSpot.
            Vector3 ext = ExtinguisherSpot(lv);
            Sign(root, "ExtSign", new Vector3(eIn, y0 + 1.25f, ext.z), new Vector2(0.2f, 0.2f), west, _extSign);
            Sign(root, "FirstAid", new Vector3(DoorA - 0.2f - 0.106f, y0 + 1.5f, Z0 + 1.6f), new Vector2(0.28f, 0.28f), west, _aidTex);
            Box(root, "FirstAidBox", new Vector3(DoorA - 0.2f - 0.06f, y0 + 1.5f, Z0 + 1.6f), new Vector3(0.08f, 0.3f, 0.3f), new Color(0.1f, 0.5f, 0.22f), false, null, Surf.Shiny(0.4f));
            Sign(root, "ExitSign", new Vector3((DoorA + DoorB) * 0.5f, y0 + 2.35f, sIn), new Vector2(0.6f, 0.2f), north, _posterExit);

            // Cable tray along the north wall above the monitors, cables dropping to the screens and the recorder shelf.
            Box(root, "CableTray", new Vector3(cx - 1.2f, y0 + 2.78f, Z1 - 0.25f), new Vector3(9.4f, 0.05f, 0.25f), new Color(0.4f, 0.42f, 0.45f), false, null, Surf.T(SecurityTextures.Kind.Metal, 1f, 0.35f, 0.7f));
            Box(root, "TrayLip", new Vector3(cx - 1.2f, y0 + 2.81f, Z1 - 0.375f), new Vector3(9.4f, 0.06f, 0.01f), new Color(0.4f, 0.42f, 0.45f), false, null, Surf.Shiny(0.35f, 0.7f));
            for (int i = 0; i < 3; i++)
                Prim(root, PrimitiveType.Cylinder, "Cable", new Vector3(cx - 1.5f + i * 1.8f + 0.55f, y0 + 2.62f, Z1 - 0.13f), new Vector3(0.018f, 0.17f, 0.018f), new Color(0.04f, 0.04f, 0.04f));
            // Floor cable run (taped) from the desk to the recorder shelf.
            Box(root, "FloorCable", new Vector3(X0 + 1.2f, y0 + 0.048f, Z1 - 0.62f), new Vector3(0.6f, 0.012f, 0.03f), new Color(0.04f, 0.04f, 0.04f), false);
            Box(root, "Tape", new Vector3(X0 + 1.2f, y0 + 0.055f, Z1 - 0.62f), new Vector3(0.12f, 0.003f, 0.06f), new Color(0.75f, 0.65f, 0.1f), false);
        }

        /// <summary>Board with hooks and labelled keys (one hook empty).</summary>
        private void BuildKeyBoard(Transform root, Vector3 at)
        {
            Box(root, "KeyBoard", at, new Vector3(0.02f, 0.5f, 0.4f), new Color(0.55f, 0.42f, 0.25f), false, null, Surf.T(SecurityTextures.Kind.Laminate, 0.4f, 0.25f));
            for (int r = 0; r < 3; r++)
            for (int k = 0; k < 4; k++)
            {
                Vector3 hk = at + new Vector3(-0.025f, 0.15f - r * 0.15f, -0.135f + k * 0.09f);
                Prim(root, PrimitiveType.Cylinder, "Hook", hk, new Vector3(0.008f, 0.02f, 0.008f), new Color(0.65f, 0.65f, 0.6f), new Vector3(0f, 0f, 90f), false, Surf.Shiny(0.7f, 1f));
                if (r == 1 && k == 2) continue;   // missing key
                Box(root, "Key", hk + new Vector3(-0.012f, -0.035f, 0f), new Vector3(0.004f, 0.05f, 0.012f), new Color(0.75f, 0.68f, 0.35f), false, null, Surf.Shiny(0.7f, 1f));
                var tagCol = (r + k) % 3 == 0 ? new Color(0.8f, 0.15f, 0.1f) : (r + k) % 3 == 1 ? new Color(0.15f, 0.35f, 0.75f) : new Color(0.9f, 0.85f, 0.2f);
                Box(root, "KeyTag", hk + new Vector3(-0.012f, -0.085f, 0f), new Vector3(0.004f, 0.045f, 0.03f), tagCol, false);
            }
            Sign(root, "KeyLabel", at + new Vector3(-0.0115f, 0.215f, 0f), new Vector2(0.16f, 0.06f), Vector3.left, _keyTagTex);
        }
    }
}
