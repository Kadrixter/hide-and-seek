using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Visuals of the toy slime blaster on every client: the flying goo blob (same ballistic arc as the server, starting
    /// at the visible muzzle and merging into the real path), its dripping trail, the splat where it lands (decal +
    /// droplets + sound), the burst on a hit seeker and the empty gun thrown away. Sounds in <see cref="ToySounds"/>.
    /// </summary>
    public static class ToyGooFx
    {
        private static readonly Dictionary<int, ToyGooBlob> Blobs = new();
        private static readonly Queue<GameObject> Decals = new();
        private static Material _decalMat;

        public static void Shot(int id, Vector3 origin, Vector3 velocity, Vector3 visualStart)
        {
            ToySounds.Shot(visualStart);
            Burst(visualStart, velocity.normalized, 4, 1.6f, 0.025f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GooBlob";
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = ToyGunModel.GooMaterial;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var blob = go.AddComponent<ToyGooBlob>();
            blob.Init(id, origin, velocity, visualStart);
            if (Blobs.TryGetValue(id, out var old) && old != null) Object.Destroy(old.gameObject);
            Blobs[id] = blob;
        }

        public static void Hit(int id, Vector3 point, Vector3 normal, Transform victim, bool splat)
        {
            if (Blobs.TryGetValue(id, out var blob))
            {
                if (blob != null) Object.Destroy(blob.gameObject);
                Blobs.Remove(id);
            }
            if (!splat) return;
            ToySounds.Splat(point, victim != null);
            Burst(point, normal, victim != null ? 16 : 10, victim != null ? 2.6f : 2f, 0.035f);
            if (victim == null) Decal(point, normal, Random.Range(0.32f, 0.46f));
        }

        internal static void Forget(int id, ToyGooBlob blob)
        {
            if (Blobs.TryGetValue(id, out var b) && b == blob) Blobs.Remove(id);
        }

        /// <summary>Little goo drops flying off a point (gravity, shrinking).</summary>
        public static void Burst(Vector3 pos, Vector3 dir, int count, float speed, float size)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = (dir.normalized * 0.6f + Random.insideUnitSphere).normalized * speed * Random.Range(0.4f, 1f) + Vector3.up * 0.8f;
                Drop(pos, v, size * Random.Range(0.6f, 1.4f), Random.Range(0.35f, 0.7f));
            }
        }

        public static void Drop(Vector3 pos, Vector3 vel, float size, float life)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "GooDrop";
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ToyGunModel.GooMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.transform.rotation = Random.rotation;
            go.AddComponent<ToyGooDrop>().Init(vel, life);
        }

        private static void Decal(Vector3 point, Vector3 normal, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GooSplat";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = point + normal * 0.012f;
            go.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = DecalMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<ToyGooDrop>().InitDecal(40f, size);
            Decals.Enqueue(go);
            while (Decals.Count > 30) { var old = Decals.Dequeue(); if (old != null) Object.Destroy(old); }
        }

        private static Material DecalMaterial
        {
            get
            {
                if (_decalMat != null) return _decalMat;
                const int S = 64;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GooSplatTex" };
                var rnd = new System.Random(11);
                var lobes = new Vector3[9];
                for (int i = 0; i < lobes.Length; i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = i == 0 ? 0f : 0.25f + (float)rnd.NextDouble() * 0.45f;
                    lobes[i] = new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, i == 0 ? 0.42f : 0.08f + (float)rnd.NextDouble() * 0.12f);
                }
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    float f = 0f;
                    foreach (var l in lobes) f = Mathf.Max(f, 1f - Mathf.Sqrt((u - l.x) * (u - l.x) + (v - l.y) * (v - l.y)) / l.z);
                    float a = Mathf.Clamp01(f * 6f);
                    float shine = Mathf.Clamp01((f - 0.45f) * 3f) * Mathf.Clamp01(1f - Mathf.Abs(u + v + 0.35f) * 2.2f);
                    var c = Color.Lerp(new Color(0.2f, 0.62f, 0.08f), new Color(0.55f, 1f, 0.3f), Mathf.Clamp01(f * 1.4f));
                    c = Color.Lerp(c, new Color(0.9f, 1f, 0.8f), shine * 0.7f);
                    px[y * S + x] = new Color(c.r, c.g, c.b, a * 0.92f);
                }
                tex.SetPixels32(px);
                tex.Apply();
                _decalMat = new Material(Shader.Find("Sprites/Default")) { name = "GooSplat", mainTexture = tex };
                return _decalMat;
            }
        }

        /// <summary>The empty gun flies out of the hand, bounces once and lies there for a while.</summary>
        public static void ThrowEmpty(Vector3 pos, Vector3 fwd)
        {
            var root = new GameObject("EmptyToyGun");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.LookRotation(fwd);
            var parts = ToyGunModel.Create(root.transform, 0, true);
            ToyGunModel.SetLevel(parts, 0f);
            parts.Root.localPosition = new Vector3(0f, -0.1f, -0.15f);
            Vector3 v = fwd * 1.6f + Vector3.up * 2.2f + Vector3.Cross(Vector3.up, fwd) * 0.8f;
            root.AddComponent<ToyGooDrop>().InitThrown(v, 25f);
        }
    }

    /// <summary>One flying goo blob (client visual).</summary>
    public class ToyGooBlob : MonoBehaviour
    {
        private int _id;
        private Vector3 _origin, _vel, _offset;
        private float _t, _nextDrip;

        public void Init(int id, Vector3 origin, Vector3 vel, Vector3 visualStart)
        {
            _id = id;
            _origin = origin;
            _vel = vel;
            _offset = visualStart - origin;
            transform.position = visualStart;
            transform.localScale = Vector3.one * 0.1f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t > ToyTuning.ShotMaxTime + 0.6f) { ToyGooFx.Forget(_id, this); Destroy(gameObject); return; }
            float merge = Mathf.Clamp01(1f - _t / 0.15f);
            Vector3 p = _origin + _vel * _t + 0.5f * Vector3.down * ToyTuning.ShotGravity * _t * _t + _offset * merge;
            Vector3 v = _vel + Vector3.down * ToyTuning.ShotGravity * _t;
            transform.position = p;
            if (v.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(v);
            float w = Mathf.Sin(_t * 31f) * 0.18f;
            transform.localScale = new Vector3(0.1f * (1f + w), 0.1f * (1f - w), 0.13f);
            if (_t >= _nextDrip)
            {
                _nextDrip = _t + 0.045f;
                ToyGooFx.Drop(p - v.normalized * 0.06f, v * 0.15f, Random.Range(0.018f, 0.03f), 0.35f);
            }
        }
    }

    /// <summary>Goo drop / splat decal / thrown empty gun: simple motion and fade-out.</summary>
    public class ToyGooDrop : MonoBehaviour
    {
        private Vector3 _vel, _scale0;
        private float _life, _age, _size;
        private int _mode; // 0 drop, 1 decal, 2 thrown gun
        private bool _resting;

        public void Init(Vector3 vel, float life) { _vel = vel; _life = life; _scale0 = transform.localScale; _mode = 0; }
        public void InitDecal(float life, float size) { _life = life; _size = size; _scale0 = transform.localScale; _mode = 1; }
        public void InitThrown(Vector3 vel, float life) { _vel = vel; _life = life; _scale0 = transform.localScale; _mode = 2; }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= _life) { Destroy(gameObject); return; }
            if (_mode == 1)
            {
                // Splat spreads a little at first, shrinks away at the end.
                float grow = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(_age / 0.12f));
                float fade = Mathf.Clamp01((_life - _age) / 2f);
                transform.localScale = _scale0 * grow * fade;
                return;
            }
            if (_mode == 0)
            {
                _vel += Vector3.down * 9.81f * dt;
                transform.position += _vel * dt;
                transform.localScale = _scale0 * Mathf.Clamp01((_life - _age) / (_life * 0.6f));
                return;
            }
            if (_resting)
            {
                if (_life - _age < 1f) transform.localScale = _scale0 * Mathf.Clamp01(_life - _age);
                return;
            }
            _vel += Vector3.down * 9.81f * dt;
            Vector3 step = _vel * dt;
            if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude + 0.05f, ~((1 << 2) | (1 << 9) | (1 << HideAndSeek.Game.PlayerAvatarBinder.LocalBodyLayer)), QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<NetPlayer>() == null)
            {
                if (hit.normal.y > 0.6f && _vel.magnitude < 3.5f)
                {
                    transform.position = hit.point + Vector3.up * 0.06f;
                    transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 90f);
                    _resting = true;
                    ToySounds.Clatter(hit.point);
                    return;
                }
                _vel = Vector3.Reflect(_vel, hit.normal) * 0.35f;
                ToySounds.Clatter(hit.point);
                return;
            }
            transform.position += step;
            transform.rotation = Quaternion.Euler(420f * dt, 260f * dt, 0f) * transform.rotation;
        }
    }
}
