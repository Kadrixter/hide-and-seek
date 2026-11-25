using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Wall bead score counter ("liczydło"): a walnut frame with turned finials, a felt panel with a gold scale, two brass
    /// wires of 15 beads each (ebony / ivory in groups of five, the second wire the other way round), a crest with an
    /// 8-ball medallion and an engraved brass "8-BALL" plaque on a board under the frame. Scored beads slide to the left (as seen from the room, local +X).
    /// Space: root on the wall at the middle of the panel, +X along the wall, +Y up, +Z from the wall, back at z = 0.
    /// </summary>
    public static class PoolScoreModel
    {
        public const int Wires = 2, BeadsPerWire = 15;
        /// <summary>Bead length along the wire (pitch of a packed stack) and its radius.</summary>
        public const float BeadL = 0.0225f, BeadR = 0.0145f;
        /// <summary>Half size of the felt panel; the wires run over its whole width.</summary>
        public const float PanelHalfW = 0.43f, PanelHalfH = 0.11f;
        const float WireZ = 0.031f, PostX = 0.462f, RailY = 0.127f;

        public static float WireY(int wire) => wire == 0 ? 0.06f : -0.045f;
        /// <summary>X of bead <paramref name="i"/> when scored (packed from the +X end, on the left as seen from the room).</summary>
        public static float ScoredX(int i) => PanelHalfW - 0.004f - BeadL * (i + 0.5f);
        /// <summary>X of bead <paramref name="i"/> when not scored (packed at the -X end, on the right as seen from the room).</summary>
        public static float RestX(int i) => -PanelHalfW + 0.004f + BeadL * (BeadsPerWire - i - 0.5f);

        public static Transform Build(Transform parent, Material template)
        {
            var root = new GameObject("ScoreCounter").transform;
            root.SetParent(parent, false);
            int layer = parent != null ? parent.gameObject.layer : 0;
            root.gameObject.layer = layer;
            var kit = new TableKit(template, layer, 0);
            Material wood = kit.TableWood, trim = kit.LegWood;
            var felt = kit.Mat(Color.white, 0f, 0.12f, PoolScoreArt.Panel);
            var plaque = kit.Mat(new Color(0.85f, 0.68f, 0.36f), 0.85f, 0.62f, PoolScoreArt.Plaque);
            var medal = kit.Mat(Color.white, 0f, 0.85f, PoolScoreArt.Medal);
            var ivory = kit.Mat(new Color(0.93f, 0.87f, 0.74f), 0f, 0.78f, BoardArt.GrainFine);
            var ebony = kit.Mat(new Color(0.07f, 0.05f, 0.04f), 0.05f, 0.8f, BoardArt.GrainFine);
            var up = CueRackMeshes.Upright;

            // ---- back board, felt panel, rails, posts
            kit.Part("BackBoard", root, BoardMeshes.Extrude("scoreback", RoundRect(0.455f, 0.13f, 0.012f), _ => 0.006f, 0.003f, 3, 4f), wood, new Vector3(0f, 0f, 0.006f), up);
            kit.Part("Panel", root, Quad("scorepanel", PanelHalfW, PanelHalfH), felt, new Vector3(0f, 0f, 0.0122f), Quaternion.identity, false);
            foreach (float sy in new[] { 1f, -1f })
            {
                kit.Part("Rail", root, BoardMeshes.RoundedBox(new Vector3(0.9f, 0.034f, 0.036f), 0.008f, 4), trim, new Vector3(0f, sy * RailY, 0.018f));
                kit.Part("RailInlay", root, BoardMeshes.RoundedBox(new Vector3(0.86f, 0.004f, 0.004f), 0.0018f, 2), kit.Brass, new Vector3(0f, sy * RailY, 0.0358f));
            }
            var post = BoardMeshes.RoundedBox(new Vector3(0.05f, 0.31f, 0.046f), 0.009f, 4);
            foreach (float sx in new[] { -1f, 1f })
            {
                kit.Part("Post", root, post, wood, new Vector3(sx * PostX, 0f, 0.023f));
                kit.Part("FinialTop", root, Finial(), trim, new Vector3(sx * PostX, 0.154f, 0.023f));
                kit.Part("FinialDrop", root, Finial(), trim, new Vector3(sx * PostX, -0.154f, 0.023f), Quaternion.Euler(180f, 0f, 0f));
                kit.Part("PostMedal", root, Stud(0.011f), kit.Brass, new Vector3(sx * PostX, 0f, 0.0455f), Quaternion.Euler(90f, 0f, 0f));
            }

            // ---- wires with ferrules and beads
            var bead = Bead();
            var rod = PoolLampMeshes.Rod(0.0022f, PostX * 2f - 0.02f);
            var ferrule = PoolLampMeshes.Rod(0.0055f, 0.007f);
            for (int w = 0; w < Wires; w++)
            {
                var wire = new GameObject("Wire" + w) { layer = layer }.transform;
                wire.SetParent(root, false);
                wire.localPosition = new Vector3(0f, WireY(w), WireZ);
                kit.Part("Rod", wire, rod, kit.Brass, new Vector3(-PostX + 0.01f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f));
                kit.Part("FerruleL", wire, ferrule, kit.Brass, new Vector3(-PostX + 0.024f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f));
                kit.Part("FerruleR", wire, ferrule, kit.Brass, new Vector3(PostX - 0.031f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f));
                for (int i = 0; i < BeadsPerWire; i++)
                {
                    bool dark = ((i / 5) & 1) == (w == 0 ? 0 : 1);
                    kit.Part("Bead" + i, wire, bead, dark ? ebony : ivory, new Vector3(RestX(i), 0f, 0f), Quaternion.Euler(0f, 0f, 90f));
                }
            }

            // ---- crest with the 8-ball medallion
            float crestY = RailY + 0.016f;
            kit.Part("Crest", root, BoardMeshes.Extrude("scorecrest", CueRackMeshes.ArchOutline(-0.2f, 0.2f, crestY - 0.004f, crestY + 0.02f, 0.066f), _ => 0.007f, 0.003f, 3, 4f), wood, new Vector3(0f, 0f, 0.012f), up);
            Vector3 mc = new Vector3(0f, crestY + 0.046f, 0.0195f);
            kit.Part("Medal", root, Disc("scoremedal", 0.031f), medal, mc, Quaternion.identity, false);
            kit.Part("MedalRing", root, PoolLampMeshes.Ring(0.0325f, 0.003f), kit.Brass, mc);

            // ---- engraved brass plaque on a board under the frame
            float py = -RailY - 0.017f - 0.042f;
            kit.Part("PlaqueBoard", root, BoardMeshes.Extrude("scoreplaqueboard", RoundRect(0.13f, 0.042f, 0.016f), _ => 0.007f, 0.003f, 3, 4f), wood, new Vector3(0f, py, 0.007f), up);
            kit.Part("PlaquePlate", root, BoardMeshes.RoundedBox(new Vector3(0.212f, 0.056f, 0.003f), 0.0012f, 2), kit.Brass, new Vector3(0f, py, 0.0155f));
            kit.Part("PlaqueFace", root, Quad("scoreplaque", 0.104f, 0.026f), plaque, new Vector3(0f, py, 0.0172f), Quaternion.identity, false);
            foreach (var (x, y) in new[] { (-0.096f, 0.02f), (0.096f, 0.02f), (-0.096f, -0.02f), (0.096f, -0.02f) })
                kit.Part("Screw", root, Stud(0.0032f), kit.Brass, new Vector3(x, py + y, 0.017f), Quaternion.Euler(90f, 0f, 0f), false);
            return root;
        }

        /// <summary>Moves <paramref name="count"/> beads of <paramref name="wire"/> to the scored (left) end, the rest to the right.</summary>
        public static void SetBeads(Transform root, int wire, int count)
        {
            var w = root != null ? root.Find("Wire" + wire) : null;
            if (w == null) return;
            count = Mathf.Clamp(count, 0, BeadsPerWire);
            for (int i = 0; i < BeadsPerWire; i++)
            {
                var b = w.Find("Bead" + i);
                if (b != null) b.localPosition = new Vector3(i < count ? ScoredX(i) : RestX(i), 0f, 0f);
            }
        }

        // ================================================================ meshes

        /// <summary>Barrel bead round +Y with a hole for the wire.</summary>
        static Mesh Bead()
        {
            float h = BeadL * 0.5f - 0.0004f;
            var p = new List<Vector2> { new(0.0032f, h), new(0.0032f, -h), new(0.0032f, -h) };
            for (int i = 0; i <= 14; i++)
            {
                float a = Mathf.Lerp(-90f, 90f, i / 14f) * Mathf.Deg2Rad;
                p.Add(new Vector2(0.0058f + (BeadR - 0.0058f) * Mathf.Cos(a), h * Mathf.Sin(a)));
            }
            p.Add(new Vector2(0.0032f, h));
            return BoardMeshes.Lathe("scorebead", p, 28, 10f);
        }

        static Mesh Finial()
        {
            var p = new List<Vector2> { new(0f, 0f), new(0.017f, 0f), new(0.018f, 0.004f), new(0.012f, 0.008f), new(0.009f, 0.012f) };
            BoardMeshes.Arc(p, 0f, 0.024f, 0.013f, -60f, 90f, 10);
            return BoardMeshes.Lathe("scorefinial", p, 24, 6f);
        }

        static Mesh Stud(float r)
        {
            var p = new List<Vector2>();
            BoardMeshes.Arc(p, 0f, -r * 0.4f, r, 0f, 90f, 6);
            p.Insert(0, new Vector2(0f, -r * 0.4f));
            return BoardMeshes.Lathe("scorestud" + r.ToString("0.0000"), p, 20, 6f);
        }

        static List<Vector2> RoundRect(float hw, float hh, float cr)
        {
            var o = new List<Vector2>();
            Vector2[] c = { new(hw - cr, hh - cr), new(-hw + cr, hh - cr), new(-hw + cr, -hh + cr), new(hw - cr, -hh + cr) };
            for (int k = 0; k < 4; k++)
                for (int i = 0; i <= 4; i++)
                {
                    float a = (k * 90f + i * 22.5f) * Mathf.Deg2Rad;
                    o.Add(c[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cr);
                }
            return o;
        }

        static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>Flat rectangle facing +Z, UV 0..1 over it as seen from the front (u runs towards -X).</summary>
        static Mesh Quad(string key, float hw, float hh)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Mesh { name = key };
            m.SetVertices(new List<Vector3> { new(-hw, -hh, 0f), new(hw, -hh, 0f), new(hw, hh, 0f), new(-hw, hh, 0f) });
            m.SetNormals(new List<Vector3> { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });
            m.SetUVs(0, new List<Vector2> { new(1f, 0f), new(0f, 0f), new(0f, 1f), new(1f, 1f) });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return Cache[key] = m;
        }

        /// <summary>Flat disc facing +Z with planar UV (the texture's inscribed circle).</summary>
        static Mesh Disc(string key, float r)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var v = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { new(0.5f, 0.5f) };
            var t = new List<int>();
            const int n = 40;
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
                uv.Add(new Vector2(0.5f - Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                if (i > 0) { t.Add(0); t.Add(i); t.Add(i + 1); }
            }
            var nn = new List<Vector3>();
            foreach (var _ in v) nn.Add(Vector3.forward);
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateBounds();
            return Cache[key] = m;
        }
    }
}
