using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Pool table in the lobby on every peer (the "net" agent owns this file, .Balls, .Replay, .Sfx; the "play" agent
    /// owns PoolTable.Aim / .Cue / .Pose / .Camera). The table's transform = TABLE SPACE of <see cref="PoolDims"/>
    /// (origin on the floor under the centre of the cloth, long axis local X, head / kitchen at -X). Follows
    /// <see cref="MatchManager.PoolNet"/> (<see cref="State"/> is always the newest server state); a stroke arrives as
    /// <see cref="OnShotMessage"/> and is simulated again here and replayed (<see cref="Rolling"/>); when it is over the
    /// balls glide / snap onto the server's positions. Four places ("seat" in the MiniTable API = place, see
    /// <see cref="PoolState"/>): team A waits at the long side at local -Z, team B at +Z, <see cref="StandOut"/> from
    /// the rail, the first player of a team towards the head, the second towards the foot.
    /// </summary>
    public sealed partial class PoolTable : MiniTable
    {
        public override byte Game => MatchManager.GamePool;
        public override string Title => "bilard";
        public override string JoinVerb => "podejdź";
        /// <summary>How far from the long rail the players wait.</summary>
        public const float StandOut = 0.85f;
        protected override float StoolZ => PoolTableModel.OuterW * 0.5f + StandOut;
        protected override float TopY => PoolDims.RailTopY;
        protected override float TableHalf => PoolTableModel.OuterW * 0.5f;
        public override float SurfaceY => PoolDims.ClothY;
        public override int Places => PoolState.Places;
        public override ulong SeatPlayer(int seat) => State.Place(seat);
        public override string SeatName(int seat) => (seat & 1) == 0 ? "drużyna A" : "drużyna B";
        public override bool Playing => State.Phase == PoolState.Playing;

        /// <summary>Last replicated state (the newest from the server; during a replay it may already hold the shot's result).</summary>
        public readonly PoolState State = new();
        public PoolTableModel Model { get; private set; }
        public PoolLampModel Lamp { get; private set; }
        /// <summary>Balls as shown right now (table space 2D) - during a replay the current replay frame, else the state.</summary>
        public readonly Vector2[] ShownPos = new Vector2[16];
        /// <summary>Bit i = ball i is shown pocketed (dropping, rolling down the ball return or lying in its tray).</summary>
        public ushort ShownDown;
        /// <summary>A shot is being replayed (balls rolling) or its result has not arrived yet: no aiming.</summary>
        public bool Rolling { get; private set; }
        /// <summary>Seat of the local player at this table (-1 = not at it).</summary>
        public int LocalSeat => NetPlayer.Local != null ? SeatOf(NetPlayer.Local.NetworkObjectId) : -1;
        /// <summary>The local player may shoot now (seated, game running, his turn, nothing rolling).</summary>
        public bool LocalTurn => LocalSeat >= 0 && Playing && State.Shooter == LocalSeat && !Rolling;
        /// <summary>Bumped on every new server state (HUD watches it).</summary>
        public int Version { get; private set; }
        /// <summary>A state has arrived at all.</summary>
        public bool HasState => _hasState;
        /// <summary>
        /// Set by the aiming code while the local player drags the cue ball in hand (table space): ball 0 is drawn
        /// there instead of <see cref="ShownPos"/>[0]. Local only; null = no override.
        /// </summary>
        public Vector2? HeldCuePos { get; set; }
        /// <summary>The aiming code hides the cursor (the seated controller then stops forcing it visible).</summary>
        public static bool CursorHidden { get; set; }

        /// <summary>Table space 2D point -> world (at ball-centre height above the cloth by default).</summary>
        public Vector3 ToWorld(Vector2 p, float lift = PoolDims.BallR) => transform.TransformPoint(new Vector3(p.x, PoolDims.ClothY + lift, p.y));
        /// <summary>World point -> table space 2D.</summary>
        public Vector2 ToTable(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return new Vector2(l.x, l.z);
        }

        // ---- hooks for the "play" agent's partial files (implemented there; empty when missing)
        /// <summary>Every frame after the balls were updated.</summary>
        partial void TickPlay(float dt);
        /// <summary>A shot starts (all peers): <paramref name="seat"/> struck the cue ball with <paramref name="shot"/>.</summary>
        partial void OnShotStarted(int seat, PoolShot shot);
        /// <summary>A new replicated state arrived (after <see cref="State"/> was updated).</summary>
        partial void OnStateChanged();

        string _lastNet;
        bool _hasState, _snapNext;
        int _endSoundRound = -1;

        public static PoolTable Create(Transform parent, Vector3 pos, Quaternion rot, Material template)
        {
            PoolBallModels.Prewarm(BallTexW); // ball textures computed on worker threads meanwhile
            var go = new GameObject("PoolTable") { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var t = go.AddComponent<PoolTable>();
            t.Model = PoolTableModel.Build(go.transform, template, LobbyLayout.Garage ? TableKit.GarageStyle : 0);
            t.BuildProps(template, pos.y);
            t.InitBalls(template);
            t.InitSfx();
            return t;
        }

        /// <summary>The lamp over the table (only the middle spot casts shadows) and two cubes of chalk on the long rails.</summary>
        void BuildProps(Material template, float floorY)
        {
            Lamp = PoolLampModel.Build(transform, template, LobbyLayout.Garage ? TableKit.GarageStyle : 0, LobbyLayout.H - floorY, true);
            if (Lamp.Lights != null)
                for (int i = 0; i < Lamp.Lights.Length; i++)
                {
                    var l = Lamp.Lights[i];
                    if (l == null) continue;
                    bool mid = i == Lamp.Lights.Length / 2;
                    l.shadows = mid ? LightShadows.Soft : LightShadows.None;
                    if (mid) l.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
                }
            float railZ = PoolTableGeom.BedZ + PoolTableGeom.RailW * 0.4f;
            var c1 = PoolChalkModel.Build(transform, template, 0.35f);
            c1.SetLocalPositionAndRotation(new Vector3(-0.58f, PoolDims.RailTopY, -railZ), Quaternion.Euler(0f, 23f, 0f));
            var c2 = PoolChalkModel.Build(transform, template, 0.7f);
            c2.SetLocalPositionAndRotation(new Vector3(0.61f, PoolDims.RailTopY, railZ), Quaternion.Euler(0f, -11f, 0f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var mm = MatchManager.Instance;
            if (mm != null)
            {
                string net = mm.PoolNet.Value.ToString();
                if (net != _lastNet)
                {
                    _lastNet = net;
                    var ns = new PoolState();
                    if (PoolState.TryDecode(net, ns)) ReceiveState(net);
                }
            }
            BuildNextBall();
            _retClock += dt;
            TickReplay(dt);
            if (!Rolling) SyncToState(dt, _snapNext);
            _snapNext = false;
            UpdateShownDown();
            if (!Rolling && State.Phase == PoolState.Over && State.Round != _endSoundRound && _hasState)
            {
                _endSoundRound = State.Round;
                MiniGameSounds.Win(this);
            }
            TickPlay(dt);
            PlaceBalls(dt);
            TickSfx(dt);
            DrivePoses();
            TickLamp(dt);
        }

        void ReceiveState(string net)
        {
            bool fresh = !_hasState;
            if (!PoolState.TryDecode(net, State)) return;
            _hasState = true;
            Version++;
            if (fresh)
            {
                _snapNext = true; // late join / first state: everything in place at once
                _endSoundRound = State.Round;
            }
            OnStateChanged();
        }

        // ---- reach for the "E" prompt / the server's distance check: the table outline, not one point

        /// <summary>Horizontal distance from <paramref name="p"/> to the table's outer edge (0 = over the table).</summary>
        public float DistanceToEdge(Vector3 p)
        {
            var l = transform.InverseTransformPoint(p);
            float dx = Mathf.Max(Mathf.Abs(l.x) - PoolTableModel.OuterL * 0.5f, 0f), dz = Mathf.Max(Mathf.Abs(l.z) - PoolTableModel.OuterW * 0.5f, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public override bool PromptNear(Vector3 eye) => DistanceToEdge(eye) <= 1.7f;

        public override Vector3 PromptTarget(Vector3 eye)
        {
            var l = transform.InverseTransformPoint(eye);
            float x = Mathf.Clamp(l.x, -PoolDims.PlayL * 0.4f, PoolDims.PlayL * 0.4f), z = Mathf.Clamp(l.z, -PoolDims.PlayW * 0.3f, PoolDims.PlayW * 0.3f);
            return transform.TransformPoint(new Vector3(x, PoolDims.ClothY, z));
        }

        /// <summary>Distance to the table edge, with the seat's own waiting spot as a small tie-break (the nearer side wins).</summary>
        public override float JoinDistance(Vector3 pos, int seat) => DistanceToEdge(pos) + 0.05f * base.JoinDistance(pos, seat);
    }
}
