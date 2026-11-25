using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// The arch in the lobby's west wall and the passage behind it (to the drift track, <see cref="DriftTrackLayout"/>):
    /// the plain wall box is replaced by a wall mesh with a round-headed opening (wallpaper on the room side in the
    /// same phase as before, brick on the passage side), framed by a dark wood architrave (plinths, pilasters, a round
    /// archivolt, keystone) with a brass "TOR DRIFTOWY" plaque above; the reveal is lined with wood. The passage is
    /// brick with an asphalt floor (its material is the track's asphalt: TrackArt.cs), a cage lamp and a closed roller
    /// shutter at the far end - the runtime track lifts it in the real lobby, the dead-lobby copy keeps it shut (no
    /// track there). The neon sign above the side sofa moves 0.8 m north to clear the arch.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void BuildLobbyArch(Transform root)
        {
            var wallW = root.Find("WallW");
            if (wallW == null) { Debug.LogWarning("[LobbyArch] WallW not found"); return; }
            var paper = wallW.GetComponent<MeshRenderer>().sharedMaterial;
            Object.DestroyImmediate(wallW.gameObject);

            var wood = Mat("LobbyDarkWood", new Color(0.55f, 0.4f, 0.3f), Tex("lobby_wood", TexKind.Wood), 0.5f, new Vector2(1, 1));
            var brick = Mat("TrackPassageBrick", new Color(0.78f, 0.62f, 0.55f), Tex("track_bricks", TexKind.Bricks), 0.08f, new Vector2(2f, 1.5f));
            var brass = Mat("TrackBrass", new Color(0.72f, 0.55f, 0.25f), null, 0.75f, Vector2.one, 0.8f);
            var concrete = Mat("TrackPassageConcrete", new Color(0.55f, 0.55f, 0.56f), Tex("track_concrete", TexKind.Concrete), 0.1f, new Vector2(2f, 2f));
            var steel = Mat("TrackSteel", new Color(0.42f, 0.44f, 0.47f), null, 0.55f, Vector2.one, 0.7f);

            ArchWall(root, paper, brick);
            ArchFrame(root, wood, -LobbyLayout.HalfW + 0.002f, 1f);
            ArchFrame(root, wood, DriftTrackLayout.PassageStart - 0.002f, -1f);
            ArchReveal(root, wood);
            ArchPlaque(root, brass);
            BuildPassage(root, brick, concrete, steel);

            // Neon above the side sofa: 0.8 m north, clear of the arch.
            foreach (var n in new[] { "Neon", "NeonGlow" })
            {
                var t = root.Find(n);
                if (t != null) t.position += new Vector3(0f, 0f, 0.8f);
            }
        }

        /// <summary>Arch outline height at <paramref name="z"/> (inside the opening).</summary>
        private static float ArchY(float z)
        {
            float r = DriftTrackLayout.ArchRadius, dz = z - DriftTrackLayout.ArchZ;
            return DriftTrackLayout.ArchSpring + Mathf.Sqrt(Mathf.Max(0f, r * r - dz * dz));
        }

        /// <summary>West wall with the opening: room face (wallpaper, the old cube's UV phase), passage face (brick), colliders.</summary>
        private static void ArchWall(Transform root, Material paper, Material brick)
        {
            float hd = LobbyLayout.HalfD, H = LobbyLayout.H, x0 = -LobbyLayout.HalfW, x1 = x0 - DriftTrackLayout.WallThick;
            float za = DriftTrackLayout.ArchZ - DriftTrackLayout.ArchRadius, zb = DriftTrackLayout.ArchZ + DriftTrackLayout.ArchRadius;
            float top = DriftTrackLayout.ArchTop;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var tRoom = new List<int>(); var tBack = new List<int>(); var tTop = new List<int>();

            void Face(float x, Vector3 normal, List<int> tris, float z0, float y0a, float y0b, float z1, float y1a, float y1b)
            {
                int b = v.Count;
                foreach (var (z, y) in new[] { (z0, y0a), (z0, y0b), (z1, y1b), (z1, y1a) })
                {
                    v.Add(new Vector3(x, y, z));
                    n.Add(normal);
                    uv.Add(new Vector2((z + hd) / LobbyLayout.D, y / H));
                }
                bool flip = normal.x < 0f;
                if (!flip) { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
                else { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
            }
            void Sides(float x, Vector3 normal, List<int> tris)
            {
                Face(x, normal, tris, -hd, 0f, H, za, 0f, H);
                Face(x, normal, tris, zb, 0f, H, hd, 0f, H);
                const int seg = 18;
                for (int i = 0; i < seg; i++)
                {
                    float z0 = Mathf.Lerp(za, zb, i / (float)seg), z1 = Mathf.Lerp(za, zb, (i + 1) / (float)seg);
                    Face(x, normal, tris, z0, ArchY(z0), H, z1, ArchY(z1), H);
                }
            }
            Sides(x0, Vector3.right, tRoom);
            Sides(x1, Vector3.left, tBack);
            // Top edge under the ceiling (seen from nowhere, keeps the mesh closed).
            int tb = v.Count;
            foreach (var p in new[] { new Vector3(x0, H, -hd), new Vector3(x1, H, -hd), new Vector3(x1, H, hd), new Vector3(x0, H, hd) }) { v.Add(p); n.Add(Vector3.up); uv.Add(Vector2.zero); }
            tTop.AddRange(new[] { tb, tb + 1, tb + 2, tb, tb + 2, tb + 3 });

            var mesh = new Mesh { name = "LobbyWallWArch" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            tRoom.AddRange(tTop);
            mesh.SetTriangles(tRoom, 0);
            mesh.SetTriangles(tBack, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var go = new GameObject("WallW");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "LobbyWallWArch");
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { paper, brick };
            go.isStatic = true;
            float xc = (x0 + x1) * 0.5f, th = DriftTrackLayout.WallThick;
            void Col(float zLo, float zHi, float yLo, float yHi)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = new Vector3(xc, (yLo + yHi) * 0.5f, (zLo + zHi) * 0.5f);
                bc.size = new Vector3(th, yHi - yLo, zHi - zLo);
            }
            Col(-hd, za, 0f, H);
            Col(zb, hd, 0f, H);
            Col(za, zb, top - 0.12f, H);
        }

        /// <summary>Architrave on one face (<paramref name="x"/> = wall face, <paramref name="dir"/> = +1 room side, -1 passage side).</summary>
        private static void ArchFrame(Transform root, Material wood, float x, float dir)
        {
            float z = DriftTrackLayout.ArchZ, r = DriftTrackLayout.ArchRadius, spring = DriftTrackLayout.ArchSpring;
            var m = new ProcMesh();
            for (int s = -1; s <= 1; s += 2)
            {
                float zc = z + s * (r + 0.055f);
                m.At(new Vector3(x + dir * 0.03f, 0.13f, zc)).RoundBox(new Vector3(0.06f, 0.26f, 0.17f), 0.01f, 2);                  // plinth
                m.At(new Vector3(x + dir * 0.022f, (0.26f + spring) * 0.5f, zc)).RoundBox(new Vector3(0.044f, spring - 0.26f, 0.12f), 0.012f, 2); // pilaster
                m.At(new Vector3(x + dir * 0.03f, spring, zc)).RoundBox(new Vector3(0.06f, 0.05f, 0.16f), 0.01f, 2);                // impost
            }
            m.At(new Vector3(x + dir * 0.02f, spring, z), Quaternion.LookRotation(Vector3.up, Vector3.right)).Torus(r + 0.055f, 0.042f, 28, 8, 0f, 180f);
            m.At(new Vector3(x + dir * 0.035f, spring + r + 0.06f, z)).RoundBox(new Vector3(0.07f, 0.2f, 0.13f), 0.012f, 2);     // keystone
            var go = new GameObject(dir > 0f ? "ArchFrame" : "ArchFramePassage");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m.ToMesh("ArchFrame"), dir > 0f ? "LobbyArchFrame" : "LobbyArchFramePassage");
            go.AddComponent<MeshRenderer>().sharedMaterial = wood;
            go.isStatic = true;
        }

        /// <summary>Wood lining of the opening's sides and arched head (through the wall's thickness).</summary>
        private static void ArchReveal(Transform root, Material wood)
        {
            float x = -LobbyLayout.HalfW - DriftTrackLayout.WallThick * 0.5f, z = DriftTrackLayout.ArchZ, r = DriftTrackLayout.ArchRadius;
            var m = new ProcMesh();
            for (int s = -1; s <= 1; s += 2)
                m.At(new Vector3(x, DriftTrackLayout.ArchSpring * 0.5f, z + s * (r - 0.012f))).Box(Vector3.zero, new Vector3(DriftTrackLayout.WallThick + 0.01f, DriftTrackLayout.ArchSpring, 0.024f));
            const int seg = 16;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI, a1 = (i + 1) / (float)seg * Mathf.PI, am = (a0 + a1) * 0.5f;
                float rr = r - 0.012f, len = 2f * rr * Mathf.Sin((a1 - a0) * 0.5f) + 0.004f;
                var c = new Vector3(x, DriftTrackLayout.ArchSpring + Mathf.Sin(am) * rr, z + Mathf.Cos(am) * rr);
                m.At(c, Quaternion.Euler(-(am * Mathf.Rad2Deg - 90f), 0f, 0f)).Box(Vector3.zero, new Vector3(DriftTrackLayout.WallThick + 0.01f, 0.024f, len));
            }
            var go = new GameObject("ArchReveal");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m.ToMesh("ArchReveal"), "LobbyArchReveal");
            go.AddComponent<MeshRenderer>().sharedMaterial = wood;
            go.isStatic = true;
        }

        private static void ArchPlaque(Transform root, Material brass)
        {
            float x = -LobbyLayout.HalfW;
            var p = Deco("ArchPlaque", root, new Vector3(x + 0.012f, DriftTrackLayout.ArchTop + 0.36f, DriftTrackLayout.ArchZ), new Vector3(0.02f, 0.16f, 0.78f), brass);
            RoundCube(p.transform, 0.008f, 0f);
            var t = new GameObject("ArchPlaqueText").AddComponent<TextMesh>();
            t.transform.SetParent(root, false);
            t.transform.position = new Vector3(x + 0.024f, DriftTrackLayout.ArchTop + 0.36f, DriftTrackLayout.ArchZ);
            t.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            t.text = "TOR DRIFTOWY";
            t.characterSize = 0.02f;
            t.fontSize = 64;
            t.fontStyle = FontStyle.Bold;
            t.anchor = TextAnchor.MiddleCenter;
            t.color = new Color(0.16f, 0.1f, 0.04f);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            AddWorldText(t, new Vector2(0.7f, 0.12f));
        }
    }
}
