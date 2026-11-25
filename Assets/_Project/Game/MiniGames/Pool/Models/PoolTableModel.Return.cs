using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;
using T = HideAndSeek.Game.MiniGames.Pool.PoolReturnTrack;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public sealed partial class PoolTableModel
    {
        /// <summary>Funnel bottom (below the cloth) and radius; radius of the drop cage's rods round the falling ball.</summary>
        const float FunnelDrop = 0.05f, FunnelBotR = 0.0385f, CageR = 0.0345f, CageRodR = 0.0027f;

        /// <summary>
        /// The ball return (see <see cref="PoolReturnTrack"/>): under every hole the leather throat ends in a metal collar
        /// and a spun funnel, under it a cage of four rods with two hoops guides the falling ball onto the gully; the two
        /// gullies (pairs of rods with a hairpin end and U spacers) run to the tray. Brackets and the tray: .Tray.
        /// </summary>
        void BuildReturn()
        {
            for (int i = 0; i < 6; i++)
            {
                bool side = G.IsSide(i);
                float r = G.HoleR(i);
                string key = side ? "side" : "corner";
                var g = Group("Pocket" + i, PocketCentre(i), Quaternion.Euler(0f, i * 37f, 0f));
                Part("Throat", BoardMeshes.Lathe("poolthroat_" + key, ThroatProfile(r), 40, 3f), _leather, g);
                Part("Collar", BoardMeshes.Lathe("poolcollar_" + key, CollarProfile(r), 40, 3f), _metal, g);
                Part("Funnel", BoardMeshes.Lathe("poolfunnel_" + key, FunnelProfile(r), 40, 3f), _metal, g);
            }
            Part("DropCages", CageMesh(), _metal);
            Part("ReturnRails", GullyMesh(), _metal);
            BuildTray();
        }

        /// <summary>Thin leather tube (outside up, over the top, inside down) from just under the cloth to the collar.</summary>
        static List<Vector2> ThroatProfile(float r)
        {
            float top = -0.004f, bot = G.ThroatY;
            return new List<Vector2>
            {
                new(r - 0.001f, bot), new(r - 0.001f, top), new(r - 0.001f, top), new(r - 0.0025f, top + 0.001f),
                new(r - 0.004f, top), new(r - 0.004f, top), new(r - 0.004f, bot),
            };
        }

        /// <summary>Rolled metal ring clamping the bottom of the leather throat.</summary>
        static List<Vector2> CollarProfile(float r)
        {
            var p = new List<Vector2>();
            PoolTableMesh.Arc(p, new Vector2(r + 0.0012f, G.ThroatY + 0.002f), 0.0042f, -90f, 270f, 16);
            return p;
        }

        /// <summary>Spun sheet-metal funnel from the collar down to the cage (outside down, a rolled bottom edge, inside up).</summary>
        static List<Vector2> FunnelProfile(float r)
        {
            float top = G.ThroatY + 0.001f, bot = G.ThroatY - FunnelDrop, mid = Mathf.Lerp(top, bot, 0.35f);
            var p = new List<Vector2> { new(FunnelBotR - 0.0012f, bot + 0.002f) };
            PoolTableMesh.Arc(p, new Vector2(FunnelBotR + 0.0004f, bot + 0.002f), 0.0016f, 180f, 360f, 6);
            p.Add(new Vector2(FunnelBotR + 0.002f, bot + 0.006f));
            p.Add(new Vector2(Mathf.Lerp(FunnelBotR, r, 0.75f) + 0.0015f, mid));
            p.Add(new Vector2(r + 0.0015f, top));
            p.Add(new Vector2(r + 0.0015f, top));
            p.Add(new Vector2(r, top));
            p.Add(new Vector2(r, top));
            p.Add(new Vector2(Mathf.Lerp(FunnelBotR, r, 0.75f), mid - 0.0005f));
            p.Add(new Vector2(FunnelBotR - 0.0012f, bot + 0.006f));
            p.Add(new Vector2(FunnelBotR - 0.0012f, bot + 0.002f));
            return p;
        }

        /// <summary>Bottom of the drop cage over pocket i's landing: a passing ball's top clears its hoop.</summary>
        public static float CageBottomY(int pocket) => T.Landing(pocket).y + PoolDims.BallR + 0.0075f;

        /// <summary>Four rods round the falling ball from inside the funnel down to just over the gully, a hoop at the bottom and one higher up.</summary>
        static Mesh CageMesh()
        {
            if (PoolTableMesh.TryGet("pool_cages", out var m)) return m;
            var b = new PoolTableMesh();
            float top = PoolDims.ClothY + G.ThroatY - FunnelDrop + 0.012f;
            for (int i = 0; i < 6; i++)
            {
                var c = PocketCentre(i);
                float bot = CageBottomY(i);
                T.At(T.Gully(T.SideOf(i)), T.LandU(i), out var h);
                Vector3 l = T.Left(h);
                for (int k = 0; k < 4; k++)
                {
                    float a = (45f + 90f * k) * Mathf.Deg2Rad;
                    Vector3 o = (h * Mathf.Cos(a) + l * Mathf.Sin(a)) * CageR;
                    PoolReturnMesh.Rod(b, new[] { new Vector3(c.x, top, c.z) + o, new Vector3(c.x, bot, c.z) + o }, CageRodR, 8);
                }
                PoolReturnMesh.Ring(b, new Vector3(c.x, bot, c.z), Vector3.up, CageR, 0.0026f, 28, 8);
                PoolReturnMesh.Ring(b, new Vector3(c.x, Mathf.Lerp(bot, top, 0.55f), c.z), Vector3.up, CageR, 0.0022f, 28, 8);
            }
            return b.Bake("pool_cages");
        }

        /// <summary>Both gullies: two rods each (on past the divider's face) joined in a hairpin behind the head drop, U spacers under the rods.</summary>
        static Mesh GullyMesh()
        {
            if (PoolTableMesh.TryGet("pool_gully", out var m)) return m;
            var b = new PoolTableMesh();
            for (int side = 0; side < 2; side++)
            {
                var l = T.Gully(side);
                int n = l.P.Length;
                for (int s = -1; s <= 1; s += 2)
                {
                    var pts = new List<Vector3>(n + 1);
                    for (int i = 0; i < n; i++) pts.Add(T.Rod(l.P[i], l.H[i], s));
                    // Into the divider: on past the stop by a ball radius.
                    pts.Add(T.Rod(l.P[n - 1] + l.H[n - 1] * (PoolDims.BallR + T.DividerHalf * 0.5f), l.H[n - 1], s));
                    PoolReturnMesh.Rod(b, pts, T.RodR, 10);
                }
                // Upstream end: the two rods bent round into a hairpin behind the head drop, kicked up a little.
                var pin = new List<Vector3>();
                Vector3 q = l.P[0] + Vector3.down * T.RodDrop, lf = T.Left(l.H[0]), back = -l.H[0];
                for (int k = 0; k <= 14; k++)
                {
                    float a = Mathf.Lerp(-90f, 90f, k / 14f) * Mathf.Deg2Rad;
                    pin.Add(q + lf * (T.RodHalfGap * Mathf.Sin(a)) + back * (T.RodHalfGap * 1.2f * Mathf.Cos(a)) + Vector3.up * (0.006f * Mathf.Cos(a)));
                }
                PoolReturnMesh.Rod(b, pin, T.RodR, 10);
                // U spacers under the rods every 9 cm (not inside the tray: it has its own saddles).
                for (float u = 0.06f; u < l.Length; u += 0.09f)
                {
                    var c = T.At(l, u, out var h);
                    if (Mathf.Abs(c.z) < T.TrayHalfL + 0.01f && c.x > T.TrayX - 0.05f) continue;
                    Vector3 a = T.Rod(c, h, -1), bb = T.Rod(c, h, 1), dn = Vector3.down * 0.0075f;
                    Vector3 lf2 = T.Left(h) * 0.003f;
                    PoolReturnMesh.Rod(b, new[] { a, a + dn + lf2, bb + dn - lf2, bb }, 0.0019f, 8);
                }
            }
            return b.Bake("pool_gully");
        }
    }
}
