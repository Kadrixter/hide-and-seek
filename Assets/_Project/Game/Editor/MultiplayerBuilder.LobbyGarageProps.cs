using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Garage props (<see cref="LobbyLayout.Garage"/>): a pegboard with hand tools on the west wall (in place of the moon
    /// painting), a stack of old tyres in the south-east corner (in place of the plant) and a blue oil drum in the
    /// north-east corner.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        /// <summary>Pegboard centre on the west wall (the wall sconces stand 0.7 m either side of it).</summary>
        public static Vector3 GaragePegboard => new(-LobbyLayout.HalfW, 1.55f, 0.1f);
        public static readonly Vector3 GarageTyres = new(7.42f, 0f, -5.42f);
        public static readonly Vector3 GarageDrum = new(7.5f, 0f, 5.42f);

        private static void BuildGarageProps(Transform root)
        {
            var g = new GameObject("GarageProps").transform;
            g.SetParent(root, false);
            Pegboard(g, GaragePegboard);
            TyreStack(g, GarageTyres);
            OilDrum(g, GarageDrum);
        }

        /// <summary>Pegboard (1.0 x 0.8 m) on battens, tools hanging on pegs. Built facing +Z, turned to face the room (+X).</summary>
        private static void Pegboard(Transform parent, Vector3 wallPos)
        {
            var p = new GameObject("Pegboard").transform;
            p.SetParent(parent, false);
            var board = Mat("GaragePegboard", Color.white, PegboardTex(), 0.15f, new Vector2(2.5f, 2f));
            var batten = Mat("LobbyCrateBand", new Color(0.4f, 0.28f, 0.16f), Tex("lobby_wood", TexKind.Wood), 0.15f, new Vector2(0.3f, 0.3f));
            var chrome = Mat("LobbySilver", new Color(0.78f, 0.78f, 0.8f), null, 0.8f, Vector2.one, 0.9f);
            var wood = Mat("LobbyDarkWood", new Color(0.55f, 0.4f, 0.3f), Tex("lobby_wood", TexKind.Wood), 0.5f, new Vector2(1, 1));
            var red = Mat("GarageToolRed", new Color(0.7f, 0.08f, 0.06f), null, 0.5f, Vector2.one);
            var yellowGrip = Mat("GarageToolYellow", new Color(0.9f, 0.7f, 0.1f), null, 0.5f, Vector2.one);
            var black = Mat("GarageRubber", new Color(0.04f, 0.04f, 0.04f), null, 0.2f, Vector2.one);
            const float bw = 1.0f, bh = 0.8f, z0 = 0.03f;
            foreach (float y in new[] { -0.3f, 0.3f })
                LocalBox(p, "Batten", new Vector3(0f, y, 0.01f), new Vector3(bw - 0.04f, 0.05f, 0.02f), batten);
            LocalBox(p, "Board", new Vector3(0f, 0f, z0 - 0.003f), new Vector3(bw, bh, 0.006f), board);

            void Peg(float x, float y) => LocalBox(p, "Peg", new Vector3(x, y, z0 + 0.02f), new Vector3(0.006f, 0.006f, 0.04f), chrome);
            // six spanners, longest left
            for (int i = 0; i < 6; i++)
            {
                float len = 0.24f - i * 0.02f, x = -0.4f + i * 0.055f, yTop = 0.3f;
                Peg(x, yTop + 0.01f);
                LocalBox(p, "SpannerShaft", new Vector3(x, yTop - len * 0.5f, z0 + 0.006f), new Vector3(0.016f - i * 0.001f, len, 0.005f), chrome);
                var ring = Cyl("SpannerRing", p, Vector3.zero, 0.034f - i * 0.002f, 0.006f, chrome);
                ring.transform.localPosition = new Vector3(x, yTop - 0.004f, z0 + 0.006f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                foreach (float s in new[] { -1f, 1f })
                    LocalBox(p, "SpannerJaw", new Vector3(x + s * (0.012f - i * 0.001f), yTop - len - 0.008f, z0 + 0.006f), new Vector3(0.008f, 0.026f, 0.005f), chrome);
            }
            // hammer
            Peg(-0.04f, 0.31f); Peg(0.06f, 0.31f);
            LocalBox(p, "HammerHandle", new Vector3(0.01f, 0.13f, z0 + 0.014f), new Vector3(0.024f, 0.3f, 0.02f), wood);
            LocalBox(p, "HammerHead", new Vector3(0.01f, 0.29f, z0 + 0.016f), new Vector3(0.12f, 0.032f, 0.03f), Mat("GarageSteel", new Color(0.3f, 0.31f, 0.33f), null, 0.45f, Vector2.one, 0.6f));
            // screwdrivers in a rack
            LocalBox(p, "DriverRack", new Vector3(0.27f, 0.22f, z0 + 0.025f), new Vector3(0.26f, 0.015f, 0.05f), batten);
            for (int i = 0; i < 4; i++)
            {
                float x = 0.17f + i * 0.065f;
                var h = Cyl("DriverHandle", p, Vector3.zero, 0.026f, 0.09f, i % 2 == 0 ? red : yellowGrip);
                h.transform.localPosition = new Vector3(x, 0.275f, z0 + 0.025f);
                var sft = Cyl("DriverShaft", p, Vector3.zero, 0.006f, 0.13f - i * 0.015f, chrome);
                sft.transform.localPosition = new Vector3(x, 0.215f - (0.13f - i * 0.015f) * 0.5f + 0.01f, z0 + 0.025f);
            }
            // pliers: two crossed arms with red grips
            Peg(0.35f, -0.02f);
            foreach (float s in new[] { -1f, 1f })
            {
                var arm = LocalBox(p, "PliersArm", new Vector3(0.35f + s * 0.018f, -0.11f, z0 + 0.008f), new Vector3(0.012f, 0.19f, 0.006f), chrome);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, s * 7f);
                var grip = LocalBox(p, "PliersGrip", new Vector3(0.35f + s * 0.03f, -0.17f, z0 + 0.01f), new Vector3(0.018f, 0.1f, 0.01f), red);
                grip.transform.localRotation = Quaternion.Euler(0f, 0f, s * 7f);
            }
            // saw outline: a hand saw blade
            Peg(-0.3f, -0.05f);
            LocalBox(p, "SawBlade", new Vector3(-0.2f, -0.14f, z0 + 0.004f), new Vector3(0.36f, 0.09f, 0.002f), chrome).transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            LocalBox(p, "SawHandle", new Vector3(-0.39f, -0.11f, z0 + 0.008f), new Vector3(0.07f, 0.1f, 0.018f), wood);
            // tape roll on a peg
            Peg(0.08f, -0.05f);
            var tape = Cyl("DuctTape", p, Vector3.zero, 0.1f, 0.045f, Mat("GarageDuctTape", new Color(0.6f, 0.61f, 0.62f), null, 0.55f, Vector2.one, 0.3f));
            tape.transform.localPosition = new Vector3(0.08f, -0.11f, z0 + 0.025f);
            tape.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var hole = Cyl("DuctTapeCore", p, Vector3.zero, 0.07f, 0.046f, black);
            hole.transform.localPosition = new Vector3(0.08f, -0.11f, z0 + 0.025f);
            hole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            p.SetPositionAndRotation(wallPos, Quaternion.Euler(0f, 90f, 0f));
        }

        /// <summary>Four worn tyres, slightly off-centre and tilted, one collider for the stack.</summary>
        private static void TyreStack(Transform parent, Vector3 pos)
        {
            var t = new GameObject("TyreStack").transform;
            t.SetParent(parent, false);
            var rubber = Mat("GarageTyre", new Color(0.09f, 0.09f, 0.09f), Tex("garage_tread", TexKind.Fabric), 0.12f, new Vector2(30f, 2f));
            var m = new ProcMesh();
            for (int i = 0; i < 4; i++)
            {
                var off = new Vector3((Hash01(i * 7 + 1) - 0.5f) * 0.07f, 0.11f + i * 0.205f, (Hash01(i * 13 + 2) - 0.5f) * 0.07f);
                var rot = Quaternion.Euler((Hash01(i * 3) - 0.5f) * 4f, Hash01(i * 5) * 90f, (Hash01(i * 11) - 0.5f) * 4f);
                // squared-off torus: tread + sidewalls via a lathe profile
                m.At(off, rot).Lathe(new[]
                {
                    new Vector2(0.21f, -0.09f), new Vector2(0.27f, -0.1f), new Vector2(0.32f, -0.085f), new Vector2(0.335f, -0.05f),
                    new Vector2(0.335f, 0.05f), new Vector2(0.32f, 0.085f), new Vector2(0.27f, 0.1f), new Vector2(0.21f, 0.09f), new Vector2(0.2f, 0f), new Vector2(0.21f, -0.09f),
                }, 36, 50f);
            }
            var go = new GameObject("Tyres");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m.ToMesh("GarageTyres"), "GarageTyres");
            go.AddComponent<MeshRenderer>().sharedMaterial = rubber;
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.42f, 0f);
            col.height = 0.84f;
            col.radius = 0.34f;
            t.position = pos;
            t.gameObject.isStatic = true;
        }

        /// <summary>200 l steel drum (blue, rolling hoops, two bungs), oil running down from the top.</summary>
        private static void OilDrum(Transform parent, Vector3 pos)
        {
            var d = new GameObject("OilDrum").transform;
            d.SetParent(parent, false);
            var paint = Mat("GarageDrum", new Color(0.1f, 0.24f, 0.5f), Tex("garage_drum", TexKind.Concrete), 0.4f, new Vector2(2f, 1f), 0.4f);
            const float r = 0.29f, h = 0.88f;
            var prof = new System.Collections.Generic.List<Vector2> { new(0f, 0.005f), new(r - 0.02f, 0.005f), new(r - 0.01f, 0f), new(r, 0.02f) };
            foreach (float hy in new[] { 0.3f, 0.58f })
            {
                prof.Add(new Vector2(r, hy - 0.025f)); prof.Add(new Vector2(r + 0.012f, hy - 0.012f));
                prof.Add(new Vector2(r + 0.012f, hy + 0.012f)); prof.Add(new Vector2(r, hy + 0.025f));
            }
            prof.Add(new Vector2(r, h - 0.02f)); prof.Add(new Vector2(r - 0.008f, h)); prof.Add(new Vector2(r - 0.02f, h - 0.015f)); prof.Add(new Vector2(0f, h - 0.015f));
            var m = new ProcMesh();
            m.At(Vector3.zero).Lathe(prof.ToArray(), 40, 35f);
            var go = new GameObject("Drum");
            go.transform.SetParent(d, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m.ToMesh("GarageDrum"), "GarageDrum");
            go.AddComponent<MeshRenderer>().sharedMaterial = paint;
            var steel = Mat("GarageSteel", new Color(0.3f, 0.31f, 0.33f), null, 0.45f, Vector2.one, 0.6f);
            foreach (var (bx, bz, bd) in new[] { (0.16f, 0.05f, 0.07f), (-0.17f, -0.04f, 0.045f) })
            {
                var b = Cyl("Bung", d, Vector3.zero, bd, 0.016f, steel);
                b.transform.localPosition = new Vector3(bx, h - 0.008f, bz);
            }
            // oil: a dark puddle on the lid and a run down the side
            var oil = Mat("GarageOil", new Color(0.03f, 0.025f, 0.02f), null, 0.85f, Vector2.one, 0.1f);
            var puddle = Cyl("OilOnLid", d, Vector3.zero, 0.2f, 0.002f, oil);
            puddle.transform.localPosition = new Vector3(0.05f, h - 0.012f, 0.06f);
            puddle.transform.localScale = new Vector3(0.22f, 0.001f, 0.15f);
            var run = LocalBox(d, "OilRun", new Vector3(0f, h - 0.25f, -r - 0.002f), new Vector3(0.05f, 0.42f, 0.003f), oil);
            run.isStatic = false;
            var floorOil = Cyl("OilOnFloor", d, Vector3.zero, 0.5f, 0.002f, oil);
            floorOil.transform.localPosition = new Vector3(-0.15f, 0.002f, -0.3f);
            floorOil.transform.localScale = new Vector3(0.55f, 0.001f, 0.32f);
            var col = d.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.height = h;
            col.radius = r + 0.01f;
            d.position = pos;
            d.rotation = Quaternion.Euler(0f, 200f, 0f);
            d.gameObject.isStatic = true;
        }
    }
}
