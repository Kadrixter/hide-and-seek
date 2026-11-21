using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// One blood decal (pool, splat, run, drop or footprint): spreads out after landing (eased), a run trickles down,
    /// slowly dries darker and duller, fades away at the end of its life. Oldest decals are recycled past a cap.
    /// </summary>
    public class BloodDecal : MonoBehaviour
    {
        private const int MaxDecals = 420;
        private static readonly List<BloodDecal> All = new List<BloodDecal>();
        public static readonly List<BloodDecal> Pools = new List<BloodDecal>();

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int WetId = Shader.PropertyToID("_Wet");

        public bool IsPool { get => _pool; set { if (value && !_pool) Pools.Add(this); _pool = value; } }
        public float Alpha { get; set; } = 1f;

        private bool _pool;
        private MeshRenderer _r;
        private MaterialPropertyBlock _mpb;
        private float _born, _life, _spreadFrom = 1f, _spreadTime;
        private Vector3 _full;
        private bool _run;
        private Vector3 _runTop;
        private float _runWidth, _runLen, _runTime;
        private float _nextShade;

        /// <summary>Current radius on the floor (metres), for footprints.</summary>
        public float Radius => transform.localScale.x * 0.5f;

        public void Init(MeshRenderer r, float life)
        {
            _r = r;
            _mpb = new MaterialPropertyBlock();
            _born = Time.time;
            _life = life;
            _full = transform.localScale;
            All.Add(this);
            while (All.Count > MaxDecals)
            {
                var old = All[0];
                All.RemoveAt(0);
                if (old != null) Destroy(old.gameObject);
            }
            Shade(1f);
        }

        /// <summary>Starts at <paramref name="from"/> of the full size and grows to it over <paramref name="time"/> s.</summary>
        public void Spread(float from, float time)
        {
            _spreadFrom = Mathf.Clamp01(from);
            _spreadTime = Mathf.Max(0f, time);
            if (_spreadTime > 0f) transform.localScale = new Vector3(_full.x * _spreadFrom, _full.y * _spreadFrom, 1f);
        }

        /// <summary>Thin run that grows downward from <paramref name="top"/> to <paramref name="length"/> m over <paramref name="time"/> s.</summary>
        public void MakeRun(Vector3 top, float width, float length, float time)
        {
            _run = true;
            _runTop = top + (transform.position - top); // keep the surface offset
            _runWidth = width;
            _runLen = length;
            _runTime = time;
            SetRun(0.02f);
        }

        private void SetRun(float len)
        {
            transform.localScale = new Vector3(_runWidth, len, 1f);
            transform.position = _runTop - transform.up * (len * 0.5f);
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (_pool) Pools.Remove(this);
        }

        private void Update()
        {
            float age = Time.time - _born;
            if (_run)
            {
                // Fast at first, then slower and slower as the blood runs out.
                float k = _runTime > 0f ? 1f - Mathf.Pow(1f - Mathf.Clamp01(age / _runTime), 2.4f) : 1f;
                if (age <= _runTime + 0.1f) SetRun(Mathf.Max(0.02f, _runLen * k));
            }
            else if (_spreadTime > 0f && age <= _spreadTime + 0.1f)
            {
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / _spreadTime), 3f);
                float s = Mathf.Lerp(_spreadFrom, 1f, k);
                transform.localScale = new Vector3(_full.x * s, _full.y * s, 1f);
            }

            if (Time.time >= _nextShade)
            {
                _nextShade = Time.time + 0.4f + Random.value * 0.2f;
                float fade = 1f - Mathf.Clamp01((age - (_life - 4f)) / 4f);
                if (fade <= 0f) { Destroy(gameObject); return; }
                Shade(fade);
            }
        }

        private void Shade(float fade)
        {
            float age = Time.time - _born;
            var c = BloodFx.Fresh;
            c.a = Alpha * fade;
            _mpb.SetColor(ColorId, c);
            _mpb.SetFloat(WetId, Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(age / 150f)));
            _r.SetPropertyBlock(_mpb);
        }
    }
}
