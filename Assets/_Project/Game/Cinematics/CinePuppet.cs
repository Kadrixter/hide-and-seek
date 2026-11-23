using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// A stand-in figure for the cinematics: the same blocky character and skin as a player (built locally on every
    /// client, nothing on the network), walking where it is told with the normal locomotion, ticked manually so a
    /// full-body pose (<see cref="Pose"/>) can be blended on top after the action layer (emotes, held items).
    /// </summary>
    public sealed class CinePuppet : MonoBehaviour, ICharacterMotionSource
    {
        public BlockyCharacter Body { get; private set; }
        public BlockyCharacterActions Actions { get; private set; }
        public string Nick { get; private set; }
        public ulong PlayerId { get; private set; } = ulong.MaxValue;

        /// <summary>Full-body override: gets the current pose (character root space) and the time, returns the target.</summary>
        public System.Func<CharacterPose, CharacterPose> Pose;
        /// <summary>Weight of <see cref="Pose"/> (0..1, the caller animates it).</summary>
        public float PoseWeight;

        BlockyCharacterAnimator _anim;
        Vector3 _target, _vel;
        float _speed, _yaw, _turnTo = float.NaN;
        bool _walking;

        public static CinePuppet Create(string name, Transform parent, Vector3 pos, float yaw, int skin, string nick, Texture2D customSkin, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var p = go.AddComponent<CinePuppet>();
            p.Nick = nick;
            p._yaw = yaw;
            p._target = pos;
            p.Body = BlockyCharacterFactory.Create(go.transform, skin, nick);
            p.Body.SetFirstPersonMode(false);
            if (customSkin != null) p.Body.SetCustomSkin(customSkin);
            p.Body.SetLayer(layer);
            p.Actions = p.Body.Actions;
            p._anim = p.Body.GetComponent<BlockyCharacterAnimator>();
            p._anim.autoUpdate = false;
            p._anim.SetMotionSource(p);
            p._anim.randomSeed = Mathf.Abs((nick ?? name).GetHashCode()) % 100000 + 1;
            return p;
        }

        /// <summary>Puppet of a player (skin, downloaded Minecraft skin, nick). Never shows wounds or cut limbs.</summary>
        public static CinePuppet Of(NetPlayer pl, Transform parent, Vector3 pos, float yaw, int layer)
        {
            var tex = pl.Character != null ? pl.Character.CustomSkin : null;
            var p = Create("Puppet_" + pl.Nick, parent, pos, yaw, pl.SkinVar.Value, pl.Nick, tex, layer);
            p.PlayerId = pl.NetworkObjectId;
            return p;
        }

        // ================================================================ movement

        /// <summary>Walks to <paramref name="to"/> (flat) at <paramref name="speed"/> m/s, then turns to <paramref name="faceYaw"/>.</summary>
        public void WalkTo(Vector3 to, float speed, float faceYaw)
        {
            _target = to;
            _speed = speed;
            _walking = true;
            _turnTo = faceYaw;
        }

        public void Place(Vector3 pos, float yaw)
        {
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            _target = pos;
            _yaw = yaw;
            _walking = false;
            _vel = Vector3.zero;
        }

        public void TurnTo(float yaw, float speed = 150f)
        {
            _turnTo = yaw;
            _turnSpeed = speed;
        }

        float _turnSpeed = 150f;

        public bool Arrived => !_walking;

        /// <summary>Advances movement and animation by <paramref name="dt"/> (call once per frame, in LateUpdate).</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || Body == null) return;
            Vector3 pos = transform.position;
            Vector3 to = _target - pos;
            to.y = 0f;
            float d = to.magnitude;
            Vector3 want = Vector3.zero;
            if (_walking)
            {
                if (d < 0.03f) _walking = false;
                else
                {
                    // Ease in and slow down on arrival.
                    float v = Mathf.Min(_speed, d * 2.2f + 0.15f);
                    want = to / d * v;
                    float face = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    if (d > 0.25f) _yaw = Mathf.MoveTowardsAngle(_yaw, face, 360f * dt);
                }
            }
            _vel = Vector3.Lerp(_vel, want, 1f - Mathf.Exp(-dt * 9f));
            transform.position = pos + _vel * dt;
            if (!_walking && !float.IsNaN(_turnTo)) _yaw = Mathf.MoveTowardsAngle(_yaw, _turnTo, _turnSpeed * dt);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            _anim.Tick(dt);
            if (Pose != null && PoseWeight > 0.001f)
            {
                var cur = CharacterPose.Capture(Body);
                var target = Pose(cur);
                CharacterPose.Lerp(cur, target, Mathf.Clamp01(PoseWeight)).Apply(Body);
            }
        }

        // ================================================================ ICharacterMotionSource

        public Vector3 Velocity => _vel;
        public float HorizontalSpeed => new Vector2(_vel.x, _vel.z).magnitude;
        public bool IsGrounded => true;
        public bool IsCrouching => false;
        public bool IsRunning => false;
        public float CrouchAmount => 0f;
        public float LookPitch { get; set; }
        public LocomotionState State => HorizontalSpeed > 0.1f ? LocomotionState.Walk : LocomotionState.Idle;
    }
}
