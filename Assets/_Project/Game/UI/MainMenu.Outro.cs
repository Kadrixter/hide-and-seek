using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Game outro: after "Wyjdź" is confirmed the menu fades away, the menu character puts the weapon away, turns to
    /// the camera (which moves in a little) and waves goodbye energetically for ~2 s, then the screen goes black and
    /// the game quits. "Wyjdź z gry" from the Esc menu first leaves the session and plays it in the menu scene.
    /// </summary>
    public partial class MainMenu
    {
        /// <summary>Set before leaving a session with "Wyjdź z gry": the menu scene plays the outro right away.</summary>
        public static bool OutroOnStart;
        public static bool OutroPlaying { get; private set; }

        private const float OutroTurn = 0.6f, OutroRaise = 0.45f, OutroWave = 2.0f, OutroFade = 0.4f;

        /// <summary>Quit button (after the confirm dialog): outro in the menu, or leave the session first.</summary>
        public static void QuitWithOutro()
        {
            if (OutroPlaying) return;
            var nm = NetworkManager.Singleton;
            if (_instance != null && _instance.isActiveAndEnabled && (nm == null || !nm.IsListening))
            {
                _instance.StartCoroutine(_instance.Outro(true));
                return;
            }
            if (nm != null && nm.IsListening && NetSession.Instance != null)
            {
                OutroOnStart = true;
                NetSession.Instance.Leave(); // loads the menu scene, MainMenu.Start plays the outro
                return;
            }
            Application.Quit();
        }

        private IEnumerator Outro(bool quit)
        {
            OutroPlaying = true;
            Debug.Log("[Outro] start");
            // stop the weapon show-off and put the weapon away
            if (_cycle != null) StopCoroutine(_cycle);
            StopLoop(0.2f);
            var a = _preview != null ? _preview.Actions : null;
            if (a != null) { a.SetRevving(false); a.SetHeldItem(HeldItem.None); }

            // UI fades out (and stays out)
            var group = _canvas.GetComponent<CanvasGroup>();
            if (group == null) group = _canvas.gameObject.AddComponent<CanvasGroup>();
            Destroy(_canvas.GetComponent<HideUnderDialog>()); // it would fade the menu back in
            group.blocksRaycasts = false;

            var cam = Camera.main;
            Vector3 camFrom = cam != null ? cam.transform.position : Vector3.zero;
            float fovFrom = cam != null ? cam.fieldOfView : 60f;
            Vector3 target = previewRoot != null ? previewRoot.position + Vector3.up * 1.05f : Vector3.zero;
            Vector3 camTo = cam != null ? Vector3.Lerp(camFrom, target, 0.22f) : Vector3.zero;
            float yawFrom = previewRoot != null ? previewRoot.eulerAngles.y : 0f;
            float yawTo = yawFrom;
            if (cam != null && previewRoot != null)
            {
                var toCam = cam.transform.position - previewRoot.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.01f) yawTo = Quaternion.LookRotation(toCam).eulerAngles.y;
            }
            var wave = _preview != null ? _preview.gameObject.AddComponent<MenuOutroWave>() : null;
            if (wave != null) wave.Character = _preview;

            var fade = BuildFade();
            float total = OutroTurn + OutroRaise + OutroWave;
            float t = 0f;
            while (t < total + OutroFade)
            {
                t += Time.captureFramerate > 0 ? Time.deltaTime : Time.unscaledDeltaTime; // fixed steps when recording
                group.alpha = Mathf.Clamp01(1f - t / 0.3f);
                // turn to the camera (keeps the yaw for Update's sway: _previewYaw)
                float k = Smooth(t / OutroTurn);
                _previewYaw = Mathf.LerpAngle(yawFrom, yawTo, k);
                // camera dolly in over the whole outro
                if (cam != null)
                {
                    float d = Smooth(t / (total + OutroFade));
                    cam.transform.position = Vector3.Lerp(camFrom, camTo, d);
                    cam.fieldOfView = Mathf.Lerp(fovFrom, fovFrom * 0.9f, d);
                }
                // wave: raise (ease), loop, a short hold at the end
                if (wave != null)
                {
                    float tw = t - OutroTurn * 0.6f;
                    wave.Time = Mathf.Max(0f, tw);
                    wave.Weight = tw <= 0f ? 0f : Smooth(tw / OutroRaise);
                }
                // black at the end
                fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01((t - total) / OutroFade));
                if (_outroHook != null) _outroHook(t);
                yield return null;
            }
            fade.color = Color.black;
            Debug.Log("[Outro] done");
            if (quit && !NoQuit) Application.Quit();
            // tests (-noquit): back to the menu
            yield return new WaitForSecondsRealtime(0.5f);
            if (wave != null) Destroy(wave);
            if (cam != null) { cam.transform.position = camFrom; cam.fieldOfView = fovFrom; }
            _previewYaw = yawFrom;
            Destroy(fade.canvas.gameObject);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            _canvas.gameObject.AddComponent<HideUnderDialog>();
            OutroPlaying = false;
            SetWeapon(SavedWeapon, false);
        }

        /// <summary>Tests: called every outro frame with the time (screenshots).</summary>
        private System.Action<float> _outroHook;

        private static bool NoQuit => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-noquit") >= 0;

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static Image BuildFade()
        {
            var c = UiKit.CreateCanvas("OutroFade", 900);
            var img = UiKit.Image("Black", c.transform, new Color(0f, 0f, 0f, 0f));
            img.rectTransform.Stretch();
            img.raycastTarget = true;
            return img;
        }
    }
}
