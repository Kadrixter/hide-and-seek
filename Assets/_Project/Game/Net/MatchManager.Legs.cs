using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Cut-off legs on the server. A leg the chainsaw takes off a hider in a round (the first saw hit, the saw kills)
    /// becomes a dropped item (<see cref="HotbarItem.SeveredLeg"/>) falling from the hip, instead of a local loose part
    /// on every peer - so it lies in the same spot for everybody and any hider can pick it up. Thrown legs are flying
    /// dropped items too: every frame the server sweeps the leg's path against the seekers' head and body; a hit knocks
    /// the seeker out (<see cref="NetPlayer.ServerStartStun"/>, then immune for a while), the leg bounces off and falls
    /// next to him. A miss just lies where it landed.
    /// </summary>
    public partial class MatchManager
    {
        private sealed class LegFlight
        {
            public NetPlayer Thrower;
            public Vector3 Prev;
            public bool HasPrev;
            public float Born;
        }

        private readonly Dictionary<int, LegFlight> _legFlights = new();

        /// <summary>Test counters (server): thrown legs, hits, knock-outs.</summary>
        public static int LegThrows, LegHits, LegStuns;

        /// <summary>Middle of the leg's box below the cut (DropState.Pos is the box middle).</summary>
        private static readonly Vector3 LegBoxCenter = new Vector3(0f, (-SeveredLegModel.Length + 0.055f) * 0.5f, 0f);

        private void SpawnLegDrop(byte code, Vector3 center, Quaternion rot, Vector3 vel, Vector3 spin, Vector3 feet, out int id)
        {
            id = _nextDropId++;
            _dropAnchor[id] = feet;
            Drops.Add(new DropState { Id = id, Item = (byte)HotbarItem.SeveredLeg, Extra = code, Pos = center, Rot = rot, Vel = vel, Spin = spin });
        }

        private static Vector3 NearestSeekerPos(NetPlayer victim)
        {
            Vector3 from = victim.transform.position - victim.transform.forward;
            float near = float.MaxValue;
            foreach (var s in NetPlayer.All)
            {
                if (s == null || !s.IsSeeker) continue;
                float d = (s.transform.position - victim.transform.position).sqrMagnitude;
                if (d < near) { near = d; from = s.transform.position; }
            }
            return from;
        }

        /// <summary>Server: a leg came off <paramref name="victim"/> right now - it drops from the hip, away from the saw.</summary>
        public void ServerLegCut(NetPlayer victim, int limb, Vector3 awayFrom)
        {
            if (!IsServer || !SeveredLegs.ServerOwned(victim, limb)) return;
            var c = victim.Character;
            Vector3 hip = c != null && c.IsBuilt ? c.LimbJointPosition(limb) : victim.transform.position + Vector3.up * BlockyCharacter.HipHeight;
            Quaternion rot = c != null && c.IsBuilt && c.LimbJoint(limb) != null ? c.LimbJoint(limb).rotation : victim.transform.rotation;
            // The body of a remote player not where its root is (not built yet / just teleported): from the root instead.
            if ((hip - victim.transform.position).sqrMagnitude > 4f)
            {
                hip = victim.transform.position + Vector3.up * BlockyCharacter.HipHeight;
                rot = victim.transform.rotation;
            }
            Vector3 dir = hip - awayFrom;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : victim.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 vel = dir * Random.Range(0.6f, 1.6f) + side * Random.Range(-0.7f, 0.7f) + Vector3.up * Random.Range(0.7f, 1.6f);
            SpawnLegDrop(SeveredLegs.Code(victim, limb), hip + rot * LegBoxCenter, rot, vel, Random.onUnitSphere * 5f, victim.transform.position, out int id);
            Debug.Log($"[Legs] {victim.Nick}: {(limb == Limb.LegL ? "left" : "right")} leg off -> drop #{id} from the hip at {hip} (root {victim.transform.position}, body {(c != null ? c.transform.position : Vector3.zero)})");
        }

        /// <summary>Server: the legs a saw kill cuts off, on the same marks as the clients' animation.</summary>
        private IEnumerator ServerKillLegs(NetPlayer seeker, NetPlayer victim, KillMove move)
        {
            if (!KillMoves.IsSaw(move) || victim == null) yield break;
            int done = victim.Severed.Value;
            int a = (done & Limb.LegR) == 0 ? Limb.LegR : Limb.LegL; // the saw finisher's slots (BlockyCharacterActions.PrepareSawSlots)
            int[] slots = { a, a == Limb.LegR ? Limb.LegL : Limb.LegR, Limb.ArmL, Limb.ArmR };
            float t = 0f;
            foreach (var ev in KillMoves.Events(move))
            {
                int cut = ev.SawSlot >= 0 ? slots[Mathf.Clamp(ev.SawSlot, 0, 3)] : ev.Severs;
                if ((cut & Limb.Legs) == 0) continue;
                yield return new WaitForSeconds(Mathf.Max(0f, ev.Time - t));
                t = ev.Time;
                if (victim == null || !victim.IsSpawned) yield break;
                Vector3 from = seeker != null ? seeker.transform.position : NearestSeekerPos(victim);
                for (int bit = Limb.LegR; bit <= Limb.LegL; bit <<= 1)
                    if ((cut & bit) != 0 && (done & bit) == 0)
                    {
                        done |= bit;
                        ServerLegCut(victim, bit, from);
                    }
            }
        }

        // ================================================================ throwing

        /// <summary>A hider let go of the button: the leg leaves the inventory and flies after the swing.</summary>
        public void ServerThrowLeg(NetPlayer pl, Vector3 dir, float power)
        {
            var ph = Phase.Value;
            if (pl == null || !pl.IsHider || pl.Life.Value != LifeState.Alive || pl.Busy.Value) return;
            if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking) return;
            if (pl.SelectedItem != HotbarItem.SeveredLeg) return;
            var def = ItemDefs.Get(HotbarItem.SeveredLeg);
            if (def?.Take == null || !def.Take(pl, out byte code)) return;
            if (dir.sqrMagnitude < 0.01f || float.IsNaN(dir.x)) dir = pl.transform.forward;
            dir.Normalize();
            power = Mathf.Clamp01(power);
            pl.LegThrowRpc(power);
            LegThrows++;
            StartCoroutine(LaunchLeg(pl, code, dir, power));
        }

        private IEnumerator LaunchLeg(NetPlayer pl, byte code, Vector3 dir, float power)
        {
            yield return new WaitForSeconds(LegThrowTimeline.Release);
            if (pl == null || !pl.IsSpawned) yield break;
            Vector3 feet = pl.transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : pl.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            Vector3 chest = feet + Vector3.up * 1.3f;
            Vector3 hand = feet + Vector3.up * 1.62f + right * 0.14f + flat * 0.35f;
            Vector3 toHand = hand - chest;
            Vector3 start = ItemPlacement.ThrowStart(chest, toHand.normalized, toHand.magnitude, 0.12f, out var s, out _, pl.transform) ? s : chest;
            Vector3 vel = dir * LegThrowTimeline.Speed(power) + Vector3.up * LegThrowTimeline.Lift(power);
            Vector3 stump = (dir * 0.6f + Vector3.up * 0.8f).normalized;
            Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(right, stump).normalized, stump);
            SpawnLegDrop(code, start, rot, vel, right * LegThrowTimeline.Spin(power), feet, out int id);
            _legFlights[id] = new LegFlight { Thrower = pl, Born = Time.time };
            Debug.Log($"[Legs] {pl.Nick} threw a leg (power {power:0.00}, {vel.magnitude:0.0} m/s) -> drop #{id}");
        }

        /// <summary>Server, every frame: flying legs against the seekers.</summary>
        private void UpdateLegs()
        {
            if (!IsServer || _legFlights.Count == 0) return;
            List<int> gone = null;
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                var d = Drops[i];
                if (!_legFlights.TryGetValue(d.Id, out var f)) continue;
                if (d.Settled || Time.time - f.Born > 4f) { (gone ??= new List<int>()).Add(d.Id); continue; }
                if (!_dropViews.TryGetValue(d.Id, out var view) || view == null) continue;
                Vector3 pos = view.transform.position;
                if (!f.HasPrev) { f.Prev = pos; f.HasPrev = true; continue; }
                Vector3 step = pos - f.Prev;
                float speed = step.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
                if (speed > 2.5f && Phase.Value == MatchPhase.Seeking)
                {
                    foreach (var sk in NetPlayer.All)
                    {
                        if (sk == null || !sk.IsSeeker || sk.Life.Value != LifeState.Alive) continue;
                        int part = LegHitPart(f.Prev, pos, sk, out Vector3 at);
                        if (part == 0) continue;
                        Drops.RemoveAt(i);
                        _dropAnchor.Remove(d.Id);
                        (gone ??= new List<int>()).Add(d.Id);
                        OnLegHit(sk, f.Thrower, d, at, step.normalized, part, view.transform.rotation);
                        break;
                    }
                }
                f.Prev = pos;
            }
            if (gone != null) foreach (int id in gone) _legFlights.Remove(id);
        }

        /// <summary>0 = miss, 1 = legs (a glancing blow), 2 = body, 3 = head.</summary>
        private static int LegHitPart(Vector3 a, Vector3 b, NetPlayer sk, out Vector3 at)
        {
            const float legR = 0.13f;
            var c = sk.Character;
            Vector3 root = sk.transform.position;
            Vector3 pelvis = c != null && c.IsBuilt ? c.Pelvis.position : root + Vector3.up * BlockyCharacter.HipHeight;
            Vector3 neck = c != null && c.IsBuilt ? c.Torso.position + c.Torso.up * BlockyCharacter.NeckRest.y : root + Vector3.up * 1.35f;
            Vector3 headC = c != null && c.IsBuilt ? c.Head.position + c.Head.up * (4f * BlockyCharacter.PixelSize) : root + Vector3.up * 1.57f;
            at = b;
            if (SegSeg(a, b, headC, headC, out var p0, out _) < 0.27f + legR) { at = p0; return 3; }
            if (SegSeg(a, b, pelvis, neck, out var p1, out _) < 0.24f + legR) { at = p1; return 2; }
            if (SegSeg(a, b, root + Vector3.up * 0.1f, pelvis, out var p2, out _) < 0.17f + legR) { at = p2; return 1; }
            return 0;
        }

        private void OnLegHit(NetPlayer sk, NetPlayer thrower, DropState d, Vector3 at, Vector3 dir, int part, Quaternion rot)
        {
            LegHits++;
            bool stun = false;
            float immune = sk.StunImmuneLeft;
            if (part >= 2 && !sk.Busy.Value && immune <= 0f)
            {
                stun = true;
                LegStuns++;
                sk.ServerStartStun();
            }
            // Bounces off and falls down next to him (on his side of the hit).
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 bounce = -dir * Random.Range(0.8f, 1.5f) + Vector3.up * Random.Range(0.6f, 1.3f) + side * Random.Range(-0.7f, 0.7f);
            SpawnLegDrop(d.Extra, at - dir * 0.18f, rot, bounce, Random.onUnitSphere * 7f, sk.transform.position, out int id);
            sk.LegHitRpc(at, dir, stun);
            string who = thrower != null ? thrower.Nick : "?";
            Debug.Log($"[Legs] leg #{d.Id} from {who} hit {sk.Nick} ({(part == 3 ? "head" : part == 2 ? "body" : "legs")}) stun={stun} immune={immune:0.0} -> drop #{id}");
            if (thrower != null && thrower.IsSpawned)
            {
                if (stun) thrower.ToastRpc("Trafiony! Szukający leży ogłuszony.", 3f);
                else if (part >= 2 && immune > 0f) thrower.ToastRpc($"Trafiony, ale jeszcze się otrząsa - odporny przez {Mathf.CeilToInt(immune)} s.", 3f);
                else if (part == 1) thrower.ToastRpc("Tylko po nogach - celuj w głowę albo tułów.", 2.5f);
            }
        }

        /// <summary>Distance between segments ab and cd (closest points out).</summary>
        private static float SegSeg(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 pa, out Vector3 pc)
        {
            Vector3 d1 = b - a, d2 = d - c, r = a - c;
            float aa = Vector3.Dot(d1, d1), ee = Vector3.Dot(d2, d2), ff = Vector3.Dot(d2, r);
            float s, t;
            if (aa < 1e-6f && ee < 1e-6f) { s = t = 0f; }
            else if (aa < 1e-6f) { s = 0f; t = Mathf.Clamp01(ff / ee); }
            else
            {
                float cc = Vector3.Dot(d1, r);
                if (ee < 1e-6f) { t = 0f; s = Mathf.Clamp01(-cc / aa); }
                else
                {
                    float bb = Vector3.Dot(d1, d2), den = aa * ee - bb * bb;
                    s = den > 1e-6f ? Mathf.Clamp01((bb * ff - cc * ee) / den) : 0f;
                    t = (bb * s + ff) / ee;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-cc / aa); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((bb - cc) / aa); }
                }
            }
            pa = a + d1 * s;
            pc = c + d2 * t;
            return Vector3.Distance(pa, pc);
        }
    }
}
