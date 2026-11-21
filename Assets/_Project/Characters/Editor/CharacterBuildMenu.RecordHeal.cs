using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>Medkit heal clips (third person and first person) of <see cref="CharacterBuildMenu.RecordAnimations"/>.</summary>
    public static partial class CharacterBuildMenu
    {
        static void RecordHeal(Clip clip, bool other)
        {
            float dur = HealTimeline.MedkitDuration;
            var h = SpawnAt(1, Vector3.zero, 0f, out _, out var ha);
            BlockyCharacter pt = null;
            BlockyCharacterAnimator pa = null;
            if (other)
            {
                pt = SpawnAt(3, new Vector3(0.05f, 0f, 0.95f), 180f, out _, out pa);
                pt.Actions.SetLimp(1f);
            }
            else h.Actions.SetLimp(1f);
            var anims = other ? new[] { ha, pa } : new[] { ha };
            TickAll(0.6f, anims);
            RecRun(clip, dur + 1.4f, t =>
            {
                float ht = t - 0.3f;
                if (ht >= 0f && ht < dur)
                {
                    h.Actions.SetHoldAnimation(other ? HoldAnim.HealOther : HoldAnim.HealSelf, ht, other ? pt.Actions.ChestPoint.position : (Vector3?)null);
                    if (other) pt.Actions.SetPatient(ht);
                }
                else if (ht >= dur && ht < dur + RecDt)
                {
                    h.Actions.SetHoldAnimation(HoldAnim.None, 0f);
                    (other ? pt : h).Actions.SetLimp(0f);
                    (other ? pt : h).Actions.SetBandaged(1f);
                }
            }, t => Orbit(other ? new Vector3(0f, 0.8f, 0.45f) : new Vector3(0f, 0.75f, 0.1f), other ? 100f - 35f * (t / dur) : 55f - 25f * (t / dur), other ? 3.4f : 3.1f, 0.25f), anims);
            Kill(h);
            if (pt != null) Kill(pt);
        }

        /// <summary>First-person view of a hold animation (medkit on the own wound / someone else's, adrenaline).</summary>
        static void RecordFppHold(Clip clip, string item)
        {
            bool inject = item == "fpp_inject", other = item == "fpp_heal_other";
            float eye = 1.05f;
            var eyeGo = new GameObject("FppRecCamera");
            var main = eyeGo.AddComponent<Camera>();
            ConfigureCamera(main);
            main.enabled = false;
            main.fieldOfView = 70f;
            var vm = FirstPersonViewModel.Create(main.transform, 0, null);
            vm.autoUpdate = false;
            var vmCam = vm.ViewModelCamera;
            vmCam.enabled = false;
            BlockyCharacter target = null;
            BlockyCharacterAnimator ta = null;
            if (inject)
            {
                target = SpawnAt(3, new Vector3(0.15f, 0f, 0.78f), 90f, out _, out ta);
                target.Actions.SetLifeStateImmediate(LifeState.Dead);
            }
            else if (other)
            {
                target = SpawnAt(3, new Vector3(0.05f, 0f, 1.0f), 180f, out _, out ta);
                target.Actions.SetLimp(1f);
            }
            if (ta != null) TickAll(0.5f, ta);
            float dur = inject ? HealTimeline.InjectDuration : HealTimeline.MedkitDuration;
            var mode = inject ? HoldAnim.Inject : other ? HoldAnim.HealOther : HoldAnim.HealSelf;
            float pitch = inject ? 38f : other ? 12f : 52f;
            int saveW = recW, saveH = recH;
            for (float t = 0f; t < dur + 0.8f; t += RecDt)
            {
                float ht = t - 0.3f;
                Vector3? tgt = target != null ? target.Actions.ChestPoint.position : (Vector3?)null;
                if (ht >= 0f && ht < dur)
                {
                    vm.SetHoldAnimation(mode, ht, tgt);
                    if (other) target.Actions.SetPatient(ht);
                }
                else vm.SetHoldAnimation(HoldAnim.None, 0f);
                main.transform.SetPositionAndRotation(new Vector3(0f, eye, 0f), Quaternion.Euler(pitch, 0f, 0f));
                vm.Tick(RecDt);
                if (ta != null) ta.Tick(RecDt);
                if (clip.Tick++ % recStep == 0)
                {
                    var tex = RenderFpp(main, vmCam, recW, recH);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(clip.Dir, $"f_{clip.Frame++:0000}.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
            }
            recW = saveW;
            recH = saveH;
            if (target != null) Kill(target);
            Object.DestroyImmediate(vm.gameObject);
            Object.DestroyImmediate(eyeGo);
        }
    }
}
