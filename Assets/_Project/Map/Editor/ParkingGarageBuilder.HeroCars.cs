using System.Linq;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// The detailed hero cars stand in fixed bays (the same every build and every round), spread over the three
    /// floors. Their bays (plus a few kept empty next to them) are taken out of the random car pool.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private struct HeroSpot
        {
            public int Level;
            public string Bay, Model, Paint;
            public bool NoseIn;
            public string[] KeepEmpty;
            public float Lat, Rot; // fixed sloppy parking (0 = straight, the random crooked pass may still apply)
        }

        private static readonly HeroSpot[] HeroSpots =
        {
            // P-1: the Lamborghini parked diagonally across two bays near the central lobby
            new HeroSpot { Level = 0, Bay = "E29", Model = "LamborghiniDiablo", Paint = "CarPaint_Arancio", NoseIn = true, KeepEmpty = new[] { "E30" }, Lat = 1.05f, Rot = 9f },
            new HeroSpot { Level = 0, Bay = "G11", Model = "Peugeot306Cabrio", Paint = "CarPaint_Sunflower", NoseIn = false },
            new HeroSpot { Level = 0, Bay = "C45", Model = "BmwE30", Paint = "CarPaint_Alpinweiss", NoseIn = true },
            // P-2
            new HeroSpot { Level = 1, Bay = "F20", Model = "MazdaRx7", Paint = "CarPaint_Brilliantrot", NoseIn = true },
            new HeroSpot { Level = 1, Bay = "B50", Model = "BmwE30", Paint = "CarPaint_Diamantschwarz", NoseIn = false },
            new HeroSpot { Level = 1, Bay = "H32", Model = "VwGolf4", Paint = "CarPaint_ReflexSilver", NoseIn = true },
            // P-3
            new HeroSpot { Level = 2, Bay = "D17", Model = "MazdaRx8", Paint = "CarPaint_MontegoBlue", NoseIn = true },
            new HeroSpot { Level = 2, Bay = "G40", Model = "BmwE30", Paint = "CarPaint_VelocityRed", NoseIn = true },
        };

        private static bool HeroReserved(int lv, string label) =>
            HeroSpots.Any(h => h.Level == lv && (h.Bay == label || (h.KeepEmpty != null && h.KeepEmpty.Contains(label)))) || WreckReserved(lv, label);

        private void PlaceHeroCars()
        {
            foreach (var h in HeroSpots)
            {
                var bay = _bays.First(b => b.Level == h.Level && b.Label == h.Bay);
                var car = HeroCars.ByName(h.Model);
                var pm = _props.Hero(car);
                var mats = (Material[])pm.Materials.Clone();
                mats[0] = M(h.Paint);
                Vector3 pos = bay.Center + bay.Inward * (bay.Depth * 0.5f - car.Length * 0.5f - 0.35f);
                float yaw = Quaternion.LookRotation(h.NoseIn ? bay.Inward : -bay.Inward).eulerAngles.y;
                _carCount++;
                var go = PlaceProp(pm, $"Car_{_carCount:000}_{car.Name}_{h.Paint.Replace("CarPaint_", "")}_{h.Bay}", _tCarsLv[h.Level], pos, yaw, mats);
                _parked.Add(new Parked
                {
                    Go = go, BayCenter = bay.Center, Inward = bay.Inward, Depth = bay.Depth, Level = h.Level, Label = h.Bay,
                    Hero = true, Fixed = h.Lat != 0f || h.Rot != 0f, FixLat = h.Lat, FixRot = h.Rot
                });
                Debug.Log($"[ParkingGarage] Hero car {car.Name} ({h.Paint}) on {LevelName(h.Level)} bay {h.Bay} at ({pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}), yaw {yaw:0}");
            }
            PlaceWreck();
        }
    }
}
