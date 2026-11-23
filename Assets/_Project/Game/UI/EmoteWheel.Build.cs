using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>Look of the <see cref="EmoteWheel"/>: ring, tabs, slices with pictures, the live preview in the middle.</summary>
    public sealed partial class EmoteWheel
    {
        private const float OuterR = 330f, InnerR = 172f, SliceIn = InnerR + 8f, Gap = 8f;
        private const int ThumbsPerFrame = 2;

        private sealed class Slice
        {
            public EmoteId Id;
            public RectTransform Rt;
            public RingSector Fill, Glow, Edge, ThumbRim;
            public RawImage Thumb;
            public Image Grey;
            public float Scale = 1f, Hot;
            public bool Available;
        }

        private RectTransform _root, _ring;
        private CanvasGroup _group;
        private Image _dim;
        private RawImage _preview;
        private RingSector _pointer;
        private Text _hint, _sub;
        private readonly List<Slice> _slices = new();
        private readonly Image[] _tabBg = new Image[2];
        private readonly Text[] _tabText = new Text[2];
        private EmotePreviewStage _stage, _thumbStage;
        private readonly Dictionary<EmoteId, RenderTexture> _pics = new();
        private readonly Queue<EmoteId> _todo = new();
        private int _thumbPx = 128;
        private float _thumbD = 124f;

        public static EmoteWheel Create(Transform canvasRoot)
        {
            var rt = UiKit.Rect("EmoteWheel", canvasRoot);
            rt.Stretch();
            var c = rt.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 30;
            var w = rt.gameObject.AddComponent<EmoteWheel>();
            w._root = rt;
            w._group = rt.gameObject.AddComponent<CanvasGroup>();
            w._group.alpha = 0f;
            w._group.blocksRaycasts = false;
            w._group.interactable = false;
            w.Build();
            rt.gameObject.SetActive(false);
            _inst = w;
            return w;
        }

        private void Build()
        {
            _dim = UiKit.Image("Dim", _root, new Color(0f, 0f, 0f, 0.28f));
            _dim.rectTransform.Stretch();
            _dim.raycastTarget = false;
            _ring = UiKit.Rect("Ring", _root);
            _ring.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (OuterR * 2f + 60f));
            RingSector.Create("Back", _ring, InnerR - 14f, OuterR + 14f, 0f, 360f, 0f, new Color(0.035f, 0.04f, 0.045f, 0.62f), 2f);
            RingSector.Create("RimOut", _ring, OuterR + 13f, OuterR + 14.5f, 0f, 360f, 0f, new Color(1f, 1f, 1f, 0.1f));
            RingSector.Create("RimIn", _ring, InnerR - 14.5f, InnerR - 13f, 0f, 360f, 0f, new Color(1f, 1f, 1f, 0.1f));

            float pr = InnerR - 20f;
            var disc = UiKit.Image("PreviewDisc", _ring, new Color(0.075f, 0.082f, 0.095f, 1f));
            disc.sprite = UiStyle.Circle;
            disc.raycastTarget = false;
            disc.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * pr * 2f);
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _preview = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            _preview.transform.SetParent(disc.transform, false);
            _preview.rectTransform.Stretch();
            _preview.raycastTarget = false;
            RingSector.Create("PreviewRim", _ring, pr - 1f, pr + 1.5f, 0f, 360f, 0f, new Color(1f, 1f, 1f, 0.16f));
            _pointer = RingSector.Create("Pointer", _ring, InnerR - 11f, InnerR - 4f, 0f, 30f, 0f, UiKit.Accent, 1.5f);

            // Tabs above the ring.
            string[] names = { "SOLO", "WSPÓLNE" };
            for (int i = 0; i < 2; i++)
            {
                var bg = UiKit.Image("Tab" + i, _root, Color.white).Rounded(true);
                bg.raycastTarget = false;
                bg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f),
                    new Vector2(i == 0 ? -78f : 78f, OuterR + 30f), new Vector2(140f, 38f));
                _tabBg[i] = bg;
                var t = UiKit.Label("T", bg.transform, names[i], 19, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
                t.rectTransform.Stretch();
                _tabText[i] = t;
            }

            _sub = UiKit.Label("Sub", _root, "", 21, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            _sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -OuterR - 26f), new Vector2(1000f, 30f));
            _sub.AddOutline(1f, 0.6f);
            _hint = UiKit.Label("Hint", _root, "", 19, TextAnchor.MiddleCenter, UiKit.TextDim);
            _hint.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -OuterR - 56f), new Vector2(1100f, 30f));
            _hint.AddOutline(1f, 0.6f);
        }

        private void BuildPage(int page)
        {
            foreach (var s in _slices) if (s.Rt != null) Destroy(s.Rt.gameObject);
            _slices.Clear();
            _builtPage = page;
            var ids = page == 0 ? Emotes.Solo : Emotes.Pair;
            int n = ids.Length;
            float step = 360f / n;
            float mid = (SliceIn + OuterR) * 0.5f;
            _thumbD = Mathf.Min(132f, 2f * Mathf.PI * mid / n - 22f);
            var dark = new Color(0.075f, 0.082f, 0.095f, 1f);
            for (int i = 0; i < n; i++)
            {
                float a = i * step;
                var s = new Slice { Id = ids[i] };
                s.Rt = UiKit.Rect("Slice" + i, _ring);
                s.Rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (OuterR * 2f + 60f));
                s.Glow = RingSector.Create("Glow", s.Rt, SliceIn - 4f, OuterR + 6f, a, step, Gap, new Color(0f, 0f, 0f, 0f), 16f);
                s.Fill = RingSector.Create("Fill", s.Rt, SliceIn, OuterR, a, step, Gap, Color.white);
                s.Edge = RingSector.Create("Edge", s.Rt, OuterR - 5f, OuterR, a, step, Gap, UiKit.Accent);
                Vector2 c = RingSector.Dir(a) * mid;
                var holder = UiKit.Image("Thumb", s.Rt, dark);
                holder.sprite = UiStyle.Circle;
                holder.raycastTarget = false;
                holder.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), c, Vector2.one * _thumbD);
                holder.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                s.Thumb = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
                s.Thumb.transform.SetParent(holder.transform, false);
                s.Thumb.rectTransform.Stretch();
                s.Thumb.raycastTarget = false;
                s.Thumb.enabled = _pics.TryGetValue(s.Id, out var pic) && pic != null;
                if (s.Thumb.enabled) s.Thumb.texture = pic;
                else _todo.Enqueue(s.Id);
                s.Grey = UiKit.Image("Grey", holder.transform, new Color(0.17f, 0.18f, 0.2f, 0f));
                s.Grey.rectTransform.Stretch();
                s.Grey.raycastTarget = false;
                s.ThumbRim = RingSector.Create("Rim", s.Rt, _thumbD * 0.5f - 1.5f, _thumbD * 0.5f + 1f, 0f, 360f, 0f, new Color(1f, 1f, 1f, 0.14f));
                s.ThumbRim.rectTransform.anchoredPosition = c;
                _slices.Add(s);
            }
            if (_stage != null) { _shown = EmoteId.None; _stage.Show(EmoteId.None); }
        }

        // ------------------------------------------------------------------ stages and pictures

        private int PartnerSkin(NetPlayer me) => _target != null ? _target.SkinVar.Value : (me.SkinVar.Value + 2) % 5;

        private void CreateStages(NetPlayer me)
        {
            DestroyStages();
            var canvas = GetComponentInParent<Canvas>();
            float k = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            int size = Mathf.Clamp(Mathf.RoundToInt((InnerR - 20f) * 2f * k / 32f) * 32, 256, 1024);
            _thumbPx = Mathf.Clamp(Mathf.RoundToInt(132f * k / 16f) * 16, 96, 256);
            var partnerChar = _target != null ? _target.Character : null;
            _stage = EmotePreviewStage.Create(me.Character, PartnerSkin(me), partnerChar, size, false);
            _preview.texture = _stage.Texture;
            _shown = EmoteId.None;
            ReleasePictures();
        }

        /// <summary>The looked-at player changed: pair pictures and the preview partner get the new look.</summary>
        private void RebuildPartner()
        {
            var me = NetPlayer.Local;
            if (me == null || !_open) return;
            CreateStages(me);
            BuildPage(_page);
        }

        private void StepThumbs()
        {
            if (_todo.Count == 0)
            {
                if (_thumbStage != null) { Destroy(_thumbStage.gameObject); _thumbStage = null; }
                return;
            }
            var me = NetPlayer.Local;
            if (me == null) return;
            if (_thumbStage == null)
                _thumbStage = EmotePreviewStage.Create(me.Character, PartnerSkin(me), _target != null ? _target.Character : null, 0, true);
            for (int k = 0; k < ThumbsPerFrame && _todo.Count > 0; k++)
            {
                var id = _todo.Dequeue();
                if (_pics.ContainsKey(id)) continue;
                var rt = new RenderTexture(_thumbPx, _thumbPx, 24, RenderTextureFormat.ARGB32) { name = "EmoteThumb", antiAliasing = 4 };
                rt.Create();
                _thumbStage.RenderEmote(id, rt);
                _pics[id] = rt;
                foreach (var s in _slices)
                    if (s.Id == id) { s.Thumb.texture = rt; s.Thumb.enabled = true; }
            }
        }

        private void ReleasePictures()
        {
            _todo.Clear();
            foreach (var s in _slices) if (s.Thumb != null) { s.Thumb.texture = null; s.Thumb.enabled = false; }
            foreach (var rt in _pics.Values)
                if (rt != null) { rt.Release(); Destroy(rt); }
            _pics.Clear();
        }

        private void DestroyStages()
        {
            ReleasePictures();
            if (_thumbStage != null) Destroy(_thumbStage.gameObject);
            _thumbStage = null;
            if (_stage != null) Destroy(_stage.gameObject);
            _stage = null;
            if (_preview != null) _preview.texture = null;
        }

        // ------------------------------------------------------------------ per frame look

        private void ApplyOpenAnim(float anim)
        {
            float e = _open ? EaseOutBack(anim) : anim * anim;
            float pg = 1f - (1f - _pageAnim) * (1f - _pageAnim);
            _group.alpha = Mathf.Clamp01(anim * 1.4f);
            _ring.localScale = Vector3.one * Mathf.LerpUnclamped(0.84f, 1f, e) * Mathf.Lerp(0.94f, 1f, pg);
            _ring.localRotation = Quaternion.Euler(0f, 0f, (1f - pg) * (_page == 1 ? 14f : -14f));
            _dim.color = new Color(0f, 0f, 0f, 0.28f * anim);
            for (int i = 0; i < 2; i++)
            {
                bool on = i == _page;
                _tabBg[i].color = on ? new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.92f) : new Color(0.06f, 0.065f, 0.075f, 0.8f);
                _tabText[i].color = on ? new Color(0.05f, 0.08f, 0.06f) : UiKit.TextDim;
                _tabBg[i].rectTransform.localScale = Vector3.one * (on ? 1.04f : 0.96f);
            }
        }

        private void UpdatePointer()
        {
            float m = _cursor.magnitude;
            _pointer.CentreAngle = Mathf.Atan2(_cursor.x, _cursor.y) * Mathf.Rad2Deg;
            var pc = UiKit.Accent;
            pc.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, DeadZone + 0.05f, m));
            _pointer.color = pc;
        }

        private void UpdateTexts(NetPlayer me)
        {
            if (_page == 1)
            {
                _sub.text = _target != null ? $"z: {_target.Nick}" : "Spójrz na gracza obok (do 2.5 m)";
                _sub.color = _target != null ? UiKit.Accent : new Color(1f, 0.72f, 0.35f);
            }
            else _sub.text = "";
            _hint.text = _toggle
                ? "mysz - wskaż   |   kółko - Solo / Wspólne   |   LPM / K - wybierz   |   PPM / Esc - anuluj"
                : "mysz - wskaż   |   kółko - Solo / Wspólne   |   puść K / LPM - wybierz   |   PPM / Esc - anuluj";
        }

        private void AnimateSlices(NetPlayer me)
        {
            float dt = Time.unscaledDeltaTime;
            var acc = UiKit.Accent;
            for (int i = 0; i < _slices.Count; i++)
            {
                var s = _slices[i];
                s.Available = me != null && Available(i, me);
                bool hot = i == _hl && _open;
                s.Hot = Mathf.MoveTowards(s.Hot, hot ? 1f : 0f, dt / 0.1f);
                float h = Mathf.SmoothStep(0f, 1f, s.Hot);
                s.Scale = Mathf.Lerp(s.Scale, hot ? (s.Available ? 1.065f : 1.025f) : 1f, 1f - Mathf.Exp(-22f * dt));
                s.Rt.localScale = Vector3.one * s.Scale;
                s.Fill.color = s.Available
                    ? Color.Lerp(new Color(0.105f, 0.115f, 0.135f, 0.8f), new Color(0.2f, 0.225f, 0.255f, 0.94f), h)
                    : Color.Lerp(new Color(0.07f, 0.072f, 0.08f, 0.55f), new Color(0.11f, 0.11f, 0.12f, 0.7f), h);
                s.Glow.color = new Color(acc.r, acc.g, acc.b, s.Available ? 0.2f * h : 0f);
                s.Edge.color = new Color(acc.r, acc.g, acc.b, s.Available ? h : 0f);
                float lum = s.Available ? Mathf.Lerp(0.84f, 1f, h) : 0.5f;
                s.Thumb.color = new Color(lum, lum, lum, 1f);
                s.Grey.color = new Color(0.17f, 0.18f, 0.2f, s.Available ? 0f : 0.55f);
                s.ThumbRim.color = s.Available ? new Color(1f, 1f, 1f, Mathf.Lerp(0.14f, 0.4f, h)) : new Color(1f, 1f, 1f, 0.06f);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
