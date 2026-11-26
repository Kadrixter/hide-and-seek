using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// The way to the drift track in the garage look (<see cref="LobbyLayout.Garage"/>, replaces the arch of
    /// MultiplayerBuilder.LobbyArch): the west wall gets a square opening (<see cref="DriftTrackLayout.GateWidth"/> x
    /// <see cref="DriftTrackLayout.GateHeight"/>) with a steel frame and hazard-striped jambs; behind it, on the
    /// passage side, a five-section garage door hangs half open - the bottom section still in the opening down to
    /// <see cref="DriftTrackLayout.GateDoorBottom"/> (you walk under it), the next one on the curve of the tracks, the
    /// rest lying flat under the passage ceiling. Torsion spring, opener rail and motor, a pull rope, yellow bollards
    /// on both sides and hazard tape on the floor. The passage itself (brick, asphalt, the shutter at the track end)
    /// is the classic one: BuildPassage.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static float GateZa => DriftTrackLayout.ArchZ - DriftTrackLayout.GateWidth * 0.5f;
        private static float GateZb => DriftTrackLayout.ArchZ + DriftTrackLayout.GateWidth * 0.5f;

        private static void BuildLobbyGarageDoor(Transform root)
        {
            var wallW = root.Find("WallW");
            if (wallW == null) { Debug.LogWarning("[GarageDoor] WallW not found"); return; }
            var paper = wallW.GetComponent<MeshRenderer>().sharedMaterial;
            Object.DestroyImmediate(wallW.gameObject);

            var brick = Mat("TrackPassageBrick", new Color(0.78f, 0.62f, 0.55f), Tex("track_bricks", TexKind.Bricks), 0.08f, new Vector2(2f, 1.5f));
            var concrete = Mat("TrackPassageConcrete", new Color(0.55f, 0.55f, 0.56f), Tex("track_concrete", TexKind.Concrete), 0.1f, new Vector2(2f, 2f));
            var steel = Mat("TrackSteel", new Color(0.42f, 0.44f, 0.47f), null, 0.55f, Vector2.one, 0.7f);

            GateWall(root, paper, brick);
            var g = new GameObject("GarageGate").transform;
            g.SetParent(root, false);
            GateFrame(g);
            GateDoor(g);
            GateBollards(g);
            BuildPassage(root, brick, concrete, steel, new Vector2(-10.65f, DriftTrackLayout.ArchZ + 0.4f));

            // Neon above the side sofa: clear of the door frame.
            foreach (var n in new[] { "Neon", "NeonGlow" })
            {
                var t = root.Find(n);
                if (t != null) t.position += new Vector3(0f, 0f, 0.95f);
            }
        }

        /// <summary>West wall with the square opening: room face (block wall), passage face (brick), colliders.</summary>
        private static void GateWall(Transform root, Material wallMat, Material brick)
        {
            float hd = LobbyLayout.HalfD, H = LobbyLayout.H, x0 = -LobbyLayout.HalfW, x1 = x0 - DriftTrackLayout.WallThick;
            float za = GateZa, zb = GateZb, top = DriftTrackLayout.GateHeight;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var tRoom = new List<int>(); var tBack = new List<int>();
            void Face(float x, Vector3 normal, List<int> tris, float z0, float z1, float y0, float y1)
            {
                int b = v.Count;
                foreach (var (z, y) in new[] { (z0, y0), (z0, y1), (z1, y1), (z1, y0) })
                {
                    v.Add(new Vector3(x, y, z));
                    n.Add(normal);
                    uv.Add(new Vector2((z + hd) / LobbyLayout.D, y / H));
                }
                if (normal.x > 0f) { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
                else { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
            }
            foreach (var (x, normal, tris) in new[] { (x0, Vector3.right, tRoom), (x1, Vector3.left, tBack) })
            {
                Face(x, normal, tris, -hd, za, 0f, H);
                Face(x, normal, tris, zb, hd, 0f, H);
                Face(x, normal, tris, za, zb, top, H);
            }
            var mesh = new Mesh { name = "LobbyWallWGate" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tRoom, 0);
            mesh.SetTriangles(tBack, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var go = new GameObject("WallW");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "LobbyWallWGate");
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { wallMat, brick };
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
            Col(za, zb, top, H);
        }

        /// <summary>Steel angle frame round the opening on the room side, lined reveal, hazard stripes on the jambs and the floor.</summary>
        private static void GateFrame(Transform g)
        {
            float x0 = -LobbyLayout.HalfW, th = DriftTrackLayout.WallThick, top = DriftTrackLayout.GateHeight;
            float za = GateZa, zb = GateZb, zc = DriftTrackLayout.ArchZ, w = DriftTrackLayout.GateWidth;
            var frame = Mat("GarageGateFrame", new Color(0.2f, 0.21f, 0.22f), null, 0.4f, Vector2.one, 0.5f);
            var reveal = Mat("GarageGateReveal", new Color(0.45f, 0.45f, 0.44f), Tex("garage_ceiling", TexKind.Concrete), 0.08f, new Vector2(0.3f, 1f));
            // reveal: the cut through the wall
            foreach (float z in new[] { za + 0.005f, zb - 0.005f })
                Box("GateReveal", g, new Vector3(x0 - th * 0.5f, top * 0.5f, z), new Vector3(th, top, 0.01f), reveal);
            Box("GateRevealHead", g, new Vector3(x0 - th * 0.5f, top - 0.005f, zc), new Vector3(th, 0.01f, w), reveal);
            // angle frame: 6 cm flange on the wall face + 5 cm leg into the reveal
            foreach (float s in new[] { -1f, 1f })
            {
                float z = zc + s * (w * 0.5f + 0.03f);
                Deco("GateJamb", g, new Vector3(x0 + 0.006f, top * 0.5f, z), new Vector3(0.012f, top, 0.06f), frame);
                Deco("GateJambLeg", g, new Vector3(x0 - 0.025f, top * 0.5f, zc + s * (w * 0.5f - 0.006f)), new Vector3(0.05f, top, 0.012f), frame);
                // hazard stripes on the lower 1.2 m of the jamb face and the leg
                Deco("GateJambHazard", g, new Vector3(x0 + 0.0125f, 0.6f, z), new Vector3(0.002f, 1.2f, 0.058f), HazardMat("GarageHazardJamb", new Vector2(1f, 5f)));
                Deco("GateLegHazard", g, new Vector3(x0 - 0.025f, 0.6f, zc + s * (w * 0.5f - 0.0125f)), new Vector3(0.048f, 1.2f, 0.002f), HazardMat("GarageHazardJamb", new Vector2(1f, 5f)));
                // anchor bolts
                for (int k = 0; k < 4; k++)
                    Cyl("JambBolt", g, new Vector3(x0 + 0.014f, 0.35f + k * 0.65f, z), 0.016f, 0.008f, GarageSteel, Quaternion.Euler(0f, 0f, 90f));
            }
            Deco("GateHead", g, new Vector3(x0 + 0.006f, top + 0.03f, zc), new Vector3(0.012f, 0.06f, w + 0.12f), frame);
            Deco("GateHeadLeg", g, new Vector3(x0 - 0.025f, top - 0.006f, zc), new Vector3(0.05f, 0.012f, w), frame);
            // floor: hazard tape in front of the opening + a steel threshold strip in the wall
            Deco("GateFloorHazard", g, new Vector3(x0 + 0.09f, 0.0015f, zc), new Vector3(0.1f, 0.003f, w + 0.1f), HazardMat("GarageHazardFloor", new Vector2(1f, 10f)));
            Deco("GateThreshold", g, new Vector3(x0 - th * 0.5f, 0.002f, zc), new Vector3(th, 0.004f, w), GarageSteel);
        }

        /// <summary>The sectional door, half open, its tracks, spring, opener and pull rope (all on the passage side).</summary>
        private static void GateDoor(Transform g)
        {
            float xw = -LobbyLayout.HalfW - DriftTrackLayout.WallThick;  // passage face of the wall
            float xd = xw - 0.045f;                                        // door plane
            float zc = DriftTrackLayout.ArchZ, dw = DriftTrackLayout.GateWidth + 0.1f, top = DriftTrackLayout.GateHeight;
            float bottom = DriftTrackLayout.GateDoorBottom;
            const int sections = 5;
            float sh = (top + 0.02f) / sections, t = 0.042f;
            const float bendR = 0.26f;
            float bendY = top + 0.03f, flatY = bendY + bendR;                 // the tracks curve over from vertical to flat here

            var panel = Mat("GarageDoorPanel", new Color(0.92f, 0.92f, 0.9f), GarageDoorTex(), 0.35f, Vector2.one, 0.15f);
            var rubber = Mat("GarageRubber", new Color(0.04f, 0.04f, 0.04f), null, 0.2f, Vector2.one);
            var door = new GameObject("GarageDoor").transform;
            door.SetParent(g, false);

            // Section positions along the track path (s = 0 at the bottom edge): vertical, the bend, then flat going west.
            Vector3 PathAt(float s)
            {
                float vertLen = bendY - bottom;
                if (s <= vertLen) return new Vector3(xd, bottom + s, zc);
                float arc = bendR * Mathf.PI * 0.5f;
                if (s <= vertLen + arc)
                {
                    float a = (s - vertLen) / bendR;
                    return new Vector3(xd - bendR + Mathf.Cos(a) * bendR, bendY + Mathf.Sin(a) * bendR, zc);
                }
                return new Vector3(xd - bendR - (s - vertLen - arc), flatY, zc);
            }
            for (int i = 0; i < sections; i++)
            {
                Vector3 a = PathAt(i * sh), b = PathAt((i + 1) * sh - 0.004f);
                Vector3 dir = (b - a).normalized;
                var sec = LocalBox(door, "DoorSection" + i, (a + b) * 0.5f, new Vector3(t, (b - a).magnitude, dw), panel);
                // section local Y along the path, local X = the panel normal (towards the lobby while vertical)
                sec.transform.localRotation = Quaternion.LookRotation(Vector3.forward, dir);
                sec.isStatic = true;
                // hinges and rollers at the section joints
                foreach (float s in new[] { -1f, 1f })
                {
                    var roller = Cyl("DoorRoller", door, a + new Vector3(0f, 0f, s * (dw * 0.5f + 0.01f)), 0.05f, 0.016f, GarageSteel, Quaternion.Euler(90f, 0f, 0f));
                    roller.isStatic = true;
                }
            }
            // bottom: rubber seal, lift handle and step plate on the lobby face, the pull rope
            LocalBox(door, "DoorSeal", new Vector3(xd, bottom - 0.008f, zc), new Vector3(t + 0.01f, 0.016f, dw - 0.02f), rubber);
            LocalBox(door, "DoorHandleBar", new Vector3(xd + t * 0.5f + 0.03f, bottom + 0.15f, zc), new Vector3(0.016f, 0.024f, 0.32f), GarageSteel);
            foreach (float s in new[] { -1f, 1f })
                LocalBox(door, "DoorHandlePost", new Vector3(xd + t * 0.5f + 0.015f, bottom + 0.15f, zc + s * 0.15f), new Vector3(0.03f, 0.024f, 0.02f), GarageSteel);
            LocalBox(door, "DoorLockPlate", new Vector3(xd + t * 0.5f + 0.002f, bottom + 0.3f, zc + 0.6f), new Vector3(0.004f, 0.12f, 0.08f), GarageSteel);
            Cyl("DoorLock", door, new Vector3(xd + t * 0.5f + 0.008f, bottom + 0.3f, zc + 0.6f), 0.035f, 0.012f, Mat("LobbySilver", new Color(0.78f, 0.78f, 0.8f), null, 0.8f, Vector2.one, 0.9f), Quaternion.Euler(0f, 0f, 90f));
            var rope = Mat("GarageRope", new Color(0.62f, 0.08f, 0.06f), Tex("lobby_fabric", TexKind.Fabric), 0.1f, new Vector2(1f, 6f));
            float ropeZ = zc - 0.62f, ropeTop = bottom + 0.02f, ropeBottom = 1.38f;
            Cyl("PullRope", door, new Vector3(xd + t * 0.5f + 0.012f, (ropeTop + ropeBottom) * 0.5f, ropeZ), 0.012f, ropeTop - ropeBottom, rope);
            Cyl("PullRopeGrip", door, new Vector3(xd + t * 0.5f + 0.012f, ropeBottom - 0.05f, ropeZ), 0.03f, 0.1f, Mat("GarageRubber", new Color(0.04f, 0.04f, 0.04f), null, 0.2f, Vector2.one));
            // collider: the hanging part of the door (you pass under it)
            var col = door.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(xd, (bottom + top) * 0.5f, zc);
            col.size = new Vector3(t + 0.03f, top - bottom, dw);

            // tracks: vertical angle, the bend, horizontal angle hung from the ceiling
            var tracks = new ProcMesh();
            float flatEnd = xd - bendR - sections * sh - 0.15f;
            foreach (float s in new[] { -1f, 1f })
            {
                float z = zc + s * (dw * 0.5f + 0.03f);
                tracks.At(Vector3.zero).Box(new Vector3(xd, bendY * 0.5f, z), new Vector3(0.07f, bendY, 0.012f));
                tracks.At(Vector3.zero).Box(new Vector3(xw - 0.006f, bendY * 0.5f, z + s * 0.02f), new Vector3(0.012f, bendY, 0.05f)); // mounting flange on the wall
                const int seg = 8;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k / (float)seg * Mathf.PI * 0.5f, a1 = (k + 1) / (float)seg * Mathf.PI * 0.5f, am = (a0 + a1) * 0.5f;
                    var c = new Vector3(xd - bendR + Mathf.Cos(am) * bendR, bendY + Mathf.Sin(am) * bendR, z);
                    tracks.At(c, Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg)).Box(Vector3.zero, new Vector3(0.07f, 2f * bendR * Mathf.Sin((a1 - a0) * 0.5f) + 0.004f, 0.012f));
                }
                tracks.At(Vector3.zero).Box(new Vector3((xd - bendR + flatEnd) * 0.5f, flatY, z), new Vector3(xd - bendR - flatEnd, 0.07f, 0.012f));
                // hangers to the passage ceiling
                foreach (float hx in new[] { flatEnd + 0.2f, (xd - bendR + flatEnd) * 0.5f })
                    tracks.At(Vector3.zero).Box(new Vector3(hx, (flatY + DriftTrackLayout.PassageHeight) * 0.5f, z), new Vector3(0.03f, DriftTrackLayout.PassageHeight - flatY, 0.006f));
            }
            // torsion spring shaft over the opening, drums at both ends
            float shaftY = DriftTrackLayout.PassageHeight - 0.05f, shaftX = xw - 0.09f;
            tracks.At(new Vector3(shaftX, shaftY, zc), Quaternion.Euler(90f, 0f, 0f))
                .Lathe(new[] { new Vector2(0f, -dw * 0.5f - 0.02f), new Vector2(0.012f, -dw * 0.5f - 0.02f), new Vector2(0.012f, dw * 0.5f + 0.02f), new Vector2(0f, dw * 0.5f + 0.02f) }, 10, 10f);
            foreach (float s in new[] { -1f, 1f })
                tracks.At(new Vector3(shaftX, shaftY, zc + s * (dw * 0.5f - 0.02f)), Quaternion.Euler(90f, 0f, 0f))
                    .Lathe(new[] { new Vector2(0f, -0.025f), new Vector2(0.045f, -0.025f), new Vector2(0.045f, 0.025f), new Vector2(0f, 0.025f) }, 16, 10f);
            var tg = new GameObject("DoorTracks");
            tg.transform.SetParent(g, false);
            tg.AddComponent<MeshFilter>().sharedMesh = SaveMesh(tracks.ToMesh("GarageDoorTracks"), "GarageDoorTracks");
            tg.AddComponent<MeshRenderer>().sharedMaterial = GarageSteel;
            tg.isStatic = true;
            // the spring coil itself (rusty), left of the centre
            var coil = new ProcMesh();
            for (int k = 0; k < 22; k++)
                coil.At(new Vector3(shaftX, shaftY, zc - 0.55f + k * 0.022f), Quaternion.Euler(90f, 0f, 0f)).Torus(0.032f, 0.008f, 14, 6);
            var cg = new GameObject("DoorSpring");
            cg.transform.SetParent(g, false);
            cg.AddComponent<MeshFilter>().sharedMesh = SaveMesh(coil.ToMesh("GarageDoorSpring"), "GarageDoorSpring");
            cg.AddComponent<MeshRenderer>().sharedMaterial = Mat("GarageRust", new Color(0.36f, 0.2f, 0.12f), null, 0.3f, Vector2.one, 0.5f);

            // opener: rail along the passage ceiling, motor at its far end, arm to the top section
            float railY = DriftTrackLayout.PassageHeight - 0.04f, motorX = flatEnd - 0.35f;
            Box("OpenerRail", g, new Vector3((xw - 0.2f + motorX) * 0.5f, railY, zc), new Vector3(xw - 0.2f - motorX, 0.035f, 0.04f), GarageSteel);
            var motor = Box("OpenerMotor", g, new Vector3(motorX, railY - 0.1f, zc), new Vector3(0.42f, 0.17f, 0.32f), Mat("GarageOpener", new Color(0.78f, 0.78f, 0.74f), null, 0.35f, Vector2.one));
            Object.DestroyImmediate(motor.GetComponent<Collider>());
            Deco("OpenerLed", g, new Vector3(motorX + 0.212f, railY - 0.1f, zc + 0.1f), new Vector3(0.004f, 0.015f, 0.015f), Emissive("GarageOpenerLed", new Color(0.2f, 1f, 0.3f), new Color(0.2f, 1f, 0.3f) * 2f));
            Vector3 topSec = PathAt(sections * sh);
            Deco("OpenerArm", g, new Vector3((topSec.x + topSec.x - 0.3f) * 0.5f, (flatY + railY) * 0.5f, zc), new Vector3(0.32f, 0.025f, 0.025f), GarageSteel,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(railY - flatY, 0.3f) * Mathf.Rad2Deg));
        }

        /// <summary>Yellow steel bollards on both sides of the opening (room side), black bands, reflective strip.</summary>
        private static void GateBollards(Transform g)
        {
            var yellow = Mat("GarageBollard", new Color(0.9f, 0.68f, 0.06f), Tex("garage_ceiling", TexKind.Concrete), 0.35f, new Vector2(1f, 1f), 0.2f);
            var black = Mat("GarageBollardBand", new Color(0.05f, 0.05f, 0.05f), null, 0.3f, Vector2.one);
            var white = Mat("GarageReflector", new Color(0.9f, 0.9f, 0.9f), null, 0.85f, Vector2.one, 0.2f);
            foreach (float z in new[] { GateZa - 0.32f, GateZb + 0.32f })
            {
                var p = new Vector3(-LobbyLayout.HalfW + 0.34f, 0f, z);
                var post = Cyl("Bollard", g, p + new Vector3(0f, 0.5f, 0f), 0.14f, 1.0f, yellow, null, true);
                post.isStatic = true;
                Cyl("BollardCap", g, p + new Vector3(0f, 1.01f, 0f), 0.125f, 0.03f, yellow);
                foreach (float y in new[] { 0.62f, 0.82f })
                    Cyl("BollardBand", g, p + new Vector3(0f, y, 0f), 0.142f, 0.07f, black);
                Cyl("BollardReflector", g, p + new Vector3(0f, 0.72f, 0f), 0.141f, 0.04f, white);
                Cyl("BollardBase", g, p + new Vector3(0f, 0.01f, 0f), 0.22f, 0.02f, GarageSteel);
            }
        }
    }
}
