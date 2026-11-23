using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// A leg cut off by the chainsaw as a loose object (lying on the floor, in the hand, thrown): the player's own
    /// upper and lower leg meshes (same skin and trousers), a raw flesh cap on the cut and a short stub of bone in it.
    /// Leg space: origin = middle of the cut (the hip end), the leg hangs along -Y, the toes point +Z.
    /// </summary>
    public static class SeveredLegModel
    {
        const float Px = BlockyCharacter.PixelSize;
        /// <summary>Hip end to sole.</summary>
        public const float Length = 12f * Px;
        /// <summary>The fist round the shin just above the foot (where it is held and thrown from).</summary>
        public static readonly Vector3 AnkleGrip = new Vector3(0f, -10f * Px, 0f);
        /// <summary>Middle of the leg's box.</summary>
        public static readonly Vector3 Center = new Vector3(0f, -6f * Px, 0f);
        /// <summary>Renderers wearing the owner's skin are named so (re-skinned when the owner is known later).</summary>
        public const string SkinPartName = "LegSkin";

        static Material _bone, _fallbackStump;

        /// <summary>
        /// Builds the leg under <paramref name="parent"/> (local origin = the cut). <paramref name="skin"/> = the owner's
        /// skin material (null = plain grey), <paramref name="stump"/> = raw flesh for the cut (null = built-in dark red).
        /// </summary>
        public static GameObject Create(Transform parent, Material skin, Material stump, bool left, int layer = 0)
        {
            var root = new GameObject("SeveredLeg");
            root.layer = layer;
            root.transform.SetParent(parent, false);

            var upper = Part(root.transform, left ? BlockyMeshBuilder.Part.UpperLegLeft : BlockyMeshBuilder.Part.UpperLegRight, Vector3.zero, skin, layer);
            Part(upper, left ? BlockyMeshBuilder.Part.LowerLegLeft : BlockyMeshBuilder.Part.LowerLegRight, BlockyCharacter.KneeRest, skin, layer);

            // Raw cut: a flesh cap a little proud of the top face, a ring of darker meat and the bone in the middle.
            float w = 4f * Px;
            Box(root.transform, "Cut", new Vector3(0f, 0.008f, 0f), new Vector3(w - 0.006f, 0.022f, w - 0.006f), stump != null ? stump : FallbackStump, layer);
            Box(root.transform, "CutRim", new Vector3(0f, -0.012f, 0f), new Vector3(w + 0.004f, 0.026f, w + 0.004f), stump != null ? stump : FallbackStump, layer);
            Box(root.transform, "Bone", new Vector3(0.006f, 0.032f, -0.004f), new Vector3(0.05f, 0.04f, 0.046f), Bone, layer);
            Box(root.transform, "BoneMarrow", new Vector3(0.006f, 0.053f, -0.004f), new Vector3(0.026f, 0.004f, 0.024f), stump != null ? stump : FallbackStump, layer);
            return root;
        }

        /// <summary>
        /// How the leg is carried (<see cref="HandGrip"/>): by the ankle, hanging at the side with the cut end down near
        /// the floor (swinging with the walk); first person: held up like a club in the lower right, cut end forward-up.
        /// </summary>
        public static HandGrip MakeGrip(System.Func<Transform, GameObject> model) => new HandGrip
        {
            Model = model,
            Grip = AnkleGrip,
            Hang = true,
            Rotation = new Vector3(0f, 0f, 180f),
            FpGrip = AnkleGrip,
            FpHand = new Vector3(0.31f, -0.38f, 0.56f),
            FpRotation = HandGrip.Aim(Vector3.up, new Vector3(0.12f, 0.5f, 0.86f), Vector3.forward, Vector3.right),
            FpScale = 0.72f,
        };

        /// <summary>Puts the owner's skin on a leg built earlier (e.g. once the owner's character is known).</summary>
        public static void SetSkin(GameObject leg, Material skin)
        {
            if (leg == null || skin == null) return;
            foreach (var r in leg.GetComponentsInChildren<MeshRenderer>(true))
                if (r.gameObject.name == SkinPartName && r.sharedMaterial != skin) r.sharedMaterial = skin;
        }

        static Transform Part(Transform parent, BlockyMeshBuilder.Part part, Vector3 local, Material skin, int layer)
        {
            var go = new GameObject(SkinPartName);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.AddComponent<MeshFilter>().sharedMesh = BlockyMeshBuilder.GetMesh(part);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = skin != null ? skin : FallbackStump;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go.transform;
        }

        static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, int layer)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.layer = layer;
            var col = g.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Bone => _bone != null ? _bone : _bone = Lit("SeveredLegBone", new Color(0.86f, 0.8f, 0.68f), 0.35f);
        static Material FallbackStump => _fallbackStump != null ? _fallbackStump : _fallbackStump = Lit("SeveredLegFlesh", new Color(0.5f, 0.05f, 0.05f), 0.6f);

        static Material Lit(string name, Color c, float gloss)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse")) { name = name, hideFlags = HideFlags.DontSave, color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            return m;
        }
    }
}
