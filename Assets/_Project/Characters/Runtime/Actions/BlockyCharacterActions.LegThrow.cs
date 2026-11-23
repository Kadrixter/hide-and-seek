using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Throwing a cut-off leg in third person (<see cref="LegThrowTimeline"/>). The leg itself is the carried item
    /// (held by the ankle, hanging at the side - the generic carry layer); this layer only moves the arms and the body:
    /// holding the button winds up - the leg goes back over the right shoulder, the chest turns away and leans back,
    /// the left hand points at the target, the arm trembles a little as the power builds; letting go swings it over the
    /// top, the leg leaves the hand in front of the head (it becomes the flying world item) and the body follows through.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        bool legAimOn;
        float legAimW, legPower, legThrowT = -1f, legThrowFrom;

        /// <summary><paramref name="aiming"/>: the button is held with the leg in the hand; <paramref name="power"/> 0..1 so far.</summary>
        public void SetLegAim(bool aiming, float power)
        {
            legAimOn = aiming;
            legPower = Mathf.Clamp01(power);
        }

        /// <summary>Lets go: the overhand swing (the leg leaves the hand at <see cref="LegThrowTimeline.Release"/>).</summary>
        public void PlayLegThrow(float power)
        {
            EnsureInit();
            legThrowFrom = Mathf.Max(legAimW, 0.35f);
            legThrowT = 0f;
            legPower = Mathf.Clamp01(power);
            legAimOn = false;
        }

        public bool LegThrowing => legThrowT >= 0f;

        /// <summary>The leg has left the hand: the carried model is hidden at once (no put-away motion).</summary>
        bool LegThrowHidesCarry => legThrowT >= LegThrowTimeline.Release;

        void ApplyLegThrow(ref CharacterPose p, float dt, float lookPitch)
        {
            bool can = VisualLifeState == LifeState.Alive && !pathActive && action == ActionKind.None && stunT < 0f && overdoseT < 0f;
            if (legThrowT >= 0f)
            {
                legThrowT += dt;
                if (legThrowT >= LegThrowTimeline.End) legThrowT = -1f;
            }
            bool aimShown = legAimOn && legThrowT < 0f && carryShownGrip != null && carryW > 0.9f;
            legAimW = Mathf.MoveTowards(legAimW, aimShown ? 1f : 0f, dt / (aimShown ? LegThrowTimeline.WindTime : 0.2f));
            if (!can)
            {
                legAimW = 0f;
                legThrowT = -1f;
                return;
            }
            if (legAimW <= 0.001f && legThrowT < 0f) return;

            var q = p;
            float t = legThrowT;
            float S(float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));
            float cock, swing = 0f, follow = 0f, w;
            if (t >= 0f)
            {
                cock = legThrowFrom;
                swing = ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 0f, LegThrowTimeline.Release + 0.03f));
                follow = S(LegThrowTimeline.Release, 0.36f);
                w = 1f - S(0.42f, LegThrowTimeline.End);
            }
            else
            {
                cock = ActionTimeline.SmoothStep01(legAimW);
                w = cock;
            }

            // Body: turns away and leans back on the wind-up, whips round and over on the throw.
            float aimTilt = Mathf.Clamp(lookPitch, -40f, 40f) * 0.3f;
            float yaw = 26f * cock * (1f - swing) - 18f * swing * (1f - 0.5f * follow);
            float lean = -9f * cock * (1f - swing) + 14f * swing + 8f * follow - aimTilt;
            q.Torso = Quaternion.Euler(lean, yaw, 4f * cock * (1f - swing) - 5f * swing);
            q.Head = Quaternion.Euler(-lean * 0.5f + aimTilt * 0.5f, -yaw * 0.85f, 0f);

            // Right hand: the cocked spot behind the shoulder, the release point in front of the head, the follow-through.
            float tremble = legThrowT < 0f ? 0.012f * legPower * cock : 0f;
            Vector3 shake = new Vector3(Mathf.Sin(time * 41f), Mathf.Sin(time * 37f + 1f), 0f) * tremble;
            Vector3 cocked = q.TorsoPoint(new Vector3(0.3f, 0.94f, -0.2f) + shake);
            Vector3 release = q.TorsoPoint(new Vector3(0.2f, 0.9f, 0.42f));
            Vector3 after = q.TorsoPoint(new Vector3(-0.05f, 0.2f, 0.44f));
            Vector3 hold = p.HandPos(0);
            Vector3 hand = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(hold, cocked, cock), release, swing), after, follow);
            Vector3 pole = q.TorsoRoot * Vector3.Lerp(Vector3.Lerp(new Vector3(1f, -1f, -0.4f), new Vector3(1f, 0.35f, -0.35f), cock),
                new Vector3(1f, -0.6f, -0.5f), swing);
            q.SolveArm(0, hand, pole);

            // The leg (held by the ankle): hanging stump-down, then pointing back down behind the shoulder, over the top.
            Quaternion holdRot = p.ForearmRoot(0) * p.PropR;
            Quaternion cockRot = LegRot(q.TorsoRoot * new Vector3(0.15f, -0.5f, -0.85f), q.TorsoRoot * Vector3.right);
            Quaternion relRot = LegRot(q.TorsoRoot * new Vector3(0f, 0.8f, 0.6f), q.TorsoRoot * Vector3.right);
            Quaternion rot = Quaternion.Slerp(Quaternion.Slerp(holdRot, cockRot, cock), relRot, swing);
            q.PropR = Quaternion.Inverse(q.ForearmRoot(0)) * rot;

            // Left hand: out in front at the target, then pulled down to the hip by the throw.
            Vector3 aim = q.TorsoPoint(new Vector3(-0.26f, 0.62f, 0.46f));
            Vector3 hip = q.TorsoPoint(new Vector3(-0.36f, 0.18f, -0.04f));
            q.SolveArm(1, Vector3.Lerp(Vector3.Lerp(p.HandPos(1), aim, cock), hip, Mathf.Max(swing * 0.7f, follow)), TorsoDir(q, -1f, -0.6f, -0.3f));

            CharacterPose.LerpArm(ref p, q, 0, w);
            CharacterPose.LerpArm(ref p, q, 1, w);
            p.Torso = Quaternion.Slerp(p.Torso, q.Torso, w);
            p.Head = Quaternion.Slerp(p.Head, q.Head, w);
            if (legThrowT < LegThrowTimeline.Release) p.PropR = Quaternion.Slerp(p.PropR, q.PropR, w);
        }

        /// <summary>Leg rotation with the stump end (+Y) along <paramref name="stump"/> and the toes as close to <paramref name="toes"/> as possible.</summary>
        static Quaternion LegRot(Vector3 stump, Vector3 toes)
        {
            stump.Normalize();
            Vector3 f = Vector3.ProjectOnPlane(toes, stump);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(Vector3.forward, stump);
            return Quaternion.LookRotation(f.normalized, stump);
        }
    }
}
