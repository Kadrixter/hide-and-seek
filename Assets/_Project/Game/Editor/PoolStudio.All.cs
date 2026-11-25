using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Studio part "all": the whole pool corner put together - the table (style 0) with the 15 balls racked in the
    /// triangle and the cue ball on the head spot, a cue lying on the cloth, chalk on the rail, the lamp over the table
    /// (lit), the wall cue rack with the four cue designs and the bridge, and the bead score counter on the wall.
    /// </summary>
    public static partial class PoolStudio
    {
        static void Part_all(Ctx s)
        {
            s.Key.enabled = false;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.pixelLightCount = 8;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            s.Fill.intensity = 0.1f;
            RenderSettings.ambientLight = new Color(0.1f, 0.095f, 0.09f);
            const float ceilingY = 2.8f;
            var ceiling = new Material(s.Template) { color = new Color(0.5f, 0.48f, 0.45f) };
            Prim(new Vector3(0f, ceilingY + 0.05f, 0f), new Vector3(30f, 0.1f, 30f), ceiling);

            // ---- table, balls, triangle, cue, chalk, lamp
            var t = s.Root("PoolTable", new Vector3(0f, 0f, 3.1f));
            PoolTableModel.Build(t, s.Template, 0);
            var pos = PoolBallModels.RackPositions();
            var order = PoolBallModels.RackOrder();
            var rng = new System.Random(7);
            for (int i = 0; i < pos.Length; i++)
            {
                var b = PoolBallModels.Build(t, s.Template, order[i]);
                b.transform.localPosition = pos[i];
                b.transform.localRotation = Quaternion.Euler(rng.Next(-35, 35), rng.Next(0, 360), rng.Next(-35, 35));
            }
            PoolRackModel.BuildTriangle(t, s.Template).localPosition = PoolDims.FootSpot;
            var cueBall = PoolBallModels.Build(t, s.Template, 0);
            cueBall.transform.localPosition = PoolDims.BallOnCloth(PoolDims.HeadSpot.x, 0.06f);
            cueBall.transform.localRotation = Quaternion.Euler(20f, 40f, 0f);

            var cue = CueModel.Build(t, s.Template, 1);
            cue.localPosition = new Vector3(-0.85f, PoolDims.ClothY + PoolDims.ButtD * 0.5f, -0.36f);
            cue.localRotation = Quaternion.LookRotation(new Vector3(1f, -0.0124f, 0.06f).normalized, Vector3.up);
            var chalk = PoolChalkModel.Build(t, s.Template, 0.45f);
            chalk.localPosition = new Vector3(0.5f, PoolDims.RailTopY, -(PoolDims.PlayW * 0.5f + 0.09f));
            chalk.localRotation = Quaternion.Euler(0f, 18f, 0f);
            var lamp = PoolLampModel.Build(t, s.Template, 0, ceilingY, true);

            // ---- the wall: cue rack with every cue design + bridge, score counter
            const int slots = 8;
            var rack = s.Root("CueRack", new Vector3(-1.5f, 0f, 5.95f), 180f);
            CueRackModel.Build(rack, s.Template, slots);
            for (int i = 0; i < slots; i++)
            {
                if (i == 2 || i == 5) continue;
                CueRackModel.SlotPose(slots, i, out var p, out var r);
                var c = CueModel.Build(rack, s.Template, i % CueModel.Variants);
                c.localPosition = p;
                c.localRotation = r;
            }
            CueRackModel.SlotPose(slots, slots, out var bp, out var br);
            var bridge = CueBridgeModel.Build(rack, s.Template);
            bridge.localPosition = bp;
            bridge.localRotation = br;

            var score = s.Root("Score", new Vector3(0.9f, 1.62f, 5.95f), 180f);
            var counter = PoolScoreModel.Build(score, s.Template);
            PoolScoreModel.SetBeads(counter, 0, 6);
            PoolScoreModel.SetBeads(counter, 1, 4);
            s.AddLight(LightType.Spot, new Vector3(-0.3f, 2.6f, 4.3f), new Vector3(-0.3f, 1.2f, 5.95f), new Color(1f, 0.9f, 0.78f), 1.6f, 5f, 80f, true);

            // ---- shots
            Vector3 tc = t.position + Vector3.up * PoolDims.ClothY;
            s.Shot("all_hero", tc + new Vector3(-2.6f, 1.25f, -2.4f), tc + new Vector3(0.1f, -0.15f, 0.3f), 48f, 1600, 1000);
            s.Shot("all_wide", new Vector3(3.2f, 2.0f, -0.9f), new Vector3(-0.4f, 0.9f, 4.2f), 55f, 1600, 1000);
            s.Shot("all_break", tc + new Vector3(-1.55f, 0.22f, -0.05f), tc + new Vector3(0.6f, 0f, 0f), 34f, 1600, 1000);
            s.Shot("all_rack", tc + new Vector3(0.12f, 0.32f, -0.42f), PoolDims.FootSpot + t.position + new Vector3(0.1f, 0f, 0f), 40f, 1600, 1000);
            s.Shot("all_cue_chalk", tc + new Vector3(0.1f, 0.3f, -0.95f), tc + new Vector3(0.3f, 0f, -0.4f), 42f, 1600, 1000);
            s.Shot("all_top", tc + new Vector3(0f, 2.0f, -0.01f), tc, 62f, 1600, 1000);
            s.Shot("all_wall", new Vector3(-0.3f, 1.3f, 2.6f), new Vector3(-0.3f, 1.15f, 5.95f), 55f, 1600, 1000);
            s.Shot("all_lamp_up", tc + new Vector3(-0.3f, 0.2f, -0.35f), tc + new Vector3(0.1f, 1.1f, 0f), 60f, 1600, 1000);
            lamp.SetOn(false);
        }
    }
}
