using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using HideAndSeek.Player;

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
        RcCargo,        // E on an RC car carrying an item: take the item
        RcStomp,        // F on an RC car (seeker)
        Bricks,         // E on the toy brick tub in the lobby
        ExitDoor,       // E on the lobby exit door
        Radio,          // E on the boombox
        SecurityButton, // E on a camera-level button in a security room
        SecurityCrate,  // E on the crate blocking the security room door
        Vending,        // E on the lobby vending machine keypad
        RodPickup,      // E on a metal rod leaning on a rack / fence (hiders)
        DropPickup,     // E on an item somebody dropped (Q)
        FlashBox,       // E on the lobby flashlight box (take = ready / put back)
        ToyGunPickup,   // E on a toy slime blaster lying on the map (hiders)
        PhonePickup,    // E on a toy phone on the floor (hiders)
        PhoneStomp,     // F on a toy phone on the floor (seeker)
        PaMic,          // E on the public-address microphone hanging on its wall station
        MapRadio,       // E / R on a radio lying on the parking (hiders)
        Extinguisher,   // E on a security-room fire extinguisher (bracket / floor)
        MiniGame,       // E at a free seat of the lobby chess / tic-tac-toe table
        LobbyProp,      // E on a lobby mini interaction (switches, fireplace, record player, fish, globe, darts)
        SecurityChair,  // E on a swivel chair in a security room
        WirePull,       // E hold at a wire-mesh fence / cage: tear a piece of wire off (LocalPlayerController.RcWire.cs)
        AntennaSwap,    // U with the RC transmitter in the hands and a wire in the inventory
        DotLink,        // E at the dot link panel in the exit-gate box (hiders, escape stage DotLink; LocalPlayerController.DotLink.cs)
        FuseInsert,     // E at a fuse box socket with a fuse of its mark (LocalPlayerController.Fuses.cs)
        FuseRemove,     // E hold at a filled fuse box socket: pull the fuse out
        FuseInfo,       // looking at a fuse box with nothing to do there (text only)
        SawWrest,       // E next to a knocked-out / blinded seeker with the chainsaw: take it (LocalPlayerController.SawWrest.cs)
        Kart,           // E at the free lobby go-kart: get in (LocalPlayerController.Kart.cs)
        LobbyTv,        // E looking at the lobby wall TV: the YouTube panel (LocalPlayerController.LobbyTv.cs)
        ArcLink,        // E at the KABELKI arcade cabinet in the lobby: the dot link puzzle (LocalPlayerController.DotLink.cs)
        Claude,         // E at Claude in the lobby corner: the survey (LobbyClaude.Survey.cs)
        CarHood,        // E at the nose of a prepared car: pick the bonnet latch with wire / lift it (LocalPlayerController.CarBattery.cs)
        CarBatteryTake, // E at an open bonnet: take the battery
        CamBattery,     // E at a security room's power tray: connect / take out the battery
        CamSwitch,      // E at a security room's camera power switch
    }

    /// <summary>
    /// Local player's gameplay input: interactions (knife, rescue, heal, pickup, lobby button),
    /// Glock (shoot, reload, holster), flashlight focus, input locks (blind seeker, busy, dead, menus).
    /// </summary>
    public partial class LocalPlayerController : MonoBehaviour
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
        private float _nextSawDryTry;
        private int _secFloor;

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
        public float HoldProgress => _holdKind != PromptKind.None ? _holdTimer / HoldDuration(_holdKind) : Prompt == PromptKind.WirePull ? WireProgress : Prompt == PromptKind.FuseRemove ? FuseProgress : 0f;

        /// <summary>True while a menu (Esc, settings board) owns the mouse.</summary>
        public static bool MenuOpen { get; set; }

        /// <summary>Downed for longer than Tuning.CrawlShare of the bleed-out time: cannot crawl any more.</summary>
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

            // ---- ready in the lobby = flashlight taken from the box (FlashlightBox, E); R no longer toggles it

            // ---- input locks
            bool dead = _p.Life.Value == LifeState.Dead || _p.Escaped.Value; // escaped: out of the round, watching (NetPlayer.Escape.cs)
            bool blind = _p.IsSeeker && (phase == MatchPhase.Hiding || phase == MatchPhase.Loading);
            bool locked = MenuOpen || _p.Busy.Value || blind || dead || phase == MatchPhase.Loading || phase == MatchPhase.Race || KillCam.Active
                          || (phase == MatchPhase.Hiding && mm != null && mm.IntroHoldsStart) // round intro (Cinematics)
                          || HideAndSeek.Game.UI.LoadingScreen.Blocking; // loading screen up (UI/LoadingScreen.cs)
            if (_p.Life.Value != LifeState.Alive || phase != _lastPhase || _p.Seat.Value >= 0 || _p.Busy.Value) _p.InputReader.ResetStance();
            _lastPhase = phase;
            _p.InputReader.InputEnabled = !locked;
            _p.InputReader.ManageCursor = !MenuOpen;
            // Downed: crawling for the first 3/4 of the bleed-out time (Tuning.CrawlShare, 2026-11-27), then too weak to move.
            bool exhausted = false;
            if (_p.Life.Value == LifeState.Downed && mm != null)
            {
                double downedFor = mm.Now - (_p.DownedUntil.Value - mm.BleedOut);
                exhausted = downedFor >= mm.BleedOut * Tuning.CrawlShare;
            }
            CrawlExhausted = exhausted;
            // Seated: no walking; E, Space or any movement key stands up. Never seated outside the lobby.
            bool seated = _p.Seat.Value >= 0;
            if (seated)
            {
                var kbs = Keyboard.current;
                bool standKey = kbs != null && !MenuOpen && (KeyOrPad.Down(Key.E) || KeyOrPad.Down(Key.Space) ||
                                KeyOrPad.Down(Key.W) || KeyOrPad.Down(Key.S) || KeyOrPad.Down(Key.A) || KeyOrPad.Down(Key.D));
                if (standKey || phase != MatchPhase.Lobby || dead)
                {
                    StandUp();
                    _ignoreEThisFrame = true;
                }
            }
            TickSofaSpring(_p.Seat.Value >= 0); // worn sofas: the spring (LocalPlayerController.SofaSpring.cs)
            // SNAKE arcade: WASD / arrows steer, E or Space leaves. No walking or looking around meanwhile.
            var mmA = MatchManager.Instance;
            int arcadeCab = mmA != null ? mmA.ArcadeCabOf(_p.NetworkObjectId) : -1;
            bool arcade = arcadeCab >= 0;
            if (arcade)
            {
                var kba = Keyboard.current;
                if (!_wasArcade) _p.Motor.SetLook(ArcadeCabs.Yaw(arcadeCab), -26f); // eye -> centre of the arcade screen
                if (kba != null && !MenuOpen)
                {
                    if (KeyOrPad.Down(Key.W) || KeyOrPad.Down(Key.UpArrow)) _p.ArcadeDirRpc(0);
                    else if (KeyOrPad.Down(Key.D) || KeyOrPad.Down(Key.RightArrow)) _p.ArcadeDirRpc(1);
                    else if (KeyOrPad.Down(Key.S) || KeyOrPad.Down(Key.DownArrow)) _p.ArcadeDirRpc(2);
                    else if (KeyOrPad.Down(Key.A) || KeyOrPad.Down(Key.LeftArrow)) _p.ArcadeDirRpc(3);
                    if ((KeyOrPad.Down(Key.E) || KeyOrPad.Down(Key.Space)) && !_ignoreEThisFrame)
                    {
                        _p.RequestArcadeRpc(false, (byte)arcadeCab);
                        _ignoreEThisFrame = true;
                    }
                }
            }
            _wasArcade = arcade;
            bool rc = _p.RcAnimating;
            bool gasp = _p.Gasping;
            bool collapsed = _p.OverdoseActive; // lying after too many cans: the body drives the view (NetPlayer.Drunk)
            _p.Motor.MovementEnabled = !locked && !exhausted && _p.Seat.Value < 0 && !arcade && !rc && !gasp && !collapsed && !_p.EmoteHoldsMove && SecurityChairDriver.LocalChair < 0 && !SpringBusy;
            bool poseWheel = PoseWheel.IsOpen || EmoteWheel.IsOpen; // K wheels: the mouse points at a slice instead of turning the head
            _p.Motor.LookEnabled = !locked && !arcade && !rc && !poseWheel && !collapsed && !_p.EmoteViewActive;
            var cc = _p.GetComponent<CharacterController>();
            bool ccOn = !dead && _p.Seat.Value < 0 && !_p.BodyCarriedNow && SecurityChairDriver.LocalChair < 0;
            if (cc != null && cc.enabled != ccOn) cc.enabled = ccOn;
            if (_p.Flashlight != null)
            {
                bool glockOut = _p.Held.Value == HeldItem.Glock;
                _p.Flashlight.ListenToInput = !glockOut && !locked && _p.Life.Value != LifeState.Downed && !rc && _p.HasFlashlight && !poseWheel;
                if (dead && _p.Flashlight.IsOn) _p.Flashlight.SetOn(false, false);
            }

            // Blood trail while crawling.
            if (_p.Life.Value == LifeState.Downed && (transform.position - _lastBloodPos).sqrMagnitude > 0.7f)
            {
                _lastBloodPos = transform.position;
                BloodFx.Decal(transform.position, Random.Range(0.18f, 0.32f));
            }

            Prompt = PromptKind.None;
            // A pad-only player has no real keyboard / mouse: PadInput adds idle ones, but never depend on them here.
            if (locked)
            {
                CancelHold();
                return;
            }

            if (ForceFlashlight && _p.Flashlight != null && !_p.Flashlight.IsOn && _p.Life.Value == LifeState.Alive && _p.HasFlashlight) _p.Flashlight.SetOn(true, false);
            if (KartTick(kb)) { CancelHold(); return; } // lobby go-kart: driving owns the input (LocalPlayerController.Kart.cs)

            // ---- lobby toy bricks: while building the mouse / E / 1-5 belong to the bricks.
            if (phase != MatchPhase.Lobby && LobbyBricks.Building) LobbyBricks.StopBuilding();
            if (LobbyBricks.LocalTick(_p, kb, mouse, _ignoreEThisFrame)) { CancelHold(); return; }

            // ---- public-address microphone in the hand: E hangs it back, LMB is the talk bar; nothing else meanwhile
            if (PaTick(kb)) { CancelHold(); return; }
            if (MapRadioTick(kb)) { CancelHold(); return; } // radio on the shoulder: Q puts it down, R panel

            // ---- fire extinguisher in both hands (LMB spray, Q put down) / foamed seeker: nothing else meanwhile
            if (ExtTick(kb, mouse)) { CancelHold(); return; }

            // ---- body carry: B asks / puts down, Space (carried) asks to be put down; owns the input meanwhile
            if (CarryTick(kb, phase)) { CancelHold(); return; }

            // ---- lobby chess / tic-tac-toe table: seated = the mouse belongs to the board (LocalPlayerController.MiniGames.cs)
            if (MiniGameTick(kb, mouse)) { CancelHold(); return; }

            // ---- dot link panel at the exit gate: the mouse belongs to the screen (LocalPlayerController.DotLink.cs)
            if (DotLinkTick()) { CancelHold(); return; }

            // ---- swivel chair in a security room
            if (ChairTick(kb)) { CancelHold(); return; }

            // ---- toy phone ringing in the pocket: E smashes it (before every other E action)
            if (PhoneControls.TakeE(_p, kb)) { CancelHold(); return; }

            // ---- fence wire torn off / RC antenna swap: the hands are busy; U swaps the antenna (LocalPlayerController.RcWire.cs)
            if (WireTick(kb)) { CancelHold(); return; }
            if (FuseTick()) { CancelHold(); return; } // working on a fuse box (LocalPlayerController.Fuses.cs)
            if (CarBatteryTick(kb)) { CancelHold(); return; } // lifting a bonnet (LocalPlayerController.CarBattery.cs)

            // ---- RC car: 6 puts it down / parks it / takes control again; WASD drives while the car camera is on.
            if (UpdateRc(kb, phase)) { CancelHold(); return; }

            // ---- T toggles the flashlight always (LMB is the gun while the Glock is out)
            if (KeyOrPad.Down(Key.T) && _p.Flashlight != null && _p.Life.Value != LifeState.Downed)
            {
                if (_p.HasFlashlight) _p.Flashlight.Toggle();
                else if (phase == MatchPhase.Lobby) GameHud.Toast("Nie masz latarki. Weź ją z pudełka przy lustrze (E).", 2.5f);
            }

            // ---- seeker: flashlight focus with the mouse wheel
            if (_p.IsSeeker && _p.Flashlight != null)
            {
                float wheel = KeyOrPad.ScrollY; // pad: L1 + D-pad up / down
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

            // ---- weapon switch: hotbar keys 1-5 (Hotbar.cs); Q drops the selected item

            // ---- hider: X = throw dust into the seeker's eyes
            if (_p.IsHider && phase == MatchPhase.Seeking && _p.Life.Value == LifeState.Alive && !_p.Busy.Value &&
                _p.InputReader.InputEnabled && KeyOrPad.Down(Key.X) && Time.time >= _nextDustTry)
            {
                _nextDustTry = Time.time + 0.6f;
                _p.RequestDustRpc();
            }

            // ---- seeker: chainsaw (hold F = full throttle, walk into a hider = kill)
            bool sawRev = _p.IsSeeker && phase == MatchPhase.Seeking && _p.Held.Value == HeldItem.Chainsaw &&
                          _p.Life.Value == LifeState.Alive && _p.InputReader.InputEnabled && (KeyOrPad.Held(Key.F) || TestSawRev) &&
                          _p.SawFuel.Value > 0f && // an empty saw does not start (MatchManager.SawFuel.cs)
                          _p.SawReady;             // pulled and caught; not stalled by dust / goo / foam / a knock-out (NetPlayer.SawNerf.cs)
            // F on an empty tank: a dry pull of the cord (the server plays it for everybody; no cutting).
            if (_p.SawInHands && phase == MatchPhase.Seeking && _p.SawDry && !_p.SawStalled && !_p.Busy.Value && _p.InputReader.InputEnabled &&
                KeyOrPad.Down(Key.F) && Time.time >= _nextSawDryTry)
            {
                _nextSawDryTry = Time.time + SawTuning.DryPullGap;
                _p.RequestSawDryPullRpc();
            }
            if (_p.SawInHands && phase == MatchPhase.Seeking && KeyOrPad.Down(Key.R) && _p.FuelCans.Value > 0 && Time.time >= _nextSawTry)
            {
                _nextSawTry = Time.time + 0.6f;
                _p.RequestRefuelHandsRpc(); // R: fill the saw from the canister in the inventory
            }
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
                    if (KeyOrPad.Down(Key.R)) _p.RequestReloadRpc();
                    bool testShot = TestShoot;
                    TestShoot = false;
                    if (((mouse != null && mouse.leftButton.wasPressedThisFrame && _p.InputReader.CursorLocked) || PadInput.Down(PadAction.Attack) || testShot) && Time.time >= _nextShot)
                    {
                        _nextShot = Time.time + Tuning.FireInterval;
                        Shoot();
                    }
                }
            }

            FindPrompt(phase);
            FilterCarryPrompt(); // body carry: no kneeling rescue / heal on a body lying in somebody's arms
            HandlePromptInput(kb);
        }

        // ================================================================ RC car

        private bool _rcLights = true;

        // ---- test hooks (scripted tests drive the same code path as the keyboard)
        public static bool TestSix, TestHoldG, TestShoot, TestHoldH, TestSawRev;
        public static Vector2 TestRcInput;
        public static bool TestRcBoost, TestRcHorn, TestRcActive;

        /// <summary>Key 6 + driving. Returns true while the RC car owns the input (no other prompts / actions).</summary>
        private bool UpdateRc(Keyboard kb, MatchPhase phase)
        {
            var mm = MatchManager.Instance;
            bool six = KeyOrPad.Down(Key.Digit6) || KeyOrPad.Down(Key.Numpad6) || TestSix || HotbarInput.Six;
            TestSix = false;
            int slot = mm != null ? mm.RcSlotOf(_p) : -1;
            bool roundPhase = phase == MatchPhase.Hiding || phase == MatchPhase.Seeking;

            if (_p.RcControlling && slot >= 0)
            {
                var car = RcCar.Peek(slot);
                if (car != null)
                {
                    KeyOrPad.Drive(out float throttle, out float steer); // WASD / arrows, pad: R2 / L2 + left stick
                    if (TestRcActive) { throttle = TestRcInput.y; steer = TestRcInput.x; }
                    car.Throttle = throttle;
                    car.SteerInput = steer;
                    car.Boost = KeyOrPad.Held(Key.LeftShift) || KeyOrPad.Held(Key.RightShift) || PadInput.Held(PadAction.Jump) || TestRcBoost; // pad: L3 or Cross
                    car.Horn = KeyOrPad.Held(Key.H) || TestRcHorn;
                    if (KeyOrPad.Down(Key.T))
                    {
                        _rcLights = !_rcLights;
                        GameAudio.Play2D(GameSfx.FlashlightAdjust, 0.5f);
                    }
                    car.Lights = _rcLights;
                    RcChargeKey(kb, slot); // R = energise the car (LocalPlayerController.RcPower.cs)
                    _p.SetRcInput(new Vector2(steer, throttle));
                    car.TickLocal(Time.deltaTime, _p.Motor.WalkSpeed, RcPower.BoostSpeed(_p), transform.position, _p);
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
                if (d.magnitude > MatchManager.RcRangeOf(slot)) GameHud.Toast($"Samochodzik jest poza zasięgiem pilota ({MatchManager.RcRangeOf(slot):0} m).", 3f);
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
                if (d <= 2.6f && InView(eye, fwd, sp, 60f)) Offer(PromptKind.ChainsawPickup, sp, Inventory.PickupText(_p, HotbarItem.Chainsaw, "E  weź piłę łańcuchową"), d);
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
                    // A car on the floor goes with its transmitter: only its holder (or anybody when nobody has it) takes it.
                    bool free = s.Mode == RcMode.Shelf || (s.Mode == RcMode.Parked && (s.Holder == _p.NetworkObjectId || s.Holder == ulong.MaxValue));
                    if (_p.IsAliveHider && free && (!hasCar || s.Holder == _p.NetworkObjectId) && (hasCar || mmR.RcRemoteSlotOf(_p) < 0))
                    {
                        if (d <= Tuning.RcPickupRange && InView(eye, fwd, aim, 40f))
                        {
                            _rcSlotPrompt = i;
                            Offer(PromptKind.RcPickup, aim + Vector3.up * 0.1f, hasCar ? "E  weź samochodzik RC" : Inventory.PickupText(_p, HotbarItem.RcCar, "E  weź samochodzik RC"), d);
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
            if ((phase == MatchPhase.Seeking || phase == MatchPhase.Hiding) && Inventory.CanPickUp(_p, HotbarItem.Syringe, phase) && MatchManager.Instance != null)
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
                        Offer(PromptKind.PickupSyringe, mp, fullS ? "E  (masz już max strzykawek)" : Inventory.PickupText(_p, HotbarItem.Syringe, "E  podnieś strzykawkę z adrenaliną"), d);
                    }
                }
            }

            // Metal rods (hiders, peg leg): MatchManager.Rods.
            if ((phase == MatchPhase.Seeking || phase == MatchPhase.Hiding) && Inventory.CanPickUp(_p, HotbarItem.Rod, phase) && MatchManager.Instance != null &&
                MatchManager.Instance.TryRodPrompt(eye, fwd, _p, out int rodId, out var rodPos, out string rodText, out float rodD) && rodD < best)
            {
                _rodId = rodId;
                Offer(PromptKind.RodPickup, rodPos, rodText, rodD);
            }

            // Toy phones: hiders pick up (E), the seeker stomps (F) - MatchManager.Phones.
            if (MatchManager.Instance != null && MatchManager.Instance.TryPhonePrompt(eye, fwd, _p, phase, out var phKind, out int phId, out var phPos, out string phText, out float phD))
            {
                _phoneId = phId;
                Offer(phKind, phPos, phText, phD);
            }

            // Public-address microphone on its wall station next to the elevators (any living player, one at a time).
            if ((phase == MatchPhase.Hiding || phase == MatchPhase.Seeking) && TryPaPrompt(eye, fwd, out var paPos, out string paText, out float paD))
                Offer(PromptKind.PaMic, paPos, paText, paD);
            if ((phase == MatchPhase.Hiding || phase == MatchPhase.Seeking) && TryMapRadioPrompt(eye, fwd, out var mrPos, out string mrText, out float mrD))
                Offer(PromptKind.MapRadio, mrPos, mrText, mrD);

            // Fire extinguishers of the security rooms (hiders).
            if ((phase == MatchPhase.Hiding || phase == MatchPhase.Seeking) && TryExtPrompt(eye, fwd, out var exPos, out string exText, out float exD))
                Offer(PromptKind.Extinguisher, exPos, exText, exD);

            // Swivel chairs of the security rooms.
            if ((phase == MatchPhase.Hiding || phase == MatchPhase.Seeking) && TryChairPrompt(eye, fwd, out var chPos, out string chText, out float chD))
                Offer(PromptKind.SecurityChair, chPos, chText, chD);

            // Wire-mesh fences / cages: a piece of wire (hiders); U over the transmitter in the hands (LocalPlayerController.RcWire.cs).
            if (TryWirePrompt(eye, fwd, phase, out var wPos, out string wText, out float wD)) Offer(PromptKind.WirePull, wPos, wText, wD);
            if (TryRcCargoPrompt(eye, fwd, phase, out var cgPos, out string cgText, out float cgD)) Offer(PromptKind.RcCargo, cgPos, cgText, cgD);
            if (TryAntennaPrompt(out var aPos, out string aText, out float aD)) Offer(PromptKind.AntennaSwap, aPos, aText, aD);
            if (TryDotLinkPrompt(eye, fwd, out var dlPos, out string dlText, out float dlD)) Offer(PromptKind.DotLink, dlPos, dlText, dlD); // exit-gate panel
            if (TryFusePrompt(eye, fwd, phase, out var fbPos2, out string fbText2, out float fbD2, out var fbKind)) Offer(fbKind, fbPos2, fbText2, fbD2); // fuse boxes
            if (TrySawWrestPrompt(eye, fwd, phase, out var swPos, out string swText, out float swD)) Offer(PromptKind.SawWrest, swPos, swText, swD - 0.5f); // chainsaw nerf
            if (TryHoodPrompt(eye, fwd, phase, out var hdKind, out var hdPos, out string hdText, out float hdD)) Offer(hdKind, hdPos, hdText, hdD); // car bonnets (LocalPlayerController.CarBattery.cs)
            if (TryCamPowerPrompt(eye, fwd, phase, out var cpKind, out var cpPos, out string cpText, out float cpD)) Offer(cpKind, cpPos, cpText, cpD); // security room camera power

            // Medkits can be picked up any time during the round (also when not hurt) and kept in the inventory.
            if (phase == MatchPhase.Seeking || phase == MatchPhase.Hiding)
            {
                var mm = MatchManager.Instance;
                if (Inventory.CanPickUp(_p, HotbarItem.Medkit, phase) && mm != null)
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
                            Offer(PromptKind.Pickup, mp, full ? "E  (masz już max apteczek)" : Inventory.PickupText(_p, HotbarItem.Medkit, "E  podnieś apteczkę"), d);
                        }
                    }
                }
            }

            // Items somebody dropped (Q): MatchManager.Drops (hiders never get a prompt for the seeker's weapons).
            if ((phase == MatchPhase.Seeking || phase == MatchPhase.Hiding) && MatchManager.Instance != null &&
                MatchManager.Instance.TryDropPrompt(eye, fwd, _p, (pos, view) => InView(eye, fwd, pos, 55f, view), out int dropId, out var dropPos, out string dropText, out float dropD))
            {
                _dropId = dropId;
                Offer(PromptKind.DropPickup, dropPos + Vector3.up * 0.15f, dropText, dropD - 0.05f);
            }

            var mmL = MatchManager.Instance;
            if (phase == MatchPhase.Lobby && _p.Seat.Value < 0 && mmL != null && !_p.Busy.Value && mmL.ArcadeCabOf(_p.NetworkObjectId) < 0)
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
                if (TryTvPrompt(eye, fwd, out var tvPos, out float tvD)) Offer(PromptKind.LobbyTv, tvPos, "E  telewizor (YouTube)", tvD);
                var claude = LobbyClaude.Instance;
                if (claude != null && !LobbyClaude.SurveyOpen)
                {
                    Vector3 cp = claude.AimPoint;
                    float d = Vector3.Distance(eye, cp);
                    if (d <= 2.6f && InView(eye, fwd, cp, 40f, claude.transform)) Offer(PromptKind.Claude, cp + Vector3.up * 0.3f, "E  porozmawiaj z Claude", d);
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
                for (int cab = 0; cab < ArcadeCabs.Count; cab++)
                {
                    if (mmL.ArcadePlayerOf(cab) != ulong.MaxValue) continue;
                    Vector3 ap = ArcadeCabs.Screen(cab);
                    float d = Vector3.Distance(eye, ap);
                    if (d <= 2.4f && InView(eye, fwd, ap, 30f) && d < best)
                    {
                        _arcadeCab = cab;
                        Offer(PromptKind.Arcade, ap + Vector3.up * 0.3f, "E  zagraj w SNAKE", d);
                    }
                }
            }

            if (phase == MatchPhase.Lobby && _p.Seat.Value < 0 && !_p.Busy.Value && mmL != null && mmL.ArcadeCabOf(_p.NetworkObjectId) < 0 && mmL.ForkHolder.Value != _p.NetworkObjectId &&
                LobbyPropDriver.TryPrompt(_p, eye, fwd, (pos, view) => InView(eye, fwd, pos, 35f, view), out int propId, out var propPos, out string propText, out float propD) && propD < best)
            {
                _propId = propId;
                Offer(PromptKind.LobbyProp, propPos, propText, propD);
            }

            if (phase == MatchPhase.Lobby && _p.Seat.Value < 0 && FlashlightBox.TryPrompt(_p, eye, fwd, out var fbPos, out var fbText, out float fbD))
                Offer(PromptKind.FlashBox, fbPos, fbText, fbD);
            if (phase == MatchPhase.Lobby && MiniGamePrompt(eye, fwd, out var mgPos, out var mgText, out float mgD)) Offer(PromptKind.MiniGame, mgPos, mgText, mgD);
            if (TryKartPrompt(eye, fwd, phase, out var kaPos, out var kaText, out float kaD)) Offer(PromptKind.Kart, kaPos, kaText, kaD); // lobby go-kart
            if (phase == MatchPhase.Lobby && TryArcLinkPrompt(eye, fwd, out var alPos, out var alText, out float alD)) Offer(PromptKind.ArcLink, alPos, alText, alD); // KABELKI cabinet

            // Toy slime blasters (hiders): MatchManager.ToyGuns.
            if ((phase == MatchPhase.Seeking || phase == MatchPhase.Hiding) && _p.IsAliveHider && MatchManager.Instance != null &&
                MatchManager.Instance.TryToyGunPrompt(eye, fwd, _p, out int toyId, out var toyPos, out string toyText, out float toyD) && toyD < best)
            {
                _toyGunId = toyId;
                Offer(PromptKind.ToyGunPickup, toyPos, toyText, toyD);
            }

            if (phase == MatchPhase.Lobby && _p.Seat.Value < 0)
            {
                var button = LobbyButton.Instance;
                if (button != null && !button.Pulling)
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
        private int _dropId;
        private int _crateLevel;
        private CrateAction _crateAct;
        private int _rcSlotPrompt;
        private int _outletIdx;
        private int _arcadeCab;
        private int _propId;
        private bool _wasArcade;
        private int _syringeId;
        private int _rodId;
        private int _toyGunId;
        private int _phoneId;
        private bool _ignoreEThisFrame;
        private int _elevCar, _elevFloor;
        private Vector3 _standPos;

        private bool SeatTaken(int index)
        {
            foreach (var o in NetPlayer.All) if (o != null && o.Seat.Value == index) return true;
            return NetPlayer.SeatBlockedByLying(index); // somebody lies along the whole sofa
        }

        private void SitNearest()
        {
            if (SpringBusy) return;
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
            _p.PrepareSit(); // the body walks into the seat from here, the view turns round smoothly
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
                // Step forward off the seat so the capsule does not end up inside the furniture (the seated body is
                // anchored to the seat, so it stands up from there and the camera follows its eyes: no jump).
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
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, maxDist, ~(1 << PlayerAvatarBinder.LocalBodyLayer | 1 << 9 | GameLayers.RagdollMask),
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
                    if (KeyOrPad.Down(Key.F) && PromptText == "F")
                    {
                        var target = Nearest(o => o.IsAliveHider);
                        if (target != null) _p.RequestKnifeRpc(target.NetworkObjectId);
                    }
                    break;
                case PromptKind.Pickup:
                    if (KeyOrPad.Down(Key.E)) _p.RequestPickupRpc(_pickupId);
                    break;
                case PromptKind.PickupSyringe:
                    if (KeyOrPad.Down(Key.E)) _p.RequestPickupSyringeRpc(_syringeId);
                    break;
                case PromptKind.RodPickup:
                    if (KeyOrPad.Down(Key.E)) _p.RequestPickupRodRpc(_rodId);
                    break;
                case PromptKind.ToyGunPickup:
                    if (KeyOrPad.Down(Key.E)) _p.RequestPickupToyGunRpc(_toyGunId);
                    break;
                case PromptKind.FlashBox:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) _p.RequestFlashBoxRpc(!_p.Ready.Value);
                    break;
                case PromptKind.ChainsawPickup:
                    if (KeyOrPad.Down(Key.E)) _p.RequestPickupChainsawRpc();
                    break;
                case PromptKind.DropPickup:
                    if (KeyOrPad.Down(Key.E)) _p.RequestPickupDropRpc(_dropId);
                    break;
                case PromptKind.SecurityCrate:
                    if (_crateAct == CrateAction.Saw ? KeyOrPad.Down(Key.F) : KeyOrPad.Down(Key.E))
                        _p.RequestCrateActionRpc((byte)_crateLevel, (byte)_crateAct);
                    break;
                case PromptKind.SecurityButton:
                    if (KeyOrPad.Down(Key.E) && SecurityRooms.Instance != null) _p.RequestSecurityFloorRpc((byte)_secFloor);
                    break;
                case PromptKind.Button:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) LobbyButton.Instance.Press();
                    break;
                case PromptKind.ElevatorCall:
                case PromptKind.ElevatorPanel:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame && MatchManager.Instance != null)
                    {
                        GameAudio.PlayAt(GameSfx.ElevatorButton, PromptWorld, 0.8f);
                        MatchManager.Instance.ElevatorRequestRpc((byte)_elevCar, (sbyte)_elevFloor);
                    }
                    break;
                case PromptKind.Seat:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) SitNearest();
                    break;
                case PromptKind.ForkPickup:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) _p.RequestForkPickupRpc();
                    break;
                case PromptKind.ForkOutlet:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) _p.RequestForkOutletRpc((byte)_outletIdx);
                    break;
                case PromptKind.Arcade:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame)
                    {
                        _p.RequestArcadeRpc(true, (byte)_arcadeCab);
                        _ignoreEThisFrame = true;
                    }
                    break;
                case PromptKind.Settings:
                    if (KeyOrPad.Down(Key.E)) GameHud.OpenSettingsPanel();
                    break;
                case PromptKind.LobbyProp:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame && MatchManager.Instance != null) MatchManager.Instance.UsePropRpc((byte)_propId);
                    break;
                case PromptKind.Vending:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) _p.RequestVendingRpc();
                    break;
                case PromptKind.Radio:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) Boombox.OpenPanel();
                    break;
                case PromptKind.LobbyTv:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) LobbyTvVideo.OpenPanel();
                    break;
                case PromptKind.Claude:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame)
                    {
                        LobbyClaude.OpenSurvey();
                        _ignoreEThisFrame = true;
                    }
                    break;
                case PromptKind.ExitDoor:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) _p.RequestExitDoorRpc();
                    break;
                case PromptKind.Bricks:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame)
                    {
                        LobbyBricks.BeginBuilding();
                        _ignoreEThisFrame = true;
                    }
                    break;
                case PromptKind.RcPickup:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) _p.RequestRcPickupRpc((byte)_rcSlotPrompt);
                    break;
                case PromptKind.RcCargo:
                    if ((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestPressE) _p.RequestRcCargoTakeRpc((byte)_rcCargoPrompt);
                    TestPressE = false;
                    break;
                case PromptKind.RcStomp:
                    if (KeyOrPad.Down(Key.F)) _p.RequestRcStompRpc((byte)_rcSlotPrompt);
                    break;
                case PromptKind.PhonePickup:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) _p.RequestPickupPhoneRpc(_phoneId);
                    break;
                case PromptKind.PhoneStomp:
                    if (KeyOrPad.Down(Key.F)) _p.RequestPhoneStompRpc(_phoneId);
                    break;
                case PromptKind.MiniGame:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) { MiniGamePromptE(); _ignoreEThisFrame = true; }
                    break;
                case PromptKind.PaMic:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) PaPromptE();
                    break;
                case PromptKind.MapRadio:
                    MapRadioPromptKeys(kb);
                    break;
                case PromptKind.Extinguisher:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) ExtPromptE();
                    break;
                case PromptKind.SecurityChair:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) ChairPromptE();
                    break;
                case PromptKind.WirePull:
                    if ((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestPressE) WirePromptE();
                    TestPressE = false;
                    break;
                case PromptKind.ArcLink:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) ArcLinkPromptE();
                    break;
                case PromptKind.DotLink:
                    if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) DotLinkPromptE();
                    break;
                case PromptKind.FuseInsert:
                case PromptKind.FuseRemove:
                    FusePromptE((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestPressE);
                    TestPressE = false;
                    break;
                case PromptKind.Kart:
                    if ((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestKartE) { TestKartE = false; KartPromptE(); }
                    break;
                case PromptKind.CarHood:
                    if ((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestPressE) HoodPromptE();
                    TestPressE = false;
                    break;
                case PromptKind.CarBatteryTake:
                    if (((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestPressE) && _hoodPrompt >= 0) _p.RequestBatteryTakeRpc((byte)_hoodPrompt);
                    TestPressE = false;
                    break;
                case PromptKind.CamBattery:
                    if ((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestPressE) CamBatteryPromptE();
                    TestPressE = false;
                    break;
                case PromptKind.CamSwitch:
                    if (((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestPressE) && _camRoom >= 0) _p.RequestCamSwitchRpc((byte)_camRoom);
                    TestPressE = false;
                    break;
                case PromptKind.SawWrest:
                    SawWrestPromptE((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestWrestE);
                    TestWrestE = false;
                    break;
            }

            // Hold H (rescue / heal other), hold G (heal self). Seated in the lobby G opens the pose wheel instead.
            bool gHeld = KeyOrPad.Held(Key.G), gDown = KeyOrPad.Down(Key.G); // G = medkit only (the pose wheel is on K)
            PromptKind wantHold = PromptKind.None;
            NetPlayer target2 = null;
            if ((Prompt == PromptKind.Rescue || Prompt == PromptKind.Heal) && (KeyOrPad.Held(Key.H) || TestHoldH || HotbarInput.HoldH))
            {
                wantHold = Prompt;
                target2 = Nearest(o => o.IsHider && (Prompt == PromptKind.Rescue ? o.Life.Value == LifeState.Downed : (o.Wounded.Value || o.Limping.Value)));
            }
            else if (Prompt == PromptKind.Resurrect && (KeyOrPad.Held(Key.H) || HotbarInput.HoldH))
            {
                wantHold = PromptKind.Resurrect;
                target2 = Nearest(o => o.IsHider && o.Life.Value == LifeState.Dead);
            }
            else if ((gHeld || HotbarInput.HoldG) && _p.IsHider && _p.Life.Value == LifeState.Downed && _p.MedkitCount.Value > 0 && !MenuOpen)
            {
                wantHold = PromptKind.SelfRevive;
                target2 = _p;
            }
            else if ((gHeld || TestHoldG || HotbarInput.HoldG) && _p.MedkitCount.Value > 0 && (_p.Wounded.Value || _p.Limping.Value) && _p.IsAliveHider)
            {
                wantHold = PromptKind.Heal;
                target2 = _p;
            }
            else if (gDown && _p.MedkitCount.Value > 0 && !_p.Wounded.Value && !_p.Limping.Value)
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
            Vector3 dir = _p.GlockShotDirection(camT); // spread: small when aimed (RMB), large from the hip

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
