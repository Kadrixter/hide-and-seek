using UnityEngine;

namespace HideAndSeek.Characters
{
    public static partial class YarisModel
    {
        /// <summary>
        /// Wheel (origin = hub centre, axle along X, outer face towards +X * side): rounded tyre filling the arch, steel
        /// rim with the Luna's silver 15" wheel cover (ten slots, emblem in the middle).
        /// </summary>
        static Mesh BuildWheel(float side)
        {
            var m = new YarisMesh(Slots);
            float r = WheelR, hw = WheelW * 0.5f;
            const int seg = 24;
            // Tyre: inner sidewall -> shoulder -> tread -> shoulder -> outer sidewall (bulges a little).
            m.Lathe(new[]
            {
                new Vector2(-hw, 0.0235f), new Vector2(-hw - 0.0006f, 0.028f), new Vector2(-hw + 0.0004f, 0.0328f),
                new Vector2(-hw + 0.003f, r - 0.0003f), new Vector2(-hw + 0.0055f, r),
                new Vector2(hw - 0.0055f, r), new Vector2(hw - 0.003f, r - 0.0003f),
                new Vector2(hw - 0.0004f, 0.0328f), new Vector2(hw + 0.0006f, 0.028f), new Vector2(hw, 0.0235f),
            }, seg, MTyre, Vector3.zero, side);
            // Tread grooves: two dark rings in the tread.
            foreach (float gx in new[] { -0.0045f, 0.0045f })
                m.Lathe(new[] { new Vector2(gx - 0.0007f, r + 0.00012f), new Vector2(gx + 0.0007f, r + 0.00012f) }, seg, MUnder, Vector3.zero, side, false);
            // Rim lip (bright edge), barrel (seen through the spokes), brake disc and hub face.
            m.Lathe(new[]
            {
                new Vector2(hw - 0.0001f, 0.0238f), new Vector2(hw + 0.0003f, 0.0232f), new Vector2(hw - 0.0004f, 0.0224f),
                new Vector2(hw - 0.0035f, 0.0218f),
            }, seg, MRim, Vector3.zero, side);
            m.Lathe(new[] { new Vector2(hw - 0.0035f, 0.0218f), new Vector2(-hw + 0.002f, 0.0218f) }, seg, MRim, Vector3.zero, side, false);
            m.Lathe(new[] { new Vector2(-0.003f, 0.0208f), new Vector2(-0.003f, 0.0045f) }, seg, MUnder, Vector3.zero, side, false);
            m.Lathe(new[] { new Vector2(-0.0026f, 0.016f), new Vector2(-0.0026f, 0.0065f) }, seg, MSeam, Vector3.zero, side, false);
            // 15" wheel cover (Luna): silver dish over the steel rim, ten slots round a raised ring, oval emblem cap.
            m.Lathe(new[]
            {
                new Vector2(hw - 0.0032f, 0.0221f), new Vector2(hw - 0.0018f, 0.0212f), new Vector2(hw - 0.0012f, 0.0175f),
                new Vector2(hw - 0.0011f, 0.0104f), new Vector2(hw - 0.0004f, 0.0092f), new Vector2(hw - 0.0002f, 0.0062f),
                new Vector2(hw + 0.0002f, 0.0052f), new Vector2(hw + 0.0003f, 0.0001f),
            }, seg, MRim, Vector3.zero, side);
            for (int k = 0; k < 10; k++)
            {
                // slot: dark window through the cover, slightly twisted like the real cover's turbine slots
                var rot = Quaternion.Euler(k * 36f, 0f, 0f) * Quaternion.Euler(14f, 0f, 0f);
                m.Box(rot * new Vector3(side * (hw - 0.0013f), 0.0142f, 0f), new Vector3(0.0008f, 0.0074f, 0.0021f), rot, MUnder, 0.2f);
            }
            m.Lathe(new[] { new Vector2(hw - 0.00085f, 0.0205f), new Vector2(hw - 0.00085f, 0.0197f) }, seg, MSeam, Vector3.zero, side, false);
            m.Lathe(new[] { new Vector2(hw + 0.0005f, 0.0034f), new Vector2(hw + 0.0005f, 0.0001f) }, 12, MChrome, Vector3.zero, side, false);
            return m.ToMesh(side > 0f ? "Yaris_WheelR" : "Yaris_WheelL");
        }
    }
}
