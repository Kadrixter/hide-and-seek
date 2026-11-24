using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// The RC rally car (2026-11-24, replaces the blocky one): a toy 3-door WRC hatchback in white with blue / red
    /// livery and the number 7 - wide box flares, short hood with a 4-lamp pod and vents, steep windscreen, roof scoop,
    /// raked hatch under a big wing on two posts, mud flaps, gravel tyres on white rims. Inside a cockpit for the car
    /// camera (<see cref="DriverEye"/>): dashboard with dials, steering wheel, roll cage, two buckets, the co-driver
    /// with his pace notes, an inner shell so the cabin is closed from inside (the glass is see-through from inside).
    /// <para>Same car space as before (BlockyProps.RcWheelPos, origin on the ground between the axles, +Z forward);
    /// physics / collider / wheel spin are the RC car's. Meshes are built once and shared; no colliders.</para>
    /// </summary>
    public static partial class RallyModel
    {
        // Material slots (sub-mesh index = slot).
        internal const int MPaint = 0, MBlue = 1, MRed = 2, MGlass = 3, MTrim = 4, MUnder = 5, MChrome = 6, MHead = 7, MTail = 8,
            MTyre = 9, MRim = 10, MInk = 11, MDisc = 12, MCabin = 13, MCage = 14, MSeat = 15, MSuit = 16, MHelmet = 17, MVisor = 18,
            MGauge = 19, Slots = 20;

        public static readonly Color PaintColor = new Color(0.93f, 0.94f, 0.95f);
        public static readonly Color BlueColor = new Color(0.06f, 0.2f, 0.62f);

        /// <summary>The driver's eyes in car space (left seat): the RC car camera sits here.</summary>
        public static readonly Vector3 DriverEye = new Vector3(-0.027f, 0.147f, -0.04f);

        static Mesh _body, _wheelR, _wheelL;
        static Material[] _mats;
        static readonly Dictionary<int, Material[]> Painted = new Dictionary<int, Material[]>();

        public static Mesh Body { get { EnsureMeshes(); return _body; } }
        public static Mesh Wheel(float side) { EnsureMeshes(); return side > 0f ? _wheelR : _wheelL; }

        static void EnsureMeshes()
        {
            if (_body != null) return;
            var loft = BuildBodyLoft();
            var m = new YarisMesh(Slots);
            loft.Emit(m, BodySlot, MTrim, MTrim);
            AddDetails(m, loft);
            AddCabin(m, loft);
            _body = m.ToMesh("Rally_Body");
            _wheelR = BuildWheel(1f);
            _wheelL = BuildWheel(-1f);
        }

        /// <summary>Material of a body quad (centre z, band index between ring i and i+1): white with a blue band low on the
        /// flanks, a blue centre stripe over hood / roof / hatch, glass, black pillars.</summary>
        static int BodySlot(float z, int band)
        {
            if (band <= RWT) return MUnder;
            if (band == RLip) return MTrim;                      // black arch lip / sill edge
            if (band == RRk || band == RLow) return MBlue;       // blue band on the flanks (flares)
            if (band == RMax) return MPaint;
            if (band == RSh) return z > ZRearGlassR && z < ZCowl ? MRed : MPaint; // red pinstripe under the windows
            if (band == RGB || band == RGM)
            {
                if (z > ZCowl) return MPaint;                    // fender / hood edge
                if (z > ZFrontGlassF) return MTrim;              // A-pillar base
                if (z > ZBpF) return MGlass;                     // door window
                if (z > ZBpR) return MTrim;                      // B-pillar
                if (z > ZRearGlassR) return MGlass;              // quarter window
                return MPaint;
            }
            if (band == RGT) return MPaint;                      // roof rail, pillars in body colour
            if (z < ZCowlF && z > ZCowl) return MTrim;           // cowl panel
            if (z <= ZCowl && z >= ZRoofF) return MGlass;        // windscreen
            if (z <= ZRoofR - 0.006f && z >= ZHatchB + 0.004f && band >= RRE) return MGlass; // hatch glass
            if (band == RRM) return z > ZF - 0.012f ? MPaint : MBlue; // centre stripe
            return MPaint;
        }

        // ================================================================ materials

        internal static Material[] Materials
        {
            get
            {
                if (_mats != null && _mats[0] != null) return _mats;
                _mats = new Material[Slots];
                _mats[MPaint] = Mat("RallyPaint", PaintColor, 0.85f, 0.05f);
                _mats[MBlue] = Mat("RallyBlue", BlueColor, 0.85f, 0.15f);
                _mats[MRed] = Mat("RallyRed", new Color(0.85f, 0.08f, 0.06f), 0.8f, 0.05f);
                _mats[MGlass] = Mat("RallyGlass", new Color(0.06f, 0.075f, 0.09f), 0.96f, 0.25f);
                _mats[MTrim] = Mat("RallyTrim", new Color(0.03f, 0.03f, 0.035f), 0.35f, 0f);
                _mats[MUnder] = Mat("RallyUnder", new Color(0.025f, 0.025f, 0.028f), 0.12f, 0f);
                _mats[MChrome] = Mat("RallyChrome", new Color(0.82f, 0.84f, 0.86f), 0.88f, 0.6f);
                _mats[MHead] = Mat("RallyLamp", new Color(0.92f, 0.93f, 0.88f), 0.95f, 0.3f, new Color(0.3f, 0.3f, 0.26f));
                _mats[MTail] = Mat("RallyTail", new Color(0.72f, 0.04f, 0.035f), 0.9f, 0f, new Color(0.25f, 0.01f, 0.01f));
                _mats[MTyre] = Mat("RallyTyre", new Color(0.045f, 0.045f, 0.05f), 0.1f, 0f);
                _mats[MRim] = Mat("RallyRim", new Color(0.92f, 0.92f, 0.9f), 0.6f, 0.1f);
                _mats[MInk] = Mat("RallyInk", new Color(0.03f, 0.03f, 0.04f), 0.3f, 0f);
                _mats[MDisc] = Mat("RallyDisc", new Color(0.97f, 0.97f, 0.95f), 0.4f, 0f);
                _mats[MCabin] = Mat("RallyCabin", new Color(0.09f, 0.095f, 0.1f), 0.2f, 0f);
                _mats[MCage] = Mat("RallyCage", new Color(0.78f, 0.62f, 0.12f), 0.55f, 0.2f);
                _mats[MSeat] = Mat("RallySeat", new Color(0.05f, 0.12f, 0.4f), 0.25f, 0f);
                _mats[MSuit] = Mat("RallySuit", new Color(0.75f, 0.1f, 0.08f), 0.3f, 0f);
                _mats[MHelmet] = Mat("RallyHelmet", new Color(0.95f, 0.95f, 0.95f), 0.8f, 0.05f);
                _mats[MVisor] = Mat("RallyVisor", new Color(0.05f, 0.06f, 0.1f), 0.95f, 0.4f);
                _mats[MGauge] = Mat("RallyGauge", new Color(0.05f, 0.14f, 0.1f), 0.7f, 0f, new Color(0.03f, 0.16f, 0.09f));
                return _mats;
            }
        }

        /// <summary>The materials with the white paint replaced (race cars in their own colour).</summary>
        public static Material[] MaterialsWithPaint(Color paint)
        {
            int key = ((int)(paint.r * 255f) << 16) | ((int)(paint.g * 255f) << 8) | (int)(paint.b * 255f);
            if (Painted.TryGetValue(key, out var mats) && mats[0] != null) return mats;
            mats = (Material[])Materials.Clone();
            mats[MPaint] = Mat("RallyPaint_" + key.ToString("x6"), paint, 0.85f, 0.05f);
            Painted[key] = mats;
            return mats;
        }

        /// <summary>Is this one of the rally car's material sets (to repaint it).</summary>
        public static bool IsRallyRenderer(Renderer r) => r != null && r.sharedMaterials.Length == Slots && r.sharedMaterials[MCabin] == Materials[MCabin];

        static Material Mat(string name, Color c, float gloss, float metal, Color? emission = null)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse")) { name = name + " (runtime)", color = c, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Glossiness", gloss);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        /// <summary>A renderer with the rally materials.</summary>
        public static Transform Renderer(string name, Transform parent, Mesh mesh, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = Materials;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return go.transform;
        }

        // ================================================================ wheels

        /// <summary>
        /// Gravel wheel (origin = hub centre, axle along X, outer face towards +X * side): tall chunky tyre with blocks
        /// on the tread, white 5-spoke rally rim, gold nut.
        /// </summary>
        static Mesh BuildWheel(float side)
        {
            var m = new YarisMesh(Slots);
            float r = WheelR, hw = WheelW * 0.5f;
            const int seg = 24;
            m.Lathe(new[]
            {
                new Vector2(-hw, 0.026f), new Vector2(-hw - 0.0008f, 0.031f), new Vector2(-hw + 0.0002f, 0.037f),
                new Vector2(-hw + 0.003f, r - 0.0005f), new Vector2(-hw + 0.006f, r),
                new Vector2(hw - 0.006f, r), new Vector2(hw - 0.003f, r - 0.0005f),
                new Vector2(hw - 0.0002f, 0.037f), new Vector2(hw + 0.0008f, 0.031f), new Vector2(hw, 0.026f),
            }, seg, MTyre, Vector3.zero, side);
            // Tread blocks (gravel tyre): two staggered rows.
            for (int k = 0; k < 18; k++)
            {
                var rot = Quaternion.Euler(k * 20f, 0f, 0f);
                float x = (k % 2 == 0 ? -1f : 1f) * hw * 0.38f;
                m.Box(rot * new Vector3(x, r + 0.0006f, 0f), new Vector3(hw * 0.7f, 0.0022f, 0.0058f), rot, MTyre, 0.3f);
            }
            // Rim: lip, dish, 5 spokes, hub.
            m.Lathe(new[]
            {
                new Vector2(hw - 0.0001f, 0.0262f), new Vector2(hw + 0.0004f, 0.0255f), new Vector2(hw - 0.0005f, 0.0245f),
                new Vector2(hw - 0.004f, 0.024f),
            }, seg, MRim, Vector3.zero, side);
            m.Lathe(new[] { new Vector2(hw - 0.004f, 0.024f), new Vector2(-hw + 0.002f, 0.024f) }, seg, MRim, Vector3.zero, side, false);
            m.Lathe(new[] { new Vector2(-0.002f, 0.0235f), new Vector2(-0.002f, 0.004f) }, seg, MUnder, Vector3.zero, side, false);
            for (int k = 0; k < 5; k++)
            {
                var rot = Quaternion.Euler(k * 72f + 18f, 0f, 0f);
                m.Box(rot * new Vector3(side * (hw - 0.0035f), 0.0135f, 0f), new Vector3(0.003f, 0.021f, 0.0055f), rot, MRim, 0.35f);
            }
            m.Lathe(new[] { new Vector2(hw - 0.0045f, 0.0075f), new Vector2(hw - 0.0005f, 0.0062f), new Vector2(hw + 0.0002f, 0.0001f) }, 16, MRim, Vector3.zero, side);
            m.Lathe(new[] { new Vector2(hw + 0.0008f, 0.0032f), new Vector2(hw + 0.0008f, 0.0001f) }, 10, MCage, Vector3.zero, side, false);
            return m.ToMesh(side > 0f ? "Rally_WheelR" : "Rally_WheelL");
        }
    }
}
