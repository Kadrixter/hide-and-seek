using UnityEngine;

namespace HideAndSeek.Audio
{
    /// <summary>
    /// Static facade for gameplay sound effects. Network code calls these on every client
    /// (e.g. from a ClientRpc); missing clips are silently ignored so code never depends on assets.
    /// 3D sounds get distance rolloff, reverb from <see cref="AudioEnvironment"/>, echo (entries flagged echo)
    /// and wall occlusion (low-pass) automatically.
    /// </summary>
    public static class GameAudio
    {
        private static GameAudioLibrary _library;
        private static GameAudioRunner _runner;

        public static GameAudioLibrary Library
        {
            get
            {
                if (_library == null) _library = Resources.Load<GameAudioLibrary>(GameAudioLibrary.ResourcePath);
                return _library;
            }
        }

        /// <summary>Master volume for sfx (0..1), set from options.</summary>
        public static float SfxVolume { get; set; } = 1f;
        public static float MusicVolume { get; set; } = 0.7f;

        // Per-category mix (0..1), multiplied with SfxVolume. Set from the options menu.
        public static float UiVolume { get; set; } = 1f;
        public static float VoiceVolume { get; set; } = 1f;
        public static float WeaponVolume { get; set; } = 1f;
        /// <summary>3D ambient emitters (vents, drips, lamps).</summary>
        public static float AmbientVolume { get; set; } = 1f;

        /// <summary>Category multiplier for a sound id (UI, voices, weapons; everything else 1).</summary>
        public static float CategoryVolume(GameSfx id)
        {
            int v = (int)id;
            if (v >= 1 && v <= 11) return UiVolume;
            if (v >= 30 && v <= 33) return VoiceVolume;
            if ((v >= 20 && v <= 23) || (v >= 50 && v <= 56)) return WeaponVolume;
            return 1f;
        }

        /// <summary>Muffle 3D sounds behind walls (one raycast per voice every ~0.12 s).</summary>
        public static bool OcclusionEnabled { get; set; } = true;

        /// <summary>Layers that block sound (walls, floors, pillars). Defaults to everything except Ignore Raycast.</summary>
        public static LayerMask OcclusionMask { get; set; } = Physics.DefaultRaycastLayers;

        private static GameAudioRunner Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("[GameAudio]");
                    Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<GameAudioRunner>();
                }
                return _runner;
            }
        }

        /// <summary>3D one-shot at a world position.</summary>
        public static AudioSource PlayAt(GameSfx id, Vector3 position, float volume = 1f)
        {
            var src = Runner.Play(Library?.Get(id), position, null, volume, true, false);
            PlayLayers(id, position, null, volume);
            return src;
        }

        /// <summary>3D one-shot following a transform (scream on a moving player).</summary>
        public static AudioSource PlayAttached(GameSfx id, Transform target, float volume = 1f)
        {
            if (target == null) return null;
            var src = Runner.Play(Library?.Get(id), target.position, target, volume, true, false);
            PlayLayers(id, target.position, target, volume);
            return src;
        }

        /// <summary>2D one-shot (UI, local-only feedback).</summary>
        public static AudioSource Play2D(GameSfx id, float volume = 1f)
            => Runner.Play(Library?.Get(id), Vector3.zero, null, volume, false, false);

        /// <summary>Starts a loop (3D if target given, else 2D). Stop it with <see cref="StopLoop"/>.</summary>
        public static AudioSource StartLoop(GameSfx id, Transform target = null, float volume = 1f)
            => Runner.Play(Library?.Get(id), target != null ? target.position : Vector3.zero, target, volume, target != null, true);

        public static void StopLoop(AudioSource source, float fade = 0.3f)
        {
            if (source != null && _runner != null) _runner.Stop(source, fade);
        }

        /// <summary>Fades out every pooled sfx voice (e.g. when leaving a match).</summary>
        public static void StopAll(float fade = 0.2f)
        {
            if (_runner != null) _runner.StopAll(fade);
        }

        /// <summary>Plays an arbitrary clip (e.g. Voice/*.mp3 hint) in 3D.</summary>
        public static AudioSource PlayClipAt(AudioClip clip, Vector3 position, float volume = 1f, float range = 40f)
        {
            if (clip == null) return null;
            var e = new SfxEntry { clips = new[] { clip }, volume = 1f, range = range, pitchJitter = 0f };
            return Runner.Play(e, position, null, volume, true, false);
        }

        /// <summary>Plays an arbitrary clip in 2D (local feedback, e.g. the player's own breathing), mixed like <paramref name="mixAs"/>.</summary>
        public static AudioSource PlayClip2D(AudioClip clip, float volume = 1f, GameSfx mixAs = GameSfx.None)
        {
            if (clip == null) return null;
            var e = new SfxEntry { id = mixAs, clips = new[] { clip }, volume = 1f, range = 10f, pitchJitter = 0f, reverbMix = 0f };
            return Runner.Play(e, Vector3.zero, null, volume, false, false);
        }

        /// <summary>Plays an arbitrary clip in 3D following <paramref name="target"/> (walls / floors muffle it as usual).</summary>
        public static AudioSource PlayClipAttached(AudioClip clip, Transform target, float volume, float range, GameSfx mixAs = GameSfx.None, float reverbMix = 0.6f)
        {
            if (clip == null || target == null) return null;
            var e = new SfxEntry { id = mixAs, clips = new[] { clip }, volume = 1f, range = range, pitchJitter = 0f, reverbMix = reverbMix };
            return Runner.Play(e, target.position, target, volume, true, false);
        }

        /// <summary>Switches the 2D music/ambient bed (menu, lobby, garage, tension). Crossfades.</summary>
        public static void SetBed(AmbientId id, float fade = 1.5f) => Runner.SetBed(Library?.Get(id), fade);

        /// <summary>Audible range in metres of a sound (library value; 0 when unknown). Useful for "who heard it" logic.</summary>
        public static float GetRange(GameSfx id)
        {
            var e = Library?.Get(id);
            return e != null ? e.range : 0f;
        }

        /// <summary>True when the library has at least one clip for the id.</summary>
        public static bool HasClip(GameSfx id)
        {
            var e = Library?.Get(id);
            return e != null && e.clips != null && e.clips.Length > 0 && e.clips[0] != null;
        }

        /// <summary>Transform of the active AudioListener (null when none).</summary>
        public static Transform Listener => Runner.Listener;

        // A gunshot also plays its far tail: quiet close by, carries far through the garage.
        private static void PlayLayers(GameSfx id, Vector3 position, Transform follow, float volume)
        {
            if (id == GameSfx.GlockShot)
                Runner.Play(Library?.Get(GameSfx.GlockShotDistant), position, follow, volume, true, false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _runner = null;
            _library = null;
        }
    }
}
