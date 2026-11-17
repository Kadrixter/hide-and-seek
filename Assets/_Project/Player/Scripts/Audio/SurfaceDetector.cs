using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Finds the acoustic surface under a foot. Order:
    /// 1. PhysicsMaterial name keyword of the ground collider,
    /// 2. <see cref="FootstepSurface"/> on the ground collider (or its parents) / on a trigger volume above it,
    /// 3. <see cref="FootstepSurface"/> zones containing the foot point,
    /// 4. render material name keyword (sub-mesh aware for MeshColliders),
    /// 5. concrete.
    /// </summary>
    public static class SurfaceDetector
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[12];

        public static SurfaceType Detect(Vector3 footPoint, Transform ignoreRoot, LayerMask mask,
            float probeUp, float probeDown, out bool hitGround)
        {
            hitGround = false;
            Vector3 origin = footPoint + Vector3.up * probeUp;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, probeUp + probeDown, mask, QueryTriggerInteraction.Collide);
            SortByDistance(n);

            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                var col = h.collider;
                if (col == null || (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot))) continue;

                if (col.isTrigger)
                {
                    // Trigger volumes only count when explicitly tagged (e.g. a flood volume).
                    var tag = col.GetComponentInParent<FootstepSurface>();
                    if (tag != null) { hitGround = true; return tag.Type; }
                    continue;
                }

                hitGround = true;
                return Classify(h, footPoint);
            }

            return FootstepSurface.TryGetZoneAt(footPoint, out var zone) ? zone : SurfaceType.Concrete;
        }

        private static SurfaceType Classify(RaycastHit h, Vector3 footPoint)
        {
            var col = h.collider;
            if (col.sharedMaterial != null && FootstepSurface.TryParseName(col.sharedMaterial.name, out var s)) return s;

            var tag = col.GetComponentInParent<FootstepSurface>();
            if (tag != null) return tag.Type;

            Vector3 probe = new Vector3(footPoint.x, h.point.y, footPoint.z);
            if (FootstepSurface.TryGetZoneAt(probe, out s) || FootstepSurface.TryGetZoneAt(h.point, out s)) return s;

            var mat = FindMaterial(h);
            if (mat != null && FootstepSurface.TryParseName(mat.name, out s)) return s;
            return SurfaceType.Concrete;
        }

        private static Material FindMaterial(RaycastHit h)
        {
            var col = h.collider;
            var renderer = col.GetComponent<Renderer>();
            if (renderer == null) renderer = col.GetComponentInParent<Renderer>();
            if (renderer == null) return null;
            var mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0) return null;
            if (mats.Length == 1) return mats[0];

            // Multi-material mesh: map the hit triangle to its sub-mesh.
            if (col is MeshCollider mc && !mc.convex && h.triangleIndex >= 0 && mc.sharedMesh != null)
            {
                var mf = renderer.GetComponent<MeshFilter>();
                var mesh = mc.sharedMesh;
                if (mf != null && mf.sharedMesh == mesh)
                {
                    int index = h.triangleIndex * 3;
                    for (int sm = 0; sm < mesh.subMeshCount && sm < mats.Length; sm++)
                    {
                        SubMeshDescriptor d = mesh.GetSubMesh(sm);
                        if (d.topology == MeshTopology.Triangles && index >= d.indexStart && index < d.indexStart + d.indexCount)
                            return mats[sm];
                    }
                }
            }
            return mats[0];
        }

        private static void SortByDistance(int n)
        {
            for (int i = 1; i < n; i++)
            {
                var key = Hits[i];
                int j = i - 1;
                while (j >= 0 && Hits[j].distance > key.distance) { Hits[j + 1] = Hits[j]; j--; }
                Hits[j + 1] = key;
            }
        }
    }
}
