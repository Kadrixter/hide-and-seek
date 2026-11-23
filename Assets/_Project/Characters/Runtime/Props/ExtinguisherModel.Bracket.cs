using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Wall bracket of the extinguisher: a steel plate screwed to the wall, a saddle hook the valve neck sits on and a
    /// strap round the back half of the tank closed by a black quick-release buckle at the front. Bracket space: wall
    /// surface at z = 0, +Z out of the wall, +Y up, floor at y = 0 of the model it holds; the tank axis stands at
    /// z = <see cref="BracketDepth"/> + tank radius.
    /// </summary>
    public static partial class ExtinguisherModel
    {
        /// <summary>Gap between the wall and the back of the tank.</summary>
        public const float BracketDepth = 0.046f;

        static readonly Dictionary<Style, Mesh> Brackets = new();
        static readonly Dictionary<Style, Mesh> Straps = new();

        /// <summary>Bracket on its own (stays on the wall when the extinguisher is taken). The strap is a child named "Strap"
        /// (the game opens it a little while the extinguisher is out).</summary>
        public static Transform CreateBracket(Transform parent, int layer, int style = -1)
        {
            var s = Styles[Mathf.Clamp(style < 0 ? GameStyle : style, 0, Styles.Length - 1)];
            var set = MeshesFor(s);
            if (!Brackets.TryGetValue(s, out var mesh)) Brackets[s] = mesh = BracketMesh(s);
            if (!Straps.TryGetValue(s, out var strap)) Straps[s] = strap = StrapMesh(s);
            var root = new GameObject("ExtBracket").transform;
            root.SetParent(parent, false);
            var mats = new[] { set.BodyMats[SubDark], set.BodyMats[SubBlack], set.BodyMats[SubChrome] };
            Make(root, "Plate", mesh, mats, layer);
            var st = new GameObject("Strap").transform;
            st.SetParent(root, false);
            st.localPosition = new Vector3(0f, 0f, BracketDepth * 0.5f);
            Make(st, "StrapMesh", strap, mats, layer);
            return root;
        }

        static void Make(Transform parent, string name, Mesh mesh, Material[] mats, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static Mesh BracketMesh(Style s)
        {
            var m = new PaMesh(3);
            float R = s.TankR, cz = BracketDepth + R, yb = s.TankTop * 0.62f, yn = s.NeckY + 0.002f;
            // Back plate with rounded corners, from the strap up to the neck hook; four screw heads.
            float y0 = yb - 0.05f, y1 = yn + 0.02f;
            m.Pebble(PaMesh.RoundRect(0.03f, (y1 - y0) * 0.5f, 0.01f, 3), PaMesh.SlabRings(0f, 0.004f, 0.0015f, 1), new Vector3(0f, (y0 + y1) * 0.5f, 0f), Vector3.right, Vector3.up, Vector3.forward, 0);
            foreach (float y in new[] { y0 + 0.018f, y1 - 0.018f })
            foreach (float x in new[] { -0.016f, 0.016f })
                m.Lathe(new Vector3(x, y, 0.004f), Vector3.forward, new[] { new Vector2(0.0042f, 0f), new Vector2(0.0042f, 0.001f), new Vector2(0.003f, 0.0022f), new Vector2(0f, 0.0025f) }, 10, 2);
            // Saddle hook under the brass neck: an arm out of the plate ending in a U the neck rests in.
            float nr = 0.03f * s.Head;
            m.Box(new Vector3(0f, yn - 0.006f, (cz - nr) * 0.5f + 0.002f), new Vector3(0.026f, 0.006f, cz - nr), Quaternion.identity, 0);
            var u = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Lerp(-30f, 210f, i / 10f) * Mathf.Deg2Rad;
                u.Add(new Vector2(Mathf.Cos(a) * (nr + 0.004f), Mathf.Sin(a) * (nr + 0.004f)));
            }
            var pts = new List<Vector3>();
            foreach (var q in u) pts.Add(new Vector3(q.x, yn - 0.006f, cz - q.y));
            m.Tube(pts, 0.0035f, 8, 0, true);
            // Arms of the strap.
            foreach (float x in new[] { -1f, 1f })
                m.Box(new Vector3(x * 0.024f, yb, BracketDepth * 0.5f + 0.002f), new Vector3(0.006f, 0.03f, BracketDepth + 0.002f), Quaternion.identity, 0);
            return m.ToMesh("ExtBracket");
        }

        /// <summary>Strap (strap space: origin half way to the tank): steel band round the back of the tank, buckle in front.</summary>
        static Mesh StrapMesh(Style s)
        {
            var m = new PaMesh(3);
            float R = s.TankR + 0.003f, cz = BracketDepth * 0.5f + s.TankR, yb = s.TankTop * 0.62f;
            var pts = new List<Vector3>();
            for (int i = 0; i <= 28; i++)
            {
                float a = Mathf.Lerp(-112f, 112f, i / 28f) * Mathf.Deg2Rad;   // 0 = at the wall side
                pts.Add(new Vector3(Mathf.Sin(a) * R, yb, cz - Mathf.Cos(a) * R));
            }
            for (int k = 0; k < pts.Count - 1; k++)
            {
                Vector3 a = pts[k], b = pts[k + 1];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 outN = new Vector3(mid.x, 0f, mid.z - cz).normalized;
                m.Box(mid + outN * 0.0012f, new Vector3(0.0024f, 0.022f, Vector3.Distance(a, b) + 0.0006f), Quaternion.LookRotation(b - a, Vector3.up), 0);
            }
            // Rubber caps on both spring-clip ends.
            foreach (var e in new[] { pts[0], pts[pts.Count - 1] })
            {
                Vector3 outN = new Vector3(e.x, 0f, e.z - cz).normalized;
                m.Pebble(PaMesh.RoundRect(0.004f, 0.012f, 0.003f, 2), PaMesh.SlabRings(-0.004f, 0.004f, 0.003f, 2), e + outN * 0.002f, Vector3.Cross(Vector3.up, outN), Vector3.up, outN, 1);
            }
            return m.ToMesh("ExtStrap");
        }
    }
}
