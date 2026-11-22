using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Hotbar on the HUD: five rounded slots at the bottom centre (keys 1-5). Empty slots show only the key number;
    /// a filled slot shows the item's pixel icon and the count. The selected slot gets a light frame around it
    /// (Minecraft style) that glides between slots; above the bar the item's name and what the left mouse button / Q do.
    /// Own canvas just under the main HUD (menus and blind overlays cover it). Also runs <see cref="Hotbar.Tick"/>.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class HotbarHud : MonoBehaviour
    {
        private const float SlotSize = 70f, Gap = 8f, FrameOut = 5f;
        private static HotbarHud _instance;
        private static Sprite _frameSprite;

        private RectTransform _bar, _frame;
        private Image _frameImg;
        private CanvasGroup _group;
        private readonly Image[] _bg = new Image[Hotbar.Slots], _ring = new Image[Hotbar.Slots], _icon = new Image[Hotbar.Slots];
        private readonly Text[] _key = new Text[Hotbar.Slots], _count = new Text[Hotbar.Slots];
        private readonly RectTransform[] _slot = new RectTransform[Hotbar.Slots];
        private Text _name, _hint;
        private float _shown;
        private int _lastSel = -1;
        private float _pulse;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[Hotbar]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HotbarHud>();
        }

        private void Awake()
        {
            var canvas = UiKit.CreateCanvas("HotbarCanvas", 9, transform);
            Build(canvas.transform);
        }

        private static float SlotX(int i) => SlotSize * 0.5f + i * (SlotSize + Gap);

        private void Build(Transform root)
        {
            float width = Hotbar.Slots * SlotSize + (Hotbar.Slots - 1) * Gap;
            _bar = UiKit.Rect("Hotbar", root);
            _bar.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(width, SlotSize + 70f));
            _group = _bar.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            for (int i = 0; i < Hotbar.Slots; i++)
            {
                var bg = UiKit.Image("Slot" + (i + 1), _bar, UiStyle.Row);
                bg.sprite = UiStyle.RoundGradient;
                bg.type = Image.Type.Sliced;
                bg.raycastTarget = false;
                var rt = bg.rectTransform;
                rt.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(SlotX(i), SlotSize * 0.5f), new Vector2(SlotSize, SlotSize));
                _slot[i] = rt;
                _bg[i] = bg;
                _ring[i] = UiStyle.Ring("Ring", rt, UiStyle.Border);

                var icon = UiKit.Image("Icon", rt, Color.white);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(46f, 46f));
                _icon[i] = icon;

                var key = UiKit.Label("Key", rt, (i + 1).ToString(), 17, TextAnchor.UpperLeft, UiKit.TextDim, FontStyle.Bold);
                key.rectTransform.Stretch(6f);
                key.AddOutline(1.5f, 0.7f);
                _key[i] = key;

                int idx = i; // the ringing phone's "E" key cap over its slot
                PhoneRingBadge.Attach(rt, () => NetPlayer.Local != null && Hotbar.ItemAt(NetPlayer.Local, idx) == HotbarItem.Phone &&
                                                PhoneControls.SmashOffered(NetPlayer.Local));

                var count = UiKit.Label("Count", rt, "", 18, TextAnchor.LowerRight, UiKit.Text, FontStyle.Bold);
                count.rectTransform.Stretch(6f);
                count.AddOutline(1.5f, 0.8f);
                _count[i] = count;
            }

            // Selection frame: a light, slightly thicker rounded border just outside the selected slot.
            _frameImg = UiKit.Image("SelectFrame", _bar, new Color(1f, 1f, 1f, 0.9f));
            _frameImg.sprite = FrameSprite();
            _frameImg.type = Image.Type.Sliced;
            _frameImg.raycastTarget = false;
            _frame = _frameImg.rectTransform;
            _frame.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(SlotX(0), SlotSize * 0.5f),
                new Vector2(SlotSize + FrameOut * 2f, SlotSize + FrameOut * 2f));

            _hint = UiKit.Label("Hint", _bar, "", 19, TextAnchor.LowerCenter, UiKit.TextDim);
            _hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, SlotSize + 10f), new Vector2(820f, 26f));
            _hint.AddOutline(1.5f);
            _name = UiKit.Label("Name", _bar, "", 23, TextAnchor.LowerCenter, UiKit.Text, FontStyle.Bold);
            _name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, SlotSize + 35f), new Vector2(820f, 30f));
            _name.AddOutline(1.5f);
            _group.alpha = 0f;
        }

        /// <summary>Rounded ring ~3.5 px thick (64 px sprite, 9-sliced) for the selection frame.</summary>
        private static Sprite FrameSprite()
        {
            if (_frameSprite != null) return _frameSprite;
            const int size = 64;
            const float radius = 17f, ring = 3.6f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude;
                float a = Mathf.Clamp01(radius - d + 0.5f);
                float edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                float inside = (cx != x + 0.5f || cy != y + 0.5f) ? radius - d : edge;
                a *= Mathf.Clamp01(ring - inside + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = radius + 1f;
            return _frameSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private void Update() => Hotbar.Tick(NetPlayer.Local);

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            bool active = Hotbar.Active(me);
            _shown = Mathf.MoveTowards(_shown, active ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            _group.alpha = _shown;
            if (_shown <= 0f || me == null) return;

            int sel = Hotbar.Selected(me);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < Hotbar.Slots; i++)
            {
                var item = Hotbar.ItemAt(me, i);
                int n = Hotbar.Count(me, item);
                bool selected = i == sel;
                bool has = item != HotbarItem.None;
                _icon[i].sprite = has ? HotbarIcons.Get(item) : null;
                _icon[i].enabled = has;
                var def = has ? ItemDefs.Get(item) : null;
                _count[i].text = def?.Badge != null ? def.Badge(me) : n > 1 ? "x" + n : "";
                _bg[i].color = selected ? new Color(0.2f, 0.215f, 0.24f, 0.95f) : has ? new Color(0.11f, 0.12f, 0.135f, 0.85f) : new Color(0.07f, 0.075f, 0.085f, 0.62f);
                _ring[i].color = has ? new Color(1f, 1f, 1f, 0.16f) : UiStyle.Border;
                _key[i].color = selected ? Color.white : has ? UiKit.TextDim : new Color(0.6f, 0.62f, 0.66f, 0.55f);
            }

            // The frame glides to the selected slot and pulses gently once when it gets there.
            if (sel != _lastSel) { _lastSel = sel; _pulse = 1f; }
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 3f);
            var target = new Vector2(SlotX(sel), SlotSize * 0.5f);
            _frame.anchoredPosition = Vector2.Lerp(_frame.anchoredPosition, target, 1f - Mathf.Exp(-22f * dt));
            _frameImg.color = new Color(1f, 1f, 1f, 0.82f + 0.15f * _pulse);
            _frame.localScale = Vector3.one * (1f + 0.04f * _pulse);

            var cur = Hotbar.ItemAt(me, sel);
            _name.text = cur == HotbarItem.None ? "" : Hotbar.Name(cur);
            _hint.text = cur == HotbarItem.None ? "" : Hotbar.Hint(me, cur);
        }
    }
}
