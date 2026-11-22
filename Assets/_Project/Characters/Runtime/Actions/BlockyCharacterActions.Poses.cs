using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Pose functions of the action layer (pure math in character-root space).</summary>
    public partial class BlockyCharacterActions
    {
        const float CrawlStride = 0.75f;
        const float ProneZ = -0.6f;
        const float ProneY = 0.13f;

        static readonly Quaternion Prone = Quaternion.Euler(90f, 0f, 0f);
        static readonly Quaternion Supine = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        static readonly Quaternion OnSide = Quaternion.LookRotation(Vector3.right, Vector3.forward);

        // ================================================================== life paths

        enum PoseId
        {
            Snapshot,
            Base,
            Kneel,
            AllFours,
            Side,
            Downed,
            Dead,
            Gasp,
            Breath,
            Buckle,
        }

        enum Ease
        {
            Smooth,
            In,
            Out
        }

        struct PathKey
        {
            public PoseId Pose;
            public float Time;
            public Ease Ease;

            public PathKey(PoseId pose, float time, Ease ease)
            {
                Pose = pose;
                Time = time;
                Ease = ease;
            }
        }

        readonly PathKey[] keys = new PathKey[6];
        int keyCount;
        bool pathActive;
        float pathTime;
        CharacterPose pathFrom;

        void StartLifePath(LifeState from, LifeState to)
        {
            pathFrom = hasLast ? lastOutput : CharacterPose.Capture(character);
            keyCount = 0;
            Key(PoseId.Snapshot, 0f, Ease.Smooth);
            if (StartSpecialPath(from, to))
            {
                pathTime = 0f;
                pathActive = true;
                fadeT = 1f;
                return;
            }
            if (from == LifeState.Alive && to == LifeState.Downed)
            {
                // Doubles over clutching the wound, knees give, onto the hands, flat on the belly.
                Key(PoseId.Buckle, 0.32f, Ease.Out);
                Key(PoseId.Kneel, 0.62f, Ease.In);
                Key(PoseId.AllFours, 0.92f, Ease.In);
                Key(PoseId.Downed, 1.3f, Ease.Out);
            }
            else if (from == LifeState.Alive && to == LifeState.Dead)
            {
                Key(PoseId.Buckle, 0.26f, Ease.Out);
                Key(PoseId.Kneel, 0.56f, Ease.In);
                Key(PoseId.Side, 0.95f, Ease.In);
                Key(PoseId.Dead, 1.35f, Ease.Out);
            }
            else if (from == LifeState.Downed && to == LifeState.Dead)
            {
                Key(PoseId.Side, 0.5f, Ease.Smooth);
                Key(PoseId.Dead, 1.0f, Ease.Smooth);
            }
            else if (from == LifeState.Downed && to == LifeState.Alive)
            {
                Key(PoseId.AllFours, 0.5f, Ease.Smooth);
                Key(PoseId.Kneel, 0.95f, Ease.Smooth);
                Key(PoseId.Base, 1.45f, Ease.Smooth);
            }
            else if (from == LifeState.Dead && to == LifeState.Alive)
            {
                Key(PoseId.Side, 0.35f, Ease.Smooth);
                Key(PoseId.AllFours, 0.75f, Ease.Smooth);
                Key(PoseId.Kneel, 1.05f, Ease.Smooth);
                Key(PoseId.Base, 1.45f, Ease.Smooth);
            }
            else // Dead -> Downed
            {
                Key(PoseId.Side, 0.4f, Ease.Smooth);
                Key(PoseId.Downed, 0.85f, Ease.Smooth);
            }

            pathTime = 0f;
            pathActive = true;
            fadeT = 1f;
        }

        void Key(PoseId id, float t, Ease e) => keys[keyCount++] = new PathKey(id, t, e);

        CharacterPose EvaluatePath(in CharacterPose basePose)
        {
            int i = 0;
            while (i < keyCount - 2 && pathTime >= keys[i + 1].Time) i++;
            var a = keys[i];
            var b = keys[i + 1];
            float x = ActionTimeline.Remap01(pathTime, a.Time, b.Time);
            x = b.Ease == Ease.In ? ActionTimeline.EaseIn(x) : b.Ease == Ease.Out ? ActionTimeline.EaseOut(x) : ActionTimeline.SmoothStep01(x);
            return CharacterPose.Lerp(EvalPose(a.Pose, basePose), EvalPose(b.Pose, basePose), x);
        }

        CharacterPose EvalPose(PoseId id, in CharacterPose basePose)
        {
            switch (id)
            {
                case PoseId.Snapshot: return pathFrom;
                case PoseId.Base: return basePose;
                case PoseId.Kneel: return KneelPose(0f, -0.1f);
                case PoseId.AllFours: return AllFoursPose();
                case PoseId.Side: return SidePose();
                case PoseId.Downed: return DownedPose();
                case PoseId.Gasp: return GaspPose();
                case PoseId.Breath: return BreathPose();
                case PoseId.Buckle: return BucklePose();
                default: return DeadPose();
            }
        }

        CharacterPose PoseFor(LifeState state, in CharacterPose basePose)
        {
            if (state == LifeState.Downed) return DownedPose();
            if (state == LifeState.Dead) return DeadPose();
            return basePose;
        }

        // ================================================================== static poses

        static Vector3 TorsoDir(in CharacterPose p, float x, float y, float z) => p.TorsoRoot * new Vector3(x, y, z);

        /// <summary>Both knees on the floor, hands on the belly. <paramref name="curl"/> adds a forward slump.</summary>
        static CharacterPose KneelPose(float curl, float z)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.45f, z);
            p.Torso = Quaternion.Euler(24f + 16f * curl, 0f, 0f);
            p.Head = Quaternion.Euler(14f, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.12f, 0.11f, z - 0.34f), new Vector3(0f, -0.3f, 1f));
            p.SolveLeg(1, new Vector3(-0.12f, 0.11f, z - 0.34f), new Vector3(0f, -0.3f, 1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.07f, 0.24f, 0.24f)), TorsoDir(p, 1f, -1f, -0.6f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.08f, 0.27f, 0.24f)), TorsoDir(p, -1f, -1f, -0.6f));
            return p;
        }

        /// <summary>Doubled over on bent knees, both hands pressed on the belly (losing the strength to stand).</summary>
        static CharacterPose BucklePose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.02f, 0.55f, -0.07f);
            p.Pelvis = Quaternion.Euler(6f, 0f, 3f);
            p.Torso = Quaternion.Euler(30f, -6f, 5f);
            p.Head = Quaternion.Euler(12f, 8f, 0f);
            p.SolveLeg(0, new Vector3(0.13f, 0f, 0.06f), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.14f, 0f, -0.08f), new Vector3(-0.1f, 0f, 1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.06f, 0.25f, 0.235f)), TorsoDir(p, 1f, -1f, -0.5f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.08f, 0.28f, 0.235f)), TorsoDir(p, -1f, -1f, -0.5f));
            return p;
        }

        static CharacterPose AllFoursPose()
        {
            var p = CharacterPose.Identity;
            const float z = ProneZ + 0.1f;
            p.PelvisPos = new Vector3(0f, 0.45f, z);
            p.Torso = Quaternion.Euler(78f, 0f, 0f);
            p.Head = Quaternion.Euler(-45f, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.12f, 0.11f, z - 0.34f), new Vector3(0f, -0.3f, 1f));
            p.SolveLeg(1, new Vector3(-0.12f, 0.11f, z - 0.34f), new Vector3(0f, -0.3f, 1f));
            p.SolveArm(0, new Vector3(0.3f, 0.11f, z + 0.6f), new Vector3(0.6f, 0f, -1f));
            p.SolveArm(1, new Vector3(-0.3f, 0.11f, z + 0.6f), new Vector3(-0.6f, 0f, -1f));
            return p;
        }

        static CharacterPose SidePose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.24f, ProneZ);
            p.Pelvis = OnSide;
            p.Torso = Quaternion.Euler(12f, 0f, 0f);
            p.Head = Quaternion.Euler(14f, 0f, 0f);
            p.ShoulderR = Quaternion.Euler(-70f, 0f, 10f);
            p.ElbowR = Quaternion.Euler(-50f, 0f, 0f);
            p.ShoulderL = Quaternion.Euler(-80f, 0f, -5f);
            p.ElbowL = Quaternion.Euler(-40f, 0f, 0f);
            p.HipR = Quaternion.Euler(-25f, 0f, 0f);
            p.KneeR = Quaternion.Euler(35f, 0f, 0f);
            p.HipL = Quaternion.Euler(-15f, 0f, 0f);
            p.KneeL = Quaternion.Euler(20f, 0f, 0f);
            return p;
        }

        /// <summary>On the back, head towards +Z, arms splayed, one knee fallen out, head turned aside.</summary>
        static CharacterPose DeadPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, ProneY, ProneZ);
            p.Pelvis = Supine;
            p.Torso = Quaternion.Euler(-3f, 0f, 4f);
            p.Head = Quaternion.Euler(-8f, 55f, 0f);
            Vector3 r = p.TorsoPoint(new Vector3(0.62f, 0.42f, 0f));
            Vector3 l = p.TorsoPoint(new Vector3(-0.52f, 0.05f, 0f));
            r.y = l.y = 0.11f;
            p.SolveArm(0, r, Vector3.down + p.TorsoRoot * Vector3.right);
            p.SolveArm(1, l, Vector3.down + p.TorsoRoot * Vector3.left);
            Vector3 hipR = p.HipPos(0), hipL = p.HipPos(1);
            Vector3 sideR = p.Pelvis * Vector3.right;
            p.SolveLeg(0, hipR + sideR * 0.08f + Vector3.back * 0.67f, Vector3.up);
            p.SolveLeg(1, hipL - sideR * 0.18f + Vector3.back * 0.5f, -sideR + Vector3.up * 0.4f);
            return p;
        }

        // ================================================================== downed (crawl)

        /// <summary>Healthy army crawl: like the downed pose but alert (head up), no twitches, stronger leg push.</summary>
        CharacterPose PronePose() => DownedPose(true);

        CharacterPose DownedPose() => DownedPose(false);

        CharacterPose DownedPose(bool healthy)
        {
            float cw = crawlW;
            float ph = crawlPhase * Mathf.PI * 2f;
            float sway = Mathf.Sin(ph) * cw;
            float br = Mathf.Sin(breathPhase);

            // Pained twitch: sharp onset, quick decay.
            float tw = twitchTime < 0.08f ? twitchTime / 0.08f : Mathf.Exp(-(twitchTime - 0.08f) * 7f);
            if (twitchTime > 1.2f || healthy) tw = 0f;
            tw *= 1f - 0.6f * cw;

            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.015f * sway, ProneY + 0.012f * Mathf.Abs(Mathf.Sin(ph)) * cw, ProneZ);
            p.Pelvis = Quaternion.Euler(0f, 3f * sway, 0f) * Prone * Quaternion.Euler(0f, 7f * sway, 0f);
            p.Torso = Quaternion.Euler(-16f + 2.5f * br + 9f * tw - (healthy ? 6f : 0f), 6f * sway, 0f);
            p.Head = Quaternion.Euler(-48f + 3f * br + 22f * tw - (healthy ? 14f : 0f), -8f * sway, 0f);

            // Arms: idle = right forearm flat in front, left hand pressed to the belly side.
            //       crawl = hands reach forward, plant and pull back alternately.
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? 1f : -1f;
                float q = crawlPhase + (arm == 0 ? 0f : 0.5f);
                q -= Mathf.Floor(q);
                const float duty = 0.55f, zF = ProneZ + 0.97f, zB = ProneZ + 0.57f;
                Vector3 crawl;
                if (q < duty) crawl = new Vector3(side * 0.3f, 0.1f, Mathf.Lerp(zF, zB, q / duty));
                else
                {
                    float s = (q - duty) / (1f - duty);
                    crawl = new Vector3(side * 0.33f, 0.1f + 0.12f * Mathf.Sin(Mathf.PI * s), Mathf.Lerp(zB, zF, ActionTimeline.SmoothStep01(s)));
                }

                Vector3 idle = arm == 0 ? new Vector3(0.24f, 0.1f, ProneZ + 0.85f) : new Vector3(-0.22f, 0.11f, ProneZ + 0.23f + 0.01f * br);
                if (healthy) idle = new Vector3(side * 0.26f, 0.1f, ProneZ + 0.82f); // both forearms flat in front
                float w = arm == 0 || healthy ? cw : cw * 0.85f;
                Vector3 target = Vector3.Lerp(idle, crawl, w);
                if (arm == 1) target += new Vector3(0.02f, 0f, 0.02f) * tw;
                Vector3 pole = arm == 0 ? new Vector3(1f, 1.2f, -0.4f) : Vector3.Lerp(new Vector3(-1f, 0.6f, -0.2f), new Vector3(-1f, 1.2f, -0.4f), w);
                p.SolveArm(arm, target, pole);
            }

            // Legs drag; one pushes weakly (frog kick) in time with the opposite arm. Twitch pulls the right knee up.
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? 1f : -1f;
                float q = crawlPhase + (leg == 0 ? 0.5f : 0f);
                float push = Mathf.Max(0f, Mathf.Sin(q * Mathf.PI * 2f)) * cw * (healthy ? 1.3f : 0.7f);
                float idleBend = leg == 1 && !healthy ? 0.06f : 0f;
                float pull = push * 0.2f + idleBend + (leg == 0 ? 0.25f * tw : 0f);
                Vector3 hip = p.HipPos(leg);
                Vector3 sole = new Vector3(hip.x + side * (0.04f + 0.12f * (push + tw * 0.3f)), 0.12f, hip.z - 0.675f + pull);
                p.SolveLeg(leg, sole, new Vector3(side * 0.6f, -1f, 0f));
            }

            return p;
        }

        // ================================================================== item holds

        static Quaternion DefaultPropLocal(HeldItem item)
        {
            switch (item)
            {
                case HeldItem.Glock: return Quaternion.LookRotation(Vector3.down, Vector3.forward);
                case HeldItem.Knife:
                case HeldItem.Fork: return Quaternion.LookRotation(new Vector3(0f, -0.45f, 1f), Vector3.up);
                default: return Quaternion.identity;
            }
        }

        /// <summary>Glock hold frame (prop origin position + rotation, root space): relaxed low aim, raised after a shot.</summary>
        void GlockFrame(in CharacterPose p, float lookPitch, out Vector3 pos, out Quaternion rot)
        {
            float a = ActionTimeline.SmoothStep01(aimRaise);
            Vector3 pivot = new Vector3(0f, 0.52f, 0f);
            Vector3 offset = Vector3.Lerp(new Vector3(0.09f, -0.16f, 0.37f), new Vector3(0.07f, 0.0f, 0.44f), a);
            float pitchDown = Mathf.Lerp(24f, 0f, a);
            float look = Mathf.Clamp(lookPitch, -60f, 60f) * Mathf.Lerp(0.4f, 1f, a);
            Quaternion rl = Quaternion.Euler(-look, 0f, 0f);
            pos = p.TorsoPoint(pivot + rl * offset);
            rot = p.TorsoRoot * rl * Quaternion.Euler(pitchDown, -3f, 0f);
            if (glockAimW > 0.001f)
            {
                GlockAimFrame(p, lookPitch, out var aPos, out var aRot);
                float w = ActionTimeline.SmoothStep01(glockAimW);
                pos = Vector3.Lerp(pos, aPos, w);
                rot = Quaternion.Slerp(rot, aRot, w);
            }
            float r = recoil;
            pos += p.TorsoRoot * new Vector3(0f, 0.035f * r, -0.08f * r);
            rot = rot * Quaternion.Euler(-24f * r, 0f, 0f);
        }

        CharacterPose ItemHoldPose(in CharacterPose basePose, HeldItem item, float lookPitch)
        {
            var p = basePose;
            switch (item)
            {
                case HeldItem.Glock:
                {
                    AimLean(ref p);
                    GlockFrame(p, lookPitch, out var gPos, out var gRot);
                    SolveGlockHands(ref p, gPos, gRot, ActionTimeline.SupportGripLocal, 0f, Vector3.zero);
                    break;
                }
                case HeldItem.Knife:
                case HeldItem.Fork:
                case HeldItem.Brick:
                    p.ShoulderR = basePose.ShoulderR * Quaternion.Euler(-12f, 0f, 0f);
                    p.ElbowR = basePose.ElbowR * Quaternion.Euler(-38f, 0f, 0f);
                    p.PropR = DefaultPropLocal(item);
                    break;
                case HeldItem.Chainsaw:
                {
                    // Two-handed at the hip, bar forward; throttle raises it and shakes everything.
                    float rv = revving ? 1f : 0f;
                    Vector3 jit = new Vector3(Mathf.Sin(time * 83f), Mathf.Sin(time * 67f), 0f) * (0.004f + 0.01f * rv);
                    Vector3 fist = p.TorsoPoint(new Vector3(0.17f, 0.17f + 0.08f * rv, 0.2f + 0.06f * rv)) + jit;
                    float look = Mathf.Clamp(lookPitch, -40f, 40f) * 0.5f;
                    Quaternion rot = p.TorsoRoot * Quaternion.Euler(-look - 8f - 14f * rv, -6f, 0f);
                    // Pull-start: the saw is held out a little lower, the left hand grabs the starter grip and yanks.
                    var st = BlockyProps.ChainsawStarter(sawStartT);
                    if (st.x > 0.001f)
                    {
                        fist = Vector3.Lerp(fist, p.TorsoPoint(new Vector3(0.12f, 0.02f, 0.3f)), st.x);
                        rot = Quaternion.Slerp(rot, p.TorsoRoot * Quaternion.Euler(16f, -18f, -10f), st.x);
                    }
                    SolveSawHands(ref p, fist, rot);
                    if (st.x > 0.001f)
                    {
                        Vector3 grip = p.HandPos(0) + rot * BlockyProps.ChainsawLeftGripLocal;
                        Vector3 starter = p.HandPos(0) + rot * (BlockyProps.ChainsawStarterLocal +
                                          BlockyProps.ChainsawPullDir * (BlockyProps.ChainsawPullLength * st.y));
                        p.SolveArm(1, Vector3.Lerp(grip, starter, st.x), TorsoDir(p, -0.2f, -1f, -0.6f));
                    }
                    break;
                }
                case HeldItem.RcRemote:
                    RemoteHoldHands(ref p, lookPitch, 0f); // transmitter in both hands (BlockyCharacterActions.RcRemote)
                    break;
                case HeldItem.Medkit:
                    p.ShoulderR = Quaternion.Euler(0f, 0f, 7f) * basePose.ShoulderR;
                    p.ElbowR = basePose.ElbowR * Quaternion.Euler(-6f, 0f, 0f);
                    // Box hangs level from its handle.
                    p.PropR = Quaternion.Inverse(p.ForearmRoot(0)) * Quaternion.Euler(0f, 0f, 0f);
                    break;
            }

            return p;
        }

        /// <summary>Right fist to the gun frame, gun oriented to it, left fist to a gun-local point (or the belt).</summary>
        static void SolveGlockHands(ref CharacterPose p, Vector3 gPos, Quaternion gRot, Vector3 leftGunLocal, float leftAtBelt, Vector3 belt)
        {
            p.SolveArm(0, gPos, TorsoDir(p, 1f, -1f, -0.4f));
            p.PropR = Quaternion.Inverse(p.ForearmRoot(0)) * gRot;
            Vector3 gun = p.HandPos(0);
            Vector3 left = Vector3.Lerp(gun + gRot * leftGunLocal, belt, leftAtBelt);
            p.SolveArm(1, left, TorsoDir(p, -1f, -1f, -0.3f));
        }

        CharacterPose ReloadPose(in CharacterPose holdPose, float lookPitch, float t)
        {
            var p = holdPose;
            var s = ActionTimeline.SampleReload(t);
            GlockFrame(p, lookPitch, out var hPos, out var hRot);
            Vector3 rPos = p.TorsoPoint(new Vector3(0.06f, 0.42f, 0.33f));
            Quaternion rRot = p.TorsoRoot * Quaternion.Euler(-22f, -14f, -38f);
            Vector3 gPos = Vector3.Lerp(hPos, rPos, s.GunTilt) + p.TorsoRoot * new Vector3(0f, 0.025f * s.Jolt, 0f);
            Quaternion gRot = Quaternion.Slerp(hRot, rRot, s.GunTilt) * Quaternion.Euler(-6f * s.Jolt, 0f, 0f);
            Vector3 belt = p.TorsoPoint(new Vector3(-0.27f, 0.06f, 0.12f));
            SolveGlockHands(ref p, gPos, gRot, s.LeftGunLocal, s.LeftAtBelt, belt);
            return p;
        }

        // ================================================================== revive

        CharacterPose RevivePose()
        {
            float c = Mathf.Repeat(reviveTime * 1.7f, 1f);
            float pump = c < 0.35f ? ActionTimeline.SmoothStep01(c / 0.35f) : 1f - ActionTimeline.SmoothStep01((c - 0.35f) / 0.65f);
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.41f, -0.1f);
            p.Torso = Quaternion.Euler(52f + 6f * pump, 0f, 0f);
            p.Head = Quaternion.Euler(-8f, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.13f, 0.1f, -0.42f), new Vector3(0.1f, -0.3f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, 0.1f, -0.42f), new Vector3(-0.1f, -0.3f, 1f));
            float y = 0.38f - 0.05f * pump;
            p.SolveArm(0, new Vector3(0.05f, y, 0.42f), new Vector3(1f, 0.2f, -0.3f));
            p.SolveArm(1, new Vector3(-0.07f, y + 0.03f, 0.40f), new Vector3(-1f, 0.2f, -0.3f));
            return p;
        }

        // ================================================================== knife attack (seeker)

        static readonly Vector3 BellyLocal = new Vector3(0f, 3.6f * Px, 2.3f * Px);
        static readonly Vector3 ShoulderGrabLocal = new Vector3(0.30f, 0.66f, 0.05f);

        /// <summary>Root-space victim belly and grab point (real victim if known, else the scripted victim).</summary>
        void VictimPoints(float t, out Vector3 belly, out Vector3 grab, out Vector3 victimUp)
        {
            if (knifeVictim != null && knifeVictim.IsBuilt)
            {
                belly = transform.InverseTransformPoint(knifeVictim.Torso.TransformPoint(BellyLocal));
                grab = transform.InverseTransformPoint(knifeVictim.Torso.TransformPoint(ShoulderGrabLocal));
                victimUp = transform.InverseTransformDirection(knifeVictim.Torso.up);
                return;
            }

            var vp = StabbedStandPose(Mathf.Min(t, 2.4f));
            Quaternion vr = Quaternion.Euler(0f, 180f, 0f);
            Vector3 vo = new Vector3(0f, 0f, KnifeAttackDistance);
            belly = vo + vr * vp.TorsoPoint(BellyLocal);
            grab = vo + vr * vp.TorsoPoint(ShoulderGrabLocal);
            victimUp = vr * (vp.TorsoRoot * Vector3.up);
        }

        CharacterPose KnifeAttackPose(in CharacterPose current, float t)
        {
            float e = ActionTimeline.SmoothStep01(t / 0.25f);
            float s = ActionTimeline.StabCurve(t);
            float cock = Mathf.Max(0f, -s), thrust = Mathf.Max(0f, s);
            float release = ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, 2.08f, 2.3f));

            var p = CharacterPose.Identity;
            p.PropR = current.PropR;
            p.PelvisPos = new Vector3(0.02f * e, 0.64f - 0.03f * e - 0.015f * thrust, -0.02f * e + 0.035f * thrust - 0.02f * cock - 0.05f * release);
            p.Pelvis = Quaternion.Euler(3f * e, -6f * e + 5f * cock - 5f * thrust, 0f);
            float twist = 13f * cock - 13f * thrust;
            p.Torso = Quaternion.Euler(4f * e + 6f * thrust - 2f * cock - 6f * release, twist, 7f * e - 3f * thrust);
            p.Head = Quaternion.Euler(6f * e - 4f * thrust, -twist * 0.7f + 6f * e, 16f * e);
            p.SolveLeg(0, new Vector3(0.15f, 0f, -0.15f * e), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, 0f, 0.17f * e), new Vector3(-0.1f, 0f, 1f));

            VictimPoints(t, out var belly, out var grab, out var vUp);

            // Right: stab.
            Vector3 from = p.TorsoPoint(new Vector3(0.26f, 0.3f, 0f));
            Vector3 dir = (belly - from).normalized;
            Vector3 hit = belly - dir * 0.24f;
            Vector3 cocked = p.TorsoPoint(new Vector3(0.36f, 0.12f, -0.12f));
            Vector3 ready = Vector3.Lerp(cocked, hit, 0.5f) + new Vector3(0f, 0.04f, 0f);
            Vector3 target = s < 0f ? Vector3.Lerp(ready, cocked, cock) : Vector3.LerpUnclamped(ready, hit, s);
            Vector3 restR = current.HandPos(0);
            target = Vector3.Lerp(restR, target, e);
            p.SolveArm(0, target, TorsoDir(p, 1f, -1f, -0.6f));
            float roll = t > KnifeStabTimes[3] ? 45f * ActionTimeline.SmoothStep01((t - KnifeStabTimes[3]) / 0.15f) * (1f - release) : 0f;
            Quaternion blade = Quaternion.LookRotation(Vector3.Slerp(dir, Vector3.up, 0.15f * cock), Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            p.PropR = Quaternion.Slerp(DefaultPropLocal(HeldItem.Knife), Quaternion.Inverse(p.ForearmRoot(0)) * blade, e);

            // Left: grab the victim's shoulder, shove at the end.
            float g = ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, 0.05f, 0.3f));
            Vector3 grabT = grab + vUp * 0.07f + new Vector3(0f, 0f, 0.1f * release);
            Vector3 restL = current.HandPos(1);
            p.SolveArm(1, Vector3.Lerp(restL, grabT, g * (1f - 0.6f * release)), TorsoDir(p, -1f, -0.6f, -0.5f));
            return p;
        }

        // ================================================================== stabbed reaction (victim)

        /// <summary>Standing part of the stabbed reaction (t &lt; 2.4). Static so the seeker can predict the victim.</summary>
        static CharacterPose StabbedStandPose(float t)
        {
            float curl = ActionTimeline.VictimCurl(t);
            float fl = ActionTimeline.VictimFlinch(t);
            float grab = ActionTimeline.SmoothStep01(t / 0.25f);
            float st = -0.2f * ActionTimeline.SmoothStep01((t - 2.1f) / 0.35f);

            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.012f * Mathf.Sin(t * 9f) * curl, 0.655f - 0.08f * curl - 0.025f * fl, 0.02f * grab - 0.13f * curl - 0.035f * fl + st);
            p.Pelvis = Quaternion.Euler(-5f * fl, 0f, 2f * Mathf.Sin(t * 3f));
            p.Torso = Quaternion.Euler(3f * grab + 13f * curl + 9f * fl, 3f * Mathf.Sin(t * 5f) * curl, 7f * grab);
            p.Head = Quaternion.Euler(28f * curl - 16f * fl, 6f * Mathf.Sin(t * 7f) * fl, 18f * grab);

            float stepR = ActionTimeline.SmoothStep01((t - 2.15f) / 0.2f), stepL = ActionTimeline.SmoothStep01((t - 2.3f) / 0.2f);
            float liftR = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 2.15f) / 0.2f)) * 0.06f;
            float liftL = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 2.3f) / 0.2f)) * 0.06f;
            p.SolveLeg(0, new Vector3(0.13f, liftR, 0.02f - 0.2f * stepR), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, liftL, -0.02f - 0.2f * stepL), new Vector3(-0.1f, 0f, 1f));

            float b = ActionTimeline.SmoothStep01((t - 0.95f) / 0.3f);
            Vector3 jerk = new Vector3(0f, 0.03f * fl, -0.05f * fl);
            Vector3 r = Vector3.Lerp(new Vector3(0.1f, 0.45f, 0.3f), new Vector3(0.06f, 0.25f, 0.235f), b) + jerk + new Vector3(0.04f * fl, 0f, 0f);
            Vector3 l = Vector3.Lerp(new Vector3(-0.12f, 0.42f, 0.28f), new Vector3(-0.08f, 0.28f, 0.235f), b) + jerk - new Vector3(0.04f * fl, 0f, 0f);
            p.SolveArm(0, p.TorsoPoint(r), TorsoDir(p, 1f, -1f, -0.5f));
            p.SolveArm(1, p.TorsoPoint(l), TorsoDir(p, -1f, -1f, -0.5f));
            return p;
        }

        CharacterPose StabbedReactionPose(float t, Quaternion propR)
        {
            CharacterPose p;
            const float standEnd = 2.4f;
            if (t < standEnd) p = StabbedStandPose(t);
            else if (t < 2.75f)
                p = CharacterPose.Lerp(StabbedStandPose(standEnd), KneelPose(1f, -0.32f), ActionTimeline.EaseIn(ActionTimeline.Remap01(t, standEnd, 2.75f)));
            else if (t < 2.98f)
                p = CharacterPose.Lerp(KneelPose(1f, -0.32f), AllFoursPose(), ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.75f, 2.98f)));
            else
                p = CharacterPose.Lerp(AllFoursPose(), DownedPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 2.98f, StabbedReactionDuration)));
            p.PropR = propR;
            return p;
        }
    }
}
