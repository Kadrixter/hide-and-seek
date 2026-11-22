using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// The ordinary parked cars: a weighted pick of the lofted models (city car, compact, saloon, estate, SUV, van, each
    /// in two variants) and a realistic paint mix (mostly grey, silver, black and white, some colours). Not statically
    /// batched: ~180 cars x ~13k vertices would add ~100 MB of combined meshes to the build; they share 12 meshes.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private List<ParkedCar> _parkedModels;

        private static readonly (string paint, float weight)[] CarPaintMix =
        {
            ("CarPaint_Silver", 14f), ("CarPaint_Grey", 12f), ("CarPaint_Black", 12f), ("CarPaint_White", 11f), ("CarPaint_PearlWhite", 5f),
            ("CarPaint_Gunmetal", 7f), ("CarPaint_Navy", 5f), ("CarPaint_Blue", 5f), ("CarPaint_Red", 5f), ("CarPaint_Maroon", 3f),
            ("CarPaint_Burgundy", 3f), ("CarPaint_Green", 2.5f), ("CarPaint_Olive", 2f), ("CarPaint_Beige", 3f), ("CarPaint_Brown", 2.5f),
            ("CarPaint_SkyBlue", 2f), ("CarPaint_Teal", 2f), ("CarPaint_Yellow", 1.2f), ("CarPaint_Orange", 1.2f),
        };

        private static readonly (string paint, float weight)[] VanPaintMix =
        {
            ("CarPaint_White", 10f), ("CarPaint_Silver", 3f), ("CarPaint_Grey", 3f), ("CarPaint_Navy", 2f), ("CarPaint_Black", 1.5f),
            ("CarPaint_Red", 1f), ("CarPaint_Beige", 1f),
        };

        private List<ParkedCar> ParkedModels => _parkedModels ??= ParkedCars.All().ToList();

        private ParkedCar ParkedModel(string name) => ParkedModels.First(m => m.Name == name);

        /// <summary>Weighted model pick from one uniform draw (vans skipped in short bays).</summary>
        private ParkedCar PickParked(double r, bool noVan)
        {
            var pool = ParkedModels.Where(m => !(noVan && m.IsVan)).ToList();
            float total = pool.Sum(m => m.Weight), acc = 0f;
            foreach (var m in pool)
            {
                acc += m.Weight;
                if (r * total < acc) return m;
            }
            return pool[pool.Count - 1];
        }

        private static string PickPaint((string paint, float weight)[] mix, double r)
        {
            float total = mix.Sum(p => p.weight), acc = 0f;
            foreach (var p in mix)
            {
                acc += p.weight;
                if (r * total < acc) return p.paint;
            }
            return mix[mix.Length - 1].paint;
        }

        private void PlaceCar(int lv, ParkedCar model, Vector3 bayCenter, Vector3 inward, float depth, string label, bool noseIn,
            string forcedPaint = null)
        {
            string paint = forcedPaint ?? PickPaint(model.IsVan ? VanPaintMix : CarPaintMix, _rng.NextDouble());
            var pm = _props.Parked(model);
            var mats = (Material[])pm.Materials.Clone();
            mats[0] = M(paint);
            float len = model.Length;
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Vector3 pos = bayCenter + inward * (depth * 0.5f - len * 0.5f - 0.35f) + side * R(-0.12f, 0.12f);
            float yaw = Quaternion.LookRotation(noseIn ? inward : -inward).eulerAngles.y + R(-3.5f, 3.5f);
            _carCount++;
            var go = PlaceProp(pm, $"Car_{_carCount:000}_{model.Name}_{paint.Replace("CarPaint_", "")}_{label}", _tCarsLv[lv], pos, yaw, mats);
            GameObjectUtility.SetStaticEditorFlags(go, DetailFlags & ~StaticEditorFlags.BatchingStatic);
            _parked.Add(new Parked { Go = go, BayCenter = bayCenter, Inward = inward, Depth = depth, Level = lv, Label = label });
        }
    }
}
