using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Per-player side of <see cref="ExtinguisherSystem"/>: hands, jet, pin, foamed seekers.</summary>
    public partial class ExtinguisherSystem
    {
        private sealed class PlayerFx
        {
            public float PinAt = -99f;
            public bool Holstered;
            public FoamBody Foam;
            public int CoughDone = -1;
            public double FoamStart;
        }

        private readonly Dictionary<ulong, PlayerFx> _fx = new();
        private readonly List<GameObject> _pins = new();

        private PlayerFx Fx(NetPlayer p)
        {
            if (!_fx.TryGetValue(p.NetworkObjectId, out var f)) _fx[p.NetworkObjectId] = f = new PlayerFx();
            return f;
        }

        private void UpdatePlayers(Unity.Netcode.NetworkList<ExtState> list, int n)
        {
            var mm = MatchManager.Instance;
            var sprayers = new bool[3];
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                var bca = p.Actions as BlockyCharacterActions;
                var fpv = p.IsOwner ? p.ViewModel as FirstPersonViewModel : null;
                var f = Fx(p);
                int idx = mm != null ? mm.ExtHeldBy(p) : -1;
                bool hold = idx >= 0 && idx < n;
                var st = hold ? list[idx] : default;
                bool pinAnim = Time.time - f.PinAt < ExtTimeline.SprayAfterUnlock;
                bool spray = hold && p.ExtSpray.Value && st.Unlocked && st.Charge > 0 && !pinAnim && p.Life.Value == LifeState.Alive;
                Vector3 fromPos = Vector3.zero;
                Quaternion fromRot = Quaternion.identity;
                if (hold && _copies[idx] != null) { fromPos = _copies[idx].Parts.Root.position; fromRot = _copies[idx].Parts.Root.rotation; }
                bca?.SetExtinguisher(hold, fromPos, fromRot, !st.Unlocked, hold ? st.Charge01 : 1f, spray);
                fpv?.SetExtinguisher(hold, fromPos, fromRot, !st.Unlocked, hold ? st.Charge01 : 1f, spray);
                if (hold)
                {
                    // Both hands on the extinguisher: no toy gun, no carried item.
                    bca?.SetToyGun(false, 0f);
                    bca?.SetCarried(0, null);
                    fpv?.SetToyGun(false, 0f);
                    fpv?.SetCarried(0, null);
                    DriveSpray(idx, p, bca, fpv, spray, st);
                    sprayers[idx] = true;
                }
                UpdateFoamed(p, f, bca, fpv);
            }
            for (int i = 0; i < 3; i++)
                if (!sprayers[i] && _copies[i] != null)
                {
                    _copies[i].Spray.Drive(false, _copies[i].Parts.Root.position, Vector3.forward);
                    _copies[i].WasSpraying = false;
                }
        }

        private void DriveSpray(int idx, NetPlayer p, BlockyCharacterActions bca, FirstPersonViewModel fpv, bool spray, in ExtState st)
        {
            var c = _copies[idx];
            Vector3 tip = default, dir = default;
            bool ok = false;
            if (fpv != null && p.Character != null && p.Character.IsFirstPerson) ok = fpv.ExtNozzleScreen(out tip, out dir);
            if (!ok && bca != null) ok = bca.ExtNozzle(out tip, out dir);
            if (!ok) { MatchManager.ExtJet(p, out tip, out dir); }
            c.Spray.Drive(spray, tip, dir);
            // Ran dry in the middle of a burst: the last sputters.
            if (c.WasSpraying && st.Charge == 0 && c.LastCharge > 0) c.Spray.Sputter();
            c.WasSpraying = p.ExtSpray.Value && st.Unlocked;
            c.LastCharge = st.Charge;
        }

        private void UpdateFoamed(NetPlayer p, PlayerFx f, BlockyCharacterActions bca, FirstPersonViewModel fpv)
        {
            bool foamed = p.IsSeeker && p.Foamed;
            float e = foamed ? p.FoamElapsed : 0f, left = foamed ? p.FoamLeft : 0f;
            bca?.SetFoamed(foamed, e, left);
            fpv?.SetFoamed(foamed, e, left);
            if (foamed && f.Foam == null && p.Character != null) f.Foam = FoamBody.Attach(p.Character);
            f.Foam?.Set(foamed, e, left, p.IsOwner && p.Character != null && p.Character.IsFirstPerson);

            // The weapon goes away for the wiping and comes back as the hands come down.
            bool hide = foamed && left > 0.3f;
            if (hide)
            {
                bca?.SetHeldItem(HeldItem.None);
                fpv?.SetHeldItem(HeldItem.None);
                f.Holstered = true;
            }
            else if (f.Holstered)
            {
                f.Holstered = false;
                bca?.SetHeldItem(p.Held.Value);
                fpv?.SetHeldItem(p.Held.Value);
            }

            // Coughing fits in step with the animation.
            if (!foamed) { f.CoughDone = -1; return; }
            if (p.FoamStart.Value != f.FoamStart) { f.FoamStart = p.FoamStart.Value; f.CoughDone = -1; }
            for (int k = f.CoughDone + 1; k < ExtTimeline.Coughs.Length; k++)
            {
                float at = ExtTimeline.Coughs[k];
                if (at > e + left - ExtTimeline.WipeOut - 0.2f || e < at) break;
                f.CoughDone = k;
                if (e - at < 0.3f && p.Character != null) ExtinguisherSounds.Cough(p.Character.Head, k + (int)p.NetworkObjectId);
            }
        }

        /// <summary>Server event: <paramref name="p"/> pulls the pin (sound now, the loose pin flies off at <see cref="ExtTimeline.PinOut"/>).</summary>
        public void OnPinPulled(NetPlayer p)
        {
            if (p == null) return;
            Fx(p).PinAt = Time.time;
            StartCoroutine(PinRoutine(p));
        }

        private IEnumerator PinRoutine(NetPlayer p)
        {
            yield return new WaitForSeconds(ExtTimeline.PinReach);
            if (p == null) yield break;
            var bca = p.Actions as BlockyCharacterActions;
            Vector3 ring = bca != null ? bca.ExtRingWorld : p.transform.position + Vector3.up;
            ExtinguisherSounds.Pin(ring);
            yield return new WaitForSeconds(ExtTimeline.PinOut - ExtTimeline.PinReach);
            if (p == null) yield break;
            ring = bca != null ? bca.ExtRingWorld : ring;
            var pin = ExtinguisherModel.CreateLoosePin(null, 0);
            pin.transform.SetPositionAndRotation(ring, bca != null && bca.ExtProp != null ? bca.ExtProp.Root.rotation : p.transform.rotation);
            var rb = pin.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.linearVelocity = p.transform.right * -0.4f + p.transform.forward * 0.6f + Vector3.up * 1.6f;
            rb.angularVelocity = Random.insideUnitSphere * 14f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            pin.AddComponent<PinSound>();
            _pins.Add(pin);
            if (_pins.Count > 6) { Destroy(_pins[0]); _pins.RemoveAt(0); }
        }

        /// <summary>Server event: the jet hit <paramref name="seeker"/> in the face.</summary>
        public void OnFoamHit(NetPlayer seeker, NetPlayer holder)
        {
            if (seeker == null || seeker.Character == null) return;
            Transform head = seeker.Character.Head;
            Vector3 face = head.TransformPoint(new Vector3(0f, 4f * BlockyCharacter.PixelSize, 0.25f));
            Vector3 from = holder != null ? (holder.transform.position + Vector3.up * 1.2f - face).normalized : head.forward;
            FoamFlakes.Burst(face, from + Vector3.up * 0.3f, 26, 2.2f, 0.09f);
            ExtinguisherSounds.Splat(face);
        }

        /// <summary>Tink on the first hits of the loose pin.</summary>
        private class PinSound : MonoBehaviour
        {
            private int _hits;
            private void OnCollisionEnter(Collision c)
            {
                if (_hits++ < 3 && c.relativeVelocity.magnitude > 0.4f) ExtinguisherSounds.PinLand(transform.position);
            }
        }
    }
}
