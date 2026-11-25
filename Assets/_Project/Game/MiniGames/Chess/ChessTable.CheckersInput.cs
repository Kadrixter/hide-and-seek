using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Checkers highlights and the local player's mouse input. Everybody sees the path of the last move. The player
    /// to move sees an orange glow under every disc that must capture (capturing is mandatory), and after clicking a
    /// disc the squares it can go to next (dots, rings for a jump). A multi-capture is clicked square by square: the
    /// path so far stays lit, the move is sent when the clicked path is a whole legal move.
    /// </summary>
    public sealed partial class ChessTable
    {
        Material _matMust;
        readonly List<Transform> _ckLastMarks = new(), _ckPathMarks = new(), _ckDots = new(), _ckMustMarks = new();
        Transform _ckHover;
        readonly List<int> _ckPath = new();
        readonly List<CheckersMove> _ckLegal = new();
        int _ckLegalFor = -1;

        /// <summary>First square of the local player's path being clicked (-1 = nothing selected).</summary>
        public int CkSelected => _ckPath.Count > 0 ? _ckPath[0] : -1;
        /// <summary>Squares clicked so far (start + landings) of the local player's move.</summary>
        public int CkPathLength => _ckPath.Count;

        /// <summary>Pieces the side to move must take with its next move (0 = no capture possible).</summary>
        public int CkMustCapture
        {
            get
            {
                var l = CkLegal();
                return l.Count > 0 && l[0].IsCapture ? l[0].Captured.Count : 0;
            }
        }

        void InitCheckersMarks()
        {
            var shader = Resources.Load<Shader>("Shaders/HnsBoardMark");
            _matMust = shader != null ? new Material(shader) : new Material(Model.Kit.Template);
            _matMust.mainTexture = MarkArt.Glow;
            _matMust.color = new Color(1f, 0.55f, 0.1f, 0.85f);
            _ckHover = Mark(_matHover, 1f, 0.002f);
        }

        List<CheckersMove> CkLegal()
        {
            if (_ckLegalFor != _ckShownVersion)
            {
                _ckLegalFor = _ckShownVersion;
                CkShown.LegalMoves(_ckLegal);
            }
            return _ckLegal;
        }

        static Transform Pooled(List<Transform> pool, int i, System.Func<Transform> make)
        {
            while (pool.Count <= i) pool.Add(make());
            return pool[i];
        }

        static void HideFrom(List<Transform> pool, int n)
        {
            for (int i = n; i < pool.Count; i++) Place(pool[i], -1, 0f);
        }

        void UpdateCheckersMarks()
        {
            int nLast = 0, nPath = 0, nDots = 0, nMust = 0;
            bool on = State.IsCheckers && GameOnBoard;
            int seat = LocalSeat;
            if (!on || seat < 0 || State.Phase != ChessState.Playing) _ckPath.Clear();
            if (on && !Animating)
            {
                var last = State.Last.Length >= 4 ? CheckersMove.ParsePath(State.Last) : null;
                if (last != null)
                    foreach (int s in last) Place(Pooled(_ckLastMarks, nLast++, () => Mark(_matLast, 1f, 0.0016f)), s, 0.0016f);
                bool myTurn = seat >= 0 && State.Phase == ChessState.Playing && CkShown.Side == State.ColourOfSeat(seat);
                if (myTurn)
                {
                    var legal = CkLegal();
                    foreach (int s in _ckPath) Place(Pooled(_ckPathMarks, nPath++, () => Mark(_matSel, 1f, 0.0022f)), s, 0.0022f);
                    if (_ckPath.Count == 0 && legal.Count > 0 && legal[0].IsCapture)
                    {
                        var seen = new HashSet<int>();
                        foreach (var m in legal)
                            if (seen.Add(m.From)) Place(Pooled(_ckMustMarks, nMust++, () => Mark(_matMust, 1.2f, 0.0019f)), m.From, 0.0019f);
                    }
                    if (_ckPath.Count > 0)
                    {
                        var seen = new HashSet<int>();
                        foreach (var m in legal)
                        {
                            if (!m.StartsWith(_ckPath) || m.Path.Count <= _ckPath.Count) continue;
                            int next = m.Path[_ckPath.Count];
                            if (!seen.Add(next)) continue;
                            var d = Pooled(_ckDots, nDots++, () => Mark(_matDot, 1f, 0.0024f));
                            d.GetComponent<MeshRenderer>().sharedMaterial = m.IsCapture ? _matRing : _matDot;
                            Place(d, next, 0.0024f);
                        }
                    }
                }
            }
            HideFrom(_ckLastMarks, nLast);
            HideFrom(_ckPathMarks, nPath);
            HideFrom(_ckDots, nDots);
            HideFrom(_ckMustMarks, nMust);
            if (!on || seat < 0) Place(_ckHover, -1, 0f);
        }

        /// <summary>Local player at the checkers board: hover and clicks. Returns true when a move was sent.</summary>
        bool CheckersPointer(NetPlayer me, Ray ray, bool click)
        {
            int seat = SeatOf(me.NetworkObjectId);
            if (seat < 0 || State.Phase != ChessState.Playing) { Place(_ckHover, -1, 0f); return false; }
            int colour = State.ColourOfSeat(seat);
            bool myTurn = CkTarget.Side == colour && !Animating;
            int sq = PickSquare(ray);
            Place(_ckHover, myTurn ? sq : -1, 0.002f);
            if (!click) return false;
            if (!myTurn) { if (sq >= 0) MiniGameSounds.Deny(this); return false; }
            if (sq < 0) { _ckPath.Clear(); return false; }
            var legal = CkLegal();
            int p = CkShown.Sq[sq];
            bool own = p != 0 && (p > 0) == (colour > 0);
            if (own && _ckPath.Count <= 1)
            {
                if (_ckPath.Count == 1 && _ckPath[0] == sq) { _ckPath.Clear(); return false; }
                bool movable = false;
                foreach (var m in legal) if (m.From == sq) { movable = true; break; }
                _ckPath.Clear();
                if (!movable) { MiniGameSounds.Deny(this); CkDenied = Time.unscaledTime; return false; }
                _ckPath.Add(sq);
                MiniGameSounds.Select(this);
                return false;
            }
            if (_ckPath.Count == 0) { MiniGameSounds.Deny(this); return false; }
            _ckPath.Add(sq);
            CheckersMove whole = null;
            bool any = false;
            foreach (var m in legal)
            {
                if (!m.StartsWith(_ckPath)) continue;
                any = true;
                if (m.Path.Count == _ckPath.Count) whole = m;
            }
            if (!any)
            {
                _ckPath.RemoveAt(_ckPath.Count - 1);
                if (_ckPath.Count == 1) _ckPath.Clear();
                MiniGameSounds.Deny(this);
                return false;
            }
            if (whole == null) { MiniGameSounds.Select(this); return false; }
            string text = whole.ToString();
            _ckPath.Clear();
            me.CheckersMoveRpc(new Unity.Collections.FixedString64Bytes(text));
            return true;
        }

        /// <summary>When the local player last clicked a disc that may not move (the HUD reminds of the capture rule).</summary>
        public float CkDenied { get; private set; } = -10f;
    }
}
