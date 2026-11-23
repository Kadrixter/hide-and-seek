using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server rules of the body carry (polled every frame on the carrier, no hooks in the match code):
    /// <list type="bullet">
    /// <item>Who: a healthy hider (not limping, both legs, hands free) carries a hider who is downed, has lost a leg or
    /// is wounded; both agree first (NetPlayer.CarryConsent.cs). Only during the hiding / seeking phases.</item>
    /// <item>Lift -> Hold after the lift time. B (carrier) or Space (carried) -> put down (floor or feet).</item>
    /// <item>The carrier is hit (limps / loses a leg / goes down / dies) or the carried dies -> drop (the body falls
    /// out of the arms; a dead one as a ragdoll). The seeker's hit itself works as always.</item>
    /// <item>Either one in a kill sequence (Busy), gone (disconnected), the round over -> ends at once.</item>
    /// </list>
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Server: can <paramref name="carrier"/> pick <paramref name="target"/> up right now? (reason for the toast)</summary>
        public static bool CanCarry(NetPlayer carrier, NetPlayer target, out string why)
        {
            why = null;
            var mm = MatchManager.Instance;
            if (mm == null || (mm.Phase.Value != MatchPhase.Seeking && mm.Phase.Value != MatchPhase.Hiding)) { why = "Teraz nie można nikogo nosić."; return false; }
            if (carrier == null || target == null || carrier == target) return false;
            bool dead = target.Life.Value == LifeState.Dead;
            bool hands = carrier.Life.Value == LifeState.Alive && !carrier.Busy.Value && carrier.Seat.Value < 0 && !carrier.HoldsPaMic
                         && !carrier.CarriesMapRadio && !carrier.ExtBlocksHands && !carrier.BodyCarryBusy && !carrier.HealingCarried;
            // Hiders carry injured / downed hiders and dead bodies; the seeker only dead bodies (no consent for a body).
            if (!(carrier.IsHider || (carrier.IsSeeker && dead))) return false;
            if (!hands) { why = "Masz zajęte ręce."; return false; }
            if (carrier.IsHider && (carrier.Limping.Value || carrier.Severed.Value != 0)) { why = "Jesteś za słaby, żeby kogoś nieść."; return false; }
            if (carrier.IsSeeker && mm.Now < carrier.BlindUntil.Value) return false;
            if (!target.IsHider || target.Busy.Value || target.Seat.Value >= 0) return false;
            if (target.BodyCarryBusy) { why = dead ? "Ktoś już niesie to ciało." : $"{target.Nick} jest już niesiony."; return false; }
            if (!NeedsCarry(target)) { why = $"{target.Nick} może iść sam."; return false; }
            // Measured to the body itself (a ragdoll may have slid away from where the player died).
            Vector3 d = (dead ? target.ChestPosition - Vector3.up * 0.3f : target.transform.position) - carrier.transform.position;
            if (Mathf.Abs(d.y) > 1.1f) return false;
            d.y = 0f;
            if (d.magnitude > Tuning.RescueRange + 0.6f) { why = dead ? "Ciało jest za daleko." : $"{target.Nick} jest za daleko."; return false; }
            return true;
        }

        /// <summary>Dead (a body), downed, a leg cut off or wounded: may be carried.</summary>
        public static bool NeedsCarry(NetPlayer p) =>
            p != null && p.IsHider && (p.Life.Value == LifeState.Dead || p.Life.Value == LifeState.Downed || (p.Life.Value == LifeState.Alive &&
                                       (p.Limping.Value || p.Wounded.Value || (p.Severed.Value & Limb.Legs) != 0)));

        /// <summary>Server: both agreed - start the lift.</summary>
        public void ServerStartCarry(NetPlayer target)
        {
            if (!IsServer) return;
            if (!CanCarry(this, target, out string why))
            {
                if (!string.IsNullOrEmpty(why)) ToastRpc(why, 2.5f);
                return;
            }
            var mm = MatchManager.Instance;
            bool dead = target.Life.Value == LifeState.Dead;
            bool floor = dead || target.Life.Value == LifeState.Downed;
            bool mirror;
            if (floor)
            {
                target.BodyLieFrame(out var pelvis, out var head);
                mirror = BodyCarryTimeline.PickMirrorLying(transform.position, pelvis, head);
            }
            else mirror = BodyCarryTimeline.PickMirror(transform.position, target.transform.position, target.transform.rotation, false);
            target.Reviving.Value = false;
            if (IsSeeker) Held.Value = HeldItem.None; // the weapon goes away (the hotbar draws it again afterwards)
            CarryVar.Value = new BodyCarryPack
            {
                Target = target.NetworkObjectId,
                Phase = (byte)BodyCarryPhase.Lift,
                Flags = (byte)((mirror ? 1 : 0) | (floor ? 2 : 0) | (dead ? 8 : 0)),
                Start = mm.Now,
            };
            if (dead) ToastRpc($"Niesiesz ciało: {target.Nick}. B - odłóż.", 3f);
            else
            {
                ToastRpc($"Niesiesz: {target.Nick}. B - odłóż.", 3f);
                target.ToastRpc($"{Nick} niesie cię na rękach. Spacja - poproś o odłożenie.", 4f);
            }
            Debug.Log($"[Carry] {Nick} lifts {(dead ? "the body of " : "")}{target.Nick} ({(floor ? "from the floor" : "standing")}, {(mirror ? "mirrored" : "head left")})");
        }

        /// <summary>Either player asks to end the carry (B / Space): gentle put down once the body is up.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCarryPutDownRpc()
        {
            var carrier = IsCarryingBody ? this : CarriedBy;
            if (carrier == null) return;
            var v = carrier.CarryVar.Value;
            if (v.PhaseValue != BodyCarryPhase.Hold) return;
            var target = carrier.CarriedPlayer;
            if (target != this && carrier != this) return;
            carrier.ServerEndCarry(BodyCarryPhase.PutDown, target);
        }

        /// <summary>Server: starts putting down / dropping, with the spot the body ends up on.</summary>
        private void ServerEndCarry(BodyCarryPhase phase, NetPlayer target)
        {
            var v = CarryVar.Value;
            if (!v.Active || v.PhaseValue == BodyCarryPhase.PutDown || v.PhaseValue == BodyCarryPhase.Drop) return;
            bool standing = phase == BodyCarryPhase.PutDown && target != null && target.Life.Value == LifeState.Alive;
            float room = FreeAhead(1.3f);
            float scale = Mathf.Clamp((room - 0.3f) / 0.5f, 0.15f, 1f);
            BodyCarryTimeline.EndFrame(transform.position, transform.rotation, v.Mirror, standing, scale, out var pos, out var rot);
            pos.y = FloorAt(pos, transform.position.y);
            v.Phase = (byte)phase;
            v.Start = MatchManager.Instance != null ? MatchManager.Instance.Now : 0d;
            v.Flags = (byte)((v.Flags & 3) | (standing ? 4 : 0));
            v.EndPos = pos;
            v.EndYaw = rot.eulerAngles.y;
            CarryVar.Value = v;
            Debug.Log($"[Carry] {Nick} {(phase == BodyCarryPhase.Drop ? "drops" : "puts down")} {(target != null ? target.Nick : "?")} " +
                      $"({(standing ? "onto the feet" : "onto the floor")}, room {room:0.00} m)");
        }

        private void ServerCarryAbort(string why)
        {
            if (!CarryVar.Value.Active) return;
            Debug.Log($"[Carry] {Nick}: carry ended ({why})");
            CarryVar.Value = default;
        }

        /// <summary>Server, every frame on every player: advances / ends this player's carry.</summary>
        private void ServerCarryTick()
        {
            var v = CarryVar.Value;
            if (!v.Active) return;
            var mm = MatchManager.Instance;
            var target = Find(v.Target);
            if (mm == null || target == null || !target.IsSpawned) { ServerCarryAbort("carried player gone"); return; }
            if (mm.Phase.Value != MatchPhase.Seeking && mm.Phase.Value != MatchPhase.Hiding) { ServerCarryAbort("round over"); return; }
            if (Busy.Value || target.Busy.Value) { ServerCarryAbort("kill sequence"); return; }
            double now = mm.Now;
            var phase = v.PhaseValue;
            if (phase == BodyCarryPhase.Lift || phase == BodyCarryPhase.Hold)
            {
                bool hit = Life.Value != LifeState.Alive || (IsHider && (Limping.Value || Severed.Value != 0)) || (IsSeeker && now < BlindUntil.Value);
                if (v.DeadBody && target.Life.Value != LifeState.Dead) { ServerCarryAbort("the body came back to life"); return; }
                if (hit || (!v.DeadBody && target.Life.Value == LifeState.Dead))
                {
                    ServerEndCarry(BodyCarryPhase.Drop, target);
                    return;
                }
            }
            float dur = BodyCarryTimeline.Duration(phase, v.FromFloor, v.EndStanding);
            if (phase == BodyCarryPhase.Lift && now >= v.Start + dur)
            {
                v.Phase = (byte)BodyCarryPhase.Hold;
                v.Start = now;
                CarryVar.Value = v;
            }
            else if ((phase == BodyCarryPhase.PutDown || phase == BodyCarryPhase.Drop) && now >= v.Start + dur + 0.15)
            {
                CarryVar.Value = default;
                Debug.Log($"[Carry] {Nick}: {target.Nick} is down, carry over");
            }
        }

        private static readonly RaycastHit[] _carryHits = new RaycastHit[12];

        /// <summary>Free distance in front of the carrier at knee height (walls, cars; players ignored).</summary>
        private float FreeAhead(float max)
        {
            Vector3 from = transform.position + Vector3.up * 0.45f;
            int n = Physics.SphereCastNonAlloc(from, 0.12f, transform.forward, _carryHits, max, CarryMask, QueryTriggerInteraction.Ignore);
            float best = max;
            for (int i = 0; i < n; i++)
            {
                var h = _carryHits[i];
                if (h.collider == null || h.collider.GetComponentInParent<NetPlayer>() != null) continue;
                if (h.distance > 0f && h.distance < best) best = h.distance;
            }
            return best;
        }

        private static float FloorAt(Vector3 pos, float fallback)
        {
            if (Physics.Raycast(new Vector3(pos.x, fallback + 0.6f, pos.z), Vector3.down, out var hit, 1.3f, CarryMask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<NetPlayer>() == null && Mathf.Abs(hit.point.y - fallback) < 0.45f)
                return hit.point.y;
            return fallback;
        }

        private static int CarryMask => ~(GameLayers.RagdollMask | (1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 2));
    }
}
