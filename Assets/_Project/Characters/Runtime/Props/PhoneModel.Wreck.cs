using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// The stomped toy phone: the body cracked in two flattened halves (keypad end and screen end) with a shattered
    /// screen, the battery cover and the battery knocked out, the antenna snapped off, keycaps, screen shards and pink
    /// splinters around it. Every piece comes from a seeded random (the phone id), so every player sees the same
    /// wreck; it stays on the floor (no colliders) until the round ends.
    /// </summary>
    public static partial class PhoneModel
    {
        /// <summary>Builds the wreck at <paramref name="pos"/> (floor) / <paramref name="yaw"/>. <paramref name="burst"/>: pieces fly out first.</summary>
        public static GameObject CreateWreck(Vector3 pos, float yaw, int seed, bool burst)
        {
            var rng = new System.Random(seed * 7919 + 13);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var root = new GameObject("ToyPhoneWreck").transform;
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var pieces = new List<Transform>();
            Transform Piece(string name, Vector3 at, float turn, Vector3 tilt)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root, false);
                t.localPosition = at;
                t.localRotation = Quaternion.Euler(tilt.x, turn, tilt.z);
                pieces.Add(t);
                return t;
            }
            const float w = Width, flat = 0.022f;

            // Keypad half: flattened, cracked edge, a few keycaps still on.
            var low = Piece("HalfKeypad", new Vector3(R(-0.01f, 0.01f), 0f, -0.055f), R(-8f, 8f), new Vector3(R(-3f, 3f), 0f, R(-4f, 4f)));
            Box(low, new Vector3(0f, flat * 0.5f, 0f), new Vector3(w * 1.08f, flat, 0.12f), Mat(Pink), 0);
            Box(low, new Vector3(0.02f, flat * 0.5f, 0.062f), new Vector3(w * 0.55f, flat * 0.8f, 0.012f), Mat(PinkDark), 0); // jagged edge
            Box(low, new Vector3(0f, flat * 0.5f, -0.055f), new Vector3(w, flat * 0.9f, 0.018f), Mat(PinkDark), 0);
            for (int i = 0; i < 4; i++)
                Box(low, new Vector3((i % 2 - 0.5f) * 0.05f + R(-0.008f, 0.008f), flat + 0.004f, -0.01f - (i / 2) * 0.03f),
                    new Vector3(0.024f, 0.008f, 0.019f), Mat(KeyColors[(i * 2 + seed) % KeyColors.Length]), 0);

            // Screen half: tilted against the keypad half, shattered screen with white cracks.
            var up = Piece("HalfScreen", new Vector3(R(-0.025f, 0.025f), 0.004f, 0.07f), R(-18f, 18f), new Vector3(R(-9f, -3f), 0f, R(-6f, 6f)));
            Box(up, new Vector3(0f, flat * 0.5f, 0f), new Vector3(w * 1.06f, flat, 0.11f), Mat(Pink), 0);
            Box(up, new Vector3(0f, flat * 0.5f, 0.045f), new Vector3(w - 0.01f, flat + 0.004f, 0.025f), Mat(PinkLight), 0);
            Box(up, new Vector3(0f, flat + 0.002f, -0.005f), new Vector3(w - 0.036f, 0.004f, 0.058f), Mat(new Color(0.08f, 0.12f, 0.11f)), 0);
            for (int i = 0; i < 4; i++)
            {
                var crack = Box(up, new Vector3(R(-0.02f, 0.02f), flat + 0.0045f, R(-0.025f, 0.015f)), new Vector3(0.003f, 0.002f, R(0.025f, 0.05f)), Mat(new Color(0.85f, 0.95f, 0.9f)), 0);
                crack.localRotation = Quaternion.Euler(0f, R(-70f, 70f), 0f);
            }

            // Battery cover and the battery knocked out, the antenna snapped off.
            var cover = Piece("BatteryCover", Scatter(rng, 0.14f, 0.26f), R(0f, 360f), new Vector3(0f, 0f, R(-4f, 4f)));
            Box(cover, new Vector3(0f, 0.004f, 0f), new Vector3(w * 0.85f, 0.007f, 0.11f), Mat(PinkDark), 0);
            Box(cover, new Vector3(0f, 0.008f, 0.04f), new Vector3(0.03f, 0.003f, 0.012f), Mat(Pink), 0);
            var bat = Piece("Battery", Scatter(rng, 0.12f, 0.24f), R(0f, 360f), new Vector3(0f, 0f, 0f));
            Box(bat, new Vector3(0f, 0.009f, 0f), new Vector3(0.05f, 0.018f, 0.075f), Mat(new Color(0.16f, 0.16f, 0.18f)), 0);
            Box(bat, new Vector3(0f, 0.0185f, 0.008f), new Vector3(0.044f, 0.002f, 0.035f), Mat(new Color(0.95f, 0.75f, 0.15f)), 0);
            Box(bat, new Vector3(0.012f, 0.009f, 0.039f), new Vector3(0.008f, 0.008f, 0.004f), Mat(new Color(0.75f, 0.75f, 0.7f)), 0);
            var ant = Piece("Antenna", Scatter(rng, 0.2f, 0.36f), R(0f, 360f), new Vector3(0f, 0f, 0f));
            Box(ant, new Vector3(0f, 0.0085f, 0f), new Vector3(0.017f, 0.017f, 0.058f), Mat(PinkDark), 0);
            Box(ant, new Vector3(0f, 0.014f, 0.04f), new Vector3(0.028f, 0.028f, 0.028f), Mat(KeyColors[0]), 0);

            // Loose keycaps, screen shards, pink splinters.
            for (int i = 0; i < 6; i++)
            {
                var k = Piece("Key", Scatter(rng, 0.1f, 0.45f), R(0f, 360f), new Vector3(R(-20f, 20f), 0f, R(-20f, 20f)));
                Box(k, new Vector3(0f, 0.006f, 0f), new Vector3(0.024f, 0.012f, 0.019f), Mat(KeyColors[(i + seed) % KeyColors.Length]), 0);
            }
            for (int i = 0; i < 5; i++)
            {
                var g = Piece("Shard", Scatter(rng, 0.06f, 0.3f), R(0f, 360f), Vector3.zero);
                Box(g, new Vector3(0f, 0.0015f, 0f), new Vector3(R(0.008f, 0.02f), 0.003f, R(0.01f, 0.025f)), Mat(new Color(0.7f, 0.9f, 0.8f)), 0);
            }
            for (int i = 0; i < 5; i++)
            {
                var sp = Piece("Splinter", Scatter(rng, 0.08f, 0.35f), R(0f, 360f), Vector3.zero);
                Box(sp, new Vector3(0f, 0.003f, 0f), new Vector3(R(0.008f, 0.018f), 0.006f, R(0.012f, 0.03f)), Mat(i % 2 == 0 ? Pink : PinkLight), 0);
            }
            if (burst) root.gameObject.AddComponent<PhoneWreckSettle>().Begin(pieces, seed);
            return root.gameObject;
        }

        static Vector3 Scatter(System.Random rng, float min, float max)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0), d = min + (float)rng.NextDouble() * (max - min);
            return new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
        }
    }

    /// <summary>The wreck's pieces fly out from the stomp point on short arcs and settle; then the component removes itself.</summary>
    public class PhoneWreckSettle : MonoBehaviour
    {
        Transform[] parts;
        Vector3[] restPos;
        Quaternion[] restRot;
        Vector3[] spin;
        float[] dur, height;
        float age;

        public void Begin(List<Transform> pieces, int seed)
        {
            var rng = new System.Random(seed * 31 + 7);
            int n = pieces.Count;
            parts = pieces.ToArray();
            restPos = new Vector3[n];
            restRot = new Quaternion[n];
            spin = new Vector3[n];
            dur = new float[n];
            height = new float[n];
            for (int i = 0; i < n; i++)
            {
                restPos[i] = parts[i].localPosition;
                restRot[i] = parts[i].localRotation;
                float d = new Vector2(restPos[i].x, restPos[i].z).magnitude;
                dur[i] = 0.18f + d * 1.1f + (float)rng.NextDouble() * 0.12f;
                height[i] = 0.04f + d * 0.6f + (float)rng.NextDouble() * 0.08f;
                spin[i] = new Vector3((float)rng.NextDouble() * 900f - 450f, (float)rng.NextDouble() * 900f - 450f, (float)rng.NextDouble() * 900f - 450f) * Mathf.Min(1f, d * 6f);
            }
            Tick(0f);
        }

        void Update() => Tick(Time.deltaTime);

        void Tick(float dt)
        {
            age += dt;
            bool done = true;
            for (int i = 0; i < parts.Length; i++)
            {
                float k = Mathf.Clamp01(age / dur[i]);
                if (k < 1f) done = false;
                Vector3 from = new Vector3(restPos[i].x * 0.15f, 0.03f, restPos[i].z * 0.15f);
                float bounce = k < 0.8f ? Mathf.Sin(Mathf.PI * k / 0.8f) : 0.18f * Mathf.Sin(Mathf.PI * (k - 0.8f) / 0.2f);
                parts[i].localPosition = Vector3.Lerp(from, restPos[i], Mathf.Min(1f, k / 0.8f)) + Vector3.up * height[i] * bounce;
                parts[i].localRotation = restRot[i] * Quaternion.Euler(spin[i] * (1f - k) * 0.35f);
            }
            if (done) Destroy(this);
        }
    }
}
