using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Tuning of the chainsaw nerf (2026-11-25).</summary>
    public static class SawTuning
    {
        /// <summary>Seeker movement while the saw is in his hands (running or not): -15%.</summary>
        public const float HoldSpeedScale = 0.85f;
        /// <summary>Pull-start: chance that it takes 2 tries / 4 tries (else 1), rolled by the server at every start.</summary>
        public const float TwoTries = 0.25f, FourTries = 0.10f;
        /// <summary>The pull begins this long after the saw comes out (the arms raise it first).</summary>
        public const float PullDelay = 0.25f;
        /// <summary><see cref="NetPlayer.SawStartAt"/> while no start is under way (far future: never "ready" by mistake).</summary>
        public const double NotStarted = 1e9;
        /// <summary>A hider takes the saw out of the hands of a knocked-out / blinded seeker from this far (feet, flat; and height).</summary>
        public const float WrestRange = 2.1f, WrestHeight = 1.2f, WrestPromptRange = 2.8f;
        /// <summary>Fuel canisters put on the map each round (on different levels while possible).</summary>
        public const int Cans = 2;
        /// <summary>Hiders on the seeker's level see the running saw on the minimap this long after the throttle is let go.</summary>
        public const float MapLinger = 1.2f;
    }

    /// <summary>
    /// The chainsaw nerf on the player: the seeker is slower while the saw runs (<see cref="SawSpeedScale"/>), the saw
    /// stalls while he cannot attack (dust / goo / foam in the eyes, knocked out: <see cref="SawStalled"/>) and a hider
    /// can wrest it out of his hands meanwhile (<see cref="SawWrestable"/>, MatchManager.SawWrest.cs); hiders never start
    /// it. Fuel canisters (<see cref="FuelCans"/>, ItemDefs.FuelCan.cs) fill the saw up again.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Fuel canisters in the inventory (0 or 1).</summary>
        public readonly NetworkVariable<byte> FuelCans = new();
        /// <summary>Server time the current pull-start began (saw drawn / back after a stall) and how many tries it needs.</summary>
        public readonly NetworkVariable<double> SawStartAt = new(SawTuning.NotStarted);
        public readonly NetworkVariable<byte> SawAttempts = new(1);

        /// <summary>The saw is in the seeker's hands (whether it runs or not).</summary>
        public bool SawInHands => IsSeeker && HasChainsaw.Value && Held.Value == HeldItem.Chainsaw && Life.Value == LifeState.Alive;

        /// <summary>Seconds into the current pull-start (synced clock; large when it started long ago).</summary>
        public float SawStartElapsed
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm == null ? 99f : (float)System.Math.Min(99d, mm.Now - SawStartAt.Value);
            }
        }

        /// <summary>The engine has caught after the pull-start (F may rev it, it may cut).</summary>
        public bool SawReady => SawInHands && !SawStalled && SawStartElapsed >= HideAndSeek.Characters.BlockyProps.ChainsawFireAt(SawAttempts.Value);

        /// <summary>The saw in the hands runs at full throttle (seen by everybody; the minimap of the hiders shows it).</summary>
        public bool SawRunning => IsSeeker && HasChainsaw.Value && Held.Value == HeldItem.Chainsaw && SawRev.Value && Life.Value == LifeState.Alive;

        /// <summary>Seeker movement multiplier from the saw in the hands (1 = no saw in the hands).</summary>
        public float SawSpeedScale => SawInHands && !Busy.Value ? SawTuning.HoldSpeedScale : 1f;

        /// <summary>Engine dead: the seeker cannot attack now (blinded, gooed, foamed, knocked out).</summary>
        public bool SawStalled => IsSeeker && CannotAttack;

        /// <summary>A hider may take the saw from this seeker now: he has it and is knocked out or has something in the eyes.</summary>
        public bool SawWrestable
        {
            get
            {
                if (!IsSeeker || !HasChainsaw.Value || Life.Value != LifeState.Alive) return false;
                var mm = MatchManager.Instance;
                double now = mm != null ? mm.Now : 0d;
                bool eyes = now < BlindUntil.Value || Gooed || Foamed;
                return Stunned || (eyes && !Busy.Value); // busy without the stun = in the middle of a kill
            }
        }

        /// <summary>Owner (hider): take the chainsaw from the seeker <paramref name="seekerId"/>.</summary>
        [Rpc(SendTo.Server)]
        public void RequestWrestSawRpc(ulong seekerId) => MatchManager.Instance?.ServerWrestSaw(this, Find(seekerId));

        /// <summary>Owner (seeker): R with the saw in the hands - pour the canister from the inventory into it.</summary>
        [Rpc(SendTo.Server)]
        public void RequestRefuelHandsRpc() => MatchManager.Instance?.ServerRefuelFromHands(this);

        /// <summary>Owner (seeker): pour the fuel canister into the chainsaw.</summary>
        [Rpc(SendTo.Server)]
        public void RequestRefuelRpc() => MatchManager.Instance?.ServerRefuelSaw(this);
    }
}
