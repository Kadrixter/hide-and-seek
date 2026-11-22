using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Places the peg-leg rods on a <see cref="BlockyCharacter"/>: fitted rods hang from the hip joint of the missing
    /// leg (so they follow the walk / downed poses), during the fitting animation the rod follows the hands and a tape
    /// roll sits in the right fist. Runs after the animator (LateUpdate order 110) and copies the body's layer and
    /// shadow mode (first person: shadows only). Added on demand by <see cref="BlockyCharacterActions"/>.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class RodLegRig : MonoBehaviour
    {
        BlockyCharacter character;
        BlockyCharacterActions actions;
        readonly RodModel.Parts[] rods = new RodModel.Parts[2];
        Transform roll;

        /// <summary>World pose of the rod of leg <paramref name="leg"/> (0 = right) if it is shown.</summary>
        public bool TryGetRod(int leg, out Vector3 top, out Quaternion rot)
        {
            var r = rods[leg];
            top = default;
            rot = Quaternion.identity;
            if (r == null || !r.Root.gameObject.activeSelf) return false;
            top = r.Root.position;
            rot = r.Root.rotation;
            return true;
        }

        void Awake()
        {
            character = GetComponent<BlockyCharacter>();
            actions = GetComponent<BlockyCharacterActions>();
        }

        RodModel.Parts Rod(int leg)
        {
            if (rods[leg] != null) return rods[leg];
            Transform hip = leg == 0 ? character.HipRight : character.HipLeft;
            rods[leg] = RodModel.Create(hip, character.gameObject.layer, true, leg == 0 ? 1f : -1f);
            return rods[leg];
        }

        void LateUpdate()
        {
            if (character == null || actions == null || !character.IsBuilt) return;
            int shown = actions.RodLegsShown;
            bool anim = actions.RodAttaching;
            int animLeg = actions.RodAttachLegIndex;
            float t = actions.RodAttachTime;
            int layer = character.gameObject.layer;
            var mode = character.IsFirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;

            for (int leg = 0; leg < 2; leg++)
            {
                int bit = leg == 0 ? Limb.LegR : Limb.LegL;
                bool animThis = anim && animLeg == leg && t >= RodTimeline.Show;
                bool on = (shown & bit) != 0 || animThis;
                if (!on)
                {
                    if (rods[leg] != null && rods[leg].Root.gameObject.activeSelf) rods[leg].Root.gameObject.SetActive(false);
                    continue;
                }
                var r = Rod(leg);
                if (!r.Root.gameObject.activeSelf) r.Root.gameObject.SetActive(true);
                if (animThis && t < RodTimeline.InsertEnd)
                {
                    // Held in the hands: pose from the animation (character root space).
                    Vector3 axis = actions.RodAnimAxis;
                    Vector3 top = transform.TransformPoint(actions.RodAnimTop);
                    Vector3 axisW = transform.TransformDirection(axis);
                    Vector3 fwd = Vector3.Cross(axisW, transform.right);
                    if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
                    r.Root.SetPositionAndRotation(top, Quaternion.LookRotation(fwd.normalized, -axisW));
                }
                else
                {
                    r.Root.localPosition = Vector3.zero;
                    r.Root.localRotation = Quaternion.identity;
                }
                RodModel.SetTape(r, animThis ? actions.RodAnimTape : 1f);
                RodModel.SetRenderMode(r, layer, mode);
            }

            // Tape roll in the right fist while wrapping.
            bool rollOn = anim && t >= RodTimeline.TapeStart - 0.25f && t < RodTimeline.TapeEnd + 0.1f;
            if (rollOn && roll == null) roll = MakeRoll();
            if (roll != null)
            {
                if (roll.gameObject.activeSelf != rollOn) roll.gameObject.SetActive(rollOn);
                if (rollOn)
                {
                    Transform hand = character.ElbowRight;
                    roll.SetPositionAndRotation(hand.TransformPoint(CharacterPose.HandLocal + new Vector3(0f, -0.04f, 0f)), hand.rotation);
                    var rr = roll.GetComponent<Renderer>();
                    roll.gameObject.layer = layer;
                    rr.shadowCastingMode = mode;
                }
            }
        }

        Transform MakeRoll()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "TapeRoll";
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            g.transform.localScale = new Vector3(0.11f, 0.03f, 0.11f);
            var src = rods[0] ?? rods[1];
            var mr = g.GetComponent<MeshRenderer>();
            mr.sharedMaterial = src != null && src.Sleeve != null ? src.Sleeve.GetComponent<Renderer>().sharedMaterial : RodModel.Metal;
            return g.transform;
        }
    }
}
