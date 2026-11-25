using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Emote id (network value). 1..31 solo, 32.. together with another player.</summary>
    public enum EmoteId : byte
    {
        None = 0,
        Wave = 1,
        Clap = 2,
        Laugh = 3,
        Facepalm = 4,
        // 5 (shrug), 6 (salute): removed, values reserved.
        Point = 7,
        DanceDisco = 8,
        // 9 (robot dance): removed, value reserved.
        DanceRoof = 10,
        SitDown = 11,
        Floss = 12,
        Breakdance = 13,
        Pushups = 14,

        Hug = 32,
        HighFive = 33,
        // 34 (fist bump): removed, value reserved.
        Handshake = 35,
        PairDance = 36,
        BearHug = 37,
        SecretShake = 38,
        Piggyback = 39,
        PattyCake = 40,
    }

    /// <summary>Static description of an emote.</summary>
    public sealed class EmoteInfo
    {
        public EmoteId Id;
        /// <summary>Name for code / logs only (the wheel shows pictures, no names).</summary>
        public string Name;
        /// <summary>One-shot length in seconds; 0 = loops until stopped.</summary>
        public float Duration;
        /// <summary>Loop period (loops only).</summary>
        public float Period;
        /// <summary>Fade in / fade out of the whole layer (s).</summary>
        public float In = 0.3f, Out = 0.4f;
        /// <summary>Loops with their own way out (getting up from the floor): seconds of that exit after the stop.</summary>
        public float Exit;
        /// <summary>Distance between the two roots (pair emotes).</summary>
        public float PairDistance;
        /// <summary>Moment shown in the wheel's picture.</summary>
        public float ThumbTime;
        /// <summary>Pair: text after the nick in the consent prompt ("chce się przytulić").</summary>
        public string Ask;
        /// <summary>The feet stay in place (WASD stops it); false = the body is on the floor (movement waits for the exit).</summary>
        public bool Standing = true;
        /// <summary>Loops: contact beats only in the first period (the way in, e.g. sitting down), not every loop.</summary>
        public bool BeatsOnce;

        public bool Loops => Duration <= 0f;
        public bool IsPair => Emotes.IsPair(Id);
    }

    public static class Emotes
    {
        /// <summary>Order of the slices in the wheel (clockwise from 12 o'clock).</summary>
        public static readonly EmoteId[] Solo =
        {
            EmoteId.Wave, EmoteId.Clap, EmoteId.Laugh, EmoteId.Facepalm, EmoteId.Point, EmoteId.Pushups,
            EmoteId.DanceDisco, EmoteId.DanceRoof, EmoteId.Floss, EmoteId.Breakdance, EmoteId.SitDown,
        };

        public static readonly EmoteId[] Pair =
        {
            EmoteId.Hug, EmoteId.BearHug, EmoteId.HighFive, EmoteId.SecretShake, EmoteId.Handshake, EmoteId.PattyCake,
            EmoteId.PairDance, EmoteId.Piggyback,
        };

        /// <summary>Furthest a partner may stand when asking (eye to eye, metres).</summary>
        public const float AskRange = 2.5f;

        public static bool IsPair(EmoteId id) => (byte)id >= 32;

        static readonly EmoteInfo[] table = Build();

        public static EmoteInfo Get(EmoteId id)
        {
            int i = (byte)id;
            return i < table.Length ? table[i] : null;
        }

        static EmoteInfo[] Build()
        {
            var t = new EmoteInfo[48];
            void Add(EmoteInfo e) => t[(byte)e.Id] = e;
            Add(new EmoteInfo { Id = EmoteId.Wave, Name = "Machanie", Duration = EmoteTimeline.WaveEnd, ThumbTime = 1.05f });
            Add(new EmoteInfo { Id = EmoteId.Clap, Name = "Oklaski", Duration = EmoteTimeline.ClapEnd, ThumbTime = 1.16f });
            Add(new EmoteInfo { Id = EmoteId.Laugh, Name = "Śmiech", Duration = EmoteTimeline.LaughEnd, ThumbTime = 1.6f });
            Add(new EmoteInfo { Id = EmoteId.Facepalm, Name = "Facepalm", Duration = EmoteTimeline.FacepalmEnd, ThumbTime = 1.5f });
            Add(new EmoteInfo { Id = EmoteId.Point, Name = "Pokazanie palcem", Duration = EmoteTimeline.PointEnd, ThumbTime = 0.95f });
            Add(new EmoteInfo { Id = EmoteId.DanceDisco, Name = "Disco", Period = EmoteTimeline.DiscoPeriod, In = 0.35f, Out = 0.35f, ThumbTime = 0.5f });
            Add(new EmoteInfo { Id = EmoteId.DanceRoof, Name = "Ręce w górę", Period = EmoteTimeline.RoofPeriod, In = 0.35f, Out = 0.35f, ThumbTime = 0.3f });
            Add(new EmoteInfo { Id = EmoteId.SitDown, Name = "Zmęczony", Period = 4.4f, In = 0.12f, Out = 0.2f, Exit = EmoteTimeline.SitExit, ThumbTime = 3f, Standing = false, BeatsOnce = true });
            Add(new EmoteInfo { Id = EmoteId.Floss, Name = "Floss", Period = EmoteTimeline.FlossPeriod, In = 0.3f, Out = 0.35f, ThumbTime = EmoteTimeline.FlossThumb });
            Add(new EmoteInfo { Id = EmoteId.Breakdance, Name = "Breakdance", Period = EmoteTimeline.BreakPeriod, In = 0.2f, Out = 0.3f, Exit = EmoteTimeline.BreakExit, ThumbTime = EmoteTimeline.BreakThumb, Standing = false });
            Add(new EmoteInfo { Id = EmoteId.Pushups, Name = "Pompki", Duration = EmoteTimeline.PushEnd, In = 0.2f, Out = 0.5f, Exit = EmoteTimeline.PushExit, ThumbTime = EmoteTimeline.PushThumb, Standing = false });

            Add(new EmoteInfo { Id = EmoteId.Hug, Name = "Przytulenie", Duration = EmoteTimeline.HugEnd, PairDistance = EmoteTimeline.HugDistance, ThumbTime = 1.9f, Ask = "chce się przytulić" });
            Add(new EmoteInfo { Id = EmoteId.HighFive, Name = "Piątka", Duration = EmoteTimeline.FiveEnd, PairDistance = EmoteTimeline.FiveDistance, ThumbTime = EmoteTimeline.FiveHit + 0.02f, Ask = "chce przybić piątkę" });
            Add(new EmoteInfo { Id = EmoteId.Handshake, Name = "Uścisk prezesa", Duration = EmoteTimeline.ShakeEnd, PairDistance = EmoteTimeline.ShakeDistance, ThumbTime = EmoteTimeline.ShakeThumb, Ask = "zaprasza Cię na uścisk prezesa" });
            Add(new EmoteInfo { Id = EmoteId.PairDance, Name = "Taniec w parze", Period = EmoteTimeline.PairDancePeriod, In = 0.4f, Out = 0.45f, PairDistance = EmoteTimeline.PairDanceDistance, ThumbTime = EmoteTimeline.PairDanceThumb, Ask = "chce z Tobą zatańczyć" });
            Add(new EmoteInfo { Id = EmoteId.BearHug, Name = "Niedźwiadek", Duration = EmoteTimeline.BearEnd, Out = 0.5f, PairDistance = EmoteTimeline.BearDistance, ThumbTime = EmoteTimeline.BearThumb, Ask = "chce Cię złapać w niedźwiadka i zakręcić" });
            Add(new EmoteInfo { Id = EmoteId.SecretShake, Name = "Tajny uścisk dłoni", Duration = EmoteTimeline.SecretEnd, PairDistance = EmoteTimeline.SecretDistance, ThumbTime = EmoteTimeline.SecretThumb, Ask = "chce Ci pokazać tajny uścisk dłoni" });
            Add(new EmoteInfo { Id = EmoteId.Piggyback, Name = "Na barana", Period = EmoteTimeline.PiggyPeriod, In = 0.25f, Out = 0.3f, Exit = EmoteTimeline.PiggyExit, PairDistance = EmoteTimeline.PiggyDistance, ThumbTime = EmoteTimeline.PiggyThumb, Standing = false, BeatsOnce = true, Ask = "chce Cię wziąć na barana" });
            Add(new EmoteInfo { Id = EmoteId.PattyCake, Name = "Łapki", Period = EmoteTimeline.PattyPeriod, In = 0.35f, Out = 0.35f, PairDistance = EmoteTimeline.PattyDistance, ThumbTime = EmoteTimeline.PattyThumb, Ask = "chce zagrać z Tobą w łapki" });
            return t;
        }
    }

    /// <summary>
    /// Key tracks for the procedural emotes: points / values at times, sampled with a Catmull-Rom spline (smooth
    /// velocity through every key, flat at both ends). Keys are Vector4 (x, y, z, time) or Vector2 (time, value).
    /// </summary>
    public static class EmoteKeys
    {
        public static Vector3 Sample(Vector4[] k, float t)
        {
            int n = k.Length;
            if (t <= k[0].w) return k[0];
            if (t >= k[n - 1].w) return k[n - 1];
            int i = 0;
            while (i < n - 2 && t > k[i + 1].w) i++;
            Vector4 a = k[i], b = k[i + 1];
            Vector4 pa = i > 0 ? k[i - 1] : a, pb = i + 2 < n ? k[i + 2] : b;
            float h = b.w - a.w;
            float u = Mathf.Clamp01((t - a.w) / Mathf.Max(1e-4f, h));
            // Tangents scaled to this segment's length (non-uniform Catmull-Rom).
            Vector3 m0 = i > 0 ? (Vector3)(b - pa) / Mathf.Max(1e-4f, b.w - pa.w) * h : Vector3.zero;
            Vector3 m1 = i + 2 < n ? (Vector3)(pb - a) / Mathf.Max(1e-4f, pb.w - a.w) * h : Vector3.zero;
            float u2 = u * u, u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * (Vector3)a + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * (Vector3)b + (u3 - u2) * m1;
        }

        public static float Sample(Vector2[] k, float t)
        {
            int n = k.Length;
            if (t <= k[0].x) return k[0].y;
            if (t >= k[n - 1].x) return k[n - 1].y;
            int i = 0;
            while (i < n - 2 && t > k[i + 1].x) i++;
            Vector2 a = k[i], b = k[i + 1];
            Vector2 pa = i > 0 ? k[i - 1] : a, pb = i + 2 < n ? k[i + 2] : b;
            float h = b.x - a.x;
            float u = Mathf.Clamp01((t - a.x) / Mathf.Max(1e-4f, h));
            float m0 = i > 0 ? (b.y - pa.y) / Mathf.Max(1e-4f, b.x - pa.x) * h : 0f;
            float m1 = i + 2 < n ? (pb.y - a.y) / Mathf.Max(1e-4f, pb.x - a.x) * h : 0f;
            float u2 = u * u, u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * a.y + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * b.y + (u3 - u2) * m1;
        }

    }
}
