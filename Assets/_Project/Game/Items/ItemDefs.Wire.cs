using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    public static partial class ItemDefs
    {
        /// <summary>
        /// A piece of fence wire torn off a wire-mesh fence (MatchManager.RcWire, hold E at the mesh). On purpose it says
        /// nothing about what it is for: no hint, LMB does nothing. With the RC transmitter in the hands it can replace
        /// the transmitter's antenna (U) - the car's range goes up by half. Dropped / picked up like any other item.
        /// </summary>
        private static ItemDef WireDef() => new ItemDef
        {
            Item = HotbarItem.Wire, Name = "Kawałek drutu", Users = ItemUsers.Hider, MaxStack = WireTuning.MaxCarried,
            DropSfx = GameSfx.ShellCasing, DropVolume = 0.55f, Glow = new Color(0.8f, 0.85f, 0.9f, 0.35f),
            Count = p => p.WireCount.Value,
            Give = (p, _) =>
            {
                if (p.WireCount.Value >= WireTuning.MaxCarried) return false;
                p.WireCount.Value++;
                return true;
            },
            Take = (NetPlayer p, out byte extra) =>
            {
                extra = 0;
                if (p.WireCount.Value == 0 || p.AntennaSwapAt.Value >= 0d) return false; // being plugged in right now
                p.WireCount.Value--;
                return true;
            },
            Hint = _ => "",
            Model = r => WireModel.CreateLoose(r, 0).gameObject,
            Hand = WireGrip,
        };

        /// <summary>Held near its hooked end, the rest sticking out of the fist forward and down; first person: across
        /// the lower right, pointing forward-left with the kinks to the eye.</summary>
        private static readonly HandGrip WireGrip = new()
        {
            Grip = WireModel.GripPoint,
            Hang = true,
            Rotation = new Vector3(-62f, 0f, 0f),
            FpGrip = new Vector3(0f, 0.05f, 0f), // by the torn end: the whole piece sticks up out of the fist
            FpHand = new Vector3(0.28f, -0.36f, 0.56f),
            FpRotation = HandGrip.Aim(Vector3.up, new Vector3(-0.35f, 0.42f, 0.84f), Vector3.right, new Vector3(0.9f, 0.3f, 0.2f)),
            FpScale = 1f,
        };
    }
}
