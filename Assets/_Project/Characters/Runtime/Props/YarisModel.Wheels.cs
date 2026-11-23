using UnityEngine;

namespace HideAndSeek.Characters
{
    public static partial class YarisModel
    {
        /// <summary>
        /// Wheel (origin = hub centre, axle along X, outer face towards +X * side): rounded tyre, silver 5-spoke alloy
        /// with a deep barrel, dark brake disc behind the spokes, chrome centre cap with the oval logo and five lug nuts.
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
                new Vector2(hw - 0.0001f, 0.0236f), new Vector2(hw + 0.0004f, 0.0226f), new Vector2(hw - 0.0003f, 0.0214f),
                new Vector2(hw - 0.0035f, 0.0208f),
            }, seg, MRim, Vector3.zero, side);
            m.Lathe(new[] { new Vector2(hw - 0.0035f, 0.0208f), new Vector2(-hw + 0.002f, 0.0208f) }, seg, MRim, Vector3.zero, side, false);
            m.Lathe(new[] { new Vector2(-0.003f, 0.0208f), new Vector2(-0.003f, 0.0045f) }, seg, MUnder, Vector3.zero, side, false);
            m.Lathe(new[] { new Vector2(-0.0026f, 0.016f), new Vector2(-0.0026f, 0.0065f) }, seg, MSeam, Vector3.zero, side, false);
            // Centre cap.
            m.Lathe(new[]
            {
                new Vector2(hw - 0.0045f, 0.0066f), new Vector2(hw - 0.0022f, 0.0062f), new Vector2(hw - 0.0012f, 0.0045f),
                new Vector2(hw - 0.0009f, 0.0001f),
            }, 16, MRim, Vector3.zero, side);
            m.Lathe(new[] { new Vector2(hw - 0.0007f, 0.0026f), new Vector2(hw - 0.0007f, 0.0001f) }, 12, MChrome, Vector3.zero, side, false);

            // Five slightly twisted double spokes from the hub ring to the rim lip, dished outwards.
            for (int k = 0; k < 5; k++)
            {
                float ang = k * 72f;
                foreach (float off in new[] { -9f, 9f })
                {
                    var rot = Quaternion.Euler(ang + off, 0f, 0f);
                    float r0 = 0.0062f, r1 = 0.0212f, len = r1 - r0;
                    // dish: the spoke leans outwards towards the hub
                    var lean = Quaternion.Euler(0f, 0f, side * 9f);
                    Vector3 c = rot * new Vector3(side * (hw - 0.0042f), (r0 + r1) * 0.5f, 0f);
                    m.Box(c, new Vector3(0.0028f, len, 0.0024f), rot * lean, MRim, 0.55f);
                }
                // lug nut between two spoke pairs
                var nr = Quaternion.Euler(ang + 36f, 0f, 0f);
                m.Box(nr * new Vector3(side * (hw - 0.0028f), 0.0046f, 0f), new Vector3(0.0016f, 0.0014f, 0.0014f), nr, MChrome, 0.6f);
            }
            // Hub ring the spokes start from.
            m.Lathe(new[] { new Vector2(hw - 0.0048f, 0.0072f), new Vector2(hw - 0.0036f, 0.0074f), new Vector2(hw - 0.0028f, 0.0066f) }, 20, MRim, Vector3.zero, side);
            return m.ToMesh(side > 0f ? "Yaris_WheelR" : "Yaris_WheelL");
        }
    }
}
