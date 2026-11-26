using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testpad &lt;dir&gt; (host; batchmode has no real devices): a virtual <see cref="Gamepad"/> driven with queued state
    /// events plays the lobby with the pad alone - walk (left stick), look (right stick), jump (Cross), sprint (L3),
    /// Options opens the Esc menu and Circle closes it, rebind Jump to Triangle on the "Przyciski pada" page (capture,
    /// swap, saved in prefs, works), get into a kart with Square, drive with R2 + left stick, get out; then, when a round
    /// starts (-autostart with a client), D-pad right / left step through the hotbar. Frames via RenderTexture.
    /// Logs: [PadTest] ... PASS / FAIL, then "[PadTest] RESULT: ...".
    /// </summary>
    public partial class NetSession
    {
        private static void PLog(string s) => Debug.Log("[PadTest] " + s);

        /// <summary>A test still needs the lobby: -autostart waits (NetSession.AutoStart).</summary>
        public static bool HoldAutoStart;

        private Gamepad _testPad;
        private GamepadState _padState;
        private int _padPass, _padFail;

        private void PadCheck(string what, bool ok, string detail)
        {
            if (ok) _padPass++; else _padFail++;
            PLog($"{what}: {detail} -> {(ok ? "PASS" : "FAIL")}");
        }

        private void PadPush() => InputSystem.QueueStateEvent(_testPad, _padState);

        private void PadButtonSet(GamepadButton b, bool down)
        {
            _padState = _padState.WithButton(b, down);
            PadPush();
        }

        private IEnumerator PadTap(GamepadButton b, float hold = 0.12f)
        {
            PadButtonSet(b, true);
            yield return new WaitForSecondsRealtime(hold);
            PadButtonSet(b, false);
            yield return null;
            yield return null;
        }

        private IEnumerator PadSticks(Vector2 left, Vector2 right, float seconds)
        {
            _padState.leftStick = left;
            _padState.rightStick = right;
            float t = 0f;
            while (t < seconds) { PadPush(); t += Time.unscaledDeltaTime; yield return null; }
            _padState.leftStick = Vector2.zero;
            _padState.rightStick = Vector2.zero;
            PadPush();
            yield return null;
        }

        /// <summary>Renders the camera that shows the game now (kart chase camera or the player's) into a PNG.</summary>
        private static void PadShot(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return;
            Camera cam = null;
            foreach (var c in Camera.allCameras)
                if (c != null && c.enabled && c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
            if (cam == null) { PLog($"shot {name}: no camera"); return; }
            System.IO.Directory.CreateDirectory(dir);
            var rt = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            rt.Release();
            Destroy(rt);
            Destroy(tex);
            PLog($"shot {name} ({cam.name})");
        }

        private IEnumerator PadTest(string dir)
        {
            HoldAutoStart = true; // the lobby steps first; -autostart starts the round after them
            _testPad = InputSystem.AddDevice<Gamepad>("PadTestGamepad");
            _testPad.MakeCurrent();
            PadInput.ForcedPad = _testPad;
            _padState = new GamepadState();
            PadPush();
            PLog($"virtual pad added: {_testPad.name}, keyboard={Keyboard.current?.name ?? "-"}, mouse={Mouse.current?.name ?? "-"}");

            // ---- lobby
            float t0 = Time.time;
            while (Time.time - t0 < 60f && (MatchManager.Instance == null || NetPlayer.Local == null || !NetPlayer.Local.IsSpawned ||
                                            MatchManager.Instance.Phase.Value != MatchPhase.Lobby || LocalPlayerController.Instance == null)) yield return null;
            yield return new WaitForSeconds(4f);
            var me = NetPlayer.Local;
            if (me == null) { PLog("RESULT: FAIL (no local player)"); Application.Quit(); yield break; }
            PadCheck("pad seen", PadInput.Connected && PadInput.Pad == _testPad, $"connected={PadInput.Connected} name='{PadInput.Name}' kind={PadInput.Kind}");
            yield return PadTap(GamepadButton.South, 0.05f); // any press: the pad becomes the last used device
            yield return new WaitForSeconds(0.3f);
            PadCheck("using pad", PadInput.UsingPad, $"UsingPad={PadInput.UsingPad} inputEnabled={me.InputReader.InputEnabled}");
            PadShot(dir, "pad_00_lobby");

            // Walk: left stick forward 1.2 s.
            Vector3 p0 = me.transform.position;
            yield return PadSticks(new Vector2(0f, 1f), Vector2.zero, 1.2f);
            float walk = Vector3.Distance(Flat(p0), Flat(me.transform.position));
            PadCheck("walk (left stick)", walk > 1.2f, $"moved {walk:0.00} m" + PadCtx(me));

            // Look: right stick right 0.6 s.
            float yaw0 = me.Motor.LookYaw;
            yield return PadSticks(Vector2.zero, new Vector2(1f, 0f), 0.6f);
            float turned = Mathf.DeltaAngle(yaw0, me.Motor.LookYaw);
            PadCheck("look (right stick)", turned > 30f, $"yaw {turned:0.0} deg" + PadCtx(me));

            // Jump: Cross.
            yield return new WaitForSeconds(0.4f);
            bool left = false;
            PadButtonSet(GamepadButton.South, true);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { if (!me.Motor.IsGrounded) left = true; yield return null; }
            PadButtonSet(GamepadButton.South, false);
            PadCheck("jump (Cross)", left, $"left the ground={left}" + PadCtx(me));
            yield return new WaitForSeconds(0.8f);

            // Sprint: L3 click, then forward; compare with walking.
            p0 = me.transform.position;
            yield return PadSticks(new Vector2(0f, 1f), Vector2.zero, 1f);
            float walk1 = Vector3.Distance(Flat(p0), Flat(me.transform.position));
            yield return new WaitForSeconds(0.3f);
            yield return PadTap(GamepadButton.LeftStick);
            p0 = me.transform.position;
            yield return PadSticks(new Vector2(0f, 1f), Vector2.zero, 1f);
            float run1 = Vector3.Distance(Flat(p0), Flat(me.transform.position));
            PadCheck("sprint (L3)", run1 > walk1 * 1.2f, $"walk {walk1:0.00} m/s, run {run1:0.00} m/s" + PadCtx(me));

            // Pause menu: Options opens, Circle closes.
            yield return PadTap(GamepadButton.Start);
            yield return new WaitForSeconds(0.6f);
            bool opened = LocalPlayerController.MenuOpen;
            yield return PadTap(GamepadButton.East);
            yield return new WaitForSeconds(0.6f);
            PadCheck("menu (Options / Circle)", opened && !LocalPlayerController.MenuOpen, $"opened={opened} closedAfterCircle={!LocalPlayerController.MenuOpen}");
            if (LocalPlayerController.MenuOpen) GameHud.TestCloseMenus();

            // Rebind Jump -> Triangle on the "Przyciski pada" page.
            GameHud.TestOpenPadPage(true);
            yield return new WaitForSeconds(0.6f);
            PLog($"menu page: {GameHud.TestMenuName}");
            var reloadBefore = PadInput.GetBinding(PadAction.Reload);
            PadBindRow.Click(PadAction.Jump);
            yield return new WaitForSeconds(0.3f);
            bool capturing = PadInput.Capturing == PadAction.Jump;
            yield return PadTap(GamepadButton.North);
            yield return new WaitForSeconds(0.3f);
            var jb = PadInput.GetBinding(PadAction.Jump);
            var rb = PadInput.GetBinding(PadAction.Reload);
            PadCheck("rebind capture", capturing && jb.Button == PadButton.Triangle && !jb.L1 && rb.Button == PadButton.Cross,
                $"capturing={capturing} Jump={jb} Reload={rb} (was {reloadBefore}) msg='{PadInput.LastCaptureMessage}'");
            Prefs.Save();
            PadInput.ReloadSettings();
            string saved = Prefs.GetString(PadInput.PrefPrefix + "bind.Jump", "");
            PadCheck("rebind persists", saved == "Triangle" && PadInput.GetBinding(PadAction.Jump).Button == PadButton.Triangle, $"pref='{saved}' after reload={PadInput.GetBinding(PadAction.Jump)}");
            GameHud.TestCloseMenus();
            yield return new WaitForSeconds(0.8f);
            left = false;
            PadButtonSet(GamepadButton.North, true);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { if (!me.Motor.IsGrounded) left = true; yield return null; }
            PadButtonSet(GamepadButton.North, false);
            PadCheck("rebound jump (Triangle)", left, $"left the ground={left}" + PadCtx(me));
            PadInput.ResetBindings();
            Prefs.Save();
            yield return new WaitForSeconds(0.8f);

            // Kart: Square at the prompt, R2 + left stick drive, Square out.
            yield return PadKartTest(me, dir);

            // ---- round (needs -autostart and a second player)
            HoldAutoStart = false;
            PLog($"lobby part done (phase {MatchManager.Instance?.Phase.Value}), round may start now");
            t0 = Time.time;
            while (Time.time - t0 < 150f && !(MatchManager.Instance != null && Hotbar.Active(NetPlayer.Local))) yield return null;
            me = NetPlayer.Local;
            if (me != null && Hotbar.Active(me))
            {
                yield return new WaitForSeconds(3f);
                PadShot(dir, "pad_20_round");
                int s0 = Hotbar.Selected(me);
                yield return PadTap(GamepadButton.DpadRight);
                yield return new WaitForSeconds(0.3f);
                int s1 = Hotbar.Selected(me);
                yield return PadTap(GamepadButton.DpadLeft);
                yield return new WaitForSeconds(0.3f);
                int s2 = Hotbar.Selected(me);
                PadCheck("hotbar (D-pad right / left)", s1 == (s0 + 1) % Hotbar.Slots && s2 == s0, $"slots {s0} -> {s1} -> {s2} (seeker={me.IsSeeker}; a seeker holding the saw may refuse)");
                p0 = me.transform.position;
                yield return PadSticks(new Vector2(0f, 1f), new Vector2(0.5f, 0f), 1f);
                PadCheck("round walk + look", Vector3.Distance(Flat(p0), Flat(me.transform.position)) > 0.5f || !me.InputReader.InputEnabled,
                    $"moved {Vector3.Distance(Flat(p0), Flat(me.transform.position)):0.00} m inputEnabled={me.InputReader.InputEnabled}");
                PadShot(dir, "pad_21_round_after");
            }
            else PLog("round: not started (no -autostart / second player) - SKIP");

            PLog($"RESULT: {(_padFail == 0 ? "PASS" : "FAIL")} ({_padPass} pass, {_padFail} fail)");
            PadInput.ForcedPad = null;
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        /// <summary>State for a FAIL line: phase, input / motor locks, menu.</summary>
        private static string PadCtx(NetPlayer me) =>
            $" [phase {MatchManager.Instance?.Phase.Value} input={me.InputReader.InputEnabled} move={me.Motor.MovementEnabled} look={me.Motor.LookEnabled} menu={LocalPlayerController.MenuOpen} busy={me.Busy.Value}]";

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private IEnumerator PadKartTest(NetPlayer me, string dir)
        {
            float t0 = Time.time;
            // First parked kart that is set up (indices follow LobbyKart.All; the track karts may come first).
            LobbyKart kart = null;
            int ki = -1;
            while (Time.time - t0 < 25f && kart == null)
            {
                var all = MatchManager.Instance != null ? MatchManager.Instance.KartsVar.Value : default;
                for (int i = 0; i < LobbyKart.All.Length && kart == null; i++)
                {
                    var k = LobbyKart.All[i];
                    if (k != null && k.Parts != null && k.View == LobbyKart.Mode.Parked && !all.Get(i).Driven) { kart = k; ki = i; }
                }
                if (kart == null) yield return null;
            }
            if (kart == null) { PLog($"kart: none parked in the lobby ({LobbyKart.All.Length} slots) - FAIL"); _padFail++; yield break; }
            PLog($"kart {ki} at {kart.transform.position}");
            Vector3 stand = kart.transform.TransformPoint(new Vector3(-1.2f, 0f, -0.2f)) + Vector3.up * 0.02f;
            Vector3 to = kart.Parts.Seat.position - (stand + Vector3.up * 1.62f);
            me.TeleportLocal(stand, YawTo(to));
            me.Motor.SetLook(YawTo(to), PitchTo(to));
            yield return new WaitForSeconds(1f);
            var lpc = LocalPlayerController.Instance;
            string shown = PadHints.Prompt(lpc.PromptText);
            PadCheck("kart prompt (pad label)", lpc.Prompt == PromptKind.Kart && shown != null && shown.StartsWith(PadInput.Label(PadAction.Interact)),
                $"{lpc.Prompt} '{lpc.PromptText}' shown as '{shown}'");
            PadShot(dir, "pad_10_kart_prompt");
            yield return PadTap(GamepadButton.West); // Square = E
            yield return new WaitForSeconds(1.8f);
            var mm = MatchManager.Instance;
            bool inKart = mm.KartsVar.Value.Get(ki).Driver == me.NetworkObjectId && LobbyKart.LocalKart == kart;
            PadCheck("kart in (Square)", inKart, $"driver={mm.KartsVar.Value.Get(ki).Driver} me={me.NetworkObjectId}");
            if (!inKart) yield break;
            Vector3 k0 = kart.transform.position;
            _padState.rightTrigger = 1f;
            yield return PadSticks(new Vector2(0.4f, 0f), Vector2.zero, 2.5f);
            _padState.rightTrigger = 0f;
            PadPush();
            float moved = Vector3.Distance(Flat(k0), Flat(kart.transform.position));
            PadShot(dir, "pad_11_kart_drive");
            PadCheck("kart drive (R2 + left stick)", moved > 3f, $"kart moved {moved:0.0} m");
            yield return new WaitForSeconds(1.5f); // stop before getting out
            yield return PadTap(GamepadButton.West);
            yield return new WaitForSeconds(1.8f);
            bool outOk = mm.KartsVar.Value.KartOf(me.NetworkObjectId) < 0;
            PadCheck("kart out (Square)", outOk, $"kartOf={mm.KartsVar.Value.KartOf(me.NetworkObjectId)}");
            if (!outOk) { kart.RequestExit(); yield return new WaitForSeconds(1.5f); }
        }
    }
}
