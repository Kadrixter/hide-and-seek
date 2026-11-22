using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Toy slime blaster in third person: held in both hands at chest height (right fist on the grip, left hand under
    /// the pump), following the look pitch; each shot kicks the gun up and the left hand racks the pump. Also the
    /// "covered in goo" layer of a hit seeker: hunched, both hands flicking the slime off.
    /// The prop is not one of the HeldItem props: it lives on the right hand socket next to them.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        bool toyOn;
        float toyW, toyKick, toyLevel = 1f, gooW, gooTarget;
        ToyGunModel.Parts toyProp;
        bool toyPropFp, toyPropShown;

        /// <summary>Gun in the hands (selected in the hotbar) and how full its tank is (0..1).</summary>
        public void SetToyGun(bool on, float level)
        {
            toyOn = on;
            toyLevel = level;
        }

        /// <summary>One shot: recoil + pump.</summary>
        public void PlayToyShot() => toyKick = 1f;

        /// <summary>Muzzle of the gun while it is shown (projectile / splash origin), else null.</summary>
        public Transform ToyMuzzle => toyPropShown && toyProp != null ? toyProp.Muzzle : null;

        /// <summary>0..1 how much the character struggles with goo on the body.</summary>
        public void SetGooed(float amount) => gooTarget = Mathf.Clamp01(amount);

        void ApplyToyGun(ref CharacterPose p, float dt, bool standing, float lookPitch)
        {
            bool can = toyOn && standing && VisualLifeState == LifeState.Alive && action == ActionKind.None && !pathActive &&
                       shownItem == HeldItem.None && sitW < 0.01f && proneW < 0.3f && rcW < 0.01f && injectW < 0.01f &&
                       medW < 0.01f && !vendingOn && shockT < 0f && gunT < 0f && flashT < 0f && carryW < 0.01f;
            toyW = Mathf.MoveTowards(toyW, can ? 1f : 0f, dt / 0.2f);
            toyKick = Mathf.MoveTowards(toyKick, 0f, dt * 3.2f);
            gooW = Mathf.MoveTowards(gooW, gooTarget, dt / 0.3f);

            if (toyW > 0.001f)
            {
                float k = toyKick * toyKick * (3f - 2f * toyKick);
                float look = Mathf.Clamp(lookPitch, -55f, 55f) * 0.75f;
                Quaternion rl = Quaternion.Euler(-look, 0f, 0f);
                Vector3 gPos = p.TorsoPoint(new Vector3(0f, 0.5f, 0f) + rl * new Vector3(0.1f, -0.18f, 0.42f));
                Quaternion gRot = p.TorsoRoot * rl * Quaternion.Euler(10f, -4f, 0f);
                gPos += p.TorsoRoot * new Vector3(0f, 0.04f * k, -0.07f * k);
                gRot = gRot * Quaternion.Euler(-20f * k, 0f, 2f * k);

                var q = p;
                q.SolveArm(0, gPos, TorsoDir(q, 1f, -1f, -0.4f));
                q.PropR = Quaternion.Inverse(q.ForearmRoot(0)) * gRot;
                // Left hand under the pump; racks it back right after a shot.
                float pump = Mathf.Sin(Mathf.Clamp01(1f - toyKick) * Mathf.PI) * (toyKick > 0f ? 1f : 0f);
                Vector3 left = q.HandPos(0) + gRot * new Vector3(0f, 0.04f, 0.27f - 0.07f * pump);
                q.SolveArm(1, left, TorsoDir(q, -1f, -1f, -0.3f));
                float w = ActionTimeline.SmoothStep01(toyW);
                CharacterPose.LerpArm(ref p, q, 0, w);
                CharacterPose.LerpArm(ref p, q, 1, w);
                if (w > 0.5f) p.PropR = q.PropR;
            }

            if (gooW > 0.001f && standing && VisualLifeState == LifeState.Alive && action == ActionKind.None && !pathActive)
            {
                // Hunched, flicking the goo off both hands (the right one keeps the weapon when one is held).
                float w = ActionTimeline.SmoothStep01(gooW);
                p.Torso = Quaternion.Slerp(p.Torso, p.Torso * Quaternion.Euler(12f + 3f * Mathf.Sin(time * 9f), 6f * Mathf.Sin(time * 3.1f), 0f), w);
                var g = p;
                float sh = Mathf.Sin(time * 17f), sh2 = Mathf.Sin(time * 15f + 1.3f);
                g.SolveArm(1, g.TorsoPoint(new Vector3(-0.42f, 0.18f + 0.06f * sh, 0.12f + 0.05f * sh2)), TorsoDir(g, -1f, -1f, 0.2f));
                CharacterPose.LerpArm(ref p, g, 1, w);
                if (shownItem == HeldItem.None && toyW < 0.01f && carryW < 0.01f)
                {
                    g.SolveArm(0, g.TorsoPoint(new Vector3(0.42f, 0.2f + 0.06f * sh2, 0.1f + 0.05f * sh)), TorsoDir(g, 1f, -1f, 0.2f));
                    CharacterPose.LerpArm(ref p, g, 0, w);
                }
            }
        }

        void UpdateToyGunProp()
        {
            bool show = toyW > 0.5f;
            if (show && toyProp == null)
            {
                toyProp = ToyGunModel.Create(socketR, gameObject.layer, true);
                toyPropFp = !character.IsFirstPerson;
            }
            if (toyProp == null) return;
            if (toyProp.Root.gameObject.activeSelf != show) toyProp.Root.gameObject.SetActive(show);
            toyPropShown = show;
            if (!show) return;
            bool fp = character.IsFirstPerson;
            if (fp != toyPropFp || toyProp.Root.gameObject.layer != gameObject.layer)
            {
                toyPropFp = fp;
                foreach (var r in toyProp.Renderers)
                {
                    if (r == null) continue;
                    r.gameObject.layer = gameObject.layer;
                    r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                }
                toyProp.Root.gameObject.layer = gameObject.layer;
            }
            ToyGunModel.SetLevel(toyProp, toyLevel);
        }
    }
}
