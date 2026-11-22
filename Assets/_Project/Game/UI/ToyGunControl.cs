using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Local input of the toy slime blaster (hider): 5 takes it into the hands / puts it away (temporary key until the
    /// new inventory gives it a slot; 1-4 also put it away), LMB fires while it is out. While the gun is out the hotbar
    /// does not use the left mouse button (<see cref="Out"/>).
    /// Inventory hook: set <see cref="Out"/> from the selected slot, call <see cref="Use"/> on a click, and
    /// <c>NetPlayer.RequestDropToyGunRpc()</c> to drop it.
    /// </summary>
    [DefaultExecutionOrder(-8)] // before HotbarHud (-5), which reads Out
    public class ToyGunControl : MonoBehaviour
    {
        /// <summary>The local hider holds the gun (LMB belongs to it).</summary>
        public static bool Out { get; set; }

        // Test hooks.
        public static bool TestShoot, TestToggle;

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
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool menu = LocalPlayerController.MenuOpen;
            if (usable && kb != null && !menu)
            {
                if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame || TestToggle)
                {
                    Out = !Out;
                    HideAndSeek.Audio.GameAudio.Play2D(HideAndSeek.Audio.GameSfx.UiClick, 0.35f);
                }
                for (int i = 0; i < 4; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame) Out = false;
            }
            else if (!usable && TestToggle == false && kb != null && !menu && Hotbar.Active(_p) && _p.IsAliveHider &&
                     (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) && !_p.HasToyGun)
                GameHud.Toast("Nie masz pistoleciku z glutem (leży gdzieś na parkingu).", 2.5f);
            TestToggle = false;
            if (!usable) Out = false;
            if (_p.ToyGunOut.Value != Out) _p.ToyGunOut.Value = Out;

            bool locked = _p.InputReader == null || _p.InputReader.CursorLocked;
            bool click = (mouse != null && mouse.leftButton.wasPressedThisFrame && locked && !menu && _p.InputReader != null && _p.InputReader.InputEnabled) || TestShoot;
            TestShoot = false;
            if (Out && click) Use(_p);
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
