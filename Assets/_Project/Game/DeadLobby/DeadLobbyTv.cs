using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using HideAndSeek.Player;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The lobby copy's wall TV during a round: a live feed through the eyes of a living player (a camera at his head,
    /// <see cref="NightVision"/> grey like the old spectator, 512 x 288 at 20 frames per second into the screen's
    /// texture), "NA ŻYWO" and his nick on the on-screen display. In reach (in front, looking at it): LMB / RMB = next /
    /// previous player, mouse wheel = TV volume (<see cref="Volume"/>, default 50 %, saved), F = full-screen spectating
    /// (<see cref="DeadLobby"/>). Its speaker is where the voices and the game sounds come out (<see cref="DeadLobbyVoice"/>).
    /// </summary>
    public sealed class DeadLobbyTv : MonoBehaviour
    {
        public const int Width = 512, Height = 288;
        public const float Fps = 20f, Reach = 6.5f;

        public static DeadLobbyTv Instance { get; private set; }
        /// <summary>Test hook: cycle as if LMB were pressed.</summary>
        public static bool TestNext;

        public NetPlayer Target { get; private set; }
        /// <summary>The body is in front of the TV and looks at it (prompt, LMB / RMB, wheel, F).</summary>
        public bool InReach { get; private set; }
        public Vector3 Center { get; private set; }
        public Vector3 Normal { get; private set; }
        /// <summary>Where the sound comes out (just in front of the screen).</summary>
        public Vector3 Speaker => Center + Normal * 0.15f - Vector3.up * 0.2f;
        /// <summary>Frames rendered so far (tests).</summary>
        public int Frames { get; private set; }

        public static float Volume
        {
            get => Mathf.Clamp01(Prefs.GetFloat("hns.tv.vol", 0.5f));
            set => Prefs.SetFloat("hns.tv.vol", Mathf.Clamp01(value));
        }

        private LobbyTv _tv;
        private Camera _cam;
        private Light _nvLight;
        private RenderTexture _rt;
        private Material _mat;
        private TextMesh _osd;
        private float _next;
        private Vector3 _eye;
        private Quaternion _look;
        private bool _snap = true, _handOver;
        private float _volumeShownUntil;

        public static void Create(DeadLobbyWorld world, NetPlayer me)
        {
            DestroyInstance();
            if (world.Tv == null || world.Tv.screen == null) { Debug.LogWarning("[DeadLobby] no TV in the lobby copy"); return; }
            var go = new GameObject("DeadLobbyTvFeed");
            SceneManager.MoveGameObjectToScene(go, world.Scene);
            var tv = go.AddComponent<DeadLobbyTv>();
            tv.Init(world.Tv, me);
            Instance = tv;
        }

        public static void DestroyInstance()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Init(LobbyTv tv, NetPlayer me)
        {
            _tv = tv;
            var screen = tv.screen;
            Center = screen.bounds.center;
            // The screen faces the room: the side towards the room's middle.
            Vector3 n = screen.transform.forward;
            if (Vector3.Dot(n, DeadLobby.Offset + Vector3.up * 1.5f - Center) < 0f) n = -n;
            Normal = n;
            _rt = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32) { name = "DeadLobbyTvFeed", filterMode = FilterMode.Bilinear };
            _mat = screen.material;
            _mat.mainTexture = _rt;
            _mat.mainTextureOffset = Vector2.zero;
            _mat.mainTextureScale = Vector2.one;
            _osd = tv.overlay;
            _cam = gameObject.AddComponent<Camera>();
            if (me != null && me.Camera != null) _cam.CopyFrom(me.Camera);
            _cam.enabled = false;
            _cam.targetTexture = _rt;
            _cam.fieldOfView = 68f;
            _cam.nearClipPlane = 0.06f;
            _cam.cullingMask = (me != null && me.Camera != null ? me.Camera.cullingMask : ~0) | (1 << PlayerAvatarBinder.LocalBodyLayer); // own corpse too
            _cam.cullingMask &= ~(1 << 9); // view models
            gameObject.AddComponent<NightVision>(); // grey, a dim light at the eyes
            var nv = transform.Find("NightVisionLight");
            _nvLight = nv != null ? nv.GetComponent<Light>() : null;
            if (_nvLight != null) _nvLight.enabled = false;
            Cycle(0);
            Debug.Log($"[DeadLobby] TV feed {Width}x{Height} @{Fps} fps, screen at {DeadLobbyWorld.ToLocal(Center)} (lobby-local), watching {(Target != null ? Target.Nick : "-")}");
        }

        /// <summary>Next (+1) / previous (-1) living player; 0 = keep the current one if he is still in the round.</summary>
        public void Cycle(int dir)
        {
            var list = NetPlayer.All;
            int n = list.Count;
            if (n == 0) { Target = null; return; }
            int start = Target != null ? list.IndexOf(Target) : -1;
            if (dir == 0 && Watchable(Target)) return;
            int step = dir < 0 ? -1 : 1;
            for (int i = 1; i <= n; i++)
            {
                var p = list[((start + step * i) % n + n) % n];
                if (!Watchable(p)) continue;
                if (p != Target) { Target = p; _snap = true; Debug.Log($"[DeadLobby] TV watches {p.Nick}"); }
                return;
            }
            Target = null;
        }

        private static bool Watchable(NetPlayer p) => p != null && p.IsSpawned && !p.IsOwner && !p.IsObserver;

        public void Watch(NetPlayer p)
        {
            if (!Watchable(p) || p == Target) return;
            Target = p;
            _snap = true;
        }

        /// <summary>F at the TV: the full-screen spectator starts on the same player.</summary>
        public void HandOverToSpectator() => _handOver = true;

        /// <summary>Back from full screen: the TV goes on with whoever the spectator watched last.</summary>
        public void TakeBackFromSpectator()
        {
            _handOver = false;
            var w = GameHud.Spectated;
            if (w != null) Watch(w);
        }

        /// <summary>Volume of a living speaker's voice on the TV: full for the watched player, others as he hears them.</summary>
        public float VoiceGain(NetPlayer speaker)
        {
            if (Target == null || speaker == null) return 0f;
            if (speaker == Target) return Volume;
            return Volume * HeardBy(speaker.EyePosition, Voice.VoiceChat.HearRange);
        }

        /// <summary>0..1: how well the watched player hears something at <paramref name="p"/> with this range.</summary>
        public float HeardBy(Vector3 p, float range)
        {
            if (Target == null) return 0f;
            Vector3 ear = Target.EyePosition;
            float k = Mathf.Clamp01(1f - Vector3.Distance(ear, p) / Mathf.Max(1f, range));
            k *= k;
            if (FloorAcoustics.DifferentFloor(ear, p)) k *= 0.12f;
            return k;
        }

        private void LateUpdate()
        {
            if (_handOver && DeadLobby.Watching)
            {
                var spec = FindAnyObjectByType<SpectatorCamera>();
                if (spec != null) { spec.Watch(Target); _handOver = false; }
            }
            if (Target != null && !Watchable(Target)) Cycle(1);
            if (Target == null && Time.frameCount % 30 == 0) Cycle(1);
            UpdateReach();
            if (!DeadLobby.InLobby || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f / Fps;
            RenderFeed();
            UpdateOsd();
        }

        private void UpdateReach()
        {
            var body = DeadLobby.Body;
            InReach = false;
            if (body == null || !DeadLobby.InLobby || !body.WalkInDone) return;
            Vector3 eye = body.Eye;
            Vector3 to = Center - eye;
            InReach = to.magnitude <= Reach && Vector3.Dot(eye - Center, Normal) > 0.4f && Vector3.Angle(body.Forward, to) < 28f;
            if (!InReach || LocalPlayerController.MenuOpen) return;
            var mouse = Mouse.current;
            if (TestNext || (mouse != null && mouse.leftButton.wasPressedThisFrame) || PadInput.Down(PadAction.Attack)) Cycle(1); // pad R2 / L2
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || PadInput.Down(PadAction.Aim)) Cycle(-1);
            TestNext = false;
            float wheel = KeyOrPad.ScrollY; // pad: L1 + D-pad up / down = volume
            if (Mathf.Abs(wheel) > 0.01f)
            {
                Volume = Mathf.Round((Volume + Mathf.Sign(wheel) * 0.05f) * 20f) / 20f;
                _volumeShownUntil = Time.unscaledTime + 2f;
            }
        }

        /// <summary>True for a short while after the volume changed (HUD line).</summary>
        public bool ShowVolume => Time.unscaledTime < _volumeShownUntil;

        private void RenderFeed()
        {
            if (Target == null)
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                GL.Clear(true, true, new Color(0.06f, 0.06f, 0.06f));
                RenderTexture.active = prev;
                return;
            }
            var ch = Target.Character;
            Vector3 eye = ch != null && ch.Head != null
                ? ch.Head.TransformPoint(new Vector3(0f, 4.6f * BlockyCharacterPx, 2.6f * BlockyCharacterPx))
                : Target.EyePosition;
            float pitch = Target.Motor != null ? Target.Motor.LookPitch : 0f;
            Quaternion look = Quaternion.Euler(-pitch, Target.transform.eulerAngles.y, 0f);
            float k = _snap ? 1f : 0.55f;
            _snap = false;
            _eye = Vector3.Lerp(_eye, eye, k);
            _look = Quaternion.Slerp(_look, look, k);
            _cam.transform.SetPositionAndRotation(_eye, _look);
            if (_nvLight != null) _nvLight.enabled = true;
            _cam.Render();
            if (_nvLight != null) _nvLight.enabled = false;
            Frames++;
        }

        private const float BlockyCharacterPx = HideAndSeek.Characters.BlockyCharacter.PixelSize;

        private void UpdateOsd()
        {
            if (_osd == null) return;
            _osd.text = Target != null ? $"<color=#ff3b30>NA ŻYWO</color>  {Target.Nick}" : "BRAK SYGNAŁU";
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
