using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Ball racks. The wooden 8-ball triangle: a lacquered hardwood frame (inner wall hugging the balls, rounded inner
    /// edge, bullnose outside, a cut bead line) with a finger scallop in the back rail and brass corner caps held by
    /// two domed pins each. The 9-ball diamond: thin moulded black plastic with soft edges. Origin of both: the centre
    /// of the apex ball, down on the cloth (y = 0 is the cloth); the rack opens towards +X, so placed on the foot spot
    /// it holds exactly <see cref="PoolBallModels.RackPositions"/> (the diamond the 9-ball layout 1-2-3-2-1).
    /// </summary>
    public static class PoolRackModel
    {
        /// <summary>Gap between the racked balls and the inner wall.</summary>
        public const float Clearance = 0.0008f;
        public const float WoodH = 0.033f, WoodT = 0.021f;
        public const float DiamondH = 0.029f, DiamondT = 0.0085f;
        const float D = PoolDims.BallD, Row = PoolBallModels.RowStep;
        const float NotchDepth = 0.009f, NotchHalf = 0.034f, NotchFloor = 0.0125f;

        static Mesh _frame, _caps, _diamond, _pin;

        /// <summary>Ball-centre triangle of the 15-ball rack (x, z): apex, back -Z, back +Z.</summary>
        public static Vector2[] TrianglePoly() => new[] { Vector2.zero, new Vector2(4f * Row, -2f * D), new Vector2(4f * Row, 2f * D) };

        /// <summary>Ball-centre rhombus of the 9-ball rack: apex, side -Z, back, side +Z.</summary>
        public static Vector2[] DiamondPoly() => new[] { Vector2.zero, new Vector2(2f * Row, -D), new Vector2(4f * Row, 0f), new Vector2(2f * Row, D) };

        /// <summary>Centres of the 9 diamond balls relative to the apex ball (x, z), rows 1-2-3-2-1 from -Z to +Z.</summary>
        public static Vector2[] DiamondBalls()
        {
            var list = new List<Vector2>();
            int[] counts = { 1, 2, 3, 2, 1 };
            for (int r = 0; r < 5; r++)
                for (int k = 0; k < counts[r]; k++)
                    list.Add(new Vector2(r * Row, (k - (counts[r] - 1) * 0.5f) * D));
            return list.ToArray();
        }

        static float InnerOffset => PoolDims.BallR + Clearance;

        public static Transform BuildTriangle(Transform parent, Material template)
        {
            var root = NewRoot("PoolRackTriangle", parent);
            var wood = Mat(template, new Color(0.34f, 0.17f, 0.09f), 0f, 0.62f, BoardArt.GrainTable);
            var brass = Mat(template, new Color(0.84f, 0.68f, 0.38f), 0.8f, 0.74f, null);
            Part("Frame", root, Frame(), wood);
            Part("Caps", root, Caps(), brass);
            // Two domed pins in each cap, on the outer wall either side of the corner's middle.
            var poly = TrianglePoly();
            var c = (poly[0] + poly[1] + poly[2]) / 3f;
            for (int i = 0; i < 3; i++)
            {
                Vector2 mid = (poly[i] - c).normalized;
                foreach (float turn in new[] { -24f, 24f })
                {
                    Vector2 nrm = Rotate(mid, turn);
                    Vector2 xz = poly[i] + nrm * (InnerOffset + WoodT + 0.0007f);
                    var pos = new Vector3(xz.x, WoodH * 0.42f, xz.y);
                    var g = Part("Pin", root, Pin(), brass);
                    g.transform.localPosition = pos;
                    g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(nrm.x, 0f, nrm.y));
                }
            }
            return root;
        }

        public static Transform BuildDiamond(Transform parent, Material template)
        {
            var root = NewRoot("PoolRackDiamond", parent);
            var plastic = Mat(template, new Color(0.035f, 0.035f, 0.04f), 0f, 0.82f, null);
            if (_diamond == null)
            {
                const float h = DiamondH, t = DiamondT, e = 0.0028f;
                var p = new List<Vector2>();
                PoolRackMeshes.Pt(p, 0f, 0f);
                PoolRackMeshes.Arc(p, e, h - e, e, 180f, 90f, 6);
                PoolRackMeshes.Arc(p, t - e, h - e, e, 90f, 0f, 6);
                // Slight draft on the outer wall and a small foot flare.
                PoolRackMeshes.Pt(p, t + 0.0004f, 0.004f, true);
                PoolRackMeshes.Pt(p, t + 0.0012f, 0.0012f);
                PoolRackMeshes.Pt(p, t + 0.0008f, 0f, true);
                PoolRackMeshes.Pt(p, 0f, 0f);
                _diamond = PoolRackMeshes.Sweep("PoolRackDiamond", PoolRackMeshes.Closed(DiamondPoly(), 24, 0.004f), true, p, InnerOffset);
            }
            Part("Frame", root, _diamond, plastic);
            return root;
        }

        // ================================================================ triangle meshes

        static List<Vector2> WoodProfile()
        {
            const float h = WoodH, t = WoodT;
            var p = new List<Vector2>();
            PoolRackMeshes.Pt(p, 0f, 0f);
            PoolRackMeshes.Arc(p, 0.002f, h - 0.002f, 0.002f, 180f, 90f, 5);
            PoolRackMeshes.Pt(p, 0.008f, h);
            PoolRackMeshes.Arc(p, t - 0.0075f, h - 0.0075f, 0.0075f, 90f, 0f, 10);
            PoolRackMeshes.Pt(p, t, 0.0165f);
            // Cut bead line round the outside: a half-round groove, hard edged.
            PoolRackMeshes.Pt(p, t, 0.0125f, true);
            for (int i = 1; i < 6; i++)
            {
                float a = (90f + 180f * i / 6f) * Mathf.Deg2Rad;
                PoolRackMeshes.Pt(p, t + 0.0015f * Mathf.Cos(a), 0.011f + 0.0015f * Mathf.Sin(a));
            }
            PoolRackMeshes.Pt(p, t, 0.0095f, true);
            PoolRackMeshes.Pt(p, t, 0.0025f, true);
            PoolRackMeshes.Pt(p, t - 0.0025f, 0f, true);
            PoolRackMeshes.Pt(p, 0f, 0f);
            return p;
        }

        /// <summary>Finger scallop in the back rail: lowers everything above <see cref="NotchFloor"/> round its middle.</summary>
        static float NotchWarp(PoolRackMeshes.Station st, float o, float y)
        {
            if (st.N.x < 0.99f || y <= NotchFloor) return y;
            float z = st.Q.y;
            if (Mathf.Abs(z) >= NotchHalf) return y;
            float k = 0.5f + 0.5f * Mathf.Cos(z / NotchHalf * Mathf.PI);
            float depth = NotchDepth * k;
            return NotchFloor + (y - NotchFloor) * (1f - depth / (WoodH - NotchFloor));
        }

        static Mesh Frame() => _frame != null ? _frame
            : (_frame = PoolRackMeshes.Sweep("PoolRackTriangle", PoolRackMeshes.Closed(TrianglePoly(), 22, 0.0025f), true, WoodProfile(), InnerOffset, NotchWarp, 5f));

        static Mesh Caps()
        {
            if (_caps != null) return _caps;
            const float h = WoodH, t = WoodT, e = 0.0007f;
            var p = new List<Vector2>();
            PoolRackMeshes.Pt(p, 0.0065f, h);
            PoolRackMeshes.Pt(p, 0.0065f, h + e, true);
            PoolRackMeshes.Arc(p, t - 0.0075f, h - 0.0075f, 0.0075f + e, 90f, 0f, 10);
            PoolRackMeshes.Pt(p, t + e, 0.0018f, true);
            PoolRackMeshes.Pt(p, t, 0.0018f);
            var poly = TrianglePoly();
            var parts = new CombineInstance[3];
            for (int i = 0; i < 3; i++)
                parts[i] = new CombineInstance
                {
                    mesh = PoolRackMeshes.Sweep("cap", PoolRackMeshes.CornerOnly(poly, i, 20, 0.016f, 5), false, p, InnerOffset),
                    transform = Matrix4x4.identity,
                };
            _caps = new Mesh { name = "PoolRackCaps" };
            _caps.CombineMeshes(parts, true, true);
            return _caps;
        }

        static Mesh Pin()
        {
            if (_pin != null) return _pin;
            // Low dome (3.2 mm across, 1.1 mm proud), its shank sunk into the cap.
            var prof = new List<Vector2> { new(0f, -0.0004f), new(0.0016f, -0.0004f), new(0.0016f, -0.0004f), new(0.0016f, 0f) };
            for (int i = 0; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI * 0.5f;
                prof.Add(new Vector2(0.0016f * Mathf.Cos(a), 0.0011f * Mathf.Sin(a)));
            }
            return _pin = BoardMeshes.Lathe("poolrackpin", prof, 16, 1f);
        }

        // ================================================================ helpers

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static Transform NewRoot(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            if (parent != null)
            {
                t.gameObject.layer = parent.gameObject.layer;
                t.SetParent(parent, false);
            }
            return t;
        }

        static GameObject Part(string name, Transform parent, Mesh mesh, Material mat)
        {
            var g = new GameObject(name) { layer = parent.gameObject.layer };
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        static Material Mat(Material template, Color c, float metallic, float gloss, Texture tex)
        {
            var m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.color = c;
            m.mainTexture = tex;
            m.mainTextureScale = Vector2.one;
            m.DisableKeyword("_EMISSION");
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }
    }
}
