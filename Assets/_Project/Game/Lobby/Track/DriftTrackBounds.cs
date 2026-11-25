using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Edges of the drift track: concrete jersey barriers in 3 m sections (red / white painted tops) round the inside
    /// of the pad with a gap at the gate, tyre walls in the barrier corners, a tall galvanised fence on the pad edge,
    /// and the brick entrance wall on the east side with the gate from the passage (a steel bollard in it keeps the
    /// karts on the track), the "TOR DRIFTOWY" lettering and the rolled-up shutter box. Colliders: long boxes.
    /// </summary>
    public static class DriftTrackBounds
    {
        private const float SegLen = 3f, GateHalf = 1.7f;

        public static void Build(Transform parent, Material brick)
        {
            var root = new GameObject("Bounds").transform;
            root.SetParent(parent, false);
            float w = DriftTrackLayout.PadWest + DriftTrackLayout.BarrierInset, e = DriftTrackLayout.PadEast - DriftTrackLayout.BarrierInset;
            float s = DriftTrackLayout.PadSouth + DriftTrackLayout.BarrierInset, n = DriftTrackLayout.PadNorth - DriftTrackLayout.BarrierInset;
            float gz = DriftTrackLayout.ArchZ;

            var concrete = new ProcMesh();
            var red = new ProcMesh();
            var white = new ProcMesh();
            int k = 0;
            void Run(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a;
                float len = d.magnitude;
                int count = Mathf.Max(1, Mathf.RoundToInt(len / SegLen));
                var rot = Quaternion.LookRotation(d / len);
                for (int i = 0; i < count; i++)
                {
                    Vector3 c = a + d * ((i + 0.5f) / count);
                    float l = len / count - 0.03f;
                    concrete.At(c + Vector3.up * 0.17f, rot).RoundBox(new Vector3(0.62f, 0.34f, l), 0.03f, 2);
                    concrete.At(c + Vector3.up * 0.46f, rot).RoundBox(new Vector3(0.36f, 0.26f, l), 0.03f, 2);
                    ((k++ & 1) == 0 ? red : white).At(c + Vector3.up * 0.69f, rot).RoundBox(new Vector3(0.22f, 0.22f, l), 0.025f, 2);
                }
                var mid = (a + b) * 0.5f;
                TrackMats.Wall(root, "BarrierCol", mid + Vector3.up * 0.5f, new Vector3(Mathf.Abs(d.x) + 0.62f, 1.0f, Mathf.Abs(d.z) + 0.62f));
            }
            Run(new Vector3(w, 0f, s), new Vector3(w, 0f, n));
            Run(new Vector3(w, 0f, n), new Vector3(e, 0f, n));
            Run(new Vector3(e, 0f, n), new Vector3(e, 0f, gz + GateHalf));
            Run(new Vector3(e, 0f, gz - GateHalf), new Vector3(e, 0f, s));
            Run(new Vector3(e, 0f, s), new Vector3(w, 0f, s));
            TrackMats.Piece(root, "Barriers", concrete.ToMesh("TrackBarriers"), TrackMats.Get("TrackConcrete", new Color(0.62f, 0.62f, 0.6f), 0.12f));
            TrackMats.Piece(root, "BarrierRed", red.ToMesh("TrackBarrierRed"), TrackMats.Get("TrackPaintRed", new Color(0.75f, 0.06f, 0.05f), 0.35f));
            TrackMats.Piece(root, "BarrierWhite", white.ToMesh("TrackBarrierWhite"), TrackMats.Get("TrackPaintWhite", new Color(0.9f, 0.9f, 0.88f), 0.35f));

            // Tyre walls in the barrier corners (in front of the concrete).
            var tyres = new ProcMesh();
            foreach (var (cx, cz, dx, dz) in new[] { (w, s, 1f, 1f), (w, n, 1f, -1f), (e, n, -1f, -1f), (e, s, -1f, 1f) })
                for (int i = 0; i < 7; i++)
                {
                    float a = i / 6f * Mathf.PI * 0.5f;
                    var p = new Vector3(cx + dx * (0.8f + (1f - Mathf.Sin(a)) * 2.4f), 0f, cz + dz * (0.8f + (1f - Mathf.Cos(a)) * 2.4f));
                    for (int h = 0; h < 3; h++) DriftTrackProps.Tyre(tyres, p + Vector3.up * (0.12f + h * 0.22f));
                    TrackMats.Wall(root, "TyreCol", p + Vector3.up * 0.35f, new Vector3(0.6f, 0.7f, 0.6f));
                }
            TrackMats.Piece(root, "TyreWalls", tyres.ToMesh("TrackTyreWalls"), DriftTrackProps.Rubber);

            Fence(root);
            EntranceWall(root, brick);
        }

        /// <summary>Galvanised fence on the pad edge (north, west, south), 4 m: posts, rails, close vertical wires.</summary>
        private static void Fence(Transform root)
        {
            float x0 = DriftTrackLayout.PadWest, x1 = DriftTrackLayout.PadEast, z0 = DriftTrackLayout.PadSouth, z1 = DriftTrackLayout.PadNorth;
            const float H = 4f;
            var m = new ProcMesh();
            void Side(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a;
                float len = d.magnitude;
                var rot = Quaternion.LookRotation(d / len);
                int posts = Mathf.RoundToInt(len / 4f);
                for (int i = 0; i <= posts; i++) m.At(a + d * (i / (float)posts) + Vector3.up * H * 0.5f).Box(Vector3.zero, new Vector3(0.09f, H, 0.09f));
                foreach (float y in new[] { 0.15f, 1.4f, 2.7f, H - 0.05f }) m.At((a + b) * 0.5f + Vector3.up * y, rot).Box(Vector3.zero, new Vector3(0.05f, 0.05f, len));
                int wires = Mathf.RoundToInt(len / 0.3f);
                for (int i = 0; i < wires; i++) m.At(a + d * ((i + 0.5f) / wires) + Vector3.up * H * 0.5f, rot).Box(Vector3.zero, new Vector3(0.012f, H - 0.1f, 0.012f));
                TrackMats.Wall(root, "FenceCol", (a + b) * 0.5f + Vector3.up * 3f, new Vector3(Mathf.Abs(d.x) + 0.3f, 6f, Mathf.Abs(d.z) + 0.3f));
            }
            Side(new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1));
            Side(new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0));
            Side(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0));
            TrackMats.Piece(root, "Fence", m.ToMesh("TrackFence"), TrackMats.Get("TrackGalvanised", new Color(0.5f, 0.52f, 0.54f), 0.55f, 0.6f));
        }

        /// <summary>East wall of the track (x = PadEast) with the gate, a bollard in it, the shutter box and the lettering.</summary>
        private static void EntranceWall(Transform root, Material brick)
        {
            float x = DriftTrackLayout.PadEast, gz = DriftTrackLayout.ArchZ, gw = DriftTrackLayout.PassageWidth * 0.5f, gh = DriftTrackLayout.PassageHeight;
            float z0 = DriftTrackLayout.PadSouth, z1 = DriftTrackLayout.PadNorth;
            const float H = 6f, T = 0.4f;
            var m = new ProcMesh();
            m.At(Vector3.zero).Box(new Vector3(x + T * 0.5f, H * 0.5f, (z0 + gz - gw) * 0.5f), new Vector3(T, H, gz - gw - z0));
            m.At(Vector3.zero).Box(new Vector3(x + T * 0.5f, H * 0.5f, (z1 + gz + gw) * 0.5f), new Vector3(T, H, z1 - gz - gw));
            m.At(Vector3.zero).Box(new Vector3(x + T * 0.5f, (gh + H) * 0.5f, gz), new Vector3(T, H - gh, gw * 2f));
            TrackMats.Piece(root, "EntranceWall", m.ToMesh("TrackEntranceWall"), brick);
            TrackMats.Wall(root, "EntranceColS", new Vector3(x + T * 0.5f, H * 0.5f, (z0 + gz - gw) * 0.5f), new Vector3(T, H, gz - gw - z0));
            TrackMats.Wall(root, "EntranceColN", new Vector3(x + T * 0.5f, H * 0.5f, (z1 + gz + gw) * 0.5f), new Vector3(T, H, z1 - gz - gw));

            var steel = TrackMats.Get("TrackSteelDark", new Color(0.22f, 0.23f, 0.25f), 0.5f, 0.7f);
            var yellow = TrackMats.Get("TrackPaintYellow", new Color(0.95f, 0.72f, 0.08f), 0.4f);
            var s = new ProcMesh();
            s.At(new Vector3(x - 0.25f, gh + 0.2f, gz), Quaternion.Euler(90f, 0f, 0f)).Lathe(new[] { new Vector2(0f, -gw), new Vector2(0.22f, -gw), new Vector2(0.22f, gw), new Vector2(0f, gw) }, 12);
            TrackMats.Piece(root, "ShutterBox", s.ToMesh("TrackShutterBox"), steel);
            // Bollard in the gate: people pass either side, a kart does not fit.
            var b = new ProcMesh();
            b.At(new Vector3(x - 0.3f, 0f, gz)).Lathe(new[] { new Vector2(0.1f, 0f), new Vector2(0.1f, 0.95f), new Vector2(0.06f, 1.02f), new Vector2(0f, 1.03f) }, 12);
            TrackMats.Piece(root, "Bollard", b.ToMesh("TrackBollard"), yellow);
            var bc = new GameObject("BollardCol").AddComponent<CapsuleCollider>();
            bc.transform.SetParent(root, false);
            bc.center = new Vector3(x - 0.3f, 0.6f, gz);
            bc.radius = 0.11f;
            bc.height = 1.2f;

            DriftTrackProps.Letters(root, "TOR DRIFTOWY", new Vector3(x - 0.02f, 4.6f, gz), Quaternion.Euler(0f, 90f, 0f), 0.11f, new Color(1f, 0.82f, 0.2f), new Vector2(9f, 1.3f));
        }
    }
}
