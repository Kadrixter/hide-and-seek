using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public sealed partial class PoolTableModel
    {
        const float LegSide = 0.14f, LegTopY = 0.44f, TurnBotY = 0.145f, PlinthY = 0.058f;

        /// <summary>
        /// Four massive legs tucked into the chamfered frame corners: a square post (rising into the frame up to the
        /// slate) with moulded collars, a turned cannonball column, a square plinth with a chamfered foot and a chrome
        /// levelling glide (hex nut, threaded stem, pad with a rubber sole).
        /// </summary>
        void BuildLegs()
        {
            float postH = PoolDims.ClothY - G.SlateT - LegTopY;
            var post = Block("pool_legpost", LegSide, postH, 0.006f);
            var collar = Block("pool_legcollar", LegSide + 0.014f, 0.016f, 0.005f);
            var ring = Block("pool_legring", LegSide + 0.008f, 0.01f, 0.004f);
            var turned = BoardMeshes.Lathe("poolleg", LegProfile(), 36, 2f);
            var plinth = Block("pool_legplinth", 0.128f, TurnBotY - PlinthY, 0.008f);
            var foot = Block("pool_legfoot", 0.112f, 0.016f, 0.006f);
            var nut = BoardMeshes.Lathe("poolglidenut", new List<Vector2> { new(0f, 0f), new(0.016f, 0f), new(0.016f, 0f), new(0.016f, 0.008f), new(0.016f, 0.008f), new(0f, 0.008f) }, 6, 4f);
            var stem = BoardMeshes.Lathe("poolglidestem", new List<Vector2> { new(0f, 0f), new(0.0075f, 0f), new(0.0075f, 0.03f), new(0f, 0.03f) }, 14, 4f);
            var pad = BoardMeshes.Lathe("poolglidepad", new List<Vector2>
            {
                new(0f, 0.004f), new(0.031f, 0.004f), new(0.031f, 0.004f), new(0.032f, 0.008f), new(0.028f, 0.013f), new(0.012f, 0.016f), new(0f, 0.016f),
            }, 28, 4f);
            var sole = BoardMeshes.Lathe("poolglidesole", new List<Vector2> { new(0f, 0f), new(0.03f, 0f), new(0.03f, 0f), new(0.03f, 0.0042f), new(0.03f, 0.0042f), new(0f, 0.0042f) }, 28, 4f);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                {
                    Vector2 p = G.LegPos(sx, sz, LegSide);
                    var g = Group("Leg", new Vector3(p.x, 0f, p.y), Quaternion.identity);
                    Kit.Part("Post", g, post, _legWood, new Vector3(0f, LegTopY + postH * 0.5f, 0f));
                    Kit.Part("Collar", g, collar, _legWood, new Vector3(0f, PoolDims.ClothY - G.ApronH - 0.008f, 0f));
                    Kit.Part("Collar", g, collar, _legWood, new Vector3(0f, LegTopY + 0.008f, 0f));
                    Kit.Part("Ring", g, ring, _legWood, new Vector3(0f, LegTopY + 0.026f, 0f));
                    Kit.Part("Column", g, turned, _legWood, Vector3.zero);
                    Kit.Part("Plinth", g, plinth, _legWood, new Vector3(0f, (TurnBotY + PlinthY) * 0.5f, 0f));
                    Kit.Part("Foot", g, foot, _legWood, new Vector3(0f, PlinthY - 0.006f, 0f));
                    Kit.Part("GlideStem", g, stem, _steel, new Vector3(0f, 0.012f, 0f));
                    Kit.Part("GlideNut", g, nut, _steel, new Vector3(0f, 0.034f, 0f));
                    Kit.Part("GlidePad", g, pad, _steel, Vector3.zero);
                    Kit.Part("GlideSole", g, sole, _rubber, Vector3.zero);
                }
        }

        /// <summary>Square block centred on the origin, top edges rounded, grain running up its faces.</summary>
        static Mesh Block(string key, float side, float h, float round)
        {
            if (PoolTableMesh.TryGet(key, out var m)) return m;
            var b = new PoolTableMesh();
            float s = side * 0.5f;
            var sq = new List<Vector2> { new(-s, -s), new(s, -s), new(s, s), new(-s, s) };
            b.Prism(sq, -h * 0.5f, h * 0.5f, round, new Vector2(1.8f, 0.9f), Vector2.right, true);
            return b.Bake(key);
        }

        /// <summary>Turned column (radius, height) between the plinth and the post: rings, a cannonball belly, rings.</summary>
        static List<Vector2> LegProfile()
        {
            float b = TurnBotY, t = LegTopY;
            var p = new List<Vector2>
            {
                new(0f, b), new(0.058f, b), new(0.058f, b), new(0.058f, b + 0.008f), new(0.052f, b + 0.012f), new(0.046f, b + 0.02f),
                new(0.048f, b + 0.026f), new(0.055f, b + 0.03f), new(0.056f, b + 0.036f), new(0.05f, b + 0.042f), new(0.042f, b + 0.052f),
                new(0.044f, b + 0.068f), new(0.056f, b + 0.092f), new(0.07f, b + 0.118f), new(0.08f, b + 0.148f), new(0.083f, b + 0.172f),
                new(0.08f, b + 0.196f), new(0.068f, b + 0.222f), new(0.055f, b + 0.238f), new(0.05f, b + 0.246f), new(0.055f, b + 0.251f),
                new(0.058f, b + 0.258f), new(0.053f, b + 0.264f), new(0.047f, b + 0.272f), new(0.052f, t - 0.012f), new(0.062f, t - 0.006f),
                new(0.064f, t), new(0.064f, t), new(0f, t),
            };
            return p;
        }
    }
}
