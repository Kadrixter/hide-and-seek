using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Candidate spots for fluid leaks (see <see cref="CarLeaks"/>): one under the engine of every car on levels -1
    /// and -2. The run goes towards the aisle when the bonnet faces it, otherwise out from under the side of the
    /// car. The decal sits <see cref="LeakY"/> above the floor - over every other floor decal layer.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        /// <summary>Height of the leak decals above the floor (floor decal layers end at 4.4 cm).</summary>
        public const float LeakY = 0.05f;

        private void BuildLeakSpots()
        {
            var spots = new List<CarLeaks.Spot>();
            foreach (var p in _parked)
            {
                if (p.Level == 0 || p.Label == "WASH") continue;
                var t = p.Go.transform;
                var mesh = p.Go.GetComponent<MeshFilter>().sharedMesh;
                float front = mesh.bounds.max.z;
                Vector3 fwd = t.forward;
                fwd.y = 0f;
                fwd.Normalize();
                Vector3 engine = t.position + fwd * (front - 0.55f); // radiator / sump, just behind the bumper
                bool noseOut = Vector3.Dot(fwd, -p.Inward) > 0f;
                Vector3 run = fwd;
                if (!noseOut)
                {
                    var right = Vector3.Cross(Vector3.up, fwd);
                    run = new System.Random(StableHash("leak" + p.Label)).NextDouble() < 0.5 ? right : -right;
                }
                Vector3 c = engine + run * 0.96f;
                c.y = FloorY(p.Level) + LeakY;
                spots.Add(new CarLeaks.Spot { Pos = c, Yaw = Quaternion.LookRotation(run).eulerAngles.y, Level = p.Level, Car = p.Go.name });
            }
            var go = new GameObject("CarLeaks");
            go.transform.SetParent(_root, false);
            var leaks = go.AddComponent<CarLeaks>();
            leaks.Spots = spots.ToArray();
            leaks.Fluids = new[] { M("Leak_Oil"), M("Leak_CoolantGreen"), M("Leak_CoolantPink"), M("Leak_Brake") };
            var shapes = new List<Mesh>();
            for (int s = 0; s < DetailTextures.LeakShapes; s++)
            for (int mirror = 0; mirror < 2; mirror++)
            {
                var m = LeakQuad(s, mirror == 1);
                _store["Props"].Add(m);
                shapes.Add(m);
            }
            leaks.Shapes = shapes.ToArray();
            Debug.Log($"[ParkingGarage] Leak spots: {spots.Count} cars on levels -1/-2, {leaks.Count} leaking per round");
        }

        /// <summary>1.6 x 3.2 m quad facing up showing one column of the leak atlas (optionally mirrored).</summary>
        private static Mesh LeakQuad(int shape, bool mirror)
        {
            float u0 = shape / (float)DetailTextures.LeakShapes, u1 = (shape + 1) / (float)DetailTextures.LeakShapes;
            if (mirror) (u0, u1) = (u1, u0);
            var m = new Mesh { name = $"LeakDecal_{shape}{(mirror ? "m" : "")}" };
            m.SetVertices(new List<Vector3> { new Vector3(-0.8f, 0f, -1.6f), new Vector3(0.8f, 0f, -1.6f), new Vector3(0.8f, 0f, 1.6f), new Vector3(-0.8f, 0f, 1.6f) });
            m.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            m.SetUVs(0, new List<Vector2> { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f) });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
