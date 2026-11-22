using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Toy slime blaster of the local hider. <see cref="Out"/> (gun in the hands) follows the selected hotbar slot
    /// (<see cref="Hotbar"/>); the left mouse button on that slot calls <see cref="Use"/> (ItemDefs), Q drops it like
    /// any other item (MatchManager.Drops, the shots left travel with it). This component only syncs the hands state.
    /// </summary>
    [DefaultExecutionOrder(-8)] // before HotbarHud (-5), which reads Out
    public class ToyGunControl : MonoBehaviour
    {
        /// <summary>The local hider holds the gun (LMB belongs to it).</summary>
        public static bool Out { get; set; }

        // Test hook (fires like a click while the gun is out).
        public static bool TestShoot;

        private NetPlayer _p;
        private static float _nextShot;

        private void Awake() => _p = GetComponent<NetPlayer>();

        private void OnDestroy()
        {
            if (_p != null && _p.IsOwner) Out = false;
        }

        private void Update()
        {
            if (_p == null || !_p.IsOwner) return;
            bool usable = Hotbar.Active(_p) && _p.IsAliveHider && _p.HasToyGun && !_p.Busy.Value && !_p.RcControlling && !_p.RcAnimating
                          && _p.Seat.Value < 0;
            // Out = the toy gun's slot is selected (set by Hotbar.Tick, which runs later this frame and next).
            Out = usable && Hotbar.ItemAt(_p, Hotbar.Selected(_p)) == HotbarItem.ToyGun;
            if (_p.ToyGunOut.Value != Out) _p.ToyGunOut.Value = Out;
            bool shoot = TestShoot;
            TestShoot = false;
            if (Out && shoot) Use(_p);
        }

        /// <summary>Fires one goo blob from the camera towards the crosshair (slightly lofted).</summary>
        public static void Use(NetPlayer p)
        {
            if (p == null || !p.HasToyGun || Time.time < _nextShot || p.Camera == null) return;
            _nextShot = Time.time + ToyTuning.FireInterval;
            var cam = p.Camera.transform;
            Vector3 dir = (cam.forward + Vector3.up * 0.06f).normalized;
            p.RequestToyShotRpc(cam.position + cam.forward * 0.35f, dir);
        }
    }
}
