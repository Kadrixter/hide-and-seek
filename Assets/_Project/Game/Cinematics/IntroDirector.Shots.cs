using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    public sealed partial class IntroDirector
    {
        /// <summary>Where the laugh is filmed from: low, in front of the killer, towards the garage.</summary>
        Vector3 LaughCamPos => _gate.Local(new Vector3(ExitGate.BoxPos.x + 0.75f, 0.7f, 3.15f));

        /// <summary>The killer's right fist (world), where the torn handle is.</summary>
        Vector3 FistWorld => _killer.Body.ElbowRight.TransformPoint(HideAndSeek.Characters.CharacterPose.HandLocal);

        void UpdateCamera(float t)
        {
            Vector3 pos, look;
            float fov;
            if (t < IntroTimeline.ShotB)
            {
                // A: wide, from the garage: the open gate with the street light, the killer by the box. Slow push in.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / IntroTimeline.ShotB));
                pos = _gate.Local(Vector3.Lerp(new Vector3(1.3f, 1.5f, 6.4f), new Vector3(0.3f, 1.42f, 5.0f), k));
                look = _gate.Local(Vector3.Lerp(new Vector3(-1.9f, 1.2f, 0f), new Vector3(-2.7f, 1.22f, 0.3f), k));
                fov = 44f;
            }
            else if (t < IntroTimeline.ShotC)
            {
                // B: along the wall from beyond the box (the side away from the killer): the lever and his arm in
                // profile (elbow dropped, not towards the lens), the grip, the yank down, forced past the stop - the
                // handle tears off (sparks from the stub) - then a step back and up to hold the fist with the handle
                // and his face turned to it.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - IntroTimeline.ShotB) / (IntroTimeline.Snap - IntroTimeline.ShotB)));
                float after = CineKit.S(t, IntroTimeline.Snap + 0.02f, IntroTimeline.ShotC);
                float lx = ExitGate.BoxPos.x + ExitGate.LeverPivotInBox.x;
                Vector3 head = _killer.Body.Head.position;
                Vector3 pivot = _gate.Local(new Vector3(lx, ExitGate.BoxPos.y + ExitGate.LeverPivotInBox.y, ExitGate.LeverPivotInBox.z));
                pos = _gate.Local(Vector3.Lerp(new Vector3(lx - 1.3f, 1.42f, 0.52f), new Vector3(lx - 1.15f, 1.4f, 0.5f), k));
                pos = Vector3.Lerp(pos, _gate.Local(new Vector3(lx - 1.5f, 1.55f, 0.8f)), after);
                look = Vector3.Lerp(pivot, head, 0.3f);
                look = Vector3.Lerp(look, Vector3.Lerp(FistWorld, head, 0.45f), after);
                fov = Mathf.Lerp(42f, 44f, after);
            }
            else if (t < IntroTimeline.ShotD)
            {
                // C: low angle on the laugh, the torn handle raised in his fist in the frame, pushing in a little.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - IntroTimeline.ShotC) / (IntroTimeline.ShotD - IntroTimeline.ShotC)));
                Vector3 head = _killer.Body.Head.position + Vector3.up * 0.05f;
                pos = Vector3.Lerp(LaughCamPos, Vector3.Lerp(LaughCamPos, head, 0.12f), k);
                look = Vector3.Lerp(head + Vector3.down * 0.2f, FistWorld, 0.3f);
                fov = Mathf.Lerp(46f, 42f, k);
            }
            else
            {
                // D / E: the gate, a step back after the slam.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - IntroTimeline.ShotD) / (IntroTimeline.Length - IntroTimeline.ShotD)));
                pos = _gate.Local(Vector3.Lerp(new Vector3(0.9f, 1.05f, 4.9f), new Vector3(1.2f, 1.25f, 6.0f), k));
                look = _gate.Local(new Vector3(-0.45f, 1.15f, 0f));
                fov = 54f;
            }
            _cam.LookAt(pos, look, fov);
            _cam.ShakeAmount = IntroTimeline.Shake(t);
            _cam.Bars = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((IntroTimeline.Length - t) / 0.45f);
            float fadeIn = 1f - Mathf.Clamp01(t / 0.45f);
            float fadeOut = Mathf.Clamp01((t - IntroTimeline.FadeOut) / (IntroTimeline.Length - IntroTimeline.FadeOut));
            _cam.Fade = Mathf.Max(fadeIn, fadeOut);

            float title = Mathf.Clamp01((t - IntroTimeline.TitleIn) / 0.3f) * (1f - Mathf.Clamp01((t - IntroTimeline.FadeOut - 0.1f) / 0.3f));
            _cam.TitleGroup.alpha = title;
            if (title > 0f && _cam.Title.text.Length == 0)
            {
                _cam.Title.text = "BRAMA ZAMKNIĘTA";
                _cam.Sub.text = _seeker ? "Nikt stąd nie wyjdzie." : "Schowaj się. Szybko.";
                if (_seeker) _cam.Sub.color = CineStyle.SeekerAccent;
            }
        }

        void UpdateLights(float t)
        {
            // Key follows the shot (from the camera side, aimed at the killer); dark after the cut to the gate.
            Vector3 chest = _killer.transform.position + Vector3.up * 1.3f;
            Vector3 keyFrom = t < IntroTimeline.ShotC ? _gate.Local(new Vector3(ExitGate.BoxPos.x + 1.6f, 2.4f, 2.6f)) : LaughCamPos + Vector3.up * 1.6f;
            Vector3 keyAt = chest;
            if (t >= IntroTimeline.ShotB && t < IntroTimeline.ShotC)
            {
                // Shot B is filmed from beyond the box: light the lever, the fist and the face from that side.
                keyFrom = _gate.Local(new Vector3(ExitGate.BoxPos.x - 0.9f, 2.3f, 1.5f));
                keyAt = Vector3.Lerp(_gate.KnobWorld, chest, 0.5f);
            }
            _key.transform.SetPositionAndRotation(keyFrom, Quaternion.LookRotation(keyAt - keyFrom));
            _key.intensity = t < IntroTimeline.ShotD ? (t < IntroTimeline.ShotC ? 1.4f : 1.8f) : 0.5f;
            _fill.transform.position = chest + _gate.LocalDir(new Vector3(-0.8f, 0.4f, 1.4f));
            // The overhead flickers after the slam.
            float flick = 1f;
            if (t > IntroTimeline.Impact && t < IntroTimeline.Impact + 0.9f)
                flick = Mathf.PerlinNoise(t * 23f, 0.3f) > 0.45f ? 1.25f : 0.08f;
            _overhead.intensity = 1.5f * flick;
            // The box lamp turns red on the click and blinks.
            if (t >= IntroTimeline.PullEnd) _gate.SetLamp(true, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 6f)));
        }

        /// <summary>One-off moments between the previous and this frame's time (skipped when joined late).</summary>
        void Events(float from, float to)
        {
            bool At(float x) => from < x && to >= x && to - x < 0.5f;
            if (At(0f)) Snd(CineSounds.Drone, 0.5f, 0f);
            if (At(IntroTimeline.PullEnd)) Snd(CineSounds.Clack, 0.95f, IntroTimeline.PullEnd);
            // The handle tears off: groan, crack, the stub spits sparks.
            if (At(IntroTimeline.Snap - CineSounds.SnapCrackInClip)) Snd(CineSounds.Snap, 1f, IntroTimeline.Snap - CineSounds.SnapCrackInClip);
            if (At(IntroTimeline.Snap)) StubSnapFx.Play(_gate.StubWorld, _gate.LocalDir(new Vector3(-0.55f, 0.35f, 0.75f)));
            if (At(IntroTimeline.LaughStart)) Snd(CineSounds.Laugh(_laughVariant), 1f, IntroTimeline.LaughStart);
            if (At(IntroTimeline.DropStart)) Snd(CineSounds.Rattle, 0.7f, IntroTimeline.DropStart);
            if (At(IntroTimeline.Impact - CineSounds.SlamHitInClip)) Snd(CineSounds.Slam, 1f, IntroTimeline.Impact - CineSounds.SlamHitInClip);
            if (At(IntroTimeline.Impact))
            {
                CineFx.DustBurst(_gate.Local(new Vector3(0f, 0f, 0.15f)), _gate.LocalDir(Vector3.right), _gate.LocalDir(Vector3.forward), ExitGate.HalfW);
                CineFx.ImpactSparks(_gate);
                CineFx.FlickerLamps(_gate.transform.position, 30f, 0.9f);
                CineFx.Flash(transform, _gate.Local(new Vector3(0f, 0.4f, 0.6f)), new Color(1f, 0.85f, 0.65f), 6f, 2.2f, 0.18f);
            }
            if (At(IntroTimeline.Impact + 0.12f)) Snd(CineSounds.Buzz, 0.3f, IntroTimeline.Impact + 0.12f);
        }

        void Snd(AudioClip c, float vol, float at)
        {
            CineSounds.Play(c, vol);
            CineRecorder.LogSound(c, vol, at);
        }
    }
}
