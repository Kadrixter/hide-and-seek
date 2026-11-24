using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    public sealed partial class OutroDirector
    {
        sealed class Fixture
        {
            public Light Light;
            public Material Tube;
            public float OnAt, Peak;
        }

        readonly List<Fixture> _fixtures = new();
        Light _key, _rim, _dim;
        Vector3 _camPos;
        float _camFov = 48f;
        bool _camInit;

        void BuildStage()
        {
            var housing = CineKit.Mat("OutroLampHousing", new Color(0.16f, 0.17f, 0.18f), 0.6f, 0.4f);
            var root = CineKit.Node("Stage", transform, Vector3.zero);
            root.SetPositionAndRotation(_gate.transform.position, _gate.transform.rotation);
            int k = 0;
            foreach (float z in new[] { 1.0f, 4.4f, 7.6f })
            foreach (float x in new[] { -3.4f, 0f, 3.4f })
            {
                var f = new Fixture { Tube = CineKit.Glow("OutroTube", new Color(0.92f, 0.96f, 1f), 0f) };
                var at = new Vector3(x, 2.93f, z);
                CineKit.Box("Housing", root, at + Vector3.up * 0.03f, new Vector3(1.32f, 0.05f, 0.2f), housing, 0.01f, null, false);
                CineKit.Box("Tube", root, at - Vector3.up * 0.005f, new Vector3(1.22f, 0.035f, 0.06f), f.Tube, 0.015f, null, false);
                f.Light = CineKit.Node("TubeLight", root, at + Vector3.down * 0.12f).gameObject.AddComponent<Light>();
                f.Light.type = LightType.Point;
                f.Light.range = z > 6f ? 8f : 7f;
                f.Light.color = new Color(0.9f, 0.95f, 1f);
                f.Light.shadows = LightShadows.None;
                f.Light.intensity = 0f;
                f.Peak = z > 6f ? 0.8f : 1.15f;
                // Front row first (closest to the gate), from the middle out: one after another like a real hall.
                f.OnAt = OutroTimeline.Lights + (k % 3 == 1 ? 0 : 1 + (k % 3) / 2) * OutroTimeline.LightStep + (k / 3) * OutroTimeline.LightStep * 2.2f;
                _fixtures.Add(f);
                k++;
            }
            _key = NewLight(root, "OutroKey", LightType.Spot, new Vector3(1.8f, 2.85f, 7.8f), new Vector3(0f, 1.0f, RowZ), new Color(1f, 0.95f, 0.88f), 60f, 13f, LightShadows.Soft);
            _rim = NewLight(root, "OutroRim", LightType.Spot, new Vector3(-0.5f, 2.8f, 0.45f), new Vector3(0f, 1.2f, RowZ + 1.2f), new Color(0.72f, 0.88f, 1f), 85f, 6f, LightShadows.None);
            _dim = NewLight(root, "OutroDim", LightType.Point, new Vector3(0f, 2.2f, 4.5f), Vector3.zero, new Color(0.6f, 0.65f, 0.8f), 0f, 9f, LightShadows.None);
        }

        static Light NewLight(Transform root, string n, LightType type, Vector3 at, Vector3 aim, Color c, float angle, float range, LightShadows sh)
        {
            var l = CineKit.Node(n, root, at).gameObject.AddComponent<Light>();
            l.type = type;
            l.color = c;
            l.range = range;
            l.shadows = sh;
            l.intensity = 0f;
            if (type == LightType.Spot)
            {
                l.spotAngle = angle;
                l.transform.localRotation = Quaternion.LookRotation(aim - at);
            }
            return l;
        }

        void UpdateStage(float t)
        {
            foreach (var f in _fixtures)
            {
                float a = t - f.OnAt;
                float on;
                if (a < 0f) on = 0f;
                else if (a < 0.22f) on = Mathf.PerlinNoise(a * 60f, f.OnAt * 10f) > 0.5f ? 0.9f : 0.05f; // tube starter flicker
                else on = 1f;
                f.Light.intensity = f.Peak * on;
                CineKit.SetGlow(f.Tube, new Color(0.92f, 0.96f, 1f), 2.4f * on);
            }
            float lit = CineKit.S(t, OutroTimeline.Lights + 0.1f, OutroTimeline.Lights + 1.0f);
            _key.intensity = 1.5f * lit;
            _rim.intensity = 1.3f * lit;
            _dim.intensity = 0.12f + 0.25f * lit;
        }

        void Events(float from, float to)
        {
            bool At(float x) => from < x && to >= x && to - x < 0.6f;
            if (At(OutroTimeline.StageIn) || (_cam == null && to >= OutroTimeline.StageIn)) StartCamera();
            if (At(OutroTimeline.Lights - 0.18f)) Snd(CineSounds.Breaker, 0.85f, OutroTimeline.Lights - 0.18f); // the thunk is 0.15 s into the clip
            foreach (var f in _fixtures)
                if (At(f.OnAt)) Snd(CineSounds.Tick, 0.18f, f.OnAt);
            if (At(OutroTimeline.TitleIn)) Snd(CineSounds.Whoosh, 0.5f, OutroTimeline.TitleIn);
            int cards = _pack.Awards?.Length ?? 0;
            for (int i = 0; i < cards; i++)
            {
                float at = OutroTimeline.Awards + 0.4f + i * OutroTimeline.CardStep;
                if (At(at)) Snd(CineSounds.Tick, 0.55f, at);
            }
        }

        void StartCamera()
        {
            if (_cam != null) return;
            HudHider.KeepEmotes = _localWinner; // before the camera hides the HUD
            _cam = CineCamera.Create("OutroCamera", transform);
            _cam.Fade = 1f;
            AttachOverlay();
            UpdateCamera(_t);
        }

        void Snd(AudioClip c, float vol, float at)
        {
            CineSounds.Play(c, vol);
            CineRecorder.LogSound(c, vol, at);
        }
    }
}
