using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Keys and mouse of the local player at a board-game table (runs before the HUD, so Esc opens our question
    /// instead of the game menu): LMB = select / move / mark, Esc = get up (asks first; resigns a running game),
    /// R = rematch, C = time control (chess, between games). Pool: the mouse goes to PoolTable.LocalPointer (aiming). Also keeps <see cref="MiniGameHud"/> up to date.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class MiniGameLocal : MonoBehaviour
    {
        static MiniGameLocal _inst;
        MiniGameHud _hud;

        public static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("MiniGameLocal");
            _inst = go.AddComponent<MiniGameLocal>();
        }

        void Awake() => _hud = MiniGameHud.Create(transform);

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }

        void Update()
        {
            var me = NetPlayer.Local;
            int seat = -1;
            var table = me != null ? MiniGameTables.LocalTable(out seat) : null;
            if (table == null) seat = -1;
            _hud.Tick(me, table, seat);
            if (table == null || me == null) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool dialog = ConfirmDialog.IsOpen || LocalPlayerController.MenuOpen;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !ConfirmDialog.ConsumesEscape)
            {
                if (table is PoolTable)
                {
                    if (table.Playing) ConfirmDialog.Show("Poddać partię i odejść?", "Przeciwnik wygra tę partię.", "Tak, odchodzę", "Nie", me.MiniLeaveRpc);
                    else ConfirmDialog.Show("Odejść od stołu?", null, "Tak", "Nie", me.MiniLeaveRpc);
                }
                else if (table.Playing)
                    ConfirmDialog.Show(table is ChessTable ? "Poddać partię i wstać?" : "Przerwać grę i odejść?",
                        table is ChessTable ? "Przeciwnik wygra przez poddanie." : "Runda nie zostanie policzona.", "Tak, wstaję", "Nie", me.MiniLeaveRpc);
                else ConfirmDialog.Show(table is ChessTable ? "Wstać od stolika?" : "Odejść od tablicy?", null, "Tak", "Nie", me.MiniLeaveRpc);
                return;
            }
            if (dialog) return;
            if (kb != null && kb.rKey.wasPressedThisFrame && !table.Playing) me.MiniRematchRpc();
            if (kb != null && kb.cKey.wasPressedThisFrame && table is ChessTable ct && !ct.Playing) me.ChessClockRpc();
            if (!MiniGameCamera.MouseRay(out var ray) || mouse == null) return;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool click = mouse.leftButton.wasPressedThisFrame && !overUi && MiniGameCamera.Weight > 0.8f;
            if (table is ChessTable chess) chess.LocalPointer(me, ray, click);
            else if (table is TttTable ttt) ttt.LocalPointer(me, ray, click);
            else if (table is PoolTable pool) pool.LocalPointer(me, ray, click);
        }
    }
}
