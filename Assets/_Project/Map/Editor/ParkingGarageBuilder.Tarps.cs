using System.Linq;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Covered cars on level -2 (construction zones): a real parked model under one of four covers, cycling so
    /// every kind shows up (see <see cref="TarpCars"/>). The car underneath is as dusty as the rest of the level.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private int _tarpIndex;

        private static readonly TarpKind[] TarpOrder =
        {
            TarpKind.Cover, TarpKind.BlueTarp, TarpKind.Canvas, TarpKind.Foil, TarpKind.Cover,
            TarpKind.BlueTarp, TarpKind.Foil, TarpKind.Canvas, TarpKind.Cover, TarpKind.BlueTarp,
        };

        private void DrapedCar(int lv, CarType type, bool alt, float x, float z, float yaw)
        {
            var p = new Vector3(x, FloorY(lv), z);
            if (NearSpawn(p, 1.4f)) return;
            int seed = StableHash($"tarp{lv}_{x:0.0}_{z:0.0}");
            var rnd = new System.Random(seed);
            string[] pool = type == CarType.Van ? new[] { "Van_0", "Van_1" }
                : type == CarType.Suv ? new[] { "Suv_0", "Suv_1", "Estate_1" }
                : new[] { "Sedan_0", "Sedan_1", "Estate_0", "CompactHatch_1", "CityHatch_0", "CompactHatch_0" };
            var model = ParkedModel(pool[rnd.Next(pool.Length)]);
            var kind = TarpOrder[_tarpIndex++ % TarpOrder.Length];
            var pm = _props.Parked(model);
            string paint = PickPaint(model.IsVan ? VanPaintMix : CarPaintMix, rnd.NextDouble());
            var mats = (Material[])pm.Materials.Clone();
            mats[0] = M(paint);
            mats = DustyMats(mats, lv == 2 ? MaterialLibrary.DustPreset.Forgotten : MaterialLibrary.DustPreset.LightPlus, lv);
            var go = PlaceProp(pm, $"TarpCar_{Tag(lv)}_{++_propIndex:000}_{kind}_{model.Name}", _tPropsLv[lv], p, yaw + (float)(rnd.NextDouble() * 4.0 - 2.0), mats);
            if (!_store.TryGetValue("Tarps", out var list)) _store["Tarps"] = list = new System.Collections.Generic.List<Mesh>();
            var built = TarpCars.Build(_lib, model, pm.Mesh, kind, seed % 1000, list);
            TarpCars.Attach(go, built, DetailFlags);
            Debug.Log($"[ParkingGarage] Covered car {go.name}: {kind} over {model.Name} ({paint}) at ({x:0.0}, {z:0.0})");
        }
    }
}
