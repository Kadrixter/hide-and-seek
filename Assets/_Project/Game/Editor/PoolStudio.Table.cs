using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>Studio part "table": the pool table (all styles) with placeholder balls for scale, whole views and close-ups.</summary>
    public static partial class PoolStudio
    {
        static void Part_table(Ctx s)
        {
            // Billiard-lamp lighting: a wide soft spot over the cloth plus a weak front spot for the apron and legs.
            s.Key.transform.position = new Vector3(0f, 2.45f, -0.15f);
            s.Key.transform.LookAt(new Vector3(0f, 0.74f, 0f));
            s.Key.spotAngle = 105f;
            s.Key.range = 7f;
            s.Key.intensity = 2.3f;
            s.AddLight(LightType.Spot, new Vector3(1.6f, 1.5f, -2.6f), new Vector3(0f, 0.45f, 0f), new Color(1f, 0.95f, 0.88f), 1.1f, 7f, 60f);
            s.AddLight(LightType.Point, new Vector3(-0.4f, 0.25f, -0.9f), new Vector3(0f, 0.5f, 0f), new Color(0.9f, 0.9f, 1f), 0.35f, 2.5f);

            var root = s.Root("PoolTable", Vector3.zero);
            PoolTableModel.Build(root, s.Template, 0);
            Balls(s, root);

            float y = PoolDims.ClothY;
            s.Shot("t_hero", new Vector3(-2.3f, 1.85f, -2.5f), new Vector3(0.05f, 0.58f, 0f), 40f);
            s.Shot("t_side", new Vector3(0f, 0.85f, -3.6f), new Vector3(0f, 0.5f, 0f), 40f);
            s.Shot("t_top", new Vector3(0f, 4.6f, -0.02f), new Vector3(0f, y, 0f), 40f);
            s.Shot("t_corner", new Vector3(1.42f, 1.08f, -1.05f), new Vector3(1.03f, 0.75f, -0.54f), 32f);
            s.Shot("t_sidepocket", new Vector3(0.15f, 1.02f, -1.15f), new Vector3(0f, 0.75f, -0.56f), 30f);
            s.Shot("t_diamond", new Vector3(0.62f, 0.92f, -0.82f), new Vector3(0.495f, 0.786f, -0.595f), 16f);
            s.Shot("t_jaw", new Vector3(0f, 0.8f, -0.22f), new Vector3(0f, 0.765f, -0.55f), 45f);
            s.Shot("t_facing", new Vector3(1.1f, 0.86f, -0.46f), new Vector3(0.97f, 0.77f, -0.53f), 40f);
            s.Shot("t_leg", new Vector3(1.55f, 0.42f, -1.25f), new Vector3(0.9f, 0.3f, -0.42f), 38f);
            s.Shot("t_bag", new Vector3(1.45f, 0.3f, -0.95f), new Vector3(1.03f, 0.62f, -0.54f), 38f);
            s.Shot("t_under", new Vector3(0.3f, 0.12f, -0.15f), new Vector3(0.9f, 0.6f, -0.5f), 60f);
            s.Shot("t_notch", new Vector3(-0.3f, 0.6f, -1.05f), new Vector3(-0.07f, 0.64f, -0.62f), 28f);
            s.Shot("t_plate", new Vector3(1.75f, 0.65f, 0f), new Vector3(1.116f, 0.648f, 0f), 18f);
            Object.DestroyImmediate(root.gameObject);

            for (int style = 1; style <= 2; style++)
            {
                root = s.Root("PoolTable" + style, Vector3.zero);
                PoolTableModel.Build(root, s.Template, style);
                Balls(s, root);
                s.Shot($"t{style}_hero", new Vector3(-2.3f, 1.85f, -2.5f), new Vector3(0.05f, 0.58f, 0f), 40f);
                s.Shot($"t{style}_corner", new Vector3(1.42f, 1.08f, -1.05f), new Vector3(1.03f, 0.75f, -0.54f), 32f);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        /// <summary>Placeholder balls (plain spheres): a racked triangle on the foot spot, the cue ball on the head spot, one in a jaw, one in the return's tray.</summary>
        static void Balls(Ctx s, Transform root)
        {
            Color[] cols =
            {
                new(0.95f, 0.78f, 0.1f), new(0.1f, 0.2f, 0.7f), new(0.8f, 0.1f, 0.08f), new(0.35f, 0.12f, 0.5f), new(0.95f, 0.45f, 0.08f),
                new(0.08f, 0.45f, 0.2f), new(0.45f, 0.1f, 0.08f), new(0.03f, 0.03f, 0.03f),
            };
            float d = PoolDims.BallD;
            int n = 0;
            for (int row = 0; row < 5; row++)
                for (int j = 0; j <= row; j++, n++)
                {
                    var p = PoolDims.BallOnCloth(PoolDims.FootSpot.x + row * d * 0.8665f, (j - row * 0.5f) * d * 1.0005f);
                    Ball(s, root, p, n == 4 ? cols[7] : cols[n % 7]);
                }
            Ball(s, root, PoolDims.BallOnCloth(PoolDims.HeadSpot.x, 0f), new Color(0.95f, 0.94f, 0.9f));
            Ball(s, root, PoolDims.BallOnCloth(0.93f, -0.455f), cols[2]);
            Ball(s, root, PoolDims.BallOnCloth(0.02f, -0.47f), cols[0]);
            var tray = PoolReturnTrack.Gully(0);
            Ball(s, root, PoolReturnTrack.At(tray, tray.Length), cols[5]);
        }

        static void Ball(Ctx s, Transform root, Vector3 pos, Color c)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.transform.SetParent(root, false);
            g.transform.localPosition = pos;
            g.transform.localScale = Vector3.one * PoolDims.BallD;
            var m = new Material(s.Template) { color = c };
            m.SetFloat("_Glossiness", 0.9f);
            g.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
