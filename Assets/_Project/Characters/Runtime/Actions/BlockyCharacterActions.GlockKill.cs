using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Glock finisher (last hider shot with the Glock): the seeker aims two-handed from ~2.3 m, shoots the leg (the
    /// victim drops to the knees and begs with raised hands), the shoulder, then the head (the victim falls onto the
    /// back), lowers the gun and blows the smoke off the muzzle.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        bool glockKill;

        static float Recoil(float t, float[] hits)
        {
            float r = 0f;
            foreach (float h in hits)
            {
                float x = t - h;
                if (x >= 0f && x < 0.5f) r = Mathf.Max(r, Mathf.Exp(-x * 13f));
            }
            return r;
        }

        CharacterPose SeekerGlockFinisher(in CharacterPose current, float t)
        {
            var hits = KillCore.HitTimes(KillMove.GlockFinisher);
            float raise = coreAfterIntro ? 1f : Seg(t, 0f, 0.45f);
            float lower = Seg(t, KillCore.GlockFinisherLowerTime, KillCore.GlockFinisherLowerTime + 0.45f);
            float blow = Seg(t, KillCore.GlockFinisherTauntTime, KillCore.GlockFinisherTauntTime + 0.4f) *
                         (1f - Seg(t, KillCore.GlockFinisherTauntTime + 1.3f, KillCore.GlockFinisherTauntTime + 1.7f));
            float r = Recoil(t, hits);

            var p = current;
            // Slight stance: lean in while aiming, a small step back with the last shot.
            float aimW = raise * (1f - lower);
            p.Torso = Quaternion.Euler(5f * aimW - 6f * r, 0f, 0f) * p.Torso;
            p.PelvisPos += new Vector3(0f, -0.03f * aimW, -0.02f * r);

            // Aim point: legs, then the chest, then the head (in this root space, tracking the real victim).
            Vector3 legPt = VictimTorso(new Vector3(0.1f, -0.35f, 0.1f));
            Vector3 chest = VictimTorso(new Vector3(0.12f, 0.55f, 0.1f));
            Vector3 head = VictimHead(new Vector3(0f, 0.2f, 0.1f));
            Vector3 aim = t < hits[0] + 0.3f ? legPt : t < hits[1] + 0.35f ? Vector3.Lerp(legPt, chest, Seg(t, hits[0] + 0.3f, hits[1] - 0.15f))
                : Vector3.Lerp(chest, head, Seg(t, hits[1] + 0.35f, hits[2] - 0.3f));

            Vector3 gun = p.TorsoPoint(new Vector3(0.04f, 0.55f, 0.46f));
            Vector3 dir = aim - gun;
            Quaternion aimRot = Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward, Vector3.up);
            gun -= aimRot * new Vector3(0f, -0.02f * r, 0.07f * r);
            aimRot *= Quaternion.Euler(-18f * r, 0f, 0f);

            // Rest (gun low), then the taunt: gun up by the face, muzzle up.
            Vector3 low = p.TorsoPoint(new Vector3(0.12f, 0.18f, 0.3f));
            Quaternion lowRot = p.TorsoRoot * Quaternion.Euler(40f, -6f, 0f);
            Vector3 face = p.TorsoPoint(new Vector3(0.1f, 0.78f, 0.24f));
            Quaternion faceRot = p.TorsoRoot * Quaternion.Euler(-82f, -20f, 0f);
            Vector3 gPos = Vector3.Lerp(Vector3.Lerp(low, gun, raise), low, lower);
            Quaternion gRot = Quaternion.Slerp(Quaternion.Slerp(lowRot, aimRot, raise), lowRot, lower);
            gPos = Vector3.Lerp(gPos, face, blow);
            gRot = Quaternion.Slerp(gRot, faceRot, blow);
            Vector3 belt = p.TorsoPoint(new Vector3(-0.27f, 0.06f, 0.12f));
            SolveGlockHands(ref p, gPos, gRot, ActionTimeline.SupportGripLocal, Mathf.Max(lower, blow), belt);

            // Head follows the aim, tilts to the muzzle for the blow.
            Vector3 look = aim - p.TorsoPoint(new Vector3(0f, 0.75f, 0f));
            float pitch = -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
            p.Head = Quaternion.Euler(Mathf.Lerp(0f, Mathf.Clamp(pitch, -40f, 40f), aimW) + 18f * blow, -12f * blow, 6f * blow);
            return p;
        }

        CharacterPose VictimGlockFinisher(float t)
        {
            var hits = KillCore.HitTimes(KillMove.GlockFinisher);
            // 1. Standing, hands going up (surrender).
            float up = coreAfterIntro ? 1f : Seg(t, 0.05f, 0.5f);
            var stand = VictimStand(t, 0f, 0.05f, 0f, -6f * up, 0.15f * up, 0f, 0f,
                Vector3.Lerp(HandsRestR, new Vector3(0.24f, 0.78f, 0.12f), up), Vector3.Lerp(HandsRestL, new Vector3(-0.26f, 0.78f, 0.12f), up));
            if (t < hits[0]) return stand;

            // 2. Leg shot: drop to the knees, hands up begging, shaking; shoulder shot jerks the torso back.
            var kneel = KneelPose(0.1f, -0.02f);
            float jerk = Pulse(t, hits[1], 1.4f);
            float shake = Mathf.Sin(t * 23f) * 0.012f;
            kneel.Torso = Quaternion.Euler(-6f - 14f * jerk + 3f * Mathf.Sin(t * 2.5f), 10f * jerk, -8f * jerk) * kneel.Torso;
            kneel.Head = Quaternion.Euler(-12f + 10f * jerk, 6f * Mathf.Sin(t * 1.7f), 0f);
            Vector3 hr = new Vector3(0.22f + shake, 0.74f + 0.04f * Mathf.Sin(t * 4f), 0.16f);
            Vector3 hl = new Vector3(-0.22f - shake, 0.72f + 0.04f * Mathf.Sin(t * 4f + 1f), 0.16f);
            // Hit shoulder: the right arm drops to the wound.
            float hurt = Seg(t, hits[1], hits[1] + 0.25f);
            hr = Vector3.Lerp(hr, new Vector3(-0.12f, 0.6f, 0.15f), hurt);
            kneel.SolveArm(0, kneel.TorsoPoint(hr), TorsoDir(kneel, 1f, -1f, -0.5f));
            kneel.SolveArm(1, kneel.TorsoPoint(hl), TorsoDir(kneel, -1f, -1f, -0.5f));
            if (t < hits[0] + 0.45f) return CharacterPose.Lerp(stand, kneel, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, hits[0], hits[0] + 0.45f)));
            if (t < hits[2]) return kneel;

            // 3. Head shot: snaps back and falls onto the back, dead.
            var snap = kneel;
            snap.Head = Quaternion.Euler(-45f, 0f, 0f);
            snap.Torso = Quaternion.Euler(-25f, 0f, 0f) * kneel.Torso;
            if (t < hits[2] + 0.12f) return CharacterPose.Lerp(kneel, snap, ActionTimeline.Remap01(t, hits[2], hits[2] + 0.12f));
            return CharacterPose.Lerp(snap, DeadPose(), ActionTimeline.EaseIn(ActionTimeline.Remap01(t, hits[2] + 0.12f, KillCore.GlockFinisherVictimDead)));
        }
    }
}
