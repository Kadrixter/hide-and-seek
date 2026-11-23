using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person side of the boombox on the shoulder. The radio itself is the real one (the game shows it to the
    /// carrier too: it sits beside the head, so only its end shows at the right edge of the view); the view-model
    /// hands follow it on screen: both hands reach for it while it is lifted / put down, the right forearm rises
    /// along the right edge to the handle while it is carried (a slow breathing sway, a nod on the bass).
    /// </summary>
    public partial class FirstPersonViewModel
    {
        bool vmrOn;
        Vector3 vmrFist, vmrLeft;
        float vmrLeftW, vmrBeat, vmrRide;

        /// <summary>
        /// Every frame from the game (local carrier): <paramref name="on"/> = the radio is in the hands / on the shoulder,
        /// <paramref name="fistWorld"/> / <paramref name="leftWorld"/> = where the hands hold it (world),
        /// <paramref name="leftW"/> = the left hand helps (lift / put), <paramref name="ride"/> = 1 when it rides on the shoulder.
        /// </summary>
        public void SetShoulderRadio(bool on, Vector3 fistWorld, Vector3 leftWorld, float leftW, float ride, float beat)
        {
            vmrOn = on;
            vmrFist = fistWorld;
            vmrLeft = leftWorld;
            vmrLeftW = leftW;
            vmrRide = ride;
            vmrBeat = beat;
        }

        bool ShoulderRadioHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            if (!vmrOn || rig == null) return false;
            // Hands on the radio's spot on screen (view-model FOV corrected); on the shoulder the fist is out of the
            // view at the top right and the forearm rises along the edge.
            Vector3 onScreen = RigOnScreen(vmrFist, 0.8f) + new Vector3(0.04f, -0.05f, 0f);
            Vector3 ride = new Vector3(0.45f, -0.2f + 0.008f * Mathf.Sin(time * 1.7f) - 0.012f * vmrBeat, 0.4f);
            rightT = Vector3.Lerp(onScreen, ride, vmrRide);
            leftT = RigOnScreen(vmrLeft, 0.8f) + new Vector3(-0.04f, -0.05f, 0f);
            wantLeft = vmrLeftW;
            show = HeldItem.None;
            SolveArm(shR, elR, ShoulderR, rightT, Vector3.Lerp(new Vector3(1f, -0.8f, -0.3f), new Vector3(1f, -1.3f, 0.2f), vmrRide));
            return true;
        }
    }
}
