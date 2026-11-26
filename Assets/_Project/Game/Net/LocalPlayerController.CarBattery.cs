using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;
using HideAndSeek.Player;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local input of the car batteries (MatchManager.CarBatteries / CamPower): E at the nose of a prepared car opens
    /// the bonnet latch with a piece of wire (the lockpick mini-game, <see cref="HideAndSeek.Game.UI.LockpickUi"/>) or lifts
    /// an unlatched bonnet, E at an open bonnet takes the battery, E at a security room's power tray connects / takes out
    /// a battery, E at the power switch turns the room's cameras on / off. While the bonnet is being lifted E stops it.
    /// </summary>
    public partial class LocalPlayerController
    {
        private int _hoodPrompt = -1;
        private int _camRoom = -1;

        /// <summary>Before the other actions: true while the player lifts a bonnet (E lets go; walking away drops it).</summary>
        private bool CarBatteryTick(UnityEngine.InputSystem.Keyboard kb)
        {
            var w = _p.HoodWork.Value;
            if (!w.Active) return false;
            if (w.Mode == HoodWorkMode.Opening)
            {
                var h = MatchManager.HoodAt(w.Hood);
                if (h != null)
                {
                    Prompt = PromptKind.CarHood;
                    PromptWorld = h.FrontWorld + Vector3.up * 0.25f;
                    PromptText = "E  puść maskę";
                }
                if (kb != null && KeyOrPad.Down(UnityEngine.InputSystem.Key.E) && !_ignoreEThisFrame) _p.HoodStopRpc();
            }
            return true;
        }

        /// <summary>FindPrompt: a bonnet in front of the player (closed: open it; open: take the battery).</summary>
        private bool TryHoodPrompt(Vector3 eye, Vector3 fwd, MatchPhase phase, out PromptKind kind, out Vector3 pos, out string text, out float dist)
        {
            kind = PromptKind.None;
            pos = default;
            text = null;
            dist = float.MaxValue;
            var mm = MatchManager.Instance;
            if (mm == null || (phase != MatchPhase.Hiding && phase != MatchPhase.Seeking) || mm.RaceMode || !_p.IsAliveHider) return false;
            if (_p.Busy.Value || _p.Seat.Value >= 0 || _p.RcAnimating || _p.WirePull.Value.Spot >= 0 || _p.Motor.IsProne) return false;
            var bits = mm.CarHoods.Value;
            var all = CarHood.All;
            Vector3 feet = transform.position;
            for (int k = 0; k < all.Count; k++)
            {
                var h = all[k];
                if (h == null) continue;
                int i = h.Index;
                if (!HoodBits.Has(bits.Active, i) || Mathf.Abs(feet.y - h.transform.position.y) > 1.3f) continue;
                if (HoodBits.Has(bits.Open, i))
                {
                    if (HoodBits.Has(bits.Taken, i)) continue;
                    Vector3 bp = h.BatteryWorld;
                    float d = Vector3.Distance(eye, bp);
                    if (d > 2.1f || d >= dist || !InView(eye, fwd, bp, 35f, h.transform)) continue;
                    dist = d;
                    pos = bp + Vector3.up * 0.1f;
                    kind = PromptKind.CarBatteryTake;
                    _hoodPrompt = i;
                    text = _p.HasCarBattery ? "E  (masz już akumulator)" : Inventory.PickupText(_p, HotbarItem.CarBattery, "E  wyjmij akumulator");
                }
                else
                {
                    Vector3 front = h.FrontWorld;
                    Vector3 nose = h.transform.forward;
                    nose.y = 0f;
                    Vector3 rel = feet - front;
                    rel.y = 0f;
                    if (Vector3.Dot(rel, nose.normalized) < -0.15f) continue; // standing beside / behind the car
                    float d = Vector3.Distance(eye, front);
                    if (d > 2.2f || d >= dist || !InView(eye, fwd, front, 40f, h.transform)) continue;
                    dist = d;
                    pos = front + Vector3.up * 0.15f;
                    kind = PromptKind.CarHood;
                    _hoodPrompt = i;
                    var worker = mm.HoodWorker(i);
                    text = worker != null && worker != _p ? $"{worker.Nick} grzebie przy masce"
                        : HoodBits.Has(bits.Unlatched, i) ? "E  podnieś maskę"
                        : _p.WireCount.Value == 0 ? "E  otwórz maskę (potrzebny kawałek drutu)"
                        : "E  otwórz zamek maski drutem";
                }
            }
            return kind != PromptKind.None;
        }

        /// <summary>FindPrompt: the camera power switch / battery tray of a security room.</summary>
        private bool TryCamPowerPrompt(Vector3 eye, Vector3 fwd, MatchPhase phase, out PromptKind kind, out Vector3 pos, out string text, out float dist)
        {
            kind = PromptKind.None;
            text = null;
            pos = default;
            dist = float.MaxValue;
            var mm = MatchManager.Instance;
            var sr = SecurityRooms.Instance;
            if (mm == null || sr == null || (phase != MatchPhase.Hiding && phase != MatchPhase.Seeking) || mm.RaceMode) return false;
            if (_p.Life.Value != LifeState.Alive || _p.Escaped.Value || _p.Busy.Value) return false;
            if (!sr.TryFindPower(eye, fwd, out bool isSwitch, out int room, out pos, out dist)) return false;
            _camRoom = room;
            var s = mm.CamPower.Value;
            if (isSwitch)
            {
                kind = PromptKind.CamSwitch;
                text = !s.IsOn(room) ? "E  włącz kamery"
                    : s.Powered(room) ? $"E  wyłącz kamery (akumulator {s.Percent(room)}%)"
                    : "E  wyłącz kamery (brak zasilania)";
                return true;
            }
            if (!_p.IsAliveHider) return false;
            kind = PromptKind.CamBattery;
            text = s.HasBattery(room) ? (_p.HasCarBattery ? "Akumulator już podłączony" : Inventory.PickupText(_p, HotbarItem.CarBattery, $"E  odłącz akumulator ({s.Percent(room)}%)"))
                : _p.HasCarBattery ? "E  podłącz akumulator"
                : "Tu podłącza się akumulator (znajdziesz go pod maską auta)";
            return true;
        }

        private void HoodPromptE()
        {
            var mm = MatchManager.Instance;
            if (mm == null || _hoodPrompt < 0) return;
            var worker = mm.HoodWorker(_hoodPrompt);
            if (worker != null && worker != _p)
            {
                GameHud.Toast("Ktoś już grzebie przy tej masce.", 2f);
                return;
            }
            if (!HoodBits.Has(mm.CarHoods.Value.Unlatched, _hoodPrompt) && _p.WireCount.Value == 0)
            {
                GameHud.Toast("Potrzebujesz kawałka drutu - urwij go z siatki ogrodzenia albo klatki.", 3f);
                return;
            }
            _ignoreEThisFrame = true;
            _p.RequestHoodRpc((byte)_hoodPrompt);
        }

        private void CamBatteryPromptE()
        {
            var mm = MatchManager.Instance;
            if (mm == null || _camRoom < 0) return;
            var s = mm.CamPower.Value;
            if (!s.HasBattery(_camRoom) && !_p.HasCarBattery)
            {
                GameHud.Toast("Nie masz akumulatora. Poszukaj aut z otwieraną maską.", 2.5f);
                return;
            }
            if (s.HasBattery(_camRoom) && _p.HasCarBattery)
            {
                GameHud.Toast("Masz już akumulator - najpierw podłącz albo odłóż swój.", 2.5f);
                return;
            }
            _p.RequestCamBatteryRpc((byte)_camRoom);
        }
    }
}
