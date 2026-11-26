using System;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Cans in the blood at server time <see cref="At"/> (wears off by itself, <see cref="Drunkenness"/>) and the
    /// server time the body collapsed (<see cref="FellAt"/>, 0 = not collapsed; <see cref="OverdoseTimeline"/>).
    /// </summary>
    public struct DrunkState : INetworkSerializable, IEquatable<DrunkState>
    {
        public float Cans;
        public double At;
        public double FellAt;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Cans);
            s.SerializeValue(ref At);
            s.SerializeValue(ref FellAt);
        }

        public bool Equals(DrunkState o) => Cans == o.Cans && At == o.At && FellAt == o.FellAt;
    }

    public partial class NetPlayer
    {
        /// <summary>
        /// Drunk from the lobby vending machine (server-written, everyone reads it): the owner's camera sways
        /// and every client animates the staggering body from the same value. At <see cref="Drunkenness.CollapseCans"/>
        /// the server starts the collapse (the start time is in the value, so late joiners see the right moment).
        /// </summary>
        public readonly NetworkVariable<DrunkState> DrunkVar = new();

        private DrunkVision _drunkFx;
        private bool _drunkBusy;                 // server: Busy was set by the collapse
        private float _odPrevT = -1f;            // every client: collapse clock last frame (sound marks)
        private bool _odView, _odHideVm, _odFlashWasOn;
        private Vector2 _odPeek;
        private float _heartVol, _heartBpm = 90f, _nextHeartBeat;

        private double ServerNow => MatchManager.Instance != null ? MatchManager.Instance.Now : 0d;

        /// <summary>Current level in cans (decays over time).</summary>
        public float DrunkLevel => Drunkenness.Level(DrunkVar.Value.Cans, ServerNow - DrunkVar.Value.At);

        /// <summary>0..1 strength of the effect right now (0 after a single can).</summary>
        public float DrunkIntensity => Drunkenness.Intensity(DrunkLevel);

        /// <summary>Seconds into the collapse (<see cref="OverdoseTimeline"/>), -1 when not collapsed. Lobby only.</summary>
        public float OverdoseTime
        {
            get
            {
                double fell = DrunkVar.Value.FellAt;
                if (fell <= 0d || Life.Value != LifeState.Alive) return -1f;
                var mm = MatchManager.Instance;
                if (mm == null || mm.Phase.Value != MatchPhase.Lobby) return -1f;
                double t = ServerNow - fell;
                if (t < -0.5d || t >= OverdoseTimeline.End) return -1f;
                return Mathf.Max(0f, (float)t);
            }
        }

        /// <summary>Lying / getting up after the collapse: no walking, items put away.</summary>
        public bool OverdoseActive => OverdoseTime >= 0f;

        /// <summary>Owner: own heartbeat as heard right now (0..1 volume, beats per minute, beats played) - tests.</summary>
        public float HeartVolume => _heartVol;
        public float HeartBpm => _heartBpm;
        public int HeartBeats { get; private set; }

        /// <summary>
        /// Server: one more can finished (each one adds to what is left of the previous ones). Reaching
        /// <see cref="Drunkenness.CollapseCans"/> in the lobby starts the collapse; the level is then set to
        /// <see cref="Drunkenness.AfterCollapseCans"/> counted from the moment the player is back on the feet.
        /// </summary>
        public void ServerAddDrink(float cans = 1f)
        {
            if (!IsServer) return;
            double now = ServerNow;
            float level = Mathf.Min(Drunkenness.MaxCans, DrunkLevel + cans);
            var mm = MatchManager.Instance;
            bool canFall = mm != null && mm.Phase.Value == MatchPhase.Lobby && Life.Value == LifeState.Alive && Seat.Value < 0
                           && DrunkVar.Value.FellAt <= 0d;
            if (canFall && level >= Drunkenness.CollapseCans - 0.001f)
            {
                DrunkVar.Value = new DrunkState { Cans = Drunkenness.AfterCollapseCans, At = now + OverdoseTimeline.End, FellAt = now };
                Busy.Value = true;
                _drunkBusy = true;
                Debug.Log($"[Drunk] {Nick} collapsed at {level:0.00} cans");
                return;
            }
            DrunkVar.Value = new DrunkState { Cans = level, At = now };
        }

        /// <summary>Server: sober at once (start of a round, back in the lobby): also ends a collapse on the spot.</summary>
        public void ServerSoberUp()
        {
            if (!IsServer) return;
            if (!DrunkVar.Value.Equals(default)) DrunkVar.Value = default;
            ServerReleaseDrunkBusy();
        }

        private void ServerReleaseDrunkBusy()
        {
            if (!_drunkBusy) return;
            _drunkBusy = false;
            Busy.Value = false;
        }

        /// <summary>Server: ends the collapse on time; anything else (round start, leaving the lobby, not alive) cancels it.</summary>
        private void ServerTickDrunk()
        {
            var d = DrunkVar.Value;
            if (d.FellAt <= 0d)
            {
                ServerReleaseDrunkBusy();
                return;
            }
            var mm = MatchManager.Instance;
            if (mm == null || mm.Phase.Value != MatchPhase.Lobby || Life.Value != LifeState.Alive)
            {
                ServerSoberUp();
                return;
            }
            if (ServerNow - d.FellAt < OverdoseTimeline.End) return;
            d.FellAt = 0d;
            DrunkVar.Value = d;
            ServerReleaseDrunkBusy();
        }

        /// <summary>Every client: body sway and collapse for everyone; camera, screen effect and heartbeat for the owner.</summary>
        private void UpdateDrunk()
        {
            if (IsServer) ServerTickDrunk();
            float od = OverdoseTime;
            float k = Life.Value == LifeState.Alive && DrunkVar.Value.Cans > 0f ? DrunkIntensity : 0f;
            float clock = Drunkenness.Clock(ServerNow);
            float seed = Drunkenness.SeedFor(NetworkObjectId);
            var bca = Actions as BlockyCharacterActions;
            bca?.SetDrunk(k, clock, seed);
            bca?.SetOverdose(od, seed);

            // Heard by everyone around: the body hitting the floor, gasping for air once the fit is over.
            if (_odPrevT >= 0f && od >= 0f)
            {
                if (_odPrevT < OverdoseTimeline.Thud && od >= OverdoseTimeline.Thud)
                    GameAudio.PlayAt(GameSfx.BodyFall, transform.position, 0.9f);
                if (_odPrevT < OverdoseTimeline.SeizeEnd + 0.2f && od >= OverdoseTimeline.SeizeEnd + 0.2f)
                    GameAudio.PlayAttached(GameSfx.HiderBreathPanic, transform, 0.55f);
            }

            if (IsOwner)
            {
                if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
                if (_bob != null)
                {
                    _bob.Drunk = k;
                    _bob.DrunkClock = clock;
                    _bob.DrunkSeed = seed;
                }
                UpdateOverdoseView(od, seed);
                UpdateDrunkFx(_bob != null ? _bob.DrunkWeight : k, clock, seed, od);
                UpdateHeart(od);
            }
            _odPrevT = od;
        }

        private void UpdateDrunkFx(float shown, float clock, float seed, float od)
        {
            // Consciousness fading: darker, blurrier edges from the knees giving until the body is up again.
            float faint = 0f;
            if (od >= 0f)
                faint = Drunkenness.Seg(od, OverdoseTimeline.Clutch, OverdoseTimeline.FallEnd)
                        * (1f - Drunkenness.Seg(od, OverdoseTimeline.GetUpStart, OverdoseTimeline.HalfKneel))
                        * (0.85f + 0.15f * Drunkenness.Fbm(od * 0.4f, seed + 61f));
            if (_drunkFx == null)
            {
                if ((shown < 0.002f && faint < 0.002f) || Camera == null) return;
                _drunkFx = Camera.GetComponent<DrunkVision>();
                if (_drunkFx == null) _drunkFx = Camera.gameObject.AddComponent<DrunkVision>();
            }
            _drunkFx.Amount = shown;
            _drunkFx.Faint = faint;
            _drunkFx.Clock = clock;
            _drunkFx.Seed = seed;
            bool on = shown >= 0.002f || faint >= 0.002f;
            if (_drunkFx.enabled != on) _drunkFx.enabled = on;
        }

        // ================================================================ collapse: first person (owner)

        /// <summary>
        /// Owner: the view follows the head down to the floor and back up (<see cref="OverdoseCamera"/>), shakes with
        /// the fit on Perlin noise (no sines, no lurching), and the mouse only turns the head a little. Hands, held
        /// items and the flashlight are put away for the whole collapse.
        /// </summary>
        private void UpdateOverdoseView(float od, float seed)
        {
            if (od < 0f)
            {
                if (_odView) EndOverdoseView();
                return;
            }
            if (!_odView) BeginOverdoseView();
            if (_bob == null) return;
            float dt = Time.deltaTime;

            // The body decides where the eyes look: level the own look first, then only a little head movement.
            Motor.SetLook(Motor.LookYaw, Mathf.MoveTowards(Motor.LookPitch, 0f, dt * 90f));
            var mouse = Mouse.current;
            if (mouse != null && InputReader != null && !LocalPlayerController.MenuOpen && (Cursor.lockState == CursorLockMode.Locked || PadInput.Look != Vector2.zero))
            {
                Vector2 d = KeyOrPad.MouseDelta(InputReader.MouseSensitivity) * InputReader.MouseSensitivity; // + right stick
                if (InputReader.InvertY) d.y = -d.y;
                float free = od < OverdoseTimeline.FallEnd ? 0.25f : 1f;
                _odPeek += d * free;
            }
            _odPeek.x = Mathf.Clamp(_odPeek.x, -28f, 28f);
            _odPeek.y = Mathf.Clamp(_odPeek.y, -16f, 16f);
            _odPeek = Vector2.Lerp(_odPeek, Vector2.zero, 1f - Mathf.Exp(-0.5f * dt)); // the head drifts back slowly
            float peekW = 1f - Drunkenness.Seg(od, OverdoseTimeline.HandsOnKnees, OverdoseTimeline.End - 0.2f);

            OverdoseCamera(od, Motor.EyeHeight, seed, out var pos, out var rot);
            rot = rot * Quaternion.Euler(0f, _odPeek.x * peekW, 0f) * Quaternion.Euler(-_odPeek.y * peekW, 0f, 0f);
            _bob.EyeOverride = pos.y;
            _bob.EyeOverrideWeight = 1f;
            _bob.ExtraPosition = new Vector3(pos.x, 0f, pos.z);
            _bob.ExtraEuler = rot.eulerAngles;
            // The drunk sway rides on the body keys while standing; on the floor it would only make the view swim.
            _bob.DrunkSwayScale = 1f - Drunkenness.Seg(od, 0.3f, OverdoseTimeline.Knees) * (1f - Drunkenness.Seg(od, OverdoseTimeline.HandsOnKnees, OverdoseTimeline.End));
        }

        private void BeginOverdoseView()
        {
            _odView = true;
            _odHideVm = true;
            _odPeek = Vector2.zero;
            _odFlashWasOn = Flashlight != null && Flashlight.IsOn;
            if (_odFlashWasOn) Flashlight.SetOn(false, false);
        }

        private void EndOverdoseView()
        {
            bool finished = _odPrevT >= OverdoseTimeline.End - 0.5f;
            _odView = false;
            _odHideVm = false;
            if (_bob != null)
            {
                _bob.ExtraEuler = Vector3.zero;
                _bob.ExtraPosition = Vector3.zero;
                _bob.EyeOverrideWeight = 0f;
                _bob.DrunkSwayScale = 1f;
            }
            // Back on the feet: the flashlight comes back on if it was on (not after a cancelled collapse).
            if (finished && _odFlashWasOn && Flashlight != null && HasFlashlight && Life.Value == LifeState.Alive) Flashlight.SetOn(true, false);
            _odFlashWasOn = false;
        }

        /// <summary>
        /// First-person eye during the collapse (root space): the keys follow the head of the third-person body
        /// (<see cref="BlockyCharacterActions.SetOverdose"/>), on the floor the head turn and the fit come from the same
        /// noise as the body. <paramref name="pos"/>.y is the eye height.
        /// </summary>
        private static void OverdoseCamera(float t, float eye, float seed, out Vector3 pos, out Quaternion rot)
        {
            Quaternion lie = Quaternion.LookRotation(new Vector3(0f, 1f, -0.16f), new Vector3(0f, 0.16f, 1f));
            Quaternion side = Quaternion.LookRotation(new Vector3(1f, -0.12f, 0.1f), new Vector3(0.12f, 0.1f, 1f));
            Vector3 lyingPos = new Vector3(0f, 0.28f, 0.2f), sidePos = new Vector3(0.08f, 0.26f, 0.24f);

            if (t >= OverdoseTimeline.SeizeStart && t < OverdoseTimeline.GetUpStart)
            {
                Seizure(t - OverdoseTimeline.SeizeStart, seed, lie, lyingPos, out pos, out rot);
                return;
            }
            if (t < OverdoseTimeline.FallEnd)
            {
                Key(t, 0f, new Vector3(0f, eye, 0f), Quaternion.identity,
                    OverdoseTimeline.Lurch, new Vector3(0.12f, eye - 0.06f, 0f), Quaternion.Euler(-6f, 4f, -12f), out pos, out rot);
                if (t < OverdoseTimeline.Lurch) return;
                Key(t, OverdoseTimeline.Lurch, new Vector3(0.12f, eye - 0.06f, 0f), Quaternion.Euler(-6f, 4f, -12f),
                    OverdoseTimeline.StepBack, new Vector3(-0.08f, eye - 0.12f, -0.2f), Quaternion.Euler(-12f, -10f, 10f), out pos, out rot);
                if (t < OverdoseTimeline.StepBack) return;
                Key(t, OverdoseTimeline.StepBack, new Vector3(-0.08f, eye - 0.12f, -0.2f), Quaternion.Euler(-12f, -10f, 10f),
                    OverdoseTimeline.Clutch, new Vector3(0.03f, 1.27f, 0.2f), Quaternion.Euler(38f, 8f, -6f), out pos, out rot);
                if (t < OverdoseTimeline.Clutch) return;
                Key(t, OverdoseTimeline.Clutch, new Vector3(0.03f, 1.27f, 0.2f), Quaternion.Euler(38f, 8f, -6f),
                    OverdoseTimeline.Knees, new Vector3(-0.04f, 1.12f, 0.24f), Quaternion.Euler(42f, -6f, 14f), out pos, out rot, true);
                if (t < OverdoseTimeline.Knees) return;
                Key(t, OverdoseTimeline.Knees, new Vector3(-0.04f, 1.12f, 0.24f), Quaternion.Euler(42f, -6f, 14f),
                    OverdoseTimeline.Topple, sidePos, side, out pos, out rot, true);
                if (t < OverdoseTimeline.Topple) return;
                Seizure(0f, seed, lie, lyingPos, out var lp, out var lr);
                Key(t, OverdoseTimeline.Topple, sidePos, side, OverdoseTimeline.FallEnd, lp, lr, out pos, out rot);
                return;
            }

            // Getting up.
            Seizure(OverdoseTimeline.GetUpStart - OverdoseTimeline.SeizeStart, seed, lie, lyingPos, out var gp, out var gr);
            Quaternion fours = Quaternion.LookRotation(new Vector3(0f, -0.85f, 0.5f), new Vector3(0f, 0.5f, 0.85f));
            Vector3 foursPos = new Vector3(0f, 0.62f, 0.36f), kneelPos = new Vector3(0.02f, 1.12f, 0.38f), bentPos = new Vector3(0f, 1.2f, 0.4f);
            Quaternion kneel = Quaternion.Euler(30f, 2f, 3f), bent = Quaternion.Euler(32f, 0f, 0f);
            if (t < OverdoseTimeline.RollSide) Key(t, OverdoseTimeline.GetUpStart, gp, gr, OverdoseTimeline.RollSide, sidePos, side, out pos, out rot);
            else if (t < OverdoseTimeline.AllFours) Key(t, OverdoseTimeline.RollSide, sidePos, side, OverdoseTimeline.AllFours, foursPos, fours, out pos, out rot);
            else if (t < OverdoseTimeline.HalfKneel) Key(t, OverdoseTimeline.AllFours, foursPos, fours, OverdoseTimeline.HalfKneel, kneelPos, kneel, out pos, out rot);
            else if (t < OverdoseTimeline.HandsOnKnees) Key(t, OverdoseTimeline.HalfKneel, kneelPos, kneel, OverdoseTimeline.HandsOnKnees, bentPos, bent, out pos, out rot);
            else Key(t, OverdoseTimeline.HandsOnKnees, bentPos, bent, OverdoseTimeline.End, new Vector3(0f, eye, 0f), Quaternion.identity, out pos, out rot);
            // Dizzy on the way up: a slow, soft wobble of the head.
            float w = Drunkenness.Seg(t, OverdoseTimeline.GetUpStart, OverdoseTimeline.RollSide) * (1f - Drunkenness.Seg(t, OverdoseTimeline.HandsOnKnees, OverdoseTimeline.End));
            rot = rot * Quaternion.Euler(3f * Drunkenness.Fbm(t * 0.7f, seed + 81f) * w, 4f * Drunkenness.Fbm(t * 0.5f, seed + 83f) * w, 4f * Drunkenness.Fbm(t * 0.6f, seed + 85f) * w);
        }

        /// <summary>On the back during the fit: the head turns and snaps like the body's (same noise), plus a Perlin tremor.</summary>
        private static void Seizure(float u, float seed, Quaternion lie, Vector3 lyingPos, out Vector3 pos, out Quaternion rot)
        {
            float jerk = Drunkenness.Seizure(u, seed, out float tonic, out _);
            float limp = Drunkenness.Seg(u, OverdoseTimeline.SeizeDuration - 1.8f, OverdoseTimeline.SeizeDuration + 0.4f);
            float fit = 1f - limp;
            float trem = 0.9f * tonic + 0.3f * fit;
            float N(float f, float k) => Drunkenness.Fbm(u * f, seed + k);
            // Same head turn as the third-person pose, a bit less (the eyes keep some of the ceiling).
            float turn = 0.8f * (28f * N(0.45f, 11f) * fit + 38f * limp);
            float nod = -8f * tonic + 7f * jerk;
            Vector3 shake = new Vector3(N(7.9f, 41f), N(8.7f, 43f), N(7.3f, 45f)) * (2.2f * trem + 1.2f * fit) + new Vector3(0f, 0f, 2.5f * jerk * N(3.1f, 47f));
            rot = lie * Quaternion.Euler(0f, turn, 0f) * Quaternion.Euler(nod, 0f, 0f) * Quaternion.Euler(shake);
            pos = lyingPos + new Vector3(0.012f * N(6.1f, 49f) * trem, 0.022f * jerk, 0.01f * N(5.7f, 51f) * trem);
        }

        private static void Key(float t, float t0, Vector3 p0, Quaternion r0, float t1, Vector3 p1, Quaternion r1,
            out Vector3 pos, out Quaternion rot, bool easeIn = false)
        {
            float x = ActionTimeline.Remap01(t, t0, t1);
            x = easeIn ? ActionTimeline.EaseIn(x) : ActionTimeline.SmoothStep01(x);
            pos = Vector3.Lerp(p0, p1, x);
            rot = Quaternion.Slerp(r0, r1, x);
        }

        // ================================================================ heartbeat (owner, 2D)

        /// <summary>
        /// Owner only (nobody else hears it): from <see cref="Drunkenness.HeartCans"/> a heartbeat that gets louder and
        /// faster up to <see cref="Drunkenness.CollapseCans"/>; pounding and irregular through the collapse, then it
        /// calms down slowly. Single beats (<see cref="StressSounds.Heartbeat"/>), so the rate is exact.
        /// </summary>
        private void UpdateHeart(float od)
        {
            float dt = Time.deltaTime, now = Time.time;
            var mm = MatchManager.Instance;
            bool lobby = mm != null && mm.Phase.Value == MatchPhase.Lobby;
            if (!lobby || Life.Value != LifeState.Alive)
            {
                _heartVol = 0f;
                _nextHeartBeat = now;
                return;
            }

            float level = DrunkLevel;
            float h = Drunkenness.Heart(level);
            float on = Drunkenness.Seg(level, Drunkenness.HeartCans - 0.15f, Drunkenness.HeartCans);
            float vol = on * Mathf.Lerp(0.14f, 0.55f, h);
            float bpm = Mathf.Lerp(92f, 138f, h);
            if (od >= 0f)
            {
                // Pounding from the first stagger, racing through the fit; on the way up it starts to settle.
                float settle = Drunkenness.Seg(od, OverdoseTimeline.SeizeEnd, OverdoseTimeline.End);
                vol = Mathf.Lerp(1f, Mathf.Max(vol, 0.6f), settle);
                bpm = Mathf.Lerp(od < OverdoseTimeline.FallEnd ? 150f : 172f, Mathf.Max(bpm, 128f), settle);
            }
            _heartVol = Mathf.Lerp(_heartVol, vol, 1f - Mathf.Exp(-(vol > _heartVol ? 2.5f : 0.18f) * dt));
            _heartBpm = Mathf.Lerp(_heartBpm, bpm, 1f - Mathf.Exp(-(bpm > _heartBpm ? 1.5f : 0.15f) * dt));
            if (_heartVol < 0.015f)
            {
                _nextHeartBeat = Mathf.Max(_nextHeartBeat, now);
                return;
            }
            if (now < _nextHeartBeat) return;

            var src = GameAudio.PlayClip2D(StressSounds.Heartbeat(_heartBpm), _heartVol, GameSfx.HeartbeatLoop);
            if (src != null) src.priority = 20;
            HeartBeats++;
            float interval = 60f / _heartBpm;
            bool arrhythmic = od >= OverdoseTimeline.Clutch && od < OverdoseTimeline.SeizeEnd;
            if (arrhythmic) interval *= Random.value < 0.14f ? Random.Range(0.5f, 0.6f) : Random.Range(0.8f, 1.22f);
            else interval *= Random.Range(0.97f, 1.03f);
            _nextHeartBeat = now + interval;
        }
    }
}
