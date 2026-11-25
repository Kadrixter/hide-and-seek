using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Hider next to a seeker who is knocked out (thrown leg) or has dust / goo / foam in the eyes and carries the
    /// chainsaw: E wrests it from him (MatchManager.ServerWrestSaw). Works with the fire extinguisher in the hands too
    /// (it falls on the floor), so spraying him and grabbing the saw is one move.
    /// </summary>
    public partial class LocalPlayerController
    {
        /// <summary>Test hook (-testsawnerf): press E at the wrest prompt.</summary>
        public static bool TestWrestE;

        private ulong _wrestId = ulong.MaxValue;
        private float _wrestNext;

        private bool TrySawWrestPrompt(Vector3 eye, Vector3 fwd, MatchPhase phase, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = float.MaxValue;
            _wrestId = ulong.MaxValue;
            if (phase != MatchPhase.Seeking || !_p.IsAliveHider || _p.Busy.Value || _p.Seat.Value >= 0) return false;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == _p || !o.SawWrestable) continue;
                Vector3 fd = o.transform.position - transform.position;
                float flat = new Vector2(fd.x, fd.z).magnitude;
                if (flat > SawTuning.WrestPromptRange || Mathf.Abs(fd.y) > SawTuning.WrestHeight) continue;
                // Knocked out he lies on the floor: aim at the body's middle, a wide cone.
                Vector3 at = o.Stunned ? o.transform.position + Vector3.up * 0.35f : o.ChestPosition;
                float d = Vector3.Distance(eye, at);
                if (d >= dist || !InView(eye, fwd, at, 65f, o.transform)) continue;
                dist = d;
                pos = at;
                _wrestId = o.NetworkObjectId;
                text = flat > SawTuning.WrestRange ? "E  wyrwij piłę (podejdź bliżej)"
                    : Inventory.PickupText(_p, HotbarItem.Chainsaw, "E  wyrwij mu piłę!");
            }
            return _wrestId != ulong.MaxValue;
        }

        private void SawWrestPromptE(bool pressed)
        {
            if (!pressed || _wrestId == ulong.MaxValue || Time.time < _wrestNext) return;
            _wrestNext = Time.time + 0.5f;
            _ignoreEThisFrame = true;
            _p.RequestWrestSawRpc(_wrestId);
        }

        /// <summary>From <see cref="ExtTick"/> while the hider holds the extinguisher: the same prompt and E.</summary>
        private void SawWrestWithExt(Keyboard kb, MatchPhase phase)
        {
            Vector3 eye = _p.EyePosition;
            Vector3 fwd = _p.Camera != null ? _p.Camera.transform.forward : transform.forward;
            if (!TrySawWrestPrompt(eye, fwd, phase, out var pos, out string text, out _)) return;
            Prompt = PromptKind.SawWrest;
            PromptWorld = pos;
            PromptText = text;
            SawWrestPromptE(kb.eKey.wasPressedThisFrame || TestWrestE);
            TestWrestE = false;
        }
    }
}
