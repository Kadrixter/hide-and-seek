using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Garage CCTV cameras: where they hang, their models with a red LED, and motion detection.</summary>
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
            public Renderer Led;     // red LED on the physical camera
        }

        private readonly List<CamPoint> _cams = new();
        private readonly List<int>[] _levelCams = { new(), new(), new() };
        private readonly Dictionary<ulong, Vector3> _prev = new();
        private readonly MaterialPropertyBlock _ledMpb = new();
        private float _nextSense, _lastSense;

        private void AddCam(int lv, string name, Vector3 pos, Vector3 dir)
        {
            _levelCams[lv].Add(_cams.Count);
            var rot = Quaternion.LookRotation(dir);
            _cams.Add(new CamPoint { Pos = pos, Rot = rot, Level = lv, Name = name, LastMotion = -99f, Led = BuildCameraModel(pos, rot, lv) });
        }

        /// <summary>Small CCTV camera hanging from the ceiling: bracket, housing, lens, sun hood and a red LED.</summary>
        private Renderer BuildCameraModel(Vector3 pos, Quaternion rot, int lv)
        {
            var root = new GameObject("CCTV").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(pos, rot);
            var housing = new Color(0.82f, 0.82f, 0.8f);
            var dark = new Color(0.12f, 0.12f, 0.13f);
            float ceiling = FloorY(lv) + 3.0f;
            // Housing behind the lens (the feed camera sits at the lens, its near plane hides the model).
            LocalPart(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.2f), new Vector3(0.13f, 0.12f, 0.3f), housing, Surf.Shiny(0.35f));
            LocalPart(root, PrimitiveType.Cube, new Vector3(0f, 0.075f, -0.17f), new Vector3(0.16f, 0.025f, 0.36f), housing, Surf.Shiny(0.35f));
            var lens = LocalPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.04f), new Vector3(0.08f, 0.012f, 0.08f), dark, Surf.Shiny(0.8f));
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Bracket up to the ceiling.
            var arm = new GameObject("Bracket").transform;
            arm.SetParent(transform, false);
            Vector3 top = new Vector3(pos.x, ceiling, pos.z) - rot * Vector3.forward * 0.25f;
            Vector3 bottom = pos - rot * Vector3.forward * 0.25f + Vector3.up * 0.06f;
            arm.position = (top + bottom) * 0.5f;
            LocalPart(arm, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.035f, Mathf.Max(0.05f, (top.y - bottom.y) * 0.5f), 0.035f), dark, default);
            LocalPart(arm, PrimitiveType.Cube, new Vector3(0f, (top.y - bottom.y) * 0.5f, 0f), new Vector3(0.14f, 0.02f, 0.14f), dark, default);
            // LED on the front, next to the lens.
            var led = LocalPart(root, PrimitiveType.Cube, new Vector3(0.045f, 0.04f, -0.045f), new Vector3(0.018f, 0.018f, 0.006f), new Color(0.15f, 0f, 0f), Surf.Glow(new Color(0.01f, 0f, 0f)));
            var lr = led.GetComponent<Renderer>();
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
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

        /// <summary>LEDs blink softly red on the cameras shown on the monitors while somebody watches them.</summary>
        private void UpdateLeds()
        {
            var mm = MatchManager.Instance;
            bool watched = mm != null && mm.SecurityWatched.Value;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2f * Mathf.PI * 0.9f);
            for (int i = 0; i < _cams.Count; i++)
            {
                var led = _cams[i].Led;
                if (led == null) continue;
                bool on = false;
                if (watched)
                    foreach (var s in _slots) if (s.CamIndex == i) { on = true; break; }
                _ledMpb.SetColor("_Color", on ? new Color(0.6f, 0.05f, 0.03f) : new Color(0.15f, 0f, 0f));
                _ledMpb.SetColor("_EmissionColor", on ? new Color(3f * pulse, 0.2f * pulse, 0.1f * pulse) : Color.black);
                led.SetPropertyBlock(_ledMpb);
            }
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

        private void BuildCameraPoints()
        {
            float[] aisleZ = { -44.75f, -28.25f, -11.75f, 11.75f, 28.25f, 44.75f };
            // Stairwells (Map ParkingGarageBuilder.Stairs): door on the south face, x0 + 1 on level 0, x0 + 3 below.
            var stairs = new[] { (x0: -62f, zf: 50.8f), (x0: 46f, zf: 50.8f), (x0: -34f, zf: -4.6f), (x0: 30f, zf: -4.6f) };
            // Elevators: the scene has "ElevatorCar_<n>_..." markers, one per level; doors face z = +-1.4.
            var elevX = new SortedDictionary<int, float>();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.name.StartsWith("ElevatorCar_")) continue;
                var p = t.name.Split('_');
                if (p.Length >= 2 && int.TryParse(p[1], out int car)) elevX[car] = t.position.x;
            }

            for (int lv = 0; lv < Levels; lv++)
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
                    var pos = new Vector3(kv.Value + 2.5f, y0 + 2.4f, -7.5f);
                    var look = new Vector3(kv.Value, y0 + 1.1f, -1.4f) - pos;
                    AddCam(lv, "WINDA " + (++e), pos, look);
                }
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
