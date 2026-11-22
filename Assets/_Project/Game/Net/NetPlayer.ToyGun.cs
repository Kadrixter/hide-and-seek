using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Tuning of the toy slime blaster and the goo.</summary>
    public static class ToyTuning
    {
        public const int MinGuns = 1, MaxGuns = 2;      // per round, random, on different levels
        public const byte Shots = 8;                    // per gun, then it is empty and thrown away
        public const float GooTime = 5f;                // seeker slowed / no melee for this long after a hit
        public const float GooSpeed = 0.3f;             // speed multiplier while gooed (last second recovers)
        public const float ShotSpeed = 17f, ShotGravity = 6.5f, ShotMaxTime = 2.6f, ShotRadius = 0.07f;
        public const float FireInterval = 0.5f;
    }

    /// <summary>
    /// Toy slime blaster state of a player. Server: <see cref="ToyAmmo"/> (0 = no gun, else shots left) and
    /// <see cref="GooUntil"/> (seeker covered in goo until this server time). Owner: <see cref="ToyGunOut"/> (the gun is in
    /// the hands - shown to everybody and playing its loud tune). Inventory API: pick up = RequestPickupToyGunRpc(id),
    /// use = <see cref="ToyGunControl.Use"/> (LMB), drop = RequestDropToyGunRpc(), in hand = ToyGunOut.
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<byte> ToyAmmo = new();
        public readonly NetworkVariable<double> GooUntil = new();
        public readonly NetworkVariable<bool> ToyGunOut = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private ToyGunAudio _toyAudio;
        private GooBody _gooBody;

        public bool HasToyGun => ToyAmmo.Value > 0;

        /// <summary>Seconds of goo left (0 = clean).</summary>
        public float GooLeft
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm == null ? 0f : Mathf.Max(0f, (float)(GooUntil.Value - mm.Now));
            }
        }

        public bool Gooed => GooLeft > 0f;

        /// <summary>Movement multiplier from the goo (own factor in the motor speed line).</summary>
        public float GooSpeedScale
        {
            get
            {
                float left = GooLeft;
                return left <= 0f ? 1f : Mathf.Lerp(1f, ToyTuning.GooSpeed, Mathf.Clamp01(left / 1f));
            }
        }

        /// <summary>The gun is in the hands and can be shown / heard.</summary>
        public bool ToyGunShown => ToyGunOut.Value && HasToyGun && IsHider && Life.Value == LifeState.Alive && ToyRoundPhase;

        private static bool ToyRoundPhase
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm != null && (mm.Phase.Value == MatchPhase.Hiding || mm.Phase.Value == MatchPhase.Seeking);
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestPickupToyGunRpc(int id) => MatchManager.Instance?.ServerPickupToyGun(this, id);

        [Rpc(SendTo.Server)]
        public void RequestToyShotRpc(Vector3 origin, Vector3 dir) => MatchManager.Instance?.ServerToyShot(this, origin, dir);

        [Rpc(SendTo.Server)]
        public void RequestDropToyGunRpc() => MatchManager.Instance?.ServerDropToyGun(this);

        /// <summary>Everyone: this player fired a goo blob (shot id, start, velocity).</summary>
        [Rpc(SendTo.Everyone)]
        public void ToyShotRpc(int shotId, Vector3 origin, Vector3 velocity)
        {
            (Actions as BlockyCharacterActions)?.PlayToyShot();
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.PlayToyShot();
            Transform muzzle = IsOwner ? (ViewModel as FirstPersonViewModel)?.ToyMuzzle : (Actions as BlockyCharacterActions)?.ToyMuzzle;
            ToyGooFx.Shot(shotId, origin, velocity, muzzle != null ? muzzle.position : origin);
        }

        /// <summary>Everyone: where the goo blob <paramref name="shotId"/> ended (victim = hit seeker, MaxValue = level,
        /// MaxValue - 1 = nothing hit).</summary>
        [Rpc(SendTo.Everyone)]
        public void ToyHitRpc(int shotId, Vector3 point, Vector3 normal, ulong victimId)
        {
            NetPlayer victim = victimId < ulong.MaxValue - 1 ? Find(victimId) : null;
            ToyGooFx.Hit(shotId, point, normal, victim != null ? victim.transform : null, victimId != ulong.MaxValue - 1);
        }

        /// <summary>Everyone: the gun ran dry and is thrown away.</summary>
        [Rpc(SendTo.Everyone)]
        public void ToyGunEmptyRpc()
        {
            Transform muzzle = (Actions as BlockyCharacterActions)?.ToyMuzzle;
            ToyGooFx.ThrowEmpty(muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f, transform.forward);
            if (IsOwner) GameHud.Toast("Pistolecik pusty - wyrzucony.", 2.5f);
        }

        /// <summary>Called from Update on every client.</summary>
        private void UpdateToyGun()
        {
            bool shown = ToyGunShown;
            float level = ToyAmmo.Value / (float)ToyTuning.Shots;
            (Actions as BlockyCharacterActions)?.SetToyGun(shown, level);
            if (IsOwner)
            {
                (ViewModel as FirstPersonViewModel)?.SetToyGun(shown, level);
                if (ToyGunOut.Value && (!HasToyGun || !IsHider || Life.Value != LifeState.Alive || !ToyRoundPhase)) ToyGunOut.Value = false;
            }
            if (shown && _toyAudio == null) _toyAudio = ToyGunAudio.Attach(this);
            if (_toyAudio != null) _toyAudio.SetPlaying(shown);

            float goo = Gooed && Life.Value == LifeState.Alive ? Mathf.Clamp01(GooLeft / 0.6f) : 0f;
            (Actions as BlockyCharacterActions)?.SetGooed(goo);
            if (goo > 0f && _gooBody == null) _gooBody = GooBody.Attach(this);
            if (_gooBody != null) _gooBody.SetAmount(goo, IsOwner);
        }
    }
}
