using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// SNAKE constants and the latest state of every cabinet received from the server (<see cref="Cab"/>, drawn by
    /// <see cref="LobbyArcade"/>, joystick and hands in NetPlayer). Every peer gets the same states, so everybody in
    /// the lobby sees both games. The record is shared by both cabinets and kept per computer
    /// (Documents\HideAndSeek\ustawienia.txt, key hns.snake.best).
    /// </summary>
    public static class ArcadeState
    {
        public const byte Playing = 0, Over = 1, Idle = 2, Ready = 3;

        /// <summary>Event flags sent with a state update.</summary>
        public const byte EvEat = 1, EvDie = 2, EvStart = 4, EvBonusSpawn = 8, EvBonusEat = 16, EvLevelUp = 32, EvTurn = 64, EvBonusGone = 128;

        /// <summary>One game per cabinet (independent: two players can play at the same time).</summary>
        public static readonly ArcadeGame[] Cab = { new(0), new(1) };

        public static ArcadeGame Of(int cab) => Cab[Mathf.Clamp(cab, 0, Cab.Length - 1)];

        private const string BestKey = "hns.snake.best";
        private static int _best = -1;
        /// <summary>Test runs (-testsnake / -snakeshots / -snakeblock / -testarcade2) keep the record in memory only, seeded by -snakebest N.</summary>
        private static readonly bool Volatile = HasArg("-testsnake") || HasArg("-snakeshots") || HasArg("-snakeblock") || HasArg("-testarcade2");

        /// <summary>Best SNAKE score seen on this computer (both cabinets), kept between game restarts.</summary>
        public static int Best
        {
            get
            {
                if (_best < 0) _best = Volatile ? ArgInt("-snakebest", 0) : Prefs.GetInt(BestKey, 0);
                return _best;
            }
            internal set
            {
                _best = value;
                if (Volatile) return;
                Prefs.SetInt(BestKey, value);
                Prefs.Save();
            }
        }

        private static bool HasArg(string a) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), a) >= 0;

        private static int ArgInt(string name, int def)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out int v) ? v : def;
        }
    }

    /// <summary>Latest SNAKE state of one cabinet (client side).</summary>
    public sealed class ArcadeGame
    {
        public readonly int Index;
        public ArcadeGame(int index) => Index = index;

        public bool Live;
        public byte State = ArcadeState.Idle;
        public readonly List<Vector2Int> Body = new();
        public Vector2Int Food;
        public bool HasBonus;
        public Vector2Int Bonus;
        /// <summary>Seconds left on the bonus food when the update arrived.</summary>
        public float BonusLeft;
        public int Score;
        public int Level = 1;
        public int Dir;
        public bool Crashed;
        public float Updated;
        /// <summary>Record when the current game started, and whether this game beat it.</summary>
        public int BestAtStart;
        public bool NewRecord;
        public float StateSince;
        /// <summary>Bumped on every update that carried events (the cabinet presses its buttons on them).</summary>
        public int EventSerial;
        public byte LastEvents;
        private bool _recordAnnounced;

        /// <summary>"+1" / "+5" popups over eaten food (cell, text, time).</summary>
        public readonly List<(Vector2Int cell, string text, float t)> Popups = new();

        public float BonusRemaining => HasBonus ? Mathf.Max(0f, BonusLeft - (Time.time - Updated)) : 0f;

        public void Apply(byte[] body, byte fx, byte fy, byte bx, byte by, byte bonusTenths, ushort score, byte dir, byte state, byte level, byte evt)
        {
            byte prev = State;
            Live = state != ArcadeState.Idle;
            Crashed = state == ArcadeState.Over;
            State = state;
            if (prev != state) StateSince = Time.time;
            Vector2Int oldFood = Food, oldBonus = Bonus;
            Body.Clear();
            if (body != null)
                for (int i = 0; i + 1 < body.Length; i += 2) Body.Add(new Vector2Int(body[i], body[i + 1]));
            Food = new Vector2Int(fx, fy);
            HasBonus = bx != 255;
            Bonus = new Vector2Int(bx, by);
            BonusLeft = bonusTenths * 0.1f;
            Score = score;
            Level = Mathf.Max(1, level);
            Dir = dir;
            Updated = Time.time;
            if (evt != 0) { EventSerial++; LastEvents = evt; }

            if ((evt & ArcadeState.EvStart) != 0 || state == ArcadeState.Ready && prev != ArcadeState.Ready)
            {
                BestAtStart = ArcadeState.Best;
                NewRecord = false;
                _recordAnnounced = false;
                Popups.Clear();
            }
            if ((evt & ArcadeState.EvEat) != 0) Popups.Add((oldFood, "+1", Time.time));
            if ((evt & ArcadeState.EvBonusEat) != 0) Popups.Add((oldBonus, "+" + SnakeRules.BonusPoints, Time.time));
            if (score > BestAtStart && score > 0)
            {
                NewRecord = true;
                // Jingle the moment an existing record falls (a first ever game just gets it at game over).
                if (!_recordAnnounced && BestAtStart > 0 && state == ArcadeState.Playing) { _recordAnnounced = true; SnakeSounds.Play(SnakeSounds.Id.RecordDing, 0f, Index); }
            }
            if (score > ArcadeState.Best) ArcadeState.Best = score;
            SnakeSounds.PlayEvents(evt, state, NewRecord, Index);
            for (int i = Popups.Count - 1; i >= 0; i--) if (Time.time - Popups[i].t > 1f) Popups.RemoveAt(i);
        }
    }
}
