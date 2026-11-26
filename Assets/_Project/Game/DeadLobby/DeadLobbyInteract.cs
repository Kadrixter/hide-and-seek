using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using HideAndSeek.Player;

namespace HideAndSeek.Game
{
    /// <summary>
    /// E in the lobby copy (dead body): sit on a sofa / pouf, use the switches and toys (light, fan, fireplace, record
    /// player, fish, globe - through the server, shared with the real lobby's state like for the living), and the round
    /// controls that must stay shut while the round runs (start lever, settings board, exit door, vending machine, SNAKE):
    /// they only answer "Runda trwa - poczekaj na koniec".
    /// </summary>
    public static class DeadLobbyInteract
    {
        private enum Kind { None, Seat, Prop, Locked }

        private static Kind _kind;
        private static int _arg;
        private static string _locked;

        public const string RoundRuns = "Runda trwa - poczekaj na koniec.";

        /// <summary>Per frame while the body is under control: finds the target in view, shows the prompt, E acts.</summary>
        public static void Tick(DeadLobbyBody body)
        {
            Vector3 eye = body.Eye, fwd = body.Forward;
            _kind = Kind.None;
            string text = null;
            Vector3 world = default;
            float best = float.MaxValue;

            void Offer(Kind k, int arg, Vector3 p, string t, float d, string locked = null)
            {
                if (d >= best) return;
                best = d; _kind = k; _arg = arg; world = p; text = t; _locked = locked;
            }

            if (body.Seat < 0)
            {
                foreach (var s in LobbySeat.All)
                {
                    if (s == null || SeatTaken(s.Index)) continue;
                    Vector3 p = s.AimPoint;
                    float d = Vector3.Distance(eye, p);
                    if (d <= 2.4f && InView(eye, fwd, p, 30f, s.transform.parent)) Offer(Kind.Seat, s.Index, p, "E  usiądź", d + 0.3f);
                }
                int bits = LobbyPropDriver.Bits;
                foreach (var pr in LobbyProp.All)
                {
                    if (pr == null || pr.kind == LobbyPropKind.Darts) continue;
                    Vector3 a = pr.Aim;
                    float d = Vector3.Distance(eye, a);
                    if (d <= pr.range && InView(eye, fwd, a, 32f, pr.transform)) Offer(Kind.Prop, (int)pr.kind, a + Vector3.up * 0.12f, pr.PromptText(bits), d);
                }
                var lever = LobbyButton.Instance;
                if (lever != null) Locked(lever.AimPoint, lever.transform, 2.6f, "E  dźwignia startu", "Runda trwa - poczekaj na koniec. Nowej rundy nie da się teraz zacząć.");
                var board = LobbySettingsBoard.Instance;
                if (board != null) Locked(board.transform.position, board.transform, 3f, "E  ustawienia rundy", "Runda trwa - ustawienia zmienisz po jej końcu.");
                var door = LobbyExitDoor.Instance;
                if (door != null) Locked(door.HandleWorld, door.transform, 2.2f, "E  wyjdź", RoundRuns);
                var vend = LobbyVending.Instance;
                if (vend != null) Locked(vend.AimPoint, vend.transform, 2.2f, "E  automat z napojami", "Runda trwa - automat działa tylko między rundami.");
                for (int cab = 0; cab < 4; cab++)
                {
                    var arc = LobbyArcade.Of(cab);
                    if (arc != null && arc.screen != null)
                        Locked(arc.screen.bounds.center, arc.transform, 2.2f, "E  zagraj w SNAKE", "Runda trwa - automaty działają tylko między rundami.");
                }
            }

            void Locked(Vector3 p, Transform owner, float range, string t, string why)
            {
                float d = Vector3.Distance(eye, p);
                if (d <= range && InView(eye, fwd, p, 32f, owner)) Offer(Kind.Locked, 0, p + Vector3.up * 0.15f, t + "  (runda trwa)", d, why);
            }

            DeadLobbyHud.SetPrompt(text, world);
            var kb = Keyboard.current;
            if (kb == null || !KeyOrPad.Down(Key.E) || _kind == Kind.None) return;
            switch (_kind)
            {
                case Kind.Seat:
                    body.SitOn(_arg);
                    break;
                case Kind.Prop:
                    if (MatchManager.Instance != null) MatchManager.Instance.GhostPropRpc((byte)_arg);
                    break;
                case Kind.Locked:
                    GameAudio.Play2D(GameSfx.UiError, 0.7f);
                    GameHud.Toast(_locked, 3.5f);
                    break;
            }
        }

        /// <summary>Another dead player's ghost sits there.</summary>
        private static bool SeatTaken(int index)
        {
            foreach (var p in NetPlayer.All)
                if (p != null && !p.IsOwner && p.LobbyGhost.Value.Mode != LobbyGhostState.Off && p.LobbyGhost.Value.Seat == index) return true;
            return false;
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>Within <paramref name="angle"/> degrees of the view and nothing solid in between (the target itself does not count).</summary>
        private static bool InView(Vector3 eye, Vector3 fwd, Vector3 p, float angle, Transform owner)
        {
            Vector3 d = p - eye;
            if (Vector3.Angle(fwd, d) > angle) return false;
            int n = Physics.RaycastNonAlloc(eye, d.normalized, Hits, Mathf.Max(0f, d.magnitude - 0.08f), ~((1 << 2) | (1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = Hits[i].transform;
                if (owner != null && t.IsChildOf(owner)) continue;
                if ((Hits[i].point - p).sqrMagnitude < 0.35f * 0.35f) continue; // the target's own collider (a parent box)
                return false;
            }
            return true;
        }
    }
}
