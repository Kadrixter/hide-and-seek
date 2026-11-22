using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// The transmitter slammed onto the concrete: the case split in three (the face half with the cracked blue plate,
    /// the dead LCD and one bent stick, the left handle end on its side, the right handle torn off), the telescopic
    /// antenna snapped into its segments, both yellow knobs and the gimbal springs popped out, the battery cover and
    /// four AA batteries scattered, a bit of green circuit board, the LED and blue / yellow / grey splinters.
    /// Every piece comes from a seeded random, so every player sees the same wreck; no colliders; it stays on the
    /// floor until the round ends.
    /// </summary>
    public static partial class RcRemoteModel
    {
        /// <summary>Builds the wreck at <paramref name="pos"/> (floor) / <paramref name="yaw"/>. <paramref name="burst"/>: pieces fly out first.</summary>
        public static GameObject CreateWreck(Vector3 pos, float yaw, int seed, bool burst)
        {
            var rng = new System.Random(seed * 6151 + 29);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var root = new GameObject("RcTransmitterWreck").transform;
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var pieces = new List<Transform>();
            Transform Piece(string name, Vector3 at, float turn, Vector3 tilt)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root, false);
                t.localPosition = at;
                t.localRotation = Quaternion.Euler(tilt.x, turn, tilt.z);
                pieces.Add(t);
                return t;
            }
            var cs = Mat(Case, 0.35f);
            var blue = Mat(Blue, 0.62f);
            var yellow = Mat(Yellow, 0.5f);
            var white = Mat(White, 0.5f);
            var rubber = Mat(Rubber, 0.08f);
            var black = Mat(Black, 0.25f);
            const int L = 0;

            // Face half lying face up (floor space: x across, z = the remote's top edge, y up): cracked blue plate,
            // a jagged edge where the left end broke off, the dead LCD, one stick still in its gimbal, bent.
            var face = Piece("FaceHalf", new Vector3(R(-0.02f, 0.02f), 0f, R(-0.02f, 0.02f)), R(-15f, 15f), new Vector3(R(-3f, 3f), 0f, R(-4f, 4f)));
            B(face, new Vector3(-0.13f, 0f, -0.065f), new Vector3(0.12f, 0.026f, 0.06f), cs, L);
            B(face, new Vector3(-0.115f, 0.026f, -0.05f), new Vector3(0.105f, 0.032f, 0.05f), blue, L);
            B(face, new Vector3(-0.115f, 0.032f, -0.042f), new Vector3(0.105f, 0.034f, -0.035f), white, L);
            B(face, new Vector3(0.12f, 0f, -0.04f), new Vector3(0.135f, 0.02f, 0.01f), cs, L);   // jagged edge
            B(face, new Vector3(0.105f, 0.026f, -0.02f), new Vector3(0.118f, 0.03f, 0.03f), blue, L);
            B(face, new Vector3(-0.04f, 0.032f, 0.02f), new Vector3(0.04f, 0.035f, 0.045f), black, L);
            B(face, new Vector3(-0.034f, 0.035f, 0.024f), new Vector3(0.034f, 0.036f, 0.041f), Mat(new Color(0.1f, 0.13f, 0.1f), 0.7f), L); // LCD off
            for (int i = 0; i < 3; i++)
            {
                var crack = B(face, Vector3.zero, new Vector3(0.003f, 0.0015f, R(0.04f, 0.09f)), white, L);
                crack.localPosition = new Vector3(R(-0.09f, 0.08f), 0.0335f, R(-0.03f, 0.03f));
                crack.localRotation = Quaternion.Euler(0f, R(-70f, 70f), 0f);
            }
            B(face, new Vector3(-0.095f, 0.032f, -0.03f), new Vector3(-0.025f, 0.036f, 0.04f), black, L); // right gimbal
            var shaft = B(face, Vector3.zero, new Vector3(0.009f, 0.04f, 0.009f), ChromeMat, L);
            shaft.localPosition = new Vector3(-0.06f, 0.052f, 0.005f);
            shaft.localRotation = Quaternion.Euler(R(20f, 35f), 0f, R(-25f, 25f));
            var led = B(face, new Vector3(-0.007f, 0.032f, -0.02f), new Vector3(0.007f, 0.038f, -0.006f), LedOff, L);
            led.name = "DeadLed";

            // Left end of the case, face up, the left handle still on it (sticking out where the top edge was).
            var end = Piece("LeftEnd", Scatter(rng, 0.17f, 0.26f), R(0f, 360f), new Vector3(R(-4f, 4f), 0f, R(-4f, 4f)));
            B(end, new Vector3(-0.055f, 0f, -0.06f), new Vector3(0.05f, 0.034f, 0.06f), cs, L);
            B(end, new Vector3(-0.045f, 0.034f, -0.05f), new Vector3(0.04f, 0.04f, 0.05f), blue, L);
            B(end, new Vector3(-0.045f, 0.04f, -0.045f), new Vector3(0.04f, 0.042f, -0.038f), white, L);
            B(end, new Vector3(-0.04f, 0f, -0.195f), new Vector3(0.04f, 0.054f, -0.06f), rubber, L);
            for (int i = 0; i < 3; i++)
                B(end, new Vector3(-0.043f, 0f, -0.18f + i * 0.03f), new Vector3(0.043f, 0.057f, -0.172f + i * 0.03f), black, L);
            B(end, new Vector3(-0.036f, 0.004f, -0.206f), new Vector3(0.036f, 0.05f, -0.195f), yellow, L);

            // Right handle torn off, lying.
            var grip = Piece("RightHandle", Scatter(rng, 0.14f, 0.24f), R(0f, 360f), new Vector3(0f, 0f, R(-6f, 6f)));
            B(grip, new Vector3(-0.04f, 0f, 0f), new Vector3(0.04f, 0.054f, 0.135f), rubber, L);
            for (int i = 0; i < 3; i++)
                B(grip, new Vector3(-0.043f, 0f, 0.02f + i * 0.03f), new Vector3(0.043f, 0.057f, 0.028f + i * 0.03f), black, L);
            B(grip, new Vector3(-0.036f, 0.004f, -0.011f), new Vector3(0.036f, 0.05f, 0f), yellow, L);

            // Antenna: socket with the first segment, the bent middle one, the thin tip with the red ball.
            var a1 = Piece("AntennaBase", Scatter(rng, 0.22f, 0.34f) + Vector3.up * 0.016f, R(0f, 360f), new Vector3(90f, 0f, 0f));
            B(a1, new Vector3(-0.016f, -0.004f, -0.016f), new Vector3(0.016f, 0.02f, 0.016f), black, L);
            B(a1, new Vector3(-0.006f, 0.02f, -0.006f), new Vector3(0.006f, 0.11f, 0.006f), ChromeMat, L);
            var a2 = Piece("AntennaMid", Scatter(rng, 0.3f, 0.48f) + Vector3.up * 0.0045f, R(0f, 360f), new Vector3(90f, 0f, 0f));
            B(a2, new Vector3(-0.0045f, 0f, -0.0045f), new Vector3(0.0045f, 0.05f, 0.0045f), ChromeMat, L);
            var bent = B(a2, new Vector3(-0.0045f, 0f, -0.0045f), new Vector3(0.0045f, 0.04f, 0.0045f), ChromeMat, L);
            bent.localPosition = new Vector3(0f, 0.065f, 0.005f);
            bent.localRotation = Quaternion.Euler(R(18f, 30f), 0f, 0f);
            var a3 = Piece("AntennaTip", Scatter(rng, 0.4f, 0.62f) + Vector3.up * 0.01f, R(0f, 360f), new Vector3(90f, 0f, 0f));
            B(a3, new Vector3(-0.003f, 0f, -0.003f), new Vector3(0.003f, 0.08f, 0.003f), ChromeMat, L);
            Ball(a3, new Vector3(0f, 0.086f, 0f), 0.02f, Mat(TipRed, 0.6f), L);

            // Stick knobs, the other stick, gimbal springs.
            for (int i = 0; i < 2; i++)
            {
                var k = Piece("Knob", Scatter(rng, 0.12f, 0.4f), R(0f, 360f), new Vector3(R(-10f, 10f), 0f, R(-10f, 10f)));
                Cyl(k, new Vector3(0f, 0.006f, 0f), 0.03f, 0.012f, yellow, L);
                Cyl(k, new Vector3(0f, 0.013f, 0f), 0.022f, 0.003f, black, L);
            }
            var st = Piece("Stick", Scatter(rng, 0.12f, 0.3f) + Vector3.up * 0.013f, R(0f, 360f), new Vector3(90f, 0f, 0f));
            B(st, new Vector3(-0.0045f, 0f, -0.0045f), new Vector3(0.0045f, 0.04f, 0.0045f), ChromeMat, L);
            Cyl(st, new Vector3(0f, 0f, 0f), 0.026f, 0.008f, black, L);
            for (int i = 0; i < 2; i++)
            {
                var sp = Piece("Spring", Scatter(rng, 0.1f, 0.32f) + Vector3.up * 0.008f, R(0f, 360f), new Vector3(90f, 0f, 0f));
                for (int c = 0; c < 5; c++)
                {
                    var coil = B(sp, Vector3.zero, new Vector3(0.016f, 0.0025f, 0.0025f), ChromeMat, L);
                    coil.localPosition = new Vector3(0f, 0.004f + c * 0.005f, 0f);
                    coil.localRotation = Quaternion.Euler(0f, c * 70f, 12f);
                }
            }

            // Battery cover and four AA cells rolled away.
            var cover = Piece("BatteryCover", Scatter(rng, 0.16f, 0.3f), R(0f, 360f), new Vector3(0f, 0f, R(-4f, 4f)));
            B(cover, new Vector3(-0.09f, 0f, -0.045f), new Vector3(0.09f, 0.006f, 0.045f), Mat(CaseLight, 0.3f), L);
            B(cover, new Vector3(-0.015f, 0.006f, 0.032f), new Vector3(0.015f, 0.01f, 0.04f), black, L);
            for (int i = 0; i < 4; i++)
            {
                var bat = Piece("Battery", Scatter(rng, 0.14f, 0.5f) + Vector3.up * 0.013f, R(0f, 360f), new Vector3(0f, 0f, 90f));
                Color shell = i % 2 == 0 ? new Color(0.75f, 0.1f, 0.08f) : new Color(0.1f, 0.1f, 0.11f);
                Cyl(bat, new Vector3(0f, 0f, 0f), 0.026f, 0.072f, Mat(shell, 0.55f), L);
                Cyl(bat, new Vector3(0f, 0.03f, 0f), 0.0265f, 0.014f, Mat(new Color(0.85f, 0.68f, 0.25f), 0.7f), L);
                Cyl(bat, new Vector3(0f, 0.04f, 0f), 0.01f, 0.006f, ChromeMat, L);
            }

            // Circuit board with a chip and solder dots, splinters.
            var pcb = Piece("Board", Scatter(rng, 0.08f, 0.22f), R(0f, 360f), new Vector3(R(-6f, 6f), 0f, R(-6f, 6f)));
            B(pcb, new Vector3(-0.05f, 0f, -0.035f), new Vector3(0.05f, 0.005f, 0.035f), Mat(new Color(0.08f, 0.42f, 0.18f), 0.5f), L);
            B(pcb, new Vector3(-0.018f, 0.005f, -0.012f), new Vector3(0.012f, 0.011f, 0.014f), black, L);
            for (int i = 0; i < 6; i++)
                B(pcb, new Vector3(0.022f + (i % 2) * 0.01f, 0.005f, -0.025f + (i / 2) * 0.02f), new Vector3(0.027f + (i % 2) * 0.01f, 0.007f, -0.02f + (i / 2) * 0.02f),
                    Mat(new Color(0.85f, 0.75f, 0.45f), 0.8f), L);
            for (int i = 0; i < 12; i++)
            {
                var sp = Piece("Splinter", Scatter(rng, 0.06f, 0.42f), R(0f, 360f), new Vector3(R(-10f, 10f), 0f, R(-10f, 10f)));
                var m = (i % 4) switch { 0 => blue, 1 => cs, 2 => yellow, _ => Mat(CaseLight, 0.3f) };
                B(sp, new Vector3(-R(0.004f, 0.012f), 0f, -R(0.006f, 0.016f)), new Vector3(R(0.004f, 0.012f), 0.006f, R(0.006f, 0.016f)), m, L);
            }

            if (burst) root.gameObject.AddComponent<PhoneWreckSettle>().Begin(pieces, seed);
            return root.gameObject;
        }

        static Vector3 Scatter(System.Random rng, float min, float max)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0), d = min + (float)rng.NextDouble() * (max - min);
            return new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
        }
    }
}
