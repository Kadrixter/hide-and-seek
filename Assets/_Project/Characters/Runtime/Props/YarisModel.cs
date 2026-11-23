using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Easter egg body for the RC car: a toy Toyota Yaris 2010 "Luna" (XP90 facelift, 5-door hatchback) in light blue
    /// metallic - round monoform body, tall roof, swept-back teardrop headlights, upper grille slot with the oval logo,
    /// wide lower intake, vertical corner tail lamps, black B-pillars, fixed quarter glass at the A-pillar, body-colour
    /// mirrors and pull handles, roof spoiler with the third brake light, roof antenna, 5-spoke alloys.
    /// <para>
    /// Same car space and footprint as the rally car (<see cref="BlockyProps.CreateRcCar"/>): origin on the ground
    /// between the axles, +Z forward, wheelbase 0.27 m; physics / collider are the RC car's. Everything but the root
    /// sits under <see cref="Parts.Squash"/> (pivot on the ground) so the whole car can squash and stretch.
    /// Meshes are built once and shared; no colliders.
    /// </para>
    /// </summary>
    public static partial class YarisModel
    {
        public sealed class Parts
        {
            public Transform Root, Squash, Body, Antenna;
            /// <summary>FR, FL, RR, RL (like the rally car).</summary>
            public readonly Transform[] Wheels = new Transform[4];
            /// <summary>Front steering pivots (R, L).</summary>
            public readonly Transform[] Steer = new Transform[2];
            /// <summary>Antenna base in car space (the crushed wreck needs it).</summary>
            public Vector3 AntennaBase;
        }

        // Material slots (sub-mesh index = slot).
        internal const int MPaint = 0, MGlass = 1, MTrim = 2, MUnder = 3, MChrome = 4, MHead = 5, MTail = 6, MAmber = 7,
            MClear = 8, MPlate = 9, MInk = 10, MTyre = 11, MRim = 12, MSeam = 13, MEu = 14, Slots = 15;

        /// <summary>Toyota light blue metallic (close to the "Blue" 8T5 / sky blue shade of the Luna ads).</summary>
        public static readonly Color PaintColor = new Color(0.3f, 0.56f, 0.9f);

        static Mesh _body, _wheelR, _wheelL, _antenna;
        static Material[] _mats;

        internal static readonly Vector3 AntennaPos = new Vector3(0f, 0f, -0.128f);

        public static Parts Create(Transform parent, int layer)
        {
            EnsureMeshes();
            var p = new Parts();
            p.Root = Node("Yaris", parent, layer);
            p.Squash = Node("YarisSquash", p.Root, layer);
            p.Body = Renderer("YarisBody", p.Squash, _body, layer);
            for (int i = 0; i < 4; i++)
            {
                bool right = i % 2 == 0, front = i < 2;
                var pos = new Vector3(right ? TrackX : -TrackX, WheelR, front ? AxleF : AxleR);
                Transform mount = p.Squash;
                if (front)
                {
                    var pivot = Node(right ? "SteerR" : "SteerL", p.Squash, layer);
                    pivot.localPosition = pos;
                    p.Steer[i] = pivot;
                    mount = pivot;
                }
                var w = Renderer("Wheel" + i, mount, right ? _wheelR : _wheelL, layer);
                w.localPosition = front ? Vector3.zero : pos;
                p.Wheels[i] = w;
            }
            p.AntennaBase = new Vector3(AntennaPos.x, YRoof(AntennaPos.z) - 0.0005f, AntennaPos.z);
            p.Antenna = Node("Antenna", p.Squash, layer);
            p.Antenna.localPosition = p.AntennaBase;
            Renderer("Mast", p.Antenna, _antenna, layer);
            return p;
        }

        static Transform Node(string name, Transform parent, int layer)
        {
            var t = new GameObject(name).transform;
            t.gameObject.layer = layer;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        static Transform Renderer(string name, Transform parent, Mesh mesh, int layer)
        {
            var t = Node(name, parent, layer);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = Materials;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return t;
        }

        static void EnsureMeshes()
        {
            if (_body != null) return;
            var loft = BuildBodyLoft();
            var m = new YarisMesh(Slots);
            loft.Emit(m, BodySlot, MPaint, MPaint);
            AddDetails(m, loft);
            _body = m.ToMesh("Yaris_Body");
            _wheelR = BuildWheel(1f);
            _wheelL = BuildWheel(-1f);
            _antenna = BuildAntenna();
        }

        /// <summary>Material of a body quad (centre z, band index between ring i and i+1).</summary>
        static int BodySlot(float z, int band)
        {
            if (band <= RWT) return MUnder;
            if (band <= RSh) return MPaint;
            if (band == RGB || band == RGM)
            {
                if (z > ZCowl) return MPaint;                    // hood crown
                if (z > ZQuarterF) return MPaint;                // A-pillar base / mirror sail
                if (z > ZQuarterR) return MGlass;                // fixed quarter glass
                if (z > ZFrontGlassF) return MTrim;              // divider bar
                if (z > ZBpF) return MGlass;                     // front door window
                if (z > ZBpR) return MTrim;                      // black B-pillar
                if (z > ZRearGlassR) return MGlass;              // rear door window
                if (z > ZCGlassF) return MTrim;                  // divider bar
                if (z > ZCGlassR) return MGlass;                 // C-pillar quarter glass
                return MPaint;
            }
            if (band == RGT) return MPaint;                      // roof rail, A / D pillars
            if (z < ZCowlF && z > ZCowl) return MTrim;           // cowl panel under the windscreen
            if (z <= ZCowl && z >= ZRoofF) return MGlass;        // windscreen
            if (z <= ZRoofR && z >= ZHatchB) return MGlass;      // hatch glass
            return MPaint;
        }

        // ================================================================ materials

        internal static Material[] Materials
        {
            get
            {
                if (_mats != null && _mats[0] != null) return _mats;
                _mats = new Material[Slots];
                _mats[MPaint] = Mat("YarisPaint", PaintColor, 0.82f, 0.2f);
                _mats[MGlass] = Mat("YarisGlass", new Color(0.045f, 0.06f, 0.075f), 0.94f, 0.1f);
                _mats[MTrim] = Mat("YarisTrim", new Color(0.035f, 0.035f, 0.04f), 0.4f, 0f);
                _mats[MUnder] = Mat("YarisUnder", new Color(0.03f, 0.03f, 0.033f), 0.15f, 0f);
                _mats[MChrome] = Mat("YarisChrome", new Color(0.86f, 0.88f, 0.9f), 0.88f, 0.55f);
                _mats[MHead] = Mat("YarisHeadLens", new Color(0.8f, 0.83f, 0.86f), 0.92f, 0.25f, new Color(0.06f, 0.06f, 0.065f));
                _mats[MTail] = Mat("YarisTail", new Color(0.72f, 0.04f, 0.035f), 0.9f, 0f, new Color(0.22f, 0.01f, 0.01f));
                _mats[MAmber] = Mat("YarisAmber", new Color(1f, 0.52f, 0.06f), 0.9f, 0f, new Color(0.12f, 0.05f, 0f));
                _mats[MClear] = Mat("YarisClear", new Color(0.9f, 0.9f, 0.92f), 0.92f, 0.1f);
                _mats[MPlate] = Mat("YarisPlate", new Color(0.93f, 0.93f, 0.9f), 0.35f, 0f);
                _mats[MInk] = Mat("YarisInk", new Color(0.04f, 0.04f, 0.05f), 0.3f, 0f);
                _mats[MTyre] = Mat("YarisTyre", new Color(0.045f, 0.045f, 0.05f), 0.12f, 0f);
                _mats[MRim] = Mat("YarisRim", new Color(0.72f, 0.74f, 0.77f), 0.7f, 0.45f);
                _mats[MSeam] = Mat("YarisSeam", new Color(0.06f, 0.09f, 0.14f), 0.3f, 0f);
                _mats[MEu] = Mat("YarisEu", new Color(0.05f, 0.18f, 0.6f), 0.4f, 0f);
                return _mats;
            }
        }

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

        // ================================================================ antenna

        static Mesh BuildAntenna()
        {
            // Short black mast raked back, on a small rubber foot (the foot is part of the mesh: it wobbles with it).
            var m = new YarisMesh(Slots);
            m.Box(new Vector3(0f, 0.0015f, -0.002f), new Vector3(0.007f, 0.003f, 0.012f), Quaternion.identity, MTrim, 0.6f);
            var rake = Quaternion.Euler(-32f, 0f, 0f);
            const float len = 0.026f;
            m.Box(new Vector3(0f, 0.002f, -0.002f) + rake * new Vector3(0f, len * 0.5f, 0f), new Vector3(0.0022f, len, 0.0022f), rake, MTrim, 0.5f);
            m.Box(new Vector3(0f, 0.002f, -0.002f) + rake * new Vector3(0f, len, 0f), new Vector3(0.003f, 0.003f, 0.003f), rake, MTrim, 0.7f);
            return m.ToMesh("Yaris_Antenna");
        }
    }
}
