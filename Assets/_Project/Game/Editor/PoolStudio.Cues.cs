using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    public static partial class PoolStudio
    {
        /// <summary>
        /// Cues and the bridge on a strip of green cloth: the 4 variants side by side (butt at -X, tip at +X), the
        /// bridge behind them with a cue resting in its middle notch, two cues unscrewed. Close-ups of every detail.
        /// </summary>
        static void Part_cues(Ctx s)
        {
            const float cloth = 0.74f;
            var felt = new Material(s.Template) { color = new Color(0.05f, 0.30f, 0.17f) };
            felt.SetFloat("_Glossiness", 0.08f);
            Prim(new Vector3(0f, cloth - 0.02f, 0.12f), new Vector3(2.4f, 0.04f, 1.2f), felt);
            s.AddLight(LightType.Point, new Vector3(0.1f, 1.25f, -0.75f), Vector3.zero, new Color(1f, 0.96f, 0.9f), 0.9f, 3f);

            float half = PoolDims.CueLength * 0.5f;
            // Lying cue: butt bumper on the cloth at -X, tip on the cloth at +X.
            Transform Lay(Transform t, float z, float rButt, float rTip)
            {
                var a = new Vector3(-half, cloth + rButt, z);
                var b = new Vector3(half, cloth + rTip, z);
                t.SetPositionAndRotation(a, Quaternion.LookRotation(b - a, Vector3.up));
                return t;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int v = 0; v < CueModel.Variants; v++)
                Lay(CueModel.Build(null, s.Template, v), -0.2f + v * 0.065f, CueModel.BumperR, CueModel.TipR);

            Debug.Log($"[PoolStudio] 4 cues built (textures included) in {watch.ElapsedMilliseconds} ms");
            // Bridge on its feet, a cue (variant 2) resting in the middle notch with the butt raised.
            var bridge = CueBridgeModel.Build(null, s.Template);
            Lay(bridge, 0.1f, 0.012f, CueBridgeModel.AxisAboveFeet);
            Vector3 head = bridge.position + bridge.forward * CueBridgeModel.Length;
            Vector3 notch = head + Vector3.up * (CueBridgeModel.NotchHeights[1] - CueBridgeModel.AxisAboveFeet) - bridge.forward * 0.002f;
            var rest = CueModel.Build(null, s.Template, 2);
            Vector3 dir = Quaternion.AngleAxis(-7f, Vector3.forward) * Vector3.right; // tip down 7 degrees
            float zAtNotch = PoolDims.CueLength - 0.12f;
            Vector3 axisAtNotch = notch + Vector3.up * CueModel.RadiusAt(zAtNotch) / Mathf.Cos(7f * Mathf.Deg2Rad);
            rest.SetPositionAndRotation(axisAtNotch - dir * zAtNotch, Quaternion.LookRotation(dir, Vector3.up));

            // Unscrewed cues: shaft pulled off the pin and laid beside the butt.
            for (int i = 0; i < 2; i++)
            {
                int v = i == 0 ? 1 : 3;
                float z = 0.3f + i * 0.12f;
                CueModel.BuildSplit(null, s.Template, v, out var shaft, out var butt);
                Lay(butt, z, CueModel.BumperR, CueModel.JointR);
                shaft.SetPositionAndRotation(butt.position + new Vector3(0.06f, 0f, 0.035f), butt.rotation);
            }

            // ---- overview
            s.Shot("cues_overview", new Vector3(0f, 1.62f, -1.05f), new Vector3(0f, cloth, 0.08f), 50f);
            s.Shot("cues_side", new Vector3(-0.1f, 0.86f, -0.75f), new Vector3(-0.1f, cloth + 0.01f, -0.1f), 62f, 1600, 700);
            // ---- tips, ferrules, chalk
            s.Shot("tips_row", new Vector3(0.84f, 0.83f, -0.27f), new Vector3(0.71f, cloth + 0.008f, -0.11f), 34f);
            s.Shot("tip_closeup", new Vector3(0.775f, 0.775f, -0.175f), new Vector3(0.725f, cloth + 0.007f, -0.135f), 30f);
            // ---- joints and rings
            s.Shot("joints", new Vector3(0.05f, 0.86f, -0.33f), new Vector3(-0.005f, cloth + 0.01f, -0.1f), 30f);
            s.Shot("joint_closeup", new Vector3(0.03f, 0.80f, -0.2f), new Vector3(-0.005f, cloth + 0.011f, -0.135f), 28f);
            // ---- forearms (points / spears / floating pearl points)
            s.Shot("forearms", new Vector3(-0.15f, 0.92f, -0.43f), new Vector3(-0.15f, cloth + 0.01f, -0.1f), 34f);
            s.Shot("points_closeup", new Vector3(-0.17f, 0.82f, -0.25f), new Vector3(-0.17f, cloth + 0.012f, -0.135f), 32f);
            // ---- wraps
            s.Shot("wraps", new Vector3(-0.40f, 0.88f, -0.40f), new Vector3(-0.40f, cloth + 0.012f, -0.1f), 30f);
            s.Shot("wrap_closeup", new Vector3(-0.38f, 0.79f, -0.16f), new Vector3(-0.38f, cloth + 0.013f, -0.08f), 26f);
            // ---- sleeves, butt caps, bumpers
            s.Shot("sleeves", new Vector3(-0.60f, 0.88f, -0.40f), new Vector3(-0.62f, cloth + 0.013f, -0.1f), 30f);
            s.Shot("butts", new Vector3(-0.88f, 0.82f, -0.33f), new Vector3(-0.72f, cloth + 0.014f, -0.1f), 32f);
            s.Shot("bumper_logo", new Vector3(-0.83f, cloth + 0.03f, -0.165f), new Vector3(-0.735f, cloth + 0.015f, -0.135f), 28f);
            // ---- unscrewed
            s.Shot("split", new Vector3(0.06f, 0.92f, 0.08f), new Vector3(0.02f, cloth + 0.01f, 0.36f), 36f);
            s.Shot("split_closeup", new Vector3(0.07f, 0.80f, 0.23f), new Vector3(0.025f, cloth + 0.011f, 0.315f), 30f);
            s.Shot("insert_closeup", new Vector3(0.012f, 0.79f, 0.29f), new Vector3(0.06f, cloth + 0.011f, 0.335f), 34f);
            s.Shot("pin_closeup", new Vector3(0.045f, 0.785f, 0.265f), new Vector3(0.01f, cloth + 0.011f, 0.30f), 30f);
            // ---- bridge
            s.Shot("bridge", new Vector3(0.2f, 1.0f, -0.35f), new Vector3(0.35f, cloth + 0.03f, 0.1f), 45f);
            s.Shot("bridge_front", new Vector3(1.02f, 0.79f, 0.1f), new Vector3(0.735f, cloth + 0.03f, 0.1f), 24f);
            s.Shot("bridge_head", new Vector3(0.93f, 0.83f, -0.08f), new Vector3(0.735f, cloth + 0.04f, 0.1f), 30f);
            s.Shot("bridge_head_back", new Vector3(0.58f, 0.86f, 0.24f), new Vector3(0.725f, cloth + 0.035f, 0.1f), 32f);
        }
    }
}
