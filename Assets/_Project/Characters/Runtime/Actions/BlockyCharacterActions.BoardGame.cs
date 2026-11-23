using UnityEngine;

namespace HideAndSeek.Characters
{
    public partial class BlockyCharacterActions
    {
        bool boardOn;
        float boardW;
        BoardGamePose.Input boardIn;

        /// <summary>Weight of the seated board-game pose (0 = not at a table).</summary>
        public float BoardGameWeight => boardW;

        /// <summary>
        /// Seated at a lobby board-game table (<see cref="BoardGamePose"/>). Points are in world space (converted to
        /// root space here); call every frame while seated, once with <paramref name="on"/> = false to stand up.
        /// </summary>
        public void SetBoardGame(bool on, Vector3 seatTop, Vector3 tableEdge, float footRestY, Vector3 reach, float reachW,
            float think, Vector3 look, float lean, float seed)
        {
            EnsureInit();
            boardOn = on;
            if (!on) return;
            float y0 = transform.position.y;
            boardIn = new BoardGamePose.Input
            {
                SeatTop = transform.InverseTransformPoint(seatTop),
                TableEdge = transform.InverseTransformPoint(tableEdge),
                FootRestY = footRestY - y0,
                Reach = transform.InverseTransformPoint(reach),
                ReachW = reachW,
                Think = think,
                Look = transform.InverseTransformPoint(look),
                Seed = seed,
                Lean = lean,
            };
        }

        /// <summary>Full-body seated pose, blended in / out (sitting down and standing up take about half a second).</summary>
        void ApplyBoardGame(ref CharacterPose p, float dt)
        {
            boardW = Mathf.MoveTowards(boardW, boardOn ? 1f : 0f, dt / 0.55f);
            if (boardW < 0.001f) return;
            var bp = BoardGamePose.Compute(boardIn, time);
            bp.PropR = p.PropR;
            p = CharacterPose.Lerp(p, bp, ActionTimeline.SmoothStep01(boardW));
        }
    }
}
