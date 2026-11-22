using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local player sitting in the lobby: the own body is drawn (only the head, which the camera sits in, stays
    /// hidden; the floating first-person hands are hidden instead) and the camera is put exactly in the eyes of the
    /// seated model. Because the seated body is anchored to the seat and sitting down starts from where the player
    /// stood, following the model's eyes also makes sitting down / standing up one continuous movement; the view
    /// turns round smoothly (<see cref="HeadBob.BeginTurnFrom"/>). Runs after the character animator (100) and before
    /// the shoulder lamp (150) and the view model (200).
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class SeatedView : MonoBehaviour
    {
        private NetPlayer _p;
        private HeadBob _bob;
        private Transform _pivot;

        /// <summary>Camera eye in world space this frame (valid while <see cref="Active"/>).</summary>
        public Vector3 Eye { get; private set; }
        public bool Active { get; private set; }

        private void Awake() => _p = GetComponent<NetPlayer>();

        private bool Resolve()
        {
            if (_bob == null)
            {
                _bob = GetComponentInChildren<HeadBob>();
                _pivot = _bob != null ? _bob.transform : null;
            }
            return _bob != null && _p != null && _p.Character != null && _p.Character.Head != null;
        }

        /// <summary>Sitting down: start the camera where it was and turn the view over <paramref name="duration"/>.</summary>
        public void BeginTurn(Quaternion fromRotation, Vector3 fromPosition, float duration)
        {
            if (!Resolve()) return;
            _bob.BeginTurnFrom(fromRotation, duration);
            // This frame's head bob (before the character is posed) already starts at the old spot.
            _bob.EyeAnchorWorld = fromPosition;
            _bob.EyeAnchorWeight = 1f;
        }

        private void LateUpdate()
        {
            if (_p == null || !_p.IsSpawned || !_p.IsOwner || !Resolve()) return;
            var act = _p.Actions as BlockyCharacterActions;
            bool seated = _p.Seat.Value >= 0;
            float w = act != null ? act.SitWeight : 0f;
            bool active = act != null && _p.Life.Value == LifeState.Alive && (seated || w > 0.002f);
            Active = active;
            _p.SetSeatBodyShown(active);
            if (!active)
            {
                _bob.EyeAnchorWeight = 0f;
                return;
            }

            // Rotation: the regular look (seat yaw + head yaw + pitch, already clamped to the pose's limits and
            // turned smoothly by the head bob). Position: the model's eyes at that look.
            Quaternion look = _pivot.rotation;
            Vector3 offset = Vector3.Lerp(SeatPoses.StandEyeOffset, SeatPoses.EyeOffset, w);
            Vector3 eye = _p.Character.Head.position + look * offset;
            // Standing up: hand back to the regular camera over the last part of the blend.
            float k = seated ? 1f : Mathf.Clamp01(w * 3f);
            Eye = eye;
            _pivot.position = Vector3.Lerp(_pivot.position, eye, k);
            _bob.EyeAnchorWorld = eye;
            _bob.EyeAnchorWeight = k;
        }
    }
}
