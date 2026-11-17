using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    public enum PromptKind
    {
        None,
        Knife,      // F on a hider
        Rescue,     // H hold on a downed hider
        Heal,       // H hold on a wounded hider (needs medkit)
        Pickup,     // E on a medkit
        Button,     // E on the lobby start button
        Settings,   // E on the lobby settings board
    }

    /// <summary>
    /// Local player's gameplay input: interactions (knife, rescue, heal, pickup, lobby button),
    /// Glock (shoot, reload, holster), flashlight focus, input locks (blind seeker, busy, dead, menus).
    /// </summary>
    public class LocalPlayerController : MonoBehaviour
    {
        public static LocalPlayerController Instance { get; private set; }

        private NetPlayer _p;
        private float _holdTimer;
        private PromptKind _holdKind;
        private NetPlayer _holdTarget;
        private float _focus;
        private float _nextShot;
        private bool _placedInLobby;
        private Vector3 _lastBloodPos;

        // Prompt for the HUD
        public PromptKind Prompt { get; private set; }
        public Vector3 PromptWorld { get; private set; }
        public string PromptText { get; private set; }
        public float HoldProgress => _holdKind != PromptKind.None ? _holdTimer / HoldDuration(_holdKind) : 0f;

        /// <summary>True while a menu (Esc, settings board) owns the mouse.</summary>
        public static bool MenuOpen { get; set; }

        private void Awake()
        {
            Instance = this;
            _p = GetComponent<NetPlayer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static float HoldDuration(PromptKind k) => k == PromptKind.Rescue ? Tuning.RescueHoldTime : Tuning.HealHoldTime;

        private void Update()
        {
            if (_p == null || !_p.IsSpawned) return;
            var mm = MatchManager.Instance;
            var phase = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (!_placedInLobby && SceneManager.GetActiveScene().name == NetSession.LobbyScene && phase == MatchPhase.Lobby)
            {
                _placedInLobby = true;
                var spawns = SpawnPoints.Lobby();
                if (spawns.Count > 0)
                {
                    var sp = spawns[Random.Range(0, spawns.Count)];
                    _p.TeleportLocal(sp.position, sp.eulerAngles.y);
                }
            }
            if (SceneManager.GetActiveScene().name != NetSession.LobbyScene) _placedInLobby = false;

            if (DebugPos && Time.time >= _nextDebug)
            {
                _nextDebug = Time.time + 2f;
                Debug.Log($"[Pos] scene={SceneManager.GetActiveScene().name} phase={phase} life={_p.Life.Value} downed={_p.Motor.Downed} " +
                          $"eye={_p.Motor.EyeHeight:0.00} crouch={_p.Motor.CrouchAmount:0.00} pos={transform.position} cam={(_p.Camera != null ? _p.Camera.transform.position.ToString() : "-")}");
            }

            // ---- input locks
            bool dead = _p.Life.Value == LifeState.Dead;
            bool blind = _p.IsSeeker && (phase == MatchPhase.Hiding || phase == MatchPhase.Loading);
            bool locked = MenuOpen || _p.Busy.Value || blind || dead || phase == MatchPhase.Loading;
            _p.InputReader.InputEnabled = !locked;
            _p.InputReader.ManageCursor = !MenuOpen;
            _p.Motor.MovementEnabled = !locked;
            _p.Motor.LookEnabled = !locked;
            var cc = _p.GetComponent<CharacterController>();
            if (cc != null && cc.enabled == dead) cc.enabled = !dead;
            if (_p.Flashlight != null)
            {
                bool glockOut = _p.Held.Value == HeldItem.Glock;
                _p.Flashlight.ListenToInput = !glockOut && !locked && _p.Life.Value != LifeState.Downed;
                if (dead && _p.Flashlight.IsOn) _p.Flashlight.SetOn(false, false);
            }

            // Blood trail while crawling.
            if (_p.Life.Value == LifeState.Downed && (transform.position - _lastBloodPos).sqrMagnitude > 0.7f)
            {
                _lastBloodPos = transform.position;
                BloodFx.Decal(transform.position, Random.Range(0.18f, 0.32f));
            }

            Prompt = PromptKind.None;
            if (locked || kb == null || mouse == null)
            {
                CancelHold();
                return;
            }

            // ---- T toggles the flashlight always (LMB is the gun while the Glock is out)
            if (kb.tKey.wasPressedThisFrame && _p.Flashlight != null && _p.Life.Value != LifeState.Downed) _p.Flashlight.Toggle();

            // ---- seeker: flashlight focus with the mouse wheel
            if (_p.IsSeeker && _p.Flashlight != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    float before = _focus;
                    _focus = Mathf.Clamp01(_focus + Mathf.Sign(wheel) * 0.125f);
                    if (!Mathf.Approximately(before, _focus))
                    {
                        _p.Flashlight.SetFocus(_focus);
                        _p.FlashFocus.Value = (byte)Mathf.RoundToInt(_focus * 255f);
                        GameAudio.Play2D(GameSfx.FlashlightAdjust, 0.5f);
                    }
                }
            }

            // ---- seeker: glock
            if (_p.IsSeeker && _p.HasGlock.Value && phase == MatchPhase.Seeking)
            {
                if (kb.qKey.wasPressedThisFrame) _p.RequestHolsterRpc(_p.Held.Value != HeldItem.Glock);
                if (_p.Held.Value == HeldItem.Glock)
                {
                    if (kb.rKey.wasPressedThisFrame) _p.RequestReloadRpc();
                    if (mouse.leftButton.wasPressedThisFrame && _p.InputReader.CursorLocked && Time.time >= _nextShot)
                    {
                        _nextShot = Time.time + Tuning.FireInterval;
                        Shoot();
                    }
                }
            }

            FindPrompt(phase);
            HandlePromptInput(kb);
        }

        // ================================================================ prompts

        private void FindPrompt(MatchPhase phase)
        {
            Vector3 eye = _p.EyePosition;
            Vector3 fwd = _p.Camera != null ? _p.Camera.transform.forward : transform.forward;
            float best = float.MaxValue;

            void Offer(PromptKind kind, Vector3 world, string text, float dist)
            {
                if (dist >= best) return;
                best = dist;
                Prompt = kind;
                PromptWorld = world;
                PromptText = text;
            }

            if (phase == MatchPhase.Seeking)
            {
                foreach (var o in NetPlayer.All)
                {
                    if (o == null || o == _p) continue;
                    Vector3 chest = o.ChestPosition;
                    float d = Vector3.Distance(eye, chest);

                    if (_p.IsSeeker && o.IsAliveHider && !o.Busy.Value && d <= Tuning.KnifePromptRange && InView(eye, fwd, chest, 50f))
                    {
                        if (d <= Tuning.KnifeRange) Offer(PromptKind.Knife, chest, "F", d);
                        else Offer(PromptKind.Knife, chest, "F (bliżej)", d + 10f);
                    }
                    else if (_p.IsAliveHider && o.IsHider && d <= Tuning.RescueRange + 0.6f && InView(eye, fwd, chest, 70f))
                    {
                        if (o.Life.Value == LifeState.Downed)
                            Offer(PromptKind.Rescue, chest, "H  przytrzymaj - uratuj", d);
                        else if (o.Life.Value == LifeState.Alive && o.Wounded.Value && _p.HasMedkit.Value)
                            Offer(PromptKind.Heal, chest, "H  przytrzymaj - ulecz", d);
                    }
                }

                var mm = MatchManager.Instance;
                if (_p.IsAliveHider && !_p.HasMedkit.Value && mm != null)
                {
                    for (int i = 0; i < mm.Medkits.Count; i++)
                    {
                        var m = mm.Medkits[i];
                        if (m.Taken) continue;
                        Vector3 mp = m.Position + Vector3.up * 0.2f;
                        float d = Vector3.Distance(eye, mp);
                        if (d <= Tuning.PickupRange + 0.8f && InView(eye, fwd, mp, 45f))
                        {
                            Offer(PromptKind.Pickup, mp, "E  apteczka", d);
                            _pickupId = m.Id;
                        }
                    }
                }
            }

            if (phase == MatchPhase.Lobby)
            {
                var button = LobbyButton.Instance;
                if (button != null)
                {
                    Vector3 bp = button.transform.position;
                    float d = Vector3.Distance(eye, bp);
                    if (d <= Tuning.ButtonRange && InView(eye, fwd, bp, 35f))
                        Offer(PromptKind.Button, bp + Vector3.up * 0.25f, "E  START", d);
                }
                var board = LobbySettingsBoard.Instance;
                if (board != null)
                {
                    Vector3 bp = board.transform.position;
                    float d = Vector3.Distance(eye, bp);
                    if (d <= Tuning.ButtonRange + 1f && InView(eye, fwd, bp, 30f))
                        Offer(PromptKind.Settings, bp, NetworkManager.Singleton.IsServer ? "E  ustawienia rundy" : "E  podgląd ustawień", d);
                }
            }
        }

        private int _pickupId;
        private float _nextDebug;
        private static readonly bool DebugPos = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-debugpos") >= 0;

        private static bool InView(Vector3 eye, Vector3 fwd, Vector3 target, float maxAngle)
        {
            Vector3 to = target - eye;
            if (Vector3.Angle(fwd, to) > maxAngle) return false;
            // Line of sight against the level only (players have no colliders for others).
            return !Physics.Linecast(eye, target - to.normalized * 0.3f, out _, ~(1 << PlayerAvatarBinder.LocalBodyLayer),
                QueryTriggerInteraction.Ignore);
        }

        private void HandlePromptInput(Keyboard kb)
        {
            switch (Prompt)
            {
                case PromptKind.Knife:
                    if (kb.fKey.wasPressedThisFrame && PromptText == "F")
                    {
                        var target = Nearest(o => o.IsAliveHider);
                        if (target != null) _p.RequestKnifeRpc(target.NetworkObjectId);
                    }
                    break;
                case PromptKind.Pickup:
                    if (kb.eKey.wasPressedThisFrame) _p.RequestPickupRpc(_pickupId);
                    break;
                case PromptKind.Button:
                    if (kb.eKey.wasPressedThisFrame) LobbyButton.Instance.Press();
                    break;
                case PromptKind.Settings:
                    if (kb.eKey.wasPressedThisFrame) GameHud.OpenSettingsPanel();
                    break;
            }

            // Hold H (rescue / heal other), hold G (heal self).
            PromptKind wantHold = PromptKind.None;
            NetPlayer target2 = null;
            if ((Prompt == PromptKind.Rescue || Prompt == PromptKind.Heal) && kb.hKey.isPressed)
            {
                wantHold = Prompt;
                target2 = Nearest(o => o.IsHider && (Prompt == PromptKind.Rescue ? o.Life.Value == LifeState.Downed : o.Wounded.Value));
            }
            else if (kb.gKey.isPressed && _p.HasMedkit.Value && _p.Wounded.Value && _p.IsAliveHider)
            {
                wantHold = PromptKind.Heal;
                target2 = _p;
            }
            else if (kb.gKey.wasPressedThisFrame && _p.HasMedkit.Value && !_p.Wounded.Value)
            {
                GameHud.Toast("Nie jesteś ranny.", 2f);
            }

            if (wantHold == PromptKind.None || target2 == null)
            {
                CancelHold();
                return;
            }
            if (_holdKind != wantHold || _holdTarget != target2)
            {
                _holdKind = wantHold;
                _holdTarget = target2;
                _holdTimer = 0f;
                if (wantHold == PromptKind.Rescue) _p.Reviving.Value = true;
            }
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= HoldDuration(_holdKind))
            {
                if (_holdKind == PromptKind.Rescue) _p.RequestRescueRpc(_holdTarget.NetworkObjectId);
                else _p.RequestHealRpc(_holdTarget.NetworkObjectId);
                CancelHold();
            }
        }

        private void CancelHold()
        {
            if (_holdKind == PromptKind.Rescue && _p != null && _p.IsSpawned && _p.Reviving.Value) _p.Reviving.Value = false;
            _holdKind = PromptKind.None;
            _holdTarget = null;
            _holdTimer = 0f;
        }

        private NetPlayer Nearest(System.Predicate<NetPlayer> filter)
        {
            NetPlayer best = null;
            float bd = float.MaxValue;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == _p || !filter(o)) continue;
                float d = Vector3.Distance(o.ChestPosition, PromptWorld);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // ================================================================ glock

        private void Shoot()
        {
            if (_p.Ammo.Value == 0)
            {
                _p.RequestShootRpc(Vector3.zero, Vector3.forward, ulong.MaxValue, Vector3.zero);
                return;
            }
            var camT = _p.Camera.transform;
            Vector3 origin = camT.position;
            Vector3 dir = camT.forward;

            // Level hit.
            float maxDist = Tuning.GlockRange;
            Vector3 hitPoint = origin + dir * maxDist;
            if (Physics.Raycast(origin, dir, out var hit, maxDist, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore))
            {
                maxDist = hit.distance;
                hitPoint = hit.point;
            }

            // Player hit: analytic capsule test (players carry no colliders for others).
            NetPlayer victim = null;
            float bestT = maxDist;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == _p || !o.IsHider || o.Life.Value == LifeState.Dead) continue;
                Vector3 a, b;
                float radius;
                if (o.Life.Value == LifeState.Downed)
                {
                    // Downed body lies behind the root (root at the shoulders).
                    a = o.transform.position + Vector3.up * 0.25f - o.transform.forward * 1.5f;
                    b = o.transform.position + Vector3.up * 0.25f + o.transform.forward * 0.2f;
                    radius = 0.32f;
                }
                else
                {
                    float h = Mathf.Lerp(1.65f, 1.05f, o.Motor.CrouchAmount);
                    a = o.transform.position + Vector3.up * 0.3f;
                    b = o.transform.position + Vector3.up * h;
                    radius = 0.36f;
                }
                if (RayCapsule(origin, dir, a, b, radius, out float t) && t < bestT)
                {
                    bestT = t;
                    victim = o;
                    hitPoint = origin + dir * t;
                }
            }
            _p.RequestShootRpc(origin, dir, victim != null ? victim.NetworkObjectId : ulong.MaxValue, hitPoint);
        }

        /// <summary>Ray vs capsule (segment a-b, radius r). Returns the approximate entry distance.</summary>
        private static bool RayCapsule(Vector3 o, Vector3 d, Vector3 a, Vector3 b, float r, out float t)
        {
            t = 0f;
            // Closest points between the ray and the segment.
            Vector3 u = d, v = b - a, w = o - a;
            float A = Vector3.Dot(u, u), B = Vector3.Dot(u, v), C = Vector3.Dot(v, v), D = Vector3.Dot(u, w), E = Vector3.Dot(v, w);
            float den = A * C - B * B;
            float sc, tc;
            if (den < 1e-6f) { sc = 0f; tc = C > 0f ? E / C : 0f; }
            else { sc = (B * E - C * D) / den; tc = (A * E - B * D) / den; }
            tc = Mathf.Clamp01(tc);
            sc = Mathf.Max(0f, (Vector3.Dot(u, (a + v * tc) - o)) / A);
            Vector3 pr = o + u * sc, ps = a + v * tc;
            float dist = Vector3.Distance(pr, ps);
            if (dist > r) return false;
            t = Mathf.Max(0f, sc - Mathf.Sqrt(r * r - dist * dist));
            return true;
        }
    }
}
