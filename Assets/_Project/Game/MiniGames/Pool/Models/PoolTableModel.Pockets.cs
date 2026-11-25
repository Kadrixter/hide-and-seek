using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public sealed partial class PoolTableModel
    {
        const float CastT = 0.004f;

        /// <summary>
        /// Pocket castings: a metal plate over the rail top round each hole (out to the outer edge) with a skirt bent
        /// down over the rail's outer moulding, leather liners lining the rail side of the holes, two screws per casting.
        /// </summary>
        void BuildPockets()
        {
            Part("PocketCastings", CastingMesh(), _metal);
            Part("PocketLiners", LinerMesh(), _leather);
            Part("CastingScrews", ScrewMesh(), _steel);
        }

        static Mesh CastingMesh()
        {
            if (PoolTableMesh.TryGet("pool_castings", out var m)) return m;
            var b = new PoolTableMesh();
            float t = PoolDims.RailTopY;
            // Skirt section (d = out from the rail's outer edge): rolled top over the plate's edge, face, rolled under.
            var skirt = new List<Vector2>();
            PoolTableMesh.Arc(skirt, new Vector2(-0.0018f, t - 0.0018f), 0.0058f, 90f, 0f, 5);
            // The skirt stops under the moulding's bead, so the rounded underside of the rail still shows below it.
            skirt.Add(new Vector2(0.004f, t - 0.029f));
            PoolTableMesh.Arc(skirt, new Vector2(0.001f, t - 0.029f), 0.003f, 0f, -90f, 3);
            skirt.Add(new Vector2(-0.013f, t - 0.032f));
            for (int i = 0; i < 6; i++)
            {
                var poly = G.CastingOutline(i, out var path, out _);
                b.Prism(poly, t - 0.0004f, t + CastT, 0.0018f, new Vector2(8f, 8f), Vector2.right);
                b.Sweep(path, false, skirt, new Vector2(8f, 8f), 30f, 40f, true);
                // Cast bead running round the plate a few millimetres inside its edge.
                var bead = new List<Vector3>();
                foreach (var q in PoolTableMesh.Offset(PoolTableMesh.Ccw(poly), -0.0055f)) bead.Add(new Vector3(q.x, t + CastT - 0.0004f, q.y));
                b.Tube(bead, 0.0011f, 0.0011f, 6, true);
            }
            return b.Bake("pool_castings");
        }

        /// <summary>Leather lining the rail-side arc of each hole, from just under the casting down below the slate.</summary>
        static Mesh LinerMesh()
        {
            if (PoolTableMesh.TryGet("pool_liners", out var m)) return m;
            var b = new PoolTableMesh();
            float t = PoolDims.RailTopY, y = PoolDims.ClothY;
            // d = towards the hole's centre: a slanted lip tucked under the casting, then straight down.
            var prof = new List<Vector2> { new(0f, t - 0.0003f), new(0.0025f, t - 0.0022f), new(0.0025f, y - G.SlateT + 0.004f) };
            for (int i = 0; i < 6; i++)
            {
                G.CastingOutline(i, out _, out var arc);
                b.Sweep(arc, false, prof, new Vector2(6f, 6f), 30f, 40f);
            }
            return b.Bake("pool_liners");
        }

        /// <summary>Domed screw heads on the castings' arms (two per casting).</summary>
        static Mesh ScrewMesh()
        {
            if (PoolTableMesh.TryGet("pool_screws", out var m)) return m;
            var b = new PoolTableMesh();
            float y = PoolDims.RailTopY + CastT, midZ = (G.BedZ + G.OutZ) * 0.5f, midX = (G.BedX + G.OutX) * 0.5f;
            for (int i = 0; i < 6; i++)
            {
                int sx = G.Sx(i), sz = G.Sz(i);
                if (G.IsSide(i))
                {
                    float x = G.SideThroat + G.SideReach - 0.012f;
                    Screw(b, new Vector3(x, y, sz * midZ));
                    Screw(b, new Vector3(-x, y, sz * midZ));
                }
                else
                {
                    Screw(b, new Vector3(sx * (G.HalfL - G.CornerThroat - G.CornerReach + 0.013f), y, sz * midZ));
                    Screw(b, new Vector3(sx * midX, y, sz * (G.HalfW - G.CornerThroat - G.CornerReach + 0.013f)));
                }
            }
            return b.Bake("pool_screws");
        }

        static void Screw(PoolTableMesh b, Vector3 c)
        {
            // Low dome (radius 4 mm) as a fan of rings, with a slot across it.
            const int seg = 14, rings = 4;
            const float r = 0.004f, h = 0.0014f;
            int start = -1;
            for (int k = 0; k <= rings; k++)
            {
                float a = k / (float)rings * Mathf.PI * 0.5f, rr = r * Mathf.Cos(a), yy = h * Mathf.Sin(a);
                for (int s = 0; s < seg; s++)
                {
                    float ang = s / (float)seg * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 nrm = (dir * Mathf.Cos(a) * h / r + Vector3.up * Mathf.Sin(a) + Vector3.up * 0.3f).normalized;
                    int id = b.Vert(c + dir * Mathf.Max(rr, 0.0002f) + Vector3.up * (yy - 0.0002f), nrm, Vector2.zero);
                    if (start < 0) start = id;
                }
            }
            for (int k = 0; k < rings; k++)
                for (int s = 0; s < seg; s++)
                {
                    int i0 = start + k * seg + s, i1 = start + k * seg + (s + 1) % seg;
                    b.Quad(i0, i1, i1 + seg, i0 + seg);
                }
        }
    }
}
