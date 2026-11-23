using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Map
{
    /// <summary>
    /// Fluid leaking from under a few damaged cars on levels -1 and -2 (engine oil, coolant, brake fluid): the map
    /// builder stores one candidate spot under the engine of every car there; at runtime <see cref="Count"/> of them
    /// get a puddle with a run towards the aisle (or out from under the side for cars parked nose-in). Which cars
    /// leak changes every round: the game calls <see cref="Apply"/> with a seed shared by all players; until then a
    /// fixed seed is used (same picture for everybody).
    /// </summary>
    public sealed class CarLeaks : MonoBehaviour
    {
        [Serializable]
        public struct Spot
        {
            public Vector3 Pos;     // middle of the decal, on the floor (+ layer height)
            public float Yaw;       // +Z of the decal = direction of the run
            public int Level;
            public string Car;
        }

        public Spot[] Spots = Array.Empty<Spot>();
        /// <summary>Decal quads, one per leak shape of the atlas.</summary>
        public Mesh[] Shapes = Array.Empty<Mesh>();
        /// <summary>Fluids: oil, green coolant, pink coolant, brake fluid.</summary>
        public Material[] Fluids = Array.Empty<Material>();
        public int Count = 3;
        public const int DefaultSeed = 20261123;

        public static CarLeaks Instance { get; private set; }
        /// <summary>Seed of the leaks shown now (tests / logs).</summary>
        public int Seed { get; private set; } = -1;
        public readonly List<string> Shown = new List<string>();

        private readonly List<GameObject> _live = new List<GameObject>();

        private void Awake()
        {
            Instance = this;
            Apply(DefaultSeed);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Shows <see cref="Count"/> leaks picked with <paramref name="seed"/> (at least 12 m apart).</summary>
        public void Apply(int seed)
        {
            if (seed == Seed) return;
            Seed = seed;
            foreach (var g in _live) if (g != null) Destroy(g);
            _live.Clear();
            Shown.Clear();
            if (Spots.Length == 0 || Shapes.Length == 0 || Fluids.Length == 0) return;
            var rnd = new System.Random(seed);
            var order = new List<int>();
            for (int i = 0; i < Spots.Length; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var picked = new List<Spot>();
            foreach (int i in order)
            {
                if (picked.Count >= Count) break;
                var s = Spots[i];
                bool far = true;
                foreach (var q in picked) if (Vector3.Distance(q.Pos, s.Pos) < 12f) far = false;
                if (!far) continue;
                picked.Add(s);
                // oil most often, then the coloured coolants, brake fluid now and then
                double f = rnd.NextDouble();
                int fluid = f < 0.45 ? 0 : f < 0.65 ? 1 : f < 0.82 ? 2 : 3;
                fluid = Mathf.Min(fluid, Fluids.Length - 1);
                var shape = Shapes[rnd.Next(Shapes.Length)];
                var go = new GameObject("Leak_" + s.Car);
                go.layer = LevelLighting.LayerOf(s.Level);
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(s.Pos, Quaternion.Euler(0f, s.Yaw + (float)(rnd.NextDouble() * 16.0 - 8.0), 0f));
                float k = 0.85f + 0.3f * (float)rnd.NextDouble();
                go.transform.localScale = new Vector3(k, 1f, k);
                go.AddComponent<MeshFilter>().sharedMesh = shape;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Fluids[fluid];
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                _live.Add(go);
                Shown.Add($"{s.Car} ({Fluids[fluid].name}, level {s.Level})");
            }
            Debug.Log($"[CarLeaks] seed {seed}: {string.Join(", ", Shown)}");
        }
    }
}
