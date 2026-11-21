using System.Collections;
using HideAndSeek.Audio;
using HideAndSeek.Characters;

using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Kill sequence on every client: starts both animations (seeker + victim), the cinematic camera, and plays sound,
    /// blood and limb cuts on the event marks of <see cref="KillMoves.Events"/> (struggle blows, killing blows, aftermath).
    /// </summary>
    public partial class NetPlayer
    {
        private IEnumerator KnifeSequence(NetPlayer victim, KillMove move, bool showAll)
        {
            bool saw = KillMoves.IsSaw(move);
            var bca = Actions as BlockyCharacterActions;
            bca?.PlayKill(move, victim != null ? victim.Character : null);
            (victim?.Actions as BlockyCharacterActions)?.PlayKillReaction(move, Character);
            // Third-person cinematic: the two players for a normal variant, everybody for the finisher.
            bool involved = IsOwner || (victim != null && victim.IsOwner);
            if (victim != null && (showAll || involved))
                KillCam.Play(this, victim, KillMoves.SequenceDuration(move), showAll, KillMoves.IsFinisher(move), showAll ? "WYKOŃCZENIE" : "",
                    KillMoves.TauntTime(move));
            if (showAll) GameAudio.Play2D(GameSfx.SeekerReleased, 0.8f);
            StartCoroutine(KillMarks(victim, move, showAll));

            if (saw)
            {
                BloodySaw();
                if (GoreWindow(move, out float gs, out float ge)) StartCoroutine(ChainsawGore(victim, gs, ge));
            }
            else if (move != KillMove.GlockFinisher) GameAudio.PlayAttached(GameSfx.KnifeDraw, transform, 0.8f);
            if (victim != null) GameAudio.PlayAttached(GameSfx.HiderScream, victim.transform, saw ? 1f : 0.7f);

            var ev = KillMoves.Events(move);
            float t = 0f;
            int shots = 0;
            bool firstBlade = true;
            for (int i = 0; i < ev.Length; i++)
            {
                yield return new WaitForSeconds(ev[i].Time - t);
                t = ev[i].Time;
                if (victim == null) yield break;
                Vector3 chest = victim.ChestPosition;
                switch (ev[i].Kind)
                {
                    case KillHitKind.Grab:
                        GameAudio.PlayAt(GameSfx.KnifeSwing, chest, 0.3f);
                        GameAudio.PlayAttached(GameSfx.HiderPain, victim.transform, 0.55f);
                        break;
                    case KillHitKind.Blunt:
                        GameAudio.PlayAt(GameSfx.BodyFall, chest, 0.55f);
                        GameAudio.PlayAttached(GameSfx.HiderPain, victim.transform, 1f);
                        if (victim.IsOwner) GameHud.Hurt(0.6f);
                        if (IsOwner || victim.IsOwner) GameHud.Kick();
                        break;
                    case KillHitKind.ShotMiss:
                    {
                        // Warning shot into the floor next to the victim's feet.
                        Vector3 floor = victim.transform.position + victim.transform.right * 0.25f - victim.transform.forward * 0.35f + Vector3.up * 0.02f;
                        GlockShotFx(floor);
                        GameAudio.PlayAt(GameSfx.BulletImpact, floor, 1f);
                        DustFx.Puff(floor, Vector3.up, 14, 0.9f, 0.06f);
                        GameAudio.PlayAttached(GameSfx.HiderScream, victim.transform, 0.9f);
                        break;
                    }
                    case KillHitKind.Shot:
                    {
                        // Leg, shoulder, head.
                        Vector3 target = shots == 0 ? victim.transform.position + Vector3.up * 0.45f
                            : shots == 1 ? chest + victim.transform.right * 0.15f + Vector3.up * 0.15f
                            : victim.ChestPosition + Vector3.up * 0.42f;
                        Vector3 from = GlockShotFx(target);
                        Vector3 sd = (target - from).normalized;
                        bool headShot = shots == 2;
                        shots++;
                        BloodFx.Spray(target, (sd + Vector3.up * (headShot ? 0.35f : 0.1f)).normalized, headShot ? 2.4f : 1.3f);
                        if (headShot)
                        {
                            BloodFx.WallSplash(target, sd, 3.5f, 0.9f);
                            BloodFx.Pool(victim.transform.position - victim.transform.forward * 0.9f, 1.0f, 12f);
                        }
                        GameAudio.PlayAttached(headShot ? GameSfx.HiderPain : GameSfx.HiderScream, victim.transform, 1f);
                        if (victim.IsOwner) GameHud.Hurt(1f);
                        if (IsOwner || victim.IsOwner) GameHud.Kick();
                        break;
                    }
                    case KillHitKind.Saw:
                    {
                        GameAudio.PlayAt(GameSfx.ChainsawCut, chest, 1f);
                        GameAudio.PlayAttached(i % 2 == 0 ? GameSfx.HiderScream : GameSfx.HiderPain, victim.transform, 1f);
                        if (victim.IsOwner) GameHud.Hurt(1f);
                        if (move != KillMove.Chainsaw)
                        {
                            Vector3 sd = (victim.transform.position - transform.position).normalized;
                            BloodFx.Spray(chest, (sd + Vector3.up * 0.3f).normalized, 2f);
                            if (IsOwner || victim.IsOwner) GameHud.Kick();
                        }
                        int cut = ev[i].SawSlot >= 0 && bca != null ? bca.SawFinisherLimb(ev[i].SawSlot) : ev[i].Severs;
                        if (cut != 0) victim.SeverLocal(cut, transform.position);
                        break;
                    }
                    default:
                    {
                        bool slash = ev[i].Kind == KillHitKind.Slash;
                        GameAudio.PlayAt(GameSfx.KnifeSwing, chest, slash ? 1f : 0.6f);
                        GameAudio.PlayAt(GameSfx.KnifeStab, chest);
                        bool loud = firstBlade || slash;
                        firstBlade = false;
                        GameAudio.PlayAttached(loud ? GameSfx.HiderScream : GameSfx.HiderPain, victim.transform, loud ? 1f : 0.8f);
                        Vector3 dir = (victim.transform.position - transform.position).normalized;
                        BloodFx.Spray(slash ? victim.ChestPosition + Vector3.up * 0.25f : chest, dir, slash ? 1.4f : 1f);
                        if (slash) BloodFx.Spray(victim.ChestPosition + Vector3.up * 0.2f, Vector3.Cross(Vector3.up, dir));
                        if (victim.IsOwner) GameHud.Hurt(slash ? 1f : 0.8f);
                        if (IsOwner || victim.IsOwner) GameHud.Kick();
                        break;
                    }
                }
            }
        }

        /// <summary>Muzzle flash, tracer and gunshot sounds of a finisher shot; returns the shot origin.</summary>
        private Vector3 GlockShotFx(Vector3 target)
        {
            Transform muzzle = Actions?.MuzzlePoint;
            Vector3 from = muzzle != null ? muzzle.position : EyePosition + transform.forward * 0.5f;
            if (muzzle != null) MuzzleFlash.Spawn(muzzle, false);
            ShotFx.Play(from, target, muzzle, false, muzzle == null);
            GameAudio.PlayAt(GameSfx.GlockShot, from, 1f);
            GameAudio.PlayAt(GameSfx.GlockShotDistant, from, 0.8f);
            GameAudio.PlayAt(GameSfx.ShellCasing, transform.position + transform.right * 0.4f, 0.5f);
            return from;
        }

        /// <summary>Body fall and the finisher taunts (independent of the hit marks).</summary>
        private IEnumerator KillMarks(NetPlayer victim, KillMove move, bool showAll)
        {
            float t = 0f;
            float fall = KillMoves.FallTime(move);
            yield return new WaitForSeconds(fall);
            t = fall;
            if (victim != null) GameAudio.PlayAt(GameSfx.BodyFall, victim.transform.position);

            if (move == KillMove.Chainsaw && showAll)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, KillMoves.CoreStart(move) + 3.0f - t));
                GameAudio.Play2D(GameSfx.WinSting, 0.9f);
            }
            if (move == KillMove.GlockFinisher)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, KillMoves.GlockFinisherTauntTime + 0.3f - t));
                GameAudio.PlayAt(GameSfx.GlockEquip, transform.position + Vector3.up * 1.5f, 0.6f);
                GameAudio.Play2D(GameSfx.WinSting, 0.9f);
                var mz = Actions?.MuzzlePoint;
                if (mz != null) DustFx.Puff(mz.position, Vector3.up, 10, 0.25f, 0.05f);
            }
            if (move == KillMove.SawFinisher)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, KillMoves.SawFinisherTauntTime - t));
                GameAudio.Play2D(GameSfx.WinSting, 0.9f);
            }
            if (move == KillMove.Finisher)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, KillMoves.FinisherFlipTime - t));
                GameAudio.PlayAt(GameSfx.KnifeSwing, transform.position + Vector3.up * 1.2f, 0.7f);
                yield return new WaitForSeconds(KillMoves.FinisherTauntTime - KillMoves.FinisherFlipTime);
                GameAudio.Play2D(GameSfx.WinSting, 0.9f);
            }
        }

        /// <summary>Time window (s from the start of the sequence) in which blood keeps spraying while the saw works.</summary>
        private static bool GoreWindow(KillMove move, out float start, out float end)
        {
            float c = KillMoves.CoreStart(move);
            switch (move)
            {
                case KillMove.Chainsaw: start = KillMoves.ChainsawGrindStart; end = KillMoves.ChainsawGrindEnd + 0.3f; return true;
                case KillMove.SawChop: start = c + 0.85f; end = c + 1.8f; return true;
                case KillMove.SawUpper: start = c + 0.65f; end = c + 1.75f; return true;
                case KillMove.SawNeck: start = c + 0.85f; end = c + 1.4f; return true;
                default: start = end = 0f; return false;
            }
        }
    }
}
