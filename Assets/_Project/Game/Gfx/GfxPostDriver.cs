using UnityEngine;

namespace HideAndSeek.Game.Gfx
{
    /// <summary>
    /// Keeps <see cref="GfxPost"/> on the camera that renders last to the screen (highest depth among enabled,
    /// full-screen cameras without a target texture): the hands camera in first person, the third-person / spectator /
    /// kill / RC cameras when they take over, the menu camera. Cameras rendering into textures (mirror, monitors,
    /// previews) are never touched. Runs at the end of every frame, before rendering.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class GfxPostDriver : MonoBehaviour
    {
        private static GfxPostDriver _instance;
        private readonly Camera[] _cams = new Camera[64];
        private GfxPost _current;

        public static Camera FinalCamera { get; private set; }

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[GfxPost]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GfxPostDriver>();
        }

        private void LateUpdate()
        {
            int n = Camera.GetAllCameras(_cams);
            Camera best = null;
            for (int i = 0; i < n; i++)
            {
                var c = _cams[i];
                _cams[i] = null;
                if (c == null || c.targetTexture != null || c.cameraType != CameraType.Game) continue;
                Rect r = c.rect;
                if (r.x > 0.001f || r.y > 0.001f || r.width < 0.999f || r.height < 0.999f) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
            FinalCamera = best;
            bool want = GfxPost.Wanted && best != null;
            GfxPost post = want ? best.GetComponent<GfxPost>() : null;
            if (want && post == null) post = best.gameObject.AddComponent<GfxPost>();
            if (_current != null && _current != post) _current.enabled = false;
            if (post != null && !post.enabled) post.enabled = true;
            _current = post;
        }
    }
}
