using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Carrying another player in the arms ("princess carry", <see cref="BodyCarryState"/>) - the carrier's side. Each
    /// frame this layer computes BOTH bodies in the carrier's root space (canonical layout: head on the carrier's left,
    /// mirrored when needed): the lift (stoop / kneel, roll the body over, up with effort; or a standing player tipped
    /// back into the arm and swept up), the carry loop (weight on the torso, breathing, the body bouncing a little on
    /// every step, dangling shins, a limp arm or an arm round the neck, the head following the carried player's look),
    /// putting down (floor or feet) and the drop when the carrier is hit. The carrier's fists are IK'd onto the body
    /// (under the knees, under the upper back). The carried's own layer (BlockyCharacterActions.BodyCarried.cs) maps
    /// the computed pose into its root, ticked right after this one (<see cref="BodyCarryTicker"/>).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        BodyCarryState bodyCarrying;
        BodyCarryPhase bodyPhaseSeen;
        bool bodyHasLast, bodyHasFrom;
        CharacterPose bodyLastCarried, bodyLastCarrier, bodyFromCarried, bodyFromCarrier;
        float bodyOutW;
        CharacterPose bodyOutPose;
        float bodyDynY, bodyDynVel, bodyLegPh, bodyGruntT;
        bool bodyDynInit;

        /// <summary>This character carries somebody (also during lift / put down / drop).</summary>
        public bool IsCarryingBody => bodyCarrying != null;

        /// <summary>Effort sound moment of the lift (fired once per lift, s from its start).</summary>
        public event System.Action BodyLiftEffort;

        /// <summary>Starts carrying <paramref name="carried"/>; both layers share <paramref name="state"/>.</summary>
        public void BeginBodyCarry(BlockyCharacterActions carried, BodyCarryState state)
        {
            if (carried == null || state == null || carried == this) return;
            EnsureInit();
            if (bodyCarrying != null) EndBodyCarry();
            state.Carrier = this;
            state.Carried = carried;
            bodyCarrying = state;
            bodyPhaseSeen = BodyCarryPhase.None;
            bodyHasLast = bodyHasFrom = false;
            bodyDynInit = false;
            bodyOutW = 0f;
            bodyGruntT = state.Phase == BodyCarryPhase.Lift ? state.Time : 99f;
            carried.AttachBodyCarried(state);
        }

        /// <summary>Stops carrying (the arms come back over a short blend; the carried keeps its last pose until it settles).</summary>
        public void EndBodyCarry()
        {
            var st = bodyCarrying;
            if (st == null) return;
            bodyCarrying = null;
            if (hasLast)
            {
                bodyOutPose = lastOutput;
                bodyOutW = 1f;
            }
            if (st.Carried != null) st.Carried.DetachBodyCarried(st);
        }

        /// <summary>Called from <see cref="ApplyLayer"/> before the additive flinches: both sides of the carry.</summary>
        void ApplyBodyCarryLayers(ref CharacterPose p, float dt)
        {
            ApplyBodyCarrier(ref p, dt);
            ApplyBodyCarried(ref p, dt);
        }

        void ApplyBodyCarrier(ref CharacterPose p, float dt)
        {
            var st = bodyCarrying;
            if (st == null || st.Carried == null || !st.Active)
            {
                if (bodyOutW > 0f)
                {
                    bodyOutW = Mathf.Max(0f, bodyOutW - dt / 0.6f);
                    var keep = p.PropR;
                    p = CharacterPose.Lerp(p, bodyOutPose, ActionTimeline.SmoothStep01(bodyOutW));
                    p.PropR = keep;
                }
                return;
            }

            if (st.Phase != bodyPhaseSeen)
            {
                bodyHasFrom = bodyHasLast && (st.Phase == BodyCarryPhase.PutDown || st.Phase == BodyCarryPhase.Drop);
                if (bodyHasFrom) { bodyFromCarried = bodyLastCarried; bodyFromCarrier = bodyLastCarrier; }
                if (st.Phase == BodyCarryPhase.Lift && bodyPhaseSeen == BodyCarryPhase.None) bodyGruntT = st.Time;
                bodyPhaseSeen = st.Phase;
            }

            bool mir = st.Mirror;
            var cb = mir ? BodyCarryPoses.Mirror(p) : p; // canonical base (locomotion + whatever was on it)
            float t = st.Time;
            float speed = animator != null && animator.Source != null ? Mathf.Max(0f, animator.Source.HorizontalSpeed) : 0f;
            bodyLegPh += dt * (1.2f + speed * 2.6f);
            float legSway = 7f * Mathf.Sin(bodyLegPh) * Mathf.Clamp01(speed / 1.4f) + 2f * Mathf.Sin(time * 1.3f);

            CharacterPose kc, cc;
            float ikW, detailW;
            switch (st.Phase)
            {
                case BodyCarryPhase.Lift:
                    if (st.FromFloor) LiftFloor(st, cb, t, legSway, out kc, out cc, out ikW, out detailW);
                    else LiftStanding(st, cb, t, legSway, out kc, out cc, out ikW, out detailW);
                    if (bodyGruntT < BodyCarryTimeline.LiftGruntTime && t >= BodyCarryTimeline.LiftGruntTime)
                    {
                        try { BodyLiftEffort?.Invoke(); }
                        catch (System.Exception e) { Debug.LogException(e); }
                    }
                    bodyGruntT = t;
                    break;
                case BodyCarryPhase.PutDown:
                    PutDown(st, cb, t, legSway, out kc, out cc, out ikW, out detailW);
                    break;
                case BodyCarryPhase.Drop:
                    Drop(st, cb, t, out kc, out cc, out ikW, out detailW);
                    break;
                default:
                    kc = HoldCarrier(cb);
                    cc = HoldCarried(kc, legSway, dt);
                    ikW = 1f;
                    detailW = 1f;
                    break;
            }

            CarriedDetails(ref cc, kc, st, detailW, speed);
            if (ikW > 0.001f)
            {
                int sev = 0, mask = st.Carried.character != null ? st.Carried.character.SeveredMask : 0;
                if ((mask & Limb.LegR) != 0) sev |= mir ? 2 : 1;
                if ((mask & Limb.LegL) != 0) sev |= mir ? 1 : 2;
                var ik = kc;
                ik.SolveArm(0, BodyCarryPoses.UnderKnees(cc, sev), ik.TorsoRoot * new Vector3(0.75f, -1f, -0.25f));
                ik.SolveArm(1, BodyCarryPoses.UnderBack(cc), ik.TorsoRoot * new Vector3(-0.75f, -1f, -0.25f));
                CharacterPose.LerpArm(ref kc, ik, 0, ikW);
                CharacterPose.LerpArm(ref kc, ik, 1, ikW);
            }

            bodyLastCarried = cc;
            bodyLastCarrier = kc;
            bodyHasLast = true;
            st.CarriedPose = mir ? BodyCarryPoses.Mirror(cc) : cc;
            st.CarriedFrame = Time.frameCount;
            var propR = p.PropR;
            p = mir ? BodyCarryPoses.Mirror(kc) : kc;
            p.PropR = propR;
        }

        // ================================================================== phases

        static float S(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        static float Back(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            const float c = 1.35f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        /// <summary>The carried's lift snapshot in canonical carrier space.</summary>
        CharacterPose StartCanonical(BodyCarryState st)
        {
            var s = BodyCarryPoses.Rebase(st.StartPose, st.StartRootPos, st.StartRootRot, transform.position, transform.rotation);
            return st.Mirror ? BodyCarryPoses.Mirror(s) : s;
        }

        /// <summary>Root frame of the carried after the carry, as a pose of <paramref name="rest"/> in canonical carrier space.</summary>
        CharacterPose EndCanonical(BodyCarryState st, in CharacterPose rest)
        {
            Vector3 pos;
            Quaternion rot;
            if (st.HasEnd) { pos = st.EndPos; rot = st.EndRot; }
            else BodyCarryTimeline.EndFrame(transform.position, transform.rotation, st.Mirror, st.EndStanding && st.Phase != BodyCarryPhase.Drop, 1f, out pos, out rot);
            var e = BodyCarryPoses.Rebase(rest, pos, rot, transform.position, transform.rotation);
            return st.Mirror ? BodyCarryPoses.Mirror(e) : e;
        }

        CharacterPose HoldCarrier(in CharacterPose cb)
        {
            var k = cb;
            float br = Mathf.Sin(breathPhase);
            k.PelvisPos += new Vector3(0f, -0.01f, 0.02f);
            k.Torso = k.Torso * Quaternion.Euler(-5f + 1.4f * br, 0f, 0f);
            k.Head = k.Head * Quaternion.Euler(7f, 0f, 0f);
            return k;
        }

        /// <summary>The cradle riding on the carrier's torso, with a spring on the step bob (the body bounces a little).</summary>
        CharacterPose HoldCarried(in CharacterPose k, float legSway, float dt)
        {
            float y = k.PelvisPos.y;
            if (!bodyDynInit) { bodyDynY = y; bodyDynVel = 0f; bodyDynInit = true; }
            const float stiff = 160f, zeta = 0.3f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                bodyDynVel += (stiff * (y - bodyDynY) - 2f * zeta * Mathf.Sqrt(stiff) * bodyDynVel) * h;
                bodyDynY += bodyDynVel * h;
            }
            float bounce = Mathf.Clamp(bodyDynY - y, -0.045f, 0.045f);
            var cr = BodyCarryPoses.Hold;
            if (bodyCarrying != null && bodyCarrying.Conscious)
            {
                // Awake: sits up more in the arms (and can reach round the neck).
                cr.Rise += 13f;
                cr.Curl += 4f;
                cr.Pelvis += new Vector3(0.02f, 0.03f, -0.02f);
            }
            cr.Pelvis.y += bounce;
            cr.Swing = -bounce * 260f;
            var c = BodyCarryPoses.CradlePose(cr, legSway);
            AttachToTorso(ref c, k);
            return c;
        }

        /// <summary>A cradle defined for an upright carrier, moved with the carrier's actual torso (lean, breathing, twist).</summary>
        static void AttachToTorso(ref CharacterPose c, in CharacterPose k)
        {
            Quaternion r = k.TorsoRoot;
            c.PelvisPos = k.PelvisPos + r * (c.PelvisPos - BlockyCharacter.PelvisRest);
            c.Pelvis = r * c.Pelvis;
        }

        void LiftFloor(BodyCarryState st, in CharacterPose cb, float t, float legSway,
            out CharacterPose kc, out CharacterPose cc, out float ikW, out float detailW)
        {
            var s = StartCanonical(st);
            Vector3 lie = Vector3.Lerp(s.PelvisPos, BodyCarryTimeline.FloorPelvisLocal, 0.45f);
            var hold = HoldCarrier(cb);
            if (t < 0.28f) cc = CharacterPose.Lerp(s, BodyCarryPoses.SideFacing(lie), S(t, 0f, 0.28f));
            else if (t < 0.55f)
            {
                var low = BodyCarryPoses.Low;
                low.Pelvis.x = lie.x;
                cc = CharacterPose.Lerp(BodyCarryPoses.SideFacing(lie), BodyCarryPoses.CradlePose(low), S(t, 0.28f, 0.55f));
            }
            else if (t < 1.0f) cc = BodyCarryPoses.CradlePose(BodyCarryPoses.Cradle.Lerp(BodyCarryPoses.Low, BodyCarryPoses.Mid, S(t, 0.55f, 1.0f)));
            else
            {
                var h = HoldCarried(hold, legSway, 0f);
                cc = CharacterPose.Lerp(BodyCarryPoses.CradlePose(BodyCarryPoses.Mid), h, Back(ActionTimeline.Remap01(t, 1.0f, BodyCarryTimeline.LiftFloor)));
            }

            if (t < 0.3f) kc = CharacterPose.Lerp(cb, BodyCarryPoses.Stoop(), S(t, 0f, 0.3f));
            else if (t < 0.55f) kc = CharacterPose.Lerp(BodyCarryPoses.Stoop(), BodyCarryPoses.Kneel(36f), S(t, 0.3f, 0.55f));
            else if (t < 1.0f)
            {
                // The heavy part: slower in the middle, a strained tremble.
                float x = ActionTimeline.Remap01(t, 0.55f, 1.0f);
                kc = CharacterPose.Lerp(BodyCarryPoses.Kneel(36f), BodyCarryPoses.Rise(1f), ActionTimeline.SmoothStep01(x * x * (2f - x)));
                float shake = Mathf.Sin(time * 47f) * 0.6f * Mathf.Sin(x * Mathf.PI);
                kc.Torso = kc.Torso * Quaternion.Euler(shake, 0f, shake * 0.5f);
            }
            else kc = CharacterPose.Lerp(BodyCarryPoses.Rise(1f), hold, S(t, 1.0f, BodyCarryTimeline.LiftFloor));
            ikW = S(t, 0.14f, 0.42f);
            detailW = S(t, 0.95f, BodyCarryTimeline.LiftFloor);
        }

        void LiftStanding(BodyCarryState st, in CharacterPose cb, float t, float legSway,
            out CharacterPose kc, out CharacterPose cc, out float ikW, out float detailW)
        {
            var s = StartCanonical(st);
            Vector3 root = new Vector3(s.PelvisPos.x, 0f, s.PelvisPos.z);
            var lean = BodyCarryPoses.LeanBack(root, 28f);
            var mid = BodyCarryPoses.Mid;
            mid.Pelvis.y = 0.82f;
            var hold = HoldCarrier(cb);
            if (t < 0.35f) cc = CharacterPose.Lerp(s, lean, S(t, 0f, 0.35f));
            else if (t < 0.75f) cc = CharacterPose.Lerp(lean, BodyCarryPoses.CradlePose(mid), S(t, 0.35f, 0.75f));
            else cc = CharacterPose.Lerp(BodyCarryPoses.CradlePose(mid), HoldCarried(hold, legSway, 0f), Back(ActionTimeline.Remap01(t, 0.75f, BodyCarryTimeline.LiftStanding)));

            if (t < 0.35f) kc = CharacterPose.Lerp(cb, BodyCarryPoses.Dip(1f), S(t, 0f, 0.35f));
            else if (t < 0.75f) kc = CharacterPose.Lerp(BodyCarryPoses.Dip(1f), BodyCarryPoses.Dip(0.35f), S(t, 0.35f, 0.75f));
            else kc = CharacterPose.Lerp(BodyCarryPoses.Dip(0.35f), hold, S(t, 0.75f, BodyCarryTimeline.LiftStanding));
            ikW = S(t, 0.08f, 0.32f);
            detailW = S(t, 0.7f, BodyCarryTimeline.LiftStanding);
        }

        void PutDown(BodyCarryState st, in CharacterPose cb, float t, float legSway,
            out CharacterPose kc, out CharacterPose cc, out float ikW, out float detailW)
        {
            var hold = HoldCarrier(cb);
            var fromC = bodyHasFrom ? bodyFromCarried : HoldCarried(hold, legSway, 0f);
            var fromK = bodyHasFrom ? bodyFromCarrier : hold;
            if (st.EndStanding)
            {
                var end = EndCanonical(st, BodyCarryPoses.StandRest());
                var lean = BodyCarryPoses.LeanBack(new Vector3(end.PelvisPos.x, 0f, end.PelvisPos.z), 26f);
                cc = t < 0.45f ? CharacterPose.Lerp(fromC, lean, S(t, 0f, 0.45f)) : CharacterPose.Lerp(lean, end, S(t, 0.45f, BodyCarryTimeline.PutDownStanding));
                kc = t < 0.45f ? CharacterPose.Lerp(fromK, BodyCarryPoses.Dip(0.8f), S(t, 0f, 0.45f)) : CharacterPose.Lerp(BodyCarryPoses.Dip(0.8f), cb, S(t, 0.45f, BodyCarryTimeline.PutDownStanding));
                ikW = 1f - S(t, 0.42f, 0.8f);
            }
            else
            {
                var end = EndCanonical(st, BodyCarryPoses.SupineRest());
                var low = BodyCarryPoses.Low;
                low.Pelvis = new Vector3(end.PelvisPos.x, low.Pelvis.y, end.PelvisPos.z);
                if (t < 0.4f) cc = CharacterPose.Lerp(fromC, BodyCarryPoses.CradlePose(BodyCarryPoses.Mid), S(t, 0f, 0.4f));
                else if (t < 0.8f) cc = BodyCarryPoses.CradlePose(BodyCarryPoses.Cradle.Lerp(BodyCarryPoses.Mid, low, S(t, 0.4f, 0.8f)));
                else cc = CharacterPose.Lerp(BodyCarryPoses.CradlePose(low), end, S(t, 0.8f, BodyCarryTimeline.PutDownFloor));
                if (t < 0.4f) kc = CharacterPose.Lerp(fromK, BodyCarryPoses.Rise(1f), S(t, 0f, 0.4f));
                else kc = CharacterPose.Lerp(BodyCarryPoses.Rise(1f), BodyCarryPoses.Kneel(30f), S(t, 0.4f, 0.8f));
                ikW = 1f - 0.75f * S(t, 0.82f, BodyCarryTimeline.PutDownFloor);
            }
            detailW = 1f - S(t, 0f, 0.4f);
        }

        void Drop(BodyCarryState st, in CharacterPose cb, float t,
            out CharacterPose kc, out CharacterPose cc, out float ikW, out float detailW)
        {
            var fromC = bodyHasFrom ? bodyFromCarried : HoldCarried(HoldCarrier(cb), 0f, 0f);
            var fromK = bodyHasFrom ? bodyFromCarrier : HoldCarrier(cb);
            var end = EndCanonical(st, BodyCarryPoses.SupineRest());
            var slip = fromC;
            slip.PelvisPos += new Vector3(0f, -0.07f, 0.03f);
            slip.KneeR = slip.KneeR * Quaternion.Euler(-25f, 0f, 0f);
            slip.KneeL = slip.KneeL * Quaternion.Euler(-35f, 0f, 0f);
            var air = end;
            air.PelvisPos.y += 0.05f;
            if (t < 0.12f) cc = CharacterPose.Lerp(fromC, slip, S(t, 0f, 0.12f));
            else if (t < 0.46f) cc = CharacterPose.Lerp(slip, air, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 0.12f, 0.46f)));
            else
            {
                cc = end;
                float b = ActionTimeline.Remap01(t, 0.46f, 0.62f);
                cc.PelvisPos.y += 0.05f * (1f - b) + 0.03f * Mathf.Sin(b * Mathf.PI);
            }
            var stag = BodyCarryPoses.Stagger(cb);
            kc = t < 0.22f ? CharacterPose.Lerp(fromK, stag, ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 0f, 0.22f)))
                : CharacterPose.Lerp(stag, cb, S(t, 0.22f, BodyCarryTimeline.DropTime));
            ikW = 1f - S(t, 0f, 0.1f);
            detailW = 1f - S(t, 0f, 0.15f);
        }

        // ================================================================== carried's arms and head

        void CarriedDetails(ref CharacterPose c, in CharacterPose k, BodyCarryState st, float w, float speed)
        {
            // Look of the carried player, canonical carrier space (zero = along the carrier's forward).
            Vector3 look = st.CarriedLook.sqrMagnitude > 1e-4f ? Quaternion.Inverse(transform.rotation) * st.CarriedLook : Vector3.forward;
            if (st.Mirror) look.x = -look.x;
            var arms = c;
            float br = Mathf.Sin(breathPhase * 1.3f + 1f);
            BodyCarryPoses.RestArms(ref arms, br);
            if (st.Conscious)
            {
                // Inner (right) arm round the carrier's neck: hand over his left shoulder.
                arms.SolveArm(0, k.TorsoPoint(new Vector3(-0.13f, 0.7f, -0.08f)), new Vector3(0.1f, -1f, 0.7f));
                arms.Head = BodyCarryPoses.HeadToward(arms, look);
            }
            else
            {
                // Outer (left) arm hangs limp and swings; the head lolls but still follows the look a little.
                Vector3 sh = arms.ShoulderPos(1);
                float sw = Mathf.Sin(bodyLegPh + 1.1f) * Mathf.Clamp01(speed / 1.4f);
                Vector3 hang = new Vector3(0.12f * sw, -1f, 0.1f + 0.18f * sw + 0.05f * Mathf.Sin(time * 0.9f));
                arms.SolveArm(1, sh + hang.normalized * 0.47f, new Vector3(-0.2f, 0f, 1f));
                var loll = Quaternion.Euler(-26f + 4f * br, 16f + 6f * Mathf.Sin(time * 0.6f), 14f);
                arms.Head = Quaternion.Slerp(loll, BodyCarryPoses.HeadToward(arms, look), 0.5f);
            }
            CharacterPose.LerpArm(ref c, arms, 0, w);
            CharacterPose.LerpArm(ref c, arms, 1, w);
            c.Head = Quaternion.Slerp(c.Head, arms.Head, w);
        }
    }
}
