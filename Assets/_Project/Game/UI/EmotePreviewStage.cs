using HideAndSeek.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Picture source of the emote wheel: a copy of the local player's character (own skin, Minecraft skin, photo face)
    /// standing on a soft floor spot, plus a partner copy for pair emotes, posed by the same emote code as in the game
    /// (<see cref="BlockyCharacterActions.SetEmotePreview"/>). Same isolation as <see cref="PosePreviewStage"/>: own
    /// layer far below the map, own lights, no fog, hidden from every other camera. Live mode loops the shown emote in
    /// the wheel's middle; thumbnail mode renders one still frame per call (<see cref="RenderEmote"/>).
    /// </summary>
    public sealed class EmotePreviewStage : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(400f, -3000f, 0f);

        private int _layer, _mask;
        private bool _thumbs;
        private Camera _cam;
        private RenderTexture _rt;
        private BlockyCharacter _me, _partner;
        private BlockyCharacterAnimator _meAnim, _partnerAnim;
        private EmoteId _shown;
        private float _clock, _time;
        private bool _fogWas;
        private Transform _lights;

        public RenderTexture Texture => _rt;

        public static EmotePreviewStage Create(BlockyCharacter source, int partnerSkin, BlockyCharacter partnerSource, int size, bool thumbs)
        {
            var go = new GameObject(thumbs ? "EmoteThumbStage" : "EmotePreviewStage");
            var st = go.AddComponent<EmotePreviewStage>();
            st._thumbs = thumbs;
            st._layer = thumbs ? PosePreviewStage.ThumbLayer : PosePreviewStage.Layer;
            st._mask = 1 << st._layer;
            go.layer = st._layer;
            go.transform.position = Origin + (thumbs ? new Vector3(200f, 0f, 0f) : Vector3.zero);
            st.Build(source, partnerSkin, partnerSource, size);
            return st;
        }

        private BlockyCharacter Copy(BlockyCharacter source, int skin, string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            var c = BlockyCharacterFactory.Create(root, source != null ? source.SkinIndex : skin, source != null ? source.PlayerNick : null);
            if (source != null && source.CustomSkin != null) c.SetCustomSkin(source.CustomSkin);
            c.SetFirstPersonMode(false);
            c.SetLayer(_layer);
            return c;
        }

        private void Build(BlockyCharacter source, int partnerSkin, BlockyCharacter partnerSource, int size)
        {
            _me = Copy(source, 0, "Me");
            _partner = Copy(partnerSource, partnerSkin, "Partner");
            _meAnim = _me.GetComponent<BlockyCharacterAnimator>();
            _partnerAnim = _partner.GetComponent<BlockyCharacterAnimator>();
            if (_meAnim != null) _meAnim.idleLookAround = false;
            if (_partnerAnim != null) _partnerAnim.idleLookAround = false;
            _me.Actions.SetEmotePartner(_partner, 0);
            _partner.Actions.SetEmotePartner(_me, 1);
            _partner.transform.parent.gameObject.SetActive(false);
            BuildFloor();

            _lights = new GameObject("Lights") { layer = _layer }.transform;
            _lights.SetParent(transform, false);
            AddLight("Key", new Vector3(36f, 150f, 0f), new Color(1f, 0.95f, 0.88f), 1.05f, true);
            AddLight("Rim", new Vector3(18f, -25f, 0f), new Color(0.62f, 0.78f, 1f), 0.75f, false);
            AddLight("Fill", new Vector3(60f, 230f, 0f), new Color(0.85f, 0.88f, 1f), 0.3f, false);

            if (!_thumbs)
            {
                _rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { name = "EmotePreview", antiAliasing = 4 };
                _rt.Create();
            }
            var camGo = new GameObject("PreviewCam") { layer = _layer };
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.cullingMask = _mask;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.075f, 0.082f, 0.095f, 1f);
            _cam.fieldOfView = 24f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 25f;
            _cam.allowHDR = false;
            _cam.allowMSAA = true;
            _cam.useOcclusionCulling = false;
            _cam.depth = -50f;
            _cam.targetTexture = _rt;
            _cam.enabled = !_thumbs;

            Camera.onPreCull += OnAnyPreCull;
            Camera.onPostRender += OnAnyPostRender;
            HideFromOtherCameras();
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (l != null && !l.transform.IsChildOf(transform) && (l.cullingMask & _mask) != 0) l.cullingMask &= ~_mask;
            Show(EmoteId.None);
        }

        private void AddLight(string name, Vector3 euler, Color color, float intensity, bool shadows)
        {
            var lg = new GameObject(name) { layer = _layer };
            lg.transform.SetParent(_lights, false);
            lg.transform.localRotation = Quaternion.Euler(euler);
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

        private void BuildFloor()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "EmoteSpot" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f).magnitude;
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color32(0, 0, 0, (byte)(a * a * 190f));
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
            q.transform.localPosition = new Vector3(0f, 0.002f, 0.4f);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = new Vector3(2.6f, 2.6f, 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(shader) { mainTexture = tex, name = "EmoteSpot" };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ------------------------------------------------------------------ runtime

        /// <summary>Live mode: loops <paramref name="id"/> (None = standing idle).</summary>
        public void Show(EmoteId id)
        {
            _shown = id;
            _clock = 0f;
            Place(id);
            _me.Actions.SetEmotePreview(id, 0f, false);
            if (Emotes.IsPair(id)) _partner.Actions.SetEmotePreview(id, 0f, false);
        }

        private void Place(EmoteId id)
        {
            var info = Emotes.Get(id);
            bool pair = info != null && info.IsPair;
            _partner.transform.parent.gameObject.SetActive(pair);
            _me.transform.parent.localPosition = Vector3.zero;
            _me.transform.parent.localRotation = Quaternion.identity;
            if (pair)
            {
                _partner.transform.parent.localPosition = new Vector3(0f, 0f, info.PairDistance);
                _partner.transform.parent.localRotation = Quaternion.Euler(0f, 180f, 0f);
                if (!_thumbs) _partner.SetLayer(_layer);
            }
            PlaceCamera(id);
        }

        private void PlaceCamera(EmoteId id)
        {
            var info = Emotes.Get(id);
            bool pair = info != null && info.IsPair;
            bool low = id == EmoteId.SitDown;
            Vector3 focus = pair ? new Vector3(0f, 1.08f, info.PairDistance * 0.5f) : new Vector3(0f, low ? 0.55f : 1.0f, low ? 0.05f : 0f);
            if (_lights != null) _lights.localRotation = Quaternion.Euler(0f, pair ? 68f : 0f, 0f);
            float dist = pair ? (_thumbs ? 5.0f : 5.6f) : low ? (_thumbs ? 3.3f : 3.9f) : (_thumbs ? 4.3f : 5.0f);
            float swing = _thumbs ? 0f : Mathf.Sin(_time * 0.5f);
            // Solo: in front, a little to the side. Pair: from the side, slightly in front of the own copy.
            Quaternion q = pair ? Quaternion.Euler(8f, 248f + 10f * swing, 0f) : Quaternion.Euler(low ? 14f : 6f, 180f + (_thumbs ? 22f : 18f * swing), 0f);
            _cam.transform.position = transform.TransformPoint(focus - q * Vector3.forward * dist);
            _cam.transform.rotation = transform.rotation * q;
        }

        /// <summary>Thumbnail mode: <paramref name="id"/> at its picture moment, rendered once into <paramref name="target"/>.</summary>
        public void RenderEmote(EmoteId id, RenderTexture target)
        {
            var info = Emotes.Get(id);
            if (_cam == null || target == null || info == null) return;
            Place(id);
            _me.Actions.SetEmotePreview(id, info.ThumbTime, true);
            if (info.IsPair) _partner.Actions.SetEmotePreview(id, info.ThumbTime, true);
            // Two ticks: the partner's torso (read by the hug) is in place before the second one.
            for (int i = 0; i < 2; i++)
            {
                if (_meAnim != null) _meAnim.Tick(1f / 60f);
                if (info.IsPair && _partnerAnim != null) _partnerAnim.Tick(1f / 60f);
            }
            _me.SetLayer(_layer);
            _partner.SetLayer(_layer);
            _cam.targetTexture = target;
            _cam.Render();
            _cam.targetTexture = null;
        }

        private void LateUpdate()
        {
            if (_thumbs) return;
            float dt = Time.unscaledDeltaTime;
            _time += dt;
            _clock += dt;
            var info = Emotes.Get(_shown);
            // One-shots replay after a short pause (the layer fades out at the end on its own).
            if (info != null && !info.Loops && _clock > info.Duration + 0.6f) Show(_shown);
            PlaceCamera(_shown);
            HideFromOtherCameras();
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
