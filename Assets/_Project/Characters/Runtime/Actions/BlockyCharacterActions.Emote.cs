using System;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Emotes (K wheel): a full-body layer on top of the standing pose, below every gameplay layer (sitting, items in
    /// use, life state...), so anything important simply wins. Solo emotes are one-shots with their own way in and out
    /// or loops (dances, sitting on the floor); pair emotes track the partner's body (<see cref="SetEmotePartner"/>).
    /// Every emote keeps breathing / weight shifts, fades in over <see cref="EmoteInfo.In"/> and out over
    /// <see cref="EmoteInfo.Out"/> (stopping a loop, walking away) or plays its own exit (<see cref="EmoteInfo.Exit"/>).
    /// Deterministic in (id, time): every peer shows the same frame for the same clock.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        EmoteId emoteId;
        float emoteT, emoteAge, emoteW, emoteStopT;
        bool emoteStopping, emoteFrozen;
        BlockyCharacter emotePartner;
        int emoteRole;
        int emoteBeatsFired;
        float emoteSeed = 3.7f;

        /// <summary>Contact moments (clap, slap, pat...), with the emote and the beat index; for sounds.</summary>
        public event Action<EmoteId, EmoteBeat, int> EmoteBeatFired;

        /// <summary>Emote playing (not stopping). None when nothing or while fading out.</summary>
        public EmoteId Emote => emoteStopping ? EmoteId.None : emoteId;

        /// <summary>Emote on screen at all (also while fading out / getting up).</summary>
        public bool EmoteShown => emoteId != EmoteId.None && (emoteW > 0.001f || !emoteStopping);

        /// <summary>Layer weight 0..1.</summary>
        public float EmoteWeight => emoteW;

        /// <summary>Seconds since the emote started (its own clock).</summary>
        public float EmoteTime => emoteT;

        /// <summary>The body is on the floor or getting up (no walking until it is done).</summary>
        public bool EmoteHoldsFeet
        {
            get
            {
                var info = Emotes.Get(emoteId);
                return info != null && !info.Standing && emoteW > 0.001f;
            }
        }

        /// <summary>
        /// Starts emote <paramref name="id"/> at <paramref name="t"/> seconds into it (late joiners / network clock), or
        /// keeps the running one in step with that clock. None = <see cref="StopEmote"/>.
        /// </summary>
        public void PlayEmote(EmoteId id, float t = 0f)
        {
            EnsureInit();
            if (id == EmoteId.None) { StopEmote(); return; }
            var info = Emotes.Get(id);
            if (info == null) return;
            if (id != emoteId && !info.Loops && t >= info.Duration - 0.05f) return; // already over (late / ending)
            if (id == emoteId && !emoteStopping)
            {
                // Same emote: only follow the shared clock when it drifted (no jitter from small corrections).
                if (Mathf.Abs(t - emoteT) > 0.2f) SetEmoteClock(t);
                return;
            }
            if (emoteId != EmoteId.None && emoteW > 0.01f) BeginCrossfade(); // emote -> emote
            emoteId = id;
            emoteT = Mathf.Max(0f, t);
            emoteAge = emoteT < 0.5f ? emoteT : 0f; // joined late: fade in from now
            emoteStopping = false;
            emoteStopT = 0f;
            emoteFrozen = false;
            emoteSeed = Rand(0f, 40f);
            emoteBeatsFired = CountBeats(id, emoteT);
        }

        void SetEmoteClock(float t)
        {
            emoteT = Mathf.Max(0f, t);
            emoteBeatsFired = CountBeats(emoteId, emoteT);
        }

        /// <summary>Ends the emote: loops fade / get up, one-shots fade out from where they are.</summary>
        public void StopEmote()
        {
            if (emoteId == EmoteId.None || emoteStopping) return;
            emoteStopping = true;
            emoteStopT = 0f;
        }

        /// <summary>Drops the emote at once (death, kill, teleport): the general cross-fade hides the jump.</summary>
        public void CancelEmote()
        {
            if (emoteId == EmoteId.None) return;
            if (emoteW > 0.01f) BeginCrossfade();
            emoteId = EmoteId.None;
            emoteW = 0f;
            emoteStopping = false;
            emotePartner = null;
        }

        /// <summary>Pair emotes: the other body (null = none) and this side's role (0 = asked, leads; 1 = accepted).</summary>
        public void SetEmotePartner(BlockyCharacter partner, int role)
        {
            emotePartner = partner;
            emoteRole = role;
        }

        /// <summary>Wheel pictures / previews: shows <paramref name="id"/> at <paramref name="t"/> right away (frozen = the clock stands).</summary>
        public void SetEmotePreview(EmoteId id, float t, bool frozen)
        {
            EnsureInit();
            emoteId = id;
            emoteT = t;
            emoteAge = 10f;
            emoteW = id != EmoteId.None ? 1f : 0f;
            emoteStopping = false;
            emoteFrozen = frozen;
            emoteBeatsFired = CountBeats(id, t);
            hasLast = false;
            fadeT = 1f;
        }

        static int CountBeats(EmoteId id, float t)
        {
            var info = Emotes.Get(id);
            var beats = EmoteTimeline.Beats(id, out _);
            if (info != null && info.Loops) t = Mathf.Repeat(t, info.Period);
            int n = 0;
            while (n < beats.Length && t >= beats[n]) n++;
            return n;
        }

        // ================================================================== per frame

        void ApplyEmote(ref CharacterPose p, float dt, bool standing)
        {
            if (emoteId == EmoteId.None) { emoteW = 0f; return; }
            var info = Emotes.Get(emoteId);
            if (!standing || action != ActionKind.None || sitting || VisualLifeState != HideAndSeek.Core.LifeState.Alive)
            {
                // Something important took the body: no exit animation, the cross-fade covers it.
                CancelEmote();
                return;
            }
            if (!emoteFrozen)
            {
                float prev = emoteT;
                emoteT += dt;
                emoteAge += dt;
                if (emoteStopping) emoteStopT += dt;
                FireEmoteBeats(info, prev, emoteT);
            }

            float wIn = info.In > 0f ? Seg(emoteAge, 0f, info.In) : 1f;
            float wOut = 1f;
            if (!info.Loops) wOut = 1f - Seg(emoteT, info.Duration - info.Out, info.Duration);
            if (emoteStopping)
                wOut = Mathf.Min(wOut, info.Exit > 0f ? 1f - Seg(emoteStopT, info.Exit - 0.2f, info.Exit) : 1f - Seg(emoteStopT, 0f, info.Out));
            emoteW = wIn * wOut;
            bool done = (!info.Loops && emoteT >= info.Duration) || (emoteStopping && emoteW <= 0.001f && emoteStopT > 0.05f);
            if (done)
            {
                emoteId = EmoteId.None;
                emoteW = 0f;
                emoteStopping = false;
                return;
            }
            if (emoteW <= 0.0005f) return;

            var ep = EmotePose(info, p, emoteT, emoteStopping ? emoteStopT : -1f);
            var keepProp = p.PropR;
            p = CharacterPose.Lerp(p, ep, ActionTimeline.SmoothStep01(emoteW));
            p.PropR = keepProp;
        }

        void FireEmoteBeats(EmoteInfo info, float prev, float now)
        {
            if (EmoteBeatFired == null) return;
            var beats = EmoteTimeline.Beats(emoteId, out var kind);
            if (beats.Length == 0) return;
            if (emoteId == EmoteId.SitDown && emoteStopping) return;
            if (info.Loops)
            {
                float period = info.Period;
                if (Mathf.Floor(now / period) != Mathf.Floor(prev / period)) emoteBeatsFired = 0; // next loop
                float a = Mathf.Repeat(prev, period), b = Mathf.Repeat(now, period);
                if (b < a) a = 0f;
                if (emoteId == EmoteId.SitDown && now > period) return; // only the sit-down itself
                while (emoteBeatsFired < beats.Length && b >= beats[emoteBeatsFired])
                {
                    int i = emoteBeatsFired++;
                    if (beats[i] >= a) Fire(i);
                }
                return;
            }
            while (emoteBeatsFired < beats.Length && now >= beats[emoteBeatsFired])
            {
                int i = emoteBeatsFired++;
                if (beats[i] >= prev - 0.05f) Fire(i);
            }

            void Fire(int i)
            {
                try { EmoteBeatFired?.Invoke(emoteId, kind, i); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        CharacterPose EmotePose(EmoteInfo info, in CharacterPose basePose, float t, float stopT)
        {
            switch (info.Id)
            {
                case EmoteId.Wave: return WavePose(basePose, t);
                case EmoteId.Clap: return ClapPose(basePose, t);
                case EmoteId.Laugh: return LaughPose(basePose, t);
                case EmoteId.Facepalm: return FacepalmPose(basePose, t);
                case EmoteId.Shrug: return ShrugPose(basePose, t);
                case EmoteId.Salute: return SalutePose(basePose, t);
                case EmoteId.Point: return PointPose(basePose, t);
                case EmoteId.DanceDisco: return DiscoPose(basePose, t);
                case EmoteId.DanceRobot: return RobotPose(basePose, t);
                case EmoteId.DanceRoof: return RoofPose(basePose, t);
                case EmoteId.SitDown: return SitFloorPose(basePose, t, stopT);
                case EmoteId.Hug: return HugPose(basePose, t);
                case EmoteId.HighFive: return HighFivePose(basePose, t);
                case EmoteId.FistBump: return FistBumpPose(basePose, t);
                case EmoteId.Handshake: return HandshakePose(basePose, t);
                case EmoteId.PairDance: return PairDancePose(basePose, t);
                default: return basePose;
            }
        }

        // ================================================================== shared helpers

        static readonly Vector3 FootR = new Vector3(0.12f, 0f, 0.01f), FootL = new Vector3(-0.12f, 0f, -0.01f);

        /// <summary>Upright stance to build on: pelvis from the base, both soles planted (re-solved after any pelvis move).</summary>
        static CharacterPose Stance(in CharacterPose b)
        {
            var p = b;
            p.Pelvis = Quaternion.identity;
            p.Torso = Quaternion.identity;
            p.Head = Quaternion.identity;
            p.PelvisPos = new Vector3(0f, BlockyCharacter.HipHeight - 0.005f, 0f);
            return p;
        }

        static void Feet(ref CharacterPose p, Vector3 r, Vector3 l)
        {
            p.SolveLeg(0, r, new Vector3(0.12f, 0f, 1f));
            p.SolveLeg(1, l, new Vector3(-0.12f, 0f, 1f));
        }

        static void Feet(ref CharacterPose p) => Feet(ref p, FootR, FootL);

        /// <summary>Breathing and slow weight shifts on top of any standing emote (never a frozen statue).</summary>
        void Alive(ref CharacterPose p, float t, float amount = 1f)
        {
            float b = Mathf.Sin(t * 2f * Mathf.PI * 0.27f + emoteSeed);
            float n1 = Mathf.PerlinNoise(t * 0.35f, emoteSeed) - 0.5f, n2 = Mathf.PerlinNoise(emoteSeed, t * 0.3f) - 0.5f;
            p.Torso = p.Torso * Quaternion.Euler(-1.3f * b * amount, 3f * n1 * amount, 1.5f * n2 * amount);
            p.Head = p.Head * Quaternion.Euler(1.1f * b * amount + 3f * n2 * amount, 5f * n1 * amount, 0f);
            p.PelvisPos += new Vector3(0.012f * n1, 0.003f * b, 0f) * amount;
        }

        /// <summary>A point in the head's space (origin at the neck pivot, +Z = the face) in root space.</summary>
        static Vector3 HeadPoint(in CharacterPose p, Vector3 local) =>
            p.TorsoPoint(BlockyCharacter.NeckRest) + p.TorsoRoot * (p.Head * local);

        /// <summary>Hand target in torso space with the elbow towards a torso-space direction.</summary>
        static void Arm(ref CharacterPose p, int arm, Vector3 torsoLocal, Vector3 poleLocal) =>
            p.SolveArm(arm, p.TorsoPoint(torsoLocal), p.TorsoRoot * poleLocal);

        /// <summary>Relaxed hanging arm (slightly away from the body).</summary>
        static void Hang(ref CharacterPose p, int arm, float swing = 0f)
        {
            float s = arm == 0 ? 1f : -1f;
            Arm(ref p, arm, new Vector3(0.37f * s, 0.06f, 0.03f + swing), new Vector3(0.3f * s, 0f, -1f));
        }

        /// <summary>0 -> 1 -> 0 between <paramref name="a"/> and <paramref name="b"/> (smooth both ways).</summary>
        static float Hump(float t, float a, float b) => Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - a) / (b - a)));

        static float Env(float t, float a, float b, float c, float d) => Seg(t, a, b) * (1f - Seg(t, c, d));
    }
}
