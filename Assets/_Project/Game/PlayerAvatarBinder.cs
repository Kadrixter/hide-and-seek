using HideAndSeek.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Attaches the blocky character model to a player and offers a third-person
    /// preview camera (V) so the model and its animations can be inspected in beta builds.
    /// </summary>
    public class PlayerAvatarBinder : MonoBehaviour
    {
        /// <summary>Layer for the local player's own body (see BetaSceneBuilder.EnsureLayer).</summary>
        public const int LocalBodyLayer = 8;

        [SerializeField] private int skinIndex;
        [SerializeField] private string playerNick = "Player";
        [SerializeField] private bool isLocalPlayer = true;

        [Header("Third-person preview")]
        [SerializeField] private float tpDistance = 3.2f;
        [SerializeField] private float tpHeight = 0.4f;
        [SerializeField] private float tpSide = 0.6f;
        [SerializeField] private float tpSmoothing = 12f;

        private BlockyCharacter _character;
        private Camera _fpCamera;
        private Camera _tpCamera;
        private bool _thirdPerson;
        private Vector3 _tpVelocity;

        public BlockyCharacter Character => _character;

        private void Start()
        {
            Transform root = transform.Find("CharacterModelRoot");
            if (root == null)
            {
                root = new GameObject("CharacterModelRoot").transform;
                root.SetParent(transform, false);
            }

            _character = BlockyCharacterFactory.Create(root, skinIndex, playerNick);
            _character.SetFirstPersonMode(isLocalPlayer);

            if (!isLocalPlayer) return;

            // The flashlight sits inside the head: keep our own body out of its light and shadows.
            _character.SetLayer(LocalBodyLayer);
            foreach (var l in GetComponentsInChildren<Light>(true))
                l.cullingMask &= ~(1 << LocalBodyLayer);

            _fpCamera = GetComponentInChildren<Camera>();
            var tpGo = new GameObject("ThirdPersonPreviewCamera");
            _tpCamera = tpGo.AddComponent<Camera>();
            if (_fpCamera != null)
            {
                _tpCamera.CopyFrom(_fpCamera);
                _tpCamera.depthTextureMode = _fpCamera.depthTextureMode;
            }
            _tpCamera.enabled = false;
        }

        private void Update()
        {
            if (!isLocalPlayer || _character == null) return;

            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame)
                SetThirdPerson(!_thirdPerson);

            if (kb != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (kb[Key.Digit1 + i].wasPressedThisFrame)
                    {
                        skinIndex = i;
                        _character.SetSkin(skinIndex, playerNick);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (!_thirdPerson || _fpCamera == null) return;

            Transform fp = _fpCamera.transform;
            Vector3 pivot = transform.position + Vector3.up * (1.5f + tpHeight);
            Vector3 desired = pivot - fp.forward * tpDistance + fp.right * tpSide;

            // Keep the camera out of walls.
            Vector3 dir = desired - pivot;
            if (Physics.SphereCast(pivot, 0.2f, dir.normalized, out RaycastHit hit, dir.magnitude,
                    ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                desired = pivot + dir.normalized * Mathf.Max(0.3f, hit.distance - 0.1f);

            Transform tp = _tpCamera.transform;
            tp.position = Vector3.SmoothDamp(tp.position, desired, ref _tpVelocity, 1f / tpSmoothing);
            tp.rotation = Quaternion.LookRotation(pivot + fp.forward * 6f - tp.position);
        }

        private void SetThirdPerson(bool on)
        {
            if (_fpCamera == null) return;
            _thirdPerson = on;
            _character.SetFirstPersonMode(!on);
            _tpCamera.transform.SetPositionAndRotation(_fpCamera.transform.position, _fpCamera.transform.rotation);
            _tpCamera.fieldOfView = _fpCamera.fieldOfView;
            _tpCamera.enabled = on;
            // Keep the FP camera's transform alive (it drives the flashlight), just stop it rendering.
            _fpCamera.enabled = !on;
        }
    }
}
