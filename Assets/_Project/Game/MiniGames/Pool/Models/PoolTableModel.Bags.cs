using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public sealed partial class PoolTableModel
    {
        /// <summary>
        /// Under every hole: a leather throat down through the slate, a rolled leather rim, a diamond-knotted net basket
        /// ending in a small leather cup and a fringe of tassels round the rim. Each pocket group sits at the hole centre
        /// on the cloth plane (local y = 0), turned a little so the six nets do not look stamped.
        /// </summary>
        void BuildBags()
        {
            for (int i = 0; i < 6; i++)
            {
                bool side = G.IsSide(i);
                float r = G.HoleR(i), depth = side ? G.SideBagDepth : G.CornerBagDepth;
                string key = side ? "side" : "corner";
                var g = Group("Pocket" + i, PocketCentre(i), Quaternion.Euler(0f, i * 37f, 0f));
                Part("Throat", BoardMeshes.Lathe("poolthroat_" + key, ThroatProfile(r), 40, 3f), _leather, g);
                Part("Rim", BoardMeshes.Lathe("poolrim_" + key, RimProfile(r), 40, 3f), _leather, g);
                Part("Cup", BoardMeshes.Lathe("poolcup_" + key, CupProfile(depth), 24, 3f), _leather, g);
                Part("Net", NetMesh(key, r, depth), _net, g);
                Part("Fringe", FringeMesh(key, r), _fringe, g);
            }
        }

        /// <summary>Thin leather tube (outside up, over the top, inside down) from just under the cloth to the rim.</summary>
        static List<Vector2> ThroatProfile(float r)
        {
            float top = -0.004f, bot = G.BagRimY;
            return new List<Vector2>
            {
                new(r - 0.001f, bot), new(r - 0.001f, top), new(r - 0.001f, top), new(r - 0.0025f, top + 0.001f),
                new(r - 0.004f, top), new(r - 0.004f, top), new(r - 0.004f, bot),
            };
        }

        /// <summary>Rolled leather ring at the top of the basket.</summary>
        static List<Vector2> RimProfile(float r)
        {
            var p = new List<Vector2>();
            PoolTableMesh.Arc(p, new Vector2(r + 0.0015f, G.BagRimY), 0.0055f, -90f, 270f, 16);
            return p;
        }

        /// <summary>Leather cup the net strands are sewn into at the bottom.</summary>
        static List<Vector2> CupProfile(float depth)
        {
            float y = G.BagRimY - depth;
            var p = new List<Vector2> { new(0f, y - 0.012f) };
            PoolTableMesh.Arc(p, new Vector2(0.004f, y + 0.012f), 0.024f, -90f, 0f, 6);
            p.Add(new Vector2(0.03f, y + 0.014f));
            p.Add(new Vector2(0.024f, y + 0.014f));
            p.Add(new Vector2(0.024f, y + 0.008f));
            p.Add(new Vector2(0f, y - 0.006f));
            return p;
        }

        const int NetStrands = 16;

        /// <summary>Basket radius at depth fraction t: a slight belly, then rounding in to the cup.</summary>
        static float BagRadius(float r, float t) => Mathf.Lerp(r + 0.001f, 0.026f, t * t) + 0.016f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f)) * (1f - t * 0.4f);

        /// <summary>Two families of helical cords crossing each other: a diamond net.</summary>
        static Mesh NetMesh(string key, float r, float depth)
        {
            if (PoolTableMesh.TryGet("pool_net_" + key, out var m)) return m;
            var b = new PoolTableMesh();
            const int steps = 16;
            float twist = Mathf.PI * 2f / NetStrands * 2.5f;
            for (int dir = -1; dir <= 1; dir += 2)
                for (int s = 0; s < NetStrands; s++)
                {
                    var pts = new List<Vector3>(steps + 1);
                    for (int k = 0; k <= steps; k++)
                    {
                        float t = k / (float)steps;
                        float a = (s + 0.25f * dir) / NetStrands * Mathf.PI * 2f + dir * twist * t;
                        float rr = BagRadius(r, t), y = G.BagRimY - 0.004f - (depth - 0.014f) * t;
                        pts.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
                    }
                    b.Tube(pts, 0.0013f, 0.0011f, 4);
                }
            // A cord ring a third of the way down keeps the diamonds even.
            var ring = new List<Vector3>();
            for (int k = 0; k < 40; k++)
            {
                float a = k / 40f * Mathf.PI * 2f, rr = BagRadius(r, 0.35f);
                ring.Add(new Vector3(Mathf.Cos(a) * rr, G.BagRimY - 0.004f - (depth - 0.014f) * 0.35f, Mathf.Sin(a) * rr));
            }
            b.Tube(ring, 0.0012f, 0.0012f, 4, true);
            return b.Bake("pool_net_" + key);
        }

        /// <summary>Tassels hanging round the rim, slightly flared and swaying (deterministic).</summary>
        static Mesh FringeMesh(string key, float r)
        {
            if (PoolTableMesh.TryGet("pool_fringe_" + key, out var m)) return m;
            var b = new PoolTableMesh();
            const int count = 30, steps = 5;
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f) / count * Mathf.PI * 2f;
                float len = 0.026f + 0.01f * Mathf.Abs(Mathf.Sin(i * 2.7f)), sway = 0.004f * Mathf.Sin(i * 1.9f);
                Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), tng = new Vector3(-o.z, 0f, o.x);
                var pts = new List<Vector3>(steps + 1);
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    float rr = r + 0.0075f + 0.016f * Mathf.Sqrt(t);
                    pts.Add(o * rr + tng * (sway * t * t) + Vector3.up * (G.BagRimY - 0.003f - len * t));
                }
                b.Tube(pts, 0.0022f, 0.0006f, 4);
            }
            return b.Bake("pool_fringe_" + key);
        }
    }
}
