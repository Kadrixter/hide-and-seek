using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Player.Editor
{
    /// <summary>
    /// Headless movement checks in the test range (stairs, ramps, crouch tunnel, jump) using
    /// <see cref="PlayerMotor.CommandProvider"/> and <see cref="PlayerMotor.Tick"/>.
    /// Batch: -executeMethod HideAndSeek.Player.Editor.PlayerMotorSmokeTest.Run
    /// </summary>
    public static class PlayerMotorSmokeTest
    {
        private const float Dt = 1f / 60f;

        private const string PlayModeKey = "HideAndSeek.Player.MotorSmoke.PlayMode";

        /// <summary>Edit-mode run (fast, but CharacterController resizing may not apply outside play mode).</summary>
        [MenuItem("HideAndSeek/Player/Run Motor Smoke Test (Edit Mode)")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(PlayerBuildMenu.ScenePath, OpenSceneMode.Single);
            int failures = Execute();
            EditorSceneManager.OpenScene(PlayerBuildMenu.ScenePath, OpenSceneMode.Single);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        /// <summary>
        /// Enters play mode in the test range and runs the checks there (authoritative).
        /// Batch: run WITHOUT -quit; the editor exits with 0 / 1 when done.
        /// </summary>
        [MenuItem("HideAndSeek/Player/Run Motor Smoke Test (Play Mode)")]
        public static void RunPlayMode()
        {
            EditorSceneManager.OpenScene(PlayerBuildMenu.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(PlayModeKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ResumeInPlayMode()
        {
            if (!SessionState.GetBool(PlayModeKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            SessionState.SetBool(PlayModeKey, false);
            int frames = 0;
            void Wait()
            {
                if (++frames < 10) return; // let Awake/Start/first frames run
                EditorApplication.update -= Wait;
                int failures = Execute();
                if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
                else EditorApplication.ExitPlaymode();
            }
            EditorApplication.update += Wait;
        }

        private static int Execute()
        {
            int failures = 0;
            var log = new StringBuilder($"[MotorSmoke] results ({(Application.isPlaying ? "play mode" : "edit mode")}):\n");
            try
            {
                var motor = UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
                if (motor == null) throw new Exception("No PlayerMotor in scene");
                Physics.SyncTransforms();

                var cmd = new MotorCommand();
                motor.CommandProvider = () => cmd;
                int steps = 0, landings = 0;
                float lastImpact = 0f;
                motor.Footstep += _ => steps++;
                motor.Landed += v => { landings++; lastImpact = v; };

                void Check(string name, bool ok, string detail)
                {
                    log.AppendLine($"  {(ok ? "PASS" : "FAIL")}  {name}: {detail}");
                    if (!ok) failures++;
                }

                int Simulate(float seconds, Action perFrame = null)
                {
                    int airborne = 0;
                    int n = Mathf.RoundToInt(seconds / Dt);
                    for (int i = 0; i < n; i++)
                    {
                        perFrame?.Invoke();
                        motor.Tick(Dt);
                        cmd.Jump = false;
                        Physics.SyncTransforms();
                        if (!motor.IsGrounded) airborne++;
                    }
                    return airborne;
                }

                // 1) walk / run speeds on flat floor
                motor.Teleport(new Vector3(0f, 0.05f, -16f), 90f);
                Simulate(0.5f);
                cmd = new MotorCommand { Move = Vector2.up };
                steps = 0;
                Vector3 p0 = motor.transform.position;
                Simulate(2f);
                float walkSpeed = motor.HorizontalSpeed;
                Check("walk speed", Mathf.Abs(walkSpeed - motor.WalkSpeed) < 0.15f, $"{walkSpeed:0.00} m/s, state {motor.State}, steps {steps} over {(motor.transform.position - p0).magnitude:0.0} m");
                cmd.Run = true;
                Simulate(1f);
                Check("run speed", Mathf.Abs(motor.HorizontalSpeed - motor.RunSpeed) < 0.2f && motor.IsRunning, $"{motor.HorizontalSpeed:0.00} m/s, state {motor.State}");
                cmd = new MotorCommand();
                Simulate(0.6f);
                Check("stops", motor.HorizontalSpeed < 0.05f && motor.State == Core.LocomotionState.Idle, $"{motor.HorizontalSpeed:0.00} m/s");

                // 2) stairs up, then the gentler stairs down
                motor.Teleport(new Vector3(-10f, 0.05f, -9.5f), 0f);
                Simulate(0.3f);
                cmd = new MotorCommand { Move = Vector2.up };
                int air = Simulate(3.0f);
                Check("climb stairs", motor.transform.position.y > 1.9f, $"y {motor.transform.position.y:0.00} z {motor.transform.position.z:0.00}, airborne frames {air}");
                air = Simulate(4.0f);
                Check("descend stairs", motor.transform.position.y < 0.1f && air < 12, $"y {motor.transform.position.y:0.00} z {motor.transform.position.z:0.00}, airborne frames {air}");

                // 3) ramp 18 up, then 30 degree ramp down
                motor.Teleport(new Vector3(10f, 0.05f, -11f), 0f);
                Simulate(0.3f);
                cmd = new MotorCommand { Move = Vector2.up, Run = true };
                air = Simulate(1.8f);
                Check("ramp up (run)", motor.transform.position.y > 1.9f, $"y {motor.transform.position.y:0.00} z {motor.transform.position.z:0.00}, airborne frames {air}");
                landings = 0;
                air = Simulate(2.2f);
                Check("ramp down 30 (run)", motor.transform.position.y < 0.1f && air < 8 && landings == 0, $"y {motor.transform.position.y:0.00} z {motor.transform.position.z:0.00}, airborne frames {air}, landings {landings}");

                // 4) steep wedge cannot be climbed
                motor.Teleport(new Vector3(5.5f, 0.05f, 8.5f), 0f);
                Simulate(0.3f);
                cmd = new MotorCommand { Move = Vector2.up };
                Simulate(2f);
                Check("steep 55 blocked", motor.transform.position.y < 0.6f, $"y {motor.transform.position.y:0.00}");

                // 5) standing cannot enter tunnel (roof at 1.3 m)
                motor.Teleport(new Vector3(0f, 0.05f, 6.5f), 0f);
                Simulate(0.3f);
                cmd = new MotorCommand { Move = Vector2.up };
                Simulate(1.5f);
                Check("tunnel blocks standing", motor.transform.position.z < 8.0f, $"z {motor.transform.position.z:0.00}");

                // 6) crouch into tunnel, try to stand inside (blocked), exit and stand
                cmd = new MotorCommand { Move = Vector2.up, Crouch = true };
                Simulate(2.5f);
                bool inside = motor.transform.position.z > 8.6f && motor.transform.position.z < 12.4f;
                Check("crouch enters tunnel", inside && motor.CrouchAmount > 0.99f, $"z {motor.transform.position.z:0.00}, crouch {motor.CrouchAmount:0.00}, speed {motor.HorizontalSpeed:0.00}, state {motor.State}");
                if (!inside)
                {
                    var cc = motor.GetComponent<CharacterController>();
                    var feet = motor.transform.position;
                    var hits = Physics.OverlapCapsule(feet + new Vector3(0, 0.3f, 0.12f), feet + new Vector3(0, cc.height - 0.3f, 0.12f), 0.3f);
                    log.Append($"  DIAG  cc.height {cc.height:0.00} center {cc.center} bounds {cc.bounds} overlaps:");
                    foreach (var h in hits) log.Append(" " + h.name);
                    log.AppendLine();
                }
                cmd = new MotorCommand();
                Simulate(1f);
                Check("cannot stand under roof", motor.CrouchAmount > 0.95f && motor.IsCrouching, $"crouch {motor.CrouchAmount:0.00}");
                cmd = new MotorCommand { Move = Vector2.up, Run = true };
                Simulate(2.5f);
                Check("stands after exit", motor.CrouchAmount < 0.01f && motor.transform.position.z > 13f, $"z {motor.transform.position.z:0.00}, crouch {motor.CrouchAmount:0.00}");

                // 7) jump
                motor.Teleport(new Vector3(0f, 0.05f, -10f), 0f);
                Simulate(0.5f);
                cmd = new MotorCommand();
                float baseY = motor.transform.position.y, maxY = baseY;
                landings = 0;
                cmd.Jump = true;
                Simulate(1.2f, () => maxY = Mathf.Max(maxY, motor.transform.position.y));
                Check("jump", maxY - baseY > 0.95f && maxY - baseY < 1.15f && landings == 1,
                    $"apex {maxY - baseY:0.00} m, landings {landings}, impact {lastImpact:0.0} m/s");

                // 8) coyote: run off the 2 m stair platform side and jump shortly after leaving the edge
                // (top of the 1 m JumpBox, walking off its +X edge, jump requested ~70 ms after leaving it)
                motor.Teleport(new Vector3(5.3f, 1.05f, 1.2f), 90f);
                Simulate(0.3f);
                cmd = new MotorCommand { Move = Vector2.up };
                bool jumped = false;
                float leftGroundAt = -1f, t = 0f;
                motor.Jumped += () => jumped = true;
                Simulate(1.5f, () =>
                {
                    t += Dt;
                    if (!motor.IsGrounded && leftGroundAt < 0f) leftGroundAt = t;
                    if (leftGroundAt > 0f && !jumped && t - leftGroundAt >= 0.065f && t - leftGroundAt < 0.085f) cmd.Jump = true;
                });
                Check("coyote jump", jumped, $"left ledge at {leftGroundAt:0.00}s, jumped {jumped}");

                motor.CommandProvider = null;
            }
            catch (Exception e)
            {
                log.AppendLine("  EXCEPTION " + e);
                failures++;
            }
            log.AppendLine(failures == 0 ? "[MotorSmoke] ALL PASSED" : $"[MotorSmoke] {failures} FAILURE(S)");
            Debug.Log(log.ToString());
            return failures;
        }
    }
}
