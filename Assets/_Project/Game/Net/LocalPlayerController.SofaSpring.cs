using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// 2026-11-27: worn sofas. Sitting down on a sofa seat: <see cref="SpringChance"/> that after 4-7 s a spring pops out
    /// of the cushion - the sitter is thrown up and forward off the seat, lands, steps back to the sofa, bends over
    /// and pushes the spring back in (NetPlayer.SofaSpring + BlockyCharacterActions.SpringPush), then stays standing
    /// (sitting down again works as always). Decided here, by the sitter's own client, like the seat itself.
    /// -sofaspring: always (tests).
    /// </summary>
    public partial class LocalPlayerController
    {
        public const float SpringChance = 0.2f;
        private static readonly bool ForceSpring = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sofaspring") >= 0;

        private int _springSeat = -1;
        private float _springAt = -1f, _springT = -1f;
        private Vector3 _springSurface, _springFwd;

        /// <summary>Thrown off / pushing the spring back: no walking, no sitting.</summary>
        public bool SpringBusy => _springT >= 0f;

        /// <summary>Every frame (after the seated block of Update).</summary>
        private void TickSofaSpring(bool seated)
        {
            int seat = _p.Seat.Value;
            if (seated && seat != _springSeat)
            {
                _springSeat = seat;
                var s = LobbySeat.Get(seat);
                bool sofa = s != null && s.Shape.Kind == SeatKind.Sofa;
                _springAt = sofa && (ForceSpring || Random.value < SpringChance) ? Time.time + Random.Range(4f, 7f) : -1f;
                if (_springAt > 0f) Debug.Log($"[Sofa] a spring will pop under seat {seat} in {_springAt - Time.time:0.0} s");
            }
            if (!seated && _springT < 0f) { _springSeat = -1; _springAt = -1f; }
            if (seated && _springAt > 0f && Time.time >= _springAt) Pop();
            if (_springT >= 0f) StepSpring();
        }

        private void Pop()
        {
            var s = LobbySeat.Get(_springSeat);
            _springAt = -1f;
            if (s == null) return;
            _springSurface = NetPlayer.SpringSurface(s);
            _springFwd = s.transform.forward;
            _springFwd.y = 0f;
            _springFwd.Normalize();
            StandUp();
            _springSeat = -1;
            _p.Motor.AddImpulse(_springFwd * 1.6f + Vector3.up * 4.4f); // a light throw: up and off the seat
            _p.SofaSpringRpc((byte)s.Index);
            _springT = 0f;
            GameHud.Toast("Au! Sprężyna wyskoczyła z kanapy.", 2.5f);
        }

        private void StepSpring()
        {
            float dt = Time.deltaTime;
            _springT += dt;
            float t = _springT;
            float yawToSofa = Mathf.Atan2(-_springFwd.x, -_springFwd.z) * Mathf.Rad2Deg;
            if (t > 0.6f && t < NetPlayer.SpringPushStart + 0.2f)
            {
                // back to the sofa: stand ~0.6 m in front of the spring, facing it
                Vector3 want = _springSurface + _springFwd * 0.62f;
                Vector3 d = want - transform.position;
                d.y = 0f;
                float step = Mathf.Min(d.magnitude, 1.8f * dt);
                if (step > 0.001f) _p.Motor.ScriptedMove(d.normalized * step + Vector3.down * 0.02f);
            }
            if (t > 0.5f)
            {
                float k = Mathf.Clamp01((t - 0.5f) / 0.6f);
                float pitch = t < NetPlayer.SpringPushStart + BlockyCharacterActions.SpringPushDuration - 0.3f ? -38f * k : -38f * (1f - Mathf.Clamp01((t - NetPlayer.SpringPushStart - BlockyCharacterActions.SpringPushDuration + 0.3f) / 0.3f));
                _p.Motor.SetLook(Mathf.LerpAngle(transform.eulerAngles.y, yawToSofa, Mathf.Clamp01(dt * 8f)), pitch);
            }
            // first person: both hands on the spring while it is pushed in
            var fpv = _p.ViewModel as FirstPersonViewModel;
            float tp = t - NetPlayer.SpringPushStart;
            bool pushing = tp > 0f && tp < BlockyCharacterActions.SpringPushDuration;
            if (fpv != null)
            {
                if (pushing)
                {
                    Vector3 top = _springSurface + Vector3.up * (SofaSpringFx.Height * BlockyCharacterActions.SpringDepth(tp) + 0.02f);
                    Vector3 side = Vector3.Cross(Vector3.up, -_springFwd).normalized;
                    fpv.SetOverrideHands(true, top + side * 0.06f, Quaternion.LookRotation(-_springFwd, Vector3.up), top - side * 0.06f);
                }
                else fpv.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
            }
            if (tp >= BlockyCharacterActions.SpringPushDuration + 0.1f)
            {
                _springT = -1f;
                fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
            }
        }
    }
}
