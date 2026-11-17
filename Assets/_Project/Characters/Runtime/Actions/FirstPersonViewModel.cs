using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person arms + held item for the local player.
    /// <para>
    /// Rendering (Built-in RP): everything lives on layer <see cref="Layer"/> ("ViewModel", 9) and is drawn by a second
    /// camera parented to the gameplay camera (same position / rotation, clear = depth only, culling = ViewModel only,
    /// rendered after the main camera, near clip 0.01). The gameplay camera stops rendering layer 9. Depth is cleared
    /// before the arms are drawn, so they never clip into walls, and because the rig sits at real world positions just
    /// in front of the eye, lights (flashlight, muzzle flash) light it correctly. The view-model camera keeps a fixed
    /// FOV so gameplay FOV changes (sprint / zoom) do not stretch the arms. Renderers cast / receive no shadows.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class FirstPersonViewModel : MonoBehaviour, IFirstPersonViewModel
    {
        public const int Layer = 9;
        public const string LayerName = "ViewModel";
        public const float ViewModelFov = 60f;

        [Tooltip("Advance in LateUpdate. Disable to drive with Tick().")]
        public bool autoUpdate = true;

        const float Px = BlockyCharacter.PixelSize;
        static readonly Vector3 ShoulderR = new Vector3(0.28f, -0.52f, 0.35f);
        static readonly Vector3 ShoulderL = new Vector3(-0.28f, -0.52f, 0.35f);
        static readonly Vector3 HiddenLeft = new Vector3(-0.32f, -0.95f, 0.4f);
        static readonly Vector3 AimPoint = new Vector3(0f, 0f, 8f);

        Transform cam;
        Camera vmCamera;
        Transform rig;
        Transform shR, elR, socketR, shL, elL;
        readonly HeldProp[] props = new HeldProp[4];
        GameObject handMag, dropMag;
        Vector3 dropVel;
        float dropAge = 99f;
        Material skinMaterial;
        ICharacterMotionSource motion;
        bool visible = true;

        HeldItem heldItem, shownItem;
        float switchTime = 99f;
        const float LowerTime = 0.16f, RaiseTime = 0.22f;

        enum Action { None, Knife, Reload }
        Action action;
        float actionTime;

        float kick, kickVel, slideKick;
        float leftW;
        Quaternion lastCamRot;
        bool hasLastRot;
        float swayYaw, swayPitch, swayYawVel, swayPitchVel;
        float bobPhase, bobAmount, time;

        public Transform MuzzlePoint
        {
            get
            {
                if (!visible || shownItem != HeldItem.Glock) return null;
                var p = props[(int)HeldItem.Glock];
                return p != null && p.gameObject.activeInHierarchy ? p.Muzzle : null;
            }
        }

        public HeldItem HeldItem => heldItem;
        public Camera ViewModelCamera => vmCamera;

        // ================================================================== creation

        /// <summary>
        /// Builds the view model under <paramref name="cameraTransform"/>: arms with the skin's colours, a view-model
        /// camera, and removes layer 9 from the gameplay camera's culling mask.
        /// </summary>
        public static FirstPersonViewModel Create(Transform cameraTransform, int skinIndex, string nick)
        {
            var go = new GameObject("FirstPersonViewModel");
            go.layer = Layer;
            go.transform.SetParent(cameraTransform, false);
            var vm = go.AddComponent<FirstPersonViewModel>();
            vm.Build(cameraTransform, skinIndex);
            return vm;
        }

        void Build(Transform cameraTransform, int skinIndex)
        {
            cam = cameraTransform;
            var main = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (main != null) main.cullingMask &= ~(1 << Layer);

            var camGo = new GameObject("ViewModelCamera");
            camGo.transform.SetParent(transform, false);
            vmCamera = camGo.AddComponent<Camera>();
            vmCamera.clearFlags = CameraClearFlags.Depth;
            vmCamera.cullingMask = 1 << Layer;
            vmCamera.fieldOfView = ViewModelFov;
            vmCamera.nearClipPlane = 0.01f;
            vmCamera.farClipPlane = 10f;
            vmCamera.useOcclusionCulling = false;
            if (main != null)
            {
                vmCamera.depth = main.depth + 1f;
                vmCamera.allowHDR = main.allowHDR;
                vmCamera.allowMSAA = main.allowMSAA;
                vmCamera.renderingPath = main.renderingPath;
                vmCamera.targetTexture = main.targetTexture;
            }

            rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);
            rig.gameObject.layer = Layer;

            shR = Part("ShoulderR", rig, ShoulderR, BlockyMeshBuilder.Part.UpperArmRight);
            elR = Part("ElbowR", shR, BlockyCharacter.ElbowRest, BlockyMeshBuilder.Part.LowerArmRight);
            shL = Part("ShoulderL", rig, ShoulderL, BlockyMeshBuilder.Part.UpperArmLeft);
            elL = Part("ElbowL", shL, BlockyCharacter.ElbowRest, BlockyMeshBuilder.Part.LowerArmLeft);
            socketR = new GameObject("HandSocketR").transform;
            socketR.SetParent(elR, false);
            socketR.localPosition = CharacterPose.HandLocal;
            socketR.gameObject.layer = Layer;
            SetSkin(skinIndex);
            Tick(0.0001f);
        }

        Transform Part(string n, Transform parent, Vector3 pos, BlockyMeshBuilder.Part part)
        {
            var t = new GameObject(n).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.gameObject.layer = Layer;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = BlockyMeshBuilder.GetMesh(part);
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return t;
        }

        /// <summary>Re-skins the arms.</summary>
        public void SetSkin(int skinIndex)
        {
            var skin = SkinLibrary.GetSkin(skinIndex);
            skinMaterial = skin != null ? skin.Material : null;
            if (skinMaterial == null)
            {
                skinMaterial = new Material(Shader.Find("Standard")) { hideFlags = HideFlags.DontSave, color = new Color(0.85f, 0.7f, 0.6f) };
            }

            foreach (var t in new[] { shR, elR, shL, elL })
                t.GetComponent<MeshRenderer>().sharedMaterial = skinMaterial;
        }

        HeldProp GetProp(HeldItem item)
        {
            if (item == HeldItem.None) return null;
            var p = props[(int)item];
            if (p != null) return p;
            p = BlockyProps.Create(item, socketR, Layer);
            p.SetRenderMode(ShadowCastingMode.Off, false);
            p.gameObject.SetActive(false);
            props[(int)item] = p;
            return p;
        }

        // ================================================================== IFirstPersonViewModel

        public void SetHeldItem(HeldItem item)
        {
            if (item == heldItem) return;
            heldItem = item;
            if (action != Action.None) action = Action.None;
            // Lower, swap at the bottom, raise again (instant when hidden).
            switchTime = visible ? 0f : 99f;
            if (!visible) shownItem = item;
        }

        public float PlayKnifeAttack()
        {
            action = Action.Knife;
            actionTime = 0f;
            return ActionTimeline.KnifeAttackDuration;
        }

        public void PlayShoot()
        {
            kickVel += 34f;
            slideKick = 0.07f;
        }

        public float PlayReload()
        {
            action = Action.Reload;
            actionTime = 0f;
            return ActionTimeline.ReloadDuration;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (rig != null) rig.gameObject.SetActive(value);
            if (vmCamera != null) vmCamera.enabled = value;
        }

        // ================================================================== update

        void LateUpdate()
        {
            if (autoUpdate) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (rig == null || dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);
            time += dt;
            if (motion == null && cam != null) motion = cam.GetComponentInParent<ICharacterMotionSource>();

            // Item switching.
            float lower = 0f;
            switchTime += dt;
            if (switchTime < LowerTime) lower = ActionTimeline.SmoothStep01(switchTime / LowerTime);
            else
            {
                shownItem = heldItem;
                if (switchTime < LowerTime + RaiseTime) lower = 1f - ActionTimeline.SmoothStep01((switchTime - LowerTime) / RaiseTime);
            }

            HeldItem show = action == Action.Knife ? HeldItem.Knife : action == Action.Reload ? HeldItem.Glock : shownItem;

            // Action clock.
            float prev = actionTime;
            if (action != Action.None)
            {
                actionTime += dt;
                if (action == Action.Reload && prev < ActionTimeline.ReloadMagOutTime && actionTime >= ActionTimeline.ReloadMagOutTime) DropMag();
                float dur = action == Action.Knife ? ActionTimeline.KnifeAttackDuration : ActionTimeline.ReloadDuration;
                if (actionTime >= dur) action = Action.None;
            }

            // Springs / sway / bob.
            StepSpring(ref kick, ref kickVel, 380f, 0.5f, dt);
            slideKick = Mathf.Max(0f, slideKick - dt);
            if (cam != null)
            {
                Quaternion rot = cam.rotation;
                if (hasLastRot)
                {
                    Vector3 d = (Quaternion.Inverse(lastCamRot) * rot).eulerAngles;
                    float yawRate = Mathf.DeltaAngle(0f, d.y) / dt, pitchRate = Mathf.DeltaAngle(0f, d.x) / dt;
                    float ty = Mathf.Clamp(-yawRate * 0.012f, -5f, 5f), tp = Mathf.Clamp(-pitchRate * 0.012f, -4f, 4f);
                    swayYaw = Mathf.SmoothDamp(swayYaw, ty, ref swayYawVel, 0.09f, 200f, dt);
                    swayPitch = Mathf.SmoothDamp(swayPitch, tp, ref swayPitchVel, 0.09f, 200f, dt);
                }

                lastCamRot = rot;
                hasLastRot = true;
            }

            float speed = motion != null && motion.IsGrounded ? motion.HorizontalSpeed : 0f;
            bobAmount = Mathf.Lerp(bobAmount, Mathf.Clamp01(speed / 4f), 1f - Mathf.Exp(-6f * dt));
            bobPhase += speed * dt / 1.5f * Mathf.PI * 2f;
            if (bobPhase > 1000f) bobPhase -= 1000f;
            float breath = Mathf.Sin(time * 1.7f);

            Vector3 pos = new Vector3(Mathf.Sin(bobPhase) * 0.014f * bobAmount, -Mathf.Abs(Mathf.Cos(bobPhase)) * 0.014f * bobAmount + 0.003f * breath, 0f);
            pos += new Vector3(-swayYaw * 0.004f, swayPitch * 0.003f, 0f);
            pos += new Vector3(0f, 0.015f * kick, -0.05f * kick);
            pos += new Vector3(0f, -0.3f * lower, 0f);
            Quaternion r = Quaternion.Euler(swayPitch + 0.35f * breath - 7f * kick, swayYaw, swayYaw * 0.6f + 2f * kick) * Quaternion.Euler(25f * lower, 0f, 0f);

            // Knife attack: whole rig lunges a little with each thrust.
            float s = action == Action.Knife ? ActionTimeline.StabCurve(actionTime) : 0f;
            pos += new Vector3(0f, 0f, 0.1f * Mathf.Max(0f, s) - 0.03f * Mathf.Max(0f, -s));
            rig.localPosition = pos;
            rig.localRotation = r;

            // Hand targets (rig space).
            Vector3 rightT;
            Quaternion propRot;
            Vector3 leftT = HiddenLeft;
            float wantLeft = 0f;
            bool magInGun = true, magInHand = false;
            float slide = slideKick > 0.035f ? 1f : slideKick / 0.035f;
            Vector3 gunPosForMag = Vector3.zero;
            Quaternion gunRotForMag = Quaternion.identity;

            switch (show)
            {
                case HeldItem.Glock:
                {
                    Vector3 gPos = new Vector3(0.16f, -0.3f, 0.78f);
                    Quaternion gRot = Quaternion.LookRotation(AimPoint - (gPos + Vector3.up * 0.14f), Vector3.up) * Quaternion.Euler(-12f * kick, 0f, 0f);
                    Vector3 leftLocal = ActionTimeline.SupportGripLocal;
                    float atBelt = 0f;
                    if (action == Action.Reload)
                    {
                        var rs = ActionTimeline.SampleReload(actionTime);
                        gPos = Vector3.Lerp(gPos, new Vector3(0.08f, -0.26f, 0.74f), rs.GunTilt) + new Vector3(0f, 0.02f * rs.Jolt, 0f);
                        gRot = Quaternion.Slerp(gRot, gRot * Quaternion.Euler(-18f, -12f, -38f), rs.GunTilt) * Quaternion.Euler(-5f * rs.Jolt, 0f, 0f);
                        leftLocal = rs.LeftGunLocal;
                        atBelt = rs.LeftAtBelt;
                        slide = Mathf.Max(slide, rs.SlideBack);
                        magInGun = rs.MagInGun;
                        magInHand = rs.MagInHand;
                    }

                    rightT = gPos;
                    propRot = gRot;
                    SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.5f));
                    Vector3 gun = HandPos(shR, elR);
                    leftT = Vector3.Lerp(gun + gRot * leftLocal, new Vector3(-0.32f, -0.85f, 0.45f), atBelt);
                    wantLeft = 1f;
                    gunPosForMag = gun;
                    gunRotForMag = gRot;
                    break;
                }
                case HeldItem.Knife:
                {
                    Vector3 rest = new Vector3(0.27f, -0.36f, 0.72f);
                    Quaternion restRot = Quaternion.LookRotation(new Vector3(-0.3f, 0.35f, 1f), Vector3.up);
                    rightT = rest;
                    propRot = restRot;
                    if (action == Action.Knife)
                    {
                        float e = ActionTimeline.SmoothStep01(actionTime / 0.25f) * (1f - ActionTimeline.SmoothStep01(ActionTimeline.Remap01(actionTime, 2.15f, 2.4f)));
                        Vector3 cocked = new Vector3(0.32f, -0.5f, 0.55f), hit = new Vector3(0.08f, -0.32f, 0.88f);
                        Vector3 ready = Vector3.Lerp(cocked, hit, 0.5f) + new Vector3(0f, 0.03f, 0f);
                        Vector3 st = s < 0f ? Vector3.Lerp(ready, cocked, -s) : Vector3.LerpUnclamped(ready, hit, s);
                        rightT = Vector3.Lerp(rest, st, e);
                        Quaternion stabRot = Quaternion.LookRotation((hit - cocked).normalized + Vector3.up * 0.1f * Mathf.Max(0f, -s), Vector3.up);
                        propRot = Quaternion.Slerp(restRot, stabRot, e);
                        float g = ActionTimeline.SmoothStep01(ActionTimeline.Remap01(actionTime, 0.05f, 0.3f)) * (1f - ActionTimeline.SmoothStep01(ActionTimeline.Remap01(actionTime, 2.08f, 2.35f)));
                        leftT = Vector3.Lerp(HiddenLeft, new Vector3(-0.14f, -0.28f, 0.78f) + new Vector3(0f, -0.02f, 0f) * Mathf.Max(0f, s), g);
                        wantLeft = g;
                    }

                    SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.4f));
                    break;
                }
                case HeldItem.Medkit:
                    rightT = new Vector3(0.26f, -0.24f, 0.68f);
                    propRot = Quaternion.Euler(0f, -25f, 0f);
                    SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.4f));
                    break;
                default:
                    rightT = new Vector3(0.28f, -0.4f, 0.68f);
                    propRot = Quaternion.identity;
                    SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.4f));
                    break;
            }

            leftW = wantLeft;
            SolveArm(shL, elL, ShoulderL, Vector3.Lerp(HiddenLeft, leftT, leftW), new Vector3(-1f, -1f, -0.4f));
            bool showLeft = leftW > 0.01f;
            if (shL.gameObject.activeSelf != showLeft) shL.gameObject.SetActive(showLeft);

            // Props.
            if (show != HeldItem.None) GetProp(show);
            for (int i = 1; i < props.Length; i++)
                if (props[i] != null && props[i].gameObject.activeSelf != ((HeldItem)i == show))
                    props[i].gameObject.SetActive((HeldItem)i == show);
            socketR.rotation = rig.rotation * propRot;
            var glock = props[(int)HeldItem.Glock];
            if (glock != null && show == HeldItem.Glock)
            {
                glock.SetSlideBack(slide);
                glock.SetMagVisible(magInGun);
            }

            if (magInHand)
            {
                if (handMag == null) handMag = MakeMag(rig);
                handMag.SetActive(true);
                Vector3 handL = HandPos(shL, elL);
                handMag.transform.localPosition = handL - gunRotForMag * ActionTimeline.MagInsertHandLocal;
                handMag.transform.localRotation = gunRotForMag;
            }
            else if (handMag != null && handMag.activeSelf) handMag.SetActive(false);

            // Ejected mag falls out of view.
            if (dropMag != null)
            {
                dropAge += dt;
                dropVel += Vector3.down * 9f * dt;
                dropMag.transform.localPosition += dropVel * dt;
                dropMag.transform.localRotation *= Quaternion.Euler(200f * dt, 0f, 90f * dt);
                if (dropAge > 0.5f) dropMag.SetActive(false);
            }
        }

        GameObject MakeMag(Transform parent)
        {
            var m = BlockyProps.CreateMagazine(parent, Layer);
            foreach (var r in m.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            return m;
        }

        void DropMag()
        {
            var glock = props[(int)HeldItem.Glock];
            if (glock == null) return;
            if (dropMag == null) dropMag = MakeMag(rig);
            dropMag.SetActive(true);
            dropMag.transform.position = glock.transform.position;
            dropMag.transform.rotation = glock.transform.rotation;
            dropVel = new Vector3(0.1f, -0.5f, 0f);
            dropAge = 0f;
        }

        static void SolveArm(Transform shoulder, Transform elbow, Vector3 shoulderPos, Vector3 target, Vector3 pole)
        {
            RigIK.SolveArm(shoulderPos, Quaternion.identity, target, pole, out var sh, out var el);
            shoulder.localPosition = shoulderPos;
            shoulder.localRotation = sh;
            elbow.localRotation = el;
        }

        /// <summary>Fist centre in rig space.</summary>
        static Vector3 HandPos(Transform shoulder, Transform elbow)
        {
            Quaternion up = shoulder.localRotation;
            return shoulder.localPosition + up * BlockyCharacter.ElbowRest + up * elbow.localRotation * CharacterPose.HandLocal;
        }

        static void StepSpring(ref float x, ref float v, float k, float zeta, float dt)
        {
            float c = 2f * zeta * Mathf.Sqrt(k);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += (-k * x - c * v) * h;
                x += v * h;
            }
        }
    }
}
