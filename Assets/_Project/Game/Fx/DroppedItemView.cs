using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// A dropped item on every peer: the item's model in a box collider with a rigidbody, thrown from the
    /// <see cref="DropState"/> start, landing with the item's sound (players never collide with it). Once the server
    /// has the resting pose the body freezes, eases onto it and stops colliding. Faint glow so it can be found in the dark.
    /// </summary>
    public class DroppedItemView : MonoBehaviour
    {
        public int Id { get; private set; }
        public HotbarItem Item { get; private set; }
        public bool Settled { get; private set; }

        /// <summary>Half height of the item's box (lifted by this much when the server puts it on the floor).</summary>
        public float HalfHeight => _col != null ? _col.size.y * 0.5f : 0.1f;

        /// <summary>Half of the item's smallest box size.</summary>
        public float MinHalfExtent => _col != null ? Mathf.Min(_col.size.x, Mathf.Min(_col.size.y, _col.size.z)) * 0.5f : 0.02f;

        /// <summary>Test counter: landing sounds played on this peer.</summary>
        public static int Landings;

        private static readonly bool TestLog = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testinv") >= 0;

        private Rigidbody _rb;
        private BoxCollider _col;
        private Light _glow;
        private ItemDef _def;
        private float _born, _calmSince = -1f, _lastHit = -10f, _glowBase, _phase;
        private Vector3 _start;
        private Vector3 _fromP, _toP;
        private Quaternion _fromR, _toR;
        private float _ease = 1f;
        private float _nextIgnore;
        private readonly HashSet<NetPlayer> _ignored = new();

        public static DroppedItemView Create(DropState d)
        {
            var def = ItemDefs.Get(d.Kind);
            var root = new GameObject("Dropped_" + d.Kind);
            var view = root.AddComponent<DroppedItemView>();
            view.Id = d.Id;
            view.Item = d.Kind;
            view._def = def;
            view._born = Time.time;
            view._start = d.Pos;
            view._phase = d.Id * 0.73f;

            // Model centred on the root, collider round its bounds.
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            def?.Model?.Invoke(holder);
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                // Only the solid parts (a chainsaw's exhaust particles would stretch the box far away).
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.bounds.extents.magnitude > 3f) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            holder.localPosition = -b.center;
            view._col = root.AddComponent<BoxCollider>();
            view._col.size = Vector3.Max(b.size, Vector3.one * 0.05f);
            if (TestLog) Debug.Log($"[Drops] view {d.Kind} #{d.Id}: box {b.size} (model offset {-b.center})");

            // The body is created only once the root stands at the start pose (an interpolated rigidbody keeps the
            // pose it was created with otherwise).
            root.transform.SetPositionAndRotation(d.Pos, d.Rot);
            view._rb = root.AddComponent<Rigidbody>();
            view._rb.position = d.Pos;
            view._rb.rotation = d.Rot;
            view._rb.mass = 1.5f;
            view._rb.angularDamping = 0.4f;
            view._rb.interpolation = RigidbodyInterpolation.Interpolate;
            view._rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (def != null && def.Glow.a > 0.01f)
            {
                var lg = new GameObject("Glow").transform;
                lg.SetParent(root.transform, false);
                lg.localPosition = Vector3.up * 0.3f;
                view._glow = lg.gameObject.AddComponent<Light>();
                view._glow.type = LightType.Point;
                view._glow.color = new Color(def.Glow.r, def.Glow.g, def.Glow.b);
                view._glow.range = 1.5f;
                view._glowBase = def.Glow.a;
                view._glow.intensity = def.Glow.a;
                view._glow.shadows = LightShadows.None;
            }

            view.IgnorePlayers();
            if (d.Settled) view.Settle(d.Pos, d.Rot, true);
            else
            {
                view._rb.linearVelocity = d.Vel;
                view._rb.angularVelocity = d.Spin;
            }
            return view;
        }

        /// <summary>Server: the body came to rest (or fell out of the world / took too long). Gives the resting pose.</summary>
        public bool Resting(out Vector3 pos, out Quaternion rot)
        {
            pos = transform.position;
            rot = transform.rotation;
            if (Settled) return false;
            float age = Time.time - _born;
            bool calm = _rb.IsSleeping() || (_rb.linearVelocity.sqrMagnitude < 0.003f && _rb.angularVelocity.sqrMagnitude < 0.02f);
            if (!calm) _calmSince = -1f;
            else if (_calmSince < 0f) _calmSince = Time.time;
            // Fell through a gap in the map, or left the garage: the server checks the spot and moves it back
            // next to the player (MatchManager.Drops / ItemPlacement.CheckRest).
            if (HideAndSeek.Map.MapInfo.HasGarage && !HideAndSeek.Map.MapInfo.InsideGarage(transform.position, -0.5f)) return true;
            if (transform.position.y < _start.y - 8f)
            {
                // Fell through a gap in the map: put it on the floor under the start point.
                pos = Physics.Raycast(_start + Vector3.up * 0.5f, Vector3.down, out var hit, 6f, ~HideAndSeek.Core.GameLayers.RagdollMask, QueryTriggerInteraction.Ignore)
                    ? hit.point + Vector3.up * (_col.size.y * 0.5f) : _start;
                rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                return true;
            }
            return (calm && age > 0.4f && Time.time - _calmSince > 0.3f) || age > 5f;
        }

        /// <summary>Freezes the body and eases it onto the server's resting pose.</summary>
        public void Settle(Vector3 pos, Quaternion rot, bool immediate = false)
        {
            Settled = true;
            _rb.isKinematic = true;
            _col.enabled = false; // no longer a solid (never blocks sound rays or prompts)
            _fromP = transform.position;
            _fromR = transform.rotation;
            _toP = pos;
            _toR = rot;
            _ease = immediate ? 1f : 0f;
            if (immediate) transform.SetPositionAndRotation(pos, rot);
        }

        private void Update()
        {
            if (_glow != null)
            {
                _glow.transform.position = transform.position + Vector3.up * 0.3f; // above the item, however it lies
                _glow.intensity = _glowBase * (0.8f + 0.2f * Mathf.Sin(Time.time * 2.2f + _phase));
            }
            if (Settled)
            {
                if (_ease < 1f)
                {
                    _ease = Mathf.Min(1f, _ease + Time.deltaTime / 0.3f);
                    float t = _ease * _ease * (3f - 2f * _ease);
                    transform.SetPositionAndRotation(Vector3.Lerp(_fromP, _toP, t), Quaternion.Slerp(_fromR, _toR, t));
                }
                return;
            }
            if (Time.time >= _nextIgnore) IgnorePlayers();
        }

        /// <summary>Players walk through dropped items (and never kick them around).</summary>
        private void IgnorePlayers()
        {
            _nextIgnore = Time.time + 0.5f;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || _ignored.Contains(p)) continue;
                _ignored.Add(p);
                foreach (var c in p.GetComponentsInChildren<Collider>(true))
                    if (c != null) Physics.IgnoreCollision(_col, c, true);
            }
        }

        private void OnCollisionEnter(Collision c)
        {
            if (Settled || _def == null) return;
            float v = c.relativeVelocity.magnitude;
            if (v < 0.7f || Time.time - _lastHit < 0.12f) return;
            bool first = _lastHit < 0f;
            _lastHit = Time.time;
            Vector3 at = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            float vol = _def.DropVolume * Mathf.Clamp01(v / 3.5f) * (first ? 1f : 0.6f);
            GameAudio.PlayAt(_def.DropSfx, at, Mathf.Max(0.12f, vol));
            Landings++;
        }
    }
}
