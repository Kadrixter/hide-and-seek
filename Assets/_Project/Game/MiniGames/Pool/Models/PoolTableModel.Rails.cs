using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public sealed partial class PoolTableModel
    {
        /// <summary>Wooden rails (six pieces split at the pockets), the moulded outer edge and the 18 inlaid sights.</summary>
        void BuildRails()
        {
            Part("Rails", RailMesh(), _wood);
            Part("RailEdge", RailEdgeMesh(), _wood);
            Part("SightSettings", SightMesh("pool_sightset", 0.0165f, 0.0088f, 0.0006f), _metal);
            Part("Sights", SightMesh("pool_sights", 0.0132f, 0.0066f, 0.001f), _pearl);
        }

        /// <summary>Wood UV scale (metres to texture): 1.1 m of grain along, 0.55 m across.</summary>
        static readonly Vector2 WoodUv = new Vector2(0.9f, 1.8f);

        static Mesh RailMesh()
        {
            if (PoolTableMesh.TryGet("pool_rails", out var m)) return m;
            var b = new PoolTableMesh();
            for (int k = 0; k < G.RailPieces.Length; k++)
            {
                // Grain runs along each rail: X on the long rails, Z on the short ones (pieces 2 and 5).
                Vector2 axis = k == 2 || k == 5 ? new Vector2(0f, 1f) : Vector2.right;
                b.Prism(G.RailPiece(k), PoolDims.ClothY, PoolDims.RailTopY, 0.0022f, WoodUv, axis, true);
            }
            return b.Bake("pool_rails");
        }

        /// <summary>
        /// Outer edge moulding swept round the rounded rectangle: a small step down from the rail top (a groove line),
        /// a quarter round, the vertical face with a bead, and a rounded underside.
        /// </summary>
        static Mesh RailEdgeMesh()
        {
            if (PoolTableMesh.TryGet("pool_railedge", out var m)) return m;
            float t = PoolDims.RailTopY, w = G.MouldW;
            var p = new List<Vector2> { new(0f, t - 0.0035f), new(0f, t - 0.0005f), new(0.0035f, t - 0.0005f) };
            PoolTableMesh.Arc(p, new Vector2(w - 0.0085f, t - 0.009f), 0.0085f, 90f, 0f, 6);
            p.Add(new Vector2(w, t - 0.023f));
            PoolTableMesh.Arc(p, new Vector2(w, t - 0.0265f), 0.0035f, 90f, -90f, 6);
            p.Add(new Vector2(w, t - 0.038f));
            PoolTableMesh.Arc(p, new Vector2(w - 0.007f, t - 0.038f), 0.007f, 0f, -90f, 5);
            p.Add(new Vector2(0f, t - 0.045f));
            var b = new PoolTableMesh();
            b.Sweep(G.OuterPath(w, out _), true, p, WoodUv, 30f, 40f);
            return b.Bake("pool_railedge");
        }

        /// <summary>Diamond inlays on the rail tops (long axis across the rail), raised by <paramref name="h"/> with a soft edge.</summary>
        static Mesh SightMesh(string key, float halfAcross, float halfAlong, float h)
        {
            if (PoolTableMesh.TryGet(key, out var m)) return m;
            var b = new PoolTableMesh();
            foreach (var (pos, across) in G.Sights())
            {
                Vector2 along = new Vector2(-across.y, across.x);
                var poly = new List<Vector2> { pos + across * halfAcross, pos + along * halfAlong, pos - across * halfAcross, pos - along * halfAlong };
                b.Prism(poly, PoolDims.RailTopY - 0.0005f, PoolDims.RailTopY + h, h * 0.5f, new Vector2(30f, 30f), across);
            }
            return b.Bake(key);
        }
    }
}
