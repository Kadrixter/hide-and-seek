using HideAndSeek.Core;
using HideAndSeek.Game.Cinematics;
using HideAndSeek.Game.Escape;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Escape, last part (Game/Escape): the exit gate opens and hiders run out. Server, every frame (EscapeDirector):
    /// <list type="bullet">
    /// <item>Stage GateOpening seen: the gate start time <see cref="EscapeGateAt"/> is stamped (every peer plays the
    /// shutter from it), the seekers get "Brama się otwiera!", everybody's minimap gets the alarm pin (seekers only
    /// draw it). Once a standing player fits under the curtain (<see cref="EscapeTimeline.StandAt"/>) -> GateOpen.</item>
    /// <item>An alive, not carried hider whose feet pass the boundary line behind the gate escapes
    /// (<see cref="NetPlayer.Escaped"/>). A hider carrying an injured / downed hider takes him along: the carry ends
    /// and both escape (a downed one is out of danger: alive again). A carried dead body is put down, it does not count.</item>
    /// <item>A seeker past the line (should not happen - the seekers' wall stops them on their own machine) is put back.</item>
    /// </list>
    /// The win: <see cref="CheckWin"/> (escaped hiders are not on the map; nobody left on the map and somebody got out = hiders).
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Server time the gate started opening (0 = shut / not this round).</summary>
        public readonly NetworkVariable<double> EscapeGateAt = new();

        double _escSeekerBackAt;
        double _escPinAt;

        /// <summary>A hider got out this round (alive): the hiders win when nobody is left on the map.</summary>
        public bool AnyEscaped()
        {
            foreach (var p in NetPlayer.All)
                if (p != null && p.IsHider && p.Escaped.Value && p.Life.Value != LifeState.Dead) return true;
            return false;
        }

        /// <summary>Every peer: minimap alarm pin at the gate every 10 s while the siren sounds (seekers see it).</summary>
        public void EscapeClientTick()
        {
            if (Escape.Value < EscapeStage.GateOpening || EscapeGateAt.Value <= 0d || Phase.Value != MatchPhase.Seeking) return;
            double t = Now - EscapeGateAt.Value;
            if (t < 0d || t > EscapeTimeline.SirenEnd || Now < _escPinAt) return;
            _escPinAt = Now + 10d;
            for (int g = 0; g < ExitGate.Count; g++) AlarmPinged?.Invoke(EscapeTimeline.ToWorld(new Vector3(0f, 0f, 1f), g));
        }

        /// <summary>Server, every frame.</summary>
        public void EscapeServerTick()
        {
            if (!IsServer) return;
            var ph = Phase.Value;
            if (Escape.Value < EscapeStage.GateOpening)
            {
                if (EscapeGateAt.Value != 0d) EscapeGateAt.Value = 0d;
                return;
            }
            if (ph != MatchPhase.Seeking && ph != MatchPhase.Hiding) return;
            if (RaceMode) return;

            if (EscapeGateAt.Value <= 0d)
            {
                EscapeGateAt.Value = Now;
                _escPinAt = 0d;
                Debug.Log($"[Escape] server: gate opening at {Now:0.00} (standing pass in {EscapeTimeline.StandAt:0.0} s, fully up in {EscapeTimeline.FullAt:0.0} s)");
                foreach (var p in NetPlayer.All)
                    if (p != null && p.IsSeeker) p.ToastRpc("Brama się otwiera! Nie pozwól im uciec.", 5f);
            }
            double t = Now - EscapeGateAt.Value;
            if (Escape.Value == EscapeStage.GateOpening && t >= EscapeTimeline.StandAt)
            {
                ServerEscapeAdvance(EscapeStage.GateOpen);
                Debug.Log($"[Escape] server: gate open for walking after {t:0.0} s");
                foreach (var p in NetPlayer.All)
                    if (p != null && p.IsSeeker) p.ToastRpc("Brama otwarta! Chowający mogą uciec.", 4f);
            }
            if (ph != MatchPhase.Seeking) return;
            // Physically the curtain lets a crouching player through a little before GateOpen.
            if (t < EscapeTimeline.CrouchAt - 0.5f) return;

            for (int i = NetPlayer.All.Count - 1; i >= 0; i--)
            {
                var p = i < NetPlayer.All.Count ? NetPlayer.All[i] : null;
                if (p == null || !p.IsSpawned || p.Escaped.Value) continue;
                if (!EscapeTimeline.PastLine(p.transform.position, out int gate)) continue;
                if (p.IsSeeker) { SeekerOverLine(p, gate); continue; }
                if (!p.IsHider || p.Life.Value != LifeState.Alive || p.BodyCarriedNow || p.Busy.Value) continue;
                ServerEscape(p, null);
            }
        }

        void SeekerOverLine(NetPlayer p, int gate)
        {
            if (Now < _escSeekerBackAt) return;
            _escSeekerBackAt = Now + 1d;
            Vector3 back = EscapeTimeline.ToWorld(new Vector3(0f, 0.05f, EscapeTimeline.SeekerWallZ + 1.2f), gate);
            Debug.Log($"[Escape] server: seeker {p.Nick} past the boundary - put back");
            EscapeTimeline.Frame(out _, out var rot, gate);
            p.TeleportRpc(back, Quaternion.LookRotation(rot * Vector3.forward).eulerAngles.y); // facing into the garage
        }

        /// <summary>Server: <paramref name="p"/> is out (and whoever he carries, if alive).</summary>
        void ServerEscape(NetPlayer p, NetPlayer carrier)
        {
            var carried = p.CarriedPlayer;
            if (carried != null) p.ServerEndCarryForEscape();
            try { ServerRcForcePark(p); } catch (System.Exception e) { Debug.LogWarning($"[Escape] RC park of {p.Nick} failed: {e.Message}"); }
            if (p.Life.Value == LifeState.Downed)
            {
                // Carried out downed: out of danger.
                p.Life.Value = LifeState.Alive;
                p.DownedUntil.Value = 0;
            }
            p.Reviving.Value = false;
            p.Escaped.Value = true;
            p.NextHintAt.Value = 0;
            int left = 0;
            foreach (var o in NetPlayer.All)
                if (o != null && o.IsHider && !o.Escaped.Value && o.Life.Value == LifeState.Alive) left++;
            Debug.Log($"[Escape] server: {p.Nick} ESCAPED{(carrier != null ? $" (carried out by {carrier.Nick})" : "")}, hiders left on the map (alive): {left}");
            p.ToastRpc(carrier != null ? $"Uciekłeś! Wyniesiono cię z parkingu ({carrier.Nick}). Oglądasz grę (LPM / PPM - zmiana gracza)."
                : "Uciekłeś! Oglądasz grę (LPM / PPM - zmiana gracza).", 6f);
            foreach (var o in NetPlayer.All)
                if (o != null && o != p && o != carrier) o.ToastRpc($"{p.Nick} jest już poza parkingiem!", 3.5f);
            if (carried != null && carried.IsHider && !carried.Escaped.Value && carried.Life.Value != LifeState.Dead)
                ServerEscape(carried, p);
        }
    }
}
