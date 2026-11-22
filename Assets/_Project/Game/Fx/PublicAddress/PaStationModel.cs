using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Wall station of the public-address system, in the station frame (<see cref="PaLayout"/>: origin on the wall at
    /// the box centre, +Z out of the wall): galvanised mounting plate with four screws, light-grey steel box with a door
    /// (seam, hinges, quarter-turn lock), red "NAGŁOŚNIENIE" plate, status lamp (green free / red in use, glows through
    /// a property block), volume knob, vent slots of the monitor speaker, cord gland underneath, steel conduit up the
    /// wall to the ceiling, and right of the box the mic hook: wall plate with a stand-off tongue whose slot takes the
    /// mic's hang tab. One mesh with six material slots; the lamp is slot <see cref="SubLamp"/>.
    /// </summary>
    public static class PaStationModel
    {
        public const int SubBody = 0, SubDark = 1, SubRed = 2, SubSteel = 3, SubRubber = 4, SubLamp = 5;
        /// <summary>Lamp lens centre (station frame) - also where a small glow sits.</summary>
        public static readonly Vector3 LampLocal = new Vector3(-0.05f, 0.05f, 0.094f);

        private static Mesh _mesh;
        private static Material[] _mats;

        public static Material[] Materials()
        {
            if (_mats != null) return _mats;
            _mats = new[]
            {
                PaMesh.Mat("PaBoxBody", new Color(0.72f, 0.73f, 0.7f), 0.38f, 0.15f),
                PaMesh.Mat("PaBoxDark", new Color(0.05f, 0.05f, 0.055f), 0.25f),
                PaMesh.Mat("PaBoxSign", new Color(0.62f, 0.05f, 0.04f), 0.35f),
                PaMesh.Mat("PaBoxSteel", new Color(0.6f, 0.61f, 0.62f), 0.55f, 0.75f),
                PaMesh.Mat("PaBoxRubber", new Color(0.04f, 0.04f, 0.042f), 0.2f),
                PaMesh.Mat("PaBoxLamp", new Color(0.25f, 0.25f, 0.25f), 0.92f, 0f, true),
            };
            return _mats;
        }

        public static Mesh Mesh(float ceilingAbove)
        {
            if (_mesh != null) return _mesh;
            var m = new PaMesh(6);
            Vector3 R = Vector3.right, U = Vector3.up, F = Vector3.forward;

            // Mounting plate + screws.
            m.Pebble(PaMesh.RoundRect(0.125f, 0.17f, 0.012f, 3), PaMesh.SlabRings(0.001f, 0.007f, 0.0015f, 2), Vector3.zero, R, U, F, SubSteel);
            foreach (float sx in new[] { -0.108f, 0.108f })
            foreach (float sy in new[] { -0.152f, 0.152f })
                Screw(m, new Vector3(sx, sy, 0.007f));

            // Box (door 4 mm proud, seam around it).
            m.Pebble(PaMesh.RoundRect(0.105f, 0.15f, 0.014f, 4), PaMesh.SlabRings(0.007f, 0.088f, 0.008f, 3), Vector3.zero, R, U, F, SubBody);
            m.Pebble(PaMesh.RoundRect(0.094f, 0.139f, 0.008f, 3), PaMesh.SlabRings(0.086f, 0.0915f, 0.0015f, 2), Vector3.zero, R, U, F, SubBody);
            const float hw = 0.0955f, hh = 0.1405f, sz = 0.0885f;
            m.Box(new Vector3(0f, hh, sz), new Vector3(hw * 2f, 0.0018f, 0.004f), Quaternion.identity, SubDark);
            m.Box(new Vector3(0f, -hh, sz), new Vector3(hw * 2f, 0.0018f, 0.004f), Quaternion.identity, SubDark);
            m.Box(new Vector3(hw, 0f, sz), new Vector3(0.0018f, hh * 2f, 0.004f), Quaternion.identity, SubDark);
            m.Box(new Vector3(-hw, 0f, sz), new Vector3(0.0018f, hh * 2f, 0.004f), Quaternion.identity, SubDark);
            // Hinges on the left edge (seen from the front: +X), lock on the right.
            foreach (float hy in new[] { -0.095f, 0.095f })
                m.Lathe(new Vector3(0.1f, hy - 0.016f, 0.09f), U, new[] { new Vector2(0f, 0f), new Vector2(0.0048f, 0f), new Vector2(0.0048f, 0.032f), new Vector2(0f, 0.032f) }, 10, SubSteel);
            m.Lathe(new Vector3(-0.075f, -0.01f, 0.0915f), F, new[] { new Vector2(0f, 0f), new Vector2(0.0105f, 0f), new Vector2(0.0105f, 0.004f), new Vector2(0.0085f, 0.0062f), new Vector2(0f, 0.0062f) }, 16, SubSteel);
            m.Box(new Vector3(-0.075f, -0.01f, 0.098f), new Vector3(0.0028f, 0.012f, 0.0016f), Quaternion.identity, SubDark);

            // Red sign plate (the text is a separate TextMesh).
            m.Pebble(PaMesh.RoundRect(0.078f, 0.017f, 0.003f, 2), PaMesh.SlabRings(0.091f, 0.0935f, 0.0008f, 1), new Vector3(0f, 0.108f, 0f), R, U, F, SubRed);

            // Status lamp: chrome bezel + domed lens.
            m.Lathe(new Vector3(LampLocal.x, LampLocal.y, 0.0915f), F, new[] { new Vector2(0.0145f, 0f), new Vector2(0.0145f, 0.003f), new Vector2(0.0125f, 0.0055f), new Vector2(0.0098f, 0.0058f) }, 20, SubSteel);
            m.Lathe(new Vector3(LampLocal.x, LampLocal.y, 0.0965f), F, new[] { new Vector2(0.0099f, 0f), new Vector2(0.0092f, 0.0022f), new Vector2(0.0065f, 0.0045f), new Vector2(0f, 0.0055f) }, 20, SubLamp);

            // Volume knob (ribbed) with a white pointer.
            var knob = new System.Collections.Generic.List<Vector2> { new(0f, 0f), new(0.0135f, 0f) };
            knob.Add(new Vector2(0.0135f, 0.011f));
            knob.Add(new Vector2(0.011f, 0.0145f));
            knob.Add(new Vector2(0f, 0.0148f));
            m.Lathe(new Vector3(0.05f, 0.05f, 0.0915f), F, knob, 18, SubRubber);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var rot = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                m.Box(new Vector3(0.05f, 0.05f, 0.0915f + 0.0055f) + rot * new Vector3(0.0136f, 0f, 0f), new Vector3(0.0016f, 0.0026f, 0.009f), rot, SubRubber);
            }
            m.Box(new Vector3(0.05f, 0.056f, 0.1066f), new Vector3(0.0018f, 0.008f, 0.0008f), Quaternion.identity, SubBody);
            // Tick marks around the knob.
            for (int i = 0; i < 7; i++)
            {
                float a = (-135f + 45f * i) * Mathf.Deg2Rad;
                Vector3 c = new Vector3(0.05f + Mathf.Sin(a) * 0.021f, 0.05f + Mathf.Cos(a) * 0.021f, 0.0925f);
                m.Box(c, new Vector3(0.0012f, 0.0035f, 0.0008f), Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg), SubDark);
            }

            // Monitor speaker vent slots.
            for (int i = 0; i < 6; i++)
                m.Box(new Vector3(0f, -0.045f - i * 0.011f, 0.0918f), new Vector3(0.1f, 0.0045f, 0.0012f), Quaternion.identity, SubDark);

            // Cord gland under the box: hex nut + rubber bush.
            Vector3 g = new Vector3(PaLayout.GlandLocal.x, -0.15f, PaLayout.GlandLocal.z);
            m.Lathe(g, Vector3.down, new[] { new Vector2(0.0125f, -0.002f), new Vector2(0.0125f, 0.008f), new Vector2(0f, 0.0085f) }, 6, SubSteel);
            m.Lathe(g + Vector3.down * 0.008f, Vector3.down, new[] { new Vector2(0.0085f, 0f), new Vector2(0.0078f, 0.008f), new Vector2(0.0062f, 0.018f), new Vector2(0.0045f, 0.025f), new Vector2(0f, 0.0252f) }, 12, SubRubber);

            // Conduit from the top of the box up the wall into the ceiling, with saddle clamps.
            Vector3 top = new Vector3(0.03f, 0.15f, 0.045f);
            m.Lathe(top, U, new[] { new Vector2(0.016f, -0.002f), new Vector2(0.016f, 0.012f), new Vector2(0.012f, 0.016f), new Vector2(0f, 0.016f) }, 6, SubSteel);
            var path = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                float y = Mathf.Lerp(top.y + 0.014f, top.y + 0.1f, t);
                float z = Mathf.Lerp(0.045f, 0.014f, Mathf.SmoothStep(0f, 1f, t));
                path.Add(new Vector3(top.x, y, z));
            }
            path.Add(new Vector3(top.x, ceilingAbove + 0.01f, 0.014f));
            m.Tube(path, 0.0105f, 10, SubSteel);
            for (float y = 0.42f; y < ceilingAbove - 0.15f; y += 0.42f)
            {
                m.Box(new Vector3(top.x, y, 0.0135f), new Vector3(0.026f, 0.012f, 0.025f), Quaternion.identity, SubSteel);
                m.Box(new Vector3(top.x, y, 0.002f), new Vector3(0.058f, 0.012f, 0.003f), Quaternion.identity, SubSteel);
            }

            // Mic hook right of the box: wall plate, stand-off tongue with a slot open at the top.
            Vector3 h = new Vector3(PaLayout.MicHangLocal.x, -0.04f, 0f);
            m.Pebble(PaMesh.RoundRect(0.026f, 0.046f, 0.006f, 2), PaMesh.SlabRings(0.001f, 0.0045f, 0.001f, 1), h, R, U, F, SubSteel);
            Screw(m, h + new Vector3(0f, 0.032f, 0.0045f));
            Screw(m, h + new Vector3(0f, -0.034f, 0.0045f));
            float slotBottom = PaLayout.MicHangLocal.y + PaMicModel.HangLocal.y;
            // tongue: two prongs + bottom bar, 14-17 mm off the wall
            foreach (float s in new[] { -1f, 1f })
                m.Box(new Vector3(h.x + s * 0.0128f, slotBottom + 0.006f, 0.0155f), new Vector3(0.0166f, 0.034f, 0.003f), Quaternion.identity, SubSteel);
            m.Box(new Vector3(h.x, slotBottom - 0.0085f, 0.0155f), new Vector3(0.0422f, 0.009f, 0.003f), Quaternion.identity, SubSteel);
            // bent foot joining the tongue to the wall plate
            m.Box(new Vector3(h.x, slotBottom - 0.0115f, 0.0095f), new Vector3(0.0422f, 0.003f, 0.012f), Quaternion.identity, SubSteel);
            // rounded prong tips
            foreach (float s in new[] { -1f, 1f })
                m.Lathe(new Vector3(h.x + s * 0.0128f, slotBottom + 0.023f, 0.014f), F, new[] { new Vector2(0f, 0f), new Vector2(0.0083f, 0f), new Vector2(0.0083f, 0.003f), new Vector2(0f, 0.003f) }, 10, SubSteel, false, 180f, 360f);

            _mesh = m.ToMesh("PaStation");
            return _mesh;
        }

        private static void Screw(PaMesh m, Vector3 at)
        {
            m.Lathe(at, Vector3.forward, new[] { new Vector2(0.0058f, 0f), new Vector2(0.0058f, 0.0018f), new Vector2(0.0045f, 0.0032f), new Vector2(0f, 0.0035f) }, 10, SubSteel);
            m.Box(at + new Vector3(0f, 0f, 0.0033f), new Vector3(0.0075f, 0.0012f, 0.0008f), Quaternion.Euler(0f, 0f, 35f), SubDark);
        }
    }
}
