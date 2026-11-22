using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>Timings (s) of smashing the toy phone; shared by third person, first person, sounds and the server.</summary>
    public static class PhoneTimeline
    {
        /// <summary>The phone leaves the hand (thrown at the floor) and lands.</summary>
        public const float Release = 0.72f, Land = 0.9f;
        /// <summary>The stomp: knee up from <see cref="StompStart"/>, the sole hits at <see cref="StompHit"/>.</summary>
        public const float StompStart = 1.0f, StompHit = 1.42f;
        public const float End = 2.2f;
        /// <summary>The seeker only stomps on a phone already lying there: his clock starts here.</summary>
        public const float SeekerStart = 0.95f;
        /// <summary>Scale of the phone in the blocky fist (on the floor it is 1).</summary>
        public const float HandScale = 1.5f;

        public static float Seg(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));
        public static float Duration(bool throwFirst) => throwFirst ? End : End - SeekerStart;

        /// <summary>
        /// Stomping sole (root space, y = 0 floor) on a phone at <paramref name="ph"/>: the body steps in towards it
        /// (<paramref name="pelvisZ"/>, the other foot <paramref name="support"/>), knee up, slam, grind, step back.
        /// Shared by the third-person body and the first-person leg.
        /// </summary>
        public static Vector3 StompSole(float t, Vector3 ph, out float pelvisZ, out float drop, out Vector3 support)
        {
            float hit = StompHit;
            float lunge = Seg(t, StompStart, hit - 0.12f) * (1f - Seg(t, 1.85f, End - 0.1f));
            pelvisZ = Mathf.Lerp(-0.05f, ph.z - 0.33f, lunge);
            support = new Vector3(-0.14f, 0f, Mathf.Lerp(-0.06f, ph.z - 0.52f, lunge));
            drop = t >= hit ? 0.07f * Seg(t, hit, hit + 0.1f) * (1f - Seg(t, 1.9f, End)) : 0f;
            Vector3 rest = new Vector3(0.12f, 0f, Mathf.Lerp(0.02f, ph.z - 0.4f, lunge));
            Vector3 raised = new Vector3(ph.x * 0.7f + 0.04f, 0.42f, ph.z - 0.08f);
            Vector3 top = ph + new Vector3(0f, PhoneModel.FrontY + 0.01f, 0f);
            Vector3 flat = ph + new Vector3(0f, PhoneModel.FrontY * 0.4f, 0f);
            if (t < hit - 0.1f) return Vector3.Lerp(rest, raised, Seg(t, StompStart, hit - 0.12f));
            if (t < hit) { float k = ActionTimeline.Remap01(t, hit - 0.1f, hit); return Vector3.Lerp(raised, top, k * k); }
            if (t < hit + 0.1f) return Vector3.Lerp(top, flat, Seg(t, hit, hit + 0.08f));
            if (t < 1.85f) return flat + new Vector3(0.014f * Mathf.Sin((t - hit) * 42f), 0f, 0f) * (1f - Seg(t, 1.6f, 1.75f));
            return Vector3.Lerp(flat, rest, Seg(t, 1.85f, End - 0.1f));
        }
    }

    /// <summary>
    /// Toy phone smashed on the floor: a hider pulls the ringing phone out, looks at it, throws it down and stomps on
    /// it; the seeker only stomps on one lying on the floor. The phone in the hand hangs on the right hand socket, the
    /// thrown / lying one is a world object (visible to the owner in first person too) until the sole lands - then the
    /// game shows the wreck (PhoneWreck, seeded by the phone id, the same for everybody).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float phoneT = -1f;
        bool phoneThrow;
        Vector3 phoneLandLocal;
        float phoneLandYaw;
        PhoneModel.Parts phoneHand, phoneWorld;
        Vector3 phoneFlyFrom;
        Quaternion phoneFlyRot;

        /// <summary>True while the smash animation runs (the item in the hand is hidden).</summary>
        public bool PhoneSmashing => phoneT >= 0f;
        /// <summary>Seconds on the phone timeline (-1 when none).</summary>
        public float PhoneSmashTime => phoneT;

        /// <summary>
        /// Starts the smash. <paramref name="landWorld"/> = floor point where the phone ends up, <paramref name="yaw"/> =
        /// its heading there. <paramref name="throwFirst"/>: pulled out of the pocket and thrown (hider), else only stomped.
        /// </summary>
        public void PlayPhoneSmash(Vector3 landWorld, float yaw, bool throwFirst)
        {
            EnsureInit();
            phoneThrow = throwFirst;
            phoneT = throwFirst ? 0f : PhoneTimeline.SeekerStart;
            phoneLandLocal = transform.InverseTransformPoint(landWorld);
            phoneLandYaw = yaw;
            if (phoneWorld != null) Destroy(phoneWorld.Root.gameObject);
            phoneWorld = null;
            if (!throwFirst) SpawnWorldPhone(landWorld, Quaternion.Euler(0f, yaw, 0f), 1f);
        }

        void SpawnWorldPhone(Vector3 pos, Quaternion rot, float scale)
        {
            phoneWorld = PhoneModel.Create(null, 0);
            phoneWorld.Root.SetPositionAndRotation(pos, rot);
            phoneWorld.Root.localScale = Vector3.one * scale;
        }

        Vector3 PhoneLandWorld => transform.TransformPoint(phoneLandLocal);
        Quaternion PhoneLandRot => Quaternion.Euler(0f, phoneLandYaw, 0f);

        /// <summary>Layer step (after the stomp layer): pose + the phone objects. Runs every frame.</summary>
        void ApplyPhoneSmash(ref CharacterPose p, float dt, bool canPose)
        {
            TickPhoneWorld(dt);
            if (phoneT < 0f) { ShowHandPhone(false); return; }
            float prev = phoneT;
            phoneT += dt;
            float t = phoneT;
            if (t >= PhoneTimeline.End || !canPose)
            {
                phoneT = -1f;
                ShowHandPhone(false);
                if (phoneWorld != null) Destroy(phoneWorld.Root.gameObject);
                phoneWorld = null;
                return;
            }

            // Thrown: the world phone takes over from the hand.
            if (phoneThrow && prev < PhoneTimeline.Release && t >= PhoneTimeline.Release && phoneHand != null)
            {
                phoneFlyFrom = phoneHand.Root.position;
                phoneFlyRot = phoneHand.Root.rotation;
                SpawnWorldPhone(phoneFlyFrom, phoneFlyRot, PhoneTimeline.HandScale);
            }
            // The sole lands: the intact phone goes, the wreck (game side, same spot, seeded by the phone id) takes over.
            if (prev < PhoneTimeline.StompHit && t >= PhoneTimeline.StompHit && phoneWorld != null)
            {
                Destroy(phoneWorld.Root.gameObject);
                phoneWorld = null;
            }

            float w = phoneThrow
                ? PhoneTimeline.Seg(t, 0f, 0.15f) * (1f - PhoneTimeline.Seg(t, PhoneTimeline.End - 0.45f, PhoneTimeline.End))
                : PhoneTimeline.Seg(t, PhoneTimeline.SeekerStart, PhoneTimeline.SeekerStart + 0.12f) * (1f - PhoneTimeline.Seg(t, PhoneTimeline.End - 0.45f, PhoneTimeline.End));
            var s = PhoneSmashPose(p, t, out bool handShown);
            p = CharacterPose.Lerp(p, s, w);
            if (handShown) p.PropR = s.PropR;
            ShowHandPhone(handShown);
        }

        CharacterPose PhoneSmashPose(in CharacterPose current, float t, out bool handShown)
        {
            float S(float a, float b) => PhoneTimeline.Seg(t, a, b);
            var q = current;
            handShown = false;
            Vector3 ph = new Vector3(Mathf.Clamp(phoneLandLocal.x, -0.2f, 0.35f), 0f, Mathf.Clamp(phoneLandLocal.z, 0.25f, 0.7f));

            if (phoneThrow && t < PhoneTimeline.StompStart)
            {
                // ---- pocket -> look at the ringing phone -> wind up over the shoulder -> throw it down hard.
                float look = S(0.08f, 0.42f), wind = S(0.46f, 0.62f), thr = S(0.62f, 0.78f);
                q.Torso = Quaternion.Euler(6f + 6f * look - 10f * wind + 34f * thr, -8f * wind + 6f * thr, 0f);
                q.Head = Quaternion.Euler(18f * look - 8f * wind + 22f * thr, 0f, 0f);
                q.PelvisPos = current.PelvisPos + new Vector3(0f, -0.05f * thr, 0.02f * thr);
                Vector3 pocket = q.TorsoPoint(new Vector3(0.27f, 0.05f, 0.1f));
                Vector3 view = q.TorsoPoint(new Vector3(0.1f, 0.5f, 0.42f));
                Vector3 up = q.TorsoPoint(new Vector3(0.32f, 0.95f, -0.05f));
                Vector3 down = q.TorsoPoint(new Vector3(0.18f, 0.12f, 0.5f));
                Vector3 hand = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(pocket, view, look), up, wind), down, thr);
                hand += new Vector3(0.006f, 0.004f, 0f) * Mathf.Sin(t * 60f) * look * (1f - wind); // it buzzes in the hand
                q.SolveArm(0, hand, TorsoDir(q, 1f, -0.5f, -0.6f));
                q.SolveArm(1, q.TorsoPoint(new Vector3(-0.33f, 0.3f + 0.25f * thr, 0.12f + 0.2f * thr)), TorsoDir(q, -1f, -0.4f, -0.5f));
                // Phone in the fist: screen to the face while looking, top backwards when winding up.
                Quaternion lookRot = Quaternion.LookRotation(new Vector3(0f, 1f, 0.3f), new Vector3(0f, 0.3f, -1f));
                Quaternion windRot = Quaternion.LookRotation(new Vector3(0f, 0.55f, -0.85f), new Vector3(0f, 0.85f, 0.55f));
                Quaternion throwRot = Quaternion.LookRotation(new Vector3(0f, -0.8f, 0.6f), new Vector3(0f, 0.6f, 0.8f));
                Quaternion rot = Quaternion.Slerp(Quaternion.Slerp(Quaternion.LookRotation(Vector3.up, Vector3.forward), lookRot, look), windRot, wind);
                rot = Quaternion.Slerp(rot, throwRot, thr);
                q.PropR = Quaternion.Inverse(q.ForearmRoot(0)) * rot;
                handShown = t >= 0.1f && t < PhoneTimeline.Release;
                return q;
            }

            // ---- stomp: step in, right knee up, the sole slams onto the phone, grinds, steps back.
            float hit = PhoneTimeline.StompHit;
            Vector3 sole = PhoneTimeline.StompSole(t, ph, out float pelvisZ, out float drop, out Vector3 support);
            float lean = t < hit ? -5f * S(PhoneTimeline.StompStart, hit - 0.1f) : 16f * S(hit, hit + 0.1f) * (1f - S(1.9f, PhoneTimeline.End));
            q.PelvisPos = new Vector3(-0.05f, BlockyCharacter.HipHeight - 0.02f - drop, pelvisZ);
            q.Pelvis = Quaternion.Euler(0f, -10f, 0f);
            q.Torso = Quaternion.Euler(lean + 6f, 8f, 0f);
            q.Head = Quaternion.Euler(30f, 0f, 0f);
            q.SolveLeg(0, sole, new Vector3(0.1f, 0.3f, 1f));
            q.SolveLeg(1, support, new Vector3(-0.1f, 0f, 1f));
            // Arms out for balance; a hider shakes the throwing hand out, the seeker keeps his weapon hand.
            q.SolveArm(1, q.TorsoPoint(new Vector3(-0.44f, 0.4f, 0.12f)), TorsoDir(q, -1f, -0.3f, -0.6f));
            if (phoneThrow) q.SolveArm(0, q.TorsoPoint(new Vector3(0.42f, 0.36f + 0.03f * Mathf.Sin(t * 20f), 0.1f)), TorsoDir(q, 1f, -0.3f, -0.6f));
            return q;
        }

        /// <summary>The thrown phone flies to the floor and bounces; then it lies there until the stomp.</summary>
        void TickPhoneWorld(float dt)
        {
            if (phoneWorld == null) return;
            var root = phoneWorld.Root;
            float t = phoneT;
            if (phoneT >= 0f && phoneThrow && t < PhoneTimeline.Land + 0.2f)
            {
                float k = Mathf.Clamp01((t - PhoneTimeline.Release) / (PhoneTimeline.Land - PhoneTimeline.Release));
                Vector3 land = PhoneLandWorld;
                Vector3 pos = Vector3.Lerp(phoneFlyFrom, land, k * k) + Vector3.up * 0.12f * Mathf.Sin(Mathf.PI * k) * (1f - k);
                float b = t > PhoneTimeline.Land ? Mathf.Sin(Mathf.PI * (t - PhoneTimeline.Land) / 0.2f) * 0.05f : 0f; // one bounce
                root.position = pos + Vector3.up * b;
                Quaternion tumble = Quaternion.Euler(0f, 0f, 25f * b / 0.05f);
                root.rotation = Quaternion.Slerp(phoneFlyRot, PhoneLandRot, ActionTimeline.SmoothStep01(k)) * tumble;
                root.localScale = Vector3.one * Mathf.Lerp(PhoneTimeline.HandScale, 1f, k);
            }
            else
            {
                root.SetPositionAndRotation(PhoneLandWorld, PhoneLandRot);
                root.localScale = Vector3.one;
            }
            PhoneModel.SetScreen(phoneWorld, Mathf.Repeat(Time.time * 3f, 1f) < 0.5f);
        }

        void ShowHandPhone(bool on)
        {
            if (on && phoneHand == null)
            {
                phoneHand = PhoneModel.Create(socketR, gameObject.layer);
                phoneHand.Root.localPosition = new Vector3(0f, -PhoneModel.Thick * 0.5f * PhoneTimeline.HandScale, 0.12f);
                phoneHand.Root.localScale = Vector3.one * PhoneTimeline.HandScale;
            }
            if (phoneHand == null) return;
            if (phoneHand.Root.gameObject.activeSelf != on) phoneHand.Root.gameObject.SetActive(on);
            if (!on) return;
            PhoneModel.SetRenderMode(phoneHand, gameObject.layer, character.IsFirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On);
            PhoneModel.SetScreen(phoneHand, Mathf.Repeat(Time.time * 3f, 1f) < 0.5f);
        }

        void OnDestroy()
        {
            if (phoneWorld != null) Destroy(phoneWorld.Root.gameObject);
            phoneWorld = null;
            DestroyRemoteWorld();
        }
    }
}
