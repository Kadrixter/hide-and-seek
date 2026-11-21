using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Timeline of the lobby exit-door easter egg (seconds from pressing E).</summary>
    public static class ExitTimeline
    {
        public const float HandOnHandle = 0.35f;
        public const float DoorOpenStart = 0.45f, DoorOpenEnd = 1.2f;
        public const float SpinUp = 1.3f;          // minigun motor starts
        public const float FireStart = 2.3f, FireEnd = 5.0f;
        public const float FallStart = 3.0f, FallEnd = 3.45f;
        public const float FigureIn = 5.6f, Grab = 6.6f, Slam = 7.4f;
        public const float Respawn = 9.4f;         // 2 s on the floor after the slam
        public const float DoorOpenYaw = -80f;

        public static float Seg(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>Door hinge yaw at time t (opens inwards, the shadow pulls it shut).</summary>
        public static float DoorYaw(float t)
        {
            if (t < Grab) return DoorOpenYaw * Seg(t, DoorOpenStart, DoorOpenEnd);
            float c = ActionTimeline.Remap01(t, Grab, Slam);
            return DoorOpenYaw * (1f - c * c);
        }
    }

    public partial class BlockyCharacterActions
    {
        float gunT = -1f;
        Vector3 gunHandleLocal = new Vector3(0.2f, 1.0f, 0.6f);

        /// <summary>Exit-door easter egg clock (t &lt; 0 = off); <paramref name="handleWorld"/> = door handle.</summary>
        public void SetGunned(float t, Vector3 handleWorld)
        {
            EnsureInit();
            if (gunT >= 0f && t < 0f && hasLast)
            {
                fadeFrom = lastOutput;
                fadeT = 0f;
            }
            gunT = t;
            if (t >= 0f) gunHandleLocal = transform.InverseTransformPoint(handleWorld);
        }

        bool GunHidesItem => gunT >= 0f;

        CharacterPose GunnedPose(in CharacterPose basePose, float t)
        {
            float S(float a, float b) => ExitTimeline.Seg(t, a, b);
            // ---- open the door: right hand on the handle, pull, then stand dazzled, shielding the eyes
            if (t < ExitTimeline.FireStart)
            {
                var p = basePose;
                if (t < ExitTimeline.DoorOpenEnd + 0.1f)
                {
                    float reach = S(0f, ExitTimeline.HandOnHandle) * (1f - S(ExitTimeline.DoorOpenEnd - 0.2f, ExitTimeline.DoorOpenEnd + 0.1f));
                    var ap = p;
                    Vector3 h = gunHandleLocal + new Vector3(0f, -0.04f * S(ExitTimeline.HandOnHandle, ExitTimeline.DoorOpenStart), 0.25f * S(ExitTimeline.DoorOpenStart, ExitTimeline.DoorOpenEnd));
                    ap.SolveArm(0, h, TorsoDir(ap, 1f, -0.6f, -0.4f));
                    CharacterPose.LerpArm(ref p, ap, 0, reach);
                }
                float daze = S(ExitTimeline.DoorOpenStart + 0.2f, ExitTimeline.DoorOpenEnd + 0.2f);
                if (daze > 0f)
                {
                    var dz = p;
                    dz.Torso = Quaternion.Euler(-8f, 6f, 0f);
                    dz.Head = Quaternion.Euler(-6f + 3f * Mathf.Sin(time * 3f), -10f, 0f);
                    dz.PelvisPos = new Vector3(0f, BlockyCharacter.HipHeight - 0.02f, -0.06f);
                    dz.SolveArm(1, dz.TorsoPoint(new Vector3(-0.05f, 0.82f, 0.22f)), TorsoDir(dz, -1f, -0.2f, -0.8f));   // hand over the eyes
                    dz.SolveArm(0, dz.TorsoPoint(new Vector3(0.32f, 0.35f, 0.18f)), TorsoDir(dz, 1f, -0.6f, -0.4f));
                    if (t > ExitTimeline.SpinUp)
                    {
                        // The whir: freezes, head tilts.
                        dz.Head = Quaternion.Euler(-2f, -4f, 8f * S(ExitTimeline.SpinUp, ExitTimeline.SpinUp + 0.4f));
                    }
                    p = CharacterPose.Lerp(p, dz, daze);
                }
                return p;
            }

            // ---- riddled with bullets: violent jerks standing, then thrown on the back
            var rigid = CharacterPose.Identity;
            float k = 1.6f;
            rigid.PelvisPos = new Vector3(0.03f * J(time, 41f), BlockyCharacter.HipHeight - 0.04f + 0.02f * J(time, 42f), -0.08f - 0.04f * Mathf.Abs(J(time, 43f)));
            rigid.Pelvis = Quaternion.Euler(6f * J(time, 44f) * k, 5f * J(time, 45f), 4f * J(time, 46f));
            rigid.Torso = Quaternion.Euler(-16f + 10f * J(time, 47f), 9f * J(time, 48f), 7f * J(time, 49f));
            rigid.Head = Quaternion.Euler(-20f + 18f * J(time, 50f), 16f * J(time, 51f), 10f * J(time, 52f));
            rigid.SolveLeg(0, new Vector3(0.16f, 0f, 0.03f), new Vector3(0.2f, 0f, 1f));
            rigid.SolveLeg(1, new Vector3(-0.16f, 0f, -0.06f), new Vector3(-0.2f, 0f, 1f));
            Vector3 jr = new Vector3(J(time, 53f), J(time, 54f), J(time, 55f)) * 0.12f;
            Vector3 jl = new Vector3(J(time, 56f), J(time, 57f), J(time, 58f)) * 0.12f;
            rigid.SolveArm(0, rigid.TorsoPoint(new Vector3(0.5f, 0.6f, 0.0f)) + jr, TorsoDir(rigid, 1f, -0.4f, -0.6f));
            rigid.SolveArm(1, rigid.TorsoPoint(new Vector3(-0.5f, 0.62f, 0.0f)) + jl, TorsoDir(rigid, -1f, -0.4f, -0.6f));
            if (t < ExitTimeline.FallStart)
            {
                float into = S(ExitTimeline.FireStart, ExitTimeline.FireStart + 0.12f);
                return CharacterPose.Lerp(basePose, rigid, into);
            }

            var lie = LieBackPose();
            // Bullets keep hitting the body on the floor until the gun stops, then it lies still.
            float tw = t < ExitTimeline.FireEnd ? 1f : 0f;
            if (tw > 0f)
            {
                lie.Torso = lie.Torso * Quaternion.Euler(10f * J(time, 60f), 5f * J(time, 61f), 8f * J(time, 62f));
                lie.Head = lie.Head * Quaternion.Euler(12f * J(time, 63f), 10f * J(time, 64f), 0f);
                lie.ShoulderR = lie.ShoulderR * Quaternion.Euler(30f * J(time, 65f), 0f, 20f * J(time, 66f));
                lie.ShoulderL = lie.ShoulderL * Quaternion.Euler(30f * J(time, 67f), 0f, 20f * J(time, 68f));
                lie.HipR = lie.HipR * Quaternion.Euler(16f * J(time, 69f), 0f, 0f);
                lie.HipL = lie.HipL * Quaternion.Euler(16f * J(time, 70f), 0f, 0f);
                lie.PelvisPos += Vector3.up * 0.025f * Mathf.Abs(J(time, 71f));
            }
            if (t < ExitTimeline.FallEnd)
            {
                float f = ActionTimeline.Remap01(t, ExitTimeline.FallStart, ExitTimeline.FallEnd);
                return CharacterPose.Lerp(rigid, lie, f * f);
            }
            return lie;
        }
    }
}
