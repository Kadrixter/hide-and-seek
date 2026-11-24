using System;
using Unity.Netcode;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Clock of the round outro (seconds from <see cref="OutroPack.StartAt"/>): fade out of the game, the stage in
    /// front of the closed gate lights up lamp by lamp while the winners walk into a row, the camera travels slowly
    /// along the row (emotes from the K wheel show on the figures), then the award cards, then back to the lobby.
    /// </summary>
    public static class OutroTimeline
    {
        public const float FadeOut = 0.5f, StageIn = 0.55f, Lights = 0.95f, LightStep = 0.16f;
        public const float WalkIn = 0.75f, WalkStagger = 0.17f;
        public const float TitleIn = 1.5f, Travel = 3.4f, Awards = 8.6f, CardStep = 0.22f;
        public const float End = 19.6f; // +5 s on the awards (2026-11-24)
        /// <summary>The server keeps the Ended phase this long (a little after the end, then the lobby loads).</summary>
        public const float Phase = End + 0.3f;
    }

    /// <summary>What the outro shows, sent by the server once when the round ends (nobody joins during a round).</summary>
    public struct OutroPack : INetworkSerializable
    {
        public byte Winner;     // MatchWinner
        public double StartAt;
        public ulong[] Row;     // NetworkObjectIds of the winners, left to right
        public AwardPack[] Awards;
        public byte Survivors, Hiders;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Winner);
            s.SerializeValue(ref StartAt);
            s.SerializeValue(ref Survivors);
            s.SerializeValue(ref Hiders);
            int n = Row?.Length ?? 0;
            s.SerializeValue(ref n);
            if (s.IsReader) Row = new ulong[n];
            for (int i = 0; i < n; i++) s.SerializeValue(ref Row[i]);
            int m = Awards?.Length ?? 0;
            s.SerializeValue(ref m);
            if (s.IsReader) Awards = new AwardPack[m];
            for (int i = 0; i < m; i++) Awards[i].NetworkSerialize(s);
        }

        public bool SeekersWon => Winner == (byte)HideAndSeek.Game.Net.MatchWinner.Seeker;
        public static OutroPack Empty => new OutroPack { Row = Array.Empty<ulong>(), Awards = Array.Empty<AwardPack>() };
    }
}
