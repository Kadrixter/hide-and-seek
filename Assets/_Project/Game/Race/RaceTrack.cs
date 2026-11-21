using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// RC race track built at runtime far away from the garage: a closed asphalt ribbon (raised 0.6 m above the
    /// ground, so leaving it means falling off), red-white kerbs, a start / finish gantry with lights, small jump
    /// ramps, short tunnels, tyre stacks, floodlit grandstand where the drivers sit. Also answers spline questions
    /// (nearest sample, point / direction at a distance) for checkpoints and respawns.
    /// </summary>
    public class RaceTrack
    {
        public static readonly Vector3 Origin = new Vector3(0f, 0f, 520f);
        public const float Width = 2.3f, Drop = 0.6f, Step = 0.4f;
        public const int Checkpoints = 16;
        /// <summary>Layout scale of the control points.</summary>
        public const float Scale = 1f;

        // Centre line (local x, height, z in metres); the start line is at the first point. Hills: the track climbs up
        // to ~1.8 m on embankments and comes back down; S-bends and a hairpin in the middle.
        private static readonly Vector3[] Ctrl =
        {
            new Vector3(0f, 0f, -22f), new Vector3(16f, 0f, -23f), new Vector3(30f, 0.2f, -20f), new Vector3(40f, 0.8f, -12f),
            new Vector3(43f, 1.6f, -1f), new Vector3(38f, 1.8f, 9f), new Vector3(28f, 1.0f, 12f), new Vector3(22f, 0.3f, 4f),
            new Vector3(12f, 0f, 2f), new Vector3(6f, 0f, 10f), new Vector3(10f, 0.4f, 19f), new Vector3(0f, 1.2f, 24f),
            new Vector3(-10f, 0.6f, 19f), new Vector3(-12f, 0f, 10f), new Vector3(-20f, 0f, 4f), new Vector3(-30f, 0.4f, 10f),
            new Vector3(-40f, 1.0f, 8f), new Vector3(-45f, 1.4f, -2f), new Vector3(-40f, 0.8f, -13f), new Vector3(-28f, 0.2f, -18f),
            new Vector3(-14f, 0f, -21f),
        };

        // Ramps and tunnels: given by a local point near the track (nearest sample is used).
        private static readonly Vector3[] RampsNear = { new Vector3(8f, 0f, -22.5f), new Vector3(-21f, 0.1f, -19.5f), new Vector3(-16f, 0f, 7f) };
        private static readonly (Vector3 from, Vector3 to)[] TunnelsNear =
        {
            (new Vector3(43f, 1.6f, -4f), new Vector3(40.5f, 1.8f, 6f)),     // through the hill top
            (new Vector3(-9f, 0.7f, 18f), new Vector3(-12f, 0.1f, 11f)),     // in the S down from the north hill
        };

        private readonly List<int> _ramps = new();
        private readonly List<(int a, int b)> _tunnels = new();
        public readonly List<Vector3> Dir3 = new();

        public readonly List<Vector3> Pts = new();   // world, closed loop (last != first)
        public readonly List<Vector3> Dirs = new();
        public readonly List<float> Dist = new();
        public float Length { get; private set; }
        public Transform Root { get; private set; }
        public readonly List<Vector3> Seats = new();
        public float SeatYaw => 0f;
        private Light[] _startLights;

        public RaceTrack()
        {
            Sample();
        }

        // ================================================================ spline

        private void Sample()
        {
            // Dense Catmull-Rom samples, then resampled every Step metres.
            var raw = new List<Vector3>();
            int n = Ctrl.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = Ctrl[(i - 1 + n) % n], p1 = Ctrl[i], p2 = Ctrl[(i + 1) % n], p3 = Ctrl[(i + 2) % n];
                for (int k = 0; k < 40; k++)
                {
                    float t = k / 40f;
                    Vector3 q = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                    raw.Add(Origin + q * Scale);
                }
            }
            var cum = new List<float> { 0f };
            for (int i = 1; i <= raw.Count; i++) cum.Add(cum[i - 1] + Vector3.Distance(raw[i - 1], raw[i % raw.Count]));
            float total = cum[cum.Count - 1];
            int count = Mathf.RoundToInt(total / Step);
            int j = 0;
            for (int s = 0; s < count; s++)
            {
                float d = s * total / count;
                while (j < raw.Count - 1 && cum[j + 1] < d) j++;
                float f = Mathf.InverseLerp(cum[j], cum[j + 1], d);
                Pts.Add(Vector3.Lerp(raw[j], raw[(j + 1) % raw.Count], f));
                Dist.Add(d);
            }
            Length = total;
            for (int i = 0; i < Pts.Count; i++)
            {
                Vector3 d = Pts[(i + 1) % Pts.Count] - Pts[(i - 1 + Pts.Count) % Pts.Count];
                Dir3.Add(d.normalized);
                d.y = 0f;
                Dirs.Add(d.normalized);
            }
            foreach (var r in RampsNear) _ramps.Add(Nearest(Origin + r));
            foreach (var (a, b) in TunnelsNear)
            {
                int ia = Nearest(Origin + a), ib = Nearest(Origin + b);
                _tunnels.Add(ia <= ib ? (ia, ib) : (ib, ia));
            }
        }

        public float GroundY => Origin.y - Drop;

        /// <summary>Is a point above the track (not fallen onto the grass)? Uses the nearest centre-line height.</summary>
        public bool OnTrack(Vector3 p, int sample) => p.y > Pts[sample].y - 0.3f;

        public int Count => Pts.Count;

        /// <summary>Nearest sample to <paramref name="p"/>; searches around <paramref name="hint"/> when it is valid.</summary>
        public int Nearest(Vector3 p, int hint = -1)
        {
            int best = 0;
            float bd = float.MaxValue;
            if (hint >= 0)
            {
                for (int k = -30; k <= 30; k++)
                {
                    int i = ((hint + k) % Count + Count) % Count;
                    float d = (Pts[i] - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = i; }
                }
                if (bd < 9f) return best;
            }
            for (int i = 0; i < Count; i++)
            {
                float d = (Pts[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public int SegmentOf(int sample) => Mathf.Clamp(sample * Checkpoints / Count, 0, Checkpoints - 1);
        public int SampleAt(float fraction) => ((Mathf.RoundToInt(fraction * Count) % Count) + Count) % Count;
        public int CheckpointSample(int cp) => SampleAt((float)((cp % Checkpoints + Checkpoints) % Checkpoints) / Checkpoints);
        public Vector3 Side(int i) => Vector3.Cross(Vector3.up, Dirs[i]).normalized; // right-hand side

        /// <summary>Starting grid slot <paramref name="k"/>: two columns behind the start line.</summary>
        public void GridSlot(int k, out Vector3 pos, out float yaw)
        {
            int row = k / 2, col = k % 2;
            int i = ((-(4 + row * 3)) % Count + Count) % Count; // 1.6 m + 1.2 m per row behind the line
            pos = Pts[i] + Side(i) * (col == 0 ? -0.5f : 0.5f) + Vector3.up * 0.05f;
            yaw = Quaternion.LookRotation(Dirs[i]).eulerAngles.y;
        }

        /// <summary>0..3 red lights, 4 = all green.</summary>
        public void SetStartLights(int lit)
        {
            if (_startLights == null) return;
            for (int i = 0; i < _startLights.Length; i++)
            {
                var l = _startLights[i];
                if (l == null) continue;
                bool green = lit >= 4;
                l.color = green ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f);
                l.enabled = green || i < lit;
                var r = l.transform.parent.GetComponent<Renderer>();
                if (r != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_Color", l.enabled ? l.color : new Color(0.15f, 0.05f, 0.05f));
                    mpb.SetColor("_EmissionColor", l.enabled ? l.color * 2f : Color.black);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        // ================================================================ build

        private Material _mat;

        public void Build()
        {
            Root = new GameObject("RaceTrack").transform;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _mat = tmp.GetComponent<Renderer>().sharedMaterial; // Standard, always in the build
            Object.Destroy(tmp);

            // Ground (grass) below the raised track.
            var ground = Cube("Ground", Origin + new Vector3(0f, -Drop - 0.25f, 0f), new Vector3(115f, 0.5f, 76f), new Color(0.3f, 0.5f, 0.24f));
            // Night grass: unlit dark green (the garage scene has no sky light to light a lawn this big).
            var gr = ground.GetComponent<Renderer>();
            gr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "RaceGrass", mainTexture = GrassTexture(), mainTextureScale = new Vector2(25f, 18f) };
            var gm = new MaterialPropertyBlock();
            gm.SetColor("_Color", new Color(0.16f, 0.26f, 0.13f));
            gr.SetPropertyBlock(gm);

            BuildRibbon();
            BuildKerbs();
            BuildStart();
            foreach (int r in _ramps) BuildRamp(r);
            foreach (var (a, b) in _tunnels) BuildTunnel(a, b);
            BuildStand();
            BuildDecor();
            BuildLights();
        }

        private GameObject Cube(string n, Vector3 pos, Vector3 size, Color c, Quaternion? rot = null, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            if (!collider) Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(Root, false);
            g.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
            g.transform.localScale = size;
            Paint(g, c);
            return g;
        }

        private static void Paint(GameObject g, Color c)
        {
            var r = g.GetComponent<Renderer>();
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }

        private static void Tex(GameObject g, Texture2D t, Vector2 tiling)
        {
            var r = g.GetComponent<Renderer>();
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetTexture("_MainTex", t);
            mpb.SetVector("_MainTex_ST", new Vector4(tiling.x, tiling.y, 0f, 0f));
            r.SetPropertyBlock(mpb);
        }

        private GameObject MeshObject(string n, Mesh m, Color c, bool collider)
        {
            var g = new GameObject(n);
            g.transform.SetParent(Root, false);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = _mat;
            Paint(g, c);
            if (collider) g.AddComponent<MeshCollider>().sharedMesh = m;
            return g;
        }

        /// <summary>Asphalt top + the side walls down to the ground (so the edge reads as a drop).</summary>
        private void BuildRibbon()
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            int n = Count;
            float h = Width * 0.5f;
            for (int i = 0; i <= n; i++)
            {
                int k = i % n;
                Vector3 s = Side(k) * h;
                float u = i * Step / 4f;
                v.Add(Pts[k] - s); uv.Add(new Vector2(0f, u));
                v.Add(Pts[k] + s); uv.Add(new Vector2(1f, u));
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                tri.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            var top = new Mesh { name = "TrackTop" };
            top.SetVertices(v);
            top.SetUVs(0, uv);
            top.SetTriangles(tri, 0);
            top.RecalculateNormals();
            top.RecalculateBounds();
            var g = MeshObject("TrackTop", top, new Color(0.32f, 0.32f, 0.34f), true);
            Tex(g, AsphaltTexture(), Vector2.one);

            // Sides.
            var sv = new List<Vector3>();
            var st = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                int b0 = sv.Count;
                for (int i = 0; i <= n; i++)
                {
                    int k = i % n;
                    Vector3 e = Pts[k] + Side(k) * h * side;
                    sv.Add(e);
                    sv.Add(new Vector3(e.x, GroundY, e.z));
                }
                for (int i = 0; i < n; i++)
                {
                    int a = b0 + i * 2;
                    if (side > 0) st.AddRange(new[] { a, a + 2, a + 1, a + 2, a + 3, a + 1 });
                    else st.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
                }
            }
            var sides = new Mesh { name = "TrackSides" };
            sides.SetVertices(sv);
            sides.SetTriangles(st, 0);
            sides.RecalculateNormals();
            MeshObject("TrackSides", sides, new Color(0.22f, 0.22f, 0.24f), false);
        }

        private void BuildKerbs()
        {
            // Flat red / white blocks along both edges on the bends (where the direction changes).
            float h = Width * 0.5f;
            for (int i = 0; i < Count; i += 1)
            {
                float turn = Vector3.SignedAngle(Dirs[i], Dirs[(i + 6) % Count], Vector3.up);
                if (Mathf.Abs(turn) < 7f) continue;
                if (InTunnel(i)) continue;
                Color c = (i / 2) % 2 == 0 ? new Color(0.85f, 0.1f, 0.08f) : new Color(0.95f, 0.95f, 0.95f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 p = Pts[i] + Side(i) * (h - 0.12f) * s + Vector3.up * 0.012f;
                    Cube("Kerb", p, new Vector3(0.24f, 0.024f, Step + 0.01f), c, Quaternion.LookRotation(Dir3[i]), false);
                }
            }
        }

        private bool InTunnel(int i)
        {
            foreach (var (ia, ib) in _tunnels)
                if (i >= ia - 2 && i <= ib + 2) return true;
            return false;
        }

        private void BuildStart()
        {
            int i = 0;
            Quaternion rot = Quaternion.LookRotation(Dirs[i]);
            Vector3 p = Pts[i];
            Vector3 side = Side(i);
            // Chequered line.
            for (int x = 0; x < 10; x++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 c = p + side * (-Width * 0.5f + (x + 0.5f) * Width / 10f) + Dirs[i] * (z - 0.5f) * (Width / 10f) + Vector3.up * 0.006f;
                Cube("Check", c, new Vector3(Width / 10f, 0.01f, Width / 10f), (x + z) % 2 == 0 ? Color.white : new Color(0.05f, 0.05f, 0.05f), rot, false);
            }
            // Grid boxes.
            for (int k = 0; k < 8; k++)
            {
                GridSlot(k, out var gp, out _);
                Cube("GridMark", gp + Vector3.up * -0.04f + Dirs[i] * 0.3f, new Vector3(0.36f, 0.012f, 0.04f), Color.white, rot, false);
            }
            // Gantry: two posts, a beam with a banner and five start lights.
            float span = Width * 0.5f + 0.5f;
            var dark = new Color(0.15f, 0.16f, 0.18f);
            Cube("GantryL", p - side * span + Vector3.up * (1.2f - Drop) * 0.5f + Vector3.down * Drop * 0.5f, new Vector3(0.15f, 1.2f + Drop, 0.15f), dark, rot);
            Cube("GantryR", p + side * span + Vector3.up * (1.2f - Drop) * 0.5f + Vector3.down * Drop * 0.5f, new Vector3(0.15f, 1.2f + Drop, 0.15f), dark, rot);
            Cube("GantryBeam", p + Vector3.up * 1.25f, new Vector3(span * 2f + 0.2f, 0.32f, 0.12f), new Color(0.08f, 0.1f, 0.2f), rot, false);
            var label = new GameObject("GantryText").AddComponent<TextMesh>();
            label.transform.SetParent(Root, false);
            label.transform.SetPositionAndRotation(p + Vector3.up * 1.25f - Dirs[i] * 0.07f, rot);
            label.text = "START / META";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(label.font);
            label.fontSize = 48;
            label.characterSize = 0.035f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = Color.white;
            _startLights = new Light[4];
            for (int k = 0; k < 4; k++)
            {
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(bulb.GetComponent<Collider>());
                bulb.name = "StartLight";
                bulb.transform.SetParent(Root, false);
                bulb.transform.position = p + Vector3.up * 1.55f + side * (-0.45f + k * 0.3f);
                bulb.transform.localScale = Vector3.one * 0.14f;
                var l = new GameObject("Glow").AddComponent<Light>();
                l.transform.SetParent(bulb.transform, false);
                l.transform.localPosition = -Vector3.forward * 1.5f;
                l.type = LightType.Point;
                l.range = 3f;
                l.intensity = 2f;
                l.shadows = LightShadows.None;
                _startLights[k] = l;
            }
            SetStartLights(0);
        }

        /// <summary>Small jump: a wedge across the track rising to 0.2 m, then a drop.</summary>
        private void BuildRamp(int i)
        {
            const float len = 1.3f, hgt = 0.2f;
            Vector3 p = Pts[i];
            Quaternion rot = Quaternion.LookRotation(Dir3[i]);
            var m = new Mesh { name = "Ramp" };
            float w = Width * 0.5f - 0.15f;
            var v = new[]
            {
                new Vector3(-w, 0f, -len * 0.5f), new Vector3(w, 0f, -len * 0.5f), new Vector3(-w, 0f, len * 0.5f), new Vector3(w, 0f, len * 0.5f),
                new Vector3(-w, hgt, len * 0.5f), new Vector3(w, hgt, len * 0.5f),
            };
            m.vertices = v;
            m.triangles = new[] { 0, 4, 1, 1, 4, 5, /*slope*/ 2, 3, 4, 3, 5, 4, /*back*/ 0, 2, 4, 1, 5, 3 /*sides*/ };
            m.RecalculateNormals();
            var g = MeshObject("Ramp", m, new Color(0.95f, 0.75f, 0.1f), false);
            g.transform.SetPositionAndRotation(p + Vector3.up * 0.002f, rot);
            var mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
            mc.convex = true;
            // Black chevrons on the slope.
            for (int k = 0; k < 3; k++)
                Cube("Chevron", g.transform.TransformPoint(new Vector3(-0.6f + k * 0.6f, hgt * 0.45f + 0.01f, 0f)), new Vector3(0.25f, 0.01f, 0.5f),
                    new Color(0.05f, 0.05f, 0.05f), rot * Quaternion.Euler(-Mathf.Atan2(hgt, len) * Mathf.Rad2Deg, 45f, 0f), false);
        }

        /// <summary>Low concrete tunnel over the track between two samples (walls follow the bend), lit inside.</summary>
        private void BuildTunnel(int a, int b)
        {
            const float hgt = 0.72f, t = 0.12f;
            float h = Width * 0.5f + t * 0.5f;
            var concrete = new Color(0.55f, 0.55f, 0.56f);
            for (int i = a; i < b; i++)
            {
                Vector3 p = (Pts[i] + Pts[i + 1]) * 0.5f;
                Quaternion rot = Quaternion.LookRotation(Dir3[i]);
                float len = Vector3.Distance(Pts[i], Pts[i + 1]) + 0.04f;
                Cube("TunnelWallL", p - Side(i) * h + Vector3.up * hgt * 0.5f, new Vector3(t, hgt, len), concrete, rot);
                Cube("TunnelWallR", p + Side(i) * h + Vector3.up * hgt * 0.5f, new Vector3(t, hgt, len), concrete, rot);
                Cube("TunnelRoof", p + Vector3.up * (hgt + 0.05f), new Vector3(Width + t * 2f + 0.1f, 0.1f, len), concrete * 0.9f, rot);
                if ((i - a) % 4 == 0)
                {
                    var l = new GameObject("TunnelLamp").AddComponent<Light>();
                    l.transform.SetParent(Root, false);
                    l.transform.position = p + Vector3.up * (hgt - 0.08f);
                    l.type = LightType.Point;
                    l.range = 1.8f;
                    l.intensity = 1.6f;
                    l.color = new Color(1f, 0.85f, 0.55f);
                    l.shadows = LightShadows.None;
                    Cube("LampBox", p + Vector3.up * (hgt - 0.02f), new Vector3(0.3f, 0.03f, 0.08f), new Color(1f, 0.9f, 0.7f), rot, false);
                }
            }
            // Portals with a stripe.
            foreach (int i in new[] { a, b })
            {
                Quaternion rot = Quaternion.LookRotation(Dir3[i]);
                Cube("Portal", Pts[i] + Vector3.up * (hgt + 0.12f), new Vector3(Width + 0.5f, 0.14f, 0.16f), new Color(0.95f, 0.75f, 0.1f), rot, false);
            }
        }

        /// <summary>Grandstand on the outside of the start straight: drivers sit on the top deck facing the track.</summary>
        private void BuildStand()
        {
            float y0 = Origin.y - Drop;
            Vector3 c = Origin + new Vector3(8f, 0f, -28.6f);
            var steel = new Color(0.25f, 0.27f, 0.3f);
            Cube("StandDeck", new Vector3(c.x, y0 + 0.6f, c.z), new Vector3(22f, 1.2f, 2.6f), new Color(0.35f, 0.36f, 0.38f));
            Cube("StandStep", new Vector3(c.x, y0 + 0.3f, c.z + 1.7f), new Vector3(22f, 0.6f, 0.8f), new Color(0.4f, 0.41f, 0.43f));
            Cube("StandBack", new Vector3(c.x, y0 + 1.9f, c.z - 1.25f), new Vector3(22f, 1.4f, 0.12f), new Color(0.12f, 0.2f, 0.42f));
            Cube("StandRoof", new Vector3(c.x, y0 + 3.4f, c.z - 0.2f), new Vector3(22.6f, 0.12f, 3.4f), steel, null, false);
            for (int k = 0; k < 5; k++)
                Cube("StandPole", new Vector3(c.x - 10.8f + k * 5.4f, y0 + 2.3f, c.z - 1.2f), new Vector3(0.12f, 2.2f, 0.12f), steel, null, false);
            Cube("StandRail", new Vector3(c.x, y0 + 1.75f, c.z + 1.25f), new Vector3(22f, 0.06f, 0.06f), new Color(0.8f, 0.8f, 0.82f), null, false);
            var sign = new GameObject("StandText").AddComponent<TextMesh>();
            sign.transform.SetParent(Root, false);
            sign.transform.SetPositionAndRotation(new Vector3(c.x, y0 + 2.25f, c.z - 1.18f), Quaternion.Euler(0f, 180f, 0f)); // readable from the track
            sign.text = "RC GRAND PRIX";
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(sign.font);
            sign.fontSize = 64;
            sign.characterSize = 0.06f;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.color = new Color(1f, 0.85f, 0.2f);
            for (int k = 0; k < 12; k++) Seats.Add(new Vector3(c.x - 9.6f + k * 1.6f, y0 + 1.22f, c.z + 0.35f));
        }

        private void BuildDecor()
        {
            // Tyre stacks on the outside of the bends, on the grass.
            float h = Width * 0.5f;
            for (int i = 0; i < Count; i += 9)
            {
                float turn = Vector3.SignedAngle(Dirs[i], Dirs[(i + 8) % Count], Vector3.up);
                if (Mathf.Abs(turn) < 18f || InTunnel(i)) continue;
                Vector3 outward = Side(i) * (turn > 0f ? -1f : 1f);
                Vector3 b = Pts[i] + outward * (h + 1.2f);
                b.y = GroundY;
                for (int s = 0; s < 3; s++)
                {
                    var tyre = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tyre.name = "Tyre";
                    tyre.transform.SetParent(Root, false);
                    tyre.transform.position = b + Vector3.up * (0.09f + s * 0.18f);
                    tyre.transform.localScale = new Vector3(0.55f, 0.09f, 0.55f);
                    Paint(tyre, s == 1 ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.06f, 0.06f, 0.07f));
                }
            }
            // Cones at the start of each ramp.
            foreach (int i in _ramps)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.Destroy(cone.GetComponent<Collider>());
                    cone.transform.SetParent(Root, false);
                    cone.transform.position = Pts[(i - 3 + Count) % Count] + Side(i) * (h + 0.25f) * s + Vector3.up * 0.1f;
                    cone.transform.localScale = new Vector3(0.12f, 0.1f, 0.12f);
                    Paint(cone, new Color(1f, 0.45f, 0.05f));
                }
            }
        }

        private void BuildLights()
        {
            // Moonlight + floodlight masts with spots over the track.
            var sun = new GameObject("RaceMoon").AddComponent<Light>();
            sun.transform.SetParent(Root, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sun.intensity = 0.75f;
            sun.color = new Color(0.82f, 0.86f, 1f);
            sun.shadows = LightShadows.None; // the garage scene far away must not shadow the track
            foreach (var m in new[] { new Vector2(-30f, -31f), new Vector2(34f, -31f), new Vector2(52f, 2f), new Vector2(-53f, 2f), new Vector2(0f, 33f), new Vector2(0f, 0f) })
            {
                Vector3 b = Origin + new Vector3(m.x, 0f, m.y) * Scale + Vector3.down * Drop;
                Cube("Mast", b + Vector3.up * 4f, new Vector3(0.2f, 8f, 0.2f), new Color(0.3f, 0.3f, 0.32f));
                Cube("MastHead", b + Vector3.up * 8.1f, new Vector3(1.2f, 0.4f, 0.2f), new Color(0.95f, 0.95f, 0.9f), Quaternion.LookRotation(Origin - b), false);
                var l = new GameObject("Flood").AddComponent<Light>();
                l.transform.SetParent(Root, false);
                l.transform.position = b + Vector3.up * 8f;
                l.transform.rotation = Quaternion.LookRotation((Origin - l.transform.position).normalized);
                l.type = LightType.Spot;
                l.spotAngle = 85f;
                l.range = 60f;
                l.intensity = 2.2f;
                l.color = new Color(1f, 0.97f, 0.9f);
                l.shadows = LightShadows.None;
            }
        }

        private static Material _textMat;

        /// <summary>Depth-tested text material (the font's own one draws through walls).</summary>
        private static Material TextMaterial(Font f)
        {
            if (_textMat != null) return _textMat;
            var sh = Resources.Load<Shader>("Shaders/HnsSecurityText");
            _textMat = sh != null ? new Material(sh) { mainTexture = f.material.mainTexture } : f.material;
            return _textMat;
        }

        // ================================================================ textures

        private static Texture2D AsphaltTexture()
        {
            const int W = 64, H = 64;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var px = new Color32[W * H];
            var rnd = new System.Random(5);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float n = 0.75f + 0.25f * (float)rnd.NextDouble();
                bool edge = x < 3 || x >= W - 3;
                bool dash = x >= W / 2 - 1 && x <= W / 2 && (y / 8) % 2 == 0;
                float v = edge || dash ? 0.95f : 0.42f * n;
                byte b = (byte)(Mathf.Clamp01(v) * 255f);
                px[y * W + x] = new Color32(b, b, (byte)Mathf.Min(255, b + 4), 255);
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        private static Texture2D GrassTexture()
        {
            const int S = 32;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[S * S];
            var rnd = new System.Random(9);
            for (int i = 0; i < px.Length; i++)
            {
                float n = 0.7f + 0.6f * (float)rnd.NextDouble();
                px[i] = new Color32((byte)(110 * n), (byte)(170 * n), (byte)(90 * n), 255);
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }
    }
}
