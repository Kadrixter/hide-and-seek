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
