using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Map;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Keeps the players' flashlights (spot light, volumetric beam, dust motes) on their own garage level for every
    /// camera that renders: before a camera culls, each flashlight that this camera could not see across the levels
    /// (<see cref="LevelSight.Sees"/>: another level, away from a ramp shaft / stairwell both are near) is switched
    /// off, and after the camera has rendered everything goes back to what the flashlight itself wants. So it holds for
    /// whatever camera is drawing - the player's own view and its hands camera, the spectator orbit camera, the RC car
    /// view, the kill cam, CCTV monitors - and for the own flashlight too (body on one level, RC car on another).
    /// <para>
    /// Why: <see cref="LevelLighting"/> only keeps a light off the geometry layers of the other levels. Players, items,
    /// ramps and stair flights (Default layer) stay lit, and the slab between the levels only blocks that light through
    /// its shadow - gone with shadows switched off, beyond the shadow distance (35 m on low) and for a frame whenever the
    /// mask lags the light (it is set before the flashlight moves, and not at all while the light is off: a light
    /// switched on one level after being switched off on another kept the old level's mask for a frame). The dust motes
    /// glow anywhere inside the cone, also above the slab (shadows or not). Players saw another floor's flashlight on
    /// each other, on a ramp or a stairway, and its dust over the floor. The mask is refreshed here as well, right
    /// before rendering, from where the light really is.
    /// </para>
    /// The dust motes are also kept between the floor and the ceiling of the light's level (FlashlightLevelCulling.Dust.cs).
    /// Cost: per camera a loop over the flashlights that are on (a few rect tests each). <c>-noflashcull</c> turns it off
    /// (test comparison only).
    /// </summary>
    public sealed partial class FlashlightLevelCulling : MonoBehaviour
    {
        /// <summary>Test switch: the old behaviour (no per-camera hiding).</summary>
        public static readonly bool Off = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-noflashcull") >= 0;

        private sealed class Entry
        {
            public Light L;
            public Renderer[] R;
            public bool WantL;
            public bool[] WantR;
            public Renderer Dust;
            public Vector4 Band;
        }

        private static FlashlightLevelCulling _inst;
        [System.NonSerialized] private readonly Dictionary<Flashlight, Entry> _map = new();
        [System.NonSerialized] private readonly List<Entry> _live = new();
        private readonly List<Camera> _stack = new();
        private readonly List<Flashlight> _dead = new();
        private int _stackFrame = -1;

        /// <summary>Flashlights hidden from the camera that culled last (tests / debug).</summary>
        public static int HiddenLast { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_inst != null || Off) return;
            var go = new GameObject("[FlashlightLevelCulling]");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<FlashlightLevelCulling>();
        }

        private void OnEnable()
        {
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= PreCull;
            Camera.onPostRender -= PostRender;
            Restore();
            _stack.Clear();
        }

        private void PreCull(Camera cam)
        {
            if (cam == null) return;
            // a render that never reported back (should not happen): start over from the flashlights' own state
            if (_stack.Count > 0 && _stackFrame != Time.frameCount)
            {
                Restore();
                _stack.Clear();
            }
            if (_stack.Count == 0)
            {
                _stackFrame = Time.frameCount;
                Capture();
            }
            _stack.Add(cam);
            Apply(cam);
        }

        private void PostRender(Camera cam)
        {
            int i = _stack.LastIndexOf(cam);
            if (i < 0) return;
            _stack.RemoveRange(i, _stack.Count - i);
            if (_stack.Count == 0) Restore();
            else Apply(_stack[_stack.Count - 1]);   // a camera rendered inside another one (mirror, monitor)
        }

        /// <summary>Notes what every flashlight wants now (set by <see cref="Flashlight"/> in LateUpdate) and refreshes its level mask.</summary>
        private void Capture()
        {
            _live.Clear();
            var ll = LevelLighting.Instance;
            var all = NetPlayer.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i] != null ? all[i].Flashlight : null;
                if (f == null) continue;
                if (!_map.TryGetValue(f, out var e))
                {
                    var rs = f.GetComponentsInChildren<Renderer>(true);
                    e = new Entry { L = f.SpotLight, R = rs, WantR = new bool[rs.Length], Dust = f.GetComponentInChildren<ParticleSystemRenderer>(true) };
                    _map[f] = e;
                }
                if (e.L == null) continue;
                e.WantL = e.L.enabled;
                bool any = e.WantL;
                for (int k = 0; k < e.R.Length; k++)
                {
                    e.WantR[k] = e.R[k] != null && e.R[k].enabled;
                    any |= e.WantR[k];
                }
                if (!any) continue;
                if (ll != null && e.WantL)
                {
                    int mask = (e.L.cullingMask & ~LevelLighting.AllLevels) | ll.LevelBits(e.L.transform.position);
                    if (e.L.cullingMask != mask) e.L.cullingMask = mask;
                }
                UpdateDustBand(e, ll);
                _live.Add(e);
            }
            if (_map.Count > _live.Count + 16) Prune();
        }

        private void Apply(Camera cam)
        {
            Vector3 eye = cam.transform.position;
            int hidden = 0;
            for (int i = 0; i < _live.Count; i++)
            {
                var e = _live[i];
                if (e.L == null) continue;
                bool show = LevelSight.Sees(eye, e.L.transform.position);
                if (!show) hidden++;
                Set(e, show);
            }
            HiddenLast = hidden;
        }

        private void Restore()
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].L != null) Set(_live[i], true);
        }

        private static void Set(Entry e, bool show)
        {
            bool l = e.WantL && show;
            if (e.L.enabled != l) e.L.enabled = l;
            for (int k = 0; k < e.R.Length; k++)
            {
                var r = e.R[k];
                if (r == null) continue;
                bool v = e.WantR[k] && show;
                if (r.enabled != v) r.enabled = v;
            }
        }

        private void Prune()
        {
            _dead.Clear();
            foreach (var kv in _map) if (kv.Key == null) _dead.Add(kv.Key);
            foreach (var f in _dead) _map.Remove(f);
        }
    }
}
