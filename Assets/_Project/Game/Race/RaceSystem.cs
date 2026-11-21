using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// RC race mode on every peer: builds the track, gives every player an RC car in an own colour, drives the
    /// local car (chase camera, countdown, checkpoints, laps, respawn after leaving the track), shows the other cars
    /// from their drivers' 20 Hz poses (solid, so cars bump into each other), and draws the race HUD.
    /// The drivers' characters sit on the grandstand with a transmitter (pose set in <see cref="NetPlayer"/>).
    /// Laps are counted by each driver and reported through <see cref="NetPlayer.RaceCps"/>; the server picks the winner.
    /// </summary>
    public class RaceSystem : MonoBehaviour
    {
        public static RaceSystem Instance { get; private set; }

        public static readonly Color[] Paints =
        {
            new Color(0.85f, 0.1f, 0.08f), new Color(0.08f, 0.3f, 0.85f), new Color(1f, 0.8f, 0.05f), new Color(0.1f, 0.7f, 0.2f),
            new Color(0.55f, 0.15f, 0.8f), new Color(1f, 0.45f, 0.05f), new Color(0.1f, 0.8f, 0.85f), new Color(1f, 0.4f, 0.7f),
            new Color(0.92f, 0.92f, 0.92f), new Color(0.12f, 0.12f, 0.13f),
        };
        private static readonly string[] PaintNames = { "czerwony", "niebieski", "żółty", "zielony", "fioletowy", "pomarańczowy", "turkusowy", "różowy", "biały", "czarny" };

        public RaceTrack Track { get; private set; }

        private readonly Dictionary<ulong, RcCar> _cars = new();
        private readonly List<NetPlayer> _order = new();
        private RcCar _mine;
        private Camera _cam;
        private bool _placed, _finished, _bumper;
        private float _cover;
        private Vector3 _camRaw;
        private int _cps, _hint = -1;
        private float _offTrackSince = -1f, _nextSend, _respawnMsgUntil, _lastBump;
        private string _msg = "";
        private float _msgUntil;
        private GUIStyle _big, _mid, _small;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[RaceSystem]");
            Instance = go.AddComponent<RaceSystem>();
            Instance.Build();
        }

        private void Build()
        {
            Track = new RaceTrack();
            Track.Build();
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.38f, 0.44f);
            RenderSettings.ambientIntensity = 1f;
            DynamicGI.UpdateEnvironment();
            Debug.Log($"[Race] track built: {Track.Length:0} m, {Track.Count} samples, {Track.Seats.Count} seats");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreCamera();
            foreach (var c in _cars.Values) if (c != null) Destroy(c.gameObject);
        }

        // ================================================================ order / lookup

        /// <summary>Players in a stable order (by client id): same grid slot, seat and colour on every peer.</summary>
        private void RefreshOrder()
        {
            _order.Clear();
            foreach (var p in NetPlayer.All) if (p != null && p.IsSpawned) _order.Add(p);
            _order.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        }

        public int SlotOf(NetPlayer p) => _order.IndexOf(p);

        public Color PaintOf(NetPlayer p) => Paints[Mathf.Max(0, SlotOf(p)) % Paints.Length];

        public Vector3 CarPos(NetPlayer p) => p != null && _cars.TryGetValue(p.NetworkObjectId, out var c) && c != null ? c.transform.position : RaceTrack.Origin;

        public static Vector3 SeatOf(int slot)
        {
            var t = Instance != null ? Instance.Track : new RaceTrack();
            if (t.Seats.Count == 0) return RaceTrack.Origin + new Vector3(slot * 1.6f, 0.62f, -27f);
            return t.Seats[Mathf.Clamp(slot, 0, t.Seats.Count - 1)];
        }

        public int Laps(NetPlayer p) => Mathf.Max(0, (p.RaceCps.Value - 1) / RaceTrack.Checkpoints);

        /// <summary>Race progress for the standings: checkpoints done + how far into the current section.</summary>
        public float Progress(NetPlayer p)
        {
            float cps = p == NetPlayer.Local ? _cps : p.RaceCps.Value;
            if (!_cars.TryGetValue(p.NetworkObjectId, out var car) || car == null) return cps;
            int s = Track.Nearest(car.transform.position);
            int from = Track.CheckpointSample((int)cps - 1);
            int span = Track.Count / RaceTrack.Checkpoints;
            float into = ((s - from) % Track.Count + Track.Count) % Track.Count;
            return cps + Mathf.Clamp01(into / Mathf.Max(1, span)) * 0.99f;
        }

        // ================================================================ frame

        private void Update()
        {
            var mm = MatchManager.Instance;
            if (mm == null || Track == null) return;
            RefreshOrder();
            var me = NetPlayer.Local;
            double now = mm.Now;
            double start = mm.RaceStartAt.Value;
            bool started = start > 0 && now >= start;
            bool over = mm.Phase.Value == MatchPhase.Ended;

            // Start lights: one red per second, all green at the start.
            if (start > 0)
            {
                double left = start - now;
                Track.SetStartLights(left > 3.0 ? 0 : left > 2.0 ? 1 : left > 1.0 ? 2 : left > 0.0 ? 3 : 4);
            }

            foreach (var p in _order)
            {
                if (!_cars.TryGetValue(p.NetworkObjectId, out var car) || car == null)
                {
                    car = RcCar.CreateRace(SlotOf(p), PaintOf(p));
                    _cars[p.NetworkObjectId] = car;
                }
                Track.GridSlot(SlotOf(p), out var gp, out float gy);
                if (p == me)
                {
                    _mine = car;
                    if (!_placed)
                    {
                        car.EnsureLocal(gp, gy);
                        car.Respawn(gp, gy);
                        car.SetSolid(false);
                        car.Lights = true;
                        _placed = true;
                        _cps = 0;
                        if (p.RaceCps.Value != 0) p.RaceCps.Value = 0;
                    }
                }
                else
                {
                    car.EnsureRemote(p.RcDrive.Value, gp, gy);
                    car.SetSolid(true);
                }
            }
            // Players who left: their cars go.
            var gone = new List<ulong>();
            foreach (var kv in _cars) if (NetPlayer.Find(kv.Key) == null) gone.Add(kv.Key);
            foreach (var id in gone) { if (_cars[id] != null) Destroy(_cars[id].gameObject); _cars.Remove(id); }

            if (me == null || _mine == null) return;
            EnsureCamera(me);
            DriveLocal(me, mm, started && !over && !_finished);
        }

        private void DriveLocal(NetPlayer me, MatchManager mm, bool canDrive)
        {
            var kb = Keyboard.current;
            float dt = Time.deltaTime;
            float throttle = 0f, steer = 0f;
            bool menu = LocalPlayerController.MenuOpen;
            if (kb != null && !menu)
            {
                if (canDrive)
                {
                    throttle = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                    steer = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                }
                if (TestAutopilot && canDrive) Autopilot(out throttle, out steer);
                _mine.Boost = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed || (TestAutopilot && canDrive);
                _mine.Horn = kb.hKey.isPressed;
                if (kb.cKey.wasPressedThisFrame) _bumper = !_bumper;
                if (kb.rKey.wasPressedThisFrame && canDrive) RespawnAtCheckpoint("Powrót na ostatni checkpoint.");
            }
            _mine.Throttle = throttle;
            _mine.SteerInput = steer;
            me.SetRcInput(new Vector2(steer, throttle));
            _mine.TickLocal(dt, 4.2f, 6.4f, _mine.transform.position, me);

            // Car-to-car contact: bump sound and a little shove of the speed.
            foreach (var kv in _cars)
            {
                var other = kv.Value;
                if (other == null || other == _mine) continue;
                Vector3 d = other.transform.position - _mine.transform.position;
                if (d.sqrMagnitude < 0.33f * 0.33f && Time.time - _lastBump > 0.35f)
                {
                    _lastBump = Time.time;
                    _mine.Knock(Mathf.Max(1.2f, Mathf.Abs(_mine.Speed)));
                }
            }

            // Fell off the track: back to the last checkpoint.
            int nearS = Track.Nearest(_mine.transform.position, _hint);
            if (!Track.OnTrack(_mine.transform.position, nearS))
            {
                if (_offTrackSince < 0f) _offTrackSince = Time.time;
                if (Time.time - _offTrackSince > 0.45f) RespawnAtCheckpoint("Wyjechałeś z toru! Powrót do checkpointu.");
            }
            else _offTrackSince = -1f;

            // Checkpoints and laps.
            if (canDrive)
            {
                int s = Track.Nearest(_mine.transform.position, _hint);
                _hint = s;
                int seg = Track.SegmentOf(s);
                if (seg == _cps % RaceTrack.Checkpoints && Track.OnTrack(_mine.transform.position, s))
                {
                    _cps++;
                    me.RaceCps.Value = (ushort)_cps;
                    int laps = Mathf.Max(0, (_cps - 1) / RaceTrack.Checkpoints);
                    int target = Mathf.Max(1, mm.Settings.Value.Laps);
                    if (seg == 0 && _cps > 1)
                    {
                        if (laps >= target)
                        {
                            _finished = true;
                            Say("META!", 4f);
                            GameAudio.Play2D(GameSfx.WinSting, 0.8f);
                        }
                        else
                        {
                            Say(laps == target - 1 ? "OSTATNIE OKRĄŻENIE!" : $"Okrążenie {laps + 1} / {target}", 2.5f);
                            GameAudio.Play2D(GameSfx.LobbyButtonPress, 0.7f);
                        }
                    }
                }
            }

            // Send the pose ~20 times per second.
            if (Time.unscaledTime >= _nextSend)
            {
                _nextSend = Time.unscaledTime + 0.05f;
                var pk = _mine.Pack();
                if (!pk.Equals(me.RcDrive.Value)) me.RcDrive.Value = pk;
            }
        }

        private void RespawnAtCheckpoint(string why)
        {
            Vector3 pos;
            float yaw;
            if (_cps <= 0) Track.GridSlot(SlotOf(NetPlayer.Local), out pos, out yaw);
            else
            {
                int i = Track.CheckpointSample(_cps - 1);
                pos = Track.Pts[i] + Vector3.up * 0.06f;
                yaw = Quaternion.LookRotation(Track.Dirs[i]).eulerAngles.y;
            }
            _mine.Respawn(pos, yaw);
            _offTrackSince = -1f;
            _hint = -1;
            Say(why, 2f);
            GameAudio.Play2D(GameSfx.FlashlightAdjust, 0.6f);
        }

        private void Say(string text, float seconds)
        {
            _msg = text;
            _msgUntil = Time.time + seconds;
        }

        // ================================================================ camera

        private void EnsureCamera(NetPlayer me)
        {
            if (_cam == null)
            {
                var go = new GameObject("RaceCamera");
                _cam = go.AddComponent<Camera>();
                if (me.Camera != null)
                {
                    _cam.CopyFrom(me.Camera);
                    SetPlayerCameras(me, false);
                }
                _cam.fieldOfView = 70f;
                _cam.nearClipPlane = 0.03f;
                _cam.farClipPlane = 400f;
                _cam.cullingMask = ~(1 << 9);
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0.05f, 0.07f, 0.13f);
                go.AddComponent<AudioListener>();
                go.tag = "MainCamera";
                PlaceCamera(1f);
            }
            else if (me.Camera != null) SetPlayerCameras(me, false);
        }

        private void LateUpdate()
        {
            if (_cam != null && _mine != null) PlaceCamera(1f - Mathf.Exp(-9f * Time.deltaTime));
        }

        private void PlaceCamera(float k)
        {
            Transform c = _mine.transform;
            Vector3 fwd = c.forward;
            if (_bumper)
            {
                _cam.transform.SetPositionAndRotation(_mine.Parts.Root.TransformPoint(new Vector3(0f, 0.17f, 0.05f)),
                    _mine.Parts.Root.rotation * Quaternion.Euler(4f, 0f, 0f));
                return;
            }
            const int mask = ~((1 << 2) | (1 << 8) | (1 << 9));
            Vector3 target = c.position + Vector3.up * 0.12f;
            // Under a roof (tunnel): the camera drops lower and comes closer.
            bool covered = Physics.Raycast(c.position + Vector3.up * 0.1f, Vector3.up, 1.2f, mask, QueryTriggerInteraction.Ignore);
            _cover = Mathf.MoveTowards(_cover, covered ? 1f : 0f, Time.deltaTime * 3f);
            Vector3 want = c.position - fwd * Mathf.Lerp(1.3f, 0.8f, _cover) + Vector3.up * Mathf.Lerp(0.55f, 0.3f, _cover);
            // Smooth the wanted point (not the clipped one), then pull it in front of any wall / roof between it and the car.
            _camRaw = (_camRaw - want).sqrMagnitude > 25f || k >= 1f ? want : Vector3.Lerp(_camRaw, want, k);
            Vector3 pos = _camRaw;
            Vector3 d = pos - target;
            float len = d.magnitude;
            if (len > 0.01f && Physics.SphereCast(target, 0.08f, d / len, out var hit, len, mask, QueryTriggerInteraction.Ignore))
                pos = target + d / len * Mathf.Max(0.05f, hit.distance - 0.02f);
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.LookRotation((target + fwd * 0.9f - pos).normalized, Vector3.up);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 68f + 9f * Mathf.Clamp01(Mathf.Abs(_mine.Speed) / 6.4f), 1f - Mathf.Exp(-3f * Time.deltaTime));
        }

        private void RestoreCamera()
        {
            if (_cam != null) Destroy(_cam.gameObject);
            var me = NetPlayer.Local;
            if (me != null && me.Camera != null) SetPlayerCameras(me, true);
        }

        /// <summary>The player's camera, its view-model overlay camera and the listener.</summary>
        private static void SetPlayerCameras(NetPlayer me, bool on)
        {
            foreach (var c in me.Camera.GetComponentsInChildren<Camera>(true)) if (c.enabled != on) c.enabled = on;
            var vm = (me.ViewModel as HideAndSeek.Characters.FirstPersonViewModel)?.ViewModelCamera;
            if (vm != null && vm.enabled != on) vm.enabled = on;
            var l = me.Camera.GetComponent<AudioListener>();
            if (l != null) l.enabled = on;
        }

        // ================================================================ test autopilot

        public static bool TestAutopilot;

        /// <summary>Test helper: follows the centre line ahead (steers towards a point 2.5 m ahead).</summary>
        private void Autopilot(out float throttle, out float steer)
        {
            int s = Track.Nearest(_mine.transform.position, _hint);
            Vector3 aim = Track.Pts[(s + 7) % Track.Count];
            Vector3 to = aim - _mine.transform.position;
            to.y = 0f;
            float ang = Vector3.SignedAngle(_mine.transform.forward, to, Vector3.up);
            steer = Mathf.Clamp(ang / 25f, -1f, 1f);
            throttle = Mathf.Abs(ang) > 50f ? 0.5f : 1f;
        }

        // ================================================================ HUD

        private void OnGUI()
        {
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (mm == null || me == null || Track == null) return;
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label) { fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _mid = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _small = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperLeft, richText = true };
            }
            float sc = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
            float W = Screen.width / sc;

            int target = Mathf.Max(1, mm.Settings.Value.Laps);
            int laps = Mathf.Max(0, (_cps - 1) / RaceTrack.Checkpoints);
            // Standings.
            var rank = new List<NetPlayer>(_order);
            rank.Sort((a, b) => Progress(b).CompareTo(Progress(a)));
            int myPos = rank.IndexOf(me) + 1;

            Shadowed(new Rect(W * 0.5f - 600f, 40f, 1200f, 50f), $"OKRĄŻENIE {Mathf.Min(laps + 1, target)} / {target}     POZYCJA {myPos} / {rank.Count}", _mid, Color.white);

            var sb = new System.Text.StringBuilder("<b>WYŚCIG</b>\n");
            for (int i = 0; i < rank.Count; i++)
            {
                var p = rank[i];
                string hex = ColorUtility.ToHtmlStringRGB(PaintOf(p));
                int l = Mathf.Max(0, (p == me ? _cps : p.RaceCps.Value) - 1) / RaceTrack.Checkpoints;
                sb.Append($"{i + 1}. <color=#{hex}>#</color> {p.Nick}{(p == me ? " (ty)" : "")}  <color=#9aa>{Mathf.Min(l, target)}/{target}</color>\n");
            }
            Shadowed(new Rect(24f, 200f, 420f, 400f), sb.ToString(), _small, Color.white);

            double start = mm.RaceStartAt.Value;
            double left = start - mm.Now;
            if (start > 0 && left > -1.2)
            {
                string t = left > 3.0 ? "PRZYGOTUJ SIĘ" : left > 0 ? Mathf.CeilToInt((float)left).ToString() : "START!";
                Shadowed(new Rect(W * 0.5f - 400f, 380f, 800f, 140f), t, left > 3.0 ? _mid : _big, left > 0 ? new Color(1f, 0.3f, 0.25f) : new Color(0.3f, 1f, 0.4f));
            }
            if (Time.time < _msgUntil) Shadowed(new Rect(W * 0.5f - 500f, 520f, 1000f, 60f), _msg, _mid, new Color(1f, 0.85f, 0.3f));
            int slot = SlotOf(me);
            Shadowed(new Rect(24f, 1080f - 70f, 1400f, 60f),
                $"Twój samochodzik: <b><color=#{ColorUtility.ToHtmlStringRGB(PaintOf(me))}>{PaintNames[Mathf.Max(0, slot) % PaintNames.Length]}</color></b>    " +
                "WASD jazda   Shift turbo   R powrót na checkpoint   C kamera   H klakson", _small, new Color(0.85f, 0.88f, 0.9f));
            GUI.matrix = Matrix4x4.identity;
        }

        private static void Shadowed(Rect r, string text, GUIStyle s, Color c)
        {
            var old = s.normal.textColor;
            s.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, s);
            s.normal.textColor = c;
            GUI.Label(r, text, s);
            s.normal.textColor = old;
        }
    }
}
