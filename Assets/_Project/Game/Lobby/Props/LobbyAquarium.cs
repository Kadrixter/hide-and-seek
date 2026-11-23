using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Aquarium life (every copy, purely visual). Fish (children "Fish_&lt;species&gt;_n") swim on smooth curved paths:
    /// steering towards wander targets with a limited turn rate, easing speed, slight banking, sometimes turning back;
    /// tetras school around a moving leader point, corydoras root along the gravel, angelfish glide slowly, goldfish
    /// waddle. The tail pivot ("Tail") wags faster the faster the fish swims. Plants ("Weed") sway, bubbles rise from
    /// the air stone and the castle ("BubbleSource*"), the water surface and caustics drift. Feeding
    /// (<see cref="LobbyPropKind.Aquarium"/>): flakes land on the water and sink, the fish dart up and eat them.
    /// Tank space: local, the water volume is <see cref="half"/> around <see cref="centre"/>.
    /// </summary>
    public class LobbyAquarium : MonoBehaviour
    {
        public Vector3 centre;
        public Vector3 half;
        public Material flakeMat, bubbleMat;

        private enum Kind { Tetra, Gold, Angel, Cory }

        private sealed class Fish
        {
            public Transform T, Tail;
            public Kind K;
            public Vector3 Pos, Dir, Target;
            public float Speed, Cruise, Turn, Phase, NextTarget, Bank;
            public Vector3 SchoolOffset;
        }

        private readonly List<Fish> _fish = new();
        private readonly List<(Transform t, Quaternion rot, float phase)> _weeds = new();
        private readonly List<(Transform t, float speed, float wob, float seed)> _bubbles = new();
        private readonly List<Vector3> _sources = new();
        private readonly List<(Transform t, Vector3 v)> _flakes = new();
        private readonly List<Material> _scroll = new();
        private readonly System.Random _rnd = new(5);
        private Vector3 _schoolGoal, _schoolPos;
        private float _schoolNext, _feedAt = -1f;
        private int _feedSeed;

        private float R() => (float)_rnd.NextDouble();

        private void Start()
        {
            foreach (Transform c in transform)
            {
                if (c.name.StartsWith("Fish"))
                {
                    var f = new Fish { T = c, Tail = c.Find("Tail"), Pos = c.localPosition, Dir = c.localRotation * Vector3.forward, Phase = R() * 10f };
                    f.K = c.name.Contains("tetra") ? Kind.Tetra : c.name.Contains("gold") ? Kind.Gold : c.name.Contains("angel") ? Kind.Angel : Kind.Cory;
                    (f.Cruise, f.Turn) = f.K switch
                    {
                        Kind.Tetra => (0.07f, 160f),
                        Kind.Gold => (0.05f, 90f),
                        Kind.Angel => (0.035f, 60f),
                        _ => (0.03f, 120f),
                    };
                    f.Speed = f.Cruise;
                    f.SchoolOffset = new Vector3(R() - 0.5f, (R() - 0.5f) * 0.5f, R() - 0.5f) * 0.12f;
                    f.Target = Wander(f);
                    _fish.Add(f);
                }
                else if (c.name == "Weed") _weeds.Add((c, c.localRotation, R() * 6.3f));
                else if (c.name == "Caustics" || c.name == "CausticsBack" || c.name == "WaterSurface")
                {
                    var r = c.GetComponent<Renderer>();
                    if (r != null && !_scroll.Contains(r.sharedMaterial)) _scroll.Add(r.sharedMaterial);
                }
            }
            // bubble sources anywhere below (the castle's one sits in its tower)
            foreach (var c in GetComponentsInChildren<Transform>())
                if (c.name.StartsWith("BubbleSource")) _sources.Add(transform.InverseTransformPoint(c.position));
            if (_sources.Count == 0) _sources.Add(centre - new Vector3(0f, half.y, 0f));
            for (int i = 0; i < 18; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(transform, false);
                b.transform.localScale = Vector3.one * (0.005f + 0.004f * (i % 3));
                var mr = b.GetComponent<Renderer>();
                mr.sharedMaterial = bubbleMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b.transform.localPosition = _sources[i % _sources.Count] + Vector3.up * (i / (float)_sources.Count) * 0.04f;
                _bubbles.Add((b.transform, 0.08f + 0.05f * R(), 0.004f + 0.006f * R(), R() * 6f));
            }
            _schoolPos = _schoolGoal = centre;
            LobbyPropDriver.Used += OnUsed;
        }

        private void OnDestroy() => LobbyPropDriver.Used -= OnUsed;

        private Vector3 Lo => centre - half;
        private Vector3 Hi => centre + half;

        /// <summary>Next point to swim to: ahead most of the time, now and then behind (the fish turns back).</summary>
        private Vector3 Wander(Fish f)
        {
            Vector3 fwd = f.Dir.sqrMagnitude > 0.01f ? f.Dir.normalized : Vector3.right;
            bool back = R() < 0.18f;
            Vector3 p = f.Pos + (back ? -fwd : fwd) * (0.15f + 0.35f * R()) + new Vector3(R() - 0.5f, (R() - 0.5f) * 0.4f, R() - 0.5f) * 0.3f;
            p = Vector3.Max(Lo + Vector3.one * 0.03f, Vector3.Min(Hi - Vector3.one * 0.03f, p));
            if (f.K == Kind.Cory) p.y = Lo.y + 0.025f;
            if (f.K == Kind.Angel) p.y = Mathf.Clamp(p.y, centre.y - half.y * 0.3f, centre.y + half.y * 0.6f);
            f.NextTarget = Time.time + 2f + 4f * R();
            return p;
        }

        private void OnUsed(LobbyPropKind kind, ulong user, int arg)
        {
            if (kind != LobbyPropKind.Aquarium) return;
            _feedAt = Time.time + 0.75f; // the hand is over the tank
            _feedSeed = arg;
        }

        private void DropFlakes()
        {
            var r = new System.Random(_feedSeed);
            for (int i = 0; i < 12; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(f.GetComponent<Collider>());
                f.transform.SetParent(transform, false);
                f.transform.localScale = Vector3.one * (0.008f + 0.006f * (float)r.NextDouble());
                f.transform.localRotation = Quaternion.Euler(90f + (float)r.NextDouble() * 40f, (float)r.NextDouble() * 360f, 0f);
                f.transform.localPosition = centre + new Vector3(((float)r.NextDouble() - 0.5f) * 0.3f, half.y - 0.003f, 0.05f + ((float)r.NextDouble() - 0.5f) * 0.12f);
                var mr = f.GetComponent<Renderer>();
                mr.sharedMaterial = flakeMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // float a moment, then sink slowly
                _flakes.Add((f.transform, new Vector3(0f, -0.008f - 0.01f * (float)r.NextDouble(), 0f)));
            }
            LobbyPropSounds.Play(LobbyPropSounds.Id.Sprinkle, transform.TransformPoint(centre + Vector3.up * half.y), 0.35f);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f), t = Time.time;
            if (_feedAt > 0f && t >= _feedAt) { _feedAt = -1f; DropFlakes(); }
            UpdateFlakes(dt, t);
            // school leader point drifts around the middle of the tank
            if (t > _schoolNext)
            {
                _schoolNext = t + 3f + 3f * R();
                _schoolGoal = centre + Vector3.Scale(half * 0.7f, new Vector3(R() * 2f - 1f, R() - 0.4f, R() * 2f - 1f));
            }
            _schoolPos = Vector3.MoveTowards(_schoolPos, _schoolGoal, dt * 0.06f);
            foreach (var f in _fish) Swim(f, dt, t);
            for (int i = 0; i < _weeds.Count; i++)
            {
                var (w, rot, ph) = _weeds[i];
                w.localRotation = rot * Quaternion.Euler(Mathf.Sin(t * 0.9f + ph) * 5f, 0f, Mathf.Sin(t * 0.6f + ph * 1.7f) * 4f);
            }
            for (int i = 0; i < _bubbles.Count; i++)
            {
                var (b, sp, wob, seed) = _bubbles[i];
                Vector3 p = b.localPosition + Vector3.up * sp * dt;
                p.x += Mathf.Sin(t * 7f + seed) * wob * dt * 8f;
                if (p.y > centre.y + half.y + 0.02f) p = _sources[(i + (int)(t * 3f)) % _sources.Count];
                b.localPosition = p;
            }
            for (int i = 0; i < _scroll.Count; i++)
                _scroll[i].mainTextureOffset = new Vector2(Mathf.Sin(t * 0.13f + i) * 0.2f + t * 0.015f * (i + 1), Mathf.Cos(t * 0.11f + i * 2f) * 0.15f);
        }

        private void UpdateFlakes(float dt, float t)
        {
            for (int i = _flakes.Count - 1; i >= 0; i--)
            {
                var (ft, v) = _flakes[i];
                if (ft == null) { _flakes.RemoveAt(i); continue; }
                Vector3 p = ft.localPosition + v * dt + new Vector3(Mathf.Sin(t * 2f + i) * 0.03f * dt, 0f, 0f);
                p.y = Mathf.Max(p.y, Lo.y + 0.01f);
                ft.localPosition = p;
                ft.Rotate(0f, 40f * dt, 0f, Space.World);
            }
        }

        private void Swim(Fish f, float dt, float t)
        {
            // what to go for: food > school / wander
            int food = -1;
            float best = 0.6f;
            for (int k = 0; k < _flakes.Count; k++)
            {
                if (_flakes[k].t == null) continue;
                float d = Vector3.Distance(_flakes[k].t.localPosition, f.Pos);
                if (d < best && (f.K != Kind.Cory || _flakes[k].t.localPosition.y < Lo.y + 0.06f)) { best = d; food = k; }
            }
            float want = f.Cruise;
            if (food >= 0)
            {
                f.Target = _flakes[food].t.localPosition;
                want = f.Cruise * 2.2f;
                if (best < 0.02f) { Destroy(_flakes[food].t.gameObject); _flakes.RemoveAt(food); f.NextTarget = 0f; }
            }
            else if (f.K == Kind.Tetra) f.Target = _schoolPos + f.SchoolOffset;
            else if (t > f.NextTarget || Vector3.Distance(f.Pos, f.Target) < 0.04f) f.Target = Wander(f);
            // separation from the others
            Vector3 away = Vector3.zero;
            foreach (var o in _fish)
            {
                if (o == f) continue;
                Vector3 d = f.Pos - o.Pos;
                float m = d.magnitude;
                if (m < 0.05f && m > 1e-4f) away += d / m * (0.05f - m);
            }
            Vector3 desired = (f.Target - f.Pos).normalized + away * 12f;
            // soft walls: steer back in near the glass
            Vector3 lo = Lo + Vector3.one * 0.03f, hi = Hi - Vector3.one * 0.03f;
            for (int a = 0; a < 3; a++)
            {
                if (f.Pos[a] < lo[a] + 0.04f) desired[a] += 1.5f;
                if (f.Pos[a] > hi[a] - 0.04f) desired[a] -= 1.5f;
            }
            if (f.K != Kind.Cory) desired.y *= 0.5f; // swim mostly level
            if (desired.sqrMagnitude < 1e-6f) desired = f.Dir;
            Vector3 newDir = Vector3.RotateTowards(f.Dir.normalized, desired.normalized, f.Turn * Mathf.Deg2Rad * dt, 0f);
            float turnRate = Vector3.SignedAngle(f.Dir, newDir, Vector3.up) / Mathf.Max(dt, 1e-4f);
            f.Dir = newDir;
            // cruise with a small pulse (tail strokes), slower while turning hard
            want *= 1f - Mathf.Clamp01(Mathf.Abs(turnRate) / 400f) * 0.5f;
            f.Speed = Mathf.MoveTowards(f.Speed, want, dt * 0.15f);
            float pulse = 1f + 0.25f * Mathf.Sin(f.Phase * 0.5f);
            f.Pos += f.Dir * f.Speed * pulse * dt;
            f.Pos = Vector3.Max(lo, Vector3.Min(hi, f.Pos));
            if (f.K == Kind.Cory) f.Pos.y = Mathf.Max(f.Pos.y, Lo.y + 0.02f);
            f.T.localPosition = f.Pos;
            f.Bank = Mathf.Lerp(f.Bank, Mathf.Clamp(-turnRate * 0.08f, -25f, 25f), dt * 4f);
            Vector3 look = new Vector3(f.Dir.x, f.Dir.y * 0.6f, f.Dir.z);
            if (look.sqrMagnitude > 1e-6f) f.T.localRotation = Quaternion.LookRotation(look) * Quaternion.Euler(0f, 0f, f.Bank);
            // tail: frequency and swing follow the speed
            f.Phase += dt * (8f + f.Speed * 180f);
            if (f.Tail != null)
                f.Tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(f.Phase) * (14f + f.Speed * 220f), 0f);
        }
    }
}
