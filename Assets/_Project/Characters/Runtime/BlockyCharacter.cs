using System;
using System.Collections.Generic;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Minecraft-style blocky character: builds the joint hierarchy with shared box meshes,
    /// applies skins and handles first-person (shadow only) rendering.
    /// The GameObject origin is at the feet, the character faces +Z.
    /// </summary>
    /// <remarks>
    /// Hierarchy (joints carry their own mesh, pivots at the joints):
    /// Root -> Pelvis -> { Torso -> { BodyMesh, Neck(Head), ShoulderR -> ElbowR, ShoulderL -> ElbowL },
    ///                     HipR -> KneeR, HipL -> KneeL }
    /// </remarks>
    [DisallowMultipleComponent]
    public class BlockyCharacter : MonoBehaviour
    {
        public const float PixelSize = BlockyMeshBuilder.Px;
        public const float HipHeight = 12f * PixelSize;
        public const float Height = 32f * PixelSize;

        // Rest positions of joints (metres, relative to their parent joint).
        public static readonly Vector3 PelvisRest = new Vector3(0f, HipHeight, 0f);
        public static readonly Vector3 NeckRest = new Vector3(0f, 12f * PixelSize, 0f);
        public static readonly Vector3 ShoulderRightRest = new Vector3(6f * PixelSize, 10f * PixelSize, 0f);
        public static readonly Vector3 ShoulderLeftRest = new Vector3(-6f * PixelSize, 10f * PixelSize, 0f);
        public static readonly Vector3 ElbowRest = new Vector3(0f, -4f * PixelSize, 0f);
        public static readonly Vector3 HipRightRest = new Vector3(2f * PixelSize, 0f, 0f);
        public static readonly Vector3 HipLeftRest = new Vector3(-2f * PixelSize, 0f, 0f);
        public static readonly Vector3 KneeRest = new Vector3(0f, -6f * PixelSize, 0f);

        [SerializeField, HideInInspector] bool built;

        [SerializeField, HideInInspector] Transform pelvis, torso, bodyMesh, head, faceQuad;
        [SerializeField, HideInInspector] Transform shoulderR, elbowR, shoulderL, elbowL;
        [SerializeField, HideInInspector] Transform hipR, kneeR, hipL, kneeL;
        [SerializeField, HideInInspector] List<Renderer> skinRenderers = new List<Renderer>();
        [SerializeField, HideInInspector] MeshRenderer faceRenderer;

        int skinIndex;
        string playerNick;
        bool firstPerson;

        static Material fallbackMaterial;
        static Material photoMaterial;
        static bool photoSearched;

        public Transform Head => head;
        public Transform Pelvis => pelvis;
        public Transform Torso => torso;
        public Transform BodyMesh => bodyMesh;
        public Transform ShoulderRight => shoulderR;
        public Transform ElbowRight => elbowR;
        public Transform ShoulderLeft => shoulderL;
        public Transform ElbowLeft => elbowL;
        public Transform HipRight => hipR;
        public Transform KneeRight => kneeR;
        public Transform HipLeft => hipL;
        public Transform KneeLeft => kneeL;

        BlockyCharacterActions actions;

        /// <summary>Action layer (ICharacterActions) added by BlockyCharacterFactory; null if absent.</summary>
        public BlockyCharacterActions Actions => actions != null ? actions : (actions = GetComponent<BlockyCharacterActions>());

        public bool IsBuilt => built && pelvis != null;

        /// <summary>The rig is driven by physics (<see cref="BlockyRagdoll"/>): the animator leaves the joints alone.</summary>
        public bool IsRagdoll { get; internal set; }
        public int SkinIndex => skinIndex;
        public string PlayerNick => playerNick;
        public bool IsFirstPerson => firstPerson;

        /// <summary>True when the special photo face is currently shown.</summary>
        public bool HasPhotoFace => faceQuad != null && faceQuad.gameObject.activeSelf;

        /// <summary>Creates the joint hierarchy and renderers. Safe to call more than once.</summary>
        public void Build()
        {
            if (IsBuilt) return;

            skinRenderers.Clear();
            pelvis = CreateJoint("Pelvis", transform, PelvisRest, null);
            torso = CreateJoint("Torso", pelvis, Vector3.zero, null);
            bodyMesh = CreateJoint("BodyMesh", torso, Vector3.zero, BlockyMeshBuilder.Part.Body);
            head = CreateJoint("Head", torso, NeckRest, BlockyMeshBuilder.Part.Head);
            shoulderR = CreateJoint("ShoulderR", torso, ShoulderRightRest, BlockyMeshBuilder.Part.UpperArmRight);
            elbowR = CreateJoint("ElbowR", shoulderR, ElbowRest, BlockyMeshBuilder.Part.LowerArmRight);
            shoulderL = CreateJoint("ShoulderL", torso, ShoulderLeftRest, BlockyMeshBuilder.Part.UpperArmLeft);
            elbowL = CreateJoint("ElbowL", shoulderL, ElbowRest, BlockyMeshBuilder.Part.LowerArmLeft);
            hipR = CreateJoint("HipR", pelvis, HipRightRest, BlockyMeshBuilder.Part.UpperLegRight);
            kneeR = CreateJoint("KneeR", hipR, KneeRest, BlockyMeshBuilder.Part.LowerLegRight);
            hipL = CreateJoint("HipL", pelvis, HipLeftRest, BlockyMeshBuilder.Part.UpperLegLeft);
            kneeL = CreateJoint("KneeL", hipL, KneeRest, BlockyMeshBuilder.Part.LowerLegLeft);

            // Photo face: a quad just in front of the head's front face (behind the hat layer so the fringe overlaps it).
            faceQuad = new GameObject("PhotoFace").transform;
            faceQuad.SetParent(head, false);
            faceQuad.localPosition = new Vector3(0f, 4f * PixelSize, 4.04f * PixelSize);
            faceQuad.localRotation = Quaternion.identity;
            faceQuad.localScale = new Vector3(8f * PixelSize, 8f * PixelSize, 1f);
            faceQuad.gameObject.layer = gameObject.layer;
            faceQuad.gameObject.AddComponent<MeshFilter>().sharedMesh = BlockyMeshBuilder.GetFaceQuad();
            faceRenderer = faceQuad.gameObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(faceRenderer);
            faceQuad.gameObject.SetActive(false);

            built = true;
        }

        void Awake()
        {
            // Meshes are not serialized (DontSave); rebuild references if this object was duplicated/loaded.
            if (IsBuilt) RestoreMeshes();
        }

        Transform CreateJoint(string jointName, Transform parent, Vector3 localPos, BlockyMeshBuilder.Part? part)
        {
            var t = new GameObject(jointName).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.identity;
            t.gameObject.layer = gameObject.layer;
            if (part.HasValue)
            {
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = BlockyMeshBuilder.GetMesh(part.Value);
                var r = t.gameObject.AddComponent<MeshRenderer>();
                ConfigureRenderer(r);
                skinRenderers.Add(r);
            }

            return t;
        }

        void RestoreMeshes()
        {
            void Fix(Transform t, BlockyMeshBuilder.Part p)
            {
                if (t == null) return;
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == null) mf.sharedMesh = BlockyMeshBuilder.GetMesh(p);
            }

            Fix(bodyMesh, BlockyMeshBuilder.Part.Body);
            Fix(head, BlockyMeshBuilder.Part.Head);
            Fix(shoulderR, BlockyMeshBuilder.Part.UpperArmRight);
            Fix(elbowR, BlockyMeshBuilder.Part.LowerArmRight);
            Fix(shoulderL, BlockyMeshBuilder.Part.UpperArmLeft);
            Fix(elbowL, BlockyMeshBuilder.Part.LowerArmLeft);
            Fix(hipR, BlockyMeshBuilder.Part.UpperLegRight);
            Fix(kneeR, BlockyMeshBuilder.Part.LowerLegRight);
            Fix(hipL, BlockyMeshBuilder.Part.UpperLegLeft);
            Fix(kneeL, BlockyMeshBuilder.Part.LowerLegLeft);
            if (faceQuad != null)
            {
                var mf = faceQuad.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == null) mf.sharedMesh = BlockyMeshBuilder.GetFaceQuad();
            }
        }

        void ConfigureRenderer(Renderer r)
        {
            r.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.allowOcclusionWhenDynamic = true;
        }

        /// <summary>
        /// Applies a skin. The index wraps around the library. If <paramref name="playerNick"/> equals
        /// <see cref="GameLayers.SpecialFaceNick"/> and the photo resource exists, the photo face is shown.
        /// </summary>
        public void SetSkin(int skinIndex, string playerNick = null)
        {
            if (!IsBuilt) Build();
            this.skinIndex = skinIndex;
            this.playerNick = playerNick;

            var skin = SkinLibrary.GetSkin(skinIndex);
            var mat = skin != null && skin.Material != null ? skin.Material : GetFallbackMaterial();
            if (customSkin != null) mat = CustomSkins.MaterialFor(customSkin, mat);
            foreach (var r in skinRenderers)
                if (r != null) r.sharedMaterial = mat;

            bool special = IsSpecialNick(playerNick);
            var photo = special ? GetPhotoMaterial() : null;
            if (photo != null)
            {
                faceRenderer.sharedMaterial = photo;
                faceQuad.gameObject.SetActive(true);
            }
            else
            {
                faceQuad.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// First-person mode for the local player: all parts render as ShadowCastingMode.ShadowsOnly, so the
        /// player sees their own shadow but never the model clipping the camera.
        /// </summary>
        private Texture2D customSkin;

        /// <summary>Uses a downloaded 64x64 Minecraft skin instead of the library skin (null = back to the library).</summary>
        public void SetCustomSkin(Texture2D tex)
        {
            customSkin = tex;
            SetSkin(skinIndex, playerNick);
        }

        public void SetFirstPersonMode(bool fp)
        {
            if (!IsBuilt) Build();
            firstPerson = fp;
            foreach (var r in skinRenderers)
                if (r != null) r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            if (faceRenderer != null)
                faceRenderer.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        // ------------------------------------------------------------------ severed limbs

        int severedMask;

        /// <summary>Bit mask of limbs currently cut off (see <see cref="Limb"/>).</summary>
        public int SeveredMask => severedMask;

        /// <summary>Shows / hides the limbs so that exactly <paramref name="mask"/> is missing. No flying parts.</summary>
        public void SetSevered(int mask)
        {
            if (!IsBuilt || mask == severedMask) return;
            severedMask = mask;
            SetLimbVisible(hipR, kneeR, (mask & Limb.LegR) == 0);
            SetLimbVisible(hipL, kneeL, (mask & Limb.LegL) == 0);
            SetLimbVisible(shoulderR, elbowR, (mask & Limb.ArmR) == 0);
            SetLimbVisible(shoulderL, elbowL, (mask & Limb.ArmL) == 0);
            SetLimbVisible(head, null, (mask & Limb.Head) == 0);
            if (faceQuad != null && (mask & Limb.Head) != 0) faceQuad.gameObject.SetActive(false);
            else if (faceQuad != null && IsSpecialNick(playerNick) && GetPhotoMaterial() != null) faceQuad.gameObject.SetActive(true);
            // Lying as a ragdoll: the missing part loses its body / collider, a returned one gets it back.
            if (IsRagdoll && TryGetComponent<BlockyRagdoll>(out var ragdoll)) ragdoll.OnSeveredChanged();
        }

        static void SetLimbVisible(Transform a, Transform b, bool visible)
        {
            if (a != null && a.TryGetComponent<MeshRenderer>(out var ra)) ra.enabled = visible;
            if (b != null && b.TryGetComponent<MeshRenderer>(out var rb)) rb.enabled = visible;
        }

        /// <summary>The flying part spawned by the last <see cref="Sever"/> call (null when none).</summary>
        public GameObject LastGib { get; private set; }

        /// <summary>Joint transform (hip / shoulder / head) of a limb.</summary>
        public Transform LimbJoint(int limb) => limb switch
        {
            Limb.LegR => hipR,
            Limb.LegL => hipL,
            Limb.ArmR => shoulderR,
            Limb.ArmL => shoulderL,
            _ => head,
        };

        /// <summary>World position of the stump (joint) of a limb.</summary>
        public Vector3 LimbJointPosition(int limb)
        {
            Transform t = limb switch
            {
                Limb.LegR => hipR,
                Limb.LegL => hipL,
                Limb.ArmR => shoulderR,
                Limb.ArmL => shoulderL,
                _ => head,
            };
            return t != null ? t.position : transform.position;
        }

        /// <summary>
        /// Cuts off one limb: hides it and lets a physical copy fly away along <paramref name="impulse"/> (m/s).
        /// Returns false when it was already missing. The part disappears after ~40 s.
        /// </summary>
        public bool Sever(int limb, Vector3 impulse)
        {
            LastGib = null;
            if (!IsBuilt || (severedMask & limb) != 0) return false;
            Transform a, b = null;
            BlockyMeshBuilder.Part pa, pb = default;
            bool isHead = false;
            switch (limb)
            {
                case Limb.LegR: a = hipR; b = kneeR; pa = BlockyMeshBuilder.Part.UpperLegRight; pb = BlockyMeshBuilder.Part.LowerLegRight; break;
                case Limb.LegL: a = hipL; b = kneeL; pa = BlockyMeshBuilder.Part.UpperLegLeft; pb = BlockyMeshBuilder.Part.LowerLegLeft; break;
                case Limb.ArmR: a = shoulderR; b = elbowR; pa = BlockyMeshBuilder.Part.UpperArmRight; pb = BlockyMeshBuilder.Part.LowerArmRight; break;
                case Limb.ArmL: a = shoulderL; b = elbowL; pa = BlockyMeshBuilder.Part.UpperArmLeft; pb = BlockyMeshBuilder.Part.LowerArmLeft; break;
                default: a = head; pa = BlockyMeshBuilder.Part.Head; isHead = true; break;
            }

            SpawnGib(a, pa, b, pb, isHead, impulse);
            SetSevered(severedMask | limb);
            return true;
        }

        void SpawnGib(Transform a, BlockyMeshBuilder.Part pa, Transform b, BlockyMeshBuilder.Part pb, bool isHead, Vector3 impulse)
        {
            if (a == null) return;
            Material mat = skinRenderers.Count > 0 && skinRenderers[0] != null ? skinRenderers[0].sharedMaterial : GetFallbackMaterial();
            var root = new GameObject(isHead ? "SeveredHead" : "SeveredLimb");
            LastGib = root;
            root.transform.SetPositionAndRotation(a.position, a.rotation);
            AddGibPart(root.transform, a, pa, mat);
            if (b != null) AddGibPart(root.transform, b, pb, mat);
            if (isHead && faceQuad != null && faceQuad.gameObject.activeSelf)
            {
                var f = Instantiate(faceQuad.gameObject, root.transform);
                f.transform.SetPositionAndRotation(faceQuad.position, faceQuad.rotation);
                f.transform.localScale = faceQuad.localScale;
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = isHead ? 4f : 6f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Collider col;
            if (isHead)
            {
                var sc = root.AddComponent<SphereCollider>();
                sc.radius = 4f * PixelSize;
                sc.center = new Vector3(0f, 4f * PixelSize, 0f);
                col = sc;
            }
            else
            {
                var bc = root.AddComponent<BoxCollider>();
                bc.size = new Vector3(4f * PixelSize, 11f * PixelSize, 4f * PixelSize);
                bc.center = new Vector3(0f, -5.5f * PixelSize, 0f);
                col = bc;
            }

            // Never push the players around.
            foreach (var cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                if (cc != null) Physics.IgnoreCollision(col, cc, true);
            // Cut off a body lying as a ragdoll: the part starts inside it and must not be shot out of it.
            if (IsRagdoll && TryGetComponent<BlockyRagdoll>(out var ragdoll)) ragdoll.IgnoreWith(col);
            rb.AddForce(impulse, ForceMode.VelocityChange);
            rb.AddTorque(UnityEngine.Random.onUnitSphere * 5f, ForceMode.VelocityChange);
            Destroy(root, 180f);
        }

        void AddGibPart(Transform parent, Transform joint, BlockyMeshBuilder.Part part, Material mat)
        {
            var go = new GameObject(part.ToString());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(joint.position, joint.rotation);
            go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = BlockyMeshBuilder.GetMesh(part);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        /// <summary>Sets the layer of the whole character hierarchy (ragdoll colliders keep their physics layer).</summary>
        public void SetLayer(int layer)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer != GameLayers.Ragdoll) t.gameObject.layer = layer;
        }

        public static bool IsSpecialNick(string nick)
        {
            return !string.IsNullOrEmpty(nick) &&
                   string.Equals(nick.Trim(), GameLayers.SpecialFaceNick, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Shared photo-face material, or null when the photo resource is missing (graceful fallback to the skin face).
        /// </summary>
        public static Material GetPhotoMaterial()
        {
            if (photoMaterial != null) return photoMaterial;
            if (photoSearched) return null;
            photoSearched = true;

            var tex = Resources.Load<Texture2D>(GameLayers.SpecialFaceResource);
            if (tex == null) return null;

            var lib = SkinLibrary.Load();
            Material template = lib != null ? lib.FacePhotoMaterial : null;
            photoMaterial = template != null ? new Material(template) : new Material(FindLitShader());
            photoMaterial.name = "PhotoFace (runtime)";
            photoMaterial.hideFlags = HideFlags.DontSave;
            photoMaterial.mainTexture = tex;

            // Centre-crop to a square, biased slightly towards the top where faces usually are.
            float w = tex.width, h = tex.height;
            if (w > h)
            {
                float s = h / w;
                photoMaterial.mainTextureScale = new Vector2(s, 1f);
                photoMaterial.mainTextureOffset = new Vector2((1f - s) * 0.5f, 0f);
            }
            else if (h > w)
            {
                float s = w / h;
                photoMaterial.mainTextureScale = new Vector2(1f, s);
                photoMaterial.mainTextureOffset = new Vector2(0f, (1f - s) * 0.65f);
            }

            return photoMaterial;
        }

        /// <summary>Forget the cached photo (e.g. after the user dropped the file in during an editor session).</summary>
        public static void ResetPhotoCache()
        {
            photoSearched = false;
            if (photoMaterial != null)
            {
                if (Application.isPlaying) Destroy(photoMaterial);
                else DestroyImmediate(photoMaterial);
            }

            photoMaterial = null;
        }

        static Shader FindLitShader()
        {
            var s = Shader.Find("Standard");
            return s != null ? s : Shader.Find("Diffuse");
        }

        static Material GetFallbackMaterial()
        {
            if (fallbackMaterial != null) return fallbackMaterial;
            fallbackMaterial = new Material(FindLitShader())
            {
                name = "BlockyFallback (runtime)",
                hideFlags = HideFlags.DontSave,
                color = new Color(0.8f, 0.8f, 0.8f)
            };
            return fallbackMaterial;
        }
    }
}
