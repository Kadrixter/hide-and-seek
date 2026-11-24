using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Plays the round intro on this client (<see cref="IntroTimeline"/>): a local puppet of the chosen killer with his
    /// skin yanks the small lever by the exit gate down and tears its handle off, laughs with the handle in his raised
    /// right hand, and the shutter slams shut (the stub stays on the box for the round). Started by
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
        Transform _handle;
        /// <summary>The handle in the fist ends up a little bigger than on the box so it reads in the blocky fist.</summary>
        const float HandleScale = 1.8f;
        Vector3 _snapKnob;
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
            _killer.Actions.SetHeldItem(HeldItem.None); // the right hand pulls (and keeps the torn handle)
            _killer.Pose = KillerPose;
            _killer.PoseWeight = 1f;
            BuildLights();
            _gate.SetOpen(1f);
            _gate.SetLever(0f);
            _gate.SetBroken(false);
            _gate.SetLamp(false);
            // The torn handle in the right fist, held by the rod like a torch: the red ball sticks out past the
            // fist (on top when the arm is raised), the torn end inside the fist. Shown from the snap (see PlaceHandle).
            _handle = _gate.MakeLooseHandle(_killer.Body.ElbowRight);
            _handle.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            PlaceHandle(0f);
            _handle.gameObject.SetActive(false);
            Debug.Log($"[Cine] intro start t={_t:0.00} killer={(killer != null ? killer.Nick : "-")} seeker={_seeker}");
        }

        /// <summary>
        /// The handle in the fist: true size at the snap (no jump while the eye is on it), growing to
        /// <see cref="HandleScale"/> while the arm swings up (the puppet and the handle exist only in the intro).
        /// </summary>
        void PlaceHandle(float t)
        {
            float s = Mathf.Lerp(1.1f, HandleScale, CineKit.S(t, IntroTimeline.Snap + 0.08f, IntroTimeline.Snap + 0.5f));
            _handle.localScale = Vector3.one * s;
            _handle.localPosition = CharacterPose.HandLocal + Vector3.down * (0.12f * s);
        }

        /// <summary>Killer's feet: in front of the box, a little to the left of the lever (his right hand reaches it).</summary>
        Vector3 KillerStand => _gate.Local(new Vector3(ExitGate.BoxPos.x + ExitGate.LeverPivotInBox.x + 0.4f, 0f, 0.58f));

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
                // Behind: catch up (jump when far). Ahead: never jump back, just hold until the server time is there.
                float server = (float)(_clock() - _startAt);
                float err = server - _t;
                if (err > 0.35f) _t = server;
                else _t += Mathf.Max(err, -dt) * Mathf.Min(1f, err > 0f ? dt * 2f : 1f);
            }
            float t = _t;
            Events(_prevT, t);
            _prevT = t;

            _gate.SetOpen(IntroTimeline.Shutter(t));
            _gate.SetLever(IntroTimeline.Lever(t));
            // The handle tears off at the snap: the stub stays on the box, the rest is in the killer's fist.
            bool torn = t >= IntroTimeline.Snap;
            _gate.SetBroken(torn);
            if (_handle.gameObject.activeSelf != torn) _handle.gameObject.SetActive(torn);
            if (torn) PlaceHandle(t);
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
            // The pull and the snap; it hands the right arm over to the laugh once the big laugh is on.
            float lw = CineKit.S(t, IntroTimeline.Reach - 0.05f, IntroTimeline.Reach + 0.12f)
                       * (1f - CineKit.S(t, IntroTimeline.LaughStart + 0.45f, IntroTimeline.LaughStart + 0.95f));
            if (lw > 0f)
            {
                Vector3 knob = _killer.Body.transform.InverseTransformPoint(_gate.KnobWorld);
                if (t < IntroTimeline.Snap) _snapKnob = knob;
                p = CharacterPose.Lerp(p, IntroPoses.Lever(p, t, knob, _snapKnob), lw);
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
            _gate.SetBroken(true);
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
                _gate.SetBroken(true);
                _gate.SetLamp(true);
            }
        }
    }
}
