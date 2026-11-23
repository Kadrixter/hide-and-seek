using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Chalk animations on the wall board: the player steps up, takes the stick off the ledge, draws the mark stroke
    /// by stroke (the texture fills in behind the tip, scratch sound), crosses out the winning line, puts the chalk
    /// back and steps back. Wiping: the eraser is taken off the ledge and rubbed over the slate in a zigzag while the
    /// chalk fades under it.
    /// </summary>
    public sealed partial class TttTable
    {
        const float TPick = 0.6f, TToStart = 1.0f, TDraw = 1.0f, TLine = 0.75f, TPut = 0.55f, THome = 0.45f;
        const float EReach = 0.55f, EWipe = 1.4f, EBack = 0.5f;
        const float ChalkLen = 0.085f;
        /// <summary>Chalk held like a pen: from the tip back towards the player and a little down.</summary>
        static readonly Vector3 HoldDir = new Vector3(0f, -0.55f, 0.83f).normalized;

        sealed class Anim
        {
            public bool Erase;
            public int Cell, Seat, Line, Seed;
            public char Mark;
            public float T, Drawn, LineDrawn;
            public float DrawEnd => TToStart + TDraw;
            public float LineEnd => DrawEnd + (Line >= 0 ? TLine : 0f);
            public float PutEnd => LineEnd + TPut;
            public float Dur => Erase ? EReach + EWipe + EBack + THome : PutEnd + THome;
        }

        readonly List<Anim> _queue = new();
        Anim _cur;
        AudioSource _scratch;
        Color[] _wipeFrom;
        float _wipeApplied;
        Transform _hoverMark;
        public bool EndSoundPending;

        public bool Animating => _cur != null || _queue.Count > 0;

        void InitAnim()
        {
            _scratch = gameObject.AddComponent<AudioSource>();
            _scratch.clip = MiniGameSounds.ChalkLoop;
            _scratch.loop = true;
            _scratch.spatialBlend = 1f;
            _scratch.minDistance = 1f;
            _scratch.maxDistance = 10f;
            _scratch.rolloffMode = AudioRolloffMode.Linear;
            _scratch.volume = 0.35f;
            _scratch.playOnAwake = false;
            var shader = Resources.Load<Shader>("Shaders/HnsBoardMark");
            var mat = shader != null ? new Material(shader) : new Material(Model.Kit.Template);
            mat.mainTexture = MarkArt.SoftSquare;
            mat.color = new Color(0.9f, 0.95f, 0.85f, 0.16f);
            _hoverMark = Model.Kit.Part("HoverCell", transform, ChessTableModel.Quad(TttTableModel.Cell * 0.92f, TttTableModel.Cell * 0.92f), mat,
                Vector3.zero, Quaternion.Euler(0f, 180f, 0f), false).transform;
            _hoverMark.gameObject.SetActive(false);
        }

        void UpdateHover()
        {
            bool on = Hover >= 0;
            if (_hoverMark.gameObject.activeSelf != on) _hoverMark.gameObject.SetActive(on);
            if (on) _hoverMark.localPosition = TttTableModel.CellLocal(Hover) + Vector3.forward * 0.0015f;
        }

        void QueueMark(int cell, char mark, int seat, int line) =>
            _queue.Add(new Anim { Cell = cell, Mark = mark, Seat = seat, Line = line, Seed = cell * 7 + 3 });

        void QueueErase(int seat) => _queue.Add(new Anim { Erase = true, Seat = seat });

        void CancelAnims()
        {
            _queue.Clear();
            _cur = null;
            if (_scratch != null) _scratch.Stop();
            ResetProps();
            EndSoundPending = false;
        }

        void ResetProps()
        {
            if (Model.ChalkA != null) Model.ChalkA.SetLocalPositionAndRotation(TttTableModel.ChalkRest(0), TttTableModel.ChalkRestRot);
            if (Model.ChalkB != null) Model.ChalkB.SetLocalPositionAndRotation(TttTableModel.ChalkRest(1), TttTableModel.ChalkRestRot);
            if (Model.Eraser != null) Model.Eraser.SetLocalPositionAndRotation(TttTableModel.EraserRest, Quaternion.identity);
        }

        Transform Chalk(int seat) => seat == 0 ? Model.ChalkA : Model.ChalkB;

        void TickAnim(float dt)
        {
            if (_cur == null)
            {
                if (_queue.Count == 0) return;
                _cur = _queue[0];
                _queue.RemoveAt(0);
                if (_cur.Erase) BeginWipe();
            }
            var a = _cur;
            a.T += dt * (_queue.Count > 0 ? 2.2f : 1f);
            if (a.Erase) TickErase(a);
            else TickMark(a);
            if (a.T >= a.Dur)
            {
                _cur = null;
                _scratch.Stop();
                ResetProps();
                if (a.Erase) Model.ClearSlate();
                if (_queue.Count == 0 && EndSoundPending)
                {
                    EndSoundPending = false;
                    MiniGameSounds.PhaseChange(this, TttState.Playing, TttState.Over, State.End);
                }
            }
        }

        /// <summary>Chalk tip (board-local) and whether it touches the slate, at the animation time.</summary>
        Vector3 TipAt(Anim a, out bool touching)
        {
            touching = false;
            Vector3 rest = TttTableModel.ChalkRest(a.Seat);
            Vector3 start = TttChalk.PenAt(a.Cell, a.Mark, 0f, a.Seed, out _) + Vector3.forward * 0.03f;
            if (a.T < TPick) return rest;
            if (a.T < TToStart) return Vector3.Lerp(rest + Vector3.forward * 0.05f, start, Mathf.SmoothStep(0f, 1f, (a.T - TPick) / (TToStart - TPick)));
            if (a.T < a.DrawEnd)
            {
                float k = (a.T - TToStart) / TDraw;
                var p = TttChalk.PenAt(a.Cell, a.Mark, k, a.Seed, out touching);
                return touching ? p : p + Vector3.forward * 0.01f;
            }
            if (a.Line >= 0 && a.T < a.LineEnd)
            {
                TttChalk.WinLine(a.Line, out var l0, out var l1);
                float k = (a.T - a.DrawEnd) / TLine;
                if (k < 0.2f) return Vector3.Lerp(TttChalk.PenAt(a.Cell, a.Mark, 1f, a.Seed, out _) + Vector3.forward * 0.03f, l0, k / 0.2f);
                touching = true;
                return Vector3.Lerp(l0, l1, (k - 0.2f) / 0.8f);
            }
            float b = Mathf.Clamp01((a.T - a.LineEnd) / TPut);
            Vector3 from = a.Line >= 0 ? TttChalk.WinLineEnd(a.Line) : TttChalk.PenAt(a.Cell, a.Mark, 1f, a.Seed, out _);
            return Vector3.Lerp(from + Vector3.forward * 0.03f, rest, Mathf.SmoothStep(0f, 1f, b));
        }

        void TickMark(Anim a)
        {
            var chalk = Chalk(a.Seat);
            Vector3 tip = TipAt(a, out bool touching);
            bool held = a.T >= TPick * 0.85f && a.T < a.PutEnd - 0.05f;
            if (chalk != null && held) chalk.SetLocalPositionAndRotation(tip, Quaternion.FromToRotation(Vector3.up, HoldDir));
            // Fill the texture up to the current progress, in steps of a few percent (the upload is the expensive part).
            if (a.T >= TToStart)
            {
                float k = Mathf.Clamp01((a.T - TToStart) / TDraw);
                if (k - a.Drawn >= 0.03f || k >= 1f && a.Drawn < 1f) { TttChalk.DrawMark(Model, a.Cell, a.Mark, k, a.Seed, a.Drawn); a.Drawn = k; Model.SlateTex.Apply(true); }
            }
            if (a.Line >= 0 && a.T >= a.DrawEnd + TLine * 0.2f)
            {
                float k = Mathf.Clamp01((a.T - a.DrawEnd - TLine * 0.2f) / (TLine * 0.8f));
                if (k - a.LineDrawn >= 0.03f || k >= 1f && a.LineDrawn < 1f) { TttChalk.DrawWinLine(Model, a.Line, a.LineDrawn, k); a.LineDrawn = k; Model.SlateTex.Apply(true); }
            }
            if (touching && !_scratch.isPlaying) _scratch.Play();
            else if (!touching && _scratch.isPlaying) _scratch.Pause();
        }
    }
}
