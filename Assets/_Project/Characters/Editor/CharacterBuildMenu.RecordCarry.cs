using System;
using System.IO;
using HideAndSeek.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Body carry in the lit studio (no network), for tuning and review videos. Command line (no -nographics):
    ///   set HNS_REC_OUT=C:\out &amp; set HNS_CARRY=stills,floor,stand,drop,mirror,leg
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod HideAndSeek.Characters.Editor.CharacterBuildMenu.RecordCarryAnimations
    /// "stills" = the carry pose from 6 angles + the carrier's first-person view; the others are 30 fps clips
    /// (lift, carry standing / walking, put down or drop, settling afterwards). HNS_REC_STEP / HNS_REC_SIZE as usual.
    /// </summary>
    public static partial class CharacterBuildMenu
    {
        public static void RecordCarryAnimations()
        {
            bool ok = false;
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            var prevSim = Physics.simulationMode;
            try
            {
                recOut = Environment.GetEnvironmentVariable("HNS_REC_OUT");
                if (string.IsNullOrEmpty(recOut)) recOut = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Logs", "carryrec");
                if (int.TryParse(Environment.GetEnvironmentVariable("HNS_REC_STEP"), out int st) && st > 0) recStep = st;
                var size = Environment.GetEnvironmentVariable("HNS_REC_SIZE");
                if (!string.IsNullOrEmpty(size) && size.Contains("x"))
                {
                    var parts = size.Split('x');
                    int.TryParse(parts[0], out recW);
                    int.TryParse(parts[1], out recH);
                }
                BuildAssets();
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildStudio(false);
                Physics.simulationMode = SimulationMode.Script;
                var floorCol = new GameObject("RecFloor").AddComponent<BoxCollider>();
                floorCol.size = new Vector3(60f, 0.2f, 60f);
                floorCol.center = new Vector3(0f, -0.1f, 0f);
                recCam = new GameObject("Rec Camera").AddComponent<Camera>();
                ConfigureCamera(recCam);
                recCam.nearClipPlane = 0.02f;
                recCam.enabled = false;
                string list = Environment.GetEnvironmentVariable("HNS_CARRY") ?? "stills";
                foreach (var raw in list.Split(','))
                {
                    string item = raw.Trim();
                    if (item.Length == 0) continue;
                    try { RecordCarry(item); }
                    catch (Exception e) { Debug.LogException(e); }
                }
                ok = true;
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                ShaderUtil.allowAsyncCompilation = prevAsync;
                Physics.simulationMode = prevSim;
            }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        sealed class CarryRig
        {
            public BlockyCharacter Carrier, Carried;
            public BlockyCharacterAnimator CarrierAnim, CarriedAnim;
            public PreviewMotion CarrierMotion;
            public BodyCarryState State;

            public void Tick(float dt)
            {
                State.Time += dt;
                CarrierAnim.Tick(dt);
                CarriedAnim.Tick(dt);
                Physics.Simulate(dt);
            }

            public void SetPhase(BodyCarryPhase phase)
            {
                State.Phase = phase;
                State.Time = 0f;
                if (phase == BodyCarryPhase.PutDown || phase == BodyCarryPhase.Drop)
                {
                    var ct = Carrier.transform;
                    BodyCarryTimeline.EndFrame(ct.position, ct.rotation, State.Mirror, State.EndStanding && phase == BodyCarryPhase.PutDown, 1f,
                        out State.EndPos, out State.EndRot);
                    State.HasEnd = true;
                }
            }

            /// <summary>Ends the carry like the game does: the carried's root jumps to the end frame.</summary>
            public void Release()
            {
                Carrier.Actions.EndBodyCarry();
                if (State.HasEnd) Carried.transform.parent.SetPositionAndRotation(State.EndPos, State.EndRot);
            }
        }

        static CarryRig SpawnCarry(bool downed, bool mirror, int severed = 0)
        {
            var rig = new CarryRig();
            rig.Carrier = SpawnAt(1, Vector3.zero, 0f, out rig.CarrierMotion, out rig.CarrierAnim);
            // Body where the carrier would have stepped to: downed head to his left, standing facing his right.
            Vector3 root;
            float yaw;
            if (downed)
            {
                Vector3 pelvis = BodyCarryTimeline.FloorPelvisLocal;
                if (mirror) pelvis.x = -pelvis.x;
                Vector3 head = mirror ? Vector3.right : Vector3.left;
                root = pelvis + head * BodyCarryTimeline.LyingPelvisBack;
                yaw = mirror ? 90f : -90f;
            }
            else
            {
                root = BodyCarryTimeline.StandRootLocal;
                if (mirror) root.x = -root.x;
                yaw = mirror ? -90f : 90f;
            }
            rig.Carried = SpawnAt(3, root, yaw, out _, out rig.CarriedAnim);
            if (severed != 0)
            {
                rig.Carried.SetSevered(severed);
                RecStumps(rig.Carried, severed);
            }
            if (downed) rig.Carried.Actions.SetLifeStateImmediate(LifeState.Downed);
            else rig.Carried.Actions.SetLimp(1f);
            TickAll(0.6f, rig.CarrierAnim, rig.CarriedAnim);
            rig.State = new BodyCarryState { Phase = BodyCarryPhase.Lift, FromFloor = downed, Conscious = !downed, Mirror = mirror, EndStanding = !downed };
            rig.Carrier.Actions.BeginBodyCarry(rig.Carried.Actions, rig.State);
            return rig;
        }

        static void RecordCarry(string item)
        {
            Vector3 mid = new Vector3(0f, 0.95f, 0.25f);
            switch (item)
            {
                case "stills":
                case "stillsm":
                {
                    bool m = item == "stillsm";
                    var rig = SpawnCarry(true, m);
                    rig.SetPhase(BodyCarryPhase.Hold);
                    for (int i = 0; i < 60; i++) rig.Tick(RecDt);
                    var clip = NewClip("carry_" + item);
                    LogCarry(rig);
                    foreach (float yaw in new[] { 0f, 40f, 90f, 150f, 210f, 300f })
                    {
                        Orbit(mid, yaw, 4.3f, 0.35f);
                        clip.Shot(recCam);
                    }
                    StillsFpp(rig, clip);
                    rig.State.Conscious = true;
                    for (int i = 0; i < 20; i++) rig.Tick(RecDt);
                    foreach (float yaw in new[] { 20f, 300f })
                    {
                        Orbit(mid, yaw, 4.3f, 0.35f);
                        clip.Shot(recCam);
                    }
                    LogCarry(rig);
                    Kill(rig.Carrier);
                    Kill(rig.Carried);
                    break;
                }
                case "floor":
                case "stand":
                case "mirror":
                case "leg":
                case "drop":
                {
                    bool downed = item != "stand" && item != "leg";
                    var rig = SpawnCarry(downed, item == "mirror", item == "leg" ? Limb.LegR : 0);
                    var clip = NewClip("carry_" + item);
                    float lift = BodyCarryTimeline.Lift(downed);
                    float total = 0f;
                    void Run(float seconds, float speed)
                    {
                        for (float t = 0f; t < seconds; t += RecDt)
                        {
                            SetMove(rig.CarrierMotion, speed);
                            rig.Tick(RecDt);
                            total += RecDt;
                            Orbit(mid, 35f + 50f * Mathf.Sin(total * 0.35f), 4.3f, 0.35f);
                            clip.Shot(recCam);
                        }
                    }
                    Run(lift, 0f);
                    rig.SetPhase(BodyCarryPhase.Hold);
                    Run(1.2f, 0f);
                    Run(2.4f, 1.8f);
                    Run(0.5f, 0f);
                    if (item == "drop")
                    {
                        rig.SetPhase(BodyCarryPhase.Drop);
                        Run(BodyCarryTimeline.DropTime, 0f);
                    }
                    else
                    {
                        rig.SetPhase(BodyCarryPhase.PutDown);
                        Run(BodyCarryTimeline.PutDown(rig.State.EndStanding), 0f);
                    }
                    rig.Release();
                    for (float t = 0f; t < 2.0f; t += RecDt)
                    {
                        rig.CarrierAnim.Tick(RecDt);
                        rig.CarriedAnim.Tick(RecDt);
                        Physics.Simulate(RecDt);
                        total += RecDt;
                        Orbit(mid, 35f + 50f * Mathf.Sin(total * 0.35f), 4.3f, 0.35f);
                        clip.Shot(recCam);
                    }
                    Debug.Log($"[CarryRec] {item}: {clip.Frame} frames");
                    Kill(rig.Carrier);
                    Kill(rig.Carried);
                    ClearGibs();
                    break;
                }
            }
        }

        static void LogCarry(CarryRig rig)
        {
            var c = rig.Carried;
            var k = rig.Carrier;
            string P(Transform t) => t == null ? "-" : $"({t.position.x:0.00},{t.position.y:0.00},{t.position.z:0.00})";
            Vector3 hr = k.ElbowRight.TransformPoint(CharacterPose.HandLocal), hl = k.ElbowLeft.TransformPoint(CharacterPose.HandLocal);
            Debug.Log($"[CarryRec] carried pelvis {P(c.Pelvis)} torso {P(c.Torso)} head {P(c.Head)} kneeR {P(c.KneeRight)} kneeL {P(c.KneeLeft)} " +
                      $"shR {P(c.ShoulderRight)} shL {P(c.ShoulderLeft)} | carrier pelvis {P(k.Pelvis)} head {P(k.Head)} shR {P(k.ShoulderRight)} " +
                      $"handR ({hr.x:0.00},{hr.y:0.00},{hr.z:0.00}) handL ({hl.x:0.00},{hl.y:0.00},{hl.z:0.00}) up {c.Torso.up} fwd {c.Torso.forward}");
        }

        /// <summary>Carrier's eyes (head hidden like in the game), looking ahead and a little down.</summary>
        static void StillsFpp(CarryRig rig, Clip clip)
        {
            rig.Carrier.SetHeadHidden(true);
            var head = rig.Carrier.Head;
            foreach (float pitch in new[] { 5f, 25f, 50f })
            {
                Quaternion look = rig.Carrier.transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
                recCam.transform.SetPositionAndRotation(head.position + look * new Vector3(0f, 0.255f, 0.1f), look);
                recCam.fieldOfView = 70f;
                clip.Shot(recCam);
            }
            rig.Carrier.SetHeadHidden(false);
        }
    }
}
