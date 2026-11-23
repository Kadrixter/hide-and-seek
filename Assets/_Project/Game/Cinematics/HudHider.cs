using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Hides the in-game HUD during the intro / outro without touching the HUD code: every top-level child of the
    /// HUD canvas gets a disabled nested Canvas (which hides its subtree) and the separate HUD canvases (hotbar,
    /// phone, public address, goo) are switched off; all of it is undone on <see cref="Pop"/>. The K wheel and the
    /// consent prompt stay usable when <see cref="KeepEmotes"/> is set (outro), menus opened later are never hidden.
    /// </summary>
    public static class HudHider
    {
        static readonly string[] OtherCanvases = { "HotbarCanvas", "PhoneHudCanvas", "PaHudCanvas", "GooCanvas", "SpectatorVoiceCanvas" };
        static readonly string[] EmoteParts = { "EmoteWheel", "ConsentPrompt", "PoseWheel" };

        static int _depth;
        static readonly List<Canvas> _added = new();
        static readonly List<Canvas> _offCanvases = new();
        static readonly HashSet<Transform> _seen = new();

        /// <summary>Keep the emote wheel and the consent prompt visible (outro).</summary>
        public static bool KeepEmotes;

        public static bool Active => _depth > 0;

        public static void Push()
        {
            _depth++;
            Apply();
        }

        public static void Pop()
        {
            if (_depth == 0) return;
            if (--_depth > 0) return;
            // Switched back on, not destroyed: a destroyed nested canvas leaves its graphics unregistered (invisible).
            foreach (var c in _added) if (c != null) c.enabled = true;
            foreach (var c in _offCanvases) if (c != null) c.enabled = true;
            _added.Clear();
            _offCanvases.Clear();
            _seen.Clear();
            KeepEmotes = false;
        }

        /// <summary>Called every frame while active (catches canvases that switch themselves back on).</summary>
        public static void Apply()
        {
            if (_depth == 0) return;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (c == null || !c.isRootCanvas) continue;
                string n = c.gameObject.name;
                if (n == "HudCanvas") HideChildren(c.transform);
                else if (System.Array.IndexOf(OtherCanvases, n) >= 0 && c.enabled)
                {
                    c.enabled = false;
                    if (!_offCanvases.Contains(c)) _offCanvases.Add(c);
                }
            }
        }

        static void HideChildren(Transform root)
        {
            foreach (Transform ch in root)
            {
                if (!_seen.Add(ch)) continue;
                bool emote = System.Array.IndexOf(EmoteParts, ch.name) >= 0;
                if ((emote && KeepEmotes) || ch.name.EndsWith("Menu")) continue;
                var own = ch.GetComponent<Canvas>();
                if (own != null)
                {
                    // Already a nested canvas (wheels, bars): switch it off and remember.
                    if (own.enabled) { own.enabled = false; _offCanvases.Add(own); }
                    continue;
                }
                var c = ch.gameObject.AddComponent<Canvas>();
                c.enabled = false;
                _added.Add(c);
            }
        }
    }
}
