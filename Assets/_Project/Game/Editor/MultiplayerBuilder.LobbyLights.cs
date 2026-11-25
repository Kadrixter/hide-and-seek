using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Lobby lights: brass flush-mount ceiling lights with frosted domes (on the light switch by the exit door,
    /// <see cref="LobbyMainLights"/>), two floor lamps with fabric drum shades, a table lamp on the side table, wall
    /// sconces with tulip glass, the ceiling fan (wooden paddles, bronze motor, light bowl, on its own wall switch).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static readonly Color WarmLight = new(1f, 0.78f, 0.52f);

        private static Material Brass => Mat("LobbyBrass", new Color(0.78f, 0.6f, 0.3f), null, 0.72f, Vector2.one, 0.85f);
        private static Material Bronze => Mat("LobbyBronze", new Color(0.28f, 0.2f, 0.14f), null, 0.55f, Vector2.one, 0.7f);

        private static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material m, Vector3 local, Quaternion? rot = null, bool shadows = true)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            if (rot.HasValue) g.transform.localRotation = rot.Value;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        private static Mesh LatheSaved(string key, Vector2[] prof, int seg = 32, bool capB = true, bool capT = true)
            => AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{key}.asset") ?? SaveMesh(LobbyMesh.Lathe(prof, seg, capB, capT), key);

        /// <summary>Open shade seen from both sides (outer + inner surface).</summary>
        private static Mesh ShadeMesh(string key, float rBottom, float rTop, float h)
        {
            var outer = new[] { new Vector2(rBottom, 0f), new Vector2(rTop, h) };
            var inner = new[] { new Vector2(rTop - 0.003f, h), new Vector2(rBottom - 0.003f, 0f) };
            var path = $"{MeshDir}/{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var a = LobbyMesh.Lathe(outer, 40, false, false);
            var b = LobbyMesh.Lathe(inner, 40, false, false);
            var m = new Mesh();
            m.CombineMeshes(new[] { new CombineInstance { mesh = a, transform = Matrix4x4.identity }, new CombineInstance { mesh = b, transform = Matrix4x4.identity } }, true, true);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            return SaveMesh(m, key);
        }

        private static Material ShadeMat(string name, Color c, float glow)
        {
            var m = Mat(name, c, Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(6f, 2f));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.42f) * glow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        private static void BuildLobbyLights(Transform root, Material darkWood, Material metal)
        {
            var lt = new GameObject("Lights").transform;
            lt.SetParent(root, false);
            float H = LobbyLayout.H, hd = LobbyLayout.HalfD;

            // ---- ceiling lights (main lights, on the switch)
            var glass = Emissive("LobbyDomeGlass", new Color(1f, 0.95f, 0.85f), new Color(1f, 0.82f, 0.58f) * 1.6f);
            var dome = LatheSaved("CeilDome", new[] { new Vector2(0f, -0.145f), new Vector2(0.08f, -0.135f), new Vector2(0.15f, -0.1f), new Vector2(0.19f, -0.05f), new Vector2(0.2f, 0f) }, 32, false, false);
            var ring = LatheSaved("CeilRing", new[] { new Vector2(0.19f, -0.03f), new Vector2(0.205f, -0.03f), new Vector2(0.23f, -0.02f), new Vector2(0.23f, 0f) }, 32, false, false);
            var finial = LatheSaved("CeilFinial", new[] { new Vector2(0f, -0.045f), new Vector2(0.012f, -0.03f), new Vector2(0.018f, -0.01f), new Vector2(0.0f, 0f) }, 12, false, false);
            var mainLights = new List<Light>();
            var glows = new List<Renderer>();
            foreach (var (x, z) in new[] { (-5.0f, -2.6f), (0.5f, -2.6f), (5.5f, -2.6f), (1.2f, 2.6f), (5.2f, 2.4f) })
            {
                var c = new GameObject("CeilingLight").transform;
                c.SetParent(lt, false);
                c.position = new Vector3(x, H, z);
                MeshObj("Ring", c, ring, Brass, Vector3.zero);
                glows.Add(MeshObj("Dome", c, dome, glass, new Vector3(0f, -0.03f, 0f), null, false).GetComponent<Renderer>());
                MeshObj("Finial", c, finial, Brass, new Vector3(0f, -0.172f, 0f));
                var l = new GameObject("LampLight").AddComponent<Light>();
                l.transform.SetParent(c, false);
                l.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                l.type = LightType.Point;
                l.color = WarmLight;
                l.range = 7.5f;
                l.intensity = 1.2f;
                l.shadows = Mathf.Approximately(x, 0.5f) ? LightShadows.Soft : LightShadows.None;
                mainLights.Add(l);
            }

            // ---- light switch by the exit door (south wall): a LobbyProp with the main lights on it
            var white = Mat("LobbyPlastic", new Color(0.9f, 0.9f, 0.86f), null, 0.35f, Vector2.one);
            var sw = WallSwitch(lt, "LightSwitch", new Vector3(LobbyLayout.ExitDoorX - 0.85f, 1.12f, -hd), 0f, white, out var rocker);
            var ml = sw.gameObject.AddComponent<LobbyMainLights>();
            ml.lights = mainLights.ToArray();
            ml.glows = glows.ToArray();
            ml.rocker = rocker;
            AddProp(sw.gameObject, LobbyPropKind.LightSwitch, new Vector3(0f, 0f, 0.02f), 1.9f);

            // ---- floor lamps: weighted base, brass pole, linen drum shade
            FloorLampModel(lt, new Vector3(LobbyLayout.SofaSide.pos.x, 0f, LobbyLayout.SofaFront.pos.z + 0.05f));
            FloorLampModel(lt, LobbyLayout.SofaFront.pos + new Vector3(1.65f, 0f, -0.25f));

            // ---- side table with a table lamp at the north end of the side sofa
            Vector3 st = LobbyLayout.SofaSide.pos + new Vector3(0f, 0f, 1.6f);
            SideTable(lt, st, darkWood);
            TableLampModel(lt, st + new Vector3(0f, 0.56f, 0f));

            // ---- wall sconces: both sides of the TV over the fireplace, both sides of the painting on the west wall
            float fx = LobbyLayout.Fireplace.x;
            Sconce(lt, new Vector3(fx - LobbyLayout.TvSconceDx, 2.05f, hd), 180f);
            Sconce(lt, new Vector3(fx + LobbyLayout.TvSconceDx, 2.05f, hd), 180f);
            Sconce(lt, new Vector3(-LobbyLayout.HalfW, 2.0f, LobbyLayout.Globe.z - 0.95f), 90f);
            Sconce(lt, new Vector3(-LobbyLayout.HalfW, 2.0f, LobbyLayout.Globe.z + 0.95f), 90f);

            // ---- ceiling fan over the coffee table + its switch next to the fireplace
            CeilingFanModel(lt, new Vector3(LobbyLayout.CoffeeTable.x, H, LobbyLayout.CoffeeTable.z), darkWood);
        }

        private static void AddProp(GameObject g, LobbyPropKind kind, Vector3 aimLocal, float range)
        {
            var p = g.AddComponent<LobbyProp>();
            p.kind = kind;
            p.aimLocal = aimLocal;
            p.range = range;
        }

        /// <summary>Wall switch plate (wall at local z 0, facing local +Z = <paramref name="facingYaw"/>), rocker on a pivot.</summary>
        private static Transform WallSwitch(Transform parent, string name, Vector3 wallPos, float facingYaw, Material white, out Transform rocker)
        {
            var s = new GameObject(name).transform;
            s.SetParent(parent, false);
            s.SetPositionAndRotation(wallPos, Quaternion.Euler(0f, facingYaw, 0f));
            var plate = LocalBox(s, "Plate", new Vector3(0f, 0f, 0.006f), new Vector3(0.085f, 0.125f, 0.012f), white);
            RoundCube(plate.transform, 0.005f, 0f);
            var piv = new GameObject("Rocker").transform;
            piv.SetParent(s, false);
            piv.localPosition = new Vector3(0f, 0f, 0.012f);
            var rk = LocalBox(piv, "RockerFace", new Vector3(0f, 0f, 0.004f), new Vector3(0.036f, 0.06f, 0.01f), white);
            RoundCube(rk.transform, 0.004f, 0f);
            rocker = piv;
            return s;
        }

        private static void FloorLampModel(Transform parent, Vector3 pos)
        {
            var p = new GameObject("FloorLamp").transform;
            p.SetParent(parent, false);
            p.position = pos;
            MeshObj("Base", p, LatheSaved("FLampBase", new[] { new Vector2(0.17f, 0f), new Vector2(0.17f, 0.012f), new Vector2(0.15f, 0.03f), new Vector2(0.06f, 0.045f), new Vector2(0.03f, 0.06f) }, 32, true, true), Bronze, Vector3.zero);
            MeshObj("Pole", p, LatheSaved("FLampPole", new[] { new Vector2(0.014f, 0f), new Vector2(0.014f, 1.32f) }, 12, false, true), Brass, new Vector3(0f, 0.06f, 0f));
            MeshObj("Knuckle", p, LatheSaved("FLampKnuckle", new[] { new Vector2(0.0f, 0f), new Vector2(0.022f, 0.01f), new Vector2(0.022f, 0.04f), new Vector2(0f, 0.05f) }, 16, false, false), Brass, new Vector3(0f, 0.7f, 0f));
            MeshObj("Harp", p, LatheSaved("FLampHarp", new[] { new Vector2(0.008f, 0f), new Vector2(0.008f, 0.08f) }, 8, true, true), Brass, new Vector3(0f, 1.38f, 0f));
            var shade = ShadeMat("LobbyShadeLinen", new Color(0.93f, 0.86f, 0.72f), 0.55f);
            MeshObj("Shade", p, ShadeMesh("FLampShade", 0.24f, 0.2f, 0.32f), shade, new Vector3(0f, 1.32f, 0f), null, false);
            MeshObj("ShadeRimB", p, LobbyMeshSaved("FLampRimB", () => LobbyMesh.Torus(0.24f, 0.006f, 40, 6)), Brass, new Vector3(0f, 1.32f, 0f));
            MeshObj("ShadeRimT", p, LobbyMeshSaved("FLampRimT", () => LobbyMesh.Torus(0.2f, 0.005f, 40, 6)), Brass, new Vector3(0f, 1.64f, 0f));
            MeshObj("Bulb", p, LatheSaved("FLampBulb", new[] { new Vector2(0f, 0f), new Vector2(0.03f, 0.02f), new Vector2(0.035f, 0.05f), new Vector2(0.02f, 0.08f), new Vector2(0f, 0.085f) }, 16, false, false),
                Emissive("LobbyBulbWarm", new Color(1f, 0.92f, 0.75f), new Color(1f, 0.75f, 0.45f) * 3f), new Vector3(0f, 1.38f, 0f), null, false);
            var col = p.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.12f;
            col.height = 1.7f;
            col.center = new Vector3(0f, 0.85f, 0f);
            var l = new GameObject("LampLight").AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.45f);
            l.range = 4.6f;
            l.intensity = 1.25f;
        }

        private static Mesh LobbyMeshSaved(string key, System.Func<Mesh> make)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{key}.asset");
            return existing != null ? Repaired(existing) : SaveMesh(make(), key);
        }

        /// <summary>A mesh saved by an older build: zero normals (NaN pixels) repaired in place.</summary>
        private static Mesh Repaired(Mesh m)
        {
            if (HideAndSeek.Core.MeshNormalFix.Repair(m) > 0) EditorUtility.SetDirty(m);
            return m;
        }

        private static void SideTable(Transform parent, Vector3 pos, Material darkWood)
        {
            var t = new GameObject("SideTable").transform;
            t.SetParent(parent, false);
            t.position = pos;
            var top = Box("Top", t, pos + new Vector3(0f, 0.53f, 0f), new Vector3(0.46f, 0.04f, 0.46f), darkWood);
            RoundCube(top.transform, 0.012f, 0f);
            var shelf = Deco("Shelf", t, pos + new Vector3(0f, 0.16f, 0f), new Vector3(0.4f, 0.025f, 0.4f), darkWood);
            RoundCube(shelf.transform, 0.008f, 0f);
            foreach (var (x, z) in new[] { (-0.19f, -0.19f), (0.19f, -0.19f), (-0.19f, 0.19f), (0.19f, 0.19f) })
                MeshObj("Leg", t, LatheSaved("SideLeg", new[] { new Vector2(0.012f, 0f), new Vector2(0.018f, 0.25f), new Vector2(0.02f, 0.51f) }, 12, true, true), darkWood, new Vector3(x, 0f, z));
            // a book and a mug on the shelf / top
            Deco("Book", t, pos + new Vector3(0.03f, 0.19f, 0.02f), new Vector3(0.22f, 0.035f, 0.16f), Mat("Book3", new Color(0.4f, 0.12f, 0.1f), null, 0.1f, Vector2.one), Quaternion.Euler(0f, 18f, 0f));
            MeshObj("Mug", t, LatheSaved("Mug", new[] { new Vector2(0.035f, 0f), new Vector2(0.04f, 0.09f), new Vector2(0.034f, 0.09f), new Vector2(0.03f, 0.012f), new Vector2(0f, 0.012f) }, 20, true, false),
                Mat("LobbyMug", new Color(0.85f, 0.25f, 0.18f), null, 0.6f, Vector2.one), new Vector3(0.14f, 0.55f, -0.12f));
        }

        private static void TableLampModel(Transform parent, Vector3 pos)
        {
            var p = new GameObject("TableLamp").transform;
            p.SetParent(parent, false);
            p.position = pos;
            var ceramic = Mat("LobbyCeramicTeal", new Color(0.13f, 0.4f, 0.42f), null, 0.85f, Vector2.one);
            MeshObj("Base", p, LatheSaved("TLampBase", new[] { new Vector2(0.06f, 0f), new Vector2(0.08f, 0.04f), new Vector2(0.095f, 0.12f), new Vector2(0.08f, 0.2f), new Vector2(0.035f, 0.25f), new Vector2(0.02f, 0.27f) }, 28, true, true), ceramic, Vector3.zero);
            MeshObj("Stem", p, LatheSaved("TLampStem", new[] { new Vector2(0.008f, 0f), new Vector2(0.008f, 0.1f) }, 8, false, true), Brass, new Vector3(0f, 0.27f, 0f));
            MeshObj("Shade", p, ShadeMesh("TLampShade", 0.16f, 0.1f, 0.18f), ShadeMat("LobbyShadeCream", new Color(0.95f, 0.9f, 0.78f), 0.7f), new Vector3(0f, 0.3f, 0f), null, false);
            var l = new GameObject("LampLight").AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.7f, 0.42f);
            l.range = 2.6f;
            l.intensity = 0.9f;
        }

        /// <summary>Wall sconce: brass back plate, curved arm, tulip glass with a warm bulb, small light.</summary>
        private static void Sconce(Transform parent, Vector3 wallPos, float facingYaw)
        {
            var s = new GameObject("Sconce").transform;
            s.SetParent(parent, false);
            s.SetPositionAndRotation(wallPos, Quaternion.Euler(0f, facingYaw, 0f));
            MeshObj("Plate", s, LatheSaved("SconcePlate", new[] { new Vector2(0.05f, 0f), new Vector2(0.045f, 0.012f), new Vector2(0.02f, 0.02f) }, 24, true, true), Brass, new Vector3(0f, 0f, 0.001f), Quaternion.Euler(90f, 0f, 0f));
            var arm = LocalBox(s, "Arm", new Vector3(0f, 0.02f, 0.07f), new Vector3(0.014f, 0.014f, 0.13f), Brass);
            RoundCube(arm.transform, 0.006f, 0f);
            var cup = LocalBox(s, "Cup", new Vector3(0f, 0.04f, 0.135f), new Vector3(0.03f, 0.02f, 0.03f), Brass);
            RoundCube(cup.transform, 0.008f, 0f);
            var tulip = Emissive("LobbyTulipGlass", new Color(1f, 0.9f, 0.75f), new Color(1f, 0.72f, 0.42f) * 1.4f);
            MeshObj("Tulip", s, LatheSaved("SconceTulip", new[] { new Vector2(0.015f, 0f), new Vector2(0.04f, 0.03f), new Vector2(0.06f, 0.08f), new Vector2(0.065f, 0.12f), new Vector2(0.058f, 0.13f) }, 28, true, false), tulip, new Vector3(0f, 0.05f, 0.135f), null, false);
            var l = new GameObject("SconceLight").AddComponent<Light>();
            l.transform.SetParent(s, false);
            l.transform.localPosition = new Vector3(0f, 0.12f, 0.2f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.45f);
            l.range = 2.8f;
            l.intensity = 0.75f;
        }
    }
}
