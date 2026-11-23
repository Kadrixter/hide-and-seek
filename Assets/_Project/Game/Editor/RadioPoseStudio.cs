using System;
using System.Collections.Generic;
using System.IO;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Test hook for the boombox carried on the shoulder: an empty lit studio with one character and the real boombox
    /// model, the third-person layer ticked by hand, contact sheets of the lift from a pallet, the carry (still / walking
    /// with music, several angles) and the put down onto the floor.
    /// Batch mode (without -nographics): -executeMethod HideAndSeek.Game.Editor.RadioPoseStudio.CaptureFromCommandLine
    /// -radioposeshots &lt;folder&gt; [-radiovariants a,b,c] (variants = ride poses to compare, see <see cref="Variants"/>).
    /// </summary>
    public static class RadioPoseStudio
    {
        private const int Tile = 520;

        private sealed class Motion : MonoBehaviour, ICharacterMotionSource
        {
            public Vector3 Vel;
            public bool Run;
            public Vector3 Velocity => Vel;
            public float HorizontalSpeed => new Vector2(Vel.x, Vel.z).magnitude;
            public bool IsGrounded => true;
            public bool IsCrouching => false;
            public bool IsRunning => Run;
            public float CrouchAmount => 0f;
            public float LookPitch => 0f;
            public LocomotionState State => HorizontalSpeed > 0.1f ? (Run ? LocomotionState.Run : LocomotionState.Walk) : LocomotionState.Idle;
        }

        /// <summary>Ride pose variants: (pivot in torso space, rotation, fist above the grip, elbow pole).</summary>
        public static readonly Dictionary<string, (Vector3 ride, Vector3 euler, Vector3 fist, Vector3 pole)> Variants = new()
        {
            ["a"] = (new Vector3(0.335f, 0.672f, -0.07f), new Vector3(12f, 90f, 0f), new Vector3(0f, 0.075f, 0f), new Vector3(1f, 0.1f, 0.9f)),
            ["b"] = (new Vector3(0.36f, 0.66f, -0.1f), new Vector3(24f, 90f, 0f), new Vector3(0f, 0.06f, 0.05f), new Vector3(1f, -0.3f, 0.6f)),
            ["c"] = (new Vector3(0.33f, 0.672f, -0.04f), new Vector3(6f, 90f, 0f), new Vector3(0f, 0.04f, 0.12f), new Vector3(0.6f, -0.2f, 1f)),
            ["d"] = (new Vector3(0.37f, 0.672f, -0.05f), new Vector3(-4f, 90f, 0f), new Vector3(0f, -0.145f, 0.19f), new Vector3(1f, -1f, 0.1f)),
            ["e"] = (new Vector3(0.335f, 0.672f, -0.24f), new Vector3(0f, 90f, 0f), new Vector3(-0.1f, 0.06f, 0f), new Vector3(0.6f, -0.2f, 1f)),
            ["g"] = (new Vector3(0.36f, 0.672f, -0.05f), new Vector3(-8f, 90f, 0f), new Vector3(0f, -0.05f, 0.13f), new Vector3(1f, -0.6f, 0.15f)),
            ["h"] = (new Vector3(0.36f, 0.672f, -0.1f), new Vector3(-8f, 90f, 0f), new Vector3(-0.12f, -0.03f, 0.12f), new Vector3(1f, -0.5f, 0.5f)),
            ["b1"] = (new Vector3(0.37f, 0.682f, -0.05f), new Vector3(-4f, 90f, 0f), new Vector3(0f, -0.14f, 0.215f), new Vector3(1f, -1f, 0.1f)),
            ["b2"] = (new Vector3(0.36f, 0.682f, -0.06f), new Vector3(-6f, 90f, 0f), new Vector3(-0.02f, -0.13f, 0.225f), new Vector3(1f, -1.2f, 0.2f)),
            ["b3"] = (new Vector3(0.38f, 0.684f, -0.04f), new Vector3(-3f, 90f, 0f), new Vector3(0.04f, -0.12f, 0.235f), new Vector3(1f, -0.8f, 0f)),
            ["s0"] = (new Vector3(0.363f, 0.719f, -0.062f), new Vector3(-2.5f, 90f, 0f), new Vector3(0.039f, -0.246f, 0.26f), new Vector3(1f, -0.82f, 0.26f)),
            ["s1"] = (new Vector3(0.379f, 0.711f, -0.052f), new Vector3(4f, 90f, 0f), new Vector3(0.027f, -0.223f, 0.26f), new Vector3(1f, -1.71f, 0.35f)),
            ["f"] = (new Vector3(0.34f, 0.672f, -0.18f), new Vector3(8f, 90f, 0f), new Vector3(-0.12f, 0.05f, 0.03f), new Vector3(0.8f, -0.4f, 1f)),
        };

        public static void CaptureFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                string Arg(string k) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                string dir = Arg("-radioposeshots") ?? "Logs/radiopose";
                var variants = (Arg("-radiovariants") ?? "a").Split(',');
                Directory.CreateDirectory(dir);
                foreach (var v in variants) Capture(dir, v.Trim());
                Debug.Log("[RadioPose] done");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(string dir, string variant)
        {
            if (Variants.TryGetValue(variant, out var vr))
            {
                ShoulderRadioRig.RideTorso = vr.ride;
                ShoulderRadioRig.RideEuler = vr.euler;
                ShoulderRadioRig.FistOnGrip = vr.fist;
                ShoulderRadioRig.RidePole = vr.pole;
                ShoulderRadioRig.HeadTilt = variant == "s0" ? 15.7f : variant == "s1" ? 14.7f : 11f;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floorMat = new Material(Shader.Find("Standard")) { color = new Color(0.42f, 0.42f, 0.43f) };
            floorMat.SetFloat("_Glossiness", 0.15f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(4, 1, 4);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            var woodMat = new Material(Shader.Find("Standard")) { color = new Color(0.62f, 0.47f, 0.3f) };
            var pallet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pallet.transform.position = new Vector3(0f, 0.435f, 0.95f);
            pallet.transform.localScale = new Vector3(1.2f, 0.87f, 0.8f);
            pallet.GetComponent<Renderer>().sharedMaterial = woodMat;

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.fieldOfView = 34f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.shadows = LightShadows.Soft;
            key.intensity = 1.1f;
            key.transform.rotation = Quaternion.Euler(46f, -30f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;
            fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            RenderSettings.ambientLight = new Color(0.36f, 0.37f, 0.4f);

            var root = new GameObject("Player");
            var motion = root.AddComponent<Motion>();
            var ch = BlockyCharacterFactory.Create(root.transform, 1, "Radio");
            var anim = ch.GetComponent<BlockyCharacterAnimator>();
            anim.autoUpdate = false;
            anim.SetMotionSource(motion);
            var act = ch.GetComponent<BlockyCharacterActions>();
            var radio = Boombox.Create("Radio", () => null);
            MapRadioLook.Wear(radio);

            Vector3 lyingPos = new Vector3(0.02f, 0.87f, 0.78f);
            Quaternion lyingRot = Quaternion.Euler(0f, 180f, 0f);
            Vector3 floorPos = new Vector3(0.05f, 0f, 0.58f);
            Quaternion floorRot = Quaternion.Euler(0f, 180f, 0f);

            var frames = new List<(Texture2D tex, string label)>();
            bool measure = false;
            var pen = new Dictionary<string, float>();
            FirstPersonViewModel vm = null;
            Camera eye = null;
            const float dt = 1f / 60f;
            float clock = 0f;

            void Place(Vector3 groundPos, Quaternion groundRot)
            {
                if (act.ShoulderRadioPose(out var p, out var r)) radio.transform.SetPositionAndRotation(p, r);
                else radio.transform.SetPositionAndRotation(groundPos, groundRot);
            }

            void Run(float from, float to, Action<float> set, Vector3 gp, Quaternion gr, float[] shots, Vector3[] views, string label)
            {
                int si = 0;
                for (float t = from; t <= to + 1e-4f; t += dt)
                {
                    set(t);
                    clock += dt;
                    root.transform.position += motion.Vel * dt;
                    anim.Tick(dt);
                    Place(gp, gr);
                    if (measure && act.ShoulderRadioOn) Measure(ch, radio.transform, pen);
                    if (vm != null)
                    {
                        bool on = act.ShoulderRadioHands(out var fist, out var left, out float lw, out float ride);
                        vm.SetShoulderRadio(on, fist, left, lw, ride, 0f);
                        vm.Tick(dt);
                    }
                    while (si < shots.Length && t >= shots[si] - 1e-4f)
                    {
                        foreach (var v in views) frames.Add((v == Vector3.zero ? ShotFpp(eye) : Shot(cam, root.transform.position, v), $"{label} {shots[si]:0.00}s"));
                        si++;
                    }
                }
            }

            Vector3 side = new Vector3(2.6f, 1.35f, 0.9f), front34 = new Vector3(1.7f, 1.55f, 2.3f), back34 = new Vector3(-1.6f, 1.6f, -2.2f);
            // Idle settle.
            Run(0f, 0.5f, _ => act.SetShoulderRadio(false, false, 99f, lyingPos, lyingRot, 0f, false), lyingPos, lyingRot, new float[0], new Vector3[0], "");
            // Lift from the pallet.
            Run(0f, 1.7f, t => act.SetShoulderRadio(true, false, t, lyingPos, lyingRot, 0f, false), lyingPos, lyingRot,
                new[] { 0.3f, 0.55f, 0.8f, 1.05f, 1.25f, 1.7f }, new[] { side }, "podnoszenie");
            // Carry, still: three angles (and walking below): how deep the radio goes into the body.
            measure = true;
            Run(1.7f, 3.2f, t => act.SetShoulderRadio(true, false, t, lyingPos, lyingRot, 0f, false), lyingPos, lyingRot,
                new[] { 3.2f }, new[] { front34, side, back34, new Vector3(0.3f, 1.7f, 2.6f), new Vector3(-2.4f, 1.5f, 0.6f) }, "noszenie");
            // Walking with music (pulsing bass).
            motion.Vel = new Vector3(0f, 0f, 1.4f);
            Run(3.2f, 5.2f, t => act.SetShoulderRadio(true, false, t, lyingPos, lyingRot, Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 4f)) * 1.2f, true), lyingPos, lyingRot,
                new[] { 4.6f, 4.75f, 4.9f }, new[] { front34 }, "chód z muzyką");
            measure = false;
            var sb = new System.Text.StringBuilder($"[RadioPose] {variant} penetration (cm, carry + walk):");
            foreach (var kv in pen) sb.Append($" {kv.Key}={kv.Value * 100f:0.0}");
            Debug.Log(sb.ToString());
            motion.Vel = Vector3.zero;
            Run(0f, 0.6f, _ => act.SetShoulderRadio(true, false, 9f, lyingPos, lyingRot, 0f, false), lyingPos, lyingRot, new float[0], new Vector3[0], "");
            // Put down on the floor in front.
            Vector3 fp = root.transform.position + floorPos;
            Run(0f, 1.5f, t => act.SetShoulderRadio(false, true, t, fp, floorRot, 0f, false), fp, floorRot,
                new[] { 0.3f, 0.6f, 0.85f, 1.05f, 1.5f }, new[] { side }, "odstawianie");

            // First person: lift from the pallet again, carry, walk.
            root.transform.position = Vector3.zero;
            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(root.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, 1.62f, 0.06f);
            eyeGo.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            eye = eyeGo.AddComponent<Camera>();
            eye.fieldOfView = 75f;
            eye.nearClipPlane = 0.05f;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = cam.backgroundColor;
            eye.enabled = false;
            vm = FirstPersonViewModel.Create(eyeGo.transform, 1, "Radio");
            vm.autoUpdate = false;
            ch.SetFirstPersonMode(true);
            Run(0f, 0.6f, _ => act.SetShoulderRadio(false, false, 99f, lyingPos, lyingRot, 0f, false), lyingPos, lyingRot, new float[0], new Vector3[0], "");
            Run(0f, 1.6f, t =>
                {
                    act.SetShoulderRadio(true, false, t, lyingPos, lyingRot, 0f, false);
                    eyeGo.transform.localRotation = Quaternion.Euler(Mathf.Lerp(30f, 5f, Mathf.Clamp01((t - 0.5f) / 0.8f)), 0f, 0f);
                }, lyingPos, lyingRot,
                new[] { 0.45f, 0.75f, 1.05f, 1.3f, 1.6f }, new[] { Vector3.zero }, "fpp");
            motion.Vel = new Vector3(0f, 0f, 1.4f);
            Run(1.6f, 3.0f, t => act.SetShoulderRadio(true, false, t, lyingPos, lyingRot, 0f, false), lyingPos, lyingRot,
                new[] { 2.6f, 3.0f }, new[] { Vector3.zero }, "fpp chód");
            motion.Vel = Vector3.zero;

            string path = Path.Combine(dir, $"radiopose_{variant}.png");
            File.WriteAllBytes(path, Sheet(frames, 5).EncodeToPNG());
            Debug.Log($"[RadioPose] {path}: {frames.Count} frames");
        }

        private static Texture2D Shot(Camera cam, Vector3 player, Vector3 offset)
        {
            Vector3 target = player + new Vector3(0f, 1.2f, 0f);
            cam.transform.position = player + offset * 1.25f;
            cam.transform.LookAt(target);
            var rt = RenderTexture.GetTemporary(Tile, Tile, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        /// <summary>
        /// -executeMethod ...RadioPoseStudio.SearchFromCommandLine [-radiosearchn N]: random search of the ride pose (around
        /// variant B: radio on the raised upper arm, the forearm holding it from the outside) for the least body overlap
        /// while standing and walking with music. Logs the best candidates as variant lines.
        /// </summary>
        public static void SearchFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int ni = Array.IndexOf(args, "-radiosearchn");
                int n = ni >= 0 && ni + 1 < args.Length && int.TryParse(args[ni + 1], out int nn) ? nn : 250;
                Search(n);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Search(int count)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Player");
            var motion = root.AddComponent<Motion>();
            var ch = BlockyCharacterFactory.Create(root.transform, 1, "Radio");
            var anim = ch.GetComponent<BlockyCharacterAnimator>();
            anim.autoUpdate = false;
            anim.SetMotionSource(motion);
            var act = ch.GetComponent<BlockyCharacterActions>();
            var radio = Boombox.Create("Radio", () => null);
            var rng = new System.Random(5);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var results = new List<(float score, string line)>();
            const float dt = 1f / 60f;
            float clock = 0f;
            for (int c = 0; c < count; c++)
            {
                var ride = new Vector3(R(0.34f, 0.4f), R(0.675f, 0.75f), R(-0.08f, 0.02f));
                var euler = new Vector3(R(-8f, 6f), 90f, 0f);
                var fist = new Vector3(R(-0.07f, 0.05f), R(-0.25f, -0.1f), R(0.22f, 0.3f));
                var pole = new Vector3(1f, R(-2f, -0.5f), R(-0.2f, 0.4f));
                float tilt = R(10f, 18f);
                if (c == 0) { ride = new Vector3(0.37f, 0.672f, -0.05f); euler = new Vector3(-4f, 90f, 0f); fist = new Vector3(0f, -0.145f, 0.19f); pole = new Vector3(1f, -1f, 0.1f); tilt = 11f; }
                ShoulderRadioRig.RideTorso = ride;
                ShoulderRadioRig.RideEuler = euler;
                ShoulderRadioRig.FistOnGrip = fist;
                ShoulderRadioRig.RidePole = pole;
                ShoulderRadioRig.HeadTilt = tilt;
                var pen = new Dictionary<string, float>();
                float gap = 0f, grip = 0f;
                root.transform.position = Vector3.zero;
                for (int f = 0; f < 200; f++)
                {
                    bool walk = f >= 90;
                    motion.Vel = walk ? new Vector3(0f, 0f, 1.5f) : Vector3.zero;
                    clock += dt;
                    float beat = walk ? Mathf.Max(0f, Mathf.Sin(clock * Mathf.PI * 4f)) * 1.3f : 0f;
                    act.SetShoulderRadio(true, false, 9f, Vector3.zero, Quaternion.identity, beat, walk);
                    root.transform.position += motion.Vel * dt;
                    anim.Tick(dt);
                    if (act.ShoulderRadioPose(out var p, out var r)) radio.transform.SetPositionAndRotation(p, r);
                    if (f >= 40 && f % 3 == 0)
                    {
                        Measure(ch, radio.transform, pen);
                        gap = Mathf.Max(gap, SupportGap(ch.ShoulderRight, radio.transform));
                        grip = Mathf.Max(grip, GripGap(ch.ElbowRight, radio.transform));
                    }
                }
                float worst = 0f, sum = 0f;
                foreach (var kv in pen) { worst = Mathf.Max(worst, kv.Value); sum += kv.Value; }
                // Small preference for the radio sitting low on the arm (no floating) and the fist near the top.
                float score = worst * 100f + sum * 20f + Mathf.Max(0f, gap - 0.012f) * 60f + Mathf.Max(0f, grip - 0.01f) * 60f;
                var sb = new System.Text.StringBuilder();
                foreach (var kv in pen) sb.Append($" {kv.Key}={kv.Value * 100f:0.0}");
                sb.Append($" gap={gap * 100f:0.0} grip={grip * 100f:0.0}");
                string line = FormattableString.Invariant($"ride=({ride.x:0.000}f, {ride.y:0.000}f, {ride.z:0.000}f) euler=({euler.x:0.0}f, 90f, 0f) fist=({fist.x:0.000}f, {fist.y:0.000}f, {fist.z:0.000}f) pole=(1f, {pole.y:0.00}f, {pole.z:0.00}f) tilt={tilt:0.0} |{sb}");
                results.Add((score, line));
                if (c == 0) Debug.Log($"[RadioSearch] B now: score={score:0.0} {line}");
            }
            results.Sort((a, b) => a.score.CompareTo(b.score));
            for (int i = 0; i < Mathf.Min(8, results.Count); i++) Debug.Log($"[RadioSearch] #{i} score={results[i].score:0.0} {results[i].line}");
        }

        /// <summary>How far the radio's bottom (middle part) is above the upper arm it rests on (m, largest while measured).</summary>
        private static float SupportGap(Transform arm, Transform radio)
        {
            var mf = arm.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return 0f;
            Bounds b = mf.sharedMesh.bounds;
            float best = float.MaxValue;
            for (int i = -2; i <= 2; i++)
            {
                Vector3 w = radio.TransformPoint(new Vector3(i * 0.06f, 0f, 0f));
                Vector3 l = arm.InverseTransformPoint(w);
                Vector3 c = b.ClosestPoint(l);
                best = Mathf.Min(best, Vector3.Distance(arm.TransformPoint(c), w));
            }
            return best;
        }

        /// <summary>How far the fist (end of the forearm block) is from the radio's face / speakers (m).</summary>
        private static float GripGap(Transform forearm, Transform radio)
        {
            var mf = forearm.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return 0f;
            Bounds b = mf.sharedMesh.bounds;
            float best = float.MaxValue;
            for (int ix = -6; ix <= 6; ix++)
            for (int iy = 0; iy <= 6; iy++)
            {
                float x = ix * 0.04f, y = 0.03f + iy * 0.035f;
                float dx = Mathf.Abs(x) - 0.168f, dy = y - 0.133f;
                float z = dx * dx + dy * dy <= 0.1f * 0.1f ? 0.075f + 0.028f : 0.075f + 0.008f; // speaker pods stand out
                Vector3 w = radio.TransformPoint(new Vector3(x, y, z));
                Vector3 l = forearm.InverseTransformPoint(w);
                best = Mathf.Min(best, Vector3.Distance(forearm.TransformPoint(b.ClosestPoint(l)), w));
            }
            return best;
        }

        /// <summary>Deepest point of the radio body (and the speaker pods) inside each body block (m).</summary>
        private static void Measure(BlockyCharacter ch, Transform radio, Dictionary<string, float> pen)
        {
            var parts = new[] { ("head", ch.Head), ("body", ch.BodyMesh), ("upperarmR", ch.ShoulderRight), ("forearmR", ch.ElbowRight),
                ("upperarmL", ch.ShoulderLeft), ("forearmL", ch.ElbowLeft) };
            const float W = 0.56f, H = 0.27f, D = 0.15f;
            foreach (var (name, t) in parts)
            {
                var mf = t != null ? t.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                float worst = pen.TryGetValue(name, out float w0) ? w0 : 0f;
                for (int ix = 0; ix <= 14; ix++)
                for (int iy = 0; iy <= 7; iy++)
                for (int iz = 0; iz <= 6; iz++)
                {
                    float z = Mathf.Lerp(-D * 0.5f, D * 0.5f + 0.028f, iz / 6f);
                    float x = Mathf.Lerp(-W * 0.5f, W * 0.5f, ix / 14f), y = Mathf.Lerp(0.004f, H, iy / 7f);
                    if (z > D * 0.5f)
                    {
                        // The speaker pods only (circles of 0.1 m round the woofers).
                        float dx = Mathf.Abs(x) - 0.168f, dy = y - 0.133f;
                        if (dx * dx + dy * dy > 0.1f * 0.1f) continue;
                    }
                    Vector3 lp = t.InverseTransformPoint(radio.TransformPoint(new Vector3(x, y, z)));
                    if (!b.Contains(lp)) continue;
                    Vector3 d1 = lp - b.min, d2 = b.max - lp;
                    float depth = Mathf.Min(Mathf.Min(d1.x, d2.x), Mathf.Min(Mathf.Min(d1.y, d2.y), Mathf.Min(d1.z, d2.z)));
                    if (depth > worst) worst = depth;
                }
                pen[name] = worst;
            }
        }

        private static Texture2D ShotFpp(Camera eye)
        {
            var rt = RenderTexture.GetTemporary(Tile * 16 / 9, Tile, 24, RenderTextureFormat.ARGB32);
            eye.targetTexture = rt;
            eye.Render();
            eye.targetTexture = null;
            foreach (var c in eye.GetComponentsInChildren<Camera>(true))
            {
                if (c == eye) continue;
                c.targetTexture = rt;
                c.Render();
                c.targetTexture = null;
            }
            RenderTexture.active = rt;
            var wide = new Texture2D(rt.width, Tile, TextureFormat.RGB24, false);
            wide.ReadPixels(new Rect(0, 0, rt.width, Tile), 0, 0);
            wide.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            // Right part of the 16:9 view (where the radio is) in a square tile, plus the middle for context.
            var tex = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
            tex.SetPixels(0, 0, Tile, Tile, wide.GetPixels(wide.width - Tile, 0, Tile, Tile));
            tex.Apply();
            return tex;
        }

        private static Texture2D Sheet(List<(Texture2D tex, string label)> frames, int cols)
        {
            int rows = Mathf.Max(1, (frames.Count + cols - 1) / cols);
            var sheet = new Texture2D(cols * Tile, rows * Tile, TextureFormat.RGB24, false);
            var px = new Color32[cols * Tile * rows * Tile];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(20, 20, 24, 255);
            sheet.SetPixels32(px);
            for (int i = 0; i < frames.Count; i++)
            {
                int cx = i % cols, cy = rows - 1 - i / cols;
                sheet.SetPixels(cx * Tile, cy * Tile, Tile, Tile, frames[i].tex.GetPixels());
            }
            sheet.Apply();
            return sheet;
        }
    }
}
