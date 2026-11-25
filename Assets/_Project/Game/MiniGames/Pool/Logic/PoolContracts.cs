using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    // CONTRACT FILE (2026-11-25, three agents work against it). Signatures here are fixed: implementations may change,
    // public names / parameters may only be ADDED to, never renamed or removed. Owner of the bodies: the "logic" agent.
    // Table space 2D: Vector2 (x, y) = table (x, z) on the cloth; ball centres. Angles in radians, 0 = +X, CCW seen from
    // above (towards +Z): dir = (cos a, sin a).

    /// <summary>One stroke of the cue.</summary>
    public struct PoolShot
    {
        /// <summary>Direction the cue ball is struck in (radians, table space).</summary>
        public float Angle;
        /// <summary>0..1 (1 = the hardest break).</summary>
        public float Power;
        /// <summary>Tip offset on the cue ball face, -1..1: X = side (+ right english), Y = height (+ follow, - draw).</summary>
        public float SpinX, SpinY;

        public PoolShot(float angle, float power, float spinX, float spinY)
        {
            Angle = angle;
            Power = power;
            SpinX = spinX;
            SpinY = spinY;
        }
    }

    /// <summary>Something that happened during a shot (sounds, effects).</summary>
    public struct PoolEvent
    {
        public const byte Strike = 0, BallBall = 1, Cushion = 2, Pocket = 3, Jaw = 4;
        /// <summary>Seconds after the stroke.</summary>
        public float T;
        public byte Kind;
        /// <summary>Ball index (0 = cue ball); B = the other ball for BallBall, the pocket 0..5 for Pocket, else -1.</summary>
        public int A, B;
        /// <summary>Impact speed (m/s) for the volume.</summary>
        public float Speed;
        public Vector2 At;
    }

    /// <summary>Whole simulated shot: frames for the animation and the facts for the rules.</summary>
    public sealed class PoolSimResult
    {
        /// <summary>Frame spacing of <see cref="Frames"/> (the view interpolates between frames).</summary>
        public const float FrameDt = 1f / 60f;
        /// <summary>Ball centres per frame (16 each, pocketed balls keep their last spot on the cloth).</summary>
        public readonly List<Vector2[]> Frames = new();
        /// <summary>Pocketed-mask per frame (bit i = ball i is down).</summary>
        public readonly List<ushort> DownMask = new();
        public readonly List<PoolEvent> Events = new();
        /// <summary>First object ball the cue ball touched (-1 = none).</summary>
        public int FirstHit = -1;
        /// <summary>Some ball touched a cushion after the first contact.</summary>
        public bool RailAfterContact;
        /// <summary>Balls pocketed in order (0 = scratch).</summary>
        public readonly List<int> Pocketed = new();
        /// <summary>Pocket of each pocketed ball (same order as <see cref="Pocketed"/>).</summary>
        public readonly List<int> PocketOf = new();
        /// <summary>Object balls that crossed the head string... (break rule: number of balls that hit a cushion).</summary>
        public int BallsToRail;
        public float Duration;
        public Vector2[] FinalPos = new Vector2[16];
        public ushort FinalDown;
    }
}
