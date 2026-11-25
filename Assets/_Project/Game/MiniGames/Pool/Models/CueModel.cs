using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Pool cues, 2026-11-25. CUE SPACE (see <see cref="PoolDims"/>): the cue lies along local +Z, the bottom of the
    /// rubber bumper at z = 0, the top of the tip at z = <see cref="PoolDims.CueLength"/>. Variants:
    /// 0 = one-piece maple house cue with a full splice, 1 = birdseye maple with 4 ebony points and veneers + black
    /// Irish linen, 2 = cocobolo with brass rings + natural Irish linen, 3 = ebony with mother-of-pearl inlays + black
    /// leather wrap. Every part is a turned mesh (<see cref="CueMesh"/>) with its texture drawn on the lathe UVs
    /// (<see cref="CueArt"/>), so the grain runs along the cue. No colliders (use <see cref="RadiusAt"/>).
    /// </summary>
    public static partial class CueModel
    {
        public const int Variants = 4;
        public static readonly string[] Names = { "House", "Points", "Linen", "Ebony" };

        // ---- layout (metres, cue space)
        public const float ButtR = PoolDims.ButtD * 0.5f, TipR = PoolDims.TipD * 0.5f;
        /// <summary>Radius at the joint (0.858 in joint, both halves flush).</summary>
        public const float JointR = 0.0109f;
        public const float BumperH = 0.011f, BumperR = 0.0142f, CapH = 0.023f;
        /// <summary>Tip: total height (side + dome), dome radius ("nickel" shape).</summary>
        public const float TipH = 0.0066f, DomeR = 0.0106f;
        public const float FerruleH = 0.0225f;
        /// <summary>Pro taper: the shaft keeps the tip's diameter over this length behind the ferrule.</summary>
        public const float ParallelLen = 0.30f;
        /// <summary>Joint pin (3/8"-10 thread) sticking out of the butt into the shaft.</summary>
        public const float PinR = 0.00476f, PinLen = 0.021f, ThreadPitch = 0.00254f;
        public const float TipZ0 = PoolDims.CueLength - TipH;
        public const float FerruleZ0 = TipZ0 - FerruleH;
        public const float ParallelZ0 = PoolDims.CueLength - ParallelLen;

        /// <summary>One-piece cues (the house cue) have no joint; <see cref="BuildSplit"/> still cuts them at the joint.</summary>
        public static bool IsOnePiece(int variant) => Mathf.Clamp(variant, 0, Variants - 1) == 0;

        /// <summary>Radius of the butt (straight taper from the bumper end to the joint).</summary>
        public static float ButtRadius(float z) => Mathf.Lerp(ButtR, JointR, z / PoolDims.JointZ);

        /// <summary>Radius of the shaft: pro taper (parallel near the tip, then a smooth flare to the joint).</summary>
        public static float ShaftRadius(float z)
        {
            if (z >= ParallelZ0) return TipR;
            float t = Mathf.Clamp01((ParallelZ0 - z) / (ParallelZ0 - PoolDims.JointZ));
            return TipR + (JointR - TipR) * Mathf.Pow(t, 1.45f);
        }

        /// <summary>Outer radius of the assembled cue at <paramref name="z"/> (0 outside 0..CueLength).</summary>
        public static float RadiusAt(float z)
        {
            if (z < 0f || z > PoolDims.CueLength) return 0f;
            if (z < BumperH) return BumperR;
            if (z < PoolDims.JointZ) return ButtRadius(z);
            if (z < TipZ0 + TipH * 0.6f) return ShaftRadius(z);
            float dz = z - (PoolDims.CueLength - DomeR);
            return Mathf.Min(TipR, Mathf.Sqrt(Mathf.Max(0f, DomeR * DomeR - dz * dz)));
        }

        /// <summary>Circumference at <paramref name="z"/> (texture painters).</summary>
        public static float Circ(float z) => 2f * Mathf.PI * (z < PoolDims.JointZ ? ButtRadius(z) : ShaftRadius(z));

        /// <summary>Builds the assembled cue under <paramref name="parent"/> (root at the cue origin).</summary>
        public static Transform Build(Transform parent, Material template, int variant)
        {
            variant = Mathf.Clamp(variant, 0, Variants - 1);
            var root = NewRoot("Cue_" + Names[variant], parent);
            var k = new Kit(template, variant, false);
            k.Parent = root;
            BuildButt(k);
            BuildShaft(k);
            return root;
        }

        /// <summary>
        /// Builds the cue unscrewed: <paramref name="butt"/> (z = 0..JointZ, with the pin) and <paramref name="shaft"/>
        /// (z = JointZ..CueLength, with the threaded insert), both rooted at the cue origin so they line up when left at identity.
        /// </summary>
        public static Transform BuildSplit(Transform parent, Material template, int variant, out Transform shaft, out Transform butt)
        {
            variant = Mathf.Clamp(variant, 0, Variants - 1);
            var root = NewRoot("CueSplit_" + Names[variant], parent);
            var k = new Kit(template, variant, true);
            k.Parent = butt = NewRoot("Butt", root);
            BuildButt(k);
            k.Parent = shaft = NewRoot("Shaft", root);
            BuildShaft(k);
            return root;
        }

        static Transform NewRoot(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            if (parent != null) t.gameObject.layer = parent.gameObject.layer;
            return t;
        }
    }
}
