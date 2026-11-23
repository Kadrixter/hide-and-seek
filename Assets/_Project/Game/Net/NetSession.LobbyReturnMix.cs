using System;
using System.Collections;
using System.Text;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Extras for -testlobbyreturn (QA): what lives between the scenes.
    /// <para>Every copy: 3 s into the end screen and 2.5 s after arriving in the lobby it logs "[LobbyView]" (every enabled
    /// camera rendering to the screen, audio listeners, Camera.main, own life / phase) and, with -lobbyshots &lt;dir&gt;,
    /// saves what is really on the screen (a picture standing still shows up here).</para>
    /// <para>-lobbytestmix (host): odd rounds every hider dies (the last one by the Glock finisher shown to everybody, ragdolls),
    /// even rounds one hider dies and the time runs out; before every 3rd round one client collapses from the cans in the lobby.</para>
    /// <para>-lobbytestrole rc|pa (client): every round this copy, as a hider, drives the RC car (still driving when the round
    /// ends or when he is killed) or holds the PA microphone.</para>
    /// </summary>
    public partial class NetSession
    {
        private static bool HasArg(string a) => Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0;

        private void StartLobbyMixWatch()
        {
            if (!HasArg("-testlobbyreturn") && !HasArg("-lobbywatch")) return;
            StartCoroutine(LobbyViewWatch());
            string role = ArgOr("-lobbytestrole", "");
            if (role.Length > 0) StartCoroutine(LobbyRoleLoop(role));
        }

        private static string LvNick => NetPlayer.Local != null ? NetPlayer.Local.Nick : "?";

        /// <summary>Every enabled screen camera / audio listener of this copy (what draws the picture, what hears).</summary>
        public static string LobbyViewState()
        {
            var sb = new StringBuilder();
            int cams = 0;
            foreach (var c in Camera.allCameras)
            {
                if (c == null || c.targetTexture != null) continue;
                cams++;
                sb.Append(sb.Length > 0 ? ", " : "").Append(c.name).Append("(d").Append(c.depth.ToString("0")).Append(c.CompareTag("MainCamera") ? ",main" : "").Append(')');
            }
            int listeners = 0;
            foreach (var l in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if (l.isActiveAndEnabled) listeners++;
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            return $"screen cameras {cams} [{sb}], listeners {listeners}, Camera.main={(Camera.main != null ? Camera.main.name : "NONE")}, " +
                   $"own cam={(me != null && me.Camera != null ? me.Camera.enabled.ToString() : "-")}, life={(me != null ? me.Life.Value.ToString() : "-")}, " +
                   $"phase={(mm != null ? mm.Phase.Value.ToString() : "-")}, scene={SceneManager.GetActiveScene().name}, killcam={UI.KillCam.Active}, rcview={(RcCarView.Active != null)}";
        }

        private IEnumerator LobbyViewWatch()
        {
            string dir = ArgOr("-lobbyshots", "");
            int round = 0;
            while (true)
            {
                while (SceneManager.GetActiveScene().name != GameScene) yield return new WaitForSeconds(0.25f);
                round++;
                var mm = MatchManager.Instance;
                while (mm != null && mm.Phase.Value != MatchPhase.Ended && SceneManager.GetActiveScene().name == GameScene) yield return null;
                if (SceneManager.GetActiveScene().name == GameScene)
                {
                    yield return new WaitForSeconds(3f);
                    Debug.Log($"[LobbyView] {LvNick} round {round} end screen: {LobbyViewState()}");
                    Shot(dir, $"{LvNick}_r{round}_a_end");
                }
                float t0 = Time.realtimeSinceStartup;
                while (SceneManager.GetActiveScene().name != LobbyScene) yield return new WaitForSeconds(0.1f);
                float took = Time.realtimeSinceStartup - t0;
                yield return new WaitForSeconds(2.5f);
                string state = LobbyViewState();
                bool bad = !state.Contains("screen cameras 1 ") || state.Contains("listeners 0") || state.Contains("NONE");
                Debug.Log($"[LobbyView] {LvNick} round {round} lobby (+{took:0.0} s after the end screen shot): {state}{(bad ? " - SUSPECT" : "")}");
                Shot(dir, $"{LvNick}_r{round}_b_lobby");
            }
        }

        // ---------------------------------------------------------------- client roles

        private IEnumerator LobbyRoleLoop(string role)
        {
            while (true)
            {
                while (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != MatchPhase.Seeking ||
                       SceneManager.GetActiveScene().name != GameScene || NetPlayer.Local == null)
                    yield return new WaitForSeconds(0.2f);
                var me = NetPlayer.Local;
                if (me.IsHider)
                {
                    if (role == "rc") yield return RoleRc(me);
                    else if (role == "pa") yield return RolePa(me);
                }
                Debug.Log($"[LobbyRole] {me.Nick} {role} set up: {LobbyViewState()}");
                while (SceneManager.GetActiveScene().name == GameScene) yield return new WaitForSeconds(0.2f);
                LocalPlayerController.TestRcActive = false;
                LocalPlayerController.TestRcInput = Vector2.zero;
            }
        }

        private IEnumerator RoleRc(NetPlayer me)
        {
            var mm = MatchManager.Instance;
            int idx = (int)(Manager.LocalClientId % 2);
            int carSlot = Hotbar.SlotOf(me, HotbarItem.RcCar);
            if (carSlot < 0)
            {
                Vector3 car = RcCarShelf.CarPos(idx);
                yield return LookAt(me, car, car + Quaternion.Euler(0f, RcCarShelf.Yaw, 0f) * Vector3.back * 2f);
                me.RequestRcPickupRpc((byte)idx);
                yield return new WaitForSeconds(0.8f);
                carSlot = Hotbar.SlotOf(me, HotbarItem.RcCar);
            }
            if (carSlot < 0) { Debug.Log($"[LobbyRole] {me.Nick} rc: no car"); yield break; }
            if (OpenSpot(out var open)) { me.TeleportLocal(open, 0f); me.Motor.SetLook(0f, 0f); }
            yield return new WaitForSeconds(0.6f);
            Hotbar.TestSelect = carSlot;
            yield return new WaitForSeconds(0.6f);
            Hotbar.TestClick = true;
            yield return new WaitForSeconds(2.6f);
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0.4f, 0.6f); // circles until the round ends
            Debug.Log($"[LobbyRole] {me.Nick} rc: driving={RcCarView.Active != null}");
        }

        private IEnumerator RolePa(NetPlayer me)
        {
            Vector3 hang = PaLayout.MicHangPos(0);
            me.TeleportLocal(new Vector3(hang.x, PaLayout.FloorY(0) + 0.05f, hang.z - 0.75f), 0f);
            yield return new WaitForSeconds(1f);
            me.RequestPaRpc(true, 0);
            yield return new WaitForSeconds(1f);
            var mm = MatchManager.Instance;
            Debug.Log($"[LobbyRole] {me.Nick} pa: holder={(mm.PaHolderPlayer != null ? mm.PaHolderPlayer.Nick : "-")}");
        }

        // ---------------------------------------------------------------- host: kills of the mixed rounds

        /// <summary>-lobbytestmix: who dies this round (see the class summary). Returns a log line.</summary>
        private IEnumerator LobbyMixKills(MatchManager mm, int round, Action<string> log)
        {
            NetPlayer seeker = null;
            var hiders = new System.Collections.Generic.List<NetPlayer>();
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                if (p.IsSeeker && seeker == null) seeker = p;
                else if (p.IsHider) hiders.Add(p);
            }
            if (hiders.Count == 0 || seeker == null) { log("mix: nobody to kill"); yield break; }
            yield return new WaitForSeconds(6f); // the roles set up (RC car driving, PA mic taken)
            if (round % 2 == 1)
            {
                for (int i = 0; i < hiders.Count - 1; i++)
                {
                    mm.TestKill(hiders[i]);
                    log($"mix: {hiders[i].Nick} killed");
                    yield return new WaitForSeconds(0.7f);
                }
                var last = hiders[hiders.Count - 1];
                Vector3 at = last.transform.position + last.transform.forward * 1.1f;
                seeker.TeleportRpc(at, Quaternion.LookRotation(-last.transform.forward).eulerAngles.y);
                yield return new WaitForSeconds(0.5f);
                mm.TestKillMove(seeker, last, KillMove.GlockFinisher);
                log($"mix: {last.Nick} - Glock finisher (shown to everybody)");
            }
            else
            {
                mm.TestKill(hiders[0]);
                log($"mix: {hiders[0].Nick} killed, the others live to the time-out");
            }
        }

        /// <summary>-lobbytestmix, lobby before round <paramref name="round"/>: every 3rd round a client collapses from the cans.</summary>
        private static void LobbyMixLobby(int round, Action<string> log)
        {
            if (round % 3 != 0) return;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.IsLocalPlayer) continue;
                p.ServerAddDrink(Drunkenness.CollapseCans);
                log($"mix: {p.Nick} drank {Drunkenness.CollapseCans} cans (collapse) right before the start");
                break;
            }
        }
    }
}
