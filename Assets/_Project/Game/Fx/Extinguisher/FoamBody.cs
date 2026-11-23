using HideAndSeek.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Extinguisher foam stuck on a hit seeker: lumpy white patches plastered over the face (eyes first), the top of the
    /// head, the hair at the sides, the chest and shoulders. Every wipe stroke of the hands (same clock as
    /// <see cref="ExtTimeline"/>) takes the face patches on its side away - they slide off, shrink and drop a few
    /// flakes - the rest shrinks as the effect wears off; a thin powder film stays a little longer. On the local
    /// player the patches only cast shadows (the screen overlay shows the foam instead).
    /// </summary>
    public class FoamBody : MonoBehaviour
    {
        private struct Blob { public Transform T; public Vector3 Size, Pos; public float Gone; public bool Face; }

        private static Material _mat;
        private Blob[] _blobs;
        private float _elapsed, _left;
        private bool _on, _fp;
        private float _nextFlake;

        public static FoamBody Attach(BlockyCharacter c)
        {
            var f = c.gameObject.AddComponent<FoamBody>();
            f.Build(c);
            return f;
        }

        public void Set(bool on, float elapsed, float left, bool firstPerson)
        {
            _on = on;
            _elapsed = elapsed;
            _left = left;
            _fp = firstPerson;
        }

        private void Build(BlockyCharacter c)
        {
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Standard")) { name = "ExtFoam", color = new Color(0.97f, 0.97f, 0.95f) };
                _mat.SetFloat("_Glossiness", 0.32f);
            }
            const float Px = BlockyCharacter.PixelSize;
            float front = 4f * Px + 0.012f;
            // Face patches: x side (-1 left / +1 right of the character) decides which stroke wipes it.
            var list = new System.Collections.Generic.List<(Transform bone, Vector3 pos, Vector3 size, bool face)>
            {
                (c.Head, new Vector3(-0.09f, 5.2f * Px, front), new Vector3(0.2f, 0.13f, 0.05f), true),
                (c.Head, new Vector3(0.09f, 5.0f * Px, front), new Vector3(0.21f, 0.14f, 0.05f), true),
                (c.Head, new Vector3(0.0f, 3.2f * Px, front + 0.006f), new Vector3(0.16f, 0.12f, 0.06f), true),
                (c.Head, new Vector3(-0.13f, 2.2f * Px, front), new Vector3(0.12f, 0.16f, 0.04f), true),
                (c.Head, new Vector3(0.14f, 1.8f * Px, front), new Vector3(0.1f, 0.15f, 0.04f), true),
                (c.Head, new Vector3(0.02f, 0.8f * Px, front), new Vector3(0.2f, 0.08f, 0.04f), true),
                (c.Head, new Vector3(0.0f, 8.1f * Px, 0.08f), new Vector3(0.36f, 0.07f, 0.26f), false),
                (c.Head, new Vector3(-4.1f * Px, 5f * Px, 0.1f), new Vector3(0.05f, 0.18f, 0.18f), false),
                (c.Head, new Vector3(4.1f * Px, 5.5f * Px, 0.08f), new Vector3(0.05f, 0.16f, 0.2f), false),
                (c.Torso, new Vector3(0.04f, 9.5f * Px, 2.2f * Px), new Vector3(0.3f, 0.2f, 0.06f), false),
                (c.Torso, new Vector3(-0.1f, 6.5f * Px, 2.2f * Px), new Vector3(0.14f, 0.18f, 0.05f), false),
                (c.ShoulderRight, new Vector3(0.02f, 1.6f * Px, 0.03f), new Vector3(0.2f, 0.07f, 0.2f), false),
                (c.ShoulderLeft, new Vector3(-0.02f, 1.6f * Px, 0.0f), new Vector3(0.18f, 0.07f, 0.18f), false),
            };
            _blobs = new Blob[list.Count];
            var rnd = new System.Random(c.name.GetHashCode() ^ 0x5bd1);
            for (int i = 0; i < list.Count; i++)
            {
                var (bone, pos, size, face) = list[i];
                if (bone == null) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Foam";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(bone, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 20f - 10f, (float)rnd.NextDouble() * 20f - 10f, (float)rnd.NextDouble() * 360f);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = _mat;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);
                // When a face patch is wiped: the first stroke on its side (right strokes are the even ones).
                float gone = 99f;
                if (face)
                {
                    int side = pos.x > 0.03f ? 1 : pos.x < -0.03f ? -1 : 0;
                    int stroke = side >= 0 ? (i % 2 == 0 ? 0 : 2) : (i % 2 == 0 ? 1 : 3);
                    gone = ExtTimeline.WipeIn + stroke * ExtTimeline.WipeStroke + 0.3f;
                }
                _blobs[i] = new Blob { T = go.transform, Size = size, Pos = pos, Gone = gone, Face = face };
            }
        }

        private void LateUpdate()
        {
            if (_blobs == null) return;
            float e = _elapsed, left = _left;
            float total = e + left;
            for (int i = 0; i < _blobs.Length; i++)
            {
                var b = _blobs[i];
                if (b.T == null) continue;
                // Body patches shrink over the effect; face ones go with their stroke (the leftover film lasts to the end).
                float k = _on ? 1f : 0f;
                if (_on)
                {
                    float appear = Mathf.Clamp01(e / 0.12f);
                    float wear = Mathf.Clamp01(left / Mathf.Max(0.5f, total * 0.6f));
                    float wiped = b.Face ? ExtTimeline.S(e, b.Gone - 0.05f, b.Gone + 0.3f) : 0f;
                    k = appear * Mathf.Lerp(0.35f, 1f, wear) * (1f - 0.75f * wiped);
                    if (b.Face && wiped > 0.01f && wiped < 0.9f && !_fp && Time.time >= _nextFlake)
                    {
                        _nextFlake = Time.time + 0.06f;
                        FoamFlakes.Drop(b.T.position, b.T.parent.right * (b.Pos.x > 0 ? 1f : -1f));
                    }
                    b.T.localPosition = b.Pos + new Vector3(Mathf.Sign(b.Pos.x) * 0.05f * wiped, -0.05f * wiped, 0f);
                }
                bool on = k > 0.02f;
                if (b.T.gameObject.activeSelf != on) b.T.gameObject.SetActive(on);
                if (!on) continue;
                var r = b.T.GetComponent<MeshRenderer>();
                b.T.gameObject.layer = b.T.parent.gameObject.layer;
                var mode = _fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
                float wob = 1f + 0.05f * Mathf.Sin(Time.time * 4f + i);
                b.T.localScale = b.Size * (k * wob);
            }
        }
    }
}
