using System.Collections.Generic;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Procedural blocky hand props (knife, Glock, medkit, loose magazine) built from boxes.
    /// Every face samples a small runtime-generated pixel atlas, so the props get the same chunky pixel look as the skins.
    /// Prop space: origin = centre of the holding fist, +Z = forward (barrel / blade), +Y = up.
    /// </summary>
    public static class BlockyProps
    {
        /// <summary>Prop pixel: half a skin pixel.</summary>
        public const float U = BlockyCharacter.PixelSize * 0.5f;

        public const string MaterialResource = "Props/BlockyProp";

        // Atlas slots (8x8 texel tiles in a 64x64 texture).
        enum Slot
        {
            Blade, Edge, HandleDark, MetalDark, Polymer, Slide, Sight, Bore, MedWhite, MedRed, MedGrey, Brass, GripTexture,
            SawOrange, SawChain, SawBar, SyrBarrel, SyrLiquid, SyrCap, Bandage,
            RcPaint, RcStripe, RcAccent, RcTire, RcRim, RcGlass, RcLight, RcTail, RcChassis, RcRemote, RcLed, RcNumber
        }

        static readonly Color[] SlotColors =
        {
            new Color(0.70f, 0.72f, 0.76f), // Blade
            new Color(0.92f, 0.94f, 0.97f), // Edge
            new Color(0.13f, 0.10f, 0.08f), // HandleDark
            new Color(0.30f, 0.31f, 0.34f), // MetalDark
            new Color(0.075f, 0.075f, 0.085f), // Polymer
            new Color(0.15f, 0.155f, 0.17f), // Slide
            new Color(0.95f, 0.93f, 0.85f), // Sight
            new Color(0.01f, 0.01f, 0.01f), // Bore
            new Color(0.93f, 0.93f, 0.90f), // MedWhite
            new Color(0.78f, 0.08f, 0.07f), // MedRed
            new Color(0.50f, 0.52f, 0.55f), // MedGrey
            new Color(0.80f, 0.62f, 0.25f), // Brass
            new Color(0.10f, 0.10f, 0.11f), // GripTexture
            new Color(0.93f, 0.45f, 0.08f), // SawOrange
            new Color(0.16f, 0.16f, 0.17f), // SawChain
            new Color(0.62f, 0.64f, 0.67f), // SawBar
            new Color(0.86f, 0.92f, 0.96f), // SyrBarrel
            new Color(0.2f, 0.6f, 1f),      // SyrLiquid
            new Color(1f, 0.8f, 0.1f),      // SyrCap
            new Color(0.96f, 0.95f, 0.9f),  // Bandage
            new Color(0.08f, 0.3f, 0.82f),  // RcPaint (rally blue)
            new Color(0.95f, 0.95f, 0.96f), // RcStripe
            new Color(1f, 0.78f, 0.08f),    // RcAccent (yellow)
            new Color(0.055f, 0.055f, 0.06f), // RcTire
            new Color(0.78f, 0.8f, 0.84f),  // RcRim
            new Color(0.07f, 0.1f, 0.14f),  // RcGlass
            new Color(1f, 0.97f, 0.78f),    // RcLight
            new Color(0.85f, 0.07f, 0.05f), // RcTail
            new Color(0.11f, 0.11f, 0.12f), // RcChassis
            new Color(0.2f, 0.21f, 0.23f),  // RcRemote
            new Color(1f, 0.12f, 0.1f),     // RcLed
            new Color(0.97f, 0.97f, 0.97f), // RcNumber (white disc with a black 7, drawn in the atlas)
        };

        /// <summary>RC car (car space: origin on the ground between the axles, +Z forward).</summary>
        public const float RcWheelRadius = 0.045f;
        /// <summary>Wheel centres: FR, FL, RR, RL.</summary>
        public static readonly Vector3[] RcWheelPos =
        {
            new Vector3(0.105f, RcWheelRadius, 0.14f), new Vector3(-0.105f, RcWheelRadius, 0.14f),
            new Vector3(0.105f, RcWheelRadius, -0.13f), new Vector3(-0.105f, RcWheelRadius, -0.13f),
        };
        public static readonly Vector3 RcAntennaBase = new Vector3(0.07f, 0.12f, -0.17f);
        public const float RcAntennaLength = 0.27f;
        /// <summary>Held car: the fist grips the cabin roof (prop origin = fist).</summary>
        public static readonly Vector3 RcCarInHand = new Vector3(0f, -0.185f, 0f);
        /// <summary>Transmitter (prop origin = right fist on the right grip): left fist position and the two sticks.</summary>
        public static readonly Vector3 RcRemoteLeftGrip = new Vector3(-0.15f, 0f, 0f);
        public static readonly Vector3 RcStickR = new Vector3(-0.012f, 0.012f + RcRemoteLift, 0.012f), RcStickL = new Vector3(-0.138f, 0.012f + RcRemoteLift, 0.012f);
        const float RcRemoteLift = 0.058f;

        /// <summary>Syringe (prop space: needle tip along +Z, fist round the barrel): plunger travel when pressed.</summary>
        public const float SyringePlungerTravel = 0.075f;
        public const float ForkScale = 1.5f;
        /// <summary>Soda can: scaled up to read next to the blocky fist; centre offset from the fist along prop +Z.</summary>
        public const float CanScale = 2.2f, CanOffset = 0.155f;
        /// <summary>Can top (drinking edge) in prop space.</summary>
        public static Vector3 CanTopLocal => new Vector3(0f, SodaCan.Height * 0.5f * CanScale, CanOffset);
        /// <summary>Tine tips (unscaled prop space).</summary>
        public static readonly Vector3 ForkTipLocal = new Vector3(0f, 0f, 0.185f);
        /// <summary>The syringe is modelled at real size; scaled up so it reads next to the blocky fist.</summary>
        public const float SyringeScale = 1.6f;
        /// <summary>Syringe needle tip in prop space.</summary>
        public static readonly Vector3 SyringeTipLocal = new Vector3(0f, 0f, 0.2f);
        /// <summary>Syringe plunger head (thumb / palm pushes here) in prop space, not pressed.</summary>
        public static readonly Vector3 SyringePlungerLocal = new Vector3(0f, 0f, -0.15f);

        /// <summary>Chainsaw: left hand on the top (wrap) handle, prop space.</summary>
        public static readonly Vector3 ChainsawLeftGripLocal = new Vector3(-0.03f, 0.21f, 0.2f);
        /// <summary>Chainsaw: pull-starter grip (right side of the engine) at rest, prop space.</summary>
        public static readonly Vector3 ChainsawStarterLocal = new Vector3(0.118f, 0.015f, 0.125f);
        /// <summary>Direction the starter cord is yanked out (prop space) and how far.</summary>
        public static readonly Vector3 ChainsawPullDir = new Vector3(0.72f, 0.6f, -0.35f).normalized;
        public const float ChainsawPullLength = 0.36f;

        /// <summary>Pull-start sequence length (s) after the saw comes out; the engine fires at <see cref="ChainsawFireTime"/>.</summary>
        public const float ChainsawStartTime = 1.3f;
        public const float ChainsawFireTime = 0.95f;

        /// <summary>Pull-start: (reach 0..1 of the left hand to the starter grip, cord out 0..1) at time <paramref name="t"/>.</summary>
        public static Vector2 ChainsawStarter(float t)
        {
            if (t < 0f || t >= ChainsawStartTime) return Vector2.zero;
            float reach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.16f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.08f) / 0.2f)));
            float pull = 0f;
            // Two yanks: fast out, slower back (the cord rewinds); the second one starts the engine.
            if (t < 0.62f) pull = Yank(t, 0.16f);
            else pull = Yank(t, 0.6f) * 1.08f;
            return new Vector2(reach, Mathf.Clamp01(pull));
        }

        static float Yank(float t, float start)
        {
            float k = t - start;
            if (k <= 0f) return 0f;
            if (k < 0.16f) return Mathf.Sin(k / 0.16f * Mathf.PI * 0.5f);
            if (k < 0.42f) return 1f - Mathf.SmoothStep(0f, 1f, (k - 0.16f) / 0.26f);
            return 0f;
        }

        static Texture2D atlas;
        static Material material;
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        // ------------------------------------------------------------------ public API

        /// <summary>Glock layout constants (prop space).</summary>
        public static readonly Vector3 GlockMuzzleLocal = new Vector3(0f, 0.14f, 0.30f);
        public const float GlockSlideTravel = 0.07f;
        const float GripAngle = 18f;

        /// <summary>Creates a prop instance. Renderers use <paramref name="layer"/>.</summary>
        public static HeldProp Create(HeldItem item, Transform parent, int layer)
        {
            var go = new GameObject("Prop_" + item);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var prop = go.AddComponent<HeldProp>();
            prop.Item = item;
            switch (item)
            {
                case HeldItem.Knife:
                    AddPart(go.transform, "Knife", GetMesh("Knife"), layer);
                    break;
                case HeldItem.Glock:
                {
                    AddPart(go.transform, "Frame", GetMesh("GlockFrame"), layer);
                    var slide = AddPart(go.transform, "Slide", GetMesh("GlockSlide"), layer);
                    var mag = AddPart(go.transform, "Mag", GetMesh("GlockMag"), layer);
                    var muzzle = new GameObject("Muzzle").transform;
                    muzzle.SetParent(go.transform, false);
                    muzzle.localPosition = GlockMuzzleLocal;
                    muzzle.gameObject.layer = layer;
                    prop.Slide = slide;
                    prop.Mag = mag;
                    prop.Muzzle = muzzle;
                    break;
                }
                case HeldItem.Medkit:
                    AddPart(go.transform, "Medkit", GetMesh("Medkit"), layer);
                    break;
                case HeldItem.Syringe:
                {
                    AddPart(go.transform, "Barrel", GetMesh("SyringeBody"), layer);
                    prop.Slide = AddPart(go.transform, "Plunger", GetMesh("SyringePlunger"), layer);
                    prop.Mag = AddPart(go.transform, "Cap", GetMesh("SyringeCap"), layer);
                    var needle = new GameObject("Tip").transform;
                    needle.SetParent(go.transform, false);
                    needle.localPosition = SyringeTipLocal;
                    prop.Muzzle = needle;
                    go.transform.localScale = Vector3.one * SyringeScale;
                    break;
                }
                case HeldItem.Bandage:
                    AddPart(go.transform, "Roll", GetMesh("BandageRoll"), layer);
                    break;
                case HeldItem.Fork:
                {
                    AddPart(go.transform, "Fork", GetMesh("Fork"), layer);
                    var tines = new GameObject("Tines").transform;
                    tines.SetParent(go.transform, false);
                    tines.localPosition = ForkTipLocal;
                    prop.Muzzle = tines;
                    go.transform.localScale = Vector3.one * ForkScale;
                    break;
                }
                case HeldItem.RcCar:
                {
                    var car = CreateRcCar(go.transform, layer);
                    car.Root.localPosition = RcCarInHand;
                    break;
                }
                case HeldItem.RcRemote:
                {
                    AddPart(go.transform, "Transmitter", GetMesh("RcRemote"), layer);
                    prop.Slide = AddPart(go.transform, "StickR", GetMesh("RcStick"), layer);
                    prop.Slide.localPosition = RcStickR;
                    prop.Mag = AddPart(go.transform, "StickL", GetMesh("RcStick"), layer);
                    prop.Mag.localPosition = RcStickL;
                    break;
                }
                case HeldItem.Brick:
                {
                    var b = AddPart(go.transform, "Brick", GetBrickMesh(), layer);
                    b.localScale = Vector3.one * 0.75f;
                    b.localPosition = new Vector3(0f, -BrickSize * 0.75f * 0.5f, 0.03f);
                    break;
                }
                case HeldItem.Can:
                {
                    // Can axis = prop +Y, the can sits on the fist's +Z side (fingers round it), logo turned to prop -X.
                    prop.Can = SodaCan.Create(go.transform, 0, layer, CanScale);
                    prop.Can.transform.localPosition = new Vector3(0f, 0f, CanOffset);
                    prop.Can.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                }
                case HeldItem.Chainsaw:
                {
                    // Everything that vibrates with the engine sits under "Shake".
                    var shake = new GameObject("Shake").transform;
                    shake.gameObject.layer = layer;
                    shake.SetParent(go.transform, false);
                    AddPart(shake, "Body", GetMesh("ChainsawBody"), layer);
                    prop.Slide = AddPart(shake, "Chain", GetMesh("ChainsawChain"), layer);
                    prop.BuildSawExtras(shake, layer);
                    var tip = new GameObject("BarTip").transform;
                    tip.SetParent(go.transform, false);
                    tip.localPosition = new Vector3(0f, 0.04f, 0.86f);
                    prop.Muzzle = tip;
                    break;
                }
            }

            prop.CollectRenderers();
            return prop;
        }

        /// <summary>
        /// Rally RC car model (blue body, white stripes, yellow livery, spoiler, roof scoop, light pod, whip antenna).
        /// Wheels, steering pivots and the antenna are separate transforms so they can spin, steer, wobble and fly off.
        /// </summary>
        public static RcCarParts CreateRcCar(Transform parent, int layer)
        {
            var root = new GameObject("RcCar").transform;
            root.gameObject.layer = layer;
            if (parent != null) root.SetParent(parent, false);
            var parts = new RcCarParts { Root = root };
            parts.Body = AddPart(root, "Body", GetMesh("RcBody"), layer);
            parts.Wheels = new Transform[4];
            parts.Steer = new Transform[2];
            for (int i = 0; i < 4; i++)
            {
                Transform mount = root;
                if (i < 2)
                {
                    var pivot = new GameObject(i == 0 ? "SteerR" : "SteerL").transform;
                    pivot.gameObject.layer = layer;
                    pivot.SetParent(root, false);
                    pivot.localPosition = RcWheelPos[i];
                    parts.Steer[i] = pivot;
                    mount = pivot;
                }
                var w = AddPart(mount, "Wheel" + i, GetMesh(RcWheelPos[i].x > 0f ? "RcWheelR" : "RcWheelL"), layer);
                w.localPosition = i < 2 ? Vector3.zero : RcWheelPos[i];
                parts.Wheels[i] = w;
            }
            var ant = new GameObject("Antenna").transform;
            ant.gameObject.layer = layer;
            ant.SetParent(root, false);
            ant.localPosition = RcAntennaBase;
            AddPart(ant, "Whip", GetMesh("RcAntenna"), layer);
            parts.Antenna = ant;
            return parts;
        }

        /// <summary>Lobby toy brick: grid cell size (world bricks are exactly one cell).</summary>
        public const float BrickSize = 0.15f;

        /// <summary>
        /// Toy brick mesh (white, tinted per renderer): a cube of <see cref="BrickSize"/> with 2x2 studs on top.
        /// Origin at the bottom centre. Own UV-less mesh so a plain coloured material works too.
        /// </summary>
        public static Mesh GetBrickMesh()
        {
            if (Meshes.TryGetValue("Brick", out var m) && m != null) return m;
            var md = new MeshData();
            float s = BrickSize, h = s * 0.5f;
            Box(md, new Vector3(0f, h, 0f), new Vector3(s - 0.002f, s - 0.002f, s - 0.002f), Slot.RcStripe, Quaternion.identity);
            float o = s * 0.25f, st = s * 0.24f;
            foreach (float x in new[] { -o, o })
            foreach (float z in new[] { -o, o })
                Box(md, new Vector3(x, s + 0.009f, z), new Vector3(st, 0.02f, st), Slot.RcStripe, Quaternion.identity);
            m = new Mesh { name = "BlockyProp_Brick", hideFlags = HideFlags.DontSave };
            m.SetVertices(md.V);
            m.SetNormals(md.N);
            m.SetUVs(0, md.UV);
            m.SetTriangles(md.T, 0);
            m.RecalculateBounds();
            m.UploadMeshData(false);
            Meshes["Brick"] = m;
            return m;
        }

        /// <summary>A loose Glock magazine (prop space identical to the gun, i.e. origin = grip fist).</summary>
        public static GameObject CreateMagazine(Transform parent, int layer)
        {
            var go = new GameObject("Magazine");
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, false);
            AddPart(go.transform, "Mag", GetMesh("GlockMag"), layer);
            return go;
        }

        public static Material GetMaterial()
        {
            if (material != null) return material;
            var template = Resources.Load<Material>(MaterialResource);
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                var shader = Shader.Find("Standard");
                material = new Material(shader != null ? shader : Shader.Find("Diffuse"));
                material.SetFloat("_Glossiness", 0.25f);
            }

            material.name = "BlockyProp (runtime)";
            material.hideFlags = HideFlags.DontSave;
            material.mainTexture = GetAtlas();
            material.color = Color.white;
            return material;
        }

        static readonly Dictionary<int, Material> PaintMats = new Dictionary<int, Material>();

        /// <summary>Prop material with the RC car body paint (rally blue) replaced by <paramref name="paint"/>.</summary>
        public static Material RcPaintMaterial(Color paint)
        {
            int key = ((int)(paint.r * 255f) << 16) | ((int)(paint.g * 255f) << 8) | (int)(paint.b * 255f);
            if (PaintMats.TryGetValue(key, out var m) && m != null) return m;
            var src = GetAtlas();
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = src.GetPixels32();
            Color32 blue = SlotColors[(int)Slot.RcPaint];
            int n = src.width;
            foreach (var slot in new[] { Slot.RcPaint, Slot.RcNumber })
            {
                int s = (int)slot, ox = (s % 8) * 8, oy = (s / 8) * 8;
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int i = (oy + y) * n + ox + x;
                    bool paintPx = slot == Slot.RcPaint || (px[i].r == blue.r && px[i].g == blue.g && px[i].b == blue.b);
                    if (!paintPx) continue;
                    float shade = 1f + (Hash(s * 131 + x * 17 + y * 977) - 0.5f) * 0.12f;
                    Color c = paint * shade;
                    c.a = 1f;
                    px[i] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            m = new Material(GetMaterial()) { name = "RcPaint", mainTexture = tex };
            PaintMats[key] = m;
            return m;
        }

        internal static Transform AddPartInternal(Transform parent, string name, Mesh mesh, int layer) => AddPart(parent, name, mesh, layer);
        internal static Mesh GetMeshInternal(string key) => GetMesh(key);

        static Transform AddPart(Transform parent, string name, Mesh mesh, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = GetMaterial();
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }

        // ------------------------------------------------------------------ atlas

        static Texture2D GetAtlas()
        {
            if (atlas != null) return atlas;
            const int n = 64;
            atlas = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "BlockyPropAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 0, 255, 255);
            for (int s = 0; s < SlotColors.Length; s++)
            {
                int ox = (s % 8) * 8, oy = (s / 8) * 8;
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    float noise = Hash(s * 131 + x * 17 + y * 977);
                    float shade = 1f + (noise - 0.5f) * 0.12f;
                    var slot = (Slot)s;
                    if (slot == Slot.GripTexture && ((x + y) % 2 == 0)) shade *= 1.35f; // stippled grip
                    if (slot == Slot.Slide && y == 7) shade *= 1.25f; // bright top edge
                    if (slot == Slot.Blade) shade *= 0.92f + 0.12f * (y / 7f); // spine darker -> edge brighter
                    if (slot == Slot.MedWhite && (x == 0 || y == 0)) shade *= 0.9f;
                    if (slot == Slot.SawChain && ((x + y * 3) % 4 == 0)) shade *= 2.2f; // glinting teeth
                    if (slot == Slot.Bandage && (x % 3 == 0)) shade *= 0.9f;                 // gauze weave
                    if (slot == Slot.SyrBarrel && y % 2 == 0 && x == 3) shade *= 0.55f;     // scale marks
                    if (slot == Slot.RcTire && (x + y) % 3 == 0) shade *= 1.6f;              // tread blocks
                    if (slot == Slot.RcGlass && x == y) shade *= 2.4f;                       // glint
                    bool rcPaintEdge = false;
                    if (slot == Slot.RcNumber)
                    {
                        // White disc with a black "7" (corners in body paint).
                        float dx = x - 3.5f, dy = y - 3.5f;
                        bool seven = (y == 6 && x >= 2 && x <= 5) || (x == 5 && y == 5) || (x == 4 && (y == 4 || y == 3)) || (x == 3 && (y == 2 || y == 1));
                        if (dx * dx + dy * dy > 13f) rcPaintEdge = true;
                        else if (seven) shade = 0.06f;
                    }
                    Color c = rcPaintEdge ? SlotColors[(int)Slot.RcPaint] : SlotColors[s] * shade;
                    c.a = 1f;
                    px[(oy + y) * n + ox + x] = c;
                }
            }

            atlas.SetPixels32(px);
            atlas.Apply(false, false);
            return atlas;
        }

        static float Hash(int v)
        {
            unchecked
            {
                uint h = (uint)v * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFF) / 65535f;
            }
        }

        // ------------------------------------------------------------------ meshes

        sealed class MeshData
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
        }

        static Mesh GetMesh(string key)
        {
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            var md = new MeshData();
            switch (key)
            {
                case "Knife": BuildKnife(md); break;
                case "GlockFrame": BuildGlockFrame(md); break;
                case "GlockSlide": BuildGlockSlide(md); break;
                case "GlockMag": BuildGlockMag(md); break;
                case "Medkit": BuildMedkit(md); break;
                case "ChainsawBody": BuildChainsawBody(md); break;
                case "ChainsawChain": BuildChainsawChain(md); break;
                case "ChainsawTooth": BuildChainsawTooth(md); break;
                case "ChainsawGrip": BoxMinMax(md, new Vector3(-0.012f, -0.022f, -0.035f), new Vector3(0.012f, 0.022f, 0.035f), Slot.HandleDark); break;
                case "ChainsawCord": BoxMinMax(md, new Vector3(-0.003f, -0.003f, 0f), new Vector3(0.003f, 0.003f, 1f), Slot.Polymer); break;
                case "SyringeBody": BuildSyringeBody(md); break;
                case "SyringePlunger": BuildSyringePlunger(md); break;
                case "SyringeCap": BoxMinMax(md, new Vector3(-0.012f, -0.012f, 0.13f), new Vector3(0.012f, 0.012f, 0.215f), Slot.SyrCap); break;
                case "BandageRoll": BuildBandage(md); break;
                case "Fork": BuildFork(md); break;
                case "RcBody": BuildRcBody(md); break;
                case "RcWheelR": BuildRcWheel(md, 1f); break;
                case "RcWheelL": BuildRcWheel(md, -1f); break;
                case "RcAntenna": BuildRcAntenna(md); break;
                case "RcRemote": BuildRcRemote(md); break;
                case "RcStick":
                    BoxMinMax(md, new Vector3(-0.004f, -0.004f, 0f), new Vector3(0.004f, 0.004f, 0.022f), Slot.RcRim);
                    BoxMinMax(md, new Vector3(-0.009f, -0.009f, 0.018f), new Vector3(0.009f, 0.009f, 0.028f), Slot.RcChassis);
                    break;
            }

            m = new Mesh { name = "BlockyProp_" + key, hideFlags = HideFlags.DontSave };
            m.SetVertices(md.V);
            m.SetNormals(md.N);
            m.SetUVs(0, md.UV);
            m.SetTriangles(md.T, 0);
            m.RecalculateBounds();
            m.UploadMeshData(false);
            Meshes[key] = m;
            return m;
        }

        static void BuildKnife(MeshData md)
        {
            // Handle through the fist, pommel, guard, stepped blade (clip point), bright edge strip.
            BoxMinMax(md, new Vector3(-0.026f, -0.032f, -0.14f), new Vector3(0.026f, 0.032f, 0.125f), Slot.HandleDark);
            BoxMinMax(md, new Vector3(-0.03f, -0.036f, -0.165f), new Vector3(0.03f, 0.036f, -0.14f), Slot.MetalDark);
            BoxMinMax(md, new Vector3(-0.032f, -0.06f, 0.125f), new Vector3(0.032f, 0.055f, 0.15f), Slot.MetalDark);
            BoxMinMax(md, new Vector3(-0.009f, -0.022f, 0.15f), new Vector3(0.009f, 0.036f, 0.31f), Slot.Blade);
            BoxMinMax(md, new Vector3(-0.0085f, -0.036f, 0.15f), new Vector3(0.0085f, -0.022f, 0.29f), Slot.Edge);
            BoxMinMax(md, new Vector3(-0.008f, -0.012f, 0.31f), new Vector3(0.008f, 0.036f, 0.345f), Slot.Blade);
            BoxMinMax(md, new Vector3(-0.007f, 0.006f, 0.345f), new Vector3(0.007f, 0.036f, 0.375f), Slot.Edge);
        }

        static void BuildGlockFrame(MeshData md)
        {
            // Dust cover / rail under the front of the slide.
            BoxMinMax(md, new Vector3(-0.03f, 0.075f, -0.07f), new Vector3(0.03f, 0.105f, 0.27f), Slot.Polymer);
            // Grip, raked back.
            var gripRot = Quaternion.Euler(GripAngle, 0f, 0f);
            Box(md, new Vector3(0f, -0.02f, -0.012f), new Vector3(0.058f, 0.235f, 0.088f), Slot.GripTexture, gripRot);
            // Backstrap beavertail.
            Box(md, new Vector3(0f, 0.085f, -0.075f), new Vector3(0.056f, 0.03f, 0.04f), Slot.Polymer, Quaternion.identity);
            // Trigger guard (bottom bar + front bar) and trigger.
            BoxMinMax(md, new Vector3(-0.018f, 0.005f, 0.035f), new Vector3(0.018f, 0.025f, 0.135f), Slot.Polymer);
            BoxMinMax(md, new Vector3(-0.018f, 0.005f, 0.115f), new Vector3(0.018f, 0.085f, 0.135f), Slot.Polymer);
            BoxMinMax(md, new Vector3(-0.008f, 0.03f, 0.06f), new Vector3(0.008f, 0.08f, 0.075f), Slot.MetalDark);
        }

        static void BuildGlockSlide(MeshData md)
        {
            BoxMinMax(md, new Vector3(-0.033f, 0.105f, -0.08f), new Vector3(0.033f, 0.18f, 0.29f), Slot.Slide);
            // Rear serrations (slightly proud dark strips).
            for (int i = 0; i < 3; i++)
            {
                float z = -0.07f + i * 0.022f;
                BoxMinMax(md, new Vector3(-0.0345f, 0.115f, z), new Vector3(0.0345f, 0.17f, z + 0.01f), Slot.Polymer);
            }

            // Ejection port (dark inset on the top right) with a brass glint.
            BoxMinMax(md, new Vector3(0.0f, 0.176f, 0.06f), new Vector3(0.028f, 0.182f, 0.14f), Slot.Bore);
            BoxMinMax(md, new Vector3(0.008f, 0.177f, 0.085f), new Vector3(0.02f, 0.183f, 0.105f), Slot.Brass);
            // Sights: rear notch (two posts) and front post, white dots.
            BoxMinMax(md, new Vector3(-0.03f, 0.18f, -0.075f), new Vector3(-0.009f, 0.205f, -0.055f), Slot.Polymer);
            BoxMinMax(md, new Vector3(0.009f, 0.18f, -0.075f), new Vector3(0.03f, 0.205f, -0.055f), Slot.Polymer);
            BoxMinMax(md, new Vector3(-0.024f, 0.19f, -0.0765f), new Vector3(-0.015f, 0.198f, -0.074f), Slot.Sight);
            BoxMinMax(md, new Vector3(0.015f, 0.19f, -0.0765f), new Vector3(0.024f, 0.198f, -0.074f), Slot.Sight);
            BoxMinMax(md, new Vector3(-0.008f, 0.18f, 0.255f), new Vector3(0.008f, 0.206f, 0.275f), Slot.Polymer);
            BoxMinMax(md, new Vector3(-0.004f, 0.192f, 0.2535f), new Vector3(0.004f, 0.2f, 0.256f), Slot.Sight);
            // Muzzle bore.
            BoxMinMax(md, new Vector3(-0.012f, 0.128f, 0.289f), new Vector3(0.012f, 0.152f, 0.293f), Slot.Bore);
        }

        static void BuildGlockMag(MeshData md)
        {
            var rot = Quaternion.Euler(GripAngle, 0f, 0f);
            // Body (mostly hidden inside the grip), base plate below the grip, top round.
            Box(md, new Vector3(0f, -0.02f, -0.012f), new Vector3(0.042f, 0.235f, 0.07f), Slot.MetalDark, rot);
            Box(md, rot * new Vector3(0f, -0.145f, 0f) + new Vector3(0f, -0.02f, -0.012f), new Vector3(0.064f, 0.03f, 0.096f), Slot.Polymer, rot);
            Box(md, rot * new Vector3(0f, 0.12f, 0.005f) + new Vector3(0f, -0.02f, -0.012f), new Vector3(0.022f, 0.016f, 0.05f), Slot.Brass, rot);
        }

        static void BuildMedkit(MeshData md)
        {
            // Carry handle in the fist, two posts, case hanging below, red crosses on both broad sides, grey latches.
            BoxMinMax(md, new Vector3(-0.02f, -0.02f, -0.09f), new Vector3(0.02f, 0.02f, 0.09f), Slot.MedGrey);
            BoxMinMax(md, new Vector3(-0.018f, -0.135f, -0.095f), new Vector3(0.018f, 0.0f, -0.065f), Slot.MedGrey);
            BoxMinMax(md, new Vector3(-0.018f, -0.135f, 0.065f), new Vector3(0.018f, 0.0f, 0.095f), Slot.MedGrey);
            BoxMinMax(md, new Vector3(-0.07f, -0.37f, -0.17f), new Vector3(0.07f, -0.13f, 0.17f), Slot.MedWhite);
            // Lid seam.
            BoxMinMax(md, new Vector3(-0.0725f, -0.165f, -0.1725f), new Vector3(0.0725f, -0.155f, 0.1725f), Slot.MedGrey);
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = side * 0.07f, x1 = side * 0.077f;
                float xa = Mathf.Min(x0, x1), xb = Mathf.Max(x0, x1);
                BoxMinMax(md, new Vector3(xa, -0.33f, -0.03f), new Vector3(xb, -0.18f, 0.03f), Slot.MedRed);
                BoxMinMax(md, new Vector3(xa, -0.285f, -0.075f), new Vector3(xb, -0.225f, 0.075f), Slot.MedRed);
            }

            BoxMinMax(md, new Vector3(-0.075f, -0.19f, -0.15f), new Vector3(0.075f, -0.15f, -0.12f), Slot.MedGrey);
            BoxMinMax(md, new Vector3(-0.075f, -0.19f, 0.12f), new Vector3(0.075f, -0.15f, 0.15f), Slot.MedGrey);
        }

        static void BuildChainsawBody(MeshData md)
        {
            // Rear handle through the right fist, engine body (orange), top wrap handle, guard, guide bar.
            BoxMinMax(md, new Vector3(-0.022f, -0.03f, -0.12f), new Vector3(0.022f, 0.03f, 0.06f), Slot.HandleDark);
            BoxMinMax(md, new Vector3(-0.022f, -0.03f, -0.14f), new Vector3(0.022f, 0.12f, -0.11f), Slot.HandleDark);
            BoxMinMax(md, new Vector3(-0.085f, -0.07f, 0.04f), new Vector3(0.085f, 0.15f, 0.36f), Slot.SawOrange);
            BoxMinMax(md, new Vector3(-0.088f, 0.0f, 0.12f), new Vector3(0.088f, 0.12f, 0.28f), Slot.MetalDark);   // cylinder fins
            BoxMinMax(md, new Vector3(0.085f, -0.03f, 0.09f), new Vector3(0.1f, 0.06f, 0.16f), Slot.Polymer);        // pull starter
            BoxMinMax(md, new Vector3(-0.065f, 0.15f, 0.06f), new Vector3(0.065f, 0.19f, 0.3f), Slot.Polymer);      // tank cap / top
            // Wrap handle: two posts and the bar across.
            BoxMinMax(md, new Vector3(-0.1f, 0.19f, 0.18f), new Vector3(0.04f, 0.23f, 0.22f), Slot.HandleDark);
            BoxMinMax(md, new Vector3(-0.1f, 0.0f, 0.18f), new Vector3(-0.07f, 0.23f, 0.22f), Slot.HandleDark);
            BoxMinMax(md, new Vector3(0.02f, 0.15f, 0.18f), new Vector3(0.05f, 0.23f, 0.22f), Slot.HandleDark);
            BoxMinMax(md, new Vector3(-0.09f, 0.0f, 0.34f), new Vector3(0.05f, 0.24f, 0.37f), Slot.Polymer);        // hand guard
            // Guide bar.
            BoxMinMax(md, new Vector3(-0.008f, -0.01f, 0.36f), new Vector3(0.008f, 0.09f, 0.84f), Slot.SawBar);
            BoxMinMax(md, new Vector3(-0.008f, 0.0f, 0.84f), new Vector3(0.008f, 0.08f, 0.87f), Slot.SawBar);
        }

        static void BuildSyringeBody(MeshData md)
        {
            // Barrel through the fist (z -0.1 .. 0.08) with blue adrenaline inside, finger flange, hub, needle.
            BoxMinMax(md, new Vector3(-0.022f, -0.022f, -0.1f), new Vector3(0.022f, 0.022f, 0.08f), Slot.SyrBarrel);
            BoxMinMax(md, new Vector3(-0.017f, -0.017f, -0.03f), new Vector3(0.017f, 0.017f, 0.075f), Slot.SyrLiquid);
            BoxMinMax(md, new Vector3(-0.045f, -0.012f, -0.104f), new Vector3(0.045f, 0.012f, -0.092f), Slot.SyrBarrel);
            BoxMinMax(md, new Vector3(-0.009f, -0.009f, 0.08f), new Vector3(0.009f, 0.009f, 0.105f), Slot.SyrCap);
            BoxMinMax(md, new Vector3(-0.0025f, -0.0025f, 0.105f), new Vector3(0.0025f, 0.0025f, 0.2f), Slot.Edge);
            // label band
            BoxMinMax(md, new Vector3(-0.0235f, -0.0235f, 0.0f), new Vector3(0.0235f, 0.0235f, 0.03f), Slot.SyrCap);
        }

        static void BuildSyringePlunger(MeshData md)
        {
            // Rod sticking out of the back of the barrel + thumb plate (slides forward when pressed).
            BoxMinMax(md, new Vector3(-0.008f, -0.008f, -0.15f), new Vector3(0.008f, 0.008f, -0.03f), Slot.SyrBarrel);
            BoxMinMax(md, new Vector3(-0.03f, -0.03f, -0.162f), new Vector3(0.03f, 0.03f, -0.15f), Slot.MetalDark);
        }

        static void BuildFork(MeshData md)
        {
            // Handle through the fist, narrow neck, head plate and four tines (along +Z).
            BoxMinMax(md, new Vector3(-0.012f, -0.004f, -0.11f), new Vector3(0.012f, 0.004f, 0.06f), Slot.Blade);
            BoxMinMax(md, new Vector3(-0.007f, -0.003f, 0.06f), new Vector3(0.007f, 0.003f, 0.1f), Slot.Blade);
            BoxMinMax(md, new Vector3(-0.016f, -0.003f, 0.1f), new Vector3(0.016f, 0.003f, 0.125f), Slot.Edge);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.0135f + i * 0.009f;
                BoxMinMax(md, new Vector3(x - 0.0022f, -0.0025f, 0.125f), new Vector3(x + 0.0022f, 0.0025f, 0.185f), Slot.Edge);
            }
        }

        static void BuildBandage(MeshData md)
        {
            // Gauze roll in the fist + a loose strip hanging from it.
            BoxMinMax(md, new Vector3(-0.045f, -0.04f, -0.04f), new Vector3(0.045f, 0.04f, 0.04f), Slot.Bandage);
            BoxMinMax(md, new Vector3(-0.04f, -0.16f, 0.036f), new Vector3(0.04f, -0.03f, 0.042f), Slot.Bandage);
        }

        static void BuildRcBody(MeshData md)
        {
            // Chassis tub + bumpers.
            BoxMinMax(md, new Vector3(-0.075f, 0.03f, -0.2f), new Vector3(0.075f, 0.058f, 0.2f), Slot.RcChassis);
            BoxMinMax(md, new Vector3(-0.11f, 0.035f, 0.205f), new Vector3(0.11f, 0.065f, 0.235f), Slot.RcChassis);   // front bumper
            BoxMinMax(md, new Vector3(-0.105f, 0.04f, -0.225f), new Vector3(0.105f, 0.068f, -0.2f), Slot.RcChassis);  // rear bumper
            BoxMinMax(md, new Vector3(-0.03f, 0.03f, 0.235f), new Vector3(0.03f, 0.04f, 0.25f), Slot.RcAccent);       // tow hook
            // Body shell (narrower in the middle = wheel arches).
            BoxMinMax(md, new Vector3(-0.1f, 0.058f, 0.1f), new Vector3(0.1f, 0.1f, 0.205f), Slot.RcPaint);      // nose
            BoxMinMax(md, new Vector3(-0.092f, 0.058f, -0.08f), new Vector3(0.092f, 0.11f, 0.1f), Slot.RcPaint);  // doors
            BoxMinMax(md, new Vector3(-0.1f, 0.058f, -0.2f), new Vector3(0.1f, 0.108f, -0.08f), Slot.RcPaint);    // rear quarter
            BoxMinMax(md, new Vector3(-0.098f, 0.1f, 0.1f), new Vector3(0.098f, 0.112f, 0.19f), Slot.RcPaint);    // hood
            // Cabin + glass.
            BoxMinMax(md, new Vector3(-0.072f, 0.11f, -0.1f), new Vector3(0.072f, 0.168f, 0.06f), Slot.RcPaint);
            BoxMinMax(md, new Vector3(-0.064f, 0.114f, 0.06f), new Vector3(0.064f, 0.16f, 0.072f), Slot.RcGlass);   // windscreen
            BoxMinMax(md, new Vector3(-0.06f, 0.116f, -0.112f), new Vector3(0.06f, 0.158f, -0.1f), Slot.RcGlass);   // rear window
            BoxMinMax(md, new Vector3(0.072f, 0.118f, -0.08f), new Vector3(0.0745f, 0.158f, 0.045f), Slot.RcGlass);  // side windows
            BoxMinMax(md, new Vector3(-0.0745f, 0.118f, -0.08f), new Vector3(-0.072f, 0.158f, 0.045f), Slot.RcGlass);
            BoxMinMax(md, new Vector3(-0.022f, 0.168f, -0.03f), new Vector3(0.022f, 0.182f, 0.04f), Slot.RcStripe); // roof scoop
            BoxMinMax(md, new Vector3(-0.016f, 0.17f, 0.04f), new Vector3(0.016f, 0.178f, 0.043f), Slot.RcChassis); // scoop mouth
            // Rally stripes over hood / roof / boot.
            foreach (float x in new[] { -0.03f, 0.012f })
            {
                BoxMinMax(md, new Vector3(x, 0.112f, 0.1f), new Vector3(x + 0.018f, 0.1135f, 0.19f), Slot.RcStripe);
                BoxMinMax(md, new Vector3(x, 0.168f, -0.1f), new Vector3(x + 0.018f, 0.1695f, -0.03f), Slot.RcStripe);
                BoxMinMax(md, new Vector3(x, 0.108f, -0.2f), new Vector3(x + 0.018f, 0.1095f, -0.1f), Slot.RcStripe);
            }
            // Yellow side livery + door numbers.
            BoxMinMax(md, new Vector3(0.1f, 0.072f, 0.1f), new Vector3(0.102f, 0.082f, 0.2f), Slot.RcAccent);
            BoxMinMax(md, new Vector3(-0.102f, 0.072f, 0.1f), new Vector3(-0.1f, 0.082f, 0.2f), Slot.RcAccent);
            BoxMinMax(md, new Vector3(0.092f, 0.06f, -0.08f), new Vector3(0.094f, 0.068f, 0.1f), Slot.RcAccent);
            BoxMinMax(md, new Vector3(-0.094f, 0.06f, -0.08f), new Vector3(-0.092f, 0.068f, 0.1f), Slot.RcAccent);
            BoxMinMax(md, new Vector3(0.1f, 0.072f, -0.2f), new Vector3(0.102f, 0.082f, -0.08f), Slot.RcAccent);
            BoxMinMax(md, new Vector3(-0.102f, 0.072f, -0.2f), new Vector3(-0.1f, 0.082f, -0.08f), Slot.RcAccent);
            BoxFull(md, new Vector3(0.0935f, 0.088f, 0.01f), new Vector3(0.002f, 0.04f, 0.04f), Slot.RcNumber, Quaternion.identity);
            BoxFull(md, new Vector3(-0.0935f, 0.088f, 0.01f), new Vector3(0.002f, 0.04f, 0.04f), Slot.RcNumber, Quaternion.Euler(0f, 180f, 0f));
            // Lights: headlights, rally light pod on the bumper, tail lights.
            BoxMinMax(md, new Vector3(0.05f, 0.075f, 0.205f), new Vector3(0.088f, 0.092f, 0.209f), Slot.RcLight);
            BoxMinMax(md, new Vector3(-0.088f, 0.075f, 0.205f), new Vector3(-0.05f, 0.092f, 0.209f), Slot.RcLight);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.066f + i * 0.044f;
                BoxMinMax(md, new Vector3(x - 0.014f, 0.066f, 0.222f), new Vector3(x + 0.014f, 0.09f, 0.24f), Slot.RcChassis);
                BoxMinMax(md, new Vector3(x - 0.01f, 0.07f, 0.24f), new Vector3(x + 0.01f, 0.086f, 0.243f), Slot.RcLight);
            }
            BoxMinMax(md, new Vector3(0.06f, 0.082f, -0.204f), new Vector3(0.094f, 0.096f, -0.2f), Slot.RcTail);
            BoxMinMax(md, new Vector3(-0.094f, 0.082f, -0.204f), new Vector3(-0.06f, 0.096f, -0.2f), Slot.RcTail);
            // Rear wing on two posts.
            BoxMinMax(md, new Vector3(0.04f, 0.108f, -0.195f), new Vector3(0.05f, 0.15f, -0.18f), Slot.RcChassis);
            BoxMinMax(md, new Vector3(-0.05f, 0.108f, -0.195f), new Vector3(-0.04f, 0.15f, -0.18f), Slot.RcChassis);
            BoxMinMax(md, new Vector3(-0.1f, 0.15f, -0.215f), new Vector3(0.1f, 0.16f, -0.165f), Slot.RcStripe);
            BoxMinMax(md, new Vector3(-0.107f, 0.14f, -0.218f), new Vector3(-0.1f, 0.17f, -0.162f), Slot.RcAccent);  // end plates
            BoxMinMax(md, new Vector3(0.1f, 0.14f, -0.218f), new Vector3(0.107f, 0.17f, -0.162f), Slot.RcAccent);
            // Exhaust + antenna mount.
            BoxMinMax(md, new Vector3(-0.07f, 0.045f, -0.235f), new Vector3(-0.05f, 0.06f, -0.225f), Slot.RcRim);
            BoxMinMax(md, new Vector3(0.062f, 0.108f, -0.178f), new Vector3(0.078f, 0.12f, -0.162f), Slot.RcChassis);
        }

        static void BuildRcWheel(MeshData md, float side)
        {
            // Octagonal blocky tyre (crossed boxes), rim + yellow hub cap on the outer face.
            float r = RcWheelRadius, w = 0.042f;
            Box(md, Vector3.zero, new Vector3(w, r * 2f, r * 1.2f), Slot.RcTire, Quaternion.identity);
            Box(md, Vector3.zero, new Vector3(w * 0.99f, r * 1.2f, r * 2f), Slot.RcTire, Quaternion.identity);
            Box(md, Vector3.zero, new Vector3(w * 0.98f, r * 1.7f, r * 1.7f), Slot.RcTire, Quaternion.Euler(45f, 0f, 0f));
            Box(md, new Vector3(side * w * 0.5f, 0f, 0f), new Vector3(0.006f, r * 1.2f, r * 1.2f), Slot.RcRim, Quaternion.identity);
            Box(md, new Vector3(side * (w * 0.5f + 0.004f), 0f, 0f), new Vector3(0.005f, r * 0.5f, r * 0.5f), Slot.RcAccent, Quaternion.identity);
        }

        static void BuildRcAntenna(MeshData md)
        {
            // Thin whip with a red tip; origin = mount on the body, along +Y.
            BoxMinMax(md, new Vector3(-0.0025f, 0f, -0.0025f), new Vector3(0.0025f, RcAntennaLength, 0.0025f), Slot.RcChassis);
            BoxMinMax(md, new Vector3(-0.007f, RcAntennaLength - 0.004f, -0.007f), new Vector3(0.007f, RcAntennaLength + 0.01f, 0.007f), Slot.RcLed);
        }

        static void BuildRcRemote(MeshData md)
        {
            // Two-stick transmitter held in both hands: right fist = origin (right grip), body spans to the left fist.
            int start = md.V.Count;
            BoxMinMax(md, new Vector3(-0.19f, -0.04f, -0.02f), new Vector3(0.04f, 0.04f, 0.012f), Slot.RcRemote);
            BoxMinMax(md, new Vector3(-0.19f, 0.04f, -0.02f), new Vector3(0.04f, 0.06f, 0.012f), Slot.RcAccent);   // yellow top band
            BoxMinMax(md, new Vector3(-0.17f, -0.075f, -0.02f), new Vector3(-0.13f, -0.04f, 0.01f), Slot.RcRemote); // left grip
            BoxMinMax(md, new Vector3(0f, -0.075f, -0.02f), new Vector3(0.04f, -0.04f, 0.01f), Slot.RcRemote);      // right grip
            BoxMinMax(md, new Vector3(-0.1f, 0.0f, 0.012f), new Vector3(-0.05f, 0.035f, 0.015f), Slot.RcGlass);     // screen
            BoxMinMax(md, new Vector3(-0.078f, -0.03f, 0.012f), new Vector3(-0.07f, -0.022f, 0.016f), Slot.RcLed); // power LED
            BoxMinMax(md, new Vector3(-0.19f, 0.06f, -0.012f), new Vector3(-0.175f, 0.075f, 0.004f), Slot.RcChassis); // antenna socket
            BoxMinMax(md, new Vector3(-0.186f, 0.075f, -0.008f), new Vector3(-0.179f, 0.25f, -0.001f), Slot.RcChassis);
            BoxMinMax(md, new Vector3(-0.188f, 0.25f, -0.01f), new Vector3(-0.177f, 0.26f, 0.001f), Slot.RcLed);
            // Grips round the fists: shift the whole transmitter up so the fist (origin) sits inside the right grip.
            for (int i = start; i < md.V.Count; i++) md.V[i] += new Vector3(0f, RcRemoteLift, 0f);
        }

        static void BoxFull(MeshData md, Vector3 center, Vector3 size, Slot slot, Quaternion rot)
        {
            int start = md.UV.Count;
            Box(md, center, size, slot, rot);
            // Stretch the whole 8x8 tile over every face (decal-like).
            int s = (int)slot;
            float ox = (s % 8) * 8f, oy = (s / 8) * 8f;
            const float inv = 1f / 64f, e = 0.01f;
            for (int i = start; i < md.UV.Count; i += 4)
            {
                md.UV[i] = new Vector2((ox + e) * inv, (oy + e) * inv);
                md.UV[i + 1] = new Vector2((ox + 8f - e) * inv, (oy + e) * inv);
                md.UV[i + 2] = new Vector2((ox + e) * inv, (oy + 8f - e) * inv);
                md.UV[i + 3] = new Vector2((ox + 8f - e) * inv, (oy + 8f - e) * inv);
            }
        }

        static void BuildChainsawTooth(MeshData md)
        {
            // One chain link with its cutter (local +Y = outward from the bar, +Z = running direction).
            BoxMinMax(md, new Vector3(-0.0125f, -0.006f, -0.009f), new Vector3(0.0125f, 0.007f, 0.009f), Slot.SawBar);
            BoxMinMax(md, new Vector3(0.002f, 0.007f, -0.008f), new Vector3(0.0125f, 0.013f, 0.004f), Slot.SawBar);
        }

        static void BuildChainsawChain(MeshData md)
        {
            // Chain around the bar (slightly wider than the bar so it is visible); jittered while running.
            BoxMinMax(md, new Vector3(-0.012f, 0.088f, 0.36f), new Vector3(0.012f, 0.106f, 0.875f), Slot.SawChain);
            BoxMinMax(md, new Vector3(-0.012f, -0.028f, 0.36f), new Vector3(0.012f, -0.01f, 0.875f), Slot.SawChain);
            BoxMinMax(md, new Vector3(-0.012f, -0.028f, 0.87f), new Vector3(0.012f, 0.106f, 0.89f), Slot.SawChain);
        }

        static void BoxMinMax(MeshData md, Vector3 min, Vector3 max, Slot slot)
        {
            Box(md, (min + max) * 0.5f, max - min, slot, Quaternion.identity);
        }

        static void Box(MeshData md, Vector3 center, Vector3 size, Slot slot, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            // +X, -X, +Y, -Y, +Z, -Z ; each face: normal, axis u, axis v, extent along u, v.
            AddFace(md, center, rot, Vector3.right, Vector3.forward, Vector3.up, h.x, h.z, h.y, size.z, size.y, slot);
            AddFace(md, center, rot, Vector3.left, Vector3.back, Vector3.up, h.x, h.z, h.y, size.z, size.y, slot);
            AddFace(md, center, rot, Vector3.up, Vector3.right, Vector3.forward, h.y, h.x, h.z, size.x, size.z, slot);
            AddFace(md, center, rot, Vector3.down, Vector3.right, Vector3.back, h.y, h.x, h.z, size.x, size.z, slot);
            AddFace(md, center, rot, Vector3.forward, Vector3.left, Vector3.up, h.z, h.x, h.y, size.x, size.y, slot);
            AddFace(md, center, rot, Vector3.back, Vector3.right, Vector3.up, h.z, h.x, h.y, size.x, size.y, slot);
        }

        static void AddFace(MeshData md, Vector3 c, Quaternion rot, Vector3 n, Vector3 u, Vector3 v,
            float dn, float du, float dv, float sizeU, float sizeV, Slot slot)
        {
            int i = md.V.Count;
            Vector3 fc = n * dn;
            md.V.Add(c + rot * (fc - u * du - v * dv));
            md.V.Add(c + rot * (fc + u * du - v * dv));
            md.V.Add(c + rot * (fc - u * du + v * dv));
            md.V.Add(c + rot * (fc + u * du + v * dv));
            Vector3 wn = rot * n;
            for (int k = 0; k < 4; k++) md.N.Add(wn);

            // One atlas texel per prop pixel, at most a full 8x8 tile.
            int s = (int)slot;
            float ox = (s % 8) * 8f, oy = (s / 8) * 8f;
            float tu = Mathf.Clamp(Mathf.Round(sizeU / U), 1f, 8f);
            float tv = Mathf.Clamp(Mathf.Round(sizeV / U), 1f, 8f);
            const float inv = 1f / 64f, e = 0.01f;
            md.UV.Add(new Vector2((ox + e) * inv, (oy + e) * inv));
            md.UV.Add(new Vector2((ox + tu - e) * inv, (oy + e) * inv));
            md.UV.Add(new Vector2((ox + e) * inv, (oy + tv - e) * inv));
            md.UV.Add(new Vector2((ox + tu - e) * inv, (oy + tv - e) * inv));

            // u = n x v for every face above (u is the viewer's right), so bl, tl, tr / bl, tr, br is clockwise.
            md.T.Add(i);
            md.T.Add(i + 2);
            md.T.Add(i + 3);
            md.T.Add(i);
            md.T.Add(i + 3);
            md.T.Add(i + 1);
        }
    }

    /// <summary>Moving parts of an RC car model.</summary>
    public class RcCarParts
    {
        public Transform Root, Body, Antenna;
        /// <summary>FR, FL, RR, RL.</summary>
        public Transform[] Wheels;
        /// <summary>Front steering pivots (R, L).</summary>
        public Transform[] Steer;
    }

    /// <summary>Runtime handle on a prop instance (moving parts + render mode).</summary>
    public class HeldProp : MonoBehaviour
    {
        public HeldItem Item;
        public Transform Slide, Mag, Muzzle;
        Renderer[] renderers;

        public SodaCan Can;

        public void CollectRenderers() => renderers = GetComponentsInChildren<Renderer>(true);

        /// <summary>Soda can: brand (label) and how far the tab is open (0..1).</summary>
        public void SetCan(int brand, float open)
        {
            if (Can == null) return;
            Can.Brand = brand;
            Can.SetOpen(open);
        }

        /// <summary>0 = slide forward (in battery), 1 = locked back.</summary>
        public void SetSlideBack(float amount)
        {
            if (Slide != null) Slide.localPosition = new Vector3(0f, 0f, -BlockyProps.GlockSlideTravel * Mathf.Clamp01(amount));
        }

        /// <summary>Syringe: 0 = plunger out, 1 = fully pressed; cap on / off.</summary>
        public void SetSyringe(float pressed, bool capOn)
        {
            if (Item != HeldItem.Syringe) return;
            if (Slide != null) Slide.localPosition = new Vector3(0f, 0f, BlockyProps.SyringePlungerTravel * Mathf.Clamp01(pressed));
            if (Mag != null && Mag.gameObject.activeSelf != capOn) Mag.gameObject.SetActive(capOn);
        }

        /// <summary>Transmitter sticks: x = steering (right stick sideways), y = throttle (left stick forward/back).</summary>
        public void SetRcSticks(Vector2 input)
        {
            if (Item != HeldItem.RcRemote) return;
            if (Mag != null) Mag.localRotation = Quaternion.Euler(-22f * input.y, 0f, 0f);
            if (Slide != null) Slide.localRotation = Quaternion.Euler(0f, 22f * input.x, 0f);
        }

        MaterialPropertyBlock tintBlock;

        /// <summary>Multiplies the prop colour (toy brick colours).</summary>
        public void SetTint(Color c)
        {
            if (renderers == null) CollectRenderers();
            if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
            tintBlock.SetColor("_Color", c);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(tintBlock);
        }

        // ---------------------------------------------------------------- chainsaw

        Transform sawShake, starterGrip, starterCord;
        Transform[] teeth;
        Renderer[] teethR;
        ParticleSystem exhaust;
        float chainPhase, sawBlood, lastExhaust = -1f;
        MaterialPropertyBlock bloodBlock;
        static Material smokeMat;

        const float ChainLoop = 0.505f * 2f + Mathf.PI * 0.058f * 2f; // top + bottom runs + both ends
        const int TeethCount = 36;

        internal void BuildSawExtras(Transform shake, int layer)
        {
            sawShake = shake;
            teeth = new Transform[TeethCount];
            teethR = new Renderer[TeethCount];
            var mesh = BlockyProps.GetMeshInternal("ChainsawTooth");
            for (int i = 0; i < TeethCount; i++)
            {
                teeth[i] = BlockyProps.AddPartInternal(shake, "Tooth" + i, mesh, layer);
                teethR[i] = teeth[i].GetComponent<Renderer>();
                teethR[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            PlaceTeeth(0f);

            starterGrip = BlockyProps.AddPartInternal(shake, "StarterGrip", BlockyProps.GetMeshInternal("ChainsawGrip"), layer);
            starterCord = BlockyProps.AddPartInternal(shake, "StarterCord", BlockyProps.GetMeshInternal("ChainsawCord"), layer);
            SetStarterPull(0f);

            // Exhaust on the left side of the engine.
            var ex = new GameObject("Exhaust");
            ex.layer = layer;
            ex.transform.SetParent(shake, false);
            ex.transform.localPosition = new Vector3(-0.09f, 0.05f, 0.1f);
            ex.transform.localRotation = Quaternion.LookRotation(new Vector3(-1f, 0.5f, -0.3f).normalized);
            exhaust = ex.AddComponent<ParticleSystem>();
            exhaust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = exhaust.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.56f, 0.6f, 0.35f), new Color(0.3f, 0.3f, 0.32f, 0.45f));
            main.gravityModifier = -0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var em = exhaust.emission;
            em.rateOverTime = 0f;
            var shape = exhaust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.008f;
            var sz = exhaust.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.6f));
            var col = exhaust.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = ex.GetComponent<ParticleSystemRenderer>();
            pr.material = SmokeMaterial;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            exhaust.Play();
        }

        static Material SmokeMaterial
        {
            get
            {
                if (smokeMat != null) return smokeMat;
                const int S = 32;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply();
                smokeMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, name = "SawSmoke" };
                return smokeMat;
            }
        }

        /// <summary>Teeth along the bar loop: forward along the top, round the tip, back along the bottom.</summary>
        void PlaceTeeth(float phase)
        {
            if (teeth == null) return;
            const float zA = 0.37f, zB = 0.875f, yTop = 0.097f, yBot = -0.019f, yMid = 0.039f, r = 0.058f;
            const float run = zB - zA, arc = Mathf.PI * r;
            for (int i = 0; i < teeth.Length; i++)
            {
                float s = Mathf.Repeat(phase + i * (ChainLoop / teeth.Length), ChainLoop);
                Vector3 pos, outward, tangent;
                if (s < run) { pos = new Vector3(0f, yTop, zA + s); outward = Vector3.up; tangent = Vector3.forward; }
                else if (s < run + arc)
                {
                    float a = (s - run) / r; // 0 at the top .. pi at the bottom, round the tip
                    outward = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                    pos = new Vector3(0f, yMid, zB) + outward * r;
                    tangent = new Vector3(0f, -Mathf.Sin(a), Mathf.Cos(a));
                }
                else if (s < run * 2f + arc) { pos = new Vector3(0f, yBot, zB - (s - run - arc)); outward = Vector3.down; tangent = Vector3.back; }
                else
                {
                    float a = (s - run * 2f - arc) / r; // inside the engine body: hidden
                    outward = new Vector3(0f, -Mathf.Cos(a), -Mathf.Sin(a));
                    pos = new Vector3(0f, yMid, zA) + outward * r;
                    tangent = new Vector3(0f, Mathf.Sin(a), -Mathf.Cos(a));
                }
                bool visible = pos.z >= zA - 0.004f;
                if (teethR[i] != null && teethR[i].enabled != visible) teethR[i].enabled = visible;
                teeth[i].localPosition = pos + new Vector3(i % 2 == 0 ? 0.0015f : -0.0015f, 0f, 0f);
                teeth[i].localRotation = Quaternion.LookRotation(tangent, outward);
                teeth[i].localScale = new Vector3(i % 2 == 0 ? 1f : -1f, 1f, 1f); // cutters alternate left / right
            }
        }

        /// <summary>
        /// Chainsaw engine state: 0 = off, ~0.3 = idling (body buzzes, chain still: the clutch is open), 1 = full throttle
        /// (chain races round the bar, the body shakes hard, thick exhaust).
        /// </summary>
        public void SetChainRun(float amount, float time)
        {
            if (Item != HeldItem.Chainsaw) return;
            float dt = lastExhaust < 0f ? 0f : Mathf.Clamp(Time.time - lastExhaust, 0f, 0.1f);
            lastExhaust = Time.time;
            bool running = amount > 0.05f;
            float rev = Mathf.InverseLerp(0.5f, 1f, amount);

            // Chain: never more than ~45 % of a link per frame, so it always reads as moving forward.
            float step = rev * 3.4f * dt;
            float link = ChainLoop / TeethCount;
            chainPhase = Mathf.Repeat(chainPhase + Mathf.Min(step, link * 0.45f), ChainLoop);
            PlaceTeeth(chainPhase);

            if (sawShake != null)
            {
                float j = running ? 0.0012f + 0.0035f * rev : 0f;
                sawShake.localPosition = new Vector3(Mathf.Sin(time * 97f) * j, Mathf.Sin(time * 131f + 1.3f) * j, Mathf.Sin(time * 71f) * j * 0.6f);
                float a = running ? 0.25f + 0.9f * rev : 0f;
                sawShake.localRotation = Quaternion.Euler(Mathf.Sin(time * 89f) * a, Mathf.Sin(time * 113f) * a * 0.6f, Mathf.Sin(time * 103f) * a);
            }
            if (Slide != null) Slide.localPosition = new Vector3(0f, running ? Mathf.Sin(time * 151f) * 0.0006f : 0f, 0f);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                em.rateOverTime = running ? 7f + 34f * rev : 0f;
                var main = exhaust.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f + 0.5f * rev, 0.6f + 1.2f * rev);
            }
        }

        /// <summary>Starter cord pulled out 0..1 (the grip follows the left hand during the pull-start).</summary>
        public void SetStarterPull(float pull)
        {
            if (starterGrip == null) return;
            Vector3 anchor = BlockyProps.ChainsawStarterLocal - new Vector3(0.012f, 0f, 0f);
            Vector3 grip = BlockyProps.ChainsawStarterLocal + BlockyProps.ChainsawPullDir * (BlockyProps.ChainsawPullLength * Mathf.Clamp01(pull));
            starterGrip.localPosition = grip;
            starterGrip.localRotation = Quaternion.identity;
            Vector3 d = grip - anchor;
            bool show = d.magnitude > 0.02f;
            if (starterCord.gameObject.activeSelf != show) starterCord.gameObject.SetActive(show);
            if (show)
            {
                starterCord.localPosition = anchor;
                starterCord.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                starterCord.localScale = new Vector3(1f, 1f, d.magnitude);
            }
        }

        /// <summary>Blood on the chain and bar after a kill (1 = soaked), fades as it is wiped off by running.</summary>
        public void SetSawBlood(float amount)
        {
            if (teethR == null || Mathf.Abs(amount - sawBlood) < 0.01f) return;
            sawBlood = amount;
            if (bloodBlock == null) bloodBlock = new MaterialPropertyBlock();
            bloodBlock.SetColor("_Color", Color.Lerp(Color.white, new Color(0.5f, 0.04f, 0.04f), Mathf.Clamp01(amount)));
            foreach (var r in teethR) if (r != null) r.SetPropertyBlock(bloodBlock);
            if (Slide != null && Slide.TryGetComponent<Renderer>(out var cr)) cr.SetPropertyBlock(bloodBlock);
        }

        public void SetMagVisible(bool visible)
        {
            if (Mag != null && Mag.gameObject.activeSelf != visible) Mag.gameObject.SetActive(visible);
        }

        public void SetRenderMode(ShadowCastingMode mode, bool receiveShadows)
        {
            if (renderers == null) CollectRenderers();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.shadowCastingMode = mode;
                r.receiveShadows = receiveShadows;
            }
        }

        public void SetLayer(int layer)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
