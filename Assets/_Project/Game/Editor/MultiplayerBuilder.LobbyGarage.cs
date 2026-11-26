using System.Collections.Generic;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Garage look of the lobby room (<see cref="LobbyLayout.Garage"/>): the shell boxes of BuildLobbyRoom get the
    /// concrete floor, block walls and a concrete ceiling; steel I-beams cross under the ceiling (east - west, clear of
    /// the lamps, the fan and the pool / chess lamps), grey conduit runs along the south and east walls, the wall
    /// sockets sit on surface boxes, the ceiling lights are hanging twin-tube shop lights. The wall finish of the
    /// classic look (wainscoting, chair rail, crown) is not built. The door: LobbyGarageDoor, props: LobbyGarageProps,
    /// textures: LobbyGarageArt.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        /// <summary>Beam lines (world z) under the ceiling.</summary>
        private static readonly float[] GarageBeamZ = { -4.4f, -1.5f, 1.35f, 4.35f };

        private static Material GarageSteel => Mat("GarageSteel", new Color(0.3f, 0.31f, 0.33f), null, 0.45f, Vector2.one, 0.6f);
        private static Material GarageBeamPaint => Mat("GarageBeam", new Color(0.32f, 0.12f, 0.08f), Tex("garage_ceiling", TexKind.Concrete), 0.25f, new Vector2(6f, 1f), 0.3f);
        private static Material GarageConduit => Mat("GarageConduit", new Color(0.55f, 0.56f, 0.57f), null, 0.5f, Vector2.one, 0.7f);

        /// <summary>Re-skins the shell (floor, ceiling, four walls) built by BuildLobbyRoom and adds beams and conduit.</summary>
        private static void BuildGarageShell(Transform root)
        {
            float W = LobbyLayout.W, D = LobbyLayout.D;
            var floor = root.Find("WoodFloor");
            if (floor != null)
            {
                floor.name = "ConcreteFloor";
                floor.GetComponent<MeshRenderer>().sharedMaterial = GarageFloorMat();
                GarageFloorUv(floor); // one texture over the whole floor (LobbyGarageFloor.cs)
                floor.GetComponent<FootstepSurface>().Type = SurfaceType.Concrete;
            }
            var ceiling = root.Find("Ceiling");
            if (ceiling != null) ceiling.GetComponent<MeshRenderer>().sharedMaterial = GarageCeilingMat();
            var ns = GarageWallMat("GarageWallNS", W + 0.4f);
            var ew = GarageWallMat("GarageWallEW", D);
            foreach (var (n, m) in new[] { ("WallN", ns), ("WallS", ns), ("WallE", ew), ("WallW", ew) })
            {
                var w = root.Find(n);
                if (w != null) w.GetComponent<MeshRenderer>().sharedMaterial = m;
            }

            var g = new GameObject("GarageShell").transform;
            g.SetParent(root, false);
            GarageBeams(g);
            GarageConduitRuns(g);
        }

        /// <summary>I-beams (0.12 wide, 0.22 deep) wall to wall, with end plates bolted to the walls.</summary>
        private static void GarageBeams(Transform g)
        {
            float H = LobbyLayout.H, hw = LobbyLayout.HalfW;
            const float depth = 0.22f, flange = 0.13f, tf = 0.016f, web = 0.012f;
            var m = new ProcMesh();
            foreach (float z in GarageBeamZ)
            {
                float len = hw * 2f;
                m.At(Vector3.zero).Box(new Vector3(0f, H - tf * 0.5f, z), new Vector3(len, tf, flange));
                m.At(Vector3.zero).Box(new Vector3(0f, H - depth + tf * 0.5f, z), new Vector3(len, tf, flange));
                m.At(Vector3.zero).Box(new Vector3(0f, H - depth * 0.5f, z), new Vector3(len, depth - 2f * tf, web));
                for (int s = -1; s <= 1; s += 2)
                    m.At(Vector3.zero).Box(new Vector3(s * (hw - 0.006f), H - depth * 0.5f - 0.02f, z), new Vector3(0.012f, depth + 0.1f, flange + 0.08f));
            }
            var go = new GameObject("Beams");
            go.transform.SetParent(g, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m.ToMesh("GarageBeams"), "GarageBeams");
            go.AddComponent<MeshRenderer>().sharedMaterial = GarageBeamPaint;
            go.isStatic = true;
            // bolt heads on the end plates
            var bolts = new ProcMesh();
            foreach (float z in GarageBeamZ)
            for (int s = -1; s <= 1; s += 2)
            foreach (float dy in new[] { -0.09f, 0.05f })
            foreach (float dz in new[] { -0.08f, 0.08f })
                bolts.At(new Vector3(s * (hw - 0.014f), H - depth * 0.5f + dy, z + dz), Quaternion.Euler(0f, 0f, 90f))
                    .Lathe(new[] { new Vector2(0f, -0.006f), new Vector2(0.013f, -0.006f), new Vector2(0.013f, 0.006f), new Vector2(0f, 0.006f) }, 6, 10f);
            var bg = new GameObject("BeamBolts");
            bg.transform.SetParent(g, false);
            bg.AddComponent<MeshFilter>().sharedMesh = SaveMesh(bolts.ToMesh("GarageBeamBolts"), "GarageBeamBolts");
            bg.AddComponent<MeshRenderer>().sharedMaterial = GarageSteel;
        }

        /// <summary>Grey conduit 0.3 m under the ceiling along the south and east walls, on stand-off clips.</summary>
        private static void GarageConduitRuns(Transform g)
        {
            float hw = LobbyLayout.HalfW, hd = LobbyLayout.HalfD, y = LobbyLayout.H - 0.32f;
            const float r = 0.022f, off = 0.045f;
            var m = new ProcMesh();
            // south wall, west -> east, bends up the east wall
            var path = new List<Vector3>
            {
                new(-hw + 0.02f, y, -hd + off), new(hw - off - 0.1f, y, -hd + off), new(hw - off, y, -hd + off + 0.1f), new(hw - off, y, hd - 0.02f),
            };
            m.Tube(path, r, 10, true);
            // a second, thinner one under it on the south wall
            m.Tube(new List<Vector3> { new(-hw + 0.02f, y - 0.07f, -hd + off), new(hw - 0.02f, y - 0.07f, -hd + off) }, r * 0.7f, 8, true);
            var clips = new ProcMesh();
            for (float x = -hw + 0.8f; x < hw - 0.3f; x += 1.4f)
                clips.At(Vector3.zero).Box(new Vector3(x, y - 0.035f, -hd + off * 0.5f), new Vector3(0.03f, 0.14f, off));
            for (float z = -hd + 1.2f; z < hd - 0.3f; z += 1.4f)
                clips.At(Vector3.zero).Box(new Vector3(hw - off * 0.5f, y, z), new Vector3(off, 0.06f, 0.03f));
            var go = new GameObject("Conduit");
            go.transform.SetParent(g, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m.ToMesh("GarageConduit"), "GarageConduit");
            go.AddComponent<MeshRenderer>().sharedMaterial = GarageConduit;
            var cg = new GameObject("ConduitClips");
            cg.transform.SetParent(g, false);
            cg.AddComponent<MeshFilter>().sharedMesh = SaveMesh(clips.ToMesh("GarageConduitClips"), "GarageConduitClips");
            cg.AddComponent<MeshRenderer>().sharedMaterial = GarageSteel;
        }

        /// <summary>
        /// Wall pieces cut out of a wall box (exit door) keep the whole wall's material: give them UVs from their world
        /// position (u = x over <paramref name="lenU"/> m, v = height over the room height) so the blocks and the paint
        /// bands line up with the rest of the wall.
        /// </summary>
        private static void GarageWorldUv(Transform root, string[] names, float lenU)
        {
            foreach (var name in names)
            {
                var t = root.Find(name);
                var mf = t != null ? t.GetComponent<MeshFilter>() : null;
                if (mf == null) continue;
                var m = Object.Instantiate(mf.sharedMesh);
                var v = m.vertices;
                var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 w = t.TransformPoint(v[i]);
                    uv[i] = new Vector2((w.x + lenU * 0.5f) / lenU, w.y / LobbyLayout.H);
                }
                m.uv = uv;
                m.RecalculateTangents();
                mf.sharedMesh = SaveMesh(m, "Garage" + name);
            }
        }

        /// <summary>Surface-mounted box behind a wall socket plate (no wainscot board in the garage).</summary>
        private static void GarageOutletBox(Transform fin, Vector3 plate, Vector3 normal)
        {
            var box = Deco("OutletBox", fin, plate - normal * 0.009f, new Vector3(0.1f, 0.1f, 0.02f), GarageConduit, Quaternion.LookRotation(-normal));
            RoundCube(box.transform, 0.006f, 0f);
            // conduit drop from the box to the floor
            Deco("OutletPipe", fin, new Vector3(plate.x, plate.y * 0.5f - 0.03f, plate.z) - normal * 0.007f, new Vector3(0.024f, plate.y - 0.05f, 0.024f), GarageConduit);
        }

        /// <summary>Hanging twin-tube shop light (replaces the brass dome); the tube glow goes to <paramref name="glows"/>.</summary>
        private static void GarageShopLight(Transform c, List<Renderer> glows)
        {
            const float drop = 0.42f, len = 1.25f;
            var housing = Mat("GarageLampHousing", new Color(0.82f, 0.83f, 0.8f), null, 0.5f, Vector2.one, 0.3f);
            var tube = Emissive("GarageLampTube", new Color(1f, 0.98f, 0.94f), new Color(1f, 0.93f, 0.8f) * 1.9f);
            var hb = LocalBox(c, "Housing", new Vector3(0f, -drop, 0f), new Vector3(len, 0.06f, 0.2f), housing);
            hb.isStatic = false;
            RoundCube(hb.transform, 0.012f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                var lip = LocalBox(c, "Reflector", new Vector3(0f, -drop - 0.04f, s * 0.095f), new Vector3(len, 0.05f, 0.008f), housing);
                lip.transform.localRotation = Quaternion.Euler(s * 25f, 0f, 0f);
                lip.isStatic = false;
                var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                t.name = "Tube";
                Object.DestroyImmediate(t.GetComponent<Collider>());
                t.transform.SetParent(c, false);
                t.transform.localPosition = new Vector3(0f, -drop - 0.05f, s * 0.045f);
                t.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                t.transform.localScale = new Vector3(0.034f, len * 0.5f - 0.04f, 0.034f);
                var tr = t.GetComponent<MeshRenderer>();
                tr.sharedMaterial = tube;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                glows.Add(tr);
                // chain: a few links from the ceiling hook to the housing
                for (int k = 0; k < 7; k++)
                {
                    float y = -0.02f - k * (drop - 0.05f) / 7f;
                    var link = LocalBox(c, "Chain", new Vector3(s * 0.5f, y - 0.025f, 0f), new Vector3(0.008f, 0.05f, k % 2 == 0 ? 0.02f : 0.006f), GarageSteel);
                    link.isStatic = false;
                }
                var hook = LocalBox(c, "Hook", new Vector3(s * 0.5f, -0.01f, 0f), new Vector3(0.05f, 0.02f, 0.05f), GarageSteel);
                hook.isStatic = false;
            }
        }
    }
}
