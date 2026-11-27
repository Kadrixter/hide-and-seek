using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Aim guide of the local shooter (only on his screen): a soft line from the cue ball to the ghost ball (a ring
    /// where the cue ball touches the object ball, or the cushion), a short line where the object ball goes and a
    /// fainter one where the cue ball goes on (<see cref="PoolSim.Predict"/>). Red when the first ball is not a legal
    /// one (<see cref="PoolRules.Targets"/>). Ball in hand: a ring under the carried cue ball, green / red = may / may
    /// not be put down there.
    /// </summary>
    public sealed partial class PoolTable
    {
        static readonly Color GuideWhite = new Color(1f, 1f, 1f, 0.55f), GuideRed = new Color(1f, 0.26f, 0.2f, 0.8f);
        /// <summary>Legal hit while the groups are not picked yet (break / open table): yellow; white = an own ball, red = a foul.</summary>
        static readonly Color GuideYellow = new Color(1f, 0.86f, 0.15f, 0.75f);
        static readonly Color GuideObj = new Color(1f, 0.93f, 0.68f, 0.6f), GuideCue = new Color(0.62f, 0.86f, 1f, 0.42f);
        static readonly Color HandOk = new Color(0.45f, 1f, 0.55f, 0.85f), HandBad = new Color(1f, 0.3f, 0.25f, 0.85f);
        const float GuideY = PoolDims.ClothY + 0.004f;

        static Texture2D _lineTex, _ringTex;
        Transform _guideRoot, _ghostRing, _handRing;
        LineRenderer _lnAim, _lnObj, _lnCue;
        Material _ghostMat, _handMat;

        void EnsureGuide()
        {
            if (_guideRoot != null) return;
            _guideRoot = new GameObject("PoolGuide") { layer = gameObject.layer }.transform;
            _guideRoot.SetParent(transform, false);
            _lnAim = Line("AimLine", 0.0055f);
            _lnObj = Line("ObjectLine", 0.0045f);
            _lnCue = Line("CueLine", 0.004f);
            _ghostRing = Ring("GhostRing", PoolDims.BallR * 2.3f, out _ghostMat);
            _handRing = Ring("HandRing", PoolDims.BallR * 2.9f, out _handMat);
        }

        static Material GuideMat(Texture tex)
        {
            var shader = Resources.Load<Shader>("Shaders/HnsBoardMark");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return new Material(shader) { mainTexture = tex };
        }

        LineRenderer Line(string name, float width)
        {
            var go = new GameObject(name) { layer = gameObject.layer };
            go.transform.SetParent(_guideRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.widthMultiplier = width;
            lr.numCapVertices = 3;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = GuideMat(LineTex);
            go.SetActive(false);
            return lr;
        }

        Transform Ring(string name, float size, out Material mat)
        {
            var go = new GameObject(name) { layer = gameObject.layer };
            go.transform.SetParent(_guideRoot, false);
            float h = size * 0.5f;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mat = GuideMat(RingTex);
            mr.sharedMaterial = mat;
            go.SetActive(false);
            return go.transform;
        }

        /// <summary>Soft-edged stripe across the line width.</summary>
        static Texture2D LineTex
        {
            get
            {
                if (_lineTex != null) return _lineTex;
                _lineTex = new Texture2D(4, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                for (int y = 0; y < 16; y++)
                {
                    float v = Mathf.Abs((y + 0.5f) / 16f * 2f - 1f);
                    float a = Mathf.Clamp01(1.25f - v * v * 1.25f);
                    for (int x = 0; x < 4; x++) _lineTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                _lineTex.Apply(false, true);
                return _lineTex;
            }
        }

        /// <summary>Ring with a faint fill (ghost ball / hand marker).</summary>
        static Texture2D RingTex
        {
            get
            {
                if (_ringTex != null) return _ringTex;
                const int n = 64;
                _ringTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f).magnitude / (n * 0.5f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.07f);
                    float fill = r < 0.86f ? 0.16f : 0f;
                    float edge = Mathf.Clamp01((1f - r) * 12f);
                    _ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring, fill) * edge));
                }
                _ringTex.Apply(false, true);
                return _ringTex;
            }
        }

        void TickGuide()
        {
            int seat = LocalSeat;
            bool holding = LocalHolding;
            bool aim = seat >= 0 && LocalTurn && !_awaitShot && !holding && _view[seat].StrokeT < 0f;
            if (_guideRoot == null && !aim && !holding) return;
            EnsureGuide();
            Show(_handRing, holding);
            if (holding)
            {
                _handRing.localPosition = new Vector3(_handPos.x, GuideY, _handPos.y);
                _handMat.color = _handOk ? HandOk : HandBad;
            }
            if (!aim)
            {
                Show(_lnAim, false);
                Show(_lnObj, false);
                Show(_lnCue, false);
                Show(_ghostRing, false);
                return;
            }
            Vector2 cue = AimCue();
            PosWithout(cue);
            bool hits = PoolSim.Predict(_scratch, (ushort)(State.Down | 1), cue, _aimAngle, out var ghost, out int hit, out var objDir, out var cueDir);
            bool legal = !hits || hit < 0 || (PoolRules.Targets(State, PoolState.TeamOf(seat)) & (1 << hit)) != 0;
            _guideHit = hits ? hit : -1;
            _guideLegal = legal;
            bool open = State.Break || State.GroupOf(PoolState.TeamOf(seat)) == 0;
            _guideOpen = open;
            Color main = !legal ? GuideRed : open ? GuideYellow : GuideWhite;
            var d = new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle));
            float len = Vector2.Dot(ghost - cue, d);
            Show(_ghostRing, len > PoolDims.BallR);
            if (len > PoolDims.BallR)
            {
                _ghostRing.localPosition = new Vector3(ghost.x, GuideY, ghost.y);
                _ghostMat.color = main;
            }
            Seg(_lnAim, cue + d * PoolDims.BallR, ghost - d * (PoolDims.BallR * 1.05f), main, len > PoolDims.BallR * 2.2f);
            bool obj = hits && hit > 0 && hit < 16 && objDir.sqrMagnitude > 1e-6f;
            if (obj)
            {
                Vector2 od = objDir.normalized;
                Vector2 o = _scratch[hit];
                Seg(_lnObj, o + od * PoolDims.BallR, o + od * 0.32f, !legal ? GuideRed : open ? GuideYellow : GuideObj, true);
            }
            else Show(_lnObj, false);
            bool cueOn = hits && cueDir.sqrMagnitude > 1e-6f;
            if (cueOn)
            {
                Vector2 cd = cueDir.normalized;
                Seg(_lnCue, ghost + cd * PoolDims.BallR, ghost + cd * 0.2f, GuideCue, true);
            }
            else Show(_lnCue, false);
        }

        static void Seg(LineRenderer lr, Vector2 a, Vector2 b, Color c, bool on)
        {
            Show(lr, on);
            if (!on) return;
            lr.SetPosition(0, new Vector3(a.x, GuideY, a.y));
            lr.SetPosition(1, new Vector3(b.x, GuideY, b.y));
            lr.sharedMaterial.color = c;
        }

        static void Show(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }
    }
}
