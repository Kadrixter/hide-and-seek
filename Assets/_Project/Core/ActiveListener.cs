using UnityEngine;

namespace HideAndSeek.Core
{
    /// <summary>
    /// The AudioListener that really hears (enabled, on an active object). Every player prefab carries a camera with a
    /// listener that stays disabled on remote copies, and <c>FindAnyObjectByType&lt;AudioListener&gt;()</c> happily returns one
    /// of those: the sound code then judged floors / walls from another player's head (since the 22.11 floor acoustics
    /// a sound on "another floor" is silent, e.g. the seeker's own chainsaw when the tracked player stood one level up).
    /// <c>-oldlistener</c> restores the old lookup (test comparison only).
    /// </summary>
    public static class ActiveListener
    {
        public static readonly bool Legacy = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-oldlistener") >= 0;
        /// <summary>Test only (-wronglistener): always takes a remote player's disabled listener, like the old lookup at its worst.</summary>
        public static readonly bool Wrong = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-wronglistener") >= 0;

        public static AudioListener Find()
        {
            if (Wrong)
                foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                    if (l != null && !l.enabled && l.gameObject.activeInHierarchy) return l;
            if (Legacy || Wrong) return Object.FindAnyObjectByType<AudioListener>();
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if (l != null && l.isActiveAndEnabled) return l;
            return null;
        }

        /// <summary>True while <paramref name="l"/> is still the listener in use (else search again).</summary>
        public static bool Valid(AudioListener l) => l != null && (Legacy || Wrong ? l.gameObject.activeInHierarchy : l.isActiveAndEnabled);
    }
}
