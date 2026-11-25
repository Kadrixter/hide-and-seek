using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Studio part "return": the ball return under the pool table (funnels, drop cages, the two gullies of twin rods,
    /// the tray under the foot rail) with placeholder balls lying in the tray, rolling on the rods and falling through
    /// a cage; views from outside, from low down, from below and cut views (cloth, rails and apron hidden). All styles.
    /// </summary>
    public static partial class PoolStudio
    {
        static void Part_return(Ctx s)
        {
            s.Key.transform.position = new Vector3(0f, 2.45f, -0.15f);
            s.Key.transform.LookAt(new Vector3(0f, 0.74f, 0f));
            s.Key.spotAngle = 105f;
            s.Key.range = 7f;
            s.Key.intensity = 2.3f;
            s.AddLight(LightType.Spot, new Vector3(1.6f, 1.5f, -2.6f), new Vector3(0f, 0.45f, 0f), new Color(1f, 0.95f, 0.88f), 1.1f, 7f, 60f);
            s.AddLight(LightType.Spot, new Vector3(2.6f, 0.9f, 0.9f), new Vector3(1.1f, 0.45f, 0f), new Color(1f, 0.95f, 0.9f), 0.9f, 6f, 50f);
            s.AddLight(LightType.Point, new Vector3(0.2f, 0.22f, -0.9f), new Vector3(0f, 0.5f, 0f), new Color(0.9f, 0.9f, 1f), 0.45f, 2.5f);

            for (int style = 0; style <= 2; style++)
            {
                var root = s.Root("PoolTable" + style, Vector3.zero);
                PoolTableModel.Build(root, s.Template, style);
                ReturnBalls(s, root);
                string p = style == 0 ? "r" : "r" + style;
                s.Shot(p + "_foot", new Vector3(2.15f, 0.78f, -0.55f), new Vector3(1.12f, 0.5f, -0.05f), 40f);
                s.Shot(p + "_tray", new Vector3(1.62f, 0.66f, -0.42f), new Vector3(1.19f, 0.465f, -0.08f), 34f);
                if (style == 0)
                {
                    s.Shot("r_hero", new Vector3(-2.3f, 1.85f, -2.5f), new Vector3(0.05f, 0.58f, 0f), 40f);
                    s.Shot("r_lowside", new Vector3(0.3f, 0.42f, -2.3f), new Vector3(0.1f, 0.5f, -0.4f), 55f);
                    s.Shot("r_corner_foot", new Vector3(1.75f, 0.52f, -1.25f), new Vector3(1.0f, 0.52f, -0.45f), 42f);
                    s.Shot("r_corner_head", new Vector3(-1.55f, 0.62f, -1.05f), new Vector3(-1.0f, 0.6f, -0.52f), 40f);
                    s.Shot("r_sidepocket", new Vector3(0.32f, 0.52f, -1.05f), new Vector3(0.0f, 0.58f, -0.56f), 40f);
                    s.Shot("r_under", new Vector3(0.1f, 0.1f, -0.05f), new Vector3(0.55f, 0.6f, -0.45f), 75f);
                    s.Shot("r_under_head", new Vector3(-0.1f, 0.1f, 0.05f), new Vector3(-0.7f, 0.6f, -0.4f), 75f);
                    s.Shot("r_under_foot", new Vector3(0.55f, 0.12f, 0.1f), new Vector3(1.1f, 0.5f, -0.2f), 70f);
                    Cut(root, false);
                    s.Shot("r_cut_top", new Vector3(0.02f, 3.4f, -0.05f), new Vector3(0.02f, 0.5f, 0f), 42f);
                    s.Shot("r_cut_persp", new Vector3(-0.6f, 1.75f, -1.75f), new Vector3(0.35f, 0.5f, 0f), 45f);
                    s.Shot("r_cut_foot", new Vector3(1.75f, 1.15f, -0.95f), new Vector3(0.95f, 0.5f, -0.2f), 45f);
                    Cut(root, true);
                    s.Shot("r_cut_bare", new Vector3(-0.6f, 1.75f, -1.75f), new Vector3(0.35f, 0.5f, 0f), 45f);
                    s.Shot("r_cut_bare_side", new Vector3(0.2f, 0.62f, -1.9f), new Vector3(0.2f, 0.52f, 0f), 50f);
                }
                Object.DestroyImmediate(root.gameObject);
            }
        }

        /// <summary>Hides the cloth, the rails and everything on them (and with <paramref name="frame"/> the apron and the beams too).</summary>
        static void Cut(Transform root, bool frame)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                string n = r.name;
                bool top = n.StartsWith("Cloth") || n.StartsWith("Slate") || n == "Rails" || n == "RailEdge" || n == "Cushions"
                           || n == "PocketCastings" || n == "PocketLiners" || n == "CastingScrews" || n.StartsWith("Sight");
                bool fr = n.StartsWith("Apron") || n == "FrameBeams" || n.StartsWith("Maker") || n.StartsWith("Plate");
                if (top || frame && fr) r.enabled = false;
            }
        }

        /// <summary>Placeholder balls: four and three in the tray, two rolling down a gully, one on the turn, one falling through a cage.</summary>
        static void ReturnBalls(Ctx s, Transform root)
        {
            Color[] cols =
            {
                new(0.95f, 0.78f, 0.1f), new(0.1f, 0.2f, 0.7f), new(0.8f, 0.1f, 0.08f), new(0.35f, 0.12f, 0.5f), new(0.95f, 0.45f, 0.08f),
                new(0.08f, 0.45f, 0.2f), new(0.45f, 0.1f, 0.08f), new(0.03f, 0.03f, 0.03f), new(0.95f, 0.94f, 0.9f),
            };
            var l0 = PoolReturnTrack.Gully(0);
            var l1 = PoolReturnTrack.Gully(1);
            float d = PoolDims.BallD;
            for (int k = 0; k < 4; k++) Ball(s, root, PoolReturnTrack.At(l0, l0.Length - k * d), cols[k]);
            for (int k = 0; k < 3; k++) Ball(s, root, PoolReturnTrack.At(l1, l1.Length - k * d), cols[4 + k]);
            Ball(s, root, PoolReturnTrack.At(l0, (l0.TurnU0 + l0.TurnU1) * 0.55f), cols[7]);
            Ball(s, root, PoolReturnTrack.At(l0, 0.55f), cols[8]);
            Ball(s, root, PoolReturnTrack.At(l0, 1.4f), cols[2]);
            var drop = PoolReturnTrack.DropPlan(1);
            Ball(s, root, new Vector3(drop.x, 0.62f, drop.y), cols[5]);
        }
    }
}
