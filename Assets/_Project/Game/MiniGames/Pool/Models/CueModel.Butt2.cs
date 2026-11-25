using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public static partial class CueModel
    {
        // ================================================================ 2: cocobolo + natural Irish linen

        /// <summary>Cocobolo sleeve with pearl diamonds, natural Irish linen, cocobolo forearm with 4 ivory spears, brass rings, ivory joint.</summary>
        static void ButtLinen(Kit k)
        {
            var pearlDash = k.Dash("ebpearl", Black, new Color(0.9f, 0.88f, 0.86f));
            k.Rings((0.0015f, k.Brass, true), (0.003f, k.Ebony, false), (0.0015f, k.Brass, true));
            float s0 = k.Z, s1 = s0 + 0.15f, mid = (s0 + s1) * 0.5f;
            var sleeve = CueArt.Panel("sleeve2", 512, 1024, s0, s1, (u, z) =>
            {
                float c = Circ(z);
                for (int i = 0; i < 4; i++)
                {
                    int d = CueArt.Diamond(u, z, i * 0.25f, mid, 0.0036f, 0.0095f, c, 0.0007f);
                    if (d >= 0) return d == 0 ? 3 : 0;
                    if (CueArt.Dot(u, z, 0.125f + i * 0.25f, mid, 0.0013f, 0.0013f, c) == 0) return 3;
                }
                // Inlaid lines: ebony / bone / ebony bands above and below the diamonds.
                float dz = Mathf.Abs(Mathf.Abs(z - mid) - 0.036f);
                if (dz < 0.0004f) return 2;
                if (dz < 0.001f) return 0;
                return -1;
            }, (l, u, z) => l switch
            {
                0 => Black,
                2 => BoneC,
                3 => CueArt.PearlAt(u, z),
                _ => CueArt.WoodAt(Woods.Cocobolo, u, z),
            });
            k.Section("Sleeve", 0.15f, k.Mat("sleeve2", Color.white, 0f, 0.86f, sleeve));
            k.Rings((0.0015f, k.Ebony, false), (0.001f, k.Brass, true), (0.003f, pearlDash, false), (0.001f, k.Brass, true), (0.0015f, k.Ebony, false));
            k.Section("Wrap", 0.262f, k.Mat("linennat", Color.white, 0f, 0.14f,
                CueArt.Linen("natural", new Color(0.62f, 0.52f, 0.38f), new Color(0.30f, 0.20f, 0.12f), 0.3f), CueArt.LinenNormal), CueArt.LinenTile);
            k.Rings((0.0015f, k.Ebony, false), (0.001f, k.Brass, true), (0.003f, pearlDash, false), (0.001f, k.Brass, true), (0.0015f, k.Ebony, false));
            float f0 = k.Z, f1 = PoolDims.JointZ - 0.022f - 0.01f, fm = f0 + (f1 - f0) * 0.52f;
            var fore = CueArt.Panel("fore2", 512, 1024, f0, f1, (u, z) =>
            {
                float c = Circ(z);
                for (int i = 0; i < 4; i++)
                {
                    float cu = 0.125f + i * 0.25f;
                    int d = CueArt.Diamond(u, z, cu, fm, 0.0027f, 0.068f, c, 0.0008f);
                    if (d >= 0)
                    {
                        if (d == 0 && CueArt.Diamond(u, z, cu, fm, 0.0011f, 0.022f, c) == 0) return 2;
                        return d == 0 ? 1 : 0;
                    }
                    if (CueArt.Dot(u, z, cu, fm + 0.08f, 0.0012f, 0.0012f, c) == 0) return 2;
                    if (CueArt.Dot(u, z, cu, fm - 0.08f, 0.0012f, 0.0012f, c) == 0) return 2;
                    if (CueArt.Diamond(u, z, i * 0.25f, f0 + 0.022f, 0.0018f, 0.006f, c) == 0) return 2;
                }
                return -1;
            }, (l, u, z) => l switch
            {
                0 => Black,
                1 => BoneC * (0.96f + 0.04f * CueArt.Noise(u * 60f, z * 300f, 60, 9)),
                2 => CueArt.PearlAt(u, z),
                _ => CueArt.WoodAt(Woods.Cocobolo, u, z),
            });
            k.Section("Forearm", f1 - f0, k.Mat("fore2", Color.white, 0f, 0.86f, fore));
            k.Rings((0.0015f, k.Brass, true), (0.0025f, k.Ebony, false), (0.002f, pearlDash, false), (0.0025f, k.Ebony, false), (0.0015f, k.Brass, true));
            k.JointCollar(k.IvoryPhenolic, k.Steel);
        }

        // ================================================================ 3: ebony + pearl + leather

        /// <summary>Ebony sleeve with framed pearl diamonds, black leather wrap, ebony forearm with floating pearl points, steel joint.</summary>
        static void ButtEbony(Kit k)
        {
            var dash = k.Dash("pearleb", new Color(0.9f, 0.88f, 0.86f), Black);
            k.Rings((0.001f, k.Nickel, true), (0.002f, k.Ebony, false), (0.001f, k.Nickel, true), (0.002f, k.Ebony, false));
            float s0 = k.Z, s1 = s0 + 0.15f, mid = (s0 + s1) * 0.5f;
            var sleeve = CueArt.Panel("sleeve3", 512, 1024, s0, s1, (u, z) =>
            {
                float c = Circ(z);
                for (int i = 0; i < 4; i++)
                {
                    float cu = 0.125f + i * 0.25f;
                    int d = CueArt.Diamond(u, z, cu, mid, 0.0048f, 0.024f, c, 0.0006f);
                    if (d >= 0)
                    {
                        if (d == 0 && CueArt.Diamond(u, z, cu, mid, 0.0016f, 0.008f, c, 0.0005f) == 1) return 1;
                        return d == 0 ? 0 : 1;
                    }
                    if (CueArt.Dot(u, z, i * 0.25f, mid, 0.0014f, 0.0014f, c) == 0) return 0;
                    if (CueArt.Diamond(u, z, cu, mid + 0.04f, 0.0018f, 0.006f, c) == 0) return 0;
                    if (CueArt.Diamond(u, z, cu, mid - 0.04f, 0.0018f, 0.006f, c) == 0) return 0;
                }
                return -1;
            }, (l, u, z) => l switch
            {
                0 => CueArt.PearlAt(u, z),
                1 => Silver,
                _ => CueArt.WoodAt(Woods.Ebony, u, z),
            });
            k.Section("Sleeve", 0.15f, k.Mat("sleeve3", Color.white, 0f, 0.9f, sleeve));
            k.Rings((0.001f, k.Nickel, true), (0.0015f, k.Ebony, false), (0.003f, dash, false), (0.0015f, k.Ebony, false), (0.001f, k.Nickel, true));
            k.Section("Wrap", 0.262f, k.Mat("leatherblack", Color.white, 0f, 0.36f,
                CueArt.Leather("black", new Color(0.075f, 0.068f, 0.062f)), CueArt.LeatherNormal), CueArt.LeatherTile);
            k.Rings((0.001f, k.Nickel, true), (0.0015f, k.Ebony, false), (0.003f, dash, false), (0.0015f, k.Ebony, false), (0.001f, k.Nickel, true));
            float f0 = k.Z, f1 = PoolDims.JointZ - 0.020f - 0.01f;
            var pts = new CueArt.Points
            {
                Offset = 0.125f, Base = f0 + 0.03f, Tip = f0 + (f1 - f0) * 0.78f, BaseHalf = 0.4f, Curve = 1.1f,
                Veneers = new[] { 0.0005f, 0.0005f },
            };
            var fore = CueArt.Panel("fore3", 512, 1024, f0, f1, (u, z) =>
            {
                float c = Circ(z);
                for (int i = 0; i < 8; i++)
                    if (CueArt.Dot(u, z, i * 0.125f, f1 - 0.012f, 0.0011f, 0.0011f, c) == 0) return 0;
                for (int i = 0; i < 4; i++)
                {
                    if (CueArt.Diamond(u, z, 0.125f + i * 0.25f, f0 + 0.05f, 0.0014f, 0.007f, c) == 0) return 9;
                    if (CueArt.Diamond(u, z, i * 0.25f, f0 + 0.02f, 0.0016f, 0.005f, c) == 0) return 0;
                }
                return CueArt.PointLayer(pts, u, z, c);
            }, (l, u, z) => l switch
            {
                0 => CueArt.PearlAt(u, z),
                1 => Silver,
                2 or 9 => Black,
                _ => CueArt.WoodAt(Woods.Ebony, u, z),
            });
            k.Section("Forearm", f1 - f0, k.Mat("fore3", Color.white, 0f, 0.9f, fore));
            k.Rings((0.001f, k.Nickel, true), (0.002f, k.Ebony, false), (0.004f, dash, false), (0.002f, k.Ebony, false), (0.001f, k.Nickel, true));
            k.JointCollar(k.Steel, k.Steel);
        }
    }
}
