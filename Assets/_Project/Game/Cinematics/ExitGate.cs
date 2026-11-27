using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// The street exit on level 0 (east wall, x = 90, z -3.5..3.5): a heavy roller shutter that the round intro slams
    /// shut, a small electrical box with a hand-sized lever next to it, and the night street behind (only seen while
    /// the shutter is up). Built at runtime in the game scene (<see cref="Ensure"/>); outside the intro the shutter is
    /// down and the lever thrown and torn off (the killer rips the handle off in the intro, only a stub is left), with
    /// a collider in the opening.
    /// Local space of <see cref="Root"/>: +Z into the garage, +X = world +Z, origin on the floor at the inner wall face.
    /// The map builder cuts the opening (older scenes still have the painted shutter: then the street stays hidden).
    /// </summary>
    public sealed partial class ExitGate : MonoBehaviour
    {
        public const float HalfW = 3.5f, OpenH = 2.75f, CurtainZ = -0.2f;
        /// <summary>Gap under the curtain from which its collider leaves room (a crouching player fits), top of the collider.</summary>
        const float BlockFrom = 1.2f, BlockTop = 3.2f;
        /// <summary>World position of the inner wall face at the opening centre (fallback when the scene has no anchor).</summary>
        public static readonly Vector3 DefaultPos = new Vector3(90f, 0f, 0f);
        /// <summary>Electrical box on the wall, local (left of the gate seen from inside).</summary>
        public static readonly Vector3 BoxPos = new Vector3(-4.75f, 1.22f, 0f);
        public const float LeverUp = 32f, LeverDown = 148f;
        /// <summary>Lever pivot relative to <see cref="BoxPos"/> (right edge of the box front, seen from the garage).</summary>
        public static readonly Vector3 LeverPivotInBox = new Vector3(0.135f, -0.05f, 0.17f);

        /// <summary>The east gate (gate 0: the intro, the outro and the dot link use this one).</summary>
        public static ExitGate Instance { get; private set; }
        /// <summary>The same gate on the west wall of level 0 (gate 1, 2026-11-24): opens together with the east one.</summary>
        public static ExitGate West { get; private set; }
        public const int Count = 2;
        public static ExitGate Get(int i) => i == 0 ? Instance : i == 1 ? West : null;
        /// <summary>0 = east, 1 = west.</summary>
        public int Index { get; private set; }

        public static Vector3 DefaultPosOf(int i) => i == 0 ? DefaultPos : new Vector3(-DefaultPos.x, DefaultPos.y, DefaultPos.z);
        public static Quaternion DefaultRotOf(int i) => Quaternion.Euler(0f, i == 0 ? -90f : 90f, 0f);
        public static string AnchorName(int i) => i == 0 ? "ExitGateAnchor" : "ExitGateAnchorW";

        public Transform Root => transform;
        float _open = -1f, _lever = -1f;
        float _slatH;
        Transform[] _slats;
        Transform _bottomBar, _leverPivot, _knob, _rod, _stub;
        BoxCollider _block;
        Light _street, _boxLamp;
        Material _lampMat;
        bool _lampRed = true;

        /// <summary>Creates both gates in the game scene if they are not there yet; returns the east one.</summary>
        public static ExitGate Ensure()
        {
            if (Instance == null) Instance = Create(0);
            if (West == null) West = Create(1);
            return Instance;
        }

        static ExitGate Create(int i)
        {
            var anchor = GameObject.Find(AnchorName(i));
            Vector3 pos = anchor != null ? anchor.transform.position : DefaultPosOf(i);
            var go = new GameObject(i == 0 ? "ExitGate" : "ExitGateWest");
            go.transform.SetPositionAndRotation(pos, DefaultRotOf(i));
            int layer = HideAndSeek.Map.LevelLighting.LayerOf(0);
            go.layer = layer;
            var g = go.AddComponent<ExitGate>();
            g.Index = i;
            g.StreetVisible = anchor != null || HasOpening(i);
            g.Build();
            g.SetOpen(0f);
            g.SetLever(1f);
            g.SetBroken(true); // both levers torn off: each box has the dot link panel (the intro tears the east one on camera)
            g.SetLamp(true); // red until the gate opens (the intro drives the east one itself; the west one had no colour)
            Debug.Log($"[Cine] exit gate {i} built at {pos}, street {(g.StreetVisible ? "open" : "behind the old painted shutter")}");
            return g;
        }

        /// <summary>The map has the cut opening (a ray from the garage through the gate leaves the building).</summary>
        static bool HasOpening(int i)
        {
            Vector3 inward = DefaultRotOf(i) * Vector3.forward;
            var from = DefaultPosOf(i) + inward + Vector3.up * 1.2f;
            return !Physics.Raycast(from, -inward, 3f, ~0, QueryTriggerInteraction.Ignore);
        }

        public bool StreetVisible { get; private set; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (West == this) West = null;
        }

        /// <summary>World point of the lever knob (for the hand IK).</summary>
        public Vector3 KnobWorld => _knob != null ? _knob.position : transform.TransformPoint(BoxPos);
        /// <summary>World point at the torn end of the stub (sparks).</summary>
        public Vector3 StubWorld => _leverPivot != null ? _leverPivot.TransformPoint(new Vector3(0f, 0.04f, 0f)) : KnobWorld;
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
            if (_block != null)
            {
                // The collider follows the curtain once a crouching player fits under it (escape: Game/Escape);
                // below that the whole opening stays blocked (no crawling under a knee-high gap).
                float from = bottom >= BlockFrom ? bottom : 0f;
                _block.enabled = bottom < OpenH - 0.02f;
                _block.center = new Vector3(0f, (from + BlockTop) * 0.5f - _block.transform.localPosition.y, 0f);
                _block.size = new Vector3(HalfW * 2f, BlockTop - from, 0.3f);
            }
            float lit = Mathf.Clamp01(f * 1.6f) * (StreetVisible ? 1f : 0f);
            if (_street != null) _street.intensity = StreetLight * lit;
            if (_streetFill != null) _streetFill.intensity = 0.8f * lit;
        }

        /// <summary>Lever throw 0 = up (armed), 1 = down (pulled), a little over 1 = forced past the stop.</summary>
        public void SetLever(float k)
        {
            if (Mathf.Approximately(k, _lever)) return;
            _lever = k;
            if (_leverPivot != null) _leverPivot.localRotation = Quaternion.Euler(Mathf.LerpUnclamped(LeverUp, LeverDown, k), 0f, 0f);
        }

        /// <summary>The handle is torn off (only the stub on the pivot) or whole.</summary>
        public void SetBroken(bool broken)
        {
            if (_rod != null) _rod.gameObject.SetActive(!broken);
            if (_knob != null) _knob.gameObject.SetActive(!broken);
            if (_stub != null) _stub.gameObject.SetActive(broken);
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
