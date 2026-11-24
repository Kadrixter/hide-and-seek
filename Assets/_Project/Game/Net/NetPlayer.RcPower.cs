using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Electrocuted by an energised RC car (<see cref="MatchManager.ServerRcZapCheck"/>): <see cref="ZapAt"/> = server
    /// time of the hit. For <see cref="RcPower.ZapTime"/> s the player stands rigid and shakes (the electrocuted pose of
    /// the fork-in-the-socket shock), Busy (no moving, no attacks, no items); every peer shows the arcs over the body and
    /// plays the crack, the owner's view shakes. Driven by server time (late joiners see the same moment).
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<double> ZapAt = new();

        private bool _zapBusy, _zapView;
        private float _zapPrevT = -1f;

        /// <summary>Seconds since the electrocution (&lt; 0 = not now).</summary>
        public float ZapTime
        {
            get
            {
                double at = ZapAt.Value;
                if (at <= 0d || Life.Value != LifeState.Alive) return -1f;
                double t = ServerNow - at;
                if (t < -0.5d || t >= RcPower.ZapTime) return -1f;
                return Mathf.Max(0f, (float)t);
            }
        }

        public bool Zapped => ZapTime >= 0f;

        /// <summary>Owner: R while steering the RC car.</summary>
        [Rpc(SendTo.Server)]
        public void RequestRcChargeRpc(byte slot) => MatchManager.Instance?.ServerRcCharge(this, slot);

        /// <summary>Server: the charged car of slot <paramref name="slot"/> touched this player (checked by the caller).</summary>
        public void ServerStartZap(int slot)
        {
            if (!IsServer) return;
            ZapAt.Value = ServerNow;
            if (!Busy.Value)
            {
                Busy.Value = true;
                _zapBusy = true;
            }
            Debug.Log($"[RcPower] {Nick} electrocuted by car {slot} at {ServerNow:0.00}");
        }

        /// <summary>Every peer, every frame (NetPlayer.Update).</summary>
        private void UpdateRcZap()
        {
            if (IsServer)
            {
                var mm = MatchManager.Instance;
                var ph = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
                bool round = ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended;
                if (!round && ZapAt.Value != 0d) ZapAt.Value = 0d;
                if (_zapBusy && (!Zapped || !round))
                {
                    _zapBusy = false;
                    Busy.Value = false;
                }
            }

            float zt = ZapTime;
            (Actions as BlockyCharacterActions)?.SetZap(zt, RcPower.ZapTime);
            if (zt >= 0f && _zapPrevT < 0f) OnZapStart(zt);
            if (IsOwner) UpdateZapView(zt);
            _zapPrevT = zt;
        }

        private void OnZapStart(float zt)
        {
            if (zt > RcPower.ZapTime - 0.2f) return;
            RcChargeFx.ZapBody(transform, RcPower.ZapTime - zt);
            if (zt < 0.4f)
            {
                // The bolt from the car (the nearest energised one) into the legs, the crack and a scream.
                for (int i = 0; i < RcCar.Count; i++)
                {
                    var car = RcCar.Peek(i);
                    if (car == null || car.View == RcCar.ViewMode.Hidden) continue;
                    if ((car.transform.position - transform.position).sqrMagnitude < 4f)
                    {
                        RcChargeFx.Bolt(car.transform.position + Vector3.up * 0.12f, transform.position + Vector3.up * 0.55f);
                        break;
                    }
                }
                GameAudio.PlayClipAt(RcChargeSounds.Zap, transform.position + Vector3.up * 0.5f, 1f, 30f);
                var scream = GameAudio.PlayAttached(GameSfx.ForkScream, transform, 0.85f);
                if (scream != null) scream.pitch = IsSeeker ? 0.8f : 1f;
            }
        }

        /// <summary>Owner: the view jerks, red flashes (like the fork shock).</summary>
        private void UpdateZapView(float zt)
        {
            if (_bob == null) _bob = GetComponentInChildren<HideAndSeek.Player.HeadBob>();
            if (_bob == null) return;
            if (zt >= 0f)
            {
                float fade = 1f - Mathf.Clamp01((zt - (RcPower.ZapTime - 0.3f)) / 0.3f);
                _bob.ExtraEuler = new Vector3(Random.Range(-1f, 1f) * 5f, Random.Range(-1f, 1f) * 5f, Random.Range(-1f, 1f) * 7f) * fade;
                _bob.ExtraPosition = Random.insideUnitSphere * 0.025f * fade;
                if (Random.value < 0.2f) GameHud.Hurt(0.4f);
                _zapView = true;
            }
            else if (_zapView)
            {
                _zapView = false;
                _bob.ExtraEuler = Vector3.zero;
                _bob.ExtraPosition = Vector3.zero;
            }
        }
    }
}
