using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Extra detail for the lobby room: exit door with a lit sign, smoke detector, wall vent, banded crates, a lower
    /// shelf with magazines under the coffee table, piping on the sofa cushions, pot rims, arcade trim
    /// (sockets, switches and the chair rail: LobbyWalls / LobbyLights).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void BuildLobbyDetails(Transform root, Material darkWood, Material metal, Material trim)
        {
            var d = new GameObject("Details").transform;
            d.SetParent(root, false);
            var white = Mat("LobbyPlastic", new Color(0.9f, 0.9f, 0.86f), null, 0.35f, Vector2.one);
            var black = Mat("LobbyPlasticBlack", new Color(0.05f, 0.05f, 0.06f), null, 0.4f, Vector2.one);
            var doorWood = Mat("LobbyDoorWood", new Color(0.42f, 0.28f, 0.18f), Tex("lobby_wood", TexKind.Wood), 0.3f, new Vector2(0.6f, 1.2f));
            var doorPanel = darkWood;
            var doorFrame = trim;
            if (LobbyLayout.Garage) // painted steel door in a steel frame
            {
                doorWood = Mat("GarageSteelDoor", new Color(0.36f, 0.42f, 0.4f), Tex("garage_ceiling", TexKind.Concrete), 0.35f, new Vector2(1f, 2f), 0.35f);
                doorPanel = Mat("GarageSteelDoorPanel", new Color(0.3f, 0.35f, 0.33f), Tex("garage_ceiling", TexKind.Concrete), 0.35f, new Vector2(1f, 1f), 0.35f);
                doorFrame = Mat("GarageGateFrame", new Color(0.2f, 0.21f, 0.22f), null, 0.4f, Vector2.one, 0.5f);
            }
            var bandWood = Mat("LobbyCrateBand", new Color(0.4f, 0.28f, 0.16f), Tex("lobby_wood", TexKind.Wood), 0.15f, new Vector2(0.3f, 0.3f));

            // ---- exit door on the south wall (x 4.3): a real opening with a hinged door (opens into the room, easter egg
            // LobbyExitDoor), white nothingness behind it and an invisible blocker - nobody ever leaves.
            float dz = -LobbyLayout.HalfD + 0.015f, dx = LobbyLayout.ExitDoorX;
            CutExitOpening(root, dx);
            if (LobbyLayout.Garage) GarageWorldUv(root, new[] { "WallS_L", "WallS_R", "WallS_Top" }, LobbyLayout.W + 0.4f); // blocks keep their size on the cut pieces
            var hinge = new GameObject("ExitDoorHinge").transform;
            hinge.SetParent(d, false);
            hinge.position = new Vector3(dx - 0.475f, 0f, dz);
            Deco("DoorSlab", hinge, new Vector3(dx, 1.05f, dz), new Vector3(0.95f, 2.1f, 0.03f), doorWood);
            Deco("DoorPanelTop", hinge, new Vector3(dx, 1.5f, dz + 0.018f), new Vector3(0.7f, 0.75f, 0.01f), doorPanel);
            Deco("DoorPanelBottom", hinge, new Vector3(dx, 0.5f, dz + 0.018f), new Vector3(0.7f, 0.6f, 0.01f), doorPanel);
            Deco("DoorPanelTopOut", hinge, new Vector3(dx, 1.5f, dz - 0.018f), new Vector3(0.7f, 0.75f, 0.01f), doorPanel);
            Deco("DoorPanelBottomOut", hinge, new Vector3(dx, 0.5f, dz - 0.018f), new Vector3(0.7f, 0.6f, 0.01f), doorPanel);
            Deco("DoorFrameL", d, new Vector3(dx - 0.52f, 1.08f, dz + 0.01f), new Vector3(0.09f, 2.18f, 0.05f), doorFrame);
            Deco("DoorFrameR", d, new Vector3(dx + 0.52f, 1.08f, dz + 0.01f), new Vector3(0.09f, 2.18f, 0.05f), doorFrame);
            Deco("DoorFrameTop", d, new Vector3(dx, 2.15f, dz + 0.01f), new Vector3(1.13f, 0.09f, 0.05f), doorFrame);
            Cyl("Handle", hinge, new Vector3(dx + 0.36f, 1.0f, dz + 0.05f), 0.05f, 0.06f, metalSilver.Value, Quaternion.Euler(90, 0, 0));
            Deco("HandleLever", hinge, new Vector3(dx + 0.3f, 1.0f, dz + 0.08f), new Vector3(0.14f, 0.025f, 0.025f), metalSilver.Value).name = "HandleLever";
            Deco("KeyPlate", hinge, new Vector3(dx + 0.36f, 0.88f, dz + 0.02f), new Vector3(0.05f, 0.1f, 0.01f), metalSilver.Value);
            Deco("HandleLeverOut", hinge, new Vector3(dx + 0.3f, 1.0f, dz - 0.05f), new Vector3(0.14f, 0.025f, 0.025f), metalSilver.Value);
            hinge.gameObject.AddComponent<LobbyExitDoor>();
            // No "WYJŚCIE" sign over the door any more (2026-11-25, the user asked to remove it).

            // ---- light switch, outlets, smoke detector, wall vent

            Cyl("SmokeDetector", d, new Vector3(2.6f, RoomH - 0.02f, -0.4f), 0.13f, 0.04f, white);
            Cyl("SmokeLed", d, new Vector3(2.64f, RoomH - 0.045f, -0.4f), 0.012f, 0.01f, Emissive("LobbyLedRed", new Color(1f, 0.1f, 0.1f), new Color(1f, 0.1f, 0.1f) * 2f));
            Deco("VentFrame", d, new Vector3(-6.6f, 3.05f, 5.985f), new Vector3(0.7f, 0.3f, 0.03f), white);
            for (int i = 0; i < 6; i++)
                Deco("VentSlat", d, new Vector3(-6.6f, 2.94f + i * 0.045f, 5.965f), new Vector3(0.64f, 0.012f, 0.02f), black, Quaternion.Euler(30, 0, 0));

            // ---- crates: corner posts + top/bottom bands, stencil
            foreach (var name in new[] { "Crate1", "Crate2", "Crate3" })
            {
                var c = FindDeep(root, name);
                if (c == null) continue;
                for (int i = 0; i < 4; i++)
                {
                    var post = Deco("Post", c, Vector3.zero, Vector3.one, bandWood);
                    post.transform.localPosition = new Vector3(i % 2 == 0 ? -0.46f : 0.46f, 0f, i < 2 ? -0.46f : 0.46f);
                    post.transform.localRotation = Quaternion.identity;
                    post.transform.localScale = new Vector3(0.12f, 1.02f, 0.12f);
                }
                foreach (float y in new[] { -0.42f, 0.42f })
                {
                    var band = Deco("Band", c, Vector3.zero, Vector3.one, bandWood);
                    band.transform.localPosition = new Vector3(0f, y, 0f);
                    band.transform.localRotation = Quaternion.identity;
                    band.transform.localScale = new Vector3(1.03f, 0.1f, 1.03f);
                }
                var st = Label3D("Stencil", c, c.position, c.rotation * Quaternion.Euler(0, 180, 0), "HNS\nFRAGILE", new Color(0.1f, 0.06f, 0.03f, 0.85f), 0.01f, new Vector2(0.45f, 0.3f));
                st.transform.localPosition = new Vector3(0f, 0.05f, -0.505f);
                st.transform.localRotation = Quaternion.identity;
            }

            // ---- coffee table: lower shelf with magazines
            Deco("TableShelf", d, LobbyLayout.CoffeeTable + new Vector3(0f, 0.12f, 0f), new Vector3(1.44f, 0.03f, 0.78f), darkWood);
            var magCols = new[] { new Color(0.8f, 0.2f, 0.15f), new Color(0.15f, 0.4f, 0.7f), new Color(0.9f, 0.8f, 0.3f) };
            for (int i = 0; i < 3; i++)
                Deco("Magazine", d, LobbyLayout.CoffeeTable + new Vector3(-0.3f + i * 0.05f, 0.145f + i * 0.008f, -0.05f + i * 0.04f), new Vector3(0.22f, 0.008f, 0.3f),
                    Mat("LobbyMagazine" + i, magCols[i], null, 0.5f, Vector2.one), Quaternion.Euler(0, i * 14f - 10f, 0));

            // ---- sofas: piping along the cushion edges
            var piping = Mat("LobbySofaPiping", new Color(0.18f, 0.04f, 0.035f), null, 0.2f, Vector2.one);
            foreach (Transform t in root)
            {
                if (t.name != "Sofa") continue;
                foreach (float x in new[] { -0.53f, 0.53f })
                {
                    var p = Deco("Piping", t, Vector3.zero, Vector3.one, piping);
                    p.transform.localPosition = new Vector3(x, 0.53f, -0.43f);
                    p.transform.localRotation = Quaternion.identity;
                    p.transform.localScale = new Vector3(1.0f, 0.018f, 0.018f);
                }
                var feetMat = metal;
                foreach (var (fx, fz) in new[] { (-1.1f, -0.35f), (1.1f, -0.35f), (-1.1f, 0.35f), (1.1f, 0.35f) })
                {
                    var foot = Deco("Foot", t, Vector3.zero, Vector3.one, feetMat);
                    foot.transform.localPosition = new Vector3(fx, 0.01f, fz);
                    foot.transform.localRotation = Quaternion.identity;
                    foot.transform.localScale = new Vector3(0.08f, 0.02f, 0.08f);
                }
            }

        }

        /// <summary>Arcade cabinet (built facing -Z at <paramref name="a"/>): coloured T-moulding on the side edges, speaker grille.</summary>
        private static void ArcadeDetails(Transform arcade, Vector3 a, Material metal)
        {
            var black = Mat("LobbyPlasticBlack", new Color(0.05f, 0.05f, 0.06f), null, 0.4f, Vector2.one);
            var tm = Emissive("LobbyArcadeTMold", new Color(0.2f, 0.8f, 1f), new Color(0.1f, 0.5f, 0.8f) * 0.8f);
            foreach (float sx in new[] { -0.395f, 0.395f })
                Deco("TMold", arcade, a + new Vector3(sx, 0.95f, -0.37f), new Vector3(0.012f, 1.9f, 0.02f), tm);
            Deco("Speaker", arcade, a + new Vector3(0, 1.63f, -0.31f), new Vector3(0.4f, 0.07f, 0.01f), black);
            for (int i = 0; i < 8; i++)
                Deco("SpeakerHole", arcade, a + new Vector3(-0.17f + i * 0.048f, 1.63f, -0.316f), new Vector3(0.02f, 0.04f, 0.004f), metal);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

            /// <summary>
        /// Exit door opening in the south wall: the wall (and the skirting) are split around a 0.94 x 2.1 m hole, behind
        /// it a small unlit white box (pure white nothingness) and an invisible collider in the doorway.
        /// </summary>
        private static void CutExitOpening(Transform root, float dx)
        {
            const float x0 = -0.46f, x1 = 0.46f, top = 2.09f; // smaller than the 0.95 x 2.1 slab: closed, it overlaps the wall (no light leaks)
            foreach (string name in new[] { "WallS", "SkirtS" })
            {
                var w = root.Find(name);
                if (w == null) continue;
                var mr = w.GetComponent<MeshRenderer>();
                var mat = mr != null ? mr.sharedMaterial : null;
                Vector3 c = w.position, s = w.localScale;
                float left = c.x - s.x * 0.5f, right = c.x + s.x * 0.5f, bottom = c.y - s.y * 0.5f, up = c.y + s.y * 0.5f;
                float a = dx + x0, b = dx + x1;
                Box(name + "_L", root, new Vector3((left + a) * 0.5f, c.y, c.z), new Vector3(a - left, s.y, s.z), mat);
                Box(name + "_R", root, new Vector3((b + right) * 0.5f, c.y, c.z), new Vector3(right - b, s.y, s.z), mat);
                if (up > top) Box(name + "_Top", root, new Vector3(dx, (Mathf.Max(bottom, top) + up) * 0.5f, c.z), new Vector3(b - a, up - Mathf.Max(bottom, top), s.z), mat);
                Object.DestroyImmediate(w.gameObject);
            }
            // White nothingness behind the door (unlit, so it reads as a blinding white void).
            string wPath = $"{LobbyArtDir}/ExitVoidWhite.mat";
            var white = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(wPath);
            if (white == null)
            {
                white = new Material(Shader.Find("Unlit/Color")) { color = Color.white, name = "ExitVoidWhite" };
                UnityEditor.AssetDatabase.CreateAsset(white, wPath);
            }
            var voidRoot = new GameObject("ExitVoid").transform;
            voidRoot.SetParent(root, false);
            float z0 = -LobbyLayout.HalfD - 0.2f, z1 = -LobbyLayout.HalfD - 3.5f;
            void Q(string n, Vector3 p, Vector3 size)
            {
                var g = Box(n, voidRoot, p, size, white);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Q("VoidBack", new Vector3(dx, 1.5f, z1), new Vector3(4f, 4f, 0.05f));
            Q("VoidL", new Vector3(dx - 1.6f, 1.5f, (z0 + z1) * 0.5f), new Vector3(0.05f, 4f, z0 - z1));
            Q("VoidR", new Vector3(dx + 1.6f, 1.5f, (z0 + z1) * 0.5f), new Vector3(0.05f, 4f, z0 - z1));
            Q("VoidFloor", new Vector3(dx, -0.03f, (z0 + z1) * 0.5f), new Vector3(3.2f, 0.05f, z0 - z1));
            Q("VoidTop", new Vector3(dx, 3.2f, (z0 + z1) * 0.5f), new Vector3(3.2f, 0.05f, z0 - z1));
            // Nobody leaves: invisible wall in the doorway.
            var blocker = new GameObject("ExitBlocker");
            blocker.transform.SetParent(root, false);
            blocker.transform.position = new Vector3(dx, 1.5f, -LobbyLayout.HalfD - 0.12f);
            blocker.AddComponent<BoxCollider>().size = new Vector3(1.0f, 3f, 0.2f);
        }
    }
}
