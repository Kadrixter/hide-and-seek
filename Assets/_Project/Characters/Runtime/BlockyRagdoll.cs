using System;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Physical ragdoll of a dead <see cref="BlockyCharacter"/>, built from the character's own joints (the bones are not
    /// copied, so everything parented to them - stumps, blood spurts, the shoulder lamp, the chest point - keeps working).
    /// <para>
    /// Each body block gets a <see cref="Rigidbody"/> with a box collider that matches the block (on the
    /// <see cref="GameLayers.Ragdoll"/> layer, on a child object so the render layer of the joint stays untouched) and a
    /// <see cref="ConfigurableJoint"/> to its parent block: knees and elbows bend one way only, the neck and the waist are
    /// limited, shoulders and hips are cones. Limits are symmetric around the middle of the allowed range (the joint is
    /// created while the limb is turned to that middle), so the result never depends on the sign conventions of the
    /// physics engine; when the animated start pose lies outside a range the middle moves just enough to contain it, so
    /// the body never snaps at the start.
    /// </para>
    /// <para>
    /// Takes over from the last animated frame with the velocities of the last two frames (the body keeps falling the way
    /// the animation moved it), optionally plus a push (a bullet). Severed limbs get no body and no collider. Starts out of
    /// any wall / floor it was animated into, never tunnels through the floor, and once everything lies still the bodies
    /// turn kinematic and the colliders switch off (no cost, nothing for raycasts or players to bump into).
    /// </para>
    /// Every client simulates its own copy from the same start state (the network only says when and how a player died).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)] // after BlockyCharacterAnimator: samples the final pose of every frame
    public class BlockyRagdoll : MonoBehaviour
    {
        const float Px = BlockyCharacter.PixelSize;

        public const int PartCount = 11;
        public const int Pelvis = 0, Torso = 1, Head = 2, UpperArmR = 3, LowerArmR = 4, UpperArmL = 5, LowerArmL = 6,
            UpperLegR = 7, LowerLegR = 8, UpperLegL = 9, LowerLegL = 10;

        // Start / settle tuning.
        const float MaxStartSpeed = 7f;      // m/s, faster animated motion is clamped
        const float SnapSpeed = 14f;         // m/s, anything faster was a teleport / pose snap: no velocity at all
        const float MaxStartSpin = 14f;      // rad/s
        const float SnapSpin = 40f;
        const float StillSpeed = 0.07f, StillSpin = 0.3f;
        const float StillTimeToFreeze = 0.6f, MinSimTime = 0.9f, MaxSimTime = 9f;
        const float ControllerRefresh = 1f;
        const float MaxLift = 0.5f;

        struct Spec
        {
            public int Parent;      // index of the parent body (-1 = none)
            public int Limb;        // Limb bit whose loss removes this part (0 = never)
            public Vector3 Center;  // collider centre / size in skin pixels, local to the joint
            public Vector3 Size;
            public float Mass;
            public Vector3 Mid;     // local Euler rotation in the middle of the allowed range (joint zero)
            public float LimX, LimY, LimZ; // half ranges in degrees around Mid (0 = locked)
            public float Damp;      // joint friction (relative angular velocity damping, 1/s)

            public Spec(int parent, int limb, Vector3 center, Vector3 size, float mass, Vector3 mid, float limX, float limY, float limZ, float damp)
            {
                Parent = parent; Limb = limb; Center = center; Size = size; Mass = mass;
                Mid = mid; LimX = limX; LimY = limY; LimZ = limZ; Damp = damp;
            }
        }

        // Local axes of every joint: +Y up the bone (limbs hang along -Y), +Z forward, +X to the character's right.
        // Positive X turns a hanging limb backwards (knee), negative forwards (hip / shoulder flexion, elbow);
        // positive Z swings a right limb outwards (negative for the left side); positive X tips the head / torso forwards.
        static readonly Spec[] Specs =
        {
            /* Pelvis    */ new Spec(-1, 0, new Vector3(0f, 2f, 0f), new Vector3(7.8f, 4f, 3.8f), 12f, Vector3.zero, 0f, 0f, 0f, 0f),
            /* Torso     */ new Spec(Pelvis, 0, new Vector3(0f, 7.5f, 0f), new Vector3(8f, 9f, 4f), 17f, new Vector3(10f, 0f, 0f), 28f, 20f, 15f, 5f),
            /* Head      */ new Spec(Torso, Limb.Head, new Vector3(0f, 4f, 0f), new Vector3(7.8f, 7.8f, 7.8f), 5.5f, Vector3.zero, 45f, 65f, 30f, 5f),
            /* UpperArmR */ new Spec(Torso, Limb.ArmR, new Vector3(0f, -1f, 0f), new Vector3(3.2f, 6f, 3.8f), 3f, new Vector3(-45f, 0f, 35f), 85f, 45f, 65f, 3f),
            /* LowerArmR */ new Spec(UpperArmR, Limb.ArmR, new Vector3(0f, -3f, 0f), new Vector3(3.2f, 6f, 3.8f), 2.2f, new Vector3(-70f, 0f, 0f), 75f, 12f, 0f, 3f),
            /* UpperArmL */ new Spec(Torso, Limb.ArmL, new Vector3(0f, -1f, 0f), new Vector3(3.2f, 6f, 3.8f), 3f, new Vector3(-45f, 0f, -35f), 85f, 45f, 65f, 3f),
            /* LowerArmL */ new Spec(UpperArmL, Limb.ArmL, new Vector3(0f, -3f, 0f), new Vector3(3.2f, 6f, 3.8f), 2.2f, new Vector3(-70f, 0f, 0f), 75f, 12f, 0f, 3f),
            /* UpperLegR */ new Spec(Pelvis, Limb.LegR, new Vector3(0f, -3f, 0f), new Vector3(3.4f, 6f, 3.8f), 8f, new Vector3(-35f, 0f, 8f), 65f, 25f, 35f, 4f),
            /* LowerLegR */ new Spec(UpperLegR, Limb.LegR, new Vector3(0f, -3f, 0f), new Vector3(3.6f, 6f, 3.8f), 4.5f, new Vector3(70f, 0f, 0f), 75f, 0f, 0f, 4f),
            /* UpperLegL */ new Spec(Pelvis, Limb.LegL, new Vector3(0f, -3f, 0f), new Vector3(3.4f, 6f, 3.8f), 8f, new Vector3(-35f, 0f, -8f), 65f, 25f, 35f, 4f),
            /* LowerLegL */ new Spec(UpperLegL, Limb.LegL, new Vector3(0f, -3f, 0f), new Vector3(3.6f, 6f, 3.8f), 4.5f, new Vector3(70f, 0f, 0f), 75f, 0f, 0f, 4f),
        };

        // A cut-off leg with a rod strapped on (RodLegRig): one thin body hanging from the hip, no knee.
        static readonly Vector3 RodCenter = new Vector3(0f, -6f, 0f);
        static readonly Vector3 RodSize = new Vector3(1.5f, 12f, 1.5f);
        const float RodMass = 2f;

        static PhysicsMaterial material;
        static readonly Collider[] Overlaps = new Collider[24];
        static readonly RaycastHit[] LineHits = new RaycastHit[8];

        /// <summary>Layers the body collides with as "the level" when it starts / checks tunnelling.</summary>
        static int EnvMask => Physics.DefaultRaycastLayers & ~(GameLayers.RagdollMask | (1 << 2) | (1 << 8) | (1 << 9));

        BlockyCharacter character;
        Transform owner;
        readonly Transform[] parts = new Transform[PartCount];
        readonly Transform[] colObjects = new Transform[PartCount];
        readonly BoxCollider[] cols = new BoxCollider[PartCount];
        readonly Rigidbody[] bodies = new Rigidbody[PartCount];
        readonly ConfigurableJoint[] joints = new ConfigurableJoint[PartCount];
        readonly bool[] present = new bool[PartCount];
        readonly bool[] rod = new bool[PartCount];

        // Pose of the last frame (velocity at the take-over).
        readonly Vector3[] samplePos = new Vector3[PartCount];
        readonly Quaternion[] sampleRot = new Quaternion[PartCount];
        float sampleTime = -1f;

        // Start pose (hard recovery when something goes badly wrong).
        Vector3 startPelvisPos;
        Quaternion startPelvisRot;
        readonly Quaternion[] startLocal = new Quaternion[PartCount];

        // Scratch.
        readonly Vector3[] velLin = new Vector3[PartCount];
        readonly Vector3[] velAng = new Vector3[PartCount];
        readonly Quaternion[] animLocal = new Quaternion[PartCount];
        readonly Vector3[] keptLin = new Vector3[PartCount];
        readonly Vector3[] keptAng = new Vector3[PartCount];

        bool active, frozen, notifySettle, interpolating;
        readonly System.Collections.Generic.List<Collider> extraIgnores = new System.Collections.Generic.List<Collider>();
        float simTime, stillTime, nextControllerRefresh;
        Vector3 lastGoodPelvis;
        int builtSevered = -1, builtRods = -1;

        /// <summary>Legs with a rod strapped on (bit mask of <see cref="Limb.LegR"/> / <see cref="Limb.LegL"/>), set by the action layer.</summary>
        public Func<int> RodLegs { get; set; }

        /// <summary>The bodies exist (simulating or lying frozen); the animator does not touch the rig meanwhile.</summary>
        public bool IsActive => active;

        /// <summary>Lying still: kinematic, colliders off.</summary>
        public bool IsFrozen => active && frozen;

        /// <summary>Time.time of the last <see cref="Begin"/>.</summary>
        public float StartTime { get; private set; } = -1f;

        /// <summary>Seconds from the start until everything lay still (-1 = still moving / never started).</summary>
        public float SettleTime { get; private set; } = -1f;

        /// <summary>How the last start went (velocities taken over, lift out of the level, start-pose adjustments).</summary>
        public string LastStartInfo { get; private set; } = "";

        /// <summary>Raised once per death when the body has come to rest (pool of blood under it etc.).</summary>
        public event Action<BlockyRagdoll> Settled;

        public BlockyCharacter Character => character;

        /// <summary>World centre of the torso block (where a body lies).</summary>
        public Vector3 TorsoCenter => character != null && character.Torso != null
            ? character.Torso.TransformPoint(Specs[Torso].Center * Px)
            : transform.position;

        /// <summary>Lowest point of the ragdoll's colliders (world Y), for tests.</summary>
        public float LowestPoint
        {
            get
            {
                float y = float.MaxValue;
                for (int i = 0; i < PartCount; i++)
                {
                    if (!present[i] || colObjects[i] == null) continue;
                    var c = cols[i];
                    Vector3 h = c.size * 0.5f;
                    for (int k = 0; k < 8; k++)
                    {
                        var corner = new Vector3((k & 1) == 0 ? -h.x : h.x, (k & 2) == 0 ? -h.y : h.y, (k & 4) == 0 ? -h.z : h.z);
                        y = Mathf.Min(y, colObjects[i].TransformPoint(c.center + corner).y);
                    }
                }
                return y == float.MaxValue ? transform.position.y : y;
            }
        }

        // ================================================================== lifecycle

        void Awake() => Resolve();

        bool Resolve()
        {
            if (character == null) character = GetComponent<BlockyCharacter>();
            if (character == null || !character.IsBuilt) return false;
            if (parts[0] != null) return true;
            parts[Pelvis] = character.Pelvis;
            parts[Torso] = character.Torso;
            parts[Head] = character.Head;
            parts[UpperArmR] = character.ShoulderRight;
            parts[LowerArmR] = character.ElbowRight;
            parts[UpperArmL] = character.ShoulderLeft;
            parts[LowerArmL] = character.ElbowLeft;
            parts[UpperLegR] = character.HipRight;
            parts[LowerLegR] = character.KneeRight;
            parts[UpperLegL] = character.HipLeft;
            parts[LowerLegL] = character.KneeLeft;
            var cc = GetComponentInParent<CharacterController>();
            owner = cc != null ? cc.transform : transform;
            return true;
        }

        void LateUpdate()
        {
            if (active || !Resolve()) return;
            for (int i = 0; i < PartCount; i++)
            {
                samplePos[i] = parts[i].position;
                sampleRot[i] = parts[i].rotation;
            }
            sampleTime = Time.time;
        }

        void OnDestroy()
        {
            if (character != null) character.IsRagdoll = false;
        }

        // ================================================================== public API

        /// <summary>
        /// Turns the character into a ragdoll from its current (animated) pose, carrying on with the motion of the last
        /// frame. <paramref name="push"/> (m/s) is a velocity kick at <paramref name="hitPoint"/>, e.g. a bullet.
        /// </summary>
        public void Begin(Vector3 hitPoint, Vector3 push, bool keepVelocity = true)
        {
            if (!Resolve()) return;
            if (active) Teardown();
            bool ok = Build(keepVelocity, false, true, out string info);
            if (!ok) return;
            StartTime = Time.time;
            SettleTime = -1f;
            notifySettle = true;
            startPelvisPos = parts[Pelvis].localPosition; // relative to the character root (it may ride an elevator)
            startPelvisRot = parts[Pelvis].localRotation;
            for (int i = 0; i < PartCount; i++) startLocal[i] = parts[i].localRotation;
            if (push.sqrMagnitude > 0.0001f) ApplyPush(hitPoint, push);
            LastStartInfo = info + (push.sqrMagnitude > 0.0001f ? $" push={push.magnitude:0.0}m/s" : "");
        }

        /// <summary>Back to animation: removes every body and joint (the rig keeps the pose it lies in).</summary>
        public void End()
        {
            if (!active) return;
            Teardown();
        }

        /// <summary>A hit on the body (wakes it up if it already lies still).</summary>
        public void Hit(Vector3 point, Vector3 push)
        {
            if (!active) return;
            Wake();
            ApplyPush(point, push);
        }

        /// <summary>The whole body jolts (adrenaline needle, a kick): a short shove up through the chest.</summary>
        public void Jolt(float strength = 1f)
        {
            if (!active) return;
            Wake();
            Vector3 up = Vector3.up * (1.1f * strength);
            if (bodies[Torso] != null) bodies[Torso].AddForce(up, ForceMode.VelocityChange);
            if (bodies[Pelvis] != null) bodies[Pelvis].AddForce(up * 0.45f, ForceMode.VelocityChange);
            if (bodies[Head] != null) bodies[Head].AddForce(up * 0.7f, ForceMode.VelocityChange);
        }

        /// <summary>A limb was cut off / put back while lying: rebuild without it (the motion carries on).</summary>
        internal void OnSeveredChanged()
        {
            if (!active) return;
            int sev = character.SeveredMask, rods = CurrentRods();
            if (sev == builtSevered && rods == builtRods) return;
            bool wasFrozen = frozen;
            for (int i = 0; i < PartCount; i++)
            {
                var rb = bodies[i];
                keptLin[i] = rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;
                keptAng[i] = rb != null && !rb.isKinematic ? rb.angularVelocity : Vector3.zero;
            }
            Teardown();
            Build(false, wasFrozen, false, out _);
            if (!wasFrozen)
                for (int i = 0; i < PartCount; i++)
                    if (bodies[i] != null)
                    {
                        bodies[i].linearVelocity = keptLin[i];
                        bodies[i].angularVelocity = keptAng[i];
                    }
        }

        /// <summary>A loose collider (a cut-off limb flying away) that must not collide with this body.</summary>
        internal void IgnoreWith(Collider c)
        {
            if (c == null) return;
            extraIgnores.RemoveAll(x => x == null);
            if (!extraIgnores.Contains(c)) extraIgnores.Add(c);
            for (int i = 0; i < PartCount; i++)
                if (cols[i] != null && cols[i].enabled) Physics.IgnoreCollision(cols[i], c, true);
        }

        // ================================================================== build / teardown

        int CurrentRods() => RodLegs != null ? RodLegs() & Limb.Legs : 0;

        bool Build(bool keepVelocity, bool kinematic, bool liftOut, out string info)
        {
            info = "";
            int sev = character.SeveredMask, rods = CurrentRods();
            builtSevered = sev;
            builtRods = rods;
            for (int i = 0; i < PartCount; i++)
            {
                var s = Specs[i];
                bool cut = s.Limb != 0 && (sev & s.Limb) != 0;
                rod[i] = false;
                present[i] = !cut;
                if (cut && (i == UpperLegR || i == UpperLegL) && (rods & s.Limb) != 0)
                {
                    present[i] = true; // the rod hangs from the hip instead of the leg
                    rod[i] = true;
                }
                if (present[i] && s.Parent >= 0 && !present[s.Parent]) present[i] = false;
            }

            EnsureColliders();
            for (int i = 0; i < PartCount; i++)
            {
                Vector3 center = (rod[i] ? RodCenter : Specs[i].Center) * Px;
                Vector3 size = (rod[i] ? RodSize : Specs[i].Size) * Px;
                cols[i].center = center;
                cols[i].size = size;
                colObjects[i].gameObject.layer = GameLayers.Ragdoll;
                cols[i].enabled = present[i] && !kinematic;
            }

            // 1. Velocity of every block in the last frame (before anything is moved here).
            float dt = Time.time - sampleTime;
            bool useSample = keepVelocity && sampleTime >= 0f && dt > 0.0005f && dt < 0.25f;
            int snapped = 0;
            for (int i = 0; i < PartCount; i++)
            {
                velLin[i] = Vector3.zero;
                velAng[i] = Vector3.zero;
                if (!useSample || !present[i]) continue;
                Vector3 c = cols[i].center;
                Vector3 now = parts[i].position + parts[i].rotation * c;
                Vector3 prev = samplePos[i] + sampleRot[i] * c;
                Vector3 v = (now - prev) / dt;
                Quaternion dq = parts[i].rotation * Quaternion.Inverse(sampleRot[i]);
                dq.ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                Vector3 w = float.IsFinite(axis.x) ? axis * (ang * Mathf.Deg2Rad / dt) : Vector3.zero;
                if (v.magnitude > SnapSpeed || w.magnitude > SnapSpin) { snapped++; continue; }
                velLin[i] = Vector3.ClampMagnitude(v, MaxStartSpeed);
                velAng[i] = Vector3.ClampMagnitude(w, MaxStartSpin);
            }
            if (snapped > 0)
                for (int i = 0; i < PartCount; i++) { velLin[i] = Vector3.zero; velAng[i] = Vector3.zero; } // a snap: start at rest

            // 2. Out of the level: an animated pose may dip into the floor or a wall a little.
            Vector3 lift = liftOut ? Depenetration() : Vector3.zero;
            if (lift.sqrMagnitude > 1e-6f) parts[Pelvis].position += lift;

            // 3. Joints are created with every limb turned to the middle of its range (that becomes the joint zero).
            int adjusted = 0;
            for (int i = 0; i < PartCount; i++)
            {
                animLocal[i] = parts[i].localRotation;
                if (!present[i] || Specs[i].Parent < 0) continue;
                var s = Specs[i];
                Quaternion mid = Quaternion.Euler(s.Mid);
                if (Outside(Quaternion.Inverse(mid) * animLocal[i], s))
                {
                    mid = FitMid(mid, animLocal[i], s);
                    adjusted++;
                }
                parts[i].localRotation = mid;
            }

            for (int i = 0; i < PartCount; i++)
            {
                if (!present[i]) continue;
                var rb = parts[i].gameObject.AddComponent<Rigidbody>();
                bodies[i] = rb;
                rb.mass = rod[i] ? RodMass : Specs[i].Mass;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.45f;
                rb.maxAngularVelocity = 25f;
                rb.maxDepenetrationVelocity = 1.2f;
                rb.solverIterations = 12;
                rb.solverVelocityIterations = 4;
                rb.sleepThreshold = 0.02f;
                rb.interpolation = RigidbodyInterpolation.None; // switched on after the first step (no blend from the joint set-up pose)
                rb.collisionDetectionMode = kinematic ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
                rb.isKinematic = kinematic;
            }

            Physics.SyncTransforms();
            for (int i = 0; i < PartCount; i++)
            {
                if (!present[i] || Specs[i].Parent < 0) continue;
                joints[i] = CreateJoint(i);
            }

            // 4. Back to the animated pose: the bodies start exactly where the animation left them.
            for (int i = 0; i < PartCount; i++) parts[i].localRotation = animLocal[i];
            Physics.SyncTransforms();
            for (int i = 0; i < PartCount; i++)
                if (bodies[i] != null) bodies[i].centerOfMass = cols[i].center; // uniform blocks: the centre of the box

            if (!kinematic)
                for (int i = 0; i < PartCount; i++)
                    if (bodies[i] != null)
                    {
                        bodies[i].linearVelocity = velLin[i];
                        bodies[i].angularVelocity = velAng[i];
                    }

            active = true;
            frozen = kinematic;
            simTime = 0f;
            stillTime = 0f;
            nextControllerRefresh = 0f;
            interpolating = false;
            lastGoodPelvis = PelvisCenter();
            character.IsRagdoll = true;
            if (!kinematic) ApplyIgnores();

            info = $"vel={(useSample ? (snapped > 0 ? "snap->0" : $"{velLin[Pelvis].magnitude:0.00}m/s") : "none")} lift={lift.magnitude:0.000} fit={adjusted} severed={sev} rods={rods}";
            return true;
        }

        ConfigurableJoint CreateJoint(int i)
        {
            var s = Specs[i];
            var j = parts[i].gameObject.AddComponent<ConfigurableJoint>();
            j.autoConfigureConnectedAnchor = true;
            j.anchor = Vector3.zero;
            j.axis = Vector3.right;
            j.secondaryAxis = Vector3.up;
            j.xMotion = ConfigurableJointMotion.Locked;
            j.yMotion = ConfigurableJointMotion.Locked;
            j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = s.LimX > 0f ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            j.angularYMotion = s.LimY > 0f ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            j.angularZMotion = s.LimZ > 0f ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            j.lowAngularXLimit = new SoftJointLimit { limit = -s.LimX };
            j.highAngularXLimit = new SoftJointLimit { limit = s.LimX };
            j.angularYLimit = new SoftJointLimit { limit = s.LimY };
            j.angularZLimit = new SoftJointLimit { limit = s.LimZ };
            j.rotationDriveMode = RotationDriveMode.Slerp;
            // No spring (a dead body has no muscle tone), only joint friction: limbs swing out and settle, never wobble.
            j.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = s.Damp, maximumForce = float.MaxValue, useAcceleration = true };
            j.enableCollision = false;
            j.enablePreprocessing = false;
            j.connectedBody = bodies[s.Parent]; // the joint frames are taken from the current (middle) pose here
            return j;
        }

        void Teardown()
        {
            for (int i = 0; i < PartCount; i++)
                if (joints[i] != null) { DestroyImmediate(joints[i]); joints[i] = null; }
            for (int i = 0; i < PartCount; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
                if (bodies[i] != null) { DestroyImmediate(bodies[i]); bodies[i] = null; }
            }
            active = false;
            frozen = false;
            if (character != null) character.IsRagdoll = false;
        }

        void EnsureColliders()
        {
            if (material == null)
                material = new PhysicsMaterial("BlockyRagdoll")
                {
                    dynamicFriction = 0.75f,
                    staticFriction = 0.95f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                    hideFlags = HideFlags.DontSave,
                };
            for (int i = 0; i < PartCount; i++)
            {
                if (colObjects[i] != null) continue;
                var go = new GameObject("RagdollCollider");
                go.layer = GameLayers.Ragdoll;
                go.transform.SetParent(parts[i], false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var bc = go.AddComponent<BoxCollider>();
                bc.sharedMaterial = material;
                bc.enabled = false;
                colObjects[i] = go.transform;
                cols[i] = bc;
            }
        }

        // ================================================================== joint ranges

        /// <summary>True when the local rotation <paramref name="q"/> (relative to the joint zero) is outside the limits.</summary>
        static bool Outside(Quaternion q, in Spec s, float margin = 3f)
        {
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            float tl = Mathf.Sqrt(q.x * q.x + q.w * q.w);
            if (tl < 1e-4f) return true; // a half turn of swing
            var twist = new Quaternion(q.x / tl, 0f, 0f, q.w / tl);
            float tw = 2f * Mathf.Atan2(twist.x, twist.w) * Mathf.Rad2Deg;
            if (tw > 180f) tw -= 360f;
            if (tw < -180f) tw += 360f;
            if (Mathf.Abs(tw) > Mathf.Max(0.5f, s.LimX - margin)) return s.LimX > 0f || Mathf.Abs(tw) > 1.5f;
            Quaternion swing = q * Quaternion.Inverse(twist);
            swing.ToAngleAxis(out float a, out Vector3 axis);
            if (a > 180f) a = 360f - a;
            if (a < 1f) return false;
            float ay = axis.y, az = axis.z;
            float len = Mathf.Sqrt(ay * ay + az * az);
            if (len < 1e-4f) return false;
            ay /= len;
            az /= len;
            float ly = Mathf.Max(0.5f, s.LimY), lz = Mathf.Max(0.5f, s.LimZ);
            float r = 1f / Mathf.Sqrt((ay / ly) * (ay / ly) + (az / lz) * (az / lz));
            return a > Mathf.Max(1f, r - margin);
        }

        /// <summary>Moves the joint zero towards the animated rotation just enough that the animated pose is inside the range.</summary>
        static Quaternion FitMid(Quaternion mid, Quaternion anim, in Spec s)
        {
            float lo = 0f, hi = 1f;
            for (int k = 0; k < 10; k++)
            {
                float m = (lo + hi) * 0.5f;
                Quaternion cand = Quaternion.Slerp(mid, anim, m);
                if (Outside(Quaternion.Inverse(cand) * anim, s)) lo = m;
                else hi = m;
            }
            return Quaternion.Slerp(mid, anim, Mathf.Min(1f, hi + 0.06f));
        }

        // ================================================================== collisions

        /// <summary>Pairs of blocks of one body that collide (the rest overlap at the joints by design).</summary>
        static bool Collides(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            if (Specs[b].Parent == a || Specs[a].Parent == b) return false;
            bool armA = a >= UpperArmR && a <= LowerArmL, armB = b >= UpperArmR && b <= LowerArmL;
            bool legA = a >= UpperLegR, legB = b >= UpperLegR;
            int sideA = a == UpperArmR || a == LowerArmR || a == UpperLegR || a == LowerLegR ? 1 : -1;
            int sideB = b == UpperArmR || b == LowerArmR || b == UpperLegR || b == LowerLegR ? 1 : -1;
            if (armA && armB) return sideA != sideB;                 // left arm vs right arm
            if (legA && legB) return sideA != sideB;                 // left leg vs right leg
            if (a == Pelvis) return b == LowerArmR || b == LowerArmL; // forearms rest on the hips; thighs / shins hinge there
            if (a == Torso) return armB || b == LowerLegR || b == LowerLegL; // arms across the chest, shins pulled up
            if (a == Head) return b == LowerArmR || b == LowerArmL || legB;
            return (armA && legB) || (legA && armB);                 // arms vs legs
        }

        void ApplyIgnores()
        {
            for (int a = 0; a < PartCount; a++)
            {
                if (!present[a]) continue;
                for (int b = a + 1; b < PartCount; b++)
                {
                    if (!present[b]) continue;
                    bool ignore = !Collides(a, b) || Overlapping(a, b);
                    Physics.IgnoreCollision(cols[a], cols[b], ignore);
                }
            }
            extraIgnores.RemoveAll(x => x == null);
            foreach (var c in extraIgnores)
                for (int i = 0; i < PartCount; i++)
                    if (present[i] && cols[i].enabled && c.enabled) Physics.IgnoreCollision(cols[i], c, true);
            IgnoreControllers();
        }

        bool Overlapping(int a, int b)
        {
            Transform ta = colObjects[a], tb = colObjects[b];
            return Physics.ComputePenetration(cols[a], ta.position, ta.rotation, cols[b], tb.position, tb.rotation, out _, out float d) && d > 0.002f;
        }

        /// <summary>
        /// Character controllers (players, RC cars) never touch the body: their layer overrides exclude the ragdoll layer and
        /// every pair is ignored explicitly as well (pairs are forgotten when a controller is switched off for a teleport,
        /// hence the periodic refresh while the body moves).
        /// </summary>
        void IgnoreControllers()
        {
            var ccs = FindObjectsByType<CharacterController>();
            foreach (var cc in ccs)
            {
                if (cc == null) continue;
                if ((cc.excludeLayers.value & GameLayers.RagdollMask) == 0) cc.excludeLayers = cc.excludeLayers.value | GameLayers.RagdollMask;
                if (!cc.enabled || !cc.gameObject.activeInHierarchy) continue;
                for (int i = 0; i < PartCount; i++)
                    if (present[i] && cols[i].enabled) Physics.IgnoreCollision(cols[i], cc, true);
            }
        }

        bool IsLevel(Collider c)
        {
            if (c == null || c.isTrigger || c is CharacterController) return false;
            if (c.gameObject.layer == GameLayers.Ragdoll) return false;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false;
            if (owner != null && c.transform.IsChildOf(owner)) return false;
            return true;
        }

        /// <summary>Smallest shift of the whole body that takes every block out of the level (capped).</summary>
        Vector3 Depenetration()
        {
            Vector3 pos = Vector3.zero, neg = Vector3.zero;
            int mask = EnvMask;
            for (int i = 0; i < PartCount; i++)
            {
                if (!present[i]) continue;
                var c = cols[i];
                Transform t = colObjects[i];
                Vector3 center = t.TransformPoint(c.center);
                Vector3 half = Vector3.Scale(c.size * 0.5f, t.lossyScale);
                int n = Physics.OverlapBoxNonAlloc(center, half, Overlaps, t.rotation, mask, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++)
                {
                    var other = Overlaps[k];
                    if (!IsLevel(other)) continue;
                    if (!Physics.ComputePenetration(c, t.position, t.rotation, other, other.transform.position, other.transform.rotation,
                            out Vector3 dir, out float dist) || dist < 0.002f) continue;
                    Vector3 v = dir * (dist + 0.004f);
                    pos = Vector3.Max(pos, v);
                    neg = Vector3.Min(neg, v);
                }
            }
            return Vector3.ClampMagnitude(pos + neg, MaxLift);
        }

        // ================================================================== simulation

        Vector3 PelvisCenter() => bodies[Pelvis] != null ? bodies[Pelvis].worldCenterOfMass : parts[Pelvis].position;

        void FixedUpdate()
        {
            if (!active || frozen) return;
            float dt = Time.fixedDeltaTime;
            simTime += dt;
            if (!interpolating)
            {
                interpolating = true;
                for (int i = 0; i < PartCount; i++)
                    if (bodies[i] != null) bodies[i].interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (simTime >= nextControllerRefresh)
            {
                nextControllerRefresh = simTime + ControllerRefresh;
                IgnoreControllers();
            }

            // Never through the floor: if the pelvis crossed a level surface since the last good step, put it back.
            Vector3 pc = PelvisCenter();
            if (!float.IsFinite(pc.x) || !float.IsFinite(pc.y) || !float.IsFinite(pc.z) || pc.y < character.transform.position.y - 4f)
            {
                Recover();
                return;
            }
            if (Crossed(lastGoodPelvis, pc, out Vector3 back))
            {
                Vector3 shift = back - pc;
                for (int i = 0; i < PartCount; i++)
                {
                    var rb = bodies[i];
                    if (rb == null) continue;
                    rb.position += shift;
                    Vector3 v = rb.linearVelocity;
                    if (v.y < 0f) v.y = 0f;
                    rb.linearVelocity = v * 0.3f;
                }
                Debug.Log($"[Ragdoll] {name}: pelvis went through the level, moved back {shift.magnitude:0.00} m");
            }
            else lastGoodPelvis = pc;

            // Settle: everything still for a moment -> frozen.
            bool still = true;
            for (int i = 0; i < PartCount && still; i++)
            {
                var rb = bodies[i];
                if (rb == null || rb.IsSleeping()) continue;
                if (rb.linearVelocity.sqrMagnitude > StillSpeed * StillSpeed || rb.angularVelocity.sqrMagnitude > StillSpin * StillSpin) still = false;
            }
            stillTime = still ? stillTime + dt : 0f;
            if ((stillTime >= StillTimeToFreeze && simTime >= MinSimTime) || simTime >= MaxSimTime) Freeze();
        }

        bool Crossed(Vector3 from, Vector3 to, out Vector3 back)
        {
            back = from;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f || len > 1f) return false; // more than 1 m in one step: moved with its root (lift, teleport)
            int n = Physics.RaycastNonAlloc(from, d / len, LineHits, len, EnvMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = LineHits[i];
                if (!IsLevel(h.collider) || h.distance >= best) continue;
                if (Vector3.Dot(h.normal, d) >= 0f) continue; // only when entering a surface from its front side
                best = h.distance;
                back = h.point + h.normal * 0.06f;
            }
            return best < float.MaxValue;
        }

        void Freeze()
        {
            for (int i = 0; i < PartCount; i++)
            {
                var rb = bodies[i];
                if (rb == null) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.isKinematic = true;
            }
            for (int i = 0; i < PartCount; i++) if (cols[i] != null) cols[i].enabled = false;
            frozen = true;
            SettleTime = Time.time - StartTime;
            if (!notifySettle) return;
            notifySettle = false;
            try { Settled?.Invoke(this); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void Wake()
        {
            if (!frozen) return;
            for (int i = 0; i < PartCount; i++)
                if (cols[i] != null) cols[i].enabled = present[i];
            for (int i = 0; i < PartCount; i++)
            {
                var rb = bodies[i];
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.WakeUp();
            }
            frozen = false;
            simTime = 0f;
            stillTime = 0f;
            nextControllerRefresh = 0f;
            lastGoodPelvis = PelvisCenter();
            ApplyIgnores();
        }

        /// <summary>Something went badly wrong (fell out of the world, NaN): back to the start pose, frozen.</summary>
        void Recover()
        {
            Debug.Log($"[Ragdoll] {name}: lost the body, back to the start pose");
            for (int i = 0; i < PartCount; i++)
            {
                var rb = bodies[i];
                if (rb == null) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.isKinematic = true;
            }
            parts[Pelvis].localPosition = startPelvisPos;
            parts[Pelvis].localRotation = startPelvisRot;
            for (int i = 1; i < PartCount; i++) parts[i].localRotation = startLocal[i];
            Physics.SyncTransforms();
            for (int i = 0; i < PartCount; i++) if (cols[i] != null) cols[i].enabled = false;
            frozen = true;
            SettleTime = Time.time - StartTime;
        }

        void ApplyPush(Vector3 point, Vector3 push)
        {
            // The whole body takes part of it, the block that was hit the rest (at the hit point: it also spins).
            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < PartCount; i++)
            {
                var rb = bodies[i];
                if (rb == null) continue;
                rb.AddForce(push * 0.35f, ForceMode.VelocityChange);
                float d = (rb.worldCenterOfMass - point).sqrMagnitude;
                if (d < best) { best = d; hit = i; }
            }
            if (hit >= 0)
            {
                var rb = bodies[hit];
                Vector3 at = best < 0.36f ? point : rb.worldCenterOfMass;
                rb.AddForceAtPosition(push * (0.65f * rb.mass), at, ForceMode.Impulse);
            }
        }
    }
}
