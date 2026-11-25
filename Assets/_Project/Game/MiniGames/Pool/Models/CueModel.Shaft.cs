using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public static partial class CueModel
    {
        /// <summary>Threaded insert in the shaft's joint end: hole radius, outer radius of the brass insert ring, hole depth.</summary>
        const float HoleR = PinR + 0.0003f, InsertR = 0.0066f, HoleDepth = 0.023f;

        /// <summary>Shaft: joint collar + insert (two-piece cues), maple with the pro taper, ferrule, layered tip with chalk.</summary>
        static void BuildShaft(Kit k)
        {
            float J = PoolDims.JointZ;
            k.Z = J;
            var maple = k.Mat("shaftmaple", Color.white, 0f, 0.6f, CueArt.WoodTex("shaft", Woods.Maple, 256, 2048, J, FerruleZ0));
            if (IsOnePiece(k.Variant))
            {
                // One-piece cue: only a split build shows the cut end grain.
                if (k.Split)
                    k.Part("ShaftEnd", CueMesh.Lathe("shaftend", new List<Vector3> { new(0f, J, 0f), new(ShaftRadius(J), J, 0f) }, Kit.Seg), maple);
            }
            else ShaftJoint(k);
            k.Tube("Shaft", k.Z, FerruleZ0, maple, J, FerruleZ0, 30, 0f, true);
            k.Tube("Ferrule", FerruleZ0, TipZ0, k.Ferrule, 0f, 0.05f, 1, 0f, true);
            Tip(k);
        }

        /// <summary>Shaft collar of the variant (from the joint up) with the threaded brass insert and a few rings above it.</summary>
        static void ShaftJoint(Kit k)
        {
            float J = PoolDims.JointZ;
            Material collar;
            float len;
            (float, Material, bool)[] rings;
            switch (k.Variant)
            {
                case 1: collar = k.Steel; len = 0.020f; rings = new[] { (0.0012f, k.Ebony, false), (0.0012f, k.Bone, false), (0.0012f, k.Ebony, false) }; break;
                case 2: collar = k.IvoryPhenolic; len = 0.022f; rings = new[] { (0.0012f, k.Brass, true), (0.0015f, k.Ebony, false) }; break;
                default: collar = k.Steel; len = 0.020f; rings = new[] { (0.0015f, k.Ebony, false), (0.002f, k.Pearl, false), (0.0015f, k.Ebony, false) }; break;
            }
            // Insert: the threaded hole (ridges inside) and its brass face ring.
            var ins = new List<Vector3> { new(0f, J + HoleDepth, 0f), new(HoleR, J + HoleDepth, 0.1f), new(HoleR, J + HoleDepth, 0.1f) };
            for (float z = J + HoleDepth - 0.001f; z > J + 0.0015f; z -= ThreadPitch)
            {
                ins.Add(new Vector3(HoleR + 0.0005f, z - ThreadPitch * 0.45f, (z - J) * 40f));
                ins.Add(new Vector3(HoleR + 0.0005f, z - ThreadPitch * 0.55f, (z - J) * 40f));
                ins.Add(new Vector3(HoleR, z - ThreadPitch, (z - J) * 40f));
            }
            ins.Add(new Vector3(HoleR + 0.0004f, J, 0f));
            ins.Add(new Vector3(HoleR + 0.0004f, J, 0f));
            ins.Add(new Vector3(InsertR, J, 0.2f));
            k.Part("Insert", CueMesh.Lathe("insert", ins, 28), k.Brass);
            // Collar: face ring from the insert out, a bevel at the joint line, then the side.
            float c = 0.0004f, z1 = J + len;
            var p = new List<Vector3>
            {
                new(InsertR, J, 0f), new(JointR - c, J, 0f), new(JointR - c, J, 0f),
                new(ShaftRadius(J + c), J + c, c / 0.05f), new(ShaftRadius(J + c), J + c, c / 0.05f),
                new(ShaftRadius(z1 - 0.00012f), z1 - 0.00012f, len / 0.05f), new(ShaftRadius(z1 - 0.00012f), z1 - 0.00012f, len / 0.05f),
                new(ShaftRadius(z1) - 0.00012f, z1, len / 0.05f), new(ShaftRadius(z1) - 0.00012f, z1, len / 0.05f),
                new(ShaftRadius(z1), z1, len / 0.05f),
            };
            k.Part("ShaftCollar", CueMesh.Lathe("shaftcollar" + k.Variant, p, Kit.Seg), collar);
            k.Z = z1;
            foreach (var (l, m, metal) in rings)
            {
                k.Tube(metal ? "ShaftRingMetal" : "ShaftRing", k.Z, k.Z + l, m, 0f, 0.05f, 1, metal ? 0.00012f : 0f, true);
                k.Z += l;
            }
        }

        /// <summary>Tip: straight laminated side, a burnished rounded edge, a "nickel" radius dome covered in chalk.</summary>
        static void Tip(Kit k)
        {
            float zc = PoolDims.CueLength - DomeR, rE = TipR - 0.00022f;
            float zE = zc + Mathf.Sqrt(DomeR * DomeR - rE * rE);
            var p = new List<Vector3>
            {
                new(TipR, TipZ0, 0f), new(TipR, zE - 0.0006f, 0.56f), new(TipR - 0.00006f, zE - 0.00022f, 0.6f), new(rE, zE, 0.62f),
            };
            float aE = Mathf.Asin(rE / DomeR);
            for (int i = 1; i <= 10; i++)
            {
                float a = aE * (1f - i / 10f);
                p.Add(new Vector3(DomeR * Mathf.Sin(a), zc + DomeR * Mathf.Cos(a), 0.62f + 0.38f * i / 10f));
            }
            bool fresh = k.Variant != 0;
            k.Part("Tip", CueMesh.Lathe("tip", p, Kit.Seg), k.Mat("tip" + fresh, Color.white, 0f, 0.18f, CueArt.Tip(fresh)));
        }
    }
}
