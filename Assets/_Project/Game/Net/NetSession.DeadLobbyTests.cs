using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using HideAndSeek.Game.Voice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testdeadlobby &lt;dir&gt; (host "Host" = seeker with -forceseeker -autostart -autostartplayers 3 -nointro, clients
    /// "Klient" and "Klient2" = hiders; all three get the switch): Klient is killed and walks into the lobby copy (shots of
    /// the walk-in), uses the TV (prompt, live grey feed, switching players, full screen and back, voice routing), the
    /// locked lever, sits on the sofa; Klient2 stays alive and has no lobby copy; then the host (seeker) dies too and both
    /// dead see each other's ghosts; the round ends, everybody is back in the real lobby, the next round starts clean.
    /// Logs: [DeadLobbyTest] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static void DlLog(string s) => Debug.Log("[DeadLobbyTest] " + s);

        private static IEnumerator DlWait(System.Func<bool> cond, float max)
        {
            float t0 = Time.time;
            while (!cond() && Time.time - t0 < max) yield return null;
        }

        private IEnumerator DeadLobbyTest(string dir)
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            DlLog($"{me.Nick}: seeking, scenes={SceneManager.sceneCount}");
            if (Manager.IsServer) yield return DlHost(dir);
            else if (me.Nick == "Klient") yield return DlVictim(dir);
            else yield return DlAlive(dir);
            yield return DlAfterRound(dir, me.Nick == "Host" ? "h" : me.Nick == "Klient" ? "c1" : "c2");
        }

        private IEnumerator DlHost(string dir)
        {
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            var h1 = ByNick("Klient");
            if (h1 == null || ByNick("Klient2") == null) { DlLog("need clients Klient and Klient2"); yield break; }
            yield return new WaitForSeconds(2f);
            mm.TestKill(h1);
            float tk = Time.time;
            DlLog("killed Klient");
            yield return DlWait(() => h1.LobbyGhost.Value.Mode == LobbyGhostState.InLobby, 30f);
            DlLog($"Klient's ghost in the lobby after {Time.time - tk:0.0} s: {h1.LobbyGhost.Value.Mode} at {h1.LobbyGhost.Value.Pos} -> {Pf(h1.LobbyGhost.Value.Mode == LobbyGhostState.InLobby)}");
            DlLog($"host alive (seeker): scenes={SceneManager.sceneCount}, dead-lobby stage={DeadLobby.Stage}, avatars={(DeadLobbyAvatars.Instance != null)} -> {Pf(SceneManager.sceneCount == 1 && DeadLobby.Stage == DeadLobbyStage.None && DeadLobbyAvatars.Instance == null)}");
            Shot(dir, "h_a_seeker_alive_view");
            yield return new WaitForSeconds(16f);

            // The host dies too.
            mm.TestKill(me);
            tk = Time.time;
            DlLog("host killed himself");
            yield return DlWait(() => DeadLobby.Stage == DeadLobbyStage.Lobby, 30f);
            bool copy = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).name == DeadLobby.SceneName) copy = true;
            DlLog($"host in the lobby after {Time.time - tk:0.0} s: stage={DeadLobby.Stage} copy={copy} active={SceneManager.GetActiveScene().name} -> {Pf(DeadLobby.Stage == DeadLobbyStage.Lobby && copy && SceneManager.GetActiveScene().name == GameScene)}");
            int tagged = GameObject.FindGameObjectsWithTag(GameLayers.SpawnPointTag).Length;
            int copyTagged = 0;
            foreach (var go in GameObject.FindGameObjectsWithTag(GameLayers.SpawnPointTag)) if (go.scene.name == DeadLobby.SceneName) copyTagged++;
            DlLog($"spawn points: {tagged} tagged, {copyTagged} of them in the copy -> {Pf(copyTagged == 0)}");
            var body = DeadLobby.Body;
            var g1 = h1.LobbyGhost.Value;
            if (body != null) body.TestPlace(new Vector3(-3.0f, 0f, 3.2f), g1.Pos + Vector3.up * (g1.Seat >= 0 ? 1.0f : 1.4f));
            yield return new WaitForSeconds(2.5f);
            var av = DeadLobbyAvatars.Of(h1);
            float diff = av != null ? Vector3.Distance(DeadLobbyWorld.ToLocal(av.transform.position), g1.Seat >= 0 && LobbySeat.Get(g1.Seat) != null ? DeadLobbyWorld.ToLocal(LobbySeat.Get(g1.Seat).transform.position) : h1.LobbyGhost.Value.Pos) : -1f;
            DlLog($"host sees Klient's ghost: avatar={(av != null)} seat={g1.Seat} diff={diff:0.00} m -> {Pf(av != null && diff >= 0f && diff < 0.3f)}");
            Shot(dir, "h_b_lobby_sees_klient");
            yield return new WaitForSeconds(9f);
            DlLog("host ends the round");
            mm.TestEndRound();
        }

        private IEnumerator DlVictim(string dir)
        {
            var me = NetPlayer.Local;
            yield return DlWait(() => me.Life.Value == LifeState.Dead, 30f);
            float td = Time.time;
            DlLog($"Klient dead={me.Life.Value == LifeState.Dead}, spectator first={GameHud.Spectated != null}");
            yield return DlWait(() => DeadLobby.Stage == DeadLobbyStage.FadeOut, 15f);
            yield return new WaitForSeconds(0.45f);
            Shot(dir, "c1_a_fade");
            yield return DlWait(() => DeadLobby.Stage == DeadLobbyStage.WalkIn, 20f);
            DlLog($"walk-in starts {Time.time - td:0.0} s after the death -> {Pf(DeadLobby.Stage == DeadLobbyStage.WalkIn)}");
            float[] at = { 0.35f, 1.7f, 2.5f, 3.4f, 4.6f };
            float tw = Time.time;
            for (int i = 0; i < at.Length; i++)
            {
                while (Time.time - tw < at[i]) yield return null;
                Shot(dir, $"c1_b_walkin_{i}");
            }
            yield return DlWait(() => DeadLobby.Stage == DeadLobbyStage.Lobby, 10f);
            var body = DeadLobby.Body;
            bool view = body != null && Camera.main == body.Cam && me.Camera != null && !me.Camera.enabled;
            DlLog($"in the lobby: stage={DeadLobby.Stage} scenes={SceneManager.sceneCount} main camera=lobby body {view} -> {Pf(DeadLobby.Stage == DeadLobbyStage.Lobby && SceneManager.sceneCount == 2 && view)}");

            // The TV.
            var tv = DeadLobbyTv.Instance;
            Vector3 tvLocal = tv != null ? DeadLobbyWorld.ToLocal(tv.Center) : new Vector3(-4f, 1.98f, 5.94f);
            body.TestPlace(new Vector3(tvLocal.x + 0.3f, 0f, tvLocal.z - 3.2f), tvLocal);
            yield return new WaitForSeconds(1.8f);
            DlLog($"TV: reach={tv != null && tv.InReach} target={(tv != null && tv.Target != null ? tv.Target.Nick : "-")} frames={(tv != null ? tv.Frames : 0)} volume={DeadLobbyTv.Volume:0.00} -> {Pf(tv != null && tv.InReach && tv.Target != null && tv.Frames > 10 && Mathf.Abs(DeadLobbyTv.Volume - 0.5f) < 0.01f)}");
            Shot(dir, "c1_c_tv_prompt");
            if (tv != null) CamShot(dir, "c1_d_tv_closeup", tv.Center + tv.Normal * 1.25f, tv.Center);
            var first = tv != null ? tv.Target : null;
            DeadLobbyTv.TestNext = true;
            yield return new WaitForSeconds(1.5f);
            DlLog($"TV switch: {(first != null ? first.Nick : "-")} -> {(tv != null && tv.Target != null ? tv.Target.Nick : "-")} -> {Pf(tv != null && tv.Target != null && tv.Target != first)}");
            Shot(dir, "c1_e_tv_switched");
            if (tv != null) CamShot(dir, "c1_f_tv_switched_closeup", tv.Center + tv.Normal * 1.25f, tv.Center);

            // Voice: a living player's voice comes out of the TV speaker, the watched one at the TV volume.
            NetPlayer living = tv != null ? tv.Target : null;
            if (living != null)
            {
                var vp = VoicePlayback.For(living);
                yield return new WaitForSeconds(0.3f);
                var src = vp.GetComponent<AudioSource>();
                float d = Vector3.Distance(vp.transform.position, tv.Speaker);
                DlLog($"voice of {living.Nick}: {d:0.00} m from the TV speaker, volume {src.volume:0.00}, 3D {src.spatialBlend:0} -> {Pf(d < 0.05f && Mathf.Abs(src.volume - 0.5f) < 0.01f && src.spatialBlend > 0.9f)}");
            }

            // F: full screen and back.
            var watched = tv != null ? tv.Target : null;
            DeadLobby.TestToggleWatch = true;
            yield return new WaitForSeconds(2f);
            DlLog($"F -> stage={DeadLobby.Stage} spectating={(GameHud.Spectated != null ? GameHud.Spectated.Nick : "-")} -> {Pf(DeadLobby.Stage == DeadLobbyStage.Watching && GameHud.Spectated != null && GameHud.Spectated == watched)}");
            Shot(dir, "c1_g_fullscreen");
            DeadLobby.TestToggleWatch = true;
            yield return new WaitForSeconds(1.5f);
            DlLog($"F again -> stage={DeadLobby.Stage} main=lobby body {Camera.main == body.Cam} spectator gone {GameHud.Spectated == null} -> {Pf(DeadLobby.Stage == DeadLobbyStage.Lobby && Camera.main == body.Cam && GameHud.Spectated == null)}");
            Shot(dir, "c1_h_back_in_lobby");

            // The start lever stays locked.
            var lever = LobbyButton.Instance;
            if (lever != null)
            {
                body.TestPlace(DeadLobbyWorld.ToLocal(lever.StandPoint) - new Vector3(0.6f, 0f, 0f), DeadLobbyWorld.ToLocal(lever.AimPoint));
                yield return new WaitForSeconds(1.2f);
                Shot(dir, "c1_i_lever_locked");
                DlLog("lever prompt shot taken (expect \"E  dźwignia startu  (runda trwa)\")");
            }
            // A lobby toy through the server (globe).
            var globe = LobbyProp.Of(LobbyPropKind.Globe);
            if (globe != null && MatchManager.Instance != null) MatchManager.Instance.GhostPropRpc((byte)LobbyPropKind.Globe);

            // Sit on the sofa facing the TV and wait for the host's ghost.
            int sofa = -1;
            float best = 99f;
            foreach (var s in LobbySeat.All)
            {
                if (s == null) continue;
                float dd = Vector3.Distance(DeadLobbyWorld.ToLocal(s.transform.position), new Vector3(-4f, 0f, 1.55f));
                if (dd < best) { best = dd; sofa = s.Index; }
            }
            body.TestPlace(new Vector3(-3.5f, 0f, 2.6f), tvLocal);
            yield return new WaitForSeconds(0.3f);
            if (sofa >= 0) body.SitOn(sofa);
            yield return new WaitForSeconds(1.5f);
            DlLog($"sitting on seat {sofa}: ghost seat={me.LobbyGhost.Value.Seat} -> {Pf(sofa >= 0 && me.LobbyGhost.Value.Seat == sofa)}");
            Shot(dir, "c1_j_sofa_tv");
            var host = ByNick("Host");
            yield return DlWait(() => DeadLobbyAvatars.Of(host) != null, 45f);
            yield return new WaitForSeconds(4f);
            var av = DeadLobbyAvatars.Of(host);
            float diff = av != null ? Vector3.Distance(DeadLobbyWorld.ToLocal(av.transform.position), host.LobbyGhost.Value.Pos) : -1f;
            DlLog($"Klient sees the host's ghost: avatar={(av != null)} diff={diff:0.00} m -> {Pf(av != null && diff >= 0f && diff < 0.3f)}");
            Shot(dir, "c1_k_sees_host");
        }

        private IEnumerator DlAlive(string dir)
        {
            var h1 = ByNick("Klient");
            yield return DlWait(() => h1 != null && h1.LobbyGhost.Value.Mode == LobbyGhostState.InLobby, 40f);
            yield return new WaitForSeconds(1f);
            DlLog($"Klient2 alive: scenes={SceneManager.sceneCount} tv={(Object.FindAnyObjectByType<LobbyTv>() != null)} door={(LobbyExitDoor.Instance != null)} ghosts={(DeadLobbyAvatars.Instance != null)} -> {Pf(SceneManager.sceneCount == 1 && LobbyExitDoor.Instance == null && DeadLobbyAvatars.Instance == null)}");
            Shot(dir, "c2_a_alive_view");
            var host = ByNick("Host");
            yield return DlWait(() => host != null && host.LobbyGhost.Value.Mode == LobbyGhostState.InLobby, 60f);
            yield return new WaitForSeconds(2f);
            DlLog($"Klient2 alive, both others dead: scenes={SceneManager.sceneCount} stage={DeadLobby.Stage} -> {Pf(SceneManager.sceneCount == 1 && DeadLobby.Stage == DeadLobbyStage.None)}");
            Shot(dir, "c2_b_alive_view2");
        }

        private IEnumerator DlAfterRound(string dir, string tag)
        {
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            yield return DlWait(() => mm.Phase.Value == MatchPhase.Lobby && SceneManager.GetActiveScene().name == LobbyScene, 90f);
            yield return new WaitForSeconds(3f);
            bool copy = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).name == DeadLobby.SceneName) copy = true;
            DlLog($"{me.Nick} back in the lobby: stage={DeadLobby.Stage} copy={copy} scenes={SceneManager.sceneCount} own camera={(me.Camera != null && me.Camera.enabled && Camera.main == me.Camera)} ghost={me.LobbyGhost.Value.Mode} -> {Pf(mm.Phase.Value == MatchPhase.Lobby && DeadLobby.Stage == DeadLobbyStage.None && !copy && me.Camera != null && Camera.main == me.Camera && me.LobbyGhost.Value.Mode == LobbyGhostState.Off)}");
            Shot(dir, tag + "_x_back_in_lobby");
            if (Manager.IsServer)
            {
                yield return new WaitForSeconds(4f);
                DlLog("host starts the next round");
                mm.TestStartNextRound();
            }
            yield return DlWait(() => mm.Phase.Value == MatchPhase.Seeking, 90f);
            yield return new WaitForSeconds(2f);
            DlLog($"{me.Nick} next round: phase={mm.Phase.Value} life={me.Life.Value} stage={DeadLobby.Stage} scenes={SceneManager.sceneCount} -> {Pf(mm.Phase.Value == MatchPhase.Seeking && me.Life.Value == LifeState.Alive && DeadLobby.Stage == DeadLobbyStage.None && SceneManager.sceneCount == 1)}");
            Shot(dir, tag + "_y_next_round");
            DlLog($"{me.Nick} DONE");
        }
    }
}
