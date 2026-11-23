using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Live blur of the game behind open menus (the game keeps running - multiplayer). Any object with a
    /// <see cref="MenuBlurRequest"/> that is enabled keeps the blur on; the driver fades it in / out (0.2 s) and keeps
    /// the effect on whichever camera currently draws the screen (own view, TPP, spectator, menu scene).
    /// </summary>
    public class MenuBlur : MonoBehaviour
    {
        public static int Requests;
        private static MenuBlur _driver;
        private float _amount;
        private MenuBlurEffect _fx;

        public static float Amount => _driver != null ? _driver._amount : 0f;

        public static void Ensure()
        {
            if (_driver != null) return;
            var go = new GameObject("MenuBlur");
            DontDestroyOnLoad(go);
            _driver = go.AddComponent<MenuBlur>();
        }

        private void Update()
        {
            float target = Requests > 0 ? 1f : 0f;
            _amount = Mathf.MoveTowards(_amount, target, Time.unscaledDeltaTime / 0.2f);
            var cam = ScreenCamera();
            if (_fx != null && (_fx.Cam != cam || cam == null))
            {
                _fx.Amount = 0f;
                _fx.enabled = false;
                _fx = null;
            }
            if (_amount <= 0.001f)
            {
                if (_fx != null) _fx.enabled = false;
                return;
            }
            if (cam == null) return;
            if (_fx == null)
            {
                _fx = cam.GetComponent<MenuBlurEffect>();
                if (_fx == null) _fx = cam.gameObject.AddComponent<MenuBlurEffect>();
                _fx.Cam = cam;
            }
            _fx.enabled = true;
            float e = _amount * _amount * (3f - 2f * _amount);
            _fx.Amount = e;
        }

        /// <summary>The enabled camera that renders last to the screen (highest depth).</summary>
        private static Camera ScreenCamera()
        {
            Camera best = null;
            foreach (var c in Camera.allCameras)
            {
                if (c.targetTexture != null || !c.isActiveAndEnabled) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
            return best;
        }
    }

    /// <summary>Put on a menu root: while it is enabled the game behind it is blurred.</summary>
    public class MenuBlurRequest : MonoBehaviour
    {
        private bool _counted;

        private void OnEnable()
        {
            MenuBlur.Ensure();
            if (!_counted) { MenuBlur.Requests++; _counted = true; }
        }

        private void OnDisable()
        {
            if (_counted) { MenuBlur.Requests = Mathf.Max(0, MenuBlur.Requests - 1); _counted = false; }
        }
    }

    /// <summary>Image effect on the screen camera (added / switched by <see cref="MenuBlur"/>).</summary>
    [RequireComponent(typeof(Camera))]
    public class MenuBlurEffect : MonoBehaviour
    {
        public Camera Cam;
        public float Amount;
        private static Material _mat;
        private static readonly int DirId = Shader.PropertyToID("_Dir");
        private static readonly int BlurId = Shader.PropertyToID("_BlurTex");
        private static readonly int AmountId = Shader.PropertyToID("_Amount");

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/HnsMenuBlur");
                if (sh == null || !sh.isSupported) { Graphics.Blit(src, dst); return; }
                _mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            if (Amount <= 0.001f) { Graphics.Blit(src, dst); return; }
            int w = Mathf.Max(2, src.width / 4), h = Mathf.Max(2, src.height / 4);
            var a = RenderTexture.GetTemporary(w, h, 0, src.format);
            var b = RenderTexture.GetTemporary(w, h, 0, src.format);
            a.filterMode = b.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, a, _mat, 0);
            float spread = Mathf.Lerp(0.4f, 1.6f, Amount);
            for (int i = 0; i < 3; i++)
            {
                _mat.SetVector(DirId, new Vector4(spread * (1f + i), 0f, 0f, 0f));
                Graphics.Blit(a, b, _mat, 1);
                _mat.SetVector(DirId, new Vector4(0f, spread * (1f + i), 0f, 0f));
                Graphics.Blit(b, a, _mat, 1);
            }
            _mat.SetTexture(BlurId, a);
            _mat.SetFloat(AmountId, Amount);
            Graphics.Blit(src, dst, _mat, 2);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }
    }
}
