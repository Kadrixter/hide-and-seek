using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Changing the seated pose while sitting (pose wheel, <see cref="PoseWheel"/>): the owner asks the server, the
    /// server checks that the player sits in the lobby and that the pose exists on that seat and is free to take
    /// (<see cref="SitPoseRules"/>), then lets the owner write <see cref="SitPoseVar"/> (an owner-written variable, also
    /// set on every sit-down). Every client then moves the body to the new pose without standing up
    /// (<see cref="HideAndSeek.Characters.BlockyCharacterActions.SitPoseBlendTime"/>).
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Owner: asks for pose <paramref name="id"/> on the current seat. False when it is not possible right now.</summary>
        public bool RequestSitPose(byte id)
        {
            if (!IsOwner || !IsSpawned || Seat.Value < 0) return false;
            if (id == SitPoseVar.Value) return true;
            if (!SitPoseRules.Allowed(LobbySeat.Get(Seat.Value), id, this)) return false;
            RequestSitPoseRpc(id, Seat.Value);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void RequestSitPoseRpc(byte id, sbyte seat, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            var mm = MatchManager.Instance;
            bool ok = mm != null && mm.Phase.Value == MatchPhase.Lobby && Life.Value == LifeState.Alive && !Busy.Value
                      && Seat.Value >= 0 && Seat.Value == seat && SitPoseRules.Allowed(LobbySeat.Get(seat), id, this);
            if (!ok)
            {
                Debug.Log($"[SeatPose] {Nick}: pose {id} on seat {seat} refused (seat now {Seat.Value})");
                SitPoseRefusedRpc(id);
                return;
            }
            SitPoseGrantedRpc(id, seat);
        }

        [Rpc(SendTo.Owner)]
        private void SitPoseGrantedRpc(byte id, sbyte seat)
        {
            // Stood up / moved to another seat meanwhile: the next sit-down picks its own pose.
            if (Seat.Value != seat) return;
            SitPoseVar.Value = id;
            Debug.Log($"[SeatPose] {Nick}: pose {id} on seat {seat}");
        }

        [Rpc(SendTo.Owner)]
        private void SitPoseRefusedRpc(byte id)
        {
            if (Seat.Value < 0) return;
            GameHud.Toast("Tej pozy nie da się teraz przyjąć.", 2f);
            GameAudio.Play2D(GameSfx.UiError, 0.5f);
        }
    }
}
