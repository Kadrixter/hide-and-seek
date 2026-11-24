using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    public static partial class ItemDefs
    {
        /// <summary>
        /// The 8 fuses of the escape objective (MatchManager.Fuses), one item kind per mark so each takes its own slot.
        /// Hiders only (the seeker never picks them up, he can only pull them out of a box). The state is a bit of
        /// <see cref="NetPlayer.FuseMask"/>. No left-click use: E at a fuse box with a free socket of the same mark puts it in.
        /// </summary>
        private static IEnumerable<ItemDef> FuseDefs()
        {
            for (int s = 0; s < FuseSymbols.Count; s++) yield return FuseDef(s);
        }

        private static ItemDef FuseDef(int s)
        {
            int bit = 1 << s;
            return new ItemDef
            {
                Item = FuseItems.Of(s), Name = "Bezpiecznik (" + FuseSymbols.Name(s) + ")", Users = ItemUsers.Hider,
                DropSfx = GameSfx.RcPlace, DropVolume = 0.65f, Glow = new Color(1f, 0.82f, 0.4f, 0.45f),
                Count = p => (p.FuseMask.Value & bit) != 0 ? 1 : 0,
                Give = (p, _) =>
                {
                    if ((p.FuseMask.Value & bit) != 0) return false;
                    p.FuseMask.Value = (byte)(p.FuseMask.Value | bit);
                    return true;
                },
                Take = (NetPlayer p, out byte extra) =>
                {
                    extra = 0;
                    var w = p.FuseWork.Value;
                    if ((p.FuseMask.Value & bit) == 0 || (w.Active && !w.Remove && w.Symbol == s)) return false; // going into a socket right now
                    p.FuseMask.Value = (byte)(p.FuseMask.Value & ~bit);
                    return true;
                },
                Hint = _ => "E przy skrzynce z bezpiecznikami - włóż do gniazda z tym znaczkiem",
                Model = r => FuseModel.Create(r, s).gameObject,
                Hand = FuseGrip(), // own grip per mark: the hand builds this mark's model
            };
        }

        /// <summary>Held by the brass end, the cap up and a marked side towards the face; first person: low right, a marked side to the eye.</summary>
        private static HandGrip FuseGrip() => new()
        {
            Grip = FuseModel.GripPoint * FuseModel.Scale,
            Scale = 1f,
            Hand = new Vector3(0.22f, 0.3f, 0.3f),
            Rotation = HandGrip.Aim(Vector3.forward, new Vector3(-0.25f, 0.85f, 0.45f), Vector3.up, new Vector3(0f, 0.45f, -0.9f)),
            Look = 0.3f,
            FpGrip = FuseModel.GripPoint * FuseModel.Scale,
            FpHand = new Vector3(0.24f, -0.28f, 0.55f),
            FpRotation = HandGrip.Aim(Vector3.forward, new Vector3(-0.42f, 0.88f, 0.2f), Vector3.up, new Vector3(0.05f, 0.15f, -1f)),
            FpScale = 0.7f,
        };
    }
}
