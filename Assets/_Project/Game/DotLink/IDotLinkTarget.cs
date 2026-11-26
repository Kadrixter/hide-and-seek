using HideAndSeek.Game.Net;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// A screen the local player can work a dot link puzzle on: the exit-gate panel (<see cref="DotLinkPanel"/>) or the
    /// lobby arcade cabinet (<see cref="DotLinkArcade"/>). <see cref="DotLinkLocal"/> (mouse, drag, draft),
    /// <see cref="DotLinkCamera"/> (close-up) and <see cref="DotLinkHud"/> work through this, so both share one input path.
    /// </summary>
    public interface IDotLinkTarget
    {
        /// <summary>The shared board as the server sent it (null until a puzzle is there).</summary>
        DotLinkBoard NetBoard { get; }
        /// <summary>Cell of the screen under a ray (-1 = off the grid / no puzzle).</summary>
        int CellUnder(Ray ray);
        /// <summary>World point of a cell centre (tests aim the virtual mouse at it).</summary>
        Vector3 CellWorld(int cell);
        void Play(AudioClip clip, float volume, float pitch = 1f);
        /// <summary>Close-up camera straight in front of the screen (false = the screen is gone).</summary>
        bool CameraPose(float fov, float zoom, out Vector3 pos, out Quaternion rot);
        /// <summary>Big text when the puzzle gets solved.</summary>
        string SolvedFlash { get; }
        /// <summary>Extra text after the status line (the arcade's clock), "" for none.</summary>
        string StatusExtra { get; }
        void SendPath(NetPlayer me, byte color, FixedString512Bytes cells, byte dragId, bool final);
        void SendClear(NetPlayer me, byte color);
        void SendLeave(NetPlayer me);
    }
}
