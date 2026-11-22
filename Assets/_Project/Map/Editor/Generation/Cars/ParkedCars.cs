using System.Collections.Generic;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Registry of the ordinary parked car models (each in two variants: rims, trim, interior, glazing).</summary>
    internal static class ParkedCars
    {
        public static IEnumerable<ParkedCar> All()
        {
            for (int v = 0; v < 2; v++)
            {
                yield return new CityHatch(v);
                yield return new CompactHatch(v);
                yield return new Sedan(v);
                yield return new Estate(v);
                yield return new Suv(v);
                yield return new Van(v);
            }
        }
    }
}
