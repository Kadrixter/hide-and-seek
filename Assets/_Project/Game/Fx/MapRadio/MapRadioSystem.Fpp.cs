using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// First person: the carrier sees the real radio (beside his head, its end at the right edge of the view) and his
    /// view-model hands on it (<see cref="FirstPersonViewModel.SetShoulderRadio"/>), fed from the third-person layer's
    /// hand targets so lifting / carrying / putting down match the body everybody else sees.
    /// </summary>
    public partial class MapRadioSystem
    {
        private bool _fppOn;

        private void UpdateFpp()
        {
            var me = NetPlayer.Local;
            var vm = me != null ? me.ViewModel as FirstPersonViewModel : null;
            if (vm == null) return;
            bool on = false;
            if (me.Actions is BlockyCharacterActions bca && bca.ShoulderRadioHands(out var fist, out var left, out float leftW, out float ride))
            {
                int i = MatchManager.Instance != null ? MatchManager.Instance.MapRadioOf(me) : -1;
                var box = ViewOf(i);
                float beat = box != null && box.MusicAudible && box.Output != null ? box.Output.Kick : 0f;
                vm.SetShoulderRadio(true, fist, left, leftW, ride, beat);
                on = true;
            }
            if (!on && _fppOn) vm.SetShoulderRadio(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f);
            _fppOn = on;
        }

        private void DestroyFpp()
        {
            var vm = NetPlayer.Local != null ? NetPlayer.Local.ViewModel as FirstPersonViewModel : null;
            if (_fppOn && vm != null) vm.SetShoulderRadio(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f);
        }
    }
}
