using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public sealed partial class PoolTableModel
    {
        /// <summary>Slate bed under the cloth (the <see cref="Cloth"/> transform sits on its surface) and the six cushions.</summary>
        void BuildBed()
        {
            Cloth = Group("Cloth", new Vector3(0f, PoolDims.ClothY, 0f), Quaternion.identity);
            Part("ClothTop", BedTop(), _cloth, Cloth);
            Part("ClothEdge", BedEdge(), _cushion, Cloth);
            Part("SlateEdge", SlateEdge(), _slate, Cloth);
            Part("SlateUnder", SlateUnder(), _slate, Cloth);
        }

        const float BedRound = 0.0015f;

        /// <summary>Cloth surface at y = 0 (local to <see cref="Cloth"/>), the whole bed texture over the bed rectangle.</summary>
        static Mesh BedTop()
        {
            if (PoolTableMesh.TryGet("pool_bedtop", out var m)) return m;
            var b = new PoolTableMesh();
            var poly = PoolTableMesh.Offset(PoolTableMesh.Ccw(G.BedOutline()), -BedRound);
            b.Cap(poly, 0f, true, Vector2.one, Vector2.right, q => new Vector2((q.x + G.BedX) / (2f * G.BedX), (q.y + G.BedZ) / (2f * G.BedZ)));
            return b.Bake("pool_bedtop");
        }

        /// <summary>Cloth wrapped over the slate's edge into the pocket holes (rounded lip, then down the slate).</summary>
        static Mesh BedEdge()
        {
            if (PoolTableMesh.TryGet("pool_bededge", out var m)) return m;
            var b = new PoolTableMesh();
            var prof = new List<Vector2>();
            PoolTableMesh.Arc(prof, new Vector2(-BedRound, -BedRound), BedRound, 90f, 0f, 3);
            prof.Add(new Vector2(0f, -0.005f));
            b.Sweep(PoolTableMesh.Ccw(G.BedOutline()), true, prof, new Vector2(4f, 4f), 30f, 50f);
            return b.Bake("pool_bededge");
        }

        /// <summary>Bare slate edge under the cloth lip (seen from below beyond the frame).</summary>
        static Mesh SlateEdge()
        {
            if (PoolTableMesh.TryGet("pool_slateedge", out var m)) return m;
            var b = new PoolTableMesh();
            var prof = new List<Vector2> { new(0f, -0.005f), new(0f, -G.SlateT) };
            b.Sweep(PoolTableMesh.Ccw(G.BedOutline()), true, prof, new Vector2(4f, 4f), 30f, 50f);
            return b.Bake("pool_slateedge");
        }

        static Mesh SlateUnder()
        {
            if (PoolTableMesh.TryGet("pool_slate", out var m)) return m;
            var b = new PoolTableMesh();
            b.Cap(PoolTableMesh.Ccw(G.BedOutline()), -G.SlateT, false, Vector2.one, Vector2.right);
            return b.Bake("pool_slate");
        }

        // ================================================================ cushions

        void BuildCushions() => Part("Cushions", CushionMesh(), _cushion);

        /// <summary>
        /// K-66 cushion section (d = out from the nose, y above the cloth), clockwise from the top back corner: back face,
        /// base, undercut, rounded nose at <see cref="PoolDims.NoseH"/>, gently rising top meeting the rail top.
        /// </summary>
        static List<Vector2> CushionProfile()
        {
            float cw = G.CushionW, top = PoolDims.RailTopY - PoolDims.ClothY;
            var p = new List<Vector2> { new(cw, top), new(cw, 0.001f), new(0.026f, 0.001f), new(0.0048f, 0.0302f) };
            PoolTableMesh.Arc(p, new Vector2(0.0036f, PoolDims.NoseH), 0.0036f, 232f, 108f, 8);
            p.Add(new Vector2(0.006f, 0.0412f));
            p.Add(new Vector2(0.013f, 0.0428f));
            p.Add(new Vector2(0.03f, 0.0442f));
            p.Add(new Vector2(cw, top));
            return p;
        }

        static Mesh CushionMesh()
        {
            if (PoolTableMesh.TryGet("pool_cushions", out var m)) return m;
            var b = new PoolTableMesh();
            var prof = CushionProfile();
            var rows = PoolTableMesh.Rows(prof, 40f);
            var loop = new List<Vector2>(prof);
            loop.RemoveAt(loop.Count - 1);
            foreach (var c in G.Cushions()) AddCushion(b, c.a, c.b, c.o, c.ka, c.kb, rows, loop);
            return b.Bake("pool_cushions");
        }

        /// <summary>One straight cushion; its ends are cut along the facing planes (the back runs further into the jaw).</summary>
        static void AddCushion(PoolTableMesh b, Vector2 a2, Vector2 b2, Vector2 o2, float ka, float kb, List<PoolTableMesh.Row> rows, List<Vector2> loop)
        {
            float y0 = PoolDims.ClothY, len = Vector2.Distance(a2, b2);
            Vector2 u2 = (b2 - a2) / len;
            Vector3 U = new Vector3(u2.x, 0f, u2.y), O = new Vector3(o2.x, 0f, o2.y);
            Vector3 A = new Vector3(a2.x, y0, a2.y), B = new Vector3(b2.x, y0, b2.y);
            int n = rows.Count;
            var ia = new int[n];
            var ib = new int[n];
            const float uv = 4f;
            for (int i = 0; i < n; i++)
            {
                var r = rows[i];
                float d = r.P.x;
                // Normals lean up a little: the cloth's fuzz catches the overhead light, so the faces never go black.
                Vector3 nrm = (O * r.N.x + Vector3.up * r.N.y).normalized + Vector3.up * 0.3f, h = O * d + Vector3.up * r.P.y;
                ia[i] = b.Vert(A - U * (ka * d) + h, nrm, new Vector2(-ka * d * uv, r.V * uv));
                ib[i] = b.Vert(B + U * (kb * d) + h, nrm, new Vector2((len + kb * d) * uv, r.V * uv));
            }
            for (int i = 0; i + 1 < n; i++)
            {
                if ((rows[i].P - rows[i + 1].P).sqrMagnitude < 1e-14f) continue;
                b.Quad(ia[i], ib[i], ib[i + 1], ia[i + 1]);
            }
            Facing(b, A, -U, O, ka, loop);
            Facing(b, B, U, O, kb, loop);
        }

        /// <summary>The cloth-covered end face of a cushion, in the facing plane (vertical, slanting towards the pocket).</summary>
        static void Facing(PoolTableMesh b, Vector3 nose, Vector3 toPocket, Vector3 o, float k, List<Vector2> loop)
        {
            Vector3 f = (o + toPocket * k).normalized;
            Vector3 n = Vector3.Cross(Vector3.up, f);
            if (Vector3.Dot(n, toPocket) < 0f) n = -n;
            b.Polygon(loop, q => nose + (o + toPocket * k) * q.x + Vector3.up * q.y, n + Vector3.up * 0.3f, new Vector2(4f, 4f));
        }
    }
}
