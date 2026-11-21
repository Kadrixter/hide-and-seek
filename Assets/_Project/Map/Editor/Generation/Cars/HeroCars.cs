using System.Collections.Generic;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Registry of the detailed hero car models (one class per model, one file each).</summary>
    internal static class HeroCars
    {
        public static IEnumerable<SportCar> All()
        {
            yield return new LamborghiniDiablo();
            yield return new Peugeot306Cabrio();
            yield return new MazdaRx7();
            yield return new MazdaRx8();
            yield return new VwGolf4();
            yield return new BmwE30();
        }

        public static SportCar ByName(string name)
        {
            foreach (var c in All())
                if (c.Name == name) return c;
            throw new KeyNotFoundException("Hero car " + name);
        }
    }
}
