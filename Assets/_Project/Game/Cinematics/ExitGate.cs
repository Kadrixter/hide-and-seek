using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// The street exit on level 0 (east wall, x = 90, z -3.5..3.5): a heavy roller shutter that the round intro slams
    /// shut, a small electrical box with a hand-sized lever next to it, and the night street behind (only seen while
    /// the shutter is up). Built at runtime in the game scene (<see cref="Ensure"/>); outside the intro the shutter is
    /// down and the lever thrown, with a collider in the opening.
    /// Local space of <see cref="Root"/>: +Z into the garage, +X = world +Z, origin on the floor at the inner wall face.
    /// The map builder cuts the opening (older scenes still have the painted shutter: then the street stays hidden).
    /// </summary>
    public sealed partial class ExitGate : MonoBehaviour
    {
        public const float HalfW = 3.5f, OpenH = 2.75f, CurtainZ = -0.2f;
        /// <summary>World position of the inner wall face at the opening centre (fallback when the scene has no anchor).</summary>
        public static readonly Vector3 DefaultPos = new Vector3(90f, 0f, 0f);
        /// <summary>Electrical box on the wall, local (left of the gate seen from inside).</summary>
        public static readonly Vector3 BoxPos = new Vector3(-4.75f, 1.22f, 0f);
        public const float LeverUp = 32f, LeverDown = 148f;

        public static ExitGate Instance { get; private set; }

        public Transform Root => transform;
        float _open = -1f, _lever = -1f;
        float _slatH;
        Transform[] _slats;
        Transform _bottomBar, _leverPivot, _knob;
        BoxCollider _block;
        Light _street, _boxLamp;
        Material _lampMat;
        bool _lampRed = true;

        /// <summary>Creates the gate in the game scene if it is not there yet.</summary>
        public static ExitGate Ensure()
        {
            if (Instance != null) return Instance;
            var anchor = GameObject.Find("ExitGateAnchor");
            Vector3 pos = anchor != null ? anchor.transform.position : DefaultPos;
            var go = new GameObject("ExitGate");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, -90f, 0f));
            int layer = HideAndSeek.Map.LevelLighting.LayerOf(0);
            go.layer = layer;
            var g = go.AddComponent<ExitGate>();
            g.StreetVisible = anchor != null || HasOpening();
            g.Build();
            Instance = g;
            g.SetOpen(0f);
            g.SetLever(1f);
            Debug.Log($"[Cine] exit gate built at {pos}, street {(g.StreetVisible ? "open" : "behind the old painted shutter")}");
            return g;
        }

        /// <summary>The map has the cut opening (a ray from the garage through the gate leaves the building).</summary>
        static bool HasOpening()
        {
            var from = DefaultPos + new Vector3(-1f, 1.2f, 0f);
            return !Physics.Raycast(from, Vector3.right, 3f, ~0, QueryTriggerInteraction.Ignore);
        }

        public bool StreetVisible { get; private set; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>World point of the lever knob (for the hand IK).</summary>
        public Vector3 KnobWorld => _knob != null ? _knob.position : transform.TransformPoint(BoxPos);
        /// <summary>World point of the box front.</summary>
        public Vector3 BoxWorld => transform.TransformPoint(BoxPos + new Vector3(0f, 0f, 0.09f));
        public Vector3 Local(Vector3 p) => transform.TransformPoint(p);
        public Vector3 LocalDir(Vector3 d) => transform.TransformDirection(d);
        /// <summary>World point on the floor under the middle of the shutter, garage side.</summary>
        public Vector3 SillWorld => transform.TransformPoint(new Vector3(0f, 0f, 0.05f));

        /// <summary>0 = shut, 1 = fully up.</summary>
        public void SetOpen(float f)
        {
            f = Mathf.Clamp01(f);
            if (Mathf.Approximately(f, _open)) return;
            _open = f;
            float bottom = f * OpenH;
            if (_bottomBar != null) _bottomBar.localPosition = new Vector3(0f, bottom + 0.045f, CurtainZ);
            if (_slats != null)
                for (int i = 0; i < _slats.Length; i++)
                {
                    float y = bottom + 0.09f + i * _slatH;
                    var s = _slats[i];
                    bool on = y < OpenH + 0.05f;
                    if (s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                    s.localPosition = new Vector3(0f, y + _slatH * 0.5f, CurtainZ);
                }
            if (_block != null) _block.enabled = f < 0.35f;
            float lit = Mathf.Clamp01(f * 1.6f) * (StreetVisible ? 1f : 0f);
            if (_street != null) _street.intensity = StreetLight * lit;
            if (_streetFill != null) _streetFill.intensity = 0.35f * lit;
        }

        /// <summary>Lever throw 0 = up (armed), 1 = down (pulled).</summary>
        public void SetLever(float k)
        {
            if (Mathf.Approximately(k, _lever)) return;
            _lever = k;
            if (_leverPivot != null) _leverPivot.localRotation = Quaternion.Euler(Mathf.Lerp(LeverUp, LeverDown, k), 0f, 0f);
        }

        /// <summary>Status lamp on the box: green (gate open) or red (locked).</summary>
        public void SetLamp(bool red, float flicker = 1f)
        {
            _lampRed = red;
            var c = red ? new Color(1f, 0.12f, 0.08f) : new Color(0.2f, 1f, 0.35f);
            if (_lampMat != null) CineKit.SetGlow(_lampMat, c, 2.2f * flicker);
            if (_boxLamp != null)
            {
                _boxLamp.color = c;
                _boxLamp.intensity = 0.5f * flicker;
            }
        }

        public bool LampRed => _lampRed;
    }
}
