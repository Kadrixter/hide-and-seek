using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// E next to an RC car that carries an item (MatchManager.RcCargo): "E  zabierz apteczkę z samochodzika". Anybody
    /// allowed to have the item; it comes before picking the car itself up (E again then takes the car).
    /// </summary>
    public partial class LocalPlayerController
    {
        private int _rcCargoPrompt = -1;

        private bool TryRcCargoPrompt(Vector3 eye, Vector3 fwd, MatchPhase phase, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = float.MaxValue;
            var mm = MatchManager.Instance;
            if (mm == null || mm.RaceMode || (phase != MatchPhase.Hiding && phase != MatchPhase.Seeking)) return false;
            if (_p.Life.Value != LifeState.Alive || _p.Busy.Value || _p.Seat.Value >= 0) return false;
            for (int i = 0; i < RcCar.Count; i++)
            {
                var item = mm.RcCargoOf(i);
                var car = RcCar.Peek(i);
                if (item == HotbarItem.None || car == null || car.View == RcCar.ViewMode.Hidden || car.View == RcCar.ViewMode.Shelf) continue;
                if (!Inventory.CanPickUp(_p, item, phase)) continue;
                Vector3 aim = car.transform.position + Vector3.up * 0.16f;
                float d = Vector3.Distance(eye, aim);
                if (d > RcCargo.TakeRange || d >= dist || !InView(eye, fwd, aim, 45f)) continue;
                dist = d;
                pos = aim + Vector3.up * 0.12f;
                _rcCargoPrompt = i;
                string name = ItemDefs.Name(item);
                text = Inventory.PickupText(_p, item, $"E  zabierz z samochodzika: {name}");
            }
            if (_rcCargoPrompt < 0 || text == null) return false;
            dist -= 0.3f; // before "E  weź samochodzik RC" on the same car
            return true;
        }
    }
}
