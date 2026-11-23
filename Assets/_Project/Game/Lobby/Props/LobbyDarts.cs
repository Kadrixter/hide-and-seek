using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Dartboard on the east wall: E at the throw line throws a dart (the server rolls where it lands, everybody sees
    /// the same flight). Three darts per round, then they are pulled out. No text at the board (the score is only
    /// logged: single / double / treble, 25 / 50, like a real board).
    /// Board space: local +Z = into the room, the face at <see cref="FaceZ"/>; the viewer's right is local -X.
    /// </summary>
    public class LobbyDarts : MonoBehaviour
    {
        public const float FaceZ = 0.045f, BoardR = 0.225f;
        // regulation radii (m)
        private const float Bull = 0.00635f, OuterBull = 0.0159f, TrebleIn = 0.099f, TrebleOut = 0.107f, DoubleIn = 0.162f, DoubleOut = 0.170f;
        private static readonly int[] Numbers = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };

        public static Vector3 BoardCentre => LobbyLayout.Dartboard;
        public const float ThrowYaw = 90f;
        public static Vector3 ThrowLine => new(LobbyLayout.Dartboard.x - 2.37f, 0f, LobbyLayout.Dartboard.z);

        public static bool CanThrowFrom(Vector3 pos)
        {
            Vector3 l = ThrowLine;
            return pos.x < l.x + 0.4f && pos.x > l.x - 1.6f && Mathf.Abs(pos.z - l.z) < 1.0f;
        }

        /// <summary>Where the thrower is placed: right behind the line, centred on the board.</summary>
        public static Vector3 ThrowSpot(Vector3 from) => ThrowLine + new Vector3(-0.3f, 0.02f, Mathf.Clamp(from.z - ThrowLine.z, -0.15f, 0.15f));

        /// <summary>Server: random hit (aimed at the middle, ~7 cm spread), packed as radius byte | angle byte &lt;&lt; 8.</summary>
        public static int RollHit(System.Random rnd)
        {
            double u1 = 1.0 - rnd.NextDouble(), u2 = rnd.NextDouble();
            double g = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
            float r = Mathf.Min(0.215f, Mathf.Abs((float)g) * 0.07f);
            int rq = Mathf.Clamp(Mathf.RoundToInt(r / 0.22f * 255f), 0, 255);
            int aq = rnd.Next(256);
            return rq | aq << 8;
        }

        public static void Unpack(int arg, out float r, out float angleDeg)
        {
            r = (arg & 0xFF) / 255f * 0.22f;
            angleDeg = ((arg >> 8) & 0xFF) / 256f * 360f;
        }

        /// <summary>Points and label ("T20", "D5", "50", "-") for a hit at radius r, angle clockwise from the top.</summary>
        public static int Score(float r, float angleDeg, out string label)
        {
            if (r <= Bull) { label = "50"; return 50; }
            if (r <= OuterBull) { label = "25"; return 25; }
            if (r > DoubleOut) { label = "pudło"; return 0; }
            int seg = Mathf.FloorToInt(Mathf.Repeat(angleDeg + 9f, 360f) / 18f) % 20;
            int n = Numbers[seg];
            if (r >= TrebleIn && r <= TrebleOut) { label = "T" + n; return n * 3; }
            if (r >= DoubleIn) { label = "D" + n; return n * 2; }
            label = n.ToString();
            return n;
        }

        // ================================================================ visuals

        private readonly List<Transform> _stuck = new();
        private int _round, _total;
        private float _clearAt = -1f;

        private struct Flight { public Transform Dart; public Vector3 From, To; public float T0, Dur; public string Text; public int Points; public bool Last; }
        private readonly List<Flight> _flights = new();
        private Material _barrel, _flightMat, _shaft;

        private void Start()
        {
            var face = transform.Find("Face");
            if (face != null) face.GetComponent<Renderer>().material.mainTexture = DartboardArt.Make();
            _barrel = new Material(Shader.Find("Standard")) { color = new Color(0.75f, 0.75f, 0.78f) };
            _barrel.SetFloat("_Metallic", 0.9f);
            _barrel.SetFloat("_Glossiness", 0.7f);
            _shaft = new Material(Shader.Find("Standard")) { color = new Color(0.08f, 0.08f, 0.09f) };
            _flightMat = new Material(Shader.Find("Standard")) { color = new Color(0.85f, 0.12f, 0.1f) };
            LobbyPropDriver.Used += OnUsed;
        }

        private void OnDestroy() => LobbyPropDriver.Used -= OnUsed;

        private void OnUsed(LobbyPropKind kind, ulong user, int arg)
        {
            if (kind != LobbyPropKind.Darts) return;
            if (_round >= 3 || _clearAt > 0f) ClearDarts();
            Unpack(arg, out float r, out float a);
            int pts = Score(r, a, out string text);
            float rad = a * Mathf.Deg2Rad;
            Vector3 hitLocal = new Vector3(-Mathf.Sin(rad) * r, Mathf.Cos(rad) * r, FaceZ);
            var np = NetPlayer.Find(user);
            Vector3 from = np != null ? np.transform.position + Vector3.up * 1.75f + np.transform.right * 0.18f + np.transform.forward * 0.3f
                : transform.TransformPoint(new Vector3(0f, 0.1f, 2.3f));
            var dart = MakeDart();
            dart.gameObject.SetActive(false);
            _round++;
            _total += pts;
            _flights.Add(new Flight { Dart = dart, From = from, To = transform.TransformPoint(hitLocal), T0 = Time.time + 0.42f, Dur = 0.3f, Text = text, Points = pts, Last = _round >= 3 });
        }

        private Transform MakeDart()
        {
            var d = new GameObject("Dart").transform;
            d.SetParent(transform, false);
            void Part(PrimitiveType t, Vector3 lp, Vector3 s, Material m, Quaternion? r = null)
            {
                var g = GameObject.CreatePrimitive(t);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(d, false);
                g.transform.localPosition = lp;
                g.transform.localRotation = r ?? Quaternion.Euler(90f, 0f, 0f);
                g.transform.localScale = s;
                g.GetComponent<Renderer>().sharedMaterial = m;
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // local +Z = the tip direction; origin at the tip
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.012f), new Vector3(0.002f, 0.012f, 0.002f), _barrel);          // point
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.045f), new Vector3(0.0075f, 0.022f, 0.0075f), _barrel);       // barrel
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.09f), new Vector3(0.004f, 0.025f, 0.004f), _shaft);           // shaft
            Part(PrimitiveType.Cube, new Vector3(0f, 0f, -0.115f), new Vector3(0.034f, 0.0008f, 0.035f), _flightMat, Quaternion.identity);
            Part(PrimitiveType.Cube, new Vector3(0f, 0f, -0.115f), new Vector3(0.0008f, 0.034f, 0.035f), _flightMat, Quaternion.identity);
            return d;
        }

        private void ClearDarts()
        {
            foreach (var t in _stuck) if (t != null) Destroy(t.gameObject);
            _stuck.Clear();
            _round = 0;
            _total = 0;
            _clearAt = -1f;
        }

        private void Update()
        {
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                var f = _flights[i];
                float u = (Time.time - f.T0) / f.Dur;
                if (u < 0f) continue;
                f.Dart.gameObject.SetActive(true);
                if (u >= 1f)
                {
                    // stuck, slightly drooping
                    Vector3 dir = (f.To - f.From).normalized;
                    f.Dart.SetPositionAndRotation(f.To, Quaternion.LookRotation(Quaternion.AngleAxis(6f, Vector3.Cross(dir, Vector3.up)) * dir));
                    _stuck.Add(f.Dart);
                    _flights.RemoveAt(i);
                    LobbyPropSounds.Play(LobbyPropSounds.Id.Thunk, f.To, 0.55f);
                    Debug.Log($"[Darts] {f.Text} ({f.Points}), round total {_total}");
                    if (f.Last) _clearAt = Time.time + 3.5f;
                    continue;
                }
                Vector3 p = Vector3.Lerp(f.From, f.To, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.12f;
                Vector3 vel = (f.To - f.From) + Vector3.up * Mathf.Cos(u * Mathf.PI) * 0.12f * Mathf.PI;
                f.Dart.SetPositionAndRotation(p, Quaternion.LookRotation(vel.normalized) * Quaternion.Euler(0f, 0f, u * 400f));
            }
            if (_clearAt > 0f && Time.time > _clearAt) ClearDarts();
        }
    }
}
