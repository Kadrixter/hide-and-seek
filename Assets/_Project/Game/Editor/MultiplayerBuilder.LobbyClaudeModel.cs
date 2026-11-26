using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Claude in the lobby corner: a blocky orange mascot (wide body, two tall black eyes, stub arms, four short legs),
    /// about 0.9 m tall. Built as a rig of named pivots that <see cref="LobbyClaude"/> animates at runtime
    /// (Rig = hop / turn, Body = breathing, ArmL / ArmR, Leg0-3, EyeL / EyeR = blinks).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private const float ClaudeLeg = 0.22f, ClaudeBodyW = 1.0f, ClaudeBodyH = 0.62f, ClaudeBodyD = 0.5f;

        private static void BuildLobbyClaude(Transform parent)
        {
            var orange = Mat("ClaudeOrange", new Color(0.85f, 0.47f, 0.34f), null, 0.3f, Vector2.one);
            var orangeDark = Mat("ClaudeOrangeDark", new Color(0.7f, 0.36f, 0.25f), null, 0.25f, Vector2.one);
            var eye = Mat("ClaudeEye", new Color(0.07f, 0.05f, 0.05f), null, 0.75f, Vector2.one);

            var root = new GameObject("LobbyClaude").transform;
            root.SetParent(parent, false);
            root.position = LobbyLayout.Claude.pos;
            root.rotation = Quaternion.Euler(0f, LobbyLayout.Claude.yaw, 0f);

            var rig = Pivot(root, "Rig", Vector3.zero);

            // legs: pivot at the hip, the leg hangs below it
            float[] lx = { -0.36f, -0.12f, 0.12f, 0.36f };
            var legMesh = new ProcMesh().At(new Vector3(0f, -ClaudeLeg * 0.5f, 0f)).RoundBox(new Vector3(0.11f, ClaudeLeg, 0.12f), 0.015f, 2);
            for (int i = 0; i < lx.Length; i++)
            {
                var leg = Pivot(rig, "Leg" + i, new Vector3(lx[i], ClaudeLeg, 0f));
                GMeshObj("LegMesh", leg, legMesh, "ClaudeLeg", orangeDark, Vector3.zero);
            }

            // body: pivot at its bottom centre (breathing scales it upwards)
            var body = Pivot(rig, "Body", new Vector3(0f, ClaudeLeg, 0f));
            GMeshObj("BodyMesh", body, new ProcMesh().At(new Vector3(0f, ClaudeBodyH * 0.5f, 0f)).RoundBox(new Vector3(ClaudeBodyW, ClaudeBodyH, ClaudeBodyD), 0.035f, 4),
                "ClaudeBody", orange, Vector3.zero);

            // eyes: tall dark slits on the front, pivot at their centre (blink = squash Y)
            foreach (var (n, x) in new[] { ("EyeL", -0.2f), ("EyeR", 0.2f) })
            {
                var e = Pivot(body, n, new Vector3(x, 0.4f, ClaudeBodyD * 0.5f + 0.004f));
                GMeshObj("EyeMesh", e, new ProcMesh().At(Vector3.zero).RoundBox(new Vector3(0.075f, 0.17f, 0.02f), 0.012f, 2), "ClaudeEyeMesh", eye, Vector3.zero);
            }

            // stub arms on the sides, pivot at the shoulder
            foreach (var (n, s) in new[] { ("ArmL", -1f), ("ArmR", 1f) })
            {
                var a = Pivot(body, n, new Vector3(s * ClaudeBodyW * 0.5f, 0.3f, 0f));
                GMeshObj("ArmMesh", a, new ProcMesh().At(new Vector3(s * 0.075f, 0f, 0f)).RoundBox(new Vector3(0.16f, 0.14f, 0.16f), 0.02f, 2), "ClaudeArm", orange, Vector3.zero);
            }

            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (ClaudeLeg + ClaudeBodyH) * 0.5f, 0f);
            col.size = new Vector3(ClaudeBodyW + 0.3f, ClaudeLeg + ClaudeBodyH, ClaudeBodyD);
            root.gameObject.AddComponent<LobbyClaude>();
        }

        private static Transform Pivot(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }
    }
}
