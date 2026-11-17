using UnityEngine;

namespace HideAndSeek.Core
{
    public enum PlayerRole : byte
    {
        None = 0,
        Hider = 1,
        Seeker = 2,
    }

    /// <summary>Hider life cycle. Downed = knifed / shot once, crawling and bleeding out (45 s).</summary>
    public enum LifeState : byte
    {
        Alive = 0,
        Downed = 1,
        Dead = 2,
    }

    /// <summary>What a character holds in its right hand.</summary>
    public enum HeldItem : byte
    {
        None = 0,
        Knife = 1,
        Glock = 2,
        Medkit = 3,
    }

    /// <summary>
    /// Third-person action animations on a character model (seen by other players).
    /// Implemented by the Characters module (BlockyCharacterActions) on the model root.
    /// All durations are in seconds so gameplay can time locks / damage to them.
    /// </summary>
    public interface ICharacterActions
    {
        /// <summary>Seeker: 4 knife stabs into the victim's belly. Returns total duration.</summary>
        float PlayKnifeAttack();

        /// <summary>Victim: reacts to the 4 stabs (flinches in sync), then collapses. Returns total duration until on the ground.</summary>
        float PlayStabbedReaction();

        void SetHeldItem(HeldItem item);

        /// <summary>Alive = normal locomotion; Downed = prone, crawls when moving (speed from ICharacterMotionSource), bleeding;
        /// Dead = lies still.</summary>
        void SetLifeState(LifeState state);

        /// <summary>Glock recoil (third person).</summary>
        void PlayShoot();

        /// <summary>Glock reload. Returns duration.</summary>
        float PlayReload();

        /// <summary>Short flinch when hit by a bullet.</summary>
        void PlayHitReaction();

        /// <summary>Kneel / work on a downed teammate (rescue with H). Looping while true.</summary>
        void SetReviving(bool reviving);

        /// <summary>World point at chest height (interaction icons F / H are drawn here).</summary>
        Transform ChestPoint { get; }

        /// <summary>Barrel tip of the third-person Glock (muzzle flash / tracer origin). Null when no gun held.</summary>
        Transform MuzzlePoint { get; }
    }

    /// <summary>
    /// First-person arms + held item attached to the local camera. Implemented by the Characters module
    /// (FirstPersonViewModel). Only exists for the local player.
    /// </summary>
    public interface IFirstPersonViewModel
    {
        void SetHeldItem(HeldItem item);
        float PlayKnifeAttack();
        void PlayShoot();
        float PlayReload();
        /// <summary>Hide arms entirely (spectating, downed, menus).</summary>
        void SetVisible(bool visible);
        /// <summary>Barrel tip of the first-person Glock (muzzle flash origin).</summary>
        Transform MuzzlePoint { get; }
    }
}
