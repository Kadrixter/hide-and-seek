using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Clips of the cut-off leg (<see cref="CharacterBuildMenu.RecordAnimations"/>): "legthrow" (a hider winds up and
    /// throws a leg at the seeker, who is knocked out and gets up again), "stun" (the knock-out alone, side view),
    /// "fpp_legthrow" (the throw in first person).
    /// </summary>
    public static partial class CharacterBuildMenu
    {
        static bool RecordLeg(string item)
        {
            switch (item)
            {
                case "legthrow": RecLegThrow(NewClip(item)); return true;
                case "stun": RecStun(NewClip(item)); return true;
                case "fpp_legthrow": RecLegThrowFpp(NewClip(item)); return true;
                default: return false;
            }
        }

        static HandGrip RecLegGrip(BlockyCharacter owner) =>
            SeveredLegModel.MakeGrip(r => SeveredLegModel.Create(r, owner != null ? owner.LimbJoint(Limb.LegR).GetComponent<MeshRenderer>().sharedMaterial : null, null, false));

        static void RecLegThrow(Clip clip)
        {
            var h = SpawnAt(2, Vector3.zero, 0f, out var hm, out var ha);
            var s = SpawnAt(1, new Vector3(0f, 0f, 4.2f), 180f, out _, out var sa);
            s.Actions.SetHeldItem(HeldItem.Knife);
            var grip = RecLegGrip(s);
            h.Actions.SetCarried(1, grip);
            TickAll(1f, ha, sa);
            const float aimAt = 0.9f, throwAt = 2.2f;
            GameObject flying = null;
            Vector3 from = default, vel = default, spinAxis = Vector3.right;
            float flyT = 0f, hitT = -1f;
            Quaternion rot0 = Quaternion.identity;
            RecRun(clip, throwAt + 7.2f, t =>
            {
                SetMove(hm, t < 0.7f ? 1.2f : 0f);
                if (t >= aimAt && t < throwAt) h.Actions.SetLegAim(true, LegThrowTimeline.Power(t - aimAt));
                if (t >= throwAt && t < throwAt + RecDt) h.Actions.PlayLegThrow(1f);
                if (t >= throwAt + LegThrowTimeline.Release && flying == null && hitT < 0f)
                {
                    // The world leg takes over from the fist (the game: a dropped item spawned by the server).
                    var sock = h.Actions.CarrySocket != null ? h.Actions.CarrySocket : h.transform;
                    h.Actions.SetCarried(0, null);
                    flying = SeveredLegModel.Create(null, s.LimbJoint(Limb.LegR).GetComponent<MeshRenderer>().sharedMaterial, null, false);
                    rot0 = sock.rotation;
                    from = sock.position + sock.rotation * (-SeveredLegModel.AnkleGrip);
                    Vector3 head = s.Head.position + Vector3.up * 0.2f;
                    flyT = 0f;
                    float dur = 0.32f;
                    vel = (head - from) / dur - 0.5f * Physics.gravity * dur;
                    spinAxis = h.transform.right;
                }
                if (flying != null)
                {
                    flyT += RecDt;
                    Vector3 pos = from + vel * flyT + 0.5f * Physics.gravity * flyT * flyT;
                    flying.transform.SetPositionAndRotation(pos, Quaternion.AngleAxis(-900f * flyT, spinAxis) * rot0);
                    if (hitT < 0f && Vector3.Distance(pos, s.Head.position + Vector3.up * 0.2f) < 0.3f)
                    {
                        hitT = t;
                        vel = new Vector3(0.6f, 1.2f, -1.4f);
                        from = pos;
                        flyT = 0f;
                    }
                    if (hitT >= 0f && pos.y < 0.12f)
                    {
                        // Lands and lies (the game: the physical dropped item).
                        flying.transform.SetPositionAndRotation(new Vector3(pos.x, 0.12f, pos.z), Quaternion.Euler(0f, 35f, 90f));
                        Object.DestroyImmediate(flying.GetComponent<Rigidbody>());
                        var landed = flying;
                        flying = null;
                        landed.name = "LandedLeg";
                    }
                }
                s.Actions.SetStun(hitT >= 0f ? t - hitT : -1f, 3.3f);
            }, t => Orbit(new Vector3(0f, 0.85f, 2.1f), 78f + 22f * (t / 9f), 4.4f, 0.4f, 40f), ha, sa);
            if (flying != null) Object.DestroyImmediate(flying);
            var rest = GameObject.Find("LandedLeg");
            if (rest != null) Object.DestroyImmediate(rest);
            Kill(h);
            Kill(s);
        }

        static void RecStun(Clip clip)
        {
            var s = SpawnAt(1, Vector3.zero, 0f, out _, out var sa);
            s.Actions.SetHeldItem(HeldItem.Knife);
            TickAll(0.8f, sa);
            RecRun(clip, StunTimeline.Duration + 1f, t => s.Actions.SetStun(t - 0.3f, 1.7f),
                t => Orbit(new Vector3(0f, 0.8f, -0.3f), 70f, 4.2f, 0.3f), sa);
            Kill(s);
        }

        static void RecLegThrowFpp(Clip clip)
        {
            var eyeGo = new GameObject("FppRecCamera");
            var main = eyeGo.AddComponent<Camera>();
            ConfigureCamera(main);
            main.enabled = false;
            main.fieldOfView = 70f;
            var vm = FirstPersonViewModel.Create(main.transform, 0, null);
            vm.autoUpdate = false;
            var vmCam = vm.ViewModelCamera;
            vmCam.enabled = false;
            var s = SpawnAt(1, new Vector3(0f, 0f, 4.5f), 180f, out _, out var sa);
            vm.SetCarried(1, RecLegGrip(s));
            TickAll(0.5f, sa);
            for (float t = 0f; t < 3.4f; t += RecDt)
            {
                if (t >= 0.8f && t < 2f) vm.SetLegAim(true, LegThrowTimeline.Power(t - 0.8f));
                if (t >= 2f && t < 2f + RecDt) vm.PlayLegThrow(1f);
                if (t >= 2f + LegThrowTimeline.Release + 0.1f) vm.SetCarried(0, null);
                main.transform.SetPositionAndRotation(new Vector3(0f, 1.62f, 0f), Quaternion.Euler(4f, 0f, 0f));
                vm.Tick(RecDt);
                sa.Tick(RecDt);
                if (clip.Tick++ % recStep == 0)
                {
                    var tex = RenderFpp(main, vmCam, recW, recH);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(clip.Dir, $"f_{clip.Frame++:0000}.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
            }
            Kill(s);
            Object.DestroyImmediate(vm.gameObject);
            Object.DestroyImmediate(eyeGo);
        }
    }
}
