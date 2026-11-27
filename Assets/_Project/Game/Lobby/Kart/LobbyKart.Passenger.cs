using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Passenger of a two-seater go-kart (karts <see cref="DriftTrackLayout.DuoFrom"/>+, 28.11): the seated pose on
    /// every client (<see cref="KartRider"/> on the right seat, hands on the grab bar) and, on the passenger's own client,
    /// riding along: controller off, own body shown, the kart camera from the passenger seat (V = chase camera), the
    /// player root kept under the seat, E gets out beside the kart. The kart drives the same as the single-seaters - the
    /// passenger only rides; the server keeps who sits where (<see cref="KartState.Passenger"/>).
    /// </summary>
    public partial class LobbyKart
    {
        private KartRider _pasRider;
        private NetPlayer _pasRiderPlayer, _localPassenger;
        private KartCamera _pasCam;
        private float _pasLeftUntil;

        public bool IsDuo => DriftTrackLayout.IsDuo(Index);

        /// <summary>The kart the local player rides in as the passenger (null = none).</summary>
        public static LobbyKart LocalPassengerKart
        {
            get
            {
                foreach (var k in All) if (k != null && k._localPassenger != null) return k;
                return null;
            }
        }

        /// <summary>Update: the passenger's pose and the local passenger's ride (after the kart moved this frame).</summary>
        private void TickPassenger(KartState st)
        {
            var pas = IsDuo && st.Carries ? NetPlayer.Find(st.Passenger) : null;
            if (pas != null && !pas.IsSpawned) pas = null;

            if (!(pas == _pasRiderPlayer && (_pasRider != null || pas == null)))
            {
                if (_pasRider != null) _pasRider.Release();
                _pasRider = null;
                _pasRiderPlayer = pas;
                if (pas != null && pas.Character != null)
                {
                    _pasRider = pas.Character.gameObject.AddComponent<KartRider>();
                    _pasRider.Init(this, pas, true);
                }
            }

            bool mine = pas != null && pas.IsOwner && Time.time >= _pasLeftUntil;
            if (mine && _localPassenger == null) BeginPassenger(pas);
            else if (!mine && _localPassenger != null) EndPassenger(true);
        }

        private void BeginPassenger(NetPlayer me)
        {
            _localPassenger = me;
            var cc = me.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                if (_col != null) Physics.IgnoreCollision(cc, _col, true);
            }
            me.Motor.Seated = true;
            me.SetCinematicBody(true);
            _pasCam = KartCamera.Begin(me, this, true);
            GameHud.Toast("Jedziesz jako pasażer :D   V - kamera, E - wysiądź", 4f);
            Debug.Log($"[Kart] {me.Nick} rides in kart {Index} as the passenger");
        }

        private void EndPassenger(bool placeOutside)
        {
            var me = _localPassenger;
            _localPassenger = null;
            if (_pasCam != null) _pasCam.End();
            _pasCam = null;
            if (me == null) return;
            me.Motor.Seated = false;
            me.SetCinematicBody(false);
            var cc = me.GetComponent<CharacterController>();
            bool free = me.Life.Value == LifeState.Alive && !me.Busy.Value;
            if (placeOutside && free && MatchManager.Instance != null && MatchManager.Instance.Phase.Value == MatchPhase.Lobby)
            {
                Vector3 exit = FindExit(true);
                me.TeleportLocal(exit, _yaw);
                Debug.Log($"[Kart] passenger {me.Nick} gets out of kart {Index} at {exit}");
            }
            if (cc != null)
            {
                if (_col != null) Physics.IgnoreCollision(cc, _col, false);
                if (free) cc.enabled = true;
            }
        }

        /// <summary>E as the passenger: get out (the server frees the seat).</summary>
        public void RequestPassengerExit()
        {
            if (_localPassenger == null) return;
            _pasLeftUntil = Time.time + 1.5f;
            MatchManager.Instance?.KartPassengerRpc((byte)Index, false);
            EndPassenger(true);
        }

        /// <summary>LateUpdate: the local passenger's root rides under the right seat.</summary>
        private void PassengerLateUpdate()
        {
            var me = _localPassenger;
            if (me == null) return;
            var cc = me.GetComponent<CharacterController>();
            if (cc != null && cc.enabled) cc.enabled = false;
            Vector3 seat = transform.TransformPoint(new Vector3(KartModel.DuoPassengerX, 0f, KartModel.SeatPos.z));
            me.transform.SetPositionAndRotation(seat, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        }

        private void PassengerDestroyed()
        {
            if (_localPassenger != null) EndPassenger(false);
            if (_pasRider != null) _pasRider.Release();
        }
    }
}
