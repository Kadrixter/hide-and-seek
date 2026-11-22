using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Column-mounted horn loudspeaker (typical car-park PA): galvanised plate bolted to the column face, a square arm
    /// with a gusset, a U yoke with pivot knobs holding the driver, an off-white aluminium re-entrant style horn
    /// (exponential flare, rolled rim, dark inside, phase plug in the throat) tilted down towards the aisle, the cable
    /// from the back of the driver looping to a gland on the plate, and a steel conduit up the column into the ceiling.
    /// Speaker frame: origin on the column face at the horn pivot height, +Z out of the column, +Y up.
    /// </summary>
    public static class PaSpeakerModel
    {
        public const int SubHorn = 0, SubDark = 1, SubSteel = 2, SubBolt = 3, SubCable = 4;
        public const float Tilt = 17f;
        /// <summary>Horn pivot (speaker frame).</summary>
        public static readonly Vector3 Pivot = new Vector3(0f, 0f, 0.225f);
        /// <summary>Centre of the horn mouth (speaker frame): where the sound comes from.</summary>
        public static Vector3 MouthLocal => Pivot + Quaternion.Euler(Tilt, 0f, 0f) * new Vector3(0f, 0f, 0.31f);
        /// <summary>Box enclosing the speaker (speaker frame) for the free-space check and the collider.</summary>
        public static readonly Vector3 BoundsCentre = new Vector3(0f, -0.0625f, 0.3f), BoundsSize = new Vector3(0.27f, 0.325f, 0.56f);

        private static Mesh _mesh;
        private static Material[] _mats;

        public static Material[] Materials()
        {
            if (_mats != null) return _mats;
            _mats = new[]
            {
                PaMesh.Mat("PaHorn", new Color(0.8f, 0.79f, 0.74f), 0.42f, 0.25f),
                PaMesh.Mat("PaHornDark", new Color(0.11f, 0.11f, 0.115f), 0.3f, 0.2f),
                PaMesh.Mat("PaGalv", new Color(0.56f, 0.58f, 0.59f), 0.45f, 0.7f),
                PaMesh.Mat("PaBolt", new Color(0.66f, 0.66f, 0.64f), 0.6f, 0.8f),
                PaMesh.Mat("PaCable", new Color(0.05f, 0.05f, 0.05f), 0.3f),
            };
            return _mats;
        }

        public static Mesh Mesh(float ceilingAbove)
        {
            if (_mesh != null) return _mesh;
            var m = new PaMesh(5);
            Vector3 R = Vector3.right, U = Vector3.up, F = Vector3.forward;

            // Plate on the column face + four bolts.
            m.Pebble(PaMesh.RoundRect(0.07f, 0.095f, 0.01f, 3), PaMesh.SlabRings(0.0005f, 0.008f, 0.0015f, 2), Vector3.zero, R, U, F, SubSteel);
            foreach (float bx in new[] { -0.046f, 0.046f })
            foreach (float by in new[] { -0.07f, 0.07f })
            {
                m.Lathe(new Vector3(bx, by, 0.008f), F, new[] { new Vector2(0.0095f, 0f), new Vector2(0.0095f, 0.0015f), new Vector2(0f, 0.0015f) }, 12, SubBolt);
                m.Lathe(new Vector3(bx, by, 0.0095f), F, new[] { new Vector2(0.0072f, 0f), new Vector2(0.0072f, 0.0055f), new Vector2(0f, 0.0055f) }, 6, SubBolt);
            }

            // Square arm out of the plate, gusset under it.
            m.Box(new Vector3(0f, 0f, 0.056f), new Vector3(0.04f, 0.04f, 0.098f), Quaternion.identity, SubSteel);
            m.Box(new Vector3(0f, -0.042f, 0.04f), new Vector3(0.006f, 0.07f, 0.068f), Quaternion.Euler(-42f, 0f, 0f), SubSteel);

            // U yoke: back bar behind the driver and two side arms reaching the pivot, short shafts into the driver.
            m.Box(new Vector3(0f, 0f, 0.108f), new Vector3(0.17f, 0.034f, 0.012f), Quaternion.identity, SubSteel);
            foreach (float s in new[] { -1f, 1f })
            {
                m.Box(new Vector3(s * 0.079f, 0f, 0.172f), new Vector3(0.008f, 0.034f, 0.14f), Quaternion.identity, SubSteel);
                m.Lathe(new Vector3(s * 0.05f, 0f, Pivot.z), new Vector3(s, 0f, 0f), new[] { new Vector2(0.0085f, 0f), new Vector2(0.0085f, 0.026f) }, 10, SubBolt);
                // pivot knob
                m.Lathe(new Vector3(s * 0.083f, 0f, Pivot.z), new Vector3(s, 0f, 0f), new[] { new Vector2(0.0075f, 0f), new Vector2(0.0075f, 0.008f), new Vector2(0.014f, 0.009f), new Vector2(0.014f, 0.018f), new Vector2(0.011f, 0.021f), new Vector2(0f, 0.021f) }, 12, SubBolt);
            }

            Quaternion tilt = Quaternion.Euler(Tilt, 0f, 0f);
            Vector3 A = tilt * F;
            Vector3 P = Pivot;
            // Driver (compression driver can) with a rounded back.
            m.Lathe(P - A * 0.088f, A, new[]
            {
                new Vector2(0f, 0f), new Vector2(0.03f, 0.001f), new Vector2(0.046f, 0.006f), new Vector2(0.054f, 0.017f),
                new Vector2(0.056f, 0.03f), new Vector2(0.056f, 0.098f), new Vector2(0.06f, 0.101f), new Vector2(0.06f, 0.107f), new Vector2(0.034f, 0.11f),
            }, 24, SubDark);
            // Back label ring + cable gland on the driver.
            m.Lathe(P - A * 0.0885f, -A, new[] { new Vector2(0.022f, 0f), new Vector2(0.022f, 0.002f), new Vector2(0f, 0.0022f) }, 18, SubBolt);

            // Horn: exponential flare from the throat, rolled rim, dark inner surface back to the throat.
            const float L = 0.29f, r0 = 0.034f, r1 = 0.118f;
            Vector3 throat = P + A * 0.02f; // right in front of the driver's flange
            var outer = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float s = i / 12f;
                float r = r0 * Mathf.Pow(r1 / r0, s * s * 0.55f + s * 0.45f);
                outer.Add(new Vector2(r + 0.002f, s * L));
            }
            outer.Add(new Vector2(r1 + 0.008f, L + 0.003f));
            outer.Add(new Vector2(r1 + 0.009f, L + 0.009f));
            outer.Add(new Vector2(r1 + 0.005f, L + 0.0125f));
            m.Lathe(throat, A, outer, 32, SubHorn);
            var inner = new List<Vector2> { new Vector2(r1 + 0.005f, L + 0.0125f), new Vector2(r1 - 0.002f, L + 0.008f) };
            for (int i = 12; i >= 0; i--)
            {
                float s = i / 12f;
                float r = r0 * Mathf.Pow(r1 / r0, s * s * 0.55f + s * 0.45f);
                inner.Add(new Vector2(Mathf.Max(0.004f, r - 0.002f), s * L - 0.002f));
            }
            inner.Add(new Vector2(0f, -0.002f));
            m.Lathe(throat, A, inner, 32, SubDark);
            // Phase plug in the throat.
            m.Lathe(throat + A * 0.004f, A, new[] { new Vector2(0.02f, 0f), new Vector2(0.012f, 0.03f), new Vector2(0.004f, 0.05f), new Vector2(0f, 0.052f) }, 14, SubBolt);
            // Clamp ring where the horn meets the driver.
            m.Lathe(throat - A * 0.006f, A, new[] { new Vector2(0.043f, 0f), new Vector2(0.045f, 0.002f), new Vector2(0.045f, 0.012f), new Vector2(0.036f, 0.014f) }, 20, SubSteel);

            // Cable: out of the driver's back, a loose loop down, to a gland at the bottom of the plate.
            Vector3 c0 = P - A * 0.088f + tilt * new Vector3(0f, -0.018f, -0.004f);
            Vector3 c1 = c0 + new Vector3(0.035f, -0.07f, -0.04f);
            Vector3 c3 = new Vector3(0.045f, -0.1f, 0.012f);
            Vector3 c2 = c3 + new Vector3(0.01f, -0.06f, 0.04f);
            var cable = new List<Vector3>();
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f, u = 1f - t;
                cable.Add(u * u * u * c0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * c3);
            }
            m.Tube(cable, 0.0042f, 7, SubCable);
            m.Lathe(new Vector3(0.045f, -0.091f, 0.008f), Vector3.down, new[] { new Vector2(0.0075f, 0f), new Vector2(0.0075f, 0.007f), new Vector2(0.005f, 0.012f), new Vector2(0f, 0.012f) }, 6, SubBolt);

            // Conduit from the top of the plate up the column into the ceiling, saddle clamps.
            Vector3 ct = new Vector3(-0.03f, 0.093f, 0.012f);
            m.Lathe(ct, U, new[] { new Vector2(0.014f, 0f), new Vector2(0.014f, 0.012f), new Vector2(0.011f, 0.015f), new Vector2(0f, 0.015f) }, 6, SubSteel);
            m.Tube(new[] { ct + U * 0.01f, new Vector3(ct.x, ceilingAbove + 0.01f, ct.z) }, 0.0095f, 10, SubSteel);
            for (float y = 0.25f; y < ceilingAbove - 0.08f; y += 0.22f)
                m.Box(new Vector3(ct.x, y, 0.0115f), new Vector3(0.024f, 0.012f, 0.023f), Quaternion.identity, SubSteel);

            _mesh = m.ToMesh("PaSpeaker");
            return _mesh;
        }
    }
}
