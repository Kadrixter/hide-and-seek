using HideAndSeek.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using HideAndSeek.Player;

namespace HideAndSeek.Game.Net
{
    /// <summary>R while steering the RC car: energise it (server decides, MatchManager.RcPower.cs).</summary>
    public partial class LocalPlayerController
    {
        public static bool TestRcCharge;
        private float _rcChargeNextTry;

        private void RcChargeKey(Keyboard kb, int slot)
        {
            bool r = (kb != null && KeyOrPad.Down(Key.R)) || TestRcCharge;
            TestRcCharge = false;
            if (!r || Time.time < _rcChargeNextTry) return;
            _rcChargeNextTry = Time.time + 0.3f;
            var mm = MatchManager.Instance;
            if (mm == null) return;
            if (mm.RcCharged(slot)) return;
            float cd = mm.RcChargeCooldown(slot);
            if (cd > 0f)
            {
                GameHud.Toast($"Akumulator się ładuje - jeszcze {Mathf.CeilToInt(cd)} s.", 1.5f);
                GameAudio.Play2D(GameSfx.FlashlightAdjust, 0.35f);
                return;
            }
            _p.RequestRcChargeRpc((byte)slot);
        }
    }
}
