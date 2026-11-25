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
    public partial class FirstPersonViewModel : MonoBehaviour, IFirstPersonViewModel
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
        readonly HeldProp[] props = new HeldProp[12];
        GameObject handMag, dropMag;
        Vector3 dropVel;
        float dropAge = 99f;
        Material skinMaterial;
        ICharacterMotionSource motion;
        bool visible = true;

        HeldItem heldItem, shownItem;
        bool revving;
        HoldAnim holdAnim;
        float holdTime;
        Vector3? holdTarget;
        HeldProp vmKit;

        /// <summary>First-person side of the injection / self-heal holds (time in seconds since the hold started).</summary>
        public void SetHoldAnimation(HoldAnim mode, float t, Vector3? worldTarget = null)
        {
            holdAnim = mode;
            holdTime = t;
            holdTarget = worldTarget;
        }

        static float Sg(float t, float a, float b) => HealTimeline.Seg(t, a, b);

        float pokeTime = 9f;
        Color brickTint = Color.white;

        /// <summary>Colour of the toy brick in the hand (lobby building).</summary>
        public void SetBrickTint(Color c) => brickTint = c;

        /// <summary>Quick push forward of the hand (a brick is put down).</summary>
        public void PlayPoke() => pokeTime = 0f;

        bool overrideOn, overrideLeft;
        Vector3 overrideRight, overrideLeftPos;
        Quaternion overrideRot;
        HeldItem? overrideItem;

        /// <summary>
        /// Scripted hands in world space (fork into a socket, arcade controls, RC car put on the floor). Left hand optional;
        /// <paramref name="item"/> replaces the shown item while the override is on.
        /// </summary>
        public void SetOverrideHands(bool on, Vector3 rightWorld, Quaternion propWorld, Vector3? leftWorld = null, HeldItem? item = null)
        {
            overrideOn = on;
            overrideItem = item;
            overrideRight = rightWorld;
            overrideRot = propWorld;
            overrideLeft = leftWorld.HasValue;
            if (leftWorld.HasValue) overrideLeftPos = leftWorld.Value;
        }

        /// <summary>Hands for the hold animations in rig space. Returns false when no hold animation is running.</summary>
        bool HoldHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            float t = holdTime;
            toyHands = false;
            if (FlashTakeHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true;
            if (PhoneHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true;
            if (RemoteThrowHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true;
            if (ExtinguisherHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true; // extinguisher / foam wiping
            if (LegThrowHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true; // FirstPersonViewModel.LegThrow.cs
            if (PaMicHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true;
            if (ShoulderRadioHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true;
            if (VendingHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true;
            if (overrideOn && rig != null)
            {
                rightT = rig.InverseTransformPoint(overrideRight);
                propRot = Quaternion.Inverse(rig.rotation) * overrideRot;
                if (overrideItem.HasValue) show = overrideItem.Value;
                SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -0.8f, -0.3f));
                if (overrideLeft)
                {
                    leftT = rig.InverseTransformPoint(overrideLeftPos);
                    wantLeft = 1f;
                }
                return true;
            }
            if (holdAnim == HoldAnim.Inject)
            {
                float sc = BlockyProps.SyringeScale;
                Vector3 chest = new Vector3(0.02f, -0.9f, 0.62f);
                if (holdTarget.HasValue && rig != null)
                {
                    chest = rig.InverseTransformPoint(holdTarget.Value);
                    chest = new Vector3(Mathf.Clamp(chest.x, -0.2f, 0.25f), Mathf.Clamp(chest.y, -0.95f, -0.45f), Mathf.Clamp(chest.z, 0.55f, 0.9f));
                }
                Vector3 hidden = new Vector3(0.32f, -0.8f, 0.35f);
                // Kept well away from the lens and right of centre, so the syringe reads and the arm does not fill the view.
                Vector3 front = new Vector3(0.24f, -0.34f, 0.66f);
                Vector3 raised = new Vector3(0.22f, -0.12f, 0.66f);
                Vector3 down = (chest - raised).normalized;
                Vector3 stab = chest - down * (BlockyProps.SyringeTipLocal.z * sc - 0.02f);
                Vector3 dir;
                if (t < 0.95f) { rightT = Vector3.Lerp(hidden, front, Sg(t, 0.45f, 0.95f)); dir = new Vector3(-0.35f, 0.05f, 1f); }
                else if (t < 1.3f) { rightT = front; dir = new Vector3(-0.45f, 0.1f, 1f); }
                else if (t < HealTimeline.InjectStab)
                {
                    float r = Sg(t, 1.3f, 1.56f), st = Mathf.Clamp01((t - 1.58f) / (HealTimeline.InjectStab - 1.58f));
                    rightT = t < 1.58f ? Vector3.Lerp(front, raised, r) : Vector3.Lerp(raised, stab, st * st);
                    dir = Vector3.Slerp(new Vector3(-0.45f, 0.1f, 1f), down, r);
                }
                else if (t < HealTimeline.InjectPullOut) { rightT = stab + down * 0.012f * HealTimeline.InjectPress(t); dir = down; }
                else
                {
                    float po = Sg(t, HealTimeline.InjectPullOut, 3.35f), lo = Sg(t, 3.45f, 3.95f);
                    rightT = Vector3.Lerp(Vector3.Lerp(stab, stab - down * 0.25f, po), hidden, lo);
                    dir = down;
                }
                propRot = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.9f ? Vector3.forward : Vector3.up);
                show = t >= HealTimeline.InjectShow && t < HealTimeline.InjectHide ? HeldItem.Syringe : HeldItem.None;
                SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -0.6f, -0.5f));
                Vector3 hand = HandPos(shR, elR);
                Vector3 tip = hand + propRot * (BlockyProps.SyringeTipLocal * sc);
                Vector3 plunger = hand + propRot * ((BlockyProps.SyringePlungerLocal + new Vector3(0f, 0f, BlockyProps.SyringePlungerTravel * HealTimeline.InjectPress(t))) * sc) - propRot * new Vector3(0f, 0f, 0.05f);
                Vector3 rest = new Vector3(-0.3f, -0.85f, 0.4f);
                if (t < 0.95f) { leftT = rest; wantLeft = 0f; }
                else if (t < 1.35f)
                {
                    leftT = Vector3.Lerp(Vector3.Lerp(rest, tip, Sg(t, 0.95f, 1.1f)), new Vector3(-0.3f, -0.42f, 0.62f), Sg(t, HealTimeline.InjectCapOff, 1.35f));
                    wantLeft = 1f;
                }
                else if (t < 1.62f) { leftT = Vector3.Lerp(new Vector3(-0.3f, -0.42f, 0.62f), rest, Sg(t, 1.35f, 1.6f)); wantLeft = 1f - Sg(t, 1.45f, 1.6f); }
                else if (t < HealTimeline.InjectPullOut) { leftT = Vector3.Lerp(rest, plunger, Sg(t, 1.62f, HealTimeline.InjectPressStart)); wantLeft = 1f; }
                else { leftT = Vector3.Lerp(plunger, rest, Sg(t, HealTimeline.InjectPullOut, 3.4f)); wantLeft = 1f - Sg(t, 3.2f, 3.4f); }
                return true;
            }
            if (holdAnim == HoldAnim.SelfHeal)
            {
                // Looking down at the own belly while sitting: hands circle round it with the roll, then tie.
                // Positions in the body frame (camera position, yaw only), converted to rig space below.
                Vector3 Belly(float a, float r = 1f) => new Vector3(0.2f * Mathf.Sin(a) * r, -0.5f + 0.015f * Mathf.Sin(a * 3f), 0.46f + 0.12f * Mathf.Cos(a) * r);
                Vector3 kit = new Vector3(0.4f, -0.66f, 0.5f);
                Vector3 hiddenR = new Vector3(0.4f, -0.55f, -0.15f), hiddenL = new Vector3(-0.4f, -0.55f, -0.15f);
                Vector3 r, l;
                float wl = 1f;
                if (t < HealTimeline.HealSitEnd) { r = hiddenR; l = hiddenL; wl = 0f; }
                else if (t < HealTimeline.HealKitOpen) { r = Vector3.Lerp(hiddenR, kit, Sg(t, HealTimeline.HealSitEnd, 0.95f)); l = hiddenL; wl = 0f; }
                else if (t < HealTimeline.HealBandageOut)
                {
                    float lid = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - HealTimeline.HealKitOpen) / 0.3f));
                    r = kit + new Vector3(0f, 0.1f * lid, 0.02f * lid);
                    l = hiddenL;
                    wl = 0f;
                }
                else if (t < HealTimeline.HealWrapStart)
                {
                    r = Vector3.Lerp(kit, Belly(0.6f), Sg(t, HealTimeline.HealBandageOut, HealTimeline.HealWrapStart));
                    l = Vector3.Lerp(hiddenL, Belly(-0.4f), Sg(t, HealTimeline.HealBandageOut, HealTimeline.HealWrapStart));
                    wl = Sg(t, HealTimeline.HealBandageOut, HealTimeline.HealWrapStart);
                }
                else if (t < HealTimeline.HealWrapEnd)
                {
                    float cyc = (t - HealTimeline.HealWrapStart) / (HealTimeline.HealWrapEnd - HealTimeline.HealWrapStart) * 3f;
                    float ph = cyc - Mathf.Floor(cyc);
                    float a = ph < 0.6f ? Mathf.Lerp(-0.9f, 2.0f, ActionTimeline.SmoothStep01(ph / 0.6f)) : Mathf.Lerp(2.0f, -0.9f, ActionTimeline.SmoothStep01((ph - 0.6f) / 0.4f));
                    r = Belly(a, ph < 0.6f ? 1f : 1.4f);
                    float b = ph < 0.6f ? -0.5f : Mathf.Lerp(-0.5f, -2.0f, Mathf.Sin(Mathf.PI * (ph - 0.6f) / 0.4f));
                    l = Belly(b);
                }
                else if (t < HealTimeline.HealTieEnd)
                {
                    float pull = Mathf.Abs(Mathf.Sin((t - HealTimeline.HealWrapEnd) / (HealTimeline.HealTieEnd - HealTimeline.HealWrapEnd) * Mathf.PI * 2f));
                    r = new Vector3(0.03f + 0.17f * pull, -0.48f, 0.6f);
                    l = new Vector3(-0.03f - 0.17f * pull, -0.48f, 0.6f);
                }
                else
                {
                    float d = Sg(t, HealTimeline.HealTieEnd, 4.95f);
                    r = Vector3.Lerp(new Vector3(0.05f, -0.48f, 0.6f), hiddenR, d);
                    l = Vector3.Lerp(new Vector3(-0.05f, -0.48f, 0.6f), hiddenL, d);
                    wl = 1f - d;
                }
                Quaternion toRig = BodyToRig();
                rightT = toRig * r;
                leftT = toRig * l;
                wantLeft = wl;
                propRot = toRig * Quaternion.Euler(0f, 90f, 0f);
                show = t >= HealTimeline.HealKitOpen + 0.15f && t < HealTimeline.HealWrapEnd + 0.25f ? HeldItem.Bandage : HeldItem.None;
                SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -0.4f, -0.5f));
                return true;
            }
            if (MedkitHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true;
            if (AntennaSwapHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true; // FirstPersonViewModel.RcWire.cs
            if (RemoteHoldHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) return true;
            return ToyGunHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft);
        }

        /// <summary>Rotation from the body frame (camera yaw only, level) to rig space (camera space).</summary>
        Quaternion BodyToRig()
        {
            if (cam == null) return Quaternion.identity;
            var yawOnly = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
            var rigParent = rig != null && rig.parent != null ? rig.parent.rotation : cam.rotation;
            return Quaternion.Inverse(rigParent) * yawOnly;
        }

        void UpdateVmKit()
        {
            bool med = MedkitOnFloor(out var medPos, out var medRot);
            bool on = (holdAnim == HoldAnim.SelfHeal && holdTime >= HealTimeline.HealSitEnd && holdTime < HealTimeline.HealTieEnd + 0.3f) || med;
            if (on && vmKit == null)
            {
                vmKit = BlockyProps.Create(HeldItem.Medkit, rig, Layer);
                vmKit.SetRenderMode(ShadowCastingMode.Off, false);
            }
            if (vmKit == null) return;
            if (vmKit.gameObject.activeSelf != on) vmKit.gameObject.SetActive(on);
            if (on)
            {
                var toRig = BodyToRig();
                vmKit.transform.localPosition = med ? medPos : toRig * new Vector3(0.42f, -0.72f, 0.52f);
                vmKit.transform.localRotation = med ? medRot : toRig * Quaternion.Euler(0f, 25f, 90f);
            }
        }

        /// <summary>Chainsaw throttle (F held): saw raised, hands shaking.</summary>
        public void SetRevving(bool value) => revving = value;

        private float extSawT = float.NaN;
        private int sawAttempts = 1;

        /// <summary>Drives the pull-start from the synced clock (see BlockyCharacterActions.SetSawStart; NaN = local clock).</summary>
        public void SetSawStart(float t, int attempts)
        {
            extSawT = t;
            sawAttempts = Mathf.Max(1, attempts);
        }

        float sawStartT = 99f;
        float sawBlood;
        HeldItem lastShow;

        /// <summary>Blood on the chain (1 = just killed), set by the game.</summary>
        public void SetSawBlood(float amount) => sawBlood = amount;
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

        private Texture2D customSkin;
        private int lastSkinIndex;

        /// <summary>Uses a downloaded Minecraft skin for the arms (null = library skin).</summary>
        public void SetCustomSkin(Texture2D tex)
        {
            customSkin = tex;
            SetSkin(lastSkinIndex);
        }

        /// <summary>Re-skins the arms.</summary>
        public void SetSkin(int skinIndex)
        {
            lastSkinIndex = skinIndex;
            var skin = SkinLibrary.GetSkin(skinIndex);
            skinMaterial = skin != null ? skin.Material : null;
            if (customSkin != null && skinMaterial != null) skinMaterial = CustomSkins.MaterialFor(customSkin, skinMaterial);
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
            BeginSwitch();
        }

        float slashTime = 99f;

        /// <summary>One quick slash (first hit that only wounds).</summary>
        public void PlaySlash() => slashTime = 0f;

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
            slashTime += dt;
            pokeTime += dt;
            if (motion == null && cam != null) motion = cam.GetComponentInParent<ICharacterMotionSource>();

            // Item switching.
            float lower = 0f;
            switchTime += dt;
            if (switchTime < LowerTime) lower = ActionTimeline.SmoothStep01(switchTime / LowerTime);
            else
            {
                shownItem = heldItem;
                SwapCarried();
                if (switchTime < LowerTime + RaiseTime) lower = 1f - ActionTimeline.SmoothStep01((switchTime - LowerTime) / RaiseTime);
            }

            HeldItem show = action == Action.Knife ? HeldItem.Knife : action == Action.Reload ? HeldItem.Glock : shownItem;
            if (show == HeldItem.Chainsaw && lastShow != HeldItem.Chainsaw && !revving) sawStartT = -RaiseTime * 0.6f; // pull once raised
            if (revving) sawStartT = Mathf.Max(sawStartT, BlockyProps.ChainsawStartTime);
            lastShow = show;
            sawStartT += dt;
            if (!float.IsNaN(extSawT)) sawStartT = revving ? Mathf.Max(extSawT, BlockyProps.ChainsawStartEnd(sawAttempts)) : extSawT;

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

            carryHands = false;
            bool holding = HoldHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft);
            UpdateVmKit();
            if (!holding)
            switch (show)
            {
                case HeldItem.Glock:
                {
                    Vector3 gPos = GlockHoldPos;
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
                case HeldItem.Fork:
                {
                    Vector3 rest = new Vector3(0.27f, -0.36f, 0.72f);
                    Quaternion restRot = Quaternion.LookRotation(new Vector3(-0.3f, 0.35f, 1f), Vector3.up);
                    rightT = rest;
                    propRot = restRot;
                    if (slashTime < 0.55f && action != Action.Knife)
                    {
                        float wind = ActionTimeline.SmoothStep01(slashTime / 0.12f);
                        float thrust = ActionTimeline.SmoothStep01(ActionTimeline.Remap01(slashTime, 0.12f, 0.2f));
                        float back = ActionTimeline.SmoothStep01(ActionTimeline.Remap01(slashTime, 0.3f, 0.55f));
                        Vector3 cocked = new Vector3(0.34f, -0.48f, 0.55f), hit = new Vector3(0.04f, -0.3f, 0.95f);
                        rightT = Vector3.Lerp(rest, Vector3.Lerp(cocked, hit, thrust), wind * (1f - back));
                        propRot = Quaternion.Slerp(restRot, Quaternion.LookRotation((hit - cocked).normalized, Vector3.up), wind * (1f - back));
                    }
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
                case HeldItem.Chainsaw:
                {
                    float rv = revving ? 1f : 0f;
                    Vector3 jit = new Vector3(Mathf.Sin(time * 83f), Mathf.Sin(time * 67f), Mathf.Sin(time * 59f)) * (0.003f + 0.008f * rv);
                    Vector3 sPos = new Vector3(0.2f, -0.46f + 0.06f * rv, 0.5f + 0.05f * rv) + jit;
                    Quaternion sRot = Quaternion.Euler(-6f - 10f * rv, -8f, 0f);
                    // Pull-start: saw held out lower and turned a little, left hand yanks the starter cord twice.
                    var st = BlockyProps.ChainsawStarter(sawStartT, sawAttempts);
                    if (st.x > 0.001f)
                    {
                        float yank = st.y;
                        // Turned so the starter side faces the camera, held up in view; each yank jerks it a little.
                        sPos = Vector3.Lerp(sPos, new Vector3(0.04f, -0.36f - 0.025f * yank, 0.56f + 0.02f * yank), st.x);
                        sRot = Quaternion.Slerp(sRot, Quaternion.Euler(8f + 5f * yank, 38f, -8f - 4f * yank), st.x);
                    }
                    rightT = sPos;
                    propRot = sRot;
                    SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.5f));
                    Vector3 grip = HandPos(shR, elR) + sRot * BlockyProps.ChainsawLeftGripLocal;
                    Vector3 starter = HandPos(shR, elR) + sRot * (BlockyProps.ChainsawStarterLocal +
                                      BlockyProps.ChainsawPullDir * (BlockyProps.ChainsawPullLength * st.y));
                    leftT = Vector3.Lerp(grip, starter, st.x);
                    wantLeft = 1f;
                    break;
                }
                case HeldItem.Medkit:
                    rightT = new Vector3(0.26f, -0.24f, 0.68f);
                    propRot = Quaternion.Euler(0f, -25f, 0f);
                    SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.4f));
                    break;
                case HeldItem.Brick:
                {
                    // Brick held up in front; a quick push forward when one is put down.
                    float poke = pokeTime < 0.32f ? Mathf.Sin(pokeTime / 0.32f * Mathf.PI) : 0f;
                    rightT = new Vector3(0.3f, -0.4f, 0.78f) + new Vector3(-0.08f, 0.05f, 0.22f) * poke;
                    propRot = Quaternion.Euler(-10f - 20f * poke, -20f, 0f);
                    SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.4f));
                    break;
                }
                default:
                    if (CarryHands(show, out rightT, out propRot, ref leftT, ref wantLeft)) break; // selected inventory item
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
            var syrProp = props[(int)HeldItem.Syringe];
            if (syrProp != null && show == HeldItem.Syringe) syrProp.SetSyringe(HealTimeline.InjectPress(holdTime), holdTime < HealTimeline.InjectCapOff);
            var brickProp = props[(int)HeldItem.Brick];
            if (brickProp != null && show == HeldItem.Brick) brickProp.SetTint(brickTint);
            UpdateVendingProp(show);
            UpdateFlashLamp();
            UpdateToyGunFp(dt);
            UpdateCarryFp();
            UpdateVmPaMic();
            UpdateVmExtinguisher();
            var sawProp = props[(int)HeldItem.Chainsaw];
            if (sawProp != null && show == HeldItem.Chainsaw)
            {
                bool engine = sawStartT >= BlockyProps.ChainsawFireAt(sawAttempts);
                sawProp.SetChainRun(revving ? 1f : engine ? 0.3f : 0f, time);
                sawProp.SetStarterPull(BlockyProps.ChainsawStarter(sawStartT, sawAttempts).y);
                sawProp.SetSawBlood(sawBlood);
            }
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
