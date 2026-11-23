using HideAndSeek.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Live preview in the middle of the pose wheel: a copy of the local player's character (same skin, also a
    /// downloaded Minecraft skin or the photo face) sitting on a small model of the same seat, posed through the same
    /// code as in the game (<see cref="BlockyCharacterActions.SetSeatedPreview"/>, i.e. <see cref="SeatPoses"/>), rendered
    /// by its own camera into a <see cref="RenderTexture"/>. Everything lives on its own layer
    /// (<see cref="Layer"/>) far below the map: no game camera or mirror draws it (the bit is also removed from their
    /// culling masks while the stage exists), no scene light (flashlights, the sun) lights it - the stage has its own
    /// key and rim lights - and fog is switched off while its camera renders. Built when the wheel opens, destroyed
    /// when it closes (and with the scene).
    /// <para>
    /// Thumbnail mode (<see cref="CreateThumbs"/>): a second stage on <see cref="ThumbLayer"/> with a still camera that
    /// renders a given pose once into a given texture (<see cref="RenderPose"/>), for the pictures in the wheel's slices.
    /// </para>
    /// </summary>
    public sealed class PosePreviewStage : MonoBehaviour
    {
        /// <summary>Unnamed, otherwise unused layer (TextureBaker uses 30, the can studio 31).</summary>
        public const int Layer = 29;
        /// <summary>Layer of the thumbnail stage (unnamed, otherwise unused): its lights must not light the live copy.</summary>
        public const int ThumbLayer = 28;
        private static readonly Vector3 Origin = new Vector3(0f, -3000f, 0f);

        private int _layer = Layer, _mask = 1 << Layer;
        private bool _thumbs;
        private BlockyCharacterAnimator _animator;

        private Camera _cam;
        private RenderTexture _rt;
        private BlockyCharacter _character;
        private BlockyCharacterActions _actions;
        private SeatShape _shape;
        private Vector3 _focus;
        private float _dist, _time, _yaw0;
        private bool _spin;
        private int _layerFrames;
        private bool _fogWas;

        public RenderTexture Texture => _rt;

        /// <summary>Builds the stage for <paramref name="seat"/> (null = generic seat) with the look of <paramref name="source"/>.</summary>
        public static PosePreviewStage Create(LobbySeat seat, BlockyCharacter source, byte pose, int size)
        {
            var go = new GameObject("PosePreviewStage");
            go.layer = Layer;
            go.transform.position = Origin;
            var st = go.AddComponent<PosePreviewStage>();
            st.Build(seat, source, pose, size);
            return st;
        }

        /// <summary>Thumbnail stage: nothing is drawn until <see cref="RenderPose"/> is called.</summary>
        public static PosePreviewStage CreateThumbs(LobbySeat seat, BlockyCharacter source, byte pose)
        {
            var go = new GameObject("PoseThumbStage");
            go.layer = ThumbLayer;
            go.transform.position = Origin + new Vector3(200f, 0f, 0f);
            var st = go.AddComponent<PosePreviewStage>();
            st._layer = ThumbLayer;
            st._mask = 1 << ThumbLayer;
            st._thumbs = true;
            st.Build(seat, source, pose, 0);
            return st;
        }

        private void Build(LobbySeat seat, BlockyCharacter source, byte pose, int size)
        {
            _shape = seat != null ? seat.Shape : SeatShape.Generic();
            BuildSeat(seat);
            BuildFloor();

            // The player's copy: the root is the seat point (seat space = character space).
            int skin = source != null ? source.SkinIndex : 0;
            string nick = source != null ? source.PlayerNick : null;
            _character = BlockyCharacterFactory.Create(transform, skin, nick);
            if (source != null && source.CustomSkin != null) _character.SetCustomSkin(source.CustomSkin);
            _character.SetFirstPersonMode(false);
            _actions = _character.Actions;
            if (_actions != null) _actions.SetSeatedPreview(_shape, pose);
            _animator = _character.GetComponent<BlockyCharacterAnimator>();
            _character.SetLayer(_layer);

            // Lights (only this layer).
            AddLight("Key", new Vector3(38f, 150f, 0f), new Color(1f, 0.95f, 0.88f), 1.05f, true);
            AddLight("Rim", new Vector3(20f, -25f, 0f), new Color(0.62f, 0.78f, 1f), 0.75f, false);
            AddLight("Fill", new Vector3(60f, 230f, 0f), new Color(0.85f, 0.88f, 1f), 0.3f, false);

            // Camera.
            if (!_thumbs)
            {
                _rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { name = "PosePreview", antiAliasing = 4 };
                _rt.Create();
            }
            var camGo = new GameObject("PreviewCam");
            camGo.layer = _layer;
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.cullingMask = _mask;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.075f, 0.082f, 0.095f, 1f);
            _cam.fieldOfView = 24f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 20f;
            _cam.allowHDR = false;
            _cam.allowMSAA = true;
            _cam.useOcclusionCulling = false;
            _cam.depth = -50f;
            _cam.targetTexture = _rt;
            _cam.enabled = !_thumbs;

            // Framing: the seated body (sofas: a bit of the cushion next to it). Pouf turns round, a sofa swings in
            // front of the backrest (from behind only the backrest would show).
            bool sofa = _shape.Kind == SeatKind.Sofa;
            _focus = new Vector3(0f, 0.68f, sofa ? -0.12f : -0.08f);
            _dist = _thumbs ? 4.3f : 3.9f;
            _spin = !_shape.HasBack && !_thumbs;
            _yaw0 = sofa ? -_shape.ArmSide * (_thumbs ? 28f : 12f) : 25f;
            PlaceCamera();

            Camera.onPreCull += OnAnyPreCull;
            Camera.onPostRender += OnAnyPostRender;
            HideFromOtherCameras();
            // Scene lights (the sun, lamps, flashlights) never light the copy.
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (l != null && !l.transform.IsChildOf(transform) && (l.cullingMask & _mask) != 0) l.cullingMask &= ~_mask;
        }

        private void AddLight(string name, Vector3 euler, Color color, float intensity, bool shadows)
        {
            var lg = new GameObject(name);
            lg.layer = _layer;
            lg.transform.SetParent(transform, false);
            lg.transform.rotation = Quaternion.Euler(euler);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.cullingMask = _mask;
            l.renderMode = LightRenderMode.ForcePixel;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.55f;
            l.shadowBias = 0.03f;
            l.shadowNormalBias = 0.25f;
        }

        // ------------------------------------------------------------------ seat model

        /// <summary>
        /// The seat under the copy: the real furniture's meshes (<see cref="CopyRealSeat"/>), else a small model from its
        /// solids (<see cref="SeatShape"/>, seat space = the stage root), each part with the material of the real piece
        /// of furniture it sits in.
        /// </summary>
        private void BuildSeat(LobbySeat seat)
        {
            if (CopyRealSeat(seat)) return;
            Renderer[] real = seat != null && seat.transform.parent != null ? seat.transform.parent.GetComponentsInChildren<Renderer>() : new Renderer[0];
            Material fallback = null;
            foreach (var r in real)
                if (r != null && r.sharedMaterial != null) { fallback = r.sharedMaterial; break; }
            if (fallback == null) fallback = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.2f, 0.15f) };

            var solids = _shape.Solids;
            for (int i = 0; i < solids.Length; i++)
            {
                var s = solids[i];
                // Not furniture: the block standing for the other sofa seat's sitter.
                if (!s.Cylinder && s.Half.y >= 0.99f) continue;
                GameObject g;
                if (s.Cylinder)
                {
                    g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    float hh = Mathf.Max(s.Half.y + s.Puff * 0.5f, 0.006f);
                    g.transform.localScale = new Vector3(s.Half.x * 2f, hh, s.Half.x * 2f);
                }
                else
                {
                    g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    g.transform.localScale = (s.Half + Vector3.one * s.Puff * 0.6f) * 2f;
                }
                Destroy(g.GetComponent<Collider>());
                g.name = "SeatPart";
                g.layer = _layer;
                g.transform.SetParent(transform, false);
                g.transform.localPosition = s.Center;
                g.transform.localRotation = s.Rot;
                var mr = g.GetComponent<MeshRenderer>();
                mr.sharedMaterial = MaterialAt(real, seat, s.Center) ?? fallback;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        /// <summary>
        /// The real piece of furniture (sofa / pouf meshes from LobbySeatModels) copied into the stage in seat space,
        /// so the preview shows exactly the seat the player sits on. False when there is nothing to copy (or the meshes
        /// were merged by static batching), then the seat is built from its solids.
        /// </summary>
        private bool CopyRealSeat(LobbySeat seat)
        {
            if (seat == null || seat.transform.parent == null) return false;
            var st = seat.transform;
            int n = 0;
            foreach (var mf in st.parent.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || mf.GetComponentInParent<LobbySeat>() != null) continue;
                if (mf.sharedMesh.name.StartsWith("Combined Mesh")) return false;
                var g = new GameObject("SeatPart");
                g.layer = _layer;
                g.transform.SetParent(transform, false);
                g.transform.localPosition = st.InverseTransformPoint(mf.transform.position);
                g.transform.localRotation = Quaternion.Inverse(st.rotation) * mf.transform.rotation;
                g.transform.localScale = mf.transform.lossyScale;
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                n++;
            }
            return n > 0;
        }

        /// <summary>Material of the smallest real renderer of the furniture whose bounds hold this seat-space point.</summary>
        private static Material MaterialAt(Renderer[] real, LobbySeat seat, Vector3 local)
        {
            if (seat == null) return null;
            Vector3 world = seat.transform.TransformPoint(local);
            Material best = null;
            float bestVol = float.MaxValue;
            foreach (var r in real)
            {
                if (r == null || r.sharedMaterial == null || !(r is MeshRenderer)) continue;
                var b = r.bounds;
                b.Expand(0.01f);
                if (!b.Contains(world)) continue;
                float vol = b.size.x * b.size.y * b.size.z;
                if (vol < bestVol) { bestVol = vol; best = r.sharedMaterial; }
            }
            return best;
        }

        /// <summary>Soft dark spot under the seat (grounds the model on the plain background).</summary>
        private void BuildFloor()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PreviewSpot" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f).magnitude;
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color32(0, 0, 0, (byte)(a * a * 200f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Spot";
            q.layer = _layer;
            q.transform.SetParent(transform, false);
            q.transform.localPosition = new Vector3(0f, 0.002f, -0.15f);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = new Vector3(_shape.Kind == SeatKind.Sofa ? 2.6f : 1.8f, 1.6f, 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(shader) { mainTexture = tex, name = "PreviewSpot" };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ------------------------------------------------------------------ runtime

        /// <summary>Shows pose <paramref name="pose"/> (moves there the same way as in the game).</summary>
        public void ShowPose(byte pose)
        {
            if (_actions != null) _actions.SetSeatedPreview(_shape, pose);
        }

        /// <summary>
        /// Thumbnail stage: puts the copy straight into <paramref name="pose"/> and renders it once into
        /// <paramref name="target"/> (same posing code as the live preview and the game).
        /// </summary>
        public void RenderPose(byte pose, RenderTexture target)
        {
            if (_cam == null || target == null || _actions == null) return;
            _actions.SetSeatedPreview(_shape, pose, true);
            if (_animator != null) _animator.Tick(1f / 60f);
            _character.SetLayer(_layer);
            _cam.targetTexture = target;
            _cam.Render();
            _cam.targetTexture = null;
        }

        private void LateUpdate()
        {
            if (_thumbs) return;
            _time += Time.unscaledDeltaTime;
            PlaceCamera();
            // Props the action layer creates later (held items) join the layer too.
            if (_layerFrames < 30 && _character != null)
            {
                _layerFrames++;
                _character.SetLayer(_layer);
            }
            HideFromOtherCameras();
        }

        private void PlaceCamera()
        {
            if (_cam == null) return;
            float yaw = _thumbs ? _yaw0 : _spin ? _yaw0 + _time * 22f : _yaw0 + 55f * Mathf.Sin(_time * 0.42f);
            // Seat space: the sitter faces +Z, so the camera starts in front of the seat.
            Quaternion q = Quaternion.Euler(_thumbs ? 11f : 9f, 180f + yaw, 0f);
            Vector3 local = _focus - q * Vector3.forward * _dist;
            _cam.transform.position = transform.TransformPoint(local);
            _cam.transform.rotation = transform.rotation * q;
        }

        private void HideFromOtherCameras()
        {
            foreach (var c in Camera.allCameras)
                if (c != null && c != _cam && (c.cullingMask & _mask) != 0) c.cullingMask &= ~_mask;
        }

        private void OnAnyPreCull(Camera c)
        {
            if (c != _cam) return;
            _fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
        }

        private void OnAnyPostRender(Camera c)
        {
            if (c != _cam) return;
            RenderSettings.fog = _fogWas;
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= OnAnyPreCull;
            Camera.onPostRender -= OnAnyPostRender;
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                if (mr != null && mr.name == "Spot" && mr.sharedMaterial != null)
                {
                    Destroy(mr.sharedMaterial.mainTexture);
                    Destroy(mr.sharedMaterial);
                }
        }
    }
}
