using UnityEngine;

namespace HideAndSeek.Characters
{
    public partial class BlockyCharacterActions
    {
        /// <summary>
        /// At the lobby pool table with a cue (<see cref="BoardGamePose"/> pool variant). World-space points: the right
        /// fist on the cue, the left fist (bridge), what the head looks at. <paramref name="bend"/> 0 = standing with the
        /// cue upright, 1 = the shooting stance; <paramref name="walk"/> 0..1 = stepping. Call every frame;
        /// <see cref="SetBoardGame"/> with false to stop.
        /// </summary>
        public void SetPoolStance(Vector3 grip, Vector3 bridge, float bend, Vector3 look, float walk, float seed)
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
            };
        }
    }
}
