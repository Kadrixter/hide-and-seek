using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// A big plug-in fuse of the garage fuse boxes: brass contact sleeve with two blades at the back (goes into the
    /// socket), cream ceramic body, black ribbed bakelite cap at the front. Its mark (<see cref="FuseSymbols"/>) is
    /// painted on a black plate on the cap's front face and on all four sides of the body, so it reads in the socket,
    /// in the hand and lying on the floor whichever way it fell. All 8 fuses are the same except for the mark.
    /// Model space: origin = middle, +Z = towards the cap (out of the socket), +Y = the side whose label reads upright.
    /// </summary>
    public static class FuseModel
    {
        /// <summary>The model is built at 1 and shown this big (chunky enough to read the mark from standing height).</summary>
        public const float Scale = 1.35f;
        public const float Half = 0.0375f, Length = 0.19f;
        /// <summary>Back end (goes into the socket first) / front face of the cap (unscaled model space).</summary>
        public const float BackZ = -0.095f, FrontZ = 0.095f;
        /// <summary>Half thickness / half length as shown (scaled).</summary>
        public const float HalfShown = (Half + 0.004f) * Scale, HalfLengthShown = FrontZ * Scale;
        /// <summary>Where the fist holds it when carried: the brass end, the body and the cap stick out (unscaled).</summary>
        public static readonly Vector3 GripPoint = new Vector3(0f, 0f, -0.15f);

        private const int SubCeramic = 0, SubCap = 1, SubBrass = 2;
        private static Mesh _body, _labels;
        private static Material[] _mats;
        private static readonly Material[] LabelMats = new Material[FuseSymbols.Count];

        /// <summary>Builds a fuse with mark <paramref name="symbol"/> under <paramref name="parent"/>.</summary>
        public static Transform Create(Transform parent, int symbol, int layer = 0)
        {
            Build();
            var root = new GameObject("Fuse_" + FuseSymbols.Name(symbol)).transform;
            root.SetParent(parent, false);
            root.gameObject.layer = layer;
            root.localScale = Vector3.one * Scale;
            Part(root, "Body", _body, _mats, layer);
            Part(root, "Labels", _labels, new[] { LabelMat(symbol) }, layer);
            return root;
        }

        private static void Part(Transform root, string name, Mesh mesh, Material[] mats, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }

        private static int _warm;

        /// <summary>
        /// Builds one piece of the fuses' looks per call (label textures, the mesh) so the round start does not pay for
        /// all of it in one frame; called every frame in the lobby. True when everything is ready.
        /// </summary>
        public static bool WarmUp()
        {
            if (_warm > FuseSymbols.Count) return true;
            if (_warm < FuseSymbols.Count) LabelMat(_warm);
            else Build();
            _warm++;
            return false;
        }

        public static Material LabelMat(int symbol)
        {
            symbol = Mathf.Clamp(symbol, 0, FuseSymbols.Count - 1);
            if (LabelMats[symbol] != null) return LabelMats[symbol];
            var m = PaMesh.Mat("FuseLabel_" + symbol, Color.white, 0.35f);
            m.mainTexture = FuseSymbols.Label(symbol);
            return LabelMats[symbol] = m;
        }

        private static void Build()
        {
            if (_body != null) return;
            _mats = new[]
            {
                PaMesh.Mat("FuseCeramic", new Color(0.86f, 0.82f, 0.7f), 0.42f),
                PaMesh.Mat("FuseBakelite", new Color(0.07f, 0.065f, 0.06f), 0.55f),
                PaMesh.Mat("FuseBrass", new Color(0.78f, 0.6f, 0.3f), 0.7f, 0.85f),
            };
            var m = new PaMesh(3);
            Vector3 o = Vector3.zero, r = Vector3.right, u = Vector3.up, f = Vector3.forward;
            // Ceramic body.
            m.Pebble(PaMesh.RoundRect(Half, Half, 0.009f), PaMesh.SlabRings(-0.058f, 0.052f, 0.006f), o, r, u, f, SubCeramic);
            // Bakelite cap, a little wider, with grip ribs on the sides.
            m.Pebble(PaMesh.RoundRect(Half + 0.004f, Half + 0.004f, 0.012f), PaMesh.SlabRings(0.048f, FrontZ, 0.008f), o, r, u, f, SubCap);
            for (int i = 0; i < 3; i++)
            {
                float z = 0.06f + i * 0.011f;
                m.Box(new Vector3(0f, Half + 0.0045f, z), new Vector3(0.05f, 0.004f, 0.004f), Quaternion.identity, SubCap);
                m.Box(new Vector3(0f, -Half - 0.0045f, z), new Vector3(0.05f, 0.004f, 0.004f), Quaternion.identity, SubCap);
                m.Box(new Vector3(Half + 0.0045f, 0f, z), new Vector3(0.004f, 0.05f, 0.004f), Quaternion.identity, SubCap);
                m.Box(new Vector3(-Half - 0.0045f, 0f, z), new Vector3(0.004f, 0.05f, 0.004f), Quaternion.identity, SubCap);
            }
            // Brass contact sleeve and the two blades.
            m.Pebble(PaMesh.RoundRect(0.027f, 0.027f, 0.006f), PaMesh.SlabRings(-0.08f, -0.056f, 0.003f), o, r, u, f, SubBrass);
            m.Box(new Vector3(0f, 0.012f, -0.088f), new Vector3(0.036f, 0.005f, 0.02f), Quaternion.identity, SubBrass);
            m.Box(new Vector3(0f, -0.012f, -0.088f), new Vector3(0.036f, 0.005f, 0.02f), Quaternion.identity, SubBrass);
            _body = m.ToMesh("FuseBody");

            // Labels: the cap's front face and the four sides of the body (symbol upright towards the cap).
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            // Seen from outside: right = the viewer's right (texture u), triangles clockwise.
            void Quad(Vector3 c, Vector3 up, Vector3 normal, float h)
            {
                Vector3 right = Vector3.Cross(up, -normal);
                int i = v.Count;
                v.Add(c - right * h - up * h); v.Add(c + right * h - up * h); v.Add(c + right * h + up * h); v.Add(c - right * h + up * h);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
            Quad(new Vector3(0f, 0f, FrontZ + 0.0008f), Vector3.up, Vector3.forward, 0.031f);
            const float side = Half + 0.0008f, zc = -0.003f, hs = 0.029f;
            Quad(new Vector3(0f, side, zc), Vector3.forward, Vector3.up, hs);
            Quad(new Vector3(0f, -side, zc), Vector3.forward, Vector3.down, hs);
            Quad(new Vector3(side, 0f, zc), Vector3.forward, Vector3.right, hs);
            Quad(new Vector3(-side, 0f, zc), Vector3.forward, Vector3.left, hs);
            _labels = new Mesh { name = "FuseLabels" };
            _labels.SetVertices(v);
            _labels.SetNormals(n);
            _labels.SetUVs(0, uv);
            _labels.SetTriangles(t, 0);
            _labels.RecalculateBounds();
        }
    }
}
