using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// The driver's door (left front) standing open: the same skin cells, window frame, glass, mirror and handle as a
    /// closed door, plus its shut faces and the inner trim (red cloth insert, armrest, pull, speaker, pocket), built in
    /// place and then swung round the hinge line.
    /// </summary>
    internal sealed partial class GolfWreck
    {
        private void DriverDoor()
        {
            int d0 = Mb.VertexCount;
            const float s = -1f;
            var card = Lib["Car_Interior"];
            EmitSide(Body, Mb, s, (i, j) =>
            {
                var c = CellMid(Body, i, j);
                if (!DoorCell(i, c)) return null;
                return i < BRound ? Paint : i == BRound ? Paint : card;
            });
            EmitSide(Cabin, Mb, s, (i, j) =>
            {
                float z = Mid(Cabin, j);
                if (z <= FG0 || z >= DF + 0.02f || i > CFrame - 1) return null;
                return i == 1 || i == 2 ? null : Trim;
            });
            EmitSide(Cabin, Mb, s, (i, j) =>
            {
                float z = Mid(Cabin, j);
                if (z <= FG0 || z >= DF + 0.02f || i > CFrame - 1) return null;
                return i == 1 || i == 2 ? null : Trim;
            }, true, 0.004f);
            foreach (bool inner in new[] { false, true })
                Cabin.PatchY(Mb, s, FG0, DF + 0.02f, t => YAt(Mathf.Lerp(FG0, DF + 0.02f, t), 0f, 1f), CSeal, CGlassTop, -Recess, Glass, 32, 4, inner);
            GlassRim(Frame(FG0, DF + 0.02f), s, new Vector2(0.4f, 0.5f));
            BeltMoulding(s, FG0 + 0.01f, DF - 0.03f);
            var (mp, _) = Side(0.8f, 0.95f, s);
            CarKit.Mirror(Mb, mp, s, Paint, 0.07f, 0.19f, 0.12f, 0.05f);
            var (hp, hn) = Side(DB + 0.12f, 0.87f, s);
            CarKit.Handle(Mb, hp, hn, Paint, 0.12f);
            DoorShutFaces();
            DoorTrim();
            // swing it open round the hinges (vertical line at the front edge of the skin)
            var hinge = new Vector3(-0.855f, 0f, DF - 0.012f);
            var rot = Quaternion.Euler(0f, DoorOpen, 0f);
            Mb.Warp(d0, p => hinge + rot * (p - hinge));
        }

        /// <summary>Front, rear and bottom faces of the door shell (painted shut faces).</summary>
        private void DoorShutFaces()
        {
            foreach (var (z, nz) in new[] { (DF - 0.001f, 1f), (DB + 0.001f, -1f) })
            {
                var sec = Body.SectionAt(z);
                Vector3 P(int k) => new Vector3(-sec[k].x, sec[k].y, z);
                Vector3 c = Vector3.zero;
                for (int k = BLower; k <= BDeck2; k++) c += P(k);
                c /= BDeck2 - BLower + 1;
                var n = new Vector3(0f, 0f, nz);
                for (int k = BLower; k < BDeck2; k++)
                    Mb.Tri(c, P(k), P(k + 1), n, n, n, Paint);
                Mb.Tri(c, P(BDeck2), P(BLower), n, n, n, Paint);
            }
            // bottom: from the skin's lower edge to the bottom of the inner card
            int j0 = 0;
            while (j0 < Body.Stations - 1 && Body.Z[j0] < DB) j0++;
            for (int j = j0; j < Body.Stations - 1 && Body.Z[j + 1] <= DF + 1e-4f; j++)
            {
                Vector3 A(int jj, int k) => new Vector3(-Body.S[jj][k].x, Body.S[jj][k].y, Body.Z[jj]);
                Mb.Quad(A(j, BLower), A(j + 1, BLower), A(j + 1, BDeck2), A(j, BDeck2), Vector3.down, Vector3.down, Vector3.down, Vector3.down, Paint);
            }
            // drain slots and the lock on the rear shut face
            Mb.Box(new Vector3(-0.79f, 0.78f, DB - 0.001f), new Vector3(0.03f, 0.07f, 0.004f), Lib["Wreck_Steel"]);
        }

        /// <summary>Inside of the door: GTI red cloth insert, armrest with the pull, chrome opener, speaker, pocket, lock pin.</summary>
        private void DoorTrim()
        {
            float x = -TubWall + 0.004f;
            var red = Lib["Wreck_SeatRed"];
            var dark = Lib["Car_Dash"];
            var right = Vector3.right;
            // cloth insert panel, slightly padded
            CarBlob.Blob(Mb, new Vector3(x + 0.004f, 0.72f, 0.36f), Quaternion.identity, new Vector3(0.012f, 0.1f, 0.33f), 0.25f, red, 0.25f, 1f, 12, 8);
            // armrest and pull handle
            CarBlob.Blob(Mb, new Vector3(x + 0.035f, 0.6f, 0.3f), Quaternion.Euler(0f, 0f, 8f), new Vector3(0.045f, 0.03f, 0.22f), 0.35f, dark, 0.4f, 1f, 12, 8);
            CarBlob.Bar(Mb, new Vector3(x + 0.03f, 0.64f, 0.12f), new Vector3(x + 0.06f, 0.7f, 0.05f), right, 0.025f, 0.02f, dark);
            // chrome opener in its recess
            CarKit.Lamp(Mb, new Vector3(x, 0.79f, 0.66f), right, Vector3.forward, CarKit.Rect(0.12f, 0.045f, 0.015f), Lib["Car_Seam"], dark, 0.002f, 0.01f);
            Mb.Box(new Vector3(x + 0.008f, 0.79f, 0.66f), new Vector3(0.012f, 0.018f, 0.09f), Lib["Car_ChromeBright"]);
            // speaker with its grille, pocket along the bottom
            CarWheels.Lathe(Mb, new Vector3(x, 0.43f, 0.62f), right, new[] { new Vector2(0.085f, 0f), new Vector2(0.085f, 0.008f), new Vector2(0.078f, 0.012f) }, 20, dark);
            CarWheels.Lathe(Mb, new Vector3(x + 0.011f, 0.43f, 0.62f), right, new[] { new Vector2(0.078f, 0f), new Vector2(0f, 0.004f) }, 20, Lib["Car_Grille"]);
            Mb.Box(new Vector3(x + 0.03f, 0.4f, 0.22f), new Vector3(0.05f, 0.1f, 0.4f), dark, null, MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegX);
            // window switch panel, lock pin on the door top
            Mb.Box(new Vector3(x + 0.07f, 0.62f, 0.42f), new Vector3(0.05f, 0.012f, 0.08f), Lib["Car_Gauge"]);
            Mb.Cylinder(new Vector3(-0.76f, 0.955f, -0.05f), new Vector3(-0.76f, 0.985f, -0.05f), 0.005f, 6, dark);
            // the red insert's edge piping
            Mb.Box(new Vector3(x + 0.016f, 0.62f, 0.36f), new Vector3(0.006f, 0.006f, 0.64f), dark);
        }
    }
}
