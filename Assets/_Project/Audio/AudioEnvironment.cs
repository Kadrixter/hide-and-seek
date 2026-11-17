using System;
using UnityEngine;

namespace HideAndSeek.Audio
{
    /// <summary>Acoustic space the local listener is in.</summary>
    public enum AudioEnvironmentKind
    {
        None = 0,       // no global reverb
        Garage = 1,     // huge underground concrete parking: long tail + slap echo
        SmallRoom = 2,  // lobby: short, dry-ish room
        Menu = 3,       // main menu: no reverb on sfx
    }

    /// <summary>Reverb parameters shared by <see cref="AudioReverbZone"/> and <see cref="AudioReverbFilter"/> (millibels / seconds).</summary>
    [Serializable]
    public struct ReverbSettings
    {
        public int room, roomHF, roomLF;
        public float decayTime, decayHFRatio;
        public int reflections;
        public float reflectionsDelay;
        public int reverb;
        public float reverbDelay, hfReference, lfReference, diffusion, density;

        public void ApplyTo(AudioReverbZone z)
        {
            if (z == null) return;
            z.reverbPreset = AudioReverbPreset.User;
            z.room = room; z.roomHF = roomHF; z.roomLF = roomLF;
            z.decayTime = decayTime; z.decayHFRatio = decayHFRatio;
            z.reflections = reflections; z.reflectionsDelay = reflectionsDelay;
            z.reverb = reverb; z.reverbDelay = reverbDelay;
            z.HFReference = hfReference; z.LFReference = lfReference;
            z.diffusion = diffusion; z.density = density;
        }

        public void ApplyTo(AudioReverbFilter f, float dryLevel = 0f)
        {
            if (f == null) return;
            f.reverbPreset = AudioReverbPreset.User;
            f.dryLevel = dryLevel;
            f.room = room; f.roomHF = roomHF; f.roomLF = roomLF;
            f.decayTime = decayTime; f.decayHFRatio = decayHFRatio;
            f.reflectionsLevel = reflections; f.reflectionsDelay = reflectionsDelay;
            f.reverbLevel = reverb; f.reverbDelay = reverbDelay;
            f.hfReference = hfReference; f.lfReference = lfReference;
            f.diffusion = diffusion; f.density = density;
        }
    }

    /// <summary>Echo used for loud sounds (gunshots, screams) in the current space.</summary>
    [Serializable]
    public struct EchoSettings
    {
        public bool enabled;
        public float delayMs, decay, wet;
    }

    /// <summary>
    /// Global acoustic environment. <see cref="Apply"/> sets one listener-wide <see cref="AudioReverbZone"/>
    /// (affects every 3D sound), the reverb copied into the local player's 2D footstep filters (via <see cref="Changed"/>)
    /// and the echo that <see cref="GameAudio"/> adds to loud sounds. Call it when a scene loads:
    /// garage map -> Garage, lobby -> SmallRoom, main menu -> Menu.
    /// </summary>
    public static class AudioEnvironment
    {
        private static AudioReverbZone _zone;

        public static AudioEnvironmentKind Current { get; private set; } = AudioEnvironmentKind.None;
        public static ReverbSettings Reverb { get; private set; }
        public static EchoSettings Echo { get; private set; }

        /// <summary>Raised after <see cref="Apply"/> (also used by the player's GarageReverb).</summary>
        public static event Action<AudioEnvironmentKind> Changed;

        /// <summary>Huge concrete parking garage: ~3 s tail, early reflections, slap echo off far walls.</summary>
        public static readonly ReverbSettings GarageReverb = new ReverbSettings
        {
            room = -700, roomHF = -500, roomLF = 0, decayTime = 3.1f, decayHFRatio = 0.55f,
            reflections = -900, reflectionsDelay = 0.035f, reverb = -250, reverbDelay = 0.045f,
            hfReference = 5000f, lfReference = 250f, diffusion = 85f, density = 100f,
        };

        /// <summary>Small furnished room (lobby).</summary>
        public static readonly ReverbSettings SmallRoomReverb = new ReverbSettings
        {
            room = -1100, roomHF = -900, roomLF = 0, decayTime = 0.55f, decayHFRatio = 0.7f,
            reflections = -500, reflectionsDelay = 0.004f, reverb = -400, reverbDelay = 0.008f,
            hfReference = 5000f, lfReference = 250f, diffusion = 100f, density = 100f,
        };

        public static readonly EchoSettings GarageEcho = new EchoSettings { enabled = true, delayMs = 240f, decay = 0.38f, wet = 0.42f };
        public static readonly EchoSettings NoEcho = new EchoSettings { enabled = false, delayMs = 100f, decay = 0f, wet = 0f };

        public static ReverbSettings ReverbFor(AudioEnvironmentKind kind) =>
            kind == AudioEnvironmentKind.Garage ? GarageReverb : SmallRoomReverb;

        public static EchoSettings EchoFor(AudioEnvironmentKind kind) =>
            kind == AudioEnvironmentKind.Garage ? GarageEcho : NoEcho;

        /// <summary>True when the current space has a global reverb.</summary>
        public static bool HasReverb => Current == AudioEnvironmentKind.Garage || Current == AudioEnvironmentKind.SmallRoom;

        /// <summary>Switches the global reverb/echo. Optionally also crossfades the 2D bed (Garage -> GarageBed, SmallRoom -> LobbyRoom, Menu -> MenuMusic).</summary>
        public static void Apply(AudioEnvironmentKind kind) => Apply(kind, false);

        public static void Apply(AudioEnvironmentKind kind, bool switchBed)
        {
            Current = kind;
            Reverb = ReverbFor(kind);
            Echo = EchoFor(kind);

            var zone = Zone;
            if (HasReverb)
            {
                Reverb.ApplyTo(zone);
                zone.enabled = true;
            }
            else
            {
                zone.reverbPreset = AudioReverbPreset.Off;
                zone.enabled = false;
            }

            if (switchBed)
            {
                switch (kind)
                {
                    case AudioEnvironmentKind.Garage: GameAudio.SetBed(AmbientId.GarageBed); break;
                    case AudioEnvironmentKind.SmallRoom: GameAudio.SetBed(AmbientId.LobbyRoom); break;
                    case AudioEnvironmentKind.Menu: GameAudio.SetBed(AmbientId.MenuMusic); break;
                    default: GameAudio.SetBed(AmbientId.None); break;
                }
            }

            Changed?.Invoke(kind);
        }

        private static AudioReverbZone Zone
        {
            get
            {
                if (_zone != null) return _zone;
                var go = new GameObject("[AudioEnvironment]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _zone = go.AddComponent<AudioReverbZone>();
                // listener-wide: a sphere far larger than any map
                _zone.minDistance = 20000f;
                _zone.maxDistance = 20001f;
                return _zone;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _zone = null;
            Current = AudioEnvironmentKind.None;
            Echo = NoEcho;
            Changed = null;
        }
    }
}
