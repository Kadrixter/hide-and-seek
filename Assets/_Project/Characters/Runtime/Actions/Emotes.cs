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
        Shrug = 5,
        Salute = 6,
        Point = 7,
        DanceDisco = 8,
        DanceRobot = 9,
        DanceRoof = 10,
        SitDown = 11,

        Hug = 32,
        HighFive = 33,
        FistBump = 34,
        Handshake = 35,
        PairDance = 36,
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

        public bool Loops => Duration <= 0f;
        public bool IsPair => Emotes.IsPair(Id);
    }

    public static class Emotes
    {
        /// <summary>Order of the slices in the wheel (clockwise from 12 o'clock).</summary>
        public static readonly EmoteId[] Solo =
        {
            EmoteId.Wave, EmoteId.Clap, EmoteId.Laugh, EmoteId.Facepalm, EmoteId.Shrug, EmoteId.Salute,
            EmoteId.Point, EmoteId.DanceDisco, EmoteId.DanceRobot, EmoteId.DanceRoof, EmoteId.SitDown,
        };

        public static readonly EmoteId[] Pair =
        {
            EmoteId.Hug, EmoteId.HighFive, EmoteId.FistBump, EmoteId.Handshake, EmoteId.PairDance,
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
            var t = new EmoteInfo[40];
            void Add(EmoteInfo e) => t[(byte)e.Id] = e;
            Add(new EmoteInfo { Id = EmoteId.Wave, Name = "Machanie", Duration = EmoteTimeline.WaveEnd, ThumbTime = 1.05f });
            Add(new EmoteInfo { Id = EmoteId.Clap, Name = "Oklaski", Duration = EmoteTimeline.ClapEnd, ThumbTime = 1.16f });
            Add(new EmoteInfo { Id = EmoteId.Laugh, Name = "Śmiech", Duration = EmoteTimeline.LaughEnd, ThumbTime = 1.6f });
            Add(new EmoteInfo { Id = EmoteId.Facepalm, Name = "Facepalm", Duration = EmoteTimeline.FacepalmEnd, ThumbTime = 1.5f });
            Add(new EmoteInfo { Id = EmoteId.Shrug, Name = "Wzruszenie ramion", Duration = EmoteTimeline.ShrugEnd, ThumbTime = 0.9f });
            Add(new EmoteInfo { Id = EmoteId.Salute, Name = "Salut", Duration = EmoteTimeline.SaluteEnd, ThumbTime = 1.3f });
            Add(new EmoteInfo { Id = EmoteId.Point, Name = "Pokazanie palcem", Duration = EmoteTimeline.PointEnd, ThumbTime = 0.95f });
            Add(new EmoteInfo { Id = EmoteId.DanceDisco, Name = "Disco", Period = EmoteTimeline.DiscoPeriod, In = 0.35f, Out = 0.35f, ThumbTime = 0.5f });
            Add(new EmoteInfo { Id = EmoteId.DanceRobot, Name = "Robot", Period = EmoteTimeline.RobotPeriod, In = 0.35f, Out = 0.35f, ThumbTime = 0.75f });
            Add(new EmoteInfo { Id = EmoteId.DanceRoof, Name = "Ręce w górę", Period = EmoteTimeline.RoofPeriod, In = 0.35f, Out = 0.35f, ThumbTime = 0.3f });
            Add(new EmoteInfo { Id = EmoteId.SitDown, Name = "Zmęczony", Period = 4.4f, In = 0.12f, Out = 0.2f, Exit = EmoteTimeline.SitExit, ThumbTime = 3f, Standing = false });

            Add(new EmoteInfo { Id = EmoteId.Hug, Name = "Przytulenie", Duration = EmoteTimeline.HugEnd, PairDistance = EmoteTimeline.HugDistance, ThumbTime = 1.9f, Ask = "chce się przytulić" });
            Add(new EmoteInfo { Id = EmoteId.HighFive, Name = "Piątka", Duration = EmoteTimeline.FiveEnd, PairDistance = EmoteTimeline.FiveDistance, ThumbTime = EmoteTimeline.FiveHit + 0.02f, Ask = "chce przybić piątkę" });
            Add(new EmoteInfo { Id = EmoteId.FistBump, Name = "Żółwik", Duration = EmoteTimeline.BumpEnd, PairDistance = EmoteTimeline.BumpDistance, ThumbTime = EmoteTimeline.BumpHit + 0.02f, Ask = "chce przybić żółwika" });
            Add(new EmoteInfo { Id = EmoteId.Handshake, Name = "Uścisk dłoni", Duration = EmoteTimeline.ShakeEnd, PairDistance = EmoteTimeline.ShakeDistance, ThumbTime = 1.1f, Ask = "chce uścisnąć Ci dłoń" });
            Add(new EmoteInfo { Id = EmoteId.PairDance, Name = "Taniec w parze", Period = EmoteTimeline.PairDancePeriod, In = 0.4f, Out = 0.45f, PairDistance = EmoteTimeline.PairDanceDistance, ThumbTime = 2.55f, Ask = "chce z Tobą zatańczyć" });
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

        /// <summary>Robot-style step: holds, then snaps to the next value with a small overshoot and settle.</summary>
        public static float Snap(float x)
        {
            x = Mathf.Clamp01(x);
            if (x >= 1f) return 1f;
            float e = 1f - Mathf.Pow(1f - x, 3f);
            return e + Mathf.Sin(x * Mathf.PI) * 0.12f * (1f - x);
        }
    }
}
