using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Fuses on the player (escape objective, MatchManager.Fuses): which marks they carry (<see cref="FuseMask"/>, bit =
    /// mark, every mark is its own inventory item / slot) and the socket they are working on (<see cref="FuseWork"/>,
    /// putting a fuse in or taking one out - played by every client from the server clock, late joiners too).
    /// Both written by the server.
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<byte> FuseMask = new();
        public readonly NetworkVariable<FuseWorkState> FuseWork = new(FuseWorkState.None);

        /// <summary>Busy at a fuse box (no item in the hand, nothing else meanwhile).</summary>
        public bool FuseHandsBusy => FuseWork.Value.Active;

        public bool HasFuse(int symbol) => symbol >= 0 && symbol < 8 && (FuseMask.Value & (1 << symbol)) != 0;
        public int FuseCount => FuseItems.CountBits(FuseMask.Value);

        /// <summary>Seconds into the current fuse work (-1 = none).</summary>
        public float FuseWorkTime
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm == null || !FuseWork.Value.Active) return -1f;
                return Mathf.Max(0f, (float)(mm.Now - FuseWork.Value.Start));
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestFuseInsertRpc(byte box, byte socket) => MatchManager.Instance?.ServerFuseInsert(this, box, socket);

        [Rpc(SendTo.Server)]
        public void RequestFuseRemoveRpc(byte box, byte socket) => MatchManager.Instance?.ServerFuseRemove(this, box, socket);

        [Rpc(SendTo.Server)]
        public void RequestFuseCancelRpc() => MatchManager.Instance?.ServerFuseCancel(this);
    }
}
