using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// One place that decides the mouse cursor every frame while playing (a local player exists): free and visible only
    /// while a menu / panel really is open (<see cref="WantsFree"/>), otherwise locked and hidden - whatever else touched
    /// it during the frame. Before, any code could unlock it and nobody locked it again: a player joining the lobby
    /// (NetPlayer.SetupRemote turned the remote reader's cursor handling off, which unlocked the cursor of everybody),
    /// focus changes, dialogs. A change made by someone else is logged ("[Cursor] ... changed outside the policy") so a
    /// new culprit shows up in the log. In the main menu (no local player) the menus manage the cursor themselves.
    /// </summary>
    [DefaultExecutionOrder(31000)]
    public sealed class CursorPolicy : MonoBehaviour
    {
        private bool _haveLast;
        private CursorLockMode _lastMode;
        private bool _lastVisible;
        private float _nextOutsideLog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("CursorPolicy");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<CursorPolicy>();
        }

        /// <summary>A menu, panel or dialog that needs the mouse pointer is open.</summary>
        public static bool WantsFree =>
            LocalPlayerController.MenuOpen || UI.ConfirmDialog.IsOpen || Boombox.PanelOpen || LobbyTvVideo.PanelOpen
            || HideAndSeek.Game.MiniGames.BoardGameView.WantsCursor // lobby chess / tic-tac-toe (mouse picks squares)
            || UI.LoadingScreen.WantsCursor; // "Anuluj" on the loading screen

        /// <summary>The policy is in charge (in a session with a local player, outside the main menu).</summary>
        public static bool Active => NetPlayer.Local != null && SceneManager.GetActiveScene().name != NetSession.MenuScene;

        private void LateUpdate()
        {
            if (!Active)
            {
                _haveLast = false;
                return;
            }
            bool free = WantsFree;
            var mode = free ? CursorLockMode.None : CursorLockMode.Locked;
            bool visible = free && !UI.PadCursor.Active; // the pad cursor draws its own pointer

            if (_haveLast && (Cursor.lockState != _lastMode || Cursor.visible != _lastVisible) && Time.unscaledTime >= _nextOutsideLog)
            {
                _nextOutsideLog = Time.unscaledTime + 2f;
                Debug.Log($"[Cursor] changed outside the policy to {Cursor.lockState}/{(Cursor.visible ? "visible" : "hidden")} " +
                          $"(menu {LocalPlayerController.MenuOpen}, dialog {UI.ConfirmDialog.IsOpen}, radio {Boombox.PanelOpen}, " +
                          $"focused {Application.isFocused}, players {NetPlayer.All.Count}) - {(free ? "free" : "locked")} again");
            }
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            if (Cursor.visible != visible) Cursor.visible = visible;
            if (!_haveLast || _lastMode != mode) Debug.Log($"[Cursor] {(free ? "free (menu / panel open)" : "locked (playing)")}, players {NetPlayer.All.Count}");
            _haveLast = true;
            _lastMode = mode;
            _lastVisible = visible;
        }
    }
}
