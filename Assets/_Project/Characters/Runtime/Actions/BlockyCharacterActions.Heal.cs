using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>Long hold animations driven by a time value (synced through the hold progress).</summary>
    public enum HoldAnim : byte
    {
        None = 0,
        /// <summary>Kneel, take out the adrenaline syringe, uncap, stab into the dead player's chest, press the plunger.</summary>
        Inject = 1,
        /// <summary>Downed: sit up, open the medkit, wrap a bandage round the belly, tie it off, push up.</summary>
        SelfHeal = 2,
        /// <summary>Wounded (limping), medkit: kneel, kit on the floor, pad on the wound, bandage round the own belly, tie.</summary>
        HealSelf = 3,
        /// <summary>Medkit on a wounded hider: kneel in front of them, pad on the wound, wrap the bandage round their waist.</summary>
        HealOther = 4,
    }

    /// <summary>Timings (seconds from the start of the hold) shared by third person, first person and sound.</summary>
    public static class HealTimeline
    {
        public const float InjectDuration = 4f;
        public const float InjectShow = 0.7f, InjectCapOff = 1.15f, InjectStab = 1.7f, InjectPressStart = 1.78f, InjectPressEnd = 3.05f, InjectPullOut = 3.1f, InjectHide = 3.75f;

        public const float SelfHealDuration = 5f;
        public const float HealSitEnd = 0.7f, HealKitOpen = 1.1f, HealBandageOut = 1.4f, HealWrapStart = 1.7f, HealWrapEnd = 4.2f, HealTieEnd = 4.7f;

        /// <summary>Medkit on a wounded (limping) hider, own or someone else's: hold time and marks.</summary>
        public const float MedkitDuration = 3.5f;
        public const float MedKneel = 0.35f, MedKitDown = 0.7f, MedKitOpen = 0.9f, MedRollOut = 1.1f, MedPad = 1.3f, MedWrapEnd = 2.7f, MedTieEnd = 3.05f;

        /// <summary>0..1 bandage round the belly during a medkit heal.</summary>
        public static float MedBandage(float t) => ActionTimeline.Remap01(t, MedPad, MedWrapEnd);

        public static bool IsMedkit(HoldAnim m) => m == HoldAnim.HealSelf || m == HoldAnim.HealOther;

        /// <summary>Resurrection: violent gasp sitting up from the dead pose, then kneel and stand.</summary>
        public const float GaspDuration = 2.1f;

        public static float Seg(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>0..1 how far the plunger is pressed.</summary>
        public static float InjectPress(float t) => Seg(t, InjectPressStart, InjectPressEnd);

        /// <summary>0..1 how much bandage is round the belly.</summary>
        public static float BandageAmount(float t) => ActionTimeline.Remap01(t, HealWrapStart, HealWrapEnd);
    }

    public partial class BlockyCharacterActions
    {
        HoldAnim holdAnim;
        float holdTime, injectW, healW;
        Vector3 injectTargetLocal = new Vector3(0.02f, 0.16f, 0.78f);
        bool gaspNext;
        float bandageExternal, bandageShown;
        Transform bandageBand;
        HeldProp floorKit;
        static Material _bandageMat;

        /// <summary>Drives a hold animation (mode None fades it out). <paramref name="t"/> = seconds since the hold started.</summary>
        public void SetHoldAnimation(HoldAnim mode, float t, Vector3? worldTarget = null)
        {
            EnsureInit();
            holdAnim = mode;
            if (mode != HoldAnim.None) holdTime = t;
            if (worldTarget.HasValue && mode == HoldAnim.HealOther) SetMedTarget(worldTarget);
            else if (worldTarget.HasValue)
            {
                // Reachable chest point in front of the kneeling helper.
                Vector3 l = transform.InverseTransformPoint(worldTarget.Value);
                l.x = Mathf.Clamp(l.x, -0.25f, 0.25f);
                l.y = Mathf.Clamp(l.y, 0.06f, 0.35f);
                l.z = Mathf.Clamp(l.z, 0.5f, 0.95f);
                injectTargetLocal = l;
            }
        }

        public HoldAnim CurrentHoldAnim => holdAnim;

        /// <summary>The next Dead -> Alive transition is a resurrection (gasp + sit up) instead of the plain get-up.</summary>
        public void PrepareResurrectGasp() => gaspNext = true;

        /// <summary>Bandage round the belly (wounded players keep it on).</summary>
        public void SetBandaged(float amount) => bandageExternal = Mathf.Clamp01(amount);

        // ================================================================== per-frame hooks (called from ApplyLayer)

        void AdvanceHold(float dt)
        {
            bool alive = VisualLifeState == LifeState.Alive && action == ActionKind.None;
            injectW = Mathf.MoveTowards(injectW, holdAnim == HoldAnim.Inject && alive ? 1f : 0f, dt / 0.25f);
            bool downed = VisualLifeState == LifeState.Downed;
            healW = Mathf.MoveTowards(healW, holdAnim == HoldAnim.SelfHeal && downed ? 1f : 0f, dt / (holdAnim == HoldAnim.SelfHeal ? 0.2f : 0.35f));
            float wrap = holdAnim == HoldAnim.SelfHeal && downed ? HealTimeline.BandageAmount(holdTime) : 0f;
            AdvanceMedkit(dt);
            float target = Mathf.Max(bandageExternal, Mathf.Max(wrap, medWrap));
            bandageShown = target > bandageShown ? target : Mathf.MoveTowards(bandageShown, target, dt * 2f);
        }

        HeldItem HoldShownItem()
        {
            if (injectW > 0.5f && holdTime >= HealTimeline.InjectShow && holdTime < HealTimeline.InjectHide) return HeldItem.Syringe;
            if (healW > 0.5f && holdTime >= HealTimeline.HealKitOpen + 0.15f && holdTime < HealTimeline.HealWrapEnd + 0.25f) return HeldItem.Bandage;
            if (medW > 0.5f) return MedkitShownItem();
            return HeldItem.None;
        }

        void UpdateHoldProps(HeldItem show)
        {
            var syr = props[(int)HeldItem.Syringe];
            if (syr != null && show == HeldItem.Syringe)
                syr.SetSyringe(HealTimeline.InjectPress(holdTime), holdTime < HealTimeline.InjectCapOff);

            // Medkit on the floor next to the hip while self-healing.
            bool medKit = medW > 0.3f && holdTime >= HealTimeline.MedKitDown - 0.05f && holdTime < HealTimeline.MedTieEnd + 0.15f;
            bool kit = (healW > 0.3f && holdTime >= HealTimeline.HealSitEnd) || medKit;
            if (kit && floorKit == null)
            {
                floorKit = BlockyProps.Create(HeldItem.Medkit, transform, gameObject.layer);
                floorKit.SetRenderMode(character.IsFirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On, true);
            }
            if (floorKit != null)
            {
                if (floorKit.gameObject.activeSelf != kit) floorKit.gameObject.SetActive(kit);
                if (kit)
                {
                    // Prop origin = handle; the case hangs 0.37 m below it -> lift so it stands on the floor, lying on its side.
                    floorKit.transform.localPosition = medKit ? new Vector3(0.36f, 0.07f, 0.3f + (holdAnim == HoldAnim.HealOther ? medStep : 0f)) : new Vector3(0.42f, 0.07f, -0.3f);
                    floorKit.transform.localRotation = Quaternion.Euler(0f, medKit ? -30f : 20f, 90f);
                }
            }

            // Bandage band round the belly (grows while wrapping, stays while wounded).
            if (bandageBand == null && bandageShown > 0.01f)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "BellyBandage";
                Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(character.Torso, false);
                b.layer = gameObject.layer;
                var r = b.GetComponent<Renderer>();
                if (_bandageMat == null)
                {
                    _bandageMat = new Material(Shader.Find("Standard")) { color = new Color(0.96f, 0.95f, 0.9f), name = "Bandage (runtime)" };
                    _bandageMat.SetFloat("_Glossiness", 0.05f);
                }
                r.sharedMaterial = _bandageMat;
                bandageBand = b.transform;
            }
            if (bandageBand != null)
            {
                bool on = bandageShown > 0.01f;
                if (bandageBand.gameObject.activeSelf != on) bandageBand.gameObject.SetActive(on);
                if (on)
                {
                    float h = Mathf.Lerp(0.015f, 0.17f, bandageShown);
                    bandageBand.localPosition = new Vector3(0f, 3.4f * Px, 0f);
                    bandageBand.localScale = new Vector3(8.5f * Px, h, 4.5f * Px);
                    var r = bandageBand.GetComponent<Renderer>();
                    r.shadowCastingMode = character.IsFirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                    bandageBand.gameObject.layer = gameObject.layer;
                }
            }
        }

        // ================================================================== injection (helper, alive)

        static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, float x) => (1 - x) * (1 - x) * a + 2 * (1 - x) * x * b + x * x * c;

        CharacterPose InjectPose(in CharacterPose current, float t)
        {
            float S(float a, float b) => HealTimeline.Seg(t, a, b);
            float k = S(0f, 0.45f);
            float lean = Mathf.Lerp(6f, 14f, k) + 8f * S(1.45f, 1.7f) * (1f - S(3.1f, 3.5f)) + 3f * HealTimeline.InjectPress(t) * (1f - S(3.1f, 3.4f)) - 4f * S(1.3f, 1.5f) * (1f - S(1.55f, 1.7f));

            // One knee down (right), left foot planted forward.
            var p = CharacterPose.Identity;
            p.PropR = current.PropR;
            p.PelvisPos = Vector3.Lerp(new Vector3(0f, 0.64f, 0f), new Vector3(0.02f, 0.47f, -0.2f), k);
            p.Pelvis = Quaternion.Euler(0f, 6f * k, 0f);
            p.Torso = Quaternion.Euler(lean, -4f * k, 0f);
            Vector3 c = injectTargetLocal;
            // Head looks at the chest.
            p.Head = Quaternion.Euler(Mathf.Lerp(0f, 22f, k) + 12f * S(1.45f, 1.7f), 0f, 0f);
            p.SolveLeg(0, Vector3.Lerp(new Vector3(0.13f, 0f, 0f), new Vector3(0.14f, 0.07f, -0.62f), k), new Vector3(0.1f, -0.4f, 1f));
            p.SolveLeg(1, Vector3.Lerp(new Vector3(-0.13f, 0f, 0f), new Vector3(-0.15f, 0f, 0.16f), k), new Vector3(-0.05f, 0.3f, 1f));

            Vector3 pocket = p.TorsoPoint(new Vector3(0.24f, 0.05f, 0.02f));
            Vector3 front = p.TorsoPoint(new Vector3(0.1f, 0.45f, 0.36f));
            Vector3 raised = p.TorsoPoint(new Vector3(0.14f, 0.92f, 0.2f));
            Vector3 down = new Vector3(0f, -1f, 0.18f).normalized;
            float sc = BlockyProps.SyringeScale;
            Vector3 stabHand = c - down * (BlockyProps.SyringeTipLocal.z * sc - 0.015f);
            Vector3 pulled = c - down * 0.45f;

            Vector3 hand;
            Vector3 dir;
            if (t < 0.95f)
            {
                hand = t < 0.7f ? Vector3.Lerp(current.HandPos(0), pocket, S(0.45f, 0.7f)) : Vector3.Lerp(pocket, front, S(0.7f, 0.95f));
                dir = new Vector3(-0.15f, -0.1f, 1f);
            }
            else if (t < 1.3f)
            {
                hand = front + new Vector3(0f, 0.01f * Mathf.Sin(t * 30f) * S(1.0f, 1.15f), 0f);
                dir = new Vector3(-0.2f, 0.05f, 1f);
            }
            else if (t < HealTimeline.InjectStab)
            {
                float r = S(1.3f, 1.56f);
                float st = Mathf.Clamp01((t - 1.58f) / (HealTimeline.InjectStab - 1.58f));
                hand = t < 1.58f ? Vector3.Lerp(front, raised, r) : Vector3.Lerp(raised, stabHand, st * st);
                dir = Vector3.Slerp(new Vector3(-0.2f, 0.05f, 1f), down, r);
            }
            else if (t < HealTimeline.InjectPullOut)
            {
                float press = HealTimeline.InjectPress(t);
                hand = stabHand + down * (0.012f * press); // pushes in a touch while pressing
                dir = down;
            }
            else
            {
                float po = S(HealTimeline.InjectPullOut, 3.35f);
                float lower = S(3.45f, 3.95f);
                hand = Vector3.Lerp(Vector3.Lerp(stabHand, pulled, po), p.TorsoPoint(new Vector3(0.22f, 0.15f, 0.2f)), lower);
                dir = Vector3.Slerp(down, new Vector3(0f, -0.4f, 1f), lower);
            }
            Vector3 elbowR = TorsoDir(p, 1f, -0.6f, -0.6f);
            p.SolveArm(0, hand, elbowR);
            var rot = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.9f ? Vector3.forward : Vector3.up);
            p.PropR = Quaternion.Inverse(p.ForearmRoot(0)) * rot;

            // Left hand: rests on the knee, pulls the cap, then the palm presses the plunger.
            Vector3 knee = p.TorsoPoint(new Vector3(-0.16f, 0.12f, 0.3f));
            Vector3 handNow = p.HandPos(0);
            Vector3 tip = handNow + rot * (BlockyProps.SyringeTipLocal * sc);
            Vector3 plungerTop = handNow + rot * ((BlockyProps.SyringePlungerLocal + new Vector3(0f, 0f, BlockyProps.SyringePlungerTravel * HealTimeline.InjectPress(t))) * sc) - rot * new Vector3(0f, 0f, 0.05f);
            Vector3 l;
            if (t < 0.95f) l = Vector3.Lerp(current.HandPos(1), knee, k);
            else if (t < 1.35f)
            {
                float grab = S(0.95f, 1.1f), away = S(HealTimeline.InjectCapOff, 1.35f);
                l = Vector3.Lerp(Vector3.Lerp(knee, tip, grab), tip + new Vector3(-0.25f, -0.05f, 0.05f), away);
            }
            else if (t < 1.62f) l = Vector3.Lerp(tip + new Vector3(-0.25f, -0.05f, 0.05f), knee, S(1.35f, 1.6f));
            else if (t < HealTimeline.InjectPullOut) l = Vector3.Lerp(knee, plungerTop, S(1.62f, HealTimeline.InjectPressStart));
            else l = Vector3.Lerp(plungerTop, knee, S(HealTimeline.InjectPullOut, 3.4f));
            p.SolveArm(1, l, TorsoDir(p, -1f, -0.4f, -0.6f));
            return p;
        }

        // ================================================================== self heal (downed)

        /// <summary>Sitting on the floor (legs forward towards +Z, where the head pointed while lying), looking at the belly.</summary>
        static CharacterPose SitFloorPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.15f, -0.52f);
            p.Torso = Quaternion.Euler(14f, 0f, 0f);
            p.Head = Quaternion.Euler(32f, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.14f, 0.07f, 0.12f), new Vector3(0.2f, 1f, 0.2f));
            p.SolveLeg(1, new Vector3(-0.14f, 0.07f, 0.14f), new Vector3(-0.2f, 1f, 0.2f));
            return p;
        }

        CharacterPose SelfHealPose(float t)
        {
            float S(float a, float b) => HealTimeline.Seg(t, a, b);
            var sit = SitFloorPose();
            float wrapT = Mathf.Clamp01((t - HealTimeline.HealWrapStart) / (HealTimeline.HealWrapEnd - HealTimeline.HealWrapStart));
            bool wrapping = t >= HealTimeline.HealWrapStart && t < HealTimeline.HealWrapEnd;
            // 3 passes of the roll round the belly: right hand sweeps from the left side over the front to the right side
            // and behind; the left hand takes the roll at the back-left and hands it over in front.
            float cyc = wrapT * 3f;
            float ph = cyc - Mathf.Floor(cyc);
            float sway = wrapping ? Mathf.Sin(ph * Mathf.PI * 2f) : 0f;
            sit.Torso = Quaternion.Euler(14f + 4f * Mathf.Abs(sway), 8f * sway, 3f * sway);
            sit.Head = Quaternion.Euler(34f, -10f * sway, 0f);

            Vector3 Belly(float ang, float r = 1f) => sit.TorsoPoint(new Vector3(0.21f * Mathf.Sin(ang) * r, 0.2f + 0.015f * Mathf.Sin(ang * 3f), 0.15f * Mathf.Cos(ang) * r + 0.03f));
            Vector3 kit = new Vector3(0.42f, 0.2f, -0.3f);
            Vector3 floorR = new Vector3(0.3f, 0.06f, -0.55f), floorL = new Vector3(-0.3f, 0.06f, -0.55f);
            Vector3 thighL = sit.TorsoPoint(new Vector3(-0.14f, -0.02f, 0.25f));

            Vector3 r, l;
            if (t < HealTimeline.HealSitEnd)
            {
                // Push up from the floor into the sit.
                r = floorR;
                l = floorL;
            }
            else if (t < HealTimeline.HealKitOpen)
            {
                r = Vector3.Lerp(floorR, kit, S(HealTimeline.HealSitEnd, 0.95f));
                l = Vector3.Lerp(floorL, thighL, S(HealTimeline.HealSitEnd, 1.0f));
            }
            else if (t < HealTimeline.HealBandageOut)
            {
                // Flip the lid, grab the roll.
                float lid = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - HealTimeline.HealKitOpen) / 0.3f));
                r = kit + new Vector3(0f, 0.12f * lid, 0.03f * lid);
                l = thighL;
            }
            else if (t < HealTimeline.HealWrapStart)
            {
                r = Vector3.Lerp(kit, Belly(0.6f), S(HealTimeline.HealBandageOut, HealTimeline.HealWrapStart));
                l = Vector3.Lerp(thighL, Belly(-0.4f), S(HealTimeline.HealBandageOut, HealTimeline.HealWrapStart));
            }
            else if (wrapping)
            {
                // Right hand: -0.9 rad (left front) -> +2.0 rad (right, round to the back) then returns empty.
                float a = ph < 0.6f ? Mathf.Lerp(-0.9f, 2.0f, ActionTimeline.SmoothStep01(ph / 0.6f)) : Mathf.Lerp(2.0f, -0.9f, ActionTimeline.SmoothStep01((ph - 0.6f) / 0.4f));
                r = Belly(a, ph < 0.6f ? 1f : 1.45f);
                // Left hand holds the end in front, reaches round the left side to take the roll when the right one returns.
                float b = ph < 0.6f ? -0.5f : Mathf.Lerp(-0.5f, -2.0f, Mathf.Sin(Mathf.PI * (ph - 0.6f) / 0.4f));
                l = Belly(b);
            }
            else if (t < HealTimeline.HealTieEnd)
            {
                // Tie: hands meet in front, two sharp pulls apart.
                float pull = Mathf.Abs(Mathf.Sin((t - HealTimeline.HealWrapEnd) / (HealTimeline.HealTieEnd - HealTimeline.HealWrapEnd) * Mathf.PI * 2f));
                r = sit.TorsoPoint(new Vector3(0.03f + 0.17f * pull, 0.21f, 0.2f + 0.04f * pull));
                l = sit.TorsoPoint(new Vector3(-0.03f - 0.17f * pull, 0.21f, 0.2f + 0.04f * pull));
            }
            else
            {
                // Hands back down to the floor to push up.
                float d = S(HealTimeline.HealTieEnd, 4.95f);
                r = Vector3.Lerp(sit.TorsoPoint(new Vector3(0.05f, 0.21f, 0.2f)), floorR, d);
                l = Vector3.Lerp(sit.TorsoPoint(new Vector3(-0.05f, 0.21f, 0.2f)), floorL, d);
                sit.Torso = Quaternion.Euler(Mathf.Lerp(14f, 2f, d), 0f, 0f);
            }
            sit.SolveArm(0, r, TorsoDir(sit, 1f, -0.2f, -0.6f));
            sit.SolveArm(1, l, TorsoDir(sit, -1f, -0.2f, -0.6f));
            sit.PropR = Quaternion.Inverse(sit.ForearmRoot(0)) * (sit.TorsoRoot * Quaternion.Euler(0f, 90f, 0f));

            // Roll from lying on the belly into the sit at the start.
            float rise = S(0f, HealTimeline.HealSitEnd);
            if (rise < 1f)
            {
                var mid = AllFoursPose();
                var from = DownedPose();
                return rise < 0.5f
                    ? CharacterPose.Lerp(from, mid, ActionTimeline.SmoothStep01(rise * 2f))
                    : CharacterPose.Lerp(mid, sit, ActionTimeline.SmoothStep01(rise * 2f - 1f));
            }
            return sit;
        }

        // ================================================================== resurrection gasp (Dead -> Alive)

        /// <summary>Sitting up with a jolt: torso up, head thrown back, arms flung out (gasp).</summary>
        static CharacterPose GaspPose()
        {
            var p = SitFloorPose();
            p.PelvisPos = new Vector3(0f, 0.15f, -0.6f);
            p.Torso = Quaternion.Euler(-14f, 0f, 0f);
            p.Head = Quaternion.Euler(-30f, 0f, 0f);
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.45f, 0.3f, -0.05f)), TorsoDir(p, 1f, -0.5f, -0.3f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.42f, 0.25f, 0.02f)), TorsoDir(p, -1f, -0.5f, -0.3f));
            return p;
        }

        /// <summary>Catching breath sitting up: leaning forward, one hand on the chest, the other on the floor.</summary>
        CharacterPose BreathPose()
        {
            var p = SitFloorPose();
            p.PelvisPos = new Vector3(0f, 0.15f, -0.6f);
            float br = Mathf.Sin(time * 9f);
            p.Torso = Quaternion.Euler(24f + 5f * br, 0f, 0f);
            p.Head = Quaternion.Euler(18f - 6f * br, 0f, 0f);
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.05f, 0.45f, 0.2f)), TorsoDir(p, 1f, -0.6f, -0.5f));
            p.SolveArm(1, new Vector3(-0.32f, 0.06f, -0.45f), TorsoDir(p, -1f, -0.3f, -0.5f));
            return p;
        }

        /// <summary>Called by StartLifePath: custom keys for resurrection / getting up after self-heal. True when handled.</summary>
        bool StartSpecialPath(LifeState from, LifeState to)
        {
            if (from == LifeState.Dead && to == LifeState.Alive && gaspNext)
            {
                gaspNext = false;
                Key(PoseId.Gasp, 0.22f, Ease.Out);
                Key(PoseId.Breath, 0.95f, Ease.Smooth);
                Key(PoseId.Kneel, 1.5f, Ease.Smooth);
                Key(PoseId.Base, HealTimeline.GaspDuration, Ease.Smooth);
                return true;
            }
            if (from == LifeState.Downed && to == LifeState.Alive && healW > 0.3f)
            {
                // Already sitting after the bandage: straight to the knees and up.
                Key(PoseId.Kneel, 0.45f, Ease.Smooth);
                Key(PoseId.Base, 0.95f, Ease.Smooth);
                return true;
            }
            return false;
        }
    }
}
