using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Runs on every client for every player (blood is local cosmetics, the pools come from the same events everywhere):
    /// stepping into a blood pool soaks the soles, the next 5-10 m leave footprints that fade as the blood runs out;
    /// wounded / limping / maimed players drip blood, a downed player lies in a slowly growing pool.
    /// </summary>
    public class BloodTracks : MonoBehaviour
    {
        public static BloodTracks Instance { get; private set; }

        private class Feet
        {
            public Vector3 Last;
            public bool Has;
            public float Stride;
            public bool Left;
            public float Blood, Total;
            public float NextDrip;
            public bool DownedPool;
        }

        private readonly Dictionary<NetPlayer, Feet> _feet = new Dictionary<NetPlayer, Feet>();
        private readonly List<NetPlayer> _gone = new List<NetPlayer>();

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[BloodTracks]");
            Instance = go.AddComponent<BloodTracks>(); // scene object: cleared with the scene like the decals
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Test hook: soak the soles of <paramref name="p"/> as if it stepped into a pool.</summary>
        public void Soak(NetPlayer p, float metres)
        {
            var f = Get(p);
            f.Blood = f.Total = metres;
        }

        public float BloodLeft(NetPlayer p) => _feet.TryGetValue(p, out var f) ? f.Blood : 0f;

        private Feet Get(NetPlayer p)
        {
            if (!_feet.TryGetValue(p, out var f)) _feet[p] = f = new Feet();
            return f;
        }

        private void Update()
        {
            foreach (var kv in _feet) if (kv.Key == null) _gone.Add(kv.Key);
            foreach (var g in _gone) _feet.Remove(g);
            _gone.Clear();

            float now = Time.time;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                var f = Get(p);
                Vector3 pos = p.transform.position;
                var life = p.Life.Value;

                // Downed: a pool slowly grows under the body (root at the shoulders, the body lies behind it).
                if (life == LifeState.Downed)
                {
                    if (!f.DownedPool)
                    {
                        f.DownedPool = true;
                        BloodFx.Pool(pos - p.transform.forward * 0.6f, Random.Range(1.0f, 1.3f), 22f);
                    }
                    f.Has = false;
                    continue;
                }
                f.DownedPool = false;
                if (life != LifeState.Alive || p.Motor == null) { f.Has = false; f.Blood = 0f; continue; }

                // Dripping wounds.
                bool bleeding = p.Limping.Value || p.Wounded.Value || p.Severed.Value != 0;
                if (bleeding && now >= f.NextDrip)
                {
                    bool maimed = p.Severed.Value != 0;
                    f.NextDrip = now + (maimed ? Random.Range(0.12f, 0.3f) : Random.Range(0.35f, 0.8f));
                    Vector3 at = pos + Random.insideUnitSphere * 0.18f + p.transform.forward * Random.Range(-0.1f, 0.15f);
                    BloodFx.DropAt(at, maimed ? 0.09f : 0.06f);
                }

                if (!f.Has || (pos - f.Last).sqrMagnitude > 4f) { f.Last = pos; f.Has = true; f.Stride = 0f; continue; }
                Vector3 d = pos - f.Last;
                d.y = 0f;
                float dist = d.magnitude;
                f.Last = pos;

                // Standing in a pool soaks the soles.
                if (p.Motor.IsGrounded || p.Motor.IsRemoteProxy)
                {
                    foreach (var pool in BloodDecal.Pools)
                    {
                        if (pool == null) continue;
                        Vector3 pp = pool.transform.position;
                        if (Mathf.Abs(pp.y - pos.y) > 0.45f) continue;
                        float dx = pp.x - pos.x, dz = pp.z - pos.z;
                        float r = pool.Radius * 0.8f;
                        if (dx * dx + dz * dz > r * r) continue;
                        if (f.Blood < f.Total * 0.85f || f.Total <= 0f)
                        {
                            f.Total = Random.Range(5f, 10f);
                            f.Blood = f.Total;
                        }
                        break;
                    }
                }

                if (f.Blood <= 0f || dist < 1e-4f || p.Motor.IsProne) continue;
                f.Blood -= dist;
                f.Stride += dist;
                float speed = dist / Mathf.Max(1e-4f, Time.deltaTime);
                float strideLen = speed > 4f ? 0.95f : p.Motor.IsCrouching ? 0.5f : 0.7f;
                if (f.Stride < strideLen) continue;
                f.Stride = 0f;
                Vector3 fwd = d / dist;
                Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg + Random.Range(-6f, 6f);
                Vector3 at2 = pos + right * (f.Left ? -0.1f : 0.1f);
                float a = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(f.Blood / Mathf.Max(0.1f, f.Total)));
                BloodFx.Footprint(at2, yaw, f.Left, a);
                f.Left = !f.Left;
            }
        }
    }
}
