using System.Collections.Generic;
using HideAndSeek.Game.MiniGames;
using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>Preview of the pool balls, the triangle and diamond racks and the chalk (part "balls").</summary>
    public static partial class PoolStudio
    {
        static void Part_balls(Ctx s)
        {
            float cy = PoolDims.ClothY, r = PoolDims.BallR;
            var felt = new Material(s.Template) { color = new Color(0.035f, 0.2f, 0.1f), mainTexture = BoardArt.Felt, mainTextureScale = new Vector2(40f, 40f) };
            felt.SetFloat("_Glossiness", 0.04f);
            Prim(new Vector3(0f, cy - 0.01f, 0f), new Vector3(3f, 0.02f, 2f), felt);
            // A soft light from the camera side for the close-ups (the key spot is high above).
            s.AddLight(LightType.Point, new Vector3(-0.4f, cy + 0.5f, -0.9f), Vector3.zero, new Color(1f, 0.97f, 0.92f), 0.6f, 3f);
            var groups = new List<GameObject>();
            GameObject Group(string name)
            {
                var g = s.Root(name, Vector3.zero).gameObject;
                groups.Add(g);
                return g;
            }
            void Show(GameObject g) { foreach (var x in groups) x.SetActive(x == g); }
            Transform Ball(Transform parent, int n, Vector3 pos)
            {
                var b = PoolBallModels.Build(parent, s.Template, n).transform;
                b.position = pos;
                return b;
            }

            // ---- all sixteen, numbers to the camera
            var row = Group("Row").transform;
            var rowCam = new Vector3(0f, cy + 0.24f, -0.56f);
            for (int n = 0; n < 16; n++)
            {
                var b = Ball(row, n, new Vector3(-0.2625f + (n % 8) * 0.075f, cy + r, n < 8 ? 0.045f : -0.045f));
                Face(b, rowCam, 0);
            }

            // ---- one ball from several sides: number, stripe flank, stripe pole, three-quarter
            var sides = Group("Sides").transform;
            var sideCam = new Vector3(0f, cy + 0.14f, -0.34f);
            int[] sideNums = { 11, 11, 11, 11, 13, 13 };
            for (int i = 0; i < sideNums.Length; i++)
            {
                var b = Ball(sides, sideNums[i], new Vector3(-0.17f + i * 0.068f, cy + r, 0f));
                Face(b, sideCam, i % 4);
                if (i >= 4) b.rotation = Quaternion.AngleAxis(i == 4 ? 35f : -50f, Vector3.up) * Quaternion.AngleAxis(25f, Vector3.right) * b.rotation;
            }

            // ---- 6, 8, 9 close
            var close = Group("Close").transform;
            var closeCam = new Vector3(0f, cy + 0.08f, -0.22f);
            int[] closeNums = { 6, 8, 9 };
            for (int i = 0; i < 3; i++) Face(Ball(close, closeNums[i], new Vector3(-0.062f + i * 0.062f, cy + r, 0f)), closeCam, 0);

            // ---- digit check: the remaining shapes, one- and two-digit
            var digits = Group("Digits").transform;
            var digitCam = new Vector3(0f, cy + 0.24f, -0.29f);
            int[] digitNums = { 2, 3, 4, 5, 7, 10, 12, 14, 15 };
            for (int i = 0; i < digitNums.Length; i++)
            {
                bool back = i < 5;
                float x = back ? -0.124f + i * 0.062f : -0.093f + (i - 5) * 0.062f;
                Face(Ball(digits, digitNums[i], new Vector3(x, cy + r, back ? 0.045f : -0.035f)), digitCam, 0);
            }

            // ---- cue ball
            var cue = Group("Cue").transform;
            var cb1 = Ball(cue, 0, new Vector3(-0.035f, cy + r, 0f));
            var cb2 = Ball(cue, 0, new Vector3(0.035f, cy + r, 0f));
            cb1.rotation = Quaternion.Euler(20f, 10f, 0f);
            cb2.rotation = Quaternion.Euler(-60f, 140f, 30f);

            // ---- the racked triangle
            var rack = Group("Rack").transform;
            var tableRoot = new GameObject("TableSpace").transform;
            tableRoot.SetParent(rack, false);
            tableRoot.localPosition = new Vector3(-PoolDims.FootSpot.x - 2f * PoolBallModels.RowStep, 0f, 0f);
            var tri = PoolRackModel.BuildTriangle(tableRoot, s.Template);
            tri.localPosition = new Vector3(PoolDims.FootSpot.x, cy, 0f);
            var pos = PoolBallModels.RackPositions();
            var order = PoolBallModels.RackOrder();
            Random.InitState(7);
            for (int i = 0; i < 15; i++)
            {
                var b = PoolBallModels.Build(tableRoot, s.Template, order[i]).transform;
                b.localPosition = pos[i];
                b.localRotation = Random.rotationUniform;
            }
            float rackMid = 0f;

            // ---- empty triangle and diamond side by side
            var racks = Group("Racks").transform;
            PoolRackModel.BuildTriangle(racks, s.Template).localPosition = new Vector3(-0.30f, cy, 0.02f);
            var dia = PoolRackModel.BuildDiamond(racks, s.Template);
            dia.localPosition = new Vector3(0.06f, cy, 0.0f);
            var diaBalls = PoolRackModel.DiamondBalls();
            int[] diaNums = { 1, 3, 6, 2, 9, 7, 4, 8, 5 };
            for (int i = 0; i < diaBalls.Length; i++)
            {
                var b = PoolBallModels.Build(racks, s.Template, diaNums[i]).transform;
                b.position = new Vector3(0.06f + diaBalls[i].x, cy + r, diaBalls[i].y);
                b.rotation = Random.rotationUniform;
            }

            // ---- chalk, new and worn
            var chalk = Group("Chalk").transform;
            PoolChalkModel.Build(chalk, s.Template, 0f).localPosition = new Vector3(-0.02f, cy, 0f);
            var worn = PoolChalkModel.Build(chalk, s.Template, 1f);
            worn.localPosition = new Vector3(0.02f, cy, 0.004f);
            worn.localRotation = Quaternion.Euler(0f, -28f, 0f);

            Show(row.gameObject);
            s.Shot("balls_row", rowCam, new Vector3(0f, cy + r, 0f), 45f);
            Show(sides.gameObject);
            s.Shot("balls_sides", sideCam, new Vector3(0f, cy + r, 0f), 40f);
            Show(close.gameObject);
            s.Shot("balls_close_689", closeCam, new Vector3(0f, cy + r, 0f), 38f);
            s.Shot("balls_close_8", closeCam + new Vector3(0.0f, -0.02f, 0.12f), new Vector3(0f, cy + r, 0f), 30f);
            Show(digits.gameObject);
            s.Shot("balls_digits", digitCam, new Vector3(0f, cy + r, 0f), 36f);
            Show(cue.gameObject);
            s.Shot("balls_cue", new Vector3(0f, cy + 0.1f, -0.2f), new Vector3(0f, cy + r, 0f), 32f);
            Show(rack.gameObject);
            s.Shot("rack_top", new Vector3(rackMid, cy + 0.75f, -0.0005f), new Vector3(rackMid, cy, 0f), 30f);
            s.Shot("rack_34", new Vector3(rackMid - 0.42f, cy + 0.32f, -0.36f), new Vector3(rackMid + 0.02f, cy + 0.01f, 0f), 34f);
            s.Shot("rack_back", new Vector3(rackMid + 0.38f, cy + 0.16f, -0.12f), new Vector3(rackMid + 0.11f, cy + 0.015f, 0f), 34f);
            Show(racks.gameObject);
            s.Shot("racks", new Vector3(-0.06f, cy + 0.55f, -0.45f), new Vector3(-0.06f, cy, 0.02f), 40f);
            s.Shot("diamond", new Vector3(0.06f + 0.30f, cy + 0.22f, -0.20f), new Vector3(0.06f + 0.10f, cy + 0.015f, 0f), 32f);
            s.Shot("rack_corner", new Vector3(-0.30f - 0.12f, cy + 0.07f, 0.02f - 0.10f), new Vector3(-0.30f, cy + 0.015f, 0.02f), 30f);
            Show(chalk.gameObject);
            s.Shot("chalk", new Vector3(0f, cy + 0.06f, -0.09f), new Vector3(0f, cy + 0.011f, 0f), 34f);
            s.Shot("chalk_top", new Vector3(0.02f, cy + 0.075f, -0.035f), new Vector3(0.02f, cy + 0.018f, 0.004f), 30f);
        }

        /// <summary>Turns ball <paramref name="b"/> so that a number (0), the plain stripe flank (1), the stripe pole (2) or a number from 45 deg (3) faces <paramref name="cam"/>.</summary>
        static void Face(Transform b, Vector3 cam, int mode)
        {
            Vector3 v = (cam - b.position).normalized;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, v).normalized;
            switch (mode)
            {
                case 1: b.rotation = Quaternion.LookRotation(up, Vector3.Cross(up, v)); break;
                case 2: b.rotation = Quaternion.LookRotation(v, up) * Quaternion.Euler(0f, 0f, 90f); break;
                case 3: b.rotation = Quaternion.AngleAxis(45f, up) * Quaternion.LookRotation(up, v); break;
                default: b.rotation = Quaternion.LookRotation(up, v); break;
            }
        }
    }
}
