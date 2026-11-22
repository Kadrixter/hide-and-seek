using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map
{
    /// <summary>
    /// Keeps light from leaking through the floor slabs. The garage builder puts every level's geometry on its own
    /// layer (<see cref="LayerOf"/>: floor top, walls, columns, props and cars of that level; the slab underside counts
    /// as the ceiling of the level below) and restricts the map lamps to their level. This component (on the map root)
    /// does the same for every other light at runtime - flashlights, car alarms, CCTV, RC cars, elevators, effects:
    /// each frame its culling mask keeps only the level it is on (plus the neighbour while it is inside a ramp,
    /// stairwell or lift opening). Objects on other layers (players, items) are not touched. Outside the garage
    /// (race track) nothing is restricted.
    /// </summary>
    public sealed class LevelLighting : MonoBehaviour
    {
        public const int FirstLayer = 20, Levels = 3;
        public const float LevelHeight = 3.4f;
        public const int AllLevels = ((1 << Levels) - 1) << FirstLayer;

        /// <summary>Openings in the slabs (x/z), set by the builder: lights inside them see both levels.</summary>
        [SerializeField] private Rect[] holes = new Rect[0];
        [SerializeField] private Vector2 halfSize = new Vector2(90.5f, 60.5f);

        public static LevelLighting Instance { get; private set; }

        private readonly List<Light> _lights = new();
        private readonly HashSet<Light> _seen = new();
        private float _nextScan;

        public static int LayerOf(int level) => FirstLayer + Mathf.Clamp(level, 0, Levels - 1);

        /// <summary>Level whose air (floor up to the next slab) contains height y.</summary>
        public static int LevelAt(float y) => Mathf.Clamp(Mathf.CeilToInt(-y / LevelHeight - 0.001f), 0, Levels - 1);

        /// <summary>Culling mask: everything but the other levels.</summary>
        public static int MaskForLevel(int level) => ~AllLevels | (1 << LayerOf(level));

        public void Configure(Rect[] slabHoles, Vector2 garageHalfSize)
        {
            holes = slabHoles;
            halfSize = garageHalfSize;
        }

        /// <summary>Level bits a light at <paramref name="p"/> may reach.</summary>
        public int LevelBits(Vector3 p)
        {
            if (Mathf.Abs(p.x) > halfSize.x + 1f || Mathf.Abs(p.z) > halfSize.y + 1f || p.y > 3.6f || p.y < -LevelHeight * Levels)
                return AllLevels;
            int lv = LevelAt(p.y);
            int bits = 1 << LayerOf(lv);
            foreach (var h in holes)
                if (h.Contains(new Vector2(p.x, p.z)))
                {
                    if (lv > 0) bits |= 1 << LayerOf(lv - 1);
                    if (lv < Levels - 1) bits |= 1 << LayerOf(lv + 1);
                    break;
                }
            return bits;
        }

        /// <summary>Puts a whole hierarchy (built at runtime on one level) on that level's layer.</summary>
        public static void SetLevelLayer(Transform root, int level)
        {
            int layer = LayerOf(level);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.4f;
                Scan();
            }
            Apply();
        }

        /// <summary>Restricts every tracked light to the level(s) it is on now.</summary>
        public void Apply()
        {
            for (int i = _lights.Count - 1; i >= 0; i--)
            {
                var l = _lights[i];
                if (l == null) { _lights.RemoveAt(i); continue; }
                if (!l.enabled || !l.gameObject.activeInHierarchy) continue;
                int mask = (l.cullingMask & ~AllLevels) | LevelBits(l.transform.position);
                if (l.cullingMask != mask) l.cullingMask = mask;
            }
        }

        public void ScanNow() => Scan();

        /// <summary>Picks up new lights that still see every level (the builder's map lamps are already restricted).</summary>
        private void Scan()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null || !_seen.Add(l)) continue;
                if (l.type == LightType.Directional || (l.cullingMask & AllLevels) != AllLevels) continue;
                _lights.Add(l);
            }
            if (_seen.Count > 4000) _seen.RemoveWhere(x => x == null);
        }

        /// <summary>Registers a light right away (no wait for the next scan), e.g. a short muzzle flash.</summary>
        public void Track(Light l)
        {
            if (l != null && _seen.Add(l)) _lights.Add(l);
        }
    }
}
