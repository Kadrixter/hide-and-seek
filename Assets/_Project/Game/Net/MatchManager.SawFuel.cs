using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Chainsaw fuel (2026-11-24).</summary>
    public static class SawFuel
    {
        /// <summary>A full saw: this many units (= seconds of holding F at the default use of 1 per second). 12 since the 2026-11-25 nerf (was 20).</summary>
        public const float Full = 12f;
        /// <summary>Lobby slider range for the use per second (tests: drain it fast).</summary>
        public const int UseMin = 1, UseMax = 20;
        /// <summary>Bit of the dropped item's extra byte: a hider threw it away (hidden: not on the seeker's minimap).</summary>
        public const byte HiderFlag = 0x80;

        /// <summary>Fuel -> the dropped item's extra byte (0 = unknown = full; 1..121 = fuel in 0.1 steps, + <see cref="HiderFlag"/>).</summary>
        public static byte Pack(float fuel, bool byHider = false) =>
            (byte)((1 + Mathf.Clamp(Mathf.RoundToInt(fuel * 10f), 0, Mathf.RoundToInt(Full * 10f))) | (byHider ? HiderFlag : 0));
        public static float Unpack(byte extra)
        {
            int v = extra & ~HiderFlag;
            return v == 0 ? Full : Mathf.Clamp((v - 1) / 10f, 0f, Full);
        }
        /// <summary>The drop was thrown away by a hider (a seeker item hidden from him: no minimap marker).</summary>
        public static bool ByHider(byte extra) => (extra & HiderFlag) != 0;
    }

    /// <summary>
    /// Chainsaw fuel on the server: a seeker holding the running saw (F, <see cref="NetPlayer.SawRev"/>) burns
    /// <see cref="NetSettings.SawUseRate"/> units a second out of <see cref="SawFuel.Full"/>; an empty saw does not start
    /// (no cutting). A saw from the shelf is full, a dropped one keeps what it had (the drop's extra byte).
    /// </summary>
    public partial class MatchManager
    {
        private float _sawEmptyToastAt;

        /// <summary>Server, every frame.</summary>
        private void UpdateSawFuel()
        {
            if (Phase.Value != MatchPhase.Seeking) return;
            UpdateSawStarts(); // pull-start tries (MatchManager.SawNerf.cs)
            float rate = Settings.Value.SawUseRate;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned || !p.HasChainsaw.Value || p.Held.Value != HeldItem.Chainsaw) continue;
                if (!p.SawRev.Value || p.SawFuel.Value <= 0f) continue;
                float f = Mathf.Max(0f, p.ServerSawFuel - rate * Time.deltaTime);
                p.ServerSawFuel = f;
                // Synced in 0.1 steps (no update every frame).
                if (f <= 0f || Mathf.Abs(p.SawFuel.Value - f) >= 0.1f) p.SawFuel.Value = f;
                if (f <= 0f)
                {
                    Debug.Log($"[Saw] {p.Nick}: the chainsaw ran out of fuel");
                    if (Time.time >= _sawEmptyToastAt)
                    {
                        _sawEmptyToastAt = Time.time + 2f;
                        p.ToastRpc("Piła: koniec paliwa - już nie odpali. Q - wyrzuć ją.", 4f);
                    }
                }
            }
        }

        /// <summary>Server: the saw in the hands of <paramref name="p"/> now has <paramref name="fuel"/>.</summary>
        public static void SetSawFuel(NetPlayer p, float fuel)
        {
            p.ServerSawFuel = Mathf.Clamp(fuel, 0f, SawFuel.Full);
            p.SawFuel.Value = p.ServerSawFuel;
        }
    }
}
