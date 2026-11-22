using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Hotbar on the HUD: four rounded slots at the bottom centre with the key number, a pixel icon, the count and,
    /// above the bar, the name of the selected item with what the left mouse button does. Own canvas just under the
    /// main HUD (menus and blind overlays cover it). Also runs <see cref="Hotbar.Tick"/> before the player controller.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class HotbarHud : MonoBehaviour
    {
        private const float SlotSize = 76f, Gap = 10f;
        private static HotbarHud _instance;

        private RectTransform _bar;
        private CanvasGroup _group;
        private readonly Image[] _bg = new Image[Hotbar.Slots], _ring = new Image[Hotbar.Slots], _icon = new Image[Hotbar.Slots];
        private readonly Text[] _key = new Text[Hotbar.Slots], _count = new Text[Hotbar.Slots];
        private readonly RectTransform[] _slot = new RectTransform[Hotbar.Slots];
        private Text _name, _hint;
        private float _shown;

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
                rt.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(SlotSize * 0.5f + i * (SlotSize + Gap), 0f), new Vector2(SlotSize, SlotSize));
                _slot[i] = rt;
                _bg[i] = bg;
                _ring[i] = UiStyle.Ring("Ring", rt, UiStyle.Border);

                var icon = UiKit.Image("Icon", rt, Color.white);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(50f, 50f));
                _icon[i] = icon;

                var key = UiKit.Label("Key", rt, (i + 1).ToString(), 18, TextAnchor.UpperLeft, UiKit.TextDim, FontStyle.Bold);
                key.rectTransform.Stretch(6f);
                key.AddOutline(1.5f, 0.7f);
                _key[i] = key;

                var count = UiKit.Label("Count", rt, "", 18, TextAnchor.LowerRight, UiKit.Text, FontStyle.Bold);
                count.rectTransform.Stretch(6f);
                count.AddOutline(1.5f, 0.8f);
                _count[i] = count;
            }

            _hint = UiKit.Label("Hint", _bar, "", 20, TextAnchor.LowerCenter, UiKit.TextDim);
            _hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, SlotSize + 8f), new Vector2(760f, 26f));
            _hint.AddOutline(1.5f);
            _name = UiKit.Label("Name", _bar, "", 24, TextAnchor.LowerCenter, UiKit.Text, FontStyle.Bold);
            _name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, SlotSize + 34f), new Vector2(760f, 30f));
            _name.AddOutline(1.5f);
            _group.alpha = 0f;
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
            Color accent = me.IsSeeker ? UiKit.SeekerColor : UiKit.Accent;
            for (int i = 0; i < Hotbar.Slots; i++)
            {
                var item = Hotbar.ItemAt(me, i);
                int n = item != HotbarItem.None ? Hotbar.Count(me, item) : 0;
                bool selected = i == sel;
                bool has = n > 0;
                _icon[i].sprite = HotbarIcons.Get(item);
                _icon[i].enabled = item != HotbarItem.None;
                _icon[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0.22f);
                _count[i].text = n > 1 ? "x" + n : "";
                _bg[i].color = selected ? new Color(0.2f, 0.215f, 0.24f, 0.97f) : new Color(0.09f, 0.1f, 0.115f, 0.82f);
                _ring[i].color = selected ? accent : UiStyle.Border;
                _key[i].color = selected ? accent : UiKit.TextDim;
                float scale = selected ? 1.08f : 1f;
                _slot[i].localScale = Vector3.Lerp(_slot[i].localScale, Vector3.one * scale, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
            var cur = Hotbar.ItemAt(me, sel);
            int have = cur != HotbarItem.None ? Hotbar.Count(me, cur) : 0;
            _name.text = cur == HotbarItem.None ? "" : have > 0 ? Hotbar.Name(cur) : Hotbar.Name(cur) + " (brak)";
            _name.color = have > 0 ? UiKit.Text : UiKit.TextDim;
            _hint.text = have > 0 ? Hotbar.Hint(me, cur) : "";
        }
    }
}
