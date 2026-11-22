using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Procedural animation for <see cref="BlockyCharacter"/>.
    /// Reads an <see cref="ICharacterMotionSource"/> from the parent chain (idle when none) and blends
    /// idle / walk / run / crouch / airborne poses with framerate-independent damping.
    /// Legs use analytic two-bone IK against procedurally planted foot targets; the gait phase advances by
    /// distance travelled (HorizontalSpeed / stride length), so stance feet do not slide.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class BlockyCharacterAnimator : MonoBehaviour
    {
        [Tooltip("Advance automatically in LateUpdate. Disable to drive the animation with Tick().")]
        public bool autoUpdate = true;

        [Tooltip("Occasionally glance around while standing still.")]
        public bool idleLookAround = true;
        /// <summary>0..1 limp on the right leg (injured hider). Set by the action layer.</summary>
        [Range(0f, 1f)] public float limp;
        float limpW;

        /// <summary>Index of the severed leg (0 = right, 1 = left) or -1: hops slowly on the other leg.</summary>
        public int hopLeg = -1;
        float hopW, hopPhase;

        [Range(0f, 85f)] public float maxHeadPitch = 65f;

        [Tooltip("Seed for idle variation (look-around timing, weight shifts). 0 = derive from instance.")]
        public int randomSeed;

        const float Px = BlockyCharacter.PixelSize;
        const float SegmentLength = 6f * Px; // thigh == shin == 6 px
        const float StandHipHeight = BlockyCharacter.HipHeight;
        const float TwoPi = Mathf.PI * 2f;

        BlockyCharacter character;
        ICharacterMotionSource source;
        bool sourceOverridden;
        bool initialized;

        // Blend weights (0..1).
        float moveW, runW, crouchW, airW, turnStepW;
        float idleW = 1f;

        // Gait.
        float phase;
        float strideLength = 1f;
        float moveAngle;
        float sideFactor;

        // Time-based oscillators.
        float time, breathPhase, swayPhase, exertion;

        // Dynamics.
        Vector3 prevLocalVelocity;
        Vector3 accelLocal;
        float prevYaw, yawRate;
        bool hasPrevFrame;
        bool wasGrounded = true;
        float prevVerticalVelocity;
        float landOffset, landVelocity;

        // Head.
        float headPitch;
        float lookYaw, lookYawVel, lookPitch, lookPitchVel;
        float lookTargetYaw, lookTargetPitch, nextLookTime;
        float weightShift, weightTarget, nextWeightTime;
        uint rng;

        /// <summary>Gait cycle phase 0..1 (left foot plants at 0, right foot at 0.5).</summary>
        public float GaitPhase => phase;

        /// <summary>The motion source in use (null = idle).</summary>
        public ICharacterMotionSource Source => source;

        /// <summary>Overrides the motion source (null re-enables the automatic parent lookup).</summary>
        public void SetMotionSource(ICharacterMotionSource motionSource)
        {
            source = motionSource;
            sourceOverridden = motionSource != null;
        }

        void OnEnable()
        {
            hasPrevFrame = false;
        }

        void LateUpdate()
        {
            if (autoUpdate) Tick(Time.deltaTime);
        }

        void Init()
        {
            initialized = true;
            uint seed = randomSeed != 0 ? (uint)randomSeed : (uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this) * 2654435761u;
            rng = seed == 0 ? 0x9E3779B9u : seed;
            breathPhase = Rand(0f, TwoPi);
            swayPhase = Rand(0f, TwoPi);
            nextLookTime = Rand(1.5f, 4f);
            nextWeightTime = Rand(3f, 8f);
            phase = 0f;
        }

        void ResolveSource()
        {
            if (sourceOverridden)
            {
                if (source is Object o && o == null) { source = null; sourceOverridden = false; }
                return;
            }

            if (source is Object so && so != null) return;
            source = null;
            if (time < nextSourceSearch) return;
            nextSourceSearch = time + 1f; // re-check occasionally (e.g. the source is added after spawn)
            source = GetComponentInParent<ICharacterMotionSource>();
        }

        float nextSourceSearch;

        /// <summary>Advances the animation by <paramref name="dt"/> seconds and poses the rig.</summary>
        public void Tick(float dt)
        {
            if (character == null) character = GetComponent<BlockyCharacter>();
            if (character == null || !character.IsBuilt) return;
            if (character.IsRagdoll) return; // a dead body lies as a ragdoll: physics owns the joints
            if (!initialized) Init();
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);
            ResolveSource();
            time += dt;

            // ---------------------------------------------------------------- inputs
            Vector3 velocity = Vector3.zero;
            float speed = 0f, crouchAmount = 0f, pitch = 0f;
            bool grounded = true, running = false;
            if (source != null)
            {
                velocity = source.Velocity;
                speed = Mathf.Max(0f, source.HorizontalSpeed);
                grounded = source.IsGrounded;
                running = source.IsRunning;
                crouchAmount = Mathf.Clamp01(source.CrouchAmount);
                if (source.IsCrouching && crouchAmount <= 0f) crouchAmount = 1f;
                pitch = source.LookPitch;
            }

            Vector3 localVel = transform.InverseTransformDirection(velocity);
            float yaw = transform.eulerAngles.y;
            if (hasPrevFrame)
            {
                float rawYawRate = Mathf.DeltaAngle(prevYaw, yaw) / dt;
                yawRate = Damp(yawRate, Mathf.Clamp(rawYawRate, -720f, 720f), 10f, dt);
                Vector3 rawAccel = Vector3.ClampMagnitude((localVel - prevLocalVelocity) / dt, 25f);
                accelLocal = Vector3.Lerp(accelLocal, rawAccel, 1f - Mathf.Exp(-5f * dt));
            }

            prevYaw = yaw;
            prevLocalVelocity = localVel;
            hasPrevFrame = true;

            // ---------------------------------------------------------------- blend weights
            float moveTarget = grounded ? Smooth01(Mathf.InverseLerp(0.05f, 0.8f, speed)) : 0f;
            moveW = Damp(moveW, moveTarget, 9f, dt);

            float runTarget = Mathf.Max(running && speed > 0.5f ? 1f : 0f, Mathf.InverseLerp(4.2f, 5.6f, speed));
            runTarget *= 1f - crouchAmount;
            runW = Damp(runW, runTarget, 5f, dt);

            crouchW = Damp(crouchW, crouchAmount, 14f, dt);
            airW = Damp(airW, grounded ? 0f : 1f, grounded ? 16f : 7f, dt);
            idleW = (1f - moveW) * (1f - airW);

            float turnTarget = (1f - moveW) * (1f - airW) * Mathf.InverseLerp(35f, 140f, Mathf.Abs(yawRate));
            turnStepW = Damp(turnStepW, turnTarget, 8f, dt);

            exertion = Damp(exertion, runW * moveW, runW * moveW > exertion ? 0.5f : 0.08f, dt);

            // Landing: kick a spring that dips the hips.
            if (grounded && !wasGrounded)
            {
                float impact = Mathf.Clamp01((-prevVerticalVelocity - 1f) / 8f);
                landVelocity -= impact * 1.3f;
            }

            wasGrounded = grounded;
            prevVerticalVelocity = velocity.y;
            StepSpring(ref landOffset, ref landVelocity, 170f, 0.72f, dt);

            // ---------------------------------------------------------------- move direction (local)
            if (speed > 0.2f && new Vector2(localVel.x, localVel.z).sqrMagnitude > 0.01f)
            {
                float target = Mathf.Atan2(localVel.x, localVel.z) * Mathf.Rad2Deg;
                moveAngle = Mathf.LerpAngle(moveAngle, target, 1f - Mathf.Exp(-10f * dt));
            }
            else
            {
                moveAngle = Mathf.LerpAngle(moveAngle, 0f, 1f - Mathf.Exp(-2f * dt));
            }

            float dirX = Mathf.Sin(moveAngle * Mathf.Deg2Rad);
            float dirZ = Mathf.Cos(moveAngle * Mathf.Deg2Rad);
            sideFactor = Mathf.Abs(dirX);

            // ---------------------------------------------------------------- gait
            float duty = Mathf.Lerp(0.6f, 0.36f, runW);
            duty = Mathf.Lerp(duty, 0.64f, crouchW * (1f - runW));
            float maxAmp = Mathf.Lerp(Mathf.Lerp(0.27f, 0.30f, runW), 0.24f, crouchW);
            float walkCycle = 0.7f + 0.2f * speed;
            float runCycle = 1.05f + 0.17f * speed;
            float desiredCycle = Mathf.Lerp(walkCycle, runCycle, runW);
            desiredCycle *= Mathf.Lerp(1f, 0.72f, sideFactor); // shorter, quicker side steps
            desiredCycle = Mathf.Clamp(desiredCycle, 0.55f, 2f * maxAmp / duty);
            strideLength = Damp(strideLength, desiredCycle, 6f, dt);

            limpW = Damp(limpW, limp, 3f, dt);
            float limpMove = limpW * moveW;

            // One-legged hopping: slow springy hops on the remaining leg, arms out for balance.
            hopW = Damp(hopW, hopLeg >= 0 ? 1f : 0f, 6f, dt);
            int standLeg = hopLeg == 0 ? 1 : 0;
            float standSide = standLeg == 0 ? 1f : -1f;
            float hopMove = hopW * moveW;
            float hopHipOffset = 0f, hopFootLift = 0f, hopAlong = 0f;
            if (hopW > 0.01f)
            {
                if (grounded && moveW > 0.1f) hopPhase += dt * (0.8f + 0.5f * Mathf.Clamp01(speed / 1.6f));
                hopPhase -= Mathf.Floor(hopPhase);
                float fr = hopPhase;
                float dip = 0f, air = 0f, c;
                if (fr < 0.3f) { dip = Mathf.Sin(Mathf.PI * fr / 0.3f) * 0.075f; c = (fr + 0.25f) / 0.55f; }
                else if (fr < 0.75f) { air = Mathf.Sin(Mathf.PI * (fr - 0.3f) / 0.45f); c = (fr - 0.3f) / 0.45f; }
                else { dip = Mathf.Sin(Mathf.PI * (fr - 0.75f) / 0.25f) * 0.05f; c = (fr - 0.75f) / 0.55f; }
                hopHipOffset = (-dip + 0.07f * air) * hopMove;
                hopFootLift = 0.07f * air * hopMove;
                hopAlong = (fr >= 0.3f && fr < 0.75f ? Mathf.Lerp(-0.12f, 0.12f, c) : Mathf.Lerp(0.12f, -0.12f, c)) * hopMove;
            }
            if (grounded)
            {
                // Limp: hurry off the injured (right) leg -> short stance on it.
                float ssNow = -Mathf.Cos(TwoPi * (phase - duty * 0.5f));
                phase += speed * dt / strideLength * (1f + 0.75f * limpMove * Mathf.Max(0f, ssNow) - 0.25f * limpMove * Mathf.Max(0f, -ssNow)) + turnStepW * dt * 1.7f;
                phase -= Mathf.Floor(phase);
            }

            // Stance half-amplitude: foot travels duty * stride while planted -> no sliding.
            float amp = duty * strideLength * 0.5f * moveW;
            float liftHeight = Mathf.Lerp(0.075f, 0.17f, runW) * Mathf.Lerp(1f, 0.75f, crouchW) * moveW + turnStepW * 0.06f;

            // Arm swing driver: +1 when the right arm is forward (left leg forward, phase 0).
            float swing = Mathf.Cos(TwoPi * (phase - 0.05f));
            float bobWave = Mathf.Cos(2f * TwoPi * (phase - duty * 0.5f)); // +1 at mid-stance

            // ---------------------------------------------------------------- idle oscillators
            float breathRate = Mathf.Lerp(0.24f, 0.62f, exertion);
            breathPhase += TwoPi * breathRate * dt;
            if (breathPhase > TwoPi * 1000f) breathPhase -= TwoPi * 1000f;
            float breath = Mathf.Sin(breathPhase);
            float breathAmp = Mathf.Lerp(1f, 2.2f, exertion);
            swayPhase += TwoPi * 0.17f * dt;
            if (swayPhase > TwoPi * 1000f) swayPhase -= TwoPi * 1000f;
            float sway = Mathf.Sin(swayPhase);

            if (time >= nextWeightTime)
            {
                float r = Rand01();
                weightTarget = r < 0.35f ? 0f : (r < 0.68f ? 1f : -1f);
                nextWeightTime = time + Rand(4f, 9f);
            }

            weightShift = Damp(weightShift, weightTarget * idleW, 1.6f, dt);
            UpdateLookAround(dt);

            // ---------------------------------------------------------------- pelvis
            // Nearly straight legs when standing (a visible knee bend shows a seam at the limb split).
            float reach = 2f * SegmentLength * Mathf.Lerp(0.9995f, 0.994f, moveW);
            float baseHip = Mathf.Sqrt(Mathf.Max(0.01f, reach * reach - amp * amp));
            float walkBob = Mathf.Min(0.024f, reach - baseHip) * (0.5f + 0.5f * bobWave);
            float runBob = -0.04f * (0.5f + 0.5f * bobWave);
            float hip = baseHip + Mathf.Lerp(walkBob, runBob, runW) * moveW;
            hip -= crouchW * 0.25f;
            hip -= 0.006f * idleW * Mathf.Abs(weightShift); // relaxed knee when shifting weight
            hip += 0.0025f * breath * breathAmp * idleW;
            hip = Mathf.Lerp(hip, StandHipHeight - 0.01f, airW);
            hip += landOffset * (1f - airW);

            float stanceSide = -Mathf.Cos(TwoPi * (phase - duty * 0.5f)); // -1 left stance, +1 right stance
            // Limp: the body drops when the weight goes onto the injured right leg; slightly hunched always.
            hip -= limpW * (0.012f + 0.05f * limpMove * Mathf.Max(0f, stanceSide));
            hip += hopHipOffset - 0.02f * hopW;
            float pelvisX = (0.011f * sway + 0.024f * weightShift - 0.035f * limpW) * idleW
                            + 0.014f * stanceSide * moveW * (1f - 0.5f * runW) * (1f - sideFactor);
            // Swing-side hip drops (negative roll lowers +X when the left leg is in stance).
            float pelvisRoll = (1.2f * sway + 3f * weightShift + 4f * limpW) * idleW
                               + 2.5f * stanceSide * moveW * (1f - runW * 0.4f)
                               - 5f * limpMove * Mathf.Max(0f, stanceSide);
            float pelvisTwist = Mathf.Lerp(5f, 9f, runW) * moveW * dirZ * (1f - crouchW * 0.5f);
            pelvisX = Mathf.Lerp(pelvisX, standSide * 2f * Px, hopW);
            pelvisRoll += hopW * (hopLeg == 0 ? -6f : 6f);
            float pelvisYaw = pelvisTwist * swing * (1f - hopW);
            float pelvisPitch = 4f * crouchW;

            // Crouch: hips sit back behind the feet so the shins stay more upright (soles closer to flat).
            float pelvisZ = -0.13f * crouchW * (1f - airW);
            Vector3 pelvisPos = new Vector3(pelvisX, hip, pelvisZ);
            Quaternion pelvisRot = Quaternion.Euler(pelvisPitch, pelvisYaw, pelvisRoll);
            character.Pelvis.localPosition = pelvisPos;
            character.Pelvis.localRotation = pelvisRot;
            Quaternion invPelvis = Quaternion.Inverse(pelvisRot);

            // ---------------------------------------------------------------- legs
            float rising = Mathf.Clamp01(velocity.y / 4f);
            float falling = Mathf.Clamp01(-velocity.y / 6f);
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? 1f : -1f; // 0 = right (+X), 1 = left
                float q = phase + (leg == 0 ? 0.5f : 0f);
                q -= Mathf.Floor(q);

                float along, lift = 0f, lateral = 0f;
                if (q < duty)
                {
                    along = Mathf.Lerp(amp, -amp, q / duty);
                }
                else
                {
                    float t = (q - duty) / (1f - duty);
                    float e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t);
                    along = Mathf.Lerp(-amp, amp, e);
                    float liftShape = Mathf.Sin(Mathf.PI * Mathf.Pow(t, Mathf.Lerp(1f, 0.7f, runW)));
                    lift = liftHeight * liftShape;
                    // Running: heel kicks back towards the glutes early in the swing.
                    along -= runW * moveW * 0.16f * Mathf.Sin(Mathf.PI * t) * (1f - t);
                    // Side steps: swing foot arcs outwards so legs do not cross.
                    lateral = side * 0.05f * sideFactor * moveW * Mathf.Sin(Mathf.PI * t);
                    if (leg == 0 && limpMove > 0f)
                    {
                        // Stiff injured leg: low lift, swings out round the side, shorter step.
                        lift *= 1f - 0.6f * limpMove;
                        lateral += 0.05f * limpMove * Mathf.Sin(Mathf.PI * t);
                    }
                }
                if (leg == 0) along *= 1f - 0.3f * limpMove;

                float width = 0.012f + 0.045f * crouchW + 0.01f * idleW;
                float idleStagger = (leg == 0 ? 0.02f : -0.015f) * idleW;
                Vector3 footRoot = new Vector3(side * (2f * Px + width) + lateral + dirX * along, lift, idleStagger + dirZ * along);
                if (hopW > 0.01f && leg == standLeg)
                    footRoot = Vector3.Lerp(footRoot, new Vector3(side * (2f * Px + 0.012f) + dirX * hopAlong, hopFootLift, dirZ * hopAlong), hopW);
                Vector3 hipPos = leg == 0 ? BlockyCharacter.HipRightRest : BlockyCharacter.HipLeftRest;
                Vector3 rel = invPelvis * (footRoot - pelvisPos) - hipPos;

                // Airborne pose: one knee tucked forward, the other trailing; legs reach down when falling.
                Vector3 airTuck = leg == 0 ? new Vector3(0.01f, -0.38f, 0.17f) : new Vector3(-0.01f, -0.55f, -0.13f);
                Vector3 airFall = new Vector3(side * 0.03f, -0.6f, leg == 0 ? 0.08f : -0.03f);
                Vector3 airRel = Vector3.Lerp(Vector3.Lerp(airTuck, airFall, falling), airTuck * 1.05f, rising * 0.5f);
                rel = Vector3.Lerp(rel, airRel, airW);

                Vector3 pole = new Vector3(side * (0.22f * crouchW + 0.06f), 0f, 1f);
                SolveLeg(leg == 0 ? character.HipRight : character.HipLeft, leg == 0 ? character.KneeRight : character.KneeLeft, rel, pole);
            }

            // ---------------------------------------------------------------- torso
            float accelLean = Mathf.Clamp(accelLocal.z * 1.4f, -6f, 8f) * (1f - airW) * Mathf.Max(moveW, 0.3f);
            float lean = (2.5f * (1f - runW) + 12f * runW) * moveW * dirZ
                         + 20f * crouchW
                         + accelLean
                         - landOffset * 140f
                         + airW * (6f * falling - 4f * rising)
                         + limpW * (7f + 5f * limpMove * Mathf.Max(0f, stanceSide))
                         + hopW * (9f + 5f * hopMove);
            float sideLean = -(2.5f + 6f * runW) * moveW * dirX;
            float bank = Mathf.Clamp(-yawRate * 0.035f * moveW * (0.4f + runW), -12f, 12f);
            float torsoYaw = -Mathf.Lerp(5f, 11f, runW) * moveW * swing * dirZ + lookYaw * 0.18f * idleW;
            // Positive roll leans left; lean slightly over the stance leg.
            float torsoRoll = sideLean + bank - 0.6f * pelvisRoll - 1.2f * stanceSide * moveW * (1f - runW)
                              - 7f * limpMove * Mathf.Max(0f, stanceSide);
            float nod = (0.8f + 1.5f * runW) * moveW * bobWave;
            float breathPitch = -0.5f * breath * breathAmp * idleW;

            Quaternion torsoRoot = Quaternion.Euler(lean + nod + breathPitch, torsoYaw, torsoRoll);
            character.Torso.localRotation = invPelvis * torsoRoot;

            // Breathing: chest expands (scale the body mesh only so children are unaffected).
            float b = breath * breathAmp;
            character.BodyMesh.localScale = new Vector3(1f + 0.008f * b, 1f + 0.004f * b, 1f + 0.03f * b);

            // ---------------------------------------------------------------- head
            float pitchTarget = Mathf.Clamp(pitch, -maxHeadPitch, maxHeadPitch);
            headPitch = Damp(headPitch, pitchTarget, 16f, dt);
            float headNod = -nod * 0.6f;
            Quaternion headRoot = Quaternion.Euler(-headPitch - lookPitch * idleW + headNod,
                lookYaw * idleW,
                -lookYaw * idleW * 0.08f - bank * 0.3f);
            // Head mostly stabilised in root space (eyes level while leaning), with a bit of the torso motion.
            Quaternion headStabilised = Quaternion.Inverse(torsoRoot) * headRoot;
            Quaternion headFollow = headRoot;
            character.Head.localRotation = Quaternion.Slerp(headFollow, headStabilised, 0.82f);
            character.Head.localPosition = BlockyCharacter.NeckRest + new Vector3(0f, 0.003f * b * idleW, 0f);

            // ---------------------------------------------------------------- arms
            float armAmp = Mathf.Lerp(26f, 52f, runW) * Mathf.Lerp(1f, 0.55f, crouchW) * moveW * Mathf.Lerp(1f, 0.45f, sideFactor);
            float armBasePitch = -8f * runW * moveW - 16f * crouchW;
            float outward = 3f + 7f * runW * moveW + 6f * crouchW + 1.2f * breath * breathAmp * idleW + hopW * (16f + 8f * hopMove);
            float inwardYaw = 14f * runW * moveW;
            float shoulderLift = 0.004f * b * idleW;

            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? 1f : -1f;
                float forward = arm == 0 ? swing : -swing; // +1 = this arm swung forward
                float idleSwing = 1.6f * Mathf.Sin(swayPhase - 0.7f + arm * 0.9f) * idleW;

                float armPitch = armBasePitch - armAmp * forward + idleSwing;
                float walkElbow = -(8f + 16f * Mathf.Max(0f, forward));
                float runElbow = -(82f + 12f * forward);
                float elbow = Mathf.Lerp(-6f + 1.5f * Mathf.Sin(swayPhase + arm), Mathf.Lerp(walkElbow, runElbow, runW), moveW);
                elbow = Mathf.Lerp(elbow, Mathf.Min(elbow, -32f), crouchW);

                // Airborne: arms out and up for balance.
                armPitch = Mathf.Lerp(armPitch, -25f - 25f * rising + 10f * falling, airW);
                float armOut = outward + airW * (28f + 30f * rising + 12f * falling);
                elbow = Mathf.Lerp(elbow, -28f, airW);

                // Landing: arms dip forward briefly.
                armPitch += landOffset * 160f;

                Quaternion upper = Quaternion.Euler(armPitch, -side * inwardYaw, side * armOut);
                Transform shoulder = arm == 0 ? character.ShoulderRight : character.ShoulderLeft;
                Transform elbowT = arm == 0 ? character.ElbowRight : character.ElbowLeft;
                shoulder.localRotation = upper;
                shoulder.localPosition = (arm == 0 ? BlockyCharacter.ShoulderRightRest : BlockyCharacter.ShoulderLeftRest)
                                         + new Vector3(0f, shoulderLift, 0f);
                elbowT.localRotation = Quaternion.Euler(elbow, 0f, 0f);
            }

            // ---------------------------------------------------------------- action layer (knife, downed, props...)
            if (actions == null && time >= nextActionsSearch)
            {
                nextActionsSearch = time + 1f;
                actions = GetComponent<BlockyCharacterActions>();
            }

            if (actions != null && actions.enabled) actions.ApplyLayer(dt);
        }

        BlockyCharacterActions actions;
        float nextActionsSearch;

        void UpdateLookAround(float dt)
        {
            bool canLook = idleLookAround && idleW > 0.6f;
            if (!canLook)
            {
                lookTargetYaw = 0f;
                lookTargetPitch = 0f;
                nextLookTime = Mathf.Max(nextLookTime, time + 1.5f);
            }
            else if (time >= nextLookTime)
            {
                bool looking = Mathf.Abs(lookTargetYaw) > 5f;
                if (looking && Rand01() < 0.6f)
                {
                    lookTargetYaw = 0f;
                    lookTargetPitch = 0f;
                    nextLookTime = time + Rand(2.5f, 6f);
                }
                else
                {
                    float sign = Rand01() < 0.5f ? -1f : 1f;
                    lookTargetYaw = sign * Rand(22f, 55f);
                    lookTargetPitch = Rand(-10f, 8f);
                    nextLookTime = time + Rand(1.2f, 3f);
                }
            }

            lookYaw = Mathf.SmoothDamp(lookYaw, lookTargetYaw, ref lookYawVel, 0.3f, 400f, dt);
            lookPitch = Mathf.SmoothDamp(lookPitch, lookTargetPitch, ref lookPitchVel, 0.35f, 200f, dt);
        }

        /// <summary>
        /// Analytic two-bone IK (equal segment lengths). <paramref name="rel"/> is the sole target relative to the
        /// hip, expressed in the hip's parent space. The knee bends towards <paramref name="pole"/>.
        /// </summary>
        internal static void SolveLeg(Transform upper, Transform lower, Vector3 rel, Vector3 pole)
        {
            float d = rel.magnitude;
            if (d < 1e-4f)
            {
                rel = Vector3.down * 0.05f;
                d = 0.05f;
            }

            Vector3 u = rel / d;
            d = Mathf.Clamp(d, 0.05f, 2f * SegmentLength * 0.999f);
            float alpha = Mathf.Acos(Mathf.Clamp(d / (2f * SegmentLength), -1f, 1f));

            Vector3 p = pole - u * Vector3.Dot(pole, u);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(u, Vector3.right);
            p.Normalize();

            Vector3 kneeDir = u * Mathf.Cos(alpha) + p * Mathf.Sin(alpha);
            Vector3 fwd = p - kneeDir * Vector3.Dot(p, kneeDir);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            upper.localRotation = Quaternion.LookRotation(fwd.normalized, -kneeDir);
            lower.localRotation = Quaternion.Euler(2f * alpha * Mathf.Rad2Deg, 0f, 0f);
        }

        static void StepSpring(ref float x, ref float v, float stiffness, float dampingRatio, float dt)
        {
            float c = 2f * dampingRatio * Mathf.Sqrt(stiffness);
            int steps = Mathf.CeilToInt(dt / (1f / 120f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += (-stiffness * x - c * v) * h;
                x += v * h;
            }
        }

        static float Damp(float current, float target, float lambda, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        }

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        float Rand01()
        {
            // xorshift32: deterministic per instance, independent of UnityEngine.Random.
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng & 0xFFFFFF) / (float)0x1000000;
        }

        float Rand(float min, float max) => min + (max - min) * Rand01();
    }
}
