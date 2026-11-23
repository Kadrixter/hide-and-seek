using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Seeker knocked out by a thrown leg (<see cref="StunTimeline"/>): <see cref="StunAt"/> = server time of the hit
    /// (0 = never this round). For <see cref="StunTimeline.Duration"/> he is Busy (no moving, no attacks, no items: the
    /// usual Busy checks), every client shows the fall / lying / getting up from the same clock (late joiners too), the
    /// owner gets the black screen and the view from the floor (NetPlayer.StunView). After it he is immune for
    /// <see cref="StunTimeline.Immunity"/> s (no extra state: derived from StunAt).
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<double> StunAt = new();

        private bool _stunBusy;     // server: Busy was set by the stun
        private float _stunPrevT = -1f;

        /// <summary>Seconds since the knock-out (&lt; 0 = not stunned now).</summary>
        public float StunTime
        {
            get
            {
                double at = StunAt.Value;
                if (at <= 0d || Life.Value != LifeState.Alive) return -1f;
                double t = ServerNow - at;
                if (t < -0.5d || t >= StunTimeline.Duration) return -1f;
                return Mathf.Max(0f, (float)t);
            }
        }

        public bool Stunned => StunTime >= 0f;

        /// <summary>Seconds left until a leg can knock him out again (0 = now).</summary>
        public float StunImmuneLeft =>
            StunAt.Value > 0d ? Mathf.Max(0f, (float)(StunAt.Value + StunTimeline.Duration + StunTimeline.Immunity - ServerNow)) : 0f;

        /// <summary>Server: starts the knock-out (the caller checked that he can be stunned now).</summary>
        public void ServerStartStun()
        {
            if (!IsServer) return;
            StunAt.Value = ServerNow;
            if (!Busy.Value)
            {
                Busy.Value = true;
                _stunBusy = true;
            }
            Debug.Log($"[Stun] {Nick} knocked out at {ServerNow:0.00}");
        }

        /// <summary>Server, every frame: gives the controls back on time; outside a round nothing is left over.</summary>
        private void ServerTickStun(bool round)
        {
            if (!round && StunAt.Value != 0d) StunAt.Value = 0d;
            if (_stunBusy && (!Stunned || !round))
            {
                _stunBusy = false;
                Busy.Value = false;
                Debug.Log($"[Stun] {Nick} back on the feet");
            }
        }

        /// <summary>Every client: the body; sounds of the fall for everybody around; the owner's view.</summary>
        private void UpdateStun()
        {
            float st = StunTime;
            var bca = Actions as BlockyCharacterActions;
            bca?.SetStun(st, Drunkenness.SeedFor(NetworkObjectId) + 17f);
            if (_stunPrevT >= 0f && st >= 0f)
            {
                if (_stunPrevT < StunTimeline.Thud && st >= StunTimeline.Thud) GameAudio.PlayAt(GameSfx.BodyFall, transform.position, 1f);
                if (_stunPrevT < StunTimeline.Stir && st >= StunTimeline.Stir)
                {
                    var g = GameAudio.PlayAttached(GameSfx.HiderPain, transform, 0.5f);
                    if (g != null) g.pitch = 0.72f; // a groan coming round
                }
                if (_stunPrevT < StunTimeline.ShakeStart && st >= StunTimeline.ShakeStart)
                    GameAudio.PlayAttached(GameSfx.HiderBreathPanic, transform, 0.35f);
            }
            if (IsOwner) UpdateStunView(st);
            _stunPrevT = st;
        }
    }
}
