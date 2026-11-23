using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// The wrecked Golf GTI (<see cref="GolfWreck"/>) on level -2 (y -6.8): nose out to the central aisle in bay D23,
    /// between two columns; the bays either side stay empty (open door on the left, the parts on the right).
    /// One prop with box colliders; not static-batched (its dust shader works in mesh space).
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private const int WreckLevel = 2;
        private const string WreckBay = "D23";
        private static readonly string[] WreckKeepEmpty = { "D22", "D24" };

        private static bool WreckReserved(int lv, string label) =>
            lv == WreckLevel && (label == WreckBay || WreckKeepEmpty.Contains(label));

        private void PlaceWreck()
        {
            var bay = _bays.First(b => b.Level == WreckLevel && b.Label == WreckBay);
            var store = _store.TryGetValue("Wreck_Golf", out var list) ? list : _store["Wreck_Golf"] = new System.Collections.Generic.List<Mesh>();
            var pm = GolfWreck.Create(_props, _lib, store);
            // rear 60 cm off the back line (clear of the wheel stops), nose towards the aisle, a few degrees off straight
            Vector3 pos = bay.Center + bay.Inward * (bay.Depth * 0.5f - 0.6f - 4.15f * 0.5f);
            float yaw = Quaternion.LookRotation(-bay.Inward).eulerAngles.y + 4f;
            var go = PlaceProp(pm, "Wreck_VwGolf4Gti_" + WreckBay, _tCarsLv[WreckLevel], pos, yaw);
            GameObjectUtility.SetStaticEditorFlags(go, DetailFlags & ~StaticEditorFlags.BatchingStatic);
            Debug.Log($"[ParkingGarage] Wreck Golf GTI on {LevelName(WreckLevel)} bay {WreckBay} at ({pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}), yaw {yaw:0}, {pm.Mesh.vertexCount} verts");
        }
    }
}
