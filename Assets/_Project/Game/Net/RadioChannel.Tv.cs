using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The two ops only the lobby TV uses (<see cref="MatchManager.LobbyTv"/>): the TV shows one video at a time, so a new
    /// link replaces the current one instead of queueing it, and "stop" empties the channel (back to the CCTV picture).
    /// Everything else (common start after every copy buffered, pause, seek, volume, the end of a video) is the
    /// boombox logic of <see cref="RadioChannel"/>.
    /// </summary>
    public sealed partial class RadioChannel
    {
        /// <summary>Server: <see cref="RadioOp.Replace"/> / <see cref="RadioOp.Clear"/>. False = refused (no change).</summary>
        private bool ServerTvOp(ref RadioState r, RadioOp op, string url)
        {
            if (op == RadioOp.Replace)
            {
                if (!MatchManager.IsYoutubeUrl(url)) return false;
                if (Queue.Count > 0) Queue.Clear();
                Queue.Add(new FixedString128Bytes(url.Trim()));
                r.Index = 0;
                BeginLoad(ref r);
                return true;
            }
            if (op == RadioOp.Clear)
            {
                if (Queue.Count > 0) Queue.Clear();
                Stop(ref r);
                r.Index = -1;
                return true;
            }
            return false;
        }

        /// <summary>Server: something is chosen (playing, paused or loading).</summary>
        public bool HasCurrent => Current != null;

        /// <summary>Server: empty and stopped, the volume kept (a round started).</summary>
        public void ServerStop()
        {
            var r = State.Value;
            if (Queue.Count > 0) Queue.Clear();
            Stop(ref r);
            r.Index = -1;
            State.Value = r;
            Debug.Log($"{Tag} stopped");
        }
    }
}
