using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The asphalt pad of the drift track: 12 x 8 tiles (25 x 25 m, each a 10 x 10 grid so the floodlights also light it
    /// well per vertex on the low profiles), texture coordinates in world metres (one asphalt tile per
    /// <see cref="AsphaltMaps.TileMetres"/>, seamless with the passage floor), one box collider; and the dark ground
    /// beyond the fence out to the night horizon.
    /// </summary>
    public static class DriftTrackPad
    {
        private const int TilesX = 12, TilesZ = 8, Grid = 10;

        public static void Build(Transform parent, Material asphalt)
        {
            var pad = new GameObject("Pad").transform;
            pad.SetParent(parent, false);
            float x0 = DriftTrackLayout.PadWest, z0 = DriftTrackLayout.PadSouth;
            float tw = DriftTrackLayout.PadLength / TilesX, td = DriftTrackLayout.PadDepth / TilesZ;
            for (int tz = 0; tz < TilesZ; tz++)
            for (int tx = 0; tx < TilesX; tx++)
            {
                var mesh = Tile(x0 + tx * tw, z0 + tz * td, tw, td);
                var go = TrackMats.Piece(pad, $"PadTile_{tx:00}_{tz:00}", mesh, asphalt, false);
                go.GetComponent<MeshRenderer>().receiveShadows = true;
            }
            var c = DriftTrackLayout.PadCenter;
            var col = TrackMats.Wall(pad, "PadCollider", new Vector3(c.x, -0.15f, c.z), new Vector3(DriftTrackLayout.PadLength, 0.3f, DriftTrackLayout.PadDepth));
            col.gameObject.AddComponent<FootstepSurface>().Type = SurfaceType.Concrete;

            // Dark ground out to the horizon (no collider: the fence keeps everybody on the pad).
            var g = new ProcMesh();
            g.At(Vector3.zero).Box(new Vector3(c.x, -0.08f, c.z), new Vector3(1200f, 0.1f, 1200f));
            TrackMats.Piece(parent, "Ground", g.ToMesh("TrackGround"), TrackMats.Get("TrackGround", new Color(0.035f, 0.04f, 0.035f), 0.05f), false);
        }

        private static Mesh Tile(float x0, float z0, float w, float d)
        {
            int n = Grid + 1;
            var v = new Vector3[n * n];
            var uv = new Vector2[n * n];
            var nr = new Vector3[n * n];
            var t = new int[Grid * Grid * 6];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = x0 + w * i / Grid, z = z0 + d * j / Grid;
                v[j * n + i] = new Vector3(x, 0f, z);
                uv[j * n + i] = new Vector2(x / AsphaltMaps.TileMetres, z / AsphaltMaps.TileMetres);
                nr[j * n + i] = Vector3.up;
            }
            int k = 0;
            for (int j = 0; j < Grid; j++)
            for (int i = 0; i < Grid; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, e = c + 1;
                t[k++] = a; t[k++] = c; t[k++] = e;
                t[k++] = a; t[k++] = e; t[k++] = b;
            }
            var m = new Mesh { name = "PadTile", vertices = v, uv = uv, normals = nr, triangles = t };
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }
}
