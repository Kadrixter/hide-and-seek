using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Garage CCTV cameras: where they hang (27 per level: aisles, ends, stairs, both doors of each elevator, ramps, inside the security room), their models with a red LED, and motion detection.</summary>
    public partial class SecurityRooms
    {
        private const float MotionRange = 36f, MotionHalfAngle = 33f, MotionMinSpeed = 0.5f;

        private struct CamPoint
        {
            public Vector3 Pos;      // world
            public Quaternion Rot;
            public int Level;
            public string Name;
            public float LastMotion;
            public float LastShown;  // last time it was on a monitor (idle cycling)
            public Renderer Led;     // the camera model (its LED sub-mesh glows through a property block)
            public Renderer Halo;    // soft red glow around the LED
            public Light Glow;       // tiny red light while watched (made on demand)
        }

        private readonly List<CamPoint> _cams = new();
        private readonly List<int>[] _levelCams = { new(), new(), new() };
        private readonly Dictionary<ulong, Vector3> _prev = new();
        private readonly MaterialPropertyBlock _ledMpb = new();
        private static Material[] _cctvMats;
        private static Material _haloMat;
        private float _nextSense, _lastSense;

        private void AddCam(int lv, string name, Vector3 pos, Vector3 dir)
        {
            _levelCams[lv].Add(_cams.Count);
            var rot = Quaternion.LookRotation(dir);
            var (model, halo) = BuildCameraModel(pos, rot, lv);
            _cams.Add(new CamPoint { Pos = pos, Rot = rot, Level = lv, Name = name, LastMotion = -99f, LastShown = -99f, Led = model, Halo = halo });
        }

        /// <summary>CCTV camera hanging from the ceiling (<see cref="CctvModel"/>: housing, sun hood, lens, bracket, cable, LED) plus the LED halo.</summary>
        private (Renderer model, Renderer halo) BuildCameraModel(Vector3 pos, Quaternion rot, int lv)
        {
            if (_cctvMats == null || _cctvMats.Length == 0 || _cctvMats[0] == null) { _cctvMats = CctvModel.Materials(); foreach (var mat in _cctvMats) Keep(mat); }
            var root = new GameObject("CCTV").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(pos, rot);
            if (!_cctvMeshes.TryGetValue((pos, rot), out var cm) || cm == null) _cctvMeshes[(pos, rot)] = cm = Keep(CctvModel.Build(pos, rot, FloorY(lv) + 3.0f));
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = cm;
            var mr = root.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = _cctvMats;
            CctvModel.Setup(mr);
            if (_haloMat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/HnsLedGlow");
                if (sh != null) _haloMat = Keep(new Material(sh) { name = "CctvLedHalo" });
            }
            Renderer halo = null;
            if (_haloMat != null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "LedHalo";
                q.transform.SetParent(root, false);
                q.transform.localPosition = CctvModel.LedLocal + Vector3.forward * 0.004f;
                q.transform.localScale = Vector3.one * 0.24f;
                halo = q.GetComponent<MeshRenderer>();
                halo.sharedMaterial = _haloMat;
                halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                halo.receiveShadows = false;
                halo.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                halo.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            HideAndSeek.Map.LevelLighting.SetLevelLayer(root, lv);
            return (mr, halo);
        }

        private GameObject LocalPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color c, Surf s)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            Paint(g.GetComponent<MeshRenderer>(), c, s, scale);
            return g;
        }

        /// <summary>Test helper: is the LED of camera <paramref name="index"/> on level <paramref name="level"/> lit right now.</summary>
        public bool LedOn(int level, int index)
        {
            var mm = MatchManager.Instance;
            if (mm == null || !mm.SecurityWatched.Value) return false;
            int ci = _levelCams[level][index];
            foreach (var s in _slots) if (s.CamIndex == ci) return true;
            return false;
        }

        /// <summary>Test helper: world position of a camera (to look at it from the garage).</summary>
        public Vector3 CameraPosition(int level, int index) => _cams[_levelCams[level][index]].Pos;

        /// <summary>Test helper: pretend camera <paramref name="index"/> of <paramref name="level"/> saw motion just now.</summary>
        public void ForceMotion(int level, int index)
        {
            var list = _levelCams[level];
            if (index < 0 || index >= list.Count) return;
            var c = _cams[list[index]];
            c.LastMotion = Time.time;
            _cams[list[index]] = c;
        }

        private static SortedDictionary<int, float> _elevXCache;
        private static readonly Dictionary<(Vector3, Quaternion), Mesh> _cctvMeshes = new();

        /// <summary>CCTV points of one level (built level by level over frames; the models are cached for the session).</summary>
        private void BuildCameraPoints(int level)
        {
            float[] aisleZ = { -44.75f, -28.25f, -11.75f, 11.75f, 28.25f, 44.75f };
            // Stairwells (Map ParkingGarageBuilder.Stairs): door on the south face, x0 + 1 on level 0, x0 + 3 below.
            var stairs = new[] { (x0: -62f, zf: 50.8f), (x0: 46f, zf: 50.8f), (x0: -34f, zf: -4.6f), (x0: 30f, zf: -4.6f) };
            // Elevators: the scene has "ElevatorCar_<n>_..." markers, one per level; doors face z = +-1.4.
            var elevX = _elevXCache;
            if (elevX == null || elevX.Count == 0)
            {
                elevX = new SortedDictionary<int, float>();
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                {
                    if (!t.name.StartsWith("ElevatorCar_")) continue;
                    var p = t.name.Split('_');
                    if (p.Length >= 2 && int.TryParse(p[1], out int car)) elevX[car] = t.position.x;
                }
                _elevXCache = elevX;
            }

            for (int lv = level; lv <= level; lv++)
            {
                float y0 = FloorY(lv), y = y0 + 2.3f;
                for (int i = 0; i < 6; i++)
                {
                    bool fromWest = i % 2 == 0;
                    AddCam(lv, "ALEJA " + (i + 1), new Vector3(fromWest ? -72f : 72f, y, aisleZ[i]), new Vector3(fromWest ? 1f : -1f, -0.07f, 0f));
                }
                AddCam(lv, "WSCHÓD", new Vector3(79.5f, y, -44f), new Vector3(0f, -0.07f, 1f));
                AddCam(lv, "ZACHÓD", new Vector3(-79.5f, y, 44f), new Vector3(0f, -0.07f, -1f));
                for (int s = 0; s < stairs.Length; s++)
                {
                    float door = lv == 0 ? stairs[s].x0 + 1f : stairs[s].x0 + 3f;
                    var pos = new Vector3(door + 2.5f, y0 + 2.4f, stairs[s].zf - 7f);
                    var look = new Vector3(door, y0 + 1.1f, stairs[s].zf) - pos;
                    AddCam(lv, "SCHODY " + (s + 1), pos, look);
                }
                int e = 0;
                foreach (var kv in elevX)
                {
                    if (e >= 2) break;
                    ++e;
                    // Both door sides of every car (z -1.4 south, z +1.4 north), on every level.
                    var pos = new Vector3(kv.Value + 2.5f, y0 + 2.4f, -7.5f);
                    AddCam(lv, "WINDA " + e + "A", pos, new Vector3(kv.Value, y0 + 1.1f, -1.4f) - pos);
                    pos = new Vector3(kv.Value - 2.5f, y0 + 2.4f, 7.5f);
                    AddCam(lv, "WINDA " + e + "B", pos, new Vector3(kv.Value, y0 + 1.1f, 1.4f) - pos);
                }
                // Inside the security room: north-west corner under its ceiling, looking at the desk and the door.
                var roomCam = new Vector3(X0 + 0.45f, y0 + Height - 0.3f, Z1 - 0.45f);
                AddCam(lv, "OCHRONA", roomCam, new Vector3((DoorA + DoorB) * 0.5f, y0 + 0.9f, Z0 + 0.6f) - roomCam);
                AddExtraCams(lv, y0, y, aisleZ);
            }
        }

        /// <summary>Motion detection: a moving, living player inside the view cone of a camera (line of sight) on its level.</summary>
        private void Sense(float dt)
        {
            if (dt <= 0f) return;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                Vector3 pos = p.transform.position;
                float speed = _prev.TryGetValue(p.NetworkObjectId, out var prev) ? Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(prev.x, 0f, prev.z)) / dt : 0f;
                _prev[p.NetworkObjectId] = pos;
                if (p.Life.Value == LifeState.Dead || speed < MotionMinSpeed || speed > 40f) continue;
                int lv = Mathf.RoundToInt(-pos.y / 3.4f);
                if (lv < 0 || lv >= Levels) continue;
                Vector3 target = pos + Vector3.up * 1.0f;
                foreach (int ci in _levelCams[lv])
                {
                    var c = _cams[ci];
                    Vector3 to = target - c.Pos;
                    float d = to.magnitude;
                    if (d > MotionRange || d < 1f) continue;
                    if (Vector3.Angle(c.Rot * Vector3.forward, to) > MotionHalfAngle) continue;
                    if (Physics.Linecast(c.Pos, target, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<NetPlayer>() != p) continue;
                    c.LastMotion = Time.time;
                    _cams[ci] = c;
                }
            }
        }
    }
}
