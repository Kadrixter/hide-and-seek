using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server side of spraying: drains the charge while the holder sprays (after the pin animation), and ten times a
    /// second tests the jet (a cone from the nozzle along the holder's look, <see cref="ExtTuning.Range"/> long, line of
    /// sight checked) against seekers. A seeker a short moment in the jet gets the foam: <see cref="NetPlayer.FoamUntil"/>
    /// (no attacks, slowed, wiping the face). More spray during the effect extends it up to <see cref="ExtTuning.FoamMax"/>
    /// in total; once it wears off the seeker is immune for <see cref="ExtTuning.Immunity"/> s, so a hider cannot keep
    /// him locked with one extinguisher.
    /// </summary>
    public partial class MatchManager
    {
        private readonly Dictionary<ulong, float> _extExposure = new();
        private readonly Dictionary<ulong, double> _extImmune = new();
        private readonly RaycastHit[] _extHits = new RaycastHit[16];
        private double _extToastAt;

        private void ServerExtSpray(int i, NetPlayer p)
        {
            var e = Extinguishers[i];
            bool spraying = p.ExtSpray.Value && e.Unlocked && e.Charge > 0 && Now - _extUnlockAt[i] >= ExtTimeline.SprayAfterUnlock - 0.15f && !p.Motion.Value.IsProne;
            if (!spraying) return;
            _extDrain[i] += Time.deltaTime * ExtState.Full / ExtTuning.SprayTime;
            int dec = Mathf.FloorToInt(_extDrain[i]);
            if (dec > 0)
            {
                _extDrain[i] -= dec;
                e.Charge = (byte)Mathf.Max(0, e.Charge - dec);
                Extinguishers[i] = e;
                if (e.Charge == 0) Debug.Log($"[Ext] extinguisher {i} is empty");
            }
            if (Phase.Value != MatchPhase.Seeking || Time.time < _extNextHitTest) return;
            _extNextHitTest = Time.time + 0.1f;
            ExtHitTest(p);
        }

        /// <summary>Jet origin and direction on the server (from the holder's position, yaw and synced look pitch).</summary>
        public static void ExtJet(NetPlayer p, out Vector3 origin, out Vector3 dir)
        {
            float pitch = p.Motion.Value.PitchDeg;
            dir = Quaternion.Euler(-pitch, p.transform.eulerAngles.y, 0f) * Vector3.forward;
            origin = p.transform.position + Vector3.up * 1.15f + p.transform.forward * 0.5f;
        }

        private void ExtHitTest(NetPlayer holder)
        {
            ExtJet(holder, out Vector3 origin, out Vector3 dir);
            foreach (var s in NetPlayer.All)
            {
                if (s == null || !s.IsSeeker || s.Life.Value != LifeState.Alive) continue;
                ulong id = s.NetworkObjectId;
                bool hit = !s.Busy.Value && InJet(origin, dir, s, out Vector3 point);
                _extExposure.TryGetValue(id, out float ex);
                ex = hit ? ex + 0.1f : Mathf.Max(0f, ex - 0.2f);
                _extExposure[id] = ex;
                if (hit && ex >= ExtTuning.ExposureToHit - 0.001f) ApplyFoam(s, holder);
            }
        }

        private bool InJet(Vector3 origin, Vector3 dir, NetPlayer s, out Vector3 point)
        {
            point = default;
            Vector3 feet = s.transform.position;
            foreach (float h in new[] { 1.55f, 1.15f, 0.8f })
            {
                Vector3 t = feet + Vector3.up * h;
                Vector3 to = t - origin;
                float d = to.magnitude;
                if (d > ExtTuning.Range || d < 0.05f) continue;
                float allow = ExtTuning.ConeDeg + Mathf.Atan2(0.22f, d) * Mathf.Rad2Deg;
                if (Vector3.Angle(dir, to) > allow) continue;
                if (!ExtClearLine(origin, t, s)) continue;
                point = t;
                return true;
            }
            return false;
        }

        /// <summary>Nothing but players between the nozzle and the target point.</summary>
        private bool ExtClearLine(Vector3 a, Vector3 b, NetPlayer target)
        {
            Vector3 d = b - a;
            int n = Physics.RaycastNonAlloc(a, d.normalized, _extHits, d.magnitude, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _extHits[i].collider;
                if (c == null || c.GetComponentInParent<NetPlayer>() != null) continue;
                return false;
            }
            return true;
        }

        private void ApplyFoam(NetPlayer seeker, NetPlayer holder)
        {
            double now = Now;
            ulong id = seeker.NetworkObjectId;
            if (seeker.FoamUntil.Value > now)
            {
                double until = System.Math.Min(seeker.FoamStart.Value + ExtTuning.FoamMax, now + ExtTuning.FoamTime);
                if (until > seeker.FoamUntil.Value + 0.05) seeker.FoamUntil.Value = until;
                _extImmune[id] = seeker.FoamUntil.Value + ExtTuning.Immunity;
                return;
            }
            if (_extImmune.TryGetValue(id, out double imm) && now < imm)
            {
                if (now >= _extToastAt)
                {
                    _extToastAt = now + 3.0;
                    holder.ToastRpc($"Zabójca jeszcze się otrząsa - piana zadziała za {imm - now:0} s.", 2.2f);
                }
                return;
            }
            seeker.FoamStart.Value = now;
            seeker.FoamUntil.Value = now + ExtTuning.FoamTime;
            _extImmune[id] = seeker.FoamUntil.Value + ExtTuning.Immunity;
            _extExposure[id] = 0f;
            seeker.FoamHitRpc(holder.NetworkObjectId);
            holder.ToastRpc($"Trafiony! {seeker.Nick} nic nie widzi i nie zaatakuje przez 5 s - uciekaj!", 3f);
            Debug.Log($"[Ext] {holder.Nick} foamed seeker {seeker.Nick} for {ExtTuning.FoamTime} s");
        }
    }
}
