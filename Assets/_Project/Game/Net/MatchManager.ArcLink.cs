using System.Threading.Tasks;
using HideAndSeek.Game.DotLink;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The dot link arcade cabinet in the lobby (server side): the exit-gate puzzle as a game for fun. One player at a
    /// time (<see cref="ArcLinkUser"/>); every time somebody steps up (E) a NEW random 10 x 10 puzzle is dealt - one is
    /// always kept ready, made off the main thread, so the start is instant. The lines live here and in
    /// <see cref="ArcLinkPathsNet"/>, so everybody in the lobby watches the progress live on the cabinet screen. Solved:
    /// the time from <see cref="ArcLinkStartAt"/> is compared with the lobby record (<see cref="ArcLinkBest"/>, kept
    /// while the server runs), the player stays 2.5 s for the flash and is let go (E again = the next puzzle).
    /// </summary>
    public partial class MatchManager
    {
        const float ArcLinkSolvedHold = 2.5f;

        public readonly NetworkVariable<FixedString128Bytes> ArcLinkPuzzleNet = new();
        public readonly NetworkVariable<FixedString512Bytes> ArcLinkPathsNet = new();
        public readonly NetworkVariable<ulong> ArcLinkUser = new(NoDotLinkUser);
        /// <summary>Server time the current puzzle was dealt (the clock on the HUD and the cabinet).</summary>
        public readonly NetworkVariable<double> ArcLinkStartAt = new();
        /// <summary>Solve time of the current puzzle in seconds (0 = not solved).</summary>
        public readonly NetworkVariable<float> ArcLinkSolvedTime = new();
        /// <summary>Lobby record (seconds, 0 = none yet) and who holds it.</summary>
        public readonly NetworkVariable<float> ArcLinkBest = new();
        public readonly NetworkVariable<FixedString64Bytes> ArcLinkBestNick = new();

        DotLinkPuzzle _alPuzzle, _alReady;
        DotLinkBoard _alBoard, _alDragBase;
        byte _alDragId;
        Task<DotLinkPuzzle> _alTask;
        double _alSolvedAt = -1;
        string _alUserNick = "";

        public NetPlayer ArcLinkUserPlayer => ArcLinkUser.Value != NoDotLinkUser ? NetPlayer.Find(ArcLinkUser.Value) : null;
        /// <summary>Server copy of the current puzzle with its solution (tests).</summary>
        public DotLinkPuzzle ArcLinkServerPuzzle => _alPuzzle;

        public static Vector3 ArcLinkOrigin => LobbyLayout.LinkArcade.pos;
        public static float ArcLinkYaw => LobbyLayout.LinkArcade.yaw;
        /// <summary>Where the player stands at the cabinet (same spot as at the SNAKE ones).</summary>
        public static Vector3 ArcLinkStand => ArcLinkOrigin + Quaternion.Euler(0f, ArcLinkYaw, 0f) * ArcadeCabs.StandLocal;

        /// <summary>May <paramref name="p"/> step up to the cabinet (lobby, standing, not in another game)?</summary>
        public bool ArcLinkCanUse(NetPlayer p) =>
            p != null && Phase.Value == MatchPhase.Lobby && !p.Busy.Value && p.Seat.Value < 0 && ArcadeCabOf(p.NetworkObjectId) < 0 && !p.BodyCarryBusy;

        /// <summary>Every frame on every peer (DotLinkDriver); the server part runs here.</summary>
        internal void UpdateArcLink()
        {
            if (!IsServer) return;
            if (_alReady == null && _alTask == null && Phase.Value == MatchPhase.Lobby) ArcLinkGenerate();
            if (_alTask != null && _alTask.IsCompleted)
            {
                var t = _alTask;
                _alTask = null;
                if (t.IsFaulted || t.Result == null) Debug.LogWarning($"[ArcLink] generation failed: {t.Exception?.GetBaseException().Message ?? "no puzzle"}");
                else _alReady = t.Result;
                // Somebody is already waiting at the cabinet for a puzzle: deal it now.
                if (_alReady != null && ArcLinkUser.Value != NoDotLinkUser && _alPuzzle == null) ArcLinkDeal();
            }
            if (ArcLinkUser.Value == NoDotLinkUser) return;
            var u = ArcLinkUserPlayer;
            string why = u == null ? "user left" : Phase.Value != MatchPhase.Lobby ? "round starts"
                : u.Busy.Value || u.Seat.Value >= 0 ? "busy"
                : Vector3.Distance(u.transform.position, ArcLinkStand) > 2.5f ? "walked away" : null;
            if (why == null && _alSolvedAt >= 0 && Now >= _alSolvedAt + ArcLinkSolvedHold) why = "solved";
            if (why != null) ArcLinkRelease(why);
        }

        void ArcLinkGenerate()
        {
            int seed = Random.Range(1, int.MaxValue);
            _alTask = Task.Run(() => DotLinkGenerator.Generate(seed, DotLinkSize, DotLinkSize, DotLinkMinColors, DotLinkMaxColors, out _));
        }

        /// <summary>Takes the ready puzzle for the player at the cabinet and starts making the next one.</summary>
        void ArcLinkDeal()
        {
            _alPuzzle = _alReady;
            _alReady = null;
            _alBoard = new DotLinkBoard(_alPuzzle);
            _alDragBase = null;
            _alSolvedAt = -1;
            ArcLinkSolvedTime.Value = 0f;
            ArcLinkPuzzleNet.Value = new FixedString128Bytes(_alPuzzle.Encode());
            ArcLinkStartAt.Value = Now;
            ArcLinkPublish();
            if (_alTask == null) ArcLinkGenerate();
            Debug.Log($"[ArcLink] new puzzle for {_alUserNick}: seed {_alPuzzle.Seed}, {_alPuzzle.Colors} colours");
        }

        void ArcLinkPublish()
        {
            _alBoard.Seq++;
            ArcLinkPathsNet.Value = new FixedString512Bytes(_alBoard.Encode());
        }

        void ArcLinkRelease(string why)
        {
            Debug.Log($"[ArcLink] cabinet free ({why}), was {_alUserNick}; pairs {(_alBoard != null ? _alBoard.CompleteCount : 0)}/{(_alPuzzle != null ? _alPuzzle.Colors : 0)}");
            ArcLinkUser.Value = NoDotLinkUser;
            _alDragBase = null;
            // The board stays on the screen (the others see how far he got) until the next player is dealt a new one.
        }

        /// <summary>Server: E at the cabinet (<paramref name="use"/>) or Esc.</summary>
        public void ServerArcLinkUse(NetPlayer p, bool use)
        {
            if (!IsServer || p == null) return;
            if (!use)
            {
                if (ArcLinkUser.Value == p.NetworkObjectId) ArcLinkRelease("left with Esc");
                return;
            }
            string why = !ArcLinkCanUse(p) ? "cannot use now" : ArcLinkUser.Value != NoDotLinkUser ? "busy"
                : Vector3.Distance(p.transform.position, ArcLinkStand) > 2.4f ? "too far" : null;
            if (why != null)
            {
                Debug.Log($"[ArcLink] {p.Nick} refused at the cabinet: {why}");
                return;
            }
            ArcLinkUser.Value = p.NetworkObjectId;
            _alUserNick = p.Nick;
            p.TeleportRpc(ArcLinkStand, ArcLinkYaw);
            // Always a fresh puzzle; if the next one is not ready yet the screen boots until it is (UpdateArcLink deals it).
            _alPuzzle = null;
            _alBoard = null;
            _alSolvedAt = -1;
            ArcLinkSolvedTime.Value = 0f;
            ArcLinkPuzzleNet.Value = default;
            ArcLinkPathsNet.Value = default;
            if (_alReady != null) ArcLinkDeal();
            else if (_alTask == null) ArcLinkGenerate();
            Debug.Log($"[ArcLink] {p.Nick} at the cabinet{(_alPuzzle == null ? " (waiting for a puzzle)" : "")}");
        }

        /// <summary>Server: a line of the user's drag (<paramref name="final"/> = mouse released).</summary>
        public void ServerArcLinkPath(NetPlayer p, byte color, string cells, byte dragId, bool final)
        {
            if (!IsServer || p == null || ArcLinkUser.Value != p.NetworkObjectId || _alBoard == null || _alBoard.SolvedFlag) return;
            if (color >= _alPuzzle.Colors) return;
            var list = DotLinkBoard.DecodeCells(cells ?? "");
            if (list == null) return;
            if (_alDragBase == null || dragId != _alDragId)
            {
                _alDragBase = _alBoard.Clone();
                _alDragId = dragId;
            }
            var next = _alDragBase.Clone();
            if (!next.SetPath(color, list)) return;
            next.Seq = _alBoard.Seq;
            _alBoard.CopyFrom(next);
            if (final) _alDragBase = null;
            if (_alBoard.Solved) ArcLinkSolved(p);
            else ArcLinkPublish();
        }

        /// <summary>Server: right mouse button on a colour = its line is wiped.</summary>
        public void ServerArcLinkClear(NetPlayer p, byte color)
        {
            if (!IsServer || p == null || ArcLinkUser.Value != p.NetworkObjectId || _alBoard == null || _alBoard.SolvedFlag) return;
            if (color >= _alPuzzle.Colors || _alBoard.Path(color).Count == 0) return;
            _alBoard.Clear(color);
            _alDragBase = null;
            ArcLinkPublish();
        }

        void ArcLinkSolved(NetPlayer p)
        {
            _alBoard.SolvedFlag = true;
            _alDragBase = null;
            _alSolvedAt = Now;
            float time = (float)(Now - ArcLinkStartAt.Value);
            ArcLinkSolvedTime.Value = Mathf.Max(0.1f, time);
            bool record = ArcLinkBest.Value <= 0f || time < ArcLinkBest.Value;
            if (record)
            {
                ArcLinkBest.Value = ArcLinkSolvedTime.Value;
                ArcLinkBestNick.Value = new FixedString64Bytes(p.Nick.Length > 30 ? p.Nick.Substring(0, 30) : p.Nick);
            }
            ArcLinkPublish();
            Debug.Log($"[ArcLink] SOLVED by {p.Nick} in {time:0.0} s{(record ? " - new record" : "")} (seed {_alPuzzle.Seed})");
        }

        /// <summary>"1:23" from seconds.</summary>
        public static string ArcLinkClock(double seconds)
        {
            int s = Mathf.Max(0, (int)seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
