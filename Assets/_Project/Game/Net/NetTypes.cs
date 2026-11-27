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
        Race = 5,     // RC race mode: countdown + racing
    }

    public enum MatchWinner : byte
    {
        None = 0,
        Seeker = 1,
        Hiders = 2,
        Racer = 3,    // RC race: MatchManager.RaceWinnerId
    }

    /// <summary>Compact locomotion state sent by the owner ~15 times per second.</summary>
    public struct MotionPack : INetworkSerializable, IEquatable<MotionPack>
    {
        public byte Flags;    // 1 grounded, 2 crouch, 4 run, 8 prone
        public byte Crouch;   // 0..255
        public byte Prone;    // 0..255 (lying down for a crawl)
        public short Pitch;   // degrees * 100

        public bool Grounded => (Flags & 1) != 0;
        public bool Crouching => (Flags & 2) != 0;
        public bool Running => (Flags & 4) != 0;
        public bool IsProne => (Flags & 8) != 0;
        public float ProneAmount => Prone / 255f;
        public float CrouchAmount => Crouch / 255f;
        public float PitchDeg => Pitch / 100f;

        public static MotionPack From(ICharacterMotionSource m, bool prone = false, float proneAmount = 0f) => new MotionPack
        {
            Flags = (byte)((m.IsGrounded ? 1 : 0) | (m.IsCrouching ? 2 : 0) | (m.IsRunning ? 4 : 0) | (prone ? 8 : 0)),
            Crouch = (byte)Mathf.RoundToInt(Mathf.Clamp01(m.CrouchAmount) * 255f),
            Prone = (byte)Mathf.RoundToInt(Mathf.Clamp01(proneAmount) * 255f),
            Pitch = (short)Mathf.RoundToInt(Mathf.Clamp(m.LookPitch, -90f, 90f) * 100f),
        };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Flags);
            s.SerializeValue(ref Crouch);
            s.SerializeValue(ref Prone);
            s.SerializeValue(ref Pitch);
        }

        public bool Equals(MotionPack o) => Flags == o.Flags && Crouch == o.Crouch && Prone == o.Prone && Math.Abs(Pitch - o.Pitch) < 50;
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
        public float BleedOut;       // seconds a downed hider survives
        public byte Syringes;        // adrenaline syringes on the map (revive a dead hider)
        public byte Mode;            // GameMode (0 = hide and seek)
        public byte Laps;            // RC race: laps to win
        public bool SeekerTakesItems; // the seeker may pick up hiders' items (medkits, syringes, rods) to deny them
        public byte Seekers;         // killers (seekers) per round, 1..players-1 (0 = old default = 1)
        public byte VoiceMode;       // voice chat: 0 = classic, 1 = low latency (Voice.VoiceMode)
        public ushort VoiceDelayMs;  // voice jitter buffer: classic = start, low latency = target (0 = mode default)
        public bool NoSaw;           // the chainsaw is not on its shelf this round (host option in the lobby)
        public byte SawUse;          // chainsaw fuel used per second of holding F (0 = default 1; SawFuel.Full in a saw)

        /// <summary>Fuel units per second of holding F.</summary>
        public float SawUseRate => SawUse < 1 ? 1f : SawUse;

        /// <summary>Wanted number of seekers (at least 1); the lever refuses a start when it leaves no hider.</summary>
        public int SeekerCount => Seekers < 1 ? 1 : Seekers;

        public static NetSettings Default => new NetSettings
        {
            HideTime = 30f,
            SeekTime = 600f,
            HintStart = 300f,
            HintInterval = 60f,
            HintMode = (byte)Core.HintMode.Both,
            Glock = true,
            Medkits = 3,
            BleedOut = 78f,
            Syringes = 2,
            Mode = (byte)GameMode.HideAndSeek,
            Laps = 3,
            Seekers = 1,
            VoiceMode = 0,
            VoiceDelayMs = 160,
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
            s.SerializeValue(ref BleedOut);
            s.SerializeValue(ref Syringes);
            s.SerializeValue(ref Mode);
            s.SerializeValue(ref Laps);
            s.SerializeValue(ref SeekerTakesItems);
            s.SerializeValue(ref Seekers);
            s.SerializeValue(ref VoiceMode);
            s.SerializeValue(ref VoiceDelayMs);
            s.SerializeValue(ref NoSaw);
            s.SerializeValue(ref SawUse);
        }

        public bool Equals(NetSettings o) => HideTime == o.HideTime && SeekTime == o.SeekTime && HintStart == o.HintStart &&
                                             HintInterval == o.HintInterval && HintMode == o.HintMode && Glock == o.Glock &&
                                             Medkits == o.Medkits && BleedOut == o.BleedOut && Syringes == o.Syringes && Mode == o.Mode && Laps == o.Laps &&
                                             SeekerTakesItems == o.SeekerTakesItems && Seekers == o.Seekers &&
                                             VoiceMode == o.VoiceMode && VoiceDelayMs == o.VoiceDelayMs && NoSaw == o.NoSaw && SawUse == o.SawUse;
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

    /// <summary>RC car state (server-written, one variable per car).</summary>
    public enum RcMode : byte
    {
        Shelf = 0,    // on the shelf in the generator room
        Carried = 1,  // in a hider's inventory (key 6 puts it down)
        Driving = 2,  // on the floor, the holder sits and steers it
        Parked = 3,   // on the floor, nobody steering (6 resumes, E picks it up)
        Doomed = 4,   // the seeker is stomping on it (frozen)
        Wreck = 5,    // crushed: static prop until the round ends
        Shot = 6,     // shot to pieces with the Glock: smoking wreck until the round ends
    }

    /// <summary>What happened to the transmitter of a destroyed car (<see cref="RcSlotState.Remote"/>).</summary>
    public enum RcRemoteFate : byte
    {
        InHand = 0,   // still with the holder (thrown as soon as they can)
        Thrown = 1,   // smashed on the floor (RemoteAt / RemotePos / RemoteYaw)
        Lost = 2,     // the holder died / left before throwing it
    }

    public struct RcSlotState : INetworkSerializable, IEquatable<RcSlotState>
    {
        public RcMode Mode;
        public ulong Holder;   // NetworkObjectId of the hider who has it (ulong.MaxValue = nobody)
        public Vector3 Pos;    // on the floor (parked / wreck / where it was put down)
        public float Yaw;
        // The transmitter (pilot) after the car was destroyed (Wreck / Shot): still in the holder's hand, thrown at the
        // floor at server time RemoteAt (its wreck lies at RemotePos / RemoteYaw until the round ends), or lost.
        public RcRemoteFate Remote;
        public double RemoteAt;
        public Vector3 RemotePos;
        public float RemoteYaw;

        public static RcSlotState OnShelf => new RcSlotState { Mode = RcMode.Shelf, Holder = ulong.MaxValue };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Mode);
            s.SerializeValue(ref Holder);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Remote);
            s.SerializeValue(ref RemoteAt);
            s.SerializeValue(ref RemotePos);
            s.SerializeValue(ref RemoteYaw);
        }

        public bool Equals(RcSlotState o) => Mode == o.Mode && Holder == o.Holder && Pos == o.Pos && Yaw == o.Yaw &&
                                             Remote == o.Remote && RemoteAt == o.RemoteAt && RemotePos == o.RemotePos && RemoteYaw == o.RemoteYaw;
    }

    /// <summary>Driven RC car pose, written by the driver ~20 times per second.</summary>
    public struct RcPack : INetworkSerializable, IEquatable<RcPack>
    {
        public Vector3 Pos;
        public short Yaw;     // degrees * 50
        public sbyte Speed;   // m/s * 20
        public sbyte Steer;   // -100..100
        public sbyte Throttle; // -100..100
        public byte Flags;    // 1 horn, 2 bump, 4 boost

        public float YawDeg => Yaw / 50f;
        public float SpeedMs => Speed / 20f;
        public bool Horn => (Flags & 1) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Speed);
            s.SerializeValue(ref Steer);
            s.SerializeValue(ref Throttle);
            s.SerializeValue(ref Flags);
        }

        public bool Equals(RcPack o) => (Pos - o.Pos).sqrMagnitude < 1e-6f && Yaw == o.Yaw && Speed == o.Speed && Steer == o.Steer && Throttle == o.Throttle && Flags == o.Flags;
    }

    /// <summary>Gameplay constants shared by server and clients.</summary>
    public static class Tuning
    {
        // Knife (seeker catches a hider)
        public const float KnifePromptRange = 2.6f;    // icon appears
        public const float KnifeRange = 1.9f;          // F works (arm's length + a step)
        public const float KnifeServerRange = 2.7f;    // server tolerance (latency)
        public const float KnifeStandOff = 0.85f;      // victim distance during the animation
        public static readonly float[] StabTimes = HideAndSeek.Characters.ActionTimeline.KnifeStabTimes;
        public const float KnifeAttackDuration = HideAndSeek.Characters.ActionTimeline.KnifeAttackDuration;
        public static float KnifeSequenceDuration => HideAndSeek.Characters.KillMoves.SequenceDuration(HideAndSeek.Characters.KillMove.Stabs); // incl. struggle and aftermath

        // RC car
        public const float RcRange = 60f;              // radio range from the driver's body
        public const float RcPickupRange = 2.2f;
        public const float RcStompPromptRange = 2.2f;  // seeker F prompt
        public const float RcStompServerRange = 3.2f;
        public const float RcStompStandOff = 0.42f;    // seeker distance from the car during the stomp

        // Melee cooldown, chainsaw first hit, dust
        public const float AttackCooldown = 13f;       // seconds between two melee attacks of the seeker
        public const float AttackSlowScale = 0.5f;     // seeker speed right after an attack / a kill (hider can run away)
        public const float AttackSlowHold = 2.5f;      // full slowdown this long, then a smooth return
        public const float AttackSlowFade = 1.5f;
        // Glock aiming (RMB): spread cone (degrees), zoom, walking speed while aiming
        public const float GlockHipSpread = 2.6f;
        public const float GlockAimSpread = 0.3f;
        public const float GlockMoveSpread = 1.6f;     // extra at full walking speed (aimed: a quarter of it)
        public const float GlockAimFovScale = 0.8f;
        public const float GlockAimSpeedScale = 0.75f;
        public const float HopSpeedScale = 0.34f;      // one-legged hider (leg cut off by the chainsaw)
        public const float DustRange = 2.6f;           // hider throws dust at a seeker this close (in front)
        public const float DustBlindTime = 4f;
        public const float DustCooldown = 20f;

        // Chainsaw
        public const float ChainsawContact = 1.3f;      // walk into a hider this close with F held
        public const float ChainsawServerRange = 2.6f;
        public const float ChainsawPromptRange = 4.0f;

        // Downed / rescue
        public const float BleedOutTime = 78f;           // 1.3 min
        public const float CrawlShare = 0.75f;           // downed players can crawl for this share of the bleed-out time
        public const float RescueRange = 2.2f;
        public const float RescueHoldTime = 3f;
        public const float HealHoldTime = HideAndSeek.Characters.HealTimeline.MedkitDuration; // medkit on a wound (animated)
        public const float PickupRange = 2.0f;
        public const int MaxMedkits = 3;
        public const int MaxSyringes = 2;
        public const float SelfReviveTime = 5f;    // downed + medkit: hold G
        public const float ResurrectTime = 4f;     // adrenaline on a dead hider: hold H
        public const float ResurrectRange = 2.2f;
        public const float ResurrectedPingEvery = 5f;
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
