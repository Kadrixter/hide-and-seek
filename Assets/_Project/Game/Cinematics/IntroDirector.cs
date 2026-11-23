using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Plays the round intro on this client (<see cref="IntroTimeline"/>): a local puppet of the chosen killer with his
    /// skin pulls the small lever by the exit gate, laughs, and the shutter slams shut. Started by
    /// <c>MatchManager.Cinematics</c> from the server start time, so a late scene load joins in the right place.
    /// The clock runs on local frame time from that point (smooth when frames are recorded at a fixed rate).
    /// </summary>
    public sealed partial class IntroDirector : MonoBehaviour
    {
        public static IntroDirector Current { get; private set; }
        /// <summary>Tests: fixed frame time recording, no resync to the server clock.</summary>
        public static bool Recording;

        public float T => _t;
        public bool Done { get; private set; }

        ExitGate _gate;
        CineCamera _cam;
        CinePuppet _killer;
        System.Func<double> _clock;
        double _startAt;
        float _t, _prevT = -1f;
        int _laughVariant;
        bool _seeker;
        Light _key, _fill, _overhead;

        public static IntroDirector Play(double startAt, System.Func<double> serverClock, NetPlayer killer, int seed, bool localIsSeeker)
        {
            if (Current != null) Destroy(Current.gameObject);
            var go = new GameObject("IntroDirector");
            var d = go.AddComponent<IntroDirector>();
            d._startAt = startAt;
            d._clock = serverClock;
            d._t = (float)(serverClock() - startAt);
            d._laughVariant = seed & 1;
            d._seeker = localIsSeeker;
            Current = d;
            d.Begin(killer);
            return d;
        }

        void Begin(NetPlayer killer)
        {
            _gate = ExitGate.Ensure();
            _cam = CineCamera.Create("IntroCamera", transform);
            _cam.Fade = 1f;
            int layer = HideAndSeek.Map.LevelLighting.LayerOf(0);
            Vector3 stand = KillerStand;
            float yaw = YawOf(_gate.LocalDir(Vector3.back)); // facing the wall / the box
            _killer = killer != null
                ? CinePuppet.Of(killer, transform, stand, yaw, layer)
                : CinePuppet.Create("Puppet_Killer", transform, stand, yaw, 1, "", null, layer);
            _killer.Actions.SetHeldItem(HeldItem.Knife);
            _killer.Pose = KillerPose;
            _killer.PoseWeight = 1f;
            BuildLights();
            _gate.SetOpen(1f);
            _gate.SetLever(0f);
            _gate.SetLamp(false);
            Debug.Log($"[Cine] intro start t={_t:0.00} killer={(killer != null ? killer.Nick : "-")} seeker={_seeker}");
        }

        /// <summary>Killer's feet: in front of the box, a little to the right of the lever (his left hand reaches it).</summary>
        Vector3 KillerStand => _gate.Local(new Vector3(ExitGate.BoxPos.x - 0.16f, 0f, 0.6f));

        static float YawOf(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

        void BuildLights()
        {
            // Cold key from the garage side, a dim fill, and a hard overhead light over the gate (it flickers on the slam).
            _key = NewLight("IntroKey", LightType.Spot, new Color(0.78f, 0.86f, 1f), 1.6f, 9f, 55f, LightShadows.Soft);
            _fill = NewLight("IntroFill", LightType.Point, new Color(0.55f, 0.6f, 0.75f), 0.45f, 5f, 0f, LightShadows.None);
            _overhead = NewLight("IntroOverhead", LightType.Spot, new Color(0.92f, 0.95f, 1f), 1.5f, 8f, 95f, LightShadows.Soft);
            _overhead.transform.SetPositionAndRotation(_gate.Local(new Vector3(-0.6f, 2.95f, 2.4f)),
                Quaternion.LookRotation(_gate.Local(new Vector3(-0.4f, 0.6f, 0f)) - _gate.Local(new Vector3(-0.6f, 2.95f, 2.4f))));
        }

        Light NewLight(string n, LightType type, Color c, float intensity, float range, float angle, LightShadows shadows)
        {
            var l = new GameObject(n).AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.type = type;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            if (type == LightType.Spot) l.spotAngle = angle;
            l.shadows = shadows;
            return l;
        }

        void LateUpdate()
        {
            if (Done) return;
            float dt = Time.deltaTime;
            _t += dt;
            if (!Recording && _clock != null)
            {
                // Stay on the server clock (frame hitches, a slow scene load): jump when far off, drift otherwise.
                float server = (float)(_clock() - _startAt);
                float err = server - _t;
                if (Mathf.Abs(err) > 0.35f) _t = server;
                else _t += err * Mathf.Min(1f, dt * 2f);
            }
            float t = _t;
            Events(_prevT, t);
            _prevT = t;

            _gate.SetOpen(IntroTimeline.Shutter(t));
            _gate.SetLever(IntroTimeline.Lever(t));
            UpdateKiller(t, dt);
            UpdateCamera(t);
            UpdateLights(t);

            if (t >= IntroTimeline.Length) Finish();
        }

        void UpdateKiller(float t, float dt)
        {
            if (t >= IntroTimeline.TurnToCamera)
            {
                Vector3 toCam = LaughCamPos - _killer.transform.position;
                _killer.TurnTo(YawOf(new Vector3(toCam.x, 0f, toCam.z)), 300f);
            }
            if (t >= IntroTimeline.ShotD)
            {
                Vector3 toGate = _gate.Local(new Vector3(0f, 0f, 0.6f)) - _killer.transform.position;
                _killer.TurnTo(YawOf(new Vector3(toGate.x, 0f, toGate.z)), 200f);
            }
            _clockForPose = t;
            _killer.Tick(dt);
        }

        float _clockForPose;

        CharacterPose KillerPose(CharacterPose cur)
        {
            float t = _clockForPose;
            var p = cur;
            // Before the reach: hunched stance with a slow look back over the shoulder at the camera.
            float stance = 1f - CineKit.S(t, IntroTimeline.TurnToBox, IntroTimeline.Reach);
            if (stance > 0f)
            {
                float glance = CineKit.S(t, 0.55f, 1.1f) * (1f - CineKit.S(t, 1.45f, 1.9f));
                p = CharacterPose.Lerp(p, IntroPoses.Stance(cur, t, glance), stance);
            }
            float lw = CineKit.S(t, IntroTimeline.Reach - 0.05f, IntroTimeline.Reach + 0.12f) * (1f - CineKit.S(t, IntroTimeline.Release + 0.25f, IntroTimeline.Release + 0.45f));
            if (lw > 0f)
            {
                Vector3 knob = _killer.Body.transform.InverseTransformPoint(_gate.KnobWorld);
                p = CharacterPose.Lerp(p, IntroPoses.Lever(p, t, knob), lw);
            }
            IntroPoses.LaughCurve(t, out float body, out _, out _);
            if (body > 0f) p = CharacterPose.Lerp(p, IntroPoses.Laugh(p, t), Mathf.Min(1f, body * 1.4f));
            return p;
        }

        void Finish()
        {
            if (Done) return;
            Done = true;
            _gate.SetOpen(0f);
            _gate.SetLever(1f);
            _gate.SetLamp(true);
            CineFade.FromBlack(0.45f);
            Debug.Log("[Cine] intro done");
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_cam != null) _cam.Release();
            if (_gate != null && !Done)
            {
                _gate.SetOpen(0f);
                _gate.SetLever(1f);
                _gate.SetLamp(true);
            }
        }
    }
}
