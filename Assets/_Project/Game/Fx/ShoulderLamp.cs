using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Blocky flashlight strapped to the right shoulder (Dying Light style). The real spot light is positioned
    /// at the same place relative to the camera (see Flashlight.HoldOffset); this is the visible model, whose
    /// lens glows while the light is on. In the lobby the lamp can be hidden (still in the box) or carried in the
    /// left hand while it is taken out of / put back into the box (<see cref="SetCarry"/>).
    /// </summary>
    [DefaultExecutionOrder(150)] // after BlockyCharacterAnimator (100): the hand bones are posed for this frame
    public class ShoulderLamp : MonoBehaviour
    {
        /// <summary>Flashlight offset from the eye that matches the model on the shoulder.</summary>
        public static readonly Vector3 EyeOffset = FlashTakeTimeline.LampEyeOffset;

        private Renderer _lens;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private bool _on, _visible = true;
        private Transform _torso, _hand;
        private Vector3 _restPos;
        private Quaternion _restRot;
        private float _toShoulder = 1f;

        public static ShoulderLamp Attach(BlockyCharacter character)
        {
            if (character == null || character.Torso == null) return null;
            var root = new GameObject("ShoulderLamp").transform;
            root.SetParent(character.Torso, false);
            // Torso pivot is at the hips; the box is x +-4 px, y 0..12 px, z +-2 px (+z = front, +x = right).
            root.localPosition = FlashTakeTimeline.LampTorsoLocal;
            root.localRotation = Quaternion.identity;
            var lamp = root.gameObject.AddComponent<ShoulderLamp>();
            lamp._torso = character.Torso;
            lamp._restPos = root.localPosition;
            lamp._restRot = root.localRotation;
            lamp._lens = LampModel.Build(root, true, character.gameObject.layer);
            lamp._renderers = root.GetComponentsInChildren<Renderer>(true);
            lamp._mpb = new MaterialPropertyBlock();
            lamp.SetOn(false);
            root.gameObject.layer = character.gameObject.layer;
            foreach (Transform t in root) t.gameObject.layer = character.gameObject.layer;
            return lamp;
        }

        public void SetOn(bool on)
        {
            if (_lens == null || (_on == on && _mpb != null && Time.frameCount > 2)) return;
            _on = on;
            _lens.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", on ? new Color(1f, 0.95f, 0.8f) * 3f : Color.black);
            _lens.SetPropertyBlock(_mpb);
        }

        /// <summary>Local player: keep the model out of the first-person view (shadow only).</summary>
        public void SetFirstPerson(bool fp)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = fp ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>Shows / hides the whole lamp with its strap (lobby: still in the box).</summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible || _renderers == null) return;
            _visible = visible;
            foreach (var r in _renderers) if (r != null) r.enabled = visible;
        }

        /// <summary>
        /// Carried in the fist of <paramref name="elbow"/> (the left forearm), <paramref name="toShoulder"/> 0..1 slides
        /// it onto its place on the shoulder; elbow null = back on the shoulder.
        /// </summary>
        public void SetCarry(Transform elbow, float toShoulder)
        {
            if (elbow == null && _hand != null)
            {
                transform.localPosition = _restPos;
                transform.localRotation = _restRot;
            }
            _hand = elbow;
            _toShoulder = toShoulder;
        }

        private void LateUpdate()
        {
            if (_hand == null || _torso == null) return;
            Vector3 hp = _hand.TransformPoint(FlashTakeTimeline.LampInHandLocal);
            Quaternion hr = _hand.rotation * FlashTakeTimeline.LampInHandRot;
            Vector3 sp = _torso.TransformPoint(_restPos);
            Quaternion sr = _torso.rotation * _restRot;
            transform.SetPositionAndRotation(Vector3.Lerp(hp, sp, _toShoulder), Quaternion.Slerp(hr, sr, _toShoulder));
        }
    }
}
