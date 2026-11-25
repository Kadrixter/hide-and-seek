using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Board highlights and the local player's mouse input: last move (everybody), check (red glow under the king,
    /// everybody), hover / selected square, dots for quiet moves and rings for captures (only the player at the
    /// board). Click a piece -> its legal moves light up -> click a square; promotions wait for the choice in the HUD.
    /// </summary>
    public sealed partial class ChessTable
    {
        Material _matLast, _matSel, _matHover, _matCheck, _matDot, _matRing;
        Transform _lastA, _lastB, _sel, _hover, _check;
        readonly List<Transform> _dots = new();
        readonly List<ChessMove> _legal = new();
        int _marksVersion = -1, _marksSel = -2;

        /// <summary>Selected square of the local player (-1 = none).</summary>
        public int Selected { get; private set; } = -1;
        /// <summary>Seat of the local player at this table (-1 = not seated here).</summary>
        public int LocalSeat => NetPlayer.Local != null ? SeatOf(NetPlayer.Local.NetworkObjectId) : -1;
        /// <summary>Promotion waiting for the piece choice (from, to), or (-1, -1).</summary>
        public (int from, int to) PendingPromotion { get; private set; } = (-1, -1);

        void InitMarks()
        {
            var shader = Resources.Load<Shader>("Shaders/HnsBoardMark");
            Material M(Texture2D tex, Color c)
            {
                var m = shader != null ? new Material(shader) : new Material(Model.Kit.Template);
                m.mainTexture = tex;
                m.color = c;
                return m;
            }
            _matLast = M(MarkArt.SoftSquare, new Color(1f, 0.82f, 0.25f, 0.42f));
            _matSel = M(MarkArt.SoftSquare, new Color(0.45f, 1f, 0.45f, 0.55f));
            _matHover = M(MarkArt.SoftSquare, new Color(1f, 1f, 1f, 0.22f));
            _matCheck = M(MarkArt.Glow, new Color(1f, 0.12f, 0.08f, 0.85f));
            _matDot = M(MarkArt.Dot, new Color(0.25f, 0.9f, 0.35f, 0.75f));
            _matRing = M(MarkArt.Ring, new Color(0.95f, 0.3f, 0.2f, 0.8f));
            _lastA = Mark(_matLast, 1f, 0.0016f);
            _lastB = Mark(_matLast, 1f, 0.0016f);
            _check = Mark(_matCheck, 1.25f, 0.0018f);
            _hover = Mark(_matHover, 1f, 0.002f);
            _sel = Mark(_matSel, 1f, 0.0022f);
        }

        Transform Mark(Material m, float size, float lift)
        {
            var g = Model.Kit.Part("Mark", Model.BoardPivot, ChessTableModel.Quad(ChessTableModel.Square * size, ChessTableModel.Square * size), m,
                Vector3.zero, Quaternion.Euler(90f, 0f, 0f), false);
            g.GetComponent<MeshRenderer>().receiveShadows = false;
            g.transform.localScale = Vector3.one;
            g.SetActive(false);
            g.name = "Mark";
            var t = g.transform;
            t.localPosition = new Vector3(0f, lift, 0f);
            return t;
        }

        static void Place(Transform mark, int sq, float lift)
        {
            if (sq < 0) { if (mark.gameObject.activeSelf) mark.gameObject.SetActive(false); return; }
            mark.localPosition = ChessTableModel.SquareLocal(sq) + Vector3.up * lift;
            if (!mark.gameObject.activeSelf) mark.gameObject.SetActive(true);
        }

        void UpdateMarks()
        {
            int local = LocalSeat;
            if (local < 0 || State.Phase != ChessState.Playing || State.IsCheckers) { Selected = -1; PendingPromotion = (-1, -1); Place(_hover, -1, 0f); }
            UpdateCheckersMarks();
            if (!GameOnBoard || State.IsCheckers)
            {
                Place(_lastA, -1, 0f); Place(_lastB, -1, 0f); Place(_check, -1, 0f); Place(_sel, -1, 0f);
                if (_marksSel != -3) { _marksSel = -3; for (int i = 0; i < _dots.Count; i++) Place(_dots[i], -1, 0f); }
                return;
            }
            // Last move and check follow the shown board (they appear when the piece lands).
            bool idle = !Animating;
            int lf = -1, lt = -1;
            if (idle && State.Last.Length >= 4) { lf = ChessBoard.ParseSq(State.Last, 0); lt = ChessBoard.ParseSq(State.Last, 2); }
            Place(_lastA, lf, 0.0016f);
            Place(_lastB, lt, 0.0016f);
            Place(_check, idle && Shown.InCheck(Shown.Side) ? Shown.KingSquare(Shown.Side) : -1, 0.0018f);
            Place(_sel, Selected, 0.0022f);
            if (_marksVersion != Version || _marksSel != Selected)
            {
                _marksVersion = Version;
                _marksSel = Selected;
                RefreshDots();
            }
        }

        void RefreshDots()
        {
            int n = 0;
            if (Selected >= 0)
            {
                Shown.LegalMoves(_legal);
                var seen = new HashSet<int>();
                foreach (var m in _legal)
                {
                    if (m.From != Selected || !seen.Add(m.To)) continue;
                    Transform d;
                    if (n < _dots.Count) d = _dots[n];
                    else { d = Mark(_matDot, 1f, 0.0024f); _dots.Add(d); }
                    d.GetComponent<MeshRenderer>().sharedMaterial = m.IsCapture ? _matRing : _matDot;
                    Place(d, m.To, 0.0024f);
                    n++;
                }
            }
            for (int i = n; i < _dots.Count; i++) Place(_dots[i], -1, 0f);
        }

        /// <summary>Square under a ray (board plane, -1 = off the board).</summary>
        public int PickSquare(Ray worldRay)
        {
            var piv = Model.BoardPivot;
            Vector3 o = piv.InverseTransformPoint(worldRay.origin), d = piv.InverseTransformDirection(worldRay.direction);
            if (Mathf.Abs(d.y) < 1e-4f) return -1;
            float t = (ChessTableModel.BoardTop - o.y) / d.y;
            if (t < 0f) return -1;
            Vector3 p = o + d * t;
            int f = Mathf.FloorToInt(p.x / ChessTableModel.Square + 4f), r = Mathf.FloorToInt(p.z / ChessTableModel.Square + 4f);
            return f < 0 || f > 7 || r < 0 || r > 7 ? -1 : r * 8 + f;
        }

        /// <summary>Local player: mouse over the board (hover mark) and clicks. Returns true when a move was sent.</summary>
        public bool LocalPointer(NetPlayer me, Ray ray, bool click)
        {
            if (State.IsCheckers) return CheckersPointer(me, ray, click);
            int seat = SeatOf(me.NetworkObjectId);
            if (seat < 0 || State.Phase != ChessState.Playing) return false;
            int colour = State.ColourOfSeat(seat);
            bool myTurn = Target.Side == colour && !Animating;
            int sq = PickSquare(ray);
            Place(_hover, myTurn ? sq : -1, 0.002f);
            if (!click || PendingPromotion.from >= 0) return false;
            if (!myTurn) { if (sq >= 0) MiniGameSounds.Deny(this); return false; }
            if (sq < 0) { Selected = -1; return false; }
            int p = Shown.Sq[sq];
            if (p != 0 && (p > 0) == (colour > 0))
            {
                Selected = Selected == sq ? -1 : sq;
                if (Selected >= 0) MiniGameSounds.Select(this);
                return false;
            }
            if (Selected < 0) return false;
            Shown.LegalMoves(_legal);
            foreach (var m in _legal)
            {
                if (m.From != Selected || m.To != sq) continue;
                if (m.Promo != 0) { PendingPromotion = (m.From, m.To); return false; }
                SendMove(me, m.From, m.To, 0);
                return true;
            }
            Selected = -1;
            MiniGameSounds.Deny(this);
            return false;
        }

        /// <summary>HUD: the promotion piece was chosen (Knight..Queen), 0 = cancel.</summary>
        public void ChoosePromotion(NetPlayer me, int type)
        {
            var pp = PendingPromotion;
            PendingPromotion = (-1, -1);
            if (pp.from < 0 || type == 0) { Selected = -1; return; }
            SendMove(me, pp.from, pp.to, type);
        }

        void SendMove(NetPlayer me, int from, int to, int promo)
        {
            Selected = -1;
            me.ChessMoveRpc((byte)from, (byte)to, (byte)promo);
        }
    }
}
