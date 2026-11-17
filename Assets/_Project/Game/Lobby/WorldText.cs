using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Makes a TextMesh depth-tested (drawn behind players / walls instead of on top) and, optionally,
    /// shrinks it so it always fits inside a frame of <see cref="fitSize"/> metres (e.g. the lobby board).
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class WorldText : MonoBehaviour
    {
        [SerializeField] private Material material;
        [Tooltip("Maximum width/height in metres (0 = no fitting).")]
        [SerializeField] private Vector2 fitSize;

        private TextMesh _text;
        private MeshRenderer _renderer;
        private Material _instance;
        private Vector3 _baseScale;

        private void Awake()
        {
            _text = GetComponent<TextMesh>();
            _renderer = GetComponent<MeshRenderer>();
            _baseScale = transform.localScale;
            if (material != null && _text.font != null)
            {
                _instance = new Material(material);
                _renderer.sharedMaterial = _instance;
                RefreshTexture();
                Font.textureRebuilt += OnFontRebuilt;
            }
        }

        private void OnDestroy() => Font.textureRebuilt -= OnFontRebuilt;

        private void OnFontRebuilt(Font f)
        {
            if (_text != null && f == _text.font) RefreshTexture();
        }

        private void RefreshTexture()
        {
            if (_instance != null && _text.font != null) _instance.mainTexture = _text.font.material.mainTexture;
        }

        private void LateUpdate()
        {
            RefreshTexture();
            if (fitSize.x <= 0f || fitSize.y <= 0f) return;

            // Bounds are world-space; measure along the text's own right/up axes.
            Bounds b = _renderer.bounds;
            Vector3 ext = b.extents;
            float w = 2f * (Mathf.Abs(Vector3.Dot(transform.right, Vector3.right)) * ext.x + Mathf.Abs(Vector3.Dot(transform.right, Vector3.forward)) * ext.z);
            float h = 2f * ext.y;
            if (w <= 0.0001f || h <= 0.0001f) return;

            float current = transform.localScale.x / Mathf.Max(0.0001f, _baseScale.x);
            float fit = Mathf.Min(fitSize.x / w, fitSize.y / h) * current;
            float k = Mathf.Min(1f, fit);
            if (Mathf.Abs(k - current) > 0.005f) transform.localScale = _baseScale * k;
        }
    }
}
