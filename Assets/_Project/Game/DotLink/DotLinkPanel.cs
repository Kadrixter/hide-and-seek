using HideAndSeek.Game.Cinematics;
using HideAndSeek.Game.Net;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// The dot link panel on every peer: a screen set into the exit-gate electrical box (its door, label and lock swing
    /// open on a hinge from stage DotLink on; before that the door is shut and the screen dead). Follows
    /// <see cref="MatchManager.DotLinkPuzzleNet"/> / <see cref="MatchManager.DotLinkPathsNet"/> (or the local user's
    /// draft while he drags), repaints the texture only on a change, a dim light of the screen on the wall, 3D sounds
    /// (ticks while lines grow / shrink, a beep per joined pair, the solved chime with two bright flashes).
    /// </summary>
    public sealed class DotLinkPanel : MonoBehaviour, IDotLinkTarget
    {
        public static DotLinkPanel Instance { get; private set; }

        ExitGate _gate;
        Transform _box, _hinge, _screenQ;
        DotLinkScreen _screen;
        Material _screenMat;
        Light _light;
        AudioSource _audio;
        float _door, _flashT = -1f;
        string _puzzleText = "", _pathsText = "";
        DotLinkPuzzle _puzzle;
        DotLinkBoard _net, _shown;
        bool _on, _seenNet;
        string _drawnKey = "";

        public DotLinkPuzzle Puzzle => _puzzle;
        /// <summary>The shared board as the server sent it (null until the puzzle is there).</summary>
        public DotLinkBoard NetBoard => _net;
        public Transform ScreenQuad => _screenQ;
        public bool Lit => _on;

        public static void Ensure()
        {
            var gate = ExitGate.Instance;
            if (gate == null || (Instance != null && Instance._gate == gate)) return;
            if (Instance != null) Destroy(Instance.gameObject);
            var box = gate.transform.Find("ElectricBox");
            if (box == null) return;
            var go = new GameObject("DotLinkPanel");
            go.layer = gate.gameObject.layer;
            go.transform.SetParent(box, false);
            var p = go.AddComponent<DotLinkPanel>();
            p._gate = gate;
            p._box = box;
            p.Build();
            Instance = p;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_screen != null) Destroy(_screen.Tex);
        }

        void Build()
        {
            var t = transform;
            var bezel = CineKit.Mat("DotLinkBezel", new Color(0.05f, 0.055f, 0.06f), 0.1f, 0.25f);
            var inside = CineKit.Mat("DotLinkInside", new Color(0.12f, 0.125f, 0.12f), 0.2f, 0.2f);
            Vector3 c = DotLinkLayout.ScreenInBox;
            const float s = DotLinkLayout.ScreenSize, rim = 0.012f;
            // Inside of the box around the screen (seen once the door is open), the bezel, the screen.
            CineKit.Quad("DotLinkInside", t, new Vector3(-0.005f, 0f, 0.1405f), new Vector2(0.31f, 0.43f), inside);
            CineKit.Box("BezelTop", t, c + new Vector3(0f, s * 0.5f + rim * 0.5f, 0.002f), new Vector3(s + rim * 2f, rim, 0.006f), bezel, 0.002f);
            CineKit.Box("BezelBottom", t, c + new Vector3(0f, -s * 0.5f - rim * 0.5f, 0.002f), new Vector3(s + rim * 2f, rim, 0.006f), bezel, 0.002f);
            CineKit.Box("BezelLeft", t, c + new Vector3(-s * 0.5f - rim * 0.5f, 0f, 0.002f), new Vector3(rim, s, 0.006f), bezel, 0.002f);
            CineKit.Box("BezelRight", t, c + new Vector3(s * 0.5f + rim * 0.5f, 0f, 0.002f), new Vector3(rim, s, 0.006f), bezel, 0.002f);
            _screen = new DotLinkScreen();
            _screenMat = new Material(FxUtil.Unlit) { name = "DotLinkScreen", mainTexture = _screen.Tex, color = Color.black };
            _screenQ = CineKit.Quad("DotLinkScreen", t, c + new Vector3(0f, 0f, 0.0005f), new Vector2(s, s), _screenMat);
            _light = CineKit.Node("DotLinkGlow", t, c + new Vector3(0f, 0f, 0.16f)).gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 1.7f;
            _light.color = new Color(0.62f, 0.8f, 0.66f);
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.minDistance = 1.2f;
            _audio.maxDistance = 16f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.playOnAwake = false;
            _audio.dopplerLevel = 0f;
            // The box door (with its label and lock) gets a hinge on its hinge side so it can swing open.
            _hinge = CineKit.Node("DotLinkDoorHinge", _box, new Vector3(-0.166f, 0f, 0.144f));
            foreach (string n in new[] { "Door", "Label", "Lock" })
            {
                var part = _box.Find(n);
                if (part != null) part.SetParent(_hinge, true);
            }
        }

        public bool CameraPose(float fov, float zoom, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = default;
            if (!DotLinkLayout.TryGet(out var lay)) return false;
            lay.CameraPose(fov, zoom, out pos, out rot);
            return true;
        }

        public string SolvedFlash => "Połączono - brama się otwiera";
        public string StatusExtra => "";
        public void SendPath(NetPlayer me, byte color, FixedString512Bytes cells, byte dragId, bool final) => me.DotLinkPathRpc(color, cells, dragId, final);
        public void SendClear(NetPlayer me, byte color) => me.DotLinkClearRpc(color);
        public void SendLeave(NetPlayer me) => me.DotLinkUseRpc(false);

        public void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (_audio == null || clip == null) return;
            _audio.pitch = pitch;
            _audio.PlayOneShot(clip, volume);
        }

        /// <summary>Cell of the screen under a ray (-1 = off the grid / no puzzle).</summary>
        public int CellUnder(Ray ray)
        {
            if (_puzzle == null || _screenQ == null) return -1;
            var plane = new Plane(_screenQ.forward, _screenQ.position);
            if (!plane.Raycast(ray, out float d)) return -1;
            Vector3 q = _screenQ.InverseTransformPoint(ray.GetPoint(d));
            float px = (q.x + 0.5f) * DotLinkScreen.Size, py = (q.y + 0.5f) * DotLinkScreen.Size;
            int cx = Mathf.FloorToInt((px - DotLinkScreen.Off) / DotLinkScreen.Cell);
            int cy = Mathf.FloorToInt((DotLinkScreen.Size - 1 - DotLinkScreen.Off - py) / DotLinkScreen.Cell);
            return cx < 0 || cy < 0 || cx >= _puzzle.W || cy >= _puzzle.H ? -1 : cy * _puzzle.W + cx;
        }

        /// <summary>World point of a cell centre on the screen (tests aim the virtual mouse at it).</summary>
        public Vector3 CellWorld(int cell)
        {
            int w = _puzzle != null ? _puzzle.W : 10;
            float px = DotLinkScreen.Off + (cell % w) * DotLinkScreen.Cell + DotLinkScreen.Cell * 0.5f;
            float py = DotLinkScreen.Size - 1 - (DotLinkScreen.Off + (cell / w) * DotLinkScreen.Cell + DotLinkScreen.Cell * 0.5f);
            return _screenQ.TransformPoint(new Vector3(px / DotLinkScreen.Size - 0.5f, py / DotLinkScreen.Size - 0.5f, 0f));
        }

        void Update()
        {
            var mm = MatchManager.Instance;
            bool open = mm != null && mm.Escape.Value >= EscapeStage.DotLink;
            ReadNet(mm, open);
            // Door: swings open in ~1.1 s; shut again (new round) at once.
            float want = open ? 1f : 0f;
            _door = want > _door ? Mathf.MoveTowards(_door, want, Time.deltaTime / 1.1f) : want;
            if (_hinge != null) _hinge.localRotation = Quaternion.Euler(0f, -112f * Mathf.SmoothStep(0f, 1f, _door), 0f);
            bool on = open && _door > 0.55f;
            if (on != _on)
            {
                _on = on;
                if (on) Play(DotLinkSounds.Power, 0.55f);
                _drawnKey = "";
            }
            // Solved flash: two bright pulses, then calm.
            bool lit = false;
            if (_flashT >= 0f)
            {
                _flashT += Time.deltaTime;
                lit = _flashT < 0.35f || (_flashT > 0.55f && _flashT < 1.05f);
                if (_flashT > 1.4f) _flashT = -1f;
            }
            float glow = !_on ? 0f : lit ? 1.1f : 0.5f;
            _light.intensity = Mathf.MoveTowards(_light.intensity, glow, Time.deltaTime * 4f);
            _screenMat.color = _on ? new Color(0.92f, 0.92f, 0.92f, 1f) : Color.black;
            Repaint(lit);
        }

        void ReadNet(MatchManager mm, bool open)
        {
            string pt = mm != null ? mm.DotLinkPuzzleNet.Value.ToString() : "";
            if (pt != _puzzleText)
            {
                _puzzleText = pt;
                _puzzle = DotLinkPuzzle.Decode(pt);
                _net = _puzzle != null ? new DotLinkBoard(_puzzle) : null;
                _shown = null;
                _pathsText = "";
                _seenNet = false;
                _drawnKey = "";
            }
            string pa = mm != null ? mm.DotLinkPathsNet.Value.ToString() : "";
            if (_net != null && pa != _pathsText)
            {
                _pathsText = pa;
                bool wasSolved = _net.SolvedFlag;
                if (_net.Decode(pa) && _net.SolvedFlag && !wasSolved && _seenNet && open)
                {
                    _flashT = 0f;
                    Play(DotLinkSounds.Solved, 0.8f);
                }
                _seenNet = true;
            }
        }

        void Repaint(bool lit)
        {
            if (!_on || _puzzle == null || _net == null)
            {
                string k = _on ? "boot" + (Time.time % 1f < 0.5f ? "1" : "0") : "off";
                if (k != _drawnKey)
                {
                    _drawnKey = k;
                    _screen.DrawOff(k == "boot1");
                }
                return;
            }
            bool mine = ReferenceEquals(DotLinkLocal.Target, this); // the local user's draft belongs to this screen
            var draft = mine ? DotLinkLocal.Draft : null;
            var show = draft ?? _net;
            int hover = mine && (draft != null || DotLinkLocal.Active) ? DotLinkLocal.Hover : -1;
            string key = $"{(draft != null ? "d" + DotLinkLocal.DraftVersion : "n" + _net.Seq)}|{hover}|{lit}";
            if (key == _drawnKey) return;
            _drawnKey = key;
            Feedback(show);
            _screen.Draw(_puzzle, show, hover, lit);
        }

        /// <summary>Ticks / join beeps for what changed since the last picture (any peer near the panel hears them).</summary>
        void Feedback(DotLinkBoard show)
        {
            if (_shown == null)
            {
                _shown = show.Clone();
                return;
            }
            int before = 0, after = 0, joinedBefore = _shown.CompleteCount, joinedAfter = show.CompleteCount;
            for (int c = 0; c < _puzzle.Colors; c++)
            {
                before += _shown.Path(c).Count;
                after += show.Path(c).Count;
            }
            if (joinedAfter > joinedBefore && !show.SolvedFlag) Play(DotLinkSounds.Join, 0.5f);
            else if (after != before) Play(DotLinkSounds.Tick, 0.32f, after > before ? Random.Range(1.0f, 1.08f) : Random.Range(0.78f, 0.84f));
            _shown.CopyFrom(show);
        }
    }
}
