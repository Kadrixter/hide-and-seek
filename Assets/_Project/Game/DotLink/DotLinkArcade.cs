using HideAndSeek.Game.Net;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// The KABELKI arcade cabinet in the lobby on every peer: the exit-gate puzzle on a square screen set into the
    /// cabinet's 4:3 one. Follows <see cref="MatchManager.ArcLinkPuzzleNet"/> / <see cref="MatchManager.ArcLinkPathsNet"/>
    /// (or the local player's draft while he drags), so everybody sees the player's lines live; ticks, join beeps and
    /// the solved chime come from the cabinet. With nobody at it (and the last board shown for a while) it runs a demo:
    /// a locally made puzzle solved line by line. Built on the third cabinet by MultiplayerBuilder (screen, glow).
    /// </summary>
    public sealed class DotLinkArcade : MonoBehaviour, IDotLinkTarget
    {
        public Renderer screen;
        public Light glow;

        public static DotLinkArcade Instance { get; private set; }

        const float ScreenSize = 0.36f, ShowLastFor = 25f;
        Transform _q;
        DotLinkScreen _screen;
        Material _mat;
        AudioSource _audio;
        string _puzzleText = "", _pathsText = "", _drawnKey = "";
        DotLinkPuzzle _puzzle;
        DotLinkBoard _net, _shown;
        float _flashT = -1f, _glowBase, _idleSince = -999f;
        bool _wasUsed;
        readonly DotLinkDemo _demo = new();

        public DotLinkBoard NetBoard => _net;
        public DotLinkPuzzle Puzzle => _puzzle;
        public Transform ScreenQuad => _q;
        /// <summary>The demo is on the screen (nobody playing, the last board shown long enough).</summary>
        public bool ShowingDemo { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_screen != null) Destroy(_screen.Tex);
        }

        void Start()
        {
            _screen = new DotLinkScreen();
            _mat = new Material(FxUtil.Unlit) { name = "ArcLinkScreen", mainTexture = _screen.Tex, color = new Color(0.92f, 0.92f, 0.92f, 1f) };
            if (screen != null)
            {
                // The cabinet's own 4:3 picture: a dark glass around the square puzzle screen.
                var dark = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "ArcLinkGlass" };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(9, 12, 10, 255);
                dark.SetPixels32(px);
                dark.Apply(false, true);
                screen.material.mainTexture = dark;
                var st = screen.transform;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "ArcLinkScreen";
                Destroy(q.GetComponent<Collider>());
                q.layer = gameObject.layer;
                q.transform.SetParent(transform, true);
                // The quad is seen from its -Z side: a millimetre out of the glass, towards the room.
                q.transform.SetPositionAndRotation(st.position - st.forward * 0.001f, st.rotation);
                q.transform.localScale = new Vector3(ScreenSize, ScreenSize, 1f);
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = _mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _q = q.transform;
            }
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.minDistance = 1.2f;
            _audio.maxDistance = 14f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.playOnAwake = false;
            _audio.dopplerLevel = 0f;
            if (glow != null) _glowBase = glow.intensity;
            ArcadeArt.ApplyLink(transform);
        }

        // ---- IDotLinkTarget

        public void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (_audio == null || clip == null) return;
            _audio.pitch = pitch;
            _audio.PlayOneShot(clip, volume);
        }

        public int CellUnder(Ray ray)
        {
            if (_puzzle == null || _q == null) return -1;
            var plane = new Plane(_q.forward, _q.position);
            if (!plane.Raycast(ray, out float d)) return -1;
            Vector3 q = _q.InverseTransformPoint(ray.GetPoint(d));
            float px = (q.x + 0.5f) * DotLinkScreen.Size, py = (q.y + 0.5f) * DotLinkScreen.Size;
            int cx = Mathf.FloorToInt((px - DotLinkScreen.Off) / DotLinkScreen.Cell);
            int cy = Mathf.FloorToInt((DotLinkScreen.Size - 1 - DotLinkScreen.Off - py) / DotLinkScreen.Cell);
            return cx < 0 || cy < 0 || cx >= _puzzle.W || cy >= _puzzle.H ? -1 : cy * _puzzle.W + cx;
        }

        public Vector3 CellWorld(int cell)
        {
            int w = _puzzle != null ? _puzzle.W : 10;
            float px = DotLinkScreen.Off + (cell % w) * DotLinkScreen.Cell + DotLinkScreen.Cell * 0.5f;
            float py = DotLinkScreen.Size - 1 - (DotLinkScreen.Off + (cell / w) * DotLinkScreen.Cell + DotLinkScreen.Cell * 0.5f);
            return _q.TransformPoint(new Vector3(px / DotLinkScreen.Size - 0.5f, py / DotLinkScreen.Size - 0.5f, 0f));
        }

        public bool CameraPose(float fov, float zoom, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = default;
            if (_q == null) return false;
            Vector3 normal = -_q.forward; // out of the screen, into the room
            float d = ScreenSize / 0.8f / (2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad)) * zoom;
            pos = _q.position + normal * d;
            rot = Quaternion.LookRotation(-normal, _q.up);
            return true;
        }

        public string SolvedFlash
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm == null) return "Ułożone!";
                float t = mm.ArcLinkSolvedTime.Value;
                bool record = t > 0f && Mathf.Approximately(t, mm.ArcLinkBest.Value);
                return $"Ułożone w {MatchManager.ArcLinkClock(t)}{(record ? " - rekord!" : "")}";
            }
        }

        public string StatusExtra
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm == null || _net == null) return "";
                double t = mm.ArcLinkSolvedTime.Value > 0f ? mm.ArcLinkSolvedTime.Value : mm.Now - mm.ArcLinkStartAt.Value;
                string best = mm.ArcLinkBest.Value > 0f ? $"     Rekord: {MatchManager.ArcLinkClock(mm.ArcLinkBest.Value)} ({mm.ArcLinkBestNick.Value})" : "";
                return $"     Czas: {MatchManager.ArcLinkClock(t)}{best}";
            }
        }

        public void SendPath(NetPlayer me, byte color, FixedString512Bytes cells, byte dragId, bool final) => me.ArcLinkPathRpc(color, cells, dragId, final);
        public void SendClear(NetPlayer me, byte color) => me.ArcLinkClearRpc(color);
        public void SendLeave(NetPlayer me) => me.ArcLinkUseRpc(false);

        // ---- every frame

        void Update()
        {
            if (_q == null) return;
            var mm = MatchManager.Instance;
            bool used = mm != null && mm.ArcLinkUser.Value != MatchManager.NoDotLinkUser;
            if (used != _wasUsed)
            {
                _wasUsed = used;
                if (!used) _idleSince = Time.time;
                else Play(DotLinkSounds.Power, 0.45f);
            }
            ReadNet(mm);
            bool lit = false;
            if (_flashT >= 0f)
            {
                _flashT += Time.deltaTime;
                lit = _flashT < 0.35f || (_flashT > 0.55f && _flashT < 1.05f);
                if (_flashT > 1.4f) _flashT = -1f;
            }
            ShowingDemo = !used && (_net == null || Time.time - _idleSince > ShowLastFor);
            if (ShowingDemo) DrawDemo();
            else Repaint(lit, used);
            if (glow != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.3f);
                glow.intensity = _glowBase * (lit ? 1.6f : pulse);
            }
        }

        void ReadNet(MatchManager mm)
        {
            string pt = mm != null ? mm.ArcLinkPuzzleNet.Value.ToString() : "";
            if (pt != _puzzleText)
            {
                _puzzleText = pt;
                _puzzle = DotLinkPuzzle.Decode(pt);
                _net = _puzzle != null ? new DotLinkBoard(_puzzle) : null;
                _shown = null;
                _pathsText = "";
                _drawnKey = "";
            }
            string pa = mm != null ? mm.ArcLinkPathsNet.Value.ToString() : "";
            if (_net != null && pa != _pathsText)
            {
                bool first = _pathsText.Length == 0;
                _pathsText = pa;
                bool wasSolved = _net.SolvedFlag;
                if (_net.Decode(pa) && _net.SolvedFlag && !wasSolved && !first)
                {
                    _flashT = 0f;
                    Play(DotLinkSounds.Solved, 0.8f);
                }
            }
        }

        void Repaint(bool lit, bool used)
        {
            if (_puzzle == null || _net == null)
            {
                // A player is at the cabinet but the puzzle is still being made: the boot cursor blinks.
                string k = used ? "boot" + (Time.time % 1f < 0.5f ? "1" : "0") : "off";
                if (k != _drawnKey)
                {
                    _drawnKey = k;
                    _screen.DrawOff(k == "boot1");
                }
                return;
            }
            bool mine = ReferenceEquals(DotLinkLocal.Target, this);
            var draft = mine ? DotLinkLocal.Draft : null;
            var show = draft ?? _net;
            int hover = mine ? DotLinkLocal.Hover : -1;
            string key = $"{(draft != null ? "d" + DotLinkLocal.DraftVersion : "n" + _net.Seq)}|{hover}|{lit}";
            if (key == _drawnKey) return;
            _drawnKey = key;
            Feedback(show);
            _screen.Draw(_puzzle, show, hover, lit);
        }

        /// <summary>Ticks / join beeps for what changed since the last picture (everyone near the cabinet hears them).</summary>
        void Feedback(DotLinkBoard show)
        {
            if (_shown == null || _shown.Puzzle != show.Puzzle)
            {
                _shown = show.Clone();
                return;
            }
            int before = 0, after = 0;
            for (int c = 0; c < _puzzle.Colors; c++)
            {
                before += _shown.Path(c).Count;
                after += show.Path(c).Count;
            }
            if (show.CompleteCount > _shown.CompleteCount && !show.SolvedFlag) Play(DotLinkSounds.Join, 0.45f);
            else if (after != before) Play(DotLinkSounds.Tick, 0.28f, after > before ? Random.Range(1.0f, 1.08f) : Random.Range(0.78f, 0.84f));
            _shown.CopyFrom(show);
        }

        void DrawDemo()
        {
            _demo.Tick(Time.deltaTime);
            string key = "demo" + _demo.Version;
            if (key == _drawnKey) return;
            _drawnKey = key;
            if (_demo.Puzzle == null) _screen.DrawOff(Time.time % 1f < 0.5f);
            else _screen.Draw(_demo.Puzzle, _demo.Board, -1, _demo.Lit);
        }
    }
}
