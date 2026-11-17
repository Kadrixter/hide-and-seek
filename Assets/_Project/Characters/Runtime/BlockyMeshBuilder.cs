using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Builds Minecraft-style box meshes whose faces map onto a 64x64 skin texture using the
    /// standard Minecraft skin layout (per box: top, bottom, right, front, left, back).
    /// All meshes are built once and shared by every character.
    /// </summary>
    public static class BlockyMeshBuilder
    {
        public const int TextureSize = 64;

        /// <summary>Size of one skin pixel in metres (character is 32 px = 1.8 m tall).</summary>
        public const float Px = 1.8f / 32f;

        const float UvInset = 0.004f; // in texels, keeps point sampling away from neighbouring texels

        public enum Part
        {
            Head,
            Body,
            UpperArmRight,
            LowerArmRight,
            UpperArmLeft,
            LowerArmLeft,
            UpperLegRight,
            LowerLegRight,
            UpperLegLeft,
            LowerLegLeft
        }

        static readonly Dictionary<Part, Mesh> Cache = new Dictionary<Part, Mesh>();
        static Mesh faceQuad;

        /// <summary>Texture-space description of one box (Minecraft layout, origin top-left, pixels).</summary>
        struct BoxUv
        {
            public int U, V, W, H, D;
            public BoxUv(int u, int v, int w, int h, int d) { U = u; V = v; W = w; H = h; D = d; }
        }

        sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        // Minecraft layout origins.
        static readonly BoxUv HeadUv = new BoxUv(0, 0, 8, 8, 8);
        static readonly BoxUv HatUv = new BoxUv(32, 0, 8, 8, 8);
        static readonly BoxUv BodyUv = new BoxUv(16, 16, 8, 12, 4);
        static readonly BoxUv JacketUv = new BoxUv(16, 32, 8, 12, 4);
        static readonly BoxUv RightArmUv = new BoxUv(40, 16, 4, 12, 4);
        static readonly BoxUv RightSleeveUv = new BoxUv(40, 32, 4, 12, 4);
        static readonly BoxUv LeftArmUv = new BoxUv(32, 48, 4, 12, 4);
        static readonly BoxUv LeftSleeveUv = new BoxUv(48, 48, 4, 12, 4);
        static readonly BoxUv RightLegUv = new BoxUv(0, 16, 4, 12, 4);
        static readonly BoxUv RightPantsUv = new BoxUv(0, 32, 4, 12, 4);
        static readonly BoxUv LeftLegUv = new BoxUv(16, 48, 4, 12, 4);
        static readonly BoxUv LeftPantsUv = new BoxUv(0, 48, 4, 12, 4);

        /// <summary>Returns the shared mesh for a body part (pivot at the joint, see BlockyCharacter).</summary>
        public static Mesh GetMesh(Part part)
        {
            if (Cache.TryGetValue(part, out var mesh) && mesh != null) return mesh;
            mesh = Build(part);
            Cache[part] = mesh;
            return mesh;
        }

        /// <summary>Unit quad (1x1, centred, facing +Z) used for the photo face.</summary>
        public static Mesh GetFaceQuad()
        {
            if (faceQuad != null) return faceQuad;
            faceQuad = new Mesh { name = "BlockyFaceQuad", hideFlags = HideFlags.DontSave };
            // Faces +Z: seen by a viewer looking along -Z. Viewer's left is +X.
            faceQuad.vertices = new[]
            {
                new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            faceQuad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            faceQuad.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            faceQuad.triangles = new[] { 0, 2, 3, 0, 3, 1 };
            faceQuad.RecalculateBounds();
            return faceQuad;
        }

        static Mesh Build(Part part)
        {
            var md = new MeshData();
            const float hatInflate = 0.5f;
            const float layerInflate = 0.25f;
            switch (part)
            {
                case Part.Head:
                    // Pivot at the neck (bottom centre of the head).
                    AddBox(md, new Vector3(-4, 0, -4), new Vector3(4, 8, 4), HeadUv, 0, 8, true, true, false, false, 0f, true, true);
                    AddBox(md, new Vector3(-4, 0, -4), new Vector3(4, 8, 4), HatUv, 0, 8, true, true, false, false, hatInflate, true, true);
                    break;
                case Part.Body:
                    // Pivot at the hips (bottom centre of the torso).
                    AddBox(md, new Vector3(-4, 0, -2), new Vector3(4, 12, 2), BodyUv, 0, 12, true, true, false, false, 0f, true, true);
                    AddBox(md, new Vector3(-4, 0, -2), new Vector3(4, 12, 2), JacketUv, 0, 12, true, true, false, false, layerInflate, true, true);
                    break;
                case Part.UpperArmRight:
                case Part.UpperArmLeft:
                {
                    // Pivot at the shoulder, 2 px below the top of the arm. Upper half = rows 0..5.
                    var baseUv = part == Part.UpperArmRight ? RightArmUv : LeftArmUv;
                    var layerUv = part == Part.UpperArmRight ? RightSleeveUv : LeftSleeveUv;
                    AddBox(md, new Vector3(-2, -4, -2), new Vector3(2, 2, 2), baseUv, 0, 6, true, false, false, true, 0f, true, false);
                    AddBox(md, new Vector3(-2, -4, -2), new Vector3(2, 2, 2), layerUv, 0, 6, true, false, false, false, layerInflate, true, false);
                    break;
                }
                case Part.LowerArmRight:
                case Part.LowerArmLeft:
                {
                    // Pivot at the elbow. Lower half = rows 6..11.
                    var baseUv = part == Part.LowerArmRight ? RightArmUv : LeftArmUv;
                    var layerUv = part == Part.LowerArmRight ? RightSleeveUv : LeftSleeveUv;
                    AddBox(md, new Vector3(-2, -6, -2), new Vector3(2, 0, 2), baseUv, 6, 12, false, true, true, false, 0f, false, true);
                    AddBox(md, new Vector3(-2, -6, -2), new Vector3(2, 0, 2), layerUv, 6, 12, false, true, false, false, layerInflate, false, true);
                    break;
                }
                case Part.UpperLegRight:
                case Part.UpperLegLeft:
                {
                    // Pivot at the hip (top of the leg).
                    var baseUv = part == Part.UpperLegRight ? RightLegUv : LeftLegUv;
                    var layerUv = part == Part.UpperLegRight ? RightPantsUv : LeftPantsUv;
                    AddBox(md, new Vector3(-2, -6, -2), new Vector3(2, 0, 2), baseUv, 0, 6, true, false, false, true, 0f, true, false);
                    AddBox(md, new Vector3(-2, -6, -2), new Vector3(2, 0, 2), layerUv, 0, 6, true, false, false, false, layerInflate, true, false);
                    break;
                }
                case Part.LowerLegRight:
                case Part.LowerLegLeft:
                {
                    // Pivot at the knee.
                    var baseUv = part == Part.LowerLegRight ? RightLegUv : LeftLegUv;
                    var layerUv = part == Part.LowerLegRight ? RightPantsUv : LeftPantsUv;
                    AddBox(md, new Vector3(-2, -6, -2), new Vector3(2, 0, 2), baseUv, 6, 12, false, true, true, false, 0f, false, true);
                    AddBox(md, new Vector3(-2, -6, -2), new Vector3(2, 0, 2), layerUv, 6, 12, false, true, false, false, layerInflate, false, true);
                    break;
                }
            }

            var mesh = new Mesh { name = "Blocky_" + part, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(md.Vertices);
            mesh.SetNormals(md.Normals);
            mesh.SetUVs(0, md.Uvs);
            mesh.SetTriangles(md.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>
        /// Adds a (segment of a) textured box. Coordinates are in skin pixels and converted to metres.
        /// Side faces use texture rows [rowStart,rowEnd) of the full box height. Caps are filler faces at a
        /// limb split, textured with the adjacent row of the front face so a bent joint shows cloth, not a hole.
        /// </summary>
        static void AddBox(MeshData md, Vector3 min, Vector3 max, BoxUv uv, int rowStart, int rowEnd,
            bool top, bool bottom, bool capTop, bool capBottom, float inflate, bool inflateTop, bool inflateBottom)
        {
            float x0 = (min.x - inflate) * Px, x1 = (max.x + inflate) * Px;
            float z0 = (min.z - inflate) * Px, z1 = (max.z + inflate) * Px;
            float y0 = (min.y - (inflateBottom ? inflate : 0f)) * Px;
            float y1 = (max.y + (inflateTop ? inflate : 0f)) * Px;

            int u = uv.U, v = uv.V, w = uv.W, h = uv.H, d = uv.D;
            int rows = rowEnd - rowStart;

            // Side faces: rect = (x, y, width, height) in Minecraft texture pixels (top-left origin).
            var rightRect = new RectInt(u, v + d + rowStart, d, rows);
            var frontRect = new RectInt(u + d, v + d + rowStart, w, rows);
            var leftRect = new RectInt(u + d + w, v + d + rowStart, d, rows);
            var backRect = new RectInt(u + d + w + d, v + d + rowStart, w, rows);

            // Each quad: bottom-left, bottom-right, top-left, top-right as seen by a viewer facing the face.
            // Right face (+X): viewer's left is the back (-Z).
            AddQuad(md, new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), Vector3.right, rightRect);
            // Front face (+Z): viewer's left is the character's right (+X).
            AddQuad(md, new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.forward, frontRect);
            // Left face (-X): viewer's left is the front (+Z).
            AddQuad(md, new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), Vector3.left, leftRect);
            // Back face (-Z): viewer's left is -X.
            AddQuad(md, new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), Vector3.back, backRect);

            if (top)
            {
                // Top: image bottom row touches the front face, image left = +X.
                AddQuad(md, new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.up,
                    new RectInt(u + d, v, w, d));
            }

            if (bottom)
            {
                // Bottom: image top row touches the front face, image left = +X.
                AddQuad(md, new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.down,
                    new RectInt(u + d + w, v, w, d));
            }

            if (capTop)
            {
                AddQuad(md, new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.up,
                    new RectInt(u + d, v + d + rowStart, w, 1));
            }

            if (capBottom)
            {
                AddQuad(md, new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.down,
                    new RectInt(u + d, v + d + rowEnd - 1, w, 1));
            }
        }

        static void AddQuad(MeshData md, Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Vector3 normal, RectInt rect)
        {
            int i = md.Vertices.Count;
            md.Vertices.Add(bl);
            md.Vertices.Add(br);
            md.Vertices.Add(tl);
            md.Vertices.Add(tr);
            for (int k = 0; k < 4; k++) md.Normals.Add(normal);

            float s = 1f / TextureSize;
            float uMin = (rect.x + UvInset) * s;
            float uMax = (rect.x + rect.width - UvInset) * s;
            // Minecraft rows grow downwards, Unity V grows upwards.
            float vTop = 1f - (rect.y + UvInset) * s;
            float vBottom = 1f - (rect.y + rect.height - UvInset) * s;
            md.Uvs.Add(new Vector2(uMin, vBottom));
            md.Uvs.Add(new Vector2(uMax, vBottom));
            md.Uvs.Add(new Vector2(uMin, vTop));
            md.Uvs.Add(new Vector2(uMax, vTop));

            // Clockwise as seen from the front: bl -> tl -> tr, bl -> tr -> br.
            md.Triangles.Add(i);
            md.Triangles.Add(i + 2);
            md.Triangles.Add(i + 3);
            md.Triangles.Add(i);
            md.Triangles.Add(i + 3);
            md.Triangles.Add(i + 1);
        }
    }
}
