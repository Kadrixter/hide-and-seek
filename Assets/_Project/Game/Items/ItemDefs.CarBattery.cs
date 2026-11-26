using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    public static partial class ItemDefs
    {
        /// <summary>
        /// A 12 V car battery taken from under a bonnet (MatchManager.CarBatteries). Hiders only, one at a time, heavy
        /// (<see cref="BatteryTuning.CarrySpeedScale"/>). Its charge travels with it (<see cref="NetPlayer.CarBattery"/>,
        /// the drop's Extra = charge in per cent): a dead one looks and reads exactly the same - you only find out when
        /// it is connected to the camera power in a security room (E at the tray next to the desk).
        /// </summary>
        private static ItemDef CarBatteryDef() => new ItemDef
        {
            Item = HotbarItem.CarBattery, Name = "Akumulator", Users = ItemUsers.Hider,
            DropSfx = GameSfx.BodyFall, DropVolume = 0.45f, Glow = new Color(0.95f, 0.75f, 0.2f, 0.4f),
            Count = p => p.CarBattery.Value > 0 ? 1 : 0,
            Give = (p, extra) =>
            {
                if (p.CarBattery.Value > 0) return false;
                p.CarBattery.Value = (byte)(1 + Mathf.Min(100, (int)extra));
                return true;
            },
            Take = (NetPlayer p, out byte extra) =>
            {
                extra = 0;
                if (p.CarBattery.Value == 0) return false;
                extra = (byte)p.CarBatteryCharge; // the dropped battery keeps its charge
                p.CarBattery.Value = 0;
                return true;
            },
            Hint = _ => "Akumulator - podłącz go do zasilania kamer w pokoju ochrony (E przy skrzynce obok biurka)",
            Model = r => CarBatteryModel.Create(r).gameObject,
            Hand = CarBatteryGrip,
        };

        /// <summary>Carried by the strap at the side like a heavy bag; first person: low at the right, the terminals up.</summary>
        private static readonly HandGrip CarBatteryGrip = new()
        {
            Grip = CarBatteryModel.StrapPoint,
            Hang = true,
            FpGrip = new Vector3(-0.03f, 0.02f, 0f),
            FpHand = new Vector3(0.33f, -0.4f, 0.66f),
            FpRotation = new Vector3(8f, -28f, 4f),
            FpScale = 0.55f,
        };
    }
}
