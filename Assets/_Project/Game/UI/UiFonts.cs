using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Menu typeface: Montserrat (SIL OFL 1.1, Game/Resources/Fonts). Text buttons use it directly; whole menus
    /// (main menu, Esc pages, confirm dialog) get it through <see cref="MenuFontScope"/>, which swaps the built-in
    /// font of every Text below it. The in-game HUD keeps the built-in font (its layouts are tuned for it).
    /// </summary>
    public static class UiFonts
    {
        private static Font _medium, _semi, _bold;

        public static Font Medium => _medium != null ? _medium : (_medium = Load("Montserrat-Medium"));
        public static Font SemiBold => _semi != null ? _semi : (_semi = Load("Montserrat-SemiBold"));
        public static Font Bold => _bold != null ? _bold : (_bold = Load("Montserrat-Bold"));

        private static Font Load(string name)
        {
            var f = Resources.Load<Font>("Fonts/" + name);
            if (f == null) Debug.LogWarning($"[UiFonts] missing font {name}, using the built-in one");
            return f != null ? f : UiKit.Font;
        }

        /// <summary>True for fonts this class hands out (already converted).</summary>
        public static bool IsMenuFont(Font f) => f != null && (f == _medium || f == _semi || f == _bold);

        /// <summary>
        /// Swaps the built-in font of every Text under <paramref name="root"/> for Montserrat (bold style -> Bold face,
        /// otherwise Medium, 6 % smaller: Montserrat is wider). Already converted texts are skipped.
        /// </summary>
        public static void Apply(Transform root)
        {
            if (root == null) return;
            var builtin = UiKit.Font;
            foreach (var t in root.GetComponentsInChildren<Text>(true))
            {
                if (t.font != builtin) continue;
                if (t.fontStyle == FontStyle.Bold || t.fontStyle == FontStyle.BoldAndItalic)
                {
                    t.font = Bold;
                    t.fontStyle = t.fontStyle == FontStyle.BoldAndItalic ? FontStyle.Italic : FontStyle.Normal;
                    t.fontSize = Mathf.Max(10, Mathf.RoundToInt(t.fontSize * 0.9f));
                }
                else
                {
                    t.font = Medium;
                    t.fontSize = Mathf.Max(10, Mathf.RoundToInt(t.fontSize * 0.94f));
                }
                // one word never wraps letter by letter ("włączone" on a narrow switch label)
                if (t.text != null && t.text.IndexOf(' ') < 0) t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        /// <summary>"MENU" -> "M E N U" (legacy Text has no letter spacing); words are kept apart by 3 spaces.</summary>
        public static string Spaced(string s)
        {
            var words = s.ToUpperInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++) words[i] = string.Join(" ", words[i].ToCharArray());
            return string.Join("   ", words);
        }
    }

    /// <summary>
    /// Keeps every Text below this object in the menu font (also rows built later, e.g. server lists) and turns
    /// labels named "Title" / "T" into the small spaced menu heading.
    /// </summary>
    public class MenuFontScope : MonoBehaviour
    {
        public bool StyleTitles = true;
        private float _next;

        private void Start() => Run();

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            Run();
        }

        private void Run()
        {
            if (StyleTitles) MenuChrome.StyleHeadings(transform);
            UiFonts.Apply(transform);
        }
    }
}
