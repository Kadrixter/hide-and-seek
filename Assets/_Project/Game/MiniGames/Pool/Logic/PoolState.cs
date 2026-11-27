using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Whole state of the lobby pool table (8-ball), replicated as one text network variable (MatchManager.PoolNet,
    /// FixedString512Bytes - <see cref="Encode"/> stays under 480 bytes, see PoolState.Codec.cs). Ball positions live on
    /// a 0.05 mm grid (<see cref="Quant"/>), so the decoded copy on a client equals the server's exactly.
    /// Players: two teams (A and B) of one or two players in four places - place 0 = A1, 1 = B1, 2 = A2, 3 = B2 (team =
    /// place &amp; 1). "Seat" in the rules (Turn, Groups, Winner, WinsA / WinsB) means the TEAM; inside a team the players
    /// take the team's innings in turn (<see cref="Up"/>), a lone player plays all of his team's turns.
    /// </summary>
    public sealed partial class PoolState
    {
        public const ulong Nobody = ulong.MaxValue;
        public const byte Waiting = 0, Playing = 1, Over = 2;
        /// <summary>Number of places at the table (two per team).</summary>
        public const int Places = 4;
        /// <summary>Groups: 0 = open table, 1 = team A has the solids (1-7), 2 = team A has the stripes (9-15).</summary>
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
        /// <summary>Players in the places A1, B1 (the first of each team) and A2, B2 (the second).</summary>
        public ulong A = Nobody, B = Nobody, A2 = Nobody, B2 = Nobody;
        /// <summary>Team (0 = A, 1 = B) to shoot next.</summary>
        public byte Turn;
        /// <summary>Bit t = team t's next inning is played by its second place (flips when the team leaves the table).</summary>
        public byte Up;
        public byte Groups;
        /// <summary>The shooter may place the cue ball first (after a foul); <see cref="Kitchen"/> = only behind the head string.</summary>
        public bool BallInHand, Kitchen;
        /// <summary>The next shot is the break.</summary>
        public bool Break;
        /// <summary>Winner team (255 = none) and <see cref="EndReason"/> of the last game.</summary>
        public byte Winner = 255, EndReason;
        /// <summary>Games won by team A / B with the current line-up.</summary>
        public byte WinsA, WinsB;
        /// <summary>Counts every game (a new rack = +1).</summary>
        public int Round;
        /// <summary>Counts the shots (every stroke = +1; the clients replay shot n when it changes).</summary>
        public int ShotNo;
        /// <summary>"Gotowy" of each place (bit p): a game starts when every seated player is ready.</summary>
        public byte Ready;
        /// <summary>Short Polish note about the last shot for the HUD ("Faul: biała w łuzie", "Pełne dla Kasi").</summary>
        public string Note = "";
        /// <summary>Ball centres (table space 2D); index 0 = cue ball, 1..15 object balls.</summary>
        public readonly Vector2[] Pos = new Vector2[16];
        /// <summary>Bit i = ball i is pocketed (the cue ball's bit is cleared again when it comes back in hand).</summary>
        public ushort Down;

        /// <summary>Player in place p (0 = A1, 1 = B1, 2 = A2, 3 = B2).</summary>
        public ulong Place(int p) => p switch { 0 => A, 1 => B, 2 => A2, 3 => B2, _ => Nobody };
        public void SetPlace(int p, ulong id)
        {
            if (p == 0) A = id;
            else if (p == 1) B = id;
            else if (p == 2) A2 = id;
            else if (p == 3) B2 = id;
        }

        /// <summary>Place of a player, -1 = not at the table.</summary>
        public int PlaceOf(ulong id)
        {
            if (id == Nobody) return -1;
            for (int p = 0; p < Places; p++)
                if (Place(p) == id) return p;
            return -1;
        }

        public static int TeamOf(int place) => place & 1;
        /// <summary>Players in a team (0..2).</summary>
        public int Members(int team) => (Place(team) != Nobody ? 1 : 0) + (Place(team + 2) != Nobody ? 1 : 0);
        public int Seated => Members(0) + Members(1);
        /// <summary>Bit mask of the occupied places.</summary>
        public byte Occupied
        {
            get
            {
                int m = 0;
                for (int p = 0; p < Places; p++) if (Place(p) != Nobody) m |= 1 << p;
                return (byte)m;
            }
        }

        /// <summary>Place that plays team <paramref name="team"/>'s next inning (its other place when that one is empty).</summary>
        public int ShooterPlace(int team)
        {
            team &= 1;
            int p = team + 2 * ((Up >> team) & 1), q = team + 2 - (p - team);
            return Place(p) != Nobody || Place(q) == Nobody ? p : q;
        }

        /// <summary>Place of the player to shoot now.</summary>
        public int Shooter => ShooterPlace(Turn);
        public ulong ShooterId => Place(Shooter);
        public bool IsDown(int ball) => (Down & (1 << ball)) != 0;
        /// <summary>Group of a team: 0 = none yet, 1 = solids, 2 = stripes.</summary>
        /// (a place 0..3 works too: team = place &amp; 1 - place A2 = 2 used to get team B's group).
        public int GroupOf(int seat) => Groups == Open ? 0 : (Groups == ASolids) == ((seat & 1) == 0) ? 1 : 2;

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
        /// kitchen, break next, open table, no winner, every team's first place up. Players, Phase, Turn, wins, Round,
        /// ShotNo, Ready, Note untouched.
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
            Up = 0;
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
            A2 = o.A2;
            B2 = o.B2;
            Turn = o.Turn;
            Up = o.Up;
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
            Ready = o.Ready;
            Note = o.Note ?? "";
            System.Array.Copy(o.Pos, Pos, 16);
            Down = o.Down;
        }
    }
}
