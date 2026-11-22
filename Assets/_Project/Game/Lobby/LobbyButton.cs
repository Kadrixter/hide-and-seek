using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Big double start lever on the lobby wall (E). Only the room owner can pull it, and only when everyone is ready;
    /// otherwise it is locked (bolt through the arm, red lamp) and only rattles. The pull itself is a shared
    /// animation (<see cref="LeverTimeline"/>): the puller drags the red bar down with the whole body, the blades
    /// slam into the lower jaws with sparks, then the server starts the round. The model is built at runtime
    /// (<see cref="StartLeverModel"/>); the scene keeps only the root with a collider body.
    /// </summary>
    public class LobbyButton : MonoBehaviour
    {
        public static LobbyButton Instance { get; private set; }
        /// <summary>Raised on every client when a pull starts (tests record from here).</summary>
        public static event System.Action PullStarted;

        StartLeverModel _m;
        MaterialPropertyBlock _mpb;
        float _pullT = -1f, _jamT = 9f, _resetT = 9f, _bolt;
        float _shownThrow;

        private void OnEnable() => Instance = this;
        private void OnDisable() { if (Instance == this) Instance = null; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            var body = transform.Find("Body");
            var template = body != null ? body.GetComponent<Renderer>().sharedMaterial : GetComponentInChildren<Renderer>(true).sharedMaterial;
            // Older scenes still carry the previous dressing: hide everything but the collider body.
            foreach (Transform c in transform)
                if (c != body) c.gameObject.SetActive(false);
            if (body != null) body.GetComponent<Renderer>().enabled = false;
            _m = StartLeverModel.Build(transform, template);
        }

        /// <summary>Point the player aims at (middle of the red bar).</summary>
        public Vector3 AimPoint => _m != null ? _m.Bar.position : transform.position + Vector3.up * 1.3f;

        /// <summary>Hand point on the red bar: 0 = puller's right hand, 1 = left (the puller faces the wall).</summary>
        public Vector3 GripWorld(int hand) => _m.Pivot.TransformPoint(new Vector3(hand == 0 ? -0.15f : 0.15f, StartLeverModel.ArmLength, 0f));

        /// <summary>Where the puller stands (feet) and which way he faces.</summary>
        public Vector3 StandPoint => transform.TransformPoint(new Vector3(0f, 0.02f, LeverTimeline.StandDistance));
        public float StandYaw => Quaternion.LookRotation(-transform.forward).eulerAngles.y;

        /// <summary>True from the start of a pull until the round starts (or the lever is reset).</summary>
        public bool Pulling => _pullT >= 0f;

        public void Press()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.RequestLeverRpc();
        }

        /// <summary>Everybody: the lever starts going down (same clock as the puller's body).</summary>
        public void BeginPull()
        {
            _pullT = 0f;
            _jamT = 9f;
            PullStarted?.Invoke();
        }

        /// <summary>Everybody: someone tried to pull while it is locked - it only rattles.</summary>
        public void Jam()
        {
            if (Pulling) return;
            _jamT = 0f;
            GameAudio.PlayAt(GameSfx.GlockDryFire, AimPoint, 0.9f);
        }

        /// <summary>Everybody: the round did not start after all, the lever springs back up.</summary>
        public void ResetLever()
        {
            _pullT = -1f;
            _resetT = 0f;
        }

        /// <summary>Test helper: shows the lever thrown to this amount (0 up, 1 down), or -1 for normal.</summary>
        public float DebugThrow { get; set; } = -1f;

        public static bool EveryoneReady(out int ready, out int need)
        {
            ready = need = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.OwnerClientId == NetworkManager.ServerClientId) continue;
                need++;
                if (p.Ready.Value) ready++;
            }
            return need > 0 && ready == need;
        }

        private void Update()
        {
            if (_m == null) return;
            float dt = Time.deltaTime;
            float throwAmt;
            if (_pullT >= 0f)
            {
                float prev = _pullT;
                _pullT += dt;
                throwAmt = LeverTimeline.Throw(_pullT);
                Events(prev, _pullT);
            }
            else
            {
                // Spring back after a reset.
                _resetT += dt;
                throwAmt = _resetT > 1f ? 0f : Mathf.Min(_shownThrow, 1f - LeverTimeline.S(_resetT, 0f, 0.8f));
            }
            if (DebugThrow >= 0f) throwAmt = DebugThrow;
            _shownThrow = throwAmt;

            _jamT += dt;
            float rattle = _jamT < 0.7f ? Mathf.Sin(_jamT * 55f) * 3.5f * Mathf.Exp(-_jamT * 5f) : 0f;
            float angle = Mathf.LerpUnclamped(StartLeverModel.UpAngle, StartLeverModel.DownAngle, throwAmt) + rattle;
            _m.Pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);

            bool go = EveryoneReady(out _, out _);
            // Lock bolt: in while not everyone is ready (and not mid-pull).
            bool locked = !go && !Pulling && DebugThrow < 0f;
            _bolt = Mathf.MoveTowards(_bolt, locked ? 1f : 0f, dt * 4f);
            var bp = _m.Bolt.localPosition;
            bp.x = Mathf.Lerp(StartLeverModel.BoltOut, StartLeverModel.BoltIn, _bolt) + (locked ? rattle * 0.002f : 0f);
            _m.Bolt.localPosition = bp;

            // Lamp: red = waiting, green = ready, flashing white-green when the blades hit the jaws.
            Color c = go || Pulling ? new Color(0.15f, 1f, 0.3f) : new Color(1f, 0.18f, 0.12f);
            float pulse = go ? 0.8f + 0.2f * Mathf.Sin(Time.time * 4f) : 0.55f + 0.1f * Mathf.Sin(Time.time * 2f);
            if (_jamT < 0.7f) pulse *= 0.6f + 0.8f * Mathf.Abs(Mathf.Sin(_jamT * 18f));
            if (_pullT >= LeverTimeline.Latch)
            {
                float flash = Mathf.Exp(-(_pullT - LeverTimeline.Latch) * 3f);
                c = Color.Lerp(c, Color.white, 0.6f * flash);
                pulse = 1.2f + 1.5f * flash * Mathf.Abs(Mathf.Sin(_pullT * 30f));
            }
            _m.Glow.color = c;
            _m.Glow.intensity = 1.4f * pulse;
            _m.Lamp.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", c);
            _mpb.SetColor("_EmissionColor", c * (1.6f * pulse));
            _m.Lamp.SetPropertyBlock(_mpb);
        }

        /// <summary>Sounds and sparks at the timeline marks crossed this frame.</summary>
        void Events(float prev, float now)
        {
            bool Cross(float mark) => prev < mark && now >= mark;
            Vector3 bar = AimPoint;
            if (Cross(LeverTimeline.Grip)) GameAudio.PlayAt(GameSfx.RcPlace, bar, 0.7f);
            if (Cross(LeverTimeline.Unstick)) GameAudio.PlayAt(GameSfx.GlockDryFire, bar, 1f);
            if (Cross(LeverTimeline.Latch))
            {
                GameAudio.PlayAt(GameSfx.LobbyButtonPress, bar, 1f);
                GameAudio.PlayAt(GameSfx.ForkSparks, transform.TransformPoint(_m.LowerJaws[0]), 0.35f);
                foreach (var j in _m.LowerJaws) LeverSparks.Burst(transform.TransformPoint(j), transform.forward);
            }
        }
    }
}
