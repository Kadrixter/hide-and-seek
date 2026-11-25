using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Aim of a player at the lobby pool table, so everybody sees his cue and stance move while he aims (owner-written,
    /// at most ~15 updates a second and only when something changed; the table smooths it on the other peers).
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>x = angle (radians, table space), y = pull-back 0..1, z / w = spin X / Y (-1..1).</summary>
        public readonly NetworkVariable<Vector4> PoolAim = new(Vector4.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Ball in hand: x / y = cue ball position (table space x / z), z = 0 none, 1 held (follows the mouse), 2 put down.</summary>
        public readonly NetworkVariable<Vector3> PoolHand = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private float _poolAimSentAt = -1f;

        /// <summary>
        /// Owner, every frame while at the pool table: the current aim. Sent when it changed and the last send is
        /// 1/15 s old (<paramref name="now"/> = send at once, e.g. the cue ball was put down or the pull started / ended).
        /// </summary>
        public void SetPoolAim(Vector4 aim, Vector3 hand, bool now)
        {
            if (!IsOwner || !IsSpawned) return;
            Vector4 a0 = PoolAim.Value;
            Vector3 h0 = PoolHand.Value;
            bool aimChanged = Mathf.Abs(Mathf.DeltaAngle(a0.x * Mathf.Rad2Deg, aim.x * Mathf.Rad2Deg)) > 0.02f || Mathf.Abs(a0.y - aim.y) > 0.002f
                              || Mathf.Abs(a0.z - aim.z) > 0.002f || Mathf.Abs(a0.w - aim.w) > 0.002f;
            bool handChanged = Mathf.Abs(h0.z - hand.z) > 0.01f || (hand.z > 0.5f && new Vector2(h0.x - hand.x, h0.y - hand.y).sqrMagnitude > 1e-6f);
            if (!aimChanged && !handChanged) return;
            float t = Time.unscaledTime;
            if (!now && _poolAimSentAt >= 0f && t - _poolAimSentAt < 1f / 15f) return;
            _poolAimSentAt = t;
            if (aimChanged) PoolAim.Value = aim;
            if (handChanged) PoolHand.Value = hand;
        }
    }
}
