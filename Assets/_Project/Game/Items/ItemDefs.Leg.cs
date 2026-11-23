using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    public static partial class ItemDefs
    {
        /// <summary>
        /// A leg the chainsaw cut off a hider (SeveredLegs, MatchManager.Legs): any hider can pick it up (E), carry it by
        /// the ankle and throw it (hold LMB = wind-up / power, let go = throw). A hit on the seeker's head or body knocks
        /// him out (NetPlayer.Stun). The seeker cannot take it. Extra / <see cref="NetPlayer.LegCode"/> = whose leg it is.
        /// </summary>
        private static ItemDef SeveredLegDef() => new ItemDef
        {
            Item = HotbarItem.SeveredLeg, Name = "Odcięta noga", Users = ItemUsers.Hider,
            DropSfx = GameSfx.BodyFall, DropVolume = 0.45f,
            Count = p => p.LegCode.Value >= 0 ? 1 : 0,
            Give = (p, extra) =>
            {
                if (p.LegCode.Value >= 0) return false;
                p.LegCode.Value = extra;
                return true;
            },
            Take = (NetPlayer p, out byte extra) =>
            {
                extra = 0;
                if (p.LegCode.Value < 0) return false;
                extra = (byte)p.LegCode.Value;
                p.LegCode.Value = -1;
                return true;
            },
            Hint = p => p.LegCharging ? $"puść LPM - rzut ({Mathf.RoundToInt(p.LegPowerShown * 100f)}%)" : "LPM przytrzymaj - zamach, puść - rzut (ogłusza szukającego)",
            Use = (p, click, hold) => { if (click || hold) p.LegUseHeld(); },
            Model = r => SeveredLegModel.Create(r, null, BloodFx.StumpMaterial, false),
            ModelFor = (r, extra) => SeveredLegs.Model(r, extra),
            FloorText = (p, extra) => Inventory.PickupText(p, HotbarItem.SeveredLeg, "E  podnieś: odciętą nogę (można nią rzucić)"),
        };
    }
}
