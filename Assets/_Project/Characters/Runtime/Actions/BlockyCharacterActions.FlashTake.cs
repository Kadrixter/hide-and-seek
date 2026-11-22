using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Timings (s) of taking the shoulder flashlight out of the lobby box and putting it back; shared by third person,
    /// first person, the lamp model and the sounds.
    /// </summary>
    public static class FlashTakeTimeline
    {
        const float Px = BlockyCharacter.PixelSize;

        public const float TakeDuration = 2.0f, PutDuration = 1.55f;
        /// <summary>Take: the left hand closes on a lamp in the box / the lamp clicks onto the shoulder.</summary>
        public const float TakeGrab = 0.5f, TakeClip = 1.22f;
        /// <summary>Put back: the lamp comes off the shoulder / is let go in the box.</summary>
        public const float PutUnclip = 0.42f, PutRelease = 1.0f;

        /// <summary>Where the hand picks a lamp (root space of the player standing at the box, facing it).</summary>
        public static readonly Vector3 GrabLocal = new Vector3(-0.06f, 0.97f, 0.5f);
        /// <summary>Lamp on the torso (torso pivot space, the pivot is at the hips; +x = right, +z = front).</summary>
        public static readonly Vector3 LampTorsoLocal = new Vector3(2.4f * Px, 10.2f * Px, 2f * Px + 0.03f);
        /// <summary>Lamp held in the left fist (elbow space: the forearm hangs along -Y), sticking out of the fist.</summary>
        public static readonly Vector3 LampInHandLocal = CharacterPose.HandLocal + new Vector3(0f, -(3f * Px + 0.035f), 0.01f);
        public static readonly Quaternion LampInHandRot = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        /// <summary>Lamp on the shoulder relative to the eye (body frame): also where the real light starts.</summary>
        public static readonly Vector3 LampEyeOffset = new Vector3(0.13f, -0.38f, 0.17f);

        public static float Duration(bool take) => take ? TakeDuration : PutDuration;

        public static float S(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>0 = in the box (hidden), 1 = in the left hand, 2 = on the shoulder. In the hand,
        /// <paramref name="toShoulder"/> 0..1 slides the lamp from the fist onto its place on the shoulder.</summary>
        public static int LampState(bool take, float t, out float toShoulder)
        {
            toShoulder = 0f;
            if (take)
            {
                if (t < TakeGrab) return 0;
                if (t >= TakeClip) return 2;
                toShoulder = S(t, TakeClip - 0.24f, TakeClip);
                return 1;
            }
            if (t < PutUnclip) return 2;
            if (t >= PutRelease) return 0;
            toShoulder = 1f - S(t, PutUnclip, PutUnclip + 0.22f);
            return 1;
        }

        /// <summary>0..1 head turned to the right shoulder (both clips).</summary>
        public static float LookAtShoulder(bool take, float t) => take
            ? S(t, 0.72f, 1.0f) * (1f - S(t, 1.5f, 1.8f))
            : S(t, 0.0f, 0.3f) * (1f - S(t, 0.45f, 0.7f));

        /// <summary>0..1 leaning over the box.</summary>
        public static float Lean(bool take, float t) => take
            ? S(t, 0.0f, 0.4f) * (1f - S(t, 0.62f, 1.0f))
            : S(t, 0.5f, 0.85f) * (1f - S(t, 1.05f, 1.4f));
    }

    /// <summary>
    /// Lobby flashlight box: lean over the box, the left hand picks a lamp, brings it up to the right shoulder, clicks it
    /// onto the strap and gives the strap a tug (or the reverse when putting it back). The lamp model itself is moved by
    /// the game (it is parented to the torso); this layer only poses the body.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float flashT = -1f;
        bool flashTake;

        /// <summary>Starts the take (true) or put-back (false) animation.</summary>
        public void PlayFlashTake(bool take)
        {
            EnsureInit();
            flashTake = take;
            flashT = 0f;
        }

        public bool FlashTaking => flashT >= 0f;

        void ApplyFlashTake(ref CharacterPose p, float dt)
        {
            if (flashT < 0f) return;
            if (VisualLifeState != LifeState.Alive || action != ActionKind.None || pathActive)
            {
                flashT = -1f;
                return;
            }
            flashT += dt;
            float t = flashT, dur = FlashTakeTimeline.Duration(flashTake);
            if (t >= dur)
            {
                flashT = -1f;
                return;
            }
            float w = FlashTakeTimeline.S(t, 0f, 0.2f) * (1f - FlashTakeTimeline.S(t, dur - 0.32f, dur));
            var q = FlashTakePose(p, t, flashTake);
            q.PropR = p.PropR;
            p = CharacterPose.Lerp(p, q, w);
        }

        CharacterPose FlashTakePose(in CharacterPose current, float t, bool take)
        {
            float S(float a, float b) => FlashTakeTimeline.S(t, a, b);
            var q = current;
            float lean = FlashTakeTimeline.Lean(take, t);
            float look = FlashTakeTimeline.LookAtShoulder(take, t);
            q.Torso = current.Torso * Quaternion.Euler(24f * lean + 6f * look, -10f * lean - 8f * look, 0f);
            q.PelvisPos = current.PelvisPos + new Vector3(0f, -0.04f * lean, -0.05f * lean);
            q.Head = Quaternion.Euler(22f * lean + 30f * look, 42f * look, -6f * look);

            Vector3 rest = current.HandPos(1);
            Vector3 grab = FlashTakeTimeline.GrabLocal;
            Vector3 above = grab + new Vector3(0f, 0.14f, -0.04f);
            Vector3 lamp = q.TorsoPoint(FlashTakeTimeline.LampTorsoLocal);
            Vector3 front = q.TorsoPoint(FlashTakeTimeline.LampTorsoLocal + new Vector3(-0.13f, -0.1f, 0.2f));
            Vector3 press = Vector3.Lerp(front, lamp, 0.22f);
            Vector3 strap = q.TorsoPoint(FlashTakeTimeline.LampTorsoLocal + new Vector3(-0.08f, -0.2f, 0.17f));

            Vector3 l;
            if (take)
            {
                if (t < 0.32f) l = Vector3.Lerp(rest, above, S(0.02f, 0.32f));
                else if (t < FlashTakeTimeline.TakeGrab) l = Vector3.Lerp(above, grab, S(0.3f, 0.48f));
                else if (t < 1.0f) l = Vector3.Lerp(grab, front, S(0.55f, 1.0f)) + new Vector3(0f, 0.08f * Mathf.Sin(Mathf.PI * S(0.55f, 1.0f)), 0f);
                else if (t < 1.3f) l = Vector3.Lerp(front, press, S(1.0f, 1.18f));
                else if (t < 1.55f) l = Vector3.Lerp(press, strap, S(1.3f, 1.42f)) + new Vector3(0f, -0.03f * Mathf.Sin(Mathf.PI * S(1.38f, 1.55f)), 0f);
                else l = Vector3.Lerp(strap, rest, S(1.55f, 1.95f));
            }
            else
            {
                if (t < FlashTakeTimeline.PutUnclip) l = Vector3.Lerp(rest, press, S(0.02f, 0.36f));
                else if (t < FlashTakeTimeline.PutRelease) l = Vector3.Lerp(press, grab, S(0.45f, 0.95f)) + new Vector3(0f, 0.1f * Mathf.Sin(Mathf.PI * S(0.45f, 0.95f)), 0f);
                else l = Vector3.Lerp(grab, rest, S(1.05f, 1.5f));
            }
            q.SolveArm(1, l, TorsoDir(q, -1f, -0.6f, -0.4f));
            return q;
        }
    }
}
