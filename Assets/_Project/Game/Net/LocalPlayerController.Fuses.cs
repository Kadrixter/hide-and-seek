using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local input at the fuse boxes (MatchManager.Fuses / FuseWork): looking at a box picks the socket nearest to the
    /// crosshair. A hider with a fuse of that socket's mark gets "E  włóż bezpiecznik" (a free socket whose mark he
    /// does not have falls back to another free socket he can fill); a filled socket of a box that is not locked gives
    /// "E  przytrzymaj - wyjmij bezpiecznik" (hiders and the seeker; the ring fills, letting go stops it). While working
    /// on a box the hands own the input.
    /// </summary>
    public partial class LocalPlayerController
    {
        private int _fuseBox = -1, _fuseSocket = -1;

        /// <summary>0..1 of the fuse being pulled out by this player (0 = none).</summary>
        public float FuseProgress
        {
            get
            {
                if (_p == null || !_p.FuseWork.Value.Active || !_p.FuseWork.Value.Remove) return 0f;
                return Mathf.Clamp01(_p.FuseWorkTime / FuseTimeline.OutPop);
            }
        }

        /// <summary>Before the other actions: true while working on a fuse box (prompt kept on the socket).</summary>
        private bool FuseTick()
        {
            var w = _p != null ? _p.FuseWork.Value : FuseWorkState.None;
            if (!w.Active) return false;
            var view = FuseBoxViews.Get(w.Box);
            if (view == null) return true;
            Prompt = w.Remove ? PromptKind.FuseRemove : PromptKind.FuseInsert;
            PromptWorld = view.SeatedWorld(w.Socket) + view.Axis * 0.2f;
            PromptText = w.Remove ? "E  przytrzymaj - wyjmij bezpiecznik" : "E  wkładasz bezpiecznik...";
            return true;
        }

        /// <summary>E prompt at the fuse box in front of the player.</summary>
        private bool TryFusePrompt(Vector3 eye, Vector3 fwd, MatchPhase phase, out Vector3 pos, out string text, out float dist, out PromptKind kind)
        {
            pos = default;
            text = null;
            dist = float.MaxValue;
            kind = PromptKind.None;
            var mm = MatchManager.Instance;
            if (mm == null || mm.RaceMode || (phase != MatchPhase.Hiding && phase != MatchPhase.Seeking)) return false;
            if (_p.Life.Value != LifeState.Alive || _p.Busy.Value || _p.Seat.Value >= 0 || _p.RcAnimating || _p.WireHandsBusy) return false;
            bool seeker = _p.IsSeeker;
            if (!seeker && !_p.IsHider) return false;
            for (int i = 0; i < FuseTuning.Boxes; i++)
            {
                var view = FuseBoxViews.Get(i);
                if (view == null) continue;
                var b = view.Box;
                Vector3 front = view.transform.TransformPoint(new Vector3(0f, FuseBoxModel.SocketY, FuseBoxModel.D));
                float d = Vector3.Distance(eye, front);
                if (d > 2.2f || d >= dist || Vector3.Dot(eye - front, view.Axis) < 0.1f) continue;
                if (!InView(eye, fwd, front, 50f, view.transform)) continue;
                int aimed = AimedSocket(view, eye, fwd);
                string t = null;
                PromptKind k = PromptKind.FuseInfo;
                int socket = -1;
                if (b.Locked) t = "E  (skrzynka działa - zablokowana)";
                else if (seeker)
                {
                    socket = b.Has(aimed) ? aimed : NearestFilled(view, b, eye, fwd);
                    if (socket >= 0) { k = PromptKind.FuseRemove; t = "E  przytrzymaj - wyjmij bezpiecznik"; }
                    else if (phase == MatchPhase.Seeking) continue;
                }
                else if (b.Has(aimed)) { socket = aimed; k = PromptKind.FuseRemove; t = $"E  przytrzymaj - wyjmij bezpiecznik ({FuseSymbols.Name(b.Symbol(aimed))})"; }
                else
                {
                    socket = _p.HasFuse(b.Symbol(aimed)) ? aimed : FillableSocket(b);
                    if (socket >= 0) { k = PromptKind.FuseInsert; t = $"E  włóż bezpiecznik ({FuseSymbols.Name(b.Symbol(socket))})"; }
                    else t = _p.FuseCount > 0 ? "E  (żaden twój bezpiecznik tu nie pasuje)" : "E  (brak bezpieczników - znajdź je na parkingu)";
                }
                if (t == null) continue;
                dist = d;
                text = t;
                kind = k;
                pos = socket >= 0 ? view.SeatedWorld(socket) + view.Axis * 0.2f : front + view.Axis * 0.1f;
                _fuseBox = i;
                _fuseSocket = socket;
            }
            if (kind == PromptKind.None) return false;
            dist -= 0.1f; // the box wins over a dropped item lying right under it
            return true;
        }

        /// <summary>Socket nearest to the view ray.</summary>
        private static int AimedSocket(FuseBoxView view, Vector3 eye, Vector3 fwd)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int s = 0; s < FuseTuning.Sockets; s++)
            {
                Vector3 c = view.SeatedWorld(s) + view.Axis * 0.1f;
                float along = Vector3.Dot(c - eye, fwd);
                float off = (c - (eye + fwd * Mathf.Max(0f, along))).magnitude;
                if (off < bd) { bd = off; best = s; }
            }
            return best;
        }

        private static int NearestFilled(FuseBoxView view, in FuseBox b, Vector3 eye, Vector3 fwd)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int s = 0; s < FuseTuning.Sockets; s++)
            {
                if (!b.Has(s)) continue;
                Vector3 c = view.SeatedWorld(s);
                float off = (c - (eye + fwd * Mathf.Max(0f, Vector3.Dot(c - eye, fwd)))).magnitude;
                if (off < bd) { bd = off; best = s; }
            }
            return best;
        }

        private int FillableSocket(in FuseBox b)
        {
            for (int s = 0; s < FuseTuning.Sockets; s++)
                if (!b.Has(s) && _p.HasFuse(b.Symbol(s))) return s;
            return -1;
        }

        private void FusePromptE(bool pressed)
        {
            if (!pressed || _fuseBox < 0 || _fuseSocket < 0 || _p.FuseWork.Value.Active) return;
            if (Prompt == PromptKind.FuseInsert) _p.RequestFuseInsertRpc((byte)_fuseBox, (byte)_fuseSocket);
            else if (Prompt == PromptKind.FuseRemove) _p.RequestFuseRemoveRpc((byte)_fuseBox, (byte)_fuseSocket);
        }
    }
}
