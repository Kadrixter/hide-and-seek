using System;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Every RC car instance (shelf / floor car, the car in the hand in first and third person, the dropped item) carries
    /// both bodies: the rally car and the Yaris easter egg (<see cref="YarisModel"/>). This component shows the one the
    /// round wants (<see cref="YarisWanted"/>, set by the game from the synced round state) and points the shared
    /// <see cref="RcCarParts"/> at its wheels / steering / antenna / body, so the car code animates whichever is shown.
    /// Once a wheel has been knocked off (crushed / shot wreck) the body no longer changes.
    /// </summary>
    public class RcCarSkin : MonoBehaviour
    {
        /// <summary>True when this round's RC cars are Yarises (null = never).</summary>
        public static Func<bool> YarisWanted;

        public RcCarParts Shared { get; private set; }
        public YarisModel.Parts Yaris { get; private set; }
        public bool IsYaris { get; private set; }

        private RcCarParts _rally;
        private GameObject _rallyGo;
        private bool _locked;

        /// <summary>Moves the rally parts under a "Rally" node, adds the hidden Yaris next to it, applies the wanted body.</summary>
        public static void Attach(RcCarParts parts, int layer)
        {
            var root = parts.Root;
            var rally = new GameObject("Rally").transform;
            rally.gameObject.layer = layer;
            rally.SetParent(root, false);
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                if (c != rally) c.SetParent(rally, false);
            }
            var skin = root.gameObject.AddComponent<RcCarSkin>();
            skin.Shared = parts;
            skin._rallyGo = rally.gameObject;
            skin._rally = new RcCarParts
            {
                Root = parts.Root, Body = parts.Body, Antenna = parts.Antenna, SteeringWheel = parts.SteeringWheel,
                Wheels = (Transform[])parts.Wheels.Clone(), Steer = (Transform[])parts.Steer.Clone(),
            };
            skin.Yaris = YarisModel.Create(root, layer);
            skin.Yaris.Root.gameObject.SetActive(false);
            skin.Apply(Wanted, true);
        }

        /// <summary>Never switches again (e.g. race cars keep the rally body).</summary>
        public void Lock() => _locked = true;

        private static bool Wanted
        {
            get
            {
                try { return YarisWanted != null && YarisWanted(); }
                catch { return false; }
            }
        }

        private void LateUpdate()
        {
            if (_locked) return;
            bool want = Wanted;
            if (want != IsYaris) Apply(want, false);
        }

        private void Apply(bool yaris, bool force)
        {
            if (!force)
                foreach (var w in Shared.Wheels)
                    if (w == null) { _locked = true; return; } // wreck: keep what was there
            IsYaris = yaris;
            _rallyGo.SetActive(!yaris);
            Yaris.Root.gameObject.SetActive(yaris);
            Shared.Body = yaris ? Yaris.Body : _rally.Body;
            Shared.SteeringWheel = yaris ? null : _rally.SteeringWheel;
            Shared.Antenna = yaris ? Yaris.Antenna : _rally.Antenna;
            for (int i = 0; i < 4; i++) Shared.Wheels[i] = yaris ? Yaris.Wheels[i] : _rally.Wheels[i];
            for (int i = 0; i < 2; i++) Shared.Steer[i] = yaris ? Yaris.Steer[i] : _rally.Steer[i];
        }
    }
}
