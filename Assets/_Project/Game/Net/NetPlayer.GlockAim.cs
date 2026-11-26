using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Glock aiming (hold RMB with the Glock out): small spread instead of the hip spread, slight zoom, gun near the
    /// middle of the screen (first person), both arms straight out for everybody else (synced flag, owner-written).
    /// Every own shot kicks the camera up and lets it settle back (local only; the body recoil comes with the shot RPC).
    /// RMB means aiming only while the Glock is held and nothing else owns the mouse (bricks, spectating, menus).
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<bool> GlockAiming = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Test hook (-testaim): aim as if RMB was held.</summary>
        public static bool TestAim;

        private float _aimW, _camKick, _camKickVel, _kickYaw;
        private int _lastAmmo = -1;
        private HeadBob _aimBob;

        /// <summary>Smoothed aim weight (0 = hip, 1 = aimed).</summary>
        public float GlockAimWeight => _aimW;

        /// <summary>Spread (degrees) of the last own shot, for logs.</summary>
        public float LastShotSpread { get; private set; }

        private bool WantsGlockAim()
        {
            if (!IsSeeker || !HasGlock.Value || Held.Value != HeldItem.Glock || Life.Value != LifeState.Alive || Busy.Value || HoldsPaMic) return false;
            var mm = MatchManager.Instance;
            if (mm == null || mm.Phase.Value != MatchPhase.Seeking || LocalPlayerController.MenuOpen || KillCam.Active) return false;
            if (mm.Now < BlindUntil.Value) return false;
            if (FoamBlocksAttack) return false; // extinguisher foam (NetPlayer.Extinguisher)
            if (Actions is BlockyCharacterActions b && b.IsBusy) return false; // reloading
            if (TestAim) return true;
            var mouse = Mouse.current;
            if (InputReader != null && InputReader.InputEnabled && PadInput.Held(PadAction.Aim)) return true; // pad: L2
            return mouse != null && InputReader != null && InputReader.InputEnabled && InputReader.CursorLocked && mouse.rightButton.isPressed;
        }

        private void UpdateGlockAim(float dt)
        {
            if (IsOwner)
            {
                bool want = WantsGlockAim();
                if (GlockAiming.Value != want) GlockAiming.Value = want;
            }
            bool aim = GlockAiming.Value && Held.Value == HeldItem.Glock && Life.Value == LifeState.Alive;
            _aimW = Mathf.MoveTowards(_aimW, aim ? 1f : 0f, dt / 0.16f);
            (Actions as BlockyCharacterActions)?.SetGlockAim(_aimW);
            if (!IsOwner) return;
            (ViewModel as FirstPersonViewModel)?.SetGlockAim(_aimW);

            // Own shot: the server takes one round off the magazine (reloads only ever add).
            int ammo = Ammo.Value;
            if (_lastAmmo >= 0 && ammo == _lastAmmo - 1 && Held.Value == HeldItem.Glock)
            {
                _camKickVel += Mathf.Lerp(30f, 20f, _aimW);
                _kickYaw = Random.Range(-1f, 1f);
            }
            _lastAmmo = ammo;
            StepKickSpring(dt);

            if (_aimBob == null) _aimBob = GetComponentInChildren<HeadBob>();
            if (_aimBob != null)
            {
                _aimBob.FovScale = Mathf.Lerp(1f, Tuning.GlockAimFovScale, ActionTimeline.SmoothStep01(_aimW));
                _aimBob.KickEuler = new Vector3(-4.6f * _camKick, 1.1f * _kickYaw * _camKick, 0.7f * _kickYaw * _camKick);
            }
        }

        /// <summary>Quick kick up, a slightly slower settle back to where the player aimed (no drift).</summary>
        private void StepKickSpring(float dt)
        {
            const float k = 260f, zeta = 0.78f;
            float c = 2f * zeta * Mathf.Sqrt(k);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                _camKickVel += (-k * _camKick - c * _camKickVel) * h;
                _camKick += _camKickVel * h;
            }
        }

        /// <summary>
        /// Direction of the next own bullet: the camera's forward plus a random offset inside the current spread cone
        /// (large from the hip and while moving, small when aimed).
        /// </summary>
        public Vector3 GlockShotDirection(Transform cam)
        {
            float moving = Motor != null ? Mathf.Clamp01(Motor.HorizontalSpeed / 3f) : 0f;
            float a = ActionTimeline.SmoothStep01(_aimW);
            float deg = Mathf.Lerp(Tuning.GlockHipSpread + Tuning.GlockMoveSpread * moving,
                                   Tuning.GlockAimSpread + Tuning.GlockMoveSpread * 0.25f * moving, a);
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(deg * Mathf.Deg2Rad);
            Vector3 d = (cam.forward + cam.right * r.x + cam.up * r.y).normalized;
            LastShotSpread = Vector3.Angle(d, cam.forward);
            Debug.Log($"[GlockAim] shot aim={_aimW:0.00} moving={moving:0.00} cone={deg:0.00} deg, off-centre={LastShotSpread:0.00} deg");
            return d;
        }
    }
}
