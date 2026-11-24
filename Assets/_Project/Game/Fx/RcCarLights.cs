using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lights of one RC car (2026-11-24, replaces the single spot): low beams from the headlamps, a long narrow beam
    /// from the 4-lamp pod, a small red / white glow at the back. The lamp lenses of the rally body glow per car
    /// (MaterialPropertyBlock on the body renderer): headlamps and fog lamps when the lights are on, tail lamps dim with
    /// the lights and bright when braking, reversing lamps when going backwards, amber parking lights. The Yaris skin
    /// keeps only the real lights (its renderer is not a rally one).
    /// </summary>
    public sealed class RcCarLights
    {
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly Color HeadOff = new Color(0.3f, 0.3f, 0.26f), HeadOn = new Color(2.4f, 2.3f, 1.95f);
        static readonly Color TailOff = new Color(0.25f, 0.01f, 0.01f), TailOn = new Color(0.9f, 0.04f, 0.03f), TailBrake = new Color(2.8f, 0.12f, 0.08f);
        static readonly Color RevOff = new Color(0.05f, 0.05f, 0.05f), RevOn = new Color(2.2f, 2.2f, 2.1f);
        static readonly Color AmberOff = new Color(0.22f, 0.1f, 0.01f), AmberOn = new Color(0.75f, 0.33f, 0.03f);

        readonly RcCarParts _parts;
        readonly Light _low, _pod, _tail;
        MaterialPropertyBlock _mpb;
        int _state = -1;
        float _brakeHold;

        RcCarLights(RcCarParts parts, Light low, Light pod, Light tail)
        {
            _parts = parts;
            _low = low;
            _pod = pod;
            _tail = tail;
        }

        public static RcCarLights Build(RcCarParts parts)
        {
            Light Spot(string name, Vector3 at, float pitch, float angle, float range, float intensity)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parts.Root, false);
                go.transform.localPosition = at;
                go.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = angle;
                l.innerSpotAngle = angle * 0.45f;
                l.range = range;
                l.intensity = intensity;
                l.color = new Color(1f, 0.93f, 0.78f);
                l.shadows = LightShadows.None;
                l.enabled = false;
                return l;
            }
            var low = Spot("Headlights", RallyModel.HeadlightPos, 7f, 80f, 7.5f, 1.8f);
            var pod = Spot("LightPod", RallyModel.PodPos, 2.5f, 38f, 13f, 2.4f);
            pod.color = new Color(1f, 0.97f, 0.9f);
            var tg = new GameObject("TailGlow");
            tg.transform.SetParent(parts.Root, false);
            tg.transform.localPosition = RallyModel.TailPos;
            var tail = tg.AddComponent<Light>();
            tail.type = LightType.Point;
            tail.range = 0.9f;
            tail.shadows = LightShadows.None;
            tail.enabled = false;
            return new RcCarLights(parts, low, pod, tail);
        }

        /// <summary>Every frame while the car exists.</summary>
        public void Tick(bool moving, bool lightsOn, float speed, float throttle)
        {
            bool on = moving && lightsOn;
            bool reverse = moving && (speed < -0.1f || (throttle < -0.05f && speed < 0.15f));
            bool brake = moving && !reverse && throttle * speed < -0.05f;
            // Brake lights stay on a moment (no flicker on a tap).
            _brakeHold = brake ? 0.25f : Mathf.Max(0f, _brakeHold - Time.deltaTime);
            brake = _brakeHold > 0f;
            Set(_low, on);
            Set(_pod, on);
            bool tail = moving && (on || brake || reverse);
            Set(_tail, tail);
            if (tail)
            {
                _tail.color = reverse ? new Color(1f, 0.95f, 0.9f) : new Color(1f, 0.08f, 0.05f);
                _tail.intensity = reverse ? 0.9f : brake ? 1.1f : 0.35f;
            }
            Glow(on, brake, reverse);
        }

        public void Off()
        {
            Set(_low, false);
            Set(_pod, false);
            Set(_tail, false);
            _brakeHold = 0f;
            Glow(false, false, false);
        }

        static void Set(Light l, bool on)
        {
            if (l != null && l.enabled != on) l.enabled = on;
        }

        /// <summary>Lens glow of the rally body (only when the state changes).</summary>
        void Glow(bool on, bool brake, bool reverse)
        {
            int state = (on ? 1 : 0) | (brake ? 2 : 0) | (reverse ? 4 : 0);
            if (state == _state) return;
            _state = state;
            var r = _parts.Body != null ? _parts.Body.GetComponent<Renderer>() : null;
            if (r == null || !RallyModel.IsRallyRenderer(r)) return;
            _mpb ??= new MaterialPropertyBlock();
            Apply(r, RallyModel.MHead, on ? HeadOn : HeadOff);
            Apply(r, RallyModel.MTail, brake ? TailBrake : on ? TailOn : TailOff);
            Apply(r, RallyModel.MRev, reverse ? RevOn : RevOff);
            Apply(r, RallyModel.MAmber, on ? AmberOn : AmberOff);
        }

        void Apply(Renderer r, int slot, Color c)
        {
            r.GetPropertyBlock(_mpb, slot);
            _mpb.SetColor(Emission, c);
            r.SetPropertyBlock(_mpb, slot);
        }
    }
}
