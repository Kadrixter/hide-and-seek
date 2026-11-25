using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Black rubber skid marks on the lobby floor: one strip per rear wheel, laid while the tyre slides (a new quad
    /// every 7 cm, joined to the previous one), a few millimetres over whatever floor / rug is under the wheel. All
    /// marks are one world-space mesh (vertex colours, alpha-blended); the oldest are recycled past
    /// <see cref="MaxQuads"/> and every mark fades away over a minute.
    /// </summary>
    public sealed class KartSkids : MonoBehaviour
    {
        public const int MaxQuads = 1400;
        private const float Step = 0.07f, HalfWidth = 0.075f, Life = 60f, FadeFrom = 35f;
        private static readonly Color Rubber = new(0.035f, 0.035f, 0.04f, 1f);

        private struct Quad { public Vector3 A0, A1, B0, B1; public float Born, Alpha; }

        private readonly List<Quad> _quads = new();
        private int _head;
        private readonly bool[] _on = new bool[2];
        private readonly Vector3[] _lastL = new Vector3[2], _lastR = new Vector3[2], _lastC = new Vector3[2];
        private Mesh _mesh;
        private bool _dirty;
        private float _nextFade;
        private Vector3[] _v;
        private Color[] _c;
        private int[] _t;

        public int Count => _quads.Count;

        public static KartSkids Create()
        {
            var go = new GameObject("KartSkidMarks");
            var s = go.AddComponent<KartSkids>();
            s._mesh = new Mesh { name = "KartSkids" };
            s._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = s._mesh;
            var r = go.AddComponent<MeshRenderer>();
            // Dark tint on the material (vertex colours carry only the fade: white x alpha).
            r.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "KartSkids", renderQueue = 2990, color = Rubber };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return s;
        }

        /// <summary>Wheel <paramref name="i"/> at <paramref name="contact"/> (floor height of the kart); lays marks while <paramref name="sliding"/>.</summary>
        public void Track(int i, Vector3 contact, Vector3 right, bool sliding, float alpha)
        {
            if (!sliding) { _on[i] = false; return; }
            Vector3 p = contact;
            if (Physics.Raycast(contact + Vector3.up * 0.3f, Vector3.down, out var hit, 0.6f, KartCollide.LevelMask, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            p.y += 0.006f;
            right.y = 0f;
            right = right.sqrMagnitude > 1e-4f ? right.normalized * HalfWidth : Vector3.right * HalfWidth;
            if (!_on[i])
            {
                _on[i] = true;
                _lastC[i] = p;
                _lastL[i] = p - right;
                _lastR[i] = p + right;
                return;
            }
            if ((p - _lastC[i]).sqrMagnitude < Step * Step) return;
            var q = new Quad { A0 = _lastL[i], A1 = _lastR[i], B0 = p - right, B1 = p + right, Born = Time.time, Alpha = alpha * 0.85f };
            if (_quads.Count < MaxQuads) _quads.Add(q);
            else { _quads[_head] = q; _head = (_head + 1) % MaxQuads; }
            _lastC[i] = p;
            _lastL[i] = q.B0;
            _lastR[i] = q.B1;
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (Time.time >= _nextFade) { _nextFade = Time.time + 1f; _dirty |= _quads.Count > 0; }
            if (!_dirty) return;
            _dirty = false;
            int n = _quads.Count;
            if (_v == null || _v.Length < n * 4)
            {
                int cap = Mathf.Min(MaxQuads, Mathf.Max(64, n * 2));
                _v = new Vector3[cap * 4];
                _c = new Color[cap * 4];
                _t = new int[cap * 6];
            }
            float now = Time.time;
            for (int k = 0; k < n; k++)
            {
                var q = _quads[k];
                float age = now - q.Born;
                float a = age >= Life ? 0f : q.Alpha * (age <= FadeFrom ? 1f : 1f - (age - FadeFrom) / (Life - FadeFrom));
                var col = new Color(1f, 1f, 1f, a);
                int v = k * 4;
                _v[v] = q.A0; _v[v + 1] = q.A1; _v[v + 2] = q.B1; _v[v + 3] = q.B0;
                _c[v] = _c[v + 1] = _c[v + 2] = _c[v + 3] = col;
                int t = k * 6;
                _t[t] = v; _t[t + 1] = v + 3; _t[t + 2] = v + 2; _t[t + 3] = v; _t[t + 4] = v + 2; _t[t + 5] = v + 1;
            }
            for (int k = n * 6; k < _t.Length; k++) _t[k] = 0;
            _mesh.Clear();
            _mesh.vertices = _v;
            _mesh.colors = _c;
            _mesh.triangles = _t;
            _mesh.RecalculateBounds();
        }
    }
}
