using System.Collections;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Toy phone requests (server) and prompts (client): pick up (E), drop (inventory), smash the ringing phone from
    /// the pocket (E: throw + stomp), the seeker stomps on one on the floor (F). State: <see cref="MatchManager.Phones"/>.
    /// </summary>
    public partial class MatchManager
    {
        // ================================================================ requests

        public void ServerPickupPhone(NetPlayer pl, int id)
        {
            if (pl == null || !pl.IsAliveHider || pl.Busy.Value) return;
            if (Phase.Value != MatchPhase.Seeking && Phase.Value != MatchPhase.Hiding) return;
            if (HasPhone(pl)) { pl.ToastRpc("Masz już telefon.", 2f); return; }
            if (!Inventory.HasRoomFor(pl, HotbarItem.Phone)) { pl.ToastRpc(Inventory.FullText, 2.5f); return; }
            for (int i = 0; i < Phones.Count; i++)
            {
                var s = Phones[i];
                if (s.Id != id || s.Mode != PhoneMode.Floor) continue;
                if (Vector3.Distance(pl.transform.position, s.Pos) > Tuning.PickupRange + 1.5f) return;
                s.Mode = PhoneMode.Carried;
                s.Holder = pl.NetworkObjectId;
                Phones[i] = s;
                _phoneHolderPos[s.Id] = pl.transform.position;
                pl.SfxRpc(GameSfx.RcPlace, 0.6f);
                pl.ToastRpc("Masz zabawkowy telefon. Kto jeszcze ma telefon, słyszy cię przez niego (V) - w całym garażu.", 5f);
                Debug.Log($"[Phone] {pl.Nick} picked up phone {id}{(PhoneRinging(s, Now) ? " (ringing)" : "")}");
                return;
            }
        }

        /// <summary>
        /// Inventory drop: the carried phone goes on the floor at <paramref name="pos"/> (snapped to the floor, max 2.5 m
        /// from the player) and becomes a normal pickup. A ringing phone keeps ringing there.
        /// </summary>
        public void ServerDropPhone(NetPlayer pl, Vector3 pos)
        {
            if (pl == null) return;
            int i = PhoneOf(pl);
            if (i < 0) return;
            Vector3 from = pl.transform.position;
            Vector3 d = pos - from;
            if (d.magnitude > 2.5f) pos = from + d.normalized * 2.5f;
            var s = Phones[i];
            s.Mode = PhoneMode.Floor;
            s.Holder = 0;
            s.Pos = FloorUnder(pos);
            s.Yaw = pl.transform.eulerAngles.y + UnityEngine.Random.Range(-30f, 30f);
            Phones[i] = s;
            Debug.Log($"[Phone] {pl.Nick} dropped phone {s.Id} at {s.Pos}");
        }

        /// <summary>Hider presses E while the phone in the pocket rings: throws it on the floor and stomps on it.</summary>
        public void ServerPhoneSmash(NetPlayer pl)
        {
            if (pl == null || !pl.IsAliveHider || pl.Busy.Value || pl.Seat.Value >= 0) return;
            int i = PhoneOf(pl);
            if (i < 0) return;
            var s = Phones[i];
            if (s.RingAt <= 0d || Now - s.RingAt > PhoneTuning.RingLength + 0.5d) return;
            Vector3 land = FloorUnder(pl.transform.position + pl.transform.forward * 0.5f + pl.transform.right * 0.08f);
            float yaw = pl.transform.eulerAngles.y + UnityEngine.Random.Range(-40f, 40f);
            s.Mode = PhoneMode.Smashed;
            s.RingAt = 0d;
            s.Holder = 0;
            s.Pos = land;
            s.Yaw = yaw;
            Phones[i] = s;
            Debug.Log($"[Phone] {pl.Nick} smashes phone {s.Id}");
            StartCoroutine(PhoneSmashRoutine(pl, s.Id, land, yaw));
        }

        private IEnumerator PhoneSmashRoutine(NetPlayer pl, int id, Vector3 land, float yaw)
        {
            pl.Busy.Value = true;
            pl.PhoneSmashRpc(id, land, yaw, true);
            yield return new WaitForSeconds(PhoneTimeline.Duration(true));
            if (pl != null && pl.IsSpawned) pl.Busy.Value = false;
        }

        /// <summary>Seeker F next to a phone on the floor: steps up and stomps on it.</summary>
        public void ServerPhoneStomp(NetPlayer seeker, int id)
        {
            if (!Seeking || seeker == null || !seeker.IsSeeker || seeker.Busy.Value || seeker.Life.Value != LifeState.Alive) return;
            for (int i = 0; i < Phones.Count; i++)
            {
                var s = Phones[i];
                if (s.Id != id || s.Mode != PhoneMode.Floor) continue;
                Vector3 d = s.Pos - seeker.transform.position;
                if (Mathf.Abs(d.y) > 1.2f) return;
                d.y = 0f;
                if (d.magnitude > PhoneTuning.StompServerRange) return;
                StartCoroutine(PhoneStompRoutine(seeker, s));
                return;
            }
        }

        private IEnumerator PhoneStompRoutine(NetPlayer seeker, PhoneState s)
        {
            seeker.Busy.Value = true;
            Vector3 dir = s.Pos - seeker.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = seeker.transform.forward;
            dir.Normalize();
            Vector3 stand = s.Pos - dir * PhoneTuning.StompStandOff - Quaternion.LookRotation(dir) * Vector3.right * 0.08f;
            stand.y = Mathf.Max(s.Pos.y, seeker.transform.position.y - 0.3f);
            seeker.TeleportRpc(stand, Quaternion.LookRotation(dir).eulerAngles.y);
            yield return new WaitForSeconds(0.08f);
            if (seeker == null || !seeker.IsSpawned) yield break;
            seeker.PhoneSmashRpc(s.Id, s.Pos, s.Yaw, false);
            yield return new WaitForSeconds(PhoneTimeline.StompHit - PhoneTimeline.SeekerStart);
            for (int i = 0; i < Phones.Count; i++)
            {
                var c = Phones[i];
                if (c.Id != s.Id || c.Mode != PhoneMode.Floor) continue;
                c.Mode = PhoneMode.Smashed;
                c.RingAt = 0d;
                Phones[i] = c;
                seeker.ToastRpc("Rozdeptałeś telefon. Chowający słyszą się przez takie telefony...", 3.5f);
                Debug.Log($"[Phone] {seeker.Nick} stomped on phone {s.Id}");
            }
            yield return new WaitForSeconds(PhoneTimeline.End - PhoneTimeline.StompHit);
            if (seeker != null && seeker.IsSpawned) seeker.Busy.Value = false;
        }

        // ================================================================ prompts (client)

        private static readonly RaycastHit[] PhoneHits = new RaycastHit[12];

        /// <summary>Level geometry between the eyes and the phone (the own body / held items never block).</summary>
        private static bool PhoneBlocked(Vector3 eye, Vector3 target, Transform me)
        {
            Vector3 to = target - eye;
            float len = to.magnitude - 0.12f;
            if (len <= 0f) return false;
            int n = Physics.RaycastNonAlloc(eye, to / to.magnitude, PhoneHits, len, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!PhoneHits[i].transform.IsChildOf(me) && PhoneHits[i].transform.GetComponentInParent<NetPlayer>() == null) return true;
            return false;
        }

        /// <summary>Client: phone in front within reach - hider picks it up (E), the seeker stomps on it (F).</summary>
        public bool TryPhonePrompt(Vector3 eye, Vector3 fwd, NetPlayer me, MatchPhase phase, out PromptKind kind, out int id, out Vector3 pos, out string text, out float dist)
        {
            kind = PromptKind.None;
            id = 0;
            pos = default;
            text = null;
            dist = float.MaxValue;
            bool hider = me.IsAliveHider && (phase == MatchPhase.Hiding || phase == MatchPhase.Seeking);
            bool seeker = me.IsSeeker && me.Life.Value == LifeState.Alive && phase == MatchPhase.Seeking;
            if ((!hider && !seeker) || Phones == null) return false;
            for (int i = 0; i < Phones.Count; i++)
            {
                var s = Phones[i];
                if (s.Mode != PhoneMode.Floor) continue;
                Vector3 mp = s.Pos + Vector3.up * 0.05f;
                float d = Vector3.Distance(eye, mp);
                if (d >= dist || Vector3.Angle(fwd, mp - eye) > 55f) continue;
                if (hider && d > Tuning.PickupRange + 1f) continue;
                if (seeker)
                {
                    Vector3 flat = s.Pos - me.transform.position;
                    if (Mathf.Abs(flat.y) > 1f) continue;
                    flat.y = 0f;
                    if (flat.magnitude > PhoneTuning.StompPromptRange) continue;
                }
                if (PhoneBlocked(eye, mp, me.transform)) continue;
                id = s.Id;
                pos = mp;
                dist = d;
            }
            if (dist == float.MaxValue) return false;
            kind = seeker ? PromptKind.PhoneStomp : PromptKind.PhonePickup;
            text = seeker ? "F  rozdepcz telefon" : HasPhone(me) ? "E  (masz już telefon)" : Inventory.PickupText(me, HotbarItem.Phone, "E  podnieś zabawkowy telefon");
            return true;
        }
    }
}
