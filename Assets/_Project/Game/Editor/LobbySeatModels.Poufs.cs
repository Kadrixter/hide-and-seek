using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;
using M = HideAndSeek.Game.Editor.LobbySeatMeshes;

namespace HideAndSeek.Game.Editor
{
    /// <summary>Pouf models (local space: origin on the floor at the centre, the seat in front = +Z).</summary>
    public static partial class MultiplayerBuilder
    {
        private static void BuildPoufModel(Transform p, int style)
        {
            switch (style)
            {
                case 1: PoufBeanBag(p); break;
                case 2: PoufDrum(p); break;
                case 3: PoufWicker(p); break;
                default: PoufChesterfield(p); break;
            }
            foreach (Transform c in p)
                if (c.GetComponent<LobbySeat>() != null) c.localPosition = new Vector3(0f, 0f, SeatShape.PoufSeatZ);
        }

        /// <summary>Round chesterfield pouf: deep diamond tufting with buttons on the sides and the top, piping, bun feet.</summary>
        private static void PoufChesterfield(Transform p)
        {
            const float y0 = 0.034f, R = 0.31f, top = SeatShape.PoufTopY, edge = top - 0.025f;
            var prof = M.RoundedProfile(
                new Vector3(0f, y0, 0f), new Vector3(R - 0.03f, y0, 0.035f), new Vector3(R, y0 + 0.02f, 0f),
                new Vector3(R + 0.006f, (y0 + edge) * 0.5f, 0.2f), new Vector3(R, edge - 0.02f, 0f),
                new Vector3(R - 0.03f, edge, 0.04f), new Vector3(0.15f, top - 0.012f, 0.25f), new Vector3(0f, top, 0f));
            // Tufts: two staggered rows of 12 on the side, a ring of 6 and the centre on top.
            var tufts = new List<Vector3>();
            for (int k = 0; k < 12; k++)
            {
                float a1 = k * Mathf.PI / 6f, a2 = a1 + Mathf.PI / 12f;
                tufts.Add(new Vector3(Mathf.Cos(a1) * R, Mathf.Lerp(y0, edge, 0.33f), Mathf.Sin(a1) * R));
                tufts.Add(new Vector3(Mathf.Cos(a2) * R, Mathf.Lerp(y0, edge, 0.72f), Mathf.Sin(a2) * R));
            }
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f + Mathf.PI / 6f;
                tufts.Add(new Vector3(Mathf.Cos(a) * 0.16f, top - 0.005f, Mathf.Sin(a) * 0.16f));
            }
            tufts.Add(new Vector3(0f, top, 0f));
            float Push(float a, float s, Vector3 b)
            {
                float d = 0f;
                foreach (var t in tufts) d += M.Dimple((b - t).magnitude, 0.026f, 0.036f);
                return 0.006f - Mathf.Min(d, 0.028f);
            }
            var velvet = SeatMat("PoufVelvetMustard", new Color(0.78f, 0.52f, 0.1f), VelvetTex(), 0.25f, new Vector2(6f, 3f));
            var dark = SeatMat("PoufPipingMustard", new Color(0.36f, 0.22f, 0.06f), VelvetTex(), 0.3f, new Vector2(4f, 1f));
            var wood = SeatMat("SeatWalnut", new Color(0.32f, 0.2f, 0.12f), Tex("lobby_wood", TexKind.Wood), 0.55f, new Vector2(0.3f, 0.3f));
            Piece(p, "Body", M.Revolve(prof, Push, 96, 64, 6f, 3f), velvet, "PoufA_Body");
            // Buttons sit in the dimples.
            var parts = new List<Mesh>();
            foreach (var t in tufts)
            {
                Vector3 n = t.y > edge ? Vector3.up : new Vector3(t.x, 0f, t.z).normalized;
                parts.Add(M.Moved(M.Ellipsoid(0.014f, 0.007f), t - n * 0.019f, Quaternion.FromToRotation(Vector3.up, n), Vector3.one));
            }
            // Piping round the top seam and the bottom.
            float pr = R - 0.03f + 0.03f * 0.72f, py = edge - 0.012f;
            parts.Add(M.Tube(t => new Vector3(Mathf.Cos(t * 6.2832f) * (pr + 0.006f), py, Mathf.Sin(t * 6.2832f) * (pr + 0.006f)), _ => 0.0085f, 96, 8, true));
            parts.Add(M.Tube(t => new Vector3(Mathf.Cos(t * 6.2832f) * (R - 0.012f), y0 + 0.008f, Mathf.Sin(t * 6.2832f) * (R - 0.012f)), _ => 0.008f, 96, 8, true));
            Piece(p, "Trim", M.Combine(parts), dark, "PoufA_Trim");
            // Bun feet.
            var feet = new List<Mesh>();
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI / 4f + k * Mathf.PI / 2f;
                feet.Add(M.Moved(M.Ellipsoid(0.036f, 0.021f, 16), new Vector3(Mathf.Cos(a) * 0.2f, 0.021f, Mathf.Sin(a) * 0.2f), Quaternion.identity, Vector3.one));
            }
            Piece(p, "Feet", M.Combine(feet), wood, "PoufA_Feet");
            CylCol(p, "PoufA_Col", R + 0.01f, 0f, top);
        }

        /// <summary>Bean bag: soft sack with panel seams, the top dished where one sits, the back a little higher, wrinkles low down.</summary>
        private static void PoufBeanBag(Transform p)
        {
            var prof = M.RoundedProfile(
                new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0f, 0.09f), new Vector3(0.39f, 0.09f, 0.1f),
                new Vector3(0.37f, 0.22f, 0.1f), new Vector3(0.24f, SeatShape.PoufTopY + 0.03f, 0.1f), new Vector3(0f, SeatShape.PoufTopY - 0.02f, 0f));
            float Push(float a, float s, Vector3 b)
            {
                float back = Mathf.Max(0f, -Mathf.Sin(a)); // local -Z = behind the sitter
                float rim = Mathf.Exp(-Mathf.Pow((s - 0.72f) / 0.12f, 2f));
                float low = Mathf.Exp(-Mathf.Pow((s - 0.36f) / 0.12f, 2f));
                return 0.07f * back * back * rim
                       + 0.008f * Mathf.Sin(a * 11f + 3f * Mathf.Sin(a * 3f)) * low
                       + 0.005f * Mathf.Sin(a * 7f + 1.3f) * rim
                       - 0.012f * Mathf.Exp(-Mathf.Pow((s - 0.95f) / 0.08f, 2f));
            }
            var cord = SeatMat("PoufCorduroyRust", new Color(0.62f, 0.27f, 0.14f), CordTex(), 0.12f, new Vector2(48f, 1f));
            var seam = SeatMat("PoufSeamRust", new Color(0.4f, 0.16f, 0.08f), CordTex(), 0.1f, new Vector2(8f, 1f));
            Piece(p, "Body", M.Revolve(prof, Push, 96, 64, 48f, 1f), cord, "PoufB_Body");
            var parts = new List<Mesh>();
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + 0.2f;
                parts.Add(M.Tube(t => M.RevolvePoint(prof, (aa, ss, bb) => Push(aa, ss, bb) + 0.002f, a, Mathf.Lerp(0.12f, 0.8f, t)), _ => 0.0055f, 48, 6, false));
            }
            parts.Add(M.Tube(t => M.RevolvePoint(prof, (aa, ss, bb) => Push(aa, ss, bb) + 0.002f, t * 6.2832f, 0.8f), _ => 0.0055f, 96, 6, true));
            Piece(p, "Seams", M.Combine(parts), seam, "PoufB_Seams");
            CylCol(p, "PoufB_Col", 0.38f, 0f, SeatShape.PoufTopY);
        }

        /// <summary>Velvet drum with vertical channel tufting, piping top and bottom, on four tapered walnut legs with brass shoes.</summary>
        private static void PoufDrum(Transform p)
        {
            const float y0 = 0.07f, R = 0.29f, top = SeatShape.PoufTopY, edge = top - 0.023f;
            var prof = M.RoundedProfile(
                new Vector3(0f, y0, 0f), new Vector3(R, y0, 0.022f), new Vector3(R, edge, 0.028f), new Vector3(0.12f, top - 0.006f, 0.2f), new Vector3(0f, top, 0f));
            const int channels = 12;
            float Push(float a, float s, Vector3 b)
            {
                float side = Mathf.Clamp01((b.y - y0 - 0.02f) / 0.02f) * Mathf.Clamp01((edge - 0.02f - b.y) / 0.02f);
                float c = Mathf.Abs(Mathf.Sin(channels * a * 0.5f));
                return -0.022f * (1f - Mathf.Sqrt(c)) * side;
            }
            var velvet = SeatMat("PoufVelvetEmerald", new Color(0.08f, 0.4f, 0.28f), VelvetTex(), 0.22f, new Vector2(6f, 3f));
            var dark = SeatMat("PoufPipingEmerald", new Color(0.03f, 0.18f, 0.12f), VelvetTex(), 0.35f, new Vector2(4f, 1f));
            var wood = SeatMat("SeatWalnut", new Color(0.32f, 0.2f, 0.12f), Tex("lobby_wood", TexKind.Wood), 0.55f, new Vector2(0.3f, 0.3f));
            var brass = SeatMat("SeatBrass", new Color(0.85f, 0.66f, 0.3f), null, 0.75f, Vector2.one, 0.9f);
            Piece(p, "Body", M.Revolve(prof, Push, 128, 48, 6f, 3f), velvet, "PoufC_Body");
            var trim = new List<Mesh>
            {
                M.Tube(t => new Vector3(Mathf.Cos(t * 6.2832f) * (R - 0.006f), edge - 0.006f, Mathf.Sin(t * 6.2832f) * (R - 0.006f)), _ => 0.0085f, 96, 8, true),
                M.Tube(t => new Vector3(Mathf.Cos(t * 6.2832f) * (R - 0.005f), y0 + 0.006f, Mathf.Sin(t * 6.2832f) * (R - 0.005f)), _ => 0.0085f, 96, 8, true),
            };
            Piece(p, "Piping", M.Combine(trim), dark, "PoufC_Piping");
            var legs = new List<Mesh>();
            var shoes = new List<Mesh>();
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI / 4f + k * Mathf.PI / 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 at = d * 0.2f + Vector3.up * y0, foot = d * 0.225f;
                legs.Add(M.Tube(t => Vector3.Lerp(at, foot + Vector3.up * 0.02f, t), t => Mathf.Lerp(0.018f, 0.012f, t), 6, 12, false));
                shoes.Add(M.Tube(t => Vector3.Lerp(foot + Vector3.up * 0.022f, foot, t), _ => 0.0125f, 2, 12, false));
                shoes.Add(M.Moved(M.Ellipsoid(0.0125f, 0.004f), foot + Vector3.up * 0.002f, Quaternion.identity, Vector3.one));
            }
            Piece(p, "Legs", M.Combine(legs), wood, "PoufC_Legs");
            Piece(p, "Shoes", M.Combine(shoes), brass, "PoufC_Shoes");
            CylCol(p, "PoufC_Col", R + 0.01f, 0f, top);
        }

        /// <summary>Wicker cube (basket weave, rounded corners, rims) with a thick linen cushion and piping on top.</summary>
        private static void PoufWicker(Transform p)
        {
            const float hx = 0.26f, r = 0.05f, top = SeatShape.PoufTopY, h = top - 0.08f;
            var wicker = SeatMat("PoufWicker", Color.white, WickerTex(), 0.15f, new Vector2(7f, 1.5f));
            var rim = SeatMat("PoufWickerRim", new Color(0.42f, 0.28f, 0.14f), null, 0.3f, Vector2.one);
            var linen = SeatMat("PoufLinen", new Color(0.82f, 0.76f, 0.64f), LinenTex(), 0.08f, new Vector2(3f, 3f));
            var pipe = SeatMat("PoufLinenPiping", new Color(0.62f, 0.55f, 0.43f), LinenTex(), 0.08f, new Vector2(3f, 3f));
            Piece(p, "Basket", M.Param(96, 6, (u, v) =>
            {
                Vector3 q = M.RoundRect(u, hx, hx, r, out var n);
                float bulge = 0.006f * Mathf.Sin(v * Mathf.PI);
                return q + n * bulge + Vector3.up * Mathf.Lerp(0.015f, h - 0.012f, v);
            }, 7f, 1.5f), wicker, "PoufD_Basket");
            var rims = new List<Mesh>
            {
                M.Tube(t => M.RoundRect(t, hx, hx, r, out _) + Vector3.up * 0.016f, _ => 0.016f, 96, 8, true),
                M.Tube(t => M.RoundRect(t, hx, hx, r, out _) + Vector3.up * (h - 0.012f), _ => 0.014f, 96, 8, true),
                M.Param(16, 16, (u, v) => new Vector3(Mathf.Lerp(-hx, hx, u), h - 0.01f, Mathf.Lerp(-hx, hx, v))),
            };
            Piece(p, "Rims", M.Combine(rims), rim, "PoufD_Rims");
            float cushionH = top - h - 0.008f;
            Vector3 cc = new Vector3(0f, h + cushionH * 0.5f - 0.004f, 0f);
            var cushion = M.SoftBox(new Vector3(2f * hx + 0.01f, cushionH, 2f * hx + 0.01f), 0.035f, 0.012f,
                q => q.y > 0f ? Vector3.down * M.Dimple(new Vector2(q.x, q.z).magnitude, 0.012f, 0.05f) : Vector3.zero, 18);
            Piece(p, "Cushion", M.Moved(cushion, cc, Quaternion.identity, Vector3.one), linen, "PoufD_Cushion");
            var trim = new List<Mesh>
            {
                M.Tube(t => M.RoundRect(t, hx - 0.006f, hx - 0.006f, 0.04f, out _) + Vector3.up * (cc.y + cushionH * 0.5f - 0.012f), _ => 0.007f, 96, 8, true),
                M.Moved(M.Ellipsoid(0.016f, 0.006f), new Vector3(0f, top - 0.012f, 0f), Quaternion.identity, Vector3.one),
            };
            Piece(p, "Piping", M.Combine(trim), pipe, "PoufD_Piping");
            BoxCol(p, new Vector3(0f, top * 0.5f, 0f), new Vector3(2f * hx, top, 2f * hx));
        }
    }
}
