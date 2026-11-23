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
            Reload,
            Kill,
            KillReaction
        }

        BlockyCharacter character;
        BlockyCharacterAnimator animator;
        bool initialized;
        Transform chestPoint, socketR;
        readonly HeldProp[] props = new HeldProp[12];
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
            if (!hasLast) drawnItem = item; // nothing drawn yet (new character, late joiner): no put-away motion
            BeginCrossfade();
        }

        public void SetLifeState(LifeState state)
        {
            EnsureInit();
            if (state != LifeState.Dead && IsRagdolled)
            {
                // Leaving death while lying as a ragdoll: the transition starts from where the body really lies.
                bool early = lifeState != LifeState.Dead; // ragdoll from the end of a finisher, Dead never arrived
                StopRagdoll();
                if (early)
                {
                    lifeState = LifeState.Dead;
                    action = ActionKind.None;
                    knifeVictim = null;
                    killer = null;
                    pendingDowned = false;
                    pathActive = false;
                }
            }
            if (state == LifeState.Dead && lifeState != LifeState.Dead) deathTime = Time.time;
            LifeState shown = VisualLifeState;
            if (state == lifeState)
            {
                // Alive while still lying after a stabbed reaction -> stand up.
                if (state == LifeState.Alive && pendingDowned && action != ActionKind.StabbedReaction && action != ActionKind.KillReaction)
                {
                    pendingDowned = false;
                    StartLifePath(LifeState.Downed, LifeState.Alive);
                }

                return;
            }

            LifeState before = lifeState;
            lifeState = state;
            pendingDowned = false;
            if (state != LifeState.Alive) reviving = false;
            if (before == LifeState.Dead && state == LifeState.Alive)
            {
                // Brought back (adrenaline): whatever death reaction / path was still running is dropped, the
                // character always gets up from where it lies (never stays frozen in a reaction pose).
                if (action != ActionKind.None) action = ActionKind.None;
                knifeVictim = null;
                stompT = -1f;
                rcW = 0f;
                StartLifePath(LifeState.Dead, LifeState.Alive);
                return;
            }
            if (state != LifeState.Alive && action == ActionKind.Reload) action = ActionKind.None;
            if (action == ActionKind.StabbedReaction) return; // resolved when the reaction ends
            if (action == ActionKind.KillReaction)
            {
                // Normal kills resolve when the reaction ends; the finisher ends in the dead pose already.
                if (KillMoves.EndsDead(killMove) && state == LifeState.Dead && actionTime >= killVictimDuration) EndFinisherReaction();
                return;
            }
            if ((action == ActionKind.KnifeAttack || action == ActionKind.Kill) && state != LifeState.Alive) action = ActionKind.None;
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
            if (JoltRagdoll()) return;
            hitVel += 16f;
            hitSide = Rand01() < 0.5f ? -1f : 1f;
        }

        bool sitting;
        float sitW;

        /// <summary>Sit on a seat (see <see cref="SetSitSeat"/> / <see cref="BeginSitFrom"/>). Blends in over 0.4-0.9 s, out over 0.45 s.</summary>
        public void SetSitting(bool value)
        {
            EnsureInit();
            sitting = value;
            if (!value) sitFromValid = false;
        }

        public bool IsSitting => sitting;

        float proneW;
        bool revving;

        /// <summary>Chainsaw throttle (F held): bar raised, everything shakes.</summary>
        public void SetRevving(bool value) => revving = value;

        float sawStartT = 99f;
        float sawBlood;

        /// <summary>Time since the chainsaw came out (pull-start animation, engine fires at <see cref="BlockyProps.ChainsawFireTime"/>).</summary>
        public float SawStartTime => sawStartT;
        /// <summary>Blood on the chain (1 = just killed), set by the game.</summary>
        public void SetSawBlood(float amount) => sawBlood = amount;
        public bool IsRevving => revving;

        /// <summary>Voluntary crawl (0 = upright, 1 = flat on the belly); driven every frame from the motor.</summary>
        public void SetProneAmount(float amount)
        {
            EnsureInit();
            proneW = Mathf.Clamp01(amount);
        }

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
            if (state != LifeState.Dead && IsRagdolled) ragdoll.End();
            if (state == LifeState.Dead && !IsRagdolled)
            {
                deathTime = Time.time;
                snappedDead = true; // the pose jumps to the dead pose: the ragdoll starts at rest
            }
            lifeState = state;
            pendingDowned = false;
            pathActive = false;
            action = ActionKind.None;
            reviving = false;
            reviveW = 0f;
            hasLast = false;
            fadeT = 1f;
            drawnItem = heldItem;
            carryW = 0f;
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
            if (!initialized || dt <= 0f || IsRagdolled) return;
            time += dt;

            ICharacterMotionSource source = animator != null ? animator.Source : null;
            float speed = source != null ? Mathf.Max(0f, source.HorizontalSpeed) : 0f;
            float lookPitch = source != null ? source.LookPitch : 0f;

            AdvanceTimers(dt, speed);

            var basePose = CharacterPose.Capture(character);
            var prevShown = shownItem;
            HeldItem show = shownItem = ResolveShownItem();
            if (show == HeldItem.Chainsaw && prevShown != HeldItem.Chainsaw && !revving && action != ActionKind.Kill) sawStartT = 0f;
            if (revving || action == ActionKind.Kill) sawStartT = Mathf.Max(sawStartT, BlockyProps.ChainsawStartTime);
            sawStartT += dt;
            basePose.PropR = DefaultPropLocal(show);
            var p = basePose;
            LifeState visual = VisualLifeState;
            bool standing = (visual == LifeState.Alive && !pathActive) || action == ActionKind.StabbedReaction || action == ActionKind.KillReaction;

            // 1. Item hold (arms).
            float targetItemW = show != HeldItem.None && !Holstering(show) ? 1f : 0f;
            itemW = Damp(itemW, targetItemW, 12f, dt);
            if (show != HeldItem.None && itemW > 0.001f)
            {
                var ip = ItemHoldPose(p, show, lookPitch);
                CharacterPose.LerpArm(ref p, ip, 0, itemW);
                if (show == HeldItem.Glock || show == HeldItem.Chainsaw || show == HeldItem.RcRemote) CharacterPose.LerpArm(ref p, ip, 1, itemW);
                p.Torso = Quaternion.Slerp(p.Torso, ip.Torso, itemW);
            }

            // 2. Reload (arms, on top of the hold).
            if (action == ActionKind.Reload)
            {
                var rp = ReloadPose(p, lookPitch, actionTime);
                CharacterPose.LerpArm(ref p, rp, 0, 1f);
                CharacterPose.LerpArm(ref p, rp, 1, 1f);
            }

            // 2b. Injured: hand on the belly; quick knife slash.
            ApplyLimpAndSlash(ref p, dt, standing, show);
            ApplyGoreLayers(ref p, dt, standing);
            ApplyPushLayer(ref p, dt);
            ApplyLeverLayer(ref p, dt);
            ApplyRodLayers(ref p, dt, standing);
            ApplyArcade(ref p, dt, standing);
            ApplyPoke(ref p, dt, standing);
            ApplyFlashTake(ref p, dt);
            ApplyToyGun(ref p, dt, standing, lookPitch);
            ApplyCarry(ref p, dt, lookPitch); // selected inventory item in the right hand
            ApplyLegThrow(ref p, dt, lookPitch); // cut-off leg: wind-up / throw (LegThrow.cs)
            ApplyPaMic(ref p, dt, standing);
            ApplyEmote(ref p, dt, standing); // K wheel emotes (full body, below every layer after this)
            ApplyShoulderRadio(ref p, dt, standing); // boombox on the shoulder (map radio)
            ApplyExtLayers(ref p, dt, standing, lookPitch); // fire extinguisher in the hands / foam wiped off the face
            ApplyBoardGame(ref p, dt); // lobby chess / tic-tac-toe table (BlockyCharacterActions.BoardGame.cs)

            // 3. Revive loop (full body).
            if (reviveW > 0.001f) p = CharacterPose.Lerp(p, RevivePose(), ActionTimeline.SmoothStep01(reviveW));

            // 3b. Sitting (full body, one of the seated variants anchored to the seat; only the head follows the look).
            if (sitW > 0.001f)
            {
                var sp = SitPoseRoot(lookPitch);
                ApplySitIdle(ref sp); // pin-up poses: breathing, a swinging foot
                ShiftToSitFrom(ref p);
                p = CharacterPose.Lerp(p, sp, ActionTimeline.SmoothStep01(sitW));
            }

            // 3c. Voluntary crawl: stand / crouch -> all fours -> flat on the belly.
            if (proneW > 0.001f && visual == LifeState.Alive && !pathActive)
            {
                var pp = PronePose();
                CharacterPose lp = proneW < 0.5f
                    ? CharacterPose.Lerp(p, AllFoursPose(), ActionTimeline.SmoothStep01(proneW * 2f))
                    : CharacterPose.Lerp(AllFoursPose(), pp, ActionTimeline.SmoothStep01(proneW * 2f - 1f));
                lp.PropR = p.PropR;
                p = lp;
            }

            // 3d. Adrenaline injection (kneeling next to a dead player).
            if (injectW > 0.001f && visual == LifeState.Alive && !pathActive)
            {
                var ip = InjectPose(p, holdTime);
                p = CharacterPose.Lerp(p, ip, ActionTimeline.SmoothStep01(injectW));
                p.PropR = ip.PropR;
            }

            // 3d'. Medkit on a wound (own / someone else's) and being treated.
            ApplyMedkitLayers(ref p, visual);

            // 3e. RC car: putting it down, sitting with the transmitter, standing up.
            if (rcW > 0.001f)
            {
                var rp = RcPose(p);
                if (sitW < 0.01f) p = CharacterPose.Lerp(p, rp, ActionTimeline.SmoothStep01(rcW));
                if (rcW > 0.5f) p.PropR = rp.PropR;
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
            else if (action == ActionKind.Kill)
            {
                var kp = KillPose(p, actionTime);
                float w = 1f - ActionTimeline.SmoothStep01(ActionTimeline.Remap01(actionTime, killSeekerDuration - 0.3f, killSeekerDuration));
                p = CharacterPose.Lerp(p, kp, w);
            }
            else if (action == ActionKind.KillReaction)
            {
                p = KillReactionPose(actionTime, p.PropR);
            }

            // 5. Life state (downed / dead / transitions).
            bool reacting = action == ActionKind.StabbedReaction || action == ActionKind.KillReaction;
            if (!reacting && (pathActive || visual != LifeState.Alive))
            {
                var lp = pathActive ? EvaluatePath(basePose) : PoseFor(visual, basePose);
                lp.PropR = p.PropR;
                if (visual == LifeState.Downed && !pathActive && healW > 0.001f)
                {
                    var hp = SelfHealPose(holdTime);
                    lp = CharacterPose.Lerp(lp, hp, ActionTimeline.SmoothStep01(healW));
                    lp.PropR = hp.PropR;
                }
                p = lp;
            }

            // 4b. Seeker stomping on a toy car (legs).
            if (visual == LifeState.Alive && !pathActive) ApplyStomp(ref p);
            ApplyPhoneSmash(ref p, dt, visual == LifeState.Alive && !pathActive); // 4c. toy phone thrown / stomped
            ApplyRemoteThrow(ref p, dt, visual == LifeState.Alive && !pathActive, lookPitch); // 4d. RC transmitter smashed

            // 5b. Lobby easter egg: fork into the socket (overrides everything).
            if (shockT >= 0f)
            {
                var sp = ShockPose(p, shockT);
                p = sp;
            }

            // 5c. Lobby exit door: shot to pieces (overrides everything).
            if (gunT >= 0f) p = GunnedPose(p, gunT);

            // 5d. Collapse after too many cans in the lobby (overrides everything).
            if (overdoseT >= 0f) p = OverdosePose(p, overdoseT);
            if (stunT >= 0f) p = StunPose(p, stunT); // 5e. knocked out by a thrown leg (Stun.cs)

            // 5e. Carrying somebody in the arms / being carried (BodyCarry*.cs).
            ApplyBodyCarryLayers(ref p, dt);

            // 6. Additive flinches.
            ApplyAdditives(ref p, standing);

            // 7. Cross-fade from the last output.
            if (fadeT < 1f)
            {
                fadeT = Mathf.Min(1f, fadeT + dt / CrossfadeTime);
                if (hasLast) p = CharacterPose.Lerp(fadeFrom, p, ActionTimeline.SmoothStep01(fadeT));
            }

            if (visual == LifeState.Alive) ApplyVending(ref p);
            p.Apply(character);
            lastOutput = p;
            hasLast = true;

            UpdateProps(p, show);
            UpdateRagdollStart();
        }

        HeldItem ResolveShownItem()
        {
            // Actions and lying down show their own item: the hand needs no put-away motion afterwards.
            if (action != ActionKind.None || VisualLifeState != LifeState.Alive) drawnItem = heldItem;
            if (emoteW > 0.05f) return HeldItem.None; // emote: hands free (comes back with the draw motion)
            if (VendingShowsCan) return HeldItem.Can;
            if (PhoneSmashing) return HeldItem.None;
            if (RemoteThrowing) return RemoteShownItem();
            if (PaMicBusy || RadioBusy) return HeldItem.None;
            if (ExtHandsBusy) return HeldItem.None; // fire extinguisher in both hands
            if (vendingOn || ShockHidesItem || GunHidesItem || overdoseT >= 0f || stunT >= 0f) return HeldItem.None;
            var rc = RcShownItem();
            if (rc != HeldItem.None || rcW > 0.5f) return rc;
            var hold = HoldShownItem();
            if (hold != HeldItem.None) return hold;
            if (brickOn && action == ActionKind.None && VisualLifeState == LifeState.Alive) return HeldItem.Brick;
            if (action == ActionKind.Kill && sawKill) return HeldItem.Chainsaw;
            if (action == ActionKind.Kill && glockKill) return HeldItem.Glock;
            if (action == ActionKind.KnifeAttack || action == ActionKind.Kill) return HeldItem.Knife;
            if (action == ActionKind.Reload) return HeldItem.Glock;
            if (heldItem == HeldItem.None && RemoteShownItem() != HeldItem.None) return HeldItem.RcRemote; // "Pilot RC" selected
            return DrawnItem();
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
                else if (action == ActionKind.Kill || action == ActionKind.KillReaction)
                {
                    AdvanceKill(prev);
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

            AdvanceHold(dt);
            AdvanceRc(dt);

            // Revive weight + pump clock.
            bool canRevive = reviving && VisualLifeState == LifeState.Alive && action != ActionKind.StabbedReaction && action != ActionKind.KillReaction;
            reviveW = Mathf.MoveTowards(reviveW, canRevive ? 1f : 0f, dt / 0.3f);
            bool canSit = sitting && VisualLifeState == LifeState.Alive && action == ActionKind.None;
            AdvanceSit(dt, canSit);
            AdvanceSitIdle(dt);
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
            bool prone = proneW > 0.5f && VisualLifeState == LifeState.Alive;
            bool crawling = (downed || prone) && speed > 0.05f && action == ActionKind.None;
            crawlW = Damp(crawlW, crawling ? 1f : 0f, 5f, dt);
            if (downed || prone) crawlPhase += Mathf.Min(speed, 2.5f) * dt / CrawlStride;
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
                // Knocked back and twisted to one side, head snaps, arms jerk up and out, knees give a little.
                float k = standing ? 1f : 0.4f;
                p.Pelvis = Quaternion.Euler(-4f * h * k, 5f * h * hitSide * k, 0f) * p.Pelvis;
                p.Torso = p.Torso * Quaternion.Euler(-17f * h * k, 15f * h * hitSide * k, 8f * h * hitSide * k);
                p.Head = p.Head * Quaternion.Euler(-22f * h * k, -12f * h * hitSide * k, 6f * h * hitSide * k);
                p.ShoulderR = Quaternion.Euler(-12f * h * k, 0f, 20f * h * k) * p.ShoulderR;
                p.ShoulderL = Quaternion.Euler(-12f * h * k, 0f, -20f * h * k) * p.ShoulderL;
                p.ElbowR = p.ElbowR * Quaternion.Euler(-22f * Mathf.Max(0f, h) * k, 0f, 0f);
                p.ElbowL = p.ElbowL * Quaternion.Euler(-22f * Mathf.Max(0f, h) * k, 0f, 0f);
                if (standing) p.PelvisPos += new Vector3(0.012f * h * hitSide, -0.02f * Mathf.Abs(h), -0.04f * h);
            }

            if (Mathf.Abs(recoil) > 0.001f && standing)
                p.Torso = p.Torso * Quaternion.Euler(-2.5f * recoil, 1.5f * recoil, 0f);
            AimRecoil(ref p, standing);
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
            UpdateVendingProp(show);
            UpdateToyGunProp();
            UpdateCarryProp();
            UpdatePaMicProp();
            UpdateExtinguisherProps();
            var saw = props[(int)HeldItem.Chainsaw];
            if (saw != null && show == HeldItem.Chainsaw)
            {
                bool engine = sawStartT >= BlockyProps.ChainsawFireTime;
                saw.SetChainRun(revving || action == ActionKind.Kill ? 1f : engine ? 0.3f : 0f, time);
                saw.SetStarterPull(BlockyProps.ChainsawStarter(sawStartT).y);
                saw.SetSawBlood(sawBlood);
            }

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

            UpdateHoldProps(show);
            UpdateRcProps(show);
            var brick = props[(int)HeldItem.Brick];
            if (brick != null && show == HeldItem.Brick) brick.SetTint(brickColor);

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

