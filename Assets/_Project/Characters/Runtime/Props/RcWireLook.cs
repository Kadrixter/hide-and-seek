using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>What an RC antenna (transmitter or car) shows.</summary>
    public enum RcAntennaLook : byte
    {
        /// <summary>The original antenna (chrome telescope on the transmitter, whip / mast on the car).</summary>
        Stock,
        /// <summary>Transmitter only: the old antenna is pulled out, the empty socket is left.</summary>
        Bare,
        /// <summary>The long fence-wire antenna (after the swap).</summary>
        Wire,
    }

    /// <summary>
    /// Sits on an "Antenna" node of an RC transmitter (<see cref="RcRemoteModel"/>) or an RC car (rally or Yaris body)
    /// and switches it between the stock antenna and the long wire one (<see cref="WireModel.CreateAntenna"/>). Stock
    /// parts are hidden by deactivating them, so whatever enables / disables the model's renderers (car hidden, first
    /// person shadows) keeps working; the wire copies its renderer state from the stock antenna every frame.
    /// </summary>
    public class RcWireMark : MonoBehaviour
    {
        public const float RemoteWireLength = 0.44f, CarWireLength = 0.4f;

        public bool IsRemote { get; private set; }
        public bool Valid { get; private set; }

        readonly List<GameObject> _stock = new();
        Renderer _ref;
        Transform _mast, _wire;
        Renderer[] _wireR;
        Vector3 _mastBase;
        RcAntennaLook _state = (RcAntennaLook)255;
        float _lift = -1f;

        /// <summary>Returns the mark of an "Antenna" node (made on first use), null when it is no RC antenna.</summary>
        public static RcWireMark For(Transform antenna)
        {
            if (antenna == null || antenna.name != "Antenna" || antenna.parent == null) return null;
            var m = antenna.GetComponent<RcWireMark>();
            if (m != null) return m.Valid ? m : null;
            string pn = antenna.parent.name;
            bool remote = pn == "RcTransmitter";
            bool car = pn == "Rally" || pn == "YarisSquash";
            if (!remote && !car) return null;
            m = antenna.gameObject.AddComponent<RcWireMark>();
            m.Init(remote);
            return m.Valid ? m : null;
        }

        void Init(bool remote)
        {
            IsRemote = remote;
            var t = transform;
            if (remote)
            {
                // Child 0 = the black socket (stays); the chrome segments and the tip go under "Mast" (pulled out).
                if (t.childCount < 2) return;
                _ref = t.GetChild(0).GetComponent<Renderer>();
                var mast = new GameObject("Mast").transform;
                mast.gameObject.layer = gameObject.layer;
                mast.SetParent(t, false);
                for (int i = t.childCount - 2; i >= 1; i--) t.GetChild(i).SetParent(mast, false);
                _mast = mast;
                _mastBase = mast.localPosition;
                _stock.Add(mast.gameObject);
            }
            else
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    _stock.Add(c.gameObject);
                    if (_ref == null) _ref = c.GetComponentInChildren<Renderer>(true);
                }
                if (_ref == null) return;
            }
            Valid = true;
        }

        /// <summary>Model root (transmitter / car) whose scale is one model metre.</summary>
        Transform ModelRoot()
        {
            for (var p = transform.parent; p != null; p = p.parent)
            {
                if (IsRemote && p.name == "RcTransmitter") return p;
                if (!IsRemote && p.GetComponent<RcCarSkin>() != null) return p;
            }
            return transform.parent;
        }

        /// <summary>Shows <paramref name="look"/>; <paramref name="lift"/> = how far the stock transmitter antenna is pulled out (m).</summary>
        public void Apply(RcAntennaLook look, float lift)
        {
            if (!Valid) return;
            if (!IsRemote && look == RcAntennaLook.Bare) look = RcAntennaLook.Stock;
            if (look != _state)
            {
                _state = look;
                bool stock = look == RcAntennaLook.Stock;
                foreach (var g in _stock) if (g != null && g.activeSelf != stock) g.SetActive(stock);
                if (look == RcAntennaLook.Wire && _wire == null) BuildWire();
                if (_wire != null) _wire.gameObject.SetActive(look == RcAntennaLook.Wire);
            }
            if (IsRemote && _mast != null && !Mathf.Approximately(lift, _lift))
            {
                _lift = lift;
                _mast.localPosition = _mastBase + Vector3.up * Mathf.Max(0f, lift);
            }
            if (_wire != null && look == RcAntennaLook.Wire && _ref != null)
                foreach (var r in _wireR)
                {
                    if (r == null) continue;
                    if (r.enabled != _ref.enabled) r.enabled = _ref.enabled;
                    if (r.shadowCastingMode != _ref.shadowCastingMode) r.shadowCastingMode = _ref.shadowCastingMode;
                    if (r.gameObject.layer != _ref.gameObject.layer) r.gameObject.layer = _ref.gameObject.layer;
                }
        }

        void BuildWire()
        {
            var root = ModelRoot();
            float k = root != null && transform.lossyScale.y > 1e-5f ? root.lossyScale.y / transform.lossyScale.y : 1f;
            int layer = _ref != null ? _ref.gameObject.layer : gameObject.layer;
            _wire = IsRemote
                ? WireModel.CreateAntenna(transform, layer, RemoteWireLength, 0.018f)
                : WireModel.CreateAntenna(transform, layer, CarWireLength, 0.006f);
            _wire.localScale = Vector3.one * k;
            if (!IsRemote) _wire.localRotation = Quaternion.Euler(-6f, 0f, 0f); // raked back a little, like the whip in the wind
            _wireR = _wire.GetComponentsInChildren<Renderer>(true);
        }
    }

    /// <summary>
    /// Finds every RC antenna under a root (a player with the transmitter / car in the hands, a car, a dropped item) -
    /// rescanned a few times per second, since hand models come and go - and applies one look to all of them.
    /// </summary>
    public sealed class RcWireTracker
    {
        static readonly List<Transform> Buf = new();
        readonly List<RcWireMark> _marks = new();
        Transform _root;
        float _next;

        public RcWireTracker(Transform root) => _root = root;

        public void Retarget(Transform root)
        {
            if (root == _root) return;
            _root = root;
            _marks.Clear();
            _next = 0f;
        }

        public void Rescan() => _next = 0f;

        /// <summary><paramref name="remote"/> + <paramref name="lift"/> for transmitters, <paramref name="carWire"/> for cars.</summary>
        public void Apply(RcAntennaLook remote, float lift, bool carWire)
        {
            if (_root == null) return;
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.3f;
                _marks.Clear();
                Buf.Clear();
                _root.GetComponentsInChildren(true, Buf);
                foreach (var t in Buf)
                {
                    if (t.name != "Antenna") continue;
                    var m = RcWireMark.For(t);
                    if (m != null) _marks.Add(m);
                }
                Buf.Clear();
            }
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                if (m == null) { _marks.RemoveAt(i); continue; }
                if (m.IsRemote) m.Apply(remote, lift);
                else m.Apply(carWire ? RcAntennaLook.Wire : RcAntennaLook.Stock, 0f);
            }
        }
    }
}
