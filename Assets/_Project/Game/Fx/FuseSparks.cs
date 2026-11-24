using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// A burst of electric sparks (fuse box coming alive): short glowing streaks shot out along a direction, falling,
    /// bouncing once off the floor and burning out, with a blue-white flash of light. Local effect, destroys itself.
    /// </summary>
    public sealed class FuseSparks : MonoBehaviour
    {
        private Transform[] _s;
        private Vector3[] _v;
        private float[] _life, _age;
        private float _floor, _t;
        private Light _flash;
        private static Material _mat;
        private static Mesh _streak;

        public static void Burst(Vector3 at, Vector3 dir, int count)
        {
            var go = new GameObject("FuseSparks");
            go.transform.position = at;
            var fx = go.AddComponent<FuseSparks>();
            fx.Init(at, dir.normalized, count);
        }

        private void Init(Vector3 at, Vector3 dir, int count)
        {
            _mat ??= new Material(Shader.Find("Unlit/Color")) { color = new Color(1f, 0.85f, 0.45f), name = "FuseSpark" };
            if (_streak == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _streak = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }
            _floor = Physics.Raycast(at, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y + 0.01f : at.y - 1.3f;
            _s = new Transform[count];
            _v = new Vector3[count];
            _life = new float[count];
            _age = new float[count];
            for (int i = 0; i < count; i++)
            {
                var g = new GameObject("Spark");
                g.transform.SetParent(transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = _streak;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = _mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.transform.position = at + Random.insideUnitSphere * 0.08f;
                _s[i] = g.transform;
                _v[i] = (dir + Random.insideUnitSphere * 0.9f + Vector3.up * 0.3f).normalized * Random.Range(1.5f, 4.2f);
                _life[i] = Random.Range(0.35f, 0.95f);
            }
            _flash = new GameObject("Flash").AddComponent<Light>();
            _flash.transform.SetParent(transform, false);
            _flash.transform.position = at + dir * 0.15f;
            _flash.type = LightType.Point;
            _flash.color = new Color(0.75f, 0.85f, 1f);
            _flash.range = 6f;
            _flash.intensity = 4f;
            _flash.shadows = LightShadows.None;
            HideAndSeek.Map.LevelLighting.Instance?.Track(_flash);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_flash != null) _flash.intensity = _t < 0.06f ? 4f : Mathf.Max(0f, 4f * (1f - (_t - 0.06f) / 0.2f)) * (Random.value > 0.3f ? 1f : 0.4f);
            bool any = false;
            for (int i = 0; i < _s.Length; i++)
            {
                if (_s[i] == null) continue;
                _age[i] += dt;
                if (_age[i] > _life[i]) { Destroy(_s[i].gameObject); continue; }
                any = true;
                _v[i] += Vector3.down * 9.8f * dt;
                Vector3 p = _s[i].position + _v[i] * dt;
                if (p.y < _floor && _v[i].y < 0f) { p.y = _floor; _v[i] = new Vector3(_v[i].x * 0.5f, -_v[i].y * 0.3f, _v[i].z * 0.5f); }
                _s[i].position = p;
                float k = 1f - _age[i] / _life[i];
                if (_v[i].sqrMagnitude > 1e-4f) _s[i].rotation = Quaternion.LookRotation(_v[i]);
                _s[i].localScale = new Vector3(0.006f, 0.006f, 0.012f + 0.02f * Mathf.Min(1f, _v[i].magnitude / 3f)) * (0.4f + 0.6f * k);
            }
            if (!any && _t > 0.3f) Destroy(gameObject);
        }
    }
}
