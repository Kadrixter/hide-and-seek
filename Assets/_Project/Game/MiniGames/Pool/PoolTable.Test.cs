using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Hooks for the in-game test (MiniGameProbe -testpool): they drive the same local aiming state the mouse does
    /// (aim angle and spin, holding LMB with a pull-back, carrying / putting down the cue ball in hand), so the stroke
    /// still goes through <see cref="Shoot"/>, the stance, the cue and the guide. Unused in normal play.
    /// </summary>
    public sealed partial class PoolTable
    {
        /// <summary>Test: LMB counts as held (the pull is kept until <see cref="TestRelease"/>).</summary>
        static bool _testLmb;
        /// <summary>Test: the cue ball in hand follows this point instead of the cursor; a click is queued.</summary>
        Vector2? _testHold;
        bool _testClick;

        /// <summary>Aim angle of the local shooter (radians, table space).</summary>
        public float AimAngle => _aimAngle;

        /// <summary>Stance (0 = upright, 1 = down on the cue) and shown pull-back of a seat, as every peer draws it.</summary>
        public float TestBend(int seat) => _view[seat & 3].Bend;
        public float TestShownPull(int seat) => _view[seat & 3].Pull;
        /// <summary>The stroke animation of a seat is running.</summary>
        public bool TestStroking(int seat) => _view[seat & 3].StrokeT >= 0f;

        /// <summary>Camera blend state for the test log.</summary>
        public string TestCamInfo()
        {
            var me = HideAndSeek.Game.Net.NetPlayer.Local;
            string cam = me != null && me.Camera != null ? me.Camera.transform.position.ToString("F2") : "-";
            CameraPose(LocalSeat < 0 ? 0 : LocalSeat, 1f, out var pos, out _);
            int vis = 0;
            string names = "";
            if (me != null && me.Character != null)
                foreach (var r in me.Character.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                    {
                        vis++;
                        if (vis <= 4) names += r.name + " ";
                    }
            int seat = LocalSeat < 0 ? 0 : LocalSeat;
            return $"own body visible renderers={vis} [{names}] bend={_view[seat].Bend:0.00} stroke={_view[seat].StrokeT:0.00} root={(me != null ? me.transform.position.ToString("F2") : "-")} board w={MiniGameCamera.Weight:0.00} active={MiniGameCamera.Active} aimW={_wAim:0.00} topW={_wTop:0.00} turn={LocalTurn} rolling={Rolling} eye={cam} pose={pos:F2}";
        }

        public void TestAim(float angle, Vector2 spin)
        {
            _aimInit = true;
            _aimAngle = Mathf.Repeat(angle + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
            _spin = Vector2.ClampMagnitude(spin, 0.8f);
            _sendNow = true;
        }

        /// <summary>LMB down and pulled back to <paramref name="pull"/> (0..1); the shot goes on <see cref="TestRelease"/>.</summary>
        public void TestPull(float pull)
        {
            _dragging = true;
            _dragPx = Mathf.Clamp01(pull) * DragFull;
            _pull = _dragPx / DragFull;
            _sendNow = true;
            _testLmb = true;
        }

        public void TestRelease() => _testLmb = false;

        public void TestTopView(bool on) => _topView = on;

        /// <summary>Ball in hand: carry the cue ball to <paramref name="p"/> (null = the cursor again), <paramref name="put"/> = click there.</summary>
        public void TestHold(Vector2? p, bool put)
        {
            _testHold = p;
            _testClick = put;
        }

        /// <summary>Ball return for the test log: per side the queue (ball, pocket, phase H/F/R, metres left to the stop).</summary>
        public string TestReturnInfo()
        {
            var sb = new System.Text.StringBuilder();
            for (int side = 0; side < 2; side++)
            {
                sb.Append(side == 0 ? "side-Z [" : " side+Z [");
                _retOrder.Clear();
                for (int i = 0; i < 16; i++)
                    if (_ret[i] >= 0 && Pool.PoolReturnTrack.SideOf(_ret[i]) == side) _retOrder.Add(i);
                _retOrder.Sort(CompareQueue);
                foreach (int i in _retOrder) sb.Append($" {i}@p{_ret[i]}:{"HFR"[_retPhase[i]]}{_retR[i]:0.000}");
                sb.Append(" ]");
            }
            return sb.ToString();
        }

        /// <summary>Pose inputs of a place for the test log (table space).</summary>
        public string TestPoseInfo(int place)
        {
            var v = _view[place & 3];
            return $"bend {v.Bend:0.00} walk {v.Walk:0.00} valid {v.PoseValid} grip {transform.InverseTransformPoint(v.Grip):F2} bridge {transform.InverseTransformPoint(v.Bridge):F2}";
        }

        /// <summary>Pocket ball i went into (-1 = not pocketed).</summary>
        public int TestReturnPocket(int i) => i >= 0 && i < 16 ? _ret[i] : -1;

        /// <summary>LocalInput: the test's hold point / click override the mouse.</summary>
        bool TestHandPoint(ref Vector2 p, ref bool click)
        {
            if (_testClick)
            {
                click = true;
                _testClick = false;
            }
            if (!_testHold.HasValue) return false;
            p = _testHold.Value;
            return true;
        }
    }
}
