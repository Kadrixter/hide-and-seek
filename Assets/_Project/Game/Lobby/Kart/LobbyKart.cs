using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// One of the five go-karts on the drift track behind the lobby (built at runtime in the lobby scene with the
    /// track; state: <see cref="MatchManager.KartsVar"/>). Three views: parked where its last driver left it (late
    /// joiners see the same), driven here (<see cref="KartPhysics"/> on this client, the pose goes out through
    /// <see cref="NetPlayer.KartDrive"/> ~30 times a second) or driven by somebody else (extrapolated along the sent
    /// velocity / yaw rate and smoothed). Every client animates the wheels (spin, steering), the steering wheel, the
    /// sprung body (squat, dive, roll), the brake light, smoke and skid marks (<see cref="KartFx"/>) and the engine /
    /// tyre sounds (<see cref="KartAudio"/>). Entering / leaving / the driver's pose: LobbyKart.Driver.cs; paint and
    /// numbers: <see cref="KartLivery"/>.
    /// </summary>
    public partial class LobbyKart : MonoBehaviour
    {
        public static readonly LobbyKart[] All = new LobbyKart[DriftTrackLayout.KartCount];

        public int Index { get; private set; }
        /// <summary>This kart's parking slot and heading.</summary>
        public Vector3 Spot => DriftTrackLayout.KartSlot(Index);
        public static float SpotYaw => DriftTrackLayout.KartSlotYaw;

        public enum Mode { Parked, Local, Remote }
        public Mode View { get; private set; } = Mode.Parked;
        public KartParts Parts { get; private set; }
        public readonly KartPhysics Phys = new();
        public BoxCollider Body => _col;

        // ---- what every client shows (from the physics, the remote pack or zero when parked)
        public float ShownSpeed { get; private set; }
        public Vector3 ShownVelocity { get; private set; }
        public float ShownSteer { get; private set; }
        public float ShownYawRate { get; private set; }
        public float Rpm01 { get; private set; } = 0.12f;
        public float Slip01 { get; private set; }
        public float Throttle { get; private set; }
        public bool HandbrakeOn { get; private set; }
        public bool BrakeOn { get; private set; }
        public bool SlidingNow { get; private set; }
        public float SlipAngleShown { get; private set; }
        /// <summary>Last bump strength (0..1), decays: camera shake.</summary>
        public float Bump { get; private set; }

        private BoxCollider _col;
        private KartFx _fx;
        private KartAudio _audio;
        private float _yaw, _wheelF, _wheelR, _pitch, _roll, _prevSpeed, _accel;
        private Vector3 _prevPos;
        private byte _epochSeen;
        private int _hitsSeen;

        /// <summary>Builds the five karts in the lobby scene once (every client).</summary>
        public static void EnsureAll()
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] != null) continue;
                var go = new GameObject("TrackKart" + i);
                var k = go.AddComponent<LobbyKart>();
                k.Index = i;
                All[i] = k;
                k.Build();
            }
        }

        /// <summary>The kart <paramref name="i"/> (null when the lobby is not loaded).</summary>
        public static LobbyKart Get(int i) => i >= 0 && i < All.Length ? All[i] : null;

        /// <summary>On the drift pad, inside the barriers (else the kart goes back in its slot).</summary>
        public static bool OnPad(Vector3 p) => DriftTrackLayout.InsidePad(p);

        private KartState State(MatchManager mm) => mm != null ? mm.KartsVar.Value.Get(Index) : KartState.AtSlot(Index, 0);

        private void Build()
        {
            var st = State(MatchManager.Instance);
            transform.SetPositionAndRotation(st.Pos, Quaternion.Euler(0f, st.Yaw, 0f));
            _yaw = st.Yaw;
            _epochSeen = st.Epoch;
            Parts = KartModel.Build(transform, KartLivery.Of(Index));
            gameObject.layer = 2; // Ignore Raycast: prompts / cameras / level probes look through it; players still bump into it
            _col = gameObject.AddComponent<BoxCollider>();
            _col.center = KartCollide.Center;
            _col.size = KartCollide.Half * 2f;
            var rb = gameObject.AddComponent<Rigidbody>(); // kinematic: a moving collider players bump into
            rb.isKinematic = true;
            rb.useGravity = false;
            _fx = gameObject.AddComponent<KartFx>();
            _fx.Init(this);
            _audio = gameObject.AddComponent<KartAudio>();
            _audio.Init(this);
            Phys.Reset(st.Pos, st.Yaw);
            Phys.Collide.Self = this;
            _prevPos = st.Pos;
            Debug.Log($"[Kart] kart {Index} ({KartLivery.Of(Index).Name} #{KartLivery.Of(Index).Number}) built at {st.Pos} yaw {st.Yaw:0}");
        }

        private void OnDestroy()
        {
            if (Index >= 0 && Index < All.Length && All[Index] == this) All[Index] = null;
            DriverDestroyed();
        }

        private void Update()
        {
            var mm = MatchManager.Instance;
            float dt = Time.deltaTime;
            if (mm == null || dt <= 0f) return;
            var st = State(mm);
            var driver = st.Driven ? NetPlayer.Find(st.Driver) : null;
            if (driver != null && !driver.IsSpawned) driver = null;

            Mode want = driver == null ? Mode.Parked : driver.IsOwner ? Mode.Local : Mode.Remote;
            if (want == Mode.Local && LeftJustNow) want = Mode.Parked;   // got out, the server has not caught up yet
            if (want != View) SwitchView(want, driver);

            switch (View)
            {
                case Mode.Local: TickLocal(driver, st, dt); break;
                case Mode.Remote: TickRemote(driver, st, dt); break;
                default: TickParked(st, dt); break;
            }
            _epochSeen = st.Epoch;
            UpdateRider(View == Mode.Parked ? null : driver);
            PushLocalPlayer(driver, dt);
            Animate(dt);
        }

        private void SwitchView(Mode want, NetPlayer driver)
        {
            var was = View;
            View = want;
            if (was == Mode.Local) EndLocalDrive(true);
            if (want == Mode.Local)
            {
                Phys.Reset(transform.position, _yaw);
                BeginLocalDrive(driver);
            }
            if (want == Mode.Remote) _remoteSince = Time.time;
            Debug.Log($"[Kart] view {was} -> {want}{(driver != null ? " (" + driver.Nick + ")" : "")}");
        }

        private void TickParked(KartState st, float dt)
        {
            Vector3 target = LeftJustNow ? _leftPos : st.Pos;
            float targetYaw = LeftJustNow ? _leftYaw : st.Yaw;
            bool snap = st.Epoch != _epochSeen || (transform.position - target).sqrMagnitude > 9f;
            float k = snap ? 1f : 1f - Mathf.Exp(-10f * dt);
            Vector3 before = transform.position;
            Vector3 pos = Vector3.Lerp(before, target, k);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, k);
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, _yaw, 0f));
            Vector3 v = (pos - before) / dt;
            ShownSpeed = Mathf.Lerp(ShownSpeed, Vector3.Dot(v, transform.forward), 1f - Mathf.Exp(-8f * dt));
            ShownVelocity = v;
            ShownSteer = Mathf.MoveTowards(ShownSteer, 0f, dt * 40f);
            ShownYawRate = 0f;
            Rpm01 = Mathf.MoveTowards(Rpm01, 0f, dt * 2f);
            Slip01 = Mathf.MoveTowards(Slip01, 0f, dt * 4f);
            Throttle = 0f;
            HandbrakeOn = BrakeOn = SlidingNow = false;
            SlipAngleShown = 0f;
        }

        /// <summary>Visuals shared by every view: wheels, steering wheel, sprung body, brake light, bumps.</summary>
        private void Animate(float dt)
        {
            if (Parts == null) return;
            _accel = Mathf.Lerp(_accel, (ShownSpeed - _prevSpeed) / dt, 1f - Mathf.Exp(-6f * dt));
            _prevSpeed = ShownSpeed;
            float lat = ShownSpeed * ShownYawRate * Mathf.Deg2Rad;
            _pitch = Mathf.Lerp(_pitch, Mathf.Clamp(-_accel * 0.55f, -3.5f, 3.5f), 1f - Mathf.Exp(-7f * dt));
            _roll = Mathf.Lerp(_roll, Mathf.Clamp(lat * 0.45f, -4f, 4f), 1f - Mathf.Exp(-7f * dt));
            float buzz = Rpm01 > 0.05f ? 0.0012f * Mathf.Sin(Time.time * (60f + 90f * Rpm01)) * (0.4f + Rpm01) : 0f;
            Parts.Body.localRotation = Quaternion.Euler(_pitch, 0f, _roll);
            Parts.Body.localPosition = new Vector3(0f, buzz, 0f);

            bool locked = HandbrakeOn;
            float rear = locked ? 0f : ShownSpeed + (SlidingNow && Throttle > 0.05f ? 2.8f * Throttle : 0f);
            _wheelF = Mathf.Repeat(_wheelF + ShownSpeed * dt / KartPhysics.FrontRadius * Mathf.Rad2Deg, 360f);
            _wheelR = Mathf.Repeat(_wheelR + rear * dt / KartPhysics.RearRadius * Mathf.Rad2Deg, 360f);
            for (int i = 0; i < 2; i++)
            {
                Parts.FrontSteer[i].localRotation = Quaternion.Euler(0f, ShownSteer, 0f);
                Parts.FrontWheel[i].localRotation = Quaternion.Euler(_wheelF, 0f, 0f);
                Parts.RearWheel[i].localRotation = Quaternion.Euler(_wheelR, 0f, 0f);
            }
            // The steering wheel turns ~2.5 times the front wheels' angle.
            Parts.SteeringWheel.localRotation = KartModel.WheelTilt * Quaternion.Euler(0f, 0f, -ShownSteer * 2.5f);

            if (Parts.BrakeLight != null)
            {
                var m = Parts.BrakeLight.sharedMaterial;
                bool on = BrakeOn || HandbrakeOn;
                m.SetColor("_EmissionColor", on ? new Color(2.4f, 0.1f, 0.05f) : new Color(0.25f, 0f, 0f));
            }

            int hits = Phys.Collide.Hits;
            if (View == Mode.Local && hits != _hitsSeen)
            {
                float k = Mathf.Clamp01(Phys.Collide.Impact / 5f);
                Bump = Mathf.Max(Bump, k);
                _audio.Bump(k);
            }
            _hitsSeen = hits;
            Phys.Collide.Impact = 0f;
            Bump = Mathf.MoveTowards(Bump, 0f, dt * 3f);
        }

        /// <summary>Other players' kart hits (seen as a sudden stop in the remote pose).</summary>
        private void RemoteBump(float speedBefore, float speedNow)
        {
            float loss = speedBefore - speedNow;
            if (loss < 1.2f) return;
            float k = Mathf.Clamp01(loss / 5f);
            Bump = Mathf.Max(Bump, k);
            _audio.Bump(k);
        }

        /// <summary>A local player standing where the kart is (not its driver) is nudged out, never launched.</summary>
        private void PushLocalPlayer(NetPlayer driver, float dt)
        {
            var me = NetPlayer.Local;
            if (me == null || me == driver || me.Life.Value != LifeState.Alive || me.Seat.Value >= 0) return;
            if (View == Mode.Parked && Mathf.Abs(ShownSpeed) < 0.1f) return; // standing still: it is just in the way
            var cc = me.GetComponent<CharacterController>();
            if (cc == null || !cc.enabled) return;
            var rot = transform.rotation;
            Vector3 local = Quaternion.Inverse(rot) * (me.transform.position - (transform.position + rot * KartCollide.Center));
            if (Mathf.Abs(me.transform.position.y - transform.position.y) > 1.2f) return;
            Vector3 half = KartCollide.Half;
            Vector3 c = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), 0f, Mathf.Clamp(local.z, -half.z, half.z));
            Vector3 d = new Vector3(local.x, 0f, local.z) - c;
            float r = cc.radius + 0.06f, dist = d.magnitude;
            if (dist >= r) return;
            Vector3 n = dist > 1e-4f ? d / dist : (Mathf.Abs(local.x) / half.x > Mathf.Abs(local.z) / half.z ? new Vector3(Mathf.Sign(local.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z)));
            Vector3 push = rot * n * Mathf.Min(r - dist, 2.5f * dt); // at most 2.5 m/s, sideways only
            cc.Move(push);
        }
    }
}
