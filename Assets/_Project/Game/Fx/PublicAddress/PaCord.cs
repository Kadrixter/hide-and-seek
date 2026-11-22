using HideAndSeek.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Coiled (spiral) microphone cord between the gland under the wall box and the mic: a fixed number of coils wound
    /// around a centre line that sags under its own weight when slack and straightens as it is pulled, the coils
    /// spreading out and getting a little thinner when stretched; short straight tails at both ends. The tube mesh has
    /// a fixed topology and is rewritten only when an end moves.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PaCord : MonoBehaviour
    {
        private const int Coils = 70, PerCoil = 8, Tail = 4, Sides = 5, CentreSamples = 48;
        private const float WireR = 0.0027f, CoilR = 0.0105f, TailLen = 0.035f;
        /// <summary>Cord length when it is pulled straight (beyond that the mic is torn out of the hand).</summary>
        public const float MaxStretch = 2.9f;

        private Mesh _mesh;
        private Vector3[] _v, _n;
        private readonly Vector3[] _c = new Vector3[CentreSamples + 1];
        private readonly float[] _len = new float[CentreSamples + 1];
        private Vector3 _lastA, _lastB, _lastBDir;
        private bool _built;

        private int PathCount => Coils * PerCoil + 1 + Tail * 2;

        public static PaCord Create(Transform parent, int layer, Material mat)
        {
            var go = new GameObject("PaCord");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var cord = go.AddComponent<PaCord>();
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return cord;
        }

        private void Init()
        {
            int pts = PathCount;
            _v = new Vector3[pts * Sides];
            _n = new Vector3[pts * Sides];
            var tris = new int[(pts - 1) * Sides * 6];
            int k = 0;
            for (int i = 0; i < pts - 1; i++)
            for (int s = 0; s < Sides; s++)
            {
                int s1 = (s + 1) % Sides;
                int a = i * Sides + s, b = i * Sides + s1, c = (i + 1) * Sides + s1, d = (i + 1) * Sides + s;
                // (a, b, c) faces along Cross(b - a, c - a): outwards for rings wound around the path direction
                tris[k++] = a; tris[k++] = b; tris[k++] = c;
                tris[k++] = a; tris[k++] = c; tris[k++] = d;
            }
            _mesh = new Mesh { name = "PaCordCoil" };
            _mesh.MarkDynamic();
            _mesh.vertices = _v;
            _mesh.normals = _n;
            _mesh.triangles = tris;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        /// <summary>
        /// Lays the cord from <paramref name="a"/> (leaving along <paramref name="aDir"/>) to <paramref name="b"/> (leaving the
        /// mic along <paramref name="bDir"/>); the sag never goes below <paramref name="floorY"/> nor behind the wall plane
        /// (<paramref name="wallPoint"/>, <paramref name="wallNormal"/> pointing out of the wall). World space.
        /// </summary>
        public void Set(Vector3 a, Vector3 aDir, Vector3 b, Vector3 bDir, float floorY, Vector3 wallPoint, Vector3 wallNormal)
        {
            if (_mesh == null) Init();
            if (_built && (a - _lastA).sqrMagnitude < 1e-8f && (b - _lastB).sqrMagnitude < 1e-8f && (bDir - _lastBDir).sqrMagnitude < 1e-6f) return;
            _built = true;
            _lastA = a; _lastB = b; _lastBDir = bDir;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // vertices are world space

            float dist = Vector3.Distance(a, b);
            float slack = Mathf.Clamp01(1f - dist / MaxStretch);
            float stretch = Mathf.Clamp01((dist - 0.5f) / (MaxStretch - 0.5f));
            // Relaxed (on the hook) it hangs in a short loop with the coils close together; pulled, it straightens.
            float reach = 0.04f + 0.14f * slack;
            float sag = 0.2f * slack * slack + 0.03f;
            Vector3 p0 = a + aDir.normalized * TailLen, p3 = b + bDir.normalized * TailLen;
            Vector3 p1 = p0 + aDir.normalized * reach + Vector3.down * sag;
            Vector3 p2 = p3 + bDir.normalized * reach + Vector3.down * sag;
            p1.y = Mathf.Max(p1.y, floorY + 0.04f);
            p2.y = Mathf.Max(p2.y, floorY + 0.04f);

            // Centre line samples + arc length.
            float total = 0f;
            for (int i = 0; i <= CentreSamples; i++)
            {
                float t = i / (float)CentreSamples, u = 1f - t;
                Vector3 p = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                p.y = Mathf.Max(p.y, floorY + 0.02f);
                float off = Vector3.Dot(p - wallPoint, wallNormal);
                if (off < 0.018f) p += wallNormal * (0.018f - off);
                _c[i] = p;
                if (i > 0) total += Vector3.Distance(_c[i - 1], p);
                _len[i] = total;
            }

            float coilR = CoilR * Mathf.Lerp(1f, 0.62f, stretch);
            int idx = 0;
            Vector3 prevTan = (p0 - a).sqrMagnitude > 1e-10f ? (p0 - a).normalized : Vector3.down;
            Vector3 nrm = Vector3.Cross(prevTan, Mathf.Abs(prevTan.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 prevPos = a;

            // Straight tail out of the gland.
            for (int i = 0; i < Tail; i++)
            {
                float t = i / (float)Tail;
                Vector3 pos = Vector3.Lerp(a, p0, t);
                WriteRing(ref idx, pos, ref prevTan, ref nrm, (p0 - a), WireR * 1.15f);
                prevPos = pos;
            }
            // Coils around the centre line.
            int coilPts = Coils * PerCoil + 1;
            int seg = 0;
            Vector3 cn = nrm;
            Vector3 cTanPrev = (_c[1] - _c[0]).normalized;
            for (int i = 0; i < coilPts; i++)
            {
                float u = i / (float)(coilPts - 1);
                float want = u * total;
                while (seg < CentreSamples - 1 && _len[seg + 1] < want) seg++;
                float segLen = Mathf.Max(1e-6f, _len[seg + 1] - _len[seg]);
                float f = Mathf.Clamp01((want - _len[seg]) / segLen);
                Vector3 centre = Vector3.Lerp(_c[seg], _c[seg + 1], f);
                Vector3 tan = (_c[seg + 1] - _c[seg]).normalized;
                // parallel transport of the coil frame along the centre line
                Vector3 ax = Vector3.Cross(cTanPrev, tan);
                if (ax.sqrMagnitude > 1e-12f) cn = Quaternion.AngleAxis(Vector3.SignedAngle(cTanPrev, tan, ax.normalized), ax.normalized) * cn;
                cTanPrev = tan;
                Vector3 bin = Vector3.Cross(tan, cn).normalized;
                cn = Vector3.Cross(bin, tan).normalized;
                float ang = u * Coils * Mathf.PI * 2f;
                // the helix starts and ends on the centre line (smooth join with the tails)
                float r = coilR * Mathf.Clamp01(Mathf.Min(u, 1f - u) * Coils * 0.8f);
                Vector3 pos = centre + (Mathf.Cos(ang) * cn + Mathf.Sin(ang) * bin) * r;
                WriteRing(ref idx, pos, ref prevTan, ref nrm, pos - prevPos, WireR);
                prevPos = pos;
            }
            // Straight tail into the strain relief.
            for (int i = 1; i <= Tail; i++)
            {
                float t = i / (float)Tail;
                Vector3 pos = Vector3.Lerp(p3, b, t);
                WriteRing(ref idx, pos, ref prevTan, ref nrm, pos - prevPos, WireR * 1.15f);
                prevPos = pos;
            }
            _mesh.vertices = _v;
            _mesh.normals = _n;
            _mesh.RecalculateBounds();
        }

        private void WriteRing(ref int idx, Vector3 pos, ref Vector3 prevTan, ref Vector3 nrm, Vector3 step, float r)
        {
            Vector3 tan = step.sqrMagnitude > 1e-12f ? step.normalized : prevTan;
            Vector3 ax = Vector3.Cross(prevTan, tan);
            if (ax.sqrMagnitude > 1e-12f) nrm = Quaternion.AngleAxis(Vector3.SignedAngle(prevTan, tan, ax.normalized), ax.normalized) * nrm;
            prevTan = tan;
            Vector3 bin = Vector3.Cross(tan, nrm).normalized;
            nrm = Vector3.Cross(bin, tan).normalized;
            for (int s = 0; s < Sides; s++)
            {
                float a = s * Mathf.PI * 2f / Sides;
                Vector3 d = Mathf.Cos(a) * nrm + Mathf.Sin(a) * bin;
                _v[idx * Sides + s] = pos + d * r;
                _n[idx * Sides + s] = d;
            }
            idx++;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
