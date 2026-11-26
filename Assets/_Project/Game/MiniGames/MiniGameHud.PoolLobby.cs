using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Join panel of the pool table (between games, mouse): two teams with two places each - a nick ("gotowy" in
    /// green), the own place marked, a free place to click (moves the player there). "Gotowy" / "Nie gotowy" and
    /// "Odejdź". A game starts when everybody at the table is ready (1 na 1, 1 na 2, 2 na 2, or solo with one team
    /// empty); after a game the same panel shows the result and "Gotowy" starts the rematch (the losers break).
    /// </summary>
    public sealed partial class MiniGameHud
    {
        GameObject _lobby;
        Text _lobbyTitle, _lobbyStatus;
        readonly Button[] _slotBtn = new Button[4];
        readonly Text[] _slotText = new Text[4];
        Button _readyBtn, _leaveBtn;
        Text _readyText;

        /// <summary>The join panel is up (tests).</summary>
        public static bool PoolLobbyShown { get; private set; }

        static MiniGameHud _lobbyHud;

        /// <summary>Test: clicks a button of the join panel like the mouse does ("ready", "leave" or "place0".."place3").</summary>
        public static bool TestClick(string what)
        {
            var h = _lobbyHud;
            if (h == null || h._lobby == null || !h._lobby.activeInHierarchy) return false;
            Button b = what == "ready" ? h._readyBtn : what == "leave" ? h._leaveBtn
                : what.StartsWith("place") && int.TryParse(what.Substring(5), out int p) && p >= 0 && p < 4 ? h._slotBtn[p] : null;
            if (b == null || !b.interactable) return false;
            b.onClick.Invoke();
            return true;
        }

        void BuildPoolLobby(Transform root)
        {
            UiKit.EnsureEventSystem();
            _lobbyHud = this;
            var panel = UiStyle.Panel("PoolLobby", root, new Color(0.05f, 0.055f, 0.06f, 0.9f));
            panel.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(660, 300));
            _lobby = panel.gameObject;
            _lobbyTitle = UiKit.Label("Title", panel.transform, "", 26, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            _lobbyTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(-30, 36));
            for (int team = 0; team < 2; team++)
            {
                float x = team == 0 ? -160f : 160f;
                var head = UiKit.Label("Team" + team, panel.transform, team == 0 ? "DRUŻYNA A" : "DRUŻYNA B", 20, TextAnchor.MiddleCenter, UiKit.TextDim, FontStyle.Bold);
                head.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -52), new Vector2(300, 28));
                for (int slot = 0; slot < 2; slot++)
                {
                    int place = team + 2 * slot;
                    var b = UiKit.BoxButton("Place" + place, panel.transform, "", () => ClickPlace(place), 22);
                    ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -86 - slot * 58), new Vector2(300, 50));
                    _slotBtn[place] = b;
                    _slotText[place] = b.GetComponentInChildren<Text>();
                }
            }
            _lobbyStatus = UiKit.Label("Status", panel.transform, "", 20, TextAnchor.MiddleCenter, UiKit.TextDim);
            _lobbyStatus.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 66), new Vector2(-30, 28));
            _readyBtn = UiKit.BoxButton("Ready", panel.transform, "Gotowy", ClickReady, 24);
            ((RectTransform)_readyBtn.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-100, 12), new Vector2(220, 48));
            _readyText = _readyBtn.GetComponentInChildren<Text>();
            var leave = _leaveBtn = UiKit.BoxButton("Leave", panel.transform, "Odejdź", () => NetPlayer.Local?.MiniLeaveRpc(), 22);
            ((RectTransform)leave.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(140, 12), new Vector2(180, 48));
            _lobby.SetActive(false);
        }

        PoolTable _lobbyTable;

        void ClickPlace(int place)
        {
            var me = NetPlayer.Local;
            if (me == null || _lobbyTable == null || _lobbyTable.SeatPlayer(place) != PoolState.Nobody) return;
            me.MiniSitRpc(MatchManager.GamePool, (byte)place);
        }

        void ClickReady()
        {
            var me = NetPlayer.Local;
            if (me == null || _lobbyTable == null) return;
            int place = _lobbyTable.LocalSeat;
            if (place < 0) return;
            me.PoolReadyRpc((_lobbyTable.State.Ready & (1 << place)) == 0);
        }

        /// <summary>Shows / fills the join panel (<paramref name="show"/> = seated at the pool table between games).</summary>
        void TickPoolLobby(PoolTable t, int place, bool show)
        {
            if (_lobby == null) BuildPoolLobby(_canvas.transform);
            PoolLobbyShown = show;
            if (_lobby.activeSelf != show) _lobby.SetActive(show);
            if (!show) return;
            _lobbyTable = t;
            var s = t.State;
            _lobbyTitle.text = s.Phase == PoolState.Over && s.Winner <= 1
                ? $"{PoolEnd(s, PoolState.TeamOf(place))}    {s.WinsA} : {s.WinsB}"
                : $"Bilard - ósemka    {s.WinsA} : {s.WinsB}";
            for (int p = 0; p < PoolState.Places; p++)
            {
                ulong id = s.Place(p);
                bool ready = (s.Ready & (1 << p)) != 0;
                string nick = t.SeatNick(p);
                _slotText[p].text = id == PoolState.Nobody ? "+ wolne miejsce" : (p == place ? "Ty: " : "") + nick + (ready ? "  - gotowy" : "");
                _slotText[p].color = id == PoolState.Nobody ? UiKit.TextDim : ready ? Good : UiKit.Text;
                _slotBtn[p].interactable = id == PoolState.Nobody;
            }
            bool meReady = place >= 0 && (s.Ready & (1 << place)) != 0;
            _readyText.text = meReady ? "Nie gotowy" : s.Phase == PoolState.Over ? "Gotowy - rewanż" : "Gotowy";
            bool solo = s.Members(0) == 0 || s.Members(1) == 0;
            _lobbyStatus.text = meReady ? "Czekamy, aż wszyscy będą gotowi..."
                : solo ? "Kliknij Gotowy, żeby grać samemu, albo poczekaj na rywala"
                : "Kliknij Gotowy - gra rusza, gdy wszyscy są gotowi";
        }

        /// <summary>Not at the pool table: the join panel is hidden, the hint line opaque again.</summary>
        void HidePoolLobby()
        {
            PoolLobbyShown = false;
            if (_lobby != null && _lobby.activeSelf) _lobby.SetActive(false);
            var hc = _hints.color;
            if (hc.a < 0.9f) _hints.color = new Color(hc.r, hc.g, hc.b, 0.92f);
        }
    }
}
