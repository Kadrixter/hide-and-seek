namespace HideAndSeek.Audio
{
    /// <summary>
    /// Every gameplay sound effect by id. Clips live in <see cref="GameAudioLibrary"/> (Resources/GameAudioLibrary).
    /// Append only - values are sent over the network.
    /// </summary>
    public enum GameSfx : ushort
    {
        None = 0,

        // UI / menu
        UiClick = 1,
        UiHover = 2,
        UiError = 3,
        LobbyButtonPress = 4,   // big green start button pressed (E)
        CountdownTick = 5,
        RoundStart = 6,         // round begins (hiders released)
        SeekerReleased = 7,     // hide phase over, seeker starts hunting (horror sting)
        WinSting = 8,
        LoseSting = 9,
        PlayerJoined = 10,
        PlayerLeft = 11,

        // Knife
        KnifeDraw = 20,
        KnifeSwing = 21,
        KnifeStab = 22,          // one stab into flesh (played 4x)
        BodyFall = 23,

        // Voice (screams)
        HiderScream = 30,        // caught / stabbed scream
        HiderPain = 31,          // short pain grunt (bullet hit, each stab)
        HiderDeath = 32,         // final death scream / gasp
        HiderBreathPanic = 33,   // heavy panicked breathing (downed)

        // Downed / rescue
        CrawlLoop = 40,          // loop: dragging body on concrete
        HeartbeatLoop = 41,      // loop: local downed player's heartbeat
        ReviveLoop = 42,         // loop: bandaging / rescue in progress
        ReviveDone = 43,
        MedkitPickup = 44,
        MedkitUse = 45,

        // Glock
        GlockEquip = 50,
        GlockShot = 51,
        GlockShotDistant = 52,   // tail heard far away
        GlockReload = 53,
        GlockDryFire = 54,
        BulletImpact = 55,
        ShellCasing = 56,

        // Hints / misc
        HintWhistle = 60,        // fallback hint sound when Voice/*.mp3 is empty
        FlashlightClick = 61,
        FlashlightAdjust = 62,   // seeker zooms the beam (mouse wheel)

        // Elevators
        ElevatorDing = 70,
        ElevatorDoor = 71,
        ElevatorMove = 72,       // loop: cabin hum while travelling
        ElevatorButton = 73,

        // Chainsaw (seeker weapon picked up in the generator room)
        ChainsawStart = 80,      // pull cord + engine starts
        ChainsawIdle = 81,       // loop: idling engine while held
        ChainsawRev = 82,        // loop: full throttle (F held), heard far away
        ChainsawCut = 83,        // cutting into a body (kill)

        // Parked cars
        CarAlarm = 90,           // loop: car alarm siren (heard across the garage)

        // Adrenaline / bandage
        InjectStab = 92,         // cap off, needle in, plunger hiss
        ResurrectGasp = 93,      // huge gasp for air when brought back
        BandageWrap = 94,        // medkit opened, bandage wrapped round the waist

        // Lobby easter eggs
        ForkSparks = 95,         // fork in the socket: short circuit, crackling arc
        ForkScream = 96,         // electrocuted scream (aaaa)
        ArcadeEat = 97,          // snake eats a dot
        ArcadeDie = 98,          // snake crashed
        ArcadeStart = 99,        // game start jingle
        ArcadeStick = 100,       // joystick click

        // RC rally car
        RcMotor = 101,           // loop: electric motor whine + gear rattle (pitch follows the speed)
        RcHorn = 102,            // loop: loud toy horn while H is held
        RcCrush = 103,           // stomped flat: plastic crunch, parts snapping off
        RcPlace = 104,           // car put down / picked up (plastic click)
        RcBump = 105,            // car hits a wall / a leg

        // Lobby exit door easter egg
        MinigunSpinUp = 106,     // barrels winding up
        MinigunFire = 107,       // loop: continuous burst
        MinigunSpinDown = 108,   // barrels winding down
        DoorOpen = 109,          // handle + creaking hinge
        DoorSlam = 110,          // door slammed shut
        VendButton = 111,     // vending machine selection button + beep
        VendDrop = 112,       // can drops into the tray
        CanOpen = 113,        // pull tab crack + fizz
        CanGulp = 114,        // three gulps
        CanAhh = 115,         // satisfied exhale after drinking
        CanCrush = 116,       // empty can squeezed in the fist
        CanBounce = 117,      // thrown can hits the floor and rolls
    }

    /// <summary>Looping ambient beds.</summary>
    public enum AmbientId : byte
    {
        None = 0,
        MenuMusic = 1,
        LobbyRoom = 2,
        GarageBed = 3,       // low rumble / room tone of the parking garage
        Ventilation = 4,     // 3D emitters near vents
        Drips = 5,           // 3D emitters, flooded -3 floor
        LampBuzz = 6,        // 3D emitters near lamps
        DistantCity = 7,     // muffled traffic near the entrance ramp
        TensionMusic = 8,    // last 2 minutes
    }
}
