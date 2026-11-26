using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Garage lamps (<see cref="LobbyLayout.Garage"/>): a black pipe floor lamp on a brake disc with an enamel shade
    /// leaning over the sofa corner, a yellow twin-head halogen work light on a tripod at the end of the front sofa,
    /// caged bulkhead lights instead of the brass sconces, and a side table of two tyres with a plywood top carrying a
    /// caged pipe lamp. Light colours / ranges stay close to the classic lamps they replace.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static readonly Vector2[] GarageTyreProfile =
        {
            new(0.21f, -0.09f), new(0.27f, -0.1f), new(0.32f, -0.085f), new(0.335f, -0.05f),
            new(0.335f, 0.05f), new(0.32f, 0.085f), new(0.27f, 0.1f), new(0.21f, 0.09f), new(0.2f, 0f), new(0.21f, -0.09f),
        };

        /// <summary>
        /// Tyre (lathe of <see cref="GarageTyreProfile"/>, axis = local Y) with tread blocks: two staggered centre rows
        /// angled into a chevron, a centre groove between them and shoulder lugs on the rounded edges.
        /// </summary>
        private static ProcMesh TyreWithTread(ProcMesh m, Vector3 off, Quaternion rot, int sides)
        {
            m.At(off, rot).Lathe(GarageTyreProfile, sides, 50f);
            const int n = 44;
            for (int i = 0; i < n; i++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    // centre rows: half a pitch apart, tilted opposite ways (chevron)
                    float a = (i + (side > 0 ? 0.5f : 0f)) / n * 360f;
                    Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
                    m.At(off + rot * (dir * 0.339f + Vector3.up * (side * 0.024f)), rot * Quaternion.Euler(0f, -a, 0f) * Quaternion.Euler(side * 16f, 0f, 0f))
                        .Box(Vector3.zero, new Vector3(0.012f, 0.03f, 0.034f));
                    // shoulder lugs, following the curve of the sidewall
                    float b = (i + 0.25f) / n * 360f;
                    Vector3 d2 = new Vector3(Mathf.Cos(b * Mathf.Deg2Rad), 0f, Mathf.Sin(b * Mathf.Deg2Rad));
                    m.At(off + rot * (d2 * 0.332f + Vector3.up * (side * 0.062f)), rot * Quaternion.Euler(0f, -b, 0f) * Quaternion.Euler(0f, 0f, side * 28f))
                        .Box(Vector3.zero, new Vector3(0.012f, 0.024f, 0.026f));
                }
            }
            return m;
        }

        private static Material GarageBlackSteel => Mat("GarageBlackSteel", new Color(0.06f, 0.06f, 0.065f), null, 0.42f, Vector2.one, 0.55f);
        private static Material GarageWorkYellow => Mat("GarageWorkYellow", new Color(0.92f, 0.66f, 0.05f), null, 0.55f, Vector2.one, 0.1f);
        private static Material GarageTyreRubber => Mat("GarageTyre", new Color(0.09f, 0.09f, 0.09f), Tex("garage_tread", TexKind.Fabric), 0.12f, new Vector2(30f, 2f));
        private static Material GarageBulb => Emissive("GarageBulbAmber", new Color(1f, 0.86f, 0.6f), new Color(1f, 0.66f, 0.32f) * 3f);

        private static GameObject GMeshObj(string name, Transform parent, ProcMesh pm, string key, Material m, Vector3 local, Quaternion? rot = null, bool shadows = true) =>
            MeshObj(name, parent, SaveMesh(pm.ToMesh(key), key), m, local, rot, shadows);

        private static Light LampLight(Transform parent, Vector3 local, Color c, float range, float intensity)
        {
            var l = new GameObject("LampLight").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = local;
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            return l;
        }

        /// <summary>Black pipe floor lamp: brake-disc base, pipe with couplings bending over at 1.55 m towards <paramref name="aim"/>, green enamel shade.</summary>
        private static void GaragePipeFloorLamp(Transform parent, Vector3 pos, Vector3 aim)
        {
            var p = new GameObject("FloorLamp").transform;
            p.SetParent(parent, false);
            var dir = aim - pos;
            dir.y = 0f;
            p.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.normalized));
            var disc = Mat("GarageBrakeDisc", new Color(0.45f, 0.4f, 0.36f), Tex("garage_ceiling", TexKind.Concrete), 0.4f, new Vector2(0.5f, 0.5f), 0.6f);
            GMeshObj("Base", p, new ProcMesh().At(Vector3.zero).Lathe(new[]
            {
                new Vector2(0.035f, 0f), new Vector2(0.15f, 0f), new Vector2(0.15f, 0.022f), new Vector2(0.09f, 0.022f), new Vector2(0.085f, 0.026f),
                new Vector2(0.085f, 0.07f), new Vector2(0.035f, 0.072f), new Vector2(0.035f, 0f),
            }, 40, 35f), "GarageBrakeDisc", disc, Vector3.zero);
            const float r = 0.016f, reach = 0.36f, top = 1.56f;
            var pipe = new ProcMesh().At(Vector3.zero).Tube(new List<Vector3>
            {
                new(0f, 0.07f, 0f), new(0f, top - 0.08f, 0f), new(0f, top - 0.025f, 0.02f), new(0f, top, 0.07f), new(0f, top, reach - 0.06f), new(0f, top - 0.03f, reach - 0.01f), new(0f, top - 0.07f, reach),
            }, r, 12, true);
            foreach (float y in new[] { 0.62f, 1.1f })
                pipe.At(new Vector3(0f, y, 0f)).Lathe(new[] { new Vector2(r + 0.004f, -0.025f), new Vector2(r + 0.006f, -0.02f), new Vector2(r + 0.006f, 0.02f), new Vector2(r + 0.004f, 0.025f) }, 8, 30f);
            GMeshObj("Pipe", p, pipe, "GaragePipeLamp", GarageBlackSteel, Vector3.zero);
            // enamel shade: green outside, white inside
            var shadeAt = new Vector3(0f, top - 0.08f, reach);
            var outside = Mat("GarageEnamelGreen", new Color(0.08f, 0.22f, 0.15f), null, 0.75f, Vector2.one);
            var inside = Emissive("GarageEnamelInside", new Color(0.92f, 0.9f, 0.84f), new Color(1f, 0.8f, 0.55f) * 0.35f);
            Vector2[] shell = { new(0.2f, -0.14f), new(0.17f, -0.09f), new(0.1f, -0.03f), new(0.05f, 0f), new(0.025f, 0.01f), new(0.025f, 0.035f) };
            GMeshObj("Shade", p, new ProcMesh().At(Vector3.zero).Lathe(shell, 32, 50f), "GarageEnamelShade", outside, shadeAt);
            var inner = new Vector2[shell.Length];
            for (int i = 0; i < shell.Length; i++) inner[i] = shell[shell.Length - 1 - i] + new Vector2(-0.003f, -0.002f);
            GMeshObj("ShadeInside", p, new ProcMesh().At(Vector3.zero).Lathe(inner, 32, 50f), "GarageEnamelShadeIn", inside, shadeAt, null, false);
            MeshObj("Bulb", p, LatheSaved("GarageEdisonBulb", new[] { new Vector2(0f, -0.11f), new Vector2(0.025f, -0.1f), new Vector2(0.034f, -0.07f), new Vector2(0.02f, -0.03f), new Vector2(0.013f, 0f) }, 16, false, false), GarageBulb, shadeAt, null, false);
            var col = p.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.15f;
            col.height = 1.6f;
            col.center = new Vector3(0f, 0.8f, 0.05f);
            LampLight(p, shadeAt + new Vector3(0f, -0.14f, 0f), new Color(1f, 0.72f, 0.45f), 4.6f, 1.25f);
        }

        /// <summary>Tripod work light: three legs, telescopic mast, T-bar with two yellow halogen heads tipped 18 degrees down towards <paramref name="aim"/>.</summary>
        private static void GarageWorkLight(Transform parent, Vector3 pos, Vector3 aim)
        {
            var p = new GameObject("FloorLamp").transform;
            p.SetParent(parent, false);
            var dir = aim - pos;
            dir.y = 0f;
            p.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.normalized));
            var frame = new ProcMesh();
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 60f) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Sin(a) * 0.38f, 0.015f, Mathf.Cos(a) * 0.38f);
                frame.At(Vector3.zero).Tube(new List<Vector3> { foot, new(Mathf.Sin(a) * 0.035f, 0.78f, Mathf.Cos(a) * 0.035f) }, 0.011f, 8, true);
                frame.At(foot + new Vector3(0f, -0.005f, 0f)).Box(Vector3.zero, new Vector3(0.04f, 0.02f, 0.06f));
                // braces from the legs to the mast
                frame.At(Vector3.zero).Tube(new List<Vector3> { Vector3.Lerp(foot, new Vector3(0f, 0.78f, 0f), 0.45f), new(0f, 0.5f, 0f) }, 0.006f, 6, true);
            }
            frame.At(Vector3.zero).Tube(new List<Vector3> { new(0f, 0.45f, 0f), new(0f, 1.25f, 0f) }, 0.02f, 12, true);
            frame.At(Vector3.zero).Tube(new List<Vector3> { new(0f, 1.2f, 0f), new(0f, 1.66f, 0f) }, 0.014f, 10, true);
            frame.At(Vector3.zero).Tube(new List<Vector3> { new(-0.26f, 1.66f, 0f), new(0.26f, 1.66f, 0f) }, 0.012f, 8, true);
            frame.At(new Vector3(0f, 1.22f, 0f)).Lathe(new[] { new Vector2(0.026f, -0.02f), new Vector2(0.03f, -0.015f), new Vector2(0.03f, 0.02f), new Vector2(0.026f, 0.025f) }, 10, 30f);
            GMeshObj("Tripod", p, frame, "GarageWorkTripod", GarageBlackSteel, Vector3.zero);
            var glass = Emissive("GarageHalogenGlass", new Color(1f, 0.95f, 0.82f), new Color(1f, 0.86f, 0.62f) * 2.4f);
            foreach (float sx in new[] { -0.17f, 0.17f })
            {
                var head = new GameObject("Head").transform;
                head.SetParent(p, false);
                head.localPosition = new Vector3(sx, 1.76f, 0.01f);
                head.localRotation = Quaternion.Euler(18f, sx * 40f, 0f);
                RBox(head, "Housing", new Vector3(0f, 0f, -0.02f), new Vector3(0.2f, 0.16f, 0.09f), GarageWorkYellow, 0.012f);
                for (int k = 0; k < 4; k++)
                    LocalBox(head, "Fin", new Vector3(-0.06f + k * 0.04f, 0f, -0.075f), new Vector3(0.008f, 0.13f, 0.03f), GarageWorkYellow);
                LocalBox(head, "Glass", new Vector3(0f, 0f, 0.026f), new Vector3(0.17f, 0.13f, 0.004f), glass).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                for (int k = 0; k < 3; k++)
                    LocalBox(head, "Grille", new Vector3(0f, -0.04f + k * 0.04f, 0.033f), new Vector3(0.18f, 0.006f, 0.006f), GarageBlackSteel);
                foreach (float bx in new[] { -0.105f, 0.105f })
                    LocalBox(head, "Bracket", new Vector3(bx, -0.06f, -0.02f), new Vector3(0.008f, 0.12f, 0.025f), GarageBlackSteel);
            }
            var col = p.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.2f;
            col.height = 1.85f;
            col.center = new Vector3(0f, 0.92f, 0f);
            LampLight(p, new Vector3(0f, 1.62f, 0.28f), new Color(1f, 0.8f, 0.58f), 4.6f, 1.25f);
        }

        /// <summary>Caged bulkhead light on a wall (local +Z = out of the wall): cast body, amber glass dome, three bars and a ring.</summary>
        private static void GarageCageSconce(Transform parent, Vector3 wallPos, float facingYaw)
        {
            var s = new GameObject("Sconce").transform;
            s.SetParent(parent, false);
            s.SetPositionAndRotation(wallPos, Quaternion.Euler(0f, facingYaw, 0f));
            var toZ = Quaternion.Euler(90f, 0f, 0f); // lathe axis (Y) out of the wall
            var body = new ProcMesh();
            body.At(Vector3.zero, toZ).Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.1f, 0f), new Vector2(0.1f, 0.025f), new Vector2(0.092f, 0.035f), new Vector2(0.07f, 0.036f) }, 28, 35f);
            body.At(new Vector3(0f, 0f, 0.035f), toZ).Torus(0.085f, 0.007f, 28, 6);
            for (int i = 0; i < 3; i++)
            {
                float phi = i * 60f * Mathf.Deg2Rad;
                var pts = new List<Vector3>();
                for (int k = -6; k <= 6; k++)
                {
                    float th = k / 6f * 80f * Mathf.Deg2Rad;
                    float rr = 0.088f * Mathf.Sin(th);
                    pts.Add(new Vector3(Mathf.Cos(phi) * rr, Mathf.Sin(phi) * rr, 0.035f + 0.085f * Mathf.Cos(th)));
                }
                body.At(Vector3.zero).Tube(pts, 0.005f, 6, true);
            }
            GMeshObj("Body", s, body, "GarageCageSconce", GarageBlackSteel, Vector3.zero);
            var amber = Emissive("GarageSconceGlass", new Color(1f, 0.82f, 0.55f), new Color(1f, 0.64f, 0.3f) * 1.5f);
            GMeshObj("Glass", s, new ProcMesh().At(new Vector3(0f, 0f, 0.035f), toZ).Lathe(new[] { new Vector2(0.075f, 0f), new Vector2(0.074f, 0.03f), new Vector2(0.06f, 0.06f), new Vector2(0.035f, 0.075f), new Vector2(0f, 0.08f) }, 24, 60f),
                "GarageSconceGlass", amber, Vector3.zero, null, false);
            LampLight(s, new Vector3(0f, 0f, 0.2f), new Color(1f, 0.72f, 0.45f), 2.8f, 0.75f);
        }

        /// <summary>Side table: two tyres with a plywood disc (top at 0.42 m), a caged pipe lamp, a mug and a rag on it.</summary>
        private static void GarageTyreTable(Transform parent, Vector3 pos)
        {
            var t = new GameObject("SideTable").transform;
            t.SetParent(parent, false);
            t.position = pos;
            var tyres = new ProcMesh();
            for (int i = 0; i < 2; i++)
                TyreWithTread(tyres, new Vector3((Hash01(i + 40) - 0.5f) * 0.04f, 0.1f + i * 0.2f, 0f), Quaternion.Euler(0f, i * 37f, (Hash01(i + 41) - 0.5f) * 2f), 36);
            GMeshObj("Tyres", t, tyres, "GarageTyreTableTread", GarageTyreRubber, Vector3.zero);
            var ply = Mat("GaragePlywood", new Color(0.86f, 0.74f, 0.56f), Tex("lobby_wood", TexKind.Wood), 0.3f, new Vector2(0.5f, 0.5f));
            MeshObj("Top", t, LatheSaved("GarageTyreTableTop", new[] { new Vector2(0f, 0f), new Vector2(0.36f, 0f), new Vector2(0.36f, 0.02f), new Vector2(0f, 0.02f) }, 40, true, true), ply, new Vector3(0f, 0.4f, 0f));
            BoxCollider(t, new Vector3(0f, 0.21f, 0f), new Vector3(0.68f, 0.42f, 0.68f));
            // caged pipe lamp: floor flange, short pipe, socket, bulb in a wire cage
            var lamp = new GameObject("TableLamp").transform;
            lamp.SetParent(t, false);
            lamp.localPosition = new Vector3(-0.08f, 0.42f, -0.06f);
            var pm = new ProcMesh();
            pm.At(Vector3.zero).Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.055f, 0f), new Vector2(0.055f, 0.012f), new Vector2(0.022f, 0.016f), new Vector2(0.02f, 0.03f), new Vector2(0f, 0.03f) }, 20, 35f);
            pm.At(Vector3.zero).Tube(new List<Vector3> { new(0f, 0.02f, 0f), new(0f, 0.2f, 0f) }, 0.012f, 10, true);
            pm.At(new Vector3(0f, 0.2f, 0f)).Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.022f, 0f), new Vector2(0.022f, 0.045f), new Vector2(0.016f, 0.05f), new Vector2(0f, 0.05f) }, 16, 35f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                var pts = new List<Vector3>();
                for (int k = 0; k <= 6; k++)
                {
                    float th = k / 6f * 160f * Mathf.Deg2Rad;
                    float rr = 0.045f * Mathf.Sin(th) + 0.012f;
                    pts.Add(new Vector3(Mathf.Cos(a) * rr, 0.245f + 0.06f * (1f - Mathf.Cos(th)), Mathf.Sin(a) * rr));
                }
                pm.At(Vector3.zero).Tube(pts, 0.003f, 5, true);
            }
            pm.At(new Vector3(0f, 0.3f, 0f)).Torus(0.057f, 0.003f, 20, 4);
            GMeshObj("Pipe", lamp, pm, "GaragePipeTableLamp", GarageBlackSteel, Vector3.zero);
            MeshObj("Bulb", lamp, LatheSaved("GarageEdisonBulbUp", new[] { new Vector2(0.013f, 0f), new Vector2(0.02f, 0.03f), new Vector2(0.034f, 0.07f), new Vector2(0.025f, 0.1f), new Vector2(0f, 0.11f) }, 16, false, false), GarageBulb, new Vector3(0f, 0.25f, 0f), null, false);
            LampLight(lamp, new Vector3(0f, 0.33f, 0f), new Color(1f, 0.7f, 0.42f), 2.6f, 0.9f);
            MeshObj("Mug", t, LatheSaved("Mug", new[] { new Vector2(0.035f, 0f), new Vector2(0.04f, 0.09f), new Vector2(0.034f, 0.09f), new Vector2(0.03f, 0.012f), new Vector2(0f, 0.012f) }, 20, true, false),
                Mat("GarageMugEnamel", new Color(0.86f, 0.86f, 0.82f), null, 0.7f, Vector2.one), new Vector3(0.16f, 0.42f, 0.12f));
            var rag = RBox(t, "Rag", new Vector3(0.12f, 0.428f, -0.14f), new Vector3(0.22f, 0.012f, 0.16f), GrimyFabric("GarageRag", new Color(0.55f, 0.12f, 0.1f), 0, 33, Vector2.one), 0.005f, 0.004f);
            rag.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        }
    }
}
