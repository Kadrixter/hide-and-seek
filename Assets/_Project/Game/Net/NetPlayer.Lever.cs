using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Start lever pull on every client: the body follows the red bar (<see cref="BlockyCharacterActions.SetLever"/>);
    /// for the owner the first-person hands hold the bar and the camera rides the head down into the squat,
    /// watching the bar, with a jolt when the blades hit the jaws.
    /// </summary>
    public partial class NetPlayer
    {
        private Coroutine _lever;

        public void PlayLeverPull()
        {
            if (_lever != null) StopCoroutine(_lever);
            _lever = StartCoroutine(LeverSequence());
        }

        private IEnumerator LeverSequence()
        {
            var lever = LobbyButton.Instance;
            if (lever == null) yield break;
            var bca = Actions as BlockyCharacterActions;
            var fpv = IsOwner ? ViewModel as FirstPersonViewModel : null;
            var bob = IsOwner ? GetComponentInChildren<HeadBob>() : null;
            float t = 0f;
            while (t < LeverTimeline.End && lever != null)
            {
                t += Time.deltaTime;
                Vector3 gr = lever.GripWorld(0), gl = lever.GripWorld(1);
                bca?.SetLever(t, gr, gl);
                if (IsOwner) LeverView(t, lever, gr, gl, fpv, bob);
                yield return null;
            }
            bca?.SetLever(-1f, Vector3.zero, Vector3.zero);
            if (IsOwner)
            {
                fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                if (bob != null)
                {
                    bob.ExtraEuler = Vector3.zero;
                    bob.ExtraPosition = Vector3.zero;
                    bob.EyeOverrideWeight = 0f;
                }
            }
            _lever = null;
        }

        private void LeverView(float t, LobbyButton lever, Vector3 gr, Vector3 gl, FirstPersonViewModel fpv, HeadBob bob)
        {
            float body = LeverTimeline.Body(t);
            float hands = LeverTimeline.Hands(t);
            Vector3 bar = 0.5f * (gr + gl);
            if (bob != null && Character != null && Character.Head != null)
            {
                // Eye rides the animated head (squat, lean back), expressed in the camera's parent space.
                var parent = bob.transform.parent;
                Vector3 eyeW = Character.Head.position + transform.rotation * new Vector3(0f, 0.27f, 0.06f);
                Vector3 local = parent != null ? parent.InverseTransformPoint(eyeW) : eyeW - transform.position;
                bob.EyeOverride = local.y;
                bob.EyeOverrideWeight = body;
                // Behind the eyes so the bar and both fists fit in the view.
                bob.ExtraPosition = new Vector3(local.x, 0f, local.z - 0.22f) * body;
                float jolt = t > LeverTimeline.Latch ? Mathf.Exp(-(t - LeverTimeline.Latch) * 8f) : 0f;
                float strain = LeverTimeline.S(t, 0.55f, 0.9f) * (1f - LeverTimeline.S(t, LeverTimeline.Unstick, 1.2f));
                bob.ExtraEuler = new Vector3(5f * jolt + Random.Range(-1f, 1f) * (2.2f * jolt + 0.5f * strain), Random.Range(-1f, 1f) * 1.4f * jolt, Random.Range(-1f, 1f) * 1.8f * jolt);

                // Look a little above the bar while holding it (bar and fists in the lower half of the view, the arms
                // come in from the bottom edge), then back to level as he stands up.
                Vector3 to = bar - (eyeW - transform.forward * 0.22f);
                float want = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg + 24f;
                want = Mathf.Lerp(0f, want, Mathf.Max(hands, body * 0.5f));
                float pitch = Mathf.LerpAngle(Motor.LookPitch, want, 1f - Mathf.Exp(-9f * Time.deltaTime));
                Motor.SetLook(lever.StandYaw, pitch);
            }
            if (fpv != null && Camera != null)
            {
                // Hands come up from below the view to the bar and go back down after letting go. The first-person
                // arms are short and drawn with their own camera, so the fists are put on the line of sight to the
                // grips at arm's length (same spot on screen as the bar), not at the bar itself.
                var cam = Camera.transform;
                Vector3 restR = cam.TransformPoint(new Vector3(0.28f, -0.55f, 0.3f)), restL = cam.TransformPoint(new Vector3(-0.28f, -0.55f, 0.3f));
                bool on = t < LeverTimeline.Release + 0.4f;
                fpv.SetOverrideHands(on, Vector3.Lerp(restR, OnScreen(cam, gr), hands), Quaternion.LookRotation(lever.transform.right),
                    Vector3.Lerp(restL, OnScreen(cam, gl), hands), HeldItem.None);
            }
        }

        /// <summary>World point at arm's length on the view ray to <paramref name="world"/>, corrected for the viewmodel FOV.</summary>
        private Vector3 OnScreen(Transform cam, Vector3 world)
        {
            Vector3 d = cam.InverseTransformPoint(world);
            if (d.z < 0.05f) d.z = 0.05f;
            float k = Mathf.Tan(FirstPersonViewModel.ViewModelFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 s = new Vector3(d.x / d.z * k, d.y / d.z * k, 1f);
            return cam.TransformPoint(s.normalized * 0.74f);
        }
    }
}
