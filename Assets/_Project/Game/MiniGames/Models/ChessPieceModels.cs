using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Staunton chess pieces for 8 cm squares: turned profiles (base with two beads and a cove, tapering stem, collars),
    /// rook battlements, bishop mitre with its slit, queen coronet, king cross, knight head carved from a side silhouette
    /// that gets thinner towards the snout and the ears, with a ridged mane, eyes and nostrils. Every piece is one mesh
    /// with two submeshes (0 = wood, 1 = dark details); the origin is the centre of the base, the front faces +Z.
    /// </summary>
    public static class ChessPieceModels
    {
        const float Mm = 0.001f;
        static readonly Mesh[] Meshes = new Mesh[7];

        /// <summary>Heights (m) of the pieces, for grabbing from the top: index = piece type.</summary>
        public static readonly float[] Height = { 0f, 0.0635f, 0.088f, 0.0965f, 0.073f, 0.1125f, 0.129f };

        public static Mesh Get(int type)
        {
            if (type < 1 || type > 6) return null;
            if (Meshes[type] != null) return Meshes[type];
            var body = new List<CombineInstance>();
            var detail = new List<CombineInstance>();
            switch (type)
            {
                case ChessBoard.Pawn: body.Add(C(BoardMeshes.Lathe("pawn", Pawn(), 40))); break;
                case ChessBoard.Rook: Rook(body); break;
                case ChessBoard.Bishop: Bishop(body, detail); break;
                case ChessBoard.Queen: Queen(body); break;
                case ChessBoard.King: King(body); break;
                case ChessBoard.Knight: Knight(body, detail); break;
            }
            var a = new Mesh { name = "PieceBody" + type };
            a.CombineMeshes(body.ToArray(), true, true);
            Mesh m = a;
            if (detail.Count > 0)
            {
                var b = new Mesh { name = "PieceDetail" + type };
                b.CombineMeshes(detail.ToArray(), true, true);
                m = new Mesh { name = "Piece" + type };
                m.CombineMeshes(new[] { C(a), C(b) }, false, true);
            }
            m.RecalculateBounds();
            return Meshes[type] = m;
        }

        static CombineInstance C(Mesh m) => new CombineInstance { mesh = m, transform = Matrix4x4.identity };
        static CombineInstance C(Mesh m, Matrix4x4 t) => new CombineInstance { mesh = m, transform = t };
        static CombineInstance C(Mesh m, Vector3 pos, Quaternion rot) => C(m, Matrix4x4.TRS(pos, rot, Vector3.one));

        // ================================================================ profile helpers (millimetres -> metres)

        sealed class Prof
        {
            public readonly List<Vector2> P = new();
            public Prof Pt(float r, float y) { P.Add(new Vector2(r, y) * Mm); return this; }
            public Prof Crease() { P.Add(P[^1]); return this; }
            /// <summary>Quadratic Bezier from the last point via (cr, cy) to (r, y).</summary>
            public Prof Bez(float cr, float cy, float r, float y, int n = 6)
            {
                Vector2 a = P[^1] / Mm, c = new Vector2(cr, cy), b = new Vector2(r, y);
                for (int i = 1; i <= n; i++)
                {
                    float t = i / (float)n, u = 1f - t;
                    P.Add((u * u * a + 2f * u * t * c + t * t * b) * Mm);
                }
                return this;
            }
            /// <summary>Arc around (0, cy) of radius r from angle a0 to a1 (degrees, 90 = top).</summary>
            public Prof Ball(float cy, float r, float a0, int n = 10)
            {
                var tmp = new List<Vector2>();
                BoardMeshes.Arc(tmp, 0f, cy, r, a0, 90f, n);
                foreach (var q in tmp) P.Add(new Vector2(Mathf.Max(0f, q.x), q.y) * Mm);
                return this;
            }
        }

        /// <summary>Foot shared by every piece: flat bottom, low chamfered plinth, two beads, cove into the stem.</summary>
        static Prof Base(float R, float stemR, float stemY)
        {
            var p = new Prof();
            float k = R / 22f;
            p.Pt(0, 0).Pt(R, 0).Crease().Pt(R, 4f * k)
             .Bez(R, 8.5f * k, R - 3.5f * k, 9f * k)
             .Pt(R - 4.5f * k, 9.5f * k)
             .Bez(R - 2.5f * k, 11.5f * k, R - 5.5f * k, 13.5f * k)
             .Bez(stemR + 0.5f, 14.5f * k, stemR, stemY, 8);
            return p;
        }

        static List<Vector2> Pawn()
        {
            var p = Base(20f, 9f, 20f);
            p.Bez(8.5f, 30f, 6.5f, 36f)
             .Bez(9.5f, 36.5f, 11.2f, 38.2f, 4).Bez(11.2f, 40f, 8.5f, 40.5f, 4).Pt(6f, 41f)
             .Ball(52f, 11.5f, -61f, 14);
            return p.P;
        }

        static void Rook(List<CombineInstance> body)
        {
            var p = Base(23f, 13f, 22f);
            p.Bez(12.5f, 40f, 13.5f, 50f).Bez(14f, 54f, 17f, 55f, 4).Bez(19.2f, 55.6f, 17.6f, 57.6f, 4)
             .Pt(17f, 58.2f).Pt(18f, 58.8f).Crease().Pt(18f, 64f).Crease().Pt(12f, 64f).Crease().Pt(11f, 63f).Pt(0f, 62.8f);
            body.Add(C(BoardMeshes.Lathe("rook", p.P, 40)));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 12f;
                body.Add(C(BoardMeshes.Sector("merlon" + i, 12f * Mm, 18f * Mm, a, a + 36f, 63.5f * Mm, 73f * Mm, 5)));
            }
        }

        static void Bishop(List<CombineInstance> body, List<CombineInstance> detail)
        {
            var p = Base(22f, 9.5f, 24f);
            p.Bez(8.5f, 42f, 6.5f, 52f)
             .Bez(11f, 52.5f, 12.5f, 54.5f, 4).Bez(12.5f, 56.5f, 9f, 57f, 4).Pt(7f, 57.5f)
             .Bez(10.5f, 58.5f, 9.5f, 60.5f, 4).Pt(6f, 61f)
             .Bez(13.5f, 64f, 12f, 75f, 8).Bez(11f, 83f, 4f, 89f, 8).Pt(2.5f, 90f)
             .Ball(93f, 3.5f, -45f, 8);
            body.Add(C(BoardMeshes.Lathe("bishop", p.P, 40)));
            // Mitre slit: a dark wedge across the front of the head.
            detail.Add(C(BoardMeshes.RoundedBox(new Vector3(17f, 2.2f, 9f) * Mm, 0.4f * Mm, 2), new Vector3(0f, 77.5f, 8.2f) * Mm,
                Quaternion.Euler(0f, 0f, 38f)));
        }

        static void Queen(List<CombineInstance> body)
        {
            var p = Base(26f, 11f, 28f);
            p.Bez(9.5f, 50f, 7.5f, 64f)
             .Bez(13f, 64.5f, 14f, 66.5f, 4).Bez(14f, 68.5f, 10f, 69f, 4).Pt(8f, 69.5f)
             .Bez(12f, 70.5f, 11f, 72.5f, 4).Pt(7.5f, 73f)
             .Bez(8.5f, 85f, 16f, 95f, 8).Pt(17f, 96f).Crease().Pt(15.5f, 97f)
             .Bez(13f, 99f, 9f, 101f, 4).Pt(6f, 102f)
             .Ball(107f, 5.5f, -65f, 10);
            body.Add(C(BoardMeshes.Lathe("queen", p.P, 44)));
            var ball = Sphere(2.9f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f * Mathf.Deg2Rad;
                body.Add(C(ball, new Vector3(Mathf.Cos(a) * 15.2f, 97.8f, Mathf.Sin(a) * 15.2f) * Mm, Quaternion.identity));
            }
        }

        static void King(List<CombineInstance> body)
        {
            var p = Base(28f, 12f, 30f);
            p.Bez(10.5f, 55f, 8f, 70f)
             .Bez(14f, 70.5f, 15f, 72.5f, 4).Bez(15f, 74.5f, 11f, 75f, 4).Pt(8.5f, 75.5f)
             .Bez(12.5f, 76.5f, 11.5f, 78.5f, 4).Pt(8f, 79f)
             .Bez(9f, 92f, 16.5f, 101f, 8).Pt(17.5f, 102f).Crease().Pt(16.5f, 103.5f)
             .Bez(12f, 106f, 6f, 107f, 4).Pt(4f, 107.5f).Pt(4f, 108f)
             .Bez(6.5f, 108.5f, 5.5f, 110.5f, 3).Pt(0f, 110.5f);
            body.Add(C(BoardMeshes.Lathe("king", p.P, 44)));
            var post = BoardMeshes.RoundedBox(new Vector3(5.6f, 20f, 5.6f) * Mm, 1.3f * Mm, 3);
            var bar = BoardMeshes.RoundedBox(new Vector3(15f, 5.6f, 5.6f) * Mm, 1.3f * Mm, 3);
            body.Add(C(post, new Vector3(0f, 119f, 0f) * Mm, Quaternion.identity));
            body.Add(C(bar, new Vector3(0f, 121.5f, 0f) * Mm, Quaternion.identity));
        }

        static Mesh Sphere(float r)
        {
            var p = new Prof();
            p.Pt(0f, -r).Ball(0f, r, -88f, 10);
            p.P[0] = new Vector2(0f, -r * Mm);
            return BoardMeshes.Lathe("sphere" + r, p.P, 16);
        }

        // ================================================================ knight

        static readonly Vector2[] KnightOutline =
        {
            new(-16.5f, 15f), new(-18.5f, 24f), new(-19f, 33f), new(-18.2f, 46f), new(-15.5f, 57f), new(-11.5f, 66f),
            new(-8f, 73f), new(-6.5f, 79f), new(-4.5f, 86.5f), new(-2.2f, 88f), new(0.2f, 83f), new(1.5f, 79.5f),
            new(5.5f, 77.5f), new(11f, 73f), new(16.5f, 67f), new(21f, 60.5f), new(24.2f, 55f), new(25.6f, 50.5f),
            new(24.6f, 46.5f), new(21.5f, 44.2f), new(17.5f, 44.8f), new(13f, 46.5f), new(9.5f, 45f), new(7.2f, 40.5f),
            new(7.5f, 35f), new(10f, 29.5f), new(13.2f, 23f), new(14.5f, 15f),
        };

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a)));

        /// <summary>Half width of the head (mm) at a point of the side view.</summary>
        static float KnightHalf(float z, float y)
        {
            float h = Mathf.Lerp(11.5f, 9f, Smooth(35f, 60f, y));
            h -= Mathf.Max(0f, z - 8f) * 0.17f * Smooth(40f, 55f, y);
            h = Mathf.Lerp(h, 3.6f, Smooth(75f, 85f, y));
            return h;
        }

        static void Knight(List<CombineInstance> body, List<CombineInstance> detail)
        {
            var p = Base(23f, 15f, 16f);
            p.Pt(15.5f, 16.5f).Bez(17.5f, 17f, 16.5f, 18.5f, 3).Pt(14f, 19f).Crease().Pt(0f, 19f);
            body.Add(C(BoardMeshes.Lathe("knightbase", p.P, 40)));

            var outline = new List<Vector2>();
            foreach (var q in KnightOutline) outline.Add(q * Mm);
            body.Add(C(BoardMeshes.Extrude("knighthead", outline, q => KnightHalf(q.x / Mm, q.y / Mm) * Mm, 3.2f * Mm, 4, 40f)));

            // Mane: a ridge with a carved zigzag edge along the back of the neck.
            var back = new List<Vector2>();
            for (int i = 1; i <= 7; i++)
            {
                Vector2 a = KnightOutline[i], b = KnightOutline[i + 1];
                for (int s = 0; s < 3; s++) back.Add(Vector2.Lerp(a, b, s / 3f));
            }
            var outer = new List<Vector2>();
            var inner = new List<Vector2>();
            for (int i = 0; i < back.Count; i++)
            {
                Vector2 d = (back[Mathf.Min(i + 1, back.Count - 1)] - back[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 nrm = new Vector2(-d.y, d.x);
                if (nrm.x > 0f) nrm = -nrm; // away from the face
                float tooth = (i & 1) == 0 ? 2.6f : 1.2f;
                float fade = Mathf.Clamp01(i / 3f) * Mathf.Clamp01((back.Count - 1 - i) / 3f);
                outer.Add((back[i] + nrm * tooth * fade) * Mm);
                inner.Add((back[i] - nrm * 3.2f) * Mm);
            }
            inner.Reverse();
            outer.AddRange(inner);
            body.Add(C(BoardMeshes.Extrude("knightmane", outer, q => 4.2f * Mm, 1.2f * Mm, 2, 40f)));

            var eye = Sphere(2.3f);
            var nostril = Sphere(1.4f);
            foreach (float sx in new[] { 1f, -1f })
            {
                detail.Add(C(eye, new Vector3(sx * (KnightHalf(9f, 66f) - 0.9f), 66f, 9f) * Mm, Quaternion.identity));
                detail.Add(C(nostril, new Vector3(sx * (KnightHalf(22.5f, 50f) - 0.8f), 50f, 22.5f) * Mm, Quaternion.identity));
            }
            // Mouth line.
            detail.Add(C(BoardMeshes.RoundedBox(new Vector3(2f * (KnightHalf(20f, 46f) - 0.6f), 1.3f, 7f) * Mm, 0.3f * Mm, 2),
                new Vector3(0f, 46.2f, 20.5f) * Mm, Quaternion.Euler(-8f, 0f, 0f)));
        }
    }
}
