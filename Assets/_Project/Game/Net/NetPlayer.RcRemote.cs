using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// "Pilot RC" on the player (state: <see cref="MatchManager.RcRemoteSlotOf"/>): <see cref="RcRemoteOut"/> = its
    /// slot is selected, so everybody sees it in both hands (third person) and the owner in first person; and the smash
    /// after the car was destroyed - started on every peer by the slot change to <see cref="RcRemoteFate.Thrown"/>.
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<bool> RcRemoteOut = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private bool _remoteThrowing;

        /// <summary>This player has the transmitter of a car (in the inventory).</summary>
        public bool HasRcRemote => MatchManager.Instance != null && MatchManager.Instance.RcRemoteSlotOf(this) >= 0;

        /// <summary>The transmitter is in the hands (selected, alive hider, round running, not driving / smashing it).</summary>
        public bool RcRemoteShown
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm == null || !RcRemoteOut.Value || !IsHider || Life.Value != LifeState.Alive || _remoteThrowing || RcAnimating) return false;
                var ph = mm.Phase.Value;
                return (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) && HasRcRemote;
            }
        }

        /// <summary>Called from Update on every peer.</summary>
        private void UpdateRcRemote()
        {
            if (IsOwner)
            {
                // Selected (also while sitting down to steer / getting up: the RC pose keeps it in the hands then).
                bool want = Hotbar.Active(this) && IsAliveHider && !Busy.Value && Seat.Value < 0 && !WireHandsBusy &&
                            Hotbar.ItemAt(this, Hotbar.Selected(this)) == HotbarItem.RcRemote;
                if (RcRemoteOut.Value != want) RcRemoteOut.Value = want;
            }
            bool shown = RcRemoteShown;
            bool selected = RcRemoteOut.Value && IsHider && Life.Value == LifeState.Alive && !_remoteThrowing && HasRcRemote;
            (Actions as BlockyCharacterActions)?.SetRcRemoteHeld(shown, selected);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetRcRemoteHeld(shown);
        }

        /// <summary>Every peer: the transmitter of car <paramref name="slot"/> is smashed (from <see cref="OnRcSlotChanged"/>).</summary>
        private void BeginRemoteThrow(int slot, in RcSlotState s)
        {
            RcRemoteFx.Ensure();
            RcRemoteFx.Instance.OnThrow(this, slot, s.RemotePos);
            (Actions as BlockyCharacterActions)?.PlayRemoteThrow(s.RemotePos, s.RemoteYaw);
            StartCoroutine(RemoteThrowClock(s.RemotePos));
        }

        /// <summary>Clock of the smash; the owner also gets the first-person hands, the look down at the floor and a kick.</summary>
        private IEnumerator RemoteThrowClock(Vector3 land)
        {
            _remoteThrowing = true;
            var fpv = IsOwner ? ViewModel as FirstPersonViewModel : null;
            float t = 0f;
            bool kicked = false;
            while (t < RcRemoteTimeline.End)
            {
                if (IsOwner)
                {
                    fpv?.SetRemoteThrow(t);
                    if (t >= RcRemoteTimeline.ThrowStart && t < RcRemoteTimeline.End - 0.3f)
                    {
                        Vector3 d = land - EyePosition;
                        float want = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -55f, -28f);
                        Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, want, Time.deltaTime * 190f));
                    }
                    if (!kicked && t >= RcRemoteTimeline.Land)
                    {
                        kicked = true;
                        GameHud.Kick();
                    }
                }
                yield return null;
                t += Time.deltaTime;
                if (Life.Value != LifeState.Alive) break;
            }
            if (IsOwner) fpv?.SetRemoteThrow(-1f);
            _remoteThrowing = false;
        }
    }
}
