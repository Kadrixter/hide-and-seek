using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// 8-ball rules (WPA-like, simplified). Break: a ball down or 4 balls to a cushion, else foul; the 8 on the break
    /// is spotted again; a scratch on the break = ball in hand in the kitchen. Open table after the break, the first
    /// ball legally pocketed picks the groups. Fouls (scratch, no ball hit, wrong ball first, no cushion after the
    /// contact) give the opponent ball in hand anywhere. The 8: legally after the own group = win, earlier = loss,
    /// with a foul = loss.
    /// </summary>
    public static class PoolRules
    {
        /// <summary>
        /// After a shot by seat <see cref="PoolState.Turn"/>: copies the final balls into <paramref name="s"/>, decides
        /// fouls, groups, ball in hand, whose turn, game over (Winner / EndReason / Phase = Over, WinsA/B), ShotNo is
        /// NOT changed here (the server bumps it when the shot starts). Nick of each seat for the note.
        /// </summary>
        public static void Apply(PoolState s, PoolSimResult r, string nickA, string nickB)
        {
            if (s == null || r == null) return;
            int me = s.Turn == 1 ? 1 : 0, opp = 1 - me;
            string meN = Nick(me == 0 ? nickA : nickB, me), oppN = Nick(opp == 0 ? nickA : nickB, opp);
            ushort targets = Targets(s, me);
            bool wasBreak = s.Break;
            if (r.FinalPos != null) System.Array.Copy(r.FinalPos, s.Pos, Mathf.Min(16, r.FinalPos.Length));
            s.Down = r.FinalDown;
            s.Break = false;
            s.BallInHand = false;
            s.Kitchen = false;

            bool scratch = (r.FinalDown & 1) != 0 || r.Pocketed.Contains(0);
            bool eight = r.Pocketed.Contains(8);
            var objs = new List<int>();
            foreach (int b in r.Pocketed)
                if (b > 0 && b < 16 && b != 8 && !objs.Contains(b)) objs.Add(b);

            if (wasBreak) AfterBreak(s, r, me, opp, meN, oppN, scratch, eight, objs);
            else AfterShot(s, r, me, opp, meN, oppN, scratch, eight, objs, targets);
            s.Quantize();
        }

        /// <summary>Balls the shooter of <paramref name="seat"/> may legally hit first (bit mask), for the HUD and the aim guide.</summary>
        public static ushort Targets(PoolState s, int seat)
        {
            if (s == null) return PoolState.ObjectMask;
            ushort on = s.OnTable;
            if (s.Break) return on;
            int g = s.GroupOf(seat);
            if (g == 0)
            {
                ushort m = (ushort)(on & ~PoolState.EightMask);
                return m != 0 ? m : on;
            }
            ushort own = (ushort)(on & PoolState.GroupMask(g));
            return own != 0 ? own : (ushort)(on & PoolState.EightMask);
        }

        static void AfterBreak(PoolState s, PoolSimResult r, int me, int opp, string meN, string oppN, bool scratch, bool eight, List<int> objs)
        {
            string spot = "";
            if (eight)
            {
                s.Down = (ushort)(s.Down & ~PoolState.EightMask);
                s.Pos[8] = SpotOnFootString(s, 8);
                spot = "8 wraca na stół. ";
            }
            bool legal = objs.Count > 0 || eight || r.BallsToRail >= 4;
            if (scratch)
            {
                Foul(s, opp, true);
                s.Note = spot + "Faul: biała w łuzie - bila z ręki dla " + oppN;
            }
            else if (!legal)
            {
                Foul(s, opp, false);
                s.Note = "Faul: za słabe rozbicie - bila z ręki dla " + oppN;
            }
            else if (objs.Count > 0 || eight)
            {
                s.Turn = (byte)me;
                s.Note = spot + (objs.Count > 0 ? Potted(objs) + " - " : "") + meN + " gra dalej";
            }
            else
            {
                s.Turn = (byte)opp;
                s.Note = "Rozbicie bez wbicia - teraz " + oppN;
            }
        }

        static void AfterShot(PoolState s, PoolSimResult r, int me, int opp, string meN, string oppN, bool scratch, bool eight, List<int> objs, ushort targets)
        {
            string foul = null;
            if (scratch) foul = "biała w łuzie";
            else if (r.FirstHit < 1 || r.FirstHit > 15) foul = "nie trafiono żadnej bili";
            else if ((targets & (1 << r.FirstHit)) == 0) foul = r.FirstHit == 8 ? "najpierw trafiona 8" : "najpierw trafiona bila rywala";
            else if (objs.Count == 0 && !eight && !r.RailAfterContact) foul = "żadna bila nie dotknęła bandy";

            if (eight)
            {
                if (foul != null) End(s, opp, PoolState.EndEightScratch, "8 wbita z faulem (" + foul + ") - " + oppN + " wygrywa!");
                else if (targets == PoolState.EightMask) End(s, me, PoolState.EndEight, meN + " wbija 8 i wygrywa!");
                else End(s, opp, PoolState.EndEightEarly, "8 wbita za wcześnie - " + oppN + " wygrywa!");
                return;
            }
            if (foul != null)
            {
                Foul(s, opp, false);
                s.Note = "Faul: " + foul + " - bila z ręki dla " + oppN;
                return;
            }
            if (s.Groups == PoolState.Open && objs.Count > 0)
            {
                bool solid = objs[0] < 8;
                s.Groups = solid == (me == 0) ? PoolState.ASolids : PoolState.AStripes;
                s.Turn = (byte)me;
                s.Note = meN + (solid ? " gra pełnymi (1-7)" : " gra połówkami (9-15)") + (s.OnEight(me) ? " - teraz 8" : "");
                return;
            }
            ushort mine = PoolState.GroupMask(s.GroupOf(me));
            bool own = false;
            foreach (int b in objs)
                if ((mine & (1 << b)) != 0) own = true;
            if (own)
            {
                s.Turn = (byte)me;
                s.Note = Potted(objs) + " - " + meN + (s.OnEight(me) ? " gra na 8" : " gra dalej");
            }
            else
            {
                s.Turn = (byte)opp;
                s.Note = (objs.Count > 0 ? Potted(objs) + " - teraz " : "Pudło - teraz ") + oppN;
            }
        }

        /// <summary>Foul: the other seat shoots with the cue ball in hand (kitchen only after a scratch on the break).</summary>
        static void Foul(PoolState s, int to, bool kitchen)
        {
            s.Turn = (byte)to;
            s.BallInHand = true;
            s.Kitchen = kitchen;
            bool cueDown = (s.Down & 1) != 0;
            s.Down = (ushort)(s.Down & ~1);
            Vector2 wish = cueDown || PoolRails.Bad(s.Pos[0]) ? PoolState.HeadSpot2 : s.Pos[0];
            s.Pos[0] = PoolSim.ClampCueBall(s.Pos, s.Down, wish, kitchen);
        }

        static void End(PoolState s, int winner, byte reason, string note)
        {
            s.Phase = PoolState.Over;
            s.Winner = (byte)winner;
            s.EndReason = reason;
            if (winner == 0 && s.WinsA < 255) s.WinsA++;
            if (winner == 1 && s.WinsB < 255) s.WinsB++;
            s.BallInHand = false;
            s.Kitchen = false;
            s.Note = note;
        }

        /// <summary>Free spot for a ball on the long string: the foot spot, else the nearest free point behind it, else in front.</summary>
        static Vector2 SpotOnFootString(PoolState s, int ball)
        {
            float x0 = PoolState.FootSpot2.x;
            for (int dir = 1; dir >= -1; dir -= 2)
                for (int k = 0; k < 2000; k++)
                {
                    float x = x0 + dir * k * 0.001f;
                    if (x > PoolRails.FieldX || x < -PoolRails.FieldX) break;
                    var p = new Vector2(x, 0f);
                    if (Free(s, p, ball)) return p;
                }
            return PoolState.FootSpot2;
        }

        static bool Free(PoolState s, Vector2 p, int self)
        {
            const float min = PoolDims.BallD + 0.0002f;
            for (int j = 0; j < 16; j++)
                if (j != self && !s.IsDown(j) && (s.Pos[j] - p).sqrMagnitude < min * min) return false;
            return true;
        }

        /// <summary>"Wbita 3" / "Wbite 3, 11" (at most 4 numbers).</summary>
        static string Potted(List<int> balls)
        {
            if (balls.Count == 1) return "Wbita " + balls[0];
            var sb = new System.Text.StringBuilder("Wbite ");
            for (int i = 0; i < balls.Count && i < 4; i++)
                sb.Append(i > 0 ? ", " : "").Append(balls[i]);
            if (balls.Count > 4) sb.Append("...");
            return sb.ToString();
        }

        static string Nick(string n, int seat)
        {
            n = n?.Trim();
            if (string.IsNullOrEmpty(n)) return seat == 0 ? "Gracz 1" : "Gracz 2";
            return n.Length > 20 ? n.Substring(0, 20) : n;
        }
    }
}
