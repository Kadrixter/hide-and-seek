using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Hint sounds played from a hider's position. Filled by the editor builder from
    /// Assets/_Project/Audio/Voice/*.mp3 into Resources/HintVoices.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "HideAndSeek/Hint Voices", fileName = "HintVoices")]
    public class HintVoices : ScriptableObject
    {
        public AudioClip[] clips = new AudioClip[0];

        private static HintVoices _instance;
        private static bool _loaded;

        public static AudioClip Get(int index)
        {
            if (!_loaded)
            {
                _instance = Resources.Load<HintVoices>("HintVoices");
                _loaded = true;
            }
            if (_instance == null || _instance.clips.Length == 0) return null;
            return _instance.clips[Mathf.Abs(index) % _instance.clips.Length];
        }
    }
}
