using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Toy phone requests and the smash animation (state lives in <see cref="MatchManager.Phones"/>):
    /// pick up (E), smash the ringing phone from the pocket (E), seeker stomps on one on the floor (F), drop.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>This player carries a toy phone.</summary>
        public bool HasPhone => MatchManager.Instance != null && MatchManager.Instance.HasPhone(this);
        /// <summary>The phone in this player's pocket is ringing right now.</summary>
        public bool PhoneRinging => MatchManager.Instance != null && MatchManager.Instance.PocketRinging(this);

        [Rpc(SendTo.Server)]
        public void RequestPickupPhoneRpc(int id) => MatchManager.Instance?.ServerPickupPhone(this, id);

        /// <summary>Owner: throw the ringing phone on the floor and stomp on it (server checks it rings).</summary>
        [Rpc(SendTo.Server)]
        public void RequestPhoneSmashRpc() => MatchManager.Instance?.ServerPhoneSmash(this);

        [Rpc(SendTo.Server)]
        public void RequestPhoneStompRpc(int id) => MatchManager.Instance?.ServerPhoneStomp(this, id);

        /// <summary>Owner: put the carried phone down at <paramref name="pos"/> (inventory "drop"; becomes a pickup).</summary>
        [Rpc(SendTo.Server)]
        public void RequestDropPhoneRpc(Vector3 pos) => MatchManager.Instance?.ServerDropPhone(this, pos);

        /// <summary>
        /// Everyone: this player smashes phone <paramref name="id"/>, ending up at <paramref name="land"/>.
        /// <paramref name="throwFirst"/>: hider pulls it out and throws it, else (seeker) only stomps.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        public void PhoneSmashRpc(int id, Vector3 land, float yaw, bool throwFirst)
        {
            PhoneWorld.Ensure();
            PhoneWorld.Instance.OnSmash(this, id, land, throwFirst);
            (Actions as BlockyCharacterActions)?.PlayPhoneSmash(land, yaw, throwFirst);
            if (IsOwner) StartCoroutine(PhoneSmashView(land, throwFirst));
        }

        /// <summary>Owner: first-person hands / leg on the same clock, the camera turns down to the phone, a kick at the hit.</summary>
        private IEnumerator PhoneSmashView(Vector3 land, bool throwFirst)
        {
            var fpv = ViewModel as FirstPersonViewModel;
            float t = throwFirst ? 0f : PhoneTimeline.SeekerStart;
            bool kicked = false;
            float lookFrom = throwFirst ? PhoneTimeline.Release - 0.15f : PhoneTimeline.SeekerStart;
            while (t < PhoneTimeline.End)
            {
                fpv?.SetPhoneSmash(t, throwFirst, land);
                if (t >= lookFrom && t < PhoneTimeline.End - 0.3f)
                {
                    Vector3 d = land - EyePosition;
                    float want = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -52f, -30f);
                    Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, want, Time.deltaTime * 170f));
                }
                if (!kicked && t >= PhoneTimeline.StompHit)
                {
                    kicked = true;
                    GameHud.Kick();
                }
                yield return null;
                t += Time.deltaTime;
                if (Life.Value != HideAndSeek.Core.LifeState.Alive) break;
            }
            fpv?.SetPhoneSmash(-1f, false, land);
        }
    }
}
