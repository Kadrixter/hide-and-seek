using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    public static partial class ItemDefs
    {
        private static float _nextRefuelTry;

        /// <summary>
        /// Chainsaw fuel canister (2026-11-25): <see cref="SawTuning.Cans"/> lie on the map every round
        /// (MatchManager.SawNerf.cs). The seeker sees them on his minimap, takes one (always, it is his tool) and with
        /// the saw in the inventory LMB fills the saw up to <see cref="SawFuel.Full"/>. Hiders may take one too, only to
        /// hide it from him (thrown away by a hider it is not on his minimap any more).
        /// </summary>
        private static ItemDef FuelCanDef() => new ItemDef
        {
            Item = HotbarItem.FuelCan, Name = "Kanister paliwa", Users = ItemUsers.Both, SeekerTool = true, SeekerMapMarker = true,
            DropSfx = GameSfx.CanBounce, DropVolume = 0.65f, Glow = new Color(1f, 0.3f, 0.2f, 0.45f),
            Count = p => p.FuelCans.Value,
            Give = (p, _) =>
            {
                if (p.FuelCans.Value >= 1) return false;
                p.FuelCans.Value++;
                return true;
            },
            Take = (NetPlayer p, out byte extra) =>
            {
                extra = p.IsHider ? SawFuel.HiderFlag : (byte)0;
                if (p.FuelCans.Value == 0) return false;
                p.FuelCans.Value--;
                return true;
            },
            Hint = p =>
            {
                if (p.IsHider) return "Kanister szukającego do piły - schowaj go (Q - wyrzuć)";
                if (!p.HasChainsaw.Value) return "Kanister do piły - najpierw zdobądź piłę";
                return p.SawFuel.Value >= SawFuel.Full - 0.05f
                    ? "Piła ma pełny bak"
                    : $"LPM - zatankuj piłę (paliwo {Mathf.CeilToInt(p.SawFuel.Value - 0.05f)}/{SawFuel.Full:0})";
            },
            Use = (p, click, _) =>
            {
                if (!click) return;
                if (p.IsHider)
                {
                    GameHud.Toast("To paliwo do piły szukającego. Schowaj kanister, żeby go nie znalazł.", 2.5f);
                    return;
                }
                if (!p.IsSeeker || Time.time < _nextRefuelTry) return;
                _nextRefuelTry = Time.time + 0.6f;
                p.RequestRefuelRpc();
            },
            Model = r => FuelCanModel.Create(r).gameObject,
            Hand = FuelCanGrip,
        };

        /// <summary>By the middle of the three top handles, hanging at the side like a heavy can; first person: low at
        /// the right, the flat side with the embossed X to the eye.</summary>
        private static readonly HandGrip FuelCanGrip = new()
        {
            Grip = FuelCanModel.HandlePoint,
            Hang = true,
            FpGrip = new Vector3(0f, -0.12f, 0f),
            FpHand = new Vector3(0.33f, -0.36f, 0.66f), // QA: the flat side with the X to the eye, the whole can in view
            FpRotation = new Vector3(6f, -22f, 4f),
            FpScale = 0.48f,
        };
    }
}
