using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Map
{
    /// <summary>
    /// Water dripping from the broken ceiling pipes (ParkingGarageBuilder.Pipes): at every <see cref="Spot"/> a drop
    /// swells at the torn pipe end, lets go at irregular intervals (now and then two in quick succession), falls to the
    /// puddle below and leaves a small ring spreading on the water. Purely local and cosmetic (not networked).
    /// Cheap: only spots within <see cref="ActiveDistance"/> of the camera (on its level) animate, the drops and rings
    /// are a small pool of renderers made once, the per-frame work allocates nothing.
    /// </summary>
    public sealed class CeilingDrips : MonoBehaviour
    {
        [Serializable]
        public struct Spot
        {
            public Vector3 Tip;     // where the drop hangs (lowest point of the torn pipe end)
            public float Surface;   // height of the puddle surface below it
            public int Level;
            public float Interval;  // mean seconds between drops
        }

        public Spot[] Spots = Array.Empty<Spot>();
        public Mesh DropMesh, RingMesh;
        public Material DropMaterial, RingMaterial;
        public float ActiveDistance = 18f;

        private const int MaxActive = 6, PoolSize = 10;
        private const float Gravity = 9.81f, Swell = 0.55f, RingLife = 0.5f, RescanEvery = 0.35f;

        private struct Drop
        {
            public Transform T;
            public MeshRenderer R;
            public int Spot;        // -1 = free
            public float Age, Y, Vy;
            public bool Falling;
        }

        private struct Ring
        {
            public Transform T;
            public MeshRenderer R;
            public float Age;       // < 0 = free
        }

        private Drop[] _drops;
        private Ring[] _rings;
        private float[] _next;
        private bool[] _hanging;
        private readonly int[] _active = new int[MaxActive];
        private int _activeCount;
        private float _rescanAt;
        private Camera _cam;
        private float _camSearchAt;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Color _ringColor = Color.white;
        private System.Random _rnd;

        private void Awake()
        {
            _rnd = new System.Random(Spots.Length * 7919 + 20261126);
            _mpb = new MaterialPropertyBlock();
            if (RingMaterial != null && RingMaterial.HasProperty(ColorId)) _ringColor = RingMaterial.GetColor(ColorId);
            _next = new float[Spots.Length];
            _hanging = new bool[Spots.Length];
            for (int i = 0; i < _next.Length; i++) _next[i] = (float)_rnd.NextDouble() * Mathf.Max(0.5f, Spots[i].Interval);
            _drops = new Drop[PoolSize];
            _rings = new Ring[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _drops[i] = new Drop { Spot = -1 };
                (_drops[i].T, _drops[i].R) = MakePiece("Drop", DropMesh, DropMaterial);
                _rings[i] = new Ring { Age = -1f };
                (_rings[i].T, _rings[i].R) = MakePiece("Ring", RingMesh, RingMaterial);
            }
        }

        private (Transform, MeshRenderer) MakePiece(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.enabled = false;
            return (go.transform, mr);
        }

        private void Update()
        {
            if (Spots.Length == 0 || DropMesh == null || RingMesh == null) return;
            float dt = Time.deltaTime, now = Time.time;
            if (now >= _rescanAt) Rescan(now);

            // new drops at the active spots
            for (int a = 0; a < _activeCount; a++)
            {
                int s = _active[a];
                if (_hanging[s] || now < _next[s]) continue;
                int d = FreeDrop();
                if (d < 0) break;
                ref var drop = ref _drops[d];
                drop.Spot = s;
                drop.Age = 0f;
                drop.Falling = false;
                drop.Y = Spots[s].Tip.y;
                drop.Vy = 0f;
                drop.T.gameObject.layer = LevelLighting.LayerOf(Spots[s].Level);
                drop.R.enabled = true;
                _hanging[s] = true;
            }

            for (int i = 0; i < _drops.Length; i++)
            {
                ref var drop = ref _drops[i];
                if (drop.Spot < 0) continue;
                var sp = Spots[drop.Spot];
                drop.Age += dt;
                if (!drop.Falling)
                {
                    // swelling at the pipe end, hangs a little lower as it grows
                    float k = Mathf.Clamp01(drop.Age / Swell);
                    drop.T.localScale = new Vector3(0.35f + 0.55f * k, 0.3f + 0.6f * k, 0.35f + 0.55f * k);
                    drop.T.position = sp.Tip + Vector3.down * (0.006f * k);
                    if (k >= 1f)
                    {
                        drop.Falling = true;
                        drop.Y = sp.Tip.y - 0.006f;
                        _hanging[drop.Spot] = false;
                        _next[drop.Spot] = now + NextInterval(sp.Interval);
                    }
                    continue;
                }
                drop.Vy += Gravity * dt;
                drop.Y -= drop.Vy * dt;
                if (drop.Y <= sp.Surface)
                {
                    Splash(new Vector3(sp.Tip.x, sp.Surface, sp.Tip.z), sp.Level);
                    drop.R.enabled = false;
                    drop.Spot = -1;
                    continue;
                }
                // stretched a little with speed
                float stretch = 1f + Mathf.Min(drop.Vy * 0.12f, 0.8f);
                drop.T.localScale = new Vector3(0.85f, 0.9f * stretch, 0.85f);
                drop.T.position = new Vector3(sp.Tip.x, drop.Y, sp.Tip.z);
            }

            for (int i = 0; i < _rings.Length; i++)
            {
                ref var ring = ref _rings[i];
                if (ring.Age < 0f) continue;
                ring.Age += dt;
                if (ring.Age >= RingLife)
                {
                    ring.Age = -1f;
                    ring.R.enabled = false;
                    continue;
                }
                float k = ring.Age / RingLife;
                float r = Mathf.Lerp(0.02f, 0.2f, 1f - (1f - k) * (1f - k));
                ring.T.localScale = new Vector3(r, 1f, r);
                var c = _ringColor;
                c.a *= 1f - k;
                _mpb.SetColor(ColorId, c);
                ring.R.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Irregular pause: mostly around the mean, sometimes a quick second drop, sometimes a long wait.</summary>
        private float NextInterval(float mean)
        {
            double r = _rnd.NextDouble();
            if (r < 0.18) return 0.12f + (float)_rnd.NextDouble() * 0.25f;
            if (r < 0.3) return mean * (1.8f + (float)_rnd.NextDouble() * 1.6f);
            return mean * (0.45f + (float)_rnd.NextDouble() * 0.9f);
        }

        private void Splash(Vector3 p, int level)
        {
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i].Age >= 0f) continue;
                ref var ring = ref _rings[i];
                ring.Age = 0f;
                ring.T.position = p;
                ring.T.localScale = new Vector3(0.02f, 1f, 0.02f);
                ring.T.rotation = Quaternion.Euler(0f, (float)_rnd.NextDouble() * 360f, 0f);
                ring.T.gameObject.layer = LevelLighting.LayerOf(level);
                ring.R.enabled = true;
                return;
            }
        }

        private int FreeDrop()
        {
            for (int i = 0; i < _drops.Length; i++)
                if (_drops[i].Spot < 0) return i;
            return -1;
        }

        /// <summary>Picks the (at most <see cref="MaxActive"/>) spots near the camera; the others stop dripping.</summary>
        private void Rescan(float now)
        {
            _rescanAt = now + RescanEvery;
            if ((_cam == null || !_cam.isActiveAndEnabled) && now >= _camSearchAt)
            {
                _camSearchAt = now + 1f;
                _cam = Camera.main;
            }
            _activeCount = 0;
            if (_cam == null) return;
            Vector3 c = _cam.transform.position;
            float max2 = ActiveDistance * ActiveDistance;
            for (int i = 0; i < Spots.Length && _activeCount < MaxActive; i++)
            {
                var sp = Spots[i];
                float dy = c.y - sp.Surface;
                if (dy < -0.5f || dy > 3.2f) continue; // another level
                float dx = c.x - sp.Tip.x, dz = c.z - sp.Tip.z;
                if (dx * dx + dz * dz > max2) continue;
                _active[_activeCount++] = i;
            }
        }
    }
}
