using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Inventory on the server and items on the floor: keeps every player's slot layout in step with what they carry,
    /// Q (drop the selected slot), E on a dropped item, the Glock that does not fit, and the falling / resting views on
    /// every peer. Who may take what: <see cref="Inventory.CanPickUp"/> (hiders never take the seeker's weapons).
    /// </summary>
    public partial class MatchManager
    {
        public NetworkList<DropState> Drops;

        private readonly Dictionary<int, DroppedItemView> _dropViews = new();
        private int _nextDropId = 1000;
        /// <summary>RC slot holder while the car lies on the floor as a dropped item.</summary>
        public const ulong RcDropHolder = ulong.MaxValue - 1;

        private void InitDrops() => Drops = new NetworkList<DropState>();

        /// <summary>Called from Update on every instance.</summary>
        private void UpdateDrops()
        {
            UpdateDropViews();
            if (!IsServer) return;
            var ph = Phase.Value;
            if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking && ph != MatchPhase.Ended)
            {
                if (Drops.Count > 0) Drops.Clear();
                return;
            }
            if (ph != MatchPhase.Ended)
                foreach (var pl in NetPlayer.All)
                    if (pl != null) Inventory.ServerReconcile(pl, ServerSpill);
            for (int i = 0; i < Drops.Count; i++)
            {
                var d = Drops[i];
                if (d.Settled || !_dropViews.TryGetValue(d.Id, out var v) || v == null || !v.Resting(out var pos, out var rot)) continue;
                d.Settled = true;
                d.Pos = pos;
                d.Rot = rot;
                Drops[i] = d;
                Debug.Log($"[Drops] {d.Kind} #{d.Id} landed at {pos}");
            }
        }

        private void UpdateDropViews()
        {
            bool show = SceneManager.GetActiveScene().name == NetSession.GameScene && !RaceMode;
            var alive = new HashSet<int>();
            for (int i = 0; show && i < Drops.Count; i++)
            {
                var d = Drops[i];
                alive.Add(d.Id);
                if (!_dropViews.TryGetValue(d.Id, out var v) || v == null) _dropViews[d.Id] = DroppedItemView.Create(d);
                else if (d.Settled && !v.Settled) v.Settle(d.Pos, d.Rot);
            }
            List<int> remove = null;
            foreach (var kv in _dropViews)
                if (!alive.Contains(kv.Key)) { if (kv.Value != null) Destroy(kv.Value.gameObject); (remove ??= new List<int>()).Add(kv.Key); }
            if (remove != null) foreach (int id in remove) _dropViews.Remove(id);
        }

        /// <summary>Where a dropped item is right now on this peer (its view; the state before it exists).</summary>
        public Vector3 DropPosition(in DropState d) =>
            _dropViews.TryGetValue(d.Id, out var v) && v != null ? v.transform.position : d.Pos;

        public Transform DropView(int id) => _dropViews.TryGetValue(id, out var v) && v != null ? v.transform : null;

        // ================================================================ server

        private void SpawnDrop(HotbarItem item, byte extra, Vector3 pos, Quaternion rot, Vector3 vel)
        {
            var spin = new Vector3(Random.Range(-6f, 6f), Random.Range(-3f, 3f), Random.Range(-6f, 6f));
            Drops.Add(new DropState { Id = _nextDropId++, Item = (byte)item, Extra = extra, Pos = pos, Rot = rot, Vel = vel, Spin = spin });
        }

        /// <summary>Q: throws one of the selected slot's item in front of the player.</summary>
        public void ServerDropSlot(NetPlayer pl, byte slot)
        {
            var ph = Phase.Value;
            if (pl == null || slot >= Inventory.Slots || (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking)) return;
            if (pl.Life.Value != LifeState.Alive || pl.Busy.Value) return;
            var item = Inventory.At(pl, slot);
            var def = ItemDefs.Get(item);
            if (def == null) return;
            if (!def.Droppable) { pl.ToastRpc($"{def.Name} to twoja podstawowa broń - nie da się go wyrzucić.", 2.5f); return; }
            if (def.CustomDrop != null) { def.CustomDrop(pl); return; }
            if (def.Take == null || !def.Take(pl, out byte extra)) return;

            Vector3 fwd = pl.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 chest = pl.transform.position + Vector3.up * 1.15f;
            float reach = 0.5f;
            foreach (var h in Physics.SphereCastAll(chest, 0.14f, fwd, 0.7f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(pl.transform) && h.distance < reach + 0.2f) reach = Mathf.Max(0f, h.distance - 0.2f);
            Vector3 start = chest + fwd * reach;
            Vector3 vel = fwd * (reach > 0.3f ? 2.3f : 0.4f) + Vector3.up * 1.3f;
            SpawnDrop(item, extra, start, Quaternion.LookRotation(fwd) * Quaternion.Euler(0f, 90f, 0f), vel);
            Debug.Log($"[Drops] {pl.Nick} dropped {item} from slot {slot + 1}");
        }

        /// <summary>E on a dropped item.</summary>
        public void ServerPickupDrop(NetPlayer pl, int id)
        {
            if (pl == null) return;
            for (int i = 0; i < Drops.Count; i++)
            {
                var d = Drops[i];
                if (d.Id != id) continue;
                var def = ItemDefs.Get(d.Kind);
                if (def == null) return;
                if (!Inventory.CanPickUp(pl, d.Kind, Phase.Value))
                {
                    if (pl.IsHider && (def.Users & ItemUsers.Hider) == 0) pl.ToastRpc("To broń szukającego - nie podniesiesz jej.", 2.5f);
                    return;
                }
                if (Vector3.Distance(pl.transform.position + Vector3.up * 0.9f, DropPosition(d)) > Tuning.PickupRange + 1.6f) return;
                if (!Inventory.HasRoomFor(pl, d.Kind)) { pl.ToastRpc(Inventory.Count(pl, d.Kind) > 0 ? $"{def.Name}: masz już max." : Inventory.FullText, 2.5f); return; }
                if (def.Give == null || !def.Give(pl, d.Extra)) return;
                Drops.RemoveAt(i);
                pl.SfxRpc(GameSfx.MedkitPickup, 0.7f);
                int slot = Inventory.PredictSlot(pl, d.Kind);
                pl.ToastRpc(slot >= 0 ? $"{def.Name} (slot {slot + 1})" : def.Name, 2f);
                Debug.Log($"[Drops] {pl.Nick} picked up {d.Kind} #{id}");
                return;
            }
        }

        /// <summary>An item that does not fit any more lands next to the player.</summary>
        private void ServerSpill(NetPlayer pl, HotbarItem item)
        {
            var def = ItemDefs.Get(item);
            if (def?.Take == null || !def.Take(pl, out byte extra)) return;
            Vector3 side = pl.transform.right;
            SpawnDrop(item, extra, pl.transform.position + side * 0.45f + Vector3.up * 1.1f, Quaternion.Euler(0f, pl.transform.eulerAngles.y, 0f),
                side * 1.1f + Vector3.up * 0.6f);
            Debug.Log($"[Drops] {pl.Nick}: {item} did not fit, spilled on the floor");
        }

        /// <summary>The Glock phase begins and the seeker has no free slot: the gun drops next to him (minimap marker).</summary>
        private void ServerGlockToFloor(NetPlayer pl)
        {
            Vector3 side = pl.transform.right;
            SpawnDrop(HotbarItem.Glock, 0, pl.transform.position + side * 0.5f + Vector3.up * 1.2f, Quaternion.Euler(0f, pl.transform.eulerAngles.y + 90f, 0f),
                side * 1.3f + Vector3.up * 0.8f);
            pl.ToastRpc("Ekwipunek pełny - glock upadł obok ciebie (zaznaczony na mapie). Wyrzuć coś (Q) i podnieś go (E).", 7f);
            Debug.Log($"[Drops] {pl.Nick}: inventory full, the Glock fell on the floor");
        }

        // ================================================================ RC car as an inventory item

        /// <summary>Drop: a carried car leaves the inventory as a dropped item; a car standing on the floor is just let go.</summary>
        public bool ServerTakeRc(NetPlayer pl, out byte slot)
        {
            slot = 0;
            int i = RcSlotOf(pl);
            if (i < 0) return false;
            var s = RcSlot(i);
            switch (s.Mode)
            {
                case RcMode.Carried:
                    SetRc(i, RcMode.Carried, RcDropHolder, s.Pos, 0f);
                    slot = (byte)i;
                    return true;
                case RcMode.Parked:
                    SetRc(i, RcMode.Parked, ulong.MaxValue, s.Pos, s.Yaw);
                    pl.ToastRpc("Samochodzik zostaje na podłodze (każdy może go wziąć).", 2.5f);
                    return false;
                default:
                    pl.ToastRpc("Najpierw wyjdź z samochodzika (6).", 2f);
                    return false;
            }
        }

        public bool ServerGiveRc(NetPlayer pl, byte slot)
        {
            if (slot >= RcCar.Count || RcSlotOf(pl) >= 0) return false;
            var s = RcSlot(slot);
            if (s.Mode != RcMode.Carried || s.Holder != RcDropHolder) return false;
            SetRc(slot, RcMode.Carried, pl.NetworkObjectId, s.Pos, 0f);
            return true;
        }

        // ================================================================ client: E prompt

        /// <summary>Nearest dropped item in front of the eyes within reach that this player may take.</summary>
        public bool TryDropPrompt(Vector3 eye, Vector3 fwd, NetPlayer me, System.Func<Vector3, Transform, bool> visible,
            out int id, out Vector3 pos, out string text, out float dist)
        {
            id = 0;
            pos = default;
            text = null;
            dist = float.MaxValue;
            HotbarItem kind = HotbarItem.None;
            for (int i = 0; i < Drops.Count; i++)
            {
                var d = Drops[i];
                if (!Inventory.CanPickUp(me, d.Kind, Phase.Value)) continue;
                Vector3 p = DropPosition(d);
                float dd = Vector3.Distance(eye, p);
                if (dd >= dist || dd > Tuning.PickupRange + 1f || Vector3.Angle(fwd, p - eye) > 55f || !visible(p, DropView(d.Id))) continue;
                id = d.Id;
                pos = p;
                dist = dd;
                kind = d.Kind;
            }
            if (kind == HotbarItem.None) return false;
            var floorText = ItemDefs.Get(kind)?.FloorText;
            byte extra = 0;
            for (int i = 0; i < Drops.Count; i++) if (Drops[i].Id == id) extra = Drops[i].Extra;
            text = floorText != null ? floorText(me, extra) : Inventory.PickupText(me, kind, "E  podnieś: " + ItemDefs.Name(kind));
            return true;
        }

        // ================================================================ test hooks

        public void TestHintPing(NetPlayer pl) => HintRpc(pl.ChestPosition, pl.NetworkObjectId, 0, false, true);
    }
}
