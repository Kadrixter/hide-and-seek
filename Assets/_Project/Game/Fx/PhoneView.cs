using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Toy phone lying on the floor: the pink model with a faint pink glint (so hiders can spot it in the dark). While
    /// it rings it buzzes in bursts - jitters, hops a few millimetres, twitches round - the screen blinks and the
    /// glint pulses brighter.
    /// </summary>
    public class PhoneView : MonoBehaviour
    {
        public bool Ringing;

        private PhoneModel.Parts _parts;
        private Light _glint;
        private Vector3 _pos;
        private float _yaw, _phase, _ringT;

        public static PhoneView Create(Vector3 pos, float yaw)
        {
            var root = new GameObject("ToyPhoneOnFloor");
            var v = root.AddComponent<PhoneView>();
            v._parts = PhoneModel.Create(root.transform, 0);
            var lg = new GameObject("Glint");
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            v._glint = lg.AddComponent<Light>();
            v._glint.type = LightType.Point;
            v._glint.color = new Color(1f, 0.45f, 0.75f);
            v._glint.range = 1.3f;
            v._glint.intensity = 0.3f;
            v._glint.shadows = LightShadows.None;
            v._phase = Random.Range(0f, 10f);
            v.Place(pos, yaw);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return v;
        }

        public void Place(Vector3 pos, float yaw)
        {
            _pos = pos;
            _yaw = yaw;
        }

        private void Update()
        {
            float t = Time.time + _phase;
            if (!Ringing)
            {
                _ringT = 0f;
                transform.SetPositionAndRotation(_pos, Quaternion.Euler(0f, _yaw, 0f));
                PhoneModel.SetScreen(_parts, false);
                float k = 0.5f + 0.5f * Mathf.Sin(t * 1.4f);
                _glint.intensity = 0.2f + 0.2f * k * k;
                return;
            }
            _ringT += Time.deltaTime;
            // Buzz pattern: 0.45 s on, 0.3 s off.
            bool buzz = Mathf.Repeat(_ringT, 0.75f) < 0.45f;
            Vector3 off = Vector3.zero;
            float twitch = 0f, tilt = 0f;
            if (buzz)
            {
                off = new Vector3(Mathf.Sin(t * 91f), 0f, Mathf.Sin(t * 77f + 1f)) * 0.004f;
                off.y = 0.012f * Mathf.Abs(Mathf.Sin(t * 47f));
                twitch = 3.5f * Mathf.Sin(t * 63f);
                tilt = 2.5f * Mathf.Sin(t * 55f);
            }
            transform.SetPositionAndRotation(_pos + off, Quaternion.Euler(tilt, _yaw + twitch, tilt * 0.6f));
            PhoneModel.SetScreen(_parts, Mathf.Repeat(_ringT * 3f, 1f) < 0.55f);
            _glint.intensity = buzz ? 0.9f + 0.4f * Mathf.Sin(t * 20f) : 0.5f;
        }
    }
}
