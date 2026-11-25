using System.Collections.Generic;
using UnityEngine;
using G = HideAndSeek.Game.MiniGames.Pool.PoolTableGeom;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public sealed partial class PoolTableModel
    {
        /// <summary>
        /// The apron under the rails (moulded board swept round the chamfered, notched frame outline), applied bead
        /// frames on its faces, the cross beams under the slate (seen from below) and a brass maker's plate on both ends.
        /// </summary>
        void BuildFrame()
        {
            Part("Apron", ApronMesh(), _wood);
            Part("ApronPanels", PanelMesh(), _wood);
            Part("FrameBeams", BeamMesh(), _raw);
            // Turned drops hanging under the apron where the chamfers meet the sides and ends.
            var drop = BoardMeshes.Lathe("poolapronfinial", FinialProfile(), 20, 2f);
            var path = G.FramePath();
            var cols = PoolTableMesh.Cols(path, true, 181f);
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 a = path[i], prev = path[(i + path.Count - 1) % path.Count], next = path[(i + 1) % path.Count];
                bool chamferEnd = Vector2.Distance(a, prev) > 0.3f && Vector2.Distance(a, next) > 0.3f;
                if (!chamferEnd) continue;
                Vector2 p = a + cols[i].Off * 0.004f;
                Kit.Part("ApronDrop", Root, drop, _wood, new Vector3(p.x, PoolDims.ClothY - G.ApronH, p.y));
            }
            foreach (int sx in new[] { -1, 1 })
            {
                float x = sx * (G.FrameHX + 0.0004f), y = PoolDims.ClothY - 0.092f;
                var rot = Quaternion.LookRotation(Vector3.up, new Vector3(sx, 0f, 0f));
                Kit.Part("MakerPlate", Root, BoardMeshes.Slab("poolplate", PlateW, PlateH, 0.0022f), _plate, new Vector3(x, y, 0f), rot);
                for (int i = 0; i < 4; i++)
                {
                    float pz = (i % 2 == 0 ? -1f : 1f) * (PlateW * 0.5f - 0.0065f), py = (i < 2 ? -1f : 1f) * (PlateH * 0.5f - 0.0065f);
                    Kit.Part("PlatePin", Root, PinMesh(), _metal, new Vector3(x + sx * 0.0022f, y + py, pz), Quaternion.Euler(0f, 0f, -90f * sx), false);
                }
            }
        }

        const float PlateW = 0.13f, PlateH = PlateW * PoolTableArt.PlateH / PoolTableArt.PlateW;

        /// <summary>
        /// Apron section (d = out from the frame line, y): cove under the rail, a bead, the flat face, an ogee and bead at
        /// the bottom edge, then the inside face back up (seen from below).
        /// </summary>
        static List<Vector2> ApronProfile()
        {
            // Top 1.5 mm under the cloth: where the frame runs under the bed (round the pockets) it must not fight the cloth.
            float t = PoolDims.ClothY - 0.0015f, b = PoolDims.ClothY - G.ApronH;
            var p = new List<Vector2> { new(-0.03f, t), new(0.017f, t), new(0.017f, t - 0.005f) };
            PoolTableMesh.Arc(p, new Vector2(0.017f, t - 0.019f), 0.014f, 90f, 180f, 6);
            PoolTableMesh.Arc(p, new Vector2(0.003f, t - 0.0225f), 0.0035f, 90f, -90f, 6);
            p.Add(new Vector2(0f, t - 0.026f));
            p.Add(new Vector2(0f, b + 0.032f));
            PoolTableMesh.Arc(p, new Vector2(0f, b + 0.026f), 0.006f, 90f, -10f, 5);
            PoolTableMesh.Arc(p, new Vector2(0.0118f, b + 0.012f), 0.0072f, 170f, 270f, 5);
            p.Add(new Vector2(0.016f, b + 0.0048f));
            PoolTableMesh.Arc(p, new Vector2(0.016f, b + 0.0024f), 0.0024f, 90f, -90f, 5);
            p.Add(new Vector2(-0.03f, b));
            p.Add(new Vector2(-0.03f, t - 0.0005f));
            return p;
        }

        static Mesh ApronMesh()
        {
            if (PoolTableMesh.TryGet("pool_apron", out var m)) return m;
            var b = new PoolTableMesh();
            b.Sweep(G.FramePath(), true, ApronProfile(), WoodUv, 30f, 40f);
            return b.Bake("pool_apron");
        }

        /// <summary>Half-round bead frames (rounded rectangles) applied to every straight apron face longer than 30 cm.</summary>
        static Mesh PanelMesh()
        {
            if (PoolTableMesh.TryGet("pool_panels", out var m)) return m;
            var b = new PoolTableMesh();
            var path = G.FramePath();
            float yTop = PoolDims.ClothY - 0.044f, yBot = PoolDims.ClothY - G.ApronH + 0.044f;
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 a = path[i], c = path[(i + 1) % path.Count];
                float len = Vector2.Distance(a, c);
                if (len < 0.3f) continue;
                Vector2 t = (c - a) / len, o = new Vector2(t.y, -t.x);
                Vector3 T = new Vector3(t.x, 0f, t.y), O = new Vector3(o.x, 0f, o.y);
                Vector3 mid = new Vector3((a.x + c.x) * 0.5f, (yTop + yBot) * 0.5f, (a.y + c.y) * 0.5f) + O * 0.0005f;
                float hw = len * 0.5f - 0.05f, hh = (yTop - yBot) * 0.5f, cr = 0.012f;
                var loop = new List<Vector3>();
                for (int q = 0; q < 4; q++)
                {
                    float sxq = q == 0 || q == 3 ? 1f : -1f, syq = q < 2 ? 1f : -1f;
                    float a0 = q * 90f;
                    for (int k = 0; k <= 4; k++)
                    {
                        float ang = (a0 + k * 22.5f) * Mathf.Deg2Rad;
                        loop.Add(mid + T * (sxq * (hw - cr) + Mathf.Cos(ang) * cr) + Vector3.up * (syq * (hh - cr) + Mathf.Sin(ang) * cr));
                    }
                }
                b.Tube(loop, 0.0035f, 0.0035f, 8, true);
            }
            return b.Bake("pool_panels");
        }

        /// <summary>Raw-wood cross beams under the slate and two long rails, plus a block over each leg.</summary>
        static Mesh BeamMesh()
        {
            if (PoolTableMesh.TryGet("pool_beams", out var m)) return m;
            var b = new PoolTableMesh();
            float y1 = PoolDims.ClothY - G.SlateT, y0 = y1 - 0.09f;
            float notchIn = G.HalfW + G.SideOff - G.NotchR - 0.02f;
            foreach (float x in new[] { -0.62f, 0f, 0.62f })
            {
                float hz = x == 0f ? notchIn : G.FrameHZ - 0.02f;
                b.Prism(Rect(x, 0f, 0.025f, hz), y0, y1, 0.003f, WoodUv, new Vector2(0f, 1f), true);
            }
            foreach (float z in new[] { -0.26f, 0.26f })
                b.Prism(Rect(0f, z, G.FrameHX - 0.25f, 0.025f), y0 + 0.02f, y1, 0.003f, WoodUv, Vector2.right, true);
            return b.Bake("pool_beams");
        }

        static List<Vector2> Rect(float cx, float cz, float hx, float hz) => new()
        {
            new(cx - hx, cz - hz), new(cx + hx, cz - hz), new(cx + hx, cz + hz), new(cx - hx, cz + hz),
        };

        /// <summary>Drop finial under the apron (top at y = 0): collar, neck, ball, point.</summary>
        static List<Vector2> FinialProfile() => new()
        {
            new(0f, -0.046f), new(0.003f, -0.044f), new(0.008f, -0.038f), new(0.0125f, -0.03f), new(0.0135f, -0.024f), new(0.011f, -0.017f),
            new(0.0065f, -0.0125f), new(0.0065f, -0.0105f), new(0.01f, -0.009f), new(0.0145f, -0.006f), new(0.0165f, -0.002f),
            new(0.0165f, 0.001f), new(0.0165f, 0.001f), new(0f, 0.001f),
        };

        /// <summary>Small domed pin (along +Y) for the maker's plate.</summary>
        static Mesh PinMesh() => BoardMeshes.Lathe("poolplatepin", new List<Vector2>
        {
            new(0f, -0.001f), new(0.0026f, -0.001f), new(0.0026f, 0f), new(0.0022f, 0.0009f), new(0.0012f, 0.0015f), new(0f, 0.0017f),
        }, 12, 1f);
    }
}
