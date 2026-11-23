using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The knocked-out seeker's own view: the eye follows the head of the third-person body (thrown back, the stagger,
    /// down onto the knees, the side, the back, up again over all fours, the head shake), the hands and the flashlight
    /// are put away, the screen goes black fast and comes back slowly and blurred (<see cref="StunScreen"/>; the sound
    /// keeps going, a little muffled). The mouse does nothing meanwhile.
    /// </summary>
    public partial class NetPlayer
    {
        private bool _stunView, _stunFlashWasOn;

        /// <summary>The first-person hands are hidden (knocked out).</summary>
        public bool StunHidesVm => _stunView;

        private void UpdateStunView(float st)
        {
            StunScreen.Show(st, Camera);
            if (st < 0f)
            {
                if (_stunView) EndStunView();
                return;
            }
            if (!_stunView) BeginStunView();
            if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
            if (_bob == null) return;
            Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, 0f, Time.deltaTime * 120f));
            StunCamera(st, Motor.EyeHeight, out var pos, out var rot);
            _bob.EyeOverride = pos.y;
            _bob.EyeOverrideWeight = 1f;
            _bob.ExtraPosition = new Vector3(pos.x, 0f, pos.z);
            _bob.ExtraEuler = rot.eulerAngles;
        }

        private void BeginStunView()
        {
            _stunView = true;
            _stunFlashWasOn = Flashlight != null && Flashlight.IsOn;
            if (_stunFlashWasOn) Flashlight.SetOn(false, false);
        }

        private void EndStunView()
        {
            _stunView = false;
            if (_bob != null)
            {
                _bob.ExtraEuler = Vector3.zero;
                _bob.ExtraPosition = Vector3.zero;
                _bob.EyeOverrideWeight = 0f;
            }
            if (_stunFlashWasOn && Flashlight != null && HasFlashlight && Life.Value == LifeState.Alive) Flashlight.SetOn(true, false);
            _stunFlashWasOn = false;
        }

        /// <summary>Eye (root space, pos.y = height) during the knock-out, keyed to <see cref="BlockyCharacterActions.SetStun"/>.</summary>
        private static void StunCamera(float t, float eye, out Vector3 pos, out Quaternion rot)
        {
            Quaternion lie = Quaternion.LookRotation(new Vector3(0f, 1f, -0.16f), new Vector3(0f, 0.16f, 1f));
            Quaternion side = Quaternion.LookRotation(new Vector3(1f, -0.12f, 0.1f), new Vector3(0.12f, 0.1f, 1f));
            Quaternion fours = Quaternion.LookRotation(new Vector3(0f, -0.85f, 0.5f), new Vector3(0f, 0.5f, 0.85f));
            Vector3 lyingPos = new Vector3(0f, 0.28f, 0.2f), sidePos = new Vector3(0.08f, 0.26f, 0.24f);
            Vector3 foursPos = new Vector3(0f, 0.62f, 0.36f), kneelPos = new Vector3(0.02f, 1.12f, 0.38f), bentPos = new Vector3(0f, 1.2f, 0.4f);
            Quaternion out1 = lie * Quaternion.Euler(0f, 40f, 0f);

            var keys = new (float t, Vector3 p, Quaternion r)[]
            {
                (0f, new Vector3(0f, eye, 0f), Quaternion.identity),
                (StunTimeline.Snap, new Vector3(0f, eye - 0.03f, -0.08f), Quaternion.Euler(-24f, -6f, 7f)),
                (StunTimeline.Reel, new Vector3(0.02f, eye - 0.08f, -0.26f), Quaternion.Euler(14f, 10f, -13f)),
                (StunTimeline.Sway, new Vector3(0.1f, eye - 0.13f, -0.22f), Quaternion.Euler(26f, -10f, 19f)),
                (StunTimeline.Knees, new Vector3(0.04f, 1.0f, -0.14f), Quaternion.Euler(42f, 10f, 22f)),
                (StunTimeline.Topple, sidePos, side),
                (StunTimeline.Flat, lyingPos, out1),
                (StunTimeline.Stir, lyingPos, out1),
                (StunTimeline.RollSide, lyingPos + new Vector3(0f, 0.04f, 0f), lie * Quaternion.Euler(18f, 14f, 0f)),
                (StunTimeline.AllFours, sidePos, side),
                (StunTimeline.HalfKneel, foursPos, fours),
                (StunTimeline.Bent, kneelPos, Quaternion.Euler(30f, 2f, 3f)),
                (StunTimeline.Bent + 0.25f, bentPos, Quaternion.Euler(32f, 0f, 0f)),
                (StunTimeline.Duration, new Vector3(0f, eye, 0f), Quaternion.identity),
            };
            pos = keys[keys.Length - 1].p;
            rot = keys[keys.Length - 1].r;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (t >= keys[i + 1].t) continue;
                float x = ActionTimeline.Remap01(t, keys[i].t, keys[i + 1].t);
                x = i == 3 || i == 4 ? ActionTimeline.EaseIn(x) : ActionTimeline.SmoothStep01(x);
                pos = Vector3.Lerp(keys[i].p, keys[i + 1].p, x);
                rot = Quaternion.Slerp(keys[i].r, keys[i + 1].r, x);
                break;
            }
            // Dazed: a slow wobble while on the feet and while getting up; the head shake at the end.
            float daze = StunTimeline.Seg(t, 0.15f, 0.5f) * (1f - StunTimeline.Seg(t, StunTimeline.Sway, StunTimeline.Knees))
                         + StunTimeline.Seg(t, StunTimeline.Stir, StunTimeline.RollSide) * (1f - StunTimeline.Seg(t, StunTimeline.Bent, StunTimeline.Duration));
            rot = rot * Quaternion.Euler(3f * Mathf.Sin(t * 2.1f) * daze, 4f * Mathf.Sin(t * 1.5f + 1f) * daze, 5f * Mathf.Sin(t * 1.8f + 2f) * daze);
            float sh = ActionTimeline.Remap01(t, StunTimeline.ShakeStart, StunTimeline.Duration);
            if (sh > 0f && sh < 1f)
            {
                float env = Mathf.Sin(sh * Mathf.PI) * (1f - 0.4f * sh);
                rot = rot * Quaternion.Euler(0f, 22f * Mathf.Sin(sh * Mathf.PI * 2f * 2.6f) * env, 0f);
            }
        }
    }
}
