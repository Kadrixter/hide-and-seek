using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Local screen effects of the extinguisher. Foamed seeker: the view plastered with thick white foam (lumpy
    /// shaded patches, drips sliding down, a milky haze), wiped off patch by patch in step with the hands - each
    /// stroke of the right hand clears the right half, the left one the left half, inner patches first - and the rest
    /// melting away at the end. Standing in a powder cloud: a grey haze (lit by your own flashlight). Holding an
    /// extinguisher: a short key hint and the pressure.
    /// </summary>
    public class FoamOverlay : MonoBehaviour
    {
        private const int Blobs = 16, Drips = 9;
        private static FoamOverlay _instance;
        private Image _haze, _cloud;
        private readonly Image[] _blobs = new Image[Blobs];
        private readonly Image[] _drips = new Image[Drips];
        private readonly Vector2[] _blobPos = new Vector2[Blobs];
        private readonly float[] _blobSize = new float[Blobs], _blobGone = new float[Blobs], _dripX = new float[Drips], _dripSpeed = new float[Drips];
        private Text _hint;
        private double _seededFor = -1;
        private float _cloudA;
        private Sprite[] _foam;
        private Sprite _drip;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[FoamOverlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FoamOverlay>();
        }

        private void Awake()
        {
            _foam = new[] { FoamSprite(3), FoamSprite(7), FoamSprite(11) };
            _drip = DripSprite();
            var canvas = UiKit.CreateCanvas("FoamCanvas", 9, transform);
            _cloud = UiKit.Image("Cloud", canvas.transform, new Color(0.5f, 0.5f, 0.5f, 0f));
            _cloud.rectTransform.Stretch();
            _cloud.raycastTarget = false;
            _haze = UiKit.Image("Haze", canvas.transform, new Color(0.9f, 0.9f, 0.88f, 0f));
            _haze.rectTransform.Stretch();
            _haze.raycastTarget = false;
            for (int i = 0; i < Drips; i++)
            {
                _drips[i] = UiKit.Image("Drip", canvas.transform, Color.white);
                _drips[i].sprite = _drip;
                _drips[i].raycastTarget = false;
                _drips[i].enabled = false;
            }
            for (int i = 0; i < Blobs; i++)
            {
                _blobs[i] = UiKit.Image("Foam", canvas.transform, Color.white);
                _blobs[i].sprite = _foam[i % _foam.Length];
                _blobs[i].raycastTarget = false;
                _blobs[i].enabled = false;
            }
            _hint = UiKit.Label("ExtHint", canvas.transform, "", 20, TextAnchor.LowerCenter, new Color(0.95f, 0.95f, 0.92f), FontStyle.Bold);
            _hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1000f, 60f));
            _hint.AddOutline(1.5f);
        }

        /// <summary>New hit: a fresh layout of patches (same for the whole effect), each with the stroke that wipes it.</summary>
        private void Seed(double start)
        {
            _seededFor = start;
            var r = new System.Random((int)(start * 1000.0) & 0x7fffffff);
            for (int i = 0; i < Blobs; i++)
            {
                // Denser in the middle (the eyes got it), a few towards the edges.
                float x = 0.5f + (float)(r.NextDouble() - 0.5) * (i < 8 ? 0.55f : 1.0f);
                float y = 0.5f + (float)(r.NextDouble() - 0.5) * (i < 8 ? 0.5f : 0.95f);
                _blobPos[i] = new Vector2(x, y);
                _blobSize[i] = (i < 8 ? 0.42f : 0.28f) + (float)r.NextDouble() * 0.2f;
                // Right half: even strokes (right hand), left half: odd ones; the eyes (inner patches) are cleared first,
                // the edges last, so the view opens up gradually over the whole effect.
                bool right = x >= 0.5f;
                float off = Mathf.Abs(x - 0.5f);
                int rank = off < 0.15f ? r.Next(0, 2) : off < 0.3f ? 1 + r.Next(0, 2) : 2 + r.Next(0, 2);
                int stroke = (right ? 0 : 1) + 2 * rank;
                _blobGone[i] = ExtTimeline.WipeIn + stroke * ExtTimeline.WipeStroke + ExtTimeline.WipeStroke * 0.45f;
            }
            for (int i = 0; i < Drips; i++)
            {
                _dripX[i] = 0.08f + 0.84f * (float)r.NextDouble();
                _dripSpeed[i] = 0.03f + 0.05f * (float)r.NextDouble();
            }
        }

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            bool foamed = me != null && me.IsSeeker && me.Foamed;
            float e = foamed ? me.FoamElapsed : 0f, left = foamed ? me.FoamLeft : 0f;
            if (foamed && me.FoamStart.Value != _seededFor) Seed(me.FoamStart.Value);
            var size = ((RectTransform)_haze.canvas.transform).rect.size;
            float appear = Mathf.Clamp01(e / 0.1f);
            float end = Mathf.Clamp01(left / 0.7f);

            _haze.color = new Color(0.9f, 0.9f, 0.88f, foamed ? appear * Mathf.Lerp(0.2f, 0.85f, Mathf.Clamp01(1f - (e - 0.4f) / 3.2f)) * end : 0f);
            for (int i = 0; i < Blobs; i++)
            {
                var b = _blobs[i];
                float wiped = foamed ? ExtTimeline.S(e, _blobGone[i] - 0.12f, _blobGone[i] + 0.25f) : 1f;
                float a = foamed ? appear * end * (1f - 0.72f * wiped) : 0f;
                b.enabled = a > 0.01f;
                if (!b.enabled) continue;
                Vector2 p = _blobPos[i];
                float side = p.x >= 0.5f ? 1f : -1f;
                // Smeared outwards and down by the hand, then gone.
                p += new Vector2(side * 0.25f * wiped, -0.08f * wiped - 0.01f * e);
                float s = _blobSize[i] * size.y * (1f + 0.08f * Mathf.Sin(Time.time * 1.3f + i)) * (1f + 0.4f * wiped);
                b.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(p.x * size.x, p.y * size.y), new Vector2(s * (1f + 0.6f * wiped), s * (1f - 0.3f * wiped)));
                b.color = new Color(1f, 1f, 1f, a);
            }
            for (int i = 0; i < Drips; i++)
            {
                // Each drip runs down from under one of the patches and goes with it when that patch is wiped.
                var d = _drips[i];
                int bi = i % Blobs;
                float wiped = foamed ? ExtTimeline.S(e, _blobGone[bi] - 0.12f, _blobGone[bi] + 0.1f) : 1f;
                float a = foamed ? appear * end * (1f - wiped) : 0f;
                d.enabled = a > 0.01f;
                if (!d.enabled) continue;
                Vector2 bp = _blobPos[bi];
                float top = bp.y - _blobSize[bi] * 0.28f;
                float len = Mathf.Min(0.35f, 0.03f + _dripSpeed[i] * Mathf.Max(0f, e - 0.2f) * 1.6f);
                float x = bp.x + (_dripX[i] - 0.5f) * _blobSize[bi] * 0.5f * size.y / size.x;
                d.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(x * size.x, top * size.y), new Vector2(size.y * 0.024f, len * size.y));
                d.color = new Color(0.97f, 0.97f, 0.95f, a * 0.85f);
            }

            // Powder cloud around the camera (anyone): grey haze, brighter in your own flashlight.
            var cam = Camera.main;
            float dens = cam != null && me != null && me.Life.Value != LifeState.Dead ? ExtinguisherSpray.Density(cam.transform.position) : 0f;
            _cloudA = Mathf.MoveTowards(_cloudA, Mathf.Clamp01(dens * 1.1f) * 0.6f, Time.deltaTime * 1.5f);
            float lit = me != null && me.Flashlight != null && me.Flashlight.IsOn ? 0.62f : 0.3f;
            _cloud.color = new Color(lit, lit, lit * 0.98f, _cloudA);

            _hint.text = HintText(me, mm);
        }

        private static string HintText(NetPlayer me, MatchManager mm)
        {
            if (me == null || mm == null) return "";
            int idx = mm.ExtHeldBy(me);
            if (idx < 0 || idx >= mm.Extinguishers.Count) return "";
            var st = mm.Extinguishers[idx];
            if (!st.Unlocked) return "LPM - zerwij plombę i wyciągnij zawleczkę   |   Q - odłóż gaśnicę";
            if (st.Charge == 0) return "Gaśnica pusta   |   Q - odłóż";
            return $"LPM (przytrzymaj) - strzelaj pianą   |   Q - odłóż   |   ciśnienie {Mathf.RoundToInt(st.Charge01 * 100f)}%";
        }

        // ---------------------------------------------------------------- sprites

        /// <summary>
        /// Patch of foam: metaball lumps (smooth union, so no seams) lit from the top-left with a soft sheen, fine
        /// bubbly grain, thinner and a bit see-through towards the edge.
        /// </summary>
        private static Sprite FoamSprite(int seed)
        {
            const int S = 128;
            var r = new System.Random(seed);
            var lumps = new Vector3[11];
            for (int i = 0; i < lumps.Length; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0), d = i == 0 ? 0f : 0.1f + (float)r.NextDouble() * 0.2f;
                lumps[i] = new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, i == 0 ? 0.2f : 0.08f + (float)r.NextDouble() * 0.08f);
            }
            float F(float u, float v)
            {
                float f = 0f;
                foreach (var l in lumps)
                {
                    float dx = (u - l.x) / l.z, dy = (v - l.y) / l.z;
                    f += Mathf.Exp(-(dx * dx + dy * dy) * 1.6f);
                }
                return f;
            }
            var px = new Color32[S * S];
            Vector3 light = new Vector3(-0.5f, 0.6f, 0.62f).normalized;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S, v = (y + 0.5f) / S, e = 1f / S;
                float f = F(u, v);
                float a = Mathf.Clamp01((f - 0.42f) / 0.22f);
                if (a <= 0f) { px[y * S + x] = new Color32(255, 255, 255, 0); continue; }
                float gx = (F(u + e, v) - F(u - e, v)) / (2f * e), gy = (F(u, v + e) - F(u, v - e)) / (2f * e);
                var n = new Vector3(-gx * 0.012f, -gy * 0.012f, 1f).normalized;
                float diff = Mathf.Clamp01(Vector3.Dot(n, light));
                float spec = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(Vector3.Reflect(-light, n), Vector3.forward)), 18f) * 0.25f;
                float grain = (Mathf.PerlinNoise(x * 0.45f + seed * 3.1f, y * 0.45f) - 0.5f) * 0.06f;
                float c = Mathf.Clamp01(0.72f + 0.26f * diff + spec + grain);
                px[y * S + x] = new Color(c, c, c * 0.985f, a * Mathf.Lerp(0.75f, 0.97f, Mathf.Clamp01(f - 0.6f)));
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "FoamPatch" + seed };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Drip: a thin streak hanging from the top, a round drop at its end.</summary>
        private static Sprite DripSprite()
        {
            const int W = 16, H = 128;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W - 0.5f, v = (y + 0.5f) / H;
                float w = Mathf.Lerp(0.22f, 0.32f, 1f - v);
                float a = Mathf.Clamp01((w - Mathf.Abs(u)) * 30f);
                float drop = Mathf.Clamp01((0.45f - new Vector2(u, (v - 0.06f) * 4f).magnitude) * 20f);
                a = Mathf.Max(a * Mathf.Clamp01(v * 8f), drop);
                float c = 0.85f + 0.15f * Mathf.Clamp01(0.3f - u * 2f);
                px[y * W + x] = new Color(c, c, c, a);
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FoamDrip" };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 1f));
        }
    }
}
