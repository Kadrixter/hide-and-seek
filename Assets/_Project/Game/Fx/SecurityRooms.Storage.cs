using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Storage and odds: steel archive shelving with binders and labelled boxes (west wall), a low two-door cabinet with
    /// a laser printer and paper (east wall), the guard's jacket on the coat rack, written notes and CCTV prints on the
    /// cork board, a cable run across the floor to the desk.
    /// </summary>
    public partial class SecurityRooms
    {
        private static Texture2D _boxLabel, _jacketPrint, _noteA, _noteB, _photo;

        private void BuildStorage(Transform root, int lv, float y0)
        {
            float fy = y0 + 0.04f;
            BuildArchiveShelf(root, lv, new Vector3(X0 + Wall * 0.5f + 0.24f, fy, Z0 + 3.35f));
            BuildSideboard(root, lv, new Vector3(X1 - Wall * 0.5f - 0.235f, fy, Z0 + 5.4f));
            BuildJacket(root, new Vector3(DoorA - 0.55f, y0, Z0 + 0.45f));
            BuildCorkNotes(root, lv, new Vector3(X0 + 5.0f, y0 + 1.5f, Z0 + Wall * 0.5f + 0.0275f + 0.0035f));
            // Taped cable run from the counter sockets to the desk end.
            var cable = CachedMesh("FloorCable" + lv, p => p.Tube(new[] { new Vector3(X0 + 3.6f, fy + 0.006f, Z0 + 0.2f), new Vector3(X0 + 3.45f, fy + 0.006f, Z0 + 1.0f),
                new Vector3(X0 + 3.7f, fy + 0.006f, Z0 + 3.5f), new Vector3(X0 + 2.2f, fy + 0.006f, Z1 - 1.0f), new Vector3(X0 + 1.6f, fy + 0.006f, Z1 - 0.4f) }, 0.006f, 6));
            MeshObj(root, "FloorCable", cable, Vector3.zero, Quaternion.identity, new Color(0.03f, 0.03f, 0.03f), Surf.Shiny(0.4f), false, false);
            Vector3 c0 = new Vector3(X0 + 3.7f, fy + 0.0125f, Z0 + 3.5f), c1 = new Vector3(X0 + 2.2f, fy + 0.0125f, Z1 - 1.0f);
            float tapeYaw = Mathf.Atan2(c1.x - c0.x, c1.z - c0.z) * Mathf.Rad2Deg;
            foreach (var t in new[] { 0.2f, 0.5f, 0.8f })
                Box(root, "CableTape", Vector3.Lerp(c0, c1, t), new Vector3(0.06f, 0.0015f, 0.12f), new Color(0.8f, 0.7f, 0.12f), false, new Vector3(0f, tapeYaw + 90f, 0f));
        }

        private void BuildArchiveShelf(Transform root, int lv, Vector3 at)
        {
            var steel = new Color(0.55f, 0.57f, 0.58f);
            var metal = Surf.T(SecurityTextures.Kind.Metal, 0.6f, 0.35f, 0.6f);
            const float w = 1.45f, d = 0.42f, h = 1.95f;
            foreach (float sz in new[] { -w * 0.5f + 0.02f, w * 0.5f - 0.02f })
            foreach (float sx in new[] { -d * 0.5f + 0.02f, d * 0.5f - 0.02f })
                Box(root, "ArchivePost", at + new Vector3(sx, h * 0.5f, sz), new Vector3(0.035f, h, 0.035f), steel, true, null, metal);
            Color[] binders = { new(0.12f, 0.25f, 0.55f), new(0.6f, 0.1f, 0.08f), new(0.1f, 0.4f, 0.17f), new(0.1f, 0.1f, 0.11f), new(0.75f, 0.6f, 0.12f) };
            if (_boxLabel == null) _boxLabel = MakeBoxLabel();
            var rnd = new System.Random(lv * 17 + 5);
            for (int s = 0; s < 5; s++)
            {
                float y = 0.08f + s * 0.43f;
                Box(root, "ArchiveShelf", at + new Vector3(0f, y, 0f), new Vector3(d, 0.02f, w), steel, false, null, metal);
                Box(root, "ArchiveLip", at + new Vector3(d * 0.5f - 0.005f, y + 0.02f, 0f), new Vector3(0.01f, 0.03f, w), steel * 0.9f, false, null, Surf.Shiny(0.4f, 0.6f));
                if (s == 4) break;
                float top = at.y + y + 0.01f;
                float z = at.z - w * 0.5f + 0.06f;
                bool boxes = (s + lv) % 2 == 0;
                while (z < at.z + w * 0.5f - 0.1f)
                {
                    if (boxes)
                    {
                        // Archive box with a hand hole and a printed label on the front.
                        float bw = 0.3f;
                        Vector3 bp = new Vector3(at.x + 0.01f, top + 0.135f, z + bw * 0.5f);
                        Box(root, "ArchiveBox", bp, new Vector3(0.36f, 0.27f, bw - 0.01f), new Color(0.66f, 0.55f, 0.4f), false, null, Surf.Shiny(0.08f));
                        Sign(root, "BoxLabel", bp + new Vector3(0.1825f, 0.02f, 0f), new Vector2(0.2f, 0.12f), Vector3.right, _boxLabel, 0.1f);
                        Box(root, "BoxHole", bp + new Vector3(0.1815f, 0.09f, 0f), new Vector3(0.002f, 0.03f, 0.09f), new Color(0.12f, 0.09f, 0.06f), false);
                        z += bw + 0.02f;
                    }
                    else
                    {
                        float bw = 0.075f;
                        bool lean = rnd.NextDouble() < 0.08;
                        var c = binders[rnd.Next(binders.Length)];
                        Vector3 bp = new Vector3(at.x + 0.03f, top + 0.16f, z + bw * 0.5f);
                        var e = lean ? new Vector3(-14f, 0f, 0f) : (Vector3?)null;
                        Box(root, "Binder", bp, new Vector3(0.29f, 0.32f, bw - 0.004f), c, false, e, Surf.Shiny(0.35f));
                        Box(root, "BinderLabel", bp + new Vector3(0.146f, 0.05f, 0f), new Vector3(0.002f, 0.11f, bw * 0.7f), new Color(0.9f, 0.9f, 0.85f), false, e);
                        Prim(root, PrimitiveType.Cylinder, "BinderHole", bp + new Vector3(0.147f, -0.08f, 0f), new Vector3(0.024f, 0.002f, 0.024f), new Color(0.03f, 0.03f, 0.03f), new Vector3(0f, 0f, 90f));
                        z += lean ? bw + 0.05f : bw;
                    }
                }
            }
        }

        private void BuildSideboard(Transform root, int lv, Vector3 at)
        {
            var grey = new Color(0.42f, 0.44f, 0.46f);
            const float w = 1.5f, d = 0.45f, h = 0.8f;
            Box(root, "Sideboard", at + new Vector3(0f, h * 0.5f, 0f), new Vector3(d, h, w), grey, true, null, Surf.T(SecurityTextures.Kind.Metal, 0.8f, 0.3f, 0.5f));
            Box(root, "SideboardTop", at + new Vector3(-0.01f, h + 0.012f, 0f), new Vector3(d + 0.02f, 0.024f, w + 0.02f), new Color(0.3f, 0.3f, 0.32f), false, null, Surf.T(SecurityTextures.Kind.Laminate, 0.8f, 0.3f));
            float fx = at.x - d * 0.5f - 0.006f;
            foreach (float s in new[] { -1f, 1f })
            {
                Box(root, "SideboardDoor", new Vector3(fx, at.y + h * 0.5f, at.z + s * w * 0.25f), new Vector3(0.012f, h - 0.06f, w * 0.5f - 0.02f), grey * 1.08f, false, null, Surf.Shiny(0.35f, 0.5f));
                Box(root, "SideboardHandle", new Vector3(fx - 0.014f, at.y + h * 0.62f, at.z + s * 0.06f), new Vector3(0.016f, 0.14f, 0.016f), new Color(0.7f, 0.7f, 0.72f), false, null, Surf.Shiny(0.7f, 0.9f));
            }
            // Laser printer: body, paper tray in front, output tray with printed sheets, status LED.
            float top = at.y + h + 0.024f;
            Vector3 pr = new Vector3(at.x, top, at.z + 0.25f);
            var body = new Color(0.86f, 0.86f, 0.84f);
            MeshObj(root, "Printer", CachedMesh("Printer", p => p.At(new Vector3(0f, 0.12f, 0f)).RoundBox(new Vector3(0.4f, 0.24f, 0.42f), 0.02f, 3)), pr, Quaternion.identity, body, Surf.Shiny(0.4f));
            Box(root, "PrinterTray", pr + new Vector3(-0.24f, 0.05f, 0f), new Vector3(0.1f, 0.03f, 0.3f), new Color(0.3f, 0.3f, 0.32f), false, null, Surf.Shiny(0.35f));
            Box(root, "PrinterTop", pr + new Vector3(0.02f, 0.2405f, 0f), new Vector3(0.24f, 0.002f, 0.32f), new Color(0.15f, 0.15f, 0.16f), false);
            for (int i = 0; i < 4; i++)
                Box(root, "Printout", pr + new Vector3(0.03f, 0.243f + i * 0.0015f, 0f), new Vector3(0.21f, 0.001f, 0.297f), new Color(0.94f, 0.94f, 0.92f), false, new Vector3(0f, i * 3f - 4f, 0f));
            Led(root, "PrinterLed", pr + new Vector3(-0.201f, 0.2f, 0.15f), new Vector3(0.004f, 0.01f, 0.01f), new Color(0.2f, 1.6f, 0.3f));
            // Ream of paper and a stack of folders next to it.
            Box(root, "PaperReam", new Vector3(at.x, top + 0.028f, at.z - 0.2f), new Vector3(0.21f, 0.055f, 0.3f), new Color(0.95f, 0.95f, 0.93f), false, new Vector3(0f, 8f, 0f));
            Box(root, "ReamWrap", new Vector3(at.x, top + 0.028f, at.z - 0.2f), new Vector3(0.212f, 0.04f, 0.302f), new Color(0.2f, 0.4f, 0.75f), false, new Vector3(0f, 8f, 0f));
            for (int i = 0; i < 3; i++)
                Box(root, "Folder", new Vector3(at.x + 0.01f, top + 0.006f + i * 0.012f, at.z - 0.52f), new Vector3(0.24f, 0.01f, 0.32f), i == 1 ? new Color(0.85f, 0.75f, 0.3f) : new Color(0.3f, 0.5f, 0.7f), false, new Vector3(0f, i * 6f - 5f, 0f));
        }

        private void BuildJacket(Transform root, Vector3 rack)
        {
            var navy = new Color(0.06f, 0.07f, 0.12f);
            var fab = Surf.T(SecurityTextures.Kind.Fabric, 0.18f, 0.1f);
            // Hangs from the rack hook on the west side, back facing the room.
            Vector3 hook = rack + new Vector3(-0.08f, 1.72f, 0f);
            Box(root, "JacketHook", hook + new Vector3(0.04f, 0f, 0f), new Vector3(0.08f, 0.012f, 0.012f), new Color(0.6f, 0.6f, 0.6f), false, null, Surf.Shiny(0.7f, 0.9f));
            var body = CachedMesh("JacketBody", p => p
                .At(new Vector3(0f, -0.32f, 0f), Quaternion.identity).RoundBox(new Vector3(0.14f, 0.6f, 0.46f), 0.06f, 5)
                .At(new Vector3(0f, -0.04f, 0f), Quaternion.identity).RoundBox(new Vector3(0.12f, 0.08f, 0.2f), 0.035f, 3));
            MeshObj(root, "Jacket", body, hook + new Vector3(-0.06f, 0f, 0f), Quaternion.Euler(0f, 0f, 3f), navy, fab);
            foreach (float s in new[] { -1f, 1f })
            {
                var sleeve = CachedMesh("JacketSleeve" + s, p => p.Tube(new[] { new Vector3(0f, -0.1f, 0.2f * s), new Vector3(0.01f, -0.3f, 0.25f * s), new Vector3(0.025f, -0.62f, 0.22f * s) }, 0.055f, 10));
                MeshObj(root, "JacketSleeve", sleeve, hook + new Vector3(-0.06f, 0f, 0f), Quaternion.identity, navy * 0.9f, fab);
            }
            BakeStorageTextures();
            Sign(root, "JacketPrint", hook + new Vector3(-0.134f, -0.2f, 0f), new Vector2(0.34f, 0.12f), Vector3.left, _jacketPrint, 0.1f);
        }

        private void BuildCorkNotes(Transform root, int lv, Vector3 face)
        {
            BakeStorageTextures();
            var rnd = new System.Random(lv * 13 + 2);
            var items = new (Texture2D tex, Vector2 size, float x, float y)[]
            {
                (_photo, new Vector2(0.2f, 0.15f), -0.3f, 0.17f), (_noteA, new Vector2(0.12f, 0.15f), 0.05f, 0.2f),
                (_noteB, new Vector2(0.15f, 0.19f), 0.3f, 0.12f), (_photo, new Vector2(0.18f, 0.135f), -0.12f, -0.18f), (_noteB, new Vector2(0.13f, 0.16f), 0.4f, -0.2f),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var g = Sign(root, "CorkNote", face + new Vector3(it.x, it.y, 0.0005f * i), it.size, Vector3.forward, it.tex, 0.1f);
                g.transform.rotation = Quaternion.AngleAxis((float)rnd.NextDouble() * 12f - 6f, Vector3.forward) * g.transform.rotation;
                Prim(root, PrimitiveType.Sphere, "Pin", face + new Vector3(it.x, it.y + it.size.y * 0.42f, 0.006f), Vector3.one * 0.014f, i % 2 == 0 ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.1f, 0.3f, 0.8f));
            }
        }

        private static void BakeStorageTextures()
        {
            if (_jacketPrint == null)
            {
                var tb = new TextureBaker(256, 96, new Color(0.06f, 0.07f, 0.12f));
                tb.Text("OCHRONA", 0.5f, 0.5f, 0.55f, new Color(0.9f, 0.88f, 0.4f), TextAnchor.MiddleCenter, true, 0.94f);
                _jacketPrint = tb.Finish("JacketPrint", 0.25f, 25);
            }
            if (_noteA == null)
            {
                var ink = new Color(0.1f, 0.15f, 0.45f);
                var tb = new TextureBaker(128, 160, new Color(0.96f, 0.88f, 0.4f));
                tb.Text("ZMIANA\nNOCNA:\nKuba\nMarek", 0.1f, 0.6f, 0.13f, ink, TextAnchor.MiddleLeft, false, 0.85f);
                _noteA = tb.Finish("NoteA", 0.2f, 26);
                tb = new TextureBaker(128, 160, new Color(0.93f, 0.93f, 0.9f));
                for (int i = 0; i < 7; i++) tb.Rect(0.45f, 0.82f - i * 0.11f, 0.7f - (i % 3) * 0.12f, 0.025f, ink);
                tb.Text("!!", 0.85f, 0.15f, 0.15f, new Color(0.7f, 0.1f, 0.08f));
                _noteB = tb.Finish("NoteB", 0.2f, 27);
                tb = new TextureBaker(160, 120, new Color(0.25f, 0.27f, 0.26f));
                tb.Rect(0.5f, 0.35f, 1f, 0.3f, new Color(0.18f, 0.2f, 0.19f));
                tb.Rect(0.62f, 0.45f, 0.08f, 0.32f, new Color(0.08f, 0.08f, 0.09f));
                tb.Circle(0.62f, 0.67f, 0.05f, new Color(0.08f, 0.08f, 0.09f));
                tb.Text("CAM07 03:47", 0.04f, 0.9f, 0.1f, new Color(0.9f, 0.9f, 0.9f), TextAnchor.MiddleLeft);
                tb.Circle(0.62f, 0.55f, 0.2f, new Color(0.85f, 0.1f, 0.1f), 0.17f, new Color(0f, 0f, 0f, 0f));
                _photo = tb.Finish("CctvPrint", 0.3f, 28);
            }
        }

        private static Texture2D MakeBoxLabel()
        {
            var tb = new TextureBaker(160, 96, new Color(0.93f, 0.92f, 0.86f));
            tb.Frame(0.5f, 0.5f, 0.94f, 0.88f, 0.03f, new Color(0.2f, 0.2f, 0.2f));
            tb.Text("ARCHIWUM", 0.5f, 0.7f, 0.18f, new Color(0.15f, 0.15f, 0.15f));
            tb.Text("2019 - 2023", 0.5f, 0.35f, 0.16f, new Color(0.1f, 0.15f, 0.45f));
            return tb.Finish("BoxLabel", 0.2f, 29);
        }
    }
}
