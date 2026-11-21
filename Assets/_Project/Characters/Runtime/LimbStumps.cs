using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Where the raw cut ("stump") of a severed limb sits on the body. The cap is attached to the body part the limb
    /// hung from (pelvis for a leg, torso for an arm / the head), never to the hidden limb's own joint: that joint keeps
    /// swinging with the walk / hop animation and the cap would poke out of the body. The cap covers exactly the limb's
    /// cross-section (4 x 4 px) and is only a few centimetres thick, flush with the body.
    /// </summary>
    public static class LimbStumps
    {
        const float Px = BlockyCharacter.PixelSize;
        const float Thick = 0.04f;

        /// <summary>Parent, local position / rotation and size of the stump cap of <paramref name="limb"/>; false when unknown.</summary>
        public static bool Placement(BlockyCharacter c, int limb, out Transform parent, out Vector3 localPos, out Quaternion localRot, out Vector3 size)
        {
            parent = null;
            localPos = Vector3.zero;
            localRot = Quaternion.identity;
            size = Vector3.one * 0.1f;
            if (c == null || !c.IsBuilt) return false;
            switch (limb)
            {
                case Limb.LegR:
                case Limb.LegL:
                {
                    // Under the hip, flush with the bottom of the torso (the leg's top face).
                    parent = c.Pelvis;
                    float x = limb == Limb.LegR ? BlockyCharacter.HipRightRest.x : BlockyCharacter.HipLeftRest.x;
                    localPos = new Vector3(x, -Thick * 0.5f + 0.004f, 0f);
                    size = new Vector3(4f * Px - 0.012f, Thick, 4f * Px - 0.012f);
                    return true;
                }
                case Limb.ArmR:
                case Limb.ArmL:
                {
                    // On the side of the torso where the upper arm touched it.
                    parent = c.Torso;
                    float side = limb == Limb.ArmR ? 1f : -1f;
                    localPos = new Vector3(side * (4f * Px + Thick * 0.5f - 0.006f), 9.5f * Px, 0f);
                    size = new Vector3(Thick, 5f * Px, 4f * Px - 0.012f);
                    return true;
                }
                default:
                {
                    // Neck: on top of the torso under the head pivot.
                    parent = c.Torso;
                    localPos = new Vector3(0f, BlockyCharacter.NeckRest.y - Thick * 0.5f + 0.004f, 0f);
                    size = new Vector3(4.6f * Px, Thick, 3.6f * Px);
                    return true;
                }
            }
        }

        /// <summary>Creates the cap as a child cube (no collider) with <paramref name="material"/>.</summary>
        public static GameObject Create(BlockyCharacter c, int limb, Material material)
        {
            if (!Placement(c, limb, out var parent, out var pos, out var rot, out var size)) return null;
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Stump_" + limb;
            if (Application.isPlaying) Object.Destroy(g.GetComponent<Collider>()); else Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = size;
            g.layer = parent.gameObject.layer;
            var r = g.GetComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        /// <summary>Direction the blood spurts from the stump (world space).</summary>
        public static Vector3 SpurtDirection(BlockyCharacter c, int limb)
        {
            if (!Placement(c, limb, out var parent, out _, out _, out _)) return Vector3.up;
            return limb switch
            {
                Limb.LegR or Limb.LegL => -parent.up,
                Limb.ArmR => parent.right,
                Limb.ArmL => -parent.right,
                _ => parent.up,
            };
        }
    }
}
