using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Garage pieces with a job (<see cref="LobbyLayout.Garage"/>): a wheel balancer in place of the floor globe (the
    /// wheel spins like the globe did: <see cref="LobbyGlobe"/> on the wheel pivot, prop kind Globe), an enamel tin sign
    /// in place of the city painting, workshop bits on the fireplace mantel (oil can, trophy, spark plugs) and a red
    /// jerrycan next to the oil drum.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        /// <summary>Wheel balancer, front (local +Z) to the room; the wheel on a shaft out of its right side.</summary>
        private static void GarageWheelBalancer(Transform parent, Vector3 pos)
        {
            var t = Frame(parent, "FloorGlobe", pos, 90f);
            var body = Mat("GarageMachineGrey", new Color(0.5f, 0.53f, 0.56f), Tex("garage_ceiling", TexKind.Concrete), 0.45f, new Vector2(0.6f, 0.6f), 0.35f);
            var blue = Mat("GarageMachineBlue", new Color(0.08f, 0.22f, 0.5f), null, 0.5f, Vector2.one, 0.25f);
            var chrome = Mat("LobbySilver", new Color(0.78f, 0.78f, 0.8f), null, 0.8f, Vector2.one, 0.9f);
            var dark = Mat("LobbyFireBlack", new Color(0.04f, 0.04f, 0.045f), null, 0.45f, Vector2.one, 0.4f);
            RBox(t, "Body", new Vector3(0f, 0.44f, -0.05f), new Vector3(0.6f, 0.8f, 0.48f), body, 0.02f);
            RBox(t, "Stripe", new Vector3(0f, 0.62f, 0.192f), new Vector3(0.6f, 0.06f, 0.01f), blue, 0.004f);
            foreach (var (x, z) in new[] { (-0.26f, -0.25f), (0.26f, -0.25f), (-0.26f, 0.15f), (0.26f, 0.15f) })
                LocalBox(t, "Foot", new Vector3(x, 0.02f, z), new Vector3(0.06f, 0.04f, 0.06f), dark);
            // weight tray on top: dark tray with dividers and a few clip-on weights
            RBox(t, "Tray", new Vector3(0f, 0.86f, -0.03f), new Vector3(0.62f, 0.04f, 0.4f), dark, 0.008f);
            for (int i = 0; i < 4; i++) LocalBox(t, "Divider", new Vector3(-0.21f + i * 0.14f, 0.885f, -0.03f), new Vector3(0.008f, 0.02f, 0.38f), dark);
            var lead = Mat("GarageLeadWeight", new Color(0.55f, 0.56f, 0.55f), null, 0.4f, Vector2.one, 0.6f);
            for (int i = 0; i < 9; i++)
                LocalBox(t, "Weight", new Vector3(-0.26f + (i % 5) * 0.12f + Hash01(i) * 0.04f, 0.885f, -0.15f + (i / 5) * 0.2f + Hash01(i + 9) * 0.04f), new Vector3(0.05f, 0.012f, 0.02f), lead)
                    .transform.localRotation = Quaternion.Euler(0f, Hash01(i + 3) * 60f, 0f);
            // display console at the back, tilted to the player
            var console = new GameObject("Console").transform;
            console.SetParent(t, false);
            console.localPosition = new Vector3(0f, 1.02f, -0.24f);
            console.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            RBox(console, "Panel", Vector3.zero, new Vector3(0.52f, 0.26f, 0.07f), blue, 0.01f);
            LocalBox(console, "Post", new Vector3(0f, -0.15f, -0.01f), new Vector3(0.06f, 0.1f, 0.04f), dark);
            var led = Emissive("GarageLedRed", new Color(0.4f, 0.03f, 0.02f), new Color(1f, 0.12f, 0.05f) * 2.2f);
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                LocalBox(console, "Window", new Vector3(x, 0.05f, 0.036f), new Vector3(0.16f, 0.07f, 0.004f), dark);
                LocalBox(console, "Digits", new Vector3(x, 0.05f, 0.039f), new Vector3(0.12f, 0.04f, 0.002f), led).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = 0; i < 6; i++)
                LocalBox(console, "Button", new Vector3(-0.19f + i * 0.076f, -0.06f, 0.038f), new Vector3(0.05f, 0.03f, 0.008f), i == 5 ? Mat("GarageToolRed", new Color(0.7f, 0.08f, 0.06f), null, 0.5f, Vector2.one) : Mat("GarageButtonGrey", new Color(0.75f, 0.76f, 0.74f), null, 0.4f, Vector2.one));
            // shaft out of the right side, wheel on it
            const float wy = 0.72f, wz = -0.05f, wx = 0.5f;
            var shaft = Cyl("Shaft", t, Vector3.zero, 0.04f, 0.34f, chrome);
            shaft.transform.localPosition = new Vector3(0.44f, wy, wz);
            shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var flange = Cyl("Flange", t, Vector3.zero, 0.15f, 0.03f, dark);
            flange.transform.localPosition = new Vector3(0.315f, wy, wz);
            flange.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(t, false);
            wheel.localPosition = new Vector3(wx, wy, wz);
            wheel.localRotation = Quaternion.Euler(0f, 0f, -90f); // pivot Y = shaft axis, pointing away from the machine
            MeshObj("Tyre", wheel, SaveMesh(TyreWithTread(new ProcMesh(), Vector3.zero, Quaternion.identity, 40).ToMesh("GarageBalancerTyreTread"), "GarageBalancerTyreTread"), GarageTyreRubber, Vector3.zero);
            var rimM = Mat("GarageRimSteel", new Color(0.62f, 0.63f, 0.64f), null, 0.6f, Vector2.one, 0.75f);
            var rim = new ProcMesh();
            Vector2[] face = { new(0.205f, 0.035f), new(0.17f, 0.045f), new(0.1f, 0.055f), new(0.045f, 0.06f), new(0.04f, 0.045f) };
            rim.At(Vector3.zero).Lathe(face, 36, 40f);
            var back = new Vector2[face.Length];
            for (int i = 0; i < face.Length; i++) back[i] = face[face.Length - 1 - i] + new Vector2(0f, -0.008f);
            rim.At(Vector3.zero).Lathe(back, 36, 40f);
            rim.At(Vector3.zero).Lathe(new[] { new Vector2(0.203f, 0.1f), new Vector2(0.203f, -0.1f) }, 36, 40f); // barrel, seen from inside
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + 30f) * Mathf.Deg2Rad;
                rim.At(new Vector3(Mathf.Cos(a) * 0.075f, 0.055f, Mathf.Sin(a) * 0.075f)).Lathe(new[] { new Vector2(0.011f, 0f), new Vector2(0.011f, 0.018f), new Vector2(0.006f, 0.022f) }, 6, 10f);
            }
            MeshObj("Rim", wheel, SaveMesh(rim.ToMesh("GarageBalancerRim"), "GarageBalancerRim"), rimM, Vector3.zero);
            // quick-release cone + wing nut on the shaft end, two clip weights on the rim edge
            MeshObj("Cone", wheel, LatheSaved("GarageBalancerCone", new[] { new Vector2(0.05f, 0f), new Vector2(0.045f, 0.03f), new Vector2(0.025f, 0.08f), new Vector2(0.025f, 0.12f), new Vector2(0f, 0.125f) }, 20, true, true), dark, new Vector3(0f, 0.05f, 0f));
            LocalBox(wheel, "WingNut", new Vector3(0f, 0.16f, 0f), new Vector3(0.16f, 0.03f, 0.02f), chrome);
            foreach (float a in new[] { 20f, 200f })
            {
                var w = LocalBox(wheel, "ClipWeight", Quaternion.Euler(0f, a, 0f) * new Vector3(0.208f, 0.09f, 0f), new Vector3(0.012f, 0.02f, 0.045f), lead);
                w.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
            BoxCollider(t, new Vector3(0.17f, 0.56f, -0.05f), new Vector3(0.96f, 1.12f, 0.7f));
            t.gameObject.AddComponent<LobbyGlobe>().sphere = wheel;
            AddProp(t.gameObject, LobbyPropKind.Globe, new Vector3(wx, wy, wz), 1.9f);
        }

        /// <summary>Enamel tin sign (1.2 x 0.72 m) on a wall, facing <paramref name="yaw"/>: chipped cream plate, red border, rust, two screws.</summary>
        private static void GarageTinSign(Transform root, Vector3 wallPos, float yaw)
        {
            var p = new GameObject("TinSign").transform;
            p.SetParent(root, false);
            var size = new Vector2(1.2f, 0.72f);
            var plate = Mat("GarageTinSign", Color.white, TinSignTex(), 0.55f, Vector2.one, 0.2f);
            var q = QuadAt("Face", p, Vector3.zero, Quaternion.identity, size, plate);
            q.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            LocalBox(p, "Back", new Vector3(0f, 0f, 0.003f), new Vector3(size.x - 0.01f, size.y - 0.01f, 0.005f), Mat("GarageRust", new Color(0.32f, 0.17f, 0.1f), null, 0.15f, Vector2.one, 0.2f));
            var red = new Color(0.6f, 0.09f, 0.07f);
            Label3D("Title", p, Vector3.zero, Quaternion.identity, "WARSZTAT", red, 0.032f, new Vector2(0.95f, 0.24f)).transform.localPosition = new Vector3(0f, 0.1f, 0.0075f);
            Label3D("Sub", p, Vector3.zero, Quaternion.identity, "NAPRAWY   OPONY   OLEJE", new Color(0.1f, 0.14f, 0.3f), 0.011f, new Vector2(0.85f, 0.08f)).transform.localPosition = new Vector3(0f, -0.13f, 0.0075f);
            foreach (Transform c in p)
                if (c.GetComponent<TextMesh>() != null) c.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (float sx in new[] { -1f, 1f })
                MeshObj("Screw", p, LatheSaved("GarageSignScrew", new[] { new Vector2(0.012f, 0f), new Vector2(0.011f, 0.004f), new Vector2(0f, 0.006f) }, 10, false, false), GarageSteel,
                    new Vector3(sx * (size.x * 0.5f - 0.045f), size.y * 0.5f - 0.045f, 0.006f), Quaternion.Euler(90f, 0f, 0f));
            p.SetPositionAndRotation(wallPos, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 1.6f));
        }

        private static Texture2D TinSignTex() => SmoothTex("garage_tin_sign", 600, 360, (x, y) =>
        {
            float u = x / 600f, v = y / 360f;
            float e = Mathf.Min(Mathf.Min(u, 1f - u) * 1.667f, Mathf.Min(v, 1f - v)); // distance to the edge, plate height = 1
            Color cream = new(0.86f, 0.8f, 0.64f), red = new(0.6f, 0.09f, 0.07f), rust = new(0.36f, 0.18f, 0.08f), steel = new(0.12f, 0.12f, 0.13f);
            var c = e < 0.075f ? red : (e < 0.09f ? cream : (e < 0.1f ? red : cream));
            c *= 0.92f + 0.08f * Fbm(u * 8f, v * 5f);
            // rust creeping in from the edges and round the screw holes, enamel chips showing dark steel
            float holes = 0f;
            foreach (float hx in new[] { 0.0375f, 0.9625f })
                holes = Mathf.Max(holes, Soak((u - hx) * 1.667f, v - 0.9375f, 0.05f, hx * 9f));
            float rustAmt = Step01(0.55f, 0.75f, Fbm(u * 9f + 1f, v * 6f) + (0.12f - Mathf.Min(e, 0.12f)) * 3f) + holes;
            c = Color.Lerp(c, rust * (0.8f + 0.4f * Fbm(u * 40f, v * 30f)), Mathf.Clamp01(rustAmt));
            float chip = 0f;
            for (int i = 0; i < 9; i++)
                chip = Mathf.Max(chip, Blot((u - Hash01(i * 7 + 1)) * 1.667f, v - Hash01(i * 11 + 4), 0.012f + 0.02f * Hash01(i * 3), i * 1.7f));
            c = Color.Lerp(c, steel, chip);
            float hole = ((u - 0.0375f) * 1.667f) * ((u - 0.0375f) * 1.667f) + (v - 0.9375f) * (v - 0.9375f);
            float hole2 = ((u - 0.9625f) * 1.667f) * ((u - 0.9625f) * 1.667f) + (v - 0.9375f) * (v - 0.9375f);
            if (hole < 0.0002f || hole2 < 0.0002f) c = Color.black;
            return c;
        });

        /// <summary>Mantel bits (fireplace frame, mantel top at 1.06 m): a racing trophy, three spark plugs, a pump oil can.</summary>
        private static void GarageMantelBits(Transform t)
        {
            var gold = Mat("LobbyBrass", new Color(0.78f, 0.6f, 0.3f), null, 0.72f, Vector2.one, 0.85f);
            var black = Mat("LobbyFireBlack", new Color(0.04f, 0.04f, 0.045f), null, 0.45f, Vector2.one, 0.4f);
            RBox(t, "TrophyBase", new Vector3(-0.68f, 1.1f, 0.2f), new Vector3(0.12f, 0.08f, 0.12f), black, 0.006f);
            MeshObj("Trophy", t, LatheSaved("GarageTrophy", new[]
            {
                new Vector2(0.04f, 0f), new Vector2(0.035f, 0.01f), new Vector2(0.012f, 0.02f), new Vector2(0.01f, 0.08f), new Vector2(0.02f, 0.09f),
                new Vector2(0.06f, 0.13f), new Vector2(0.07f, 0.2f), new Vector2(0.066f, 0.2f), new Vector2(0.055f, 0.14f), new Vector2(0f, 0.12f),
            }, 24, true, false), gold, new Vector3(-0.68f, 1.14f, 0.2f));
            foreach (float s in new[] { -1f, 1f })
            {
                var handle = MeshObj("TrophyHandle", t, LobbyMeshSaved("GarageTrophyHandle", () => LobbyMesh.Torus(0.035f, 0.006f, 16, 6)), gold, new Vector3(-0.68f + s * 0.07f, 1.31f, 0.2f), Quaternion.Euler(90f, 0f, 0f));
                handle.transform.localScale = new Vector3(0.8f, 1f, 1f);
            }
            var ceramic = Mat("GarageSparkCeramic", new Color(0.92f, 0.9f, 0.86f), null, 0.75f, Vector2.one);
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector3(0.38f + i * 0.05f, 1.06f, 0.16f + (i % 2) * 0.05f);
                MeshObj("PlugBody", t, LatheSaved("GarageSparkBody", new[] { new Vector2(0.004f, 0f), new Vector2(0.007f, 0.025f), new Vector2(0.011f, 0.026f), new Vector2(0.011f, 0.04f) }, 6, true, true), GarageSteel, at);
                MeshObj("PlugCeramic", t, LatheSaved("GarageSparkTop", new[] { new Vector2(0.008f, 0f), new Vector2(0.007f, 0.045f), new Vector2(0.004f, 0.05f), new Vector2(0.003f, 0.06f), new Vector2(0f, 0.062f) }, 12, true, false), ceramic, at + new Vector3(0f, 0.04f, 0f));
            }
            var oiler = Mat("GarageOilerRed", new Color(0.62f, 0.1f, 0.06f), null, 0.6f, Vector2.one, 0.4f);
            MeshObj("OilCan", t, LatheSaved("GarageOilCan", new[] { new Vector2(0.055f, 0f), new Vector2(0.058f, 0.01f), new Vector2(0.055f, 0.05f), new Vector2(0.02f, 0.09f), new Vector2(0.015f, 0.1f) }, 24, true, true), oiler, new Vector3(0.7f, 1.06f, 0.22f));
            var spout = new ProcMesh().At(Vector3.zero).Tube(new List<Vector3> { new(0f, 0.095f, 0f), new(-0.03f, 0.14f, 0.02f), new(-0.1f, 0.2f, 0.06f) }, 0.004f, 6, true);
            GMeshObj("Spout", t, spout, "GarageOilCanSpout", GarageSteel, new Vector3(0.7f, 1.06f, 0.22f));
        }

        /// <summary>Small paint tin (lid rim, coloured label, a dried run of paint) standing at <paramref name="local"/>.</summary>
        private static void GaragePaintCan(Transform parent, Vector3 local, int seed)
        {
            Color[] paints = { new(0.85f, 0.85f, 0.82f), new(0.62f, 0.1f, 0.07f), new(0.1f, 0.25f, 0.55f), new(0.85f, 0.65f, 0.1f), new(0.15f, 0.4f, 0.2f) };
            var col = paints[Mathf.Abs(seed) % paints.Length];
            var can = new GameObject("PaintCan").transform;
            can.SetParent(parent, false);
            can.localPosition = local;
            can.localRotation = Quaternion.Euler(0f, Hash01(seed + 70) * 360f, 0f);
            float s = 0.8f + 0.4f * Hash01(seed + 71);
            can.localScale = new Vector3(s, s, s);
            MeshObj("Tin", can, LatheSaved("GaragePaintTin", new[] { new Vector2(0.06f, 0f), new Vector2(0.062f, 0.006f), new Vector2(0.06f, 0.012f), new Vector2(0.06f, 0.11f), new Vector2(0.063f, 0.118f), new Vector2(0.05f, 0.12f), new Vector2(0f, 0.12f) }, 24, true, false), GarageSteel, Vector3.zero);
            var label = Mat("GaragePaintLabel" + Mathf.Abs(seed) % paints.Length, col, null, 0.35f, Vector2.one);
            MeshObj("Label", can, LatheSaved("GaragePaintLabel", new[] { new Vector2(0.0615f, 0.025f), new Vector2(0.0615f, 0.095f) }, 24, false, false), label, Vector3.zero);
            LocalBox(can, "Run", new Vector3(0f, 0.09f, 0.0625f), new Vector3(0.012f, 0.06f, 0.002f), label).isStatic = false;
        }

        /// <summary>Red 20 l jerrycan (x-pressed sides, three handles) standing next to <paramref name="pos"/>.</summary>
        private static void GarageJerrycan(Transform parent, Vector3 pos, float yaw)
        {
            var j = Frame(parent, "Jerrycan", pos, yaw);
            var red = Mat("GarageJerryRed", new Color(0.58f, 0.07f, 0.05f), Tex("garage_ceiling", TexKind.Concrete), 0.45f, new Vector2(0.4f, 0.4f), 0.3f);
            RBox(j, "Body", new Vector3(0f, 0.23f, 0f), new Vector3(0.34f, 0.44f, 0.16f), red, 0.02f);
            foreach (float s in new[] { -1f, 1f })
            foreach (float a in new[] { 32f, -32f })
                LocalBox(j, "Press", new Vector3(0f, 0.22f, s * 0.081f), new Vector3(0.38f, 0.02f, 0.006f), red).transform.localRotation = Quaternion.Euler(0f, 0f, a);
            for (int i = 0; i < 3; i++)
                RBox(j, "Handle", new Vector3(-0.08f + i * 0.08f, 0.475f, 0f), new Vector3(0.022f, 0.05f, 0.11f), red, 0.008f);
            RBox(j, "HandleBar", new Vector3(0f, 0.505f, 0f), new Vector3(0.2f, 0.02f, 0.03f), red, 0.008f);
            var cap = Cyl("Spout", j, Vector3.zero, 0.05f, 0.05f, GarageSteel);
            cap.transform.localPosition = new Vector3(0.13f, 0.47f, 0f);
            cap.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            BoxCollider(j, new Vector3(0f, 0.26f, 0f), new Vector3(0.34f, 0.52f, 0.16f));
        }
    }
}
