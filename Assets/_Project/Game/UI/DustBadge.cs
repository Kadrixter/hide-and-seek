using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Small round icon in the bottom right corner (hider, seeking phase, alive): dust in the seeker's eyes. Cooling
    /// down = dark sweep + seconds left; ready = bright with an "X" key cap. Replaces the two status lines
    /// "Kurz w oczy (X): za 0:12" / "X - rzuć kurz w oczy szukającemu".
    /// </summary>
    public sealed class DustBadge : MonoBehaviour
    {
        private const float Size = 58f;

        private CanvasGroup _group;
        private Image _icon, _sweep, _ring, _cap;
        private Text _secs;
        private float _shown;
        private bool _wasReady;
        private float _pop;

        public static DustBadge Create(Transform canvasRoot)
        {
            var root = UiKit.Rect("DustBadge", canvasRoot);
            root.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-26, 24), new Vector2(Size, Size));
            var b = root.gameObject.AddComponent<DustBadge>();
            b._group = root.gameObject.AddComponent<CanvasGroup>();
            b._group.alpha = 0f;
            b._group.blocksRaycasts = false;

            var bg = UiKit.Image("Bg", root, new Color(0.035f, 0.04f, 0.045f, 0.7f));
            bg.sprite = UiStyle.Circle;
            bg.rectTransform.Stretch();
            bg.raycastTarget = false;
            b._icon = UiKit.Image("Cloud", root, Color.white);
            b._icon.sprite = CloudSprite();
            b._icon.raycastTarget = false;
            b._icon.rectTransform.Stretch(9f);
            b._sweep = UiKit.Image("Sweep", root, new Color(0f, 0f, 0f, 0.62f));
            b._sweep.sprite = UiStyle.Circle;
            b._sweep.type = Image.Type.Filled;
            b._sweep.fillMethod = Image.FillMethod.Radial360;
            b._sweep.fillOrigin = (int)Image.Origin360.Top;
            b._sweep.fillClockwise = false;
            b._sweep.rectTransform.Stretch();
            b._sweep.raycastTarget = false;
            b._ring = UiKit.Image("Ring", root, Color.white);
            b._ring.sprite = RingSprite();
            b._ring.rectTransform.Stretch();
            b._ring.raycastTarget = false;
            b._secs = UiKit.Label("Secs", root, "", 22, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            b._secs.rectTransform.Stretch();
            b._secs.AddOutline(1.5f, 0.8f);

            b._cap = UiKit.Image("Key", root, new Color(0.93f, 0.93f, 0.9f, 0.95f)).Rounded(true);
            b._cap.raycastTarget = false;
            b._cap.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(6, 6), new Vector2(24, 24));
            var key = UiKit.Label("K", b._cap.transform, "X", 16, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f), FontStyle.Bold);
            key.rectTransform.Stretch();
            return b;
        }

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            bool show = me != null && mm != null && mm.Phase.Value == MatchPhase.Seeking && me.IsHider && me.Life.Value == LifeState.Alive;
            _shown = Mathf.MoveTowards(_shown, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _group.alpha = _shown;
            if (!show) return;

            double left = me.DustReadyAt.Value - mm.Now;
            bool ready = left <= 0d;
            if (ready && !_wasReady) _pop = 1f;
            _wasReady = ready;
            _pop = Mathf.MoveTowards(_pop, 0f, Time.unscaledDeltaTime * 2.5f);

            _sweep.fillAmount = ready ? 0f : Mathf.Clamp01((float)(left / Tuning.DustCooldown));
            _icon.color = ready ? new Color(0.93f, 0.82f, 0.6f, 1f) : new Color(0.6f, 0.55f, 0.46f, 0.8f);
            _ring.color = ready ? new Color(0.93f, 0.82f, 0.6f, 0.9f) : new Color(1f, 1f, 1f, 0.15f);
            _secs.text = ready ? "" : Mathf.CeilToInt((float)left).ToString();
            _cap.gameObject.SetActive(ready);
            transform.localScale = Vector3.one * (1f + 0.18f * _pop * _pop);
        }

        private static Sprite _cloud, _ringSprite;

        /// <summary>A puff of dust: a few overlapping soft circles.</summary>
        private static Sprite CloudSprite()
        {
            if (_cloud != null) return _cloud;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            var blobs = new[] { new Vector3(22, 26, 13), new Vector3(36, 30, 15), new Vector3(46, 22, 10), new Vector3(30, 40, 11), new Vector3(15, 18, 7) };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float a = 0f;
                foreach (var b in blobs)
                {
                    float d = new Vector2(x + 0.5f - b.x, y + 0.5f - b.y).magnitude;
                    a = Mathf.Max(a, Mathf.Clamp01(b.z - d + 0.5f));
                }
                // small specks flying off to the right
                if ((x - 52) * (x - 52) + (y - 40) * (y - 40) < 6 || (x - 57) * (x - 57) + (y - 31) * (y - 31) < 4) a = 1f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _cloud = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                float a = Mathf.Clamp01(r - d) * Mathf.Clamp01(d - (r - 2.5f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
