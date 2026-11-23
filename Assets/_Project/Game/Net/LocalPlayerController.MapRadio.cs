using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local input of the radios found on the parking. Lying radio (alive hiders): E lifts it onto the shoulder, R opens
    /// its panel (link from YouTube, play / pause, volume). Carrying: Q puts it down, R opens the panel, T still toggles
    /// the flashlight; items, attacks and other prompts wait until it is down (the hand holds the radio).
    /// </summary>
    public partial class LocalPlayerController
    {
        private int _mapRadioPrompt = -1;
        private float _mapRadioNextRequest;

        /// <summary>Test hooks (-testmapradio): the same paths as the keys.</summary>
        public static bool TestMapRadioE, TestMapRadioQ, TestMapRadioR;

        /// <summary>Called before the other actions; true while the radio is on the shoulder (the rest is skipped).</summary>
        private bool MapRadioTick(Keyboard kb)
        {
            var mm = MatchManager.Instance;
            int i = mm != null ? mm.MapRadioOf(_p) : -1;
            if (i < 0) return false;
            bool q = kb.qKey.wasPressedThisFrame || TestMapRadioQ;
            bool r = kb.rKey.wasPressedThisFrame || TestMapRadioR;
            TestMapRadioQ = TestMapRadioR = false;
            if (q && Time.time >= _mapRadioNextRequest)
            {
                _mapRadioNextRequest = Time.time + 0.4f;
                _p.RequestMapRadioPutRpc();
            }
            if (r) OpenMapRadioPanel(i);
            if (kb.tKey.wasPressedThisFrame && _p.Flashlight != null && _p.HasFlashlight) _p.Flashlight.Toggle();
            return true;
        }

        private static void OpenMapRadioPanel(int i)
        {
            var box = MapRadioSystem.Instance != null ? MapRadioSystem.Instance.ViewOf(i) : null;
            if (box != null && MapRadioSystem.LocalMayUse(i)) Boombox.OpenPanel(box);
        }

        /// <summary>Prompt at the nearest lying radio in view (alive hiders, during the round).</summary>
        private bool TryMapRadioPrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = float.MaxValue;
            _mapRadioPrompt = -1;
            var mm = MatchManager.Instance;
            if (mm == null || mm.MapRadios == null || !_p.IsAliveHider || _p.Motor.IsProne) return false;
            for (int i = 0; i < mm.MapRadios.Count; i++)
            {
                var s = mm.MapRadios[i];
                if (s.Mode != MapRadioState.Lying) continue;
                Vector3 c = s.Pos + Vector3.up * 0.16f;
                float d = Vector3.Distance(eye, c);
                Vector3 flat = s.Pos - transform.position;
                if (d >= dist || new Vector2(flat.x, flat.z).magnitude > MatchManager.MapRadioReach || !InView(eye, fwd, c, 40f, MapRadioSystem.Instance?.ViewOf(i)?.transform)) continue;
                _mapRadioPrompt = i;
                pos = c + Vector3.up * 0.3f;
                dist = d;
            }
            if (_mapRadioPrompt < 0) return false;
            text = "E  weź radio na ramię     R  panel radia";
            return true;
        }

        private void MapRadioPromptKeys(Keyboard kb)
        {
            if (_mapRadioPrompt < 0) return;
            if (((kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) || TestMapRadioE) && Time.time >= _mapRadioNextRequest)
            {
                _mapRadioNextRequest = Time.time + 0.4f;
                _ignoreEThisFrame = true;
                _p.RequestMapRadioPickRpc((byte)_mapRadioPrompt);
            }
            if (kb.rKey.wasPressedThisFrame || TestMapRadioR) OpenMapRadioPanel(_mapRadioPrompt);
            TestMapRadioE = TestMapRadioR = false;
        }
    }
}
