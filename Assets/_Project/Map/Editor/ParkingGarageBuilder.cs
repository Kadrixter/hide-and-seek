using System.Collections.Generic;
using System.Linq;
using HideAndSeek.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Procedurally lays out the three-floor underground parking garage into the active scene.
    /// Coordinates: X = west/east (180 m), Z = south/north (120 m). Floor tops: Level -1 y=0,
    /// Level -2 y=-3.4, Level -3 y=-6.8. Static geometry is merged per category, per level and per
    /// 30 x 30 m chunk. Everything is seeded and deterministic.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        // ------------------------------------------------------------------ layout constants
        public const float HX = 90f, HZ = 60f;
        public const int Levels = 3;
        private const float WallT = 0.4f;
        private const float Slab = 0.4f;
        private const float Clear = 3.0f;
        private const float BeamDepth = 0.45f;

        public static float FloorY(int lv) => -3.4f * lv;
        public static float CeilY(int lv) => FloorY(lv) + Clear;

        private static readonly float[] ColX = Enumerable.Range(0, 23).Select(i => -82.5f + 7.5f * i).ToArray();
        private static readonly float[] ColZ = { -47.6f, -36.5f, -20f, -8.1f, 8.1f, 20f, 36.5f, 48.6f };
        private static readonly string[] ColRow = { "A", "B", "C", "D", "E", "F", "G", "H" };
        private const float ParkX = 75f;                                            // bay rows span [-75, 75]
        private const float CrossX = 79.5f;                                         // cross aisle centre |x|
        private static readonly float[] AisleZ = { -44.75f, -28.25f, -11.75f, 11.75f, 28.25f, 44.75f };
        private const float AisleHalf = 3.25f;
        private static readonly int[] AisleDir = { 1, -1, 1, -1, 1, -1 };
        private const float NorthFront = 50.8f;                                     // south face of the north rooms
        private const float TunnelWall = -56.2f;                                    // tunnel wall z[-56.2, -56]

        public struct StairDef
        {
            public float X0, Zf;
            public StairDef(float x0, float zf) { X0 = x0; Zf = zf; }
            public float ZA => Zf + 2f;                       // flight start (top)
            public float ZB => Zf + 2f + Risers * Run;        // flight end (bottom)
            public float ZIn => Zf + 9.2f;                    // interior back face
        }

        public struct RampDef
        {
            public float XTop, XBot, Z0, Z1;
            public RampDef(float xTop, float xBot, float z0, float z1) { XTop = xTop; XBot = xBot; Z0 = z0; Z1 = z1; }
            public float XMin => Mathf.Min(XTop, XBot);
            public float XMax => Mathf.Max(XTop, XBot);
        }

        public static readonly StairDef[] Stairs =
        {
            new StairDef(-62f, NorthFront), new StairDef(46f, NorthFront), new StairDef(-34f, -4.6f), new StairDef(30f, -4.6f)
        };
        public static readonly RampDef[] Ramps = { new RampDef(-44f, -66f, -7.5f, -0.5f), new RampDef(44f, 66f, 0.5f, 7.5f) };

        public const float StairWidth = 4f, Run = 0.28f, Rise = 0.17f;
        public const int Risers = 20;

        // ------------------------------------------------------------------ state
        private readonly MaterialLibrary _lib;
        private readonly PropFactory _props;
        private readonly Dictionary<string, List<Mesh>> _store;
        private readonly Random _rng = new Random(20261117);

        private Transform _root, _tFloors, _tWalls, _tColumns, _tRamps, _tStairs, _tProps, _tCars, _tLights, _tDecals, _tSpawns;
        private readonly Transform[] _tPropsLv = new Transform[Levels];
        private readonly Transform[] _tCarsLv = new Transform[Levels];
        private readonly ChunkedBuilder[] _walls = new ChunkedBuilder[Levels];
        private readonly ChunkedBuilder[] _columns = new ChunkedBuilder[Levels];
        private readonly ChunkedBuilder[] _decals = new ChunkedBuilder[Levels];
        private readonly ChunkedBuilder[] _signs = new ChunkedBuilder[Levels];
        private readonly ChunkedBuilder[] _decor = new ChunkedBuilder[Levels];
        private readonly ChunkedBuilder[] _ceiling = new ChunkedBuilder[Levels];
        private readonly ChunkedBuilder[] _cages = new ChunkedBuilder[Levels];
        private readonly ChunkedBuilder[] _slabs = new ChunkedBuilder[Levels];
        private readonly ChunkedBuilder[] _ramps = new ChunkedBuilder[Levels - 1];
        private readonly ChunkedBuilder[] _steps = new ChunkedBuilder[Levels - 1];
        private ChunkedBuilder _roof;

        private readonly List<Vector3> _spawnPoints = new List<Vector3>();
        private readonly List<Bay> _bays = new List<Bay>();
        private int _carCount;
        private int _lightCount;

        private struct Bay
        {
            public Vector3 Center;
            public Vector3 Inward;     // from aisle into the bay
            public float Width, Depth;
            public string Label;
            public int Level;
            public bool NoCars, NoVan;
        }

        private struct Opening
        {
            public float A0, A1, H;
            public Opening(float a0, float a1, float h) { A0 = a0; A1 = a1; H = h; }
        }

        public ParkingGarageBuilder(MaterialLibrary lib, Dictionary<string, List<Mesh>> store)
        {
            _lib = lib;
            _store = store;
            if (!_store.ContainsKey("Props")) _store["Props"] = new List<Mesh>();
            _props = new PropFactory(lib, _store["Props"]);
            _props.StoreFor = key =>
            {
                if (!_store.TryGetValue(key, out var list)) _store[key] = list = new List<Mesh>();
                return list;
            };
        }

        public int LightCount => _lightCount;

        private Material M(string n) => _lib[n];
        private static string Tag(int lv) => "L" + (lv + 1);
        private static string LevelName(int lv) => "P-" + (lv + 1);
        private Material LevelPaint(int lv) => M(lv == 0 ? "Paint_Blue" : lv == 1 ? "Paint_Orange" : "Paint_Red");
        private Material FloorMat(int lv) => M(lv == 2 ? "Floor_Concrete_Old" : "Floor_Concrete");
        private Material WallMat(int lv) => M(lv == 2 ? "Wall_Concrete_Old" : "Wall_Concrete");
        private Material CeilMat(int lv) => M(lv >= 2 ? "Ceiling_Concrete_Old" : "Ceiling_Concrete");
        private Material ColumnMat(int lv) => M(lv == 2 ? "Column_Concrete_Old" : "Column_Concrete");
        private Material HazardMat(int lv) => M(lv == 2 ? "Hazard_Old" : "Hazard");
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
            for (int lv = 0; lv < Levels; lv++)
            {
                _tPropsLv[lv] = Group(_tProps, "Level-" + (lv + 1));
                _tCarsLv[lv] = Group(_tCars, "Level-" + (lv + 1));
                _walls[lv] = new ChunkedBuilder(_lib);
                _columns[lv] = new ChunkedBuilder(_lib);
                _decals[lv] = new ChunkedBuilder(_lib);
                _signs[lv] = new ChunkedBuilder(_lib);
                _decor[lv] = new ChunkedBuilder(_lib);
                _ceiling[lv] = new ChunkedBuilder(_lib);
                _cages[lv] = new ChunkedBuilder(_lib);
                _slabs[lv] = new ChunkedBuilder(_lib);
            }
            for (int k = 0; k < Levels - 1; k++)
            {
                _ramps[k] = new ChunkedBuilder(_lib);
                _steps[k] = new ChunkedBuilder(_lib);
            }
            _roof = new ChunkedBuilder(_lib);

            DefineSpawnPoints();
            BuildSlabs();
            BuildBeams();
            foreach (var r in Ramps) BuildRamp(r);
            foreach (var s in Stairs) BuildStair(s);
            for (int lv = 0; lv < Levels; lv++)
            {
                BuildPerimeter(lv);
                BuildColumns(lv);
                BuildTunnel(lv);
                BuildNorthRooms(lv);
                BuildLobbyNorth(lv);
                BuildLobbyCentral(lv);
                BuildBays(lv);
                BuildAisleMarkings(lv);
                BuildGrime(lv);
                BuildCeilingServices(lv);
                BuildZones(lv);
                BuildCommonProps(lv);
            }
            PlaceCars();
            BuildLights();
            BuildSpawnObjects();

            for (int lv = 0; lv < Levels; lv++)
            {
                string t = Tag(lv);
                EmitChunks(_slabs[lv], "Slab_" + t, Group(_tFloors, "Level-" + (lv + 1)), true, true);
                EmitChunks(_walls[lv], "Walls_" + t, Group(_tWalls, "Level-" + (lv + 1)), true, true);
                EmitChunks(_columns[lv], "Columns_" + t, Group(_tColumns, "Level-" + (lv + 1)), true, true);
                var dg = Group(_tDecals, "Level-" + (lv + 1));
                EmitChunks(_decals[lv], "FloorMarkings_" + t, dg, false, false, ShadowCastingMode.Off);
                EmitChunks(_signs[lv], "Signage_" + t, dg, false, false, ShadowCastingMode.Off);
                EmitChunks(_decor[lv], "FloorFixtures_" + t, _tPropsLv[lv], true, false);
                EmitChunks(_ceiling[lv], "CeilingServices_" + t, _tPropsLv[lv], false, false);
                EmitChunks(_cages[lv], "Cages_" + t, _tPropsLv[lv], true, false);
            }
            EmitChunks(_roof, "Roof", _tFloors, true, true);
            EmitUndersides();

            for (int k = 0; k < Levels - 1; k++)
            {
                EmitChunks(_ramps[k], $"Ramps_{Tag(k)}-{Tag(k + 1)}", _tRamps, true, true);
                EmitChunks(_steps[k], $"Steps_{Tag(k)}-{Tag(k + 1)}", _tStairs, false, true);
            }
            ParkSomeCarsCrooked();
            BuildMapDetail();
            ApplyLevelLayers();
            return _root.gameObject;
        }

        // ================================================================== helpers

        private const StaticEditorFlags StructureFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                         StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        private const StaticEditorFlags DetailFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic |
                                                      StaticEditorFlags.ReflectionProbeStatic;

        private static Transform Group(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, DetailFlags);
            return t;
        }

        private void EmitChunks(ChunkedBuilder cb, string name, Transform parent, bool collider, bool occluder,
            ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            foreach (var kv in cb.Chunks)
            {
                var b = kv.Value;
                if (b.IsEmpty) continue;
                int cx = kv.Key % ChunkedBuilder.NX, cz = kv.Key / ChunkedBuilder.NX;
                string n = $"{name}_{cx}_{cz}";
                var mesh = b.ToMesh(n);
                string file = $"Chunk_{cx}_{cz}";
                if (!_store.TryGetValue(file, out var list)) _store[file] = list = new List<Mesh>();
                list.Add(mesh);
                var go = new GameObject(n);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = b.Materials;
                mr.shadowCastingMode = shadows;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                GameObjectUtility.SetStaticEditorFlags(go, occluder ? StructureFlags : DetailFlags);
            }
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
            GameObjectUtility.SetStaticEditorFlags(go, DetailFlags);
            // hazard-light anchors (read by CarAlarms): empty children lying on the lamps, +z = outward normal
            foreach (var bl in pm.Blinkers)
            {
                var a = new GameObject("Blinker").transform;
                a.SetParent(go.transform, false);
                a.localPosition = bl.Pos;
                a.localRotation = Quaternion.LookRotation(bl.Normal, Mathf.Abs(bl.Normal.y) > 0.9f ? Vector3.forward : Vector3.up);
                a.localScale = new Vector3(bl.Size.x, bl.Size.y, 1f);
            }
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

        /// <summary>Axis-aligned wall slab with door openings along its long axis (heights relative to y0).</summary>
        private void Wall(ChunkedBuilder b, float x0, float x1, float z0, float z1, float y0, float y1,
            Material mat, params Opening[] ops)
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
                    b.BoxMinMax(new Vector3(o.A0 - 0.07f, y0, t0), new Vector3(o.A0 + 0.004f, top + 0.07f, t1), frame);
                    b.BoxMinMax(new Vector3(o.A1 - 0.004f, y0, t0), new Vector3(o.A1 + 0.07f, top + 0.07f, t1), frame);
                    if (top + 0.07f < y1) b.BoxMinMax(new Vector3(o.A0, top - 0.004f, t0), new Vector3(o.A1, top + 0.07f, t1), frame);
                }
                else
                {
                    b.BoxMinMax(new Vector3(t0, y0, o.A0 - 0.07f), new Vector3(t1, top + 0.07f, o.A0 + 0.004f), frame);
                    b.BoxMinMax(new Vector3(t0, y0, o.A1 - 0.004f), new Vector3(t1, top + 0.07f, o.A1 + 0.07f), frame);
                    if (top + 0.07f < y1) b.BoxMinMax(new Vector3(t0, top - 0.004f, o.A0), new Vector3(t1, top + 0.07f, o.A1), frame);
                }
            }
        }

        private void LevelWall(int lv, float x0, float x1, float z0, float z1, params Opening[] ops) =>
            Wall(_walls[lv], x0, x1, z0, z1, FloorY(lv), CeilY(lv), WallMat(lv), ops);

        /// <summary>Vertical paint/sign quad on a wall face. n = outward face normal.</summary>
        private static void WallQuad(ChunkedBuilder b, Vector3 center, Vector3 n, float halfLen, float halfH, Material m)
        {
            Vector3 u = Vector3.Cross(n, Vector3.up).normalized * halfLen;
            b.Face(center, u, Vector3.up * halfH, m);
        }

        /// <summary>Sign plate (thin box) with centred text, mounted on a surface with outward normal n.</summary>
        private void SignPlate(ChunkedBuilder b, Vector3 surfacePoint, Vector3 n, float w, float h, string text,
            float textH, Material plate, Material textMat)
        {
            var rot = Quaternion.LookRotation(n, Vector3.up);
            b.Box(surfacePoint + n * 0.012f, new Vector3(w, h, 0.024f), plate, rot);
            b.Text(text, surfacePoint + n * 0.03f, n, Vector3.up, textH, textMat);   // 6 mm over the plate
        }

        // ================================================================== spawn points

        private void DefineSpawnPoints()
        {
            var perLevel = new (float x, float z)[]
            {
                (-55f, -44.75f), (25f, -44.75f), (-15f, -28.25f), (50f, -28.25f), (-60f, -11.75f), (15f, 11.75f),
                (-38f, 11.75f), (55f, 28.25f), (-25f, 28.25f), (2.5f, 44.75f), (65f, 44.75f), (-CrossX, 20f),
                (CrossX, -25f), (-30f, -58.1f), (28f, 58.7f)
            };
            for (int lv = 0; lv < Levels; lv++)
                foreach (var (x, z) in perLevel)
                    _spawnPoints.Add(new Vector3(x + lv * 1.5f * Mathf.Sign(x == 0 ? 1 : x) * (Mathf.Abs(x) < 70f ? 1f : 0f), FloorY(lv) + 0.05f, z));
        }

        private void BuildSpawnObjects()
        {
            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                var p = _spawnPoints[i];
                int lv = Mathf.RoundToInt(-p.y / 3.4f);
                var go = new GameObject($"Spawn_{i + 1:00}_{Tag(lv)}");
                go.transform.SetParent(_tSpawns, false);
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, (i * 137) % 360, 0));
                go.tag = GameLayers.SpawnPointTag;
            }
            var seeker = new GameObject("SeekerSpawn");
            seeker.transform.SetParent(_tSpawns, false);
            seeker.transform.SetPositionAndRotation(new Vector3(0f, FloorY(0) + 0.05f, -3.6f), Quaternion.Euler(0, 180f, 0));
        }

        // ================================================================== slabs & beams

        private static List<Rect> SlabHoles()
        {
            var holes = Ramps.Select(r => Rect.MinMaxRect(r.XMin, r.Z0, r.XMax, r.Z1)).ToList();
            holes.AddRange(Stairs.Select(s => Rect.MinMaxRect(s.X0, s.ZA, s.X0 + 2f, s.ZB - Run)));
            return holes;
        }

        private void BuildSlabs()
        {
            var outer = Rect.MinMaxRect(-HX - WallT, -HZ - WallT, HX + WallT, HZ + WallT);
            for (int lv = 0; lv < Levels; lv++)
            {
                bool lowest = lv == Levels - 1;
                var rects = lowest ? new List<Rect> { outer } : Subtract(outer, SlabHoles());
                foreach (var r in rects)
                {
                    var c = new Vector3(r.center.x, FloorY(lv) - Slab * 0.5f, r.center.y);
                    var s = new Vector3(r.width, Slab, r.height);
                    _slabs[lv].Box(c, s, FloorMat(lv), null, MeshBuilder.Faces.PosY);
                    if (!lowest) Under(lv + 1).Box(c, s, CeilMat(lv + 1), null, MeshBuilder.Faces.All & ~MeshBuilder.Faces.PosY);
                }
            }
            _roof.Box(new Vector3(0, CeilY(0) + Slab * 0.5f, 0), new Vector3(outer.width, Slab, outer.height), CeilMat(0), null,
                MeshBuilder.Faces.NegY | MeshBuilder.Faces.PosY);
        }

        private void BuildBeams()
        {
            foreach (float z in ColZ)
            {
                var faces = MeshBuilder.Faces.All & ~MeshBuilder.Faces.PosY;
                _roof.Box(new Vector3(0, CeilY(0) - BeamDepth * 0.5f, z), new Vector3(2 * HX, BeamDepth, 0.5f), CeilMat(0), null, faces);
                for (int lv = 1; lv < Levels; lv++)
                    Under(lv).Box(new Vector3(0, CeilY(lv) - BeamDepth * 0.5f, z), new Vector3(2 * HX, BeamDepth, 0.5f), CeilMat(lv), null, faces);
            }
        }

        // ================================================================== perimeter

        private void BuildPerimeter(int lv)
        {
            var b = _walls[lv];
            float y0 = FloorY(lv);
            LevelWall(lv, -HX - WallT, HX + WallT, -HZ - WallT, -HZ);
            LevelWall(lv, -HX - WallT, HX + WallT, HZ, HZ + WallT);
            LevelWall(lv, -HX - WallT, -HX, -HZ, HZ);
            // Level 0: street exit opening; the roller shutter itself is built at runtime (Game/Cinematics/ExitGate).
            LevelWall(lv, HX, HX + WallT, -HZ, HZ, lv == 0 ? new[] { new Opening(-3.5f, 3.5f, 2.75f) } : new Opening[0]);

            void Band(Vector3 n, float along0, float along1, float fixedCoord, bool alongX)
            {
                float len = along1 - along0;
                float mid = (along0 + along1) * 0.5f;
                Vector3 P(float y) => (alongX ? new Vector3(mid, y, fixedCoord) : new Vector3(fixedCoord, y, mid)) + n * 0.012f;
                WallQuad(b, P(y0 + 0.06f), n, len * 0.5f, 0.06f, M("Paint_Grey"));
                WallQuad(b, P(y0 + 0.56f), n, len * 0.5f, 0.44f, LevelPaint(lv));
                WallQuad(b, P(y0 + 1.04f), n, len * 0.5f, 0.04f, M("Paint_White"));
            }
            Band(Vector3.forward, -HX, HX, -HZ, true);
            Band(Vector3.back, -HX, HX, HZ, true);
            Band(Vector3.right, -HZ, HZ, -HX, false);
            if (lv == 0)
            {
                Band(Vector3.left, -HZ, -4.2f, HX, false);
                Band(Vector3.left, 4.2f, HZ, HX, false);
            }
            else Band(Vector3.left, -HZ, HZ, HX, false);

            // pilasters (not inside the tunnel, stairs, lobby, corridor or dock)
            foreach (float x in ColX)
            {
                bool blocked = Stairs.Any(s => s.Zf > 40f && x > s.X0 - 0.6f && x < s.X0 + StairWidth + 0.6f) ||
                               (x > -6.5f && x < 6.5f) || (x > 11.5f && x < 44.5f) || (lv == 0 && x > -85f && x < -63f);
                if (!blocked) AddPilaster(lv, new Vector3(x, 0, HZ), Vector3.back);
            }
            foreach (float z in ColZ)
            {
                AddPilaster(lv, new Vector3(-HX, 0, z), Vector3.right);
                AddPilaster(lv, new Vector3(HX, 0, z), Vector3.left);
            }

            var white = M("Text_White");
            for (float x = -75f; x <= 75f; x += 30f)
                b.Text(LevelName(lv), new Vector3(x + 7.5f, y0 + 1.75f, HZ - 0.015f), Vector3.back, Vector3.up, 0.7f, white);
            foreach (float z in new[] { -40f, -28f, 28f, 40f })
            {
                b.Text(LevelName(lv), new Vector3(-HX + 0.015f, y0 + 1.75f, z), Vector3.right, Vector3.up, 0.7f, white);
                b.Text(LevelName(lv), new Vector3(HX - 0.015f, y0 + 1.75f, z), Vector3.left, Vector3.up, 0.7f, white);
            }
            for (int j = 0; j < ColZ.Length; j++)
            {
                b.Text(ColRow[j], new Vector3(-HX + 0.015f, y0 + 1.75f, ColZ[j] + 1.6f), Vector3.right, Vector3.up, 0.5f, white);
                b.Text(ColRow[j], new Vector3(HX - 0.015f, y0 + 1.75f, ColZ[j] - 1.6f), Vector3.left, Vector3.up, 0.5f, white);
            }

            if (lv == 0)
            {
                // street exit on the east wall: lintel here, the shutter, rails and the street behind at runtime (ExitGate)
                b.BoxMinMax(new Vector3(HX - 0.45f, y0 + 2.75f, -3.7f), new Vector3(HX, CeilY(lv), 3.7f), M("PM_Grey"));
                var gate = new GameObject("ExitGateAnchor").transform;
                gate.SetParent(_root, false);
                gate.SetPositionAndRotation(new Vector3(HX, y0, 0f), Quaternion.Euler(0f, -90f, 0f));
                SignPlate(_signs[lv], new Vector3(HX - 0.45f, y0 + 2.62f, 0f), Vector3.left, 1.8f, 0.26f, "EXIT CLOSED", 0.15f, M("Sign_Red"), white);
            }
        }

        private void AddPilaster(int lv, Vector3 basePos, Vector3 n)
        {
            var b = _walls[lv];
            float y0 = FloorY(lv), y1 = CeilY(lv);
            Vector3 right = Vector3.Cross(n, Vector3.up);
            Vector3 c = basePos + n * 0.175f;
            Vector3 size = new Vector3(Mathf.Abs(right.x) * 0.6f + Mathf.Abs(n.x) * 0.35f, 0, Mathf.Abs(right.z) * 0.6f + Mathf.Abs(n.z) * 0.35f);
            b.Box(new Vector3(c.x, (y0 + 1f + y1) * 0.5f, c.z), new Vector3(size.x, y1 - y0 - 1f, size.z), ColumnMat(lv));
            b.Box(new Vector3(c.x, y0 + 0.5f, c.z) + n * 0.005f, new Vector3(size.x + 0.01f, 1f, size.z + 0.01f), HazardMat(lv));
        }

        // ================================================================== columns

        private static bool ColumnBlocked(float x, float z)
        {
            foreach (var r in Ramps)
                if (x > r.XMin - 0.8f && x < r.XMax + 0.8f && z > r.Z0 - 0.8f && z < r.Z1 + 0.8f) return true;
            foreach (var s in Stairs)
                if (x > s.X0 - 0.8f && x < s.X0 + StairWidth + 0.8f && z > s.Zf - 0.5f && z < s.ZIn + 0.5f) return true;
            return false;
        }

        private void BuildColumns(int lv)
        {
            var b = _columns[lv];
            var s = _signs[lv];
            float y0 = FloorY(lv), y1 = CeilY(lv);
            for (int j = 0; j < ColZ.Length; j++)
            for (int i = 0; i < ColX.Length; i++)
            {
                float x = ColX[i], z = ColZ[j];
                if (ColumnBlocked(x, z)) continue;
                b.Box(new Vector3(x, (y0 + 1f + y1) * 0.5f, z), new Vector3(0.6f, y1 - y0 - 1f, 0.6f), ColumnMat(lv));
                b.Box(new Vector3(x, y0 + 0.5f, z), new Vector3(0.6f, 1f, 0.6f), HazardMat(lv), null, MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
                b.Box(new Vector3(x, y0 + 1.07f, z), new Vector3(0.612f, 0.14f, 0.612f), LevelPaint(lv), null,
                    MeshBuilder.Faces.PosX | MeshBuilder.Faces.NegX | MeshBuilder.Faces.PosZ | MeshBuilder.Faces.NegZ);
                foreach (float cx in new[] { -0.3f, 0.3f })
                foreach (float cz in new[] { -0.3f, 0.3f })
                    b.Box(new Vector3(x + cx, y0 + 0.6f, z + cz), new Vector3(0.05f, 1.2f, 0.05f), M("Metal_Dark"));
                string id = ColRow[j] + (i + 1);
                foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    var p = new Vector3(x, y0 + 1.95f, z) + n * 0.3f;
                    SignPlate(s, p, n, 0.5f, 0.34f, id, 0.2f, M("Sign_White"), M("Text_Black"));
                    s.Text(LevelName(lv), new Vector3(x, y0 + 1.6f, z) + n * 0.306f, n, Vector3.up, 0.12f, M("Text_White"));
                }
                // damaged columns on the oldest level: cracks and water stains
                if (lv == 2 && _rng.NextDouble() < 0.45)
                {
                    var n = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right }[_rng.Next(4)];
                    Vector3 right = Vector3.Cross(n, Vector3.up);
                    s.FaceUV(new Vector3(x, y0 + R(1.3f, 2.3f), z) + n * 0.312f, right * 0.3f, Vector3.up * 0.6f, M("Crack"), Vector2.zero, Vector2.one);
                }
            }
        }

        // ================================================================== bays & cars

        private bool ConstructionZone(int lv, float x, float z) => lv == 2 && x > -75f && x < -45f && z > 15f && z < 25f;

        private void BuildBays(int lv)
        {
            float y0 = FloorY(lv);
            var rows = new (float zMin, float zMax, bool aisleAtMin, string letter, float xa, float xb, bool noVan)[]
            {
                (-53f, -48f, false, "S", -ParkX, ParkX, true),
                (-41.5f, -36.5f, true, "A", -ParkX, ParkX, false), (-36.5f, -31.5f, false, "B", -ParkX, ParkX, false),
                (-25f, -20f, true, "C", -ParkX, ParkX, false), (-20f, -15f, false, "D", -ParkX, ParkX, false),
                (15f, 20f, true, "E", -ParkX, ParkX, false), (20f, 25f, false, "F", -ParkX, ParkX, false),
                (31.5f, 36.5f, true, "G", -ParkX, ParkX, false), (36.5f, 41.5f, false, "H", -ParkX, ParkX, false),
                (3.5f, 8.5f, false, "J", -65f, -45f, true), (-8.5f, -3.5f, true, "K", 45f, 65f, true),
            };
            foreach (var row in rows)
            {
                Vector3 inward = row.aisleAtMin ? Vector3.forward : Vector3.back;
                int count = Mathf.RoundToInt((row.xb - row.xa) / 2.5f);
                for (int i = 0; i < count; i++)
                {
                    float xa = row.xa + i * 2.5f;
                    var c = new Vector3(xa + 1.25f, y0, (row.zMin + row.zMax) * 0.5f);
                    bool ev = lv == 1 && (row.letter == "C" || row.letter == "D") && c.x < -50f;
                    AddBay(lv, c, inward, 2.5f, row.zMax - row.zMin, row.letter + (i + 1).ToString("00"),
                        ConstructionZone(lv, c.x, c.z), row.noVan, ev);
                }
            }
            // perpendicular bays along the side walls
            for (int i = 0; i < 13; i++)
            {
                foreach (float zs in new[] { -45f, 12.5f })
                {
                    float z = zs + i * 2.5f + 1.25f;
                    AddBay(lv, new Vector3(-87.5f, y0, z), Vector3.left, 2.5f, 5f, "W" + (zs < 0 ? i + 1 : i + 14).ToString("00"), false, false, false);
                    AddBay(lv, new Vector3(87.5f, y0, z), Vector3.right, 2.5f, 5f, "X" + (zs < 0 ? i + 1 : i + 14).ToString("00"), false, false, false);
                }
            }
        }

        private readonly List<Vector3> _evChargers = new List<Vector3>();

        private void AddBay(int lv, Vector3 center, Vector3 inward, float width, float depth, string label, bool noCars,
            bool noVan, bool ev)
        {
            _bays.Add(new Bay { Center = center, Inward = inward, Width = width, Depth = depth, Label = label, Level = lv, NoCars = noCars, NoVan = noVan });
            var d = _decals[lv];
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            float y = center.y + DecalY.Paint;
            if (ev)
            {
                var c = center;
                c.y = center.y + DecalY.Under;
                d.Face(c, side * (width * 0.5f - 0.1f), inward * (depth * 0.5f), M("Paint_Green"));
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 c = center + side * (s * width * 0.5f) + inward * 0.1f;
                c.y = y;
                d.Face(c, side * 0.06f, inward * (depth * 0.5f - 0.1f), M("Paint_White"));
            }
            Vector3 tp = center - inward * (depth * 0.5f - 0.75f);
            tp.y = center.y + DecalY.Mark;
            d.Text(ev ? "EV" : label, tp, Vector3.up, inward, 0.38f, M("Text_Floor"));
            if (noCars) return;
            Vector3 ws = center + inward * (depth * 0.5f - 0.75f);
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            _decor[lv].Box(new Vector3(ws.x, center.y + 0.06f, ws.z), new Vector3(1.6f, 0.12f, 0.16f), M("Concrete_Yellow"), rot,
                MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
            if (ev)
            {
                var cp = center + inward * (depth * 0.5f - 0.05f);
                // Back-to-back EV bays share one (double) charger: the second one stood inside the first.
                if (_evChargers.Exists(e => (e - cp).sqrMagnitude < 1f)) return;
                _evChargers.Add(cp);
                PlaceProp(_props.EvCharger(), "EvCharger_" + label, _tPropsLv[lv], cp, Quaternion.LookRotation(-inward).eulerAngles.y);
            }
        }

        private void PlaceCars()
        {
            int[] targets = { 64, 64, 52 };
            for (int lv = 0; lv < Levels; lv++)
            {
                var candidates = _bays.Where(b => b.Level == lv && !b.NoCars).ToList();
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }
                candidates = candidates.Where(b => !HeroReserved(b.Level, b.Label)).ToList();
                for (int k = 0; k < targets[lv] && k < candidates.Count; k++)
                {
                    var bay = candidates[k];
                    double r = _rng.NextDouble();
                    var model = PickParked(r, bay.NoVan);
                    PlaceCar(lv, model, bay.Center, bay.Inward, bay.Depth, bay.Label, _rng.NextDouble() < 0.65);
                }
            }
            PlaceHeroCars();
        }

        public int CarCount => _carCount;

        // ================================================================== aisles & floor markings

        private void BuildAisleMarkings(int lv)
        {
            var d = _decals[lv];
            float y = FloorY(lv) + DecalY.Paint, yMark = FloorY(lv) + DecalY.Mark;
            float[][] bumps =
            {
                new[] { -48.75f, 33.75f }, new[] { -18.75f, 56.25f }, new[] { -63.75f, 18.75f },
                new[] { -26.25f, 48.75f }, new[] { -56.25f, 11.25f }, new[] { -33.75f, 63.75f }
            };
            for (int a = 0; a < AisleZ.Length; a++)
            {
                float z = AisleZ[a];
                for (float x = -74f; x < 74f; x += 3f)
                    d.FloorQuad(x, x + 1.5f, z - 0.06f, z + 0.06f, y, M("Paint_Yellow"));
                Vector3 dir = AisleDir[a] > 0 ? Vector3.right : Vector3.left;
                foreach (float x in new[] { -60f, -35f, 25f, 55f })
                    d.FloorDecal(new Vector3(x, yMark, z - 1.6f * AisleDir[a]), dir, 1.0f, 2.2f, M("Arrow_White"));
                d.FloorQuad(-ParkX, ParkX, z - AisleHalf + 0.02f, z - AisleHalf + 0.12f, y, M("Paint_White"));
                d.FloorQuad(-ParkX, ParkX, z + AisleHalf - 0.12f, z + AisleHalf - 0.02f, y, M("Paint_White"));
                foreach (float bx in bumps[a])
                    SpeedBump(lv, new Vector3(bx, FloorY(lv), z), Vector3.forward, AisleHalf * 2f - 0.4f);
            }
            // zebra crossings to the central and north lobbies
            foreach (int a in new[] { 2, 3, 5 })
                for (float x = -1.95f; x < 1.6f; x += 0.9f)
                    d.FloorQuad(x, x + 0.45f, AisleZ[a] - AisleHalf + 0.3f, AisleZ[a] + AisleHalf - 0.3f, FloorY(lv) + DecalY.Hatch, M("Paint_White"));   // over the aisle lines
            // cross aisles
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * CrossX;
                for (float z = -51f; z < 47f; z += 3f)
                    d.FloorQuad(x - 0.06f, x + 0.06f, z, z + 1.5f, y, M("Paint_Yellow"));
                Vector3 dir = sx < 0 ? Vector3.back : Vector3.forward;
                foreach (float z in new[] { -40f, -20f, 18f, 36f })
                    d.FloorDecal(new Vector3(x + 2f * sx, yMark, z), dir, 1.0f, 2.2f, M("Arrow_White"));
                foreach (float z in new[] { -32f, 30f })
                    SpeedBump(lv, new Vector3(x, FloorY(lv), z), Vector3.right, 8f);
                d.Text(LevelName(lv), new Vector3(x, yMark, -14f), Vector3.up, dir, 1.4f, M("Text_Floor"));
            }
            // walkways: in front of the north rooms and along the tunnel wall
            d.FloorQuad(-84f, 84f, 48.15f, 48.25f, y, M("Paint_Yellow"));
            d.FloorQuad(-84f, 84f, -53.25f, -53.15f, y, M("Paint_Yellow"));
            for (float x = -84f; x < 84f; x += 12f)
                d.Text("WALK", new Vector3(x + 6f, yMark, -54.6f), Vector3.up, Vector3.right, 0.5f, M("Text_FloorYellow"));
        }

        private void SpeedBump(int lv, Vector3 c, Vector3 across, float length)
        {
            var rot = Quaternion.LookRotation(across, Vector3.up);
            var b = _decor[lv];
            b.Box(c + Vector3.up * 0.025f, new Vector3(0.55f, 0.05f, length), HazardMat(lv), rot, MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
            b.Box(c + Vector3.up * 0.065f, new Vector3(0.32f, 0.03f, length), HazardMat(lv), rot, MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
        }

        /// <summary>Oil stains, cracks, puddles, flooding and drains per level.</summary>
        private void BuildGrime(int lv)
        {
            var d = _decals[lv];
            float y0 = FloorY(lv);
            foreach (var bay in _bays.Where(b => b.Level == lv))
            {
                if (_rng.NextDouble() < 0.4)
                {
                    var c = bay.Center + Vector3.Cross(Vector3.up, bay.Inward) * R(-0.4f, 0.4f) + bay.Inward * R(-1f, 1f);
                    c.y = y0 + DecalY.Stain;
                    float s = R(0.6f, 1.6f);
                    d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.forward * 0.01f, s, s * R(0.8f, 1.3f), M("Oil_Stain"));
                }
            }
            int cracks = lv == 2 ? 110 : 40;
            for (int i = 0; i < cracks; i++)
            {
                var c = _rng.NextDouble() < 0.7
                    ? new Vector3(R(-74f, 74f), y0 + DecalY.Crack, AisleZ[_rng.Next(AisleZ.Length)] + R(-2.5f, 2.5f))
                    : new Vector3((_rng.NextDouble() < 0.5 ? -1f : 1f) * (CrossX + R(-3f, 3f)), y0 + DecalY.Crack, R(-50f, 46f));
                float s = lv == 2 ? R(2f, 4.5f) : R(1.5f, 3.5f);
                d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.right * 0.01f, s, s, M("Crack"));
            }
            int puddles = lv == 0 ? 14 : lv == 1 ? 24 : 55;
            for (int i = 0; i < puddles; i++)
            {
                Vector3 c;
                int sel = _rng.Next(5);
                if (sel == 0) c = new Vector3(R(-73f, 73f), 0, AisleZ[_rng.Next(AisleZ.Length)] + R(-2f, 2f));
                else if (sel == 1) c = new Vector3((_rng.NextDouble() < 0.5 ? -1f : 1f) * (CrossX + R(-3f, 3f)), 0, R(-50f, 45f));
                else if (sel == 2) c = new Vector3(R(-84f, 84f), 0, -58.1f + R(-0.8f, 0.8f));
                else if (sel == 3) c = new Vector3(R(-80f, 80f), 0, R(-55.5f, -53.5f));
                else c = new Vector3(ColX[_rng.Next(ColX.Length)] + R(-1.2f, 1.2f), 0, ColZ[_rng.Next(ColZ.Length)] + R(-1.5f, 1.5f));
                var p = new Vector3(c.x, y0 + DecalY.Puddle, c.z);
                float scale = lv == 2 ? 1.5f : 1f;
                d.FloorDecal(p, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.forward * 0.01f, R(1.2f, 3.2f) * scale, R(1.5f, 4f) * scale, M("Puddle"));
            }
            if (lv == 2)
            {
                // partially flooded south-east quarter
                foreach (var (cx, cz, w, l, yo) in new[] { (46f, -44f, 34f, 22f, DecalY.Flood), (66f, -33f, 18f, 12f, DecalY.Flood + 0.004f), (34f, -30f, 10f, 7f, DecalY.Flood + 0.008f) })
                    d.FloorDecal(new Vector3(cx, y0 + yo, cz), new Vector3(R(-0.15f, 0.15f), 0, 1f), w, l, M("Flood_Water"));
                // water stains running down the walls
                for (int i = 0; i < 45; i++)
                {
                    bool ew = _rng.NextDouble() < 0.4;
                    Vector3 n, p;
                    if (ew)
                    {
                        float sx = _rng.NextDouble() < 0.5 ? -1f : 1f;
                        n = sx < 0 ? Vector3.right : Vector3.left;
                        p = new Vector3(sx * HX - n.x * -0.02f, y0 + R(1.4f, 2.2f), R(-55f, 55f));
                    }
                    else
                    {
                        n = Vector3.back;
                        p = new Vector3(R(-85f, 85f), y0 + R(1.4f, 2.2f), HZ - 0.02f);
                    }
                    var right = Vector3.Cross(n, Vector3.up);
                    _signs[lv].FaceUV(p, right * R(0.4f, 0.9f), Vector3.up * R(0.8f, 1.4f), M("Oil_Stain"), Vector2.zero, Vector2.one);
                }
            }
            foreach (float x in new[] { -63.75f, -33.75f, -3.75f, 26.25f, 56.25f })
            foreach (float z in new[] { -36.5f, -20f, 20f, 36.5f })
                d.FloorQuad(x - 0.25f, x + 0.25f, z - 0.25f, z + 0.25f, y0 + DecalY.Stain, M("Grate"));
        }

        // ================================================================== ceiling services

        public static readonly Vector3[] FlickerSpots =
        {
            new Vector3(-33.75f, 0, -28.25f), new Vector3(26.25f, 0, 28.25f),
            new Vector3(-56.25f, 1, -44.75f), new Vector3(11.25f, 1, 11.75f), new Vector3(48.75f, 1, 44.75f),
            new Vector3(-63.75f, 2, -11.75f), new Vector3(-18.75f, 2, -28.25f), new Vector3(18.75f, 2, 28.25f),
            new Vector3(56.25f, 2, -44.75f), new Vector3(3.75f, 2, 44.75f)
        };

        private static bool IsFlicker(int lv, float x, float z) =>
            FlickerSpots.Any(f => (int)f.y == lv && Mathf.Abs(f.x - x) < 0.1f && Mathf.Abs(f.z - z) < 0.1f);

        private void DeadFixture(ChunkedBuilder b, Vector3 p, bool alongX, bool broken = false)
        {
            var rot = alongX ? Quaternion.identity : Quaternion.Euler(0, 90f, 0);
            if (broken)
            {
                // one hanger snapped: fixture dangles at an angle
                rot = rot * Quaternion.Euler(0, 0, 28f);
                p += Vector3.down * 0.35f;
                b.Cylinder(p + rot * new Vector3(0.6f, 0, 0), p + rot * new Vector3(0.6f, 0, 0) + Vector3.up * 0.1f + Vector3.up * 0.55f, 0.006f, 4, M("Metal_Dark"));
            }
            b.Box(p + rot * (Vector3.down * 0.05f), new Vector3(1.3f, 0.1f, 0.22f), M("PM_Grey"), rot);
            b.Box(p + rot * (Vector3.down * 0.105f), new Vector3(1.22f, 0.012f, 0.16f), M("Fixture_Dead"), rot);
        }

        private void BuildCeilingServices(int lv)
        {
            var b = _ceiling[lv];
            float c = CeilY(lv), y0 = FloorY(lv);
            bool old = lv == 2;

            foreach (float z in AisleZ)
                for (float x = -71.25f; x < 72f; x += 7.5f)
                    if (!IsFlicker(lv, x, z)) DeadFixture(b, new Vector3(x, c, z), true, old && _rng.NextDouble() < 0.25);
            foreach (float x in new[] { -CrossX, CrossX })
            foreach (float z in new[] { -42f, -28f, -14f, 1f, 14f, 28f, 42f })
                DeadFixture(b, new Vector3(x, c, z), false, old && _rng.NextDouble() < 0.25);
            for (float x = -81f; x < 82f; x += 9f)
                DeadFixture(b, new Vector3(x, c, -58.1f), true, old && _rng.NextDouble() < 0.3);

            // ventilation ducts and cable trays
            foreach (float z in new[] { -24f, 24f })
                Duct(b, -89.5f, 89.5f, z, c);
            foreach (float z in new[] { -39.6f, 39.6f })
            {
                float yt = c - 0.3f;
                b.Box(new Vector3(0, yt, z), new Vector3(178f, 0.02f, 0.45f), M("Metal_Galvanized"));
                b.Box(new Vector3(0, yt + 0.04f, z - 0.225f), new Vector3(178f, 0.08f, 0.015f), M("Metal_Galvanized"));
                b.Box(new Vector3(0, yt + 0.04f, z + 0.225f), new Vector3(178f, 0.08f, 0.015f), M("Metal_Galvanized"));
                for (int k = 0; k < 4; k++)
                    b.Cylinder(new Vector3(-89f, yt + 0.035f, z - 0.15f + k * 0.1f), new Vector3(89f, yt + 0.035f, z - 0.15f + k * 0.1f), 0.025f, 6, M(k == 2 ? "PM_Orange" : "Plastic_Black"), false);
                for (float x = -88f; x < 89f; x += 2.5f)
                {
                    b.Box(new Vector3(x, c - 0.15f, z - 0.25f), new Vector3(0.03f, 0.3f, 0.03f), M("Metal_Dark"));
                    b.Box(new Vector3(x, c - 0.15f, z + 0.25f), new Vector3(0.03f, 0.3f, 0.03f), M("Metal_Dark"));
                    b.Box(new Vector3(x, yt - 0.02f, z), new Vector3(0.04f, 0.03f, 0.6f), M("Metal_Dark"));
                }
            }
            // sprinklers
            float sprY = c - 0.15f, mainY = c - 0.6f;
            foreach (float z in new[] { -52f, -33f, -26f, -16f, 16f, 26f, 33f, 46.5f })
            {
                b.Cylinder(new Vector3(-89f, sprY, z), new Vector3(89f, sprY, z), 0.035f, 8, M(old ? "Rust" : "PM_Red"), false);
                for (float x = -87.5f; x < 89f; x += 3f)
                {
                    b.Cylinder(new Vector3(x, sprY, z), new Vector3(x, sprY - 0.12f, z), 0.012f, 6, M("Metal_Steel"));
                    b.Cylinder(new Vector3(x, sprY - 0.12f, z), new Vector3(x, sprY - 0.13f, z), 0.04f, 8, M("Metal_Steel"));
                }
                foreach (float x in new[] { -78f, 78f })
                    b.Cylinder(new Vector3(x, sprY, z), new Vector3(x, mainY, z), 0.035f, 8, M("PM_Red"), false);
            }
            foreach (float x in new[] { -78f, 78f })
            {
                b.Cylinder(new Vector3(x, mainY, -55.5f), new Vector3(x, mainY, 47.5f), 0.07f, 10, M("PM_Red"));
                for (float z = -54f; z < 47f; z += 4f)
                    b.Cylinder(new Vector3(x, mainY, z), new Vector3(x, c, z), 0.012f, 4, M("Metal_Dark"), false);
            }
            // big drain pipes along the cross aisles
            foreach (float x in new[] { -72.6f, 72.6f })
            {
                b.Cylinder(new Vector3(x, c - 0.75f, -55.8f), new Vector3(x, c - 0.75f, 47.5f), 0.1f, 12, M("PM_Grey"));
                for (float z = -54f; z < 47f; z += 3.5f)
                {
                    b.Cylinder(new Vector3(x, c - 0.75f, z), new Vector3(x, c, z), 0.012f, 4, M("Metal_Dark"), false);
                    b.Cylinder(new Vector3(x, c - 0.75f, z - 0.05f), new Vector3(x, c - 0.75f, z + 0.05f), 0.115f, 12, M("Metal_Dark"));
                }
            }
            // down pipes on aisle-side columns
            for (int i = 1; i < ColX.Length - 1; i += 4)
            {
                foreach (var (z, off) in new[] { (ColZ[0], 0.38f), (ColZ[7], -0.38f) })
                {
                    var p = new Vector3(ColX[i] + 0.2f, 0, z + off);
                    b.Cylinder(new Vector3(p.x, y0, p.z), new Vector3(p.x, c, p.z), 0.07f, 10, M("PM_Grey"));
                    for (float yy = y0 + 0.6f; yy < c; yy += 1.1f)
                        b.Box(new Vector3(p.x - 0.04f, yy, p.z - 0.04f), new Vector3(0.18f, 0.04f, 0.18f), M("Metal_Dark"));
                }
            }
            // service tunnel: pipe bundle on the outer wall
            foreach (var (yy, mat, r) in new[] { (2.0f, "PM_Green", 0.12f), (2.35f, "PM_Blue", 0.08f), (2.62f, "PM_Grey", 0.09f), (0.5f, "Metal_Dark", 0.06f) })
            {
                b.Cylinder(new Vector3(-85.8f, y0 + yy, -HZ + 0.25f), new Vector3(85.8f, y0 + yy, -HZ + 0.25f), r, 10, M(old && mat != "Metal_Dark" ? "Rust" : mat));
            }
            for (float x = -84f; x < 85f; x += 3f)
                b.Box(new Vector3(x, y0 + 2.3f, -HZ + 0.15f), new Vector3(0.05f, 0.9f, 0.3f), M("Metal_Dark"));
            // corridor pipes along the north wall
            foreach (var (yy, mat, r) in new[] { (2.2f, "PM_Green", 0.09f), (2.45f, "PM_Blue", 0.06f), (2.65f, "PM_Grey", 0.07f) })
                b.Cylinder(new Vector3(12.2f, y0 + yy, HZ - 0.2f), new Vector3(43.8f, y0 + yy, HZ - 0.2f), r, 10, M(old ? "Rust" : mat));
        }

        private void Duct(ChunkedBuilder b, float x0, float x1, float z, float c)
        {
            const float h = 0.45f, w = 0.9f;
            float y = c - 0.06f - h * 0.5f;
            b.Box(new Vector3((x0 + x1) * 0.5f, y, z), new Vector3(x1 - x0, h, w), M("Metal_Galvanized"));
            for (float x = x0 + 0.75f; x < x1; x += 1.5f)
                b.Box(new Vector3(x, y, z), new Vector3(0.05f, h + 0.06f, w + 0.06f), M("Metal_Galvanized"));
            for (float x = x0 + 1.5f; x < x1; x += 3f)
                b.Box(new Vector3(x, c - 0.03f, z), new Vector3(0.05f, 0.06f, w + 0.25f), M("Metal_Dark"));
            for (float x = x0 + 3f; x < x1; x += 6f)
                b.Box(new Vector3(x, y - h * 0.5f - 0.005f, z), new Vector3(0.6f, 0.01f, 0.4f), M("Grate"));
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
            l.range = 3.2f;
            l.intensity = 0.55f;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            _lightCount++;
        }

        private void FlickerLamp(Vector3 pos, string name, FlickeringLight.Pattern pattern, int seed, float range, float intensity,
            LightShadows shadows, bool fixture = true)
        {
            var go = fixture
                ? PlaceProp(_props.LightFixture(true), name, _tLights, pos, 0f, null, false)
                : new GameObject(name);
            if (!fixture) { go.transform.SetParent(_tLights, false); go.transform.position = pos; }
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            var lgo = new GameObject("Light");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(0, -0.3f, 0);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.86f, 0.66f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = shadows;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            var fl = lgo.AddComponent<FlickeringLight>();
            fl.Configure(pattern, intensity, fixture ? go.GetComponent<MeshRenderer>() : null, new Color(1f, 0.86f, 0.62f), seed);
            _lightCount++;
        }

        private void BuildLights()
        {
            // EXIT signs above every stairwell door; a few carry a tiny green light
            var lit = new HashSet<(int, int)> { (0, 2), (0, 1), (1, 3), (1, 0), (2, 2), (2, 1) };
            for (int si = 0; si < Stairs.Length; si++)
            {
                var s = Stairs[si];
                for (int lv = 0; lv < Levels; lv++)
                {
                    float door = lv == 0 ? s.X0 + 1f : s.X0 + 3f;
                    ExitSign(new Vector3(door, FloorY(lv) + 2.38f, s.Zf), 180f, false, ' ', lit.Contains((lv, si)), $"ExitSign_Stairs{si + 1}_{Tag(lv)}");
                }
            }
            // hanging signs over the aisles (emissive only)
            for (int lv = 0; lv < Levels; lv++)
            {
                ExitSign(new Vector3(-50f, CeilY(lv) - 0.5f, AisleZ[2]), 90f, true, '>', false, $"ExitSign_Hanging_{Tag(lv)}_a");
                ExitSign(new Vector3(50f, CeilY(lv) - 0.5f, AisleZ[3]), -90f, true, '<', false, $"ExitSign_Hanging_{Tag(lv)}_b");
                ExitSign(new Vector3(-20f, CeilY(lv) - 0.5f, AisleZ[5]), 90f, true, '^', false, $"ExitSign_Hanging_{Tag(lv)}_c");
            }
            // broken flickering ceiling lamps
            for (int i = 0; i < FlickerSpots.Length; i++)
            {
                var f = FlickerSpots[i];
                int lv = (int)f.y;
                var pattern = (i % 3 == 1) ? FlickeringLight.Pattern.Dying : FlickeringLight.Pattern.Unstable;
                FlickerLamp(new Vector3(f.x, CeilY(lv), f.z), $"FlickeringLamp_{Tag(lv)}_{i + 1}", pattern, 17 + i * 31, 7f, 0.9f,
                    i == 0 ? LightShadows.Soft : LightShadows.None);
            }
            // dim lamp inside the security booth (level -1)
            FlickerLamp(new Vector3(80.5f, FloorY(0) + 2.3f, -9.5f), "SecurityBoothLamp_L1", FlickeringLight.Pattern.Unstable, 999, 4f, 0.5f,
                LightShadows.None, false);
        }
    }
}
