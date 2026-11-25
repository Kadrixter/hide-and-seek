using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Studio part "room": the lamp over a plain green table placeholder (studio lights dimmed so the lamp's own light
    /// shows), the wall cue rack with placeholder cues and the bead score counter on the wall at z = 6.
    /// </summary>
    public static partial class PoolStudio
    {
        const float RoomCeiling = 2.8f;

        static void Part_room(Ctx s)
        {
            s.Key.enabled = false;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.pixelLightCount = 8; // every spot as a pixel light (with shadows), not per-vertex
            // Shadow-caster variants compiled asynchronously would be skipped in the first (only) frame: compile them now.
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            s.Fill.intensity = 0.06f;
            RenderSettings.ambientLight = new Color(0.07f, 0.07f, 0.08f);
            var ceiling = new Material(s.Template) { color = new Color(0.5f, 0.48f, 0.45f) };
            Prim(new Vector3(0f, RoomCeiling + 0.05f, 0f), new Vector3(30f, 0.1f, 30f), ceiling);
            RoomTable(s);

            var table = s.Root("Table", Vector3.zero);
            var lamp = PoolLampModel.Build(table, s.Template, 0, RoomCeiling, true);
            var table2 = s.Root("Table2", new Vector3(0f, 0f, -3.2f));
            var lamp2 = PoolLampModel.Build(table2, s.Template, 1, RoomCeiling, true);

            s.Shot("lamp_side", new Vector3(0.4f, 1.55f, -3.1f), new Vector3(0f, 1.6f, 0f), 50f);
            s.Shot("lamp_table", new Vector3(-2.3f, 2.1f, -2.0f), new Vector3(0f, 0.9f, 0f), 45f);
            s.Shot("lamp_below", new Vector3(0.35f, 0.95f, -0.5f), new Vector3(0f, 1.9f, 0f), 60f);
            s.Shot("lamp_shade", new Vector3(0.35f, 1.85f, -0.55f), new Vector3(0f, 1.82f, 0f), 40f);
            s.Shot("lamp_balls", new Vector3(0.85f, 0.93f, -0.42f), new Vector3(0.45f, 0.74f, 0.02f), 40f);
            s.Shot("lamp_balls_top", new Vector3(0.62f, 1.3f, -0.1f), new Vector3(0.52f, 0.74f, 0.02f), 30f);
            s.Shot("lamp_chain", new Vector3(-0.5f, 2.35f, -0.45f), new Vector3(-0.72f, 2.35f, 0f), 45f);
            s.Shot("lamp_style1", new Vector3(0.4f, 1.55f, -6.2f), new Vector3(0f, 1.6f, -3.2f), 50f);
            s.Shot("lamp_style1_below", new Vector3(0.35f, 0.95f, -3.7f), new Vector3(0f, 1.9f, -3.2f), 60f);

            // The wall pieces get their own light (the lamps are switched off there: shows SetOn too).
            lamp.SetOn(false);
            lamp2.SetOn(false);
            s.Shot("lamp_off", new Vector3(0.4f, 1.55f, -3.1f), new Vector3(0f, 1.6f, 0f), 50f);
            RoomWall(s);
        }

        /// <summary>Cue rack with placeholder cues, triangle, bridge, chalk and balls; the score counter next to it.</summary>
        static void RoomWall(Ctx s)
        {
            const int slots = 8;
            var rack = s.Root("RackWall", new Vector3(-1.4f, 0f, 5.95f), 180f);
            CueRackModel.Build(rack, s.Template, slots);
            var butt = StudioMat(s, new Color(0.16f, 0.07f, 0.04f), 0f, 0.8f);
            var wrap = StudioMat(s, new Color(0.05f, 0.05f, 0.06f), 0f, 0.3f);
            var maple = StudioMat(s, new Color(0.9f, 0.8f, 0.6f), 0f, 0.7f);
            var white = StudioMat(s, new Color(0.95f, 0.95f, 0.92f), 0f, 0.5f);
            var blue = StudioMat(s, new Color(0.15f, 0.35f, 0.75f), 0f, 0.2f);
            var brass = StudioMat(s, new Color(0.78f, 0.62f, 0.32f), 0.9f, 0.7f);
            for (int i = 0; i < slots; i++)
            {
                if (i == 3 || i == 6) continue; // two empty slots show the cups and notches
                CueRackModel.SlotPose(slots, i, out var p, out var r);
                PlaceholderCue(rack, p, r, butt, wrap, maple, white, blue, null);
            }
            CueRackModel.SlotPose(slots, slots, out var bp, out var br);
            PlaceholderCue(rack, bp, br, butt, wrap, maple, white, blue, brass);

            // Triangle on the peg (apex up, inner corner on the peg).
            Vector3 peg = CueRackModel.TrianglePeg(slots);
            var tri = new GameObject("Triangle").transform;
            tri.SetParent(rack, false);
            tri.localPosition = peg + new Vector3(0f, 0f, -0.015f);
            const float side = 0.30f, bw = 0.024f;
            // Inner triangle: apex at the origin, pointing up; each bar sits outside its inner edge.
            Vector3 a0 = Vector3.zero, a1 = new Vector3(-side * 0.5f, -side * 0.866f, 0f), a2 = new Vector3(side * 0.5f, -side * 0.866f, 0f);
            Vector3 cen = (a0 + a1 + a2) / 3f;
            foreach (var (p0, p1) in new[] { (a0, a1), (a1, a2), (a2, a0) })
            {
                Vector3 mid = (p0 + p1) * 0.5f;
                Vector3 outward = (mid - cen).normalized;
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.transform.SetParent(tri, false);
                bar.transform.localPosition = mid + outward * bw * 0.5f;
                bar.transform.localRotation = Quaternion.LookRotation(Vector3.forward, outward);
                bar.transform.localScale = new Vector3(side + bw * 1.8f, bw, 0.025f);
                bar.GetComponent<Renderer>().sharedMaterial = butt;
            }

            // Balls in the crown cups, chalk on the flat end.
            float outer = (CueRackModel.SlotX(slots, slots) - CueRackModel.SlotX(slots, 0)) * 0.5f + 0.11f;
            Color[] bc = { new Color(0.95f, 0.75f, 0.1f), new Color(0.75f, 0.08f, 0.06f), new Color(0.03f, 0.03f, 0.03f) };
            for (int k = 0; k < 3; k++)
                Sphere(rack, new Vector3(outer - 0.07f * (k + 1), CueRackModel.CrownY + 0.0214f, 0.066f), PoolDims.BallD, StudioMat(s, bc[k], 0f, 0.92f));
            for (int k = 0; k < 2; k++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.transform.SetParent(rack, false);
                c.transform.localPosition = new Vector3(-outer + 0.06f + k * 0.04f, CueRackModel.CrownY + 0.011f, 0.06f);
                c.transform.localRotation = Quaternion.Euler(0f, k * 17f, 0f);
                c.transform.localScale = Vector3.one * 0.022f;
                c.GetComponent<Renderer>().sharedMaterial = blue;
            }

            var score = s.Root("ScoreWall", new Vector3(1.0f, 1.62f, 5.95f), 180f);
            var counter = PoolScoreModel.Build(score, s.Template);
            PoolScoreModel.SetBeads(counter, 0, 7);
            PoolScoreModel.SetBeads(counter, 1, 4);

            var wallSpot = s.AddLight(LightType.Spot, new Vector3(-0.4f, 2.75f, 4.3f), new Vector3(-0.4f, 1.1f, 5.95f), new Color(1f, 0.92f, 0.8f), 2.6f, 7f, 90f, true);
            wallSpot.renderMode = LightRenderMode.ForcePixel;
            wallSpot.shadowStrength = 1f;
            wallSpot.shadowBias = 0.01f;       // the rack sits a few cm off the wall: the default bias eats those shadows
            wallSpot.shadowNormalBias = 0.1f;
            s.AddLight(LightType.Point, new Vector3(-0.3f, 1.4f, 3.2f), new Vector3(0f, 1f, 6f), new Color(0.8f, 0.85f, 1f), 0.5f, 6f);

            s.Shot("rack_front", new Vector3(-1.4f, 1.1f, 3.3f), new Vector3(-1.4f, 1.0f, 5.95f), 50f, 1000, 1100);
            s.Shot("rack_cups", new Vector3(-1.15f, 0.62f, 5.45f), new Vector3(-1.35f, 0.3f, 5.88f), 40f);
            s.Shot("rack_rail", new Vector3(-1.1f, 1.62f, 5.5f), new Vector3(-1.35f, 1.4f, 5.9f), 40f);
            s.Shot("rack_crown", new Vector3(-1.75f, 1.98f, 5.45f), new Vector3(-1.6f, 1.85f, 5.9f), 45f);
            s.Shot("rack_side", new Vector3(-0.3f, 1.2f, 4.6f), new Vector3(-1.4f, 1.0f, 5.9f), 50f, 1000, 1100);
            s.Shot("score_front", new Vector3(1.0f, 1.62f, 4.75f), new Vector3(1.0f, 1.6f, 5.95f), 45f);
            s.Shot("score_close", new Vector3(0.55f, 1.75f, 5.45f), new Vector3(0.75f, 1.62f, 5.92f), 40f);
        }

        static Material StudioMat(Ctx s, Color c, float metal, float gloss)
        {
            var m = new Material(s.Template) { color = c };
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        static void Sphere(Transform parent, Vector3 local, float d, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.transform.localScale = Vector3.one * d;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        /// <summary>Placeholder cue in cue space (butt at z = 0, tip along +Z): butt, wrap, shaft, ferrule, tip; a brass head for the bridge.</summary>
        static void PlaceholderCue(Transform parent, Vector3 pos, Quaternion rot, Material butt, Material wrap, Material maple, Material white, Material blue, Material head)
        {
            var cue = new GameObject("Cue").transform;
            cue.SetParent(parent, false);
            cue.localPosition = pos;
            cue.localRotation = rot;
            float L = PoolDims.CueLength;
            float R(float z) => Mathf.Lerp(PoolDims.ButtD, PoolDims.TipD, Mathf.Pow(z / L, 0.8f)) * 0.5f;
            void Seg(float z0, float z1, Material m)
            {
                var prof = new System.Collections.Generic.List<Vector2> { new(0f, 0f), new(R(z0), 0f), new(R(z0), 0f), new(R(z1), z1 - z0), new(R(z1), z1 - z0), new(0f, z1 - z0) };
                var g = new GameObject("Seg");
                g.transform.SetParent(cue, false);
                g.transform.localPosition = new Vector3(0f, 0f, z0);
                g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                g.AddComponent<MeshFilter>().sharedMesh = HideAndSeek.Game.MiniGames.BoardMeshes.Lathe($"phcue{z0:0.000}_{z1:0.000}", prof, 20, 2f);
                g.AddComponent<MeshRenderer>().sharedMaterial = m;
            }
            Seg(0f, 0.02f, wrap);
            Seg(0.02f, 0.2f, butt);
            Seg(0.2f, 0.45f, wrap);
            Seg(0.45f, 0.735f, butt);
            Seg(0.735f, L - 0.03f, maple);
            Seg(L - 0.03f, L - 0.008f, white);
            Seg(L - 0.008f, L, blue);
            if (head == null) return;
            var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
            h.transform.SetParent(cue, false);
            h.transform.localPosition = new Vector3(0f, 0f, L - 0.02f);
            h.transform.localScale = new Vector3(0.1f, 0.008f, 0.07f);
            h.GetComponent<Renderer>().sharedMaterial = head;
        }

        /// <summary>Plain table placeholder: green cloth slab 2.3 x 1.3 at the cloth height on dark wooden legs, a few balls.</summary>
        static void RoomTable(Ctx s)
        {
            var cloth = new Material(s.Template) { color = new Color(0.05f, 0.33f, 0.16f) };
            cloth.SetFloat("_Glossiness", 0.05f);
            var wood = new Material(s.Template) { color = new Color(0.2f, 0.12f, 0.07f) };
            wood.SetFloat("_Glossiness", 0.45f);
            var ball = new Material(s.Template) { color = new Color(0.95f, 0.93f, 0.86f) };
            ball.SetFloat("_Glossiness", 0.9f);
            foreach (float z in new[] { 0f, -3.2f })
            {
                Prim(new Vector3(0f, PoolDims.ClothY - 0.03f, z), new Vector3(2.3f, 0.06f, 1.3f), cloth);
                Prim(new Vector3(0f, PoolDims.ClothY - 0.17f, z), new Vector3(2.2f, 0.22f, 1.2f), wood);
                foreach (var (x, zz) in new[] { (-0.95f, -0.45f), (0.95f, -0.45f), (-0.95f, 0.45f), (0.95f, 0.45f) })
                    Prim(new Vector3(x, 0.3f, z + zz), new Vector3(0.14f, 0.6f, 0.14f), wood);
                foreach (var (x, zz) in new[] { (-0.5f, 0.1f), (0.5f, 0f), (0.55f, 0.06f), (0.55f, -0.06f), (0.1f, -0.3f) })
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    g.transform.position = PoolDims.BallOnCloth(x, z + zz);
                    g.transform.localScale = Vector3.one * PoolDims.BallD;
                    g.GetComponent<Renderer>().sharedMaterial = ball;
                }
            }
        }
    }
}
