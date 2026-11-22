using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Furniture: office chair, recorder shelf (blinking DVRs), filing cabinet, binder shelf, lockers, coat rack, water cooler, bin, PC and UPS under the desk.</summary>
    public partial class SecurityRooms
    {
        private readonly List<(Renderer r, float phase, Color c)> _blinkLeds = new();

        private void BuildFurniture(Transform root, int lv, float cx, float cz, float y0)
        {
            BuildOfficeChair(root, new Vector3(cx + 0.45f, y0, Z1 - 1.75f), 14f + lv * 9f);
            // Plastic chair mat under it.
            Box(root, "ChairMat", new Vector3(cx + 0.45f, y0 + 0.045f, Z1 - 1.75f), new Vector3(1.3f, 0.006f, 1.1f), new Color(0.22f, 0.23f, 0.24f), false, null, Surf.Shiny(0.55f));

            BuildRecorderShelf(root, new Vector3(X0 + 0.68f, y0, Z1 - 0.38f));
            BuildFilingCabinet(root, new Vector3(X0 + 0.38f, y0, Z1 - 2.1f));
            BuildBinderShelf(root, new Vector3(X0 + 0.22f, y0 + 1.72f, Z1 - 2.6f));
            for (int i = 0; i < 2; i++) BuildLocker(root, new Vector3(X0 + 0.38f, y0, Z0 + 0.55f + i * 0.62f), i);

            // Coat rack with a hi-vis vest and a cap (south wall, by the baffle).
            Vector3 cr = new Vector3(DoorA - 0.55f, y0, Z0 + 0.45f);
            var wood = new Color(0.3f, 0.2f, 0.12f);
            Prim(root, PrimitiveType.Cylinder, "RackPole", cr + Vector3.up * 0.9f, new Vector3(0.04f, 0.9f, 0.04f), wood, null, false, Surf.T(SecurityTextures.Kind.Laminate, 0.4f, 0.3f));
            for (int i = 0; i < 3; i++)
                Box(root, "RackFoot", cr + Vector3.up * 0.03f, new Vector3(0.5f, 0.03f, 0.04f), wood, false, new Vector3(0f, i * 60f, 0f));
            Box(root, "Vest", cr + new Vector3(0.1f, 1.38f, 0f), new Vector3(0.05f, 0.55f, 0.4f), new Color(0.85f, 0.75f, 0.08f), false, new Vector3(0f, 0f, -4f), Surf.T(SecurityTextures.Kind.Fabric, 0.2f));
            Box(root, "VestStripe", cr + new Vector3(0.13f, 1.3f, 0f), new Vector3(0.005f, 0.05f, 0.41f), new Color(0.8f, 0.8f, 0.8f), false, null, Surf.Shiny(0.7f, 0.3f));
            Box(root, "VestStripe", cr + new Vector3(0.13f, 1.45f, 0f), new Vector3(0.005f, 0.05f, 0.41f), new Color(0.8f, 0.8f, 0.8f), false, null, Surf.Shiny(0.7f, 0.3f));
            Prim(root, PrimitiveType.Cylinder, "Cap", cr + new Vector3(0f, 1.83f, 0f), new Vector3(0.2f, 0.05f, 0.2f), new Color(0.06f, 0.08f, 0.16f), null, false, Surf.T(SecurityTextures.Kind.Fabric, 0.1f));
            Box(root, "CapPeak", cr + new Vector3(0f, 1.8f, -0.12f), new Vector3(0.16f, 0.012f, 0.09f), new Color(0.05f, 0.06f, 0.12f), false);

            // Water cooler with a half-empty bottle and paper cups.
            Vector3 wc = new Vector3(X0 + 0.4f, y0, Z0 + 2.2f);
            Box(root, "Cooler", wc + Vector3.up * 0.5f, new Vector3(0.32f, 1.0f, 0.32f), new Color(0.78f, 0.78f, 0.76f), true, null, Surf.Shiny(0.35f));
            Prim(root, PrimitiveType.Cylinder, "Bottle", wc + Vector3.up * 1.2f, new Vector3(0.26f, 0.2f, 0.26f), new Color(0.25f, 0.45f, 0.7f), null, false, Surf.Shiny(0.85f));
            Box(root, "Tap", wc + new Vector3(0.17f, 0.85f, -0.06f), new Vector3(0.03f, 0.04f, 0.04f), new Color(0.2f, 0.35f, 0.8f), false);
            Box(root, "TapHot", wc + new Vector3(0.17f, 0.85f, 0.06f), new Vector3(0.03f, 0.04f, 0.04f), new Color(0.75f, 0.12f, 0.1f), false);
            Prim(root, PrimitiveType.Cylinder, "CupStack", wc + new Vector3(0.19f, 0.62f, 0.12f), new Vector3(0.06f, 0.08f, 0.06f), new Color(0.88f, 0.88f, 0.85f));

            // Perforated bin with crumpled papers, one ball missed (built by TrashBinProp, no probes like the rest).
            Vector3 bin = new Vector3(cx - 4.0f, y0, Z1 - 1.45f);
            TrashBinProp.Create(root, bin, 125f, 20 + lv, 1, false, false);

            // PC tower and UPS under the desk, power strip with a red switch.
            Vector3 pc = new Vector3(cx - 3.1f, y0, Z1 - 0.5f);
            Box(root, "PcTower", pc + Vector3.up * 0.22f, new Vector3(0.2f, 0.44f, 0.45f), new Color(0.1f, 0.1f, 0.11f), true, null, Surf.Shiny(0.3f));
            Led(root, "PcLed", pc + new Vector3(0.05f, 0.4f, -0.226f), new Vector3(0.01f, 0.01f, 0.004f), new Color(0.2f, 0.6f, 2f));
            _blinkLeds.Add((Led(root, "HddLed", pc + new Vector3(0.05f, 0.37f, -0.226f), new Vector3(0.01f, 0.006f, 0.004f), new Color(2f, 0.8f, 0.1f)).GetComponent<Renderer>(), 0.3f, new Color(2f, 0.8f, 0.1f)));
            Box(root, "Ups", pc + new Vector3(0.45f, 0.13f, 0.05f), new Vector3(0.17f, 0.26f, 0.38f), new Color(0.12f, 0.12f, 0.13f), true, null, Surf.Shiny(0.3f));
            Led(root, "UpsLed", pc + new Vector3(0.45f, 0.22f, -0.141f), new Vector3(0.012f, 0.012f, 0.004f), new Color(0.2f, 1.6f, 0.3f));
            Box(root, "PowerStrip", new Vector3(cx - 1.0f, y0 + 0.03f, Z1 - 0.3f), new Vector3(0.4f, 0.04f, 0.06f), new Color(0.85f, 0.85f, 0.82f), false);
            Led(root, "StripSwitch", new Vector3(cx - 1.17f, y0 + 0.055f, Z1 - 0.3f), new Vector3(0.03f, 0.012f, 0.03f), new Color(1.4f, 0.1f, 0.05f));
        }

        /// <summary>Office chair: five-star base on castors, gas lift, padded seat, reclined back, armrests.</summary>
        private void BuildOfficeChair(Transform root, Vector3 at, float yaw)
        {
            var chair = new GameObject("OfficeChair").transform;
            chair.SetParent(root, false);
            chair.SetPositionAndRotation(at, Quaternion.Euler(0f, 180f + yaw, 0f));   // faces the desk (north)
            var black = new Color(0.06f, 0.06f, 0.07f);
            var fabric = new Color(0.16f, 0.17f, 0.2f);
            var chrome = new Color(0.55f, 0.56f, 0.58f);
            for (int i = 0; i < 5; i++)
            {
                var q = Quaternion.Euler(0f, i * 72f, 0f);
                var leg = LocalPart(chair, PrimitiveType.Cube, q * new Vector3(0f, 0.085f, 0.17f), new Vector3(0.05f, 0.035f, 0.34f), black, Surf.Shiny(0.4f));
                leg.transform.localRotation = q * Quaternion.Euler(-6f, 0f, 0f);
                var wheel = LocalPart(chair, PrimitiveType.Cylinder, q * new Vector3(0f, 0.03f, 0.32f), new Vector3(0.055f, 0.02f, 0.055f), black, Surf.Shiny(0.5f));
                wheel.transform.localRotation = q * Quaternion.Euler(0f, 0f, 90f);
            }
            LocalPart(chair, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(0.09f, 0.03f, 0.09f), black, Surf.Shiny(0.4f));
            LocalPart(chair, PrimitiveType.Cylinder, new Vector3(0f, 0.26f, 0f), new Vector3(0.05f, 0.14f, 0.05f), chrome, Surf.Shiny(0.75f, 0.9f));
            LocalPart(chair, PrimitiveType.Cube, new Vector3(0f, 0.41f, 0f), new Vector3(0.24f, 0.03f, 0.24f), black, Surf.Shiny(0.3f, 0.5f));
            LocalPart(chair, PrimitiveType.Cube, new Vector3(0f, 0.46f, 0.01f), new Vector3(0.5f, 0.08f, 0.48f), fabric, Surf.T(SecurityTextures.Kind.Fabric, 0.25f));
            LocalPart(chair, PrimitiveType.Cube, new Vector3(0f, 0.5f, -0.21f), new Vector3(0.46f, 0.03f, 0.06f), fabric, Surf.T(SecurityTextures.Kind.Fabric, 0.25f));
            // Back: spine bar and a padded, slightly reclined backrest.
            var spine = LocalPart(chair, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0.27f), new Vector3(0.06f, 0.3f, 0.03f), black, Surf.Shiny(0.4f));
            spine.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            var back = LocalPart(chair, PrimitiveType.Cube, new Vector3(0f, 0.92f, 0.3f), new Vector3(0.46f, 0.55f, 0.07f), fabric, Surf.T(SecurityTextures.Kind.Fabric, 0.25f));
            back.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            var lumbar = LocalPart(chair, PrimitiveType.Cube, new Vector3(0f, 0.78f, 0.265f), new Vector3(0.4f, 0.12f, 0.04f), fabric, Surf.T(SecurityTextures.Kind.Fabric, 0.25f));
            lumbar.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                LocalPart(chair, PrimitiveType.Cube, new Vector3(0.24f * s, 0.56f, 0.03f), new Vector3(0.03f, 0.18f, 0.04f), black, Surf.Shiny(0.4f));
                LocalPart(chair, PrimitiveType.Cube, new Vector3(0.24f * s, 0.655f, -0.0f), new Vector3(0.07f, 0.03f, 0.26f), black, Surf.Shiny(0.35f));
            }
            var col = chair.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, 0.05f);
            col.size = new Vector3(0.55f, 1.0f, 0.55f);
        }

        /// <summary>Open steel shelf with stacked video recorders (blinking HDD LEDs, small green displays).</summary>
        private void BuildRecorderShelf(Transform root, Vector3 at)
        {
            var steel = new Color(0.3f, 0.31f, 0.33f);
            var metal = Surf.T(SecurityTextures.Kind.Metal, 0.6f, 0.35f, 0.7f);
            foreach (float sx in new[] { -0.42f, 0.42f })
            foreach (float sz in new[] { -0.2f, 0.2f })
                Box(root, "ShelfPost", at + new Vector3(sx, 1.0f, sz), new Vector3(0.035f, 2.0f, 0.035f), steel, true, null, metal);
            for (int s = 0; s < 5; s++)
            {
                float y = 0.12f + s * 0.44f;
                Box(root, "Shelf", at + new Vector3(0f, y, 0f), new Vector3(0.88f, 0.025f, 0.44f), steel, false, null, metal);
                if (s == 4) continue;
                int boxes = s == 0 ? 1 : 2;
                for (int b = 0; b < boxes; b++)
                {
                    float bh = s == 0 ? 0.24f : 0.075f;
                    Vector3 bp = at + new Vector3(0f, y + 0.013f + bh * 0.5f + b * (bh + 0.02f), -0.02f);
                    var body = s == 0 ? new Color(0.13f, 0.13f, 0.14f) : new Color(0.08f, 0.08f, 0.09f);
                    Box(root, s == 0 ? "UpsBig" : "Recorder", bp, new Vector3(0.8f, bh, 0.36f), body, false, null, Surf.Shiny(0.35f, 0.3f));
                    float fz = bp.z - 0.181f;
                    if (s == 0)
                    {
                        Led(root, "UpsPanel", new Vector3(bp.x + 0.25f, bp.y + 0.06f, fz), new Vector3(0.12f, 0.04f, 0.004f), new Color(0.1f, 0.5f, 0.2f));
                        continue;
                    }
                    Led(root, "RecDisplay", new Vector3(bp.x - 0.22f, bp.y, fz), new Vector3(0.14f, 0.03f, 0.004f), new Color(0.15f, 0.9f, 0.35f));
                    for (int l = 0; l < 4; l++)
                    {
                        var c = l == 3 ? new Color(1.8f, 0.2f, 0.1f) : new Color(0.2f, 1.6f, 0.3f);
                        var led = Led(root, "RecLed", new Vector3(bp.x + 0.08f + l * 0.05f, bp.y + 0.012f, fz), new Vector3(0.01f, 0.01f, 0.004f), c);
                        _blinkLeds.Add((led.GetComponent<Renderer>(), (s * 7 + b * 3 + l) * 0.37f, c));
                    }
                }
            }
            // Cable bundle from the shelf up into the tray.
            Prim(root, PrimitiveType.Cylinder, "CableBundle", at + new Vector3(0.3f, 2.4f, 0.15f), new Vector3(0.06f, 0.42f, 0.06f), new Color(0.05f, 0.05f, 0.05f));
        }

        private void BuildFilingCabinet(Transform root, Vector3 at)
        {
            var grey = new Color(0.45f, 0.47f, 0.46f);
            Box(root, "Cabinet", at + Vector3.up * 0.66f, new Vector3(0.5f, 1.32f, 0.46f), grey, true, null, Surf.T(SecurityTextures.Kind.Metal, 0.7f, 0.3f, 0.5f));
            for (int d = 0; d < 4; d++)
            {
                Vector3 dp = at + new Vector3(0.255f, 1.14f - d * 0.32f, 0f);
                Box(root, "CabDrawer", dp, new Vector3(0.012f, 0.29f, 0.42f), new Color(0.5f, 0.52f, 0.51f), false, null, Surf.Shiny(0.35f, 0.5f));
                Box(root, "CabHandle", dp + new Vector3(0.015f, 0.06f, 0f), new Vector3(0.02f, 0.025f, 0.16f), new Color(0.7f, 0.72f, 0.74f), false, null, Surf.Shiny(0.7f, 0.9f));
                Box(root, "CabLabel", dp + new Vector3(0.008f, -0.02f, 0f), new Vector3(0.004f, 0.04f, 0.09f), new Color(0.88f, 0.86f, 0.8f), false);
            }
            // Second drawer slightly open.
            Box(root, "CabDrawerOpen", at + new Vector3(0.3f, 0.82f, 0f), new Vector3(0.1f, 0.28f, 0.42f), new Color(0.5f, 0.52f, 0.51f), false, null, Surf.Shiny(0.35f, 0.5f));
            Prim(root, PrimitiveType.Cylinder, "CactusPot", at + new Vector3(0f, 1.38f, 0.1f), new Vector3(0.1f, 0.05f, 0.1f), new Color(0.6f, 0.3f, 0.18f));
            Prim(root, PrimitiveType.Capsule, "Cactus", at + new Vector3(0f, 1.49f, 0.1f), new Vector3(0.06f, 0.08f, 0.06f), new Color(0.15f, 0.35f, 0.15f));
        }

        /// <summary>Wall shelf with a row of lever-arch binders (spine labels, finger holes).</summary>
        private void BuildBinderShelf(Transform root, Vector3 at)
        {
            Box(root, "BinderShelf", at, new Vector3(0.3f, 0.025f, 1.5f), new Color(0.75f, 0.73f, 0.68f), false, null, Surf.T(SecurityTextures.Kind.Laminate, 0.6f, 0.3f));
            Box(root, "ShelfBracket", at + new Vector3(-0.05f, -0.08f, -0.6f), new Vector3(0.2f, 0.14f, 0.02f), new Color(0.3f, 0.3f, 0.32f), false);
            Box(root, "ShelfBracket", at + new Vector3(-0.05f, -0.08f, 0.6f), new Vector3(0.2f, 0.14f, 0.02f), new Color(0.3f, 0.3f, 0.32f), false);
            Color[] cols = { new(0.1f, 0.22f, 0.5f), new(0.55f, 0.08f, 0.07f), new(0.1f, 0.4f, 0.15f), new(0.1f, 0.1f, 0.1f), new(0.7f, 0.55f, 0.1f) };
            float z = at.z - 0.68f;
            for (int i = 0; i < 16; i++)
            {
                float w = 0.075f;
                bool lean = i == 15;
                var c = cols[(i * 3 + i / 4) % cols.Length];
                Vector3 bp = new Vector3(at.x + 0.02f, at.y + 0.17f, z + w * 0.5f);
                Box(root, "Binder", bp, new Vector3(0.28f, 0.31f, w - 0.004f), c, false, lean ? new Vector3(-18f, 0f, 0f) : null, Surf.Shiny(0.35f));
                Box(root, "BinderLabel", bp + new Vector3(0.141f, 0.05f, 0f), new Vector3(0.003f, 0.1f, w * 0.7f), new Color(0.9f, 0.9f, 0.86f), false, lean ? new Vector3(-18f, 0f, 0f) : null);
                Prim(root, PrimitiveType.Cylinder, "BinderHole", bp + new Vector3(0.142f, -0.08f, 0f), new Vector3(0.025f, 0.002f, 0.025f), new Color(0.03f, 0.03f, 0.03f), new Vector3(0f, 0f, 90f));
                z += lean ? w + 0.06f : w;
                if (i == 10) z += 0.12f;
            }
        }

        private void BuildLocker(Transform root, Vector3 at, int i)
        {
            var c = new Color(0.32f, 0.4f, 0.48f);
            Box(root, "Locker", at + Vector3.up * 0.92f, new Vector3(0.5f, 1.84f, 0.58f), c, true, null, Surf.T(SecurityTextures.Kind.Metal, 0.8f, 0.3f, 0.5f));
            Box(root, "LockerDoor", at + new Vector3(0.255f, 0.92f, 0f), new Vector3(0.012f, 1.76f, 0.52f), c * 1.08f, false, null, Surf.Shiny(0.35f, 0.5f));
            for (int s = 0; s < 4; s++)
                Box(root, "LockerVent", at + new Vector3(0.262f, 1.6f - s * 0.04f, 0f), new Vector3(0.006f, 0.012f, 0.3f), new Color(0.05f, 0.05f, 0.06f), false);
            Box(root, "LockerHandle", at + new Vector3(0.27f, 0.95f, 0.18f), new Vector3(0.02f, 0.12f, 0.03f), new Color(0.7f, 0.7f, 0.72f), false, null, Surf.Shiny(0.7f, 0.9f));
            Box(root, "LockerNumber", at + new Vector3(0.262f, 1.72f, 0.0f), new Vector3(0.004f, 0.05f, 0.08f), new Color(0.9f, 0.88f, 0.8f), false);
        }

        private void UpdateBlinkLeds()
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var (r, ph, c) in _blinkLeds)
            {
                if (r == null) continue;
                float k = Mathf.PerlinNoise(Time.time * 6f + ph * 10f, ph) > 0.55f ? 1f : 0.08f;
                mpb.SetColor("_Color", c * 0.3f);
                mpb.SetColor("_EmissionColor", c * k);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
