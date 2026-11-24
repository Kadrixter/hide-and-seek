using System.Collections;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// One of the two RC rally cars in the game scene. Shown on the shelf, hidden while carried, driven by its holder
    /// (local kinematic physics on the driver's client, interpolated everywhere else), parked on the floor, or crushed.
    /// Wheels spin and steer, the antenna whips with the acceleration, the body squats and rolls; electric motor
    /// whine follows the speed, H = loud horn, T = headlights.
    /// </summary>
    public class RcCar : MonoBehaviour
    {
        public const int Count = 2;
        public const float Wheelbase = 0.27f;
        public const float ReverseSpeed = 1.8f;
        /// <summary>The car is on layer "Ignore Raycast" so level probes (and its own) never hit it.</summary>
        public const int CarLayer = 2;

        private static readonly RcCar[] Cars = new RcCar[Count];

        public enum ViewMode { Hidden, Shelf, Parked, Local, Remote, Frozen, Wreck }

        public int Index { get; private set; }
        public RcCarParts Parts { get; private set; }
        public ViewMode View { get; private set; } = ViewMode.Hidden;
        public float Speed { get; private set; }
        public float SteerAngle { get; private set; }
        public float Yaw => _yaw;
        /// <summary>Last bump strength (0..1), decays; the car camera shakes with it.</summary>
        public float Bump { get; private set; }
        public float Throttle { get; set; }
        public float SteerInput { get; set; }
        public bool Horn { get; set; }
        public bool Lights { get; set; } = true;
        public bool Boost { get; set; }

        private float _yaw, _vy, _steer, _spin, _prevSpeed, _accel, _lateral, _yawRate;
        private float _antX, _antZ, _antVX, _antVZ;
        private Quaternion _tilt = Quaternion.identity;
        private CharacterController _cc;
        private Vector3 _netPos;
        private float _netYaw, _netSpeed, _remoteSince;
        private AudioSource _motor, _hornSrc;
        private float _motorBase = 1f;
        private Light _headlight;
        private Renderer[] _renderers;
        private bool _crushed;
        private Vector3 _staticPos;
        private float _staticYaw;

        // ================================================================ registry

        /// <summary>Car <paramref name="i"/> in the current scene (created on demand).</summary>
        public static RcCar Get(int i)
        {
            if (i < 0 || i >= Count) return null;
            if (Cars[i] == null)
            {
                var go = new GameObject("RcCar_" + i);
                go.layer = CarLayer;
                var car = go.AddComponent<RcCar>();
                car.Index = i;
                car.Parts = BlockyProps.CreateRcCar(go.transform, CarLayer);
                car._renderers = go.GetComponentsInChildren<Renderer>(true);
                car.BuildHeadlight();
                car.SetVisible(false);
                Cars[i] = car;
            }
            return Cars[i];
        }

        public static RcCar Peek(int i) => i >= 0 && i < Count ? Cars[i] : null;

        // ================================================================ race cars (not in the shelf registry)

        private BoxCollider _solid;

        /// <summary>A car for the RC race in its own paint (not one of the two shelf cars).</summary>
        public static RcCar CreateRace(int id, Color paint)
        {
            var go = new GameObject("RaceCar_" + id);
            go.layer = CarLayer;
            var car = go.AddComponent<RcCar>();
            car.Index = -1 - id;
            car.Parts = BlockyProps.CreateRcCar(go.transform, CarLayer);
            car._renderers = go.GetComponentsInChildren<Renderer>(true);
            var mat = BlockyProps.RcPaintMaterial(paint);
            foreach (var r in car._renderers) r.sharedMaterial = mat;
            car.BuildHeadlight();
            car.SetVisible(false);
            return car;
        }

        /// <summary>Remote race cars are solid (the local car's controller bumps into them); the own car is not.</summary>
        public void SetSolid(bool on)
        {
            if (on && _solid == null)
            {
                _solid = gameObject.AddComponent<BoxCollider>();
                _solid.center = new Vector3(0f, 0.11f, 0f);
                _solid.size = new Vector3(0.25f, 0.2f, 0.5f);
            }
            if (_solid != null) _solid.enabled = on;
        }

        /// <summary>Puts the locally driven car back at <paramref name="pos"/> standing still (race respawn).</summary>
        public void Respawn(Vector3 pos, float yaw)
        {
            bool had = _cc != null && _cc.enabled;
            if (_cc != null) _cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            _yaw = yaw;
            Speed = 0f;
            _vy = 0f;
            _steer = 0f;
            if (_cc != null) _cc.enabled = had || View == ViewMode.Local;
            Physics.SyncTransforms();
        }

        /// <summary>Bump effect + sound (car-to-car contact in the race).</summary>
        public void Knock(float speed) => HitSomething(speed);

        public bool IsGroundedLocal => _cc != null && _cc.enabled && _cc.isGrounded;

        private void OnDestroy()
        {
            if (Index >= 0 && Index < Count && Cars[Index] == this) Cars[Index] = null;
            StopAudio();
        }

        private void BuildHeadlight()
        {
            var lg = new GameObject("Headlights");
            lg.transform.SetParent(Parts.Root, false);
            lg.transform.localPosition = new Vector3(0f, 0.09f, 0.26f);
            lg.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            _headlight = lg.AddComponent<Light>();
            _headlight.type = LightType.Spot;
            _headlight.spotAngle = 78f;
            _headlight.range = 7f;
            _headlight.intensity = 1.7f;
            _headlight.color = new Color(1f, 0.93f, 0.75f);
            _headlight.shadows = LightShadows.None;
            _headlight.enabled = false;
        }

        private void SetVisible(bool on)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers) if (r != null && r.enabled != on) r.enabled = on;
        }

        // ================================================================ view modes (driven by the slot state)

        public void Hide()
        {
            if (View == ViewMode.Hidden) return;
            EndLocalPhysics();
            View = ViewMode.Hidden;
            SetVisible(false);
            Speed = 0f;
            StopAudio();
        }

        /// <summary>Standing on the shelf board (exact pose, no ground probe).</summary>
        public void ShowOnShelf(Vector3 pos, float yaw)
        {
            if (View == ViewMode.Shelf && (pos - _staticPos).sqrMagnitude < 1e-6f) return;
            EnterStatic(ViewMode.Shelf, pos, yaw, false);
        }

        /// <summary>Parked on the floor (snapped to the ground below).</summary>
        public void ShowParked(Vector3 pos, float yaw)
        {
            if (View == ViewMode.Parked && (pos - _staticPos).sqrMagnitude < 1e-6f) return;
            EnterStatic(ViewMode.Parked, pos, yaw, true);
        }

        /// <summary>Held still under the seeker's boot.</summary>
        public void Freeze(Vector3 pos, float yaw)
        {
            if (View == ViewMode.Frozen) return;
            if (View != ViewMode.Local && View != ViewMode.Remote) EnterStatic(ViewMode.Frozen, pos, yaw, true);
            EndLocalPhysics();
            View = ViewMode.Frozen;
            Speed = 0f;
            Throttle = 0f;
            Horn = false;
        }

        private void EnterStatic(ViewMode mode, Vector3 pos, float yaw, bool snap)
        {
            EndLocalPhysics();
            View = mode;
            _staticPos = pos;
            _staticYaw = yaw;
            Speed = 0f;
            Throttle = 0f;
            Horn = false;
            _yaw = yaw;
            if (snap && GroundBelow(pos + Vector3.up * 0.3f, 1.2f, out var hit)) pos.y = hit.point.y;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            _tilt = mode == ViewMode.Shelf ? Quaternion.identity : GroundTilt();
            Parts.Root.localRotation = _tilt;
            SetVisible(true);
            StopAudio();
        }

        /// <summary>The local player drives this car from <paramref name="pos"/>.</summary>
        public void EnsureLocal(Vector3 pos, float yaw)
        {
            if (View == ViewMode.Local) return;
            bool wasShown = View == ViewMode.Parked || View == ViewMode.Frozen;
            View = ViewMode.Local;
            if (!wasShown) transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            _yaw = transform.eulerAngles.y;
            Speed = 0f;
            _vy = 0f;
            SetVisible(true);
            if (_cc == null)
            {
                _cc = gameObject.AddComponent<CharacterController>();
                _cc.excludeLayers = GameLayers.RagdollMask; // drives through dead bodies like before
                _cc.radius = 0.1f;
                _cc.height = 0.2f;
                _cc.center = new Vector3(0f, 0.11f, 0f);
                _cc.skinWidth = 0.01f;
                _cc.stepOffset = 0.07f;
                _cc.slopeLimit = 50f;
                _cc.minMoveDistance = 0f;
            }
            _cc.enabled = true;
        }

        /// <summary>Somebody else drives it: follow the network pose.</summary>
        public void EnsureRemote(RcPack pack, Vector3 fallbackPos, float fallbackYaw)
        {
            if (View != ViewMode.Remote)
            {
                EndLocalPhysics();
                bool wasShown = View == ViewMode.Parked || View == ViewMode.Frozen;
                View = ViewMode.Remote;
                _remoteSince = Time.time;
                if (!wasShown) transform.SetPositionAndRotation(fallbackPos, Quaternion.Euler(0f, fallbackYaw, 0f));
                _yaw = transform.eulerAngles.y;
                _netPos = transform.position;
                _netYaw = _yaw;
                SetVisible(true);
            }
            // A stale pose from an earlier drive (far from where the car was put down) is ignored for a moment.
            bool stale = pack.Pos == Vector3.zero || (Time.time - _remoteSince < 0.6f && (pack.Pos - fallbackPos).sqrMagnitude > 9f);
            if (stale) return;
            _netPos = pack.Pos;
            _netYaw = pack.YawDeg;
            _netSpeed = pack.SpeedMs;
            SteerInput = pack.Steer / 100f;
            Throttle = pack.Throttle / 100f;
            Horn = pack.Horn;
            Lights = (pack.Flags & 8) != 0;
            Boost = (pack.Flags & 4) != 0;
        }

        private void EndLocalPhysics()
        {
            if (_cc != null) _cc.enabled = false;
        }

        // ================================================================ local driving physics

        /// <summary>
        /// Driver's client: arcade car physics on a CharacterController (slopes, ramps, small steps, walls).
        /// Top speed = the character's walk speed, Shift = run speed. Returns the pose to send.
        /// </summary>
        public void TickLocal(float dt, float walkSpeed, float runSpeed, Vector3 driverPos, NetPlayer driver)
        {
            if (View != ViewMode.Local || _cc == null || !_cc.enabled || dt <= 0f) return;
            float throttle = Mathf.Clamp(Throttle, -1f, 1f);
            float maxF = Boost ? runSpeed : walkSpeed;
            float target = throttle > 0.01f ? throttle * maxF : throttle < -0.01f ? throttle * ReverseSpeed : 0f;

            // Radio range: the car will not drive further away from the body.
            Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Vector3 toCar = transform.position - driverPos;
            toCar.y = 0f;
            if (toCar.magnitude > MatchManager.RcRangeOf(Index) && Vector3.Dot(fwd * Mathf.Sign(Mathf.Abs(target) > 0.01f ? target : Speed), toCar) > 0f)
                target = 0f;

            float rate = Mathf.Abs(target) < 0.01f ? 3.5f : Speed * target < 0f ? 11f : Boost ? 7.5f : 5.5f;
            Speed = Mathf.MoveTowards(Speed, target, rate * dt);
            _steer = Mathf.MoveTowards(_steer, Mathf.Clamp(SteerInput, -1f, 1f), dt * 5f);
            SteerAngle = _steer * 32f * Mathf.Lerp(1f, 0.5f, Mathf.Abs(Speed) / Mathf.Max(1f, runSpeed));
            _yawRate = Speed / Wheelbase * Mathf.Tan(SteerAngle * Mathf.Deg2Rad) * Mathf.Rad2Deg;
            _yaw += _yawRate * dt;
            fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Vector3 move = fwd * Speed * dt;

            // Bumper probe (the capsule alone would let the nose poke into walls).
            if (Mathf.Abs(Speed) > 0.01f)
            {
                Vector3 dir = fwd * Mathf.Sign(Speed);
                const float half = 0.17f;
                if (Physics.SphereCast(transform.position + Vector3.up * 0.09f, 0.06f, dir, out var hit, half + move.magnitude, LevelMask, QueryTriggerInteraction.Ignore)
                    && hit.normal.y < 0.6f)
                {
                    move = dir * Mathf.Max(0f, hit.distance - half);
                    HitSomething(Mathf.Abs(Speed));
                    Speed = -Speed * 0.25f;
                }
            }

            // Remote players carry no colliders: bounce off their legs.
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == driver || o.IsOwner || o.Life.Value == LifeState.Dead) continue;
                Vector3 d = transform.position + move - o.transform.position;
                if (Mathf.Abs(d.y) > 1f) continue;
                d.y = 0f;
                float r = o.Life.Value == LifeState.Downed ? 0.25f : 0.3f;
                if (d.sqrMagnitude >= r * r || d.sqrMagnitude < 1e-6f) continue;
                move += d.normalized * (r - d.magnitude);
                if (Mathf.Abs(Speed) > 0.6f) HitSomething(Mathf.Abs(Speed) * 0.7f);
                Speed *= 0.3f;
            }

            _vy = _cc.isGrounded ? -1.5f : Mathf.Max(_vy - 9.81f * dt, -12f);
            _cc.Move(move + Vector3.up * (_vy * dt));
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void HitSomething(float speed)
        {
            if (speed < 0.8f) return;
            float k = Mathf.Clamp01(speed / 5f);
            Bump = Mathf.Max(Bump, k);
            GameAudio.PlayAt(GameSfx.RcBump, transform.position + Vector3.up * 0.1f, 0.4f + 0.6f * k);
        }

        public RcPack Pack()
        {
            return new RcPack
            {
                Pos = transform.position,
                Yaw = (short)Mathf.RoundToInt(Mathf.Repeat(_yaw, 360f) * 50f),
                Speed = (sbyte)Mathf.Clamp(Mathf.RoundToInt(Speed * 20f), -127, 127),
                Steer = (sbyte)Mathf.RoundToInt(Mathf.Clamp(SteerInput, -1f, 1f) * 100f),
                Throttle = (sbyte)Mathf.RoundToInt(Mathf.Clamp(Throttle, -1f, 1f) * 100f),
                Flags = (byte)((Horn ? 1 : 0) | (Boost ? 4 : 0) | (Lights ? 8 : 0)),
            };
        }

        // ================================================================ per frame visuals / audio

        private static int LevelMask => ~((1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9) | (1 << CarLayer));

        private static bool GroundBelow(Vector3 from, float dist, out RaycastHit hit)
            => Physics.Raycast(from, Vector3.down, out hit, dist, LevelMask, QueryTriggerInteraction.Ignore);

        /// <summary>Body tilt from the ground under the four corners (car on a ramp / kerb).</summary>
        private Quaternion GroundTilt()
        {
            Vector3 p = transform.position + Vector3.up * 0.25f;
            Vector3 f = transform.forward * 0.15f, r = transform.right * 0.09f;
            float hf = GroundBelow(p + f, 0.6f, out var a) ? a.point.y : transform.position.y;
            float hb = GroundBelow(p - f, 0.6f, out var b) ? b.point.y : transform.position.y;
            float hr = GroundBelow(p + r, 0.6f, out var c) ? c.point.y : transform.position.y;
            float hl = GroundBelow(p - r, 0.6f, out var d) ? d.point.y : transform.position.y;
            float pitch = Mathf.Atan2(hb - hf, 0.3f) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(hl - hr, 0.18f) * Mathf.Rad2Deg;
            return Quaternion.Euler(Mathf.Clamp(pitch, -30f, 30f), 0f, Mathf.Clamp(roll, -25f, 25f));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || View == ViewMode.Hidden) return;
            Bump = Mathf.MoveTowards(Bump, 0f, dt * 3f);

            if (View == ViewMode.Remote)
            {
                // Interpolate towards the latest pose, a little ahead along the reported speed.
                Vector3 ahead = _netPos + Quaternion.Euler(0f, _netYaw, 0f) * Vector3.forward * (_netSpeed * 0.05f);
                float k = 1f - Mathf.Exp(-14f * dt);
                Vector3 before = transform.position;
                transform.position = Vector3.Lerp(transform.position, ahead, k);
                float prevYaw = _yaw;
                _yaw = Mathf.LerpAngle(_yaw, _netYaw, k);
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                float newSpeed = Vector3.Dot(transform.position - before, transform.forward) / dt;
                if (Mathf.Abs(Speed) > 1.2f && Mathf.Abs(newSpeed) < Mathf.Abs(Speed) * 0.3f && Mathf.Abs(_netSpeed) < 0.6f) HitSomething(Mathf.Abs(Speed));
                Speed = Mathf.Lerp(Speed, newSpeed, 1f - Mathf.Exp(-10f * dt));
                _yawRate = Mathf.DeltaAngle(prevYaw, _yaw) / dt;
                _steer = Mathf.MoveTowards(_steer, SteerInput, dt * 5f);
                SteerAngle = _steer * 32f * Mathf.Lerp(1f, 0.5f, Mathf.Abs(Speed) / 5.8f);
            }

            bool moving = View == ViewMode.Local || View == ViewMode.Remote;
            if (!moving && View != ViewMode.Frozen) { Speed = 0f; _yawRate = 0f; }

            // Body dynamics: squat under acceleration, roll in the turns.
            _accel = Mathf.Lerp(_accel, (Speed - _prevSpeed) / dt, 1f - Mathf.Exp(-8f * dt));
            _prevSpeed = Speed;
            _lateral = Speed * _yawRate * Mathf.Deg2Rad;
            if (View != ViewMode.Shelf && View != ViewMode.Wreck)
            {
                if (moving || View == ViewMode.Frozen) _tilt = Quaternion.Slerp(_tilt, GroundTilt(), 1f - Mathf.Exp(-12f * dt));
                var dyn = Quaternion.Euler(Mathf.Clamp(-_accel * 0.7f, -5f, 5f), 0f, Mathf.Clamp(_lateral * 1.1f, -7f, 7f));
                float buzz = moving ? Mathf.Abs(Speed) * 0.0008f * Mathf.Sin(Time.time * 70f) : 0f;
                Parts.Root.localRotation = _tilt * dyn;
                Parts.Root.localPosition = new Vector3(0f, buzz, 0f);
            }

            if (View != ViewMode.Wreck)
            {
                // Wheels spin with the distance, the front pair steers.
                _spin += Speed * dt / BlockyProps.RcWheelRadius * Mathf.Rad2Deg;
                _spin = Mathf.Repeat(_spin, 360f);
                for (int i = 0; i < 4; i++)
                    if (Parts.Wheels[i] != null) Parts.Wheels[i].localRotation = Quaternion.Euler(_spin, 0f, 0f);
                for (int i = 0; i < 2; i++)
                    if (Parts.Steer[i] != null) Parts.Steer[i].localRotation = Quaternion.Euler(0f, moving ? SteerAngle : 0f, 0f);

                // Whip antenna: spring driven by acceleration and cornering, buzzing with speed.
                float tx = Mathf.Clamp(-_accel * 4f, -35f, 35f) - Mathf.Abs(Speed) * 2.2f;
                float tz = Mathf.Clamp(-_lateral * 5f, -35f, 35f);
                _antVX += ((tx - _antX) * 140f - _antVX * 7f) * dt;
                _antVZ += ((tz - _antZ) * 140f - _antVZ * 7f) * dt;
                _antX += _antVX * dt;
                _antZ += _antVZ * dt;
                float wob = moving ? Mathf.Abs(Speed) * 0.5f * Mathf.Sin(Time.time * 23f) : 0f;
                Parts.Antenna.localRotation = Quaternion.Euler(_antX + wob, 0f, _antZ);
            }

            UpdateAudio(moving);
            if (_headlight != null)
            {
                bool lit = moving && Lights;
                if (_headlight.enabled != lit) _headlight.enabled = lit;
            }
        }

        private void UpdateAudio(bool moving)
        {
            if (moving && _motor == null)
            {
                _motor = GameAudio.StartLoop(GameSfx.RcMotor, transform, 1f);
                if (_motor != null) _motorBase = _motor.volume;
            }
            else if (!moving && _motor != null) { GameAudio.StopLoop(_motor, 0.25f); _motor = null; }
            if (_motor != null)
            {
                float sp = Mathf.Abs(Speed) / 5.8f;
                _motor.pitch = 0.62f + 1.05f * sp + 0.12f * Mathf.Abs(Throttle);
                _motor.volume = _motorBase * Mathf.Clamp01(0.3f + 0.45f * Mathf.Abs(Throttle) + 0.5f * sp);
            }
            bool horn = moving && Horn;
            if (horn && _hornSrc == null) _hornSrc = GameAudio.StartLoop(GameSfx.RcHorn, transform, 1f);
            else if (!horn && _hornSrc != null) { GameAudio.StopLoop(_hornSrc, 0.05f); _hornSrc = null; }
        }

        private void StopAudio()
        {
            if (_motor != null) { GameAudio.StopLoop(_motor, 0.1f); _motor = null; }
            if (_hornSrc != null) { GameAudio.StopLoop(_hornSrc, 0.05f); _hornSrc = null; }
        }

        // ================================================================ crushed

        /// <summary>Under the seeker's boot: body flattened, antenna bent, wheels and plastic bits fly off; stays as a prop.</summary>
        public void Crush(Vector3 pos, float yaw)
        {
            if (_crushed) return;
            _crushed = true;
            if (View == ViewMode.Hidden || View == ViewMode.Shelf) EnterStatic(ViewMode.Parked, pos, yaw, true);
            EndLocalPhysics();
            View = ViewMode.Wreck;
            Speed = 0f;
            StopAudio();
            if (_headlight != null) _headlight.enabled = false;
            Vector3 c = transform.position + Vector3.up * 0.08f;
            GameAudio.PlayAt(GameSfx.RcCrush, c, 1f);

            var rnd = new System.Random(1234 + Index * 77);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Parts.Body.localScale = new Vector3(1.14f, 0.42f, 1.05f);
            Parts.Body.localPosition = new Vector3(0f, -0.014f, 0f);
            Parts.Root.localRotation = _tilt * Quaternion.Euler(R(-5f, 5f), R(-6f, 6f), R(-7f, 7f));
            Parts.Antenna.localPosition = new Vector3(BlockyProps.RcAntennaBase.x, BlockyProps.RcAntennaBase.y * 0.42f, BlockyProps.RcAntennaBase.z);
            Parts.Antenna.localRotation = Quaternion.Euler(R(60f, 82f), R(-30f, 30f), R(-20f, 20f));

            // Wheels pop off.
            for (int i = 0; i < 4; i++)
            {
                var w = Parts.Wheels[i];
                if (w == null) continue;
                Vector3 wp = w.position;
                Quaternion wr = w.rotation;
                w.SetParent(null, true);
                w.SetPositionAndRotation(wp, wr);
                w.gameObject.layer = CarLayer;
                var col = w.gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(0.042f, BlockyProps.RcWheelRadius * 2f, BlockyProps.RcWheelRadius * 2f);
                var rb = w.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.06f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Vector3 outward = (wp - c);
                outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Random.insideUnitSphere;
                rb.linearVelocity = outward * R(1.2f, 2.4f) + Vector3.up * R(1.6f, 2.8f);
                rb.angularVelocity = new Vector3(R(-30f, 30f), R(-10f, 10f), R(-30f, 30f));
                FxUtil.Runner.StartCoroutine(Settle(rb, 2.5f));
                Parts.Wheels[i] = null;
            }

            // Plastic shards in the livery colours.
            var colors = new[] { new Color(0.08f, 0.3f, 0.82f), new Color(0.95f, 0.95f, 0.96f), new Color(1f, 0.78f, 0.08f), new Color(0.07f, 0.1f, 0.14f) };
            for (int i = 0; i < 9; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = "RcShard";
                g.layer = CarLayer;
                float s = R(0.012f, 0.03f);
                g.transform.localScale = new Vector3(s, s * 0.35f, s * R(0.8f, 1.6f));
                g.transform.SetPositionAndRotation(c + new Vector3(R(-0.08f, 0.08f), R(0f, 0.05f), R(-0.12f, 0.12f)), Random.rotation);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", colors[i % colors.Length]);
                g.GetComponent<Renderer>().SetPropertyBlock(mpb);
                var rb = g.AddComponent<Rigidbody>();
                rb.mass = 0.01f;
                Vector2 dir2 = Random.insideUnitCircle.normalized;
                rb.linearVelocity = new Vector3(dir2.x, 0f, dir2.y) * R(0.8f, 2.2f) + Vector3.up * R(1f, 2.2f);
                rb.angularVelocity = Random.insideUnitSphere * 40f;
                FxUtil.Runner.StartCoroutine(Settle(rb, 2f));
            }

            // Dust puff.
            var go = new GameObject("RcCrushDust");
            go.transform.position = c;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(0.55f, 0.53f, 0.5f, 0.45f);
            main.gravityModifier = -0.05f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.12f;
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            col2.color = gr;
            go.GetComponent<ParticleSystemRenderer>().material = FxUtil.Unlit;
            ps.Play();
        }

        // ================================================================ shot (Glock)

        /// <summary>Bullet hit sparks on the car (every client, from the shot effect).</summary>
        public void BulletHit(Vector3 point, Vector3 dir)
        {
            Sparks(point, -dir, 26, 3.2f);
        }

        /// <summary>
        /// Shot with the Glock: the battery pack pops, the car jumps and rolls onto its side / roof, two wheels fly
        /// off, it blackens and keeps smoking until the round ends.
        /// </summary>
        public void ShotDown(Vector3 pos, float yaw)
        {
            if (_crushed) return;
            _crushed = true;
            bool wasMoving = View == ViewMode.Local || View == ViewMode.Remote;
            if (View == ViewMode.Hidden || View == ViewMode.Shelf) EnterStatic(ViewMode.Parked, pos, yaw, true);
            else if (!wasMoving && (transform.position - pos).sqrMagnitude > 0.04f) EnterStatic(ViewMode.Parked, pos, yaw, true);
            float startSpeed = Speed;
            EndLocalPhysics();
            View = ViewMode.Wreck;
            Speed = 0f;
            StopAudio();
            if (_headlight != null) _headlight.enabled = false;
            Vector3 c = transform.position + Vector3.up * 0.1f;
            GameAudio.PlayAt(GameSfx.RcCrush, c, 0.9f);
            GameAudio.PlayAt(GameSfx.BulletImpact, c, 1f);

            var rnd = new System.Random(4321 + Index * 31);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // Scorched paint.
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(0.33f, 0.31f, 0.3f));
            foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(mpb);

            // Two wheels come off.
            for (int i = 1; i <= 2; i++)
            {
                var w = Parts.Wheels[i];
                if (w == null) continue;
                Vector3 wp = w.position;
                Quaternion wr = w.rotation;
                w.SetParent(null, true);
                w.SetPositionAndRotation(wp, wr);
                w.gameObject.layer = CarLayer;
                var col = w.gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(0.042f, BlockyProps.RcWheelRadius * 2f, BlockyProps.RcWheelRadius * 2f);
                var rb = w.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.06f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Vector3 outward = wp - c;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Random.insideUnitSphere;
                rb.linearVelocity = outward * R(1.6f, 3f) + Vector3.up * R(2.2f, 3.4f) + transform.forward * startSpeed * 0.5f;
                rb.angularVelocity = new Vector3(R(-40f, 40f), R(-10f, 10f), R(-40f, 40f));
                FxUtil.Runner.StartCoroutine(Settle(rb, 2.5f));
                Parts.Wheels[i] = null;
            }
            Parts.Antenna.localRotation = Quaternion.Euler(R(40f, 70f), R(-40f, 40f), R(-30f, 30f));

            Sparks(c, Vector3.up, 60, 4.5f);
            Flash(c);
            StartCoroutine(FlipRoutine(R(0f, 1f) < 0.5f ? 1f : -1f, R(0f, 1f) < 0.45f, startSpeed));
            StartCoroutine(SmokeRoutine());
        }

        /// <summary>Hop and roll onto the side (or all the way onto the roof), sliding on a little if it was driving.</summary>
        private IEnumerator FlipRoutine(float side, bool roof, float speed)
        {
            Quaternion from = Parts.Root.localRotation;
            Quaternion to = _tilt * Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(-25f, 25f), side * (roof ? 180f : 90f));
            Vector3 start = transform.position;
            Vector3 slide = transform.forward * Mathf.Clamp(speed, -3f, 3f) * 0.18f;
            float lift = roof ? 0.07f : 0.09f; // lying on the side / roof the pivot sits higher
            const float dur = 0.55f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float hop = Mathf.Sin(k * Mathf.PI) * 0.22f;
                Parts.Root.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                Parts.Root.localPosition = new Vector3(0f, hop + lift * k, 0f);
                transform.position = start + slide * Mathf.Sqrt(k);
                yield return null;
            }
            GameAudio.PlayAt(GameSfx.RcBump, transform.position + Vector3.up * 0.1f, 1f);
            Sparks(transform.position + Vector3.up * 0.05f, Vector3.up, 14, 1.6f);
        }

        /// <summary>Thick smoke at first, then a thin wisp that keeps rising until the car is gone.</summary>
        private IEnumerator SmokeRoutine()
        {
            var go = new GameObject("RcSmoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, -0.05f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.12f, 0.12f, 0.75f), new Color(0.3f, 0.3f, 0.3f, 0.55f));
            main.gravityModifier = -0.06f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            var em = ps.emission;
            em.rateOverTime = 28f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.8f;
            go.GetComponent<ParticleSystemRenderer>().material = FxUtil.Soft;
            ps.Play();

            // A small flickering ember light inside the wreck for a few seconds.
            var lg = new GameObject("RcEmber");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.45f, 0.12f);
            l.range = 1.4f;
            l.shadows = LightShadows.None;
            float t = 0f;
            while (t < 9f && l != null)
            {
                t += Time.deltaTime;
                l.intensity = Mathf.Lerp(1.6f, 0f, t / 9f) * (0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 9f, Index));
                em.rateOverTime = Mathf.Lerp(28f, 5f, t / 9f);
                yield return null;
            }
            if (lg != null) Destroy(lg);
        }

        private static void Sparks(Vector3 at, Vector3 dir, int count, float speed)
        {
            var go = new GameObject("RcSparks");
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir : Vector3.up));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.55f, 0.15f));
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.02f;
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.dampen = 0.4f;
            col.bounce = 0.45f;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.04f;
            pr.lengthScale = 1.5f;
            pr.material = FxUtil.Unlit;
            ps.Play();
        }

        private static void Flash(Vector3 at)
        {
            var go = new GameObject("RcFlash");
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.7f, 0.35f);
            l.range = 4f;
            l.intensity = 4f;
            l.shadows = LightShadows.None;
            FxUtil.Runner.StartCoroutine(FadeLight(l, 0.35f));
        }

        private static IEnumerator FadeLight(Light l, float time)
        {
            float t = 0f, i0 = l.intensity;
            while (t < time && l != null)
            {
                t += Time.deltaTime;
                l.intensity = i0 * (1f - t / time);
                yield return null;
            }
            if (l != null) Destroy(l.gameObject);
        }

        /// <summary>Loose parts come to rest, then freeze (no colliders left for players to trip over).</summary>
        private static IEnumerator Settle(Rigidbody rb, float minTime)
        {
            yield return new WaitForSeconds(minTime);
            float t = 0f;
            while (rb != null && !rb.IsSleeping() && t < 5f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (rb == null) yield break;
            rb.isKinematic = true;
            var col = rb.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }
}
