using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Emotes over the network. Solo: the owner writes <see cref="EmoteVar"/> (id + server start time); every peer
    /// plays it on the body at the shared clock (late joiners join a loop in step). Pair: the server writes
    /// <see cref="PairVar"/> on both players (<see cref="NetPlayer"/>.EmotePair); the owners walk to their spots and
    /// both bodies start at the same server time. The owner ends its emote when it walks (a fresh WASD / jump press),
    /// gets hurt, the round phase changes or anything else takes the body; one-shots end by themselves. While an
    /// emote is on, the own view goes to the third-person <see cref="EmoteCamera"/>.
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<SoloEmotePack> EmoteVar = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<PairEmotePack> PairVar = new();

        private bool _emoteHooked, _moveArmed, _pairStopSent;
        private int _hurtSig;
        private MatchPhase _emotePhase;
        private EmoteCamera _emoteCam;
        private System.Func<MotorCommand> _approachCmd;
        private float _approachLog;

        private BlockyCharacterActions EmoteBody => Actions as BlockyCharacterActions;

        /// <summary>The own view is (or is going) third person for an emote.</summary>
        public bool EmoteViewActive => _emoteCam != null && _emoteCam.Weight > 0f;

        /// <summary>Hands taken by an emote (the hotbar item goes back into the pocket).</summary>
        public bool EmoteHandsBusy
        {
            get
            {
                var b = EmoteBody;
                return PairVar.Value.Active || EmoteVar.Value.Id != 0 || (b != null && b.EmoteWeight > 0.01f);
            }
        }

        /// <summary>Owner: no walking (pair emote running, sitting on the floor or getting up).</summary>
        public bool EmoteHoldsMove
        {
            get
            {
                if (PairVar.Value.Active) return _approachCmd == null || Motor.CommandProvider != _approachCmd;
                var b = EmoteBody;
                return b != null && b.EmoteHoldsFeet;
            }
        }

        private static double Now => MatchManager.Instance != null ? MatchManager.Instance.Now : 0d;

        /// <summary>Owner: starts solo emote <paramref name="id"/>. False (with a toast) when not possible now.</summary>
        public bool StartSoloEmote(EmoteId id)
        {
            if (!IsOwner || !IsSpawned || Emotes.Get(id) == null || Emotes.IsPair(id)) return false;
            if (!EmoteRules.Free(this, out var why)) { if (!string.IsNullOrEmpty(why)) GameHud.Toast(why, 2f); return false; }
            if (PairVar.Value.Active) StopPairEmoteRpc();
            EmoteVar.Value = new SoloEmotePack { Id = (byte)id, StartAt = Now };
            ArmEmote();
            Debug.Log($"[Emote] {Nick}: {id}");
            return true;
        }

        /// <summary>Owner: stops the own emote (solo or pair).</summary>
        public void StopEmote()
        {
            if (!IsOwner) return;
            if (EmoteVar.Value.Id != 0) EmoteVar.Value = default;
            if (PairVar.Value.Active && !_pairStopSent) { _pairStopSent = true; StopPairEmoteRpc(); }
        }

        private void ArmEmote()
        {
            _moveArmed = false;
            _pairStopSent = false;
            _hurtSig = HurtSignature();
            _emotePhase = MatchManager.Instance != null ? MatchManager.Instance.Phase.Value : MatchPhase.Lobby;
        }

        private int HurtSignature() => (Wounded.Value ? 1 : 0) | (Limping.Value ? 2 : 0) | (Severed.Value << 2) | ((int)Life.Value << 10);

        /// <summary>Called from Update on every peer.</summary>
        private void UpdateEmote()
        {
            var body = EmoteBody;
            if (body == null) return;
            if (!_emoteHooked)
            {
                _emoteHooked = true;
                body.EmoteBeatFired += OnEmoteBeat;
            }
            if (IsServer)
            {
                Consent.Tick();
                ServerPairTick();
            }
            if (IsOwner) OwnerEmoteTick();

            // What the body should show at the shared clock.
            var pv = PairVar.Value;
            var sv = EmoteVar.Value;
            EmoteId id = EmoteId.None;
            double start = 0d;
            NetPlayer partner = null;
            if (pv.Active)
            {
                partner = Find(pv.Partner);
                if (Now >= pv.StartAt) { id = (EmoteId)pv.Id; start = pv.StartAt; }
            }
            else if (sv.Id != 0) { id = (EmoteId)sv.Id; start = sv.StartAt; }

            if (id != EmoteId.None)
            {
                body.SetEmotePartner(partner != null ? partner.Character : null, pv.Role);
                body.PlayEmote(id, (float)(Now - start));
            }
            else if (body.Emote != EmoteId.None) body.StopEmote();

            if (IsOwner)
            {
                if (_emoteCam == null) _emoteCam = gameObject.AddComponent<EmoteCamera>();
                bool view = (body.EmoteShown || pv.Active) && Life.Value == LifeState.Alive && !KillCam.Active;
                _emoteCam.Focus = partner != null && pv.Active ? partner.transform : null;
                _emoteCam.Low = body.EmoteHoldsFeet;
                _emoteCam.SetWanted(view);
            }
        }

        private void OwnerEmoteTick()
        {
            var sv = EmoteVar.Value;
            var pv = PairVar.Value;
            if (sv.Id == 0 && !pv.Active)
            {
                if (_approachCmd != null && Motor.CommandProvider == _approachCmd) Motor.CommandProvider = null;
                return;
            }
            var mm = MatchManager.Instance;
            bool moving = InputReader != null && (InputReader.Move.sqrMagnitude > 0.05f || InputReader.JumpHeld);
            if (!moving) _moveArmed = true; // a key still held from before the emote does not count
            bool hurt = HurtSignature() != _hurtSig;
            bool phase = mm != null && mm.Phase.Value != _emotePhase;
            bool free = EmoteRules.Free(this, out _);
            bool stop = (moving && _moveArmed) || hurt || phase || !free || KillCam.Active;

            if (sv.Id != 0)
            {
                var info = Emotes.Get((EmoteId)sv.Id);
                bool over = info == null || pv.Active || (!info.Loops && Now - sv.StartAt >= info.Duration);
                if (stop || over)
                {
                    EmoteVar.Value = default;
                    if (stop) Debug.Log($"[Emote] {Nick}: stopped ({(moving ? "move" : hurt ? "hurt" : phase ? "phase" : "busy")})");
                }
            }

            if (pv.Active)
            {
                if (stop && !_pairStopSent)
                {
                    _pairStopSent = true;
                    StopPairEmoteRpc();
                    Debug.Log($"[Emote] {Nick}: pair stopped ({(moving ? "move" : hurt ? "hurt" : phase ? "phase" : "busy")})");
                }
                PairApproach(pv);
            }
            else if (_approachCmd != null && Motor.CommandProvider == _approachCmd) Motor.CommandProvider = null;
        }

        /// <summary>Owner: walks to the own spot of the pair emote and turns to the partner; stops once there.</summary>
        private void PairApproach(in PairEmotePack pv)
        {
            Vector3 to = pv.Spot - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            double t = Now - pv.StartAt;
            bool walk = d > 0.03f && t < 0.6;
            _approachCmd ??= ApproachCommand;
            if (walk && Motor.CommandProvider == null) Motor.CommandProvider = _approachCmd;
            else if (!walk && Motor.CommandProvider == _approachCmd) Motor.CommandProvider = null;
            if (Time.unscaledTime >= _approachLog && t < 0.8 && (walk || d > 0.06f))
            {
                _approachLog = Time.unscaledTime + 0.5f;
                Debug.Log($"[Emote] {Nick} approach t={t:0.00} d={d:0.00} walk={walk} cmd={(Motor.CommandProvider == _approachCmd)} move={Motor.MovementEnabled} v={Motor.HorizontalSpeed:0.00}");
            }
            float dt = Time.deltaTime;
            Motor.SetLook(Mathf.MoveTowardsAngle(Motor.LookYaw, pv.Yaw, dt * (t < 0 ? 260f : 120f)), Mathf.MoveTowards(Motor.LookPitch, 0f, dt * 60f));
        }

        private MotorCommand ApproachCommand()
        {
            var pv = PairVar.Value;
            Vector3 to = pv.Spot - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 0.025f) return default;
            Vector3 local = transform.InverseTransformDirection(to / d);
            float speed = Mathf.Clamp01(d / 0.3f) * 0.55f;
            return new MotorCommand { Move = new Vector2(local.x, local.z).normalized * speed };
        }

        private void OnEmoteBeat(EmoteId id, EmoteBeat kind, int index)
        {
            // Contact sounds of a pair once (the side that asked plays them).
            if (Emotes.IsPair(id) && PairVar.Value.Role != 0) return;
            EmoteSounds.Play(kind, ChestPosition + transform.forward * 0.25f, IsOwner);
        }
    }
}
