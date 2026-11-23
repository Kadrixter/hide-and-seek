using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Player list in the top right corner of the HUD (lobby and round): "GRACZE (n)" + summary, then one row per player
    /// (<see cref="PlayerListRow"/>). Lobby: host / ready (has a flashlight from the box) / waiting. Round: SZUKA or the
    /// hider's state (chowa się / zraniony / ranny / powalony / martwy). The speaking player is highlighted.
    /// A downed hider shows the bleed-out left (bar + time) to everybody who sees "powalony" - the same rows for every
    /// role and spectators - while the seeking phase runs (the server only lets players bleed out then).
    /// Narrow (360 px of 1920) and grows downwards only as far as needed, so it stays clear of the banner, the lever
    /// countdown and the toasts in the middle of the screen.
    /// </summary>
    public sealed class PlayerListHud : MonoBehaviour
    {
        public const float Width = 360f;
        private const float Pad = 10f, HeaderH = 30f, MaxRows = 12;

        private RectTransform _panel;
        private Text _title, _summary;
        private readonly List<PlayerListRow> _rows = new();
        private readonly List<NetPlayer> _sorted = new();
        private Text _more;

        public static PlayerListHud Create(Transform canvasRoot)
        {
            var bg = UiKit.Image("PlayerList", canvasRoot, new Color(0.035f, 0.04f, 0.045f, 0.58f)).Rounded();
            bg.raycastTarget = false;
            var rt = bg.rectTransform;
            rt.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(Width, 80));
            UiStyle.Ring("Border", rt, UiStyle.Border);
            var hud = bg.gameObject.AddComponent<PlayerListHud>();
            hud._panel = rt;

            hud._title = UiKit.Label("Title", rt, "GRACZE", 19, TextAnchor.MiddleLeft, UiKit.Text, FontStyle.Bold);
            hud._title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(Pad + 4, -Pad + 2), new Vector2(-2 * Pad, HeaderH));
            hud._title.supportRichText = true;
            hud._title.AddOutline(1f, 0.6f);
            hud._summary = UiKit.Label("Summary", rt, "", 17, TextAnchor.MiddleRight, UiKit.TextDim);
            hud._summary.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Pad - 4, -Pad + 2), new Vector2(-2 * Pad - 8, HeaderH));
            hud._summary.supportRichText = true;
            hud._summary.AddOutline(1f, 0.6f);
            var line = UiKit.Image("Line", rt, new Color(1f, 1f, 1f, 0.12f));
            line.raycastTarget = false;
            line.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -Pad - HeaderH - 1), new Vector2(-2 * Pad, 1));

            hud._more = UiKit.Label("More", rt, "", 16, TextAnchor.MiddleCenter, UiKit.TextDim);
            hud._more.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, Pad - 2), new Vector2(-2 * Pad, 22));
            return hud;
        }

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            if (me == null) return;
            var phase = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            bool lobby = phase == MatchPhase.Lobby;
            bool round = phase == MatchPhase.Hiding || phase == MatchPhase.Seeking || phase == MatchPhase.Ended;

            _sorted.Clear();
            foreach (var p in NetPlayer.All) if (p != null) _sorted.Add(p);
            // Seeker first, then by nick (stable: rows do not jump when states change).
            _sorted.Sort((a, b) => a.Role.Value == b.Role.Value ? string.CompareOrdinal(a.Nick, b.Nick) : b.Role.Value.CompareTo(a.Role.Value));

            int ready = 0, need = 0, hiders = 0, alive = 0;
            foreach (var p in _sorted)
            {
                if (p.OwnerClientId != NetworkManager.ServerClientId) { need++; if (p.Ready.Value) ready++; }
                if (p.IsHider) { hiders++; if (p.Life.Value != LifeState.Dead) alive++; }
            }
            _title.text = $"GRACZE <color=#8f969e>({_sorted.Count})</color>";
            if (lobby && need > 0)
                _summary.text = ready >= need ? $"<color=#6dff7a>gotowi {ready}/{need}</color>" : $"gotowi {ready}/{need}";
            else if (round && hiders > 0)
                _summary.text = $"w grze <color=#77ccff>{alive}/{hiders}</color>";
            else _summary.text = "";

            int shown = Mathf.Min(_sorted.Count, (int)MaxRows);
            while (_rows.Count < shown) _rows.Add(new PlayerListRow(_panel));
            float y = -Pad - HeaderH - 6f;
            float t = Time.unscaledTime;
            var vc = Voice.VoiceChat.Instance;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (i >= shown) { row.SetActive(false); continue; }
                row.SetActive(true);
                row.Root.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(-2 * Pad + 4, PlayerListRow.Height));
                y -= PlayerListRow.Height;
                var p = _sorted[i];
                var d = Describe(p, phase, round, mm);
                d.Me = p == me;
                if (d.Me)
                {
                    d.Speaking = vc != null && vc.Transmitting;
                    d.Level = vc != null ? Mathf.Clamp01(vc.MicLevel * 1.6f) : 0f;
                }
                else
                {
                    d.Speaking = Voice.VoiceChat.IsSpeaking(p);
                    d.Level = -1f;
                }
                row.Set(d, t + i * 0.37f);
            }
            int hidden = _sorted.Count - shown;
            _more.text = hidden > 0 ? $"+{hidden} więcej" : "";
            float h = -y + 4f + (hidden > 0 ? 22f : 0f) + Pad - 4f;
            if (Mathf.Abs(_panel.sizeDelta.y - h) > 0.5f) _panel.sizeDelta = new Vector2(Width, h);
        }

        private static readonly Color CHost = new Color(1f, 0.84f, 0.37f), CReady = new Color(0.43f, 1f, 0.48f),
            CWait = new Color(0.6f, 0.62f, 0.64f), CHider = new Color(0.47f, 0.8f, 1f), CHurt = new Color(1f, 0.82f, 0.5f),
            CWounded = new Color(1f, 0.62f, 0.35f), CDowned = new Color(1f, 0.42f, 0.4f), CDead = new Color(0.5f, 0.5f, 0.52f);

        /// <summary>State column text and colours of one player.</summary>
        private static PlayerListRow.Data Describe(NetPlayer p, MatchPhase phase, bool round, MatchManager mm)
        {
            var d = new PlayerListRow.Data { Nick = p.Nick, NickColor = UiKit.Text, Bleed = -1f };
            bool host = p.OwnerClientId == NetworkManager.ServerClientId;
            if (!round || p.Role.Value == PlayerRole.None)
            {
                if (phase == MatchPhase.Lobby && !host)
                {
                    d.State = p.Ready.Value ? "gotowy" : "czeka";
                    d.StateColor = p.Ready.Value ? CReady : CWait;
                    if (!p.Ready.Value) d.NickColor = new Color(0.8f, 0.8f, 0.78f);
                }
                else
                {
                    d.State = host ? "host" : "";
                    d.StateColor = host ? CHost : CWait;
                }
                return d;
            }
            if (p.IsSeeker)
            {
                d.Seeker = true;
                d.State = "SZUKA";
                d.StateColor = UiKit.SeekerColor;
                return d;
            }
            switch (p.Life.Value)
            {
                case LifeState.Alive:
                    bool legs = (p.Severed.Value & Limb.Legs) != 0;
                    if (p.Wounded.Value) { d.State = "ranny"; d.StateColor = CWounded; }
                    else if (p.Limping.Value || legs) { d.State = "zraniony"; d.StateColor = CHurt; }
                    else { d.State = "chowa się"; d.StateColor = CHider; }
                    break;
                case LifeState.Downed:
                    d.State = "powalony";
                    d.StateColor = CDowned;
                    d.Pulse = true;
                    // Server: DownedUntil = down time + BleedOut, death when the server clock passes it. Lifting does
                    // not stop or add time (ServerRescue just stands the player up), so the bar keeps running until
                    // the state changes. After the round (Ended) nobody bleeds out any more: no bar.
                    // The seekers never see how long a downed hider has left (only the hiders and dead hiders do).
                    var viewer = NetPlayer.Local;
                    bool seekerViews = viewer != null && viewer.IsSeeker;
                    if (phase == MatchPhase.Seeking && mm != null && p.DownedUntil.Value > 0d && !seekerViews)
                    {
                        float total = mm.BleedOut;
                        float left = Mathf.Max(0f, (float)p.BleedLeftNow); // paused while carried
                        if (p.BleedPaused) { d.State = "niesiony"; d.Pulse = false; }
                        d.BleedTotal = total;
                        d.BleedLeft = left;
                        d.Bleed = total > 0f ? Mathf.Clamp01(left / total) : 0f;
                    }
                    break;
                default:
                    d.State = "martwy";
                    d.StateColor = CDead;
                    d.NickColor = new Color(0.55f, 0.55f, 0.56f);
                    d.Dead = true;
                    break;
            }
            return d;
        }
    }
}
