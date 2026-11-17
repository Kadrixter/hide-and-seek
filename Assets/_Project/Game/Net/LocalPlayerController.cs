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

        /// <summary>Downed for longer than Tuning.CrawlTime: cannot crawl any more.</summary>
        public bool CrawlExhausted { get; private set; }

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

            if (TestButton && !_testPlaced && LobbyButton.Instance != null && phase == MatchPhase.Lobby && Time.timeSinceLevelLoad > 1.5f)
            {
                _testPlaced = true;
                Vector3 aim = LobbyButton.Instance.AimPoint;
                Vector3 from = aim + new Vector3(-1.4f, 0f, 0f);
                from.y = 0.02f;
                Vector3 dir = aim - (from + Vector3.up * 1.62f);
                _p.TeleportLocal(from, Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z)).eulerAngles.y);
                _p.Motor.SetLook(_p.Motor.LookYaw, Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg);
            }
            if ((DebugPos || TestButton) && Time.time >= _nextDebug)
            {
                _nextDebug = Time.time + 1f;
                string extra = "";
                if (LobbyButton.Instance != null)
                {
                    Vector3 e = _p.EyePosition, t = LobbyButton.Instance.AimPoint, to = t - e;
                    Vector3 f = _p.Camera != null ? _p.Camera.transform.forward : transform.forward;
                    string hitName = Physics.Linecast(e, t - to.normalized * 0.3f, out var h, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore)
                        ? h.collider.name + "/" + h.collider.gameObject.layer : "none";
                    extra = $" id={GetHashCode()} d={to.magnitude:0.00} ang={Vector3.Angle(f, to):0} hit={hitName} instances={FindObjectsByType<LocalPlayerController>(FindObjectsInactive.Include).Length}";
                }
                Debug.Log($"[Prompt] {Prompt} '{PromptText}'{extra}");
                Debug.Log($"[Pos] scene={SceneManager.GetActiveScene().name} phase={phase} life={_p.Life.Value} downed={_p.Motor.Downed} " +
                          $"eye={_p.Motor.EyeHeight:0.00} crouch={_p.Motor.CrouchAmount:0.00} pos={transform.position} cam={(_p.Camera != null ? _p.Camera.transform.position.ToString() : "-")}");
            }

            // ---- input locks
            bool dead = _p.Life.Value == LifeState.Dead;
            bool blind = _p.IsSeeker && (phase == MatchPhase.Hiding || phase == MatchPhase.Loading);
            bool locked = MenuOpen || _p.Busy.Value || blind || dead || phase == MatchPhase.Loading;
            _p.InputReader.InputEnabled = !locked;
            _p.InputReader.ManageCursor = !MenuOpen;
            // Downed: crawling only for the first CrawlTime seconds, then too weak to move.
            bool exhausted = false;
            if (_p.Life.Value == LifeState.Downed && mm != null)
            {
                double downedFor = mm.Now - (_p.DownedUntil.Value - mm.BleedOut);
                exhausted = downedFor >= Tuning.CrawlTime;
            }
            CrawlExhausted = exhausted;
            _p.Motor.MovementEnabled = !locked && !exhausted;
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

            if (ForceFlashlight && _p.Flashlight != null && !_p.Flashlight.IsOn && _p.Life.Value == LifeState.Alive) _p.Flashlight.SetOn(true, false);

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
                        else if (o.Life.Value == LifeState.Alive && o.Wounded.Value && _p.MedkitCount.Value > 0)
                            Offer(PromptKind.Heal, chest, "H  przytrzymaj - ulecz", d);
                    }
                }
            }

            // Medkits can be picked up any time during the round (also when not hurt) and kept in the inventory.
            if (phase == MatchPhase.Seeking || phase == MatchPhase.Hiding)
            {
                var mm = MatchManager.Instance;
                if (_p.IsAliveHider && mm != null)
                {
                    bool full = _p.MedkitCount.Value >= Tuning.MaxMedkits;
                    for (int i = 0; i < mm.Medkits.Count; i++)
                    {
                        var m = mm.Medkits[i];
                        if (m.Taken) continue;
                        Vector3 mp = m.Position + Vector3.up * 0.2f;
                        float d = Vector3.Distance(eye, mp);
                        if (d < best && d <= Tuning.PickupRange + 1f && InView(eye, fwd, mp, 55f))
                        {
                            _pickupId = m.Id;
                            Offer(PromptKind.Pickup, mp, full ? "E  (masz już max apteczek)" : "E  podnieś apteczkę", d);
                        }
                    }
                }
            }

            if (phase == MatchPhase.Lobby)
            {
                var button = LobbyButton.Instance;
                if (button != null)
                {
                    Vector3 bp = button.AimPoint;
                    float d = Vector3.Distance(eye, bp);
                    if (d <= Tuning.ButtonRange && InView(eye, fwd, bp, 40f, button.transform))
                        Offer(PromptKind.Button, bp + Vector3.up * 0.2f, "E  START", d);
                }
                var board = LobbySettingsBoard.Instance;
                if (board != null)
                {
                    Vector3 bp = board.transform.position;
                    float d = Vector3.Distance(eye, bp);
                    if (d <= Tuning.ButtonRange + 1f && InView(eye, fwd, bp, 35f, board.transform))
                        Offer(PromptKind.Settings, bp, NetworkManager.Singleton.IsServer ? "E  ustawienia rundy" : "E  podgląd ustawień", d);
                }
            }
        }

        private int _pickupId;
        private float _nextDebug;
        private static readonly bool ForceFlashlight = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-flashlight") >= 0;
        private bool _testPlaced;
        private static readonly bool TestButton = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testbutton") >= 0;
        private static readonly bool DebugPos = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-debugpos") >= 0;

        private bool InView(Vector3 eye, Vector3 fwd, Vector3 target, float maxAngle, Transform ignore = null)
        {
            Vector3 to = target - eye;
            if (Vector3.Angle(fwd, to) > maxAngle) return false;
            // Line of sight against the level only; our own capsule and the target object itself never block.
            float dist = to.magnitude - 0.3f;
            return dist <= 0f || !LevelRaycast(eye, to.normalized, dist, ignore, out _);
        }

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Nearest hit along the ray, skipping the local player's own colliders (and <paramref name="ignore"/>).</summary>
        private bool LevelRaycast(Vector3 origin, Vector3 dir, float maxDist, Transform ignore, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, maxDist, ~(1 << PlayerAvatarBinder.LocalBodyLayer | 1 << 9),
                QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.transform.IsChildOf(transform)) continue;
                if (ignore != null && h.transform.IsChildOf(ignore)) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; }
            }
            return bestD < float.MaxValue;
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
            else if (kb.gKey.isPressed && _p.MedkitCount.Value > 0 && _p.Wounded.Value && _p.IsAliveHider)
            {
                wantHold = PromptKind.Heal;
                target2 = _p;
            }
            else if (kb.gKey.wasPressedThisFrame && _p.MedkitCount.Value > 0 && !_p.Wounded.Value)
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
            if (LevelRaycast(origin, dir, maxDist, null, out var hit))
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
