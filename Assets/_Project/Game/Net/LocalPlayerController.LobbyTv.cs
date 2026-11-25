using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Lobby wall TV prompt: looking at its screen from the room (up to <see cref="TvReach"/> m) offers E = the YouTube panel.</summary>
    public partial class LocalPlayerController
    {
        public const float TvReach = 6.5f;

        /// <summary>The view ray hits the TV screen (a small margin around it), in reach, nothing of the level in between.</summary>
        private bool TryTvPrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out float dist)
        {
            pos = default;
            dist = float.MaxValue;
            var tv = LobbyTvVideo.Instance;
            if (tv == null) return false;
            Vector3 c = tv.ScreenCenter, n = tv.ScreenNormal;
            float facing = Vector3.Dot(eye - c, n);
            float denom = Vector3.Dot(fwd, -n);
            if (facing < 0.3f || denom < 0.15f) return false;
            float t = facing / denom; // along the view ray to the screen plane
            if (t > TvReach) return false;
            Vector3 hit = eye + fwd * t, local = hit - c;
            Vector3 right = Vector3.Cross(Vector3.up, n).normalized;
            Vector2 half = tv.ScreenSize * 0.5f + new Vector2(0.1f, 0.1f);
            if (Mathf.Abs(Vector3.Dot(local, right)) > half.x || Mathf.Abs(local.y) > half.y) return false;
            if (!InView(eye, fwd, hit, 90f, tv.transform)) return false;
            pos = hit;
            dist = t + 0.5f; // anything closer in front of the TV wins
            return true;
        }
    }
}
