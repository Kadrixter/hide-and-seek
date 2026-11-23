using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Procedural mesh parts (<see cref="ProcMesh"/>) for the security rooms: built once, shared by the three levels.</summary>
    public partial class SecurityRooms
    {
        private readonly Dictionary<string, Mesh> _meshCache = new();

        private Mesh CachedMesh(string key, System.Action<ProcMesh> build)
        {
            if (_meshCache.TryGetValue(key, out var m) && m != null) return m;
            var pm = new ProcMesh();
            build(pm);
            m = pm.ToMesh("Sec_" + key);
            _meshCache[key] = m;
            return m;
        }

        /// <summary>A mesh part in world space (or local to <paramref name="parent"/> when <paramref name="local"/>).</summary>
        private GameObject MeshObj(Transform parent, string n, Mesh mesh, Vector3 pos, Quaternion rot, Color c, Surf s = default, bool local = false, bool shadows = true)
        {
            var g = new GameObject(n);
            g.transform.SetParent(parent, false);
            if (local) g.transform.SetLocalPositionAndRotation(pos, rot);
            else g.transform.SetPositionAndRotation(pos, rot);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            Paint(r, c, s, Vector3.one);
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }
    }
}
