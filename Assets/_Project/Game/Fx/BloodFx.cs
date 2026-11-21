using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Blood: sprays of stretched droplets that splat where they land (floor, walls, ceiling), pools that slowly spread,
    /// runs trickling down walls, arterial spurts from stumps, drips from cut-off limbs and wounded players.
    /// Everything is drawn with the lit, glossy <c>HideAndSeek/Blood</c> shader (procedural splat textures with normal
    /// maps), so blood shines under a flashlight and dries darker over time. Pools are remembered: stepping into one
    /// leaves bloody footprints (<see cref="BloodTracks"/>).
    /// </summary>
    public static class BloodFx
    {
        public static readonly Color Fresh = new Color(0.42f, 0.015f, 0.02f, 1f);
        private static readonly Color Drop = new Color(0.5f, 0.02f, 0.025f, 1f);
        private static readonly Color DropDark = new Color(0.24f, 0.0f, 0.01f, 1f);

        /// <summary>Layers blood may land on: no RC cars / ignore-raycast, local body, triggers layer 9.</summary>
        public static readonly int SurfaceMask = ~((1 << 2) | (1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9));

        // ================================================================ public API

        /// <summary>Burst of blood from a wound towards <paramref name="direction"/>; <paramref name="amount"/> 1 = a stab.</summary>
        public static void Spray(Vector3 position, Vector3 direction, float amount = 1f)
        {
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            direction.Normalize();
            amount = Mathf.Clamp(amount, 0.2f, 3f);
            Droplets(position, direction, Mathf.RoundToInt(34 * amount), 1.4f, 4.6f, 26f, Mathf.RoundToInt(4 * amount + 1));
            Mist(position, direction, Mathf.RoundToInt(14 * amount));

            // Pool on the floor under / a little in front of the wound, and a splash on a wall right behind the victim.
            if (SurfaceRay(position, Vector3.down, 3f, out var floor))
                Pool(floor.point + Flat(direction) * Random.Range(0.1f, 0.45f), Random.Range(0.3f, 0.55f) * Mathf.Sqrt(amount), Random.Range(3f, 6f));
            if (amount >= 0.8f && Random.value < 0.75f) WallSplash(position, direction, 2.4f, 0.45f * Mathf.Sqrt(amount));
        }

        /// <summary>Flat blood pool on the floor below <paramref name="position"/>, spreading out over a few seconds.</summary>
        public static void Decal(Vector3 position, float size = 0.35f, float life = 240f)
            => Pool(position, size, Random.Range(2.5f, 5f), life);

        /// <summary>Pool that grows from a small puddle to <paramref name="size"/> metres over <paramref name="spreadTime"/>.</summary>
        public static BloodDecal Pool(Vector3 position, float size, float spreadTime, float life = 240f)
        {
            if (SurfaceRay(position + Vector3.up * 0.5f, Vector3.down, 2.2f, out var hit))
                position = hit.point;
            else return null;
            if (hit.normal.y < 0.6f) return null;
            var d = Spawn(Kind.Splat, position, hit.normal, size * Random.Range(0.85f, 1.2f), life);
            d.Spread(Random.Range(0.18f, 0.32f), spreadTime);
            d.IsPool = true;
            return d;
        }

        /// <summary>Single small drop on the floor (dripping wound).</summary>
        public static void DropAt(Vector3 position, float size = 0.06f)
        {
            if (!SurfaceRay(position + Vector3.up * 0.4f, Vector3.down, 2f, out var hit)) return;
            var d = Spawn(Kind.Drop, hit.point, hit.normal, size * Random.Range(0.7f, 1.4f), 180f);
            d.Spread(0.6f, 0.4f);
        }

        /// <summary>Big splash on the nearest wall along <paramref name="direction"/> (with runs trickling down).</summary>
        public static void WallSplash(Vector3 from, Vector3 direction, float maxDist, float size)
        {
            Vector3 dir = (Flat(direction) + Random.insideUnitSphere * 0.35f + Vector3.up * Random.Range(-0.1f, 0.25f)).normalized;
            if (!SurfaceRay(from, dir, maxDist, out var hit) || Mathf.Abs(hit.normal.y) > 0.5f) return;
            float s = size * Mathf.Lerp(1.15f, 0.55f, hit.distance / maxDist);
            var d = Spawn(Kind.Splat, hit.point, hit.normal, s, 240f);
            d.Spread(0.55f, 0.25f);
            int runs = Random.Range(1, 4);
            for (int i = 0; i < runs; i++)
            {
                Vector3 side = Vector3.Cross(hit.normal, Vector3.up).normalized;
                Run(hit.point + side * Random.Range(-s * 0.3f, s * 0.3f) + Vector3.down * Random.Range(0f, s * 0.15f), hit.normal,
                    Random.Range(0.18f, 0.7f), Random.Range(4f, 11f));
            }
        }

        /// <summary>Thin run of blood trickling down a wall from <paramref name="top"/>.</summary>
        public static void Run(Vector3 top, Vector3 normal, float length, float time)
        {
            var d = Spawn(Kind.Run, top, normal, 1f, 240f);
            d.MakeRun(top, Random.Range(0.025f, 0.05f), length, time);
        }

        /// <summary>Splat where a flying droplet landed (called from <see cref="BloodDroplets"/>).</summary>
        public static void Splat(Vector3 point, Vector3 normal, float size)
        {
            var d = Spawn(Kind.Splat, point, normal, size, 200f);
            d.Spread(0.7f, 0.15f);
            // Drops that hit a wall high enough sometimes start to run.
            if (Mathf.Abs(normal.y) < 0.4f && size > 0.07f && Random.value < 0.3f)
                Run(point, normal, Random.Range(0.08f, 0.3f), Random.Range(3f, 8f));
        }

        /// <summary>Bloody shoe print (alpha fades as the sole runs out of blood).</summary>
        public static void Footprint(Vector3 position, float yaw, bool left, float alpha)
        {
            if (!SurfaceRay(position + Vector3.up * 0.4f, Vector3.down, 1.2f, out var hit) || hit.normal.y < 0.7f) return;
            var d = Spawn(Kind.Foot, hit.point, hit.normal, 1f, 200f, yaw);
            d.transform.localScale = new Vector3(left ? -0.13f : 0.13f, 0.31f, 1f);
            d.Alpha = Mathf.Clamp01(alpha);
            d.Spread(1f, 0f);
        }

        /// <summary>Arterial spurt from a stump: pulses with the heartbeat, weakening over <paramref name="duration"/>.</summary>
        public static void Spurt(Transform joint, Vector3 worldDir, float duration)
        {
            if (joint == null) return;
            var go = new GameObject("BloodSpurt");
            go.transform.SetParent(joint, false);
            go.transform.rotation = Quaternion.LookRotation(worldDir.sqrMagnitude > 1e-4f ? worldDir : Vector3.down);
            var ps = MakeDropletSystem(go, 0, 1.2f, 3.4f, 9f, 18);
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.stopAction = ParticleSystemStopAction.None;
            go.AddComponent<BloodSpurt>().Init(ps, duration);
            ps.Play();
        }

        /// <summary>A cut-off limb keeps dripping for a while (drops splat where it rolls).</summary>
        public static void GibBleed(Transform gib, float duration)
        {
            if (gib == null) return;
            var go = new GameObject("GibBleed");
            go.transform.SetParent(gib, false);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.up);
            var ps = MakeDropletSystem(go, 0, 0.3f, 1.4f, 70f, 10);
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.stopAction = ParticleSystemStopAction.None;
            go.AddComponent<BloodSpurt>().Init(ps, duration, false);
            ps.Play();
        }

        /// <summary>Raw red flesh cap on a stump (on the body and on the flying part).</summary>
        public static GameObject Stump(Transform parent, Vector3 worldPos, Quaternion worldRot, Vector3 size)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "BloodStump";
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.SetPositionAndRotation(worldPos, worldRot);
            g.transform.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = StumpMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.layer = parent != null ? parent.gameObject.layer : 0;
            return g;
        }

        // ================================================================ surfaces

        private static readonly RaycastHit[] Hits = new RaycastHit[12];

        /// <summary>Nearest level surface along a ray (never players, RC cars, flying gibs or triggers).</summary>
        public static bool SurfaceRay(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, dist, SurfaceMask, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (!IsLevel(h.collider)) continue;
                if (h.distance < bd) { bd = h.distance; best = h; }
            }
            return bd < float.MaxValue;
        }

        public static bool IsLevel(Collider c)
        {
            if (c == null) return false;
            if (c is CharacterController) return false;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false;
            return true;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }

        // ================================================================ particles

        private static void Droplets(Vector3 pos, Vector3 dir, int count, float vMin, float vMax, float angle, int splats)
        {
            var go = new GameObject("BloodSpray");
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            var ps = MakeDropletSystem(go, count, vMin, vMax, angle, splats);
            ps.Play();
        }

        private static ParticleSystem MakeDropletSystem(GameObject go, int burst, float vMin, float vMax, float angle, int splats)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.25f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(vMin, vMax);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.034f);
            main.startColor = new ParticleSystem.MinMaxGradient(Drop, DropDark);
            main.gravityModifier = 1.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 400;
            var em = ps.emission;
            em.rateOverTime = 0f;
            if (burst > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst), new ParticleSystem.Burst(0.06f, (short)(burst / 3)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = 0.025f;
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.collidesWith = SurfaceMask;
            col.dampen = 1f;
            col.bounce = 0f;
            col.lifetimeLoss = 1f; // a drop that lands is gone (it became a splat)
            col.sendCollisionMessages = true;
            col.quality = ParticleSystemCollisionQuality.Medium;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.7f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.018f;
            r.lengthScale = 1.1f;
            r.material = FxUtil.Soft;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<BloodDroplets>().Init(ps, splats);
            return ps;
        }

        private static void Mist(Vector3 pos, Vector3 dir, int count)
        {
            if (count <= 0) return;
            var go = new GameObject("BloodMist");
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.02f, 0.03f, 0.55f), new Color(0.3f, 0.01f, 0.02f, 0.35f));
            main.gravityModifier = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.03f;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.8f));
            var c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            c.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Soft;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        // ================================================================ decals

        public enum Kind { Splat, Drop, Run, Foot }

        private static BloodDecal Spawn(Kind kind, Vector3 point, Vector3 normal, float size, float life, float yaw = float.NaN)
        {
            var q = new GameObject("Blood" + kind);
            var mf = q.AddComponent<MeshFilter>();
            mf.sharedMesh = QuadMesh;
            var mr = q.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFor(kind);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Vector3 n = normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up;
            Quaternion rot;
            if (!float.IsNaN(yaw))
            {
                Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                rot = Quaternion.LookRotation(-n, Vector3.ProjectOnPlane(fwd, n));
            }
            else if (kind == Kind.Run)
            {
                Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n);
                rot = Quaternion.LookRotation(-n, up.sqrMagnitude > 1e-4f ? up : Vector3.forward);
            }
            else
            {
                Vector3 t = Vector3.ProjectOnPlane(Random.onUnitSphere, n);
                rot = Quaternion.LookRotation(-n, t.sqrMagnitude > 1e-4f ? t : Vector3.Cross(n, Vector3.right));
            }
            q.transform.SetPositionAndRotation(point + n * (0.004f + Random.value * 0.004f), rot);
            q.transform.localScale = new Vector3(size, size, 1f);
            var d = q.AddComponent<BloodDecal>();
            d.Init(mr, life);
            return d;
        }

        private static Mesh _quad;

        private static Mesh QuadMesh
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "BloodQuad" };
                _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
                _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                _quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                _quad.tangents = new[] { new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f) };
                _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                _quad.RecalculateBounds();
                return _quad;
            }
        }

        // ================================================================ materials / textures

        private static Shader _shader;
        private static Material[] _splatMats;
        private static Material _dropMat, _runMat, _footMat, _stumpMat;

        private static Shader BloodShader
        {
            get
            {
                if (_shader == null) _shader = Resources.Load<Shader>("Shaders/HnsBlood");
                if (_shader == null) _shader = Shader.Find("HideAndSeek/Blood");
                return _shader;
            }
        }

        private static Material MaterialFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Drop:
                    return _dropMat != null ? _dropMat : (_dropMat = MakeMat("BloodDrop", BloodTextures.Splat(91, 32, 0.2f, out var dn), dn));
                case Kind.Run:
                    return _runMat != null ? _runMat : (_runMat = MakeMat("BloodRun", BloodTextures.Run(out var rn), rn));
                case Kind.Foot:
                    return _footMat != null ? _footMat : (_footMat = MakeMat("BloodFoot", BloodTextures.Foot(out var fn), fn));
                default:
                    if (_splatMats == null)
                    {
                        _splatMats = new Material[4];
                        for (int i = 0; i < _splatMats.Length; i++)
                            _splatMats[i] = MakeMat("BloodSplat" + i, BloodTextures.Splat(17 + i * 31, 128, 1f, out var sn), sn);
                    }
                    return _splatMats[Random.Range(0, _splatMats.Length)];
            }
        }

        public static Material StumpMaterial
        {
            get
            {
                if (_stumpMat != null) return _stumpMat;
                var tex = BloodTextures.Flesh(out var n);
                _stumpMat = MakeMat("BloodStump", tex, n);
                _stumpMat.SetColor("_Color", new Color(0.62f, 0.07f, 0.07f, 1f));
                _stumpMat.SetFloat("_Glossiness", 0.7f);
                return _stumpMat;
            }
        }

        private static Material MakeMat(string name, Texture2D albedo, Texture2D normal)
        {
            var m = new Material(BloodShader) { name = name, mainTexture = albedo };
            m.SetTexture("_BumpMap", normal);
            m.SetColor("_Color", Fresh);
            return m;
        }
    }
}
