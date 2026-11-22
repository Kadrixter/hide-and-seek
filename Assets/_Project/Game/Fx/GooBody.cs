using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Green goo stuck on a hit seeker: glossy flattened blobs on the head, chest, back, arms and a leg, wobbling, with
    /// drops falling off now and then; they shrink away as the goo wears off. On the local player the blobs only cast
    /// shadows (the screen overlay <see cref="GooOverlay"/> shows the goo instead).
    /// </summary>
    public class GooBody : MonoBehaviour
    {
        private Transform[] _blobs;
        private Vector3[] _scales;
        private float _amount, _nextDrip;
        private bool _fp, _built;

        public static GooBody Attach(NetPlayer p)
        {
            var g = p.gameObject.AddComponent<GooBody>();
            g.Build(p.Character);
            return g;
        }

        private void Build(BlockyCharacter c)
        {
            if (c == null) return;
            const float Px = BlockyCharacter.PixelSize;
            // (bone, local position, size)
            var spots = new (Transform, Vector3, Vector3)[]
            {
                (c.Head, new Vector3(0.05f, 7.8f * Px, 0.06f), new Vector3(0.26f, 0.08f, 0.24f)),
                (c.Head, new Vector3(-4.1f * Px, 4.5f * Px, 0.02f), new Vector3(0.05f, 0.16f, 0.13f)),
                (c.Head, new Vector3(0.03f, 2.5f * Px, 4.1f * Px), new Vector3(0.16f, 0.12f, 0.05f)),
                (c.Torso, new Vector3(-0.05f, 8f * Px, 2.2f * Px), new Vector3(0.24f, 0.2f, 0.06f)),
                (c.Torso, new Vector3(0.08f, 4f * Px, 2.2f * Px), new Vector3(0.14f, 0.16f, 0.05f)),
                (c.Torso, new Vector3(0.02f, 9f * Px, -2.2f * Px), new Vector3(0.2f, 0.18f, 0.05f)),
                (c.ElbowRight, new Vector3(2.1f * Px, -3f * Px, 0f), new Vector3(0.05f, 0.18f, 0.14f)),
                (c.ShoulderLeft, new Vector3(-2.1f * Px, -2f * Px, 0.02f), new Vector3(0.05f, 0.15f, 0.15f)),
                (c.KneeLeft, new Vector3(0.02f, -2f * Px, 2.1f * Px), new Vector3(0.14f, 0.16f, 0.05f)),
            };
            _blobs = new Transform[spots.Length];
            _scales = new Vector3[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var (bone, pos, size) = spots[i];
                if (bone == null) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Goo";
                Destroy(go.GetComponent<Collider>());
                go.layer = c.gameObject.layer;
                go.transform.SetParent(bone, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
                go.GetComponent<MeshRenderer>().sharedMaterial = ToyGunModel.GooMaterial;
                _blobs[i] = go.transform;
                _scales[i] = size;
                go.SetActive(false);
            }
            _built = true;
        }

        /// <summary>0..1 goo shown; <paramref name="firstPerson"/> = own body (shadows only).</summary>
        public void SetAmount(float amount, bool firstPerson)
        {
            _amount = amount;
            _fp = firstPerson;
        }

        private void LateUpdate()
        {
            if (!_built) return;
            float a = _amount;
            for (int i = 0; i < _blobs.Length; i++)
            {
                var b = _blobs[i];
                if (b == null) continue;
                bool on = a > 0.01f;
                if (b.gameObject.activeSelf != on) b.gameObject.SetActive(on);
                if (!on) continue;
                b.gameObject.layer = b.parent.gameObject.layer;
                var r = b.GetComponent<MeshRenderer>();
                var mode = _fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
                float wob = 1f + 0.12f * Mathf.Sin(Time.time * 7f + i * 1.7f);
                b.localScale = Vector3.Scale(_scales[i], new Vector3(wob, 2f - wob, wob)) * Mathf.Lerp(0.3f, 1f, a);
            }
            if (a > 0.3f && !_fp && Time.time >= _nextDrip)
            {
                _nextDrip = Time.time + Random.Range(0.15f, 0.4f);
                var b = _blobs[Random.Range(0, _blobs.Length)];
                if (b != null) ToyGooFx.Drop(b.position, Vector3.down * 0.3f, Random.Range(0.02f, 0.035f), 0.5f);
            }
        }
    }
}
