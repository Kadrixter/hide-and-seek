using UnityEngine;

namespace HideAndSeek.Characters
{
    public partial class BlockyCharacterActions
    {
        /// <summary>
        /// At the lobby pool table with a cue (<see cref="BoardGamePose"/> pool variant). World-space points: the right
        /// fist on the cue, the left fist (bridge), what the head looks at. <paramref name="bend"/> 0 = standing with the
        /// cue upright, 1 = the shooting stance; <paramref name="walk"/> 0..1 = stepping. Call every frame;
        /// <see cref="SetBoardGame"/> with false to stop. <paramref name="clearZ"/> / <paramref name="clearY"/>: the table's
        /// outer edge straight in front of the root (distance, rail-top height above the root; 0 = no table): the torso
        /// and the head never lean down into it.
        /// </summary>
        public void SetPoolStance(Vector3 grip, Vector3 bridge, float bend, Vector3 look, float walk, float seed, float clearZ = 0f, float clearY = 0f)
        {
            EnsureInit();
            boardOn = true;
            boardIn = new BoardGamePose.Input
            {
                Pool = true,
                Reach = transform.InverseTransformPoint(grip),
                ReachW = 1f,
                Bridge = transform.InverseTransformPoint(bridge),
                Bend = bend,
                Walk = walk,
                Look = transform.InverseTransformPoint(look),
                Seed = seed,
                ClearZ = clearZ,
                ClearY = clearY,
            };
        }
    }
}
