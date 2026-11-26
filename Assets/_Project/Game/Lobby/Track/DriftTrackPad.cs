using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The ground of the drift track: the asphalt paddock by the gate where the karts park (east of the start straight,
    /// <see cref="PaddockWest"/>; open for free drifting, onto the circuit through the gap in the straight's barrier),
    /// dark grass over the rest of the pad round the circuit (<see cref="DriftTrackRoad"/> lies on top), one box
    /// collider for the whole pad (top at y 0), and the dark ground beyond the fence out to the night horizon. The
    /// meshes are grids (a vertex every few metres) so the floodlights also light them well per vertex on the low
    /// profiles; asphalt texture coordinates in world metres (one tile per <see cref="AsphaltMaps.TileMetres"/>,
    /// seamless with the passage floor).
    /// </summary>
    public static class DriftTrackPad
    {
        /// <summary>West edge of the paddock asphalt (the start straight's east barrier) and its north edge.</summary>
        public const float PaddockWest = -97.4f, PaddockNorth = 50f;
        private const float AsphaltY = -0.015f, GrassY = -0.06f;

        public static Material Grass => TrackMats.Get("TrackGrass", new Color(0.06f, 0.085f, 0.05f), 0.06f);

        public static void Build(Transform parent, Material asphalt)
        {
            var pad = new GameObject("Pad").transform;
            pad.SetParent(parent, false);
            float pe = DriftTrackLayout.PadEast, ps = DriftTrackLayout.PadSouth;
            Tiles(pad, "Paddock", asphalt, PaddockWest, pe, ps, PaddockNorth, 3, 4, 10, AsphaltY, true);
            Tiles(pad, "Grass", Grass, DriftTrackLayout.PadWest, pe, ps, DriftTrackLayout.PadNorth, 4, 3, 10, GrassY, false);
            var c = DriftTrackLayout.PadCenter;
            var col = TrackMats.Wall(pad, "PadCollider", new Vector3(c.x, -0.15f, c.z), new Vector3(DriftTrackLayout.PadLength, 0.3f, DriftTrackLayout.PadDepth));
            col.gameObject.AddComponent<FootstepSurface>().Type = SurfaceType.Concrete;

            // Dark ground out to the horizon (no collider: the fence keeps everybody on the pad).
            var g = new ProcMesh();
            g.At(Vector3.zero).Box(new Vector3(c.x, -0.15f, c.z), new Vector3(1200f, 0.1f, 1200f));
            TrackMats.Piece(parent, "Ground", g.ToMesh("TrackGround"), TrackMats.Get("TrackGround", new Color(0.035f, 0.04f, 0.035f), 0.05f), false);
        }

        /// <summary>The rectangle x0..x1 / z0..z1 as tx x tz grid tiles (grid cells each way) at height <paramref name="y"/>.</summary>
        private static void Tiles(Transform pad, string name, Material mat, float x0, float x1, float z0, float z1, int tx, int tz, int grid, float y, bool tangents)
        {
            float tw = (x1 - x0) / tx, td = (z1 - z0) / tz;
            for (int j = 0; j < tz; j++)
            for (int i = 0; i < tx; i++)
            {
                var mesh = Tile(x0 + i * tw, z0 + j * td, tw, td, grid, y, tangents);
                var go = TrackMats.Piece(pad, $"{name}_{i:00}_{j:00}", mesh, mat, false);
                go.GetComponent<MeshRenderer>().receiveShadows = true;
            }
        }

        private static Mesh Tile(float x0, float z0, float w, float d, int grid, float y, bool tangents)
        {
            int n = grid + 1;
            var v = new Vector3[n * n];
            var uv = new Vector2[n * n];
            var nr = new Vector3[n * n];
            var t = new int[grid * grid * 6];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = x0 + w * i / grid, z = z0 + d * j / grid;
                v[j * n + i] = new Vector3(x, y, z);
                uv[j * n + i] = new Vector2(x / AsphaltMaps.TileMetres, z / AsphaltMaps.TileMetres);
                nr[j * n + i] = Vector3.up;
            }
            int k = 0;
            for (int j = 0; j < grid; j++)
            for (int i = 0; i < grid; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, e = c + 1;
                t[k++] = a; t[k++] = c; t[k++] = e;
                t[k++] = a; t[k++] = e; t[k++] = b;
            }
            var m = new Mesh { name = "PadTile", vertices = v, uv = uv, normals = nr, triangles = t };
            if (tangents) m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }
}
