using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Jet and cloud of one extinguisher: a dense white cone of powder out of the nozzle (fast, slowing down, stopped by
    /// walls), feeding big soft puffs along the jet that hang in the air for several seconds, drift and spread - they
    /// really hide what is behind them (<see cref="Density"/> also drives the screen haze of a player standing in it).
    /// The 3D hiss follows the nozzle; the nozzle trembles (driven by the hands). When the tank runs dry the jet
    /// coughs a few last puffs (<see cref="Sputter"/>). Lit particles (HnsPowderCloud): dark in the dark garage, white
    /// in a flashlight beam.
    /// </summary>
    public class ExtinguisherSpray : MonoBehaviour
    {
        private struct Puff { public Vector3 Pos; public float Born, Life, Radius; }
        private static readonly List<Puff> Puffs = new();
        private static Material _mat;
        private static Texture2D _tex;

        private ParticleSystem _jet, _cloud;
        private AudioSource _hiss;
        private float _on, _nextPuff, _sputterUntil, _vol;
        private Vector3 _tip, _dir = Vector3.forward;
        private bool _spraying;

        public static ExtinguisherSpray Create(Transform parent)
        {
            var go = new GameObject("ExtSpray");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<ExtinguisherSpray>();
            s.Build();
            return s;
        }

        /// <summary>Every frame: spraying now, nozzle tip and direction (world).</summary>
        public void Drive(bool spraying, Vector3 tip, Vector3 dir)
        {
            _spraying = spraying;
            if (spraying || Time.time < _sputterUntil) { _tip = tip; _dir = dir.normalized; }
        }

        /// <summary>The tank ran dry while spraying: a few weak last puffs.</summary>
        public void Sputter()
        {
            _sputterUntil = Time.time + 1.2f;
            ExtinguisherSounds.Sputter(_tip);
        }

        /// <summary>0..~1 cloud density at a point (sum of the puffs around it).</summary>
        public static float Density(Vector3 p)
        {
            float d = 0f, now = Time.time;
            for (int i = Puffs.Count - 1; i >= 0; i--)
            {
                var f = Puffs[i];
                float age = now - f.Born;
                if (age > f.Life) { Puffs.RemoveAt(i); continue; }
                float r = f.Radius * (0.6f + 0.8f * Mathf.Sqrt(age / f.Life));
                float dist = Vector3.Distance(p, f.Pos + Vector3.up * 0.05f * age);
                if (dist > r) continue;
                float fade = Mathf.Clamp01(age / 0.4f) * (1f - Mathf.Clamp01((age - f.Life * 0.55f) / (f.Life * 0.45f)));
                d += (1f - dist / r) * fade * 0.55f;
            }
            return d;
        }

        private void Build()
        {
            _mat ??= MakeMaterial();
            _jet = MakeSystem("Jet", 600);
            var main = _jet.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6.5f, 9.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 0.98f, 0.75f), new Color(0.92f, 0.93f, 0.95f, 0.55f));
            main.gravityModifier = 0.05f;
            var em = _jet.emission;
            em.rateOverTime = 0f;
            var limit = _jet.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.6f;
            limit.dampen = 0.06f;
            var sz = _jet.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 3.5f), new Keyframe(1f, 6.5f)));
            var col = _jet.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.9f, 0.35f);
            var coll = _jet.collision;
            coll.enabled = true;
            coll.type = ParticleSystemCollisionType.World;
            coll.quality = ParticleSystemCollisionQuality.Low;
            coll.dampen = 0.85f;
            coll.bounce = 0.05f;
            coll.collidesWith = ~((1 << 8) | (1 << 9));
            coll.radiusScale = 0.3f;

            _cloud = MakeSystem("Cloud", 400);
            main = _cloud.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.96f, 0.96f, 0.95f, 0.5f), new Color(0.88f, 0.89f, 0.9f, 0.38f));
            main.gravityModifier = -0.004f;
            em = _cloud.emission;
            em.rateOverTime = 0f;
            sz = _cloud.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1.2f), new Keyframe(1f, 2.3f)));
            col = _cloud.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.08f, 0.5f);
            var rot = _cloud.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            var noise = _cloud.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.1f;
            var vel = _cloud.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(0.0f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);

            _hiss = ExtinguisherSounds.HissSource(transform);
        }

        private ParticleSystem MakeSystem(string name, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _mat;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0f;
            r.maxParticleSize = 3f;
            ps.Play();
            return ps;
        }

        private static ParticleSystem.MinMaxGradient Fade(float inFrac, float hold)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, Mathf.Min(inFrac, 0.3f)), new GradientAlphaKey(0.85f, hold), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool sputter = Time.time < _sputterUntil;
            float want = _spraying ? 1f : 0f;
            if (sputter) want = Mathf.PerlinNoise(Time.time * 9f, 3.1f) > 0.55f ? 0.35f * (_sputterUntil - Time.time) : 0f;
            _on = Mathf.MoveTowards(_on, want, dt / (want > _on ? 0.06f : 0.12f));
            if (_on > 0.01f) EmitJet(dt);

            // Hiss: own source at the nozzle, quieter on other floors.
            if (_hiss != null)
            {
                _hiss.transform.position = _tip;
                _vol = Mathf.MoveTowards(_vol, _spraying ? 1f : 0f, dt / (_spraying ? 0.05f : 0.15f));
                float floorGain = 1f;
                var al = ActiveListener.Find();
                if (al != null && FloorAcoustics.ListenerFloor(al.transform.position.y) != FloorAcoustics.SourceFloor(_tip.y - 1f)) floorGain = 0.15f;
                _hiss.volume = _vol * 0.75f * floorGain * GameAudio.SfxVolume;
                _hiss.pitch = 1f + 0.04f * Mathf.Sin(Time.time * 3f);
                if (_vol > 0.001f && !_hiss.isPlaying) { _hiss.time = Random.Range(0f, 1.8f); _hiss.Play(); }
                else if (_vol <= 0.001f && _hiss.isPlaying) _hiss.Stop();
            }
        }

        private void EmitJet(float dt)
        {
            int n = Mathf.RoundToInt(420f * _on * dt + Random.value);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.AngleAxis(Random.Range(0f, 360f), _dir) * Quaternion.AngleAxis(Random.Range(0f, 7f), Perp(_dir)) * _dir;
                ep.position = _tip + d * Random.Range(0f, 0.05f);
                ep.velocity = d * Random.Range(6.5f, 9.5f) * Mathf.Lerp(0.4f, 1f, _on);
                ep.applyShapeToPosition = false;
                _jet.Emit(ep, 1);
            }
            // Cloud puffs along the jet up to the first obstacle.
            if (Time.time < _nextPuff) return;
            _nextPuff = Time.time + 0.055f / Mathf.Max(0.3f, _on);
            float reach = 4.2f;
            if (Physics.Raycast(_tip, _dir, out var hit, reach, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) reach = hit.distance;
            float along = Mathf.Lerp(0.5f, reach, Mathf.Pow(Random.value, 0.7f));
            var cp = new ParticleSystem.EmitParams
            {
                position = _tip + _dir * along + Random.insideUnitSphere * (0.1f + along * 0.12f),
                velocity = _dir * Random.Range(0.2f, 0.6f) + Random.insideUnitSphere * 0.12f,
                applyShapeToPosition = false,
            };
            _cloud.Emit(cp, 1);
            Puffs.Add(new Puff { Pos = cp.position, Born = Time.time, Life = 6f, Radius = 0.9f + along * 0.15f });
            if (Puffs.Count > 600) Puffs.RemoveAt(0);
        }

        private static Vector3 Perp(Vector3 d) => Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;

        private static Material MakeMaterial()
        {
            var sh = Resources.Load<Shader>("Shaders/HnsPowderCloud");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = "ExtPowder", mainTexture = PuffTexture() };
            m.SetColor("_Color", Color.white);
            return m;
        }

        /// <summary>Soft round puff with a cauliflower edge and a little inner structure.</summary>
        public static Texture2D PuffTexture()
        {
            if (_tex != null) return _tex;
            const int S = 64;
            _tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "ExtPuff", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                float a = Mathf.Atan2(v, u);
                float r = Mathf.Sqrt(u * u + v * v) / (0.84f + 0.05f * Mathf.Sin(a * 3f) + 0.03f * Mathf.Sin(a * 7f + 1f));
                float n = Mathf.PerlinNoise(x * 0.13f, y * 0.13f) * 0.5f + Mathf.PerlinNoise(x * 0.31f + 7f, y * 0.31f) * 0.5f;
                float al = Mathf.Clamp01(1f - r);
                al = al * al * (3f - 2f * al) * (0.65f + 0.35f * n);
                float shade = 0.9f + 0.1f * n;
                px[y * S + x] = new Color(shade, shade, shade, al);
            }
            _tex.SetPixels32(px);
            _tex.Apply(true, true);
            return _tex;
        }
    }
}
