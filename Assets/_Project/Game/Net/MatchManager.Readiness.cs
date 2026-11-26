using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Readiness signals for the loading screen (<see cref="UI.LoadingScreen"/>), local to every copy and read-only: they
    /// change nothing in the round flow. <see cref="PhaseChangedLocal"/> repeats the phase change seen here (it shows the
    /// screen in the same frame the map / lobby load starts, before the scene switch stalls the main thread);
    /// <see cref="LobbyReadyFrame"/> / <see cref="MapReadyFrame"/> are the last frames in which the per-frame parts
    /// (mini games, lobby kart + drift track, bricks, boombox... / chainsaw shelf, car alarms, security rooms, public
    /// address, race track) ran in the lobby / map scene, i.e. their first-time builds are done.
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Raised on this copy after <see cref="Phase"/> changed (previous, new). Subscribers must not throw.</summary>
        public static event System.Action<MatchPhase, MatchPhase> PhaseChangedLocal;

        /// <summary>Last frame the lobby parts ran with phase Lobby in the lobby scene (-1 = never).</summary>
        public static int LobbyReadyFrame { get; private set; } = -1;

        /// <summary>Last frame the map parts ran in the map scene during a round (Hiding / Seeking / Ended / Race; -1 = never).</summary>
        public static int MapReadyFrame { get; private set; } = -1;

        private void NotifyPhaseLocal(MatchPhase prev, MatchPhase phase)
        {
            try { PhaseChangedLocal?.Invoke(prev, phase); }
            catch (System.Exception e) { Debug.LogException(e); } // UI only: never in the way of the phase change
        }

        /// <summary>End of <see cref="Update"/>: every part ran this frame, so the scene's first-time builds are done.</summary>
        private void MarkReadyFrames()
        {
            var ph = Phase.Value;
            string scene = SceneManager.GetActiveScene().name;
            if (ph == MatchPhase.Lobby && scene == NetSession.LobbyScene) LobbyReadyFrame = Time.frameCount;
            else if (ph != MatchPhase.Lobby && ph != MatchPhase.Loading && scene == NetSession.GameScene) MapReadyFrame = Time.frameCount;
        }
    }
}
