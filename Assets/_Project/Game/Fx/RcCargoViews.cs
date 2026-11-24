using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The item an RC car carries (<see cref="MatchManager.RcCargoVar"/>), on every peer: the item's own model shrunk
    /// to fit the roof, riding on the car body (tilts with it), popping on with a short overshoot and the pickup sound,
    /// a faint bob so it reads as a load. Hidden with the car (carried, on the shelf).
    /// </summary>
    public static class RcCargoViews
    {
        private const float MaxSize = 0.17f;
        private static readonly HotbarItem[] Shown = new HotbarItem[RcCar.Count];
        private static readonly Transform[] Models = new Transform[RcCar.Count];
        private static readonly float[] Born = new float[RcCar.Count];
        private static readonly float[] Roof = { -1f, -1f };

        public static void Sync(bool inGame)
        {
            var mm = MatchManager.Instance;
            for (int i = 0; i < RcCar.Count; i++)
            {
                var car = RcCar.Peek(i);
                var want = inGame && mm != null ? mm.RcCargoOf(i) : HotbarItem.None;
                if (car == null) { Shown[i] = HotbarItem.None; Models[i] = null; continue; }
                if (want != Shown[i] || (want != HotbarItem.None && Models[i] == null))
                {
                    if (Models[i] != null) Object.Destroy(Models[i].gameObject);
                    Models[i] = null;
                    bool popped = want != HotbarItem.None && Shown[i] == HotbarItem.None;
                    Shown[i] = want;
                    if (want != HotbarItem.None)
                    {
                        Models[i] = Build(car, i, want);
                        Born[i] = Time.time;
                        if (popped && car.View != RcCar.ViewMode.Hidden && car.View != RcCar.ViewMode.Shelf)
                            GameAudio.PlayAt(GameSfx.MedkitPickup, car.transform.position, 0.7f);
                    }
                }
                var m = Models[i];
                if (m == null) continue;
                bool visible = car.View != RcCar.ViewMode.Hidden && car.View != RcCar.ViewMode.Shelf;
                if (m.gameObject.activeSelf != visible) m.gameObject.SetActive(visible);
                if (!visible) continue;
                // Pop on (0.3 s, overshoot), then a slow bob.
                float t = (Time.time - Born[i]) / 0.3f;
                float s = t >= 1f ? 1f : 1f + 1.7f * Mathf.Pow(t - 1f, 3f) + 0.7f * Mathf.Pow(t - 1f, 2f);
                m.localScale = Vector3.one * Mathf.Max(0.01f, s);
                m.localPosition = new Vector3(0f, Roof[i] + 0.004f * Mathf.Sin(Time.time * 3.1f + i), -0.01f);
            }
        }

        private static Transform Build(RcCar car, int i, HotbarItem item)
        {
            var def = ItemDefs.Get(item);
            var root = car.Parts != null && car.Parts.Root != null ? car.Parts.Root : car.transform;
            if (Roof[i] < 0f)
            {
                // Top of the body (the antenna is thin and tall: only the larger parts count).
                float top = 0.1f;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var b = r.bounds;
                    if (b.size.x < 0.05f && b.size.z < 0.05f) continue;
                    top = Mathf.Max(top, root.InverseTransformPoint(b.max).y);
                }
                Roof[i] = top;
            }
            var pivot = new GameObject("Cargo").transform;
            pivot.gameObject.layer = RcCar.CarLayer;
            // Built at the origin (world box = model box), then hung on the car.
            var holder = new GameObject("Model").transform;
            holder.SetParent(pivot, false);
            if (def?.ModelFor != null) def.ModelFor(holder, 0);
            else def?.Model?.Invoke(holder);
            // Fit: largest side to MaxSize, lying flat across the car, bottom on the roof.
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                r.gameObject.layer = RcCar.CarLayer;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            foreach (var c in holder.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var l in holder.GetComponentsInChildren<Light>(true)) Object.Destroy(l);
            float big = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float k = big > 1e-4f ? Mathf.Min(1f, MaxSize / big) : 1f;
            holder.localScale = Vector3.one * k;
            holder.localPosition = new Vector3(-bounds.center.x * k, -bounds.min.y * k, -bounds.center.z * k);
            pivot.SetParent(root, false);
            pivot.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Debug.Log($"[RcCargo] view car {i}: {item} (model {bounds.size}, scale {k:0.00}, roof {Roof[i]:0.000})");
            return pivot;
        }
    }
}
