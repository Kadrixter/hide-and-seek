using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// The sixteen 3D balls: built one per frame once their (1024 x 512) textures are computed off the main thread,
    /// placed from <see cref="ShownPos"/> every frame, rolled by the distance they travel (axis = up x direction,
    /// angle = distance / radius). A pocketed ball runs into the hole, drops into the net basket and stays there,
    /// piled on the balls already in it, until the table is racked again; a ball coming back (the cue ball after a
    /// scratch, a new rack) pops up at its new spot. Outside a replay the shown balls follow <see cref="State"/>:
    /// small differences glide, large ones snap.
    /// </summary>
    public sealed partial class PoolTable
    {
        /// <summary>Ball texture width in the lobby (2:1, 1024 x 512).</summary>
        public const int BallTexW = 1024;
        const float FallInto = 0.12f, PopTime = 0.25f, GlideSpeed = 0.2f, SnapDist = 0.03f;

        Transform _ballRoot;
        readonly Transform[] _balls = new Transform[16];
        Material _template;
        int _built;
        float _buildWait;
        readonly Quaternion[] _rot = new Quaternion[16];
        readonly Vector3[] _placed = new Vector3[16];
        readonly bool[] _hasPlaced = new bool[16];
        readonly float[] _pop = new float[16];
        /// <summary>Pocket a ball lies in (-1 = on the cloth), its place in that pocket's pile, seconds since it dropped.</summary>
        readonly int[] _bag = new int[16], _bagSlot = new int[16];
        readonly float[] _fallT = new float[16];
        readonly float[] _fallDur = new float[16];
        readonly Vector3[] _fallFrom = new Vector3[16];
        readonly int[] _pile = new int[6];

        /// <summary>All sixteen balls exist.</summary>
        public bool BallsReady => _built >= 16;
        /// <summary>3D ball <paramref name="i"/> (null until built). Its local position / rotation are table space.</summary>
        public Transform BallTransform(int i) => i >= 0 && i < 16 ? _balls[i] : null;
        /// <summary>Ball <paramref name="i"/> lies in (or drops into) a pocket.</summary>
        public bool ShownInPocket(int i) => i >= 0 && i < 16 && _bag[i] >= 0;

        void InitBalls(Material template)
        {
            _template = template;
            _ballRoot = new GameObject("Balls") { layer = gameObject.layer }.transform;
            _ballRoot.SetParent(transform, false);
            var rng = new System.Random(4711);
            for (int i = 0; i < 16; i++)
            {
                _bag[i] = -1;
                _pop[i] = 1f;
                _fallT[i] = 99f;
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

        /// <summary>Resting spot of the <paramref name="slot"/>-th ball in a pocket's basket (table space).</summary>
        static Vector3 SlotPos(int pocket, int slot)
        {
            var c = PoolTableModel.PocketCentre(pocket);
            float depth = PoolTableGeom.IsSide(pocket) ? PoolTableGeom.SideBagDepth : PoolTableGeom.CornerBagDepth;
            float bottom = c.y + PoolTableGeom.BagRimY - depth + 0.03f;
            if (slot == 0) return new Vector3(c.x, bottom, c.z);
            // Further balls settle on the ones under them, a little off centre, up to just under the cloth; then a ring.
            int layer = Mathf.Min(slot, 3);
            float a = slot * 2.4f + pocket, off = slot <= 3 ? 0.012f : 0.028f;
            float y = slot <= 3 ? bottom + layer * 0.043f : bottom + 0.11f;
            return new Vector3(c.x + Mathf.Cos(a) * off, y, c.z + Mathf.Sin(a) * off);
        }

        /// <summary>Puts ball <paramref name="i"/> into a pocket: dropping from <paramref name="from"/> (table space) or lying there at once.</summary>
        void PutInBag(int i, int pocket, bool animate, Vector3 from)
        {
            if (_bag[i] >= 0) return;
            pocket = Mathf.Clamp(pocket, 0, 5);
            _bag[i] = pocket;
            _bagSlot[i] = _pile[pocket]++;
            _fallFrom[i] = from;
            float drop = Mathf.Max(0.02f, PoolDims.ClothY - SlotPos(pocket, _bagSlot[i]).y);
            _fallDur[i] = FallInto + Mathf.Max(0.08f, Mathf.Sqrt(2f * drop / 9.81f));
            _fallT[i] = animate ? 0f : 99f;
        }

        void TakeOutOfBag(int i)
        {
            int p = _bag[i];
            if (p < 0) return;
            _bag[i] = -1;
            _pile[p] = Mathf.Max(0, _pile[p] - 1);
            for (int k = 0; k < 16; k++)
                if (_bag[k] == p && _bagSlot[k] > _bagSlot[i]) _bagSlot[k]--;
            _fallT[i] = 99f;
        }

        /// <summary>Position of a pocketed ball: into the hole, then falling into its place in the basket.</summary>
        Vector3 BagPos(int i)
        {
            int p = _bag[i];
            Vector3 slot = SlotPos(p, _bagSlot[i]);
            float t = _fallT[i];
            if (t >= _fallDur[i]) return slot;
            var c = PoolTableModel.PocketCentre(p);
            Vector3 mouth = new Vector3(c.x, c.y - PoolDims.BallR * 0.5f, c.z), from = _fallFrom[i];
            if (t < FallInto)
            {
                float k = t / FallInto, e = 1f - (1f - k) * (1f - k);
                return new Vector3(Mathf.Lerp(from.x, mouth.x, e), Mathf.Lerp(from.y, mouth.y, k * k), Mathf.Lerp(from.z, mouth.z, e));
            }
            float f = Mathf.Clamp01((t - FallInto) / Mathf.Max(0.01f, _fallDur[i] - FallInto));
            return new Vector3(Mathf.Lerp(mouth.x, slot.x, f), Mathf.Lerp(mouth.y, slot.y, f * f), Mathf.Lerp(mouth.z, slot.z, f));
        }

        Vector3 ClothPos(Vector2 p) => new Vector3(p.x, PoolDims.ClothY + PoolDims.BallR, p.y);

        /// <summary>Where ball i is drawn right now (table space).</summary>
        Vector3 CurrentLocal(int i)
        {
            if (_bag[i] >= 0) return BagPos(i);
            if (i == 0 && !Rolling)
            {
                if (HeldCuePos.HasValue) return ClothPos(HeldCuePos.Value);
                // Ball in hand: the shooter's hand position (local live, others from his synced aim; PoolTable.Aim).
                if (HandBall(out var hand)) return ClothPos(hand);
            }
            return ClothPos(ShownPos[i]);
        }

        // ================================================================ following the server state

        /// <summary>Outside a replay: shown balls -> <see cref="State"/> (pocketed into baskets, returned balls pop up, small moves glide).</summary>
        void SyncToState(float dt, bool instant)
        {
            if (!_hasState || State.ShotNo < _shownShot) return; // an older state than what is shown: wait for the newer one
            _shownShot = State.ShotNo;
            for (int i = 0; i < 16; i++)
            {
                if (State.IsDown(i))
                {
                    // The state keeps a pocketed ball's last spot on the cloth: its nearest pocket is the one it went into.
                    if (_bag[i] < 0) PutInBag(i, NearestPocket(State.Pos[i]), !instant && _hasPlaced[i], CurrentLocal(i));
                    continue;
                }
                Vector2 want = State.Pos[i];
                if (_bag[i] >= 0)
                {
                    TakeOutOfBag(i);
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
            for (int i = 0; i < 16; i++) if (_bag[i] >= 0) m |= 1 << i;
            ShownDown = (ushort)m;
        }

        /// <summary>Moves the 3D balls: position, rolling rotation, pop-up scale; the landing knock of a ball dropping into a basket.</summary>
        void PlaceBalls(float dt)
        {
            for (int i = 0; i < 16; i++)
            {
                if (_bag[i] >= 0 && _fallT[i] < _fallDur[i])
                {
                    _fallT[i] += dt;
                    if (_fallT[i] >= _fallDur[i]) BagLanded(i);
                }
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
