using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// A real parked car under one of four covers (see <see cref="TarpDrape"/>):
    /// fitted grey car cover (mirror pockets, elastic hem), blue tarp with eyelets, two ropes and bricks on the hem,
    /// green matt sheet (opaque fabric), old canvas pulled back off the bonnet with an old tyre
    /// on the roof. Built once per (model, kind, seed) and kept in the mesh store.
    /// </summary>
    internal static class TarpCars
    {
        public sealed class Built
        {
            public Mesh Cloth, Extras;
            public Material[] ClothMats, ExtrasMats;
        }

        private static readonly Dictionary<string, Built> Cache = new Dictionary<string, Built>();

        public static void ClearCache() => Cache.Clear();

        public static Built Build(MaterialLibrary lib, ParkedCar model, Mesh carMesh, TarpKind kind, int seed, List<Mesh> store)
        {
            string key = $"{model.Name}_{kind}_{seed}";
            if (Cache.TryGetValue(key, out var hit) && hit.Cloth != null) return hit;
            var probeGo = new GameObject("TarpProbe") { hideFlags = HideFlags.DontSave };
            probeGo.transform.position = TarpDrape.ProbeOffset;
            var probe = probeGo.AddComponent<MeshCollider>();
            probe.sharedMesh = carMesh;
            GameObject clothGo = null;
            try
            {
                Physics.SyncTransforms();
                var d = new TarpDrape(probe, kind, seed);
                var rnd = new System.Random(seed);
                var b = carMesh.bounds;
                if (kind == TarpKind.Canvas)
                {
                    d.CutZ = b.center.z + b.size.z * (0.02f + 0.08f * (float)rnd.NextDouble());
                    d.CutSlope = (rnd.NextDouble() < 0.5 ? -1f : 1f) * (0.2f + 0.25f * (float)rnd.NextDouble());
                }
                if (kind == TarpKind.BlueTarp)
                {
                    d.RopeZ.Add(b.center.z + b.size.z * 0.24f);
                    d.RopeZ.Add(b.center.z - b.size.z * 0.27f);
                }
                d.Drape();
                var extras = new MeshBuilder(lib);
                extras.Reserve(lib["Rubber"]);
                if (kind == TarpKind.Canvas || (kind == TarpKind.BlueTarp && rnd.NextDouble() < 0.5))
                    d.RoofTyre(extras, lib["Rubber"], lib["Car_Well"]);
                d.PinchRopes();

                string mainMat = kind == TarpKind.Cover ? "Cover_Grey" : kind == TarpKind.BlueTarp ? "Tarp_Blue" : kind == TarpKind.Foil ? "Foil_Green" : "Canvas_Old";
                string hemMat = kind == TarpKind.Cover ? "Cover_Hem" : kind == TarpKind.BlueTarp ? "Tarp_BlueHem" : mainMat;
                var built = new Built
                {
                    Cloth = d.ToMesh("Tarp_" + key, lib.Tile(lib[mainMat])),
                    ClothMats = new[] { lib[mainMat], lib[hemMat] }
                };
                store.Add(built.Cloth);

                if (kind == TarpKind.BlueTarp)
                {
                    clothGo = new GameObject("TarpClothProbe") { hideFlags = HideFlags.DontSave };
                    clothGo.transform.position = TarpDrape.ProbeOffset;
                    clothGo.AddComponent<MeshCollider>().sharedMesh = built.Cloth;
                    Physics.SyncTransforms();
                    var cloth = clothGo.GetComponent<MeshCollider>();
                    string rope = rnd.NextDouble() < 0.5 ? "Rope_Yellow" : "Rope_White";
                    foreach (float z in d.RopeZ) d.Rope(extras, cloth, z, lib[rope]);
                    d.Grommets(extras, lib["Metal_Galvanized"]);
                    d.Bricks(extras, lib["Brick"], 2 + rnd.Next(2));
                }
                else if (kind == TarpKind.Canvas)
                {
                    d.Roll(extras, lib["Canvas_Dark"]);
                    d.Bricks(extras, lib["Brick"], 1 + rnd.Next(2));
                }
                if (!extras.IsEmpty)
                {
                    built.Extras = extras.ToMesh("TarpExtras_" + key);
                    built.ExtrasMats = extras.Materials;
                    store.Add(built.Extras);
                }
                Cache[key] = built;
                return built;
            }
            finally
            {
                Object.DestroyImmediate(probeGo);
                if (clothGo != null) Object.DestroyImmediate(clothGo);
            }
        }

        /// <summary>Adds the cloth and its extras as children of the car object.</summary>
        public static void Attach(GameObject car, Built b, StaticEditorFlags flags)
        {
            Child(car.transform, "Cover", b.Cloth, b.ClothMats, flags);
            if (b.Extras != null) Child(car.transform, "CoverExtras", b.Extras, b.ExtrasMats, flags);
        }

        private static void Child(Transform parent, string name, Mesh mesh, Material[] mats, StaticEditorFlags flags)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }
    }
}
