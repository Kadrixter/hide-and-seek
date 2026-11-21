using System.Collections.Generic;
using System.Text.RegularExpressions;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Car alarms on the parked cars of the garage. Every client builds the same sorted car list from the scene
    /// (objects named "Car_000_..."); the server watches hiders: running close to a car, bumping into it or brushing
    /// past it can set the alarm off (crouching / crawling never does). Alarms howl for 15-25 s with flashing hazard
    /// lights, are heard across the garage and pin the car on the seeker's minimap.
    /// </summary>
    public class CarAlarms : MonoBehaviour
    {
        public static CarAlarms Instance { get; private set; }

        // Trigger tuning (per server check, 5 checks per second).
        private const float CheckInterval = 0.2f;
        private const float BumpDistance = 0.14f, BrushDistance = 0.35f, RunDistance = 0.9f;
        private const float BumpChance = 0.45f, BrushChancePerCheck = 0.025f, RunChancePerCheck = 0.07f;
        private const float WalkSpeed = 1.0f, RunSpeed = 4.2f;
        public const float Cooldown = 20f;

        private class Car
        {
            public Transform T;          // transform of the mesh (local bounds are in this space)
            public Bounds Local;   // collider box (gameplay distance)
            public Bounds Visual;  // visible body in local space (lamp placement)
            public Vector3 WorldCenter;
            public float Radius;
            public double AlarmUntil, CooldownUntil; // server clock (MatchManager.Now)
            public double EffectUntil;               // local clock (effects)
            public AudioSource Loop;
            public GameObject Lights;
            public Renderer[] LampRenderers;
            public Light Glow;
        }

        private readonly List<Car> _cars = new();
        private readonly Dictionary<ulong, Vector3> _lastPos = new();
        private readonly Dictionary<ulong, int> _contactCar = new();
        private float _nextCheck;
        private static Material _lampOn, _lampOff;

        public int CarCount => _cars.Count;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("CarAlarms");
            Instance = go.AddComponent<CarAlarms>();
        }

        private void Awake() => Scan();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Cars come from the map as "Car_###_..." objects with BoxColliders in local space. The colliders (not the mesh:
        /// static batching replaces sharedMesh with a combined mesh in builds) give the exact rotated car box.
        /// </summary>
        private void Scan()
        {
            var rx = new Regex(@"^Car_\d{3}_");
            var found = new List<Transform>();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                if (rx.IsMatch(t.name) && t.GetComponent<BoxCollider>() != null) found.Add(t);
            found.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            foreach (var t in found)
            {
                Bounds b = default;
                bool first = true;
                foreach (var bc in t.GetComponents<BoxCollider>())
                {
                    var cb = new Bounds(bc.center, bc.size);
                    if (first) { b = cb; first = false; }
                    else b.Encapsulate(cb);
                }
                // Visible body: the renderer's world AABB (valid also when statically batched) brought into local space.
                // Cars stand at ~0/90/180/270 degrees (+-3.5), so swapping the horizontal extents is accurate enough.
                Bounds vis = b;
                var rend = t.GetComponent<Renderer>();
                if (rend != null)
                {
                    var wb = rend.bounds;
                    bool swapped = Mathf.Abs(t.right.x) < 0.7f;
                    var ext = swapped ? new Vector3(wb.extents.z, wb.extents.y, wb.extents.x) : wb.extents;
                    vis = new Bounds(t.InverseTransformPoint(wb.center), ext * 2f);
                }
                _cars.Add(new Car { T = t, Local = b, Visual = vis, WorldCenter = t.TransformPoint(b.center), Radius = new Vector2(b.extents.x, b.extents.z).magnitude + RunDistance + 1f });
            }
            Debug.Log($"[CarAlarms] {_cars.Count} cars");
        }

        // ================================================================ server

        public void ServerTick(MatchManager mm, double now)
        {
            if (Time.time < _nextCheck) return;
            float dt = CheckInterval + (Time.time - _nextCheck);
            _nextCheck = Time.time + CheckInterval;
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || !pl.IsAliveHider || pl.Busy.Value) continue;
                ulong id = pl.NetworkObjectId;
                Vector3 pos = pl.transform.position;
                float speed = 0f;
                if (_lastPos.TryGetValue(id, out var last))
                {
                    Vector3 d = pos - last;
                    d.y = 0f;
                    speed = d.magnitude / Mathf.Max(0.05f, dt);
                    if (speed > 15f) speed = 0f; // teleport / elevator
                }
                _lastPos[id] = pos;

                var m = pl.Motion.Value;
                if (m.Crouching || m.IsProne) { _contactCar.Remove(id); continue; } // sneaking never triggers
                if (speed < WalkSpeed * 0.6f) { _contactCar.Remove(id); continue; }

                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < _cars.Count; i++)
                {
                    var car = _cars[i];
                    Vector3 off = pos - car.WorldCenter;
                    if (Mathf.Abs(off.y) > 2.5f || off.x * off.x + off.z * off.z > car.Radius * car.Radius) continue;
                    float dist = DistanceToCar(car, pos);
                    if (dist < bestD) { bestD = dist; best = i; }
                }
                if (best < 0 || bestD > RunDistance) { _contactCar.Remove(id); continue; }
                var target = _cars[best];
                if (now < target.AlarmUntil || now < target.CooldownUntil) continue;

                bool running = speed >= RunSpeed || m.Running;
                float chance = 0f;
                if (bestD <= BumpDistance)
                {
                    // One big roll per bump (entering contact), small rolls while staying in contact.
                    bool newContact = !_contactCar.TryGetValue(id, out int c) || c != best;
                    chance = newContact ? (running ? BumpChance : BumpChance * 0.5f) : BrushChancePerCheck * 2f;
                    _contactCar[id] = best;
                }
                else
                {
                    _contactCar.Remove(id);
                    if (running) chance = RunChancePerCheck;
                    else if (bestD <= BrushDistance) chance = BrushChancePerCheck;
                }
                if (chance > 0f && Random.value < chance)
                {
                    float duration = Random.Range(15f, 25f);
                    target.AlarmUntil = now + duration;
                    target.CooldownUntil = now + duration + Cooldown;
                    Debug.Log($"[CarAlarms] {pl.Nick} set off car {best} (dist {bestD:0.00} m, speed {speed:0.0} m/s, {(running ? "running" : "walking")})");
                    mm.CarAlarmRpc(best, duration, id);
                }
            }
        }

        /// <summary>Server test helper: sets off the car nearest to <paramref name="pos"/>.</summary>
        public int ServerForceNearest(MatchManager mm, Vector3 pos, double now)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _cars.Count; i++)
            {
                float d = DistanceToCar(_cars[i], pos) + Mathf.Abs(pos.y - _cars[i].WorldCenter.y) * 3f;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return -1;
            float duration = Random.Range(15f, 25f);
            _cars[best].AlarmUntil = now + duration;
            _cars[best].CooldownUntil = now + duration + Cooldown;
            mm.CarAlarmRpc(best, duration, ulong.MaxValue);
            return best;
        }

        private const float PlayerRadius = 0.3f;

        /// <summary>Horizontal gap between the player's capsule and the car's (rotated) box, 0 when touching.</summary>
        private static float DistanceToCar(Car car, Vector3 world)
        {
            Vector3 p = car.T.InverseTransformPoint(world);
            var b = car.Local;
            float dx = Mathf.Max(0f, Mathf.Abs(p.x - b.center.x) - b.extents.x);
            float dz = Mathf.Max(0f, Mathf.Abs(p.z - b.center.z) - b.extents.z);
            return Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - PlayerRadius);
        }

        /// <summary>Car frame for tests: floor point under the centre, side axis, length axis, half sizes.</summary>
        public (Vector3 floor, Vector3 right, Vector3 forward, Vector3 half) CarFrame(int index)
        {
            var c = _cars[index];
            var b = c.Local;
            Vector3 floor = c.T.TransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
            return (floor, c.T.right, c.T.forward, b.extents);
        }

        public Vector3 CarPosition(int index) => index >= 0 && index < _cars.Count ? _cars[index].WorldCenter : Vector3.zero;

        // ================================================================ all clients

        public void PlayAlarm(int index, float duration)
        {
            if (index < 0 || index >= _cars.Count) return;
            var car = _cars[index];
            car.EffectUntil = Time.timeAsDouble + duration;
            if (car.Loop != null) GameAudio.StopLoop(car.Loop, 0.1f);
            car.Loop = GameAudio.StartLoop(GameSfx.CarAlarm, car.T, 1f);
            EnsureLights(car);
            car.Lights.SetActive(true);
        }

        private void EnsureLights(Car car)
        {
            if (car.Lights != null) return;
            if (_lampOn == null)
            {
                _lampOn = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.55f, 0.05f) };
                _lampOn.EnableKeyword("_EMISSION");
                _lampOn.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.05f) * 4f);
                _lampOff = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.2f, 0.05f) };
            }
            var root = new GameObject("AlarmLights");
            root.transform.SetParent(car.T, false);
            var b = car.Visual;
            float y = b.min.y + Mathf.Clamp(b.size.y * 0.45f, 0.55f, 0.95f);
            var lamps = new List<Renderer>();
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(g.GetComponent<Collider>());
                g.name = "Blinker";
                g.transform.SetParent(root.transform, false);
                g.transform.localPosition = new Vector3(b.center.x + sx * (b.extents.x - 0.12f), y, b.center.z + sz * (b.extents.z - 0.005f));
                g.transform.localScale = new Vector3(0.22f, 0.08f, 0.03f);
                var r = g.GetComponent<Renderer>();
                r.sharedMaterial = _lampOff;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lamps.Add(r);
                // side repeaters
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(s.GetComponent<Collider>());
                s.name = "SideRepeater";
                s.transform.SetParent(root.transform, false);
                s.transform.localPosition = new Vector3(b.center.x + sx * (b.extents.x - 0.005f), y + 0.05f, b.center.z + sz * (b.extents.z * 0.55f));
                s.transform.localScale = new Vector3(0.02f, 0.05f, 0.12f);
                var sr = s.GetComponent<Renderer>();
                sr.sharedMaterial = _lampOff;
                lamps.Add(sr);
            }
            var lg = new GameObject("HazardGlow").AddComponent<Light>();
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = new Vector3(b.center.x, b.max.y + 0.3f, b.center.z);
            lg.type = LightType.Point;
            lg.color = new Color(1f, 0.55f, 0.1f);
            lg.range = 7f;
            lg.intensity = 0f;
            lg.shadows = LightShadows.None;
            lg.renderMode = LightRenderMode.ForcePixel; // must not drop to a vertex light next to the garage lamps
            car.Lights = root;
            car.LampRenderers = lamps.ToArray();
            car.Glow = lg;
        }

        private void Update()
        {
            double now = Time.timeAsDouble;
            foreach (var car in _cars)
            {
                if (car.Lights == null || !car.Lights.activeSelf) continue;
                bool active = now < car.EffectUntil;
                if (!active)
                {
                    if (car.Loop != null) { GameAudio.StopLoop(car.Loop, 0.4f); car.Loop = null; }
                    car.Lights.SetActive(false);
                    continue;
                }
                // Hazard lights: ~1.6 Hz, on 55 % of the cycle.
                bool on = Mathf.Repeat((float)now * 1.6f, 1f) < 0.55f;
                foreach (var r in car.LampRenderers) r.sharedMaterial = on ? _lampOn : _lampOff;
                car.Glow.intensity = on ? 2.2f : 0f;
            }
        }
    }
}
