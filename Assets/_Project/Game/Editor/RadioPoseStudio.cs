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
            // Carry, still: three angles.
            Run(1.7f, 3.2f, t => act.SetShoulderRadio(true, false, t, lyingPos, lyingRot, 0f, false), lyingPos, lyingRot,
                new[] { 3.2f }, new[] { front34, side, back34, new Vector3(0.3f, 1.7f, 2.6f), new Vector3(-2.4f, 1.5f, 0.6f) }, "noszenie");
            // Walking with music (pulsing bass).
            motion.Vel = new Vector3(0f, 0f, 1.4f);
            Run(3.2f, 5.2f, t => act.SetShoulderRadio(true, false, t, lyingPos, lyingRot, Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 4f)) * 1.2f, true), lyingPos, lyingRot,
                new[] { 4.6f, 4.75f, 4.9f }, new[] { front34 }, "chód z muzyką");
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
