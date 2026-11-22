using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Audio.Editor
{
    /// <summary>
    /// Import settings of the recorded stress breathing (Audio/Resources/Stress, ElevenLabs): mono, Vorbis, decompressed
    /// on load (short clips; the game reads the samples once to find the quiet points between breaths).
    /// </summary>
    public class StressAudioImport : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Audio/Resources/Stress/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = true;
            importer.defaultSampleSettings = s;
        }
    }
}
