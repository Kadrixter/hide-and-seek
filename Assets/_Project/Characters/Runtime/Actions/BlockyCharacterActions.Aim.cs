using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Glock aiming (RMB): both arms straight out in front of the chest, the gun level with the eyes and close to the
    /// body's centre line, following the look pitch fully. <see cref="GlockFrame"/> blends into this frame by
    /// <see cref="SetGlockAim"/>; the shot recoil (spring) is added on top by the frame and by <see cref="AimRecoil"/>.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float glockAimW;

        /// <summary>0 = relaxed low hold, 1 = aiming with both arms straight (driven every frame by the game).</summary>
        public void SetGlockAim(float weight) => glockAimW = Mathf.Clamp01(weight);

        public float GlockAimWeight => glockAimW;

        /// <summary>Gun frame while aiming (root space), before the recoil.</summary>
        void GlockAimFrame(in CharacterPose p, float lookPitch, out Vector3 pos, out Quaternion rot)
        {
            float look = Mathf.Clamp(lookPitch, -65f, 65f);
            Quaternion rl = Quaternion.Euler(-look, 0f, 0f);
            // Pivot between the shoulders; the arms reach almost fully forward, the gun sits a hand's width right of centre.
            Vector3 pivot = new Vector3(0f, 0.58f, 0f);
            Vector3 offset = new Vector3(0.035f, 0.035f, 0.5f);
            pos = p.TorsoPoint(pivot + rl * offset);
            rot = p.TorsoRoot * rl * Quaternion.Euler(0f, -1.5f, 0f);
        }

        /// <summary>Aiming stance: a slight forward lean into the gun (applied before the arms are solved).</summary>
        void AimLean(ref CharacterPose p) => p.Torso = p.Torso * Quaternion.Euler(4f * glockAimW, 0f, 0f);

        /// <summary>Head part of the aim / recoil: chin down towards the sights, snaps back with each shot.</summary>
        void AimRecoil(ref CharacterPose p, bool standing)
        {
            if (!standing || shownItem != HeldItem.Glock) return;
            p.Head = p.Head * Quaternion.Euler(4f * glockAimW - 6f * recoil, 0f, 0f);
        }
    }
}
