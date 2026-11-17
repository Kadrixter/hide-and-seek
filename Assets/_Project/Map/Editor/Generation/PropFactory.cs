using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>A reusable prop mesh (local space, pivot on the floor) plus its materials and collider boxes.</summary>
    internal sealed class PropMesh
    {
        public Mesh Mesh;
        public Material[] Materials;
        public readonly List<Bounds> Colliders = new List<Bounds>();
    }

    internal enum CarType { Sedan, Hatchback, Suv, Van }

    /// <summary>Builds blocky cars and props from boxes/cylinders; meshes are cached and registered for saving.</summary>
    internal sealed class PropFactory
    {
        private readonly MaterialLibrary _lib;
        private readonly List<Mesh> _store;
        private readonly Dictionary<string, PropMesh> _cache = new Dictionary<string, PropMesh>();

        public PropFactory(MaterialLibrary lib, List<Mesh> meshStore)
        {
            _lib = lib;
            _store = meshStore;
        }

        private Material M(string n) => _lib[n];

        public PropMesh Get(string key, Action<MeshBuilder, PropMesh> build)
        {
            if (_cache.TryGetValue(key, out var pm)) return pm;
            pm = new PropMesh();
            var mb = new MeshBuilder(_lib);
            build(mb, pm);
            pm.Mesh = mb.ToMesh("Prop_" + key);
            pm.Materials = mb.Materials;
            if (pm.Colliders.Count == 0) pm.Colliders.Add(pm.Mesh.bounds);
            _store.Add(pm.Mesh);
            _cache[key] = pm;
            return pm;
        }

        private static Bounds B(Vector3 center, Vector3 size) => new Bounds(center, size);

        // ================================================================== cars

        public PropMesh Car(CarType type) => Get("Car_" + type, (mb, pm) => BuildCar(mb, pm, type));

        private void BuildCar(MeshBuilder mb, PropMesh pm, CarType type)
        {
            // slot 0 is always the paint (swapped per instance)
            mb.Reserve(M(MaterialLibrary.CarPaints[0]));
            var paint = M(MaterialLibrary.CarPaints[0]);
            var glass = M("Glass_Dark");
            var rubber = M("Rubber");
            var trim = M("Car_Trim");
            var head = M("Car_Headlight");
            var tail = M("Car_Taillight");
            var ind = M("Car_Indicator");
            var plate = M("Car_Plate");
            var chrome = M("Car_Chrome");

            float L, W, wheelR, bodyBottom, bodyTop, cabinH, cabinLen, cabinZ, wheelBase;
            switch (type)
            {
                case CarType.Hatchback:
                    L = 4.0f; W = 1.75f; wheelR = 0.31f; bodyBottom = 0.3f; bodyTop = 0.88f; cabinH = 0.55f;
                    cabinLen = 2.25f; cabinZ = -0.55f; wheelBase = 1.25f; break;
                case CarType.Suv:
                    L = 4.7f; W = 1.9f; wheelR = 0.38f; bodyBottom = 0.38f; bodyTop = 1.12f; cabinH = 0.62f;
                    cabinLen = 2.85f; cabinZ = -0.45f; wheelBase = 1.45f; break;
                case CarType.Van:
                    L = 5.1f; W = 2.0f; wheelR = 0.36f; bodyBottom = 0.36f; bodyTop = 1.1f; cabinH = 1.05f;
                    cabinLen = 4.15f; cabinZ = -0.45f; wheelBase = 1.65f; break;
                default:
                    L = 4.55f; W = 1.8f; wheelR = 0.32f; bodyBottom = 0.3f; bodyTop = 0.86f; cabinH = 0.5f;
                    cabinLen = 2.3f; cabinZ = -0.2f; wheelBase = 1.4f; break;
            }
            float hw = W * 0.5f, hl = L * 0.5f;
            float bodyH = bodyTop - bodyBottom;

            // underbody + main body
            mb.Box(new Vector3(0, bodyBottom - 0.06f, 0), new Vector3(W - 0.25f, 0.14f, L - 0.7f), trim);
            mb.Box(new Vector3(0, bodyBottom + bodyH * 0.5f, 0), new Vector3(W, bodyH, L), paint);
            // bumpers
            mb.Box(new Vector3(0, bodyBottom + 0.12f, hl + 0.04f), new Vector3(W - 0.02f, 0.22f, 0.1f), trim);
            mb.Box(new Vector3(0, bodyBottom + 0.12f, -hl - 0.04f), new Vector3(W - 0.02f, 0.22f, 0.1f), trim);
            // side skirts / door lines
            mb.Box(new Vector3(hw + 0.005f, bodyBottom + 0.07f, 0), new Vector3(0.02f, 0.1f, L - 1.9f), trim);
            mb.Box(new Vector3(-hw - 0.005f, bodyBottom + 0.07f, 0), new Vector3(0.02f, 0.1f, L - 1.9f), trim);
            float doorTop = bodyTop - 0.04f;
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (hw + 0.004f);
                mb.Box(new Vector3(x, (bodyBottom + 0.14f + doorTop) * 0.5f, cabinZ + 0.1f), new Vector3(0.012f, doorTop - bodyBottom - 0.14f, 0.015f), trim);
                if (type != CarType.Van)
                    mb.Box(new Vector3(x, (bodyBottom + 0.14f + doorTop) * 0.5f, cabinZ + cabinLen * 0.5f - 0.05f), new Vector3(0.012f, doorTop - bodyBottom - 0.14f, 0.015f), trim);
                // door handles
                mb.Box(new Vector3(s * (hw + 0.012f), bodyTop - 0.16f, cabinZ - 0.15f), new Vector3(0.02f, 0.03f, 0.14f), chrome);
                mb.Box(new Vector3(s * (hw + 0.012f), bodyTop - 0.16f, cabinZ + 0.75f), new Vector3(0.02f, 0.03f, 0.14f), chrome);
            }

            // wheels (inside the body footprint, slightly sticking out)
            foreach (float zs in new[] { -1f, 1f })
            foreach (float xs in new[] { -1f, 1f })
            {
                var c = new Vector3(xs * (hw - 0.1f), wheelR, zs * wheelBase);
                mb.Cylinder(c - Vector3.right * 0.13f, c + Vector3.right * 0.13f, wheelR, 14, rubber);
                mb.Cylinder(c + Vector3.right * (xs * 0.13f), c + Vector3.right * (xs * 0.145f), wheelR * 0.58f, 10, chrome);
                // dark wheel arch
                mb.Box(new Vector3(xs * (hw - 0.02f), bodyBottom + 0.12f, zs * wheelBase), new Vector3(0.05f, 0.24f, wheelR * 2.3f), trim, null, MeshBuilder.Faces.PosX | MeshBuilder.Faces.NegX);
            }

            // lights / plates / grille
            float lightY = bodyTop - 0.13f;
            foreach (float xs in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(xs * (hw - 0.3f), lightY, hl + 0.01f), new Vector3(0.36f, 0.12f, 0.03f), head);
                mb.Box(new Vector3(xs * (hw - 0.06f), lightY, hl - 0.05f), new Vector3(0.1f, 0.1f, 0.1f), ind);
                mb.Box(new Vector3(xs * (hw - 0.22f), lightY, -hl - 0.01f), new Vector3(0.32f, 0.13f, 0.03f), tail);
                // mirrors
                if (type != CarType.Van)
                    mb.Box(new Vector3(xs * (hw + 0.09f), bodyTop + 0.08f, cabinZ + cabinLen * 0.5f - 0.2f), new Vector3(0.16f, 0.1f, 0.08f), paint);
            }
            mb.Box(new Vector3(0, lightY - 0.02f, hl + 0.01f), new Vector3(W - 1.0f, 0.14f, 0.03f), trim);
            mb.Box(new Vector3(0, bodyBottom + 0.14f, hl + 0.095f), new Vector3(0.5f, 0.11f, 0.01f), plate);
            mb.Box(new Vector3(0, bodyBottom + 0.3f, -hl - 0.025f), new Vector3(0.5f, 0.11f, 0.01f), plate);

            float cabinBottom = bodyTop;
            if (type == CarType.Van)
            {
                // tall box van: cab with windscreen, cargo body
                float vanFrontZ = cabinZ + cabinLen * 0.5f;
                mb.Box(new Vector3(0, cabinBottom + cabinH * 0.5f, cabinZ), new Vector3(W - 0.04f, cabinH, cabinLen), paint);
                mb.Box(new Vector3(0, cabinBottom + cabinH * 0.55f, vanFrontZ + 0.01f), new Vector3(W - 0.2f, cabinH * 0.6f, 0.03f), glass);
                foreach (float xs in new[] { -1f, 1f })
                {
                    mb.Box(new Vector3(xs * (hw - 0.01f), cabinBottom + cabinH * 0.55f, vanFrontZ - 0.45f), new Vector3(0.03f, cabinH * 0.5f, 0.7f), glass);
                    mb.Box(new Vector3(xs * (hw + 0.08f), cabinBottom + 0.25f, vanFrontZ - 0.15f), new Vector3(0.1f, 0.22f, 0.06f), trim);
                    // sliding door rail
                    mb.Box(new Vector3(xs * (hw - 0.005f), cabinBottom + cabinH - 0.15f, cabinZ - 0.6f), new Vector3(0.02f, 0.03f, 2.0f), trim);
                }
                // rear door seam + windows
                mb.Box(new Vector3(0, cabinBottom + cabinH * 0.5f - 0.2f, -hl - 0.005f), new Vector3(0.015f, cabinH + bodyH - 0.3f, 0.02f), trim);
                mb.Box(new Vector3(0, cabinBottom + cabinH * 0.62f, -hl - 0.005f), new Vector3(W - 0.5f, 0.35f, 0.02f), glass);
                pm.Colliders.Add(B(new Vector3(0, (bodyBottom + cabinBottom + cabinH) * 0.5f, 0), new Vector3(W, cabinBottom + cabinH - bodyBottom, L)));
            }
            else
            {
                float cw = W - 0.24f;
                float ccz = cabinZ;
                // glass cabin
                mb.Box(new Vector3(0, cabinBottom + cabinH * 0.5f, ccz), new Vector3(cw, cabinH, cabinLen), glass);
                // roof
                mb.Box(new Vector3(0, cabinBottom + cabinH + 0.03f, ccz - 0.05f), new Vector3(cw + 0.04f, 0.06f, cabinLen - 0.25f), paint);
                // pillars
                float py = cabinBottom + cabinH * 0.5f;
                float px = cw * 0.5f + 0.01f;
                float[] pz = type == CarType.Hatchback
                    ? new[] { ccz + cabinLen * 0.5f - 0.05f, ccz + 0.2f, ccz - cabinLen * 0.5f + 0.08f }
                    : new[] { ccz + cabinLen * 0.5f - 0.05f, ccz + 0.1f, ccz - cabinLen * 0.5f + 0.08f };
                foreach (float xs in new[] { -1f, 1f })
                foreach (float z in pz)
                    mb.Box(new Vector3(xs * px, py, z), new Vector3(0.06f, cabinH, 0.1f), paint);
                // front/back pillar crossbars (windscreen frame)
                mb.Box(new Vector3(0, cabinBottom + cabinH - 0.03f, ccz + cabinLen * 0.5f - 0.01f), new Vector3(cw + 0.02f, 0.06f, 0.04f), paint);
                mb.Box(new Vector3(0, cabinBottom + cabinH - 0.03f, ccz - cabinLen * 0.5f + 0.01f), new Vector3(cw + 0.02f, 0.06f, 0.04f), paint);
                if (type == CarType.Suv)
                {
                    foreach (float xs in new[] { -1f, 1f })
                        mb.Box(new Vector3(xs * (cw * 0.5f - 0.1f), cabinBottom + cabinH + 0.09f, ccz - 0.05f), new Vector3(0.04f, 0.05f, cabinLen - 0.4f), trim);
                    // spare wheel
                    mb.Cylinder(new Vector3(0, bodyBottom + bodyH * 0.55f, -hl - 0.02f), new Vector3(0, bodyBottom + bodyH * 0.55f, -hl - 0.26f), 0.34f, 14, rubber);
                }
                pm.Colliders.Add(B(new Vector3(0, (bodyBottom - 0.1f + bodyTop) * 0.5f, 0), new Vector3(W, bodyTop - bodyBottom + 0.1f, L + 0.1f)));
                pm.Colliders.Add(B(new Vector3(0, cabinBottom + cabinH * 0.5f, ccz), new Vector3(cw, cabinH, cabinLen)));
            }
        }

        // ================================================================== props

        public PropMesh Pallet() => Get("Pallet", (mb, pm) => BuildPallet(mb, Vector3.zero));

        private void BuildPallet(MeshBuilder mb, Vector3 o)
        {
            var wood = M("Wood");
            // 1.2 x 0.8 footprint, 0.144 high
            for (int i = 0; i < 3; i++)
                mb.Box(o + new Vector3(-0.55f + i * 0.55f, 0.011f, 0), new Vector3(0.1f, 0.022f, 0.8f), wood);
            for (int i = 0; i < 3; i++)
                mb.Box(o + new Vector3(0, 0.022f + 0.039f, -0.35f + i * 0.35f), new Vector3(1.2f, 0.078f, 0.1f), wood);
            for (int i = 0; i < 7; i++)
                mb.Box(o + new Vector3(0, 0.122f + 0.011f, -0.36f + i * 0.12f), new Vector3(1.2f, 0.022f, 0.085f), wood);
        }

        public PropMesh PalletStack(int variant) => Get("PalletStack" + variant, (mb, pm) =>
        {
            var rng = new System.Random(500 + variant);
            int pallets = variant == 2 ? 6 : 1;
            for (int i = 0; i < pallets; i++) BuildPallet(mb, new Vector3(0, i * 0.145f, 0));
            if (variant == 2) return;
            float y = 0.145f;
            int layers = variant == 0 ? 3 : 2;
            var card = M("Cardboard");
            for (int l = 0; l < layers; l++)
            {
                for (int ix = 0; ix < 3; ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    if (l == layers - 1 && rng.NextDouble() < 0.3) continue;
                    var size = new Vector3(0.38f, 0.32f + (float)rng.NextDouble() * 0.04f, 0.38f);
                    mb.Box(new Vector3(-0.4f + ix * 0.4f, y + size.y * 0.5f, -0.2f + iz * 0.4f), size, card);
                }
                y += 0.36f;
            }
        });

        public PropMesh Crate(float size) => Get("Crate" + size.ToString("0.00"), (mb, pm) =>
        {
            var wood = M("Wood");
            var dark = M("PM_DarkGrey");
            float s = size;
            mb.Box(new Vector3(0, s * 0.5f, 0), new Vector3(s - 0.04f, s - 0.04f, s - 0.04f), wood);
            // edge battens
            float b = 0.07f;
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                mb.Box(new Vector3(x * (s * 0.5f - b * 0.5f), s * 0.5f, z * (s * 0.5f - b * 0.5f)), new Vector3(b, s, b), wood);
            foreach (float y in new[] { b * 0.5f, s - b * 0.5f })
            {
                mb.Box(new Vector3(0, y, s * 0.5f - b * 0.5f), new Vector3(s, b, b), wood);
                mb.Box(new Vector3(0, y, -s * 0.5f + b * 0.5f), new Vector3(s, b, b), wood);
                mb.Box(new Vector3(s * 0.5f - b * 0.5f, y, 0), new Vector3(b, b, s), wood);
                mb.Box(new Vector3(-s * 0.5f + b * 0.5f, y, 0), new Vector3(b, b, s), wood);
            }
            // stencil plate
            mb.Box(new Vector3(0, s * 0.55f, s * 0.5f + 0.005f), new Vector3(s * 0.4f, s * 0.15f, 0.01f), dark);
        });

        public PropMesh CardboardBox(int variant) => Get("Box" + variant, (mb, pm) =>
        {
            var rng = new System.Random(900 + variant);
            var card = M("Cardboard");
            int n = 1 + variant % 3;
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                var size = new Vector3(0.4f + (float)rng.NextDouble() * 0.35f, 0.3f + (float)rng.NextDouble() * 0.25f, 0.35f + (float)rng.NextDouble() * 0.3f);
                var rot = Quaternion.Euler(0, (float)rng.NextDouble() * 20f - 10f, 0);
                mb.Box(new Vector3((float)rng.NextDouble() * 0.06f, y + size.y * 0.5f, 0), size, card, rot);
                y += size.y;
            }
        });

        public PropMesh Dumpster(string paintMat) => Get("Dumpster_" + paintMat, (mb, pm) =>
        {
            var p = M(paintMat);
            var dark = M("Plastic_Black");
            var steel = M("Metal_Dark");
            var rubber = M("Rubber");
            // body 2.0 x 1.15 x 1.1 with tapered look via two boxes
            mb.Box(new Vector3(0, 0.2f + 0.45f, 0), new Vector3(1.9f, 0.9f, 1.05f), p);
            mb.Box(new Vector3(0, 1.1f + 0.06f, 0.05f), new Vector3(2.0f, 0.12f, 1.2f), p);
            // ribs
            foreach (float x in new[] { -0.6f, 0f, 0.6f })
            {
                mb.Box(new Vector3(x, 0.65f, 0.545f), new Vector3(0.08f, 0.9f, 0.04f), p);
                mb.Box(new Vector3(x, 0.65f, -0.545f), new Vector3(0.08f, 0.9f, 0.04f), p);
            }
            // lids (one slightly open)
            mb.Box(new Vector3(-0.5f, 1.27f, 0.05f), new Vector3(0.98f, 0.06f, 1.24f), dark);
            mb.Box(new Vector3(0.5f, 1.33f, 0.0f), new Vector3(0.98f, 0.06f, 1.24f), dark, Quaternion.Euler(-9f, 0, 0));
            // side pockets
            mb.Box(new Vector3(1.0f, 0.75f, 0), new Vector3(0.14f, 0.18f, 0.6f), steel);
            mb.Box(new Vector3(-1.0f, 0.75f, 0), new Vector3(0.14f, 0.18f, 0.6f), steel);
            foreach (float x in new[] { -0.75f, 0.75f })
            foreach (float z in new[] { -0.4f, 0.4f })
            {
                mb.Box(new Vector3(x, 0.17f, z), new Vector3(0.12f, 0.08f, 0.12f), steel);
                mb.Cylinder(new Vector3(x - 0.04f, 0.08f, z), new Vector3(x + 0.04f, 0.08f, z), 0.08f, 10, rubber);
            }
            pm.Colliders.Add(B(new Vector3(0, 0.7f, 0), new Vector3(2.1f, 1.4f, 1.25f)));
        });

        public PropMesh WheelieBin(string paintMat) => Get("Bin_" + paintMat, (mb, pm) =>
        {
            var p = M(paintMat);
            var dark = M("Plastic_Black");
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(0.58f, 0.95f, 0.7f), p);
            mb.Box(new Vector3(0, 1.0f, 0.02f), new Vector3(0.62f, 0.05f, 0.76f), p);
            mb.Box(new Vector3(0, 0.95f, -0.38f), new Vector3(0.5f, 0.05f, 0.06f), dark);
            mb.Cylinder(new Vector3(-0.3f, 0.1f, -0.3f), new Vector3(0.3f, 0.1f, -0.3f), 0.1f, 10, M("Rubber"));
        });

        public PropMesh Drum(string paintMat) => Get("Drum_" + paintMat, (mb, pm) =>
        {
            var p = M(paintMat);
            mb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0.88f, 0), 0.29f, 16, p);
            foreach (float y in new[] { 0.3f, 0.58f })
                mb.Cylinder(new Vector3(0, y, 0), new Vector3(0, y + 0.025f, 0), 0.3f, 16, p);
            mb.Cylinder(new Vector3(0.15f, 0.88f, 0), new Vector3(0.15f, 0.9f, 0), 0.03f, 8, M("Metal_Dark"));
        });

        public PropMesh Cone() => Get("Cone", (mb, pm) =>
        {
            var o = M("PM_Orange");
            var w = M("PM_White");
            mb.Box(new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.04f, 0.4f), M("Plastic_Black"));
            mb.Cylinder(new Vector3(0, 0.04f, 0), new Vector3(0, 0.3f, 0), 0.15f, 12, o, true, 0.1f);
            mb.Cylinder(new Vector3(0, 0.3f, 0), new Vector3(0, 0.42f, 0), 0.1f, 12, w, true, 0.075f);
            mb.Cylinder(new Vector3(0, 0.42f, 0), new Vector3(0, 0.7f, 0), 0.075f, 12, o, true, 0.025f);
        });

        public PropMesh Bollard() => Get("Bollard", (mb, pm) =>
        {
            mb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0.75f, 0), 0.1f, 12, M("PM_Yellow"));
            mb.Cylinder(new Vector3(0, 0.75f, 0), new Vector3(0, 0.9f, 0), 0.1f, 12, M("Plastic_Black"), true, 0.06f);
            mb.Cylinder(new Vector3(0, 0.55f, 0), new Vector3(0, 0.63f, 0), 0.103f, 12, M("Plastic_Black"), false);
        });

        public PropMesh TireStack(int count) => Get("Tires" + count, (mb, pm) =>
        {
            var r = M("Rubber");
            for (int i = 0; i < count; i++)
            {
                float y = i * 0.22f;
                float ox = (i % 2) * 0.04f;
                mb.Cylinder(new Vector3(ox, y, 0), new Vector3(ox, y + 0.21f, 0), 0.33f, 14, r);
            }
        });

        public PropMesh Shelf(float length, int variant) => Get($"Shelf{length:0.0}_{variant}", (mb, pm) =>
        {
            var frame = M(variant % 2 == 0 ? "PM_Blue" : "PM_Grey");
            var deck = M("Metal_Galvanized");
            var rng = new System.Random(1300 + variant);
            float d = 0.6f, h = 2.1f;
            foreach (float x in new[] { -length * 0.5f + 0.03f, length * 0.5f - 0.03f })
            foreach (float z in new[] { -d * 0.5f + 0.03f, d * 0.5f - 0.03f })
                mb.Box(new Vector3(x, h * 0.5f, z), new Vector3(0.05f, h, 0.05f), frame);
            float[] levels = { 0.12f, 0.8f, 1.45f, 2.05f };
            foreach (float y in levels)
            {
                mb.Box(new Vector3(0, y, 0), new Vector3(length, 0.03f, d), deck);
                if (y > 2f) continue;
                float x = -length * 0.5f + 0.1f;
                while (x < length * 0.5f - 0.35f)
                {
                    float w = 0.25f + (float)rng.NextDouble() * 0.35f;
                    if (x + w > length * 0.5f - 0.05f) break;
                    if (rng.NextDouble() < 0.75)
                    {
                        float bh = 0.18f + (float)rng.NextDouble() * 0.38f;
                        string mat = rng.NextDouble() < 0.7 ? "Cardboard" : (rng.NextDouble() < 0.5 ? "PM_Red" : "PM_Teal");
                        mb.Box(new Vector3(x + w * 0.5f, y + 0.015f + bh * 0.5f, (float)rng.NextDouble() * 0.1f - 0.05f), new Vector3(w - 0.03f, bh, d - 0.12f), M(mat));
                    }
                    x += w + 0.03f;
                }
            }
            pm.Colliders.Add(B(new Vector3(0, h * 0.5f, 0), new Vector3(length, h, d)));
        });

        /// <summary>Heavy pallet rack 2.7 m wide, 2.6 m high, 1.1 m deep with loaded pallets.</summary>
        public PropMesh PalletRack(int variant) => Get("PalletRack" + variant, (mb, pm) =>
        {
            var up = M("PM_Blue");
            var beam = M("PM_Orange");
            float w = 2.7f, d = 1.1f, h = 2.6f;
            foreach (float x in new[] { -w * 0.5f, w * 0.5f })
            {
                foreach (float z in new[] { -d * 0.5f, d * 0.5f })
                    mb.Box(new Vector3(x, h * 0.5f, z), new Vector3(0.08f, h, 0.08f), up);
                for (int i = 0; i < 4; i++)
                    mb.Box(new Vector3(x, 0.3f + i * 0.65f, 0), new Vector3(0.04f, 0.04f, d), up);
            }
            var rng = new System.Random(1400 + variant);
            foreach (float y in new[] { 0.05f, 1.25f })
            {
                if (y > 0.5f)
                {
                    mb.Box(new Vector3(0, y - 0.06f, d * 0.5f), new Vector3(w, 0.1f, 0.05f), beam);
                    mb.Box(new Vector3(0, y - 0.06f, -d * 0.5f), new Vector3(w, 0.1f, 0.05f), beam);
                }
                foreach (float x in new[] { -0.65f, 0.65f })
                {
                    if (rng.NextDouble() < 0.2) continue;
                    var o = new Vector3(x, y, 0);
                    for (int i = 0; i < 3; i++)
                        mb.Box(o + new Vector3(-0.55f + i * 0.55f, 0.07f, 0), new Vector3(0.1f, 0.14f, 1.0f), M("Wood"));
                    mb.Box(o + new Vector3(0, 0.145f, 0), new Vector3(1.2f, 0.02f, 1.0f), M("Wood"));
                    float bh = 0.5f + (float)rng.NextDouble() * 0.45f;
                    mb.Box(o + new Vector3(0, 0.155f + bh * 0.5f, 0), new Vector3(1.1f, bh, 0.95f), M("Cardboard"));
                }
            }
            pm.Colliders.Add(B(new Vector3(0, h * 0.5f, 0), new Vector3(w + 0.1f, h, d + 0.1f)));
        });

        public PropMesh ElectricalCabinet(int variant) => Get("ECab" + variant, (mb, pm) =>
        {
            var body = M(variant % 2 == 0 ? "PM_Grey" : "PM_DarkGrey");
            var dark = M("Plastic_Black");
            mb.Box(new Vector3(0, 1.0f, 0), new Vector3(0.8f, 2.0f, 0.5f), body);
            mb.Box(new Vector3(0, 0.05f, 0.0f), new Vector3(0.82f, 0.1f, 0.52f), dark);
            mb.Box(new Vector3(0, 1.0f, 0.252f), new Vector3(0.01f, 1.8f, 0.01f), dark);
            mb.Box(new Vector3(0.06f, 1.1f, 0.265f), new Vector3(0.03f, 0.18f, 0.03f), M("Metal_Steel"));
            mb.Box(new Vector3(-0.2f, 1.6f, 0.252f), new Vector3(0.18f, 0.16f, 0.01f), M("Sign_Yellow"));
            mb.Text("!", new Vector3(-0.2f, 1.6f, 0.258f), Vector3.forward, Vector3.up, 0.12f, M("Text_Black"));
            // vents
            for (int i = 0; i < 4; i++)
                mb.Box(new Vector3(0.2f, 0.3f + i * 0.06f, 0.252f), new Vector3(0.25f, 0.02f, 0.01f), dark);
        });

        public PropMesh Transformer() => Get("Transformer", (mb, pm) =>
        {
            var g = M("PM_Grey");
            mb.Box(new Vector3(0, 0.9f, 0), new Vector3(1.4f, 1.8f, 1.0f), g);
            for (int i = 0; i < 7; i++)
            {
                float z = -0.42f + i * 0.14f;
                mb.Box(new Vector3(0.76f, 0.85f, z), new Vector3(0.12f, 1.3f, 0.03f), g);
                mb.Box(new Vector3(-0.76f, 0.85f, z), new Vector3(0.12f, 1.3f, 0.03f), g);
            }
            for (int i = 0; i < 3; i++)
                mb.Cylinder(new Vector3(-0.4f + i * 0.4f, 1.8f, 0), new Vector3(-0.4f + i * 0.4f, 2.1f, 0), 0.06f, 10, M("Plastic_Black"));
            mb.Box(new Vector3(0, 1.3f, 0.505f), new Vector3(0.4f, 0.3f, 0.01f), M("Sign_Yellow"));
            mb.Text("HV", new Vector3(0, 1.3f, 0.512f), Vector3.forward, Vector3.up, 0.18f, M("Text_Black"));
            pm.Colliders.Add(B(new Vector3(0, 1.0f, 0), new Vector3(1.7f, 2.0f, 1.05f)));
        });

        public PropMesh Tank() => Get("Tank", (mb, pm) =>
        {
            var w = M("PM_White");
            var steel = M("Metal_Dark");
            foreach (float x in new[] { -0.45f, 0.45f })
            foreach (float z in new[] { -0.45f, 0.45f })
                mb.Box(new Vector3(x, 0.15f, z), new Vector3(0.08f, 0.3f, 0.08f), steel);
            mb.Cylinder(new Vector3(0, 0.3f, 0), new Vector3(0, 2.3f, 0), 0.7f, 20, w);
            mb.Cylinder(new Vector3(0, 2.3f, 0), new Vector3(0, 2.45f, 0), 0.7f, 20, w, true, 0.45f);
            mb.Cylinder(new Vector3(0.6f, 1.2f, 0), new Vector3(1.1f, 1.2f, 0), 0.08f, 10, steel);
            mb.Cylinder(new Vector3(0, 2.45f, 0), new Vector3(0, 2.75f, 0), 0.08f, 10, steel);
            pm.Colliders.Add(B(new Vector3(0, 1.25f, 0), new Vector3(1.45f, 2.5f, 1.45f)));
        });

        public PropMesh Pump() => Get("Pump", (mb, pm) =>
        {
            var b = M("PM_Blue");
            mb.Box(new Vector3(0, 0.1f, 0), new Vector3(1.2f, 0.2f, 0.6f), M("Metal_Dark"));
            mb.Cylinder(new Vector3(-0.45f, 0.45f, 0), new Vector3(0.15f, 0.45f, 0), 0.22f, 14, b);
            mb.Cylinder(new Vector3(0.15f, 0.45f, 0), new Vector3(0.45f, 0.45f, 0), 0.25f, 14, M("PM_Red"));
            mb.Cylinder(new Vector3(0.3f, 0.7f, 0), new Vector3(0.3f, 1.3f, 0), 0.07f, 10, M("Metal_Dark"));
        });

        public PropMesh Door(string mat, float width = 1.0f, float height = 2.05f) => Get($"Door_{mat}_{width:0.00}", (mb, pm) =>
        {
            var p = M(mat);
            // pivot at hinge (x = 0), leaf extends to +x
            mb.Box(new Vector3(width * 0.5f, height * 0.5f, 0), new Vector3(width, height, 0.05f), p);
            mb.Box(new Vector3(width - 0.1f, 1.0f, 0.04f), new Vector3(0.12f, 0.03f, 0.04f), M("Metal_Steel"));
            mb.Box(new Vector3(width - 0.1f, 1.0f, -0.04f), new Vector3(0.12f, 0.03f, 0.04f), M("Metal_Steel"));
            mb.Box(new Vector3(width * 0.5f, 1.55f, 0.028f), new Vector3(0.3f, 0.4f, 0.01f), M("Glass_Dark"));
        });

        public PropMesh ExtinguisherBox() => Get("ExtinguisherBox", (mb, pm) =>
        {
            var red = M("PM_Red");
            // back against -Z (wall), facing +Z
            mb.Box(new Vector3(0, 0.35f, 0.1f), new Vector3(0.38f, 0.7f, 0.2f), red);
            mb.Box(new Vector3(0, 0.38f, 0.205f), new Vector3(0.28f, 0.48f, 0.01f), M("Glass_Dark"));
            mb.Cylinder(new Vector3(0, 0.12f, 0.1f), new Vector3(0, 0.55f, 0.1f), 0.08f, 10, red);
            mb.Box(new Vector3(0, 0.08f, 0.206f), new Vector3(0.34f, 0.1f, 0.01f), M("Sign_White"));
            mb.Text("FIRE", new Vector3(0, 0.08f, 0.212f), Vector3.forward, Vector3.up, 0.075f, M("Text_Black"));
        });

        public PropMesh PayMachine() => Get("PayMachine", (mb, pm) =>
        {
            var body = M("PM_Teal");
            mb.Box(new Vector3(0, 0.8f, 0), new Vector3(0.65f, 1.6f, 0.45f), body);
            mb.Box(new Vector3(0, 1.62f, 0.02f), new Vector3(0.7f, 0.06f, 0.5f), M("PM_DarkGrey"));
            mb.Box(new Vector3(0, 1.25f, 0.226f), new Vector3(0.4f, 0.28f, 0.01f), M("Glass_Dark"));
            mb.Box(new Vector3(0, 0.9f, 0.23f), new Vector3(0.3f, 0.12f, 0.02f), M("Plastic_Black"));
            mb.Box(new Vector3(0.15f, 0.7f, 0.23f), new Vector3(0.1f, 0.03f, 0.02f), M("Metal_Steel"));
            mb.Text("PAY", new Vector3(0, 1.48f, 0.227f), Vector3.forward, Vector3.up, 0.09f, M("Text_White"));
        });

        public PropMesh Bench() => Get("Bench", (mb, pm) =>
        {
            var s = M("Metal_Steel");
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.05f, 0.42f), M("Wood"));
            mb.Box(new Vector3(0, 0.75f, -0.2f), new Vector3(1.6f, 0.3f, 0.04f), M("Wood"));
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                mb.Box(new Vector3(x, 0.22f, 0), new Vector3(0.05f, 0.44f, 0.4f), s);
                mb.Box(new Vector3(x, 0.6f, -0.2f), new Vector3(0.05f, 0.5f, 0.04f), s);
            }
        });

        public PropMesh WaterHeater() => Get("WaterHeater", (mb, pm) =>
        {
            mb.Cylinder(new Vector3(0, 0.05f, 0), new Vector3(0, 1.7f, 0), 0.32f, 16, M("PM_White"));
            mb.Cylinder(new Vector3(0, 1.7f, 0), new Vector3(0, 2.6f, 0), 0.04f, 8, M("Metal_Dark"));
            mb.Cylinder(new Vector3(0.12f, 1.7f, 0), new Vector3(0.12f, 2.6f, 0), 0.03f, 8, M("PM_Red"));
            mb.Box(new Vector3(0, 0.6f, 0.32f), new Vector3(0.18f, 0.25f, 0.04f), M("PM_Grey"));
        });

        // ================================================================== zone props

        /// <summary>Blocky motorbike, length along Z.</summary>
        public PropMesh Motorbike(string paint) => Get("Motorbike_" + paint, (mb, pm) =>
        {
            var p = M(paint);
            var rubber = M("Rubber");
            var chrome = M("Car_Chrome");
            var dark = M("Car_Trim");
            foreach (float z in new[] { -0.72f, 0.72f })
            {
                mb.Cylinder(new Vector3(-0.06f, 0.32f, z), new Vector3(0.06f, 0.32f, z), 0.32f, 14, rubber);
                mb.Cylinder(new Vector3(-0.065f, 0.32f, z), new Vector3(0.065f, 0.32f, z), 0.15f, 10, chrome);
            }
            mb.Box(new Vector3(0, 0.55f, 0), new Vector3(0.18f, 0.12f, 1.25f), dark, Quaternion.Euler(-8f, 0, 0));
            mb.Box(new Vector3(0, 0.5f, -0.05f), new Vector3(0.3f, 0.3f, 0.45f), M("Metal_Dark"));
            mb.Box(new Vector3(0, 0.82f, 0.25f), new Vector3(0.34f, 0.22f, 0.45f), p);
            mb.Box(new Vector3(0, 0.82f, -0.35f), new Vector3(0.28f, 0.1f, 0.6f), M("Plastic_Black"));
            mb.Box(new Vector3(0, 0.75f, -0.75f), new Vector3(0.22f, 0.12f, 0.35f), p);
            mb.Box(new Vector3(0, 0.75f, 0.62f), new Vector3(0.06f, 0.6f, 0.06f), chrome, Quaternion.Euler(20f, 0, 0));
            mb.Box(new Vector3(0, 1.05f, 0.55f), new Vector3(0.7f, 0.035f, 0.035f), chrome);
            mb.Box(new Vector3(0, 0.95f, 0.72f), new Vector3(0.16f, 0.14f, 0.08f), M("Car_Headlight"));
            mb.Cylinder(new Vector3(0.17f, 0.35f, 0.1f), new Vector3(0.17f, 0.42f, -0.85f), 0.05f, 8, chrome);
            pm.Colliders.Add(B(new Vector3(0, 0.6f, 0), new Vector3(0.5f, 1.2f, 2.1f)));
        });

        public PropMesh Bicycle(string paint) => Get("Bicycle_" + paint, (mb, pm) =>
        {
            var p = M(paint);
            foreach (float z in new[] { -0.52f, 0.52f })
                mb.Cylinder(new Vector3(-0.02f, 0.34f, z), new Vector3(0.02f, 0.34f, z), 0.34f, 16, M("Rubber"));
            mb.Box(new Vector3(0, 0.58f, 0.05f), new Vector3(0.04f, 0.04f, 0.65f), p);
            mb.Box(new Vector3(0, 0.46f, -0.22f), new Vector3(0.04f, 0.04f, 0.6f), p, Quaternion.Euler(-35f, 0, 0));
            mb.Box(new Vector3(0, 0.46f, 0.3f), new Vector3(0.04f, 0.04f, 0.55f), p, Quaternion.Euler(40f, 0, 0));
            mb.Box(new Vector3(0, 0.62f, -0.28f), new Vector3(0.04f, 0.6f, 0.04f), p, Quaternion.Euler(-15f, 0, 0));
            mb.Box(new Vector3(0, 0.92f, -0.33f), new Vector3(0.12f, 0.05f, 0.24f), M("Plastic_Black"));
            mb.Box(new Vector3(0, 0.95f, 0.42f), new Vector3(0.5f, 0.03f, 0.03f), M("Car_Chrome"));
            pm.Colliders.Add(B(new Vector3(0, 0.5f, 0), new Vector3(0.4f, 1.0f, 1.7f)));
        });

        public PropMesh BikeRack(int slots) => Get("BikeRack" + slots, (mb, pm) =>
        {
            var m = M("Metal_Galvanized");
            for (int i = 0; i < slots; i++)
            {
                float x = (i - (slots - 1) * 0.5f) * 0.75f;
                mb.Box(new Vector3(x, 0.4f, -0.3f), new Vector3(0.05f, 0.8f, 0.05f), m);
                mb.Box(new Vector3(x, 0.4f, 0.3f), new Vector3(0.05f, 0.8f, 0.05f), m);
                mb.Box(new Vector3(x, 0.82f, 0f), new Vector3(0.05f, 0.05f, 0.65f), m);
            }
            pm.Colliders.Add(B(new Vector3(0, 0.45f, 0), new Vector3(slots * 0.75f, 0.9f, 0.7f)));
        });

        public PropMesh SecurityBooth() => Get("SecurityBooth", (mb, pm) =>
        {
            var w = M("PM_White");
            var g = M("Glass_Dark");
            float sx = 2.2f, sz = 2.0f, h = 2.5f;
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                mb.Box(new Vector3(x * (sx * 0.5f - 0.05f), h * 0.5f, z * (sz * 0.5f - 0.05f)), new Vector3(0.1f, h, 0.1f), w);
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(sx, 1.0f, sz), w);
            mb.Box(new Vector3(0, 1.65f, 0), new Vector3(sx - 0.04f, 1.3f, sz - 0.04f), g);
            mb.Box(new Vector3(0, h + 0.08f, 0), new Vector3(sx + 0.3f, 0.16f, sz + 0.3f), M("PM_Blue"));
            mb.Box(new Vector3(0, h - 0.05f, sz * 0.5f + 0.02f), new Vector3(sx - 0.2f, 0.18f, 0.02f), M("Sign_Blue"));
            mb.Text("SECURITY", new Vector3(0, h - 0.05f, sz * 0.5f + 0.035f), Vector3.forward, Vector3.up, 0.13f, M("Text_White"));
            mb.Box(new Vector3(0, 1.02f, sz * 0.5f + 0.15f), new Vector3(sx - 0.2f, 0.04f, 0.3f), M("PM_Grey"));
            pm.Colliders.Add(B(new Vector3(0, h * 0.5f, 0), new Vector3(sx, h, sz)));
        });

        /// <summary>Barrier gate: base at origin, arm extends along +X (5 m). Only the base collides.</summary>
        public PropMesh BarrierGate() => Get("BarrierGate", (mb, pm) =>
        {
            mb.Box(new Vector3(0, 0.55f, 0), new Vector3(0.4f, 1.1f, 0.4f), M("PM_Yellow"));
            mb.Box(new Vector3(0, 1.12f, 0), new Vector3(0.45f, 0.05f, 0.45f), M("Plastic_Black"));
            mb.Box(new Vector3(2.6f, 0.95f, 0), new Vector3(4.9f, 0.1f, 0.06f), M("Hazard"));
            mb.Box(new Vector3(5.0f, 0.45f, 0), new Vector3(0.08f, 0.9f, 0.08f), M("PM_Yellow"));
            pm.Colliders.Add(B(new Vector3(0, 0.55f, 0), new Vector3(0.4f, 1.1f, 0.4f)));
            pm.Colliders.Add(B(new Vector3(5.0f, 0.45f, 0), new Vector3(0.1f, 0.9f, 0.1f)));
        });

        /// <summary>Car wash gantry spanning X (width 6 m), brushes hang into the bay.</summary>
        public PropMesh CarWashGantry() => Get("CarWashGantry", (mb, pm) =>
        {
            var steel = M("PM_Grey");
            var brush = M("Brush_Blue");
            foreach (float x in new[] { -3f, 3f })
            {
                mb.Box(new Vector3(x, 1.4f, 0), new Vector3(0.3f, 2.8f, 0.5f), steel);
                mb.Box(new Vector3(x, 0.05f, 0), new Vector3(0.5f, 0.1f, 1.2f), M("Metal_Dark"));
            }
            mb.Box(new Vector3(0, 2.7f, 0), new Vector3(6.3f, 0.3f, 0.5f), steel);
            mb.Cylinder(new Vector3(-2.2f, 2.5f, 0.4f), new Vector3(-2.2f, 0.25f, 0.4f), 0.38f, 14, brush);
            mb.Cylinder(new Vector3(2.2f, 2.5f, 0.4f), new Vector3(2.2f, 0.25f, 0.4f), 0.38f, 14, brush);
            mb.Cylinder(new Vector3(-2.5f, 2.2f, -0.3f), new Vector3(2.5f, 2.2f, -0.3f), 0.3f, 14, brush);
            mb.Box(new Vector3(0, 2.7f, 0.26f), new Vector3(1.8f, 0.22f, 0.02f), M("Sign_Blue"));
            mb.Text("CAR WASH", new Vector3(0, 2.7f, 0.275f), Vector3.forward, Vector3.up, 0.15f, M("Text_White"));
            pm.Colliders.Add(B(new Vector3(-3f, 1.4f, 0), new Vector3(0.4f, 2.8f, 0.6f)));
            pm.Colliders.Add(B(new Vector3(3f, 1.4f, 0), new Vector3(0.4f, 2.8f, 0.6f)));
        });

        /// <summary>Scaffold tower: bays along X (each 2.5 m), 2 lifts, 1.2 m deep.</summary>
        public PropMesh Scaffold(int bays) => Get("Scaffold" + bays, (mb, pm) =>
        {
            var tube = M("Metal_Galvanized");
            var plank = M("Wood");
            float len = bays * 2.5f, d = 1.2f;
            for (int i = 0; i <= bays; i++)
            {
                float x = -len * 0.5f + i * 2.5f;
                foreach (float z in new[] { -d * 0.5f, d * 0.5f })
                {
                    mb.Cylinder(new Vector3(x, 0, z), new Vector3(x, 2.75f, z), 0.025f, 6, tube);
                    mb.Box(new Vector3(x, 0.01f, z), new Vector3(0.15f, 0.02f, 0.15f), M("Metal_Dark"));
                }
                foreach (float y in new[] { 0.15f, 1.3f, 2.5f })
                    mb.Cylinder(new Vector3(x, y, -d * 0.5f), new Vector3(x, y, d * 0.5f), 0.022f, 6, tube);
            }
            foreach (float y in new[] { 0.15f, 1.3f, 2.5f })
            foreach (float z in new[] { -d * 0.5f, d * 0.5f })
                mb.Cylinder(new Vector3(-len * 0.5f, y, z), new Vector3(len * 0.5f, y, z), 0.022f, 6, tube);
            for (int i = 0; i < bays; i++)
            {
                float x = -len * 0.5f + i * 2.5f;
                mb.Cylinder(new Vector3(x, 0.15f, -d * 0.5f), new Vector3(x + 2.5f, 2.5f, -d * 0.5f), 0.02f, 6, tube);
                foreach (float y in new[] { 1.33f, 2.53f })
                    for (int k = 0; k < 4; k++)
                        mb.Box(new Vector3(x + 1.25f, y + 0.025f, -d * 0.5f + 0.15f + k * 0.3f), new Vector3(2.45f, 0.04f, 0.27f), plank);
            }
            pm.Colliders.Add(B(new Vector3(0, 1.4f, 0), new Vector3(len, 2.8f, d)));
        });

        /// <summary>Car shape covered by a tarp (Sedan/Suv/Van proportions).</summary>
        public PropMesh TarpCar(CarType type, string tarp) => Get($"TarpCar_{type}_{tarp}", (mb, pm) =>
        {
            var t = M(tarp);
            float L = type == CarType.Van ? 5.1f : type == CarType.Suv ? 4.7f : 4.5f;
            float W = type == CarType.Van ? 2.05f : 1.9f;
            float bodyTop = type == CarType.Suv ? 1.15f : 0.9f;
            float cabH = type == CarType.Van ? 1.1f : 0.55f;
            float cabL = type == CarType.Van ? L - 0.9f : 2.5f;
            foreach (float z in new[] { -1.4f, 1.4f })
            foreach (float x in new[] { -0.8f, 0.8f })
                mb.Cylinder(new Vector3(x - 0.12f, 0.32f, z), new Vector3(x + 0.12f, 0.32f, z), 0.32f, 12, M("Rubber"));
            mb.Box(new Vector3(0, (0.22f + bodyTop) * 0.5f, 0), new Vector3(W, bodyTop - 0.22f, L), t);
            mb.Box(new Vector3(0, bodyTop + cabH * 0.5f, type == CarType.Van ? -0.45f : -0.2f), new Vector3(W - 0.2f, cabH, cabL), t);
            // sagging folds
            mb.Box(new Vector3(0, bodyTop + 0.02f, 0.9f), new Vector3(W + 0.02f, 0.06f, 0.5f), t, Quaternion.Euler(-12f, 0, 0));
            mb.Box(new Vector3(W * 0.5f + 0.01f, 0.35f, 0), new Vector3(0.04f, 0.3f, L - 0.4f), t, Quaternion.Euler(0, 0, 8f));
            mb.Box(new Vector3(-W * 0.5f - 0.01f, 0.35f, 0), new Vector3(0.04f, 0.3f, L - 0.4f), t, Quaternion.Euler(0, 0, -8f));
            foreach (float z in new[] { -1.2f, 1.2f })
                mb.Box(new Vector3(0, bodyTop * 0.5f + 0.1f, z), new Vector3(W + 0.05f, 0.02f, 0.03f), M("PM_Yellow"));
            pm.Colliders.Add(B(new Vector3(0, (bodyTop + cabH) * 0.5f + 0.1f, 0), new Vector3(W, bodyTop + cabH - 0.1f, L)));
        });

        /// <summary>Temporary construction fence panel 3.5 m along X with concrete feet.</summary>
        public PropMesh TempFence() => Get("TempFence", (mb, pm) =>
        {
            mb.Box(new Vector3(0, 1.1f, 0), new Vector3(3.4f, 1.8f, 0.02f), M("Wire_Mesh"));
            foreach (float x in new[] { -1.72f, 1.72f })
            {
                mb.Cylinder(new Vector3(x, 0.12f, 0), new Vector3(x, 2.05f, 0), 0.022f, 6, M("Metal_Galvanized"));
                mb.Box(new Vector3(x, 0.07f, 0), new Vector3(0.25f, 0.14f, 0.6f), M("Rubble"));
            }
            mb.Cylinder(new Vector3(-1.72f, 2.02f, 0), new Vector3(1.72f, 2.02f, 0), 0.02f, 6, M("Metal_Galvanized"));
            mb.Cylinder(new Vector3(-1.72f, 0.2f, 0), new Vector3(1.72f, 0.2f, 0), 0.02f, 6, M("Metal_Galvanized"));
            mb.Box(new Vector3(0, 1.3f, 0.02f), new Vector3(0.9f, 0.5f, 0.01f), M("Sign_Yellow"));
            mb.Text("DANGER", new Vector3(0, 1.38f, 0.027f), Vector3.forward, Vector3.up, 0.13f, M("Text_Black"));
            mb.Text("KEEP OUT", new Vector3(0, 1.2f, 0.027f), Vector3.forward, Vector3.up, 0.09f, M("Text_Black"));
            pm.Colliders.Add(B(new Vector3(0, 1.05f, 0), new Vector3(3.5f, 2.1f, 0.12f)));
        });

        public PropMesh Rubble(int variant) => Get("Rubble" + variant, (mb, pm) =>
        {
            var rng = new System.Random(3100 + variant);
            int n = 10 + variant * 3;
            for (int i = 0; i < n; i++)
            {
                float r = (float)rng.NextDouble();
                var s = new Vector3(0.2f + r * 0.6f, 0.12f + (float)rng.NextDouble() * 0.35f, 0.2f + (float)rng.NextDouble() * 0.6f);
                float rad = 1.2f * (1f - r * 0.5f);
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * rad * 2f, s.y * 0.4f + (1f - r) * 0.25f, ((float)rng.NextDouble() - 0.5f) * rad * 1.6f);
                mb.Box(p, s, M("Rubble"), Quaternion.Euler((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f - 20f));
            }
            for (int i = 0; i < 4; i++)
            {
                var a = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.5f, 0.1f, ((float)rng.NextDouble() - 0.5f) * 1.2f);
                mb.Cylinder(a, a + new Vector3(((float)rng.NextDouble() - 0.5f) * 1.2f, 0.4f + (float)rng.NextDouble() * 0.6f, ((float)rng.NextDouble() - 0.5f) * 1.2f), 0.012f, 4, M("Rust"));
            }
            pm.Colliders.Add(B(new Vector3(0, 0.3f, 0), new Vector3(2.0f, 0.6f, 1.6f)));
        });

        public PropMesh SackStack() => Get("SackStack", (mb, pm) =>
        {
            BuildPallet(mb, Vector3.zero);
            for (int l = 0; l < 4; l++)
            for (int i = 0; i < 2; i++)
            {
                bool rot = l % 2 == 1;
                var size = rot ? new Vector3(0.38f, 0.14f, 0.75f) : new Vector3(1.15f, 0.14f, 0.38f);
                var c = rot ? new Vector3(-0.2f + i * 0.4f, 0.145f + 0.07f + l * 0.14f, 0) : new Vector3(0, 0.145f + 0.07f + l * 0.14f, -0.2f + i * 0.4f);
                mb.Box(c, size, M("Sack"));
            }
        });

        public PropMesh EvCharger() => Get("EvCharger", (mb, pm) =>
        {
            mb.Box(new Vector3(0, 0.75f, 0), new Vector3(0.4f, 1.5f, 0.3f), M("PM_White"));
            mb.Box(new Vector3(0, 1.2f, 0.152f), new Vector3(0.26f, 0.18f, 0.01f), M("Glass_Dark"));
            mb.Box(new Vector3(0, 0.75f, 0.152f), new Vector3(0.36f, 0.06f, 0.01f), M("Paint_Green"));
            mb.Cylinder(new Vector3(0.18f, 0.95f, 0.1f), new Vector3(0.25f, 0.3f, 0.35f), 0.025f, 6, M("Plastic_Black"));
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.6f, 0.1f, 0.5f), M("Rubble"));
            mb.Text("EV", new Vector3(0, 1.42f, 0.152f), Vector3.forward, Vector3.up, 0.09f, M("Text_Black"));
        });

        public PropMesh Workbench() => Get("Workbench", (mb, pm) =>
        {
            mb.Box(new Vector3(0, 0.88f, 0), new Vector3(2.0f, 0.06f, 0.7f), M("Wood"));
            foreach (float x in new[] { -0.95f, 0.95f })
            foreach (float z in new[] { -0.3f, 0.3f })
                mb.Box(new Vector3(x, 0.43f, z), new Vector3(0.05f, 0.86f, 0.05f), M("PM_DarkGrey"));
            mb.Box(new Vector3(0, 0.25f, 0), new Vector3(1.9f, 0.03f, 0.6f), M("Metal_Galvanized"));
            mb.Box(new Vector3(0.7f, 0.97f, 0.2f), new Vector3(0.2f, 0.12f, 0.15f), M("PM_Blue"));
            mb.Box(new Vector3(-0.4f, 0.95f, -0.1f), new Vector3(0.5f, 0.08f, 0.25f), M("PM_Red"));
            mb.Box(new Vector3(0, 1.5f, -0.33f), new Vector3(1.9f, 0.9f, 0.03f), M("Wood"));
        });

        public PropMesh Generator() => Get("Generator", (mb, pm) =>
        {
            mb.Box(new Vector3(0, 0.1f, 0), new Vector3(3.2f, 0.2f, 1.3f), M("Metal_Dark"));
            mb.Box(new Vector3(-0.4f, 0.9f, 0), new Vector3(2.2f, 1.4f, 1.1f), M("PM_Green"));
            mb.Box(new Vector3(1.15f, 0.8f, 0), new Vector3(0.7f, 1.2f, 1.0f), M("PM_DarkGrey"));
            for (int i = 0; i < 6; i++)
                mb.Box(new Vector3(-1.4f + i * 0.4f, 1.0f, 0.56f), new Vector3(0.25f, 0.6f, 0.02f), M("Grate"));
            mb.Cylinder(new Vector3(-1.2f, 1.6f, 0), new Vector3(-1.2f, 2.9f, 0), 0.1f, 10, M("Rust"));
            mb.Box(new Vector3(1.15f, 1.1f, 0.505f), new Vector3(0.4f, 0.3f, 0.01f), M("Sign_Yellow"));
            mb.Text("GEN", new Vector3(1.15f, 1.1f, 0.512f), Vector3.forward, Vector3.up, 0.14f, M("Text_Black"));
            pm.Colliders.Add(B(new Vector3(0, 0.85f, 0), new Vector3(3.2f, 1.7f, 1.3f)));
        });

        public PropMesh TrashBags(int variant) => Get("TrashBags" + variant, (mb, pm) =>
        {
            var rng = new System.Random(2100 + variant);
            int count = 2 + variant % 3;
            for (int i = 0; i < count; i++)
            {
                float s = 0.45f + (float)rng.NextDouble() * 0.2f;
                var p = new Vector3((float)rng.NextDouble() * 0.8f - 0.4f, s * 0.4f, (float)rng.NextDouble() * 0.6f - 0.3f);
                var rot = Quaternion.Euler((float)rng.NextDouble() * 20f - 10f, (float)rng.NextDouble() * 90f, (float)rng.NextDouble() * 20f - 10f);
                mb.Box(p, new Vector3(s, s * 0.8f, s * 0.9f), M("Plastic_Black"), rot);
                mb.Box(p + Vector3.up * s * 0.45f, new Vector3(0.12f, 0.12f, 0.12f), M("Plastic_Black"), rot);
            }
        });

        public PropMesh MopBucket() => Get("MopBucket", (mb, pm) =>
        {
            mb.Box(new Vector3(0, 0.2f, 0), new Vector3(0.45f, 0.32f, 0.35f), M("PM_Yellow"));
            mb.Cylinder(new Vector3(0.1f, 0.3f, 0), new Vector3(0.05f, 1.4f, 0.1f), 0.015f, 6, M("Wood"));
        });

        public PropMesh StackedChairs() => Get("Barrier", (mb, pm) =>
        {
            // folding road barrier: two A-frame legs with striped board
            var h = M("Hazard");
            mb.Box(new Vector3(0, 0.85f, 0), new Vector3(1.5f, 0.2f, 0.04f), h);
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.2f, 0.04f), h);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                mb.Box(new Vector3(x, 0.5f, 0.12f), new Vector3(0.05f, 1.0f, 0.05f), M("PM_DarkGrey"), Quaternion.Euler(14f, 0, 0));
                mb.Box(new Vector3(x, 0.5f, -0.12f), new Vector3(0.05f, 1.0f, 0.05f), M("PM_DarkGrey"), Quaternion.Euler(-14f, 0, 0));
            }
        });

        public PropMesh LightFixture(bool flicker) => Get("Fixture" + (flicker ? "Flicker" : "Dead"), (mb, pm) =>
        {
            // long axis along X, hangs down from y = 0
            mb.Box(new Vector3(0, -0.05f, 0), new Vector3(1.3f, 0.1f, 0.22f), M("PM_Grey"));
            mb.Box(new Vector3(0, -0.105f, 0), new Vector3(1.22f, 0.012f, 0.16f), M(flicker ? "Lamp_Flicker" : "Fixture_Dead"));
        });
    }
}
