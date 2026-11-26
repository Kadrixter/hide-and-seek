using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
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
    public partial class NetPlayer : NetworkBehaviour
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
        /// <summary>Hit once: limps and holds the belly. The next hit downs (bleeding).</summary>
        public readonly NetworkVariable<bool> Limping = new();
        /// <summary>Bit mask of cut-off limbs (<see cref="Limb"/>): a leg after the chainsaw's first hit, all of them after the finisher.</summary>
        public readonly NetworkVariable<byte> Severed = new();
        /// <summary>Seeker: server time at which the next melee attack (knife / chainsaw) is allowed.</summary>
        public readonly NetworkVariable<double> AttackReadyAt = new();
        /// <summary>Seeker: dust in the eyes until this server time.</summary>
        public readonly NetworkVariable<double> BlindUntil = new();
        /// <summary>Hider: next dust throw allowed at this server time.</summary>
        public readonly NetworkVariable<double> DustReadyAt = new();
        public readonly NetworkVariable<byte> MedkitCount = new();
        public readonly NetworkVariable<byte> SyringeCount = new();
        /// <summary>Brought back with adrenaline: blinks blue on the seeker's minimap every few seconds.</summary>
        public readonly NetworkVariable<bool> Resurrected = new();
        public readonly NetworkVariable<HeldItem> Held = new();
        public readonly NetworkVariable<bool> HasGlock = new();
        public readonly NetworkVariable<bool> HasChainsaw = new();
        /// <summary>Fuel left in this player's chainsaw (server writes, 0.1 steps; MatchManager.SawFuel.cs).</summary>
        public readonly NetworkVariable<float> SawFuel = new(HideAndSeek.Game.Net.SawFuel.Full);
        /// <summary>Server: the exact fuel (the network variable follows in 0.1 steps).</summary>
        public float ServerSawFuel = HideAndSeek.Game.Net.SawFuel.Full;
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
        /// <summary>Chainsaw at full throttle (owner holds F).</summary>
        public readonly NetworkVariable<bool> SawRev = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Downed player this one is lifting (ulong.MaxValue = nobody) and the progress 0..255.</summary>
        public readonly NetworkVariable<ulong> ReviveTarget = new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> ReviveProgress = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Hold animation running (Characters.HoldAnim: 1 inject, 2 self-heal); timing comes from ReviveProgress.</summary>
        public readonly NetworkVariable<byte> HoldAnimVar = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Pose of the RC car this player drives (owner, ~20 Hz).</summary>
        public readonly NetworkVariable<RcPack> RcDrive = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>RC race: checkpoints passed (counted by the driver; laps = (n - 1) / 16).</summary>
        public readonly NetworkVariable<ushort> RaceCps = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Lobby toy bricks: colour in the hand while building (0 = not building, 1..5).</summary>
        public readonly NetworkVariable<byte> BrickColor = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Lobby seat index the player sits on (-1 = standing).</summary>
        public readonly NetworkVariable<sbyte> Seat = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Lobby "ready" flag (server-written via <see cref="SetReadyRpc"/>).</summary>
        public readonly NetworkVariable<bool> Ready = new();

        public string Nick => NickVar.Value.ToString();
        public bool IsHider => Role.Value == PlayerRole.Hider;
        public bool IsSeeker => Role.Value == PlayerRole.Seeker;
        public bool IsAliveHider => IsHider && Life.Value == LifeState.Alive && !Escaped.Value; // escaped = out of the round (NetPlayer.Escape.cs)

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
        private AudioSource _heartbeat, _crawlLoop, _reviveLoop, _sawIdle, _sawRev;
        private float _nextMotionSend;
        private Transform _chestFallback;
        private ShoulderLamp _shoulderLamp;
        private bool _vmVisible = true;

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
            _shoulderLamp = ShoulderLamp.Attach(Character);
            SetupRagdoll();
            if (Flashlight != null) Flashlight.HoldOffset = ShoulderLamp.EyeOffset;
            if (_steps != null) _steps.MasterVolume = LocalProfile.FootstepVolume;
            if (IsOwner) SetupLocal();
            else SetupRemote();

            NickVar.OnValueChanged += (_, _) => RefreshSkin();
            SkinVar.OnValueChanged += (_, _) => RefreshSkin();
            McNickVar.OnValueChanged += (_, _) => LoadMcSkin();
            LoadMcSkin();
            Life.OnValueChanged += (old, v) =>
            {
                // Dead -> Alive during a round = adrenaline: violent gasp instead of the plain get-up.
                var mmL = MatchManager.Instance;
                if (old == LifeState.Dead && v == LifeState.Alive && mmL != null && mmL.Phase.Value == MatchPhase.Seeking)
                {
                    (Actions as BlockyCharacterActions)?.PrepareResurrectGasp();
                    GameAudio.PlayAttached(GameSfx.ResurrectGasp, transform, 1f);
                    if (IsOwner) BeginResurrectRecovery();
                }
                ApplyLife(v, true);
            };
            Held.OnValueChanged += (_, v) => ApplyHeld(v);
            FlashOn.OnValueChanged += (_, v) => { if (!IsOwner && Flashlight != null) Flashlight.SetOn(v, true); };
            FlashFocus.OnValueChanged += (_, v) => { if (!IsOwner && Flashlight != null) Flashlight.SetFocus(v / 255f); };
            Reviving.OnValueChanged += (_, v) => ApplyReviving(v);
            Seat.OnValueChanged += (_, v) => ApplySeat(v, true);
            ApplySeat(Seat.Value, false);
            Severed.OnValueChanged += (_, v) => ApplySevered(v, true);
            ApplySevered(Severed.Value, false);

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
                _netSkin = null;
                Character?.SetCustomSkin(null);
                (ViewModel as FirstPersonViewModel)?.SetCustomSkin(null);
                return;
            }
            Debug.Log($"[Skin] {Nick}: downloading Minecraft skin {mc}");
            MinecraftSkins.Get(mc, (tex, err) =>
            {
                Debug.Log($"[Skin] {Nick}: {mc} -> {(tex != null ? "applied" : "failed: " + err)}");
                if (this == null || tex == null || McNickVar.Value.ToString() != mc) return;
                if (_netSkin == null) ApplySkinTexture(tex);
                // The owner shares the skin file itself, so others need neither Mojang access nor a download.
                if (IsOwner && IsSpawned)
                {
                    byte[] png = tex.EncodeToPNG();
                    if (png != null && png.Length <= MaxSkinBytes) SubmitSkinRpc(png);
                }
            });
        }

        private const int MaxSkinBytes = 48 * 1024;
        private byte[] _skinPng;      // server copy, re-sent to late joiners
        private Texture2D _netSkin;

        private void ApplySkinTexture(Texture2D tex)
        {
            Character?.SetCustomSkin(tex);
            (ViewModel as FirstPersonViewModel)?.SetCustomSkin(tex);
        }

        [Rpc(SendTo.Server)]
        private void SubmitSkinRpc(byte[] png)
        {
            if (png == null || png.Length > MaxSkinBytes) return;
            _skinPng = png;
            SkinPngRpc(png, RpcTarget.Everyone);
        }

        /// <summary>Server: sends the stored skin file to one newly connected client.</summary>
        public void SendSkinTo(ulong clientId)
        {
            if (_skinPng != null) SkinPngRpc(_skinPng, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SkinPngRpc(byte[] png, RpcParams rpcParams)
        {
            if (png == null || png.Length == 0 || IsOwner) return;
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!raw.LoadImage(png)) return;
            var tex = CustomSkins.Prepare(raw, "net_" + Nick);
            if (tex == null) return;
            _netSkin = tex;
            ApplySkinTexture(tex);
            Debug.Log($"[Skin] {Nick}: received skin file ({png.Length} B)");
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
            _shoulderLamp?.SetFirstPerson(true);
            Character.SetLayer(PlayerAvatarBinder.LocalBodyLayer);
            foreach (var l in GetComponentsInChildren<Light>(true))
                l.cullingMask &= ~(1 << PlayerAvatarBinder.LocalBodyLayer);
            if (InputReader != null)
            {
                InputReader.MouseSensitivity = LocalProfile.MouseSensitivity;
                InputReader.CrouchToggleMode = LocalProfile.CrouchToggle;
            }
            if (_steps != null) _steps.LocalPlayer = true;
            if (Flashlight != null) Flashlight.Toggled += on => FlashOn.Value = on;
            ViewModel = ViewModelBridge.Create(Camera != null ? Camera.transform : transform, SkinVar.Value, Nick);
            gameObject.AddComponent<LocalPlayerController>();
            _seatView = gameObject.AddComponent<SeatedView>();
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
            if (_shoulderLamp != null && Flashlight != null) _shoulderLamp.SetOn(Flashlight.Level > 0.05f);
            if (IsOwner)
            {
                if (Time.unscaledTime >= _nextMotionSend)
                {
                    _nextMotionSend = Time.unscaledTime + 1f / 15f;
                    var pack = MotionPack.From(Motor, Motor.IsProne, Motor.ProneAmount);
                    if (!pack.Equals(Motion.Value)) Motion.Value = pack;
                }
            }
            else
            {
                var m = Motion.Value;
                Motor.ApplyRemoteState(m.Grounded, m.Crouching, m.CrouchAmount, m.Running, m.PitchDeg,
                    Life.Value == LifeState.Downed, m.IsProne, m.ProneAmount);
            }

            UpdateChainsaw();
            UpdateHoldAnimation();
            UpdateArcadeHands();
            UpdateRc();
            UpdateGasp();
            UpdateBrickHand();
            UpdateFlashCarry();
            UpdateToyGun();
            UpdateDrunk();
            UpdateRcRemote();
            UpdateHandItem();
            UpdateLegThrow(); // NetPlayer.LegThrow.cs
            UpdateStun(); // NetPlayer.Stun.cs
            UpdateRcZap(); // electrocuted by an energised RC car (NetPlayer.RcPower.cs)
            UpdateEmote();
            UpdateBodyCarry();

            float prone = Life.Value == LifeState.Alive ? Motor.ProneAmount : 0f;
            (Actions as BlockyCharacterActions)?.SetProneAmount(prone);
            if (IsOwner)
            {
                bool healing = HoldAnimVar.Value == (byte)HoldAnim.SelfHeal && Life.Value == LifeState.Downed;
                bool vm = (Life.Value == LifeState.Alive && prone < 0.4f || healing) && !KillCam.Active && !_shockHideVm && !_odHideVm && !StunHidesVm
                          && !_rcCamOn && !(RcAnimating && !_rcHandsOn) && !OwnBodyShown && !EmoteViewActive;
                if (vm != _vmVisible) { _vmVisible = vm; ViewModel?.SetVisible(vm); }
            }

            // Crawl drag sound while a downed (or voluntarily crawling) player moves; voluntary crawl is quieter.
            bool downedCrawl = Life.Value == LifeState.Downed && Motor.HorizontalSpeed > 0.2f && !BodyCarriedNow;
            bool crawling = downedCrawl || (prone > 0.9f && Motor.HorizontalSpeed > 0.2f);
            if (crawling && _crawlLoop == null)
                _crawlLoop = GameAudio.StartLoop(GameSfx.CrawlLoop, transform, (IsOwner ? 0.5f : 0.8f) * (downedCrawl ? 1f : 0.45f));
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
                ApplyOwnBodyMode();
                _vmVisible = life == LifeState.Alive && !OwnBodyShown;
                ViewModel?.SetVisible(_vmVisible);
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

        private void ApplySeat(sbyte seat, bool changed)
        {
            if (Actions is BlockyCharacterActions act)
            {
                if (seat >= 0)
                {
                    // The seated body is anchored to the seat; sitting down blends from where the player stood.
                    var s = LobbySeat.Get(seat);
                    act.SetSitSeat(s != null ? s.transform : null, s != null ? s.Shape : null);
                    if (changed && Character != null)
                    {
                        bool prepared = Time.frameCount - _sitFromFrame <= 2;
                        Vector3 from = prepared ? _sitFromPos : Character.transform.position;
                        Quaternion fromRot = prepared ? _sitFromRot : Character.transform.rotation;
                        if (s == null || (from - s.transform.position).sqrMagnitude < 3.5f * 3.5f)
                        {
                            act.BeginSitFrom(from, fromRot);
                            if (IsOwner && prepared && Camera != null) _seatView?.BeginTurn(_sitCamRot, _sitCamPos, act.SitInTime);
                        }
                    }
                }
                act.SetSitting(seat >= 0);
            }
            if (IsOwner) Motor.Seated = seat >= 0;
        }

        [Rpc(SendTo.Server)]
        public void SetReadyRpc(bool ready)
        {
            var mm = MatchManager.Instance;
            if (mm != null && mm.Phase.Value != MatchPhase.Lobby) return;
            Ready.Value = ready;
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
        public void ToastRpc(FixedString128Bytes text, float seconds)
        {
            Debug.Log("[Toast] " + text);
            GameHud.Toast(text.ToString(), seconds);
        }

        // ================================================================ owner -> server requests

        [Rpc(SendTo.Server)]
        public void RequestKnifeRpc(ulong victimId) => MatchManager.Instance?.ServerKnife(this, Find(victimId));

        [Rpc(SendTo.Server)]
        public void RequestShootRpc(Vector3 origin, Vector3 dir, ulong victimId, Vector3 hitPoint, sbyte rcSlot)
            => MatchManager.Instance?.ServerShoot(this, origin, dir, victimId == ulong.MaxValue ? null : Find(victimId), hitPoint, rcSlot);

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

        [Rpc(SendTo.Server)]
        public void RequestSelfReviveRpc() => MatchManager.Instance?.ServerSelfRevive(this);

        [Rpc(SendTo.Server)]
        public void RequestPickupSyringeRpc(int id) => MatchManager.Instance?.ServerPickupSyringe(this, id);

        [Rpc(SendTo.Server)]
        public void RequestResurrectRpc(ulong targetId) => MatchManager.Instance?.ServerResurrect(this, Find(targetId));

        [Rpc(SendTo.Server)]
        public void RequestChainsawRpc(ulong victimId) => MatchManager.Instance?.ServerChainsaw(this, Find(victimId));

        [Rpc(SendTo.Server)]
        public void RequestForkPickupRpc() => MatchManager.Instance?.ServerPickupFork(this);

        [Rpc(SendTo.Server)]
        public void RequestForkOutletRpc(byte outlet) => MatchManager.Instance?.ServerForkOutlet(this, outlet);

        [Rpc(SendTo.Server)]
        public void RequestArcadeRpc(bool start, byte cab) => MatchManager.Instance?.ServerArcade(this, start, cab);

        [Rpc(SendTo.Server)]
        public void ArcadeDirRpc(byte dir) => MatchManager.Instance?.ServerArcadeDir(this, dir);

        /// <summary>Lobby easter egg: this player pushes the fork into socket <paramref name="outlet"/>.</summary>
        [Rpc(SendTo.Everyone)]
        public void ShockRpc(byte outlet)
        {
            if (_shock != null) StopCoroutine(_shock);
            _shock = StartCoroutine(ShockSequence(outlet));
        }

        private Coroutine _shock;
        private bool _shockHideVm;

        /// <summary>Lobby easter egg: this player opened the exit door (minigun, the shadow shuts the door).</summary>
        [Rpc(SendTo.Everyone)]
        public void ExitDoorRpc()
        {
            if (_shock != null) StopCoroutine(_shock);
            _shock = StartCoroutine(ExitDoorSequence());
        }

        private IEnumerator ExitDoorSequence()
        {
            var door = LobbyExitDoor.Instance;
            door?.Play(this);
            var bca = Actions as BlockyCharacterActions;
            var fpv = IsOwner ? ViewModel as FirstPersonViewModel : null;
            var bob = IsOwner ? GetComponentInChildren<HeadBob>() : null;
            float t = 0f;
            while (t < ExitTimeline.Respawn)
            {
                t += Time.deltaTime;
                Vector3 handle = door != null ? door.HandleWorld : transform.position + transform.forward * 0.7f + Vector3.up;
                bca?.SetGunned(t, handle);
                if (IsOwner)
                {
                    // First person: hand on the handle, stare into the light, shaken by the hits, thrown on the back,
                    // then the head lifts to see the shadow shut the door.
                    fpv?.SetOverrideHands(t < ExitTimeline.DoorOpenEnd, handle + transform.forward * 0.03f, Quaternion.LookRotation(transform.forward));
                    _shockHideVm = t >= ExitTimeline.DoorOpenEnd;
                    float hit = t >= ExitTimeline.FireStart && t < ExitTimeline.FireEnd ? (t < ExitTimeline.FallEnd ? 1f : 0.45f) : 0f;
                    float fall = HealTimeline.Seg(t, ExitTimeline.FallStart, ExitTimeline.FallEnd);
                    float lookUp = fall * (1f - HealTimeline.Seg(t, ExitTimeline.FigureIn - 0.4f, ExitTimeline.FigureIn + 0.4f));
                    float watch = HealTimeline.Seg(t, ExitTimeline.FigureIn - 0.4f, ExitTimeline.FigureIn + 0.4f);
                    if (bob != null)
                    {
                        bob.ExtraEuler = new Vector3(Random.Range(-1f, 1f) * 7f * hit - 60f * lookUp - 8f * watch - 6f * HealTimeline.Seg(t, ExitTimeline.DoorOpenStart, ExitTimeline.DoorOpenEnd) * (1f - fall),
                            Random.Range(-1f, 1f) * 6f * hit, Random.Range(-1f, 1f) * 9f * hit + 14f * fall * (1f - watch * 0.6f));
                        bob.ExtraPosition = Random.insideUnitSphere * 0.035f * hit + new Vector3(0f, 0f, -0.85f * fall); // the head lies behind the feet
                        bob.EyeOverride = Mathf.Lerp(0.22f, 0.36f, watch);
                        bob.EyeOverrideWeight = fall;
                    }
                    if (t < ExitTimeline.DoorOpenEnd + 0.3f)
                        Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, 0f, Time.deltaTime * 60f));
                }
                yield return null;
            }
            bca?.SetGunned(-1f, Vector3.zero);
            _shockHideVm = false;
            if (IsOwner)
            {
                fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                if (bob != null)
                {
                    bob.ExtraEuler = Vector3.zero;
                    bob.ExtraPosition = Vector3.zero;
                    bob.EyeOverrideWeight = 0f;
                }
            }
            _shock = null;
        }

        [Rpc(SendTo.Server)]
        public void RequestExitDoorRpc() => MatchManager.Instance?.ServerExitDoor(this);
        [Rpc(SendTo.Server)]
        public void RequestVendingRpc() => MatchManager.Instance?.ServerBuyCan(this);

        private IEnumerator ShockSequence(int idx)
        {
            Vector3 outlet = MatchManager.OutletPos[idx], n = MatchManager.OutletNormal[idx];
            var bca = Actions as BlockyCharacterActions;
            var fpv = IsOwner ? ViewModel as FirstPersonViewModel : null;
            var bob = IsOwner ? GetComponentInChildren<HeadBob>() : null;
            bool fx = false, fell = false;
            float t = 0f;
            while (t < ShockTimeline.Respawn)
            {
                t += Time.deltaTime;
                bca?.SetShock(t, outlet);
                if (!fx && t >= ShockTimeline.Insert)
                {
                    fx = true;
                    ShockFx.Play(outlet, n, ShockTimeline.ShockEnd - ShockTimeline.Insert, ShockTimeline.Respawn - ShockTimeline.Insert);
                    GameAudio.PlayAt(GameSfx.ForkSparks, outlet, 1f);
                    GameAudio.PlayAttached(GameSfx.ForkScream, transform, 1f);
                }
                if (!fell && t >= ShockTimeline.ShockEnd + 0.3f)
                {
                    fell = true;
                    GameAudio.PlayAt(GameSfx.BodyFall, transform.position, 0.9f);
                }
                if (IsOwner)
                {
                    // First person: hand pushes the fork in, then the view shakes, falls and lies looking up.
                    float shake = t >= ShockTimeline.Insert && t < ShockTimeline.ShockEnd ? 1f
                        : t >= ShockTimeline.FallEnd && t < ShockTimeline.TwitchEnd ? 0.5f * (1f - (t - ShockTimeline.FallEnd) / (ShockTimeline.TwitchEnd - ShockTimeline.FallEnd)) : 0f;
                    float down = t < ShockTimeline.Insert ? HealTimeline.Seg(t, 0f, 0.45f) : 0f;
                    float lie = HealTimeline.Seg(t, ShockTimeline.ShockEnd, ShockTimeline.FallEnd);
                    if (bob != null)
                    {
                        bob.ExtraEuler = new Vector3(Random.Range(-1f, 1f) * 6f * shake + 22f * down - 75f * lie, Random.Range(-1f, 1f) * 6f * shake, Random.Range(-1f, 1f) * 8f * shake + 20f * lie);
                        bob.ExtraPosition = Random.insideUnitSphere * 0.03f * shake + new Vector3(0f, 0f, -0.85f * lie);
                        bob.EyeOverride = t < ShockTimeline.ShockEnd ? Mathf.Lerp(Motor.EyeHeight, 0.95f, down) : 0.22f;
                        bob.EyeOverrideWeight = t < ShockTimeline.Insert ? down : t < ShockTimeline.ShockEnd ? 1f - HealTimeline.Seg(t, ShockTimeline.Insert, ShockTimeline.Insert + 0.15f) : lie;
                    }
                    if (t >= ShockTimeline.Insert && t < ShockTimeline.ShockEnd && Random.value < 0.25f) GameHud.Hurt(0.5f);
                    fpv?.SetOverrideHands(t < ShockTimeline.Insert + 0.1f, ForkHandWorld(outlet, n, t), Quaternion.LookRotation(-n, Vector3.up));
                    _shockHideVm = t >= ShockTimeline.Insert + 0.1f;
                }
                yield return null;
            }
            bca?.SetShock(-1f, outlet);
            _shockHideVm = false;
            if (IsOwner)
            {
                fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                if (bob != null)
                {
                    bob.ExtraEuler = Vector3.zero;
                    bob.ExtraPosition = Vector3.zero;
                    bob.EyeOverrideWeight = 0f;
                }
            }
            _shock = null;
        }

        /// <summary>First-person fork hand: from the hip towards the socket, pushed in at the end.</summary>
        private Vector3 ForkHandWorld(Vector3 outlet, Vector3 n, float t)
        {
            float len = BlockyProps.ForkTipLocal.z * BlockyProps.ForkScale;
            Vector3 cam = Motor.transform.position + Vector3.up * 0.95f;
            Vector3 start = cam + Motor.transform.forward * 0.4f + Motor.transform.right * 0.2f - Vector3.up * 0.35f;
            Vector3 end = outlet + n * (len - 0.03f);
            return Vector3.Lerp(start, end, HealTimeline.Seg(t, 0.3f, ShockTimeline.Insert));
        }

        /// <summary>Arcade player: hands on the joystick / buttons (everyone), first-person hands for the owner.</summary>
        private void UpdateArcadeHands()
        {
            var mm = MatchManager.Instance;
            int cab = mm != null ? mm.ArcadeCabOf(NetworkObjectId) : -1;
            bool on = cab >= 0 && Life.Value == LifeState.Alive;
            Vector2 dir = Vector2.zero;
            var game = ArcadeState.Of(Mathf.Max(0, cab));
            if (on && game.Live && !game.Crashed)
                dir = game.Dir switch { 0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, _ => Vector2.left };
            int c = Mathf.Max(0, cab);
            (Actions as BlockyCharacterActions)?.SetArcade(on, ArcadeCabs.StickBall(c) + ArcadeCabs.Tilt(c, dir, 0.035f), ArcadeCabs.Buttons(c), dir);
            if (IsOwner && _shock == null && (on || _arcadeHands))
            {
                ArcadeHands.FirstPerson(Camera != null ? Camera.transform : transform, dir, out var handR, out var handL);
                (ViewModel as FirstPersonViewModel)?.SetOverrideHands(on, handR, Quaternion.identity, handL);
            }
            _arcadeHands = on;
        }

        private bool _arcadeHands;
        private byte _brickShown;

        /// <summary>Toy brick of the chosen colour in the hand while building (everybody sees it).</summary>
        private void UpdateBrickHand()
        {
            byte c = Life.Value == LifeState.Alive ? BrickColor.Value : (byte)0;
            if (c == _brickShown) return;
            _brickShown = c;
            Color? col = c > 0 && c <= LobbyBricks.Colors.Length ? LobbyBricks.Colors[c - 1] : (Color?)null;
            (Actions as BlockyCharacterActions)?.SetBrick(col);
            if (IsOwner && ViewModel is FirstPersonViewModel fpv)
            {
                if (col.HasValue) fpv.SetBrickTint(col.Value);
                fpv.SetHeldItem(col.HasValue ? HeldItem.Brick : Held.Value);
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestRadioRpc(RadioOp op, FixedString128Bytes url) => MatchManager.Instance?.ServerRadio(this, op, url.ToString());

        [Rpc(SendTo.Server)]
        public void RequestPlaceBrickRpc(int packed) => MatchManager.Instance?.ServerPlaceBrick(this, packed);

        [Rpc(SendTo.Server)]
        public void RequestUndoBrickRpc() => MatchManager.Instance?.ServerUndoBrick(this);

        // ================================================================ resurrection recovery (owner)

        private float _gaspT = -1f;

        /// <summary>Owner: catching breath after the adrenaline (no walking until the body has got up).</summary>
        public bool Gasping => _gaspT >= 0f && _gaspT < HealTimeline.GaspDuration - 0.3f;

        /// <summary>
        /// Dead -> Alive: clears everything that could still hold the player (crouch / sit-up overrides, holds, seat,
        /// stance, camera offsets), re-seats the controller on the floor and plays the first-person gasp.
        /// </summary>
        private void BeginResurrectRecovery()
        {
            _gaspT = 0f;
            Motor.ForceCrouch = false;
            Motor.DownedSitting = false;
            Motor.Downed = false;
            if (HoldAnimVar.Value != 0) HoldAnimVar.Value = 0;
            if (Reviving.Value) Reviving.Value = false;
            if (ReviveTarget.Value != ulong.MaxValue) ReviveTarget.Value = ulong.MaxValue;
            if (ReviveProgress.Value != 0) ReviveProgress.Value = 0;
            if (Seat.Value >= 0) Seat.Value = -1;
            InputReader?.ResetStance();
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            TeleportLocal(transform.position + Vector3.up * 0.05f, transform.eulerAngles.y);
            if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
            if (_bob != null)
            {
                _bob.ExtraEuler = Vector3.zero;
                _bob.ExtraPosition = Vector3.zero;
            }
            if (Camera != null && RcCarView.Active == null && !KillCam.Active) Camera.enabled = true;
            Debug.Log($"[NetPlayer] {Nick} resurrected: recovery reset (pos {transform.position})");
        }

        /// <summary>First-person gasp: from lying on the back, a violent sit-up, a few breaths, onto the knees, up.</summary>
        private void UpdateGasp()
        {
            if (!IsOwner || _gaspT < 0f) return;
            if (Life.Value != LifeState.Alive) { EndGasp(); return; }
            _gaspT += Time.deltaTime;
            float t = _gaspT, g = HealTimeline.GaspDuration;
            if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
            if (_bob == null) { if (t > g) _gaspT = -1f; return; }
            float eye;
            if (t < 0.22f) eye = Mathf.Lerp(0.3f, 0.78f, RcTimeline.Seg(t, 0f, 0.22f));
            else if (t < 0.95f) eye = 0.78f + 0.02f * Mathf.Sin(t * 9f);
            else if (t < 1.5f) eye = Mathf.Lerp(0.78f, 1.02f, RcTimeline.Seg(t, 0.95f, 1.5f));
            else eye = Mathf.Lerp(1.02f, Motor.EyeHeight, RcTimeline.Seg(t, 1.5f, g));
            _bob.EyeOverride = eye;
            _bob.EyeOverrideWeight = 1f - RcTimeline.Seg(t, g - 0.2f, g + 0.1f);
            float look = t < 0.22f ? Mathf.Lerp(-55f, -12f, RcTimeline.Seg(t, 0f, 0.22f)) : -12f * (1f - RcTimeline.Seg(t, 0.4f, 1.2f));
            float breath = t > 0.22f && t < 1.5f ? 5f * Mathf.Sin(t * 9f) * (1f - RcTimeline.Seg(t, 1f, 1.5f)) : 0f;
            _bob.ExtraEuler = new Vector3(look + breath, 0f, 0f);
            if (t > g + 0.15f) EndGasp();
        }

        private void EndGasp()
        {
            var cc = GetComponent<CharacterController>();
            Debug.Log($"[NetPlayer] {Nick} recovery done: life={Life.Value} busy={Busy.Value} move={Motor.MovementEnabled} look={Motor.LookEnabled} cc={(cc != null && cc.enabled)} downed={Motor.Downed} grounded={Motor.IsGrounded}");
            _gaspT = -1f;
            if (_bob != null)
            {
                _bob.EyeOverrideWeight = 0f;
                _bob.ExtraEuler = Vector3.zero;
            }
        }

        // ================================================================ RC car

        private int _rcSlot = -1;
        private bool _rcDriving, _rcPlacing, _rcCamOn, _rcHandsOn, _rcPlacedSound, _rcEye;
        private float _rcT, _rcExitAt = -1f, _rcStandT = 99f, _nextRcSend;
        private Vector2 _rcInput;
        private HeadBob _bob;

        /// <summary>The car is still in the hand during the put-down animation (the world car is hidden).</summary>
        public bool RcCarInHand => _rcDriving && _rcPlacing && _rcT < RcTimeline.Release;
        /// <summary>Kneeling / sitting / standing up for the RC car: no walking, no looking around.</summary>
        public bool RcAnimating => _rcDriving || _rcStandT < RcTimeline.StandUp;
        /// <summary>Owner: steering right now (car camera on, car not under the seeker's boot).</summary>
        public bool RcControlling
        {
            get
            {
                var mm = MatchManager.Instance;
                return _rcDriving && _rcCamOn && _rcExitAt < 0f && mm != null && _rcSlot >= 0 && mm.RcSlot(_rcSlot).Mode == RcMode.Driving;
            }
        }
        public int RcSlotIndex => _rcSlot;
        public bool RcCameraOn => _rcCamOn;
        /// <summary>Seconds since the current drive started (sit-down animation clock).</summary>
        public float RcDriveTime => _rcDriving ? _rcT : 0f;

        /// <summary>Owner: stick positions for the transmitter animation (x steer, y throttle).</summary>
        public void SetRcInput(Vector2 input) => _rcInput = input;

        /// <summary>Called on every peer when one of the car slots changes and this player is (or was) its holder.</summary>
        public void OnRcSlotChanged(int i, RcSlotState old, RcSlotState now)
        {
            // The car is gone and this holder smashes its transmitter (NetPlayer.RcRemote).
            if (now.Holder == NetworkObjectId && now.Remote == RcRemoteFate.Thrown && old.Remote != RcRemoteFate.Thrown)
            {
                BeginRemoteThrow(i, now);
                return;
            }
            if (now.Holder == NetworkObjectId && now.Mode == RcMode.Driving && old.Mode != RcMode.Driving)
            {
                _rcSlot = i;
                _rcDriving = true;
                _rcPlacing = old.Mode == RcMode.Carried;
                _rcT = 0f;
                _rcExitAt = -1f;
                _rcPlacedSound = false;
                return;
            }
            if (i != _rcSlot || !_rcDriving) return;
            if (now.Mode == RcMode.Doomed) return; // frozen under the boot, the driver still watches
            if (now.Mode == RcMode.Wreck || now.Mode == RcMode.Shot)
            {
                if (_rcExitAt < 0f) _rcExitAt = Time.time + 0.9f; // see it crushed, then back to the body
                return;
            }
            if (now.Mode != RcMode.Driving) StopRcDrive();
        }

        private void StopRcDrive()
        {
            if (!_rcDriving) return;
            _rcDriving = false;
            _rcExitAt = -1f;
            _rcStandT = 0f;
        }

        private void UpdateRc()
        {
            float dt = Time.deltaTime;
            var mm = MatchManager.Instance;
            if (_rcDriving)
            {
                _rcT += dt;
                if (_rcExitAt >= 0f && Time.time >= _rcExitAt) StopRcDrive();
                // Safety net: the slot is no longer ours (missed change, scene change, round over).
                var cur = mm != null && _rcSlot >= 0 ? mm.RcSlot(_rcSlot) : default;
                if (mm == null || _rcSlot < 0 || cur.Holder != NetworkObjectId ||
                    !(cur.Mode == RcMode.Driving || cur.Mode == RcMode.Doomed || cur.Mode == RcMode.Wreck || cur.Mode == RcMode.Shot) || Life.Value != LifeState.Alive)
                    StopRcDrive();
            }
            else _rcStandT += dt;

            RcSlotState st = mm != null && _rcSlot >= 0 ? mm.RcSlot(_rcSlot) : default;
            Vector2 input = IsOwner ? _rcInput : new Vector2(RcDrive.Value.Steer / 100f, RcDrive.Value.Throttle / 100f);
            // RC race: everybody sits on the grandstand with the transmitter (the race system drives the cars).
            if (RaceSystem.Instance != null && mm != null && (mm.Phase.Value == MatchPhase.Race || mm.Phase.Value == MatchPhase.Ended))
            {
                (Actions as BlockyCharacterActions)?.SetRcDrive(true, false, 5f, input, RaceSystem.Instance.CarPos(this));
                return;
            }
            (Actions as BlockyCharacterActions)?.SetRcDrive(_rcDriving, _rcPlacing, _rcT, input, st.Pos);
            if (_rcDriving && _rcPlacing && !_rcPlacedSound && _rcT >= RcTimeline.Release)
            {
                _rcPlacedSound = true;
                GameAudio.PlayAt(GameSfx.RcPlace, st.Pos + Vector3.up * 0.05f, 0.8f);
            }
            if (!IsOwner) return;

            var car = _rcSlot >= 0 ? RcCar.Peek(_rcSlot) : null;
            var fpv = ViewModel as FirstPersonViewModel;

            // First-person hand carries the car down onto the floor.
            bool hands = _rcDriving && _rcPlacing && _rcT < RcTimeline.Release + 0.05f && Camera != null;
            if (hands)
            {
                Vector3 floorFist = st.Pos - BlockyProps.RcCarInHand;
                Vector3 cam = Camera.transform.position;
                Vector3 start = cam + transform.forward * 0.42f + transform.right * 0.22f - Vector3.up * 0.32f;
                Vector3 hand = Vector3.Lerp(start, floorFist + Vector3.up * 0.12f, RcTimeline.Seg(_rcT, 0.1f, 0.55f));
                hand = Vector3.Lerp(hand, floorFist, RcTimeline.Seg(_rcT, 0.55f, RcTimeline.Release - 0.05f));
                fpv?.SetOverrideHands(true, hand, Quaternion.Euler(0f, st.Yaw, 0f), null, HeldItem.RcCar);
                _rcHandsOn = true;
            }
            else if (_rcHandsOn)
            {
                fpv?.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                _rcHandsOn = false;
            }

            // Camera: from the eyes (kneel, look at the floor) to the car, and back.
            bool camWanted = _rcDriving && car != null && _rcT >= (_rcPlacing ? RcTimeline.CameraIn : RcTimeline.Resume * 0.6f) &&
                             (_rcExitAt < 0f || Time.time < _rcExitAt);
            if (camWanted && !_rcCamOn)
            {
                RcCarView.Begin(this, car);
                _rcCamOn = true;
                SetCinematicBody(true); // the own body sitting on the floor is visible from the car
            }
            else if (!camWanted && _rcCamOn)
            {
                RcCarView.Active?.End();
                _rcCamOn = false;
                SetCinematicBody(false);
            }

            if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
            const float sitEye = 0.8f;
            if (_bob != null && _shock == null)
            {
                if (_rcDriving)
                {
                    float down = _rcPlacing ? RcTimeline.Seg(_rcT, 0f, 0.45f) : RcTimeline.Seg(_rcT, 0f, 0.3f);
                    float sit = _rcPlacing ? RcTimeline.Seg(_rcT, RcTimeline.Release, 1.25f) : RcTimeline.Seg(_rcT, 0.3f, RcTimeline.Resume);
                    _bob.EyeOverride = Mathf.Lerp(Mathf.Lerp(Motor.EyeHeight, 1.02f, down), sitEye, sit);
                    _bob.EyeOverrideWeight = 1f;
                    _rcEye = true;
                    if (_rcPlacing && _rcT < RcTimeline.CameraIn)
                    {
                        // Watch the hand put the car down.
                        Vector3 d = st.Pos - (transform.position + Vector3.up * _bob.EyeOverride);
                        float want = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -75f, -20f);
                        Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, want, dt * 140f));
                    }
                }
                else if (_rcStandT < RcTimeline.StandUp + 0.1f)
                {
                    _bob.EyeOverride = sitEye;
                    _bob.EyeOverrideWeight = 1f - RcTimeline.Seg(_rcStandT, 0.1f, RcTimeline.StandUp);
                    _rcEye = true;
                    Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, 0f, dt * 60f));
                }
                else if (_rcEye)
                {
                    _bob.EyeOverrideWeight = 0f;
                    _rcEye = false;
                }
            }

            // Send the car pose ~20 times per second.
            if (_rcCamOn && car != null && car.View == RcCar.ViewMode.Local && Time.unscaledTime >= _nextRcSend)
            {
                _nextRcSend = Time.unscaledTime + 0.05f;
                var pk = car.Pack();
                if (!pk.Equals(RcDrive.Value)) RcDrive.Value = pk;
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestRcPickupRpc(byte slot) => MatchManager.Instance?.ServerRcPickup(this, slot);

        [Rpc(SendTo.Server)]
        public void RequestRcDeployRpc(byte slot, Vector3 pos, float yaw) => MatchManager.Instance?.ServerRcDeploy(this, slot, pos, yaw);

        [Rpc(SendTo.Server)]
        public void RequestRcResumeRpc(byte slot) => MatchManager.Instance?.ServerRcResume(this, slot);

        [Rpc(SendTo.Server)]
        public void RequestRcParkRpc(byte slot, Vector3 pos, float yaw) => MatchManager.Instance?.ServerRcPark(this, slot, pos, yaw);

        [Rpc(SendTo.Server)]
        public void RequestRcStompRpc(byte slot) => MatchManager.Instance?.ServerRcStomp(this, slot);

        /// <summary>Seeker stomps on RC car <paramref name="slot"/> lying at <paramref name="carPos"/>.</summary>
        [Rpc(SendTo.Everyone)]
        public void StompRpc(byte slot, Vector3 carPos)
        {
            (Actions as BlockyCharacterActions)?.PlayStomp(carPos);
            GameAudio.PlayAttached(GameSfx.KnifeSwing, transform, 0.45f);
            if (IsOwner) StartCoroutine(StompView(carPos));
        }

        private IEnumerator StompView(Vector3 carPos)
        {
            float t = 0f;
            bool kicked = false;
            while (t < RcTimeline.Stomp)
            {
                t += Time.deltaTime;
                Vector3 d = carPos - EyePosition;
                float want = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -80f, -30f);
                if (t < 0.75f) Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, want, Time.deltaTime * 160f));
                if (!kicked && t >= RcTimeline.StompHit)
                {
                    kicked = true;
                    GameHud.Kick();
                }
                yield return null;
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestSecurityFloorRpc(byte floor) => MatchManager.Instance?.ServerSecurityFloor(this, floor);

        /// <summary>Asks the server to push / pull / cut the crate of a security room (<see cref="CrateAction"/>).</summary>
        [Rpc(SendTo.Server)]
        public void RequestCrateActionRpc(byte level, byte action) => MatchManager.Instance?.ServerCrateAction(this, level, action);

        /// <summary>Old name kept for tests: push the crate aside from outside.</summary>
        public void RequestPushCrateRpc(byte level) => RequestCrateActionRpc(level, (byte)CrateAction.OpenOutside);

        /// <summary>Everybody: this player pushes / drags the crate (the owner walks along) or cuts it with the chainsaw.</summary>
        [Rpc(SendTo.Everyone)]
        public void CrateActionRpc(byte level, byte action)
        {
            var act = (CrateAction)action;
            SecurityRooms.Instance?.BeginCrateAction(level, act);
            var bca = Actions as BlockyCharacterActions;
            if (act == CrateAction.Saw) bca?.PlayCrateSaw();
            else bca?.PlayPush(SecurityRooms.CratePushTime, SecurityRooms.ModeOf(act), SecurityRooms.CrateShift);
            if (IsOwner) StartCoroutine(CrateWalk(level, act));
        }

        private IEnumerator CrateWalk(byte level, CrateAction act)
        {
            bool walks = SecurityRooms.WalksAlong(act, level, out var a, out var b);
            SecurityRooms.ActionStand(level, act, out float yaw);
            float t0 = Time.time, total = act == CrateAction.Saw ? BlockyCharacterActions.CrateSawTime : SecurityRooms.CratePushTime;
            float lastSlide = 0f;
            while (Time.time - t0 < total)
            {
                float t = Time.time - t0;
                if (walks && Motor != null) Motor.Teleport(Vector3.Lerp(a, b, BlockyCharacterActions.CrateProgress(t)) + Vector3.up * 0.02f, yaw, Motor.LookPitch);
                if (act == CrateAction.Saw && Motor != null) Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, CrateSawViewPitch(t), Time.deltaTime * 120f));
                // Camera jolt at the start of every shove, rattle while the saw bites.
                float slide = act == CrateAction.Saw ? 0f : BlockyCharacterActions.CrateSlide(t);
                if (slide > 0.3f && lastSlide <= 0.3f) GameHud.Kick();
                lastSlide = slide;
                if (act == CrateAction.Saw && t > BlockyCharacterActions.CrateSawBiteStart && t < BlockyCharacterActions.CrateSawBreakTime && Time.frameCount % 9 == 0) GameHud.Kick();
                if (act == CrateAction.Saw && Mathf.Abs(t - BlockyCharacterActions.CrateSawBreakTime) < Time.deltaTime) GameHud.Kick();
                yield return null;
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestPickupChainsawRpc() => MatchManager.Instance?.ServerPickupChainsaw(this);

        [Rpc(SendTo.Server)]
        public void RequestEquipRpc(HeldItem item) => MatchManager.Instance?.ServerEquip(this, item);

        // ================================================================ effects on every client

        [Rpc(SendTo.Everyone)]
        public void KnifeSequenceRpc(ulong victimId, byte move, bool showAll)
        {
            var victim = Find(victimId);
            StartCoroutine(KnifeSequence(victim, (KillMove)move, showAll));
        }

        private float _holdAnimT, _lastHoldAnimT, _bandageUntil;
        private bool _wasHurt;
        private HoldAnim _lastHoldAnim;

        /// <summary>
        /// Injection / self-heal animations on every client. The owner uses its exact hold timer; others follow the
        /// synced progress. Sounds fire when the animation clock crosses their marks.
        /// </summary>
        private void UpdateHoldAnimation()
        {
            var bca = Actions as BlockyCharacterActions;
            var mode = (HoldAnim)HoldAnimVar.Value;
            float dur = mode == HoldAnim.Inject ? HealTimeline.InjectDuration : mode == HoldAnim.SelfHeal ? HealTimeline.SelfHealDuration
                : HealTimeline.IsMedkit(mode) ? HealTimeline.MedkitDuration : 0f;
            float target;
            var lpc = IsOwner ? LocalPlayerController.Instance : null;
            if (lpc != null && mode != HoldAnim.None && lpc.HoldKind != PromptKind.None) target = lpc.HoldProgress * dur;
            else target = ReviveProgress.Value / 255f * dur;
            if (mode != _lastHoldAnim) { _holdAnimT = 0f; _lastHoldAnimT = 0f; }
            _holdAnimT = IsOwner ? target : Mathf.MoveTowards(_holdAnimT, target, Time.deltaTime * 1.5f + Mathf.Abs(target - _holdAnimT) * 0.2f);
            Vector3? chest = null;
            NetPlayer patient = mode == HoldAnim.HealOther ? Find(ReviveTarget.Value) : null;
            if (mode == HoldAnim.Inject)
            {
                var victim = Find(ReviveTarget.Value);
                if (victim != null) chest = victim.ChestPosition;
            }
            if (patient != null && patient != this)
            {
                chest = patient.ChestPosition;
                (patient.Actions as BlockyCharacterActions)?.SetPatient(_holdAnimT);
            }
            bca?.SetHoldAnimation(mode, _holdAnimT, chest);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetHoldAnimation(mode, _holdAnimT, chest);
            // A fresh bandage stays on the belly for a while after the wound was treated (medkit).
            bool hurt = (Wounded.Value || Limping.Value) && IsHider && Life.Value == LifeState.Alive;
            if (_wasHurt && !hurt && Life.Value == LifeState.Alive) _bandageUntil = Time.time + 90f;
            if (Life.Value != LifeState.Alive) _bandageUntil = 0f;
            _wasHurt = hurt;
            bca?.SetBandaged((Wounded.Value && IsHider) || Time.time < _bandageUntil ? 1f : 0f);
            bool hop = IsHider && Life.Value == LifeState.Alive && (Severed.Value & Limb.Legs) != 0;
            bool limp = !hop && (Limping.Value || Wounded.Value) && IsHider && Life.Value == LifeState.Alive;
            bca?.SetLimp(limp ? 1f : 0f);
            bca?.SetHop(hop ? ((Severed.Value & Limb.LegR) != 0 ? 0 : 1) : -1);
            if (IsOwner && Motor != null)
                Motor.SpeedScale = (Time.time < _dustFreezeUntil ? 0f : hop ? Tuning.HopSpeedScale : limp ? 0.8f : 1f) * CombatSpeedScale * GooSpeedScale * FoamSpeedScale * CarrySpeedScale;
            if (IsOwner)
            {
                if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
                if (_bob != null) _bob.Limp = limp || hop ? 1f : 0f;
            }
            if (IsOwner && Motor != null)
            {
                Motor.ForceCrouch = mode == HoldAnim.Inject || HealTimeline.IsMedkit(mode);
                Motor.DownedSitting = mode == HoldAnim.SelfHeal;
                // Guide the view: down to the own belly while bandaging, onto the chest while injecting.
                float wantPitch = float.NaN;
                if (mode == HoldAnim.SelfHeal) wantPitch = -58f;
                else if (mode == HoldAnim.HealSelf) wantPitch = -55f;
                else if (mode == HoldAnim.HealOther && chest.HasValue)
                {
                    // Turn to the treated hider and look at the wound.
                    Vector3 d = chest.Value + Vector3.down * 0.3f - Motor.transform.position - Vector3.up * Motor.EyeHeight;
                    float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    Motor.SetLook(Mathf.MoveTowardsAngle(Motor.LookYaw, yaw, Time.deltaTime * 240f), Motor.LookPitch);
                    wantPitch = Mathf.Clamp(-Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -45f, 10f);
                }
                else if (mode == HoldAnim.Inject && chest.HasValue)
                {
                    Vector3 d = chest.Value - Motor.transform.position - Vector3.up * Motor.EyeHeight;
                    wantPitch = Mathf.Clamp(-Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -70f, -20f);
                }
                if (!float.IsNaN(wantPitch))
                    Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, wantPitch, Time.deltaTime * 90f));
            }

            // Sounds on the animation marks.
            float a = _lastHoldAnimT, b = _holdAnimT;
            if (mode == HoldAnim.Inject && a < 1.05f && b >= 1.05f)
            {
                var victim = Find(ReviveTarget.Value);
                GameAudio.PlayAt(GameSfx.InjectStab, victim != null ? victim.ChestPosition : transform.position, 1f);
            }
            if (mode == HoldAnim.Inject && a < HealTimeline.InjectStab && b >= HealTimeline.InjectStab)
                Find(ReviveTarget.Value)?.Actions?.PlayHitReaction(); // the body jolts when the needle goes in
            if (mode == HoldAnim.SelfHeal && a < HealTimeline.HealSitEnd && b >= HealTimeline.HealSitEnd)
                GameAudio.PlayAttached(GameSfx.BandageWrap, transform, 1f);
            if (HealTimeline.IsMedkit(mode) && a < HealTimeline.MedKitOpen && b >= HealTimeline.MedKitOpen)
                GameAudio.PlayAttached(GameSfx.MedkitPickup, transform, 0.6f);
            if (HealTimeline.IsMedkit(mode) && a < HealTimeline.MedPad && b >= HealTimeline.MedPad)
            {
                var who = mode == HoldAnim.HealOther && patient != null ? patient : this;
                GameAudio.PlayAttached(GameSfx.BandageWrap, who.transform, 1f);
                GameAudio.PlayAttached(GameSfx.HiderPain, who.transform, 0.45f);
            }
            _lastHoldAnimT = b;
            _lastHoldAnim = mode;
        }

        /// <summary>
        /// Chainsaw engine: a quiet low idle loop while held, the loud throttle loop only while it is used (F / a cut),
        /// dead and silent with an empty tank (until refuelled and pulled again) or while stalled; drives the animations.
        /// </summary>
        private float _sawBlood, _sawSlashUntil = -99f;
        private bool _wasSawOut, _sawEngineWasOn;
        /// <summary>Idle loop level (owner / other players) and pitch: low revs, well under the throttle (2026-11-26).</summary>
        private const float SawIdleOwnerVol = 0.32f, SawIdleOtherVol = 0.5f, SawIdlePitch = 0.92f;

        /// <summary>Called when the saw cut somebody: the chain is soaked, it wipes clean over ~40 s of running.</summary>
        public void BloodySaw() => _sawBlood = 1f;

        private void UpdateChainsaw()
        {
            bool sawOut = Held.Value == HeldItem.Chainsaw && Life.Value == LifeState.Alive;
            bool killing = Busy.Value && sawOut && IsSeeker && !Stunned;
            // Stalled (blinded / foamed / knocked out, NetPlayer.SawNerf.cs): the engine dies, a new pull-start after.
            // An empty tank (synced fuel, MatchManager.SawFuel.cs) kills the engine the same way until it is refuelled.
            bool stalled = sawOut && (SawStalled || SawDry) && !killing;
            // The engine runs once the synced pull-start has caught (NetPlayer.SawStart.cs; tries rolled by the server).
            bool fired = killing || (sawOut && IsSeeker && !stalled && SawStartElapsed >= BlockyProps.ChainsawFireAt(SawAttempts.Value));
            bool rev = sawOut && fired && (SawRev.Value || killing || Time.time < _sawSlashUntil);
            UpdateSawStartView(sawOut, stalled, killing);
            // The running engine died in the hands (tank ran dry / blinded / knocked out): it sputters out.
            if (_sawEngineWasOn && !fired && sawOut && stalled) PlaySawStall();
            _sawEngineWasOn = fired;
            _wasSawOut = sawOut;
            bool idle = sawOut && !rev && fired;
            if (sawOut) _sawBlood = Mathf.Max(0f, _sawBlood - Time.deltaTime * (rev ? 0.06f : 0.012f));
            (Actions as BlockyCharacterActions)?.SetSawBlood(_sawBlood);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetSawBlood(_sawBlood);
            if (idle && _sawIdle == null)
            {
                _sawIdle = GameAudio.StartLoop(GameSfx.ChainsawIdle, transform, IsOwner ? SawIdleOwnerVol : SawIdleOtherVol);
                if (_sawIdle != null) _sawIdle.pitch = SawIdlePitch;
            }
            else if (!idle && _sawIdle != null) { GameAudio.StopLoop(_sawIdle, 0.15f); _sawIdle = null; }
            if (rev && _sawRev == null) _sawRev = GameAudio.StartLoop(GameSfx.ChainsawRev, transform, IsOwner ? 0.75f : 1f);
            else if (!rev && _sawRev != null) { GameAudio.StopLoop(_sawRev, 0.2f); _sawRev = null; }
            (Actions as BlockyCharacterActions)?.SetRevving(rev);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetRevving(rev);
        }


        /// <summary>Cuts a limb off (local visual: the part flies away, blood) on this client.</summary>
        public void SeverLocal(int limb, Vector3 awayFrom)
        {
            if (Character == null || !Character.IsBuilt) return;
            Vector3 pos = Character.LimbJointPosition(limb);
            Vector3 dir = pos - awayFrom;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            // The part drops off next to the body (a gentle push away from the saw, a little up).
            Vector3 impulse = dir * Random.Range(0.6f, 1.6f) + side * Random.Range(-0.7f, 0.7f) + Vector3.up * Random.Range(0.7f, 1.6f);
            if (!Character.Sever(limb, impulse)) return;
            var gib = Character.LastGib;
            if (gib != null && SeveredLegs.ServerOwned(this, limb)) { Destroy(gib); gib = null; } // the server's leg item takes its place
            if (gib != null)
            {
                BloodFx.Stump(gib.transform, gib.transform.position, gib.transform.rotation,
                    limb == Limb.Head ? new Vector3(0.2f, 0.05f, 0.2f) : new Vector3(0.13f, 0.05f, 0.13f));
                BloodFx.GibBleed(gib.transform, Random.Range(5f, 9f));
            }
            AddStumps(limb, true);
            BloodFx.Spray(pos, (Vector3.up + dir * 0.4f).normalized, 1.6f);
            BloodFx.Spray(pos, (side + Vector3.up * 0.2f).normalized, 1.2f);
            BloodFx.Pool(transform.position + dir * 0.3f, Random.Range(0.7f, 1.1f), Random.Range(6f, 10f));
            GameAudio.PlayAt(GameSfx.BodyFall, pos, 0.5f);
        }

        /// <summary>Continuous blood while the chainsaw works (between <paramref name="start"/> and <paramref name="end"/> seconds).</summary>
        private IEnumerator ChainsawGore(NetPlayer victim, float start, float end)
        {
            yield return new WaitForSeconds(start);
            float t = start;
            float nextDecal = 0f;
            while (t < end && victim != null)
            {
                Vector3 c = victim.ChestPosition + Vector3.down * 0.15f;
                Vector3 toward = (victim.transform.position - transform.position).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, toward);
                BloodFx.Spray(c, (toward + side * Random.Range(-1.2f, 1.2f) + Vector3.up * Random.Range(0.1f, 0.9f)).normalized, 1.5f);
                if (t >= nextDecal)
                {
                    nextDecal = t + 0.35f;
                    BloodFx.Pool(victim.transform.position + Random.insideUnitSphere * 0.7f, Random.Range(0.4f, 0.8f), Random.Range(4f, 8f));
                    BloodFx.WallSplash(c, toward, 3f, Random.Range(0.4f, 0.8f));
                }
                if (IsOwner || victim.IsOwner) GameHud.Kick();
                yield return new WaitForSeconds(0.14f);
                t += 0.14f;
            }
        }

        /// <summary>Local player only: show the own body in third person (kill camera) or go back to first person.</summary>
        public void SetCinematicBody(bool thirdPerson)
        {
            if (!IsOwner || Character == null) return;
            _cinematicBody = thirdPerson;
            ApplyOwnBodyMode();
        }

        private bool _cinematicBody;

        /// <summary>
        /// Own body render mode: first person = shadows only; dead / cinematic camera = everything; seated in the
        /// lobby (<see cref="SeatBodyShown"/>) = the body is drawn, only the head (camera inside it) stays hidden.
        /// </summary>
        private void ApplyOwnBodyMode()
        {
            if (!IsOwner || Character == null) return;
            bool showAll = _cinematicBody || Life.Value == LifeState.Dead;
            bool fp = !showAll && !OwnBodyShown;
            Character.SetFirstPersonMode(fp);
            Character.SetHeadHidden(!showAll && OwnBodyShown);
            _shoulderLamp?.SetFirstPerson(fp);
        }

        [Rpc(SendTo.Everyone)]
        public void ShotFxRpc(Vector3 hitPoint, ulong victimId, sbyte rcSlot = -1, Vector3 shotDir = default)
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
            // Own camera kick (up and back again) is a spring in NetPlayer.GlockAim, no permanent climb.

            var victim = victimId == ulong.MaxValue ? null : Find(victimId);
            if (victim != null)
            {
                victim.Actions?.PlayHitReaction();
                victim.OnBulletHit(hitPoint, shotDir.sqrMagnitude > 0.01f ? shotDir : (hitPoint - from).normalized);
                GameAudio.PlayAttached(GameSfx.HiderPain, victim.transform);
                BloodFx.Spray(hitPoint, (hitPoint - from).normalized);
                if (victim.IsOwner) GameHud.Hurt(1f);
            }
            else if (rcSlot >= 0)
            {
                // The car itself bursts when the server switches it to the Shot state; here just the hit sparks.
                GameAudio.PlayAt(GameSfx.BulletImpact, hitPoint, 1f);
                RcCar.Peek(rcSlot)?.BulletHit(hitPoint, (hitPoint - from).normalized);
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

        /// <summary>First knife hit: one quick slash, the victim flinches and starts limping.</summary>
        [Rpc(SendTo.Everyone)]
        public void SlashRpc(ulong victimId)
        {
            var victim = Find(victimId);
            (Actions as BlockyCharacterActions)?.PlaySlash(victim != null ? victim.Character : null);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.PlaySlash();
            GameAudio.PlayAttached(GameSfx.KnifeSwing, transform, 0.8f);
            if (victim != null) StartCoroutine(SlashHit(victim));
        }

        private IEnumerator SlashHit(NetPlayer victim)
        {
            yield return new WaitForSeconds(BlockyCharacterActions.SlashHitTime);
            if (victim == null) yield break;
            victim.Actions?.PlayHitReaction();
            GameAudio.PlayAt(GameSfx.KnifeStab, victim.ChestPosition, 1f);
            GameAudio.PlayAt(GameSfx.HiderPain, victim.ChestPosition, 1f);
            if (victim.IsOwner) GameHud.Hurt(0.6f);
        }

        // ================================================================ chainsaw first hit, dust

        private void ApplySevered(byte v, bool animate)
        {
            if (Character == null || !Character.IsBuilt) return;
            if (v == 0) { ClearStumps(); Character.SetSevered(0); return; }
            // A cut the local kill sequence has not shown yet (late message, joined mid-kill): the part still falls off.
            int fresh = v & ~Character.SeveredMask;
            if (animate && fresh != 0)
            {
                Vector3 from = transform.position - transform.forward;
                float near = float.MaxValue; // the nearest seeker cut it (there can be several)
                foreach (var s in All)
                {
                    if (s == null || !s.IsSeeker) continue;
                    float d = (s.transform.position - transform.position).sqrMagnitude;
                    if (d < near) { near = d; from = s.transform.position; }
                }
                for (int bit = 1; bit <= Limb.Head; bit <<= 1)
                    if ((fresh & bit) != 0) SeverLocal(bit, from);
            }
            else if (fresh != 0) AddStumps(fresh, false);
            Character.SetSevered(Character.SeveredMask | v);
        }

        private readonly List<GameObject> _stumps = new();

        private void ClearStumps()
        {
            foreach (var g in _stumps) if (g != null) Destroy(g);
            _stumps.Clear();
        }

        /// <summary>Raw flesh caps on the body where limbs are missing (+ spurting blood when just cut).</summary>
        private void AddStumps(int mask, bool fresh)
        {
            if (Character == null) return;
            for (int bit = 1; bit <= Limb.Head; bit <<= 1)
            {
                if ((mask & bit) == 0) continue;
                // Flat cap on the pelvis / torso (not on the hidden limb's joint, which keeps animating).
                var cap = LimbStumps.Create(Character, bit, BloodFx.StumpMaterial);
                if (cap == null) continue;
                _stumps.Add(cap);
                if (!fresh) continue;
                var anchor = new GameObject("StumpSpurt").transform;
                anchor.SetParent(cap.transform.parent, false);
                anchor.localPosition = cap.transform.localPosition;
                _stumps.Add(anchor.gameObject);
                BloodFx.Spurt(anchor, LimbStumps.SpurtDirection(Character, bit), bit == Limb.Head ? 6f : Random.Range(4.5f, 7f));
            }
        }

        /// <summary>First chainsaw hit: a low sweep, the leg flies off, the victim starts hopping.</summary>
        [Rpc(SendTo.Everyone)]
        public void SawSlashRpc(ulong victimId, byte limb)
        {
            var victim = Find(victimId);
            (Actions as BlockyCharacterActions)?.PlaySawSlash(victim != null ? victim.Character : null, limb);
            _sawSlashUntil = Time.time + 1.1f; // the running saw revs up for the swing (no pull-start sound: it is already running)
            StartCoroutine(SawSlashHit(victim, limb));
        }

        private IEnumerator SawSlashHit(NetPlayer victim, int limb)
        {
            yield return new WaitForSeconds(BlockyCharacterActions.SawSlashHitTime);
            if (victim == null) yield break;
            GameAudio.PlayAt(GameSfx.ChainsawCut, victim.ChestPosition + Vector3.down * 0.5f, 1f);
            GameAudio.PlayAttached(GameSfx.HiderScream, victim.transform, 1f);
            BloodySaw();
            victim.SeverLocal(limb, transform.position);
            victim.Actions?.PlayHitReaction();
            if (victim.IsOwner) GameHud.Hurt(1f);
            if (IsOwner || victim.IsOwner) GameHud.Kick();
        }

        [Rpc(SendTo.Server)]
        public void RequestDustRpc() => MatchManager.Instance?.ServerDust(this);

        private float _dustFreezeUntil;

        /// <summary>Hider throws dust (runs on every client); <paramref name="seekerId"/> is the seeker it hits or MaxValue.</summary>
        [Rpc(SendTo.Everyone)]
        public void DustRpc(ulong seekerId)
        {
            (Actions as BlockyCharacterActions)?.PlayDust();
            if (IsOwner) _dustFreezeUntil = Time.time + BlockyCharacterActions.DustDuration;
            StartCoroutine(DustThrow(Find(seekerId)));
        }

        private IEnumerator DustThrow(NetPlayer seeker)
        {
            yield return new WaitForSeconds(0.32f);
            GameAudio.PlayAttached(GameSfx.BandageWrap, transform, 0.5f);
            DustFx.Puff(transform.position + transform.forward * 0.45f + transform.right * 0.3f + Vector3.up * 0.05f, Vector3.up, 14, 0.8f, 0.07f);
            yield return new WaitForSeconds(BlockyCharacterActions.DustReleaseTime - 0.32f);
            GameAudio.PlayAttached(GameSfx.KnifeSwing, transform, 0.45f);
            var bca = Actions as BlockyCharacterActions;
            Vector3 hand = bca != null ? bca.DustHandWorld : transform.position + Vector3.up;
            Vector3 dir = transform.forward + Vector3.up * 0.15f;
            if (seeker != null) dir = (seeker.EyePosition - hand).normalized;
            DustFx.Puff(hand, dir, 70, 4.2f, 0.12f);
            if (seeker == null) yield break;
            yield return new WaitForSeconds(0.12f);
            DustFx.Puff(seeker.EyePosition, -dir, 40, 1.4f, 0.14f);
            GameAudio.PlayAttached(GameSfx.HiderPain, seeker.transform, 0.6f);
        }

        [Rpc(SendTo.Everyone)]
        public void SfxRpc(GameSfx sfx, float volume) => GameAudio.PlayAttached(sfx, transform, volume);
    }
}


