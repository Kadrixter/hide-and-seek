using System.Collections;
using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lightning on an energised RC car: jagged blue-white arcs that jump over the body and down to the floor, each
    /// re-struck many times a second (random zigzag, flickering gaps), a pale blue glow under the car and the
    /// buzzing of the current. Also the arcs crawling over an electrocuted player and the bolt into him.
    /// </summary>
    public class RcChargeFx : MonoBehaviour
    {
        private static readonly Color GlowCol = new Color(0.35f, 0.62f, 1f, 0.45f);
        private static readonly Color CoreCol = new Color(0.86f, 0.94f, 1f, 1f);
        private static readonly Color LightCol = new Color(0.5f, 0.72f, 1f);
        // Car body box (Parts.Root local): the arcs start and end on it.
        private static readonly Vector3 BodyMin = new Vector3(-0.12f, 0.04f, -0.24f), BodyMax = new Vector3(0.12f, 0.15f, 0.24f);

        private Arcs _body, _ground;
        private Light _light;
        private AudioSource _buzz;
        private RcCar _car;
        private bool _on;

        /// <summary>Every frame for each car: <paramref name="on"/> = charged and on the floor, <paramref name="elapsed"/> = s since charged.</summary>
        public static void Set(RcCar car, bool on, float elapsed)
        {
            if (car == null) return;
            var fx = car.GetComponent<RcChargeFx>();
            if (!on)
            {
                if (fx != null && fx._on) fx.Off();
                return;
            }
            if (fx == null)
            {
                fx = car.gameObject.AddComponent<RcChargeFx>();
                fx._car = car;
            }
            fx.Tick(elapsed);
        }

        private void Build()
        {
            var root = _car.Parts.Root;
            _body = new Arcs(root, 7, 0.018f, 0.006f);
            _ground = new Arcs(root, 4, 0.022f, 0.007f);
            var lg = new GameObject("RcChargeGlow");
            lg.layer = gameObject.layer;
            lg.transform.SetParent(root, false);
            lg.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = LightCol;
            _light.range = 1.8f;
            _light.shadows = LightShadows.None;
        }

        private void Tick(float elapsed)
        {
            if (_body == null) Build();
            if (!_on)
            {
                _on = true;
                GameAudio.PlayAt(HideAndSeek.Audio.GameSfx.ForkSparks, transform.position + Vector3.up * 0.1f, 0.6f);
                var clip = RcChargeSounds.Buzz;
                _buzz = GameAudio.PlayClipAttached(clip, transform, 0.9f, 22f);
                if (_buzz != null && elapsed > 0.1f && elapsed < clip.length - 0.1f) _buzz.time = elapsed;
            }
            float now = Time.time;
            // Body arcs: corner to corner over the shell; floor arcs: from the sill down to a spot on the ground.
            _body.Update(now, (out Vector3 a, out Vector3 b) =>
            {
                // Crawls over one face of the shell (a straight line between two faces would cut through the body).
                a = RandomOnBody();
                b = a + new Vector3(Random.Range(-0.14f, 0.14f), Random.Range(-0.07f, 0.07f), Random.Range(-0.2f, 0.2f));
                b = Vector3.Max(BodyMin, Vector3.Min(BodyMax, b));
                if (Mathf.Abs(Mathf.Abs(a.x) - BodyMax.x) < 1e-4f) b.x = a.x * 1.05f;
                else b.z = a.z * 1.05f;
                a = new Vector3(a.x * 1.05f, a.y, a.z * 1.05f);
            }, 0.28f);
            _ground.Update(now, (out Vector3 a, out Vector3 b) =>
            {
                a = RandomOnBody();
                a.y = BodyMin.y + 0.01f;
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(0.18f, 0.42f);
                Vector3 wp = _car.Parts.Root.TransformPoint(new Vector3(a.x + c.x, 0.25f, a.z + c.y));
                b = Physics.Raycast(wp, Vector3.down, out var hit, 0.8f, ~(1 << RcCar.CarLayer), QueryTriggerInteraction.Ignore)
                    ? _car.Parts.Root.InverseTransformPoint(hit.point + Vector3.up * 0.005f)
                    : new Vector3(a.x + c.x, -0.01f, a.z + c.y);
            }, 0.3f);
            float spike = Random.value < 0.12f ? 1.6f : 1f;
            _light.enabled = true;
            _light.intensity = (0.9f + 1.4f * Mathf.PerlinNoise(now * 17f, _car.Index)) * spike;
        }

        private static Vector3 RandomOnBody()
        {
            var p = new Vector3(Random.Range(BodyMin.x, BodyMax.x), Random.Range(BodyMin.y, BodyMax.y), Random.Range(BodyMin.z, BodyMax.z));
            // On a side / the nose / the tail, not over the roof: the driver's camera sits on the roof and an arc right
            // in front of the lens would fill the screen.
            if (Random.value < 0.6f) p.x = Random.value < 0.5f ? BodyMin.x : BodyMax.x;
            else p.z = Random.value < 0.5f ? BodyMin.z : BodyMax.z;
            return p;
        }

        private void Off()
        {
            _on = false;
            _body?.Hide();
            _ground?.Hide();
            if (_light != null) _light.enabled = false;
            if (_buzz != null && _buzz.clip == RcChargeSounds.Buzz) GameAudio.StopLoop(_buzz, 0.15f);
            _buzz = null;
        }

        private void OnDisable()
        {
            if (_on) Off();
        }

        private void OnDestroy()
        {
            if (_on) Off();
        }

        // ================================================================ arcs on an electrocuted player / bolt

        /// <summary>Arcs crawling over a player's body for <paramref name="duration"/> s (every peer).</summary>
        public static void ZapBody(Transform body, float duration)
        {
            if (body != null) FxUtil.Runner.StartCoroutine(ZapRoutine(body, duration));
        }

        private static IEnumerator ZapRoutine(Transform body, float duration)
        {
            var holder = new GameObject("ZapArcs");
            holder.transform.SetParent(body, false);
            var arcs = new Arcs(holder.transform, 6, 0.03f, 0.009f);
            var lg = new GameObject("ZapGlow");
            lg.transform.SetParent(holder.transform, false);
            lg.transform.localPosition = new Vector3(0f, 1.0f, 0.1f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = LightCol;
            l.range = 3.2f;
            l.shadows = LightShadows.None;
            float t = 0f;
            while (t < duration && holder != null)
            {
                float fade = 1f - Mathf.Clamp01((t - (duration - 0.35f)) / 0.35f);
                arcs.Update(Time.time, (out Vector3 a, out Vector3 b) =>
                {
                    // On the skin (front / back / sides of the blocky body), crawling up and down it.
                    float zs = Random.value < 0.5f ? 0.19f : -0.19f;
                    if (Random.value < 0.65f)
                    {
                        a = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 1.7f), zs);
                        b = new Vector3(Mathf.Clamp(a.x + Random.Range(-0.3f, 0.3f), -0.32f, 0.32f), a.y + Random.Range(-0.6f, 0.6f), zs);
                    }
                    else
                    {
                        float xs = Random.value < 0.5f ? 0.34f : -0.34f;
                        a = new Vector3(xs, Random.Range(0.1f, 1.6f), Random.Range(-0.19f, 0.19f));
                        b = new Vector3(xs, a.y + Random.Range(-0.6f, 0.6f), Random.Range(-0.19f, 0.19f));
                    }
                    b.y = Mathf.Clamp(b.y, 0f, 1.75f);
                }, 0.22f, fade);
                l.intensity = (1.2f + 2.4f * Random.value) * fade;
                t += Time.deltaTime;
                yield return null;
            }
            if (holder != null) Destroy(holder);
        }

        /// <summary>One big bolt from <paramref name="from"/> to <paramref name="to"/> (world), struck a few times.</summary>
        public static void Bolt(Vector3 from, Vector3 to) => FxUtil.Runner.StartCoroutine(BoltRoutine(from, to));

        private static IEnumerator BoltRoutine(Vector3 from, Vector3 to)
        {
            var holder = new GameObject("ZapBolt");
            var arcs = new Arcs(holder.transform, 2, 0.04f, 0.012f);
            float t = 0f;
            while (t < 0.4f && holder != null)
            {
                arcs.Update(Time.time, (out Vector3 a, out Vector3 b) => { a = from; b = to; }, 0.12f, 1f - t / 0.4f, 0.5f);
                t += Time.deltaTime;
                yield return null;
            }
            if (holder != null) Destroy(holder);
        }

        // ================================================================ a set of flickering zigzag arcs

        private delegate void Ends(out Vector3 a, out Vector3 b);

        private sealed class Arcs
        {
            private readonly LineRenderer[] _glow, _core;
            private readonly float[] _next;
            private readonly Vector3[] _pts = new Vector3[10];
            private readonly Vector3[][] _byLen = new Vector3[11][];

            public Arcs(Transform parent, int count, float glowWidth, float coreWidth)
            {
                _glow = new LineRenderer[count];
                _core = new LineRenderer[count];
                _next = new float[count];
                for (int i = 0; i < count; i++)
                {
                    _glow[i] = Line(parent, "ArcGlow", glowWidth, GlowCol);
                    _core[i] = Line(parent, "ArcCore", coreWidth, CoreCol);
                }
            }

            private static LineRenderer Line(Transform parent, string name, float width, Color c)
            {
                var go = new GameObject(name);
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.sharedMaterial = FxUtil.Unlit;
                lr.widthMultiplier = width;
                lr.startColor = lr.endColor = c;
                lr.numCapVertices = 1;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                return lr;
            }

            /// <summary>Re-strikes every arc whose time is up (new ends, new zigzag, sometimes a gap).</summary>
            public void Update(float now, Ends ends, float jag, float alpha = 1f, float gapChance = 0.3f)
            {
                for (int i = 0; i < _glow.Length; i++)
                {
                    var g = _glow[i];
                    var c = _core[i];
                    if (g == null || c == null) continue;
                    if (now >= _next[i])
                    {
                        _next[i] = now + Random.Range(0.035f, 0.1f);
                        bool show = Random.value >= gapChance;
                        g.enabled = c.enabled = show;
                        if (show)
                        {
                            ends(out var a, out var b);
                            int n = Zigzag(a, b, jag);
                            var pts = _byLen[n] ??= new Vector3[n];
                            System.Array.Copy(_pts, pts, n);
                            g.positionCount = c.positionCount = n;
                            g.SetPositions(pts);
                            c.SetPositions(pts);
                        }
                    }
                    var gc = GlowCol;
                    gc.a *= alpha * Random.Range(0.6f, 1f);
                    g.startColor = g.endColor = gc;
                    var cc = CoreCol;
                    cc.a *= alpha;
                    c.startColor = c.endColor = cc;
                }
            }

            private int Zigzag(Vector3 a, Vector3 b, float jag)
            {
                float len = Vector3.Distance(a, b);
                int n = Mathf.Clamp(3 + Mathf.RoundToInt(len / 0.05f), 4, _pts.Length);
                Vector3 dir = (b - a).normalized;
                Vector3 s1 = Vector3.Cross(dir, Vector3.up);
                if (s1.sqrMagnitude < 1e-4f) s1 = Vector3.Cross(dir, Vector3.right);
                s1.Normalize();
                Vector3 s2 = Vector3.Cross(dir, s1);
                float amp = Mathf.Min(0.12f, len * jag);
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)(n - 1);
                    float env = Mathf.Sin(t * Mathf.PI);
                    Vector2 o = Random.insideUnitCircle * amp * env;
                    _pts[k] = Vector3.Lerp(a, b, t) + s1 * o.x + s2 * o.y;
                }
                return n;
            }

            public void Hide()
            {
                for (int i = 0; i < _glow.Length; i++)
                {
                    if (_glow[i] != null) _glow[i].enabled = false;
                    if (_core[i] != null) _core[i].enabled = false;
                }
            }
        }
    }
}
