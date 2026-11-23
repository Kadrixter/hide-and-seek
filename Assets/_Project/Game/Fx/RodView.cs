using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Metal rod on the map: stuck in a rubble heap (slightly bent near the top, a third of it buried, broken bits
    /// of concrete round where it goes in) or, on old maps, leaning against a rack / fence. A faint cold glint helps
    /// to find it in the dark garage. The rod's pose can be driven every frame (<see cref="SetPose"/>) - the pull
    /// animation moves it out in jerks, wobbles it, and after the rip it goes into the puller's hands.
    /// </summary>
    public class RodView : MonoBehaviour
    {
        /// <summary>Length of the bent top part and its bend (degrees).</summary>
        public const float BendLength = 0.2f, BendAngle = 13f;

        private Light _glint;
        private float _phase;
        private Transform _rod, _upper;
        private Vector3 _restBase, _restAxis, _bendSide;
        private bool _embedded;
        private int _shownDone = -1;

        /// <summary>Rest pose: buried end, axis (towards the top), side the top is bent to.</summary>
        public Vector3 RestBase => _restBase;
        public Vector3 RestAxis => _restAxis;
        public Vector3 BendSide => _bendSide;
        public bool Embedded => _embedded;

        public static GameObject Create(RodState r)
        {
            var root = new GameObject(r.Embedded ? "MetalRodInRubble" : "MetalRod");
            var view = root.AddComponent<RodView>();
            view._embedded = r.Embedded;
            view._restBase = r.Base;
            view._restAxis = (r.Top - r.Base).normalized;
            // bent away from the puller's side a little, sideways
            Vector3 toStand = r.Stand - r.Base;
            toStand.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, toStand.sqrMagnitude > 1e-4f ? toStand.normalized : Vector3.forward);
            view._bendSide = Vector3.ProjectOnPlane(side * (r.Id % 2 == 0 ? 1f : -1f) + toStand.normalized * 0.4f, view._restAxis).normalized;
            if (view._bendSide.sqrMagnitude < 0.5f) view._bendSide = Vector3.Cross(view._restAxis, Vector3.right).normalized;
            view.BuildRod(root.transform);
            if (r.Embedded) view.BuildBed(root.transform, r);

            var lg = new GameObject("Glint");
            lg.transform.SetParent(root.transform, false);
            lg.transform.position = Vector3.Lerp(r.Top, r.Base, 0.4f) + Vector3.up * 0.25f;
            view._glint = lg.AddComponent<Light>();
            view._glint.type = LightType.Point;
            view._glint.color = new Color(0.75f, 0.85f, 1f);
            view._glint.range = 1.6f;
            view._glint.intensity = 0.55f;
            view._glint.shadows = LightShadows.None;
            view._phase = Random.Range(0f, 10f);
            view.SetPose(r.Base, view._restAxis);
            return root;
        }

        /// <summary>The loose rod model, re-cut into a straight lower part and a bent top part.</summary>
        private void BuildRod(Transform root)
        {
            _rod = new GameObject("Rod").transform;
            _rod.SetParent(root, false);
            // RodModel: origin = top, hanging along -Y. Here: origin = the bend, lower part along -Y, top part up.
            var parts = RodModel.CreateLoose(_rod, 0);
            parts.Root.localPosition = new Vector3(0f, BendLength, 0f);
            float pipeLen = RodModel.Length - RodModel.FootHeight + 0.01f;
            // lower pipe: from the bend down
            float lower = pipeLen - BendLength;
            parts.Pipe.localScale = new Vector3(RodModel.PipeWidth, lower, RodModel.PipeWidth);
            parts.Pipe.localPosition = new Vector3(0f, -BendLength - lower * 0.5f, 0f);
            // top part: a copy of the pipe, bent
            _upper = new GameObject("BentTop").transform;
            _upper.SetParent(_rod, false);
            var top = Instantiate(parts.Pipe.gameObject, _upper).transform;
            top.localPosition = new Vector3(0f, BendLength * 0.5f, 0f);
            top.localScale = new Vector3(RodModel.PipeWidth, BendLength + 0.01f, RodModel.PipeWidth);
            top.localRotation = Quaternion.identity;
            _upper.localRotation = Quaternion.Euler(BendAngle, 0f, 0f);
            if (parts.Clamp != null) parts.Clamp.gameObject.SetActive(false);
            foreach (var mr in _rod.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>Broken concrete round the point where the rod goes in (static, stays after the rod is out).</summary>
        private void BuildBed(Transform root, RodState r)
        {
            var bed = new GameObject("RubbleBed").transform;
            bed.SetParent(root, false);
            Material mat = null;
            foreach (var c in Physics.OverlapSphere(r.Entry, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                if (c.name.StartsWith("Rubble_") && c.TryGetComponent<MeshRenderer>(out var mr)) { mat = mr.sharedMaterial; break; }
            if (mat == null)
            {
                var sh = Shader.Find("Standard");
                mat = new Material(sh != null ? sh : Shader.Find("Diffuse")) { color = new Color(0.55f, 0.55f, 0.53f), hideFlags = HideFlags.DontSave };
            }
            var rnd = new System.Random(r.Id * 977);
            Vector3 e = r.Entry;
            for (int i = 0; i < 9; i++)
            {
                float a = (float)(i / 9.0 * Mathf.PI * 2.0 + rnd.NextDouble() * 0.6);
                float d = 0.07f + (float)rnd.NextDouble() * 0.12f;
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(g.GetComponent<Collider>());
                g.name = "Chunk";
                g.transform.SetParent(bed, false);
                float s = 0.035f + (float)rnd.NextDouble() * 0.06f;
                g.transform.position = e + new Vector3(Mathf.Cos(a) * d, s * 0.2f - 0.01f, Mathf.Sin(a) * d);
                g.transform.rotation = Quaternion.Euler((float)rnd.NextDouble() * 60f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 60f);
                g.transform.localScale = new Vector3(s, s * 0.7f, s * 1.2f);
                var r2 = g.GetComponent<MeshRenderer>();
                r2.sharedMaterial = mat;
                r2.lightProbeUsage = LightProbeUsage.Off;
            }
        }

        /// <summary>Puts the rod with its buried (lower) end at <paramref name="baseEnd"/>, the straight part along <paramref name="axis"/>.</summary>
        public void SetPose(Vector3 baseEnd, Vector3 axis)
        {
            if (_rod == null) return;
            axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
            Vector3 side = Vector3.ProjectOnPlane(_bendSide, axis);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(axis, Vector3.right);
            float lower = RodModel.Length - BendLength;
            _rod.SetPositionAndRotation(baseEnd + axis * lower, Quaternion.LookRotation(side.normalized, axis));
        }

        /// <summary>World point on the rod <paramref name="fromTop"/> metres below its (bent) top end.</summary>
        public Vector3 PointFromTop(float fromTop)
        {
            if (_rod == null) return transform.position;
            if (fromTop <= BendLength) return _upper.TransformPoint(new Vector3(0f, BendLength - fromTop, 0f));
            return _rod.TransformPoint(new Vector3(0f, -(fromTop - BendLength), 0f));
        }

        /// <summary>Direction of the straight part (towards the top).</summary>
        public Vector3 Axis => _rod != null ? _rod.up : _restAxis;

        public void SetRodVisible(bool on)
        {
            if (_rod != null && _rod.gameObject.activeSelf != on) _rod.gameObject.SetActive(on);
            if (_glint != null) _glint.enabled = on;
        }

        /// <summary>Rest pose from the shared state (not while somebody pulls it - the pull driver poses it then).</summary>
        public void Sync(RodState r)
        {
            if (r.BeingPulled || !_embedded || r.Done == _shownDone) return;
            _shownDone = r.Done;
            float o = r.Done > 0 ? RodPullTimeline.OutAfter[Mathf.Min(r.Done, RodPullTimeline.OutAfter.Length) - 1] : 0f;
            SetPose(_restBase + _restAxis * (o * RodPullTimeline.Buried), _restAxis);
            SetRodVisible(true);
        }

        /// <summary>Forces <see cref="Sync"/> to re-apply the rest pose (after a pull stopped).</summary>
        public void Unsynced() => _shownDone = -1;

        private void Update()
        {
            if (_glint == null) return;
            // Slow shimmer, like a reflection of a distant lamp.
            float k = 0.5f + 0.5f * Mathf.Sin((Time.time + _phase) * 1.3f);
            _glint.intensity = 0.35f + 0.3f * k * k;
        }
    }
}
