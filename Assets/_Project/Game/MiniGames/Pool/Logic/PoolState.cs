using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Whole state of the lobby pool table (8-ball), replicated as one text network variable (MatchManager.PoolNet,
    /// FixedString512Bytes - <see cref="Encode"/> stays under 480 bytes, see PoolState.Codec.cs). Ball positions live on
    /// a 0.05 mm grid (<see cref="Quant"/>), so the decoded copy on a client equals the server's exactly.
    /// </summary>
    public sealed partial class PoolState
    {
        public const ulong Nobody = ulong.MaxValue;
        public const byte Waiting = 0, Playing = 1, Over = 2;
        /// <summary>Groups: 0 = open table, 1 = seat A has the solids (1-7), 2 = seat A has the stripes (9-15).</summary>
        public const byte Open = 0, ASolids = 1, AStripes = 2;
        /// <summary>Why a game ended (EndReason).</summary>
        public const byte EndNone = 0, EndEight = 1, EndEightEarly = 2, EndEightScratch = 3, EndLeft = 4;
        /// <summary>Ball masks: solids 1-7, stripes 9-15, the 8, all object balls 1-15.</summary>
        public const ushort SolidsMask = 0x00FE, StripesMask = 0xFE00, EightMask = 0x0100, ObjectMask = 0xFFFE;
        /// <summary>Head string: the kitchen is x &lt;= KitchenX (the head spot's x).</summary>
        public static readonly float KitchenX = PoolDims.HeadSpot.x;
        /// <summary>Head spot (cue ball start) and foot spot (apex of the rack) in table space 2D.</summary>
        public static readonly Vector2 HeadSpot2 = new Vector2(PoolDims.HeadSpot.x, 0f), FootSpot2 = new Vector2(PoolDims.FootSpot.x, 0f);
        /// <summary>Grid of the ball positions (m) and the gap between racked balls.</summary>
        public const float QStep = 0.00005f, RackGap = 0.0001f;

        public byte Phase;
        public ulong A = Nobody, B = Nobody;
        /// <summary>Seat (0 = A, 1 = B) to shoot next.</summary>
        public byte Turn;
        public byte Groups;
        /// <summary>The shooter may place the cue ball first (after a foul); <see cref="Kitchen"/> = only behind the head string.</summary>
        public bool BallInHand, Kitchen;
        /// <summary>The next shot is the break.</summary>
        public bool Break;
        /// <summary>Winner seat (255 = none) and <see cref="EndReason"/> of the last game.</summary>
        public byte Winner = 255, EndReason;
        /// <summary>Games won by seat A / B with the current pair of players.</summary>
        public byte WinsA, WinsB;
        /// <summary>Counts every game (a new rack = +1).</summary>
        public int Round;
        /// <summary>Counts the shots (every stroke = +1; the clients replay shot n when it changes).</summary>
        public int ShotNo;
        /// <summary>Rematch wishes (bit 0 = seat A, bit 1 = seat B).</summary>
        public byte Rematch;
        /// <summary>Short Polish note about the last shot for the HUD ("Faul: biała w łuzie", "Pełne dla Kasi").</summary>
        public string Note = "";
        /// <summary>Ball centres (table space 2D); index 0 = cue ball, 1..15 object balls.</summary>
        public readonly Vector2[] Pos = new Vector2[16];
        /// <summary>Bit i = ball i is pocketed (the cue ball's bit is cleared again when it comes back in hand).</summary>
        public ushort Down;

        public ulong Seat(int s) => s == 0 ? A : B;
        public int SeatOf(ulong id) => id == Nobody ? -1 : id == A ? 0 : id == B ? 1 : -1;
        public bool IsDown(int ball) => (Down & (1 << ball)) != 0;
        /// <summary>Group of a seat: 0 = none yet, 1 = solids, 2 = stripes.</summary>
        public int GroupOf(int seat) => Groups == Open ? 0 : (Groups == ASolids) == (seat == 0) ? 1 : 2;

        /// <summary>Object balls still on the table (bit mask, bit 0 never set).</summary>
        public ushort OnTable => (ushort)(~Down & ObjectMask);
        /// <summary>Mask of a group (1 = solids, 2 = stripes, else 0).</summary>
        public static ushort GroupMask(int group) => group == 1 ? SolidsMask : group == 2 ? StripesMask : (ushort)0;
        /// <summary>Is ball n a solid (1-7) / a stripe (9-15)?</summary>
        public static bool IsSolid(int n) => n >= 1 && n <= 7;
        public static bool IsStripe(int n) => n >= 9 && n <= 15;
        /// <summary>Balls of the seat's group still on the table (0 = shooting at the 8); -1 while the table is open.</summary>
        public int LeftOf(int seat)
        {
            int g = GroupOf(seat);
            return g == 0 ? -1 : Count((ushort)(OnTable & GroupMask(g)));
        }

        /// <summary>The seat is on the 8 (own group cleared, 8 still up).</summary>
        public bool OnEight(int seat) => LeftOf(seat) == 0 && !IsDown(8);

        public static int Count(ushort m)
        {
            int n = 0;
            for (; m != 0; m &= (ushort)(m - 1)) n++;
            return n;
        }

        /// <summary>A coordinate snapped to the network grid (NaN / infinity -> 0, clamped to +-6.5 m).</summary>
        public static float Quant(float x) => QInt(x) * QStep;

        internal static int QInt(float x)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) return 0;
            return (int)Mathf.Round(Mathf.Clamp(x, -6.5f, 6.5f) / QStep);
        }

        /// <summary>Snaps every ball onto the network grid (done after a rack and after each shot).</summary>
        public void Quantize()
        {
            for (int i = 0; i < 16; i++) Pos[i] = new Vector2(Quant(Pos[i].x), Quant(Pos[i].y));
        }

        /// <summary>
        /// A fresh rack: 15 balls in the triangle (apex = ball 1 on the foot spot, widening towards +X, 0.1 mm apart),
        /// the 8 in the middle, one solid and one stripe in the back corners, the rest shuffled with <paramref name="rng"/>
        /// (null = the fixed order of <see cref="PoolBallModels.RackOrder"/>). Cue ball on the head spot, in hand in the
        /// kitchen, break next, open table, no winner. Players, Phase, Turn, wins, Round, ShotNo, Rematch, Note untouched.
        /// </summary>
        public void Rack(System.Random rng)
        {
            int[] order = PoolBallModels.RackOrder();
            if (rng != null)
            {
                // Slots: 0 apex (1), 4 middle (8), 10 / 14 back corners (a solid and a stripe), the other 11 shuffled.
                var solids = new System.Collections.Generic.List<int> { 2, 3, 4, 5, 6, 7 };
                var stripes = new System.Collections.Generic.List<int> { 9, 10, 11, 12, 13, 14, 15 };
                int cs = solids[rng.Next(solids.Count)], ct = stripes[rng.Next(stripes.Count)];
                solids.Remove(cs);
                stripes.Remove(ct);
                bool solidLeft = rng.Next(2) == 0;
                var rest = new System.Collections.Generic.List<int>(solids);
                rest.AddRange(stripes);
                for (int i = rest.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (rest[i], rest[j]) = (rest[j], rest[i]);
                }
                order = new int[15];
                order[0] = 1;
                order[4] = 8;
                order[10] = solidLeft ? cs : ct;
                order[14] = solidLeft ? ct : cs;
                int k = 0;
                for (int s = 0; s < 15; s++)
                    if (s != 0 && s != 4 && s != 10 && s != 14) order[s] = rest[k++];
            }
            float step = PoolDims.BallD + RackGap, rowStep = step * 0.8660254f;
            int slot = 0;
            for (int row = 0; row < 5; row++)
                for (int k = 0; k <= row; k++)
                    Pos[order[slot++]] = new Vector2(FootSpot2.x + row * rowStep, (k - row * 0.5f) * step);
            Pos[0] = HeadSpot2;
            Down = 0;
            Groups = Open;
            Break = true;
            BallInHand = true;
            Kitchen = true;
            Winner = 255;
            EndReason = EndNone;
            Quantize();
        }

        /// <summary>A copy of this state.</summary>
        public PoolState Clone()
        {
            var s = new PoolState();
            s.CopyFrom(this);
            return s;
        }

        public void CopyFrom(PoolState o)
        {
            if (o == null || o == this) return;
            Phase = o.Phase;
            A = o.A;
            B = o.B;
            Turn = o.Turn;
            Groups = o.Groups;
            BallInHand = o.BallInHand;
            Kitchen = o.Kitchen;
            Break = o.Break;
            Winner = o.Winner;
            EndReason = o.EndReason;
            WinsA = o.WinsA;
            WinsB = o.WinsB;
            Round = o.Round;
            ShotNo = o.ShotNo;
            Rematch = o.Rematch;
            Note = o.Note ?? "";
            System.Array.Copy(o.Pos, Pos, 16);
            Down = o.Down;
        }
    }
}
