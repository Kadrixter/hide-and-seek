using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Claude, the orange mascot in the lobby corner (model: MultiplayerBuilder.LobbyClaudeModel). Purely local idle
    /// animation so it never stands still: breathing, a slow sway, blinks, turning towards the local player when they
    /// come close, little foot shuffles and an arm wave now and then. E (LocalPlayerController, PromptKind.Claude) makes
    /// it hop and opens the survey (LobbyClaude.Survey.cs).
    /// </summary>
    public partial class LobbyClaude : MonoBehaviour
    {
        private static readonly List<LobbyClaude> All = new();

        /// <summary>The Claude of the real lobby (the dead-lobby copy far away is skipped).</summary>
        public static LobbyClaude Instance
        {
            get
            {
                foreach (var c in All)
                    if (c != null && c.isActiveAndEnabled && Mathf.Abs(c.transform.position.z) < 500f) return c;
                return null;
            }
        }

        /// <summary>Where the E prompt sits (top of the body, a bit in front).</summary>
        public Vector3 AimPoint => transform.TransformPoint(new Vector3(0f, 0.72f, 0.2f));

        private Transform _rig, _body, _eyeL, _eyeR, _armL, _armR;
        private readonly Transform[] _legs = new Transform[4];
        private Vector3 _legBase0;
        private float _yaw, _nextBlink, _blinkT = -1f, _nextWave, _waveT = -1f, _waveSide = 1f, _nextShuffle, _shuffleT = -1f, _hopT = -1f, _happyUntil;
        private float _seed;

        private void Awake()
        {
            _rig = transform.Find("Rig");
            if (_rig == null) { enabled = false; return; }
            _body = _rig.Find("Body");
            _eyeL = _body.Find("EyeL");
            _eyeR = _body.Find("EyeR");
            _armL = _body.Find("ArmL");
            _armR = _body.Find("ArmR");
            for (int i = 0; i < 4; i++) _legs[i] = _rig.Find("Leg" + i);
            _legBase0 = _legs[0] != null ? _legs[0].localPosition : Vector3.zero;
            _seed = Random.value * 10f;
            float now = Time.time;
            _nextBlink = now + Random.Range(1.5f, 4f);
            _nextWave = now + Random.Range(6f, 12f);
            _nextShuffle = now + Random.Range(3f, 7f);
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Somebody pressed E: a happy hop.</summary>
        public void Poke()
        {
            _hopT = 0f;
            _happyUntil = Time.time + 1.6f;
        }

        private void Update()
        {
            float t = Time.time + _seed, dt = Time.deltaTime;

            // ---- turn towards the local player (within 6 m), otherwise drift back to the room
            float target = 0f;
            var me = NetPlayer.Local;
            if (me != null)
            {
                Vector3 d = transform.InverseTransformPoint(me.transform.position);
                d.y = 0f;
                if (d.magnitude < 6f && d.z > -0.2f) target = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -38f, 38f);
            }
            if (SurveyOpen) target *= 1.15f;
            target += Mathf.Sin(t * 0.23f) * 6f; // idle look-around
            _yaw = Mathf.LerpAngle(_yaw, target, 1f - Mathf.Exp(-dt * 2.5f));

            // ---- hop (E)
            float hopY = 0f, squash = 0f;
            if (_hopT >= 0f)
            {
                _hopT += dt;
                const float dur = 0.55f;
                float k = _hopT / dur;
                if (k < 0.18f) squash = Mathf.Sin(k / 0.18f * Mathf.PI) * 0.12f;           // crouch
                else if (k < 1f) hopY = Mathf.Sin((k - 0.18f) / 0.82f * Mathf.PI) * 0.22f; // up and down
                else if (k < 1.3f) squash = Mathf.Sin((k - 1f) / 0.3f * Mathf.PI) * 0.08f; // landing
                else _hopT = -1f;
            }
            _rig.localPosition = new Vector3(0f, hopY, 0f);
            _rig.localRotation = Quaternion.Euler(0f, _yaw, Mathf.Sin(t * 0.9f) * 1.6f);

            // ---- breathing
            float breath = Mathf.Sin(t * 2.1f);
            float sy = 1f + breath * 0.018f - squash + hopY * 0.25f;
            float sxz = 1f - breath * 0.008f + squash * 0.6f - hopY * 0.1f;
            _body.localScale = new Vector3(sxz, sy, sxz);
            _body.localRotation = Quaternion.Euler(SurveyOpen ? Mathf.Max(0f, Mathf.Sin(t * 1.3f)) * 4f : Mathf.Sin(t * 0.7f) * 1.2f, 0f, 0f); // nods while you type

            // ---- blinks (happy squint after a poke)
            if (_blinkT < 0f && Time.time >= _nextBlink) { _blinkT = 0f; _nextBlink = Time.time + Random.Range(2.5f, 5.5f) + (Random.value < 0.2f ? -2.2f : 0f); }
            float eye = 1f;
            if (_blinkT >= 0f)
            {
                _blinkT += dt;
                eye = 1f - Mathf.Sin(Mathf.Clamp01(_blinkT / 0.14f) * Mathf.PI) * 0.9f;
                if (_blinkT >= 0.14f) _blinkT = -1f;
            }
            if (Time.time < _happyUntil) eye = Mathf.Min(eye, 0.35f);
            _eyeL.localScale = _eyeR.localScale = new Vector3(1f, eye, 1f);

            // ---- arms: gentle swing, a wave now and then (and on a poke)
            if (_waveT < 0f && (Time.time >= _nextWave || (_hopT >= 0f && _hopT < dt * 1.5f)))
            {
                _waveT = 0f;
                _waveSide = Random.value < 0.5f ? -1f : 1f;
                _nextWave = Time.time + Random.Range(8f, 15f);
            }
            float waveL = 0f, waveR = 0f;
            if (_waveT >= 0f)
            {
                _waveT += dt;
                const float wd = 1.6f;
                float env = Mathf.Sin(Mathf.Clamp01(_waveT / wd) * Mathf.PI);
                float w = env * (55f + Mathf.Sin(_waveT * 16f) * 18f);
                if (_waveSide < 0f) waveL = w; else waveR = w;
                if (_waveT >= wd) _waveT = -1f;
            }
            float swing = Mathf.Sin(t * 2.1f + 0.6f) * 4f;
            _armL.localRotation = Quaternion.Euler(0f, 0f, -swing - waveL - hopY * 120f);
            _armR.localRotation = Quaternion.Euler(0f, 0f, swing + waveR + hopY * 120f);

            // ---- legs: a little shuffle (step in place) every few seconds
            if (_shuffleT < 0f && Time.time >= _nextShuffle) { _shuffleT = 0f; _nextShuffle = Time.time + Random.Range(4f, 9f); }
            for (int i = 0; i < 4; i++)
            {
                if (_legs[i] == null) continue;
                float lift = 0f;
                if (_shuffleT >= 0f)
                {
                    float ph = _shuffleT * 7f - (i % 2) * Mathf.PI;
                    if (ph > 0f && ph < Mathf.PI * 3f) lift = Mathf.Max(0f, Mathf.Sin(ph)) * 0.035f;
                }
                var p = _legs[i].localPosition;
                _legs[i].localPosition = new Vector3(p.x, _legBase0.y + lift, p.z);
                _legs[i].localRotation = Quaternion.Euler(hopY > 0.01f ? 18f : 0f, 0f, 0f);
            }
            if (_shuffleT >= 0f)
            {
                _shuffleT += dt;
                if (_shuffleT > 1.4f) _shuffleT = -1f;
            }
        }
    }
}
