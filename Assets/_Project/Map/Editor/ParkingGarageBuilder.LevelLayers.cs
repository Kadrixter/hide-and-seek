using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Light separation between the levels (see <see cref="LevelLighting"/>): every level's geometry goes on its own
    /// layer, the slab undersides and beams are a separate mesh belonging to the level below (its ceiling), the map
    /// lamps only light their own level, and the map root gets the runtime component that does the same for every
    /// other light (with the ramp / stair openings where a light may see both levels). Ramps and stair flights stay on
    /// Default: they join two levels and are lit from both.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private readonly ChunkedBuilder[] _under = new ChunkedBuilder[Levels];

        /// <summary>Ceiling of level <paramref name="lv"/>: underside of the slab above it and its beams.</summary>
        private ChunkedBuilder Under(int lv) => _under[lv] ??= new ChunkedBuilder(_lib);

        private void EmitUndersides()
        {
            for (int lv = 1; lv < Levels; lv++)
                if (_under[lv] != null)
                    EmitChunks(_under[lv], "SlabUnder_" + Tag(lv), Group(_tFloors, "Level-" + (lv + 1)), true, true);
        }

        private void ApplyLevelLayers()
        {
            for (int lv = 0; lv < Levels; lv++)
            {
                string g = "Level-" + (lv + 1);
                foreach (var parent in new[] { _tFloors, _tWalls, _tColumns, _tDecals })
                {
                    var t = parent.Find(g);
                    if (t != null) LevelLighting.SetLevelLayer(t, lv);
                }
                LevelLighting.SetLevelLayer(_tPropsLv[lv], lv);
                LevelLighting.SetLevelLayer(_tCarsLv[lv], lv);
            }
            foreach (Transform t in _tFloors)
                if (t.name.StartsWith("Roof")) t.gameObject.layer = LevelLighting.LayerOf(0);

            int lamps = 0;
            foreach (var l in _tLights.GetComponentsInChildren<Light>(true))
            {
                int lv = LevelLighting.LevelAt(l.transform.position.y);
                l.cullingMask = LevelLighting.MaskForLevel(lv);
                lamps++;
            }
            foreach (Transform t in _tLights)
                LevelLighting.SetLevelLayer(t, LevelLighting.LevelAt(t.position.y - 0.5f));

            var ll = _root.gameObject.AddComponent<LevelLighting>();
            ll.Configure(SlabHoles().ToArray(), new Vector2(HX + WallT, HZ + WallT));
            Debug.Log($"[ParkingGarage] Level layers {LevelLighting.FirstLayer}-{LevelLighting.FirstLayer + Levels - 1}: {lamps} map lights restricted to their level, {SlabHoles().Count} slab openings");
        }
    }
}
