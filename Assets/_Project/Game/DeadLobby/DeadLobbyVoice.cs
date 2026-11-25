using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Sound for a player in the lobby copy (one AudioListener, at the lobby body): the dead players' voices come from
    /// their ghosts (3D in the room); the living players' voices come out of the TV speaker at <see cref="DeadLobbyTv.Volume"/>
    /// (full for the watched player, the others as loud as he hears them, a little band-limited like a TV); game sounds
    /// near the watched player are played again at the speaker (<see cref="OnGameSound"/>). Full-screen spectating and the
    /// rest of the game use the normal voice rules (<see cref="VoicePlayback"/> returns to its own logic).
    /// </summary>
    public static class DeadLobbyVoice
    {
        private static readonly Vector3 Home = new(0f, 1.55f, 0f);
        private static bool _hooked;

        /// <summary>
        /// Hook in <c>VoicePlayback.Update</c>: true when the lobby copy decides where and how loud this voice plays
        /// (the playback then skips its own spatial logic this frame).
        /// </summary>
        public static bool Route(NetPlayer owner, Transform voice, AudioSource src, AudioEchoFilter echo, AudioLowPassFilter lowPass)
        {
            Hook();
            if (!DeadLobby.InLobby || owner == null || voice == null || src == null)
            {
                if (voice != null && voice.localPosition != Home) voice.localPosition = Home;
                return false;
            }
            if (echo != null) echo.enabled = false;
            src.spatialBlend = 1f;
            if (owner.IsObserver)
            {
                var g = owner.LobbyGhost.Value;
                if (g.Mode == LobbyGhostState.Off || g.Mode == LobbyGhostState.WalkingIn)
                {
                    // Not in the lobby yet: the observer channel as before (flat).
                    src.spatialBlend = 0f;
                    src.volume = 1f;
                    if (lowPass != null) lowPass.cutoffFrequency = 22000f;
                    return true;
                }
                var av = DeadLobbyAvatars.Of(owner);
                voice.position = av != null ? av.Head : DeadLobbyWorld.ToWorld(g.Pos) + Vector3.up * 1.55f;
                src.maxDistance = 24f;
                src.volume = 1f;
                if (lowPass != null) lowPass.cutoffFrequency = 22000f;
                return true;
            }
            var tv = DeadLobbyTv.Instance;
            if (tv == null) { src.volume = 0f; return true; }
            voice.position = tv.Speaker;
            src.maxDistance = 26f;
            src.volume = tv.VoiceGain(owner);
            if (lowPass != null) lowPass.cutoffFrequency = 6500f;
            return true;
        }

        /// <summary>Leaving the lobby: every voice back on its player's head.</summary>
        public static void ResetAll()
        {
            foreach (var p in NetPlayer.All)
            {
                var vp = p != null ? p.GetComponentInChildren<Voice.VoicePlayback>() : null;
                if (vp != null) vp.transform.localPosition = Home;
            }
        }

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            GameAudio.SpatialPlayed += OnGameSound;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => Hook();

        /// <summary>A 3D one-shot somewhere in the garage: near the watched player it is also heard from the TV.</summary>
        private static void OnGameSound(AudioClip clip, Vector3 pos, float volume, float range)
        {
            if (clip == null || !DeadLobby.InLobby || DeadLobbyWorld.InLobbySpace(pos)) return;
            var tv = DeadLobbyTv.Instance;
            if (tv == null || tv.Target == null) return;
            float k = tv.HeardBy(pos, range) * DeadLobbyTv.Volume;
            if (k < 0.02f) return;
            GameAudio.PlayClipAt(clip, tv.Speaker, Mathf.Clamp01(volume * k), 16f);
        }
    }
}
