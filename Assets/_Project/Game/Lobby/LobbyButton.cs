using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Big green start button in the lobby room. Press with E.</summary>
    public class LobbyButton : MonoBehaviour
    {
        public static LobbyButton Instance { get; private set; }

        [SerializeField] private Transform cap;
        [SerializeField] private Light glow;
        [SerializeField] private Renderer capRenderer;

        private Vector3 _capRest;
        private float _press;
        private MaterialPropertyBlock _mpb;

        private void OnEnable() => Instance = this;
        private void OnDisable() { if (Instance == this) Instance = null; }

        private void Start()
        {
            if (cap != null) _capRest = cap.localPosition;
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>Point the player aims at (top of the green cap).</summary>
        public Vector3 AimPoint => cap != null ? cap.position + Vector3.up * 0.05f : transform.position + Vector3.up * 1.15f;

        public void Press()
        {
            var mm = MatchManager.Instance;
            if (mm == null) return;
            mm.ButtonPressedRpc(transform.position);
            mm.RequestStartRpc();
        }

        /// <summary>Visual press (called on every client via MatchManager.ButtonPressedRpc).</summary>
        public void Animate() => _press = 1f;

        private void Update()
        {
            _press = Mathf.MoveTowards(_press, 0f, Time.deltaTime * 2.5f);
            if (cap != null) cap.localPosition = _capRest + Vector3.down * (0.05f * Mathf.Clamp01(_press * 2f));
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            if (glow != null) glow.intensity = 1.6f * pulse + _press * 3f;
            if (capRenderer != null)
            {
                capRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", new Color(0.1f, 0.9f, 0.2f) * (1.2f * pulse + _press * 2f));
                capRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
