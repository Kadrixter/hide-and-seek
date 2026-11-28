using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby tests (opt-in, host in the lobby):
    /// <para>-lobbyshots DIR: renders the named views (<see cref="Views"/>, same as the editor's lobby shots) from a
    /// free camera, then walks to every mini interaction, uses it and saves the player's screen + a side view
    /// (prop_*.png), logs "[LobbyShots]".</para>
    /// <para>-lobbyfilm DIR (host, best with a client): frames of a camera flight through the lobby (fly_*.png, 25 fps,
    /// the client feeds the fish on the way) and of three dart throws from the side (darts_*.png).</para>
    /// <para>-testarcade2 (host + 1 client, both in the lobby): host at cabinet 0, the client at cabinet 1, both games
    /// run at once with an autopilot; logs both states ("[Arcade2]") and photographs both screens (-arcade2shots DIR).</para>
    /// </summary>
    public sealed class LobbyShotsProbe : MonoBehaviour
    {
        public static readonly (string name, Vector3 eye, Vector3 at, float fov)[] Views =
        {
            ("overview_se", new Vector3(7.4f, 2.7f, -5.4f), new Vector3(-3f, 0.6f, 2.5f), 72f),
            ("overview_nw", new Vector3(-7.4f, 2.7f, 5.4f), new Vector3(3f, 0.6f, -2.5f), 72f),
            ("lounge", new Vector3(-0.8f, 1.65f, -0.6f), new Vector3(-4.6f, 0.7f, 3.6f), 62f),
            ("fireplace", new Vector3(-3.3f, 1.35f, 3.3f), new Vector3(-4f, 1.2f, 6f), 60f),
            ("tv", new Vector3(-4.0f, 1.3f, 0.9f), new Vector3(-4f, 2.1f, 6f), 66f),
            ("fan", new Vector3(-3.0f, 1.5f, 0.6f), new Vector3(-4f, 3.15f, 2.8f), 55f),
            ("games", new Vector3(1.4f, 1.7f, 0.2f), new Vector3(5.2f, 1.1f, 4.8f), 62f),
            ("arcades", new Vector3(4.8f, 1.6f, 2.6f), new Vector3(4.8f, 1.2f, 6f), 58f),
            ("darts", new Vector3(5.0f, 1.65f, 3.0f), new Vector3(8f, 1.6f, 3f), 55f),
            ("east", new Vector3(3.0f, 1.7f, -0.8f), new Vector3(8f, 1.2f, -1.6f), 66f),
            ("south", new Vector3(-1.5f, 1.7f, -1.0f), new Vector3(-3f, 0.9f, -6f), 66f),
            ("mirror", new Vector3(1.6f, 1.6f, -3.2f), new Vector3(1.6f, 1.45f, -6f), 55f),
            ("aquarium", new Vector3(-2.75f, 1.45f, -4.2f), new Vector3(-2.75f, 1.0f, -6f), 50f),
            ("record", new Vector3(-4.6f, 1.45f, -4.5f), new Vector3(-4.5f, 0.6f, -6f), 50f),
            ("west", new Vector3(-3.0f, 1.7f, -0.6f), new Vector3(-8f, 1.0f, -1.8f), 66f),
            ("bookshelf", new Vector3(-5.0f, 1.5f, -2.6f), new Vector3(-8f, 1.2f, -2.6f), 60f),
            ("exit", new Vector3(3.0f, 1.65f, -2.8f), new Vector3(5.2f, 1.2f, -6f), 60f),
            ("gate", new Vector3(-4.6f, 1.65f, 0.2f), new Vector3(-8.2f, 1.5f, 2.3f), 62f),
            ("gate_close", new Vector3(-6.6f, 1.55f, 2.25f), new Vector3(-9.5f, 2.1f, 2.25f), 70f),
            ("pegboard", new Vector3(-6.4f, 1.6f, 0.1f), new Vector3(-8f, 1.55f, 0.1f), 55f),
            ("sofa_close", new Vector3(-2.9f, 1.25f, 0.4f), new Vector3(-4.3f, 0.45f, 1.6f), 58f),
            ("ceiling", new Vector3(1.0f, 1.6f, -2.0f), new Vector3(-3f, 3.6f, 1.5f), 75f),
            ("ceiling_up", new Vector3(0.5f, 1.7f, 0.0f), new Vector3(0.6f, 3.6f, 0.4f), 80f),
            ("passage_ceiling", new Vector3(-8.6f, 1.6f, 2.25f), new Vector3(-12.0f, 3.0f, 2.25f), 70f),
            ("exit_ceiling", new Vector3(-11.0f, 1.6f, 1.5f), new Vector3(-12.1f, 3.0f, 2.25f), 75f),
        };

        private string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(a, "-lobbyshots") < 0 && System.Array.IndexOf(a, "-testarcade2") < 0 && System.Array.IndexOf(a, "-lobbyfilm") < 0) return;
            var g = new GameObject("LobbyShotsProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<LobbyShotsProbe>();
        }

        private static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        private static void Log(string s) => Debug.Log("[LobbyShots] " + s);

        private IEnumerator Start()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || !MatchManager.InLobbyScene)
            {
                mm = MatchManager.Instance;
                yield return null;
            }
            yield return new WaitForSeconds(2.5f);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testarcade2") >= 0)
            {
                _dir = Arg("-arcade2shots");
                if (_dir != null) System.IO.Directory.CreateDirectory(_dir);
                if (mm.IsServer) yield return ArcadeTwo(mm);
                yield break;
            }
            if (Arg("-lobbyfilm") is string film)
            {
                _dir = film;
                System.IO.Directory.CreateDirectory(_dir);
                yield return Film(mm);
                Log("film done");
                yield break;
            }
            _dir = Arg("-lobbyshots");
            System.IO.Directory.CreateDirectory(_dir);
            foreach (var v in Views) { Render(v.name, v.eye, v.at, v.fov); yield return null; }
            Log($"{Views.Length} views rendered");
            if (mm.IsServer) yield return UseProps(mm);
            Log("done");
        }

        /// <summary>Renders the lobby from a free camera into DIR/NAME.png (what other players would see).</summary>
        private int _w = 1280, _h = 720;

        public void Render(string name, Vector3 from, Vector3 at, float fov = 60f)
        {
            if (_dir == null) return;
            int w = _w, h = _h;
            var cam = new GameObject("ProbeCam").AddComponent<Camera>();
            cam.enabled = false;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9); // no first-person arms
            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(cam.gameObject);
        }

        private void Shot(string name)
        {
            if (_dir != null) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, name + ".png"));
        }

        // ================================================================ -lobbyfilm DIR: camera flight + darts

        /// <summary>Camera key points of the flight through the lobby (eye, look-at).</summary>
        private static readonly (Vector3 eye, Vector3 at)[] Flight =
        {
            (new Vector3(6.8f, 2.4f, -5.0f), new Vector3(-2f, 0.8f, 2f)),
            (new Vector3(1.0f, 1.8f, -1.2f), new Vector3(-4f, 0.9f, 3.5f)),
            (new Vector3(-2.4f, 1.5f, 1.0f), new Vector3(-4f, 1.1f, 5.6f)),     // lounge, fireplace
            (new Vector3(-3.3f, 1.4f, 3.6f), new Vector3(-4f, 1.6f, 5.9f)),     // fireplace + TV
            (new Vector3(-3.0f, 1.6f, 1.8f), new Vector3(-4f, 3.2f, 2.9f)),     // fan
            (new Vector3(0.6f, 1.7f, 2.4f), new Vector3(4.8f, 1.3f, 5.6f)),     // arcades
            (new Vector3(4.8f, 1.6f, 3.4f), new Vector3(4.8f, 1.3f, 5.6f)),
            (new Vector3(5.2f, 1.7f, 2.0f), new Vector3(8f, 1.7f, 3f)),         // darts
            (new Vector3(4.6f, 1.7f, -1.0f), new Vector3(8f, 1.2f, -1.8f)),     // lever, cork board, vending
            (new Vector3(2.4f, 1.6f, -2.8f), new Vector3(1.6f, 1.5f, -6f)),     // mirror
            (new Vector3(-1.4f, 1.4f, -4.0f), new Vector3(-2.75f, 1.0f, -6f)),  // aquarium
            (new Vector3(-2.75f, 1.2f, -4.85f), new Vector3(-2.75f, 1.05f, -6f)),
            (new Vector3(-4.4f, 1.5f, -3.6f), new Vector3(-6.5f, 0.9f, -5.6f)), // record player, radio
            (new Vector3(-4.0f, 1.7f, -1.0f), new Vector3(-8f, 1.2f, -1.5f)),   // bookshelf, globe, bricks
            (new Vector3(1.5f, 2.3f, -3.5f), new Vector3(-3f, 0.8f, 2.5f)),
        };

        private static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) =>
            0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (3f * b - a - 3f * c + d) * t * t * t);

        private IEnumerator Film(MatchManager mm)
        {
            var me = NetPlayer.Local;
            NetPlayer actor = null;
            for (float wait = 0f; wait < 10f && actor == null; wait += 0.25f)
            {
                foreach (var p in NetPlayer.All) if (p != me) actor = p;
                if (actor == null) yield return new WaitForSeconds(0.25f);
            }
            if (actor != null) yield return new WaitForSeconds(5f);
            // both players wait in the north-west corner behind the side sofa, out of the camera's way
            Vector3 park = new Vector3(-7.2f, 0.02f, 4.7f);
            me.Motor.Teleport(park + new Vector3(0f, 0f, -1.1f), 90f);
            if (actor != null) actor.TeleportRpc(park, 90f);
            // the film camera is the "main" camera (the mirror reflects for Camera.main), rendering into a texture
            const int w = 1280, h = 720;
            var playerCam = Camera.main;
            string playerTag = playerCam != null ? playerCam.tag : "MainCamera";
            if (playerCam != null) playerCam.tag = "Untagged";
            var cam = new GameObject("FilmCam").AddComponent<Camera>();
            cam.tag = "MainCamera";
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            void Grab(string name)
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            }
            Time.captureFramerate = 25; // fixed game time per frame: the film plays at real speed
            const int fps = 25;
            const float seconds = 26f;
            int frames = (int)(fps * seconds), n = Flight.Length;
            int fedStage = 0;
            for (int i = 0; i < frames; i++)
            {
                float u = i / (float)(frames - 1) * (n - 1);
                int k = Mathf.Min((int)u, n - 2);
                float t = Mathf.SmoothStep(0f, 1f, u - k) * 0.35f + (u - k) * 0.65f;
                var a = Flight[Mathf.Max(0, k - 1)]; var b = Flight[k]; var c = Flight[k + 1]; var d = Flight[Mathf.Min(n - 1, k + 2)];
                cam.fieldOfView = 62f;
                cam.transform.position = CatmullRom(a.eye, b.eye, c.eye, d.eye, t);
                cam.transform.LookAt(CatmullRom(a.at, b.at, c.at, d.at, t));
                var aq = LobbyProp.Of(LobbyPropKind.Aquarium);
                if (actor != null && aq != null)
                {
                    // the actor steps to the left end of the tank and feeds the fish while the camera comes close
                    Vector3 side = new Vector3(aq.Aim.x - 1.0f, 0.02f, aq.Aim.z + 0.35f);
                    if (fedStage == 0 && k >= 9) { fedStage = 1; actor.TeleportRpc(side, Quaternion.LookRotation(new Vector3(aq.Aim.x - side.x, 0f, aq.Aim.z - side.z)).eulerAngles.y); }
                    if (fedStage == 1 && k >= 10 && u - k > 0.3f) { fedStage = 2; mm.ServerUseProp(actor, (byte)LobbyPropKind.Aquarium); }
                    if (fedStage == 2 && k >= 12) { fedStage = 3; actor.TeleportRpc(park, 90f); }
                }
                yield return new WaitForEndOfFrame(); // the camera (and the mirror for it) rendered this frame
                Grab($"fly_{i:0000}");
                yield return null;
            }
            Log($"flight {frames} frames");
            if (actor != null)
            {
                // darts: the actor throws three, seen from behind on the right (no text at the board)
                actor.TeleportRpc(LobbyDarts.ThrowSpot(LobbyDarts.ThrowLine), LobbyDarts.ThrowYaw);
                yield return new WaitForSeconds(1.5f);
                cam.transform.position = new Vector3(LobbyDarts.ThrowLine.x - 1.2f, 1.85f, LobbyDarts.ThrowLine.z - 1.3f);
                cam.transform.LookAt(Vector3.Lerp(LobbyDarts.ThrowLine + Vector3.up * 1.4f, LobbyDarts.BoardCentre, 0.6f));
                cam.fieldOfView = 55f;
                for (int i = 0; i < fps * 6; i++)
                {
                    if (i == fps / 2 || i == fps * 2 || i == fps * 7 / 2) mm.ServerUseProp(actor, (byte)LobbyPropKind.Darts);
                    yield return new WaitForEndOfFrame();
                    Grab($"darts_{i:0000}");
                    yield return null;
                }
            }
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            Destroy(cam.gameObject);
            rt.Release();
            Destroy(rt);
            Destroy(tex);
            if (playerCam != null) playerCam.tag = playerTag;
        }

        // ================================================================ mini interactions

        private IEnumerator UseProps(MatchManager mm)
        {
            var me = NetPlayer.Local;
            // with a second player (a client) it is the actor and the host watches its body; alone, the host acts
            NetPlayer actor = me;
            for (float w = 0f; w < 8f && actor == me; w += 0.25f)
            {
                foreach (var p in NetPlayer.All) if (p != me) actor = p;
                if (actor == me) yield return new WaitForSeconds(0.25f);
            }
            Log($"actor {(actor == me ? "host" : "client")}");
            if (actor != me) yield return new WaitForSeconds(5f); // the client is placed at a lobby spawn once its scene is ready
            foreach (LobbyPropKind k in System.Enum.GetValues(typeof(LobbyPropKind)))
            {
                var prop = LobbyProp.Of(k);
                if (prop == null) { Log($"{k}: MISSING"); continue; }
                Vector3 aim = prop.Aim;
                Vector3 n = k == LobbyPropKind.Globe ? (new Vector3(0f, 0f, 0f) - aim) : prop.transform.forward;
                n.y = 0f;
                n.Normalize();
                Vector3 stand = k == LobbyPropKind.Darts ? LobbyDarts.ThrowSpot(LobbyDarts.ThrowLine) : new Vector3(aim.x, 0.02f, aim.z) + n * 0.62f;
                Vector3 look = aim - (stand + Vector3.up * 1.62f);
                float yaw = Quaternion.LookRotation(new Vector3(look.x, 0f, look.z)).eulerAngles.y;
                if (actor == me) me.Motor.Teleport(stand, yaw);
                else actor.TeleportRpc(stand, yaw);
                // watcher: beside and behind the actor, looking at the hand / object
                Vector3 right = Vector3.Cross(Vector3.up, -n).normalized;
                Vector3 cam = stand + n * 1.9f + right * 1.3f + Vector3.up * 1.55f;
                if (actor != me)
                {
                    Vector3 watch = new Vector3(cam.x, 0.02f, cam.z);
                    Vector3 wl = Vector3.Lerp(stand + Vector3.up * 1.2f, aim, 0.6f) - (watch + Vector3.up * 1.62f);
                    me.Motor.Teleport(watch, Quaternion.LookRotation(new Vector3(wl.x, 0f, wl.z)).eulerAngles.y);
                    me.Motor.SetLook(Quaternion.LookRotation(new Vector3(wl.x, 0f, wl.z)).eulerAngles.y, Mathf.Clamp(Mathf.Atan2(wl.y, new Vector2(wl.x, wl.z).magnitude) * Mathf.Rad2Deg, -60f, 60f));
                }
                else me.Motor.SetLook(yaw, Mathf.Clamp(Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg, -60f, 60f));
                yield return new WaitForSeconds(0.8f);
                int before = mm.PropBits.Value;
                int shots = k == LobbyPropKind.Darts ? 3 : 1;
                for (int s = 0; s < shots; s++)
                {
                    if (s > 0) yield return new WaitForSeconds(1.3f);
                    mm.ServerUseProp(actor, (byte)k);
                    float dur = HideAndSeek.Characters.BlockyCharacterActions.ReachDuration(prop.Style);
                    float[] at = { 0.3f, 0.5f, 0.75f };
                    float t = 0f;
                    for (int f = 0; f < at.Length; f++)
                    {
                        yield return new WaitForSeconds(at[f] * dur - t);
                        t = at[f] * dur;
                        if (s == 0 || f == 1) Shot($"prop_{k}_{s}_{f}");
                    }
                }
                yield return new WaitForSeconds(k == LobbyPropKind.Aquarium ? 3.5f : 1.6f);
                Shot($"prop_{k}_after");
                Log($"{k}: bits {before} -> {mm.PropBits.Value} on={LobbyPropDriver.On(k)}");
                if (k == LobbyPropKind.LightSwitch)
                {
                    Render("react_lights_off", new Vector3(7.4f, 2.7f, -5.4f), new Vector3(-3f, 0.6f, 2.5f), 72f);
                    mm.ServerUseProp(actor, (byte)k); // back on
                    yield return new WaitForSeconds(1f);
                }
            }
            // aquarium film: 60 frames at 10 fps from the front, the actor feeds the fish at frame 10
            var aqua = LobbyProp.Of(LobbyPropKind.Aquarium);
            if (aqua != null)
            {
                Vector3 aim = aqua.Aim, side = new Vector3(aim.x + 0.75f, 0.02f, aim.z + 0.55f);
                Vector3 al = aim - (side + Vector3.up * 1.62f);
                float ay = Quaternion.LookRotation(new Vector3(al.x, 0f, al.z)).eulerAngles.y;
                if (actor == me) me.Motor.Teleport(side, ay); else actor.TeleportRpc(side, ay);
                yield return new WaitForSeconds(2.5f);
                Vector3 tank = aqua.transform.position + Vector3.up * 0.25f;
                for (int i = 0; i < 60; i++)
                {
                    if (i == 10) mm.ServerUseProp(actor, (byte)LobbyPropKind.Aquarium);
                    Render($"aqua_{i:000}", tank + new Vector3(0f, 0.04f, 0.95f), tank, 46f);
                    yield return new WaitForSeconds(0.1f);
                }
            }
            Render("react_darts", new Vector3(6.6f, 1.75f, 3.0f), LobbyLayout.Dartboard, 40f);
            Render("react_aquarium", new Vector3(-2.75f, 1.1f, -4.9f), new Vector3(-2.75f, 1.05f, -6f), 55f);
            Render("react_record", new Vector3(-4.3f, 1.25f, -5.0f), new Vector3(-4.5f, 0.65f, -5.75f), 50f);
        }

        // ================================================================ two arcades at once

        private IEnumerator ArcadeTwo(MatchManager mm)
        {
            var me = NetPlayer.Local;
            NetPlayer other = null;
            for (float w = 0f; w < 30f && other == null; w += 0.25f)
            {
                foreach (var p in NetPlayer.All) if (p != me) other = p;
                yield return new WaitForSeconds(0.25f);
            }
            if (other == null) { Debug.Log("[Arcade2] FAIL no second player"); yield break; }
            yield return new WaitForSeconds(5f); // the client finishes loading and gets placed at a lobby spawn first
            me.Motor.Teleport(ArcadeCabs.Stand(0), ArcadeCabs.Yaw(0));
            for (int i = 0; i < 5 && Vector3.Distance(other.transform.position, ArcadeCabs.Stand(1)) > 0.5f; i++)
            {
                other.TeleportRpc(ArcadeCabs.Stand(1), ArcadeCabs.Yaw(1));
                yield return new WaitForSeconds(1.2f);
            }
            Debug.Log($"[Arcade2] client at {other.transform.position}, stand 1 {ArcadeCabs.Stand(1)}");
            mm.ServerArcade(me, true, 0);
            mm.ServerArcade(other, true, 1);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[Arcade2] players cab0={mm.ArcadePlayerOf(0)} cab1={mm.ArcadePlayerOf(1)} me={me.NetworkObjectId} other={other.NetworkObjectId}");
            bool dual = mm.ArcadePlayerOf(0) == me.NetworkObjectId && mm.ArcadePlayerOf(1) == other.NetworkObjectId;
            // try to steal the busy cabinet: must fail
            mm.ServerArcade(other, true, 0);
            Debug.Log($"[Arcade2] steal attempt -> cab0 still {mm.ArcadePlayerOf(0)}");
            mm.ServerArcadeDir(me, 1);
            mm.ServerArcadeDir(other, 0);
            float t0 = Time.time;
            bool shot = false;
            while (Time.time - t0 < 14f)
            {
                for (int c = 0; c < 2; c++)
                {
                    var g = ArcadeState.Cab[c];
                    var pl = c == 0 ? me : other;
                    if (g.State == ArcadeState.Over && Time.time - g.StateSince > 1.2f) mm.ServerArcadeDir(pl, 1);
                    else if (g.State == ArcadeState.Playing && g.Body.Count > 1)
                    {
                        int d = SnakePilot.Next(g.Body, g.Food, g.HasBonus, g.Bonus);
                        if (d >= 0) mm.ServerArcadeDir(pl, (byte)d);
                    }
                }
                if (!shot && Time.time - t0 > 6f)
                {
                    shot = true;
                    Render("arcade2_behind", new Vector3(4.8f, 1.95f, 3.2f), new Vector3(4.8f, 1.2f, 5.6f), 55f);
                    Render("arcade2_screens", new Vector3(4.8f, 1.5f, 4.0f), new Vector3(4.8f, 1.35f, 5.2f), 50f);
                    Shot("arcade2_host_fp");
                }
                yield return null;
            }
            var a = ArcadeState.Cab[0];
            var b = ArcadeState.Cab[1];
            Debug.Log($"[Arcade2] cab0 state={a.State} score={a.Score} len={a.Body.Count} head={(a.Body.Count > 0 ? a.Body[0].ToString() : "-")} | " +
                      $"cab1 state={b.State} score={b.Score} len={b.Body.Count} head={(b.Body.Count > 0 ? b.Body[0].ToString() : "-")} best={ArcadeState.Best}");
            bool independent = a.Body.Count > 0 && b.Body.Count > 0 && (a.Body[0] != b.Body[0] || a.Score != b.Score);
            mm.ServerArcade(me, false);
            mm.ServerArcade(other, false);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[Arcade2] stopped cab0={mm.ArcadePlayerOf(0)} cab1={mm.ArcadePlayerOf(1)} live0={ArcadeState.Cab[0].Live} live1={ArcadeState.Cab[1].Live}");
            Debug.Log($"[Arcade2] {(dual && independent ? "PASS" : "FAIL")} dual={dual} independent={independent}");
        }
    }
}
