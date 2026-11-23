using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>Slate wipe (new round) and the seated poses of the tic-tac-toe players.</summary>
    public sealed partial class TttTable
    {
        void BeginWipe()
        {
            _wipeFrom = Model.SlateTex.GetPixels();
            var clean = (Color[])_wipeFrom.Clone();
            // The clean slate with the grid, as ClearSlate makes it.
            Model.ClearSlate();
            _wipeTo = Model.SlateTex.GetPixels();
            Model.SlateTex.SetPixels(clean);
            Model.SlateTex.Apply(true);
            _wipeApplied = 0f;
        }

        /// <summary>Eraser position (slate-local) during the wipe: zigzag across the slate, from the far side to the near one.</summary>
        Vector3 EraserAt(Anim a)
        {
            float half = TttTableModel.SlateSize * 0.36f;
            Vector3 home = _eraserHome;
            if (a.T < EReach) return home;
            if (a.T < EReach + EWipe)
            {
                float k = (a.T - EReach) / EWipe;
                float x = Mathf.Sin(k * Mathf.PI * 5f) * half;
                float z = Mathf.Lerp(half, -half, k) * -Side(a.Seat);
                Vector3 p = new Vector3(x, TttTableModel.SlateTop, z);
                if (k < 0.1f) return Vector3.Lerp(home, p, k / 0.1f);
                return p;
            }
            float b = Mathf.Clamp01((a.T - EReach - EWipe) / EBack);
            return Vector3.Lerp(new Vector3(0f, TttTableModel.SlateTop, half * Side(a.Seat)), home, Mathf.SmoothStep(0f, 1f, b));
        }

        void TickErase(Anim a)
        {
            if (Model.Eraser == null) return;
            if (a.T >= EReach * 0.8f && a.T < EReach + EWipe + EBack)
            {
                Model.Eraser.localPosition = EraserAt(a);
                Model.Eraser.localRotation = Quaternion.Euler(0f, 90f + Mathf.Sin(a.T * 9f) * 8f, 0f);
            }
            float k = Mathf.Clamp01((a.T - EReach) / EWipe);
            if (k > 0f && k - _wipeApplied > 0.08f && _wipeFrom != null)
            {
                _wipeApplied = k;
                var px = new Color[_wipeFrom.Length];
                float w = Mathf.SmoothStep(0f, 1f, k);
                for (int i = 0; i < px.Length; i++) px[i] = Color.LerpUnclamped(_wipeFrom[i], _wipeTo[i], w);
                Model.SlateTex.SetPixels(px);
                Model.SlateTex.Apply(true);
            }
            if (a.T >= EReach && a.T < EReach + 0.05f) MiniGameSounds.Erase(this);
        }

        protected override void PoseInputs(int seat, out Vector3 reach, out float reachW, out float think, out Vector3 look, out float lean)
        {
            reach = HandRest(seat);
            reachW = 0f;
            bool myTurn = State.Phase == TttState.Playing && State.Turn == seat && !Animating;
            think = myTurn ? 1f : 0.2f;
            lean = myTurn ? 6f : 0f;
            look = Centre;
            var a = _cur;
            if (a == null) return;
            if (a.Seat != seat)
            {
                look = transform.TransformPoint(a.Erase ? EraserAt(a) : TipAt(a, out _));
                return;
            }
            think = 0f;
            if (a.Erase)
            {
                Vector3 e = EraserAt(a) + Vector3.up * 0.075f;
                reach = transform.TransformPoint(e);
                float end = EReach + EWipe + EBack;
                reachW = a.T < EReach ? Mathf.SmoothStep(0f, 1f, a.T / EReach) : a.T < end ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (a.T - end) / THome);
                look = reach;
                return;
            }
            Vector3 tip = TipAt(a, out _);
            Vector3 back = new Vector3(0f, 0.9f, Side(seat) * 0.45f).normalized;
            Vector3 fist = tip + back * (ChalkLen + 0.03f);
            Vector3 restFist = TttTableModel.ChalkRest(seat) + Vector3.up * 0.06f;
            if (a.T < TPick)
            {
                reach = transform.TransformPoint(restFist);
                reachW = Mathf.SmoothStep(0f, 1f, a.T / TPick);
            }
            else if (a.T < a.PutEnd)
            {
                reach = transform.TransformPoint(a.T < TPick + 0.1f ? restFist : fist);
                reachW = 1f;
            }
            else
            {
                reach = transform.TransformPoint(restFist);
                reachW = 1f - Mathf.SmoothStep(0f, 1f, (a.T - a.PutEnd) / THome);
            }
            look = transform.TransformPoint(tip);
        }
    }
}
