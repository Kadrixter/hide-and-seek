using System;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public enum MatchPhase : byte
    {
        Lobby = 0,
        Loading = 1,
        Hiding = 2,
        Seeking = 3,
        Ended = 4,
    }

    public enum MatchWinner : byte
    {
        None = 0,
        Seeker = 1,
        Hiders = 2,
    }

    /// <summary>Compact locomotion state sent by the owner ~15 times per second.</summary>
    public struct MotionPack : INetworkSerializable, IEquatable<MotionPack>
    {
        public byte Flags;    // 1 grounded, 2 crouch, 4 run
        public byte Crouch;   // 0..255
        public short Pitch;   // degrees * 100

        public bool Grounded => (Flags & 1) != 0;
        public bool Crouching => (Flags & 2) != 0;
        public bool Running => (Flags & 4) != 0;
        public float CrouchAmount => Crouch / 255f;
        public float PitchDeg => Pitch / 100f;

        public static MotionPack From(ICharacterMotionSource m) => new MotionPack
        {
            Flags = (byte)((m.IsGrounded ? 1 : 0) | (m.IsCrouching ? 2 : 0) | (m.IsRunning ? 4 : 0)),
            Crouch = (byte)Mathf.RoundToInt(Mathf.Clamp01(m.CrouchAmount) * 255f),
            Pitch = (short)Mathf.RoundToInt(Mathf.Clamp(m.LookPitch, -90f, 90f) * 100f),
        };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Flags);
            s.SerializeValue(ref Crouch);
            s.SerializeValue(ref Pitch);
        }

        public bool Equals(MotionPack o) => Flags == o.Flags && Crouch == o.Crouch && Math.Abs(Pitch - o.Pitch) < 50;
    }

    /// <summary>Round settings as sent over the network (mirrors Core.GameSettings).</summary>
    public struct NetSettings : INetworkSerializable, IEquatable<NetSettings>
    {
        public float HideTime;
        public float SeekTime;
        public float HintStart;      // seconds into the seek phase
        public float HintInterval;
        public byte HintMode;        // Core.HintMode flags
        public bool Glock;           // seeker gets a Glock for the last 2 minutes
        public byte Medkits;

        public static NetSettings Default => new NetSettings
        {
            HideTime = 30f,
            SeekTime = 600f,
            HintStart = 300f,
            HintInterval = 60f,
            HintMode = (byte)Core.HintMode.Both,
            Glock = true,
            Medkits = 3,
        };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref HideTime);
            s.SerializeValue(ref SeekTime);
            s.SerializeValue(ref HintStart);
            s.SerializeValue(ref HintInterval);
            s.SerializeValue(ref HintMode);
            s.SerializeValue(ref Glock);
            s.SerializeValue(ref Medkits);
        }

        public bool Equals(NetSettings o) => HideTime == o.HideTime && SeekTime == o.SeekTime && HintStart == o.HintStart &&
                                             HintInterval == o.HintInterval && HintMode == o.HintMode && Glock == o.Glock &&
                                             Medkits == o.Medkits;
    }

    public struct MedkitState : INetworkSerializable, IEquatable<MedkitState>
    {
        public int Id;
        public Vector3 Position;
        public bool Taken;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Position);
            s.SerializeValue(ref Taken);
        }

        public bool Equals(MedkitState o) => Id == o.Id && Taken == o.Taken && Position == o.Position;
    }

    /// <summary>Gameplay constants shared by server and clients.</summary>
    public static class Tuning
    {
        // Knife (seeker catches a hider)
        public const float KnifePromptRange = 4.0f;    // icon appears
        public const float KnifeRange = 3.0f;          // F works
        public const float KnifeServerRange = 4.5f;    // server tolerance (latency)
        public const float KnifeStandOff = 0.85f;      // victim distance during the animation
        public static readonly float[] StabTimes = HideAndSeek.Characters.ActionTimeline.KnifeStabTimes;
        public const float KnifeAttackDuration = HideAndSeek.Characters.ActionTimeline.KnifeAttackDuration;
        public const float KnifeSequenceDuration = HideAndSeek.Characters.ActionTimeline.StabbedReactionDuration + 0.1f; // incl. victim collapse

        // Downed / rescue
        public const float BleedOutTime = 78f;           // 1.3 min
        public const float CrawlTime = 20f;              // downed players can crawl only this long
        public const float RescueRange = 2.2f;
        public const float RescueHoldTime = 3f;
        public const float HealHoldTime = 1.5f;
        public const float PickupRange = 2.0f;
        public const int MaxMedkits = 3;
        public const float GlockHintInterval = 20f;

        // Glock
        public const float GlockAtRemaining = 120f;
        public const int MagSize = 5;
        public const int Mags = 3;                      // 1 loaded + 2 spare
        public const float FireInterval = 0.55f;
        public const float ReloadTime = HideAndSeek.Characters.ActionTimeline.ReloadDuration;
        public const float GlockRange = 70f;

        // Lobby
        public const float ButtonRange = 2.6f;
        public const float EndScreenTime = 9f;
        public const float HintPinTime = 8f;
    }
}
