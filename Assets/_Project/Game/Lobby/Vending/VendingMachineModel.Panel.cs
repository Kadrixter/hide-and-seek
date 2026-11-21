using UnityEngine;

namespace HideAndSeek.Game
{
    public sealed partial class VendingMachineModel
    {
        /// <summary>Where the player aims to buy (middle of the keypad).</summary>
        public static readonly Vector3 KeypadLocal = new Vector3(-0.41f, 1.27f, 0.325f);
        /// <summary>Bought can resting in the tray (lying across, in front of the flap).</summary>
        public static readonly Vector3 TrayCanLocal = new Vector3(-0.5f, BinY, 0.04f);
        /// <summary>Behind the flap, where the can rolls out from.</summary>
        public static readonly Vector3 ChuteLocal = new Vector3(-0.33f, BinY, 0.04f);

        Material keyCap, ledGreen, ledRed;

        void BuildPanel()
        {
            var t = paint;
            keyCap = Mat(t, new Color(0.72f, 0.74f, 0.77f), 0.6f, 0.7f);
            ledGreen = Glow(t, new Color(0.2f, 1f, 0.35f), null, 2f);
            ledRed = Glow(t, new Color(1f, 0.15f, 0.1f), null, 1.6f);
            const float z = 0.325f, fx = -0.4f;
            // Column panel (red), framed in charcoal.
            Rb("PanelFace", new Vector3(-0.375f, 1.08f, z), new Vector3(0.05f, 1.28f, 0.27f), 0.012f, paint);
            Rb("PanelLower", new Vector3(-0.375f, 0.27f, z), new Vector3(0.05f, 0.34f, 0.27f), 0.012f, paint);
            Rb("PanelSeam", new Vector3(-0.401f, 0.442f, z), new Vector3(0.004f, 0.006f, 0.25f), 0.002f, black);
            Rb("PanelTrimL", new Vector3(-0.39f, 1.08f, 0.19f), new Vector3(0.03f, 1.28f, 0.012f), 0.004f, charcoal);
            Rb("PanelTrimR", new Vector3(-0.39f, 1.08f, 0.46f), new Vector3(0.03f, 1.28f, 0.012f), 0.004f, charcoal);
            // Display in a black bezel.
            Rb("DisplayBezel", new Vector3(fx, 1.5f, z), new Vector3(0.012f, 0.075f, 0.21f), 0.006f, black);
            Quad("Display", new Vector3(fx - 0.0065f, 1.5f, z), new Vector2(0.18f, 0.048f), FaceFront, displayMat);
            // Keypad 4 x 3 on a dark plate; each key is its own transform so it can be pressed.
            Rb("KeypadPlate", new Vector3(fx, 1.27f, z), new Vector3(0.012f, 0.25f, 0.2f), 0.008f, charcoal);
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 3; col++)
            {
                var k = Rb("Key", new Vector3(fx - 0.011f, 1.357f - row * 0.058f, z - 0.06f + col * 0.06f), new Vector3(0.014f, 0.046f, 0.046f), 0.007f, keyCap);
                Rb("KeyRing", new Vector3(fx - 0.0065f, 1.357f - row * 0.058f, z - 0.06f + col * 0.06f), new Vector3(0.004f, 0.052f, 0.052f), 0.004f, black);
                Keys[row * 3 + col] = k;
            }
            // Coin slot with a chrome bezel, coin return button.
            Rb("CoinPlate", new Vector3(fx - 0.004f, 1.07f, z - 0.035f), new Vector3(0.012f, 0.1f, 0.065f), 0.01f, chrome);
            Rb("CoinSlit", new Vector3(fx - 0.011f, 1.075f, z - 0.035f), new Vector3(0.003f, 0.05f, 0.007f), 0.001f, black);
            Cyl("CoinReturn", new Vector3(fx - 0.008f, 1.07f, z + 0.06f), 0.034f, 0.014f, AlongX, chrome);
            Cyl("CoinReturnCap", new Vector3(fx - 0.016f, 1.07f, z + 0.06f), 0.024f, 0.006f, AlongX, black);
            // Note acceptor: black mouth with a green guide light.
            Rb("NoteAcceptor", new Vector3(fx - 0.006f, 0.94f, z), new Vector3(0.025f, 0.075f, 0.19f), 0.01f, black);
            Rb("NoteSlit", new Vector3(fx - 0.019f, 0.945f, z), new Vector3(0.003f, 0.008f, 0.15f), 0.001f, rubber);
            Rb("NoteLed", new Vector3(fx - 0.019f, 0.925f, z), new Vector3(0.003f, 0.005f, 0.14f), 0.001f, ledGreen);
            // Contactless card reader with status LEDs.
            Rb("Reader", new Vector3(fx - 0.004f, 0.8f, z), new Vector3(0.016f, 0.1f, 0.1f), 0.01f, black);
            Quad("ReaderIcon", new Vector3(fx - 0.0125f, 0.805f, z), new Vector2(0.07f, 0.07f), FaceFront, readerMat);
            for (int i = 0; i < 4; i++)
                Rb("ReaderLed", new Vector3(fx - 0.013f, 0.758f, z - 0.024f + i * 0.016f), new Vector3(0.003f, 0.006f, 0.01f), 0.001f, i == 0 ? ledGreen : black);
            // Speaker grille.
            Rb("Speaker", new Vector3(fx, 0.67f, z), new Vector3(0.01f, 0.07f, 0.12f), 0.008f, charcoal);
            for (int a = 0; a < 3; a++)
            for (int b = 0; b < 7; b++)
                Rb("SpeakerHole", new Vector3(fx - 0.0055f, 0.65f + a * 0.02f, z - 0.045f + b * 0.015f), new Vector3(0.002f, 0.007f, 0.007f), 0.0015f, black);
            // Coin return cup: chrome frame around a dark recess, with a coin left inside.
            Rb("CupFrame", new Vector3(fx - 0.004f, 0.53f, z), new Vector3(0.02f, 0.11f, 0.12f), 0.012f, chrome);
            Rb("CupHole", new Vector3(fx - 0.0105f, 0.525f, z), new Vector3(0.009f, 0.08f, 0.09f), 0.008f, rubber);
            Cyl("LeftCoin", new Vector3(fx - 0.012f, 0.495f, z + 0.02f), 0.022f, 0.003f, Quaternion.Euler(0f, 0f, 82f), chrome);
            // Service lock and a small power LED on the side of the column.
            Cyl("ServiceLock", new Vector3(fx - 0.004f, 1.62f, z + 0.08f), 0.022f, 0.012f, AlongX, chrome);
            Rb("PowerLed", new Vector3(fx - 0.004f, 1.62f, z - 0.09f), new Vector3(0.004f, 0.008f, 0.008f), 0.002f, ledRed);
        }

