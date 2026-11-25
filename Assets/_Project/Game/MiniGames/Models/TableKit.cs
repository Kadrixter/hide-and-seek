using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Materials and shared furniture of the board-game tables: a game table with turned legs and an apron, a turned
    /// stool with a leather cushion. Built at runtime from a Standard material template (the lobby's own).
    /// </summary>
    public sealed class TableKit
    {
        public readonly Material Template;
        public Material TableWood, LegWood, Leather, Brass, Dark, PieceLight, PieceDark, PieceLightDetail, PieceDarkDetail;
        public readonly int Layer;
        /// <summary>Style variant (0 = classic boxwood / ebony on walnut, 1 = maple / rosewood on dark oak).</summary>
        public readonly int Style;

        public TableKit(Material template, int layer, int style)
        {
            Template = template;
            Layer = layer;
            Style = style;
            bool alt = style == 1;
            TableWood = Mat(alt ? new Color(0.30f, 0.20f, 0.14f) : new Color(0.46f, 0.29f, 0.17f), 0.05f, 0.62f, BoardArt.GrainTable);
            LegWood = Mat(alt ? new Color(0.26f, 0.17f, 0.12f) : new Color(0.40f, 0.25f, 0.15f), 0.05f, 0.55f, BoardArt.GrainTable);
            Leather = Mat(alt ? new Color(0.12f, 0.20f, 0.14f) : new Color(0.36f, 0.09f, 0.07f), 0f, 0.42f, BoardArt.Leather);
            Brass = Mat(new Color(0.78f, 0.62f, 0.32f), 0.9f, 0.72f);
            Dark = Mat(new Color(0.04f, 0.035f, 0.03f), 0.1f, 0.5f);
            PieceLight = Mat(alt ? new Color(0.95f, 0.88f, 0.74f) : new Color(0.93f, 0.83f, 0.64f), 0f, 0.66f, BoardArt.GrainFine);
            PieceDark = Mat(alt ? new Color(0.36f, 0.12f, 0.08f) : new Color(0.075f, 0.055f, 0.045f), 0.05f, 0.74f, BoardArt.GrainFine);
            PieceLightDetail = Mat(new Color(0.16f, 0.11f, 0.08f), 0f, 0.5f);
            PieceDarkDetail = Mat(alt ? new Color(0.1f, 0.04f, 0.03f) : new Color(0.55f, 0.47f, 0.38f), 0f, 0.5f);
        }

        public Material Mat(Color c, float metallic, float gloss, Texture tex = null)
        {
            var m = new Material(Template) { color = c, mainTexture = tex };
            m.DisableKeyword("_EMISSION");
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", gloss);
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.mainTextureScale = Vector2.one;
            return m;
        }

        public Material Glow(Color c, Texture tex, float intensity)
        {
            var m = Mat(Color.black, 0f, 0.5f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            if (tex != null) m.SetTexture("_EmissionMap", tex);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        public GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, bool shadows = true)
        {
            var g = new GameObject(name) { layer = Layer };
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos) => Part(name, parent, mesh, mat, pos, Quaternion.identity);

        public static void Collider(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var g = new GameObject(name) { layer = parent.gameObject.layer };
            g.transform.SetParent(parent, false);
            var c = g.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        /// <summary>
        /// Invisible wall over a table (layer Ignore Raycast: players collide with it, raycasts with the default mask
        /// do not see it), so nobody can jump up and stand on the table.
        /// </summary>
        public static void Blocker(Transform parent, string name, float topY, float width, float depth, float height = 2.4f)
        {
            var g = new GameObject(name) { layer = 2 };
            g.transform.SetParent(parent, false);
            var c = g.AddComponent<BoxCollider>();
            c.center = new Vector3(0f, topY + height * 0.5f, 0f);
            c.size = new Vector3(width, height, depth);
        }

        // ================================================================ table

        /// <summary>
        /// Game table with a rounded top (<paramref name="top"/> x <paramref name="top"/>, corner radius <see cref="CornerRadius"/>, surface at <paramref name="height"/>)
        /// on four turned legs with an apron and corner blocks. Collider: the top and the legs as one box.
        /// </summary>
        /// <summary>Corner radius of the game table's top, seen from above.</summary>
        public const float CornerRadius = 0.14f;

        public void Table(Transform root, float top, float height) => Table(root, top, top, height);

        /// <summary>Rectangular game table: <paramref name="width"/> along local X (the sides without seats), <paramref name="depth"/> along Z (seat to seat).</summary>
        public void Table(Transform root, float width, float depth, float height)
        {
            const float thick = 0.045f;
            // Rounded top: big corner radius in plan and a bullnose edge (more rounded than a plain box, 2026-11-24).
            Part("Top", root, BoardMeshes.RoundedTop(width, depth, CornerRadius, thick, 0.016f), TableWood, new Vector3(0f, height, 0f));
            // Moulded edge under the top, following the same rounded outline.
            Part("TopLip", root, BoardMeshes.RoundedTop(width - 0.03f, depth - 0.03f, CornerRadius - 0.015f, 0.02f, 0.008f), LegWood, new Vector3(0f, height - thick + 0.002f, 0f));
            float insetX = width * 0.5f - 0.075f, insetZ = depth * 0.5f - 0.075f;
            float apronH = 0.085f, apronY = height - thick - 0.018f - apronH * 0.5f;
            // Aprons only on the two sides without a seat (the seated players' knees go under the top).
            float len = depth - 0.17f;
            foreach (float sx in new[] { 1f, -1f })
            {
                Vector3 at = new Vector3(sx * (insetX + 0.005f), apronY, 0f);
                Part("Apron", root, BoardMeshes.RoundedBox(new Vector3(0.024f, apronH, len), 0.005f, 3), LegWood, at);
                // Bead along the bottom edge of the apron.
                Part("ApronBead", root, BoardMeshes.RoundedBox(new Vector3(0.03f, 0.012f, len - 0.02f), 0.005f, 3), TableWood, at + Vector3.down * (apronH * 0.5f));
            }
            var leg = BoardMeshes.Lathe("tableleg" + height.ToString("0.00"), LegProfile(height - thick - 0.018f), 24, 1.5f);
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                Part("Leg", root, leg, LegWood, new Vector3(sx * insetX, 0f, sz * insetZ));
            Collider(root, "TableCollider", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
        }

        /// <summary>Turned leg: square-ish block under the apron, vase, rings, tapering foot.</summary>
        static List<Vector2> LegProfile(float h)
        {
            var p = new List<Vector2>
            {
                new(0f, 0f), new(0.017f, 0f), new(0.017f, 0f), new(0.019f, 0.012f), new(0.016f, 0.03f),
                new(0.014f, 0.12f), new(0.016f, h * 0.38f), new(0.022f, h * 0.45f), new(0.024f, h * 0.5f),
                new(0.019f, h * 0.57f), new(0.015f, h * 0.6f), new(0.021f, h * 0.63f), new(0.021f, h * 0.66f),
                new(0.016f, h * 0.68f), new(0.025f, h * 0.71f), new(0.03f, h * 0.73f), new(0.03f, h * 0.73f),
                new(0.03f, h), new(0.03f, h), new(0f, h),
            };
            return p;
        }

        // ================================================================ stool

        public const float StoolHeight = 0.40f;
        /// <summary>Height of the stool's ring stretcher (the seated player's feet rest on it).</summary>
        public const float FootRing = 0.13f;

        /// <summary>Round turned stool: leather cushion on a wooden seat, four splayed legs, a ring stretcher. No collider.</summary>
        public void Stool(Transform root, Vector3 pos)
        {
            var s = new GameObject("Stool") { layer = Layer }.transform;
            s.SetParent(root, false);
            s.localPosition = pos;
            float h = StoolHeight;
            var seat = new List<Vector2>
            {
                new(0f, h - 0.05f), new(0.175f, h - 0.05f), new(0.175f, h - 0.05f), new(0.185f, h - 0.04f), new(0.185f, h - 0.025f),
                new(0.178f, h - 0.018f), new(0.178f, h - 0.018f), new(0f, h - 0.018f),
            };
            Part("Seat", s, BoardMeshes.Lathe("stoolseat", seat, 40, 2f), TableWood, Vector3.zero);
            var cushion = new List<Vector2>
            {
                new(0f, h - 0.02f), new(0.172f, h - 0.02f), new(0.172f, h - 0.02f), new(0.176f, h - 0.006f), new(0.17f, h + 0.012f),
                new(0.15f, h + 0.026f), new(0.09f, h + 0.034f), new(0f, h + 0.036f),
            };
            Part("Cushion", s, BoardMeshes.Lathe("stoolcushion", cushion, 40, 3f), Leather, Vector3.zero);
            // Brass studs round the cushion.
            var stud = BoardMeshes.Lathe("stud", new List<Vector2> { new(0f, 0f), new(0.006f, 0f), new(0.0055f, 0.003f), new(0.003f, 0.0055f), new(0f, 0.006f) }, 10, 1f);
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                Part("Stud", s, stud, Brass, new Vector3(Mathf.Cos(a) * 0.177f, h - 0.012f, Mathf.Sin(a) * 0.177f), Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))) * Quaternion.Euler(90f, 0f, 0f), false);
            }
            float legLen = h - 0.05f;
            var legP = new List<Vector2>
            {
                new(0f, 0f), new(0.013f, 0f), new(0.013f, 0f), new(0.015f, 0.02f), new(0.017f, legLen * 0.5f), new(0.02f, legLen * 0.62f),
                new(0.016f, legLen * 0.66f), new(0.021f, legLen * 0.7f), new(0.019f, legLen), new(0.019f, legLen), new(0f, legLen),
            };
            var legMesh = BoardMeshes.Lathe("stoolleg", legP, 20, 1.5f);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 foot = outDir * 0.2f, top = outDir * 0.12f + Vector3.up * (h - 0.05f);
                Vector3 axis = (top - foot).normalized;
                Part("StoolLeg", s, legMesh, LegWood, foot, Quaternion.FromToRotation(Vector3.up, axis) * Quaternion.identity);
            }
            // Ring stretcher (torus) joining the legs.
            var ring = new List<Vector2>();
            BoardMeshes.Arc(ring, 0.17f, 0f, 0.009f, -90f, 270f, 14);
            Part("Stretcher", s, BoardMeshes.Lathe("stoolring", ring, 40, 1f), LegWood, Vector3.up * FootRing);
        }
    }
}
