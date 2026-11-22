using HideAndSeek.Core;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local input of the toy phone: while the phone in the pocket rings, E smashes it - this takes priority over
    /// every other E action in that moment (<see cref="LocalPlayerController"/> asks first). The song stops locally at
    /// once; the server starts the throw-and-stomp animation for everybody.
    /// </summary>
    public static class PhoneControls
    {
        /// <summary>Test hook: acts like one E press.</summary>
        public static bool TestE;

        /// <summary>The local player can smash the ringing phone right now (the "E" badge over the phone slot).</summary>
        public static bool SmashOffered(NetPlayer p)
        {
            var mm = MatchManager.Instance;
            if (p == null || mm == null || !p.IsAliveHider || p.Busy.Value || p.Seat.Value >= 0 || p.RcControlling || p.RcAnimating) return false;
            var ph = mm.Phase.Value;
            return (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) && mm.PocketRinging(p);
        }

        /// <summary>Called by the controller before the prompts; true = E was used for the phone this frame.</summary>
        public static bool TakeE(NetPlayer p, Keyboard kb)
        {
            bool e = TestE || (kb != null && kb.eKey.wasPressedThisFrame);
            TestE = false;
            if (!e || !SmashOffered(p)) return false;
            Smash(p);
            return true;
        }

        /// <summary>"Throw and stomp" (also for an inventory button): local stop of the song + request to the server.</summary>
        public static void Smash(NetPlayer p)
        {
            if (!SmashOffered(p)) return;
            PhoneWorld.Ensure();
            PhoneWorld.Instance.SilenceLocal(p);
            p.RequestPhoneSmashRpc();
        }
    }
}