        void BuildBin()
        {
            // Front panel around the delivery opening (opening z [-0.39, 0.13], y [0.17, 0.37]).
            Rb("BinBottom", new Vector3(-0.375f, 0.135f, -0.15f), new Vector3(0.05f, 0.07f, 0.67f), 0.01f, paint);
            Rb("BinTop", new Vector3(-0.375f, 0.395f, -0.15f), new Vector3(0.05f, 0.05f, 0.67f), 0.01f, paint);
            Rb("BinSideL", new Vector3(-0.375f, 0.27f, 0.157f), new Vector3(0.05f, 0.2f, 0.055f), 0.01f, paint);
            Rb("BinSideR", new Vector3(-0.375f, 0.27f, -0.437f), new Vector3(0.05f, 0.2f, 0.055f), 0.01f, paint);
            Rb("BinRecess", new Vector3(-0.3f, 0.27f, -0.13f), new Vector3(0.12f, 0.2f, 0.52f), 0.004f, black);
            Rb("BinFrameTop", new Vector3(-0.402f, 0.372f, -0.13f), new Vector3(0.006f, 0.008f, 0.53f), 0.003f, chrome);
            Rb("BinFrameBot", new Vector3(-0.402f, 0.168f, -0.13f), new Vector3(0.006f, 0.008f, 0.53f), 0.003f, chrome);
            // Flap: dark smoked plastic hinged at the top, with a chrome push bar.
            Flap = new GameObject("FlapHinge") { layer = layer }.transform;
            Flap.SetParent(transform, false);
            Flap.localPosition = new Vector3(-0.404f, 0.366f, -0.13f);
            Rb("Flap", new Vector3(0f, -0.097f, 0f), new Vector3(0.007f, 0.19f, 0.51f), 0.003f, black, Flap);
            Rb("FlapBar", new Vector3(-0.007f, -0.16f, 0f), new Vector3(0.01f, 0.018f, 0.24f), 0.005f, chrome, Flap);
            // Tray ledge in front: floor, cheeks and a lip so the can does not roll off.
            Rb("TrayFloor", new Vector3(-0.49f, 0.152f, -0.13f), new Vector3(0.18f, 0.02f, 0.56f), 0.006f, charcoal);
            Rb("TrayCheekL", new Vector3(-0.49f, 0.195f, 0.15f), new Vector3(0.18f, 0.07f, 0.012f), 0.004f, charcoal);
            Rb("TrayCheekR", new Vector3(-0.49f, 0.195f, -0.41f), new Vector3(0.18f, 0.07f, 0.012f), 0.004f, charcoal);
            Rb("TrayLip", new Vector3(-0.577f, 0.18f, -0.13f), new Vector3(0.012f, 0.04f, 0.56f), 0.004f, chrome);
            for (int i = 0; i < 6; i++)
                Rb("TrayRib", new Vector3(-0.49f, 0.1635f, -0.36f + i * 0.09f), new Vector3(0.16f, 0.003f, 0.01f), 0.0015f, black);
        }
    }
}
