using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Mechanical bridge ("rake"), 2026-11-25, in CUE SPACE: a long stained-ash stick along +Z (rubber bumper at
    /// z = 0), a chrome socket and a chrome head plate at z = <see cref="Length"/>. The plate stands across the stick in
    /// the X/Y plane, local +Y up: three round notches at different heights on a wavy crown, two arches between three feet.
    /// Lying on a table, the stick's axis is about <see cref="AxisAboveFeet"/> above the feet. No colliders.
    /// </summary>
    public static class CueBridgeModel
    {
        public const float Length = PoolDims.CueLength;
        public const float ButtR = 0.0135f, TopR = 0.0105f, HeadThick = 0.008f, SocketLen = 0.042f;
        /// <summary>Height of the stick's axis above the bottom of the head's feet.</summary>
        public const float AxisAboveFeet = 0.016f;
        /// <summary>Notch radius (a cue shaft fits with a little play) and the heights of the notch bottoms above the feet.</summary>
        public const float NotchR = 0.0072f;
        public static readonly float[] NotchHeights = { 0.024f, 0.038f, 0.052f };

        static readonly CueArt.Wood Ash = new()
        {
            Light = new Color(0.52f, 0.33f, 0.18f), Dark = new Color(0.26f, 0.14f, 0.07f), Lines = 14, LineAmt = 0.7f,
            LineSharp = 3f, Warp = 1.2f, Pores = 0.16f, Streak = 0.15f, Seed = 21,
        };

        public static Transform Build(Transform parent, Material template)
        {
            var root = new GameObject("CueBridge").transform;
            root.SetParent(parent, false);
            if (parent != null) root.gameObject.layer = parent.gameObject.layer;
            var k = new CueModel.Kit(template, 0, false) { Parent = root };
            var chrome = k.Mat("bridgechrome", new Color(0.80f, 0.81f, 0.83f), 0.85f, 0.74f);
            float zs = Length - HeadThick - SocketLen;

            // Rubber bumper and a black cap at the butt end.
            var bump = new List<Vector3> { new(0f, 0f, 0f), new(0.0105f, 0f, 0.02f) };
            CueMesh.Fillet(bump, 0.0105f, 0.0015f, 0.0015f, -90f, 0f, 5, 0.05f);
            bump.Add(new Vector3(0.012f, 0.012f, 0.3f));
            k.Part("Bumper", CueMesh.Lathe("bridgebumper", bump, 32), k.Rubber);
            var cap = new List<Vector3> { new(0.012f, 0.012f, 0f), new(StickR(0.012f) - 0.001f, 0.012f, 0.02f) };
            CueMesh.Fillet(cap, StickR(0.012f) - 0.001f, 0.013f, 0.001f, -90f, 0f, 5, 0.05f);
            cap.Add(new Vector3(StickR(0.04f), 0.04f, 0.6f));
            k.Part("ButtCap", CueMesh.Lathe("bridgecap", cap, 32), k.Phenolic);

            // Stained ash stick (grain along the length) with two thin painted rings near the butt.
            var tex = CueArt.Panel("bridgestick", 256, 2048, 0.04f, zs + 0.004f, (u, z) =>
                (z > 0.06f && z < 0.0625f) || (z > 0.066f && z < 0.0668f) ? 1 : -1,
                (l, u, z) => l == 1 ? new Color(0.80f, 0.64f, 0.30f) : CueArt.WoodAt(Ash, u, z));
            var stick = new List<Vector3>();
            for (int i = 0; i <= 6; i++)
            {
                float z = Mathf.Lerp(0.04f, zs + 0.004f, i / 6f);
                stick.Add(new Vector3(StickR(z), z, (z - 0.04f) / (zs + 0.004f - 0.04f)));
            }
            k.Part("Stick", CueMesh.Lathe("bridgestick", stick, 32), k.Mat("bridgestick", Color.white, 0f, 0.62f, tex));

            // Chrome socket: a flared collar round the stick, a straight barrel, a round boss on the plate.
            var sock = new List<Vector3>
            {
                new(StickR(zs) - 0.0002f, zs - 0.0008f, 0f), new(StickR(zs) + 0.0004f, zs, 0.01f), new(0.0128f, zs + 0.0035f, 0.05f),
            };
            sock.Add(new Vector3(0.0128f, zs + 0.006f, 0.1f));
            sock.Add(new Vector3(0.0128f, zs + 0.006f, 0.1f));
            sock.Add(new Vector3(0.0122f, zs + 0.008f, 0.12f));
            sock.Add(new Vector3(0.0122f, Length - HeadThick - 0.006f, 0.5f));
            sock.Add(new Vector3(0.0122f, Length - HeadThick - 0.0035f, 0.6f));
            CueMesh.Fillet(sock, 0.0122f + 0.0025f, Length - HeadThick - 0.0025f, 0.0025f, 180f, 90f, 5, 0.7f);
            k.Part("Socket", CueMesh.Lathe("bridgesocket", sock, 32), chrome);
            // Set screw on the side of the barrel.
            var screw = new List<Vector3> { new(0f, 0f, 0f), new(0.0026f, 0f, 0f), new(0.0026f, 0f, 0f), new(0.0026f, 0.0016f, 0.1f), new(0.0018f, 0.0024f, 0.2f), new(0f, 0.0024f, 0.3f) };
            var s = k.Part("SetScrew", CueMesh.Lathe("bridgescrew", screw, 16), chrome);
            s.transform.localPosition = new Vector3(0.0118f, 0f, zs + 0.022f);
            s.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // Head plate: outline in (x, y), extruded across Z with rounded edges.
            var head = k.Part("Head", BoardMeshes.Extrude("cuebridgehead", HeadOutline(), _ => HeadThick * 0.5f, 0.0026f, 5, 20f), chrome);
            head.transform.localPosition = new Vector3(0f, 0f, Length - HeadThick * 0.5f);
            head.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            // Domed acorn nut on a washer: holds the plate on the socket's threaded end.
            float L = Length;
            var nut = new List<Vector3> { new(0.0088f, L - 0.0004f, 0f), new(0.0088f, L + 0.0011f, 0.02f), new(0.0088f, L + 0.0011f, 0.02f) };
            nut.Add(new Vector3(0.0068f, L + 0.0011f, 0.04f));
            nut.Add(new Vector3(0.0068f, L + 0.0011f, 0.04f));
            nut.Add(new Vector3(0.0068f, L + 0.0045f, 0.08f));
            CueMesh.Fillet(nut, 0f, L + 0.0045f, 0.0068f, 0f, 90f, 8, 0.1f);
            k.Part("AcornNut", CueMesh.Lathe("bridgenut", nut, 32), chrome);
            return root;
        }

        /// <summary>Radius of the stick (straight taper).</summary>
        public static float StickR(float z) => Mathf.Lerp(ButtR, TopR, z / Length);

        /// <summary>
        /// Plate outline in the extrusion's (z, y) plane; the 90 degree turn of the part maps its first coordinate to +X.
        /// Feet at y = -AxisAboveFeet, lowest notch on +X, the tallest peak on -X.
        /// </summary>
        static List<Vector2> HeadOutline()
        {
            float yb = -AxisAboveFeet, w = 0.058f;
            var o = new List<Vector2>();
            // Bottom, left to right: three feet with two arches between them.
            o.Add(new Vector2(-w + 0.003f, yb));
            foreach (float cx in new[] { -0.034f, 0.034f })
            {
                o.Add(new Vector2(cx - 0.011f, yb));
                for (int i = 1; i < 8; i++)
                {
                    float a = Mathf.PI * (1f - i / 8f);
                    o.Add(new Vector2(cx + Mathf.Cos(a) * 0.011f, yb + Mathf.Sin(a) * 0.006f));
                }
                o.Add(new Vector2(cx + 0.011f, yb));
            }
            o.Add(new Vector2(w - 0.003f, yb));
            // Right side up to the crown.
            o.Add(new Vector2(w, yb + 0.004f));
            o.Add(new Vector2(w, yb + 0.022f));
            // Crown, right to left: a peak before each notch (round, fits the shaft), the tallest peak on the left.
            float[] notchX = { 0.034f, 0.0f, -0.034f };
            float[] peakH = { 0.038f, 0.052f, 0.066f, 0.071f };
            float[] peakX = { 0.049f, 0.017f, -0.017f, -0.049f };
            for (int n = 0; n < 3; n++)
            {
                AddPeak(o, peakX[n], yb + peakH[n]);
                float cy = yb + NotchHeights[n] + NotchR;
                for (int i = 0; i <= 10; i++)
                {
                    float a = -Mathf.PI * i / 10f;
                    o.Add(new Vector2(notchX[n] + Mathf.Cos(a) * NotchR, cy + Mathf.Sin(a) * NotchR));
                }
            }
            AddPeak(o, peakX[3], yb + peakH[3]);
            o.Add(new Vector2(-w, yb + 0.03f));
            o.Add(new Vector2(-w, yb + 0.004f));
            return o;
        }

        /// <summary>Rounded crown peak (small arc) centred at x, top at y.</summary>
        static void AddPeak(List<Vector2> o, float x, float y)
        {
            const float r = 0.0065f;
            for (int i = 0; i <= 6; i++)
            {
                float a = Mathf.PI * (0.08f + 0.84f * i / 6f);
                o.Add(new Vector2(x + Mathf.Cos(a) * r, y - r + Mathf.Sin(a) * r));
            }
        }
    }
}
