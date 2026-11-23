using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    public enum KillWeapon : byte { Knife, Saw, Glock }

    /// <summary>
    /// Server-only statistics of the current round, for the awards shown in the outro (<see cref="AwardBook"/>).
    /// Every hook is a static one-liner that does nothing on clients or outside a round, so game code can call it
    /// without checks: <see cref="Hit"/> (seeker attacks), <see cref="Healed"/>, <see cref="Rescued"/>,
    /// <see cref="Revived"/>, <see cref="ToyBroken"/>, <see cref="LegStun"/> (combat branch: leg throw stun),
    /// <see cref="Voice"/> (voice relay). Deaths, distance, survival time, RC driving and "seen by a seeker" are
    /// sampled by <see cref="Tick"/> from the network state, so they need no hooks.
    /// </summary>
    public sealed partial class RoundStats
    {
        public sealed class Entry
        {
            public ulong Id;
            public string Nick;
            public bool Seeker;
            public int KnifeKills, SawKills, GlockKills, Heals, Rescues, Revives, Toys, LegStuns;
            public float Distance, RcTime, VoiceTime, AliveTime, SeenTime;
            public double CaughtAt = -1d, DiedAt = -1d, FirstHitAt = -1d;
            public LifeState LastLife = LifeState.Alive;
            public Vector3 LastPos;
            public bool HasPos;
        }

        static RoundStats _cur;
        /// <summary>Statistics of the round being played (server), null otherwise.</summary>
        public static RoundStats Current => _cur;

        readonly Dictionary<ulong, Entry> _e = new();
        readonly Dictionary<ulong, (ulong by, KillWeapon w)> _lastHit = new();
        public double RoundStart, SeekStart = -1d, EndAt = -1d;
        float _seenTimer;
        public string FirstBloodNick;
        public ulong FirstBloodId = ulong.MaxValue;
        /// <summary>Order in which hiders died (last one = last stand).</summary>
        public readonly List<ulong> DeathOrder = new();

        public IEnumerable<Entry> Entries => _e.Values;

        /// <summary>Server: new round (players are placed in the map).</summary>
        public static void BeginRound(double now)
        {
            _cur = new RoundStats { RoundStart = now };
            foreach (var p in NetPlayer.All) if (p != null) _cur.Get(p);
            Debug.Log($"[Stats] round start, {_cur._e.Count} players");
        }

        /// <summary>Server: the round ended (the stats stay readable for the awards until the next round).</summary>
        public static void EndRound(double now)
        {
            if (_cur != null && _cur.EndAt < 0d) _cur.EndAt = now;
        }

        public static void Clear() => _cur = null;

        Entry Get(NetPlayer p)
        {
            if (!_e.TryGetValue(p.NetworkObjectId, out var e))
            {
                e = new Entry { Id = p.NetworkObjectId, Nick = p.Nick, Seeker = p.IsSeeker, LastLife = p.Life.Value };
                _e[p.NetworkObjectId] = e;
            }
            e.Nick = p.Nick;
            e.Seeker = p.IsSeeker;
            return e;
        }

        public Entry Find(ulong id) => _e.TryGetValue(id, out var e) ? e : null;

        static double Now => MatchManager.Instance != null ? MatchManager.Instance.Now : 0d;
        static bool On => _cur != null && _cur.EndAt < 0d;

        // ================================================================ hooks (server)

        /// <summary>A seeker's attack landed on a hider (wound, leg cut, kill sequence start, Glock hit).</summary>
        public static void Hit(NetPlayer seeker, NetPlayer victim, KillWeapon weapon)
        {
            if (!On || seeker == null || victim == null) return;
            var s = _cur.Get(seeker);
            var v = _cur.Get(victim);
            double now = Now;
            _cur._lastHit[victim.NetworkObjectId] = (seeker.NetworkObjectId, weapon);
            if (v.CaughtAt < 0d) v.CaughtAt = now;
            if (s.FirstHitAt < 0d) s.FirstHitAt = now;
            if (_cur.FirstBloodId == ulong.MaxValue)
            {
                _cur.FirstBloodId = seeker.NetworkObjectId;
                _cur.FirstBloodNick = seeker.Nick;
            }
        }

        /// <summary>A hider dressed somebody else's wounds (medkit on another player).</summary>
        public static void Healed(NetPlayer helper, NetPlayer target)
        {
            if (!On || helper == null || target == null || helper == target) return;
            _cur.Get(helper).Heals++;
        }

        /// <summary>A hider picked a downed one up (H).</summary>
        public static void Rescued(NetPlayer helper, NetPlayer target)
        {
            if (!On || helper == null || target == null || helper == target) return;
            _cur.Get(helper).Rescues++;
        }

        /// <summary>A hider brought a dead one back with adrenaline.</summary>
        public static void Revived(NetPlayer helper, NetPlayer target)
        {
            if (!On || helper == null || target == null || helper == target) return;
            _cur.Get(helper).Revives++;
        }

        /// <summary>A seeker destroyed a toy (RC car stomped / shot, phone stomped).</summary>
        public static void ToyBroken(NetPlayer seeker)
        {
            if (!On || seeker == null) return;
            _cur.Get(seeker).Toys++;
        }

        /// <summary>A thrown leg stunned a seeker (combat branch hook: call it where the stun starts).</summary>
        public static void LegStun(NetPlayer thrower, NetPlayer seeker)
        {
            if (!On || thrower == null) return;
            _cur.Get(thrower).LegStuns++;
        }

        /// <summary>Voice relay: one frame of <paramref name="seconds"/> sent by <paramref name="speaker"/>.</summary>
        public static void Voice(NetPlayer speaker, float seconds)
        {
            if (!On || speaker == null) return;
            var mm = MatchManager.Instance;
            if (mm == null || (mm.Phase.Value != MatchPhase.Hiding && mm.Phase.Value != MatchPhase.Seeking)) return;
            _cur.Get(speaker).VoiceTime += seconds;
        }

        /// <summary>Test switch -testoutro: fake numbers for one player.</summary>
        public static Entry TestEntry(NetPlayer p) => _cur?.Get(p);

        // ================================================================ sampling (server, every frame)

        public static void Tick(MatchManager mm, float dt)
        {
            if (!On || mm == null) return;
            _cur.Sample(mm, dt);
        }

        void Sample(MatchManager mm, float dt)
        {
            var ph = mm.Phase.Value;
            if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking) return;
            double now = mm.Now;
            if (ph == MatchPhase.Seeking && SeekStart < 0d) SeekStart = now;
            bool moving = !mm.IntroRunning; // nobody walks during the intro (and the clock of the round starts after it)

            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                var e = Get(p);
                var life = p.Life.Value;
                if (life != e.LastLife)
                {
                    if (life == LifeState.Dead && e.LastLife != LifeState.Dead) OnDied(p, e, now);
                    e.LastLife = life;
                }
                Vector3 pos = p.transform.position;
                if (moving && e.HasPos && life != LifeState.Dead)
                {
                    Vector3 d = pos - e.LastPos;
                    float flat = new Vector2(d.x, d.z).magnitude;
                    // Teleports (spawn, kill stand-off, elevators) and lying still do not count.
                    if (flat < 12f * Mathf.Max(dt, 0.02f) && flat > 0.004f && Mathf.Abs(d.y) < 0.8f) e.Distance += flat;
                }
                e.LastPos = pos;
                e.HasPos = true;
                if (p.IsHider && life != LifeState.Dead && moving) e.AliveTime += dt;
            }

            for (int i = 0; i < RcCar.Count; i++)
            {
                var s = mm.RcSlot(i);
                if (s.Mode != RcMode.Driving) continue;
                var h = NetPlayer.Find(s.Holder);
                if (h != null) Get(h).RcTime += dt;
            }

            _seenTimer += dt;
            if (_seenTimer >= 0.5f && ph == MatchPhase.Seeking)
            {
                SampleSeen(_seenTimer);
                _seenTimer = 0f;
            }
        }

        void OnDied(NetPlayer p, Entry e, double now)
        {
            e.DiedAt = now;
            if (e.CaughtAt < 0d) e.CaughtAt = now;
            if (p.IsHider) DeathOrder.Add(p.NetworkObjectId);
            if (_lastHit.TryGetValue(p.NetworkObjectId, out var h))
            {
                var by = Find(h.by);
                if (by != null)
                {
                    if (h.w == KillWeapon.Knife) by.KnifeKills++;
                    else if (h.w == KillWeapon.Saw) by.SawKills++;
                    else by.GlockKills++;
                }
            }
            Debug.Log($"[Stats] {p.Nick} died, credit {(h.by != 0 && Find(h.by) != null ? Find(h.by).Nick + " (" + h.w + ")" : "-")}");
        }

        static int SightMask => ~((1 << PlayerAvatarBinder.LocalBodyLayer) | GameLayers.RagdollMask | (1 << 2) | (1 << 9));

        /// <summary>Hiders in a seeker's field of view (same level, 16 m, 60 degrees, nothing in between).</summary>
        void SampleSeen(float step)
        {
            foreach (var h in NetPlayer.All)
            {
                if (h == null || !h.IsHider || h.Life.Value == LifeState.Dead) continue;
                Vector3 chest = h.transform.position + Vector3.up * 1.2f;
                foreach (var s in NetPlayer.All)
                {
                    if (s == null || !s.IsSeeker || s.Life.Value != LifeState.Alive) continue;
                    Vector3 eye = s.EyePosition;
                    Vector3 to = chest - eye;
                    if (Mathf.Abs(to.y) > 2f || to.magnitude > 16f) continue;
                    if (Vector3.Angle(s.transform.forward, new Vector3(to.x, 0f, to.z)) > 60f) continue;
                    if (Physics.Linecast(eye, chest, out var hit, SightMask, QueryTriggerInteraction.Ignore)
                        && hit.collider.GetComponentInParent<NetPlayer>() != h) continue;
                    Get(h).SeenTime += step;
                    break;
                }
            }
        }
    }
}
