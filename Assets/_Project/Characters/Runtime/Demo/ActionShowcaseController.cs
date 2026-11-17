using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Showcase for the action layer. Spawns a seeker/victim pair, a crawler, a gunman and a medic with a patient.
    /// Keys: K knife pair, D downed/crawl -> dead -> alive, G shoot, R reload, H revive, T hit flinch,
    /// F first-person view (Q cycles item there), 1-5 skins.
    /// </summary>
    public class ActionShowcaseController : MonoBehaviour
    {
        public Vector3 rowOrigin = new Vector3(0f, 0f, 4.5f);

        BlockyCharacter seeker, victim, crawler, gunman, medic, patient;
        ShowcaseCrawlerMotion crawlerMotion;
        float victimGetUpAt = -1f;
        bool fpp;
        Camera mainCam;
        Behaviour orbit;
        Vector3 camPos;
        Quaternion camRot;
        FirstPersonViewModel viewModel;
        GameObject fppTarget;

        void Start()
        {
            seeker = Spawn("Seeker", new Vector3(-4.5f, 0f, 0f), 0f, 1);
            victim = Spawn("Victim", new Vector3(-4.5f, 0f, ActionTimeline.KnifeAttackDistance), 180f, 3);
            seeker.Actions.SetHeldItem(HeldItem.Knife);

            var crawlerRoot = new GameObject("Crawler");
            crawlerRoot.transform.SetParent(transform, false);
            crawlerRoot.transform.localPosition = rowOrigin + new Vector3(-1.2f, 0f, 0f);
            crawlerMotion = crawlerRoot.AddComponent<ShowcaseCrawlerMotion>();
            crawler = BlockyCharacterFactory.Create(crawlerRoot.transform, 2);

            var gunRoot = new GameObject("Gunman");
            gunRoot.transform.SetParent(transform, false);
            gunRoot.transform.localPosition = rowOrigin + new Vector3(2f, 0f, 0f);
            var demo = gunRoot.AddComponent<DemoMotionSource>();
            demo.mode = DemoMotionMode.Walk;
            demo.walkSpeed = 1.3f;
            demo.trackLength = 2.5f;
            demo.trackRadius = 0.6f;
            gunman = BlockyCharacterFactory.Create(gunRoot.transform, 0);
            gunman.Actions.SetHeldItem(HeldItem.Glock);

            patient = Spawn("Patient", new Vector3(5.5f, 0f, 0f), 0f, 4);
            patient.Actions.SetLifeStateImmediate(LifeState.Downed);
            medic = Spawn("Medic", new Vector3(5.05f, 0f, -0.3f), 90f, 2);
            medic.Actions.SetHeldItem(HeldItem.Medkit);

            mainCam = Camera.main;
            if (mainCam != null) orbit = mainCam.GetComponent<ShowcaseOrbitCamera>();
        }

        BlockyCharacter Spawn(string n, Vector3 offset, float yaw, int skin)
        {
            var root = new GameObject(n);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = rowOrigin + offset;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return BlockyCharacterFactory.Create(root.transform, skin);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.fKey.wasPressedThisFrame) ToggleFpp();

            if (kb.kKey.wasPressedThisFrame)
            {
                if (fpp && viewModel != null)
                {
                    viewModel.SetHeldItem(HeldItem.Knife);
                    viewModel.PlayKnifeAttack();
                }
                else
                {
                    victim.Actions.SetLifeStateImmediate(LifeState.Alive);
                    seeker.Actions.PlayKnifeAttack(victim);
                    victim.Actions.PlayStabbedReaction();
                    victim.Actions.SetLifeState(LifeState.Downed);
                    victimGetUpAt = Time.time + ActionTimeline.StabbedReactionDuration + 2.5f;
                }
            }

            if (victimGetUpAt > 0f && Time.time >= victimGetUpAt)
            {
                victimGetUpAt = -1f;
                victim.Actions.SetLifeState(LifeState.Alive);
            }

            if (kb.dKey.wasPressedThisFrame)
            {
                var a = crawler.Actions;
                a.SetLifeState(a.LifeState == LifeState.Alive ? LifeState.Downed : a.LifeState == LifeState.Downed ? LifeState.Dead : LifeState.Alive);
                crawlerMotion.crawl = a.LifeState == LifeState.Downed;
            }

            if (kb.gKey.wasPressedThisFrame)
            {
                if (fpp && viewModel != null)
                {
                    viewModel.SetHeldItem(HeldItem.Glock);
                    viewModel.PlayShoot();
                    MuzzleFlash.Spawn(viewModel.MuzzlePoint, true);
                }
                else
                {
                    gunman.Actions.PlayShoot();
                    MuzzleFlash.Spawn(gunman.Actions.MuzzlePoint, false);
                }
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                if (fpp && viewModel != null)
                {
                    viewModel.SetHeldItem(HeldItem.Glock);
                    viewModel.PlayReload();
                }
                else gunman.Actions.PlayReload();
            }

            if (kb.hKey.wasPressedThisFrame) medic.Actions.SetReviving(!medic.Actions.IsReviving);

            if (kb.tKey.wasPressedThisFrame)
                foreach (var c in new[] { seeker, gunman, medic, crawler })
                    c.Actions.PlayHitReaction();

            if (kb.qKey.wasPressedThisFrame && viewModel != null)
                viewModel.SetHeldItem((HeldItem)(((int)viewModel.HeldItem + 1) % 4));

            for (int i = 0; i < 5; i++)
            {
                if (!kb[Key.Digit1 + i].wasPressedThisFrame) continue;
                foreach (var c in new[] { seeker, victim, crawler, gunman, medic, patient }) c.SetSkin(i);
                if (viewModel != null) viewModel.SetSkin(i);
            }
        }

        void ToggleFpp()
        {
            if (mainCam == null) return;
            fpp = !fpp;
            if (fpp)
            {
                camPos = mainCam.transform.position;
                camRot = mainCam.transform.rotation;
                if (orbit != null) orbit.enabled = false;
                mainCam.transform.SetPositionAndRotation(transform.TransformPoint(rowOrigin + new Vector3(2f, 1.6f, -3.2f)),
                    Quaternion.LookRotation(Vector3.forward));
                if (viewModel == null)
                {
                    viewModel = FirstPersonViewModel.Create(mainCam.transform, 0, null);
                    viewModel.SetHeldItem(HeldItem.Glock);
                }

                viewModel.SetVisible(true);
            }
            else
            {
                if (viewModel != null) viewModel.SetVisible(false);
                mainCam.transform.SetPositionAndRotation(camPos, camRot);
                if (orbit != null) orbit.enabled = true;
            }
        }

        void OnGUI()
        {
            GUI.Label(new Rect(12, 12, 900, 22),
                fpp
                    ? "FPP: G shoot  R reload  K knife  Q cycle item  F back  1-5 skin"
                    : "K knife pair   D downed/crawl > dead > alive   G shoot   R reload   H revive   T hit   F first person   1-5 skins");
        }
    }

    /// <summary>Motion source for the crawler: crawls around a small circle (3 s move / 2 s rest) while <see cref="crawl"/>.</summary>
    public class ShowcaseCrawlerMotion : MonoBehaviour, ICharacterMotionSource
    {
        public bool crawl;
        public float crawlSpeed = 0.6f;
        public float radius = 1.0f;

        Vector3 center;
        float angle, t, speed;

        public Vector3 Velocity { get; private set; }
        public float HorizontalSpeed => speed;
        public bool IsGrounded => true;
        public bool IsCrouching => false;
        public bool IsRunning => false;
        public float CrouchAmount => 0f;
        public float LookPitch => 0f;
        public LocomotionState State => speed > 0.05f ? LocomotionState.Walk : LocomotionState.Idle;

        void Awake() => center = transform.localPosition - new Vector3(radius, 0f, 0f);

        void Update()
        {
            t += Time.deltaTime;
            bool moving = crawl && Mathf.Repeat(t, 5f) < 3f;
            speed = Mathf.MoveTowards(speed, moving ? crawlSpeed : 0f, Time.deltaTime * 2f);
            angle += speed / radius * Time.deltaTime;
            var p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            var tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            transform.localPosition = p;
            transform.localRotation = Quaternion.LookRotation(tangent);
            Velocity = transform.parent != null ? transform.parent.TransformDirection(tangent * speed) : tangent * speed;
        }
    }
}
