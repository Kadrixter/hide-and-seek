using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Rim looks of the ordinary parked cars.</summary>
    internal enum ParkedRim { Alloy5, AlloyMulti, Hubcap, SteelBlack }

    /// <summary>
    /// Light wheel for the parked cars (~1/5 of the vertices of a hero wheel): rounded tyre, rim lip, dark barrel,
    /// brake disc and one of a few rim faces. Built in local space like <see cref="CarWheels.Wheel"/>.
    /// </summary>
    internal static class ParkedWheel
    {
        private const int Sides = 18;

        public static void Build(MeshBuilder mb, Vector3 c, float side, float r, float width, float rimR, ParkedRim style, Material rim)
        {
            var lib = mb.Lib;
            var dark = lib["Car_Well"];
            Vector3 ax = Vector3.right * side;
            float hw = width * 0.5f;
            CarWheels.Lathe(mb, c, ax, new[]
            {
                new Vector2(rimR - 0.004f, -hw + 0.015f), new Vector2(rimR + 0.035f, -hw - 0.004f), new Vector2(r - 0.045f, -hw - 0.006f),
                new Vector2(r - 0.008f, -hw + 0.02f), new Vector2(r, -hw + 0.045f), new Vector2(r, hw - 0.045f),
                new Vector2(r - 0.008f, hw - 0.02f), new Vector2(r - 0.045f, hw + 0.006f), new Vector2(rimR + 0.035f, hw + 0.004f),
                new Vector2(rimR - 0.004f, hw - 0.015f),
            }, Sides, lib["Rubber"], 35f);
            float face = hw - 0.04f;
            if (style == ParkedRim.Hubcap)
            {
                // plastic wheel cover: flat outer ring, a step, a slightly domed centre with a badge disc
                var cap = lib["Car_RimSilver"];
                CarWheels.Lathe(mb, c, ax, new[]
                {
                    new Vector2(rimR + 0.004f, hw - 0.03f), new Vector2(rimR + 0.006f, hw - 0.012f), new Vector2(rimR - 0.012f, hw - 0.004f),
                    new Vector2(rimR * 0.72f, hw - 0.01f), new Vector2(rimR * 0.66f, hw - 0.018f), new Vector2(rimR * 0.3f, hw - 0.012f),
                    new Vector2(0f, hw - 0.008f),
                }, Sides, cap, 30f);
                // vent slots in the cover
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    Vector3 d = Mathf.Cos(a) * Vector3.up + Mathf.Sin(a) * Vector3.forward;
                    var rot = Quaternion.LookRotation(Vector3.Cross(d, ax), ax);
                    mb.Box(c + d * (rimR * 0.82f) + ax * (hw - 0.0075f), new Vector3(0.006f, 0.002f, rimR * 0.16f), dark, rot);
                }
                CarWheels.Lathe(mb, c + ax * (hw - 0.0075f), ax, new[] { new Vector2(0.03f, 0f), new Vector2(0f, 0.001f) }, 12, lib["Car_Trim"]);
                return;
            }
            // rim barrel with the outer lip, dark inside, brake disc
            CarWheels.Lathe(mb, c, ax, new[]
            {
                new Vector2(rimR, -hw + 0.035f), new Vector2(rimR, hw - 0.03f), new Vector2(rimR + 0.008f, hw - 0.018f),
                new Vector2(rimR - 0.008f, hw - 0.01f), new Vector2(rimR - 0.024f, face),
            }, Sides, rim, 40f);
            CarWheels.Lathe(mb, c, ax, new[] { new Vector2(rimR - 0.02f, face - 0.1f), new Vector2(0f, face - 0.1f) }, 12, dark);
            CarWheels.Lathe(mb, c, ax, new[] { new Vector2(rimR * 0.78f, face - 0.055f), new Vector2(rimR * 0.2f, face - 0.055f) }, 14, lib["Metal_Dark"]);
            Vector3 e1 = Vector3.up, e2 = Vector3.Cross(ax, e1);
            float hub = 0.055f, rOut = rimR - 0.02f;
            CarWheels.Lathe(mb, c, ax, new[] { new Vector2(hub, face - 0.02f), new Vector2(hub, face + 0.004f), new Vector2(0f, face + 0.01f) }, 12, rim);
            if (style == ParkedRim.SteelBlack)
            {
                // pressed steel: flat dish with round holes and a nut ring
                CarWheels.Lathe(mb, c, ax, new[] { new Vector2(rOut, face - 0.004f), new Vector2(rOut * 0.75f, face - 0.004f), new Vector2(hub, face + 0.002f) }, Sides, rim);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI * 2f / 6f;
                    Vector3 d = Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
                    CarWheels.Lathe(mb, c + d * (rOut * 0.62f) + ax * (face - 0.001f), ax, new[] { new Vector2(rOut * 0.13f, 0f), new Vector2(0f, 0f) }, 8, dark);
                }
                return;
            }
            int n = style == ParkedRim.Alloy5 ? 5 : 7;
            float w = style == ParkedRim.Alloy5 ? 0.06f : 0.032f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 d = Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
                Vector3 p0 = c + d * (hub * 0.8f) + ax * (face + 0.002f);
                Vector3 p1 = c + d * rOut + ax * (face - 0.01f);
                var rot = Quaternion.LookRotation(p1 - p0, ax);
                mb.Box((p0 + p1) * 0.5f, new Vector3(w, 0.022f, (p1 - p0).magnitude), rim, rot);
                if (style == ParkedRim.AlloyMulti)
                {
                    // split spokes: a second, twisted one next to it
                    float b = a + 0.2f;
                    Vector3 q1 = c + (Mathf.Cos(b) * e1 + Mathf.Sin(b) * e2) * rOut + ax * (face - 0.01f);
                    mb.Box((p0 + q1) * 0.5f, new Vector3(w, 0.02f, (q1 - p0).magnitude), rim, Quaternion.LookRotation(q1 - p0, ax));
                }
            }
        }
    }
}
