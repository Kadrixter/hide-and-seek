using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>8-ball rules on hand-made shot results (<see cref="PoolRules.Apply"/>).</summary>
    public static partial class PoolSimCheck
    {
        static PoolState Game(bool brk, byte groups, byte turn)
        {
            var s = Racked(3);
            s.Phase = PoolState.Playing;
            s.A = 1;
            s.B = 2;
            s.Break = brk;
            s.BallInHand = brk;
            s.Kitchen = brk;
            s.Groups = groups;
            s.Turn = turn;
            s.Pos[0] = new Vector2(-0.4f, 0f);
            return s;
        }

        /// <summary>A result: final balls = the state's, with <paramref name="potted"/> down; first hit, cushion after contact.</summary>
        static PoolSimResult Res(PoolState s, int firstHit, bool rail, int railed, params int[] potted)
        {
            var r = new PoolSimResult { FirstHit = firstHit, RailAfterContact = rail, BallsToRail = railed };
            System.Array.Copy(s.Pos, r.FinalPos, 16);
            ushort down = s.Down;
            foreach (int b in potted)
            {
                r.Pocketed.Add(b);
                r.PocketOf.Add(0);
                down |= (ushort)(1 << b);
            }
            r.FinalDown = down;
            return r;
        }

        static void Rules()
        {
            // Break: a ball down -> same player, table open.
            var s = Game(true, PoolState.Open, 0);
            PoolRules.Apply(s, Res(s, 1, true, 5, 3), "Ala", "Kasia");
            Check("break pot keeps the turn, open table", s.Turn == 0 && s.Groups == PoolState.Open && !s.Break && !s.BallInHand, s.Note);
            // Break: nothing down, 2 balls to a cushion -> foul, ball in hand anywhere for B.
            s = Game(true, PoolState.Open, 0);
            PoolRules.Apply(s, Res(s, 1, true, 2), "Ala", "Kasia");
            Check("weak break = foul, ball in hand", s.Turn == 1 && s.BallInHand && !s.Kitchen, s.Note);
            // Break: nothing down, 5 to a cushion -> B's turn, no foul.
            s = Game(true, PoolState.Open, 0);
            PoolRules.Apply(s, Res(s, 1, true, 5), "Ala", "Kasia");
            Check("legal empty break passes the turn", s.Turn == 1 && !s.BallInHand, s.Note);
            // Break scratch -> ball in hand in the kitchen.
            s = Game(true, PoolState.Open, 0);
            PoolRules.Apply(s, Res(s, 1, true, 6, 0), "Ala", "Kasia");
            Check("break scratch = ball in hand in the kitchen", s.Turn == 1 && s.BallInHand && s.Kitchen && !s.IsDown(0) && s.Pos[0].x <= PoolState.KitchenX + 1e-4f, s.Note);
            // 8 on the break is spotted again, game goes on.
            s = Game(true, PoolState.Open, 0);
            PoolRules.Apply(s, Res(s, 1, true, 6, 8), "Ala", "Kasia");
            Check("8 on the break is spotted", s.Phase == PoolState.Playing && !s.IsDown(8) && s.Turn == 0, s.Note);

            // Open table: a stripe down picks the groups (A = shooter -> stripes).
            s = Game(false, PoolState.Open, 0);
            PoolRules.Apply(s, Res(s, 11, true, 1, 11), "Ala", "Kasia");
            Check("first pot picks the groups", s.Groups == PoolState.AStripes && s.Turn == 0 && s.GroupOf(1) == 1, s.Note);
            // B shoots and pots a solid on an open table -> B solids = A stripes.
            s = Game(false, PoolState.Open, 1);
            PoolRules.Apply(s, Res(s, 2, true, 1, 2), "Ala", "Kasia");
            Check("seat B pots a solid -> B solids", s.Groups == PoolState.AStripes && s.Turn == 1 && s.GroupOf(1) == 1, s.Note);
            // Scratch (not on the break) -> foul, ball in hand anywhere.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, 2, true, 1, 0), "Ala", "Kasia");
            Check("scratch = foul, ball in hand anywhere", s.Turn == 1 && s.BallInHand && !s.Kitchen && !s.IsDown(0), s.Note);
            // Scratch while potting an own ball: still a foul.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, 2, true, 1, 2, 0), "Ala", "Kasia");
            Check("pot + scratch = foul", s.Turn == 1 && s.BallInHand && s.IsDown(2), s.Note);
            // Wrong ball first.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, 12, true, 1), "Ala", "Kasia");
            Check("opponent's ball first = foul", s.Turn == 1 && s.BallInHand, s.Note);
            // Nothing hit.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, -1, false, 0), "Ala", "Kasia");
            Check("no ball hit = foul", s.Turn == 1 && s.BallInHand, s.Note);
            // No cushion after the contact.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, 3, false, 0), "Ala", "Kasia");
            Check("no cushion after contact = foul", s.Turn == 1 && s.BallInHand, s.Note);
            // Legal miss -> turn passes without ball in hand.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, 3, true, 1), "Ala", "Kasia");
            Check("legal miss passes the turn", s.Turn == 1 && !s.BallInHand, s.Note);
            // Own ball potted -> same player.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, 4, true, 1, 4), "Ala", "Kasia");
            Check("own pot keeps the turn", s.Turn == 0 && !s.BallInHand, s.Note);
            // Only the opponent's ball potted (legal hit) -> turn passes.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, 4, true, 1, 13), "Ala", "Kasia");
            Check("only opponent's ball potted passes the turn", s.Turn == 1 && !s.BallInHand, s.Note);

            // 8 too early -> loss.
            s = Game(false, PoolState.ASolids, 0);
            PoolRules.Apply(s, Res(s, 4, true, 1, 8), "Ala", "Kasia");
            Check("8 too early = loss", s.Phase == PoolState.Over && s.Winner == 1 && s.EndReason == PoolState.EndEightEarly && s.WinsB == 1, s.Note);
            // 8 legally after the own group -> win.
            s = Game(false, PoolState.ASolids, 0);
            s.Down = PoolState.SolidsMask;
            PoolRules.Apply(s, Res(s, 8, true, 1, 8), "Ala", "Kasia");
            Check("8 after the group = win", s.Phase == PoolState.Over && s.Winner == 0 && s.EndReason == PoolState.EndEight && s.WinsA == 1, s.Note);
            // 8 with a scratch -> loss.
            s = Game(false, PoolState.ASolids, 0);
            s.Down = PoolState.SolidsMask;
            PoolRules.Apply(s, Res(s, 8, true, 1, 8, 0), "Ala", "Kasia");
            Check("8 + scratch = loss", s.Phase == PoolState.Over && s.Winner == 1 && s.EndReason == PoolState.EndEightScratch, s.Note);
            // On the 8, hitting a stripe first is a foul.
            s = Game(false, PoolState.ASolids, 0);
            s.Down = PoolState.SolidsMask;
            PoolRules.Apply(s, Res(s, 10, true, 1), "Ala", "Kasia");
            Check("on the 8, other ball first = foul", s.Turn == 1 && s.BallInHand, s.Note);
            // Last own ball potted -> note says the 8 is next.
            s = Game(false, PoolState.ASolids, 0);
            s.Down = (ushort)(PoolState.SolidsMask & ~(1 << 5));
            PoolRules.Apply(s, Res(s, 5, true, 1, 5), "Ala", "Kasia");
            Check("last own ball -> on the 8", s.Turn == 0 && s.OnEight(0), s.Note);
            // Open table: hitting the 8 first is a foul.
            s = Game(false, PoolState.Open, 0);
            PoolRules.Apply(s, Res(s, 8, true, 1), "Ala", "Kasia");
            Check("open table, 8 first = foul", s.Turn == 1 && s.BallInHand, s.Note);
            // Ball in hand after a scratch never overlaps another ball.
            s = Game(false, PoolState.ASolids, 0);
            s.Pos[5] = PoolState.HeadSpot2;
            PoolRules.Apply(s, Res(s, 2, true, 1, 0), "Ala", "Kasia");
            Check("cue ball back in hand is free", PoolSim.CanPlaceCueBall(s.Pos, s.Down, s.Pos[0], s.Kitchen), $"{s.Pos[0]} vs {s.Pos[5]}");
        }
    }
}
