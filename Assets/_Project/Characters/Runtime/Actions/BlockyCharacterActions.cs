using System;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Third-person action layer on top of <see cref="BlockyCharacterAnimator"/> (knife kill, stabbed reaction,
    /// downed crawl / dead, held props, Glock shoot / reload, hit flinch, reviving).
    /// <para>
    /// Runs right after the locomotion pose every frame: <see cref="BlockyCharacterAnimator.Tick"/> calls
    /// <see cref="ApplyLayer"/> at its end, so it works both with the animator's LateUpdate and with manual ticking.
    /// The layer captures the locomotion pose, blends its own layers on top (item hold, reload, revive, one-shot
    /// actions, life state, additive flinches) and writes the result back. Every discontinuity (action start / end,
    /// item change) is hidden by a 0.15 s cross-fade from the last output pose.
    /// </para>
    /// All calls are local and deterministic: on a network game call them from RPCs / state changes on every client.
    /// </summary>
    [DisallowMultipleComponent]
    public partial class BlockyCharacterActions : MonoBehaviour, ICharacterActions
    {
        /// <summary>Times (s from PlayKnifeAttack) at which the blade hits. Same table drives the victim flinches.</summary>
        public static readonly float[] KnifeStabTimes = ActionTimeline.KnifeStabTimes;

        public const float KnifeAttackDuration = ActionTimeline.KnifeAttackDuration;
        public const float StabbedReactionDuration = ActionTimeline.StabbedReactionDuration;
        public const float ReloadDuration = ActionTimeline.ReloadDuration;
        public const float KnifeAttackDistance = ActionTimeline.KnifeAttackDistance;

        /// <summary>Seeker: fired when the blade hits (index 0..3), in sync with <see cref="KnifeStabTimes"/>.</summary>
        public event Action<int> StabHit;

        /// <summary>Glock reload sub-events for sound sync.</summary>
        public event Action ReloadMagOut, ReloadMagIn, ReloadSlideRack;

        const float Px = BlockyCharacter.PixelSize;
        const float CrossfadeTime = 0.15f;

        enum ActionKind
        {
            None,
            KnifeAttack,
            StabbedReaction,
            Reload
        }

        BlockyCharacter character;
        BlockyCharacterAnimator animator;
        bool initialized;
        Transform chestPoint, socketR;
        readonly HeldProp[] props = new HeldProp[4];
        GameObject handMag;

        HeldItem heldItem;
        HeldItem shownItem;
        float itemW;

        ActionKind action;
        float actionTime;
        BlockyCharacter knifeVictim;
        int firedEvents;

        bool reviving;
        float reviveW, reviveTime;

        LifeState lifeState;
        bool pendingDowned;

        // Cross-fade ("dead blend") from the last output pose.
        CharacterPose lastOutput, fadeFrom;
        bool hasLast;
        float fadeT = 1f;

        // Additive springs.
        float recoil, recoilVel, hit, hitVel, hitSide = 1f;
        float aimRaise, slideKick;

        // Downed.
        float crawlPhase, crawlW, twitchTime = 99f, nextTwitch = 3f, breathPhase;
        float time;
        uint rng = 0x2545F491u;

        bool lastFirstPerson;
        int lastLayer = -1;

        // ================================================================== public API (ICharacterActions)

        public HeldItem HeldItem => heldItem;
        public LifeState LifeState => lifeState;

        /// <summary>Life state as shown (Downed after a stabbed reaction even if SetLifeState was not called).</summary>
        public LifeState VisualLifeState => pendingDowned && lifeState == LifeState.Alive ? LifeState.Downed : lifeState;

        /// <summary>True while a one-shot action (knife attack, stabbed reaction, reload) is playing.</summary>
        public bool IsBusy => action != ActionKind.None;

        public bool IsReviving => reviving;

        public Transform ChestPoint
        {
            get
            {
                EnsureInit();
                return chestPoint;
            }
        }

        public Transform MuzzlePoint
        {
            get
            {
                if (shownItem != HeldItem.Glock) return null;
                var p = props[(int)HeldItem.Glock];
                return p != null && p.gameObject.activeInHierarchy ? p.Muzzle : null;
            }
        }

        public float PlayKnifeAttack() => PlayKnifeAttack(null);

        /// <summary>
        /// Seeker knife kill. With <paramref name="victim"/> the hands track the victim's actual shoulder / belly;
        /// without it the victim is assumed at <see cref="KnifeAttackDistance"/> straight ahead, facing us.
        /// </summary>
        public float PlayKnifeAttack(BlockyCharacter victim)
        {
            EnsureInit();
            knifeVictim = victim;
            StartAction(ActionKind.KnifeAttack);
            return KnifeAttackDuration;
        }

        public float PlayStabbedReaction()
        {
            EnsureInit();
            reviving = false;
            reviveW = 0f;
            StartAction(ActionKind.StabbedReaction);
            return StabbedReactionDuration;
        }

        public void SetHeldItem(HeldItem item)
        {
            EnsureInit();
            if (item == heldItem) return;
            heldItem = item;
            BeginCrossfade();
        }

        public void SetLifeState(LifeState state)
        {
            EnsureInit();
            LifeState shown = VisualLifeState;
            if (state == lifeState)
            {
                // Alive while still lying after a stabbed reaction -> stand up.
                if (state == LifeState.Alive && pendingDowned && action != ActionKind.StabbedReaction)
                {
                    pendingDowned = false;
                    StartLifePath(LifeState.Downed, LifeState.Alive);
                }

                return;
            }

            lifeState = state;
            pendingDowned = false;
            if (state != LifeState.Alive) reviving = false;
            if (state != LifeState.Alive && action == ActionKind.Reload) action = ActionKind.None;
            if (action == ActionKind.StabbedReaction) return; // resolved when the reaction ends
            if (action == ActionKind.KnifeAttack && state != LifeState.Alive) action = ActionKind.None;
            if (shown != state) StartLifePath(shown, state);
        }

        public void PlayShoot()
        {
            EnsureInit();
            recoilVel += 30f;
            aimRaise = 1.4f;
            slideKick = 0.07f;
        }

        public float PlayReload()
        {
            EnsureInit();
            StartAction(ActionKind.Reload);
            return ReloadDuration;
        }

        public void PlayHitReaction()
        {
            EnsureInit();
            hitVel += 16f;
            hitSide = Rand01() < 0.5f ? -1f : 1f;
        }

        bool sitting;
        float sitW;

        /// <summary>Sit on a seat (root on the floor in front of the seat, seat top ~0.45 m). Blends in/out over 0.35 s.</summary>
        public void SetSitting(bool value)
        {
            EnsureInit();
            sitting = value;
        }

        public bool IsSitting => sitting;

        public void SetReviving(bool value)
        {
            EnsureInit();
            if (reviving == value) return;
            reviving = value;
            if (value) reviveTime = 0f;
        }

        /// <summary>Stops every one-shot action / revive immediately (with a short cross-fade).</summary>
        public void CancelActions()
        {
            action = ActionKind.None;
            reviving = false;
            BeginCrossfade();
        }

        /// <summary>Snap to a state without transition animation (late joiners, respawn).</summary>
        public void SetLifeStateImmediate(LifeState state)
        {
            EnsureInit();
            lifeState = state;
            pendingDowned = false;
            pathActive = false;
            action = ActionKind.None;
            reviving = false;
            reviveW = 0f;
            hasLast = false;
            fadeT = 1f;
        }

        // ================================================================== setup

        void EnsureInit()
        {
            if (initialized) return;
            if (character == null) character = GetComponent<BlockyCharacter>();
            if (character == null || !character.IsBuilt) return;
            animator = GetComponent<BlockyCharacterAnimator>();
            initialized = true;

            chestPoint = new GameObject("ChestPoint").transform;
            chestPoint.SetParent(character.Torso, false);
            chestPoint.localPosition = new Vector3(0f, 8f * Px, 0f);
            chestPoint.gameObject.layer = gameObject.layer;

            socketR = new GameObject("HandSocketR").transform;
            socketR.SetParent(character.ElbowRight, false);
            socketR.localPosition = CharacterPose.HandLocal;
            socketR.gameObject.layer = gameObject.layer;

            uint seed = (uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this) * 2246822519u;
            rng = seed == 0 ? 0x2545F491u : seed;
            nextTwitch = Rand(2f, 5f);
            breathPhase = Rand(0f, 6f);
        }

        void StartAction(ActionKind kind)
        {
            BeginCrossfade();
            action = kind;
            actionTime = 0f;
            firedEvents = 0;
        }

        void BeginCrossfade()
        {
            if (!hasLast) return;
            fadeFrom = lastOutput;
            fadeT = 0f;
        }

        HeldProp GetProp(HeldItem item)
        {
            if (item == HeldItem.None) return null;
            var p = props[(int)item];
            if (p != null) return p;
            p = BlockyProps.Create(item, socketR, gameObject.layer);
            p.gameObject.SetActive(false);
            props[(int)item] = p;
            lastLayer = -1; // force render-mode refresh
            return p;
        }

        // ================================================================== per-frame

        /// <summary>Called by <see cref="BlockyCharacterAnimator.Tick"/> right after the locomotion pose is written.</summary>
        public void ApplyLayer(float dt)
        {
            EnsureInit();
            if (!initialized || dt <= 0f) return;
            time += dt;

            ICharacterMotionSource source = animator != null ? animator.Source : null;
            float speed = source != null ? Mathf.Max(0f, source.HorizontalSpeed) : 0f;
            float lookPitch = source != null ? source.LookPitch : 0f;

            AdvanceTimers(dt, speed);

            var basePose = CharacterPose.Capture(character);
            HeldItem show = shownItem = ResolveShownItem();
            basePose.PropR = DefaultPropLocal(show);
            var p = basePose;
            LifeState visual = VisualLifeState;
            bool standing = (visual == LifeState.Alive && !pathActive) || action == ActionKind.StabbedReaction;

            // 1. Item hold (arms).
            float targetItemW = show != HeldItem.None ? 1f : 0f;
            itemW = Damp(itemW, targetItemW, 12f, dt);
            if (show != HeldItem.None && itemW > 0.001f)
            {
                var ip = ItemHoldPose(p, show, lookPitch);
                CharacterPose.LerpArm(ref p, ip, 0, itemW);
                if (show == HeldItem.Glock) CharacterPose.LerpArm(ref p, ip, 1, itemW);
                p.Torso = Quaternion.Slerp(p.Torso, ip.Torso, itemW);
            }

            // 2. Reload (arms, on top of the hold).
            if (action == ActionKind.Reload)
            {
                var rp = ReloadPose(p, lookPitch, actionTime);
                CharacterPose.LerpArm(ref p, rp, 0, 1f);
                CharacterPose.LerpArm(ref p, rp, 1, 1f);
            }

            // 3. Revive loop (full body).
            if (reviveW > 0.001f) p = CharacterPose.Lerp(p, RevivePose(), ActionTimeline.SmoothStep01(reviveW));

            // 3b. Sitting (full body, head keeps looking around from the base pose).
            if (sitW > 0.001f)
            {
                var sp = SitPose();
                sp.Head = p.Head;
                p = CharacterPose.Lerp(p, sp, ActionTimeline.SmoothStep01(sitW));
            }

            // 4. One-shot full-body actions.
            if (action == ActionKind.KnifeAttack)
            {
                var kp = KnifeAttackPose(p, actionTime);
                float w = 1f - ActionTimeline.SmoothStep01(ActionTimeline.Remap01(actionTime, KnifeAttackDuration - 0.25f, KnifeAttackDuration));
                p = CharacterPose.Lerp(p, kp, w);
            }
            else if (action == ActionKind.StabbedReaction)
            {
                p = StabbedReactionPose(actionTime, p.PropR);
            }

            // 5. Life state (downed / dead / transitions).
            if (action != ActionKind.StabbedReaction && (pathActive || visual != LifeState.Alive))
            {
                var lp = pathActive ? EvaluatePath(basePose) : PoseFor(visual, basePose);
                lp.PropR = p.PropR;
                p = lp;
            }

            // 6. Additive flinches.
            ApplyAdditives(ref p, standing);

            // 7. Cross-fade from the last output.
            if (fadeT < 1f)
            {
                fadeT = Mathf.Min(1f, fadeT + dt / CrossfadeTime);
                if (hasLast) p = CharacterPose.Lerp(fadeFrom, p, ActionTimeline.SmoothStep01(fadeT));
            }

            p.Apply(character);
            lastOutput = p;
            hasLast = true;

            UpdateProps(p, show);
        }

        HeldItem ResolveShownItem()
        {
            if (action == ActionKind.KnifeAttack) return HeldItem.Knife;
            if (action == ActionKind.Reload) return HeldItem.Glock;
            return heldItem;
        }

        void AdvanceTimers(float dt, float speed)
        {
            // One-shot action clock + events.
            if (action != ActionKind.None)
            {
                float prev = actionTime;
                actionTime += dt;
                if (action == ActionKind.KnifeAttack)
                {
                    while (firedEvents < KnifeStabTimes.Length && actionTime >= KnifeStabTimes[firedEvents])
                    {
                        int i = firedEvents++;
                        try { StabHit?.Invoke(i); }
                        catch (Exception e) { Debug.LogException(e); }
                    }

                    if (actionTime >= KnifeAttackDuration) EndAction();
                }
                else if (action == ActionKind.StabbedReaction)
                {
                    if (actionTime >= StabbedReactionDuration) EndStabbedReaction();
                }
                else if (action == ActionKind.Reload)
                {
                    if (Crossed(prev, actionTime, ActionTimeline.ReloadMagOutTime))
                    {
                        DropMagazine();
                        Fire(ReloadMagOut);
                    }

                    if (Crossed(prev, actionTime, ActionTimeline.ReloadMagInTime)) Fire(ReloadMagIn);
                    if (Crossed(prev, actionTime, ActionTimeline.ReloadSlideRackTime)) Fire(ReloadSlideRack);
                    if (actionTime >= ReloadDuration) EndAction();
                }
            }

            // Revive weight + pump clock.
            bool canRevive = reviving && VisualLifeState == LifeState.Alive && action != ActionKind.StabbedReaction;
            reviveW = Mathf.MoveTowards(reviveW, canRevive ? 1f : 0f, dt / 0.3f);
            bool canSit = sitting && VisualLifeState == LifeState.Alive && action == ActionKind.None;
            sitW = Mathf.MoveTowards(sitW, canSit ? 1f : 0f, dt / 0.35f);
            reviveTime += dt;

            // Springs.
            StepSpring(ref recoil, ref recoilVel, 420f, 0.5f, dt);
            StepSpring(ref hit, ref hitVel, 240f, 0.42f, dt);
            aimRaise = Mathf.Max(0f, aimRaise - dt * 0.9f);
            slideKick = Mathf.Max(0f, slideKick - dt);

            // Life path.
            if (pathActive)
            {
                pathTime += dt;
                if (pathTime >= keys[keyCount - 1].Time) pathActive = false;
            }

            // Downed: crawl phase, twitches, breathing.
            bool downed = VisualLifeState == LifeState.Downed;
            bool crawling = downed && speed > 0.05f && action == ActionKind.None;
            crawlW = Damp(crawlW, crawling ? 1f : 0f, 5f, dt);
            if (downed) crawlPhase += Mathf.Min(speed, 2.5f) * dt / CrawlStride;
            crawlPhase -= Mathf.Floor(crawlPhase);
            breathPhase += dt * Mathf.Lerp(2.4f, 3.6f, crawlW);
            if (breathPhase > 1000f) breathPhase -= 1000f;
            twitchTime += dt;
            if (downed && !pathActive && time >= nextTwitch)
            {
                twitchTime = 0f;
                nextTwitch = time + Rand(2.5f, 6f);
            }
        }

        static bool Crossed(float prev, float now, float mark) => prev < mark && now >= mark;

        static void Fire(Action a)
        {
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void EndAction()
        {
            action = ActionKind.None;
            knifeVictim = null;
            BeginCrossfade();
        }

        void EndStabbedReaction()
        {
            action = ActionKind.None;
            // The reaction ends exactly in the downed pose.
            if (lifeState == LifeState.Alive) pendingDowned = true;
            else if (lifeState == LifeState.Dead) StartLifePath(LifeState.Downed, LifeState.Dead);
        }

        // ================================================================== additive layers

        void ApplyAdditives(ref CharacterPose p, bool standing)
        {
            float h = hit;
            if (Mathf.Abs(h) > 0.001f)
            {
                float k = standing ? 1f : 0.4f;
                p.Torso = p.Torso * Quaternion.Euler(-10f * h * k, 8f * h * hitSide * k, 5f * h * hitSide * k);
                p.Head = p.Head * Quaternion.Euler(-12f * h * k, -6f * h * hitSide * k, 0f);
                p.ShoulderR = Quaternion.Euler(0f, 0f, 9f * h * k) * p.ShoulderR;
                p.ShoulderL = Quaternion.Euler(0f, 0f, -9f * h * k) * p.ShoulderL;
                if (standing) p.PelvisPos += new Vector3(0f, -0.02f * Mathf.Abs(h), -0.025f * h);
            }

            if (Mathf.Abs(recoil) > 0.001f && standing)
                p.Torso = p.Torso * Quaternion.Euler(-2.5f * recoil, 1.5f * recoil, 0f);
        }

        // ================================================================== props

        void UpdateProps(in CharacterPose p, HeldItem show)
        {
            // Render mode / layer follow the character (first-person body = shadows only).
            bool fp = character.IsFirstPerson;
            int layer = gameObject.layer;
            if (show != HeldItem.None) GetProp(show);
            if (fp != lastFirstPerson || layer != lastLayer)
            {
                lastFirstPerson = fp;
                lastLayer = layer;
                foreach (var prop in props)
                {
                    if (prop == null) continue;
                    prop.SetLayer(layer);
                    prop.SetRenderMode(fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On, true);
                }

                if (handMag != null) SetMagRender(handMag, layer, fp);
                chestPoint.gameObject.layer = layer;
                socketR.gameObject.layer = layer;
            }

            for (int i = 1; i < props.Length; i++)
            {
                var prop = props[i];
                if (prop == null) continue;
                bool on = (HeldItem)i == show;
                if (prop.gameObject.activeSelf != on) prop.gameObject.SetActive(on);
            }

            socketR.localPosition = CharacterPose.HandLocal;
            socketR.localRotation = p.PropR;

            // Medkit is put down on the floor while reviving.
            if (show == HeldItem.Medkit && reviveW > 0.5f)
                socketR.SetPositionAndRotation(transform.TransformPoint(new Vector3(0.38f, 0.37f, 0.12f)), transform.rotation * Quaternion.Euler(0f, 25f, 0f));

            var glock = props[(int)HeldItem.Glock];
            bool magInHand = false;
            if (glock != null && show == HeldItem.Glock)
            {
                float slide = slideKick > 0.035f ? 1f : slideKick / 0.035f;
                bool magInGun = true;
                if (action == ActionKind.Reload)
                {
                    var s = ActionTimeline.SampleReload(actionTime);
                    slide = Mathf.Max(slide, s.SlideBack);
                    magInGun = s.MagInGun;
                    magInHand = s.MagInHand;
                }

                glock.SetSlideBack(slide);
                glock.SetMagVisible(magInGun);
            }

            if (magInHand)
            {
                if (handMag == null)
                {
                    handMag = BlockyProps.CreateMagazine(transform, gameObject.layer);
                    SetMagRender(handMag, gameObject.layer, character.IsFirstPerson);
                }

                if (!handMag.activeSelf) handMag.SetActive(true);
                Vector3 handL = character.ElbowLeft.TransformPoint(CharacterPose.HandLocal);
                Quaternion gunRot = socketR.rotation;
                handMag.transform.SetPositionAndRotation(handL - gunRot * ActionTimeline.MagInsertHandLocal, gunRot);
            }
            else if (handMag != null && handMag.activeSelf)
            {
                handMag.SetActive(false);
            }
        }

        static void SetMagRender(GameObject mag, int layer, bool fp)
        {
            foreach (var t in mag.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in mag.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        void DropMagazine()
        {
            var glock = props[(int)HeldItem.Glock];
            if (glock == null || !glock.gameObject.activeInHierarchy || !Application.isPlaying) return;
            var mag = BlockyProps.CreateMagazine(null, gameObject.layer);
            mag.transform.SetPositionAndRotation(glock.transform.position, glock.transform.rotation);
            SetMagRender(mag, gameObject.layer, character.IsFirstPerson);
            var drop = mag.AddComponent<DroppedProp>();
            drop.groundY = transform.position.y + 0.035f;
            drop.velocity = glock.transform.rotation * new Vector3(0f, -0.6f, 0f) + transform.right * 0.2f;
        }

        // ================================================================== helpers

        static float Damp(float current, float target, float lambda, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));

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

        float Rand01()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng & 0xFFFFFF) / (float)0x1000000;
        }

        float Rand(float min, float max) => min + (max - min) * Rand01();
    }

    /// <summary>Falls with gravity and a little spin, rests on a flat floor height, then disappears.</summary>
    public class DroppedProp : MonoBehaviour
    {
        public float groundY;
        public Vector3 velocity;
        public float lifetime = 4f;
        Vector3 spin;
        float age;
        bool resting;

        void Start()
        {
            spin = new Vector3(UnityEngine.Random.Range(-400f, 400f), UnityEngine.Random.Range(-200f, 200f), UnityEngine.Random.Range(-300f, 300f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (!resting)
            {
                velocity += Physics.gravity * dt;
                transform.position += velocity * dt;
                transform.rotation = Quaternion.Euler(spin * dt) * transform.rotation;
                if (transform.position.y <= groundY)
                {
                    transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                    // Lie flat on a side.
                    transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 90f);
                    resting = true;
                }
            }

            if (age > lifetime) Destroy(gameObject);
        }
    }
}
