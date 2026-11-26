using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine.InputSystem;
using HideAndSeek.Player;

namespace HideAndSeek.Game.Net
{
    /// <summary>Public-address microphone: requests to the server and the owner's talk bar (left mouse button).</summary>
    public partial class NetPlayer
    {
        /// <summary>Test hook (-testpa): the talk bar is held down.</summary>
        public static bool TestPaTalk;

        /// <summary>This player holds a PA microphone.</summary>
        public bool HoldsPaMic => MatchManager.Instance != null && MatchManager.Instance.PaHolder.Value == NetworkObjectId;

        /// <summary>Owner: the talk bar of the held microphone is pressed right now (the voice goes to the speakers).</summary>
        public bool PaTransmitting
        {
            get
            {
                if (!IsOwner || !HoldsPaMic || Life.Value != LifeState.Alive) return false;
                if (TestPaTalk) return true;
                var mouse = Mouse.current;
                if (PadInput.Held(PadAction.Attack) && !LocalPlayerController.MenuOpen && InputReader != null && InputReader.InputEnabled) return true; // pad: R2
                return mouse != null && mouse.leftButton.isPressed && !LocalPlayerController.MenuOpen &&
                       InputReader != null && InputReader.InputEnabled && InputReader.CursorLocked;
            }
        }

        /// <summary>Owner: take the microphone of <paramref name="station"/> (level 0..2) or hang the held one back.</summary>
        [Rpc(SendTo.Server)]
        public void RequestPaRpc(bool take, byte station) => MatchManager.Instance?.ServerPa(this, take, station);
    }
}
