namespace HideAndSeek.Game.Voice
{
    /// <summary>Processing applied to a <see cref="VoiceStream"/>'s output on the audio thread (16 kHz mono, in place).</summary>
    public interface IVoicePost
    {
        void Process(float[] data);
    }

    public partial class VoiceStream
    {
        /// <summary>
        /// Optional processing of the played samples, before Unity resamples and spatialises them (so anything it adds,
        /// e.g. the crackle of a loudspeaker, comes from the source's position). Set before the source plays.
        /// </summary>
        public IVoicePost Post { get; set; }
    }
}
