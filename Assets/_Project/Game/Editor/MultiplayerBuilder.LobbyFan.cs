using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Ceiling fan over the coffee table: ceiling medallion, canopy, downrod, bronze motor housing with a trim ring,
    /// five walnut paddles on curved brass irons, frosted light bowl with a finial and a pull chain. The rotor spins
    /// with <see cref="LobbyFanMotor"/>; the fan has its own wall switch next to the fireplace (LobbyProp FanSwitch).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void CeilingFanModel(Transform parent, Vector3 ceilingPoint, Material darkWood)
        {
            var f = new GameObject("CeilingFan").transform;
            f.SetParent(parent, false);
            f.position = ceilingPoint;
            bool garage = LobbyLayout.Garage; // garage: no plaster medallion, black steel body, galvanised blades
            var Bronze = garage ? GarageBlackSteel : MultiplayerBuilder.Bronze;
            var Brass = garage ? GarageSteel : MultiplayerBuilder.Brass;
            var plaster = Mat("LobbyCeilingMedallion", new Color(0.86f, 0.83f, 0.76f), null, 0.15f, Vector2.one);
            if (!garage) MeshObj("Medallion", f, LatheSaved("FanMedallion", new[] { new Vector2(0.1f, -0.03f), new Vector2(0.16f, -0.03f), new Vector2(0.2f, -0.022f), new Vector2(0.3f, -0.02f), new Vector2(0.33f, -0.012f), new Vector2(0.38f, -0.012f), new Vector2(0.42f, -0.006f), new Vector2(0.42f, 0f) }, 48, true, false), plaster, Vector3.zero);
            MeshObj("Canopy", f, LatheSaved("FanCanopy", new[] { new Vector2(0.02f, -0.09f), new Vector2(0.05f, -0.08f), new Vector2(0.085f, -0.04f), new Vector2(0.09f, 0f) }, 32, true, false), Bronze, new Vector3(0f, -0.03f, 0f));
            MeshObj("Downrod", f, LatheSaved("FanRod", new[] { new Vector2(0.014f, -0.32f), new Vector2(0.014f, 0f) }, 12, false, false), Bronze, new Vector3(0f, -0.11f, 0f));
            float my = -0.45f;
            MeshObj("Motor", f, LatheSaved("FanMotor", new[] { new Vector2(0.08f, -0.11f), new Vector2(0.125f, -0.1f), new Vector2(0.14f, -0.08f), new Vector2(0.14f, -0.04f), new Vector2(0.12f, -0.01f), new Vector2(0.06f, 0.02f), new Vector2(0.02f, 0.03f) }, 40, true, false), Bronze, new Vector3(0f, my, 0f));
            MeshObj("TrimRing", f, LobbyMeshSaved("FanTrim", () => LobbyMesh.Torus(0.141f, 0.006f, 48, 6)), Brass, new Vector3(0f, my - 0.045f, 0f));

            // rotor: paddles on irons, spins under the motor
            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(f, false);
            rotor.localPosition = new Vector3(0f, my - 0.12f, 0f);
            MeshObj("Hub", rotor, LatheSaved("FanHub", new[] { new Vector2(0.06f, -0.025f), new Vector2(0.1f, -0.025f), new Vector2(0.13f, -0.012f), new Vector2(0.13f, 0.012f) }, 40, true, true), Bronze, Vector3.zero);
            var walnut = garage
                ? Mat("GarageFanBlade", new Color(0.6f, 0.62f, 0.62f), Tex("garage_ceiling", TexKind.Concrete), 0.5f, new Vector2(0.3f, 1f), 0.7f)
                : Mat("LobbyFanWalnut", new Color(0.5f, 0.33f, 0.2f), Tex("lobby_wood", TexKind.Wood), 0.55f, new Vector2(0.3f, 1.2f));
            var paddle = LobbyMeshSaved("FanPaddle", () => LobbyMesh.Paddle(0.56f, 0.11f, 0.15f, 0.012f));
            for (int i = 0; i < 5; i++)
            {
                var arm = new GameObject("Blade").transform;
                arm.SetParent(rotor, false);
                arm.localRotation = Quaternion.Euler(0f, i * 72f, 0f);
                // brass iron: from the hub out and down a little
                var iron = LocalBox(arm, "Iron", new Vector3(0f, -0.018f, 0.17f), new Vector3(0.035f, 0.01f, 0.12f), Brass);
                iron.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                RoundCube(iron.transform, 0.004f, 0f);
                foreach (float sx in new[] { -0.03f, 0.03f })
                    MeshObj("Screw", arm, LatheSaved("FanScrew", new[] { new Vector2(0.006f, 0f), new Vector2(0.006f, 0.004f), new Vector2(0f, 0.006f) }, 8, false, false), Brass, new Vector3(sx, -0.02f, 0.27f));
                // paddle pitched 12 degrees
                MeshObj("Paddle", arm, paddle, walnut, new Vector3(0f, -0.032f, 0.2f), Quaternion.Euler(0f, 0f, 12f));
            }
            // light kit under the motor (does not spin)
            var bowlMat = Emissive("LobbyFanBowl", new Color(1f, 0.93f, 0.8f), new Color(1f, 0.8f, 0.55f) * 1.3f);
            MeshObj("LightFitter", f, LatheSaved("FanFitter", new[] { new Vector2(0.07f, -0.035f), new Vector2(0.075f, -0.02f), new Vector2(0.07f, 0f) }, 32, false, false), Brass, new Vector3(0f, my - 0.145f, 0f));
            MeshObj("LightBowl", f, LatheSaved("FanBowl", new[] { new Vector2(0f, -0.095f), new Vector2(0.03f, -0.09f), new Vector2(0.06f, -0.07f), new Vector2(0.075f, -0.03f), new Vector2(0.072f, 0f) }, 32, false, false), bowlMat, new Vector3(0f, my - 0.18f, 0f), null, false);
            MeshObj("Finial", f, LatheSaved("FanFinial", new[] { new Vector2(0f, -0.03f), new Vector2(0.009f, -0.025f), new Vector2(0.012f, -0.008f), new Vector2(0f, 0f) }, 12, false, false), Brass, new Vector3(0f, my - 0.275f, 0f));
            // pull chain with a small bell
            for (int i = 0; i < 10; i++)
                Ball("Chain", f, ceilingPoint + new Vector3(0.05f, my - 0.16f - i * 0.016f, 0f), new Vector3(0.006f, 0.012f, 0.006f), Brass);
            MeshObj("ChainBell", f, LatheSaved("FanBell", new[] { new Vector2(0f, -0.032f), new Vector2(0.01f, -0.03f), new Vector2(0.006f, -0.004f), new Vector2(0f, 0f) }, 12, false, false), Brass, new Vector3(0.05f, my - 0.33f, 0f));
            var light = new GameObject("FanLight").AddComponent<Light>();
            light.transform.SetParent(f, false);
            light.transform.localPosition = new Vector3(0f, my - 0.4f, 0f);
            light.type = LightType.Point;
            light.color = WarmLight;
            light.range = 5f;
            light.intensity = 0.9f;
            light.shadows = LightShadows.Soft;
            var motor = f.gameObject.AddComponent<LobbyFanMotor>();
            motor.rotor = rotor;

            // wall switch on the north wall, between the fireplace and the side table
            var white = Mat("LobbyPlastic", new Color(0.9f, 0.9f, 0.86f), null, 0.35f, Vector2.one);
            var sw = WallSwitch(parent, "FanSwitch", new Vector3(LobbyLayout.Fireplace.x - 1.1f, 1.12f, LobbyLayout.HalfD), 180f, white, out var rocker);
            var flip = sw.gameObject.AddComponent<LobbySwitchRocker>();
            flip.rocker = rocker;
            flip.kind = LobbyPropKind.FanSwitch;
            AddProp(sw.gameObject, LobbyPropKind.FanSwitch, new Vector3(0f, 0f, 0.02f), 1.9f);
        }
    }
}
