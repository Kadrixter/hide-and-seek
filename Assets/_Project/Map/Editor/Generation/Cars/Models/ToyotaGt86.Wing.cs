using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// The GT86's absurd time-attack wing: a gloss black cambered blade wider than the car, standing above the roof
    /// line behind the tail, with big swept end plates and a gurney flap, carried by two tall bare-aluminium stanchion
    /// plates (lightening holes, bolts) that come up from brackets bolted to the bottom of the rear bumper, clear of the
    /// boot lid, lamps and plate.
    /// </summary>
    internal sealed partial class ToyotaGt86
    {
        private const float WingX = 0.37f, WingThick = 0.012f, WingY = 1.52f, WingZ = -2.21f, WingHalf = 1.0f, WingChord = 0.37f;

        private void GtWing()
        {
            var alu = Lib["Car_RimSilver"];
            var black = Lib["Car_WingBlack"];
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * WingX;
                // stanchion: leans back a little, bottom edge on the bumper bracket, top under the blade
                CarWings.Plate(Mb, new[]
                {
                    new Vector2(-2.134f, 0.27f), new Vector2(-2.134f, 0.45f), new Vector2(-2.166f, 1.47f),
                    new Vector2(-2.256f, 1.47f), new Vector2(-2.222f, 0.27f),
                }, x, WingThick, alu);
                foreach (float y in new[] { 0.62f, 0.86f, 1.1f, 1.33f })
                {
                    float t = (y - 0.45f) / 1.02f;
                    float zc = (Mathf.Lerp(-2.134f, -2.166f, t) + Mathf.Lerp(-2.222f, -2.256f, (y - 0.27f) / 1.2f)) * 0.5f;
                    CarWings.Hole(Mb, x, zc, y, 0.022f, WingThick);
                }
                // bracket bolted to the bumper under the plate, and the clamp block under the blade
                Mb.Box(new Vector3(x, 0.315f, -2.13f), new Vector3(0.085f, 0.1f, 0.008f), alu);
                Mb.Box(new Vector3(x - s * 0.018f, 0.315f, -2.175f), new Vector3(0.024f, 0.1f, 0.09f), alu);
                foreach (float y in new[] { 0.29f, 0.34f })
                    CarWings.Bolt(Mb, new Vector3(x + s * 0.03f, y, -2.134f), Vector3.back);
                foreach (float z in new[] { -2.18f, -2.235f })
                    CarWings.Bolt(Mb, new Vector3(x + s * WingThick * 0.5f, 0.3f, z), Vector3.right * s);
                Mb.Box(new Vector3(x, WingY - 0.035f, WingZ), new Vector3(0.04f, 0.035f, 0.14f), alu);
                foreach (float z in new[] { WingZ + 0.04f, WingZ - 0.04f })
                    CarWings.Bolt(Mb, new Vector3(x + s * 0.02f, WingY - 0.035f, z), Vector3.right * s, 0.007f);
                // swept end plate
                CarWings.Plate(Mb, new[]
                {
                    new Vector2(-1.99f, 1.44f), new Vector2(-2.07f, 1.64f), new Vector2(-2.47f, 1.68f),
                    new Vector2(-2.49f, 1.42f), new Vector2(-2.18f, 1.35f),
                }, s * (WingHalf + 0.004f), 0.006f, black);
            }
            CarWings.Airfoil(Mb, new Vector3(0f, WingY, WingZ), WingHalf, WingChord, 0.12f, 8f, black, 0.05f);
            // gurney flap along the trailing edge
            Vector3 te = new Vector3(0f, WingY, WingZ) + Quaternion.Euler(8f, 0f, 0f) * new Vector3(0f, 0f, -WingChord * 0.5f);
            Mb.Box(te + new Vector3(0f, 0.012f, 0.002f), new Vector3(WingHalf * 2f, 0.026f, 0.004f), black);
        }

        /// <summary>Body and greenhouse boxes plus the wing blade and both stanchions.</summary>
        protected override void Colliders()
        {
            base.Colliders();
            Pm.Colliders.Add(new Bounds(new Vector3(0f, WingY + 0.01f, WingZ - 0.03f), new Vector3(WingHalf * 2f + 0.01f, 0.26f, 0.5f)));
            foreach (float s in new[] { -1f, 1f })
                Pm.Colliders.Add(new Bounds(new Vector3(s * WingX, 0.87f, -2.195f), new Vector3(0.04f, 1.2f, 0.12f)));
        }
    }
}
