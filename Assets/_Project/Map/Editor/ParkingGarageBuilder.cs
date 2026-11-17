using System.Collections.Generic;
using System.Linq;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Procedurally lays out the two-floor underground parking garage into the active scene.
    /// Coordinates: X = east/west (90 m), Z = south/north (60 m). Level -1 floor top at y = 0,
    /// Level -2 floor top at y = -3.4. Everything is seeded and deterministic.
    /// </summary>
    internal sealed class ParkingGarageBuilder
    {
        // ------------------------------------------------------------------ layout constants
        private const float HX = 45f, HZ = 30f;
        private const float WallT = 0.4f;
        private const float Slab = 0.4f;
        private const float Clear = 3.0f;
        private const float RoomFront = 21f;      // south face of the north service rooms
        private const float BeamDepth = 0.45f;

        private static readonly float[] ColX = { -37.5f, -30f, -22.5f, -15f, -7.5f, 0f, 7.5f, 15f, 22.5f, 30f, 37.5f };
        private static readonly float[] ColZ = { -24.6f, -13.5f, 3f, 14.1f };
        private static readonly string[] ColRow = { "A", "B", "C", "D" };
        private static readonly float[] AisleZ = { -21.75f, -5.25f, 11.25f };     // aisle centre lines
        private static readonly float[] AisleHalf = { 3.25f, 3.25f, 3.25f };
        private static readonly int[] AisleDir = { 1, -1, 1 };                     // traffic direction along X
        private const float CrossX = 34.5f;                                         // cross aisle centre |x|

        private static readonly float[] StairX0 = { -38f, 13f };                   // interior west edge
        private const float StairWidth = 4f;
        private const float FlightZ0 = 23f, Run = 0.28f, Rise = 0.17f;
        private const int Risers = 20;
        private static float FlightZ1 => FlightZ0 + Risers * Run;                  // 28.6

        private static float FloorY(int lv) => lv == 0 ? 0f : -3.4f;
        private static float CeilY(int lv) => FloorY(lv) + Clear;

        // ------------------------------------------------------------------ state
        private readonly MaterialLibrary _lib;
        private readonly PropFactory _props;
        private readonly List<Mesh> _meshes;
        private readonly Random _rng = new Random(20261117);

        private Transform _root, _tFloors, _tWalls, _tColumns, _tRamps, _tStairs, _tProps, _tCars, _tLights, _tDecals, _tSpawns;
        private readonly Transform[] _tPropsLv = new Transform[2];
        private readonly MeshBuilder[] _walls = new MeshBuilder[2];
        private readonly MeshBuilder[] _columns = new MeshBuilder[2];
        private readonly MeshBuilder[] _decals = new MeshBuilder[2];
        private readonly MeshBuilder[] _signs = new MeshBuilder[2];
        private readonly MeshBuilder[] _decor = new MeshBuilder[2];
        private readonly MeshBuilder[] _ceiling = new MeshBuilder[2];
        private readonly MeshBuilder[] _cages = new MeshBuilder[2];
        private MeshBuilder _slabL1, _slabL2, _roof, _ramps, _stairs;

        private readonly List<Vector3> _spawnPoints = new List<Vector3>();
        private int _carCount;

        private struct Bay
        {
            public Vector3 Center;
            public Vector3 Inward;     // from aisle into the bay
            public float Width, Depth;
            public string Label;
            public int Level;
        }

        private readonly List<Bay> _bays = new List<Bay>();

        private struct Opening
        {
            public float A0, A1, H;
            public Opening(float a0, float a1, float h) { A0 = a0; A1 = a1; H = h; }
        }

        public ParkingGarageBuilder(MaterialLibrary lib, List<Mesh> meshStore)
        {
            _lib = lib;
            _meshes = meshStore;
            _props = new PropFactory(lib, meshStore);
        }

        private Material M(string n) => _lib[n];
        private Material LevelPaint(int lv) => lv == 0 ? M("Paint_Blue") : M("Paint_Orange");
        private static string LevelName(int lv) => lv == 0 ? "P-1" : "P-2";
        private float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ================================================================== entry

        public GameObject Build()
        {
            _root = new GameObject("ParkingGarage").transform;
            _tFloors = Group(_root, "Floors");
            _tWalls = Group(_root, "Walls");
            _tColumns = Group(_root, "Columns");
            _tRamps = Group(_root, "Ramps");
            _tStairs = Group(_root, "Stairs");
            _tProps = Group(_root, "Props");
            _tCars = Group(_root, "Cars");
            _tLights = Group(_root, "Lights");
            _tDecals = Group(_root, "Decals");
            _tSpawns = Group(_root, GameLayers.SpawnPointsRoot);
            for (int lv = 0; lv < 2; lv++)
            {
                _tPropsLv[lv] = Group(_tProps, "Level" + (lv == 0 ? "-1" : "-2"));
                _walls[lv] = new MeshBuilder(_lib);
                _columns[lv] = new MeshBuilder(_lib);
                _decals[lv] = new MeshBuilder(_lib);
                _signs[lv] = new MeshBuilder(_lib);
                _decor[lv] = new MeshBuilder(_lib);
                _ceiling[lv] = new MeshBuilder(_lib);
                _cages[lv] = new MeshBuilder(_lib);
            }
            _slabL1 = new MeshBuilder(_lib);
            _slabL2 = new MeshBuilder(_lib);
            _roof = new MeshBuilder(_lib);
            _ramps = new MeshBuilder(_lib);
            _stairs = new MeshBuilder(_lib);

            DefineSpawnPoints();
            BuildSlabs();
            BuildBeams();
            for (int lv = 0; lv < 2; lv++)
            {
                BuildPerimeter(lv);
                BuildColumns(lv);
                BuildRooms(lv);
                BuildBays(lv);
                BuildAisleMarkings(lv);
                BuildGrime(lv);
                BuildCeilingServices(lv);
                BuildLevelProps(lv);
            }
            BuildRamp(-45f, -39f, -22f, 0f, true);
            BuildRamp(39f, 45f, 22f, 0f, false);
            foreach (float x0 in StairX0) BuildStair(x0);
            PlaceCars();
            BuildLights();
            BuildSpawnObjects();

            Emit(_slabL1, "Slab_Level-1", _tFloors, true);
            Emit(_slabL2, "Slab_Level-2", _tFloors, true);
            Emit(_roof, "Roof", _tFloors, true);
            Emit(_ramps, "Ramps", _tRamps, true);
            Emit(_stairs, "Steps", _tStairs, false);
            for (int lv = 0; lv < 2; lv++)
            {
                string s = lv == 0 ? "Level-1" : "Level-2";
                Emit(_walls[lv], "Walls_" + s, _tWalls, true);
                Emit(_columns[lv], "Columns_" + s, _tColumns, true);
                Emit(_decals[lv], "FloorMarkings_" + s, _tDecals, false, ShadowCastingMode.Off);
                Emit(_signs[lv], "Signage_" + s, _tDecals, false, ShadowCastingMode.Off);
                Emit(_decor[lv], "FloorFixtures_" + s, _tPropsLv[lv], true);
                Emit(_ceiling[lv], "CeilingServices_" + s, _tPropsLv[lv], false);
                Emit(_cages[lv], "Cages_" + s, _tPropsLv[lv], true);
            }
            return _root.gameObject;
        }

        // ================================================================== helpers

        private static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.gameObject.isStatic = true;
            return t;
        }

        private GameObject Emit(MeshBuilder b, string name, Transform parent, bool collider,
            ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            if (b.IsEmpty) return null;
            var mesh = b.ToMesh(name);
            _meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = b.Materials;
            mr.shadowCastingMode = shadows;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return go;
        }

        private GameObject PlaceProp(PropMesh pm, string name, Transform parent, Vector3 pos, float yaw,
            Material[] mats = null, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.AddComponent<MeshFilter>().sharedMesh = pm.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats ?? pm.Materials;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (collider)
                foreach (var b in pm.Colliders)
                {
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = b.center;
                    bc.size = b.size;
                }
            go.isStatic = true;
            return go;
        }

        private bool NearSpawn(Vector3 p, float radius)
        {
            foreach (var s in _spawnPoints)
                if (Mathf.Abs(s.y - p.y) < 2f && new Vector2(s.x - p.x, s.z - p.z).sqrMagnitude < radius * radius)
                    return true;
            return false;
        }

        /// <summary>Splits outer minus holes into a small set of axis-aligned rectangles.</summary>
        private static List<Rect> Subtract(Rect outer, IList<Rect> holes)
        {
            var xs = new List<float> { outer.xMin, outer.xMax };
            var ys = new List<float> { outer.yMin, outer.yMax };
            foreach (var h in holes)
            {
                xs.Add(Mathf.Clamp(h.xMin, outer.xMin, outer.xMax));
                xs.Add(Mathf.Clamp(h.xMax, outer.xMin, outer.xMax));
                ys.Add(Mathf.Clamp(h.yMin, outer.yMin, outer.yMax));
                ys.Add(Mathf.Clamp(h.yMax, outer.yMin, outer.yMax));
            }
            xs = xs.Distinct().OrderBy(v => v).ToList();
            ys = ys.Distinct().OrderBy(v => v).ToList();
            var strips = new List<Rect>();
            for (int j = 0; j < ys.Count - 1; j++)
            {
                if (ys[j + 1] - ys[j] < 1e-4f) continue;
                float runStart = float.NaN;
                for (int i = 0; i < xs.Count - 1; i++)
                {
                    var c = new Vector2((xs[i] + xs[i + 1]) * 0.5f, (ys[j] + ys[j + 1]) * 0.5f);
                    bool filled = xs[i + 1] - xs[i] > 1e-4f && !holes.Any(h => h.Contains(c));
                    if (filled && float.IsNaN(runStart)) runStart = xs[i];
                    if (!filled && !float.IsNaN(runStart))
                    {
                        strips.Add(Rect.MinMaxRect(runStart, ys[j], xs[i], ys[j + 1]));
                        runStart = float.NaN;
                    }
                }
                if (!float.IsNaN(runStart)) strips.Add(Rect.MinMaxRect(runStart, ys[j], xs[xs.Count - 1], ys[j + 1]));
            }
            // merge vertically adjacent strips with identical x extents
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int a = 0; a < strips.Count && !merged; a++)
                for (int b = 0; b < strips.Count && !merged; b++)
                {
                    if (a == b) continue;
                    var ra = strips[a];
                    var rb = strips[b];
                    if (Mathf.Abs(ra.xMin - rb.xMin) < 1e-4f && Mathf.Abs(ra.xMax - rb.xMax) < 1e-4f && Mathf.Abs(ra.yMax - rb.yMin) < 1e-4f)
                    {
                        strips[a] = Rect.MinMaxRect(ra.xMin, ra.yMin, ra.xMax, rb.yMax);
                        strips.RemoveAt(b);
                        merged = true;
                    }
                }
            }
            return strips;
        }

        /// <summary>Axis-aligned wall slab with door/window openings along its long axis (heights relative to y0).</summary>
        private void Wall(MeshBuilder b, float x0, float x1, float z0, float z1, float y0, float y1,
            Material mat = null, params Opening[] ops)
        {
            mat = mat != null ? mat : M("Wall_Concrete");
            bool alongX = (x1 - x0) >= (z1 - z0);
            float a0 = alongX ? x0 : z0, a1 = alongX ? x1 : z1;
            var holes = ops.Select(o => Rect.MinMaxRect(o.A0, y0 - 1f, o.A1, y0 + o.H)).ToList();
            foreach (var r in Subtract(Rect.MinMaxRect(a0, y0, a1, y1), holes))
            {
                if (alongX) b.BoxMinMax(new Vector3(r.xMin, r.yMin, z0), new Vector3(r.xMax, r.yMax, z1), mat);
                else b.BoxMinMax(new Vector3(x0, r.yMin, r.xMin), new Vector3(x1, r.yMax, r.xMax), mat);
            }
            var frame = M("Metal_Steel");
            foreach (var o in ops)
            {
                float t0 = (alongX ? z0 : x0) - 0.03f, t1 = (alongX ? z1 : x1) + 0.03f;
                float top = y0 + o.H;
                if (alongX)
                {
                    b.BoxMinMax(new Vector3(o.A0 - 0.07f, y0, t0), new Vector3(o.A0, top + 0.07f, t1), frame);
                    b.BoxMinMax(new Vector3(o.A1, y0, t0), new Vector3(o.A1 + 0.07f, top + 0.07f, t1), frame);
                    if (top + 0.07f < y1) b.BoxMinMax(new Vector3(o.A0, top, t0), new Vector3(o.A1, top + 0.07f, t1), frame);
                }
                else
                {
                    b.BoxMinMax(new Vector3(t0, y0, o.A0 - 0.07f), new Vector3(t1, top + 0.07f, o.A0), frame);
                    b.BoxMinMax(new Vector3(t0, y0, o.A1), new Vector3(t1, top + 0.07f, o.A1 + 0.07f), frame);
                    if (top + 0.07f < y1) b.BoxMinMax(new Vector3(t0, top, o.A0), new Vector3(t1, top + 0.07f, o.A1), frame);
                }
            }
        }

        /// <summary>Vertical paint/sign quad on a wall face. n = outward face normal.</summary>
        private static void WallQuad(MeshBuilder b, Vector3 center, Vector3 n, float halfLen, float halfH, Material m)
        {
            Vector3 u = Vector3.Cross(n, Vector3.up).normalized * halfLen;
            b.Face(center, u, Vector3.up * halfH, m);
        }

        /// <summary>Sign plate (thin box) with centred text, mounted on a surface with outward normal n.</summary>
        private void SignPlate(MeshBuilder b, Vector3 surfacePoint, Vector3 n, float w, float h, string text,
            float textH, Material plate, Material textMat)
        {
            Vector3 right = Vector3.Cross(n, Vector3.up).normalized;
            var rot = Quaternion.LookRotation(n, Vector3.up);
            b.Box(surfacePoint + n * 0.012f, new Vector3(w, h, 0.024f), plate, rot);
            b.Text(text, surfacePoint + n * 0.026f, n, Vector3.up, textH, textMat);
            _ = right;
        }

        // ================================================================== spawn points

        private void DefineSpawnPoints()
        {
            float a = FloorY(0) + 0.05f, b = FloorY(1) + 0.05f;
            _spawnPoints.AddRange(new[]
            {
                new Vector3(-34.5f, a, -21f), new Vector3(26f, a, -21.75f), new Vector3(-18f, a, -5.25f),
                new Vector3(18f, a, -5.25f), new Vector3(-22f, a, 11.25f), new Vector3(34.5f, a, 5f),
                new Vector3(-42f, a, 26f), new Vector3(-11f, a, 23f), new Vector3(26f, a, 28.7f),
                new Vector3(8f, a, 23.5f),
                new Vector3(-34.5f, b, -18f), new Vector3(34.5f, b, 10f), new Vector3(0f, b, -5.25f),
                new Vector3(-20f, b, -21.75f), new Vector3(20f, b, 11.25f), new Vector3(-42f, b, -27f),
                new Vector3(42f, b, 26.5f), new Vector3(-11f, b, 23f), new Vector3(-25f, b, 23.2f),
                new Vector3(30f, b, 28.7f),
            });
        }

        private void BuildSpawnObjects()
        {
            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                var go = new GameObject($"Spawn_{i + 1:00}_{(_spawnPoints[i].y > -1f ? "L1" : "L2")}");
                go.transform.SetParent(_tSpawns, false);
                go.transform.SetPositionAndRotation(_spawnPoints[i], Quaternion.Euler(0, (i * 137) % 360, 0));
                go.tag = GameLayers.SpawnPointTag;
            }
            var seeker = new GameObject("SeekerSpawn");
            seeker.transform.SetParent(_tSpawns, false);
            seeker.transform.SetPositionAndRotation(new Vector3(0f, FloorY(0) + 0.05f, 26.5f), Quaternion.Euler(0, 180f, 0));
        }

        // ================================================================== slabs & beams

        private static IEnumerable<Rect> SlabHoles()
        {
            yield return Rect.MinMaxRect(-45f, -22f, -39f, 0f);   // west ramp
            yield return Rect.MinMaxRect(39f, 0f, 45f, 22f);      // east ramp
            foreach (float x0 in StairX0)
                yield return Rect.MinMaxRect(x0, FlightZ0, x0 + 2f, HZ);
        }

        private void BuildSlabs()
        {
            var outer = Rect.MinMaxRect(-HX - WallT, -HZ - WallT, HX + WallT, HZ + WallT);
            var floor = M("Floor_Concrete");
            var ceil = M("Ceiling_Concrete");
            foreach (var r in Subtract(outer, SlabHoles().ToList()))
            {
                var c = new Vector3(r.center.x, -Slab * 0.5f, r.center.y);
                var s = new Vector3(r.width, Slab, r.height);
                _slabL1.Box(c, s, floor, null, MeshBuilder.Faces.PosY);
                _slabL1.Box(c, s, ceil, null, MeshBuilder.Faces.All & ~MeshBuilder.Faces.PosY);
            }
            var c2 = new Vector3(0, FloorY(1) - Slab * 0.5f, 0);
            var s2 = new Vector3(outer.width, Slab, outer.height);
            _slabL2.Box(c2, s2, floor, null, MeshBuilder.Faces.PosY);
            _slabL2.Box(c2, s2, ceil, null, MeshBuilder.Faces.NegY);
            _roof.Box(new Vector3(0, CeilY(0) + Slab * 0.5f, 0), new Vector3(outer.width, Slab, outer.height), ceil, null,
                MeshBuilder.Faces.NegY | MeshBuilder.Faces.PosY);
        }

        private void BuildBeams()
        {
            var ceil = M("Ceiling_Concrete");
            foreach (float z in ColZ)
            {
                // under the roof (level -1 ceiling)
                _roof.Box(new Vector3(0, CeilY(0) - BeamDepth * 0.5f, z), new Vector3(2 * HX, BeamDepth, 0.5f), ceil, null,
                    MeshBuilder.Faces.All & ~MeshBuilder.Faces.PosY);
                // under the level -1 slab (level -2 ceiling); stop before the ramp voids
                _slabL1.Box(new Vector3(0, CeilY(1) - BeamDepth * 0.5f, z), new Vector3(2 * 38.75f, BeamDepth, 0.5f), ceil, null,
                    MeshBuilder.Faces.All & ~MeshBuilder.Faces.PosY);
            }

        }

        // ================================================================== perimeter

        private void BuildPerimeter(int lv)
        {
            var b = _walls[lv];
            float y0 = FloorY(lv), y1 = CeilY(lv);
            Wall(b, -HX - WallT, HX + WallT, -HZ - WallT, -HZ, y0, y1);
            Wall(b, -HX - WallT, HX + WallT, HZ, HZ + WallT, y0, y1);
            Wall(b, -HX - WallT, -HX, -HZ, HZ, y0, y1);
            Wall(b, HX, HX + WallT, -HZ, HZ, y0, y1);

            // painted bands (inner faces). Skip wall stretches beside the ramp voids.
            void Band(Vector3 n, float along0, float along1, float fixedCoord, bool alongX)
            {
                float len = along1 - along0;
                float mid = (along0 + along1) * 0.5f;
                Vector3 P(float y) => alongX ? new Vector3(mid, y, fixedCoord) + n * 0.012f : new Vector3(fixedCoord, y, mid) + n * 0.012f;
                WallQuad(b, P(y0 + 0.06f), n, len * 0.5f, 0.06f, M("Paint_Grey"));
                WallQuad(b, P(y0 + 0.56f), n, len * 0.5f, 0.44f, LevelPaint(lv));
                WallQuad(b, P(y0 + 1.04f), n, len * 0.5f, 0.04f, M("Paint_White"));
            }
            Band(Vector3.forward, -HX, HX, -HZ, true);
            Band(Vector3.back, -HX, HX, HZ, true);
            Band(Vector3.right, -HZ, -22f, -HX, false);
            Band(Vector3.right, 0f, HZ, -HX, false);
            Band(Vector3.left, -HZ, 0f, HX, false);
            Band(Vector3.left, 22f, HZ, HX, false);

            // pilasters
            foreach (float x in ColX)
            {
                AddPilaster(lv, new Vector3(x, 0, -HZ), Vector3.forward);
                bool inStair = StairX0.Any(s => x > s - 0.6f && x < s + StairWidth + 0.6f) || (x > -6.5f && x < 6.5f);
                if (!inStair) AddPilaster(lv, new Vector3(x, 0, HZ), Vector3.back);
            }
            foreach (float z in ColZ)
            {
                if (!(z > -22.5f && z < 0.5f)) AddPilaster(lv, new Vector3(-HX, 0, z), Vector3.right);
                if (!(z > -0.5f && z < 22.5f)) AddPilaster(lv, new Vector3(HX, 0, z), Vector3.left);
            }

            // big level markings on the walls
            var white = M("Text_White");
            foreach (float x in new[] { -26f, -11f, 4f, 19f, 34f })
                b.Text(LevelName(lv), new Vector3(x, y0 + 1.75f, -HZ + 0.015f), Vector3.forward, Vector3.up, 0.7f, white);
            foreach (float z in new[] { -8.25f, 8f })
                b.Text(LevelName(lv), new Vector3(z > 0 ? -HX + 0.015f : HX - 0.015f, y0 + 1.75f, z),
                    z > 0 ? Vector3.right : Vector3.left, Vector3.up, 0.7f, white);
            // row letters on the east/west walls next to each column line
            for (int j = 0; j < ColZ.Length; j++)
            {
                float z = ColZ[j];
                if (!(z > -22.5f && z < 0.5f))
                    b.Text(ColRow[j], new Vector3(-HX + 0.015f, y0 + 1.75f, z + 1.6f), Vector3.right, Vector3.up, 0.5f, white);
                if (!(z > -0.5f && z < 22.5f))
                    b.Text(ColRow[j], new Vector3(HX - 0.015f, y0 + 1.75f, z - 1.6f), Vector3.left, Vector3.up, 0.5f, white);
            }

            if (lv == 0)
            {
                // roller shutter (closed street exit) at the south end of the east lane
                b.BoxMinMax(new Vector3(39.7f, y0, -HZ), new Vector3(44.3f, y0 + 2.75f, -HZ + 0.1f), M("Shutter"));
                b.BoxMinMax(new Vector3(39.5f, y0 + 2.75f, -HZ), new Vector3(44.5f, y1, -HZ + 0.45f), M("PM_Grey"));
                b.BoxMinMax(new Vector3(39.5f, y0, -HZ), new Vector3(39.7f, y0 + 2.75f, -HZ + 0.16f), M("Metal_Dark"));
                b.BoxMinMax(new Vector3(44.3f, y0, -HZ), new Vector3(44.5f, y0 + 2.75f, -HZ + 0.16f), M("Metal_Dark"));
                SignPlate(_signs[lv], new Vector3(42f, y0 + 2.6f, -HZ + 0.45f), Vector3.forward, 1.6f, 0.28f, "NO EXIT", 0.17f, M("Sign_Red"), white);
            }
        }

        private void AddPilaster(int lv, Vector3 basePos, Vector3 n)
        {
            var b = _walls[lv];
            float y0 = FloorY(lv), y1 = CeilY(lv);
            Vector3 right = Vector3.Cross(n, Vector3.up);
            Vector3 c = basePos + n * 0.175f;
            Vector3 size = new Vector3(Mathf.Abs(right.x) * 0.6f + Mathf.Abs(n.x) * 0.35f, 0, Mathf.Abs(right.z) * 0.6f + Mathf.Abs(n.z) * 0.35f);
            b.Box(new Vector3(c.x, (y0 + 1f + y1) * 0.5f, c.z), new Vector3(size.x, y1 - y0 - 1f, size.z), M("Column_Concrete"));
            b.Box(new Vector3(c.x, y0 + 0.5f, c.z) + n * 0.005f, new Vector3(size.x + 0.01f, 1f, size.z + 0.01f), M("Hazard"));
        }

        // ================================================================== columns

        private void BuildColumns(int lv)
        {
            var b = _columns[lv];
            var s = _signs[lv];
            float y0 = FloorY(lv), y1 = CeilY(lv);
            for (int j = 0; j < ColZ.Length; j++)
            for (int i = 0; i < ColX.Length; i++)
            {
                float x = ColX[i], z = ColZ[j];
                b.Box(new Vector3(x, (y0 + 1f + y1) * 0.5f, z), new Vector3(0.6f, y1 - y0 - 1f, 0.6f), M("Column_Concrete"));
                b.Box(new Vector3(x, y0 + 0.5f, z), new Vector3(0.6f, 1f, 0.6f), M("Hazard"), null, MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
                b.Box(new Vector3(x, y0 + 1.07f, z), new Vector3(0.612f, 0.14f, 0.612f), LevelPaint(lv), null,
                    MeshBuilder.Faces.PosX | MeshBuilder.Faces.NegX | MeshBuilder.Faces.PosZ | MeshBuilder.Faces.NegZ);
                // corner guards
                foreach (float cx in new[] { -0.3f, 0.3f })
                foreach (float cz in new[] { -0.3f, 0.3f })
                    b.Box(new Vector3(x + cx, y0 + 0.6f, z + cz), new Vector3(0.05f, 1.2f, 0.05f), M("Metal_Dark"));
                string id = ColRow[j] + (i + 1);
                foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    var p = new Vector3(x, y0 + 1.95f, z) + n * 0.3f;
                    SignPlate(s, p, n, 0.46f, 0.34f, id, 0.22f, M("Sign_White"), M("Text_Black"));
                    s.Text(LevelName(lv), new Vector3(x, y0 + 1.6f, z) + n * 0.302f, n, Vector3.up, 0.12f, M("Text_White"));
                }
            }
        }

        // ================================================================== rooms

        private void BuildRooms(int lv)
        {
            var b = _walls[lv];
            float y0 = FloorY(lv), y1 = CeilY(lv);
            float zf = RoomFront, zi = RoomFront + 0.2f;

            // technical (L-1) / pump (L-2) room
            Wall(b, -16.2f, -7.8f, zf, zi, y0, y1, null, new Opening(-10.6f, -9.4f, 2.1f));
            Wall(b, -16.2f, -16f, zi, HZ, y0, y1);
            Wall(b, -8f, -7.8f, zi, HZ, y0, y1);
            PlaceProp(_props.Door("PM_DarkGrey", 1.15f), "Door_TechRoom", _tPropsLv[lv], new Vector3(-9.45f, y0, zi + 0.05f), -110f);
            SignPlate(_signs[lv], new Vector3(-12.5f, y0 + 1.7f, zf), Vector3.back, 1.1f, 0.42f, lv == 0 ? "DANGER" : "PUMPS", 0.2f, M("Sign_Yellow"), M("Text_Black"));
            SignPlate(_signs[lv], new Vector3(-12.5f, y0 + 1.33f, zf), Vector3.back, 1.1f, 0.24f, lv == 0 ? "HIGH VOLTAGE" : "STAFF ONLY", 0.12f, M("Sign_Yellow"), M("Text_Black"));

            // elevator lobby
            Wall(b, -6.2f, 6.2f, zf, zi, y0, y1, null, new Opening(-1.6f, 1.6f, 2.5f));
            Wall(b, -6.2f, -6f, zi, HZ, y0, y1);
            Wall(b, 6f, 6.2f, zi, HZ, y0, y1);
            _decals[lv].FloorQuad(-6f, 6f, zi, HZ, y0 + 0.004f, M("Tile_Floor"));
            _decals[lv].FloorQuad(-1.6f, 1.6f, zf, zi, y0 + 0.004f, M("Metal_Steel"));
            foreach (float ex in new[] { -2.6f, 2.6f })
            {
                // frame
                b.BoxMinMax(new Vector3(ex - 1.0f, y0, HZ - 0.15f), new Vector3(ex - 0.85f, y0 + 2.35f, HZ), M("Metal_Steel"));
                b.BoxMinMax(new Vector3(ex + 0.85f, y0, HZ - 0.15f), new Vector3(ex + 1.0f, y0 + 2.35f, HZ), M("Metal_Steel"));
                b.BoxMinMax(new Vector3(ex - 1.0f, y0 + 2.2f, HZ - 0.15f), new Vector3(ex + 1.0f, y0 + 2.45f, HZ), M("Metal_Steel"));
                // doors (closed)
                b.BoxMinMax(new Vector3(ex - 0.85f, y0, HZ - 0.08f), new Vector3(ex - 0.006f, y0 + 2.2f, HZ - 0.03f), M("Metal_Steel"));
                b.BoxMinMax(new Vector3(ex + 0.006f, y0, HZ - 0.08f), new Vector3(ex + 0.85f, y0 + 2.2f, HZ - 0.03f), M("Metal_Steel"));
                b.BoxMinMax(new Vector3(ex - 0.006f, y0, HZ - 0.05f), new Vector3(ex + 0.006f, y0 + 2.2f, HZ - 0.03f), M("Plastic_Black"));
                // floor indicator above
                _signs[lv].Box(new Vector3(ex, y0 + 2.62f, HZ - 0.03f), new Vector3(0.5f, 0.2f, 0.06f), M("Sign_Dark"));
                if (ex < 0)
                    _signs[lv].Text(LevelName(lv), new Vector3(ex, y0 + 2.62f, HZ - 0.065f), Vector3.back, Vector3.up, 0.14f, M("Text_GlowRed"));
                // threshold
                _decals[lv].FloorQuad(ex - 0.9f, ex + 0.9f, HZ - 0.25f, HZ - 0.03f, y0 + 0.006f, M("Metal_Steel"));
            }
            // call buttons
            b.Box(new Vector3(0, y0 + 1.15f, HZ - 0.03f), new Vector3(0.14f, 0.3f, 0.06f), M("Metal_Steel"));
            b.Box(new Vector3(0, y0 + 1.22f, HZ - 0.065f), new Vector3(0.05f, 0.05f, 0.02f), M("Plastic_Black"));
            b.Box(new Vector3(0, y0 + 1.08f, HZ - 0.065f), new Vector3(0.05f, 0.05f, 0.02f), M("Plastic_Black"));
            SignPlate(_signs[lv], new Vector3(0, y0 + 2.75f, zf), Vector3.back, 1.4f, 0.36f, "LIFT " + LevelName(lv), 0.2f, M("Sign_Blue"), M("Text_White"));
            SignPlate(_signs[lv], new Vector3(-6f, y0 + 1.8f, 25.5f), Vector3.right, 0.9f, 0.7f, LevelName(lv), 0.45f, M("Sign_Blue"), M("Text_White"));

            // maintenance corridor
            Wall(b, 18.8f, 38.8f, 27.2f, 27.4f, y0, y1, null, new Opening(22f, 23.2f, 2.1f), new Opening(33f, 34.2f, 2.1f));
            Wall(b, 18.8f, 19f, 27.4f, HZ, y0, y1);
            Wall(b, 38.6f, 38.8f, 27.4f, HZ, y0, y1);
            PlaceProp(_props.Door("PM_Grey", 1.15f), "Door_Corridor_A", _tPropsLv[lv], new Vector3(23.15f, y0, 27.45f), 100f);
            SignPlate(_signs[lv], new Vector3(28f, y0 + 2.3f, 27.2f), Vector3.back, 2.2f, 0.3f, "MAINTENANCE", 0.18f, M("Sign_White"), M("Text_Black"));

            if (lv == 0) BuildCages(lv);
        }

        private void BuildCages(int lv)
        {
            var b = _cages[lv];
            float y0 = FloorY(lv);
            const float h = 2.4f, zFront = 25.5f;
            var wire = M("Wire_Mesh");
            var post = M("Metal_Galvanized");
            float[] xs = { -33f, -29f, -25f, -21f, -17f };
            for (int i = 0; i < xs.Length - 1; i++)
            {
                float a = xs[i], c = xs[i + 1];
                float doorA = a + 2.6f, doorB = a + 3.6f;
                // front panels with door gap
                b.BoxMinMax(new Vector3(a, y0 + 0.05f, zFront - 0.01f), new Vector3(doorA, y0 + h, zFront + 0.01f), wire);
                b.BoxMinMax(new Vector3(doorB, y0 + 0.05f, zFront - 0.01f), new Vector3(c, y0 + h, zFront + 0.01f), wire);
                b.BoxMinMax(new Vector3(a, y0 + h - 0.05f, zFront - 0.03f), new Vector3(c, y0 + h, zFront + 0.03f), post);
                b.BoxMinMax(new Vector3(a, y0, zFront - 0.03f), new Vector3(c, y0 + 0.05f, zFront + 0.03f), post);
                foreach (float px in new[] { a, a + 1.3f, doorA, doorB, c })
                    b.BoxMinMax(new Vector3(px - 0.03f, y0, zFront - 0.03f), new Vector3(px + 0.03f, y0 + h, zFront + 0.03f), post);
                // open cage door (rotated wire panel)
                var rot = Quaternion.Euler(0, -70f + i * 15f, 0);
                var hinge = new Vector3(doorB, y0 + 0.05f, zFront);
                b.Box(hinge + rot * new Vector3(-0.5f, (h - 0.1f) * 0.5f, 0), new Vector3(1f, h - 0.1f, 0.02f), wire, rot);
                b.Box(hinge + rot * new Vector3(-0.5f, h - 0.1f, 0), new Vector3(1f, 0.04f, 0.04f), post, rot);
                b.Box(hinge + rot * new Vector3(-1f, (h - 0.1f) * 0.5f, 0), new Vector3(0.04f, h - 0.1f, 0.04f), post, rot);
                // number plate
                SignPlate(_signs[lv], new Vector3((a + doorA) * 0.5f, y0 + 1.9f, zFront - 0.02f), Vector3.back, 0.5f, 0.25f, "S" + (i + 1), 0.16f, M("Sign_Yellow"), M("Text_Black"));
            }
            // dividers along Z
            foreach (float x in xs)
            {
                b.BoxMinMax(new Vector3(x - 0.01f, y0 + 0.05f, zFront), new Vector3(x + 0.01f, y0 + h, HZ), wire);
                b.BoxMinMax(new Vector3(x - 0.03f, y0 + h - 0.05f, zFront), new Vector3(x + 0.03f, y0 + h, HZ), post);
                b.BoxMinMax(new Vector3(x - 0.03f, y0, HZ - 0.06f), new Vector3(x + 0.03f, y0 + h, HZ), post);
            }
        }

        // ================================================================== ramps

        private void BuildRamp(float xMin, float xMax, float zTop, float zBottom, bool west)
        {
            float yTop = FloorY(0), yBot = FloorY(1);
            float xc = (xMin + xMax) * 0.5f, w = xMax - xMin;
            var top = new Vector3(xc, yTop, zTop);
            var bot = new Vector3(xc, yBot, zBottom);
            Vector3 dir = bot - top;
            float len = dir.magnitude;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 up = rot * Vector3.up;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 right = rot * Vector3.right;
            Vector3 mid = (top + bot) * 0.5f;
            _ramps.Box(mid - up * 0.2f, new Vector3(w, 0.4f, len + 0.04f), M("Ramp_Concrete"), rot);
            // curbs
            foreach (float s in new[] { -1f, 1f })
                _ramps.Box(mid + up * 0.075f + right * (s * (w * 0.5f - 0.15f)), new Vector3(0.3f, 0.15f, len), M("Concrete_Yellow"), rot);
            // markings on the ramp surface: centre line + arrows
            for (float t = 1.5f; t < len - 1f; t += 3f)
                _ramps.Face(top + fwd * t + up * 0.008f, right * 0.06f, fwd * 0.75f, M("Paint_Yellow"));
            foreach (float s in new[] { -1f, 1f })
            foreach (float t in new[] { len * 0.3f, len * 0.7f })
            {
                Vector3 arrowDir = s > 0 ? fwd : -fwd;
                Vector3 c = top + fwd * t + right * (s * w * 0.25f) + up * 0.01f;
                Vector3 r = Vector3.Cross(up, arrowDir).normalized;
                _ramps.FaceUV(c, r * 0.6f, arrowDir * 1.2f, M("Arrow_White"), Vector2.zero, Vector2.one);
            }
            // hazard strips at both transitions and a trench drain at the bottom
            _decals[0].Face(new Vector3(xc, yTop + 0.01f, zTop + (west ? -0.4f : 0.4f)), new Vector3(w * 0.5f - 0.3f, 0, 0), new Vector3(0, 0, 0.3f), M("Hazard"));
            _decals[1].Face(new Vector3(xc, yBot + 0.01f, zBottom + (west ? 0.5f : -0.5f)), new Vector3(w * 0.5f - 0.3f, 0, 0), new Vector3(0, 0, 0.25f), M("Grate"));
            var fl = _decals[0];
            Vector3 down = west ? Vector3.forward : Vector3.back;
            fl.Text("P-2", new Vector3(xc, yTop + 0.012f, zTop - down.z * 3.2f), Vector3.up, down, 1.0f, M("Text_Floor"));
            fl.FloorDecal(new Vector3(xc, yTop + 0.012f, zTop - down.z * 5.5f), down, 1.0f, 2.2f, M("Arrow_White"));
            fl.Text("SLOW", new Vector3(xc, yTop + 0.012f, zTop - down.z * 7.6f), Vector3.up, down, 0.8f, M("Text_FloorYellow"));
            var fl2 = _decals[1];
            fl2.Text("P-1", new Vector3(xc, yBot + 0.012f, zBottom + down.z * 3.2f), Vector3.up, -down, 1.0f, M("Text_Floor"));
            fl2.FloorDecal(new Vector3(xc, yBot + 0.012f, zBottom + down.z * 5.5f), -down, 1.0f, 2.2f, M("Arrow_White"));

            // side walls: inner wall (towards the cross aisle) on both levels, end walls closing the voids
            float wx0 = west ? xMax : xMin - 0.25f, wx1 = west ? xMax + 0.25f : xMin;
            float z0 = Mathf.Min(zTop, zBottom), z1 = Mathf.Max(zTop, zBottom);
            Wall(_walls[0], wx0, wx1, z0, z1, FloorY(0), CeilY(0));
            Wall(_walls[1], wx0, wx1, z0, z1, FloorY(1), CeilY(1));
            float ex0 = west ? -HX : xMin - 0.25f, ex1 = west ? xMax + 0.25f : HX;
            // level -1 wall at the low end (void edge), level -2 wall at the high end (under the ramp)
            Wall(_walls[0], ex0, ex1, west ? zBottom : zBottom - 0.25f, west ? zBottom + 0.25f : zBottom, FloorY(0), CeilY(0));
            Wall(_walls[1], ex0, ex1, west ? zTop - 0.25f : zTop, west ? zTop : zTop + 0.25f, FloorY(1), CeilY(1));
            // safety paint on the wall faces facing the cross aisle
            Vector3 n = west ? Vector3.right : Vector3.left;
            float fx = west ? xMax + 0.25f : xMin - 0.25f;
            for (int lv = 0; lv < 2; lv++)
            {
                WallQuad(_walls[lv], new Vector3(fx, FloorY(lv) + 0.5f, (z0 + z1) * 0.5f) + n * 0.01f, n, (z1 - z0) * 0.5f, 0.5f, M("Hazard"));
                _walls[lv].Text("RAMP", new Vector3(fx, FloorY(lv) + 1.8f, (z0 + z1) * 0.5f) + n * 0.015f, n, Vector3.up, 0.6f, M("Text_White"));
            }
            // wall-side rails along the ramp (on the perimeter side)
            float railX = west ? -HX + 0.08f : HX - 0.08f;
            Vector3 a = new Vector3(railX, yTop + 0.9f, zTop), c2 = new Vector3(railX, yBot + 0.9f, zBottom);
            _ramps.Cylinder(a, c2, 0.04f, 8, M("PM_Yellow"));
        }

        // ================================================================== stairs

        private void BuildStair(float x0)
        {
            float z0 = RoomFront, zi = RoomFront + 0.2f;
            float xDoorL1 = x0 + 1f, xDoorL2 = x0 + 3f;
            for (int lv = 0; lv < 2; lv++)
            {
                var b = _walls[lv];
                float y0 = FloorY(lv), y1 = CeilY(lv);
                float door = lv == 0 ? xDoorL1 : xDoorL2;
                Wall(b, x0 - 0.2f, x0 + StairWidth + 0.2f, z0, zi, y0, y1, null, new Opening(door - 0.6f, door + 0.6f, 2.1f));
                Wall(b, x0 - 0.2f, x0, zi, HZ, y0, y1);
                Wall(b, x0 + StairWidth, x0 + StairWidth + 0.2f, zi, HZ, y0, y1);
                SignPlate(_signs[lv], new Vector3(lv == 0 ? door + 1.3f : door - 1.3f, y0 + 1.6f, z0), Vector3.back, 0.9f, 0.26f, "STAIRS", 0.16f, M("Sign_Blue"), M("Text_White"));
                _decals[lv].Text(LevelName(lv), new Vector3(x0 + StairWidth - 0.015f, y0 + 1.6f, zi + 0.9f), Vector3.left, Vector3.up, 0.5f, M("Text_White"));
                // hatched no-parking zone in front of the door
                _decals[lv].FloorQuad(door - 1.2f, door + 1.2f, 19.6f, z0, y0 + 0.011f, M("Hatch_Yellow"));
            }
            // level -1: parapet + rail along the stair void
            Wall(_walls[0], x0 + 2f, x0 + 2.15f, FlightZ0, HZ, FloorY(0), FloorY(0) + 1.05f);
            _walls[0].Cylinder(new Vector3(x0 + 2.075f, 1.1f, FlightZ0), new Vector3(x0 + 2.075f, 1.1f, HZ), 0.035f, 8, M("PM_Yellow"));
            _decals[0].FloorQuad(x0, x0 + 2f, FlightZ0 - 0.06f, FlightZ0, FloorY(0) + 0.006f, M("Paint_Yellow"));
            // level -2: separating wall between the flight and the corridor
            Wall(_walls[1], x0 + 2f, x0 + 2.15f, zi, FlightZ1, FloorY(1), CeilY(1));
            _decals[1].Text("P-2", new Vector3(x0 + 2.165f, FloorY(1) + 1.6f, 25.5f), Vector3.right, Vector3.up, 0.5f, M("Text_White"));

            // steps (visual only)
            for (int i = 0; i < Risers - 1; i++)
            {
                float top = FloorY(0) - (i + 1) * Rise;
                float za = FlightZ0 + i * Run, zb = za + Run;
                _stairs.BoxMinMax(new Vector3(x0, FloorY(1), za), new Vector3(x0 + 2f, top, zb), M("Stair_Concrete"),
                    MeshBuilder.Faces.PosY | MeshBuilder.Faces.PosZ | MeshBuilder.Faces.NegZ);
                _stairs.Face(new Vector3(x0 + 1f, top + 0.004f, zb - 0.03f), new Vector3(1f, 0, 0), new Vector3(0, 0, 0.03f), M("Paint_Yellow"));
            }
            // handrail on the outer wall
            float yA = FloorY(0) + 0.9f, yB = FloorY(1) + 0.9f;
            _stairs.Cylinder(new Vector3(x0 + 0.07f, yA, FlightZ0 - 0.3f), new Vector3(x0 + 0.07f, yA, FlightZ0), 0.03f, 8, M("PM_Yellow"));
            _stairs.Cylinder(new Vector3(x0 + 0.07f, yA, FlightZ0), new Vector3(x0 + 0.07f, yB, FlightZ1), 0.03f, 8, M("PM_Yellow"));
            _stairs.Cylinder(new Vector3(x0 + 0.07f, yB, FlightZ1), new Vector3(x0 + 0.07f, yB, FlightZ1 + 0.4f), 0.03f, 8, M("PM_Yellow"));

            // invisible ramp collider through the step mid-points so a CharacterController glides smoothly
            var pTop = new Vector3(x0 + 1f, FloorY(0), FlightZ0);
            var pBot = new Vector3(x0 + 1f, FloorY(1), FlightZ1 - Run);
            Vector3 dir = pBot - pTop;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var col = new GameObject("StairRampCollider");
            col.transform.SetParent(_tStairs, false);
            col.transform.SetPositionAndRotation((pTop + pBot) * 0.5f - rot * Vector3.up * 0.15f, rot);
            var bc = col.AddComponent<BoxCollider>();
            bc.size = new Vector3(2f, 0.3f, dir.magnitude + 0.1f);
            col.isStatic = true;
        }

        // ================================================================== bays & cars

        private void BuildBays(int lv)
        {
            float y0 = FloorY(lv);
            var rows = new (float zMin, float zMax, bool aisleAtMin, string letter)[]
            {
                (-30f, -25f, false, "A"), (-18.5f, -13.5f, true, "B"), (-13.5f, -8.5f, false, "C"),
                (-2f, 3f, true, "D"), (3f, 8f, false, "E"), (14.5f, 19.5f, true, "F")
            };
            foreach (var row in rows)
            {
                Vector3 inward = row.aisleAtMin ? Vector3.forward : Vector3.back;
                int num = 0;
                for (int i = 0; i < 24; i++)
                {
                    float xa = -30f + i * 2.5f, xb = xa + 2.5f;
                    bool walkway = row.letter == "F" && ((xa >= -2.5f && xb <= 2.5f) || (xa >= 12.5f && xb <= 17.5f));
                    if (walkway) continue;
                    num++;
                    AddBay(lv, new Vector3((xa + xb) * 0.5f, y0, (row.zMin + row.zMax) * 0.5f), inward, 2.5f, row.zMax - row.zMin, row.letter + num.ToString("00"));
                }
            }
            // walkway gaps in the north row
            foreach (var (xa, xb, label) in new[] { (-2.5f, 2.5f, "LIFT"), (12.5f, 17.5f, "STAIRS") })
            {
                _decals[lv].FloorQuad(xa + 0.3f, xb - 0.3f, 14.5f, RoomFront, y0 + 0.008f, M("Paint_Green"));
                _decals[lv].FloorQuad(xa + 0.15f, xa + 0.3f, 14.5f, RoomFront, y0 + 0.009f, M("Paint_White"));
                _decals[lv].FloorQuad(xb - 0.3f, xb - 0.15f, 14.5f, RoomFront, y0 + 0.009f, M("Paint_White"));
                _decals[lv].Text(label, new Vector3((xa + xb) * 0.5f, y0 + 0.012f, 16.2f), Vector3.up, Vector3.forward, 0.55f, M("Text_Floor"));
                _decals[lv].Text("^", new Vector3((xa + xb) * 0.5f, y0 + 0.012f, 17.6f), Vector3.up, Vector3.forward, 0.9f, M("Text_Floor"));
                // zebra crossing over aisle 3
                for (float x = xa + 0.55f; x < xb - 0.4f; x += 0.9f)
                    _decals[lv].FloorQuad(x, x + 0.45f, AisleZ[2] - AisleHalf[2] + 0.3f, AisleZ[2] + AisleHalf[2] - 0.3f, y0 + 0.01f, M("Paint_White"));
            }
            if (lv == 0)
            {
                // perpendicular bays along the side lanes of level -1
                for (int i = 0; i < 8; i++)
                {
                    float za = 1f + i * 2.5f;
                    AddBay(lv, new Vector3(-42.5f, y0, za + 1.25f), Vector3.left, 2.5f, 5f, "W" + (i + 1).ToString("00"));
                    float zb = -21f + i * 2.5f;
                    AddBay(lv, new Vector3(42.5f, y0, zb + 1.25f), Vector3.right, 2.5f, 5f, "X" + (i + 1).ToString("00"));
                }
            }
        }

        private void AddBay(int lv, Vector3 center, Vector3 inward, float width, float depth, string label)
        {
            var bay = new Bay { Center = center, Inward = inward, Width = width, Depth = depth, Label = label, Level = lv };
            _bays.Add(bay);
            var d = _decals[lv];
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            float y = center.y + 0.01f;
            // separator lines on both sides (shared lines are drawn twice, harmless)
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 c = center + side * (s * width * 0.5f) + inward * 0.1f;
                c.y = y;
                d.Face(c, side * 0.06f, inward * (depth * 0.5f - 0.1f), M("Paint_White"));
            }
            // bay number near the aisle
            Vector3 tp = center - inward * (depth * 0.5f - 0.75f);
            tp.y = center.y + 0.012f;
            d.Text(label, tp, Vector3.up, inward, 0.38f, M("Text_Floor"));
            // wheel stop near the back
            Vector3 ws = center + inward * (depth * 0.5f - 0.75f);
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            _decor[lv].Box(new Vector3(ws.x, center.y + 0.06f, ws.z), new Vector3(1.6f, 0.12f, 0.16f), M("Concrete_Yellow"), rot,
                MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
        }

        private void PlaceCars()
        {
            for (int lv = 0; lv < 2; lv++)
            {
                var candidates = _bays.Where(b => b.Level == lv).ToList();
                // deterministic shuffle
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }
                int target = lv == 0 ? 26 : 22;
                var parent = Group(_tCars, lv == 0 ? "Level-1" : "Level-2");
                for (int k = 0; k < target && k < candidates.Count; k++)
                {
                    var bay = candidates[k];
                    double r = _rng.NextDouble();
                    var type = r < 0.38 ? CarType.Sedan : r < 0.62 ? CarType.Hatchback : r < 0.83 ? CarType.Suv : CarType.Van;
                    // vans only fit in bays without a column at the aisle edge
                    if (type == CarType.Van && (bay.Label[0] == 'A' || bay.Label[0] == 'F')) type = CarType.Sedan;
                    string paint = type == CarType.Van
                        ? new[] { "CarPaint_White", "CarPaint_Silver", "CarPaint_Grey", "CarPaint_Navy", "CarPaint_White" }[_rng.Next(5)]
                        : MaterialLibrary.CarPaints[_rng.Next(MaterialLibrary.CarPaints.Length)];
                    var pm = _props.Car(type);
                    var mats = (Material[])pm.Materials.Clone();
                    mats[0] = M(paint);
                    float len = type == CarType.Van ? 5.1f : type == CarType.Suv ? 4.7f : type == CarType.Hatchback ? 4.0f : 4.55f;
                    bool noseIn = _rng.NextDouble() < 0.65;
                    Vector3 side = Vector3.Cross(Vector3.up, bay.Inward);
                    Vector3 pos = bay.Center + bay.Inward * (bay.Depth * 0.5f - len * 0.5f - 0.35f) + side * R(-0.12f, 0.12f);
                    float yaw = Quaternion.LookRotation(noseIn ? bay.Inward : -bay.Inward).eulerAngles.y + R(-3.5f, 3.5f);
                    _carCount++;
                    PlaceProp(pm, $"Car_{_carCount:00}_{type}_{paint.Replace("CarPaint_", "")}_{bay.Label}", parent, pos, yaw, mats);
                }
            }
            // one van parked in the loading area in front of the maintenance corridor (level -1)
            var van = _props.Car(CarType.Van);
            var vm = (Material[])van.Materials.Clone();
            vm[0] = M("CarPaint_White");
            _carCount++;
            PlaceProp(van, $"Car_{_carCount:00}_Van_Loading", _tCars.GetChild(0), new Vector3(30.5f, FloorY(0), 24.6f), 87f, vm);
        }

        // ================================================================== aisles & floor markings

        private void BuildAisleMarkings(int lv)
        {
            var d = _decals[lv];
            float y = FloorY(lv) + 0.01f;
            for (int a = 0; a < AisleZ.Length; a++)
            {
                float z = AisleZ[a];
                for (float x = -29f; x < 29f; x += 3f)
                    d.FloorQuad(x, x + 1.5f, z - 0.06f, z + 0.06f, y, M("Paint_Yellow"));
                Vector3 dir = AisleDir[a] > 0 ? Vector3.right : Vector3.left;
                foreach (float x in new[] { -24f, -6f, 12f, 27f })
                {
                    d.FloorDecal(new Vector3(x, y + 0.002f, z - 1.6f * AisleDir[a]), dir, 1.0f, 2.2f, M("Arrow_White"));
                }
                // edge lines
                d.FloorQuad(-30f, 30f, z - AisleHalf[a] + 0.02f, z - AisleHalf[a] + 0.12f, y, M("Paint_White"));
                d.FloorQuad(-30f, 30f, z + AisleHalf[a] - 0.12f, z + AisleHalf[a] - 0.02f, y, M("Paint_White"));
                // speed bumps
                foreach (float bx in a == 0 ? new[] { -11.25f, 18.75f } : a == 1 ? new[] { 6f, -26f } : new[] { -11f, 26f })
                    SpeedBump(lv, new Vector3(bx, FloorY(lv), z), Vector3.forward, AisleHalf[a] * 2f - 0.4f);
            }
            // cross aisles
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * CrossX;
                for (float z = -27f; z < 19f; z += 3f)
                    d.FloorQuad(x - 0.06f, x + 0.06f, z, z + 1.5f, y, M("Paint_Yellow"));
                Vector3 dir = sx < 0 ? Vector3.back : Vector3.forward;
                foreach (float z in new[] { -17f, 0.5f, 17f })
                    d.FloorDecal(new Vector3(x + 2f * (sx < 0 ? -1f : 1f), y + 0.002f, z), dir, 1.0f, 2.2f, M("Arrow_White"));
                SpeedBump(lv, new Vector3(x, FloorY(lv), -9f), Vector3.right, 8f);
            }
            // level text in the middle of each cross aisle and the main aisle
            d.Text(LevelName(lv), new Vector3(-CrossX, y + 0.002f, -4f), Vector3.up, Vector3.back, 1.4f, M("Text_Floor"));
            d.Text(LevelName(lv), new Vector3(CrossX, y + 0.002f, -4f), Vector3.up, Vector3.forward, 1.4f, M("Text_Floor"));
            // hatched zones: lobby entrance, tech door, corridor doors
            d.FloorQuad(-2f, 2f, 19.6f, RoomFront, y + 0.001f, M("Hatch_Yellow"));
            d.FloorQuad(-11.2f, -8.8f, 19.6f, RoomFront, y + 0.001f, M("Hatch_Yellow"));
            if (lv == 0) d.FloorQuad(39.7f, 44.3f, -HZ + 0.1f, -HZ + 2.6f, y + 0.001f, M("Hatch_Yellow"));
            // walkway strip in front of the rooms
            d.FloorQuad(-38f, 38f, 19.55f, 19.65f, y, M("Paint_Yellow"));
        }

        private void SpeedBump(int lv, Vector3 c, Vector3 across, float length)
        {
            var rot = Quaternion.LookRotation(across, Vector3.up);
            var b = _decor[lv];
            b.Box(c + Vector3.up * 0.025f, new Vector3(0.55f, 0.05f, length), M("Hazard"), rot, MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
            b.Box(c + Vector3.up * 0.065f, new Vector3(0.32f, 0.03f, length), M("Hazard"), rot, MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
        }

        /// <summary>Oil stains, cracks, puddles and drains (transparent decals) per level.</summary>
        private void BuildGrime(int lv)
        {
            var d = _decals[lv];
            float y0 = FloorY(lv);
            foreach (var bay in _bays.Where(b => b.Level == lv))
            {
                if (_rng.NextDouble() < 0.45)
                {
                    var c = bay.Center + Vector3.Cross(Vector3.up, bay.Inward) * R(-0.4f, 0.4f) + bay.Inward * R(-1f, 1f);
                    c.y = y0 + 0.006f;
                    float s = R(0.6f, 1.6f);
                    d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.forward * 0.01f, s, s * R(0.8f, 1.3f), M("Oil_Stain"));
                }
            }
            int cracks = 14;
            for (int i = 0; i < cracks; i++)
            {
                int a = _rng.Next(AisleZ.Length);
                var c = new Vector3(R(-30f, 30f), y0 + 0.007f, AisleZ[a] + R(-2.5f, 2.5f));
                float s = R(1.5f, 3.5f);
                d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.right * 0.01f, s, s, M("Crack"));
            }
            for (int i = 0; i < 6; i++)
            {
                var c = new Vector3((_rng.NextDouble() < 0.5 ? -1f : 1f) * CrossX + R(-3f, 3f), y0 + 0.007f, R(-25f, 18f));
                float s = R(1.5f, 3f);
                d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.right * 0.01f, s, s, M("Crack"));
            }
            // puddles: more on the lower level
            var spots = new List<Vector3>();
            int puddles = lv == 0 ? 5 : 11;
            for (int i = 0; i < puddles; i++)
            {
                Vector3 c;
                int sel = _rng.Next(4);
                if (sel == 0) c = new Vector3(R(-29f, 29f), 0, AisleZ[_rng.Next(3)] + R(-2f, 2f));
                else if (sel == 1) c = new Vector3((_rng.NextDouble() < 0.5 ? -1f : 1f) * (CrossX + R(-3f, 3f)), 0, R(-26f, 18f));
                else if (sel == 2) c = new Vector3(R(-28f, 28f), 0, -HZ + R(0.8f, 2.5f));
                else c = new Vector3(ColX[_rng.Next(ColX.Length)] + R(-1.2f, 1.2f), 0, ColZ[_rng.Next(ColZ.Length)] + R(-1.5f, 1.5f));
                spots.Add(c);
            }
            if (lv == 1)
            {
                spots.Add(new Vector3(-43.4f, 0, 6.8f));
                spots.Add(new Vector3(43.4f, 0, -6.8f));
            }
            foreach (var p in spots)
            {
                var c = new Vector3(p.x, y0 + 0.014f, p.z);
                d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.forward * 0.01f, R(1.2f, 3.2f), R(1.5f, 4f), M("Puddle"));
            }
            // floor drains next to column lines B and C
            foreach (float x in new[] { -26.25f, -3.75f, 18.75f })
            foreach (float z in new[] { -13.5f, 3f })
                d.FloorQuad(x - 0.25f, x + 0.25f, z - 0.25f, z + 0.25f, y0 + 0.008f, M("Grate"));
        }

        // ================================================================== ceiling services

        private static readonly Vector3[] FlickerSpots =
        {
            new Vector3(-11.25f, 0, -5.25f), new Vector3(11.25f, 0, 11.25f), new Vector3(18.75f, 1, -21.75f)
        };

        private static bool IsFlicker(int lv, float x, float z) =>
            FlickerSpots.Any(f => (int)f.y == lv && Mathf.Abs(f.x - x) < 0.1f && Mathf.Abs(f.z - z) < 0.1f);

        private void DeadFixture(MeshBuilder b, Vector3 p, bool alongX)
        {
            var rot = alongX ? Quaternion.identity : Quaternion.Euler(0, 90f, 0);
            b.Box(p + Vector3.down * 0.05f, new Vector3(1.3f, 0.1f, 0.22f), M("PM_Grey"), rot);
            b.Box(p + Vector3.down * 0.105f, new Vector3(1.22f, 0.012f, 0.16f), M("Fixture_Dead"), rot);
        }

        private void BuildCeilingServices(int lv)
        {
            var b = _ceiling[lv];
            float c = CeilY(lv), y0 = FloorY(lv);
            float xs0 = lv == 0 ? -44.6f : -38.6f, xs1 = -xs0;

            // dead light fixtures
            foreach (float z in AisleZ)
                for (float x = -33.75f; x < 34f; x += 7.5f)
                    if (!IsFlicker(lv, x, z)) DeadFixture(b, new Vector3(x, c, z), true);
            foreach (float x in new[] { -CrossX, CrossX })
            foreach (float z in new[] { -19.4f, -5.25f, 9f })
                DeadFixture(b, new Vector3(x, c, z), false);
            foreach (var p in new[] { new Vector3(-2.5f, c, 25.5f), new Vector3(2.5f, c, 25.5f), new Vector3(-12f, c, 25.5f), new Vector3(24f, c, 28.7f), new Vector3(33f, c, 28.7f), new Vector3(9.5f, c, 25.5f) })
                DeadFixture(b, p, true);

            // ventilation ducts
            foreach (float z in new[] { -9.6f, 7.0f })
                Duct(b, xs0, xs1, z, c);
            // cable trays with cables
            foreach (float z in new[] { -16.2f, 17.6f })
            {
                float yt = c - 0.3f;
                float len = xs1 - xs0;
                b.Box(new Vector3(0, yt, z), new Vector3(len, 0.02f, 0.45f), M("Metal_Galvanized"));
                b.Box(new Vector3(0, yt + 0.04f, z - 0.225f), new Vector3(len, 0.08f, 0.015f), M("Metal_Galvanized"));
                b.Box(new Vector3(0, yt + 0.04f, z + 0.225f), new Vector3(len, 0.08f, 0.015f), M("Metal_Galvanized"));
                for (int k = 0; k < 4; k++)
                    b.Cylinder(new Vector3(xs0, yt + 0.035f, z - 0.15f + k * 0.1f), new Vector3(xs1, yt + 0.035f, z - 0.15f + k * 0.1f), 0.025f, 6, M(k == 2 ? "PM_Orange" : "Plastic_Black"), false);
                for (float x = xs0 + 1f; x < xs1; x += 2.5f)
                {
                    b.Box(new Vector3(x, c - 0.15f, z - 0.25f), new Vector3(0.03f, 0.3f, 0.03f), M("Metal_Dark"));
                    b.Box(new Vector3(x, c - 0.15f, z + 0.25f), new Vector3(0.03f, 0.3f, 0.03f), M("Metal_Dark"));
                    b.Box(new Vector3(x, yt - 0.02f, z), new Vector3(0.04f, 0.03f, 0.6f), M("Metal_Dark"));
                }
            }
            // sprinkler system
            float sprY = c - 0.15f, mainY = c - 0.6f;
            foreach (float z in new[] { -27.6f, -19f, -11.2f, -2.6f, 5f, 13f, 19.8f })
            {
                b.Cylinder(new Vector3(xs0, sprY, z), new Vector3(xs1, sprY, z), 0.035f, 8, M("PM_Red"), false);
                for (float x = xs0 + 1.5f; x < xs1; x += 3f)
                {
                    b.Cylinder(new Vector3(x, sprY, z), new Vector3(x, sprY - 0.12f, z), 0.012f, 6, M("Metal_Steel"));
                    b.Cylinder(new Vector3(x, sprY - 0.12f, z), new Vector3(x, sprY - 0.13f, z), 0.04f, 8, M("Metal_Steel"));
                }
                foreach (float x in new[] { -36.2f, 36.2f })
                    b.Cylinder(new Vector3(x, sprY, z), new Vector3(x, mainY, z), 0.035f, 8, M("PM_Red"), false);
            }
            foreach (float x in new[] { -36.2f, 36.2f })
            {
                b.Cylinder(new Vector3(x, mainY, -29.5f), new Vector3(x, mainY, 20.3f), 0.07f, 10, M("PM_Red"));
                for (float z = -28f; z < 20f; z += 4f)
                    b.Cylinder(new Vector3(x, mainY, z), new Vector3(x, c, z), 0.012f, 4, M("Metal_Dark"), false);
            }
            // big drain pipes along the cross aisles
            foreach (float x in new[] { -32.4f, 32.4f })
            {
                b.Cylinder(new Vector3(x, c - 0.75f, -29.8f), new Vector3(x, c - 0.75f, 20.5f), 0.1f, 12, M("PM_Grey"));
                for (float z = -27f; z < 20f; z += 3.5f)
                {
                    b.Cylinder(new Vector3(x, c - 0.75f, z), new Vector3(x, c, z), 0.012f, 4, M("Metal_Dark"), false);
                    b.Cylinder(new Vector3(x, c - 0.75f, z - 0.05f), new Vector3(x, c - 0.75f, z + 0.05f), 0.115f, 12, M("Metal_Dark"));
                }
                // down pipe into the floor near the south wall
                b.Cylinder(new Vector3(x, c - 0.75f, -29.8f), new Vector3(x, y0, -29.8f), 0.1f, 12, M("PM_Grey"));
            }
            // vertical down pipes on some columns
            foreach (var (ix, iz) in new[] { (1, 0), (4, 0), (7, 3), (9, 3), (2, 3), (6, 0) })
            {
                var p = new Vector3(ColX[ix] + 0.2f, 0, ColZ[iz] + (iz == 0 ? 0.38f : -0.38f));
                b.Cylinder(new Vector3(p.x, y0, p.z), new Vector3(p.x, c, p.z), 0.07f, 10, M("PM_Grey"));
                for (float yy = y0 + 0.6f; yy < c; yy += 1.1f)
                    b.Box(new Vector3(p.x - 0.04f, yy, p.z - 0.04f), new Vector3(0.18f, 0.04f, 0.18f), M("Metal_Dark"));
            }
            // conduits + junction boxes on the south wall
            foreach (float x in new[] { -19f, 1.5f, 26.5f })
            {
                b.Cylinder(new Vector3(x, y0 + 0.4f, -HZ + 0.05f), new Vector3(x, c, -HZ + 0.05f), 0.025f, 6, M("Metal_Galvanized"));
                b.Box(new Vector3(x, y0 + 1.5f, -HZ + 0.07f), new Vector3(0.25f, 0.3f, 0.12f), M("PM_Grey"));
            }
            // corridor pipes along the north wall
            foreach (var (yy, mat, r) in new[] { (2.2f, "PM_Green", 0.09f), (2.45f, "PM_Blue", 0.06f), (2.65f, "PM_Grey", 0.07f) })
                b.Cylinder(new Vector3(19.1f, y0 + yy, HZ - 0.2f), new Vector3(38.5f, y0 + yy, HZ - 0.2f), r, 10, M(mat));
            // pump room / tech room cable ladders
            b.Box(new Vector3(-15.85f, (y0 + c) * 0.5f, 25.5f), new Vector3(0.05f, c - y0, 0.4f), M("Metal_Galvanized"));
        }

        private void Duct(MeshBuilder b, float x0, float x1, float z, float c)
        {
            const float h = 0.45f, w = 0.9f;
            float y = c - 0.06f - h * 0.5f;
            b.Box(new Vector3((x0 + x1) * 0.5f, y, z), new Vector3(x1 - x0, h, w), M("Metal_Galvanized"));
            for (float x = x0 + 0.75f; x < x1; x += 1.5f)
                b.Box(new Vector3(x, y, z), new Vector3(0.05f, h + 0.06f, w + 0.06f), M("Metal_Galvanized"));
            for (float x = x0 + 1.5f; x < x1; x += 3f)
                b.Box(new Vector3(x, c - 0.03f, z), new Vector3(0.05f, 0.06f, w + 0.25f), M("Metal_Dark"));
            for (float x = x0 + 3f; x < x1; x += 6f)
            {
                b.Box(new Vector3(x, y - h * 0.5f - 0.005f, z), new Vector3(0.6f, 0.01f, 0.4f), M("Grate"));
            }
        }

        // ================================================================== props per level

        private void BuildLevelProps(int lv)
        {
            float y0 = FloorY(lv);
            var t = _tPropsLv[lv];
            int n = 0;
            string Name(string s) => $"{s}_{++n:00}";
            void P(PropMesh pm, string name, float x, float z, float yaw)
            {
                var p = new Vector3(x, y0, z);
                if (NearSpawn(p, 1.2f)) return;
                PlaceProp(pm, Name(name), t, p, yaw);
            }

            // fire extinguisher boxes on columns
            for (int j = 0; j < ColZ.Length; j++)
            for (int i = 0; i < ColX.Length; i++)
            {
                if ((i + j + lv) % 2 != 0) continue;
                bool north = j == 0 || j == 2;
                var pos = new Vector3(ColX[i], y0 + 1.0f, ColZ[j] + (north ? 0.3f : -0.3f));
                PlaceProp(_props.ExtinguisherBox(), Name("ExtinguisherBox"), t, pos, north ? 0f : 180f, null, false);
            }
            foreach (float x0 in StairX0)
                PlaceProp(_props.ExtinguisherBox(), Name("ExtinguisherBox"), t, new Vector3(x0 + (lv == 0 ? 3.5f : 0.5f), y0 + 1.0f, RoomFront), 180f, null, false);

            // elevator lobby
            P(_props.Bench(), "Bench", -4.2f, 29.3f, 180f);
            P(_props.Bench(), "Bench", 4.2f, 29.3f, 180f);
            P(_props.WheelieBin("PM_DarkGrey"), "Bin", 5.4f, 22.0f, 200f);
            P(_props.PayMachine(), "PayMachine", -5.5f, 22.4f, 90f);
            foreach (float bx in new[] { -2.3f, 2.3f, -3.4f, 3.4f })
                P(_props.Bollard(), "Bollard", bx, 20.4f, 0f);

            // waste area between lobby and east stairs
            if (lv == 0)
            {
                P(_props.Dumpster("PM_Green"), "Dumpster", 8.0f, 29.25f, 0f);
                P(_props.Dumpster("PM_Blue"), "Dumpster", 10.8f, 29.25f, 180f);
                P(_props.WheelieBin("PM_Green"), "Bin", 7.0f, 26.6f, 10f);
                P(_props.WheelieBin("PM_Blue"), "Bin", 7.8f, 26.7f, -8f);
                P(_props.WheelieBin("PM_DarkGrey"), "Bin", 12.0f, 27.0f, 95f);
                P(_props.PalletStack(2), "PalletPile", 11.6f, 22.8f, 90f);
                P(_props.TrashBags(0), "TrashBags", 9.4f, 27.6f, 0f);
                P(_props.TrashBags(1), "TrashBags", 6.9f, 28.4f, 40f);
            }
            else
            {
                P(_props.Dumpster("PM_Orange"), "Dumpster", 8.4f, 29.25f, 180f);
                P(_props.PalletStack(1), "PalletStack", 11.7f, 28.8f, 90f);
                P(_props.PalletStack(2), "PalletPile", 11.7f, 26.6f, 90f);
                P(_props.Crate(1.0f), "Crate", 7.0f, 26.5f, 12f);
                P(_props.Drum("PM_Blue"), "Drum", 6.9f, 25.2f, 0f);
            }

            // technical room (L-1) / pump room (L-2)
            if (lv == 0)
            {
                for (int i = 0; i < 9; i++)
                    P(_props.ElectricalCabinet(i), "ElectricalCabinet", -15.5f + i * 0.82f, 29.7f, 180f);
                P(_props.Transformer(), "Transformer", -14.2f, 24.6f, 90f);
                P(_props.ElectricalCabinet(1), "ElectricalCabinet", -15.7f, 22.3f, 90f);
                P(_props.Crate(0.8f), "Crate", -8.7f, 27.8f, 5f);
            }
            else
            {
                P(_props.Tank(), "Tank", -14.3f, 25.6f, 0f);
                P(_props.Tank(), "Tank", -10.3f, 28.6f, 40f);
                P(_props.Pump(), "Pump", -13.5f, 28.9f, 0f);
                P(_props.Pump(), "Pump", -9.0f, 25.2f, 90f);
                P(_props.ElectricalCabinet(2), "ElectricalCabinet", -15.6f, 22.6f, 90f);
            }

            // storage cages (L-1) / pallet storage (L-2)
            if (lv == 0)
            {
                float[] cageX = { -31f, -27f, -23f, -19f };
                for (int i = 0; i < cageX.Length; i++)
                {
                    float cx = cageX[i];
                    P(_props.Shelf(1.8f, i), "Shelf", cx - 0.6f, 29.6f, 180f);
                    switch (i)
                    {
                        case 0:
                            P(_props.TireStack(4), "Tires", cx + 1.3f, 28.9f, 0f);
                            P(_props.TireStack(3), "Tires", cx + 1.3f, 27.9f, 0f);
                            P(_props.CardboardBox(2), "Boxes", cx - 1.2f, 27.0f, 15f);
                            break;
                        case 1:
                            P(_props.Crate(1.0f), "Crate", cx + 1.0f, 28.4f, 8f);
                            P(_props.CardboardBox(4), "Boxes", cx - 1.0f, 26.8f, -10f);
                            break;
                        case 2:
                            P(_props.PalletStack(0), "PalletStack", cx + 0.9f, 27.0f, 90f);
                            P(_props.Drum("PM_Red"), "Drum", cx - 1.4f, 27.0f, 0f);
                            break;
                        default:
                            P(_props.Crate(0.8f), "Crate", cx + 1.2f, 27.0f, -6f);
                            P(_props.Crate(0.8f), "Crate", cx + 1.25f, 28.2f, 14f);
                            P(_props.TireStack(2), "Tires", cx - 1.2f, 27.2f, 0f);
                            break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                    P(_props.PalletRack(i), "PalletRack", -31.5f + i * 2.85f, 29.35f, 180f);
                for (int i = 0; i < 3; i++)
                    P(_props.PalletRack(i + 5), "PalletRack", -31.5f + i * 2.85f, 25.6f, 0f);
                P(_props.PalletStack(0), "PalletStack", -21.5f, 25.8f, 10f);
                P(_props.PalletStack(1), "PalletStack", -19.2f, 26.5f, -6f);
                P(_props.PalletStack(2), "PalletPile", -29f, 22.3f, 90f);
                P(_props.CardboardBox(5), "Boxes", -21f, 22.5f, 30f);
            }

            // maintenance corridor
            P(_props.WaterHeater(), "WaterHeater", 20.0f, 29.5f, 0f);
            P(_props.Shelf(2.0f, 3 + lv), "Shelf", 29f, 29.6f, 180f);
            P(_props.Crate(0.8f), "Crate", 31.5f, 29.4f, 4f);
            P(_props.MopBucket(), "MopBucket", 36.8f, 28.4f, 30f);
            P(_props.Drum(lv == 0 ? "PM_Blue" : "PM_Red"), "Drum", 37.9f, 29.4f, 0f);
            P(_props.CardboardBox(6 + lv), "Boxes", 22.0f, 29.5f, 0f);

            if (lv == 0)
            {
                // loading area in front of the corridor
                P(_props.PalletStack(0), "PalletStack", 21.5f, 25.6f, 0f);
                P(_props.PalletStack(1), "PalletStack", 22.9f, 25.6f, 0f);
                P(_props.PalletStack(2), "PalletPile", 36.5f, 25.8f, 90f);
                P(_props.StackedChairs(), "Barrier", 35f, 22.2f, 0f);
                P(_props.Crate(1.0f), "Crate", 20.2f, 23.0f, 20f);
                // west lane dead end
                P(_props.PalletStack(0), "PalletStack", -44f, 29f, 90f);
                P(_props.Crate(1.0f), "Crate", -44f, 27.4f, 10f);
                P(_props.Crate(0.8f), "Crate", -40.2f, 29.2f, -10f);
                P(_props.Drum("PM_Red"), "Drum", -40.2f, 27.9f, 0f);
                P(_props.Drum("PM_Blue"), "Drum", -39.6f, 28.4f, 0f);
                // ramp approach and street shutter
                foreach (var (cx, cz) in new[] { (-43.5f, -24f), (-40.5f, -24f), (40.5f, -26f), (43.5f, -26f), (42f, -27.2f) })
                    P(_props.Cone(), "Cone", cx, cz, R(0, 360));
                P(_props.StackedChairs(), "Barrier", 42f, -28.4f, 0f);
            }
            else
            {
                // pocket under the west ramp top
                P(_props.PalletStack(0), "PalletStack", -44f, -29f, 0f);
                P(_props.PalletStack(1), "PalletStack", -44f, -24f, 90f);
                P(_props.Dumpster("PM_Green"), "Dumpster", -40.8f, -29.2f, 0f);
                P(_props.TrashBags(2), "TrashBags", -39.6f, -27.6f, 15f);
                P(_props.TrashBags(3), "TrashBags", 41.5f, 28.0f, 70f);
                P(_props.TireStack(5), "Tires", -39.8f, -23f, 0f);
                P(_props.Crate(1.0f), "Crate", -42.4f, -22.9f, 8f);
                // pocket under the east ramp top
                P(_props.Dumpster("PM_Blue"), "Dumpster", 43.8f, 29.2f, 180f);
                P(_props.Crate(1.0f), "Crate", 44.2f, 23f, 6f);
                P(_props.Crate(0.8f), "Crate", 44.2f, 24.2f, -12f);
                P(_props.Drum("PM_Red"), "Drum", 40f, 29.4f, 0f);
                P(_props.Drum("PM_Red"), "Drum", 40.65f, 29.4f, 0f);
                P(_props.PalletStack(2), "PalletPile", 40.2f, 23.2f, 0f);
                // ramp bottoms
                foreach (var (cx, cz) in new[] { (-44.2f, 5f), (-39.8f, 5f), (44.2f, -5f), (39.8f, -5f) })
                    P(_props.Cone(), "Cone", cx, cz, R(0, 360));
                P(_props.StackedChairs(), "Barrier", 0f, -29.4f, 0f);
                P(_props.PalletStack(1), "PalletStack", 36.5f, -29f, 0f);
            }
            // scattered clutter along the south wall and cross aisles
            for (int i = 0; i < 6; i++)
            {
                float x = R(-38.5f, 38.5f);
                if (x > -31f && x < 31f) continue; // keep parking bays clear
                P(_rng.NextDouble() < 0.5 ? _props.Drum(_rng.NextDouble() < 0.5 ? "PM_Blue" : "PM_Red") : _props.Crate(0.8f), "Clutter", x, -29.4f, R(0, 360));
            }
        }

        // ================================================================== lights & signs

        private PropMesh ExitSignMesh(bool hanging, char arrow) => _props.Get($"ExitSign_{(hanging ? "Hang" : "Wall")}_{(int)arrow}", (mb, pm) =>
        {
            mb.Box(new Vector3(0, 0, 0.04f), new Vector3(0.66f, 0.26f, 0.08f), M("Lamp_Exit"));
            mb.Face(new Vector3(0, 0, 0.0815f), new Vector3(-0.3f, 0, 0), new Vector3(0, 0.11f, 0), M("Exit_Green"));
            mb.Text("EXIT" + arrow, new Vector3(0, 0, 0.084f), Vector3.forward, Vector3.up, 0.15f, M("Text_Glow"));
            if (hanging)
            {
                char back = arrow == '<' ? '>' : arrow == '>' ? '<' : arrow;
                mb.Face(new Vector3(0, 0, -0.0015f), new Vector3(0.3f, 0, 0), new Vector3(0, 0.11f, 0), M("Exit_Green"));
                mb.Text("EXIT" + back, new Vector3(0, 0, -0.004f), Vector3.back, Vector3.up, 0.15f, M("Text_Glow"));
                foreach (float x in new[] { -0.25f, 0.25f })
                    mb.Cylinder(new Vector3(x, 0.13f, 0.04f), new Vector3(x, 0.48f, 0.04f), 0.008f, 4, M("Metal_Dark"));
            }
        });

        private void ExitSign(Vector3 pos, float yaw, bool hanging, char arrow, bool withLight, string name)
        {
            var go = PlaceProp(ExitSignMesh(hanging, arrow), name, _tLights, pos, yaw, null, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (!withLight) return;
            var lgo = new GameObject("GreenGlow");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(0, -0.1f, 0.35f);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.2f, 1f, 0.4f);
            l.range = 3.5f;
            l.intensity = 0.55f;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
            l.lightmapBakeType = LightmapBakeType.Realtime;
        }

        private void BuildLights()
        {
            // EXIT signs above stairwell doors (both levels) with tiny green point lights
            foreach (float x0 in StairX0)
            {
                ExitSign(new Vector3(x0 + 1f, FloorY(0) + 2.38f, RoomFront - 0.0f), 180f, false, ' ', true, "ExitSign_Stairs_L1");
                ExitSign(new Vector3(x0 + 3f, FloorY(1) + 2.38f, RoomFront - 0.0f), 180f, false, ' ', true, "ExitSign_Stairs_L2");
            }
            // hanging signs pointing towards the stairs
            ExitSign(new Vector3(-28.5f, CeilY(0) - 0.5f, AisleZ[1]), 90f, true, '<', true, "ExitSign_Hanging_L1");
            ExitSign(new Vector3(28.5f, CeilY(1) - 0.5f, AisleZ[0]), -90f, true, '<', true, "ExitSign_Hanging_L2");
            ExitSign(new Vector3(28.5f, CeilY(0) - 0.5f, AisleZ[2]), -90f, true, '>', false, "ExitSign_Hanging_Dark_L1");
            ExitSign(new Vector3(-28.5f, CeilY(1) - 0.5f, AisleZ[2]), 90f, true, '>', false, "ExitSign_Hanging_Dark_L2");

            // broken flickering ceiling lamps
            for (int i = 0; i < FlickerSpots.Length; i++)
            {
                var f = FlickerSpots[i];
                int lv = (int)f.y;
                var pos = new Vector3(f.x, CeilY(lv), f.z);
                var go = PlaceProp(_props.LightFixture(true), "FlickeringLamp_" + (i + 1), _tLights, pos, 0f, null, false);
                go.isStatic = false;
                var lgo = new GameObject("Light");
                lgo.transform.SetParent(go.transform, false);
                lgo.transform.localPosition = new Vector3(0, -0.3f, 0);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.86f, 0.66f);
                l.range = 7.5f;
                l.intensity = 0.9f;
                l.shadows = i == 0 ? LightShadows.Soft : LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                var fl = lgo.AddComponent<FlickeringLight>();
                fl.Configure(i == 1 ? FlickeringLight.Pattern.Dying : FlickeringLight.Pattern.Unstable, 0.9f,
                    go.GetComponent<MeshRenderer>(), new Color(1f, 0.86f, 0.62f), 17 + i * 31);
            }
        }
    }
}
