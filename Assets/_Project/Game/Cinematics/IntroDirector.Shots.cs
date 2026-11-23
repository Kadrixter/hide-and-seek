using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    public sealed partial class IntroDirector
    {
        /// <summary>Where the laugh is filmed from: low, in front of the killer, towards the garage.</summary>
        Vector3 LaughCamPos => _gate.Local(new Vector3(ExitGate.BoxPos.x + 0.75f, 0.52f, 2.55f));

        void UpdateCamera(float t)
        {
            Vector3 pos, look;
            float fov;
            float kx = ExitGate.BoxPos.x;
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
                // B: close on the hand and the small lever, from the killer's left, a slow creep in.
                float k = Mathf.Clamp01((t - IntroTimeline.ShotB) / (IntroTimeline.ShotC - IntroTimeline.ShotB));
                // Low, from the side of the lever, looking up a little: the hand comes into the frame from above.
                // From the killer's left, between him and the wall: the lever in profile, his hand coming in.
                pos = _gate.Local(Vector3.Lerp(new Vector3(kx + 1.08f, 1.2f, 0.44f), new Vector3(kx + 0.92f, 1.18f, 0.42f), k));
                Vector3 knob = _gate.KnobWorld;
                look = Vector3.Lerp(_gate.Local(new Vector3(kx - 0.05f, 1.18f, 0.12f)), knob, 0.55f);
                fov = 42f;
            }
            else if (t < IntroTimeline.ShotD)
            {
                // C: low angle on the laugh, pushing in a little.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - IntroTimeline.ShotC) / (IntroTimeline.ShotD - IntroTimeline.ShotC)));
                Vector3 head = _killer.Body.Head.position + Vector3.up * 0.05f;
                pos = Vector3.Lerp(LaughCamPos, Vector3.Lerp(LaughCamPos, head, 0.22f), k);
                look = head + Vector3.down * 0.12f;
                fov = Mathf.Lerp(42f, 38f, k);
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
            _key.transform.SetPositionAndRotation(keyFrom, Quaternion.LookRotation(chest - keyFrom));
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
            if (At(IntroTimeline.PullEnd))
            {
                Snd(CineSounds.Clack, 0.95f, IntroTimeline.PullEnd);
                LeverSparks.Burst(_gate.KnobWorld + _gate.LocalDir(Vector3.down) * 0.1f, _gate.LocalDir(Vector3.forward));
            }
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
