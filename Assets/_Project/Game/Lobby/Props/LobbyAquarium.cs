using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Aquarium: the fish (children named "Fish*") wander on smooth curves inside the tank with a tail wiggle; bubbles
    /// rise from the air stone. Feeding (<see cref="LobbyPropKind.Aquarium"/>): flakes drop on the water and sink slowly,
    /// every fish swims to the nearest flake and eats it. Tank space: local, water = <see cref="half"/> around <see cref="centre"/>.
    /// </summary>
    public class LobbyAquarium : MonoBehaviour
    {
        public Vector3 centre;
        public Vector3 half;
        public Material flakeMat, bubbleMat;
        private Transform[] _fish;
        private Vector3[] _target, _vel;
        private float[] _seed;
        private readonly List<(Transform t, Vector3 v)> _flakes = new();
        private readonly List<Transform> _bubbles = new();
        private readonly System.Random _rnd = new(5);
        private float _feedAt = -1f;
        private int _feedSeed;

        private void Start()
        {
            var list = new List<Transform>();
            foreach (Transform c in transform) if (c.name.StartsWith("Fish")) list.Add(c);
            _fish = list.ToArray();
            _target = new Vector3[_fish.Length];
            _vel = new Vector3[_fish.Length];
            _seed = new float[_fish.Length];
            for (int i = 0; i < _fish.Length; i++) { _seed[i] = i * 1.7f; _target[i] = RandomPoint(); }
            for (int i = 0; i < 7; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(transform, false);
                b.transform.localScale = Vector3.one * (0.008f + 0.005f * (i % 3));
                b.GetComponent<Renderer>().sharedMaterial = bubbleMat;
                b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b.transform.localPosition = BubbleStart(i * 0.06f);
                _bubbles.Add(b.transform);
            }
            LobbyPropDriver.Used += OnUsed;
        }

        private void OnDestroy() => LobbyPropDriver.Used -= OnUsed;

        private Vector3 RandomPoint() => centre + Vector3.Scale(half * 0.8f,
            new Vector3((float)_rnd.NextDouble() * 2f - 1f, (float)_rnd.NextDouble() * 1.6f - 0.8f, (float)_rnd.NextDouble() * 2f - 1f));

        private Vector3 BubbleStart(float up) => centre + new Vector3(half.x * 0.75f, -half.y + 0.03f + up, half.z * 0.5f);

        private void OnUsed(LobbyPropKind kind, ulong user, int arg)
        {
            if (kind != LobbyPropKind.Aquarium) return;
            _feedAt = Time.time + 0.75f; // the hand is over the tank
            _feedSeed = arg;
        }

        private void DropFlakes()
        {
            var r = new System.Random(_feedSeed);
            for (int i = 0; i < 9; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(f.GetComponent<Collider>());
                f.transform.SetParent(transform, false);
                f.transform.localScale = Vector3.one * (0.009f + 0.006f * (float)r.NextDouble());
                f.transform.localRotation = Quaternion.Euler(90f + (float)r.NextDouble() * 40f, (float)r.NextDouble() * 360f, 0f);
                f.transform.localPosition = centre + new Vector3(((float)r.NextDouble() - 0.5f) * half.x * 0.7f, half.y - 0.005f, ((float)r.NextDouble() - 0.5f) * half.z * 0.7f);
                var mr = f.GetComponent<Renderer>();
                mr.sharedMaterial = flakeMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _flakes.Add((f.transform, new Vector3(0f, -0.01f - 0.01f * (float)r.NextDouble(), 0f)));
            }
            LobbyPropSounds.Play(LobbyPropSounds.Id.Sprinkle, transform.TransformPoint(centre + Vector3.up * half.y), 0.35f);
        }

        private void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            if (_feedAt > 0f && t >= _feedAt) { _feedAt = -1f; DropFlakes(); }
            for (int i = _flakes.Count - 1; i >= 0; i--)
            {
                var (ft, v) = _flakes[i];
                if (ft == null) { _flakes.RemoveAt(i); continue; }
                Vector3 p = ft.localPosition + v * dt + new Vector3(Mathf.Sin(t * 2f + i) * 0.03f * dt, 0f, 0f);
                p.y = Mathf.Max(p.y, centre.y - half.y + 0.01f);
                ft.localPosition = p;
                ft.Rotate(0f, 40f * dt, 0f, Space.World);
            }
            if (_fish == null) return;
            for (int i = 0; i < _fish.Length; i++)
            {
                var f = _fish[i];
                Vector3 pos = f.localPosition;
                int best = -1;
                float bestD = 9f;
                for (int k = 0; k < _flakes.Count; k++)
                {
                    if (_flakes[k].t == null) continue;
                    float d = Vector3.Distance(_flakes[k].t.localPosition, pos);
                    if (d < bestD) { bestD = d; best = k; }
                }
                float speed = 0.06f;
                if (best >= 0)
                {
                    _target[i] = _flakes[best].t.localPosition;
                    speed = 0.16f;
                    if (bestD < 0.025f) { Destroy(_flakes[best].t.gameObject); _flakes.RemoveAt(best); }
                }
                else if (Vector3.Distance(pos, _target[i]) < 0.05f || Random.value < dt * 0.15f) _target[i] = RandomPoint();
                Vector3 want = (_target[i] - pos).normalized * speed;
                _vel[i] = Vector3.Lerp(_vel[i], want, dt * 1.8f);
                pos += _vel[i] * dt;
                pos = Vector3.Max(centre - half * 0.9f, Vector3.Min(centre + half * 0.9f, pos));
                f.localPosition = pos;
                Vector3 flat = new Vector3(_vel[i].x, _vel[i].y * 0.4f, _vel[i].z);
                if (flat.sqrMagnitude > 1e-6f)
                    f.localRotation = Quaternion.Slerp(f.localRotation,
                        Quaternion.LookRotation(flat) * Quaternion.Euler(0f, Mathf.Sin(t * (8f + speed * 40f) + _seed[i]) * 12f, 0f), dt * 5f);
            }
            for (int i = 0; i < _bubbles.Count; i++)
            {
                var b = _bubbles[i];
                Vector3 p = b.localPosition + new Vector3(Mathf.Sin(t * 6f + i) * 0.01f * dt, 0.09f * dt, 0f);
                if (p.y > centre.y + half.y) p = BubbleStart(0f);
                b.localPosition = p;
            }
        }
    }
}
