using System.Collections;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby "WYJŚCIE" door (on its hinge). E opens it inwards: blinding white light pours in, a minigun winds up and
    /// shreds whoever opened it (tracers, muzzle flicker, sparks, holes, blood), then a black figure with a knife
    /// leans in, grabs the door and slams it shut. Behind the door there is nothing but white. Played on every client
    /// from <see cref="NetPlayer"/>'s exit-door RPC; the server owns the victim's lock / respawn.
    /// </summary>
    public class LobbyExitDoor : MonoBehaviour
    {
        public static LobbyExitDoor Instance { get; private set; }

        /// <summary>Spot in front of the door where the opener is placed (outside the door's swing), facing the door.</summary>
        public static readonly Vector3 StandPos = D(4.55f, 0f, -5.15f);
        /// <summary>Old plan coordinates (door at x 4.3 in a south wall at z -6) moved to the door in <see cref="LobbyLayout"/>.</summary>
        private static Vector3 D(float x, float y, float z) => new(x - 4.3f + LobbyLayout.ExitDoorX, y, z + 6f - LobbyLayout.HalfD);
        public const float StandYaw = 180f;
        public static readonly Vector3 Muzzle = D(4.3f, 1.35f, -7.2f);

        public bool Running { get; private set; }
        public Vector3 HandleWorld => _handle != null ? _handle.position : transform.position + new Vector3(0.83f, 1f, 0.06f);

        private Quaternion _closedRot;
        private Transform _handle;
        private Light _flood, _fill, _flash;
        private Transform _gun;
        private Transform _figure, _figTorso, _figHead, _figArmL, _figArmR;
        private Material _black;
        private float _dazzle;
        private Texture2D _white;
        private bool _localVictim;
        private int _holes;

        private void Awake()
        {
            Instance = this;
            _closedRot = transform.localRotation;
            _handle = transform.Find("HandleLever");
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            // Blinding flood light from the white nothingness (shadows on: nothing leaks through the walls; only on
            // while the door is open).
            _flood = new GameObject("ExitFlood").AddComponent<Light>();
            _flood.transform.position = D(4.3f, 1.7f, -8.4f);
            _flood.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.12f, 1f));
            _flood.type = LightType.Spot;
            _flood.spotAngle = 70f;
            _flood.range = 18f;
            _flood.color = new Color(1f, 0.98f, 0.95f);
            _flood.shadows = LightShadows.Soft;
            _flood.enabled = false;
            _fill = new GameObject("ExitFill").AddComponent<Light>();
            _fill.transform.position = D(4.3f, 1.3f, -5.6f);
            _fill.type = LightType.Point;
            _fill.range = 4.5f;
            _fill.color = Color.white;
            _fill.shadows = LightShadows.None;
            _fill.enabled = false;
            _gun = new GameObject("Minigun").transform;
            _gun.position = Muzzle;
            _flash = _gun.gameObject.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.range = 11f;
            _flash.color = new Color(1f, 0.82f, 0.45f);
            _flash.shadows = LightShadows.Hard;
            _flash.enabled = false;
            foreach (var l in new[] { _flood, _fill })
                l.transform.SetParent(transform.parent, true);
            _gun.SetParent(transform.parent, true);

            // The shadow: black blocky figure (unlit = pure silhouette), long arms, faint white eyes, knife.
            _black = new Material(Shader.Find("Unlit/Color")) { color = Color.black, name = "ExitShadow" };
            var eyes = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.85f, 0.85f, 0.85f), name = "ExitShadowEyes" };
            _figure = new GameObject("ExitShadowFigure").transform;
            _figure.SetParent(transform.parent, false);
            Part(_figure, "LegL", new Vector3(-0.12f, 0.43f, 0f), new Vector3(0.22f, 0.86f, 0.22f), _black);
            Part(_figure, "LegR", new Vector3(0.12f, 0.43f, 0f), new Vector3(0.22f, 0.86f, 0.22f), _black);
            _figTorso = new GameObject("Torso").transform;
            _figTorso.SetParent(_figure, false);
            _figTorso.localPosition = new Vector3(0f, 0.86f, 0f);
            Part(_figTorso, "Body", new Vector3(0f, 0.38f, 0f), new Vector3(0.5f, 0.76f, 0.26f), _black);
            _figHead = new GameObject("Head").transform;
            _figHead.SetParent(_figTorso, false);
            _figHead.localPosition = new Vector3(0f, 0.78f, 0f);
            Part(_figHead, "Skull", new Vector3(0f, 0.23f, 0f), new Vector3(0.46f, 0.46f, 0.46f), _black);
            Part(_figHead, "EyeL", new Vector3(-0.1f, 0.26f, 0.232f), new Vector3(0.06f, 0.025f, 0.005f), eyes);
            Part(_figHead, "EyeR", new Vector3(0.1f, 0.26f, 0.232f), new Vector3(0.06f, 0.025f, 0.005f), eyes);
            _figArmL = new GameObject("ArmL").transform;
            _figArmL.SetParent(_figTorso, false);
            _figArmL.localPosition = new Vector3(-0.31f, 0.68f, 0f);
            Part(_figArmL, "Arm", new Vector3(0f, 0f, 0.5f), new Vector3(0.16f, 0.16f, 1f), _black);
            _figArmR = new GameObject("ArmR").transform;
            _figArmR.SetParent(_figTorso, false);
            _figArmR.localPosition = new Vector3(0.31f, 0.68f, 0f);
            Part(_figArmR, "Arm", new Vector3(0f, 0f, 0.36f), new Vector3(0.16f, 0.16f, 0.72f), _black);
            var knife = BlockyProps.Create(HeldItem.Knife, _figArmR, 0);
            knife.transform.localPosition = new Vector3(0f, 0f, 0.74f);
            knife.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            knife.transform.localScale = Vector3.one * 1.3f;
            _figure.gameObject.SetActive(false);
        }

        private static void Part(Transform parent, string n, Vector3 lp, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // ================================================================ sequence

        public void Play(NetPlayer victim)
        {
            StopAllCoroutines();
            StartCoroutine(Run(victim));
        }

        private IEnumerator Run(NetPlayer victim)
        {
            Running = true;
            _localVictim = victim != null && victim.IsOwner;
            _holes = 0;
            bool opened = false, spun = false, fired = false, screamed = false, fell = false, stopped = false, figure = false, slammed = false;
            AudioSource fireLoop = null;
            ParticleSystem tracers = null;
            float nextHit = 0f, nextBlood = 0f, nextPain = 0f;
            float t = 0f;
            while (t < ExitTimeline.Respawn)
            {
                t += Time.deltaTime;
                float yaw = ExitTimeline.DoorYaw(t);
                transform.localRotation = _closedRot * Quaternion.Euler(0f, yaw, 0f);
                float open = Mathf.Clamp01(Mathf.Abs(yaw) / Mathf.Abs(ExitTimeline.DoorOpenYaw));
                bool lit = open > 0.01f && t < ExitTimeline.Slam;
                if (_flood.enabled != lit) _flood.enabled = lit;
                if (_fill.enabled != lit) _fill.enabled = lit;
                _flood.intensity = 6.5f * open;
                _fill.intensity = 2.2f * open;
                _dazzle = _localVictim ? Mathf.Clamp01(open * 0.55f) * (t < ExitTimeline.FireStart ? 1f : 0.5f) * (1f - ExitTimeline.Seg(t, ExitTimeline.FallEnd, ExitTimeline.FireEnd)) : 0f;

                if (!opened && t >= ExitTimeline.DoorOpenStart - 0.1f)
                {
                    opened = true;
                    GameAudio.PlayAt(GameSfx.DoorOpen, HandleWorld, 1f);
                }
                if (!spun && t >= ExitTimeline.SpinUp)
                {
                    spun = true;
                    GameAudio.PlayAt(GameSfx.MinigunSpinUp, Muzzle, 1f);
                }
                bool firing = t >= ExitTimeline.FireStart && t < ExitTimeline.FireEnd;
                if (!fired && firing)
                {
                    fired = true;
                    fireLoop = GameAudio.StartLoop(GameSfx.MinigunFire, _gun, 1f);
                    tracers = Tracers(victim);
                }
                if (firing)
                {
                    _flash.enabled = Random.value < 0.6f;
                    _flash.intensity = 3f + 5f * Random.value;
                    if (tracers != null && victim != null) tracers.transform.rotation = Quaternion.LookRotation(victim.ChestPosition - Muzzle);
                    if (t >= nextHit)
                    {
                        nextHit = t + 0.07f;
                        Impact(victim);
                    }
                    if (victim != null && t >= nextBlood)
                    {
                        nextBlood = t + 0.09f;
                        Vector3 c = victim.ChestPosition + Random.insideUnitSphere * 0.15f;
                        BloodFx.Spray(c, (c - Muzzle).normalized + Random.insideUnitSphere * 0.4f);
                        victim.Actions?.PlayHitReaction();
                        if (_localVictim && Random.value < 0.5f) GameHud.Hurt(0.7f);
                    }
                    if (victim != null && t < ExitTimeline.FallEnd + 0.5f && t >= nextPain)
                    {
                        nextPain = t + 0.28f;
                        GameAudio.PlayAttached(GameSfx.HiderPain, victim.transform, 1f);
                    }
                }
                if (!screamed && t >= ExitTimeline.FireStart + 0.05f && victim != null)
                {
                    screamed = true;
                    GameAudio.PlayAttached(GameSfx.HiderScream, victim.transform, 1f);
                }
                if (!fell && t >= ExitTimeline.FallEnd)
                {
                    fell = true;
                    if (victim != null)
                    {
                        GameAudio.PlayAt(GameSfx.BodyFall, victim.transform.position, 1f);
                        for (int i = 0; i < 3; i++) BloodFx.Decal(victim.transform.position + victim.transform.forward * -0.6f + Random.insideUnitSphere * 0.4f, Random.Range(0.3f, 0.55f));
                    }
                }
                if (!stopped && t >= ExitTimeline.FireEnd)
                {
                    stopped = true;
                    _flash.enabled = false;
                    if (fireLoop != null) GameAudio.StopLoop(fireLoop, 0.05f);
                    fireLoop = null;
                    if (tracers != null) tracers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    GameAudio.PlayAt(GameSfx.MinigunSpinDown, Muzzle, 1f);
                }
                if (t >= ExitTimeline.FigureIn && t < ExitTimeline.Slam + 0.05f)
                {
                    if (!figure) { figure = true; _figure.gameObject.SetActive(true); }
                    PoseFigure(t, victim);
                }
                if (!slammed && t >= ExitTimeline.Slam)
                {
                    slammed = true;
                    transform.localRotation = _closedRot;
                    _figure.gameObject.SetActive(false);
                    _flood.enabled = _fill.enabled = false;
                    GameAudio.PlayAt(GameSfx.DoorSlam, HandleWorld, 1f);
                    if (_localVictim) GameHud.Kick();
                }
                yield return null;
            }
            if (fireLoop != null) GameAudio.StopLoop(fireLoop, 0.05f);
            transform.localRotation = _closedRot;
            _flood.enabled = _fill.enabled = _flash.enabled = false;
            _figure.gameObject.SetActive(false);
            _dazzle = 0f;
            Running = false;
        }

        /// <summary>The shadow slides in behind the jamb, leans into the light, reaches for the door edge and pulls it shut.</summary>
        private void PoseFigure(float t, NetPlayer victim)
        {
            float inn = ExitTimeline.Seg(t, ExitTimeline.FigureIn, ExitTimeline.FigureIn + 0.7f);
            float back = ExitTimeline.Seg(t, ExitTimeline.Grab + 0.1f, ExitTimeline.Slam);
            // Hidden well behind the wall (the knife must not poke through it), leaning into the doorway, back out.
            Vector3 hidden = D(5.3f, 0f, -7.3f), leaning = D(4.5f, 0f, -6.35f), retreat = D(4.45f, 0f, -6.9f);
            _figure.position = Vector3.Lerp(Vector3.Lerp(hidden, leaning, inn), retreat, back);
            _figure.rotation = Quaternion.Euler(0f, 0f, 0f);
            _figTorso.localRotation = Quaternion.Euler(10f * inn, 0f, 16f * inn * (1f - back * 0.6f));
            // Looks at the body on the floor.
            if (victim != null)
            {
                Vector3 to = victim.ChestPosition - _figHead.position;
                _figHead.rotation = Quaternion.Slerp(_figTorso.rotation, Quaternion.LookRotation(to), 0.8f);
            }
            // Left arm: hangs, then stretches out to the door edge and stays on it while it swings shut.
            Vector3 edge = transform.TransformPoint(new Vector3(0.93f, 1.05f, 0.03f));
            float reach = ExitTimeline.Seg(t, ExitTimeline.FigureIn + 0.6f, ExitTimeline.Grab);
            Vector3 down = _figArmL.position + _figTorso.rotation * new Vector3(0f, -0.9f, 0.2f);
            Vector3 target = Vector3.Lerp(down, edge, reach);
            Vector3 d = target - _figArmL.position;
            _figArmL.rotation = Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d : Vector3.forward);
            _figArmL.localScale = new Vector3(1f, 1f, Mathf.Max(0.6f, d.magnitude));
            // Right arm: knife raised, blade catching the light.
            // Right arm: knife held low in front, blade up, catching the light (stays below the door head).
            _figArmR.localRotation = Quaternion.Euler(38f - 6f * Mathf.Sin(t * 3f), -12f, 0f);
        }

        /// <summary>Tracer stream from the gun to the victim (sparks where they hit the walls).</summary>
        private ParticleSystem Tracers(NetPlayer victim)
        {
            var go = new GameObject("MinigunTracers");
            go.transform.position = Muzzle;
            go.transform.rotation = Quaternion.LookRotation((victim != null ? victim.ChestPosition : Muzzle + Vector3.forward) - Muzzle);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = ExitTimeline.FireEnd - ExitTimeline.FireStart;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 70f;
            main.startSize = 0.025f;
            main.startColor = new Color(1f, 0.9f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 55f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4.5f;
            shape.radius = 0.06f;
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.lifetimeLoss = 1f;
            col.quality = ParticleSystemCollisionQuality.Medium;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = FxUtil.Unlit;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.012f;
            r.lengthScale = 2f;
            ps.Play();
            return ps;
        }

        /// <summary>A stray round: spark burst + bullet hole where it hits the room.</summary>
        private void Impact(NetPlayer victim)
        {
            Vector3 aim = (victim != null ? victim.ChestPosition : Muzzle + Vector3.forward * 5f) + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.9f), 0f);
            Vector3 dir = (aim - Muzzle).normalized;
            int mask = ~((1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9) | (1 << 2));
            if (!Physics.Raycast(Muzzle + dir * 1.3f, dir, out var hit, 20f, mask, QueryTriggerInteraction.Ignore)) return;
            if (hit.collider.GetComponentInParent<NetPlayer>() != null) return;
            ShotFx.Play(Muzzle, hit.point, null, false, true);
            if (Random.value < 0.35f) GameAudio.PlayAt(GameSfx.BulletImpact, hit.point, 0.6f);
            if (_holes >= 40) return;
            _holes++;
            var hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hole.name = "BulletHole";
            Destroy(hole.GetComponent<Collider>());
            hole.transform.SetPositionAndRotation(hit.point + hit.normal * 0.004f, Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            hole.transform.localScale = Vector3.one * Random.Range(0.035f, 0.055f);
            var mr = hole.GetComponent<MeshRenderer>();
            mr.sharedMaterial = FxUtil.Unlit;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(0.03f, 0.025f, 0.02f, 0.9f));
            mr.SetPropertyBlock(mpb);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void OnGUI()
        {
            if (_dazzle <= 0.01f || Event.current.type != EventType.Repaint) return;
            if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            GUI.color = new Color(1f, 1f, 1f, _dazzle);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);
            GUI.color = Color.white;
        }
    }
}
