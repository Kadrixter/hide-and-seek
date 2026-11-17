using Unity.Netcode.Components;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Client-authoritative transform: the owning player moves locally, everyone else interpolates.</summary>
    [DisallowMultipleComponent]
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
