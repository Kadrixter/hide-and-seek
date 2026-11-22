using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person toy slime blaster: raised from below when selected, held in both hands right of centre and aimed at
    /// the crosshair; a shot kicks it up and the left hand racks the pump. The tank shows the goo left.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        bool toyWanted, toyHands;
        float toyRaise, toyFpKick, toyFpLevel = 1f;
        ToyGunModel.Parts toyFp;

        /// <summary>Gun selected in the hotbar (and usable) + how full the tank is.</summary>
        public void SetToyGun(bool on, float level)
        {
            toyWanted = on;
            toyFpLevel = level;
        }

        public void PlayToyShot() => toyFpKick = 1f;

        /// <summary>Muzzle of the view-model gun while it is shown.</summary>
        public Transform ToyMuzzle => toyHands && toyFp != null && visible ? toyFp.Muzzle : null;

        bool ToyGunHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            if (!toyWanted && toyRaise <= 0.001f) return false;
            if (show != HeldItem.None) return false;
            float lower = 1f - ActionTimeline.SmoothStep01(toyRaise);
            float k = toyFpKick * toyFpKick * (3f - 2f * toyFpKick);
            Vector3 pos = new Vector3(0.27f, -0.37f, 0.74f) + new Vector3(0f, 0.02f * k - 0.35f * lower, -0.06f * k);
            propRot = Quaternion.LookRotation(AimPoint - (pos + Vector3.up * 0.15f), Vector3.up) * Quaternion.Euler(-16f * k + 30f * lower, 0f, 3f * k);
            rightT = pos;
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.5f));
            float pump = toyFpKick > 0f ? Mathf.Sin(Mathf.Clamp01(1f - toyFpKick) * Mathf.PI) : 0f;
            leftT = HandPos(shR, elR) + propRot * (new Vector3(0f, 0.04f, 0.27f - 0.07f * pump) * 0.72f);
            wantLeft = 1f - lower;
            toyHands = true;
            return true;
        }

        void UpdateToyGunFp(float dt)
        {
            toyRaise = Mathf.MoveTowards(toyRaise, toyWanted ? 1f : 0f, dt / 0.22f);
            toyFpKick = Mathf.MoveTowards(toyFpKick, 0f, dt * 3.2f);
            if (toyHands && toyFp == null)
            {
                toyFp = ToyGunModel.Create(socketR, Layer, false);
                toyFp.Root.localScale = Vector3.one * 0.72f;
            }
            if (toyFp == null) return;
            if (toyFp.Root.gameObject.activeSelf != toyHands) toyFp.Root.gameObject.SetActive(toyHands);
            if (toyHands) ToyGunModel.SetLevel(toyFp, toyFpLevel);
        }
    }
}
