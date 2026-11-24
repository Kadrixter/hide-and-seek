using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Third person of working on a fuse box socket (<see cref="FuseTimeline"/>): the right fist brings the fuse up to
    /// the socket, lines it up and pushes it home with a short press (or closes on a seated fuse's cap, tugs twice and
    /// pulls it out towards the chest); the left hand rests flat on the box frame meanwhile, the torso leans and turns a
    /// little towards the socket and the head watches the hand. The fuse itself is posed by the game (it follows
    /// <see cref="RightHandWorld"/> or the socket axis).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float fuseT = -1f, fuseW;
        bool fuseRemove;
        Vector3 fuseSlotL, fuseAxisL = Vector3.back, fuseBraceL;

        /// <summary>
        /// Fuse work: time on <see cref="FuseTimeline"/> (&lt; 0 = off), taking out or putting in, the seated fuse's centre
        /// and the socket's outward axis (world), the point on the box frame for the left hand (world).
        /// </summary>
        public void SetFuseWork(float t, bool remove, Vector3 seatedWorld, Vector3 axisWorld, Vector3 braceWorld)
        {
            EnsureInit();
            fuseT = t;
            fuseRemove = remove;
            if (t < 0f) return;
            fuseSlotL = transform.InverseTransformPoint(seatedWorld);
            fuseAxisL = transform.InverseTransformDirection(axisWorld).normalized;
            fuseBraceL = transform.InverseTransformPoint(braceWorld);
        }

        public bool FuseWorking => FuseTimeline.Running(fuseT, fuseRemove);

        void ApplyFuseLayer(ref CharacterPose p, float dt, bool standing)
        {
            bool on = FuseTimeline.Running(fuseT, fuseRemove) && standing;
            fuseW = Damp(fuseW, on ? 1f : 0f, 14f, dt);
            if (fuseW < 0.001f) return;
            float t = Mathf.Max(0f, fuseT);
            bool rm = fuseRemove;
            float body = FuseTimeline.Body(t, rm) * fuseW;

            // Torso: a slight lean in and a turn of the right shoulder towards the socket; breathing goes on underneath.
            float yaw = Mathf.Clamp(Mathf.Atan2(fuseSlotL.x - 0.15f, Mathf.Max(0.3f, fuseSlotL.z)) * Mathf.Rad2Deg * 0.5f, -18f, 18f);
            float lean = Mathf.Clamp((1.35f - fuseSlotL.y) * 30f, -6f, 16f) + 5f;
            float tug = 0f;
            if (rm) foreach (float k in FuseTimeline.Tugs) tug += Mathf.Exp(-Mathf.Pow((t - k) / 0.08f, 2f));
            if (rm && t > FuseTimeline.OutPop) tug += 1.4f * Mathf.Exp(-(t - FuseTimeline.OutPop) * 9f);
            p.Torso = p.Torso * Quaternion.Euler((lean - 5f * tug) * body, yaw * body, 0f);

            // Right fist: on the fuse's cap along the socket axis (pressed in a little at the click).
            float o = FuseTimeline.OnSocketLine(t, rm) ? FuseTimeline.Out(t, rm) : rm ? FuseTimeline.Popped : FuseTimeline.Approach;
            Vector3 cap = fuseSlotL + fuseAxisL * (o + FuseTimeline.FistOnCap - (rm ? 0f : FuseTimeline.Press(t)));
            if (!FuseTimeline.OnSocketLine(t, rm)) cap += Vector3.up * 0.04f; // held a little higher on the way in / out
            float reach = FuseTimeline.Reach(t, rm) * fuseW;
            var ap = p;
            Vector3 shoulder = ap.ShoulderPos(0);
            Vector3 toCap = cap - shoulder;
            if (toCap.magnitude > 0.6f) cap = shoulder + toCap.normalized * 0.6f; // never lock the elbow straight
            ap.SolveArm(0, Vector3.Lerp(p.HandPos(0), cap, reach), TorsoDir(ap, 1f, -0.85f, -0.35f));
            CharacterPose.LerpArm(ref p, ap, 0, Mathf.Clamp01(reach * 1.2f));

            // Left hand flat on the frame.
            float brace = FuseTimeline.Brace(t, rm) * fuseW;
            if (brace > 0.001f)
            {
                var lp = p;
                Vector3 b = fuseBraceL;
                Vector3 ls = lp.ShoulderPos(1);
                if ((b - ls).magnitude > 0.6f) b = ls + (b - ls).normalized * 0.6f;
                lp.SolveArm(1, Vector3.Lerp(p.HandPos(1), b, brace), TorsoDir(lp, -1f, -0.8f, -0.3f));
                CharacterPose.LerpArm(ref p, lp, 1, brace);
            }

            // Eyes on the socket.
            Vector3 look = fuseSlotL - new Vector3(0f, 1.6f, 0f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(look.y, Mathf.Max(0.2f, new Vector2(look.x, look.z).magnitude)) * Mathf.Rad2Deg, -30f, 40f);
            float hy = Mathf.Clamp(Mathf.Atan2(look.x, Mathf.Max(0.2f, look.z)) * Mathf.Rad2Deg, -35f, 35f);
            p.Head = Quaternion.Slerp(p.Head, Quaternion.Euler(pitch * 0.75f, hy * 0.5f, 0f), body * 0.85f);
        }
    }
}
