using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Clips the volumetric flashlight beam (nested cone shells from <see cref="FlashlightProcedural.BuildBeamMesh"/>)
    /// against the level: one ray per shell segment, vertices beyond the first hit collapse onto it, so the beam
    /// never pokes through walls, floors or ceilings for players on the other side.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public class BeamOcclusion : MonoBehaviour
    {
        private const int Segments = 40, Rings = 16, Shells = 3;
        private const int Ring = Segments + 1;
        private const int PerShell = Ring * (Rings + 1);

        [SerializeField] private float updateInterval = 0.05f;
        [SerializeField] private LayerMask mask = ~((1 << 8) | (1 << 9) | (1 << 2));

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Vector3[] _base, _verts;
        private float[] _t;
        private float _next;
        private bool _valid;

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            var src = _filter.sharedMesh;
            if (src == null || src.vertexCount != PerShell * Shells) return;
            _mesh = Instantiate(src);
            _mesh.name = src.name + " (clipped)";
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
            _base = _mesh.vertices;
            _verts = (Vector3[])_base.Clone();
            float len = _base[Rings * Ring].z;
            _t = new float[Rings + 1];
            for (int ri = 0; ri <= Rings; ri++) _t[ri] = len > 0f ? _base[ri * Ring].z / len : 1f;
            _valid = true;
        }

        private void LateUpdate()
        {
            if (!_valid || (_renderer != null && !_renderer.enabled)) return;
            if (Time.time < _next) return;
            _next = Time.time + updateInterval;

            for (int s = 0; s < Shells; s++)
            {
                int b = s * PerShell;
                for (int si = 0; si < Segments; si++)
                {
                    Vector3 p0 = _base[b + si];
                    Vector3 pe = _base[b + Rings * Ring + si];
                    Vector3 w0 = transform.TransformPoint(p0);
                    Vector3 we = transform.TransformPoint(pe);
                    Vector3 d = we - w0;
                    float dist = d.magnitude;
                    float frac = 1f;
                    if (dist > 0.01f && Physics.Raycast(w0, d / dist, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
                        frac = Mathf.Max(0.02f, (hit.distance + 0.05f) / dist);

                    for (int ri = 0; ri <= Rings; ri++)
                    {
                        float t = _t[ri];
                        float f = t <= 0f ? 1f : Mathf.Min(t, frac) / t;
                        int i = b + ri * Ring + si;
                        _verts[i] = p0 + (_base[i] - p0) * f;
                    }
                }
                // Seam column (si == Segments) duplicates column 0.
                for (int ri = 0; ri <= Rings; ri++)
                    _verts[b + ri * Ring + Segments] = _verts[b + ri * Ring];
            }
            _mesh.vertices = _verts;
        }
    }
}
