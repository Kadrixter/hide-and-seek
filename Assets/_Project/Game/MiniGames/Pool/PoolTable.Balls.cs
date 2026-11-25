using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// The sixteen 3D balls: built one per frame once their (1024 x 512) textures are computed off the main thread,
    /// placed from <see cref="ShownPos"/> every frame, rolled by the distance they travel (axis = up x direction,
    /// angle = distance / radius). A pocketed ball runs into the hole and down the ball return to the tray under the
    /// foot rail (PoolTable.Return) and lies there until the table is racked again; a ball coming back (the cue ball
    /// after a scratch once the shooter picks it up, a new rack) pops up at its new spot. Outside a replay the shown
    /// balls follow <see cref="State"/>: small differences glide, large ones snap.
    /// </summary>
    public sealed partial class PoolTable
    {
        /// <summary>Ball texture width in the lobby (2:1, 1024 x 512).</summary>
        public const int BallTexW = 1024;
        const float PopTime = 0.25f, GlideSpeed = 0.2f, SnapDist = 0.03f;

        Transform _ballRoot;
        readonly Transform[] _balls = new Transform[16];
        Material _template;
        int _built;
        float _buildWait;
        readonly Quaternion[] _rot = new Quaternion[16];
        readonly Vector3[] _placed = new Vector3[16];
        readonly bool[] _hasPlaced = new bool[16];
        readonly float[] _pop = new float[16];

        /// <summary>All sixteen balls exist.</summary>
        public bool BallsReady => _built >= 16;
        /// <summary>3D ball <paramref name="i"/> (null until built). Its local position / rotation are table space.</summary>
        public Transform BallTransform(int i) => i >= 0 && i < 16 ? _balls[i] : null;
        /// <summary>Ball <paramref name="i"/> is pocketed: dropping, rolling down the ball return or lying in its tray.</summary>
        public bool ShownInPocket(int i) => i >= 0 && i < 16 && _ret[i] >= 0;

        void InitBalls(Material template)
        {
            _template = template;
            _ballRoot = new GameObject("Balls") { layer = gameObject.layer }.transform;
            _ballRoot.SetParent(transform, false);
            var rng = new System.Random(4711);
            for (int i = 0; i < 16; i++)
            {
                _ret[i] = -1;
                _pop[i] = 1f;
                // Resting balls lie any way up, the numbers turned every which way.
                _rot[i] = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
            }
            var start = PoolBallModels.RackPositions();
            var order = PoolBallModels.RackOrder();
            ShownPos[0] = new Vector2(PoolDims.HeadSpot.x, 0f);
            for (int k = 0; k < 15; k++) ShownPos[order[k]] = new Vector2(start[k].x, start[k].z);
        }

        /// <summary>One ball per frame (texture upload + compression), after the worker threads finished the pixels (or 8 s at the latest).</summary>
        void BuildNextBall()
        {
            if (_built >= 16) return;
            _buildWait += Time.unscaledDeltaTime;
            if (!PoolBallModels.PixelsReady(BallTexW) && _buildWait < 8f) return;
            var g = PoolBallModels.Build(_ballRoot, _template, _built, BallTexW);
            var r = g.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            _balls[_built] = g.transform;
            _hasPlaced[_built] = false;
            _built++;
        }

        // ================================================================ pockets

        /// <summary>Pocket nearest to a table point.</summary>
        static int NearestPocket(Vector2 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int k = 0; k < 6; k++)
            {
                var c = PoolTableModel.PocketCentre(k);
                float d = (new Vector2(c.x, c.z) - p).sqrMagnitude;
                if (d < bd) { bd = d; best = k; }
            }
            return best;
        }

        Vector3 ClothPos(Vector2 p) => new Vector3(p.x, PoolDims.ClothY + PoolDims.BallR, p.y);

        /// <summary>Where ball i is drawn right now (table space).</summary>
        Vector3 CurrentLocal(int i)
        {
            if (_ret[i] >= 0) return ReturnPos(i);
            if (i == 0 && !Rolling)
            {
                if (HeldCuePos.HasValue) return ClothPos(HeldCuePos.Value);
                // Ball in hand: the shooter's hand position (local live, others from his synced aim; PoolTable.Aim).
                if (HandBall(out var hand)) return ClothPos(hand);
            }
            return ClothPos(ShownPos[i]);
        }

        // ================================================================ following the server state

        /// <summary>Outside a replay: shown balls -> <see cref="State"/> (pocketed ones down the return, returned balls pop up, small moves glide).</summary>
        void SyncToState(float dt, bool instant)
        {
            if (!_hasState || State.ShotNo < _shownShot) return; // an older state than what is shown: wait for the newer one
            _shownShot = State.ShotNo;
            for (int i = 0; i < 16; i++)
            {
                if (State.IsDown(i))
                {
                    // The state keeps a pocketed ball's last spot on the cloth: its nearest pocket is the one it went into.
                    if (_ret[i] < 0) PutInReturn(i, NearestPocket(State.Pos[i]), !instant && _hasPlaced[i], CurrentLocal(i), _retClock);
                    continue;
                }
                Vector2 want = State.Pos[i];
                if (_ret[i] >= 0)
                {
                    // Ball in hand: the cue ball waits in the tray until the shooter takes it out.
                    if (i == 0 && !instant && KeepCueInReturn())
                    {
                        ShownPos[i] = want;
                        continue;
                    }
                    TakeOutOfReturn(i);
                    ShownPos[i] = want;
                    _hasPlaced[i] = false;
                    _pop[i] = instant ? 1f : 0f;
                    continue;
                }
                float d = (want - ShownPos[i]).magnitude;
                if (instant || d > SnapDist)
                {
                    ShownPos[i] = want;
                    if (!instant && d > 0.1f) { _pop[i] = 0f; _hasPlaced[i] = false; }
                }
                else ShownPos[i] = Vector2.MoveTowards(ShownPos[i], want, dt * GlideSpeed);
            }
        }

        void UpdateShownDown()
        {
            int m = 0;
            for (int i = 0; i < 16; i++) if (_ret[i] >= 0) m |= 1 << i;
            ShownDown = (ushort)m;
        }

        /// <summary>Moves the 3D balls: position, rolling rotation, pop-up scale (the return's queue first).</summary>
        void PlaceBalls(float dt)
        {
            TickReturn(dt);
            for (int i = 0; i < 16; i++)
            {
                if (_pop[i] < 1f) _pop[i] = Mathf.Min(1f, _pop[i] + dt / PopTime);
                var b = _balls[i];
                if (b == null) continue;
                Vector3 p = CurrentLocal(i);
                if (_hasPlaced[i])
                {
                    Vector3 d = p - _placed[i];
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist > 1e-6f && dist < 0.15f)
                    {
                        Vector3 axis = Vector3.Cross(Vector3.up, d / dist);
                        _rot[i] = Quaternion.AngleAxis(dist / PoolDims.BallR * Mathf.Rad2Deg, axis) * _rot[i];
                    }
                }
                _placed[i] = p;
                _hasPlaced[i] = true;
                float s = _pop[i] >= 1f ? 1f : Mathf.SmoothStep(0.05f, 1f, _pop[i]);
                b.SetLocalPositionAndRotation(p + Vector3.up * ((1f - s) * PoolDims.BallR), _rot[i]);
                b.localScale = new Vector3(s, s, s);
            }
        }
    }
}
