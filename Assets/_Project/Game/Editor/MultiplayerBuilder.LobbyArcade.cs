using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// SNAKE arcade screen: tilted back like a real cabinet monitor, standing fully in front of the cabinet body
    /// (the old one was tilted forward and its lower edge went into the body), a dark bezel 4 mm behind the glass
    /// and a raised frame lip just outside the picture (nothing covers the screen, no coplanar faces).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        /// <summary>Screen centre relative to the cabinet origin (front faces -Z). Keep in sync with MatchManager.ArcadeScreen.</summary>
        public static readonly Vector3 ArcadeScreenLocal = new Vector3(0f, 1.35f, -0.36f);
        public const float ArcadeScreenTilt = 12f;
        public static readonly Vector2 ArcadeScreenSize = new Vector2(0.5f, 0.375f);

        private static GameObject ArcadeScreen(Transform a, Vector3 pos)
        {
            var rot = Quaternion.Euler(ArcadeScreenTilt, 0f, 0f);
            Vector3 c = pos + ArcadeScreenLocal;
            Vector3 up = rot * Vector3.up, right = rot * Vector3.right, back = rot * Vector3.forward; // back = into the cabinet
            Vector2 s = ArcadeScreenSize;

            var bezelMat = Mat("LobbyArcadeBezel", new Color(0.02f, 0.02f, 0.03f), null, 0.6f, Vector2.one);
            const float bezelDepth = 0.04f;
            Deco("Bezel", a, c + back * (0.004f + bezelDepth * 0.5f), new Vector3(s.x + 0.1f, s.y + 0.095f, bezelDepth), bezelMat, rot);

            // Raised lip around the picture (outside its edges, 6 mm in front of the glass).
            var lip = Mat("LobbyArcadeLip", new Color(0.08f, 0.08f, 0.1f), null, 0.75f, Vector2.one, 0.6f);
            const float w = 0.018f, d = 0.012f;
            Vector3 fwd = -back * (d * 0.5f + 0.002f);
            Deco("LipTop", a, c + up * (s.y * 0.5f + w * 0.5f + 0.002f) + fwd, new Vector3(s.x + 2f * w + 0.008f, w, d), lip, rot);
            Deco("LipBottom", a, c - up * (s.y * 0.5f + w * 0.5f + 0.002f) + fwd, new Vector3(s.x + 2f * w + 0.008f, w, d), lip, rot);
            Deco("LipLeft", a, c - right * (s.x * 0.5f + w * 0.5f + 0.002f) + fwd, new Vector3(w, s.y + 0.004f, d), lip, rot);
            Deco("LipRight", a, c + right * (s.x * 0.5f + w * 0.5f + 0.002f) + fwd, new Vector3(w, s.y + 0.004f, d), lip, rot);

            var screenMat = Unlit("LobbyArcadeScreen", null);
            return QuadAt("Screen", a, c, rot, s, screenMat);
        }

        // ================================================================ control panel

        /// <summary>Panel centre (cabinet space) and slope: rises 12 degrees towards the screen.</summary>
        public static readonly Vector3 ArcadePanelLocal = new Vector3(0f, 0.99f, -0.43f);
        public const float ArcadePanelSlope = -12f;

        /// <summary>World point on the panel's top surface; (x, z) across / along the panel (z towards the screen).</summary>
        private static Vector3 PanelTop(Vector3 pos, float x, float z, float lift = 0f)
        {
            var rot = Quaternion.Euler(ArcadePanelSlope, 0f, 0f);
            return pos + ArcadePanelLocal + rot * new Vector3(x, 0.03f + lift, z);
        }

        private static void ArcadeControls(Transform a, Vector3 pos, Material metal)
        {
            var rot = Quaternion.Euler(ArcadePanelSlope, 0f, 0f);
            Vector3 n = rot * Vector3.up;
            var panelMat = Mat("LobbyArcadePanel", new Color(0.12f, 0.12f, 0.16f), null, 0.5f, Vector2.one);
            Deco("Panel", a, pos + ArcadePanelLocal, new Vector3(0.68f, 0.06f, 0.30f), panelMat, rot);
            // Printed overlay (texture drawn at runtime by ArcadeArt), metal front strip and lip, corner screws.
            QuadAt("PanelArt", a, PanelTop(pos, 0f, 0f, 0.002f), rot * Quaternion.Euler(90f, 0f, 0f), new Vector2(0.66f, 0.28f),
                Mat("LobbyArcadePanelArt", Color.white, null, 0.55f, Vector2.one));
            var chrome = Mat("LobbyArcadeChrome", new Color(0.72f, 0.74f, 0.78f), null, 0.85f, Vector2.one, 0.9f);
            Deco("PanelTrim", a, pos + ArcadePanelLocal + rot * new Vector3(0f, 0f, -0.156f), new Vector3(0.70f, 0.075f, 0.012f), chrome, rot);
            Deco("PanelLip", a, pos + ArcadePanelLocal + rot * new Vector3(0f, 0.036f, -0.147f), new Vector3(0.70f, 0.008f, 0.02f), chrome, rot);
            foreach (var (x, z) in new[] { (-0.31f, -0.12f), (0.31f, -0.12f), (-0.31f, 0.12f), (0.31f, 0.12f) })
                Cyl("Screw", a, PanelTop(pos, x, z, 0.003f), 0.012f, 0.004f, chrome, rot);

            // Joystick: base plate, dust washer, chrome collar, rubber boot, shaft (pivots at its foot), red ball.
            var dark = Mat("LobbyArcadeStickBase", new Color(0.05f, 0.05f, 0.06f), null, 0.5f, Vector2.one, 0.4f);
            var rubber = Mat("LobbyArcadeRubber", new Color(0.03f, 0.03f, 0.03f), null, 0.15f, Vector2.one);
            Vector3 s = PanelTop(pos, -0.17f, -0.01f);
            Cyl("StickPlate", a, s + n * 0.002f, 0.075f, 0.004f, dark, rot);
            Cyl("StickWasher", a, s + n * 0.005f, 0.05f, 0.004f, rubber, rot);
            Cyl("StickCollar", a, s + n * 0.008f, 0.034f, 0.004f, chrome, rot);
            Cyl("StickBoot", a, s + Vector3.up * 0.016f, 0.024f, 0.022f, rubber);
            Cyl("Stick", a, s + Vector3.up * 0.0425f, 0.013f, 0.085f, metal);
            var red = Mat("LobbyArcadeRed", new Color(0.9f, 0.1f, 0.1f), null, 0.85f, Vector2.one);
            Ball("StickBall", a, s + Vector3.up * 0.107f, Vector3.one * 0.055f, red);

            // Buttons: chrome ring, coloured plunger with a domed top (pressed in by LobbyArcade while playing).
            var ringMat = Mat("LobbyArcadeBtnRing", new Color(0.06f, 0.06f, 0.07f), null, 0.8f, Vector2.one, 0.5f);
            var cols = new[] { new Color(0.95f, 0.15f, 0.15f), new Color(0.2f, 0.5f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.9f, 0.3f), Color.white, new Color(1f, 0.55f, 0.1f) };
            var spots = new[] { (0.03f, -0.045f, 0.046f), (0.10f, -0.035f, 0.046f), (0.05f, 0.035f, 0.046f), (0.12f, 0.045f, 0.046f), (0.235f, 0.085f, 0.032f), (0.285f, 0.085f, 0.032f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var (x, z, d) = spots[i];
                Vector3 b = PanelTop(pos, x, z);
                var g = new GameObject(i < 4 ? "Button" : "StartButton").transform;
                g.SetParent(a, false);
                g.SetPositionAndRotation(b, rot);
                Cyl("Ring", g, b + n * 0.004f, d + 0.014f, 0.008f, ringMat, rot);
                var capMat = Emissive("LobbyArcadeBtn" + i, cols[i], cols[i] * 0.45f);
                Cyl("Cap", g, b + n * 0.011f, d, 0.012f, capMat, rot);
                var dome = Ball("Dome", g, b + n * 0.017f, new Vector3(d, 0.012f, d), capMat);
                dome.transform.rotation = rot;
            }
        }

        // ================================================================ cabinet trim

        private static void ArcadeTrim(Transform a, Vector3 pos, Material metal, Material body)
        {
            var chrome = Mat("LobbyArcadeChrome", new Color(0.72f, 0.74f, 0.78f), null, 0.85f, Vector2.one, 0.9f);
            var black = Mat("LobbyArcadeBlack", new Color(0.02f, 0.02f, 0.025f), null, 0.4f, Vector2.one);
            // Coin door: framed metal door, lit coin slots with a slit, coin return flaps, lock.
            Deco("CoinFrame", a, pos + new Vector3(0f, 0.55f, -0.307f), new Vector3(0.34f, 0.40f, 0.012f), black);
            Deco("CoinDoor", a, pos + new Vector3(0f, 0.55f, -0.318f), new Vector3(0.3f, 0.36f, 0.012f), metal);
            var slotLit = Emissive("LobbyCoinSlot", new Color(1f, 0.3f, 0.2f), new Color(1f, 0.2f, 0.1f) * 1.2f);
            for (int i = 0; i < 2; i++)
            {
                float x = -0.06f + i * 0.12f;
                Deco("CoinSlot", a, pos + new Vector3(x, 0.63f, -0.327f), new Vector3(0.06f, 0.08f, 0.008f), slotLit);
                Deco("CoinSlit", a, pos + new Vector3(x, 0.635f, -0.333f), new Vector3(0.005f, 0.04f, 0.004f), black);
                Deco("CoinReturn", a, pos + new Vector3(x, 0.47f, -0.328f), new Vector3(0.05f, 0.03f, 0.01f), black);
            }
            Cyl("CoinLock", a, pos + new Vector3(0f, 0.41f, -0.326f), 0.022f, 0.006f, chrome, Quaternion.Euler(90f, 0f, 0f));
            // Kick plate at the floor, roof cap, chrome strips around the marquee.
            Deco("KickPlate", a, pos + new Vector3(0f, 0.05f, -0.307f), new Vector3(0.70f, 0.10f, 0.012f), metal);
            Deco("RoofCap", a, pos + new Vector3(0f, 1.918f, 0.02f), new Vector3(0.82f, 0.032f, 0.82f), black);
            Deco("MarqueeTop", a, pos + new Vector3(0f, 1.888f, -0.335f), new Vector3(0.70f, 0.014f, 0.08f), chrome);
            Deco("MarqueeBottom", a, pos + new Vector3(0f, 1.672f, -0.335f), new Vector3(0.70f, 0.014f, 0.08f), chrome);
            // Bevelled front edges of the screen box (45 degree strips between the side panels and the bezel area).
            foreach (float sx in new[] { -1f, 1f })
                Deco("Bevel", a, pos + new Vector3(sx * 0.335f, 1.36f, -0.315f), new Vector3(0.03f, 0.56f, 0.03f), body, Quaternion.Euler(0f, 45f, 0f));
        }
    }
}
