using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Tic-tac-toe chalkboard on every peer: follows <see cref="MatchManager.TttNet"/>; the two players stand in front
    /// of the board (A / X on the left, B / O on the right, as seen from the room). A new mark is drawn in chalk by the player's hand (TttTable.Anim),
    /// the winner crosses out the line; a new round, or the players leaving, wipes the slate with the eraser.
    /// A late join or a jump in the state redraws the slate at once. Local input: click a cell on your turn.
    /// </summary>
    public sealed partial class TttTable : MiniTable
    {
        public override byte Game => MatchManager.GameTtt;
        public override string Title => "kółko i krzyżyk";
        public override string JoinVerb => "podejdź do tablicy";
        protected override float StoolZ => StandZ;
        protected override float TopY => TttTableModel.LedgeY;
        protected override float TableHalf => 0f;
        public override float SurfaceY => TttTableModel.GridY;
        public override ulong SeatPlayer(int seat) => State.Seat(seat);
        public override string SeatName(int seat) => seat == 0 ? "X" : "O";
        public override bool Playing => State.Phase == TttState.Playing;

        /// <summary>Where the players wait: half a metre left / right of the centre, this far out from the wall.</summary>
        const float StandX = 0.45f, StandZ = 0.85f;

        public override Vector3 SeatStand(int seat) => transform.TransformPoint(new Vector3(seat == 0 ? StandX : -StandX, TttTableModel.FloorY + 0.02f, StandZ));
        public override float SeatYaw(int seat) => transform.eulerAngles.y + 180f;
        public override Vector3 LeavePoint(int seat) => transform.TransformPoint(new Vector3(seat == 0 ? 0.8f : -0.8f, TttTableModel.FloorY + 0.02f, 1.45f));
        public override Vector3 Centre => transform.TransformPoint(new Vector3(0f, TttTableModel.GridY, TttTableModel.SlateZ));

        public override void CameraPose(int seat, float zoom, out Vector3 pos, out Quaternion rot)
        {
            pos = transform.TransformPoint(new Vector3(seat == 0 ? 0.06f : -0.06f, TttTableModel.GridY + 0.05f, 0.74f * zoom));
            rot = Quaternion.LookRotation(transform.TransformPoint(new Vector3(0f, TttTableModel.GridY, 0f)) - pos, Vector3.up);
        }

        public TttTableModel Model { get; private set; }
        public readonly TttState State = new();
        string _lastNet;
        bool _hasState;
        /// <summary>Marks already on the slate (or being drawn).</summary>
        string _drawn = ".........";
        int _drawnRound = -1, _drawnLine = -1;
        public int Version { get; private set; }
        public int Hover { get; private set; } = -1;

        public static TttTable Create(Transform parent, Vector3 pos, Quaternion rot, Material template)
        {
            var go = new GameObject("TttBoard") { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var t = go.AddComponent<TttTable>();
            t.Model = TttTableModel.Build(go.transform, template);
            t.InitAnim();
            return t;
        }

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm != null)
            {
                string net = mm.TttNet.Value.ToString();
                if (net != _lastNet)
                {
                    _lastNet = net;
                    var ns = new TttState();
                    if (TttState.TryDecode(net, ns)) OnState(ns);
                }
            }
            TickAnim(Time.deltaTime);
            DrivePoses();
            if (LocalSeat < 0 || State.Phase != TttState.Playing) Hover = -1;
            UpdateHover();
        }

        public int LocalSeat => NetPlayer.Local != null ? SeatOf(NetPlayer.Local.NetworkObjectId) : -1;

        bool SlateDirty => _drawn != "........." || _drawnLine >= 0;

        void OnState(TttState ns)
        {
            byte prevPhase = State.Phase;
            TttState.TryDecode(ns.Encode(), State);
            Version++;
            bool fresh = !_hasState;
            _hasState = true;
            if (!fresh && ns.Cells == "........." && (ns.Round != _drawnRound || SlateDirty))
            {
                // New round (wiped by the player who starts it) or the players left (wiped by whoever is still there).
                if (SlateDirty)
                {
                    int by = ns.Round != _drawnRound ? ns.Starter : ns.A != TttState.Nobody ? 0 : ns.B != TttState.Nobody ? 1 : -1;
                    if (by >= 0) QueueErase(by);
                    else { CancelAnims(); Redraw(ns.Cells, -1); }
                }
                _drawnRound = ns.Round;
                _drawn = ".........";
                _drawnLine = -1;
            }
            else if (!fresh && ns.Round == _drawnRound && Diff(_drawn, ns.Cells, out int cell))
            {
                char mark = ns.Cells[cell];
                QueueMark(cell, mark, mark == 'X' ? 0 : 1, ns.Line);
                _drawn = ns.Cells;
                _drawnLine = ns.Line;
            }
            else if (ns.Cells != _drawn || ns.Round != _drawnRound || ns.Line != _drawnLine)
            {
                CancelAnims();
                Redraw(ns.Cells, ns.Line);
                _drawn = ns.Cells;
                _drawnRound = ns.Round;
                _drawnLine = ns.Line;
            }
            if (prevPhase != ns.Phase)
            {
                if (Animating && ns.Phase == TttState.Over) EndSoundPending = true;
                else MiniGameSounds.PhaseChange(this, prevPhase, ns.Phase, ns.End);
            }
        }

        /// <summary>True when <paramref name="now"/> is <paramref name="before"/> plus exactly one mark.</summary>
        static bool Diff(string before, string now, out int cell)
        {
            cell = -1;
            for (int i = 0; i < 9; i++)
            {
                if (before[i] == now[i]) continue;
                if (before[i] != '.' || cell >= 0) { cell = -1; return false; }
                cell = i;
            }
            return cell >= 0;
        }

        /// <summary>Slate exactly as the state says (grid, every mark, winning line).</summary>
        void Redraw(string cells, int line)
        {
            Model.ClearSlate();
            for (int i = 0; i < 9; i++)
                if (cells[i] != '.') TttChalk.DrawMark(Model, i, cells[i], 1f, i * 7 + 3);
            if (line >= 0) TttChalk.DrawWinLine(Model, line, 0f, 1f);
            Model.SlateTex.Apply(true);
        }

        // ================================================================ input

        /// <summary>Cell under a ray (board plane, -1 = outside the grid).</summary>
        public int PickCell(Ray worldRay)
        {
            Vector3 o = transform.InverseTransformPoint(worldRay.origin), d = transform.InverseTransformDirection(worldRay.direction);
            if (Mathf.Abs(d.z) < 1e-4f) return -1;
            float t = (TttTableModel.SlateZ - o.z) / d.z;
            if (t < 0f) return -1;
            Vector3 p = o + d * t;
            float half = TttTableModel.GridSize * 0.5f, y = p.y - TttTableModel.GridY;
            if (Mathf.Abs(p.x) > half || Mathf.Abs(y) > half) return -1;
            int col = Mathf.Clamp(Mathf.FloorToInt((half - p.x) / TttTableModel.Cell), 0, 2);
            int row = Mathf.Clamp(Mathf.FloorToInt((half - y) / TttTableModel.Cell), 0, 2);
            return row * 3 + col;
        }

        public bool LocalPointer(NetPlayer me, Ray ray, bool click)
        {
            int seat = SeatOf(me.NetworkObjectId);
            if (seat < 0 || State.Phase != TttState.Playing) { Hover = -1; return false; }
            bool myTurn = State.Turn == seat && !Animating;
            int cell = PickCell(ray);
            Hover = myTurn && cell >= 0 && State.Cells[cell] == '.' ? cell : -1;
            if (!click) return false;
            if (!myTurn || cell < 0 || State.Cells[cell] != '.') { if (cell >= 0) MiniGameSounds.Deny(this); return false; }
            me.TttMoveRpc((byte)cell);
            Hover = -1;
            return true;
        }

        protected override float ApplyPose(BlockyCharacterActions act, int seat, float seed)
        {
            PoseInputs(seat, out var reach, out float reachW, out float think, out var look, out _);
            act.SetBoardStand(reach, reachW, think, look, StepTo(seat), seed);
            return reachW;
        }
    }
}
