using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Creates the first-person arms / held item for the local player (Characters module).</summary>
    public static class ViewModelBridge
    {
        public static IFirstPersonViewModel Create(Transform cameraTransform, int skin, string nick)
        {
            return HideAndSeek.Characters.FirstPersonViewModel.Create(cameraTransform, skin, nick);
        }
    }
}
