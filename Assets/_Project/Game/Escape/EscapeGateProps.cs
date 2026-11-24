using HideAndSeek.Game.Cinematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Escape
{
    /// <summary>
    /// What the escape adds to the exit gate (built at runtime under the <see cref="ExitGate"/> object, every peer):
    /// colliders for the driveway behind the shutter (the street art of the gate is visual only), the painted
    /// boundary line with "WYJAZD" on the asphalt, the invisible wall that stops seekers just before the line (only
    /// switched on on a seeker's own machine - movement is owner side), two orange rotating warning beacons above
    /// the opening, and the floor of the limbo where escaped players wait (<see cref="EscapeTimeline.Limbo"/>).
    /// </summary>
    public sealed class EscapeGateProps
    {
        public BoxCollider SeekerWall { get; private set; }
        public readonly Transform[] BeaconSpin = new Transform[2];
        public readonly Light[] BeaconSpot = new Light[2];
        public readonly Light[] BeaconGlow = new Light[2];
        public Material BeaconMat { get; private set; }
        public Transform SirenPoint { get; private set; }
        /// <summary>Green "EXIT OPEN" plate over the map's red "EXIT CLOSED" sign (on while the gate is open / opening).</summary>
        public GameObject OpenSign { get; private set; }

        public static EscapeGateProps Build(ExitGate gate)
        {
            var p = new EscapeGateProps();
            p.BuildDriveway(gate.transform);
            p.BuildLine(gate.transform);
            p.BuildBeacons(gate.transform);
            BuildLimboFloor();
            return p;
        }

        static BoxCollider Solid(string name, Transform parent, Vector3 min, Vector3 max)
        {
            var t = CineKit.Node(name, parent, (min + max) * 0.5f);
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z));
            return c;
        }

        void BuildDriveway(Transform gate)
        {
            var t = CineKit.Node("EscapeDriveway", gate, Vector3.zero);
            float w = EscapeTimeline.DriveHalfW;
            // Asphalt (its top at the garage floor level) from inside the wall to past the end.
            Solid("DriveFloor", t, new Vector3(-w - 0.6f, -0.6f, 0.05f), new Vector3(w + 0.6f, 0f, EscapeTimeline.DriveEndZ - 1f));
            // Retaining walls (the gate art draws them from z -0.4 to -12, 3.2 m tall).
            for (int s = -1; s <= 1; s += 2)
                Solid("DriveWall" + s, t, new Vector3(s * w, 0f, -0.35f), new Vector3(s * (w + 0.4f), 4.2f, EscapeTimeline.DriveEndZ - 1f));
            // Nobody walks out into the street: a wall at the end of the driveway.
            Solid("DriveEnd", t, new Vector3(-w - 0.4f, 0f, EscapeTimeline.DriveEndZ), new Vector3(w + 0.4f, 4.2f, EscapeTimeline.DriveEndZ - 0.4f));
            // The seekers' wall, on only for the local seeker (EscapeGateDriver).
            SeekerWall = Solid("SeekerWall", t, new Vector3(-w - 0.2f, -0.2f, EscapeTimeline.SeekerWallZ), new Vector3(w + 0.2f, 4.2f, EscapeTimeline.SeekerWallZ - 0.3f));
            SeekerWall.gameObject.layer = 2; // Ignore Raycast: no effect on shots / prompts
            SeekerWall.enabled = false;
        }

        void BuildLine(Transform gate)
        {
            var t = CineKit.Node("EscapeLine", gate, Vector3.zero);
            float w = EscapeTimeline.DriveHalfW;
            var hazard = CineKit.Mat("EscLineHazard", Color.white, 0f, 0.35f, StartLeverArt.Hazard);
            hazard.mainTextureScale = new Vector2(12f, 1f);
            var up = Quaternion.Euler(-90f, 0f, 0f); // quads face +Z; this lays them flat, facing up
            CineKit.Quad("BoundaryStripe", t, new Vector3(0f, 0.012f, EscapeTimeline.LineZ), new Vector2(w * 2f, 0.32f), hazard, up);
            var yellow = CineKit.Mat("EscLineYellow", new Color(0.95f, 0.78f, 0.12f), 0f, 0.3f);
            CineKit.Quad("BoundaryEdgeIn", t, new Vector3(0f, 0.011f, EscapeTimeline.LineZ + 0.24f), new Vector2(w * 2f, 0.08f), yellow, up);
            CineKit.Quad("BoundaryEdgeOut", t, new Vector3(0f, 0.011f, EscapeTimeline.LineZ - 0.24f), new Vector2(w * 2f, 0.08f), yellow, up);
            // "WYJAZD" painted on the asphalt before the line, readable when walking out.
            var paint = CineKit.Mat("EscLineText", Color.white, 0f, 0.2f,
                StartLeverArt.Label(new[] { "WYJAZD" }, 96, 24, 2, new Color(0.07f, 0.07f, 0.08f), new Color(0.95f, 0.93f, 0.85f)));
            CineKit.Quad("BoundaryText", t, new Vector3(0f, 0.011f, EscapeTimeline.LineZ + 1.4f), new Vector2(3.6f, 0.9f), paint, up); // read by whoever walks out
            // Short posts at both ends of the line with a dim amber top.
            var post = CineKit.Mat("EscPost", new Color(0.95f, 0.72f, 0.1f), 0.2f, 0.4f);
            var cap = CineKit.Glow("EscPostCap", new Color(1f, 0.55f, 0.12f), 1.4f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = new Vector3(s * (w - 0.25f), 0f, EscapeTimeline.LineZ);
                CineKit.Cyl("Post", t, c + Vector3.up * 0.45f, 0.14f, 0.9f, 1, post);
                CineKit.Sphere("PostCap", t, c + Vector3.up * 0.93f, 0.12f, cap);
            }
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildBeacons(Transform gate)
        {
            var dark = CineKit.Mat("EscBeaconBase", new Color(0.08f, 0.08f, 0.09f), 0.4f, 0.4f);
            BeaconMat = CineKit.Glow("EscBeaconGlass", new Color(1f, 0.5f, 0.05f), 0.4f);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                var b = CineKit.Node("EscBeacon" + i, gate, new Vector3(s * (ExitGate.HalfW + 0.45f), 2.62f, 0.16f));
                CineKit.Box("Bracket", b, new Vector3(0f, -0.06f, -0.08f), new Vector3(0.16f, 0.04f, 0.22f), dark, 0.008f);
                CineKit.Cyl("Base", b, new Vector3(0f, -0.02f, 0f), 0.15f, 0.05f, 1, dark);
                CineKit.Cyl("Glass", b, new Vector3(0f, 0.07f, 0f), 0.13f, 0.14f, 1, BeaconMat);
                CineKit.Sphere("Dome", b, new Vector3(0f, 0.14f, 0f), 0.13f, BeaconMat);
                var spin = CineKit.Node("Spin", b, new Vector3(0f, 0.08f, 0f));
                var spot = CineKit.Node("Spot", spin, Vector3.zero, Quaternion.Euler(12f, 0f, 0f)).gameObject.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.spotAngle = 38f;
                spot.range = 16f;
                spot.color = new Color(1f, 0.52f, 0.08f);
                spot.intensity = 0f;
                spot.shadows = LightShadows.None;
                var glow = CineKit.Node("Glow", b, new Vector3(0f, 0.1f, 0.25f)).gameObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.range = 5f;
                glow.color = new Color(1f, 0.5f, 0.1f);
                glow.intensity = 0f;
                glow.shadows = LightShadows.None;
                BeaconSpin[i] = spin;
                BeaconSpot[i] = spot;
                BeaconGlow[i] = glow;
            }
            SirenPoint = CineKit.Node("SirenPoint", gate, new Vector3(0f, 2.6f, 0.4f));
            // The map sign: 1.8 x 0.26 m at y 2.62, 0.45 m in front of the wall face (ParkingGarageBuilder).
            var sign = CineKit.Mat("EscOpenSign", Color.white, 0f, 0.3f,
                StartLeverArt.Label(new[] { "EXIT OPEN" }, 128, 20, 2, new Color(0.08f, 0.55f, 0.2f), new Color(0.95f, 1f, 0.95f)));
            var q = CineKit.Quad("EscOpenSign", gate, new Vector3(0f, 2.62f, 0.49f), new Vector2(1.84f, 0.3f), sign);
            OpenSign = q.gameObject;
            OpenSign.SetActive(false);
        }

        static void BuildLimboFloor()
        {
            if (GameObject.Find("EscapeLimbo") != null) return;
            var go = new GameObject("EscapeLimbo");
            var c = go.AddComponent<BoxCollider>();
            c.size = new Vector3(40f, 1f, 40f);
            go.transform.position = new Vector3(156f, EscapeTimeline.LimboY - 0.5f, 0f);
        }
    }
}
