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
            if (info.BeatsOnce && emoteStopping) return;
            if (info.Loops)
            {
                float period = info.Period;
                if (Mathf.Floor(now / period) != Mathf.Floor(prev / period)) emoteBeatsFired = 0; // next loop
                float a = Mathf.Repeat(prev, period), b = Mathf.Repeat(now, period);
                if (b < a) a = 0f;
                if (info.BeatsOnce && now > period) return; // only the way in (sitting down, jumping on)
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
                try { EmoteBeatFired?.Invoke(emoteId, EmoteTimeline.BeatKind(emoteId, i, kind), i); }
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
                case EmoteId.Point: return PointPose(basePose, t);
                case EmoteId.DanceDisco: return DiscoPose(basePose, t);
                case EmoteId.DanceRoof: return RoofPose(basePose, t);
                case EmoteId.SitDown: return SitFloorPose(basePose, t, stopT);
                case EmoteId.Floss: return FlossPose(basePose, t);
                case EmoteId.Breakdance: return BreakdancePose(basePose, t, stopT);
                case EmoteId.Pushups: return PushupsPose(basePose, t, stopT);
                case EmoteId.Hug: return HugPose(basePose, t);
                case EmoteId.HighFive: return HighFivePose(basePose, t);
                case EmoteId.Handshake: return HandshakePose(basePose, t);
                case EmoteId.PairDance: return PairDancePose(basePose, t);
                case EmoteId.BearHug: return BearHugPose(basePose, t);
                case EmoteId.SecretShake: return SecretShakePose(basePose, t);
                case EmoteId.Piggyback: return PiggybackPose(basePose, t, stopT);
                case EmoteId.PattyCake: return PattyCakePose(basePose, t);
                default: return basePose;
            }
        }
    }
}
