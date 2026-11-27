using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using HideAndSeek.Player;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Track go-karts on the local player (<see cref="LobbyKart"/>): the "E  wsiądź do gokarta" prompt at a free kart,
    /// and while driving: W / S (arrows) gas and brake / reverse, A / D steer, Space handbrake, R back to the parking
    /// spot, E get out (pad: <see cref="KeyOrPad.Drive"/>, Cross handbrake, Square out, Triangle reset); no walking, looking or other actions meanwhile (the chase camera has the mouse).
    /// </summary>
    public partial class LocalPlayerController
    {
        // ---- test hooks (scripted tests drive through the same code as the keyboard)
        public static bool TestKartE, TestKartActive, TestKartHandbrake;
        public static float TestKartThrottle, TestKartSteer;

        private float _kartNextRequest;
        private int _kartPrompt = -1;
        private bool _kartPromptPassenger;

        /// <summary>FindPrompt: the nearest free kart in front of the player.</summary>
        private bool TryKartPrompt(Vector3 eye, Vector3 fwd, MatchPhase phase, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = float.MaxValue;
            _kartPrompt = -1;
            _kartPromptPassenger = false;
            var mm = MatchManager.Instance;
            if (phase != MatchPhase.Lobby || mm == null || _p.Motor.IsProne || mm.KartRefusal(_p) != null) return false;
            var all = mm.KartsVar.Value;
            for (int i = 0; i < LobbyKart.All.Length; i++)
            {
                var kart = LobbyKart.All[i];
                if (kart == null || kart.Parts == null) continue;
                var st = all.Get(i);
                // Driver's seat: a parked kart nobody drives.
                if (!st.Driven && kart.View == LobbyKart.Mode.Parked)
                {
                    Vector3 seat = kart.Parts.Seat.position + Vector3.up * 0.2f;
                    float d = Vector3.Distance(eye, seat);
                    if (d <= 2.6f && d < dist && InView(eye, fwd, seat, 45f, kart.transform))
                    {
                        dist = d;
                        pos = seat + Vector3.up * 0.25f;
                        _kartPrompt = i;
                        _kartPromptPassenger = false;
                    }
                }
                // Two-seater's right seat: free, the kart (nearly) standing - driven or not.
                if (kart.Parts.Duo && !st.Carries && Mathf.Abs(kart.ShownSpeed) < 1.5f)
                {
                    Vector3 seat = kart.Parts.PassengerSeat.position + Vector3.up * 0.2f;
                    float d = Vector3.Distance(eye, seat);
                    if (d <= 2.6f && d < dist && InView(eye, fwd, seat, 45f, kart.transform))
                    {
                        dist = d;
                        pos = seat + Vector3.up * 0.25f;
                        _kartPrompt = i;
                        _kartPromptPassenger = true;
                    }
                }
            }
            if (_kartPrompt < 0) return false;
            var l = KartLivery.Of(_kartPrompt);
            text = _kartPromptPassenger ? $"E  wsiądź jako pasażer ({l.Name}, nr {l.Number})"
                : DriftTrackLayout.IsDuo(_kartPrompt) ? $"E  wsiądź za kierownicę ({l.Name}, nr {l.Number})"
                : $"E  wsiądź do gokarta ({l.Name}, nr {l.Number})";
            return true;
        }

        private void KartPromptE()
        {
            if (_kartPrompt < 0 || Time.time < _kartNextRequest) return;
            _kartNextRequest = Time.time + 0.6f;
            _ignoreEThisFrame = true;
            if (_kartPromptPassenger) MatchManager.Instance?.KartPassengerRpc((byte)_kartPrompt, true);
            else MatchManager.Instance?.KartRequestRpc((byte)_kartPrompt, true, transform.position, transform.eulerAngles.y);
        }

        /// <summary>Update: true while the local player drives the kart (everything else is skipped).</summary>
        private bool KartTick(Keyboard kb)
        {
            // Riding as a two-seater's passenger: no walking or looking (the kart camera has the mouse), E gets out.
            var ride = LobbyKart.LocalPassengerKart;
            if (ride != null)
            {
                _p.Motor.MovementEnabled = false;
                _p.Motor.LookEnabled = false;
                var pcc = _p.GetComponent<CharacterController>();
                if (pcc != null && pcc.enabled) pcc.enabled = false;
                if (KeyOrPad.Down(Key.E) && !_ignoreEThisFrame)
                {
                    _ignoreEThisFrame = true;
                    ride.RequestPassengerExit();
                }
                return true;
            }
            var kart = LobbyKart.LocalKart;
            if (kart == null) return false;
            _p.Motor.MovementEnabled = false;
            _p.Motor.LookEnabled = false;
            var cc = _p.GetComponent<CharacterController>();
            if (cc != null && cc.enabled) cc.enabled = false;

            // Pad: R2 gas, L2 brake / reverse (analog), left stick steers, Cross handbrake, Square out, Triangle reset.
            KeyOrPad.Drive(out float throttle, out float steer);
            bool handbrake = KeyOrPad.Held(Key.Space);
            if (TestKartActive)
            {
                throttle = TestKartThrottle;
                steer = TestKartSteer;
                handbrake = TestKartHandbrake;
            }
            LobbyKart.SetInput(throttle, steer, handbrake);

            if ((KeyOrPad.Down(Key.E) && !_ignoreEThisFrame) || TestKartE)
            {
                TestKartE = false;
                _ignoreEThisFrame = true;
                kart.RequestExit();
            }
            else if (KeyOrPad.Down(Key.R) && Time.time >= _kartNextRequest)
            {
                _kartNextRequest = Time.time + 1f;
                kart.RequestReset("R");
            }
            return true;
        }
    }
}
