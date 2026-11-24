using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Stage of the hiders' escape objective in a round (only ever goes forward, reset at round start).</summary>
    public enum EscapeStage : byte
    {
        /// <summary>Find the fuses and fill the right fuse box (one per floor, only one is "the" box).</summary>
        Fuses = 0,
        /// <summary>The right box is full: the dot link panel at the broken lever (exit gate box) is open.</summary>
        DotLink = 1,
        /// <summary>The dot link is solved: the exit gate is slowly opening.</summary>
        GateOpening = 2,
        /// <summary>The gate is open: hiders run past the parking boundary to escape.</summary>
        GateOpen = 3,
    }

    /// <summary>
    /// Shared contract of the escape objective (2026-11-24). Three parts, each in its own files:
    /// fuses + fuse boxes advance Fuses -> DotLink, the dot link panel advances DotLink -> GateOpening,
    /// the exit gate / race / escape part advances GateOpening -> GateOpen and handles the escaped hiders and the win.
    /// Every part reads <see cref="Escape"/> and calls <see cref="ServerEscapeAdvance"/>; nobody sets the variable directly.
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<EscapeStage> Escape = new(EscapeStage.Fuses);

        /// <summary>Server: moves the objective forward to <paramref name="to"/> (ignored if it is not ahead).</summary>
        public void ServerEscapeAdvance(EscapeStage to)
        {
            if (!IsServer || to <= Escape.Value) return;
            Debug.Log($"[Escape] stage {Escape.Value} -> {to}");
            Escape.Value = to;
        }

        /// <summary>Server, at round start (before Loading).</summary>
        private void ResetEscape() => Escape.Value = EscapeStage.Fuses;
    }
}
