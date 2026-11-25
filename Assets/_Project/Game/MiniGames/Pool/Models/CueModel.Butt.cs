using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public static partial class CueModel
    {
        /// <summary>Wood species of the cues (colours straight in the texture, the materials stay white).</summary>
        static class Woods
        {
            public static readonly CueArt.Wood Maple = new()
            {
                Light = new Color(0.84f, 0.72f, 0.52f), Dark = new Color(0.70f, 0.53f, 0.33f), Lines = 34, LineAmt = 0.32f,
                LineSharp = 5f, Pores = 0.05f, Streak = 0.07f, Warp = 0.5f, Seed = 1,
            };
            public static readonly CueArt.Wood Birdseye = new()
            {
                Light = new Color(0.86f, 0.71f, 0.48f), Dark = new Color(0.55f, 0.36f, 0.19f), Lines = 26, LineAmt = 0.3f,
                Pores = 0.06f, Eyes = 0.45f, Streak = 0.1f, Seed = 2,
            };
            public static readonly CueArt.Wood Ebony = new()
            {
                Light = new Color(0.085f, 0.068f, 0.058f), Dark = new Color(0.028f, 0.024f, 0.022f), Lines = 30, LineAmt = 0.8f,
                LineSharp = 2.5f, Pores = 0.18f, Streak = 0.22f, StreakTint = new Color(1.5f, 1.25f, 1f), Seed = 3,
            };
            public static readonly CueArt.Wood Cocobolo = new()
            {
                Light = new Color(0.60f, 0.31f, 0.16f), Dark = new Color(0.20f, 0.08f, 0.04f), Lines = 23, LineAmt = 0.85f,
                LineSharp = 2.2f, Warp = 1.9f, Pores = 0.1f, Streak = 0.25f, StreakTint = new Color(1.1f, 0.85f, 0.7f), Seed = 4,
            };
            public static readonly CueArt.Wood HouseStain = new()
            {
                Light = new Color(0.36f, 0.15f, 0.07f), Dark = new Color(0.17f, 0.06f, 0.03f), Lines = 22, LineAmt = 0.55f,
                LineSharp = 3f, Pores = 0.12f, Streak = 0.15f, Seed = 5,
            };
        }

        static readonly Color Gold = new(0.86f, 0.68f, 0.30f), Silver = new(0.72f, 0.73f, 0.75f), Black = new(0.025f, 0.022f, 0.02f);
        static readonly Color BoneC = new(0.91f, 0.87f, 0.77f), RedC = new(0.58f, 0.07f, 0.06f);

        /// <summary>Butt: bumper and cap (all variants), then the variant's sleeve, rings, wrap, forearm and joint.</summary>
        static void BuildButt(Kit k)
        {
            k.Z = 0f;
            Bumper(k);
            Cap(k);
            switch (k.Variant)
            {
                case 0: ButtHouse(k); break;
                case 1: ButtPoints(k); break;
                case 2: ButtLinen(k); break;
                default: ButtEbony(k); break;
            }
        }

        /// <summary>Rubber bumper: rounded bottom edge, two grip grooves, a recessed logo medallion.</summary>
        static void Bumper(Kit k)
        {
            const float rl = 0.0094f, rec = 0.0006f, f = 0.0018f;
            var p = new List<Vector3> { new(rl, rec, 0f), new(rl, 0f, 0.01f), new(rl, 0f, 0.01f) };
            CueMesh.Fillet(p, BumperR - f, f, f, -90f, 0f, 6, 0.1f);
            float h = BumperH;
            p.Add(new Vector3(BumperR, h - 0.0045f, 0.3f));
            CueMesh.Fillet(p, BumperR, h - 0.0037f, 0.0004f, -90f, -270f, 6, 0.32f);
            p.Add(new Vector3(BumperR, h - 0.0018f, 0.4f));
            CueMesh.Fillet(p, BumperR, h - 0.001f, 0.0004f, -90f, -270f, 6, 0.42f);
            p.Add(new Vector3(BumperR, h, 0.5f));
            k.Part("Bumper", CueMesh.Lathe("bumper", p, Kit.Seg), k.Rubber);
            var logo = k.Part("BumperLogo", CueMesh.Disc("bumperlogo", rl, Kit.Seg), k.Mat("logo", Color.white, 0.3f, 0.45f, CueArt.Logo));
            logo.transform.localPosition = new Vector3(0f, 0f, rec);
            k.Z = h;
        }

        /// <summary>Butt cap: black phenolic, its bottom face round the bumper and a rounded edge.</summary>
        static void Cap(Kit k)
        {
            float z0 = k.Z, z1 = z0 + CapH, r0 = ButtRadius(z0), f = 0.0012f;
            var p = new List<Vector3> { new(BumperR, z0, 0f), new(r0 - f, z0, 0.02f) };
            CueMesh.Fillet(p, r0 - f, z0 + f, f, -90f, 0f, 6, 0.05f);
            p.Add(new Vector3(ButtRadius(z1), z1, CapH / 0.05f));
            k.Part("ButtCap", CueMesh.Lathe("buttcap", p, Kit.Seg), k.Phenolic);
            k.Z = z1;
        }

        /// <summary>Flat end grain across the cue at <paramref name="z"/> facing +Z (only a split one-piece cue shows it).</summary>
        static void EndFace(Kit k, float z, Material mat) =>
            k.Part("ButtEnd", CueMesh.Lathe("buttend", new List<Vector3> { new(ButtRadius(z), z, 1f), new(0f, z, 1f) }, Kit.Seg), mat);

        // ================================================================ 0: house cue

        /// <summary>One-piece house cue: stained full splice (4 long points into the maple), printed gold lines and "19 OZ".</summary>
        static void ButtHouse(Kit k)
        {
            float z0 = k.Z, z1 = PoolDims.JointZ;
            var pts = new CueArt.Points { Base = 0.30f, Tip = 0.575f, BaseHalf = 1f, Curve = 1.05f, Splice = true };
            var tex = CueArt.Panel("house", 256, 2048, z0, z1, (u, z) =>
            {
                if ((z > 0.046f && z < 0.0475f) || (z > 0.0495f && z < 0.0502f) || (z > 0.262f && z < 0.2635f)) return 5;
                if (CueArt.Text("19 OZ", u, z, 0f, 0.10f, 0.0011f, Circ(z))) return 5;
                return CueArt.PointLayer(pts, u, z, Circ(z));
            }, (l, u, z) => l switch
            {
                -1 => CueArt.WoodAt(Woods.Maple, u, z),
                0 => CueArt.WoodAt(Woods.HouseStain, u, z),
                5 => Gold,
                _ => Black,
            }, 4);
            var mat = k.Mat("housebutt", Color.white, 0f, 0.7f, tex);
            k.Tube("Butt", z0, z1, mat, z0, z1, 2);
            k.Z = z1;
            if (k.Split) EndFace(k, z1, mat);
        }

        // ================================================================ 1: points

        /// <summary>Ebony sleeve with birdseye windows, black Irish linen, birdseye forearm with 4 ebony points and 4 veneers.</summary>
        static void ButtPoints(Kit k)
        {
            var dash = k.Dash("ebbone", Black, BoneC);
            k.Rings((0.0012f, k.Nickel, true), (0.0018f, k.Ebony, false), (0.0012f, k.Bone, false), (0.0018f, k.Ebony, false));
            float s0 = k.Z, s1 = s0 + 0.15f;
            var sleeve = CueArt.Panel("sleeve1", 512, 1024, s0, s1, (u, z) =>
            {
                float c = Circ(z), mid = (s0 + s1) * 0.5f;
                for (int i = 0; i < 4; i++)
                {
                    float cu = 0.125f + i * 0.25f;
                    if (CueArt.Diamond(u, z, cu, mid, 0.0021f, 0.013f, c) == 0) return 3;
                    int w = CueArt.Window(u, z, cu, mid, 0.0042f, 0.05f, 0.0026f, c, 0.0008f);
                    if (w >= 0) return w == 0 ? 2 : 1;
                    if (CueArt.Dot(u, z, i * 0.25f, mid, 0.0016f, 0.0016f, c) == 0) return 3;
                    if (CueArt.Dot(u, z, i * 0.25f, mid - 0.032f, 0.001f, 0.001f, c) == 0) return 3;
                    if (CueArt.Dot(u, z, i * 0.25f, mid + 0.032f, 0.001f, 0.001f, c) == 0) return 3;
                }
                return -1;
            }, (l, u, z) => l switch
            {
                1 => BoneC,
                2 => CueArt.WoodAt(Woods.Birdseye, u, z),
                3 => CueArt.PearlAt(u, z),
                _ => CueArt.WoodAt(Woods.Ebony, u, z),
            });
            k.Section("Sleeve", 0.15f, k.Mat("sleeve1", Color.white, 0f, 0.86f, sleeve));
            k.Rings((0.0012f, k.Bone, false), (0.003f, dash, false), (0.0012f, k.Bone, false), (0.0016f, k.Nickel, true));
            k.Section("Wrap", 0.262f, k.Mat("linenblack", Color.white, 0f, 0.12f,
                CueArt.Linen("black", new Color(0.045f, 0.045f, 0.05f), new Color(0.86f, 0.86f, 0.83f), 0.2f), CueArt.LinenNormal), CueArt.LinenTile);
            k.Rings((0.0016f, k.Nickel, true), (0.0012f, k.Bone, false), (0.003f, dash, false), (0.0012f, k.Bone, false));
            float f0 = k.Z, f1 = PoolDims.JointZ - 0.022f - 0.01f;
            var pts = new CueArt.Points { Offset = 0.125f, Base = f0, Tip = f0 + (f1 - f0) * 0.8f, BaseHalf = 0.68f, Veneers = new[] { 0.0005f, 0.0006f, 0.0006f, 0.0005f } };
            var fore = CueArt.Panel("fore1", 512, 1024, f0, f1, (u, z) =>
            {
                float c = Circ(z);
                for (int i = 0; i < 4; i++)
                {
                    if (CueArt.Diamond(u, z, 0.125f + i * 0.25f, f0 + 0.05f, 0.0022f, 0.011f, c) == 0) return 10;
                    if (CueArt.Dot(u, z, i * 0.25f, f0 + 0.018f, 0.0013f, 0.0013f, c) == 0) return 10;
                }
                return CueArt.PointLayer(pts, u, z, c);
            }, (l, u, z) => l switch
            {
                0 => CueArt.WoodAt(Woods.Ebony, u, z),
                1 => BoneC,
                2 or 4 => Black,
                3 => RedC,
                10 => CueArt.PearlAt(u, z),
                _ => CueArt.WoodAt(Woods.Birdseye, u, z),
            });
            k.Section("Forearm", f1 - f0, k.Mat("fore1", Color.white, 0f, 0.86f, fore));
            k.Rings((0.0015f, k.Nickel, true), (0.002f, k.Ebony, false), (0.003f, dash, false), (0.002f, k.Ebony, false), (0.0015f, k.Bone, false));
            k.JointCollar(k.Steel, k.Steel);
        }
    }
}
