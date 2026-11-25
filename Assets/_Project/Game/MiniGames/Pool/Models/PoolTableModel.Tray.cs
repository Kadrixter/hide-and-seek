using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;
using T = HideAndSeek.Game.MiniGames.Pool.PoolReturnTrack;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public sealed partial class PoolTableModel
    {
        const float TrayWall = 0.011f, TrayFloorY0 = 0.414f, TrayFloorY1 = 0.426f;
        /// <summary>Top of the tray's long walls: level with the resting balls' centres (their upper halves show).</summary>
        static float TrayWallTop => T.EndY + 0.003f;

        /// <summary>
        /// The collection tray under the foot rail (a wooden trough lined with cloth, metal beads on its walls, a
        /// rubber-faced divider in the middle where the two gullies stop, saddles carrying the rods), hung from the foot
        /// apron by two steel straps; and the gully supports: straps screwed to the inside of the long aprons, brackets
        /// on the legs and a threaded hanger from the foot rail holding each turn.
        /// </summary>
        void BuildTray()
        {
            Part("Tray", TrayMesh(), _wood);
            Part("TrayFelt", TrayFeltMesh(), _cushion);
            Part("TrayBeads", TrayBeadMesh(), _metal);
            Part("TrayBumpers", TrayBumperMesh(), _rubber);
            Part("ReturnBrackets", BracketMesh(), _steel);
        }

        const float TrayIn = T.TrayHalfW, TrayOut = T.TrayHalfW + TrayWall;

        static Mesh TrayMesh()
        {
            if (PoolTableMesh.TryGet("pool_tray", out var m)) return m;
            var b = new PoolTableMesh();
            float x = T.TrayX, hl = T.TrayHalfL;
            var uvx = new Vector2(0f, 1f);
            b.Prism(Rect(x, 0f, TrayOut, hl), TrayFloorY0, TrayFloorY1, 0.002f, WoodUv, uvx, true);
            foreach (int s in new[] { -1, 1 })
            {
                float cx = x + s * (TrayIn + TrayWall * 0.5f);
                b.Prism(Rect(cx, 0f, TrayWall * 0.5f, hl), TrayFloorY1 - 0.001f, TrayWallTop, 0.0035f, WoodUv, uvx);
                // Low sills at the ends: the rods (and the balls) come in over them.
                b.Prism(Rect(x, s * (hl - TrayWall * 0.5f), TrayIn, TrayWall * 0.5f), TrayFloorY1 - 0.001f, TrayFloorY1 + 0.006f, 0.002f, WoodUv, Vector2.right);
            }
            // Divider: a block between the two stops (the rubber faces are a separate mesh).
            b.Prism(Rect(x, 0f, TrayIn, T.DividerHalf - 0.0025f), TrayFloorY1 - 0.001f, T.EndY + 0.024f, 0.003f, WoodUv, Vector2.right);
            return b.Bake("pool_tray");
        }

        static Mesh TrayFeltMesh()
        {
            if (PoolTableMesh.TryGet("pool_trayfelt", out var m)) return m;
            var b = new PoolTableMesh();
            b.Cap(Rect(T.TrayX, 0f, TrayIn, T.TrayHalfL - TrayWall), TrayFloorY1 + 0.0004f, true, new Vector2(4f, 4f), Vector2.right);
            return b.Bake("pool_trayfelt");
        }

        /// <summary>Half-round metal beads along the tops of the long walls, round the divider's top and saddles under the rods.</summary>
        static Mesh TrayBeadMesh()
        {
            if (PoolTableMesh.TryGet("pool_traybeads", out var m)) return m;
            var b = new PoolTableMesh();
            float x = T.TrayX, hl = T.TrayHalfL;
            foreach (int s in new[] { -1, 1 })
            {
                float cx = x + s * (TrayIn + TrayWall * 0.5f);
                PoolReturnMesh.Rod(b, new[] { new Vector3(cx, TrayWallTop + 0.001f, -hl + 0.003f), new Vector3(cx, TrayWallTop + 0.001f, hl - 0.003f) }, 0.0045f, 12);
            }
            float dy = T.EndY + 0.024f;
            PoolReturnMesh.Rod(b, new[] { new Vector3(x - TrayIn + 0.002f, dy, 0f), new Vector3(x + TrayIn - 0.002f, dy, 0f) }, T.DividerHalf, 12);
            // Saddles: small posts from the floor up into the rods every 8 cm along the tray.
            for (int side = 0; side < 2; side++)
            {
                var l = T.Gully(side);
                for (float u = l.Length - 0.03f; u > 0f; u -= 0.08f)
                {
                    var c = T.At(l, u, out var h);
                    if (Mathf.Abs(c.z) > hl - TrayWall - 0.01f) break;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var r = T.Rod(c, h, s);
                        b.Prism(Rect(r.x, r.z, 0.0035f, 0.003f), TrayFloorY1, r.y, 0.001f, Vector2.one, Vector2.right);
                    }
                }
            }
            return b.Bake("pool_traybeads");
        }

        static Mesh TrayBumperMesh()
        {
            if (PoolTableMesh.TryGet("pool_traybumpers", out var m)) return m;
            var b = new PoolTableMesh();
            foreach (int s in new[] { -1, 1 })
            {
                float z = s * (T.DividerHalf - 0.00125f);
                b.Prism(Rect(T.TrayX, z, TrayIn - 0.004f, 0.00125f), T.EndY - 0.022f, T.EndY + 0.018f, 0.001f, Vector2.one, Vector2.right);
            }
            return b.Bake("pool_traybumpers");
        }

        // ================================================================ supports

        static Mesh BracketMesh()
        {
            if (PoolTableMesh.TryGet("pool_retbrackets", out var m)) return m;
            var b = new PoolTableMesh();
            float apronIn = G.FrameHZ - 0.03f - 0.0016f, bottom = PoolDims.ClothY - G.ApronH;
            for (int side = 0; side < 2; side++)
            {
                var l = T.Gully(side);
                float sz = side == 0 ? -1f : 1f;
                // Straps on the inside of the long apron.
                foreach (float x in new[] { -0.58f, -0.25f, 0.25f, 0.58f })
                {
                    var c = AtX(l, x);
                    float ys = c.y - T.RodDrop - T.RodR - 0.0016f, zin = Mathf.Abs(c.z) - T.RodHalfGap - 0.012f;
                    var pts = new[] { Z(x, bottom + 0.045f, sz * apronIn), Z(x, ys, sz * apronIn), Z(x, ys, sz * zin), Z(x, ys + 0.011f, sz * zin) };
                    PoolReturnMesh.Strap(b, pts, Vector3.right, 0.02f, 0.003f);
                    foreach (float y in new[] { bottom + 0.012f, bottom + 0.032f })
                        PoolReturnMesh.Dome(b, Z(x, y, sz * (apronIn - 0.0015f)), new Vector3(0f, 0f, -sz), 0.0042f, 0.0016f);
                }
                // Brackets screwed to the legs' outer faces, the strap jogging out past the collar under the post.
                foreach (int sx in new[] { -1, 1 })
                {
                    float x = sx * 0.86f;
                    var c = AtX(l, x);
                    float face = Mathf.Abs(G.LegPos(sx, side == 0 ? -1 : 1, LegSide).y) + LegSide * 0.5f + 0.0016f;
                    float ys = c.y - T.RodDrop - T.RodR - 0.0016f, ty = Mathf.Max(ys, LegTopY + 0.037f), zo = Mathf.Abs(c.z) + T.RodHalfGap + 0.012f;
                    var pts = new List<Vector3> { Z(x, ty + 0.028f, sz * face), Z(x, ty, sz * face) };
                    if (ty - ys > 0.002f) { pts.Add(Z(x, ty, sz * (face + 0.014f))); pts.Add(Z(x, ys, sz * (face + 0.014f))); }
                    else pts.Add(Z(x, ys, sz * (face + 0.014f)));
                    pts.Add(Z(x, ys, sz * zo));
                    pts.Add(Z(x, ys + 0.011f, sz * zo));
                    PoolReturnMesh.Strap(b, pts, Vector3.right, 0.02f, 0.003f);
                    PoolReturnMesh.Dome(b, Z(x, ty + 0.016f, sz * (face + 0.0015f)), new Vector3(0f, 0f, sz), 0.0042f, 0.0016f);
                }
                TurnHanger(b, l);
                // Tray straps: screwed under the foot apron, down the tray's inner wall, under its floor, up the outer wall.
                float zt = sz * 0.2f, xa = G.FrameHX - 0.016f, xi = T.TrayX - TrayOut - 0.0016f, xo = T.TrayX + TrayOut + 0.0016f, yb = TrayFloorY0 - 0.0016f;
                var tp = new[] { Z(xa - 0.011f, bottom - 0.0016f, zt), Z(xi - 0.006f, bottom - 0.0016f, zt), Z(xi, bottom - 0.012f, zt), Z(xi, yb, zt), Z(xo, yb, zt), Z(xo, TrayFloorY1 + 0.02f, zt) };
                PoolReturnMesh.Strap(b, tp, Vector3.forward, 0.022f, 0.003f);
                PoolReturnMesh.Dome(b, Z(xa - 0.008f, bottom - 0.003f, zt), Vector3.down, 0.0045f, 0.0016f);
                PoolReturnMesh.Dome(b, Z(xi - 0.0015f, TrayFloorY1 + 0.014f, zt), Vector3.left, 0.0042f, 0.0016f);
                PoolReturnMesh.Dome(b, Z(xo + 0.0015f, TrayFloorY1 + 0.01f, zt), Vector3.right, 0.0042f, 0.0016f);
            }
            return b.Bake("pool_retbrackets");
        }

        /// <summary>Threaded rod from the underside of the foot rail down to a cradle under the middle of the turn (on its outer side, clear of the balls).</summary>
        static void TurnHanger(PoolTableMesh b, T.Line l)
        {
            var c = T.At(l, (l.TurnU0 + l.TurnU1) * 0.5f, out var h);
            Vector3 o = new Vector3(c.x - l.TurnC.x, 0f, c.z - l.TurnC.y).normalized;
            float ys = c.y - T.RodDrop - T.RodR - 0.0016f, out_ = PoolDims.BallR + 0.0085f, inn = T.RodHalfGap + 0.012f;
            Vector3 Pt(float lat, float y) => new Vector3(c.x, y, c.z) + o * lat;
            PoolReturnMesh.Strap(b, new[] { Pt(out_, ys + 0.016f), Pt(out_, ys), Pt(-inn, ys), Pt(-inn, ys + 0.011f) }, h, 0.018f, 0.003f);
            Vector3 rodBot = Pt(out_ + 0.0045f, ys - 0.006f), rodTop = Pt(out_ + 0.0045f, PoolDims.ClothY + 0.003f);
            PoolReturnMesh.Rod(b, new[] { rodBot, rodTop }, 0.0026f, 8);
            PoolReturnMesh.Ring(b, Pt(out_ + 0.0045f, ys + 0.0035f), Vector3.up, 0.0042f, 0.0022f, 6, 6);
            PoolReturnMesh.Ring(b, Pt(out_ + 0.0045f, ys - 0.0035f), Vector3.up, 0.0042f, 0.0022f, 6, 6);
            PoolReturnMesh.Dome(b, Pt(out_ + 0.0045f, PoolDims.ClothY - 0.0002f), Vector3.down, 0.009f, 0.0025f);
        }

        static Vector3 Z(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>Gully point whose x is nearest to <paramref name="x"/> (on the straight part).</summary>
        static Vector3 AtX(T.Line l, float x)
        {
            int best = 0;
            for (int i = 1; i < l.P.Length; i++)
                if (Mathf.Abs(l.P[i].x - x) < Mathf.Abs(l.P[best].x - x) && l.U[i] < l.TurnU0 + 0.001f) best = i;
            var p = l.P[best];
            return new Vector3(x, p.y, p.z);
        }
    }
}
