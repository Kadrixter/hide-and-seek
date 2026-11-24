using System.Collections.Generic;
using HideAndSeek.Game.Escape;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// A hider who ran past the parking boundary behind the open exit gate (Game/Escape). Server-written, reset with
    /// the round (<c>MatchManager.ResetPlayer</c>); late joiners get the value with the object. Escaped = out of the
    /// round alive: the body disappears from the map on every peer (renderers off, the own flashlight off), the owner waits in the
    /// limbo under the street (<see cref="EscapeTimeline.Limbo"/>) and watches the others like the dead
    /// (GameHud spectator, LMB / RMB = next player). Not an "alive hider" any more for the rules
    /// (<see cref="IsAliveHider"/>): no hints, no hits, no interactions. The items stay with the player.
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<bool> Escaped = new();

        bool _escApplied;
        readonly List<Renderer> _escHiddenR = new();
        float _escLimboCheck;

        /// <summary>Every peer, every frame (EscapeDirector): applies <see cref="Escaped"/> to the body and the owner's view.</summary>
        public void EscapeTick()
        {
            bool want = Escaped.Value;
            if (want != _escApplied)
            {
                _escApplied = want;
                if (want) EscapeHide();
                else EscapeShow();
            }
            if (want && IsOwner && Time.time >= _escLimboCheck)
            {
                // Keep the owner in the limbo (a teleport that lost the race with a late NetworkTransform update, a fall).
                _escLimboCheck = Time.time + 1f;
                Vector3 lim = EscapeTimeline.Limbo(OwnerClientId);
                if ((transform.position - lim).sqrMagnitude > 4f) TeleportLocal(lim, 0f);
            }
        }

        /// <summary>Server: the carrier runs out with the player in his arms - the carry ends without the put-down.</summary>
        public void ServerEndCarryForEscape() => ServerCarryAbort("escaped through the exit gate");

        void EscapeHide()
        {
            _escHiddenR.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r != null && r.enabled && (Camera == null || !r.transform.IsChildOf(Camera.transform))) { r.enabled = false; _escHiddenR.Add(r); }
            if (IsOwner)
            {
                if (Flashlight != null && Flashlight.IsOn) Flashlight.SetOn(false, false);
                if (FlashOn.Value) FlashOn.Value = false;
                Motor.CommandProvider = null;
                TeleportLocal(EscapeTimeline.Limbo(OwnerClientId), 0f);
                _escLimboCheck = Time.time + 1f;
            }
            Debug.Log($"[Escape] {Nick} escaped: body hidden ({_escHiddenR.Count} renderers){(IsOwner ? ", own view -> spectator" : "")}");
        }

        void EscapeShow()
        {
            foreach (var r in _escHiddenR) if (r != null) r.enabled = true;
            _escHiddenR.Clear();
            Debug.Log($"[Escape] {Nick}: escape cleared, body shown again");
        }
    }
}
