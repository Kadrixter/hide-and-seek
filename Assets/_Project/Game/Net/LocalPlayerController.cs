using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    public enum PromptKind
    {
        None,
        Knife,      // F on a hider
        Rescue,     // H hold on a downed hider
        Heal,       // H hold on a wounded hider (needs medkit)
        Pickup,     // E on a medkit
        Button,     // E on the lobby start button
        Settings,   // E on the lobby settings board
        Seat,       // E on a sofa / pouf
        ElevatorCall,  // E on the call button next to elevator doors
        ElevatorPanel, // E on a level button inside the cabin
        ChainsawPickup, // E on the shelf in the generator room (seeker)
        SelfRevive,     // G hold while downed with a medkit
        PickupSyringe,  // E on an adrenaline syringe
        Resurrect,      // H hold on a dead hider with adrenaline
        ForkPickup,     // E on the fork on the lobby coffee table
        ForkOutlet,     // E on a wall socket while holding the fork
        Arcade,         // E on the SNAKE arcade
        RcPickup,       // E on an RC car (shelf / parked on the floor)
        RcStomp,        // F on an RC car (seeker)
        Bricks,         // E on the toy brick tub in the lobby
        ExitDoor,       // E on the lobby exit door
        Radio,          // E on the boombox
        SecurityButton, // E on a camera-level button in a security room
        SecurityCrate,  // E on the crate blocking the security room door
        Vending,        // E on the lobby vending machine keypad
    }

    /// <summary>
    /// Local player's gameplay input: interactions (knife, rescue, heal, pickup, lobby button),
    /// Glock (shoot, reload, holster), flashlight focus, input locks (blind seeker, busy, dead, menus).
    /// </summary>
    public class LocalPlayerController : MonoBehaviour
    {
        public static LocalPlayerController Instance { get; private set; }

        private NetPlayer _p;
        private float _holdTimer;
        private MatchPhase _lastPhase;
        private PromptKind _holdKind;
        private NetPlayer _holdTarget;
        private float _focus;
        private float _nextShot;
        private float _nextSawTry, _nextDustTry;
        private int _secFloor;

        private HeldItem NextWeapon(HeldItem cur)
        {
            HeldItem[] order = { HeldItem.Knife, HeldItem.Chainsaw, HeldItem.Glock };
            int i = System.Array.IndexOf(order, cur);
            for (int k = 1; k <= order.Length; k++)
            {
                var next = order[(i + k + order.Length) % order.Length];
                if (next == HeldItem.Chainsaw && !_p.HasChainsaw.Value) continue;
                if (next == HeldItem.Glock && !_p.HasGlock.Value) continue;
                return next;
            }
            return HeldItem.Knife;
        }

        /// <summary>Alive hider the running chainsaw touches (close, in front).</summary>
        /// <summary>Seconds until the seeker's next melee attack is allowed (8 s cooldown).</summary>
        private double AttackWait()
        {
            var mm = MatchManager.Instance;
            return mm != null ? _p.AttackReadyAt.Value - mm.Now : 0d;
        }

        private NetPlayer SawContact()
        {
            Vector3 me = transform.position;
            Vector3 fwd = transform.forward;
            NetPlayer best = null;
            float bestD = Tuning.ChainsawContact;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == _p || !o.IsAliveHider || o.Busy.Value) continue;
                Vector3 d = o.transform.position - me;
                if (Mathf.Abs(d.y) > 1.2f) continue;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > bestD) continue;
                if (dist > 0.3f && Vector3.Angle(fwd, d) > 75f) continue;
                best = o;
                bestD = dist;
            }
            return best;
        }
        private bool _placedInLobby;
        private Vector3 _lastBloodPos;

        // Prompt for the HUD
        public PromptKind Prompt { get; private set; }
        public Vector3 PromptWorld { get; private set; }
        public string PromptText { get; private set; }
        public PromptKind HoldKind => _holdKind;
        public NetPlayer HoldTarget => _holdTarget;
        public float HoldDurationNow => _holdKind != PromptKind.None ? HoldDuration(_holdKind) : 0f;
        public float HoldProgress => _holdKind != PromptKind.None ? _holdTimer / HoldDuration(_holdKind) : 0f;

        /// <summary>True while a menu (Esc, settings board) owns the mouse.</summary>
        public static bool MenuOpen { get; set; }

        /// <summary>Downed for longer than Tuning.CrawlTime: cannot crawl any more.</summary>
        public bool CrawlExhausted { get; private set; }

        private void Awake()
        {
            Instance = this;
            _p = GetComponent<NetPlayer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static float HoldDuration(PromptKind k) => k == PromptKind.Rescue ? Tuning.RescueHoldTime
            : k == PromptKind.SelfRevive ? Tuning.SelfReviveTime
            : k == PromptKind.Resurrect ? Tuning.ResurrectTime
            : Tuning.HealHoldTime;

        private void Update()
        {
            if (_p == null || !_p.IsSpawned) return;
            var mm = MatchManager.Instance;
            var phase = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (!_placedInLobby && SceneManager.GetActiveScene().name == NetSession.LobbyScene && phase == MatchPhase.Lobby)
            {
                _placedInLobby = true;
                var spawns = SpawnPoints.Lobby();
                if (spawns.Count > 0)
                {
                    var sp = spawns[Random.Range(0, spawns.Count)];
                    _p.TeleportLocal(sp.position, sp.eulerAngles.y);
                }
            }
            if (SceneManager.GetActiveScene().name != NetSession.LobbyScene) _placedInLobby = false;

            if (TestButton && !_testPlaced && LobbyButton.Instance != null && phase == MatchPhase.Lobby && Time.timeSinceLevelLoad > 1.5f)
            {
                _testPlaced = true;
                Vector3 aim = LobbyButton.Instance.AimPoint;
                Vector3 from = aim + new Vector3(-1.4f, 0f, 0f);
                from.y = 0.02f;
                Vector3 dir = aim - (from + Vector3.up * 1.62f);
                _p.TeleportLocal(from, Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z)).eulerAngles.y);
                _p.Motor.SetLook(_p.Motor.LookYaw, Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg);
            }
            if (TestMirror && !_testPlaced && phase == MatchPhase.Lobby && Time.timeSinceLevelLoad > 1.5f)
            {
                var mirror = FindFirstObjectByType<PlanarMirror>();
                if (mirror != null)
                {
                    _testPlaced = true;
                    Vector3 mp = mirror.transform.position;
                    _p.TeleportLocal(new Vector3(mp.x + 0.4f, 0.02f, mp.z + 2.3f), 180f);
                    _p.Motor.SetLook(200f, -5f);
                }
            }
            if (TestReady && !_testReadyDone && phase == MatchPhase.Lobby && Time.timeSinceLevelLoad > 6f)
            {
                _testReadyDone = true;
                _p.SetReadyRpc(true);
            }
            if (TestPull && phase == MatchPhase.Lobby && LobbyButton.Instance != null &&
                ((!_pull1 && Time.timeSinceLevelLoad > 4f) || (!_pull2 && Time.timeSinceLevelLoad > 9f)))
            {
                if (!_pull1) _pull1 = true; else _pull2 = true;
                Debug.Log("[Test] pulling lever");
                LobbyButton.Instance.Press();
            }
            if (TestElevator && !_elevTestStarted && (phase == MatchPhase.Hiding || phase == MatchPhase.Seeking) &&
                ElevatorSystem.Instance != null && ElevatorSystem.Instance.Cars.Count > 0 && MatchManager.Instance.Elevators.Count > 0)
            {
                _elevTestStarted = true;
                StartCoroutine(ElevatorTest());
            }
            if (TestSit && !_testPlaced && phase == MatchPhase.Lobby && Time.timeSinceLevelLoad > 2f && LobbySeat.All.Count > 0)
            {
                _testPlaced = true;
                PromptWorld = LobbySeat.All[0].AimPoint;
                SitNearest();
            }
            if (TestBoardDist > 0f && !_testPlaced && LobbySettingsBoard.Instance != null && phase == MatchPhase.Lobby && Time.timeSinceLevelLoad > 1.5f)
            {
                _testPlaced = true;
                Vector3 bp = LobbySettingsBoard.Instance.transform.position;
                Vector3 from = new Vector3(bp.x + TestBoardSide, 0.02f, bp.z - TestBoardDist);
                _p.TeleportLocal(from, 0f);
                _p.Motor.SetLook(0f, Mathf.Atan2(bp.y - 1.64f, TestBoardDist) * Mathf.Rad2Deg);
            }
            if ((DebugPos || TestButton) && Time.time >= _nextDebug)
            {
                _nextDebug = Time.time + 1f;
                string extra = "";
                if (LobbyButton.Instance != null)
                {
                    Vector3 e = _p.EyePosition, t = LobbyButton.Instance.AimPoint, to = t - e;
                    Vector3 f = _p.Camera != null ? _p.Camera.transform.forward : transform.forward;
                    string hitName = Physics.Linecast(e, t - to.normalized * 0.3f, out var h, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore)
                        ? h.collider.name + "/" + h.collider.gameObject.layer : "none";
                    extra = $" id={GetHashCode()} d={to.magnitude:0.00} ang={Vector3.Angle(f, to):0} hit={hitName} instances={FindObjectsByType<LocalPlayerController>(FindObjectsInactive.Include).Length}";
                }
                Debug.Log($"[Prompt] {Prompt} '{PromptText}'{extra}");
                Debug.Log($"[Pos] scene={SceneManager.GetActiveScene().name} phase={phase} life={_p.Life.Value} downed={_p.Motor.Downed} " +
                          $"eye={_p.Motor.EyeHeight:0.00} crouch={_p.Motor.CrouchAmount:0.00} pos={transform.position} cam={(_p.Camera != null ? _p.Camera.transform.position.ToString() : "-")}");
            }

            _ignoreEThisFrame = false;

            // ---- R = ready / not ready in the lobby (the room owner starts with the lever)
            var kbr = Keyboard.current;
            if (phase == MatchPhase.Lobby && kbr != null && !MenuOpen && kbr.rKey.wasPressedThisFrame && !NetworkManager.Singleton.IsServer)
            {
                _p.SetReadyRpc(!_p.Ready.Value);
                GameAudio.Play2D(GameSfx.UiClick, 0.8f);
            }

            // ---- input locks
            bool dead = _p.Life.Value == LifeState.Dead;
            bool blind = _p.IsSeeker && (phase == MatchPhase.Hiding || phase == MatchPhase.Loading);
            bool locked = MenuOpen || _p.Busy.Value || blind || dead || phase == MatchPhase.Loading || phase == MatchPhase.Race || KillCam.Active;
            if (_p.Life.Value != LifeState.Alive || phase != _lastPhase || _p.Seat.Value >= 0 || _p.Busy.Value) _p.InputReader.ResetStance();
            _lastPhase = phase;
            _p.InputReader.InputEnabled = !locked;
            _p.InputReader.ManageCursor = !MenuOpen;
            // Downed: crawling only for the first CrawlTime seconds, then too weak to move.
            bool exhausted = false;
            if (_p.Life.Value == LifeState.Downed && mm != null)
            {
                double downedFor = mm.Now - (_p.DownedUntil.Value - mm.BleedOut);
                exhausted = downedFor >= Tuning.CrawlTime;
            }
            CrawlExhausted = exhausted;
            // Seated: no walking; E, Space or any movement key stands up. Never seated outside the lobby.
            bool seated = _p.Seat.Value >= 0;
            if (seated)
            {
                var kbs = Keyboard.current;
                bool standKey = kbs != null && !MenuOpen && (kbs.eKey.wasPressedThisFrame || kbs.spaceKey.wasPressedThisFrame ||
                                kbs.wKey.wasPressedThisFrame || kbs.sKey.wasPressedThisFrame || kbs.aKey.wasPressedThisFrame || kbs.dKey.wasPressedThisFrame);
                if (standKey || phase != MatchPhase.Lobby || dead)
                {
                    StandUp();
                    _ignoreEThisFrame = true;
                }
            }
            // SNAKE arcade: WASD / arrows steer, E or Space leaves. No walking or looking around meanwhile.
            var mmA = MatchManager.Instance;
            bool arcade = mmA != null && mmA.ArcadePlayer.Value == _p.NetworkObjectId;
            if (arcade)
            {
                var kba = Keyboard.current;
                if (!_wasArcade) _p.Motor.SetLook(0f, -24f);
                if (kba != null && !MenuOpen)
                {
                    if (kba.wKey.wasPressedThisFrame || kba.upArrowKey.wasPressedThisFrame) _p.ArcadeDirRpc(0);
                    else if (kba.dKey.wasPressedThisFrame || kba.rightArrowKey.wasPressedThisFrame) _p.ArcadeDirRpc(1);
                    else if (kba.sKey.wasPressedThisFrame || kba.downArrowKey.wasPressedThisFrame) _p.ArcadeDirRpc(2);
                    else if (kba.aKey.wasPressedThisFrame || kba.leftArrowKey.wasPressedThisFrame) _p.ArcadeDirRpc(3);
                    if ((kba.eKey.wasPressedThisFrame || kba.spaceKey.wasPressedThisFrame) && !_ignoreEThisFrame)
                    {
                        _p.RequestArcadeRpc(false);
                        _ignoreEThisFrame = true;
                    }
                }
            }
            _wasArcade = arcade;
            bool rc = _p.RcAnimating;
            bool gasp = _p.Gasping;
            _p.Motor.MovementEnabled = !locked && !exhausted && _p.Seat.Value < 0 && !arcade && !rc && !gasp;
            _p.Motor.LookEnabled = !locked && !arcade && !rc;
            var cc = _p.GetComponent<CharacterController>();
            bool ccOn = !dead && _p.Seat.Value < 0;
            if (cc != null && cc.enabled != ccOn) cc.enabled = ccOn;
            if (_p.Flashlight != null)
            {
                bool glockOut = _p.Held.Value == HeldItem.Glock;
                _p.Flashlight.ListenToInput = !glockOut && !locked && _p.Life.Value != LifeState.Downed && !rc;
                if (dead && _p.Flashlight.IsOn) _p.Flashlight.SetOn(false, false);
            }

            // Blood trail while crawling.
            if (_p.Life.Value == LifeState.Downed && (transform.position - _lastBloodPos).sqrMagnitude > 0.7f)
            {
                _lastBloodPos = transform.position;
                BloodFx.Decal(transform.position, Random.Range(0.18f, 0.32f));
            }

            Prompt = PromptKind.None;
            if (locked || kb == null || mouse == null)
            {
                CancelHold();
                return;
            }

            if (ForceFlashlight && _p.Flashlight != null && !_p.Flashlight.IsOn && _p.Life.Value == LifeState.Alive) _p.Flashlight.SetOn(true, false);

            // ---- lobby toy bricks: while building the mouse / E / 1-5 belong to the bricks.
            if (phase != MatchPhase.Lobby && LobbyBricks.Building) LobbyBricks.StopBuilding();
            if (LobbyBricks.LocalTick(_p, kb, mouse, _ignoreEThisFrame)) { CancelHold(); return; }

            // ---- RC car: 6 puts it down / parks it / takes control again; WASD drives while the car camera is on.
            if (UpdateRc(kb, phase)) { CancelHold(); return; }

            // ---- T toggles the flashlight always (LMB is the gun while the Glock is out)
            if (kb.tKey.wasPressedThisFrame && _p.Flashlight != null && _p.Life.Value != LifeState.Downed) _p.Flashlight.Toggle();

            // ---- seeker: flashlight focus with the mouse wheel
            if (_p.IsSeeker && _p.Flashlight != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    float before = _focus;
                    _focus = Mathf.Clamp01(_focus + Mathf.Sign(wheel) * 0.125f);
                    if (!Mathf.Approximately(before, _focus))
                    {
                        _p.Flashlight.SetFocus(_focus);
                        _p.FlashFocus.Value = (byte)Mathf.RoundToInt(_focus * 255f);
                        GameAudio.Play2D(GameSfx.FlashlightAdjust, 0.5f);
                    }
                }
            }

            // ---- seeker: weapon switch (Q cycles knife -> chainsaw -> Glock)
            if (_p.IsSeeker && phase == MatchPhase.Seeking && (_p.HasGlock.Value || _p.HasChainsaw.Value) && kb.qKey.wasPressedThisFrame && _p.InputReader.InputEnabled)
                _p.RequestEquipRpc(NextWeapon(_p.Held.Value));

            // ---- hider: X = throw dust into the seeker's eyes
            if (_p.IsHider && phase == MatchPhase.Seeking && _p.Life.Value == LifeState.Alive && !_p.Busy.Value &&
                _p.InputReader.InputEnabled && kb.xKey.wasPressedThisFrame && Time.time >= _nextDustTry)
            {
                _nextDustTry = Time.time + 0.6f;
                _p.RequestDustRpc();
            }

            // ---- seeker: chainsaw (hold F = full throttle, walk into a hider = kill)
            bool sawRev = _p.IsSeeker && phase == MatchPhase.Seeking && _p.Held.Value == HeldItem.Chainsaw &&
                          _p.Life.Value == LifeState.Alive && _p.InputReader.InputEnabled && kb.fKey.isPressed;
            if (_p.SawRev.Value != sawRev) _p.SawRev.Value = sawRev;
            if (sawRev && Time.time >= _nextSawTry)
            {
                var victim = SawContact();
                if (victim != null)
                {
                    _nextSawTry = Time.time + 0.5f;
                    _p.RequestChainsawRpc(victim.NetworkObjectId);
                }
            }

            // ---- seeker: glock
            if (_p.IsSeeker && _p.HasGlock.Value && phase == MatchPhase.Seeking)
            {
                if (_p.Held.Value == HeldItem.Glock)
                {
                    if (kb.rKey.wasPressedThisFrame) _p.RequestReloadRpc();
                    bool testShot = TestShoot;
                    TestShoot = false;
                    if (((mouse.leftButton.wasPressedThisFrame && _p.InputReader.CursorLocked) || testShot) && Time.time >= _nextShot)
                    {
                        _nextShot = Time.time + Tuning.FireInterval;
                        Shoot();
                    }
                }
            }

            FindPrompt(phase);
            HandlePromptInput(kb);
        }

        // ================================================================ RC car

        private bool _rcLights = true;

        // ---- test hooks (scripted tests drive the same code path as the keyboard)
        public static bool TestSix, TestHoldG, TestShoot, TestHoldH;
        public static Vector2 TestRcInput;
        public static bool TestRcBoost, TestRcHorn, TestRcActive;

        /// <summary>Key 6 + driving. Returns true while the RC car owns the input (no other prompts / actions).</summary>
        private bool UpdateRc(Keyboard kb, MatchPhase phase)
        {
            var mm = MatchManager.Instance;
            bool six = kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame || TestSix;
            TestSix = false;
            int slot = mm != null ? mm.RcSlotOf(_p) : -1;
            bool roundPhase = phase == MatchPhase.Hiding || phase == MatchPhase.Seeking;

            if (_p.RcControlling && slot >= 0)
            {
                var car = RcCar.Peek(slot);
                if (car != null)
                {
                    float throttle = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                    float steer = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                    if (TestRcActive) { throttle = TestRcInput.y; steer = TestRcInput.x; }
                    car.Throttle = throttle;
                    car.SteerInput = steer;
                    car.Boost = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed || TestRcBoost;
                    car.Horn = kb.hKey.isPressed || TestRcHorn;
                    if (kb.tKey.wasPressedThisFrame)
                    {
                        _rcLights = !_rcLights;
                        GameAudio.Play2D(GameSfx.FlashlightAdjust, 0.5f);
                    }
                    car.Lights = _rcLights;
                    _p.SetRcInput(new Vector2(steer, throttle));
                    car.TickLocal(Time.deltaTime, _p.Motor.WalkSpeed, _p.Motor.RunSpeed, transform.position, _p);
                    if (six)
                    {
                        car.Throttle = 0f;
                        car.Horn = false;
                        _p.RequestRcParkRpc((byte)slot, car.transform.position, car.Yaw);
                    }
                }
                return true;
            }
            if (_p.RcAnimating) return true; // sitting down / getting up

            if (!six || !roundPhase || mm == null) return false;
            if (!_p.IsAliveHider)
            {
                if (_p.IsSeeker) GameHud.Toast("Samochodziki są dla chowających się.", 2f);
                return false;
            }
            if (_p.Seat.Value >= 0 || _p.Motor.IsProne) return false;
            if (slot < 0)
            {
                GameHud.Toast("Nie masz samochodzika. Dwa leżą na półce w pomieszczeniu z generatorami (poziom -1).", 4f);
                return false;
            }
            var s = mm.RcSlot(slot);
            if (s.Mode == RcMode.Carried)
            {
                if (!_p.Motor.IsGrounded) return false;
                if (FindRcPlace(out var pos, out float yaw)) _p.RequestRcDeployRpc((byte)slot, pos, yaw);
                else GameHud.Toast("Za mało miejsca na podłodze przed tobą.", 2f);
            }
            else if (s.Mode == RcMode.Parked)
            {
                Vector3 d = s.Pos - transform.position;
                if (d.magnitude > Tuning.RcRange) GameHud.Toast($"Samochodzik jest poza zasięgiem pilota ({Tuning.RcRange:0} m).", 3f);
                else _p.RequestRcResumeRpc((byte)slot);
            }
            return false;
        }

        /// <summary>Free floor in front of the feet for the car (flat enough, no wall / furniture in the way).</summary>
        private bool FindRcPlace(out Vector3 pos, out float yaw)
        {
            yaw = transform.eulerAngles.y;
            Vector3 want = transform.TransformPoint(HideAndSeek.Characters.RcTimeline.PlaceLocal);
            pos = want;
            int mask = ~((1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9) | (1 << RcCar.CarLayer));
            if (!Physics.Raycast(want + Vector3.up * 0.5f, Vector3.down, out var hit, 0.9f, mask, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.8f || Mathf.Abs(hit.point.y - transform.position.y) > 0.3f) return false;
            pos = hit.point;
            // Nothing between the chest and the spot (own capsule ignored).
            Vector3 chest = transform.position + Vector3.up * 0.9f;
            Vector3 to = pos + Vector3.up * 0.12f - chest;
            if (LevelRaycast(chest, to.normalized, to.magnitude, null, out _)) return false;
            // Room for the car body (own capsule ignored: it reaches 0.3 m forward).
            var rot = Quaternion.Euler(0f, yaw, 0f);
            int n = Physics.OverlapBoxNonAlloc(pos + Vector3.up * 0.13f, new Vector3(0.12f, 0.08f, 0.24f), _overlap, rot, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!_overlap[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        private readonly Collider[] _overlap = new Collider[16];

        // ================================================================ prompts

        private void FindPrompt(MatchPhase phase)
        {
            Vector3 eye = _p.EyePosition;
            Vector3 fwd = _p.Camera != null ? _p.Camera.transform.forward : transform.forward;
            float best = float.MaxValue;

            void Offer(PromptKind kind, Vector3 world, string text, float dist)
            {
                if (dist >= best) return;
                best = dist;
                Prompt = kind;
                PromptWorld = world;
                PromptText = text;
            }

            if (phase == MatchPhase.Seeking)
            {
                foreach (var o in NetPlayer.All)
                {
                    if (o == null || o == _p) continue;
                    Vector3 chest = o.ChestPosition;
                    float d = Vector3.Distance(eye, chest);

                    if (_p.IsSeeker && _p.Held.Value == HeldItem.Chainsaw && o.IsAliveHider && !o.Busy.Value && d <= Tuning.ChainsawPromptRange && InView(eye, fwd, chest, 50f))
                    {
                        Offer(PromptKind.Knife, chest, AttackWait() > 0.05 ? $"Piła gotowa za {AttackWait():0.0} s" : "F  przytrzymaj i wejdź w niego", d);
                    }
                    else if (_p.IsSeeker && o.IsAliveHider && !o.Busy.Value && d <= Tuning.KnifePromptRange && InView(eye, fwd, chest, 50f))
                    {
                        if (AttackWait() > 0.05) Offer(PromptKind.Knife, chest, $"Atak za {AttackWait():0.0} s", d);
                        else if (d <= Tuning.KnifeRange) Offer(PromptKind.Knife, chest, "F", d);
                        else Offer(PromptKind.Knife, chest, "F (bliżej)", d + 10f);
                    }
                    else if (_p.IsAliveHider && o.IsHider && o.Life.Value == LifeState.Dead && _p.SyringeCount.Value > 0 &&
                             d <= Tuning.ResurrectRange + 0.8f && InView(eye, fwd, chest, 70f))
                    {
                        Offer(PromptKind.Resurrect, chest, "H  przytrzymaj - wskrześ (adrenalina)", d);
                    }
                    else if (_p.IsAliveHider && o.IsHider && d <= Tuning.RescueRange + 0.6f && InView(eye, fwd, chest, 70f))
                    {
                        if (o.Life.Value == LifeState.Downed)
                            Offer(PromptKind.Rescue, chest, "H  przytrzymaj - uratuj", d);
                        else if (o.Life.Value == LifeState.Alive && (o.Wounded.Value || o.Limping.Value) && _p.MedkitCount.Value > 0)
                            Offer(PromptKind.Heal, chest, "H  przytrzymaj - ulecz", d);
                    }
                }
            }

            // Elevators (game map).
            var es = ElevatorSystem.Instance;
            if (es != null && MatchManager.Instance != null && MatchManager.Instance.Elevators.Count > 0 && _p.Life.Value != LifeState.Dead)
            {
                foreach (var car in es.Cars)
                {
                    for (int fi = 0; fi < car.Floors.Count; fi++)
                    {
                        var fl = car.Floors[fi];
                        if (Mathf.Abs(eye.y - (fl.Y + 1.5f)) > 1.6f) continue;
                        for (int face = 0; face < 2; face++)
                        {
                            Vector3 bp = es.CallButtonPos(car, fl, face);
                            float d = Vector3.Distance(eye, bp);
                            if (d <= 2.2f && d < best && InView(eye, fwd, bp, 30f))
                            {
                                _elevCar = car.Index;
                                _elevFloor = fi;
                                Offer(PromptKind.ElevatorCall, bp, "E  przywołaj windę", d);
                            }
                        }
                        if (!es.InCabin(car, fi, transform.position)) continue;
                        for (int t = 0; t < car.Floors.Count; t++)
                        {
                            Vector3 bp = es.PanelButtonPos(car, fl, t);
                            Vector3 to = bp - eye;
                            float ang = Vector3.Angle(fwd, to);
                            float score = to.magnitude + ang * 0.03f;
                            if (to.magnitude <= 1.9f && ang < 14f && score < best)
                            {
                                _elevCar = car.Index;
                                _elevFloor = t;
                                Offer(PromptKind.ElevatorPanel, bp, t == fi ? $"E  poziom {car.Floors[t].Name} (otwórz)" : $"E  jedź na poziom {car.Floors[t].Name}", score);
                            }
                        }
                    }
                }
            }

            // Security room door: hiders push / pull the crate, the seeker cuts it with the chainsaw.
            if ((phase == MatchPhase.Hiding || phase == MatchPhase.Seeking) && SecurityRooms.Instance != null && _p.Life.Value == LifeState.Alive && !_p.Busy.Value &&
                SecurityRooms.Instance.TryFindCrateAction(transform.position, eye, fwd, _p.IsSeeker,
                    _p.IsSeeker && phase == MatchPhase.Seeking && _p.Held.Value == HeldItem.Chainsaw && _p.HasChainsaw.Value,
                    out var crateAct, out int crateLv, out var cratePos, out float crateD))
            {
                _crateLevel = crateLv;
                _crateAct = crateAct;
                Offer(PromptKind.SecurityCrate, cratePos, crateAct switch
                {
                    CrateAction.CloseOutside => "E  zasuń skrzynię przed drzwi",
                    CrateAction.OpenInside => "E  odepchnij skrzynię",
                    CrateAction.CloseInside => "E  zasuń skrzynię za sobą",
                    CrateAction.Saw => "F  rozpiłuj skrzynię",
                    _ => "E  przepchnij skrzynię",
                }, crateD);
            }

            // Security room: buttons choosing which level the cameras show.
            if (SecurityRooms.Instance != null && SecurityRooms.Instance.TryFindButton(eye, fwd, out var secPos, out int secFloor, out float secD))
            {
                _secFloor = secFloor;
                Offer(PromptKind.SecurityButton, secPos, secFloor == SecurityRooms.Instance.SelectedFloor
                    ? $"E  kamery: poziom {SecurityRooms.LevelName(secFloor)} (wybrane)" : $"E  kamery: poziom {SecurityRooms.LevelName(secFloor)}", secD);
            }

            // Chainsaw on the generator-room shelf (seeker only).
            if (phase == MatchPhase.Seeking && _p.IsSeeker && ChainsawShelf.Instance != null && MatchManager.Instance != null && !MatchManager.Instance.ChainsawTaken.Value)
            {
                Vector3 sp = ChainsawShelf.Instance.SawPoint;
                float d = Vector3.Distance(eye, sp);
                if (d <= 2.6f && InView(eye, fwd, sp, 60f)) Offer(PromptKind.ChainsawPickup, sp, "E  weź piłę łańcuchową", d);
            }

            // RC cars: hiders pick one up (shelf or floor), the seeker stomps on one lying on the floor.
            var mmR = MatchManager.Instance;
            if (mmR != null && (phase == MatchPhase.Seeking || phase == MatchPhase.Hiding))
            {
                bool hasCar = mmR.RcSlotOf(_p) >= 0;
                for (int i = 0; i < RcCar.Count; i++)
                {
                    var s = mmR.RcSlot(i);
                    var car = RcCar.Peek(i);
                    Vector3 cp = car != null && car.View != RcCar.ViewMode.Hidden ? car.transform.position : s.Mode == RcMode.Shelf ? RcCarShelf.CarPos(i) : s.Pos;
                    Vector3 aim = cp + Vector3.up * 0.1f;
                    float d = Vector3.Distance(eye, aim);
                    if (_p.IsAliveHider && (s.Mode == RcMode.Shelf || s.Mode == RcMode.Parked) && (!hasCar || s.Holder == _p.NetworkObjectId))
                    {
                        if (d <= Tuning.RcPickupRange && InView(eye, fwd, aim, 40f))
                        {
                            _rcSlotPrompt = i;
                            Offer(PromptKind.RcPickup, aim + Vector3.up * 0.1f, "E  weź samochodzik RC", d);
                        }
                    }
                    else if (_p.IsSeeker && phase == MatchPhase.Seeking && (s.Mode == RcMode.Driving || s.Mode == RcMode.Parked))
                    {
                        Vector3 flat = cp - transform.position;
                        flat.y = 0f;
                        if (flat.magnitude <= Tuning.RcStompPromptRange && Mathf.Abs(cp.y - transform.position.y) < 1f && InView(eye, fwd, aim, 55f))
                        {
                            _rcSlotPrompt = i;
                            Offer(PromptKind.RcStomp, aim + Vector3.up * 0.15f, "F  rozdepcz samochodzik", d - 0.4f);
                        }
                    }
                }
            }

            // Adrenaline syringes (hiders).
            if ((phase == MatchPhase.Seeking || phase == MatchPhase.Hiding) && _p.IsAliveHider && MatchManager.Instance != null)
            {
                var mmS = MatchManager.Instance;
                bool fullS = _p.SyringeCount.Value >= Tuning.MaxSyringes;
                for (int i = 0; i < mmS.Syringes.Count; i++)
                {
                    var m = mmS.Syringes[i];
                    if (m.Taken) continue;
                    Vector3 mp = m.Position + Vector3.up * 0.1f;
                    float d = Vector3.Distance(eye, mp);
                    if (d < best && d <= Tuning.PickupRange + 1f && InView(eye, fwd, mp, 55f))
                    {
                        _syringeId = m.Id;
                        Offer(PromptKind.PickupSyringe, mp, fullS ? "E  (masz już max strzykawek)" : "E  podnieś strzykawkę z adrenaliną", d);
                    }
                }
            }

            // Medkits can be picked up any time during the round (also when not hurt) and kept in the inventory.
            if (phase == MatchPhase.Seeking || phase == MatchPhase.Hiding)
            {
                var mm = MatchManager.Instance;
                if (_p.IsAliveHider && mm != null)
                {
                    bool full = _p.MedkitCount.Value >= Tuning.MaxMedkits;
                    for (int i = 0; i < mm.Medkits.Count; i++)
                    {
                        var m = mm.Medkits[i];
                        if (m.Taken) continue;
                        Vector3 mp = m.Position + Vector3.up * 0.2f;
                        float d = Vector3.Distance(eye, mp);
                        if (d < best && d <= Tuning.PickupRange + 1f && InView(eye, fwd, mp, 55f))
                        {
                            _pickupId = m.Id;
                            Offer(PromptKind.Pickup, mp, full ? "E  (masz już max apteczek)" : "E  podnieś apteczkę", d);
                        }
                    }
                }
            }

            var mmL = MatchManager.Instance;
            if (phase == MatchPhase.Lobby && _p.Seat.Value < 0 && mmL != null && !_p.Busy.Value && mmL.ArcadePlayer.Value != _p.NetworkObjectId)
            {
                if (mmL.ForkHolder.Value == ulong.MaxValue)
                {
                    Vector3 fp = MatchManager.ForkTablePos;
                    float d = Vector3.Distance(eye, fp);
                    if (d <= 2.3f && InView(eye, fwd, fp, 30f)) Offer(PromptKind.ForkPickup, fp + Vector3.up * 0.1f, "E  weź widelec", d - 0.2f);
                }
                else if (mmL.ForkHolder.Value == _p.NetworkObjectId)
                {
                    for (int i = 0; i < MatchManager.OutletPos.Length; i++)
                    {
                        Vector3 op = MatchManager.OutletPos[i];
                        float d = Vector3.Distance(eye, op);
                        if (d <= 2.1f && InView(eye, fwd, op, 35f) && Vector3.Dot(MatchManager.OutletNormal[i], eye - op) > 0f)
                        {
                            _outletIdx = i;
                            Offer(PromptKind.ForkOutlet, op + Vector3.up * 0.15f, "E  włóż widelec do gniazdka", d - 0.3f);
                        }
                    }
                }
                var bb = Boombox.Instance;
                if (bb != null)
                {
                    Vector3 bp = bb.AimPoint;
                    float d = Vector3.Distance(eye, bp);
                    if (d <= 2.3f && InView(eye, fwd, bp, 35f, bb.transform)) Offer(PromptKind.Radio, bp + Vector3.up * 0.25f, "E  radio", d);
                }
                var vend = LobbyVending.Instance;
                if (vend != null && vend.Free && mmL.ForkHolder.Value != _p.NetworkObjectId)
                {
                    Vector3 vp = vend.AimPoint;
                    float d = Vector3.Distance(eye, vp);
                    if (d <= 2.4f && InView(eye, fwd, vp, 40f, vend.transform)) Offer(PromptKind.Vending, vp + Vector3.up * 0.2f, "E  kup napój", d);
                }
                var exit = LobbyExitDoor.Instance;
                if (exit != null && !exit.Running && mmL.ForkHolder.Value != _p.NetworkObjectId)
                {
                    Vector3 hp = exit.HandleWorld;
                    float d = Vector3.Distance(eye, hp);
                    if (d <= 2.2f && InView(eye, fwd, hp, 35f, exit.transform)) Offer(PromptKind.ExitDoor, hp + Vector3.up * 0.15f, "E  wyjdź", d);
                }
                if (LobbyBricks.Instance != null && mmL.ForkHolder.Value != _p.NetworkObjectId)
                {
                    Vector3 tp = LobbyBricks.TubAim;
                    float d = Vector3.Distance(eye, tp);
                    if (d <= 2.4f && InView(eye, fwd, tp, 35f, LobbyBricks.Tub)) Offer(PromptKind.Bricks, tp + Vector3.up * 0.15f, "E  buduj z klocków", d);
                }
                if (mmL.ArcadePlayer.Value == ulong.MaxValue)
                {
                    Vector3 ap = MatchManager.ArcadeScreen;
                    float d = Vector3.Distance(eye, ap);
                    if (d <= 2.4f && InView(eye, fwd, ap, 30f)) Offer(PromptKind.Arcade, ap + Vector3.up * 0.3f, "E  zagraj w SNAKE", d);
                }
            }

            if (phase == MatchPhase.Lobby && _p.Seat.Value < 0)
            {
                var button = LobbyButton.Instance;
                if (button != null)
                {
                    Vector3 bp = button.AimPoint;
                    float d = Vector3.Distance(eye, bp);
                    if (d <= Tuning.ButtonRange && InView(eye, fwd, bp, 40f, button.transform))
                    {
                        bool host = NetworkManager.Singleton.IsServer;
                        int ready = 0, need = 0;
                        foreach (var o in NetPlayer.All)
                        {
                            if (o == null || o.OwnerClientId == NetworkManager.ServerClientId) continue;
                            need++;
                            if (o.Ready.Value) ready++;
                        }
                        string text = !host ? "E  (tylko właściciel pokoju)"
                            : need == 0 ? "E  start (potrzeba 2 graczy)"
                            : ready < need ? $"E  start (gotowi {ready}/{need})"
                            : "E  START";
                        Offer(PromptKind.Button, bp + Vector3.up * 0.15f, text, d);
                    }
                }
                foreach (var seat in LobbySeat.All)
                {
                    if (seat == null || SeatTaken(seat.Index)) continue;
                    Vector3 sp = seat.AimPoint;
                    float d = Vector3.Distance(eye, sp);
                    if (d <= 2.4f && InView(eye, fwd, sp, 30f, seat.transform.parent))
                        Offer(PromptKind.Seat, sp, "E  usiądź", d + 0.3f);
                }
                var board = LobbySettingsBoard.Instance;
                if (board != null)
                {
                    Vector3 bp = board.transform.position;
                    float d = Vector3.Distance(eye, bp);
                    if (d <= Tuning.ButtonRange + 1f && InView(eye, fwd, bp, 35f, board.transform))
                        Offer(PromptKind.Settings, bp, NetworkManager.Singleton.IsServer ? "E  ustawienia rundy" : "E  podgląd ustawień", d);
                }
            }
        }

        private int _pickupId;
        private int _crateLevel;
        private CrateAction _crateAct;
        private int _rcSlotPrompt;
        private int _outletIdx;
        private bool _wasArcade;
        private int _syringeId;
        private bool _ignoreEThisFrame;
        private int _elevCar, _elevFloor;
        private Vector3 _standPos;

        private bool SeatTaken(int index)
        {
            foreach (var o in NetPlayer.All) if (o != null && o.Seat.Value == index) return true;
            return false;
        }

        private void SitNearest()
        {
            LobbySeat best = null;
            float bd = float.MaxValue;
            foreach (var s in LobbySeat.All)
            {
                if (s == null || SeatTaken(s.Index)) continue;
                float d = Vector3.Distance(s.AimPoint, PromptWorld);
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null) return;
            _standPos = transform.position;
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // must be off before we land inside the sofa collider
            _p.TeleportLocal(best.transform.position, best.transform.eulerAngles.y);
            _p.Seat.Value = (sbyte)best.Index;
        }

        private void StandUp()
        {
            var seat = LobbySeat.Get(_p.Seat.Value);
            _p.Seat.Value = -1;
            if (seat != null)
            {
                // Step forward off the seat so the capsule does not end up inside the furniture.
                Vector3 off = seat.transform.position + seat.transform.forward * 0.6f;
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
                _p.TeleportLocal(off, seat.transform.eulerAngles.y);
            }
        }
        private float _nextDebug;
        private static readonly bool ForceFlashlight = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-flashlight") >= 0;
        private bool _testPlaced;
        private static float ArgF(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }
        private static readonly float TestBoardDist = ArgF("-testboard");
        private static readonly bool TestMirror = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testmirror") >= 0;
        private static readonly bool TestReady = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testready") >= 0;
        private static readonly bool TestPull = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testpull") >= 0;
        private bool _testReadyDone, _pull1, _pull2, _elevTestStarted;
        private static readonly bool TestElevator = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testelevator") >= 0;

        private System.Collections.IEnumerator ElevatorTest()
        {
            var es = ElevatorSystem.Instance;
            var mm = MatchManager.Instance;
            var car = es.Cars[0];
            var f0 = car.Floors[0];
            int last = car.Floors.Count - 1;
            _p.TeleportLocal(new Vector3(car.X, f0.Y + 0.05f, -2.9f), 0f);
            _p.Motor.SetLook(0f, 5f);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[ElevTest] call at level {f0.Name}, car state floor={mm.Elevators[0].Floor} mode={mm.Elevators[0].Mode}");
            mm.ElevatorRequestRpc(0, 0);
            yield return new WaitForSeconds(8f);
            Debug.Log($"[ElevTest] after call: floor={mm.Elevators[0].Floor} mode={mm.Elevators[0].Mode} open={ElevatorSystem.DoorOpen(mm.Elevators[0], mm.Now):0.00}");
            _p.TeleportLocal(new Vector3(car.X, f0.Y + 0.05f, -0.3f), 90f);
            _p.Motor.SetLook(90f, 0f);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[ElevTest] ride to level {car.Floors[last].Name}");
            mm.ElevatorRequestRpc(0, (sbyte)last);
            yield return new WaitForSeconds(1.2f + ElevatorSystem.DoorTime + ElevatorSystem.TravelBase + ElevatorSystem.TravelPerFloor * last + 2f);
            Debug.Log($"[ElevTest] arrived: my y={transform.position.y:0.00} (level {car.Floors[last].Name} y={car.Floors[last].Y:0.00}), floor={mm.Elevators[0].Floor} mode={mm.Elevators[0].Mode}");
            _p.Motor.SetLook(180f, 0f);
        }
        private static readonly bool TestSit = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testsit") >= 0;
        private static readonly float TestBoardSide = ArgF("-testboardside");
        private static readonly bool TestButton = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testbutton") >= 0;
        private static readonly bool DebugPos = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-debugpos") >= 0;

        private bool InView(Vector3 eye, Vector3 fwd, Vector3 target, float maxAngle, Transform ignore = null)
        {
            Vector3 to = target - eye;
            if (Vector3.Angle(fwd, to) > maxAngle) return false;
            // Line of sight against the level only; our own capsule and the target object itself never block.
            float dist = to.magnitude - 0.3f;
            return dist <= 0f || !LevelRaycast(eye, to.normalized, dist, ignore, out _);
        }

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Nearest hit along the ray, skipping the local player's own colliders (and <paramref name="ignore"/>).</summary>
        private bool LevelRaycast(Vector3 origin, Vector3 dir, float maxDist, Transform ignore, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, maxDist, ~(1 << PlayerAvatarBinder.LocalBodyLayer | 1 << 9),
                QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.transform.IsChildOf(transform)) continue;
                if (ignore != null && h.transform.IsChildOf(ignore)) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; }
            }
            return bestD < float.MaxValue;
        }

        private void HandlePromptInput(Keyboard kb)
        {
            switch (Prompt)
            {
                case PromptKind.Knife:
                    if (kb.fKey.wasPressedThisFrame && PromptText == "F")
                    {
                        var target = Nearest(o => o.IsAliveHider);
                        if (target != null) _p.RequestKnifeRpc(target.NetworkObjectId);
                    }
                    break;
                case PromptKind.Pickup:
                    if (kb.eKey.wasPressedThisFrame) _p.RequestPickupRpc(_pickupId);
                    break;
                case PromptKind.PickupSyringe:
                    if (kb.eKey.wasPressedThisFrame) _p.RequestPickupSyringeRpc(_syringeId);
                    break;
                case PromptKind.ChainsawPickup:
                    if (kb.eKey.wasPressedThisFrame) _p.RequestPickupChainsawRpc();
                    break;
                case PromptKind.SecurityCrate:
                    if (_crateAct == CrateAction.Saw ? kb.fKey.wasPressedThisFrame : kb.eKey.wasPressedThisFrame)
                        _p.RequestCrateActionRpc((byte)_crateLevel, (byte)_crateAct);
                    break;
                case PromptKind.SecurityButton:
                    if (kb.eKey.wasPressedThisFrame && SecurityRooms.Instance != null) _p.RequestSecurityFloorRpc((byte)_secFloor);
                    break;
                case PromptKind.Button:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) LobbyButton.Instance.Press();
                    break;
                case PromptKind.ElevatorCall:
                case PromptKind.ElevatorPanel:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame && MatchManager.Instance != null)
                    {
                        GameAudio.PlayAt(GameSfx.ElevatorButton, PromptWorld, 0.8f);
                        MatchManager.Instance.ElevatorRequestRpc((byte)_elevCar, (sbyte)_elevFloor);
                    }
                    break;
                case PromptKind.Seat:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) SitNearest();
                    break;
                case PromptKind.ForkPickup:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) _p.RequestForkPickupRpc();
                    break;
                case PromptKind.ForkOutlet:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) _p.RequestForkOutletRpc((byte)_outletIdx);
                    break;
                case PromptKind.Arcade:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame)
                    {
                        _p.RequestArcadeRpc(true);
                        _ignoreEThisFrame = true;
                    }
                    break;
                case PromptKind.Settings:
                    if (kb.eKey.wasPressedThisFrame) GameHud.OpenSettingsPanel();
                    break;
                case PromptKind.Vending:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) _p.RequestVendingRpc();
                    break;
                case PromptKind.Radio:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) Boombox.OpenPanel();
                    break;
                case PromptKind.ExitDoor:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) _p.RequestExitDoorRpc();
                    break;
                case PromptKind.Bricks:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame)
                    {
                        LobbyBricks.BeginBuilding();
                        _ignoreEThisFrame = true;
                    }
                    break;
                case PromptKind.RcPickup:
                    if (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) _p.RequestRcPickupRpc((byte)_rcSlotPrompt);
                    break;
                case PromptKind.RcStomp:
                    if (kb.fKey.wasPressedThisFrame) _p.RequestRcStompRpc((byte)_rcSlotPrompt);
                    break;
            }

            // Hold H (rescue / heal other), hold G (heal self).
            PromptKind wantHold = PromptKind.None;
            NetPlayer target2 = null;
            if ((Prompt == PromptKind.Rescue || Prompt == PromptKind.Heal) && (kb.hKey.isPressed || TestHoldH))
            {
                wantHold = Prompt;
                target2 = Nearest(o => o.IsHider && (Prompt == PromptKind.Rescue ? o.Life.Value == LifeState.Downed : (o.Wounded.Value || o.Limping.Value)));
            }
            else if (Prompt == PromptKind.Resurrect && kb.hKey.isPressed)
            {
                wantHold = PromptKind.Resurrect;
                target2 = Nearest(o => o.IsHider && o.Life.Value == LifeState.Dead);
            }
            else if (kb.gKey.isPressed && _p.IsHider && _p.Life.Value == LifeState.Downed && _p.MedkitCount.Value > 0 && !MenuOpen)
            {
                wantHold = PromptKind.SelfRevive;
                target2 = _p;
            }
            else if ((kb.gKey.isPressed || TestHoldG) && _p.MedkitCount.Value > 0 && (_p.Wounded.Value || _p.Limping.Value) && _p.IsAliveHider)
            {
                wantHold = PromptKind.Heal;
                target2 = _p;
            }
            else if (kb.gKey.wasPressedThisFrame && _p.MedkitCount.Value > 0 && !_p.Wounded.Value && !_p.Limping.Value)
            {
                GameHud.Toast("Nie jesteś ranny.", 2f);
            }

            if (wantHold == PromptKind.None || target2 == null)
            {
                CancelHold();
                return;
            }
            if (_holdKind != wantHold || _holdTarget != target2)
            {
                _holdKind = wantHold;
                _holdTarget = target2;
                _holdTimer = 0f;
                if (wantHold == PromptKind.Rescue)
                {
                    _p.Reviving.Value = true;
                    _p.ReviveTarget.Value = target2.NetworkObjectId;
                }
                else if (wantHold == PromptKind.Resurrect)
                {
                    _p.ReviveTarget.Value = target2.NetworkObjectId;
                    _p.HoldAnimVar.Value = (byte)HideAndSeek.Characters.HoldAnim.Inject;
                }
                else if (wantHold == PromptKind.SelfRevive)
                {
                    _p.HoldAnimVar.Value = (byte)HideAndSeek.Characters.HoldAnim.SelfHeal;
                }
                else if (wantHold == PromptKind.Heal)
                {
                    // Medkit on a wound: animated on every client (own belly or the other hider's waist).
                    _p.ReviveTarget.Value = target2.NetworkObjectId;
                    _p.HoldAnimVar.Value = (byte)(target2 == _p ? HideAndSeek.Characters.HoldAnim.HealSelf : HideAndSeek.Characters.HoldAnim.HealOther);
                }
            }
            _holdTimer += Time.deltaTime;
            if (_holdKind == PromptKind.Rescue || _holdKind == PromptKind.Resurrect || _holdKind == PromptKind.SelfRevive || _holdKind == PromptKind.Heal)
            {
                byte prog = (byte)Mathf.Clamp(Mathf.RoundToInt(HoldProgress * 255f), 0, 255);
                if (prog != _p.ReviveProgress.Value) _p.ReviveProgress.Value = prog;
            }
            if (_holdTimer >= HoldDuration(_holdKind))
            {
                if (_holdKind == PromptKind.Rescue) _p.RequestRescueRpc(_holdTarget.NetworkObjectId);
                else if (_holdKind == PromptKind.SelfRevive) _p.RequestSelfReviveRpc();
                else if (_holdKind == PromptKind.Resurrect) _p.RequestResurrectRpc(_holdTarget.NetworkObjectId);
                else _p.RequestHealRpc(_holdTarget.NetworkObjectId);
                CancelHold();
            }
        }

        private void CancelHold()
        {
            if (_holdKind != PromptKind.None && _p != null && _p.IsSpawned)
            {
                if (_p.Reviving.Value) _p.Reviving.Value = false;
                _p.ReviveTarget.Value = ulong.MaxValue;
                _p.ReviveProgress.Value = 0;
                if (_p.HoldAnimVar.Value != 0) _p.HoldAnimVar.Value = 0;
            }
            _holdKind = PromptKind.None;
            _holdTarget = null;
            _holdTimer = 0f;
        }

        private NetPlayer Nearest(System.Predicate<NetPlayer> filter)
        {
            NetPlayer best = null;
            float bd = float.MaxValue;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == _p || !filter(o)) continue;
                float d = Vector3.Distance(o.ChestPosition, PromptWorld);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // ================================================================ glock

        private void Shoot()
        {
            if (_p.Ammo.Value == 0)
            {
                _p.RequestShootRpc(Vector3.zero, Vector3.forward, ulong.MaxValue, Vector3.zero, -1);
                return;
            }
            var camT = _p.Camera.transform;
            Vector3 origin = camT.position;
            Vector3 dir = camT.forward;

            // Level hit.
            float maxDist = Tuning.GlockRange;
            Vector3 hitPoint = origin + dir * maxDist;
            if (LevelRaycast(origin, dir, maxDist, null, out var hit))
            {
                maxDist = hit.distance;
                hitPoint = hit.point;
            }

            // Player hit: analytic capsule test (players carry no colliders for others).
            NetPlayer victim = null;
            float bestT = maxDist;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == _p || !o.IsHider || o.Life.Value == LifeState.Dead) continue;
                Vector3 a, b;
                float radius;
                if (o.Life.Value == LifeState.Downed)
                {
                    // Downed body lies behind the root (root at the shoulders).
                    a = o.transform.position + Vector3.up * 0.25f - o.transform.forward * 1.5f;
                    b = o.transform.position + Vector3.up * 0.25f + o.transform.forward * 0.2f;
                    radius = 0.32f;
                }
                else
                {
                    float h = Mathf.Lerp(1.65f, 1.05f, o.Motor.CrouchAmount);
                    a = o.transform.position + Vector3.up * 0.3f;
                    b = o.transform.position + Vector3.up * h;
                    radius = 0.36f;
                }
                if (RayCapsule(origin, dir, a, b, radius, out float t) && t < bestT)
                {
                    bestT = t;
                    victim = o;
                    hitPoint = origin + dir * t;
                }
            }

            // RC cars on the floor: small analytic capsule along the body (their colliders sit on Ignore Raycast).
            sbyte rcHit = -1;
            var mm = MatchManager.Instance;
            for (int i = 0; i < RcCar.Count && mm != null; i++)
            {
                var car = RcCar.Peek(i);
                var mode = mm.RcSlot(i).Mode;
                if (car == null || !(mode == RcMode.Driving || mode == RcMode.Parked)) continue;
                if (car.View is RcCar.ViewMode.Hidden or RcCar.ViewMode.Shelf or RcCar.ViewMode.Wreck) continue;
                Vector3 c = car.transform.position + Vector3.up * 0.09f;
                Vector3 fwd = car.transform.forward * 0.2f;
                if (RayCapsule(origin, dir, c - fwd, c + fwd, 0.13f, out float t) && t < bestT)
                {
                    bestT = t;
                    victim = null;
                    rcHit = (sbyte)i;
                    hitPoint = origin + dir * t;
                }
            }
            _p.RequestShootRpc(origin, dir, victim != null ? victim.NetworkObjectId : ulong.MaxValue, hitPoint, rcHit);
        }

        /// <summary>Ray vs capsule (segment a-b, radius r). Returns the approximate entry distance.</summary>
        private static bool RayCapsule(Vector3 o, Vector3 d, Vector3 a, Vector3 b, float r, out float t)
        {
            t = 0f;
            // Closest points between the ray and the segment.
            Vector3 u = d, v = b - a, w = o - a;
            float A = Vector3.Dot(u, u), B = Vector3.Dot(u, v), C = Vector3.Dot(v, v), D = Vector3.Dot(u, w), E = Vector3.Dot(v, w);
            float den = A * C - B * B;
            float sc, tc;
            if (den < 1e-6f) { sc = 0f; tc = C > 0f ? E / C : 0f; }
            else { sc = (B * E - C * D) / den; tc = (A * E - B * D) / den; }
            tc = Mathf.Clamp01(tc);
            sc = Mathf.Max(0f, (Vector3.Dot(u, (a + v * tc) - o)) / A);
            Vector3 pr = o + u * sc, ps = a + v * tc;
            float dist = Vector3.Distance(pr, ps);
            if (dist > r) return false;
            t = Mathf.Max(0f, sc - Mathf.Sqrt(r * r - dist * dist));
            return true;
        }
    }
}
