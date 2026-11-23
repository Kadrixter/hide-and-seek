using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>Hint line while the local player carries a radio (own canvas, bottom left like the PA line).</summary>
    public partial class MapRadioSystem
    {
        /// <summary>Current hint (tests).</summary>
        public static string HudText { get; private set; } = "";
        private Text _hud;

        private void UpdateHud(MatchManager mm, int n)
        {
            var me = NetPlayer.Local;
            string text = "";
            int i = mm != null && me != null ? mm.MapRadioOf(me) : -1;
            if (i >= 0 && i < n)
            {
                var box = _views[i].Box;
                var ch = mm.MapChannel(i);
                bool playing = ch != null && ch.State.Value.Playing && ch.Current != null;
                text = "<color=#ffd27a>Radio na ramieniu</color> - Q odstaw, R panel (link z YouTube)" +
                       (playing ? "   |   <color=#ff8a7a>gra - słychać cię z daleka</color>" : "");
                if (box != null && box.IsLoading) text = "<color=#ffd27a>Radio</color> - ładowanie utworu...   Q odstaw, R panel";
            }
            HudText = text;
            if (_hud == null)
            {
                if (text.Length == 0) return;
                var canvas = UiKit.CreateCanvas("MapRadioHud", 9, transform);
                _hud = UiKit.Label("MapRadioLine", canvas.transform, "", 21, TextAnchor.LowerLeft, new Color(0.95f, 0.9f, 0.78f));
                _hud.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 430), new Vector2(1100, 30));
                _hud.supportRichText = true;
                _hud.AddOutline();
            }
            if (_hud.text != text) _hud.text = text;
        }
    }
}
