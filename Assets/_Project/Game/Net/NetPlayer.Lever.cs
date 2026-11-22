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
                // A bit behind the eyes so the bar and both fists fit in the view.
                bob.ExtraPosition = new Vector3(local.x, 0f, local.z - 0.14f) * body;
                float jolt = t > LeverTimeline.Latch ? Mathf.Exp(-(t - LeverTimeline.Latch) * 8f) : 0f;
                float strain = LeverTimeline.S(t, 0.55f, 0.9f) * (1f - LeverTimeline.S(t, LeverTimeline.Unstick, 1.2f));
                bob.ExtraEuler = new Vector3(5f * jolt + Random.Range(-1f, 1f) * (2.2f * jolt + 0.5f * strain), Random.Range(-1f, 1f) * 1.4f * jolt, Random.Range(-1f, 1f) * 1.8f * jolt);

                // Look at the bar while holding it, then back to level as he stands up.
                Vector3 to = bar - eyeW;
                float want = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                want = Mathf.Lerp(0f, want, Mathf.Max(hands, body * 0.5f));
                float pitch = Mathf.LerpAngle(Motor.LookPitch, want, 1f - Mathf.Exp(-9f * Time.deltaTime));
                Motor.SetLook(lever.StandYaw, pitch);
            }
            if (fpv != null && Camera != null)
            {
                // Hands come up from below the view to the bar and go back down after letting go.
                var cam = Camera.transform;
                Vector3 restR = cam.TransformPoint(new Vector3(0.28f, -0.55f, 0.3f)), restL = cam.TransformPoint(new Vector3(-0.28f, -0.55f, 0.3f));
                bool on = t < LeverTimeline.Release + 0.4f;
                fpv.SetOverrideHands(on, Vector3.Lerp(restR, gr, hands), Quaternion.LookRotation(lever.transform.right), Vector3.Lerp(restL, gl, hands), HeldItem.None);
            }
        }
    }
}
