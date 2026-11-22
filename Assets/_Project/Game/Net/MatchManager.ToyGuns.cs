using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>Toy slime blaster lying on the map (server-written).</summary>
    public struct ToyGunState : INetworkSerializable, IEquatable<ToyGunState>
    {
        public int Id;
        public Vector3 Pos;
        public float Yaw;
        public byte Ammo;
        public bool Taken;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Ammo);
            s.SerializeValue(ref Taken);
        }

        public bool Equals(ToyGunState o) => Id == o.Id && Taken == o.Taken && Ammo == o.Ammo && Pos == o.Pos && Yaw == o.Yaw;
    }

    /// <summary>
    /// Toy slime blasters: 1-2 per round on the floor next to racks / fences (different levels, never in the security
    /// rooms, away from the rods), picked up with E by alive hiders. The server simulates every goo blob (ballistic arc,
    /// level and seeker capsule tests); a hit seeker is covered in goo for <see cref="ToyTuning.GooTime"/> s: slowed and
    /// no knife / chainsaw. Every client draws the blob (<see cref="ToyGooFx"/>) from the same start values.
    /// </summary>
    public partial class MatchManager
    {
        public NetworkList<ToyGunState> ToyGuns;

        private readonly Dictionary<int, GameObject> _toyViews = new();
        private readonly Dictionary<ulong, float> _toyNextShot = new();
        private readonly RaycastHit[] _toyHits = new RaycastHit[16];
        private bool _toyGunsSpawned;
        private int _toyShotSeq;

        private void InitToyGuns() => ToyGuns = new NetworkList<ToyGunState>();

        /// <summary>Called from Update on every instance.</summary>
        private void UpdateToyGuns()
        {
            var local = NetPlayer.Local;
            if (local != null && local.GetComponent<ToyGunControl>() == null) local.gameObject.AddComponent<ToyGunControl>();
            GooOverlay.Ensure();
            UpdateToyViews();
            if (!IsServer) return;

            var ph = Phase.Value;
            if (ph == MatchPhase.Lobby || ph == MatchPhase.Loading)
            {
                _toyGunsSpawned = false;
                if (ToyGuns.Count > 0) ToyGuns.Clear();
                foreach (var pl in NetPlayer.All)
                {
                    if (pl == null) continue;
                    if (pl.ToyAmmo.Value != 0) pl.ToyAmmo.Value = 0;
                    if (pl.GooUntil.Value != 0) pl.GooUntil.Value = 0;
                }
                return;
            }
            // After the rods (they pick their spots first, the guns keep away from them).
            if (!_toyGunsSpawned && _rodsSpawned && ph == MatchPhase.Hiding && !RaceMode && SceneManager.GetActiveScene().name == NetSession.GameScene)
            {
                _toyGunsSpawned = true;
                SpawnToyGuns(UnityEngine.Random.Range(ToyTuning.MinGuns, ToyTuning.MaxGuns + 1));
            }
        }

        /// <summary>Server: places <paramref name="count"/> guns on the floor (also used by tests).</summary>
        public void SpawnToyGuns(int count)
        {
            ToyGuns.Clear();
            var picked = new List<RodSpots.Spot>();
            for (int attempt = 0; attempt < 6 && picked.Count < count; attempt++)
                foreach (var s in RodSpots.Pick(count + 2, 20f))
                {
                    if (picked.Count >= count) break;
                    bool ok = true;
                    foreach (var r in Rods) if (Vector3.Distance(r.Base, s.Base) < 5f) ok = false;
                    foreach (var q in picked) if (Vector3.Distance(q.Base, s.Base) < 20f || Mathf.Abs(q.Base.y - s.Base.y) < 1.5f && picked.Count + 1 < count) ok = false;
                    if (ok) picked.Add(s);
                }
            for (int i = 0; i < picked.Count; i++)
            {
                var s = picked[i];
                Vector3 outDir = s.Base - new Vector3(s.Top.x, s.Base.y, s.Top.z);
                outDir = outDir.sqrMagnitude > 1e-4f ? outDir.normalized : Vector3.forward;
                Vector3 pos = s.Base + outDir * 0.22f;
                ToyGuns.Add(new ToyGunState { Id = 300 + i, Pos = pos, Yaw = UnityEngine.Random.Range(0f, 360f), Ammo = ToyTuning.Shots });
                Debug.Log($"[ToyGun] gun {300 + i} at {pos} next to {s.Near}");
            }
        }

        private void UpdateToyViews()
        {
            var alive = new HashSet<int>();
            bool inGame = SceneManager.GetActiveScene().name == NetSession.GameScene;
            for (int i = 0; inGame && i < ToyGuns.Count; i++)
            {
                var g = ToyGuns[i];
                if (g.Taken) continue;
                alive.Add(g.Id);
                if (!_toyViews.TryGetValue(g.Id, out var go) || go == null) _toyViews[g.Id] = ToyGunView.Create(g.Pos, g.Yaw, g.Ammo / (float)ToyTuning.Shots);
            }
            List<int> remove = null;
            foreach (var kv in _toyViews)
                if (!alive.Contains(kv.Key)) { if (kv.Value != null) Destroy(kv.Value); (remove ??= new List<int>()).Add(kv.Key); }
            if (remove != null) foreach (int id in remove) _toyViews.Remove(id);
        }

        /// <summary>Client: nearest gun in front of the eyes within reach (E prompt).</summary>
        public bool TryToyGunPrompt(Vector3 eye, Vector3 fwd, NetPlayer me, out int id, out Vector3 pos, out string text, out float dist)
        {
            id = 0;
            pos = default;
            text = null;
            dist = float.MaxValue;
            for (int i = 0; i < ToyGuns.Count; i++)
            {
                var g = ToyGuns[i];
                if (g.Taken) continue;
                Vector3 mp = g.Pos + Vector3.up * 0.1f;
                float d = Vector3.Distance(eye, mp);
                if (d >= dist || d > Tuning.PickupRange + 1f || Vector3.Angle(fwd, mp - eye) > 55f) continue;
                id = g.Id;
                pos = mp;
                dist = d;
            }
            if (dist == float.MaxValue) return false;
            text = me.HasToyGun ? "E  (masz już pistolecik)" : Inventory.PickupText(me, HotbarItem.ToyGun, "E  podnieś pistolecik z glutem");
            return true;
        }

        public void ServerPickupToyGun(NetPlayer pl, int id)
        {
            if (pl == null || !pl.IsAliveHider || pl.Busy.Value) return;
            if (Phase.Value != MatchPhase.Seeking && Phase.Value != MatchPhase.Hiding) return;
            if (pl.HasToyGun) { pl.ToastRpc("Masz już pistolecik.", 2f); return; }
            if (!Inventory.HasRoomFor(pl, HotbarItem.ToyGun)) { pl.ToastRpc(Inventory.FullText, 2.5f); return; }
            for (int i = 0; i < ToyGuns.Count; i++)
            {
                var g = ToyGuns[i];
                if (g.Id != id || g.Taken) continue;
                if (Vector3.Distance(pl.transform.position, g.Pos) > Tuning.PickupRange + 1.5f) return;
                g.Taken = true;
                ToyGuns[i] = g;
                int slot = Inventory.PredictSlot(pl, HotbarItem.ToyGun) + 1;
                pl.ToyAmmo.Value = (byte)Mathf.Max(1, (int)g.Ammo);
                pl.SfxRpc(GameSfx.MedkitPickup, 0.7f);
                pl.ToastRpc($"Pistolecik z glutem ({g.Ammo} strz.): {slot} - do ręki, LPM - strzał. Gra na cały parking!", 4.5f);
                Debug.Log($"[ToyGun] {pl.Nick} picked up gun {id} ({g.Ammo} shots)");
                return;
            }
        }

        /// <summary>Server: the gun is thrown on the floor like any dropped item (Q; the shots left travel with it).</summary>
        public void ServerDropToyGun(NetPlayer pl)
        {
            int slot = Inventory.SlotOf(pl, HotbarItem.ToyGun);
            if (slot >= 0) ServerDropSlot(pl, (byte)slot);
        }

        public void ServerToyShot(NetPlayer pl, Vector3 origin, Vector3 dir)
        {
            if (pl == null || !pl.IsAliveHider || !pl.HasToyGun || pl.Busy.Value) return;
            if (Phase.Value != MatchPhase.Seeking && Phase.Value != MatchPhase.Hiding) return;
            if (_toyNextShot.TryGetValue(pl.NetworkObjectId, out float next) && Time.time < next) return;
            _toyNextShot[pl.NetworkObjectId] = Time.time + ToyTuning.FireInterval * 0.85f;
            if (Vector3.Distance(origin, pl.EyePosition) > 1.5f) origin = pl.EyePosition;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : pl.transform.forward;
            Vector3 vel = dir * ToyTuning.ShotSpeed;
            int shotId = ++_toyShotSeq;
            pl.ToyAmmo.Value--;
            pl.ToyShotRpc(shotId, origin, vel);
            if (pl.ToyAmmo.Value == 0) pl.ToyGunEmptyRpc();
            StartCoroutine(ToyShotRoutine(pl, shotId, origin, vel));
        }

        private IEnumerator ToyShotRoutine(NetPlayer shooter, int shotId, Vector3 pos, Vector3 vel)
        {
            float t = 0f;
            int mask = ~((1 << 2) | (1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9));
            Transform ignore = shooter != null ? shooter.transform : null;
            while (t < ToyTuning.ShotMaxTime)
            {
                yield return null;
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                t += dt;
                Vector3 next = pos + vel * dt + 0.5f * Vector3.down * ToyTuning.ShotGravity * dt * dt;
                vel += Vector3.down * ToyTuning.ShotGravity * dt;
                Vector3 seg = next - pos;
                float len = seg.magnitude;

                // Seeker body: analytic capsule (players carry no colliders for others).
                NetPlayer victim = null;
                float vt = float.MaxValue;
                foreach (var o in NetPlayer.All)
                {
                    if (o == null || !o.IsSeeker || o.Life.Value != LifeState.Alive) continue;
                    float h = Mathf.Lerp(1.7f, 1.1f, o.Motor != null ? o.Motor.CrouchAmount : 0f);
                    Vector3 a = o.transform.position + Vector3.up * 0.25f, b = o.transform.position + Vector3.up * h;
                    float f = SegmentCapsule(pos, next, a, b, 0.38f + ToyTuning.ShotRadius);
                    if (f >= 0f && f < vt) { vt = f; victim = o; }
                }

                // Level.
                float lt = float.MaxValue;
                RaycastHit best = default;
                if (len > 1e-4f)
                {
                    int n = Physics.SphereCastNonAlloc(pos, ToyTuning.ShotRadius * 0.6f, seg / len, _toyHits, len, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        var hh = _toyHits[i];
                        if (hh.collider == null || (ignore != null && hh.transform.IsChildOf(ignore))) continue;
                        if (hh.collider.GetComponentInParent<NetPlayer>() != null) continue;
                        if (hh.distance <= 0f && hh.point == Vector3.zero) continue;
                        float f = hh.distance / len;
                        if (f < lt) { lt = f; best = hh; }
                    }
                }

                if (victim != null && vt <= lt)
                {
                    Vector3 hp = Vector3.Lerp(pos, next, vt);
                    victim.GooUntil.Value = Now + ToyTuning.GooTime;
                    shooter?.ToyHitRpc(shotId, hp, -seg.normalized, victim.NetworkObjectId);
                    Debug.Log($"[ToyGun] shot {shotId} hit seeker {victim.Nick} at {hp} after {t:0.00} s");
                    yield break;
                }
                if (lt < float.MaxValue)
                {
                    shooter?.ToyHitRpc(shotId, best.point, best.normal, ulong.MaxValue);
                    Debug.Log($"[ToyGun] shot {shotId} hit {best.collider.name} at {best.point} after {t:0.00} s");
                    yield break;
                }
                pos = next;
                if (pos.y < -20f) break;
            }
            shooter?.ToyHitRpc(shotId, pos, Vector3.up, ulong.MaxValue - 1);
        }

        /// <summary>Fraction 0..1 along p0-p1 where the segment enters the capsule a-b (radius r), or -1.</summary>
        private static float SegmentCapsule(Vector3 p0, Vector3 p1, Vector3 a, Vector3 b, float r)
        {
            const int Steps = 6;
            for (int i = 0; i <= Steps; i++)
            {
                float f = i / (float)Steps;
                Vector3 p = Vector3.Lerp(p0, p1, f);
                Vector3 ab = b - a;
                float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                if ((a + ab * k - p).sqrMagnitude <= r * r) return f;
            }
            return -1f;
        }
    }
}
