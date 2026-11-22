using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Local HUD for the toy slime blaster: green goo running down the screen edges while the local seeker is covered
    /// in it (with "slowed, no knife" and the seconds left), and the gun with its shots for a hider who carries one
    /// (until the new inventory shows it in a slot).
    /// </summary>
    public class GooOverlay : MonoBehaviour
    {
        private static GooOverlay _instance;
        private Image _goo;
        private Text _status, _ammo;
        private float _alpha;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[GooOverlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GooOverlay>();
        }

        private void Awake()
        {
            var canvas = UiKit.CreateCanvas("GooCanvas", 8, transform);
            _goo = UiKit.Image("Goo", canvas.transform, new Color(1f, 1f, 1f, 0f));
            _goo.raycastTarget = false;
            _goo.sprite = MakeSprite();
            _goo.rectTransform.Stretch();
            _status = UiKit.Label("GooStatus", canvas.transform, "", 26, TextAnchor.MiddleCenter, new Color(0.6f, 1f, 0.4f), FontStyle.Bold);
            _status.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(900f, 40f));
            _status.AddOutline(2f);
            _ammo = UiKit.Label("ToyAmmo", canvas.transform, "", 20, TextAnchor.LowerRight, new Color(0.75f, 1f, 0.55f), FontStyle.Bold);
            _ammo.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 150f), new Vector2(520f, 60f));
            _ammo.AddOutline(1.5f);
        }

        private static Sprite MakeSprite()
        {
            const int W = 256, H = 144;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GooScreen" };
            var rnd = new System.Random(5);
            // Drips hanging from the top edge, thicker blobs in the corners and along the sides.
            var drip = new float[W];
            for (int x = 0; x < W; x++) drip[x] = 0.08f + 0.05f * Mathf.Sin(x * 0.11f) + 0.04f * (float)rnd.NextDouble();
            for (int k = 0; k < 14; k++)
            {
                int cx = rnd.Next(W), w = 3 + rnd.Next(6);
                float len = 0.15f + (float)rnd.NextDouble() * 0.35f;
                for (int x = Mathf.Max(0, cx - w); x < Mathf.Min(W, cx + w); x++)
                {
                    float f = 1f - Mathf.Abs(x - cx) / (float)w;
                    drip[x] = Mathf.Max(drip[x], len * Mathf.Sqrt(f));
                }
            }
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)(W - 1), v = y / (float)(H - 1);
                float top = 1f - v;
                float a = top < drip[x] ? 1f : 0f;
                float side = Mathf.Min(u, 1f - u);
                a = Mathf.Max(a, Mathf.Clamp01((0.07f + 0.03f * Mathf.Sin(v * 23f) - side) * 18f));
                float corner = Mathf.Sqrt(Mathf.Pow(Mathf.Min(u, 1f - u) * 1.8f, 2f) + Mathf.Pow(v, 2f));
                a = Mathf.Max(a, Mathf.Clamp01((0.22f - corner) * 10f));
                float shade = 0.75f + 0.25f * Mathf.Sin(x * 0.3f + y * 0.21f);
                px[y * W + x] = new Color(0.35f * shade, 0.9f * shade, 0.18f * shade, a * 0.82f);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
        }

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            float left = me != null && me.Life.Value == Core.LifeState.Alive ? me.GooLeft : 0f;
            float want = left > 0f ? Mathf.Clamp01(left / 0.8f) : 0f;
            _alpha = Mathf.MoveTowards(_alpha, want, Time.unscaledDeltaTime * 4f);
            _goo.color = new Color(1f, 1f, 1f, _alpha);
            _goo.enabled = _alpha > 0.001f;
            _status.text = left > 0f ? $"OBLEPIONY GLUTEM  {left:0.0} s   (spowolniony, nóż i piła nie działają)" : "";

            bool gun = me != null && me.HasToyGun && Hotbar.Active(me);
            _ammo.text = gun ? $"Pistolecik z glutem: {me.ToyAmmo.Value}/{ToyTuning.Shots}\n" +
                               (me.ToyGunOut.Value ? "LPM - strzał, 1-4 - schowaj" : "5 - weź do ręki (gra na cały parking!)") : "";
        }
    }
}
