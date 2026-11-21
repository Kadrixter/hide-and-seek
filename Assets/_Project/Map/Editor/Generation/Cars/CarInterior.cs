using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Cabin furniture seen through the glass: seats, steering wheel, dashboard, console.</summary>
    internal static class CarInterior
    {
        /// <summary>Front seat; <paramref name="p"/> = floor point under the cushion centre, seat faces +z.</summary>
        public static void Seat(MeshBuilder mb, Vector3 p, float width, float cushionY, Material cover, Material side, bool sporty,
            float recline = 16f, float backH = 0.62f)
        {
            var lib = mb.Lib;
            float depth = 0.5f;
            var tilt = Quaternion.Euler(-6f, 0, 0);
            Vector3 cc = new Vector3(p.x, cushionY, p.z);
            mb.Box(new Vector3(p.x, (p.y + cushionY) * 0.5f - 0.03f, p.z - 0.02f), new Vector3(width * 0.6f, cushionY - p.y - 0.06f, depth * 0.6f), lib["Car_Dash"]);
            CarBlob.Blob(mb, cc, tilt, new Vector3(width * 0.5f, 0.065f, depth * 0.5f), 0.3f, cover, 0.3f);
            var back = Quaternion.Euler(-recline, 0, 0);
            Vector3 bc = cc + new Vector3(0, 0.03f, -depth * 0.5f + 0.02f) + back * new Vector3(0, backH * 0.5f, -0.02f);
            CarBlob.Blob(mb, bc, back, new Vector3(width * 0.48f, backH * 0.5f, 0.065f), 0.3f, cover, 0.35f);
            if (sporty)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    CarBlob.Blob(mb, cc + tilt * new Vector3(s * (width * 0.5f - 0.035f), 0.06f, 0.02f), tilt, new Vector3(0.045f, 0.06f, depth * 0.46f), 0.5f, side, 0.35f);
                    CarBlob.Blob(mb, bc + back * new Vector3(s * (width * 0.5f - 0.03f), -0.05f, 0.05f), back, new Vector3(0.045f, backH * 0.38f, 0.06f), 0.5f, side, 0.4f);
                }
                Vector3 hc = bc + back * new Vector3(0, backH * 0.5f + 0.1f, 0);
                CarBlob.Blob(mb, hc, back, new Vector3(width * 0.3f, 0.1f, 0.055f), 0.4f, cover, 0.4f);
                mb.Box(hc + back * new Vector3(0, -0.015f, 0.054f), new Vector3(width * 0.22f, 0.035f, 0.004f), lib["Car_Seam"], back);
            }
            else
            {
                Vector3 hc = bc + back * new Vector3(0, backH * 0.5f + 0.13f, 0);
                CarBlob.Blob(mb, hc, back, new Vector3(width * 0.27f, 0.085f, 0.05f), 0.45f, cover, 0.45f);
                foreach (float s in new[] { -1f, 1f })
                    mb.Cylinder(bc + back * new Vector3(s * width * 0.12f, backH * 0.5f - 0.02f, 0), bc + back * new Vector3(s * width * 0.12f, backH * 0.5f + 0.06f, 0), 0.006f, 6, lib["Car_ChromeBright"]);
            }
        }

        /// <summary>Rear bench across the cabin; <paramref name="z"/> = cushion centre.</summary>
        public static void RearBench(MeshBuilder mb, float halfW, float floorY, float cushionY, float z, Material cover,
            float backH = 0.55f, float recline = 22f)
        {
            mb.Box(new Vector3(0, (floorY + cushionY) * 0.5f, z), new Vector3(halfW * 2f, cushionY - floorY + 0.05f, 0.48f), cover);
            var back = Quaternion.Euler(-recline, 0, 0);
            mb.Box(new Vector3(0, cushionY, z - 0.26f) + back * new Vector3(0, backH * 0.5f, 0), new Vector3(halfW * 2f, backH, 0.12f), cover, back);
            foreach (float s in new[] { -1f, 1f })
                mb.Box(new Vector3(s * halfW * 0.5f, cushionY + 0.002f, z + 0.02f), new Vector3(0.008f, 0.06f, 0.42f), mb.Lib["Car_Seam"]);
        }

        /// <summary>Steering wheel centred at c, facing the driver along -axis (axis points to the dash).</summary>
        public static void SteeringWheel(MeshBuilder mb, Vector3 c, Vector3 axis, float r, Material rim, int spokes = 3)
        {
            var lib = mb.Lib;
            axis.Normalize();
            var prof = new Vector2[13];
            for (int i = 0; i <= 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                prof[i] = new Vector2(r + Mathf.Cos(a) * 0.017f, Mathf.Sin(a) * 0.017f);
            }
            CarWheels.Lathe(mb, c, axis, prof, 28, rim, 80f);
            Vector3 up = Vector3.Cross(axis, Vector3.Cross(Vector3.up, axis)).normalized;
            Vector3 right = Vector3.Cross(up, axis).normalized;
            mb.Cylinder(c + axis * 0.02f, c + axis * 0.3f, 0.03f, 10, lib["Car_Dash"]);
            mb.Cylinder(c - axis * 0.025f, c + axis * 0.03f, 0.075f, 16, lib["Car_LeatherBlack"]);
            for (int s = 0; s < spokes; s++)
            {
                float a = spokes == 3 ? new[] { 0f, Mathf.PI, -Mathf.PI * 0.5f }[s] : s * Mathf.PI * 2f / spokes;
                Vector3 d = Mathf.Cos(a) * right + Mathf.Sin(a) * up;
                Vector3 mid = c + d * (r * 0.55f) + axis * 0.012f;
                mb.Box(mid, new Vector3(0.05f, 0.016f, r * 0.85f), lib["Car_LeatherBlack"], Quaternion.LookRotation(d, -axis));
            }
        }

        /// <summary>
        /// Dashboard: main moulding across the car between zFront and zBack (top at yTop), driver binnacle with
        /// gauges at x = driverX, centre stack with vents and radio.
        /// </summary>
        public static void Dash(MeshBuilder mb, float halfW, float zFront, float zBack, float yTop, float yLow, float driverX,
            Material mat, bool radio = true)
        {
            var lib = mb.Lib;
            float d = zFront - zBack;
            mb.Box(new Vector3(0, (yTop + yLow) * 0.5f, zBack + d * 0.5f), new Vector3(halfW * 2f, yTop - yLow, d), mat);
            mb.Box(new Vector3(0, yTop - 0.06f, zBack - 0.05f), new Vector3(halfW * 2f - 0.04f, 0.12f, 0.1f), mat, Quaternion.Euler(25f, 0, 0));
            // binnacle hood and gauges
            mb.Box(new Vector3(driverX, yTop + 0.045f, zBack + 0.08f), new Vector3(0.42f, 0.09f, 0.22f), mat);
            mb.Box(new Vector3(driverX, yTop - 0.04f, zBack - 0.005f), new Vector3(0.38f, 0.13f, 0.02f), lib["Car_Gauge"]);
            foreach (float gx in new[] { -0.09f, 0.09f })
            {
                Vector3 gc = new Vector3(driverX + gx, yTop - 0.04f, zBack - 0.017f);
                CarWheels.Lathe(mb, gc, Vector3.back, new[] { new Vector2(0.055f, -0.002f), new Vector2(0.055f, 0.002f), new Vector2(0.05f, 0.002f) }, 18, lib["Car_ChromeBright"]);
                mb.Box(gc + new Vector3(0.015f, 0.012f, -0.002f), new Vector3(0.045f, 0.004f, 0.002f), lib["Car_Needle"], Quaternion.Euler(0, 0, 35f));
            }
            // centre stack
            mb.Box(new Vector3(0, yTop - 0.2f, zBack - 0.03f), new Vector3(0.24f, 0.3f, 0.08f), mat);
            foreach (float vx in new[] { -0.06f, 0.06f })
                mb.Box(new Vector3(vx, yTop - 0.09f, zBack - 0.072f), new Vector3(0.1f, 0.05f, 0.006f), lib["Car_Grille"]);
            if (radio)
            {
                mb.Box(new Vector3(0, yTop - 0.2f, zBack - 0.072f), new Vector3(0.18f, 0.05f, 0.006f), lib["Car_Gauge"]);
                mb.Box(new Vector3(0.02f, yTop - 0.2f, zBack - 0.0755f), new Vector3(0.06f, 0.012f, 0.002f), lib["Car_Needle"]);
            }
            // side vents at both ends
            foreach (float s in new[] { -1f, 1f })
                CarWheels.Lathe(mb, new Vector3(s * (halfW - 0.08f), yTop - 0.08f, zBack - 0.004f), Vector3.back,
                    new[] { new Vector2(0.045f, -0.004f), new Vector2(0.045f, 0.004f), new Vector2(0f, 0.004f) }, 14, lib["Car_Grille"]);
        }

        /// <summary>Centre tunnel console between the seats with gear lever and handbrake.</summary>
        public static void Console(MeshBuilder mb, float z0, float z1, float floorY, float h, Material mat, Material knob)
        {
            var lib = mb.Lib;
            mb.Box(new Vector3(0, floorY + h * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.2f, h, z1 - z0), mat);
            Vector3 g = new Vector3(0, floorY + h, z1 - 0.15f);
            mb.Box(g + new Vector3(0, 0.003f, 0), new Vector3(0.11f, 0.006f, 0.13f), lib["Car_LeatherBlack"]);
            mb.Cylinder(g, g + new Vector3(0, 0.13f, -0.02f), 0.008f, 8, lib["Car_ChromeBright"]);
            mb.Cylinder(g + new Vector3(0, 0.12f, -0.02f), g + new Vector3(0, 0.17f, -0.025f), 0.022f, 10, knob);
            mb.Box(new Vector3(0, floorY + h + 0.04f, z0 + 0.16f), new Vector3(0.03f, 0.03f, 0.22f), lib["Car_Dash"], Quaternion.Euler(-14f, 0, 0));
        }
    }
}
