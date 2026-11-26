using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Small props shared by the desk, the coffee corner and the table of the long abandoned room: empty stained mugs
    /// (one cracked), old crushed soda cans with faded labels, crinkled wrappers. Nothing edible anywhere.
    /// </summary>
    public partial class SecurityRooms
    {
        private static readonly Color DriedCoffee = new Color(0.12f, 0.08f, 0.05f);

        /// <summary>
        /// Ceramic mug: thick rim, inside, a looped handle. <paramref name="at"/> = on the surface. With
        /// <paramref name="dregs"/> a dried brown crust at the bottom and a tide mark inside (long gone cold).
        /// </summary>
        private void MugModel(Transform parent, Vector3 at, Color body, Color band, float yaw, bool dregs = true, bool cracked = false)
        {
            var m = CachedMesh("Mug", p => p
                .Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.036f, 0f), new Vector2(0.04f, 0.006f), new Vector2(0.04f, 0.094f), new Vector2(0.036f, 0.098f),
                               new Vector2(0.034f, 0.094f), new Vector2(0.034f, 0.012f), new Vector2(0f, 0.012f) }, 20, 50f)
                .At(new Vector3(0.042f, 0.05f, 0f), Quaternion.Euler(90f, 0f, 0f)).Torus(0.024f, 0.0065f, 14, 8, -100f, 100f));
            var rot = Quaternion.Euler(0f, yaw, 0f);
            // Old ceramic: dull, a little grey.
            MeshObj(parent, "Mug", m, at, rot, Color.Lerp(body, new Color(0.45f, 0.43f, 0.4f), 0.25f), Surf.Shiny(0.45f));
            MeshObj(parent, "MugBand", CachedMesh("MugBand", p => p.Lathe(new[] { new Vector2(0.0405f, 0.055f), new Vector2(0.0405f, 0.075f) }, 20)), at, rot,
                Color.Lerp(band, new Color(0.5f, 0.48f, 0.45f), 0.35f), Surf.Shiny(0.4f), false, false);
            if (dregs)
            {
                MeshObj(parent, "MugDregs", CachedMesh("MugDregs", p => p.Lathe(new[] { new Vector2(0.0338f, 0.0125f), new Vector2(0.03f, 0.0142f), new Vector2(0f, 0.0146f) }, 20)),
                    at, rot, DriedCoffee, Surf.Shiny(0.15f), false, false);
                MeshObj(parent, "MugTideMark", CachedMesh("MugTide", p => p.Lathe(new[] { new Vector2(0.0339f, 0.06f), new Vector2(0.0339f, 0.052f) }, 20)),
                    at, rot, new Color(0.3f, 0.2f, 0.13f), Surf.Shiny(0.1f), false, false);
            }
            if (!cracked) return;
            // A hairline crack running down from the rim and a chipped notch in it.
            var crack = CachedMesh("MugCrack", p => p.Tube(new[] { new Vector3(0f, 0.097f, -0.0402f), new Vector3(0.006f, 0.075f, -0.0404f), new Vector3(0.002f, 0.055f, -0.0404f),
                new Vector3(0.01f, 0.03f, -0.0402f), new Vector3(0.007f, 0.012f, -0.04f) }, 0.0009f, 4, false));
            MeshObj(parent, "MugCrack", crack, at, rot, new Color(0.08f, 0.07f, 0.06f), Surf.Shiny(0.1f), false, false);
            MeshObj(parent, "MugChip", CachedMesh("MugChip", p => p.At(new Vector3(0f, 0.096f, -0.039f)).Box(Vector3.zero, new Vector3(0.012f, 0.006f, 0.004f))),
                at, rot, new Color(0.62f, 0.58f, 0.52f), Surf.Shiny(0.1f), false, false);
        }

        // ================================================================ crushed cans

        /// <summary>How an old can was crushed.</summary>
        private enum Crush
        {
            /// <summary>Stamped flat from the top: short, accordion folds, standing.</summary>
            Stomped,
            /// <summary>Squeezed in a fist: pinched waist, oval, dents; standing or lying.</summary>
            Squeezed,
            /// <summary>Stepped on while lying: flattened in the middle, ends still round; lies on its flat side.</summary>
            Flattened,
        }

        private static readonly Dictionary<(Crush, int), (Mesh label, Mesh alu)> _canMeshes = new();
        private static readonly Dictionary<int, Material> _canLabelMats = new();
        private const int CanBrands = 3;   // brands used by the rooms (SodaCanArt labels 0, 2, 6: cola, energy, root beer)

        private static int CanBrand(int i) => new[] { 0, 2, 6 }[((i % CanBrands) + CanBrands) % CanBrands];

        /// <summary>
        /// Old crushed soda can: the printed body (SodaCanArt label, dulled and dirty) and the aluminium ends, both
        /// deformed. <paramref name="at"/> = on the surface; <paramref name="lying"/> lays a squeezed can on its side
        /// (flattened ones always lie). Two renderers, meshes shared by all rooms.
        /// </summary>
        private void CrushedCan(Transform parent, Vector3 at, float yaw, Crush kind, int brand, int variant, bool lying = false)
        {
            var (label, alu) = CanMeshes(kind, variant & 1);
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pos = at;
            if (kind == Crush.Flattened)
            {
                // Axis along the floor, the flat side down; rests on the round ends.
                q *= Quaternion.Euler(90f, 0f, 0f);
                pos = at + Vector3.up * 0.031f + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -0.055f);
            }
            else if (lying)
            {
                q *= Quaternion.Euler(0f, 0f, 90f);
                pos = at + Vector3.up * 0.03f + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.055f, 0f, 0f);
            }
            var g = MeshObj(parent, "CrushedCan", label, pos, q, Color.white);
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = CanLabelMaterial(CanBrand(brand));
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(0.66f, 0.63f, 0.58f));   // faded and grimy
            r.SetPropertyBlock(mpb);
            MeshObj(parent, "CrushedCanEnds", alu, pos, q, new Color(0.55f, 0.55f, 0.54f), Surf.Shiny(0.45f, 0.7f), false, false);
        }

        private Material CanLabelMaterial(int brand)
        {
            if (_canLabelMats.TryGetValue(brand, out var m) && m != null) return m;
            if (_standard == null) _standard = Shader.Find("Standard");
            m = new Material(_standard) { name = "Sec_CanLabel" + brand, mainTexture = SodaCanArt.Label(brand) };
            m.SetFloat("_Glossiness", 0.32f);
            m.SetFloat("_Metallic", 0.35f);
            return _canLabelMats[brand] = Keep(m);
        }

        /// <summary>Printed body (uv = SodaCanArt label: u round, v up) and aluminium ends of a crushed can.</summary>
        private static (Mesh label, Mesh alu) CanMeshes(Crush kind, int variant)
        {
            if (_canMeshes.TryGetValue((kind, variant), out var pair) && pair.label != null) return pair;
            const int seg = 28;
            float seed = 1.3f + variant * 2.9f + (int)kind * 0.7f;
            // Printed wall: many rings so it can fold. Ends: domed bottom, neck, rim, lid.
            var printed = new List<Vector2>();
            for (int i = 0; i <= 18; i++) printed.Add(new Vector2(0.033f, Mathf.Lerp(0.016f, 0.104f, i / 18f)));
            var bottom = new[] { new Vector2(0f, 0.004f), new Vector2(0.022f, 0f), new Vector2(0.032f, 0.008f), new Vector2(0.033f, 0.016f) };
            var top = new[] { new Vector2(0.033f, 0.104f), new Vector2(0.027f, 0.118f), new Vector2(0.026f, 0.122f), new Vector2(0.023f, 0.12f), new Vector2(0f, 0.119f) };
            Vector3 D(Vector3 p) => CrushPoint(p, kind, seed);
            var label = Revolve(printed, seg, true, D, "Sec_CanCrushedLabel" + kind + variant);
            var ends = Revolve(new List<Vector2>(top), seg, false, D, null);
            var alu = Revolve(new List<Vector2>(bottom), seg, false, D, "Sec_CanCrushedEnds" + kind + variant, ends);
            Object.Destroy(ends);
            pair = (Keep(label), Keep(alu));
            _canMeshes[(kind, variant)] = pair;
            return pair;
        }

        /// <summary>Revolves a (radius, height) profile, deforms it, recomputes the normals (creases stay sharp-ish).</summary>
        private static Mesh Revolve(List<Vector2> prof, int seg, bool labelUv, System.Func<Vector3, Vector3> deform, string name, Mesh append = null)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            if (append != null)
            {
                append.GetVertices(v);
                append.GetUVs(0, uv);
                append.GetTriangles(t, 0);
            }
            int start = v.Count;
            float y0 = prof[0].y, y1 = prof[prof.Count - 1].y;
            for (int i = 0; i < prof.Count; i++)
            for (int j = 0; j <= seg; j++)
            {
                // Like SodaCan: u = 0 at +X going round towards +Z.
                float u = j / (float)seg, a = u * Mathf.PI * 2f;
                v.Add(deform(new Vector3(Mathf.Cos(a) * prof[i].x, prof[i].y, Mathf.Sin(a) * prof[i].x)));
                uv.Add(new Vector2(u, labelUv ? Mathf.InverseLerp(y0, y1, prof[i].y) : 0.5f));
            }
            for (int i = 0; i + 1 < prof.Count; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = start + i * (seg + 1) + j, b = a + seg + 1;
                t.Add(a); t.Add(b); t.Add(a + 1);
                t.Add(a + 1); t.Add(b); t.Add(b + 1);
            }
            var m = new Mesh { name = name ?? "Sec_CanPart" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Crush deformation of a point of an upright can (base at y = 0, height 0.122).</summary>
        private static Vector3 CrushPoint(Vector3 p, Crush kind, float seed)
        {
            const float h0 = 0.122f;
            float h = Mathf.Clamp01(p.y / h0);
            float r = new Vector2(p.x, p.z).magnitude;
            float a = Mathf.Atan2(p.z, p.x);
            float mid = Mathf.Sin(Mathf.PI * Mathf.Clamp01((h - 0.1f) / 0.78f));        // 0 at the ends, 1 in the middle
            float dent = 0.13f * Mathf.Sin(5f * a + seed + h * 7f) + 0.08f * Mathf.Sin(9f * a - seed * 2f + h * 23f) + 0.04f * Mathf.Sin(17f * a + h * 41f);
            switch (kind)
            {
                case Crush.Stomped:
                {
                    // Height down to ~40 %, the wall folded into 3-4 accordion rings bulging out, the top tilted.
                    float fold = Mathf.Sin(h * Mathf.PI * 7f + seed + Mathf.Sin(2f * a + seed) * 0.8f);
                    float k = 1f + mid * (0.16f + 0.1f * fold + dent * 0.6f);
                    float y = p.y * 0.4f + mid * 0.004f * Mathf.Sin(3f * a + seed) + h * 0.009f * Mathf.Cos(a - seed);
                    return new Vector3(Mathf.Cos(a) * r * k, y, Mathf.Sin(a) * r * k);
                }
                case Crush.Squeezed:
                {
                    float k = 1f + mid * (dent - 0.26f);
                    return new Vector3(Mathf.Cos(a) * r * k * (1f + 0.24f * mid), p.y * (1f - 0.08f * mid) + mid * 0.004f * Mathf.Sin(7f * a + seed),
                                       Mathf.Sin(a) * r * k * (1f - 0.32f * mid));
                }
                default:
                {
                    // Flattened along local z (becomes the floor normal when lying), spread along x, creased.
                    float flat = Mathf.SmoothStep(0f, 1f, mid);
                    float k = 1f + flat * dent * 0.5f;
                    float x = Mathf.Cos(a) * r * k * (1f + 0.45f * flat);
                    float z = Mathf.Sin(a) * r * k * (1f - 0.72f * flat) + flat * 0.003f * Mathf.Sin(11f * h + seed);
                    return new Vector3(x, p.y * (1f - 0.05f * flat), z);
                }
            }
        }

        /// <summary>Crinkled old wrapper (foil / plastic), lying flat; faded <paramref name="c"/>.</summary>
        private void Wrapper(Transform parent, Vector3 at, float yaw, Color c, int variant)
        {
            var mesh = CachedMesh("Wrapper" + (variant & 1), p => p.At(Vector3.zero, Quaternion.identity, new Vector3(1f, 0.18f, 1f)).RoundBox(new Vector3(0.11f, 0.05f, 0.06f + variant % 2 * 0.03f), 0.02f, 4));
            MeshObj(parent, "Wrapper", mesh, at + Vector3.up * 0.004f, Quaternion.Euler(variant * 3f, yaw, (variant % 3) * 4f), Color.Lerp(c, new Color(0.5f, 0.48f, 0.44f), 0.45f), Surf.Shiny(0.55f, 0.3f), false, false);
        }
    }
}
