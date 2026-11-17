using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Networked player: identity (nick, skin), round state (role, life, wounded, items, ammo) written by the server,
    /// and locomotion / flashlight written by the owner. Configures the shared Player prefab as either the local
    /// first-person player or a remote proxy, and plays action effects (knife, gun, rescue) on every client.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All = new();
        public static NetPlayer Local { get; private set; }

        // ---- server-written state
        public readonly NetworkVariable<FixedString32Bytes> NickVar = new();
        public readonly NetworkVariable<byte> SkinVar = new();
        /// <summary>Minecraft nick for a downloaded skin (empty = built-in skin).</summary>
        public readonly NetworkVariable<FixedString32Bytes> McNickVar = new();
        public readonly NetworkVariable<PlayerRole> Role = new();
        public readonly NetworkVariable<LifeState> Life = new();
        public readonly NetworkVariable<bool> Wounded = new();
        public readonly NetworkVariable<byte> MedkitCount = new();
        public readonly NetworkVariable<HeldItem> Held = new();
        public readonly NetworkVariable<bool> HasGlock = new();
        public readonly NetworkVariable<byte> Ammo = new();
        public readonly NetworkVariable<byte> SpareMags = new();
        public readonly NetworkVariable<double> DownedUntil = new();
        public readonly NetworkVariable<bool> Busy = new();
        public readonly NetworkVariable<int> Kills = new();
        /// <summary>Server time of this hider's next hint (visible to the owner only).</summary>
        public readonly NetworkVariable<double> NextHintAt = new(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        // ---- owner-written state
        public readonly NetworkVariable<MotionPack> Motion = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> FlashOn = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> FlashFocus = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Reviving = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public string Nick => NickVar.Value.ToString();
        public bool IsHider => Role.Value == PlayerRole.Hider;
        public bool IsSeeker => Role.Value == PlayerRole.Seeker;
        public bool IsAliveHider => IsHider && Life.Value == LifeState.Alive;

        // ---- components
        public PlayerMotor Motor { get; private set; }
        public PlayerInputReader InputReader { get; private set; }
        public Flashlight Flashlight { get; private set; }
        public Camera Camera { get; private set; }
        public BlockyCharacter Character { get; private set; }
        public ICharacterActions Actions { get; private set; }
        public IFirstPersonViewModel ViewModel { get; private set; }
        private FootstepAudio _steps;
        private OwnerNetworkTransform _nt;
        private AudioSource _heartbeat, _crawlLoop, _reviveLoop;
        private float _nextMotionSend;
        private Transform _chestFallback;

        /// <summary>Chest-height world point for interaction icons.</summary>
        public Vector3 ChestPosition
        {
            get
            {
                var cp = Actions?.ChestPoint;
                if (cp != null) return cp.position;
                return transform.position + Vector3.up * (Life.Value == LifeState.Alive ? 1.2f : 0.35f);
            }
        }

        /// <summary>Eye position (approx. for remote players).</summary>
        public Vector3 EyePosition => Camera != null ? Camera.transform.position : transform.position + Vector3.up * 1.6f;

        // ================================================================ lifecycle

        private void Awake()
        {
            Motor = GetComponent<PlayerMotor>();
            InputReader = GetComponent<PlayerInputReader>();
            _steps = GetComponent<FootstepAudio>();
            Flashlight = GetComponentInChildren<Flashlight>(true);
            Camera = GetComponentInChildren<Camera>(true);
            _nt = GetComponent<OwnerNetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer && NetSession.Instance != null)
            {
                var (nick, skin, mc) = NetSession.Instance.TakeProfile(OwnerClientId);
                NickVar.Value = nick;
                SkinVar.Value = (byte)skin;
                McNickVar.Value = mc ?? "";
            }

            BuildCharacter();
            if (_steps != null) _steps.MasterVolume = LocalProfile.FootstepVolume;
            if (IsOwner) SetupLocal();
            else SetupRemote();

            NickVar.OnValueChanged += (_, _) => RefreshSkin();
            SkinVar.OnValueChanged += (_, _) => RefreshSkin();
            McNickVar.OnValueChanged += (_, _) => LoadMcSkin();
            LoadMcSkin();
            Life.OnValueChanged += (_, v) => ApplyLife(v, true);
            Held.OnValueChanged += (_, v) => ApplyHeld(v);
            FlashOn.OnValueChanged += (_, v) => { if (!IsOwner && Flashlight != null) Flashlight.SetOn(v, true); };
            FlashFocus.OnValueChanged += (_, v) => { if (!IsOwner && Flashlight != null) Flashlight.SetFocus(v / 255f); };
            Reviving.OnValueChanged += (_, v) => ApplyReviving(v);

            ApplyLife(Life.Value, false);
            ApplyHeld(Held.Value);
            if (!IsOwner && Flashlight != null)
            {
                Flashlight.SetOn(FlashOn.Value, false);
                Flashlight.SetFocus(FlashFocus.Value / 255f);
            }
            GameAudio.PlayAt(GameSfx.PlayerJoined, transform.position, 0.6f);
            Debug.Log($"[NetPlayer] spawned {Nick} owner={OwnerClientId} local={IsOwner} actions={(Actions != null)}");
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            StopLoops();
        }

        public override void OnDestroy()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            base.OnDestroy();
        }

        private void BuildCharacter()
        {
            Transform root = transform.Find("CharacterModelRoot");
            if (root == null)
            {
                root = new GameObject("CharacterModelRoot").transform;
                root.SetParent(transform, false);
            }
            Character = BlockyCharacterFactory.Create(root, SkinVar.Value, Nick);
            Actions = Character.GetComponentInChildren<ICharacterActions>(true);
            if (Actions == null) Actions = GetComponentInChildren<ICharacterActions>(true);
            // Motion for the animator always comes from the motor (simulated locally or fed from the network).
            var anim = Character.GetComponent<BlockyCharacterAnimator>();
            if (anim != null) anim.SetMotionSource(Motor);
        }

        private void LoadMcSkin()
        {
            string mc = McNickVar.Value.ToString();
            if (string.IsNullOrEmpty(mc))
            {
                Character?.SetCustomSkin(null);
                (ViewModel as FirstPersonViewModel)?.SetCustomSkin(null);
                return;
            }
            MinecraftSkins.Get(mc, (tex, err) =>
            {
                if (this == null || tex == null || McNickVar.Value.ToString() != mc) return;
                Character?.SetCustomSkin(tex);
                (ViewModel as FirstPersonViewModel)?.SetCustomSkin(tex);
            });
        }

        private void RefreshSkin()
        {
            if (Character != null) Character.SetSkin(SkinVar.Value, Nick);
        }

        private void SetupLocal()
        {
            Local = this;
            // The prefab ships with camera, listener and input disabled so remote copies never grab them.
            foreach (var other in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if (!other.transform.IsChildOf(transform)) other.enabled = false;
            if (Camera != null)
            {
                Camera.enabled = true;
                var listener = Camera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }
            if (InputReader != null) InputReader.enabled = true;
            Character.SetFirstPersonMode(true);
            Character.SetLayer(PlayerAvatarBinder.LocalBodyLayer);
            foreach (var l in GetComponentsInChildren<Light>(true))
                l.cullingMask &= ~(1 << PlayerAvatarBinder.LocalBodyLayer);
            if (InputReader != null) InputReader.MouseSensitivity = LocalProfile.MouseSensitivity;
            if (_steps != null) _steps.LocalPlayer = true;
            if (Flashlight != null) Flashlight.Toggled += on => FlashOn.Value = on;
            ViewModel = ViewModelBridge.Create(Camera != null ? Camera.transform : transform, SkinVar.Value, Nick);
            gameObject.AddComponent<LocalPlayerController>();
            if (Camera != null) Camera.tag = "MainCamera";
        }

        private void SetupRemote()
        {
            Motor.SetRemoteProxy(true);
            if (InputReader != null)
            {
                InputReader.ManageCursor = false;
                InputReader.enabled = false;
            }
            if (Camera != null)
            {
                Camera.enabled = false;
                var listener = Camera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
            if (Flashlight != null) Flashlight.ListenToInput = false;
            if (_steps != null) _steps.LocalPlayer = false;
            foreach (var zone in GetComponentsInChildren<AudioReverbZone>(true)) zone.enabled = false;
            Character.SetFirstPersonMode(false);
        }

        // ================================================================ per frame

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                if (Time.unscaledTime >= _nextMotionSend)
                {
                    _nextMotionSend = Time.unscaledTime + 1f / 15f;
                    var pack = MotionPack.From(Motor);
                    if (!pack.Equals(Motion.Value)) Motion.Value = pack;
                }
            }
            else
            {
                var m = Motion.Value;
                Motor.ApplyRemoteState(m.Grounded, m.Crouching, m.CrouchAmount, m.Running, m.PitchDeg,
                    Life.Value == LifeState.Downed);
            }

            // Crawl drag sound while a downed player moves.
            bool crawling = Life.Value == LifeState.Downed && Motor.HorizontalSpeed > 0.2f;
            if (crawling && _crawlLoop == null) _crawlLoop = GameAudio.StartLoop(GameSfx.CrawlLoop, transform, IsOwner ? 0.5f : 0.8f);
            else if (!crawling && _crawlLoop != null) { GameAudio.StopLoop(_crawlLoop); _crawlLoop = null; }
        }

        // ================================================================ state application

        private void ApplyLife(LifeState life, bool changed)
        {
            Actions?.SetLifeState(life);
            Motor.Downed = life == LifeState.Downed;
            if (IsOwner)
            {
                if (life == LifeState.Downed && _heartbeat == null) _heartbeat = GameAudio.StartLoop(GameSfx.HeartbeatLoop, null, 0.9f);
                if (life != LifeState.Downed && _heartbeat != null) { GameAudio.StopLoop(_heartbeat, 1f); _heartbeat = null; }
                if (life == LifeState.Dead) Character.SetFirstPersonMode(false);
                else Character.SetFirstPersonMode(true);
                ViewModel?.SetVisible(life == LifeState.Alive);
            }
            if (changed)
            {
                if (life == LifeState.Dead) GameAudio.PlayAttached(GameSfx.HiderDeath, transform);
                if (life == LifeState.Downed) GameAudio.PlayAttached(GameSfx.HiderBreathPanic, transform, 0.7f);
            }
            if (life != LifeState.Downed && _crawlLoop != null) { GameAudio.StopLoop(_crawlLoop); _crawlLoop = null; }
        }

        private void ApplyHeld(HeldItem item)
        {
            Actions?.SetHeldItem(item);
            if (IsOwner) ViewModel?.SetHeldItem(item);
        }

        private void ApplyReviving(bool on)
        {
            Actions?.SetReviving(on);
            if (on && _reviveLoop == null) _reviveLoop = GameAudio.StartLoop(GameSfx.ReviveLoop, transform, 0.8f);
            if (!on && _reviveLoop != null) { GameAudio.StopLoop(_reviveLoop); _reviveLoop = null; }
        }

        private void StopLoops()
        {
            GameAudio.StopLoop(_heartbeat, 0f);
            GameAudio.StopLoop(_crawlLoop, 0f);
            GameAudio.StopLoop(_reviveLoop, 0f);
            _heartbeat = _crawlLoop = _reviveLoop = null;
        }

        // ================================================================ helpers

        public static NetPlayer Find(ulong networkObjectId)
        {
            foreach (var p in All) if (p != null && p.NetworkObjectId == networkObjectId) return p;
            return null;
        }

        public static NetPlayer FindByClient(ulong clientId)
        {
            foreach (var p in All) if (p != null && p.OwnerClientId == clientId) return p;
            return null;
        }

        /// <summary>Owner-side teleport that also tells the NetworkTransform not to interpolate.</summary>
        public void TeleportLocal(Vector3 pos, float yaw)
        {
            Motor.Teleport(pos, yaw);
            if (_nt != null && _nt.CanCommitToTransform) _nt.Teleport(pos, Quaternion.Euler(0f, yaw, 0f), transform.localScale);
        }

        // ================================================================ server -> owner

        [Rpc(SendTo.Owner)]
        public void TeleportRpc(Vector3 pos, float yaw) => TeleportLocal(pos, yaw);

        /// <summary>Local feedback for round events (role reveal etc.).</summary>
        [Rpc(SendTo.Owner)]
        public void ToastRpc(FixedString128Bytes text, float seconds) => GameHud.Toast(text.ToString(), seconds);

        // ================================================================ owner -> server requests

        [Rpc(SendTo.Server)]
        public void RequestKnifeRpc(ulong victimId) => MatchManager.Instance?.ServerKnife(this, Find(victimId));

        [Rpc(SendTo.Server)]
        public void RequestShootRpc(Vector3 origin, Vector3 dir, ulong victimId, Vector3 hitPoint)
            => MatchManager.Instance?.ServerShoot(this, origin, dir, victimId == ulong.MaxValue ? null : Find(victimId), hitPoint);

        [Rpc(SendTo.Server)]
        public void RequestReloadRpc() => MatchManager.Instance?.ServerReload(this);

        [Rpc(SendTo.Server)]
        public void RequestHolsterRpc(bool glockOut) => MatchManager.Instance?.ServerHolster(this, glockOut);

        [Rpc(SendTo.Server)]
        public void RequestRescueRpc(ulong targetId) => MatchManager.Instance?.ServerRescue(this, Find(targetId));

        [Rpc(SendTo.Server)]
        public void RequestHealRpc(ulong targetId) => MatchManager.Instance?.ServerHeal(this, Find(targetId));

        [Rpc(SendTo.Server)]
        public void RequestPickupRpc(int medkitId) => MatchManager.Instance?.ServerPickup(this, medkitId);

        // ================================================================ effects on every client

        [Rpc(SendTo.Everyone)]
        public void KnifeSequenceRpc(ulong victimId)
        {
            var victim = Find(victimId);
            StartCoroutine(KnifeSequence(victim));
        }

        private IEnumerator KnifeSequence(NetPlayer victim)
        {
            if (Actions is BlockyCharacterActions bca && victim != null) bca.PlayKnifeAttack(victim.Character);
            else Actions?.PlayKnifeAttack();
            victim?.Actions?.PlayStabbedReaction();
            if (IsOwner) ViewModel?.PlayKnifeAttack();
            GameAudio.PlayAttached(GameSfx.KnifeDraw, transform, 0.8f);
            if (victim != null && victim.IsOwner) GameHud.BeginVictimView(this, Tuning.KnifeSequenceDuration);

            float t = 0f;
            for (int i = 0; i < Tuning.StabTimes.Length; i++)
            {
                yield return new WaitForSeconds(Tuning.StabTimes[i] - t);
                t = Tuning.StabTimes[i];
                if (victim == null) yield break;
                Vector3 chest = victim.ChestPosition;
                GameAudio.PlayAt(GameSfx.KnifeSwing, chest, 0.6f);
                GameAudio.PlayAt(GameSfx.KnifeStab, chest);
                GameAudio.PlayAttached(i == 0 ? GameSfx.HiderScream : GameSfx.HiderPain, victim.transform, i == 0 ? 1f : 0.8f);
                BloodFx.Spray(chest, (victim.transform.position - transform.position).normalized);
                if (victim.IsOwner) GameHud.Hurt(0.8f);
            }
            yield return new WaitForSeconds(Tuning.KnifeSequenceDuration - t - 0.4f);
            if (victim != null) GameAudio.PlayAt(GameSfx.BodyFall, victim.transform.position);
        }

        [Rpc(SendTo.Everyone)]
        public void ShotFxRpc(Vector3 hitPoint, ulong victimId)
        {
            Actions?.PlayShoot();
            Transform muzzle = null;
            if (IsOwner && ViewModel != null)
            {
                ViewModel.PlayShoot();
                muzzle = ViewModel.MuzzlePoint;
            }
            if (muzzle == null) muzzle = Actions?.MuzzlePoint;
            Vector3 from = muzzle != null ? muzzle.position : EyePosition + transform.forward * 0.5f;
            if (muzzle != null) MuzzleFlash.Spawn(muzzle, IsOwner);
            ShotFx.Play(from, hitPoint, muzzle, IsOwner, muzzle == null);
            GameAudio.PlayAt(GameSfx.GlockShot, from);
            GameAudio.PlayAt(GameSfx.GlockShotDistant, from, 0.8f);
            GameAudio.PlayAt(GameSfx.ShellCasing, transform.position + transform.right * 0.4f, 0.5f);
            if (IsOwner) GameHud.Kick();

            var victim = victimId == ulong.MaxValue ? null : Find(victimId);
            if (victim != null)
            {
                victim.Actions?.PlayHitReaction();
                GameAudio.PlayAttached(GameSfx.HiderPain, victim.transform);
                BloodFx.Spray(hitPoint, (hitPoint - from).normalized);
                if (victim.IsOwner) GameHud.Hurt(1f);
            }
            else
            {
                GameAudio.PlayAt(GameSfx.BulletImpact, hitPoint, 0.8f);
            }
        }

        [Rpc(SendTo.Everyone)]
        public void DryFireRpc() => GameAudio.PlayAttached(GameSfx.GlockDryFire, transform, 0.8f);

        [Rpc(SendTo.Everyone)]
        public void ReloadFxRpc()
        {
            Actions?.PlayReload();
            if (IsOwner) ViewModel?.PlayReload();
            GameAudio.PlayAttached(GameSfx.GlockReload, transform);
        }

        [Rpc(SendTo.Everyone)]
        public void SfxRpc(GameSfx sfx, float volume) => GameAudio.PlayAttached(sfx, transform, volume);
    }
}
