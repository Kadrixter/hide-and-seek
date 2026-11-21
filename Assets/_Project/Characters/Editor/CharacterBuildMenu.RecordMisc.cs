using System;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>Non-kill clips of <see cref="CharacterBuildMenu.RecordAnimations"/> (locomotion, healing, reload, saw, ...).</summary>
    public static partial class CharacterBuildMenu
    {
        /// <summary>Ticks the animators for <paramref name="seconds"/>; <paramref name="each"/>(t) runs before every tick.</summary>
        static void RecRun(Clip clip, float seconds, Action<float> each, Action<float> aim, params BlockyCharacterAnimator[] anims)
        {
            for (float t = 0f; t < seconds; t += RecDt)
            {
                each?.Invoke(t);
                foreach (var a in anims) a.Tick(RecDt);
                Physics.Simulate(RecDt);
                aim(t);
                clip.Shot(recCam);
            }
        }

        static void SetMove(PreviewMotion m, float speed, bool run = false, float crouch = 0f)
        {
            m.HorizontalSpeed = speed;
            m.Velocity = new Vector3(0f, 0f, speed);
            m.IsRunning = run;
            m.IsCrouching = crouch > 0.5f;
            m.CrouchAmount = crouch;
        }

        static void RecordMisc(string item)
        {
            var clip = NewClip(item);
            Vector3 body = new Vector3(0f, 0.85f, 0f);
            switch (item)
            {
                case "walk":
                case "run":
                case "crouch":
                case "limp":
                case "prone":
                {
                    var c = SpawnAt(2, Vector3.zero, 0f, out var m, out var a);
                    if (item == "limp") c.Actions.SetLimp(1f);
                    float sp = item == "run" ? 4.6f : item == "crouch" ? 1.4f : item == "prone" ? 0.95f : item == "limp" ? 1.9f : 2.2f;
                    TickAll(0.4f, a);
                    RecRun(clip, 4.5f, t =>
                    {
                        float on = t > 0.6f && t < 3.8f ? 1f : 0f;
                        SetMove(m, sp * on, item == "run", item == "crouch" ? 1f : 0f);
                        if (item == "prone") c.Actions.SetProneAmount(Mathf.Clamp01(t / 0.5f) * (1f - Mathf.Clamp01((t - 3.9f) / 0.5f)));
                    }, t => Orbit(item == "prone" ? new Vector3(0f, 0.35f, 0f) : body, 90f - 40f * (t / 4.5f), 3.4f, 0.4f), a);
                    Kill(c);
                    break;
                }
                case "hop":
                {
                    var c = SpawnAt(2, Vector3.zero, 0f, out var m, out var a);
                    c.SetSevered(Limb.LegR);
                    RecStumps(c, Limb.LegR);
                    c.Actions.SetHop(0);
                    TickAll(0.4f, a);
                    RecRun(clip, 5f, t => SetMove(m, t > 0.8f && t < 4f ? 1f : 0f), t => Orbit(new Vector3(0f, 0.75f, 0f), 150f - 120f * (t / 5f), 3.4f, 0.15f), a);
                    Kill(c);
                    break;
                }
                case "sawleg":
                {
                    var s = SpawnAt(1, Vector3.zero, 0f, out _, out var sa);
                    var v = SpawnAt(3, new Vector3(0f, 0f, ActionTimeline.KnifeAttackDistance), 180f, out var vm, out var va);
                    s.Actions.SetHeldItem(HeldItem.Chainsaw);
                    TickAll(1.6f, sa, va);
                    bool cut = false;
                    RecRun(clip, 4.5f, t =>
                    {
                        if (t >= 0.3f && t < 0.3f + RecDt) { s.Actions.SetRevving(true); s.Actions.PlaySawSlash(v, Limb.LegR); }
                        if (!cut && t >= 0.3f + BlockyCharacterActions.SawSlashHitTime)
                        {
                            cut = true;
                            if (v.Sever(Limb.LegR, new Vector3(0.4f, 1.2f, -1.2f))) RecStumps(v, Limb.LegR);
                            v.Actions.PlayHitReaction();
                            v.Actions.SetHop(0);
                        }
                        if (t > 1.4f) s.Actions.SetRevving(false);
                        SetMove(vm, t > 1.8f && t < 4.2f ? 0.9f : 0f);
                    }, t => Orbit(new Vector3(0f, 0.6f, 0.45f), 70f + 60f * (t / 4.5f), 3.0f, 0.35f), sa, va);
                    Kill(s);
                    Kill(v);
                    break;
                }
                case "downed":
                case "dead":
                {
                    var c = SpawnAt(2, Vector3.zero, 0f, out var m, out var a);
                    TickAll(0.4f, a);
                    RecRun(clip, item == "dead" ? 3f : 6f, t =>
                    {
                        if (t >= 0.3f && t < 0.3f + RecDt) c.Actions.SetLifeState(item == "dead" ? LifeState.Dead : LifeState.Downed);
                        if (item == "downed") SetMove(m, t > 2.2f && t < 5f ? 0.7f : 0f);
                    }, t => Orbit(new Vector3(0f, 0.4f, -0.3f), 70f + 30f * (t / 6f), 3.2f, 0.7f), a);
                    Kill(c);
                    break;
                }
                case "getup":
                {
                    var c = SpawnAt(2, Vector3.zero, 0f, out _, out var a);
                    c.Actions.SetLifeStateImmediate(LifeState.Downed);
                    TickAll(0.6f, a);
                    RecRun(clip, 2.6f, t => { if (t >= 0.3f && t < 0.3f + RecDt) c.Actions.SetLifeState(LifeState.Alive); },
                        t => Orbit(new Vector3(0f, 0.6f, -0.3f), 80f, 3.2f, 0.5f), a);
                    Kill(c);
                    break;
                }
                case "revive":
                {
                    var p = SpawnAt(4, Vector3.zero, 0f, out _, out var pa);
                    p.Actions.SetLifeStateImmediate(LifeState.Downed);
                    var md = SpawnAt(2, new Vector3(-0.45f, 0f, -0.3f), 90f, out _, out var ma);
                    md.Actions.SetHeldItem(HeldItem.Medkit);
                    TickAll(0.5f, pa, ma);
                    RecRun(clip, 5.2f, t =>
                    {
                        if (t >= 0.3f && t < 0.3f + RecDt) md.Actions.SetReviving(true);
                        if (t >= 3.6f && t < 3.6f + RecDt) { md.Actions.SetReviving(false); p.Actions.SetLifeState(LifeState.Alive); }
                    }, t => Orbit(new Vector3(-0.2f, 0.5f, -0.3f), 110f - 40f * (t / 5.2f), 3.4f, 0.55f), pa, ma);
                    Kill(p);
                    Kill(md);
                    break;
                }
                case "inject":
                {
                    var helper = SpawnAt(1, Vector3.zero, 0f, out _, out var ha);
                    var dead = SpawnAt(3, new Vector3(0.15f, 0f, 0.78f), 90f, out _, out var da);
                    dead.Actions.SetLifeStateImmediate(LifeState.Dead);
                    TickAll(0.5f, ha, da);
                    RecRun(clip, 7f, t =>
                    {
                        if (t < HealTimeline.InjectDuration) helper.Actions.SetHoldAnimation(HoldAnim.Inject, t, dead.Actions.ChestPoint.position);
                        else if (t < HealTimeline.InjectDuration + RecDt)
                        {
                            helper.Actions.SetHoldAnimation(HoldAnim.None, 0f);
                            dead.Actions.PrepareResurrectGasp();
                            dead.Actions.SetLifeState(LifeState.Alive);
                        }
                        if (Mathf.Abs(t - HealTimeline.InjectStab) < RecDt * 0.5f) dead.Actions.PlayHitReaction();
                    }, t => Orbit(new Vector3(0.05f, 0.5f, 0.45f), 75f - 35f * (t / 7f), 2.7f, 0.5f), ha, da);
                    Kill(helper);
                    Kill(dead);
                    break;
                }
                case "selfheal":
                {
                    var c = SpawnAt(2, Vector3.zero, 0f, out _, out var a);
                    c.Actions.SetLifeStateImmediate(LifeState.Downed);
                    TickAll(0.6f, a);
                    RecRun(clip, 6.6f, t =>
                    {
                        if (t < HealTimeline.SelfHealDuration) c.Actions.SetHoldAnimation(HoldAnim.SelfHeal, t);
                        else if (t < HealTimeline.SelfHealDuration + RecDt)
                        {
                            c.Actions.SetBandaged(1f);
                            c.Actions.SetLifeState(LifeState.Alive);
                            c.Actions.SetHoldAnimation(HoldAnim.None, 0f);
                        }
                    }, t => Orbit(new Vector3(0f, 0.4f, -0.35f), 60f - 30f * (t / 6.6f), 2.6f, 0.6f), a);
                    Kill(c);
                    break;
                }
                case "heal_self":
                case "heal_other":
                    RecordHeal(clip, item == "heal_other");
                    break;
                case "fpp_heal_self":
                case "fpp_heal_other":
                case "fpp_inject":
                    RecordFppHold(clip, item);
                    break;
                case "hit":
                {
                    var c = SpawnAt(2, Vector3.zero, 0f, out var m, out var a);
                    TickAll(0.4f, a);
                    RecRun(clip, 4.4f, t =>
                    {
                        if (Mathf.Repeat(t, 1.1f) < RecDt) c.Actions.PlayHitReaction();
                        SetMove(m, t > 2.2f ? 2.2f : 0f);
                    }, t => Orbit(body, 50f, 3.0f, 0.4f), a);
                    Kill(c);
                    break;
                }
                case "reload":
                {
                    var c = SpawnAt(0, Vector3.zero, 0f, out _, out var a);
                    c.Actions.SetHeldItem(HeldItem.Glock);
                    TickAll(0.8f, a);
                    RecRun(clip, 4.4f, t =>
                    {
                        if (t >= 0.4f && t < 0.4f + RecDt) c.Actions.PlayShoot();
                        if (t >= 1.2f && t < 1.2f + RecDt) c.Actions.PlayReload();
                    }, t => Orbit(new Vector3(0f, 1.05f, 0.15f), 35f, 2.1f, 0.35f, 32f), a);
                    Kill(c);
                    break;
                }
                case "saw":
                {
                    var c = SpawnAt(1, Vector3.zero, 0f, out var m, out var a);
                    TickAll(0.4f, a);
                    RecRun(clip, 6f, t =>
                    {
                        if (t >= 0.2f && t < 0.2f + RecDt) c.Actions.SetHeldItem(HeldItem.Chainsaw);
                        c.Actions.SetRevving(t > 2.6f && t < 4.2f);
                        SetMove(m, t > 4.6f ? 2.2f : 0f);
                    }, t => Orbit(new Vector3(0f, 0.9f, 0.2f), 60f - 20f * (t / 6f), 2.6f, 0.35f), a);
                    Kill(c);
                    break;
                }
                default:
                    Debug.LogWarning("[Rec] unknown clip " + item);
                    break;
            }
            ClearGibs();
        }
    }
}
