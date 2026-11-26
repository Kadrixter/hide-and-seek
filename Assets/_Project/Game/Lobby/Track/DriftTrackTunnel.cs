using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Structures over the circuit (<see cref="DriftTrackCircuit"/>): the ~90 m tunnel on the north run (concrete box
    /// round the road with the two kinks inside, a grass roof, portals with a hazard band and "TUNEL" over the entry,
    /// warm emissive strip lamps under the roof and only three real point lights, no shadows; the tunnel mesh is its own
    /// collider) and the start / finish gantry over the straight ("META" towards the coming karts, "START" on the back).
    /// </summary>
    public static class DriftTrackTunnel
    {
        private const float Wall = 0.6f, Ground = -0.08f;

        public static void Build(Transform parent)
        {
            var root = new GameObject("Structures").transform;
            root.SetParent(parent, false);
            Tunnel(root);
            Gantry(root);
        }

        private static void Tunnel(Transform root)
        {
            var tun = DriftTrackCircuit.Tunnel;
            int n = tun.Length, first = -1, last = -1;
            for (int i = 0; i < n; i++)
                if (tun[i]) { if (first < 0) first = i; last = i; }
            if (first < 0) return;

            const float H = DriftTrackCircuit.TunnelHeight, W = DriftTrackCircuit.TunnelHalf, Wo = W + Wall, Ho = H + Wall;
            var concrete = new MeshBuf();
            var grass = new MeshBuf();
            var lamps = new ProcMesh();
            for (int i = first; i < last; i++)
            {
                int j = i + 1;
                Vector3 ci = DriftTrackCircuit.Center[i], cj = DriftTrackCircuit.Center[j];
                Vector3 ri = DriftTrackRoad.Flat(DriftTrackCircuit.Right[i]), rj = DriftTrackRoad.Flat(DriftTrackCircuit.Right[j]);
                foreach (int side in new[] { -1, 1 })
                {
                    // inner face (towards the road), outer face
                    concrete.Quad(G(ci + ri * side * W), G(cj + rj * side * W), cj + rj * side * W + Vector3.up * H, ci + ri * side * W + Vector3.up * H, -ri * side);
                    concrete.Quad(G(ci + ri * side * Wo), G(cj + rj * side * Wo), cj + rj * side * Wo + Vector3.up * Ho, ci + ri * side * Wo + Vector3.up * Ho, ri * side);
                }
                concrete.Quad(ci - ri * W + Vector3.up * H, ci + ri * W + Vector3.up * H, cj + rj * W + Vector3.up * H, cj - rj * W + Vector3.up * H, Vector3.down);
                grass.Quad(ci - ri * Wo + Vector3.up * Ho, ci + ri * Wo + Vector3.up * Ho, cj + rj * Wo + Vector3.up * Ho, cj - rj * Wo + Vector3.up * Ho, Vector3.up);
                if ((i - first) % 4 == 1)
                {
                    var f = DriftTrackRoad.Flat(DriftTrackCircuit.Fwd[i]);
                    foreach (float lat in new[] { -3.2f, 3.2f })
                        lamps.At(ci + ri * lat + Vector3.up * (H - 0.04f), Quaternion.LookRotation(f)).Box(Vector3.zero, new Vector3(0.28f, 0.06f, 2.2f));
                }
            }
            // Portals: the frame faces and a deeper concrete header with a hazard band.
            var band = new MeshBuf();
            foreach (var (row, dir) in new[] { (first, -1f), (last, 1f) })
            {
                Vector3 c = DriftTrackCircuit.Center[row], r = DriftTrackRoad.Flat(DriftTrackCircuit.Right[row]), f = DriftTrackRoad.Flat(DriftTrackCircuit.Fwd[row]) * dir;
                foreach (int side in new[] { -1, 1 })
                    concrete.Quad(G(c + r * side * W), G(c + r * side * Wo), c + r * side * Wo + Vector3.up * Ho, c + r * side * W + Vector3.up * Ho, f);
                concrete.Quad(c - r * W + Vector3.up * H, c + r * W + Vector3.up * H, c + r * W + Vector3.up * Ho, c - r * W + Vector3.up * Ho, f);
                var rot = Quaternion.LookRotation(f);
                Vector3 front = c + f * 0.4f;
                concrete.Box(front + Vector3.up * (H + 1.05f), rot, new Vector3(Wo * 2f + 1.2f, 2.1f, 0.8f));
                foreach (int side in new[] { -1, 1 })
                    concrete.Box(G(front + r * side * (Wo + 0.1f)) + Vector3.up * (H * 0.5f + 0.04f), rot, new Vector3(1.4f, H + 0.1f, 0.8f));
                for (int k = 0; k < 12; k++) // yellow / black hazard band along the header's foot
                {
                    float lat = -W + (k + 0.5f) * (W * 2f / 12f);
                    if ((k & 1) == 0) band.Box(front + f * 0.41f + r * lat + Vector3.up * (H + 0.2f), rot, new Vector3(W * 2f / 12f, 0.4f, 0.02f));
                }
                if (dir < 0f) // the entry, seen by the coming karts
                    DriftTrackProps.Letters(root, "TUNEL", front + f * 0.42f + Vector3.up * (H + 1.25f), Quaternion.LookRotation(-f), 0.5f, new Color(0.95f, 0.92f, 0.85f), new Vector2(10f, 1.4f));
            }
            var mesh = concrete.ToMesh("TrackTunnel");
            var go = TrackMats.Piece(root, "Tunnel", mesh, TrackMats.Get("TrackConcreteWall", new Color(0.46f, 0.46f, 0.44f), 0.1f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            TrackMats.Piece(root, "TunnelRoof", grass.ToMesh("TrackTunnelRoof"), DriftTrackPad.Grass);
            TrackMats.Piece(root, "TunnelBand", band.ToMesh("TrackTunnelBand"), TrackMats.Get("TrackPaintYellow", new Color(0.95f, 0.72f, 0.08f), 0.4f), false);
            TrackMats.Piece(root, "TunnelLamps", lamps.ToMesh("TrackTunnelLamps"), TrackMats.Glow("TrackTunnelLampGlow", new Color(1f, 0.85f, 0.6f), new Color(1.6f, 1.15f, 0.65f)), false);

            // Three warm lights along the tube (no shadows): the strips do the rest.
            for (int k = 0; k < 3; k++)
            {
                int row = Mathf.RoundToInt(Mathf.Lerp(first, last, (k + 0.5f) / 3f));
                var l = new GameObject("TunnelLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = DriftTrackCircuit.Center[row] + Vector3.up * (H - 0.7f);
                l.type = LightType.Point;
                l.range = 19f;
                l.intensity = 1.5f;
                l.color = new Color(1f, 0.74f, 0.48f);
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
            }
        }

        /// <summary>Start / finish gantry over the straight: two posts outside the barriers, a beam and a board.</summary>
        private static void Gantry(Transform root)
        {
            int sr = DriftTrackCircuit.StartRow;
            Vector3 c = DriftTrackCircuit.Center[sr], f = DriftTrackRoad.Flat(DriftTrackCircuit.Fwd[sr]), r = DriftTrackRoad.Flat(DriftTrackCircuit.Right[sr]);
            var rot = Quaternion.LookRotation(f);
            var steel = new MeshBuf();
            var board = new MeshBuf();
            const float post = 8.2f, top = 6.4f;
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 p = c + r * side * post;
                p.y = 0f;
                steel.Box(p + Vector3.up * top * 0.5f, rot, new Vector3(0.4f, top, 0.4f));
                TrackMats.Wall(root, "GantryPostCol", p + Vector3.up * top * 0.5f, new Vector3(0.45f, top, 0.45f));
            }
            steel.Box(c + Vector3.up * (top - 0.25f), rot, new Vector3(post * 2f + 0.4f, 0.5f, 0.5f));
            board.Box(c + Vector3.up * (top - 1.2f), rot, new Vector3(11f, 1.3f, 0.12f));
            TrackMats.Piece(root, "Gantry", steel.ToMesh("TrackGantry"), TrackMats.Get("TrackSteelDark", new Color(0.22f, 0.23f, 0.25f), 0.5f, 0.7f));
            TrackMats.Piece(root, "GantryBoard", board.ToMesh("TrackGantryBoard"), TrackMats.Get("TrackLineBlack", new Color(0.03f, 0.03f, 0.03f), 0.2f));
            var yellow = new Color(0.95f, 0.72f, 0.08f);
            DriftTrackProps.Letters(root, "META", c + Vector3.up * (top - 1.2f) - f * 0.08f, rot, 0.55f, yellow, new Vector2(10f, 1.1f));
            DriftTrackProps.Letters(root, "START", c + Vector3.up * (top - 1.2f) + f * 0.08f, Quaternion.LookRotation(-f), 0.55f, yellow, new Vector2(10f, 1.1f));
        }

        private static Vector3 G(Vector3 p) { p.y = Ground; return p; }
    }
}
