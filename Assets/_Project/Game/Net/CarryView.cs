using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public partial class NetPlayer
    {
        /// <summary>Local player: the own body is drawn while carrying somebody / being carried (<see cref="CarryView"/>).</summary>
        public bool CarryBodyShown { get; private set; }

        /// <summary>Own body drawn in first person (seated or carry), only the head stays hidden.</summary>
        public bool OwnBodyShown => SeatBodyShown || CarryBodyShown;

        internal void SetCarryBodyShown(bool on)
        {
            if (CarryBodyShown == on) return;
            CarryBodyShown = on;
            ApplyOwnBodyMode();
        }
    }

    /// <summary>
    /// First person during a body carry (local player, both roles): the own body is drawn (head hidden) and the camera
    /// sits in the model's eyes, like <see cref="SeatedView"/>. The carrier sees his own arms under the body he holds
    /// (and the body in the lower part of the view), the carried looks out of his own head lying in the arms (and
    /// sees his legs, the carrier's face and shoulder). Blends in / out so lifting and putting down stay one movement.
    /// Runs after the seated view (it may switch the eye anchor off) and before the view model.
    /// </summary>
    [DefaultExecutionOrder(121)]
    public class CarryView : MonoBehaviour
    {
        private NetPlayer _p;
        private HeadBob _bob;
        private Transform _pivot;
        private float _w;
        private float _bobIntensity = -1f;

        /// <summary>Eye anchor weight now (tests).</summary>
        public float Weight => _w;

        private void Awake() => _p = GetComponent<NetPlayer>();

        private void LateUpdate()
        {
            if (_p == null || !_p.IsSpawned || !_p.IsOwner || _p.Character == null || _p.Character.Head == null) return;
            if (_bob == null)
            {
                _bob = GetComponentInChildren<HeadBob>();
                _pivot = _bob != null ? _bob.transform : null;
                if (_bob == null) return;
            }
            var act = _p.Actions as BlockyCharacterActions;
            bool on = act != null && _p.Life.Value != LifeState.Dead && (act.IsCarryingBody || act.IsCarriedBody || act.IsSettlingAfterCarry || act.CarryHealWeight > 0.01f);
            float rate = on ? 1f / 0.25f : 1f / 0.4f;
            _w = Mathf.MoveTowards(_w, on ? 1f : 0f, Time.deltaTime * rate);
            _p.SetCarryBodyShown(_w > 0.001f);
            if (_w <= 0.001f)
            {
                if (_bobIntensity >= 0f) { _bob.Intensity = _bobIntensity; _bobIntensity = -1f; }
                return;
            }
            // The model's own head already bobs with every step: the camera bob only adds a little.
            if (_bobIntensity < 0f) _bobIntensity = _bob.Intensity;
            _bob.Intensity = Mathf.Lerp(_bobIntensity, _bobIntensity * 0.35f, _w);

            Quaternion look = _pivot.rotation;
            Vector3 eye = _p.Character.Head.position + look * SeatPoses.EyeOffset;
            float k = ActionTimeline.SmoothStep01(_w);
            _pivot.position = Vector3.Lerp(_pivot.position, eye, k);
            _bob.EyeAnchorWorld = eye;
            _bob.EyeAnchorWeight = k;
        }
    }
}
