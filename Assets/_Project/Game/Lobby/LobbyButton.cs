using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Start lever on the lobby wall (E). Only the room owner can pull it, and only when everyone is ready.
    /// The status lamp is red while someone is not ready and green when the round can start.
    /// </summary>
    public class LobbyButton : MonoBehaviour
    {
        public static LobbyButton Instance { get; private set; }

        [Tooltip("Pivot of the lever arm (rotates around local X).")]
        [SerializeField] private Transform handle;
        [SerializeField] private Transform knob;
        [SerializeField] private Renderer lamp;
        [SerializeField] private Light glow;
        [SerializeField] private float upAngle = -40f;
        [SerializeField] private float downAngle = 50f;

        private float _pull;
        private MaterialPropertyBlock _mpb;

        private void OnEnable() => Instance = this;
        private void OnDisable() { if (Instance == this) Instance = null; }

        private void Start() => _mpb = new MaterialPropertyBlock();

        /// <summary>Point the player aims at (the knob).</summary>
        public Vector3 AimPoint => knob != null ? knob.position : transform.position + Vector3.up * 1.3f;

        public void Press()
        {
            var mm = MatchManager.Instance;
            if (mm == null) return;
            mm.ButtonPressedRpc(transform.position);
            mm.RequestStartRpc();
        }

        /// <summary>Visual pull (called on every client via MatchManager.ButtonPressedRpc).</summary>
        public void Animate() => _pull = 1f;

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
            _pull = Mathf.MoveTowards(_pull, 0f, Time.deltaTime * 1.4f);
            // Quick pull down, slow spring back.
            float t = _pull > 0.75f ? (1f - _pull) / 0.25f : _pull / 0.75f;
            if (handle != null) handle.localRotation = Quaternion.Euler(Mathf.Lerp(upAngle, downAngle, Mathf.Clamp01(t)), 0f, 0f);

            bool go = EveryoneReady(out _, out _);
            Color c = go ? new Color(0.15f, 1f, 0.3f) : new Color(1f, 0.18f, 0.12f);
            float pulse = go ? 0.8f + 0.2f * Mathf.Sin(Time.time * 4f) : 0.55f + 0.1f * Mathf.Sin(Time.time * 2f);
            if (glow != null)
            {
                glow.color = c;
                glow.intensity = 1.4f * pulse;
            }
            if (lamp != null)
            {
                lamp.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", c);
                _mpb.SetColor("_EmissionColor", c * (1.6f * pulse));
                lamp.SetPropertyBlock(_mpb);
            }
        }
    }
}
