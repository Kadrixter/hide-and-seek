using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local input of the fence wire (MatchManager.RcWire): the E prompt at a wire-mesh fence / cage ("przytrzymaj -
    /// urwij kawałek drutu", hold E for 4 s - the ring fills, letting go stops it, RcWireDriver) and U with the RC
    /// transmitter in the hands and a piece of wire in the inventory = swap the antenna. U is free everywhere else
    /// (G is the medkit on yourself, E / R / B / X belong to other things near the transmitter). While tearing wire or
    /// swapping the antenna the hands own the input.
    /// </summary>
    public partial class LocalPlayerController
    {
        /// <summary>Tests: press U / E without a keyboard.</summary>
        public static bool TestKeyU, TestPressE;

        private short _wireSpot = -1;
        private float _wireAlong;
        private sbyte _wireSide = 1;

        public const string WirePromptText = "E  przytrzymaj - urwij kawałek drutu";
        public const string AntennaPromptText = "U  wymień antenę pilota na drut";

        /// <summary>0..1 of the wire being torn off by this player (0 = none).</summary>
        public float WireProgress
        {
            get
            {
                var mm = MatchManager.Instance;
                if (_p == null || mm == null || _p.WirePull.Value.Spot < 0) return 0f;
                return Mathf.Clamp01((float)(mm.Now - _p.WirePull.Value.Start) / WirePullTimeline.Duration);
            }
        }

        public bool WirePulling => _p != null && _p.WirePull.Value.Spot >= 0;

        /// <summary>Before the other actions: true while the wire pull / antenna swap owns the hands; U starts the swap.</summary>
        private bool WireTick(Keyboard kb)
        {
            var mm = MatchManager.Instance;
            if (mm == null || _p == null) return false;
            var w = _p.WirePull.Value;
            var spots = WireSpots.All;
            if (w.Spot >= 0 && w.Spot < spots.Count)
            {
                Prompt = PromptKind.WirePull;
                PromptWorld = spots[w.Spot].Grip(w.Along, w.Side);
                PromptText = WirePromptText;
                return true;
            }
            if (_p.AntennaSwapAt.Value >= 0d) return true;
            bool u = TestKeyU || (kb != null && kb.uKey.wasPressedThisFrame);
            TestKeyU = false;
            if (u && !MenuOpen && mm.AntennaSwapBlock(_p) == null)
            {
                _p.RequestAntennaSwapRpc();
                return true;
            }
            return false;
        }

        /// <summary>E prompt at the nearest free wire-mesh panel in front of the player (hiders, hands free).</summary>
        private bool TryWirePrompt(Vector3 eye, Vector3 fwd, MatchPhase phase, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = float.MaxValue;
            var mm = MatchManager.Instance;
            if (mm == null || (phase != MatchPhase.Hiding && phase != MatchPhase.Seeking) || mm.RaceMode) return false;
            if (!_p.IsAliveHider || _p.Busy.Value || _p.Seat.Value >= 0 || _p.RcAnimating || _p.AntennaSwapAt.Value >= 0d || !_p.Motor.IsGrounded) return false;
            var spots = WireSpots.All;
            if (spots.Count == 0) return false;
            var taken = mm.WireTaken.Value;
            Vector3 feet = transform.position;
            Vector3 f = new Vector3(fwd.x, 0f, fwd.z);
            if (f.sqrMagnitude < 1e-4f) f = transform.forward;
            f.Normalize();
            int best = -1;
            for (int i = 0; i < spots.Count; i++)
            {
                if (taken.Get(i)) continue;
                var s = spots[i];
                if (Mathf.Abs(feet.y - s.Floor) > 0.6f) continue;
                Vector3 rel = feet - s.Center;
                rel.y = 0f;
                float along = Vector3.Dot(rel, s.Along);
                if (Mathf.Abs(along) > s.Half + 0.3f) continue;
                float perp = Vector3.Dot(rel, s.Normal);
                if (Mathf.Abs(perp) > 1.15f || Mathf.Abs(perp) < 0.12f) continue;
                int side = perp >= 0f ? 1 : -1;
                if (Vector3.Angle(f, -s.Normal * side) > 65f) continue;
                float a = s.ClampAlong(along);
                Vector3 grip = s.Grip(a, side);
                float d = Vector3.Distance(eye, grip);
                if (d >= dist) continue;
                dist = d;
                best = i;
                pos = grip;
                _wireAlong = a;
                _wireSide = (sbyte)side;
            }
            if (best < 0) return false;
            _wireSpot = (short)best;
            text = Inventory.HasRoomFor(_p, HotbarItem.Wire) ? WirePromptText
                : _p.WireCount.Value > 0 ? "E  (więcej drutu nie uniesiesz)" : "E  (ekwipunek pełny - Q wyrzuć coś)";
            dist += 0.25f; // anything else at the fence (a dropped item, a rod) comes first
            return true;
        }

        /// <summary>Low-priority prompt over the transmitter in the hands: U swaps the antenna (only with a wire somewhere in the inventory).</summary>
        private bool TryAntennaPrompt(out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = 40f;
            var mm = MatchManager.Instance;
            var cam = _p.Camera != null ? _p.Camera.transform : null;
            if (mm == null || cam == null || mm.AntennaSwapBlock(_p) != null) return false;
            pos = cam.position + cam.forward * 0.8f - cam.up * 0.2f - cam.right * 0.12f;
            text = AntennaPromptText;
            return true;
        }

        private void WirePromptE()
        {
            if (_wireSpot < 0) return;
            if (!Inventory.HasRoomFor(_p, HotbarItem.Wire))
            {
                GameHud.Toast(_p.WireCount.Value > 0 ? "Więcej drutu nie uniesiesz." : Inventory.FullText, 2.5f);
                return;
            }
            _p.RequestWirePullRpc(_wireSpot, _wireAlong, _wireSide);
        }
    }
}
