using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>Slate wipe and the standing poses of the two players at the chalkboard.</summary>
    public sealed partial class TttTable
    {
        void BeginWipe()
        {
            _wipeFrom = Model.SlateTex.GetPixels();
            if (Model.CleanPixels == null) { Model.ClearSlate(); Model.SlateTex.SetPixels(_wipeFrom); }
            _wipeApplied = 0f;
        }

        /// <summary>Eraser position (board-local, felt against the slate) during the wipe: a zigzag top to bottom.</summary>
        Vector3 EraserAt(Anim a, out bool onBoard)
        {
            onBoard = false;
            float halfX = TttTableModel.SlateW * 0.36f, top = TttTableModel.SlateH * 0.36f;
            Vector3 home = TttTableModel.EraserRest;
            Vector3 first = new Vector3(-halfX, top, TttTableModel.SlateZ + 0.001f);
            if (a.T < EReach) return home;
            if (a.T < EReach + EWipe)
            {
                float k = (a.T - EReach) / EWipe;
                Vector3 p = new Vector3(Mathf.Sin(k * Mathf.PI * 4.5f - Mathf.PI * 0.5f) * halfX, Mathf.Lerp(top, -top, k), TttTableModel.SlateZ + 0.001f);
                if (k < 0.08f) return Vector3.Lerp(home + Vector3.forward * 0.04f, first, k / 0.08f);
                onBoard = true;
                return p;
            }
            float b = Mathf.Clamp01((a.T - EReach - EWipe) / EBack);
            return Vector3.Lerp(new Vector3(0f, -top, TttTableModel.SlateZ + 0.04f), home, Mathf.SmoothStep(0f, 1f, b));
        }

        void TickErase(Anim a)
        {
            if (Model.Eraser == null) return;
            if (a.T >= EReach * 0.85f && a.T < EReach + EWipe + EBack)
            {
                Vector3 e = EraserAt(a, out bool onBoard);
                Model.Eraser.localPosition = e;
                // Felt side against the board while wiping, flat on the ledge otherwise.
                float face = Mathf.Clamp01((a.T - EReach) / 0.15f) * Mathf.Clamp01((EReach + EWipe + 0.1f - a.T) / 0.15f);
                Model.Eraser.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(90f, 0f, Mathf.Sin(a.T * 11f) * 10f), face);
                _ = onBoard;
            }
            float k = Mathf.Clamp01((a.T - EReach) / EWipe);
            if (k > 0f && k - _wipeApplied > 0.07f && _wipeFrom != null && Model.CleanPixels != null)
            {
                _wipeApplied = k;
                var clean = Model.CleanPixels;
                var px = new Color[_wipeFrom.Length];
                // Rows above the eraser are already wiped (it travels top to bottom), the rest fades a little.
                int w = TttTableModel.TexW, h = TttTableModel.TexH;
                float edge = 1f - k;
                for (int y = 0; y < h; y++)
                {
                    float rowWiped = Mathf.Clamp01((y / (float)h - edge) * 6f + 0.5f);
                    float amt = Mathf.Max(rowWiped, k * 0.35f);
                    int r = y * w;
                    for (int x = 0; x < w; x++) px[r + x] = Color.LerpUnclamped(_wipeFrom[r + x], clean[r + x], amt);
                }
                Model.SlateTex.SetPixels(px);
                Model.SlateTex.Apply(true);
            }
            if (a.T >= EReach && a.T < EReach + 0.05f) MiniGameSounds.Erase(this);
        }

        /// <summary>Where a player stands this frame: at the own spot, or stepped up in front of what the hand works on.</summary>
        Vector3 StepTo(int seat)
        {
            var a = _cur;
            Vector3 stand = SeatStand(seat);
            if (a == null || a.Seat != seat) return stand;
            float x = a.Erase ? 0f : Mathf.Clamp(TttTableModel.CellLocal(a.Cell).x + (seat == 0 ? 0.12f : -0.12f), -0.4f, 0.4f);
            return transform.TransformPoint(new Vector3(x, TttTableModel.FloorY, 0.42f));
        }

        protected override void PoseInputs(int seat, out Vector3 reach, out float reachW, out float think, out Vector3 look, out float lean)
        {
            reach = Centre;
            reachW = 0f;
            bool myTurn = State.Phase == TttState.Playing && State.Turn == seat && !Animating;
            think = myTurn ? 1f : 0.2f;
            lean = 0f;
            look = Centre;
            var a = _cur;
            if (a == null) return;
            if (a.Seat != seat)
            {
                // Watch the other player's hand.
                look = transform.TransformPoint(a.Erase ? EraserAt(a, out _) : TipAt(a, out _));
                think = 0f;
                return;
            }
            think = 0f;
            if (a.Erase)
            {
                Vector3 e = EraserAt(a, out bool onBoard);
                reach = transform.TransformPoint(e + (onBoard ? Vector3.forward * 0.08f : Vector3.up * 0.07f));
                float end = EReach + EWipe + EBack;
                reachW = a.T < EReach ? Mathf.SmoothStep(0f, 1f, a.T / EReach) : a.T < end ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (a.T - end) / THome);
                look = transform.TransformPoint(e);
                return;
            }
            Vector3 tip = TipAt(a, out _);
            Vector3 fist = tip + HoldDir * (ChalkLen + 0.03f);
            Vector3 restFist = TttTableModel.ChalkRest(seat) + new Vector3(0f, 0.06f, 0.03f);
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
