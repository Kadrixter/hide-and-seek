using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Per-player peg-leg glue on every client (added by MatchManager.Rods): passes the fitted rods to the character,
    /// replaces one-legged hopping with walking on the rod (own speed multiplier, applied after NetPlayer's speed so
    /// other modifiers stay intact), plays the rod sounds and, for the local player, freezes movement during the
    /// fitting, lowers the camera and shows the first-person hands with the rod.
    /// Runs after NetPlayer (-40) so its values win for this frame; the motor (-50) reads them next frame.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class RodLegDriver : MonoBehaviour
    {
        private NetPlayer _p;
        private BlockyCharacterActions _bca;
        private HeadBob _bob;
        private float _lastAttachT = -1f;
        private bool _fppOn;
        private RodModel.Parts _fppRod;

        /// <summary>True while this player walks on a rod (no free stump left).</summary>
        public bool RodWalking { get; private set; }

        private void Awake() => _p = GetComponent<NetPlayer>();

        private void OnDestroy()
        {
            if (_bca != null) _bca.RodStep -= OnRodStep;
            if (_fppRod != null && _fppRod.Root != null) Destroy(_fppRod.Root.gameObject);
        }

        /// <summary>Called by NetPlayer.RodAttachRpc on every client.</summary>
        public void OnAttachStarted()
        {
            _lastAttachT = 0f;
            if (_p != null && _p.IsOwner) _p.InputReader?.ResetStance();
        }

        private void Update()
        {
            if (_p == null || !_p.IsSpawned) return;
            if (_bca == null)
            {
                _bca = _p.Actions as BlockyCharacterActions;
                if (_bca == null) return;
                _bca.RodStep += OnRodStep;
            }

            int severedLegs = _p.Severed.Value & Limb.Legs;
            int rods = _p.RodLegs.Value & severedLegs;
            _bca.SetRodLegs(rods);
            bool alive = _p.IsHider && _p.Life.Value == LifeState.Alive;
            int shown = _bca.RodLegsShown & severedLegs;
            int free = severedLegs & ~shown;
            RodWalking = alive && shown != 0 && free == 0;
            if (RodWalking) _bca.SetHop(-1);

            bool attaching = _bca.RodAttaching;
            AttachSounds(attaching);
            if (!_p.IsOwner) return;

            var motor = _p.Motor;
            if (motor == null) return;
            if (attaching)
            {
                motor.SpeedScale = 0f;
                motor.ForceCrouch = true;
                float t = _bca.RodAttachTime;
                float want = t < RodTimeline.StandStart + 0.2f ? -52f : 0f;
                if (t > 0.25f) motor.SetLook(motor.LookYaw, Mathf.MoveTowards(motor.LookPitch, want, Time.deltaTime * (t < RodTimeline.StandStart ? 150f : 60f)));
            }
            else if (RodWalking && motor.SpeedScale > 0.001f)
            {
                // NetPlayer applied the one-legged hop factor (times any other modifiers): swap it for the rod factor.
                float rod = motor.IsRunning ? ItemTuning.RodRunScale : ItemTuning.RodSpeedScale;
                motor.SpeedScale *= rod / Tuning.HopSpeedScale;
            }
            if (RodWalking)
            {
                if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
                if (_bob != null) _bob.Limp = 0.45f;
            }
        }

        private void LateUpdate()
        {
            if (_p == null || !_p.IsOwner || _bca == null) return;
            UpdateFirstPerson(_bca.RodAttaching);
        }

        private void OnRodStep(Vector3 tip, bool scrape)
        {
            if (scrape) RodSounds.Scrape(tip);
            else RodSounds.Step(tip);
        }

        /// <summary>Sounds at the marks of the fitting animation (all clients, 3D at the hips).</summary>
        private void AttachSounds(bool attaching)
        {
            float t = attaching ? _bca.RodAttachTime : -1f;
            float prev = _lastAttachT;
            _lastAttachT = t;
            if (t < 0f || prev < 0f) return;
            Vector3 hip = transform.position + Vector3.up * 0.2f;
            bool Crossed(float mark) => prev < mark && t >= mark;
            if (Crossed(RodTimeline.Show - 0.05f)) GameAudio.PlayAt(GameSfx.KnifeSwing, hip, 0.35f);
            if (Crossed(RodTimeline.Push1)) { RodSounds.Insert(hip); GameAudio.PlayAt(GameSfx.HiderPain, hip + Vector3.up, 0.55f); }
            if (Crossed(RodTimeline.Push2)) { RodSounds.Insert(hip); GameAudio.PlayAt(GameSfx.HiderPain, hip + Vector3.up, 0.8f); }
            for (int i = 0; i < 3; i++)
                if (Crossed(RodTimeline.TapeStart + i * 0.3f)) RodSounds.Tape(hip);
            if (Crossed(RodTimeline.Duration - 0.35f)) RodSounds.Step(transform.position);
        }

        /// <summary>
        /// Local player: first-person hands and a rod copy on the view-model layer, laid out in camera space so they
        /// stay in frame (the real body is shadow-only): the rod is pulled up from the right, lined up with the stump
        /// at the bottom of the view (same axis as the real rod), shoved on twice, then the right hand wraps tape round it.
        /// </summary>
        private void UpdateFirstPerson(bool attaching)
        {
            var fpv = _p.ViewModel as FirstPersonViewModel;
            var ch = _p.Character;
            var cam = _p.Camera != null ? _p.Camera.transform : null;
            float t = attaching ? _bca.RodAttachTime : -1f;
            bool on = attaching && fpv != null && cam != null && ch != null && ch.IsBuilt && ch.IsFirstPerson &&
                      t >= RodTimeline.Show - 0.1f && t < RodTimeline.StandStart + 0.25f;
            if (!on)
            {
                if (_fppOn) fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                _fppOn = false;
                if (_fppRod != null && _fppRod.Root.gameObject.activeSelf) _fppRod.Root.gameObject.SetActive(false);
                return;
            }
            _fppOn = true;
            float S(float a, float b) => RodTimeline.Seg(t, a, b);

            // Axis of the real rod (top -> tip) in camera space; anchor = where the stump is in the view.
            Vector3 axisW = transform.TransformDirection(_bca.RodAnimAxis);
            Vector3 axis = cam.InverseTransformDirection(axisW).normalized;
            Vector3 anchor = new Vector3(0.03f, -0.42f, 0.5f);
            Vector3 top, r, l;
            Vector3 ax = axis;
            if (t < RodTimeline.BringEnd)
            {
                float e = S(RodTimeline.Show, RodTimeline.BringEnd);
                Vector3 a0 = new Vector3(0.3f, 0.9f, -0.3f).normalized;
                ax = Vector3.Slerp(a0, axis, e).normalized;
                Vector3 grip = Vector3.Lerp(new Vector3(0.34f, -0.62f, 0.42f), anchor + axis * (0.24f + 0.3f), e) + new Vector3(0f, 0.08f * Mathf.Sin(Mathf.PI * e), 0f);
                top = grip - ax * 0.3f;
                r = grip;
                l = Vector3.Lerp(new Vector3(-0.32f, -0.95f, 0.4f), top + ax * 0.1f, S(0.8f, RodTimeline.BringEnd));
            }
            else if (t < RodTimeline.TapeStart)
            {
                top = anchor + axis * RodTimeline.Offset(t);
                r = top + axis * 0.3f;
                l = top + axis * 0.1f;
            }
            else
            {
                top = anchor;
                l = top + axis * 0.3f;
                Vector3 u = Vector3.Cross(axis, Vector3.forward);
                if (u.sqrMagnitude < 1e-4f) u = Vector3.right;
                u.Normalize();
                Vector3 v = Vector3.Cross(u, axis).normalized;
                float ang = RodTimeline.Tape(t) * 2.5f * Mathf.PI * 2f;
                r = Vector3.Lerp(top + axis * 0.3f, top + axis * 0.08f + (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * 0.11f, S(RodTimeline.TapeStart, RodTimeline.TapeStart + 0.15f));
                float away = S(RodTimeline.StandStart, RodTimeline.StandStart + 0.25f);
                r = Vector3.Lerp(r, new Vector3(0.34f, -0.9f, 0.4f), away);
                l = Vector3.Lerp(l, new Vector3(-0.32f, -0.95f, 0.4f), away);
            }
            Vector3 fwd = Vector3.Cross(ax, Vector3.right);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            Quaternion rotW = cam.rotation * Quaternion.LookRotation(fwd.normalized, -ax);
            fpv.SetOverrideHands(true, cam.TransformPoint(r), rotW, cam.TransformPoint(l), HeldItem.None);

            if (_fppRod == null)
            {
                _fppRod = RodModel.Create(null, FirstPersonViewModel.Layer, true, _bca.RodAttachLegIndex == 0 ? 1f : -1f);
                RodModel.SetRenderMode(_fppRod, FirstPersonViewModel.Layer, ShadowCastingMode.Off);
            }
            if (!_fppRod.Root.gameObject.activeSelf) _fppRod.Root.gameObject.SetActive(true);
            _fppRod.Root.SetPositionAndRotation(cam.TransformPoint(top), rotW);
            RodModel.SetTape(_fppRod, _bca.RodAnimTape);
        }
    }
}
