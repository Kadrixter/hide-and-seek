using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>Acoustic surface type used to pick footstep sounds.</summary>
    public enum SurfaceType
    {
        Concrete = 0,
        Metal = 1,
        Water = 2,
    }

    /// <summary>
    /// Tags a surface for footstep audio. Two ways to use it:
    /// <list type="bullet">
    /// <item>On (or above) a GameObject with a collider: steps landing on that collider use <see cref="Type"/>.</item>
    /// <item>As a zone: set <see cref="ZoneSize"/> &gt; 0. Any step whose foot point lies inside the oriented box
    /// (centred on this transform) uses <see cref="Type"/>. Handy for decal puddles without colliders or flooded areas.</item>
    /// </list>
    /// Lives in its own tiny assembly (HideAndSeek.Player.Surfaces) so map builders can reference it.
    /// </summary>
    [DisallowMultipleComponent]
    public class FootstepSurface : MonoBehaviour
    {
        [SerializeField] private SurfaceType type = SurfaceType.Concrete;
        [Tooltip("Zone box size in metres, oriented with this transform (scale ignored). Zero = not a zone (collider tag only).")]
        [SerializeField] private Vector3 zoneSize = Vector3.zero;
        [Tooltip("Higher priority wins when zones overlap.")]
        [SerializeField] private int priority;

        private static readonly List<FootstepSurface> Zones = new List<FootstepSurface>();

        public SurfaceType Type { get => type; set => type = value; }
        public Vector3 ZoneSize { get => zoneSize; set { zoneSize = value; Refresh(); } }
        public int Priority { get => priority; set => priority = value; }
        public bool IsZone => zoneSize.x > 0f && zoneSize.y > 0f && zoneSize.z > 0f;

        private void OnEnable() => Refresh();
        private void OnDisable() => Zones.Remove(this);

        private void Refresh()
        {
            Zones.Remove(this);
            if (isActiveAndEnabled && IsZone) Zones.Add(this);
        }

        /// <summary>True if the world point is inside this zone's box.</summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (!IsZone) return false;
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            Vector3 lossy = transform.lossyScale;
            Vector3 half = new Vector3(zoneSize.x / Mathf.Max(1e-4f, Mathf.Abs(lossy.x)),
                                       zoneSize.y / Mathf.Max(1e-4f, Mathf.Abs(lossy.y)),
                                       zoneSize.z / Mathf.Max(1e-4f, Mathf.Abs(lossy.z))) * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>Finds the highest-priority enabled zone containing the point.</summary>
        public static bool TryGetZoneAt(Vector3 worldPoint, out SurfaceType surface)
        {
            surface = SurfaceType.Concrete;
            int best = int.MinValue;
            bool found = false;
            for (int i = 0; i < Zones.Count; i++)
            {
                var z = Zones[i];
                if (z == null || z.priority < best || !z.Contains(worldPoint)) continue;
                best = z.priority;
                surface = z.type;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Keyword matching used for physics-material and render-material names (case-insensitive).
        /// Metal: metal, grate, steel, iron, grid, drain. Water: puddle, water, wet, flood.
        /// </summary>
        public static bool TryParseName(string name, out SurfaceType surface)
        {
            surface = SurfaceType.Concrete;
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.Contains("puddle") || n.Contains("water") || n.Contains("wet") || n.Contains("flood"))
            {
                surface = SurfaceType.Water;
                return true;
            }
            if (n.Contains("metal") || n.Contains("grate") || n.Contains("steel") || n.Contains("iron") ||
                n.Contains("grid") || n.Contains("drain"))
            {
                surface = SurfaceType.Metal;
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate() => Refresh();

        private void OnDrawGizmosSelected()
        {
            if (!IsZone) return;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = type == SurfaceType.Water ? new Color(0.2f, 0.5f, 1f, 0.35f)
                         : type == SurfaceType.Metal ? new Color(0.8f, 0.8f, 0.9f, 0.35f)
                         : new Color(0.6f, 0.6f, 0.6f, 0.35f);
            Gizmos.DrawCube(Vector3.zero, zoneSize);
            Gizmos.DrawWireCube(Vector3.zero, zoneSize);
        }
#endif
    }
}
