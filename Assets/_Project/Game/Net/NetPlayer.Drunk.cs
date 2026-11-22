using System;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Cans in the blood at server time <see cref="At"/>; wears off by itself (<see cref="Drunkenness"/>).</summary>
    public struct DrunkState : INetworkSerializable, IEquatable<DrunkState>
    {
        public float Cans;
        public double At;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Cans);
            s.SerializeValue(ref At);
        }

        public bool Equals(DrunkState o) => Cans == o.Cans && At == o.At;
    }

    public partial class NetPlayer
    {
        /// <summary>
        /// Drunk from the lobby vending machine (server-written, everyone reads it): the owner's camera sways
        /// and every client animates the staggering body from the same value.
        /// </summary>
        public readonly NetworkVariable<DrunkState> DrunkVar = new();

        private DrunkVision _drunkFx;

        private double ServerNow => MatchManager.Instance != null ? MatchManager.Instance.Now : 0d;

        /// <summary>Current level in cans (decays over time).</summary>
        public float DrunkLevel => Drunkenness.Level(DrunkVar.Value.Cans, ServerNow - DrunkVar.Value.At);

        /// <summary>0..1 strength of the effect right now (0 after a single can).</summary>
        public float DrunkIntensity => Drunkenness.Intensity(DrunkLevel);

        /// <summary>Server: one more can finished (each one adds to what is left of the previous ones).</summary>
        public void ServerAddDrink(float cans = 1f)
        {
            if (!IsServer) return;
            float level = Mathf.Min(Drunkenness.MaxCans, DrunkLevel + cans);
            DrunkVar.Value = new DrunkState { Cans = level, At = ServerNow };
        }

        /// <summary>Server: sober at once (start of a round, so it never affects the game itself).</summary>
        public void ServerSoberUp()
        {
            if (!IsServer) return;
            if (DrunkVar.Value.Cans != 0f) DrunkVar.Value = default;
        }

        /// <summary>Every client: body sway for everyone, camera and screen effect for the owner.</summary>
        private void UpdateDrunk()
        {
            float k = Life.Value == LifeState.Alive && DrunkVar.Value.Cans > 0f ? DrunkIntensity : 0f;
            float clock = Drunkenness.Clock(ServerNow);
            float seed = Drunkenness.SeedFor(NetworkObjectId);
            (Actions as BlockyCharacterActions)?.SetDrunk(k, clock, seed);
            if (!IsOwner) return;

            if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
            if (_bob != null)
            {
                _bob.Drunk = k;
                _bob.DrunkClock = clock;
                _bob.DrunkSeed = seed;
            }

            float shown = _bob != null ? _bob.DrunkWeight : k;
            if (_drunkFx == null)
            {
                if (shown < 0.002f || Camera == null) return;
                _drunkFx = Camera.GetComponent<DrunkVision>();
                if (_drunkFx == null) _drunkFx = Camera.gameObject.AddComponent<DrunkVision>();
            }
            _drunkFx.Amount = shown;
            _drunkFx.Clock = clock;
            _drunkFx.Seed = seed;
            bool on = shown >= 0.002f;
            if (_drunkFx.enabled != on) _drunkFx.enabled = on;
        }
    }
}
