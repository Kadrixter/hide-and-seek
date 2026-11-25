using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HideAndSeek.Game.Net
{
    /// <summary>Tuning of the toy phones (+ test overrides from the command line).</summary>
    public static class PhoneTuning
    {
        /// <summary>Seconds between the songs of one phone (2026-11-25: rarer, 180-360 s, was 60-150 - there are 4-6 phones now).</summary>
        public const float RingGapMin = 180f, RingGapMax = 360f;
        /// <summary>Phones per round, random (2026-11-25: 4-6 whatever the player count).</summary>
        public const int MinPhones = 4, MaxPhones = 6;
        /// <summary>Volume of the ringtone song (2026-11-25: 20% of the old full volume).</summary>
        public const float RingVolume = 0.2f;
        /// <summary>Ringtone audible range (m) - "around", walls / floors muffle it like every 3D sound.</summary>
        public const float RingRange = 26f;
        public const float StompPromptRange = 1.8f, StompServerRange = 2.6f, StompStandOff = 0.55f;
        public const string RingClipPath = "Phone/imyourlittlebutterfly";

        private static AudioClip _clip;
        private static bool _clipLoaded;

        public static AudioClip RingClip
        {
            get
            {
                if (!_clipLoaded) { _clip = Resources.Load<AudioClip>(RingClipPath); _clipLoaded = true; }
                return _clip;
            }
        }

        /// <summary>Length of the ringtone (the whole song plays unless the phone is smashed).</summary>
        public static float RingLength => RingClip != null ? RingClip.length : 15.12f;

        /// <summary>-phonering a,b: ring gap in seconds (tests), else <see cref="RingGapMin"/>-<see cref="RingGapMax"/> s.</summary>
        public static float NextGap()
        {
            var g = Gap;
            return Random.Range(g.x, g.y);
        }

        private static Vector2? _gap;
        private static Vector2 Gap
        {
            get
            {
                if (_gap.HasValue) return _gap.Value;
                _gap = new Vector2(RingGapMin, RingGapMax);
                string arg = Arg("-phonering");
                if (arg != null)
                {
                    var parts = arg.Split(',');
                    if (parts.Length == 2 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float a) &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float b)) _gap = new Vector2(a, b);
                }
                return _gap.Value;
            }
        }

        /// <summary>-phonecount N: fixed number of phones per round (tests), 0 = by player count.</summary>
        public static int ForcedCount => int.TryParse(Arg("-phonecount"), out int n) ? n : 0;

        private static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }

    /// <summary>
    /// Where toy phones can lie: on the floor next to the racks / cage shelves / site fences that also hold the rods
    /// (<see cref="RodSpots"/>, never in the security rooms), shifted along the object, well apart, on different
    /// levels first, away from the rods of this round.
    /// </summary>
    public static class PhoneSpots
    {
        public struct Spot
        {
            public Vector3 Pos;
            public float Yaw;
            public string Near;
        }

        /// <summary>A phone lying flat: its box must be free, the floor flat and level with the rod spot.</summary>
        private static readonly ItemPlacement.Profile Profile = new ItemPlacement.Profile
        {
            HalfWidth = 0.18f, Height = 0.16f, Headroom = 0f, LosRadius = 0.06f,
        };

        public static List<Spot> Pick(int count, List<Vector3> avoid, float minSpacing = 14f)
        {
            var all = new List<Spot>();
            foreach (var r in RodSpots.Candidates())
                if (TryShift(r, out var s)) all.Add(s);
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            var picked = new List<Spot>();
            for (int pass = 0; pass < 3 && picked.Count < count; pass++)
                foreach (var s in all)
                {
                    if (picked.Count >= count) break;
                    bool ok = true;
                    foreach (var a in avoid) if (Vector3.Distance(a, s.Pos) < 3f) ok = false;
                    foreach (var p in picked)
                    {
                        float d = Vector3.Distance(p.Pos, s.Pos);
                        if (d < (pass == 2 ? 4f : minSpacing)) ok = false;
                        // pass 0: one per level first (levels are 3.4 m apart)
                        if (pass == 0 && Mathf.Abs(p.Pos.y - s.Pos.y) < 1.5f && picked.Count < 3) ok = false;
                    }
                    if (ok) picked.Add(s);
                }
            return picked;
        }

        /// <summary>From a rod spot: a little along the object face, on free and flat floor.</summary>
        private static bool TryShift(RodSpots.Spot r, out Spot spot)
        {
            spot = default;
            Vector3 toObj = r.Top - r.Base;
            toObj.y = 0f;
            if (toObj.sqrMagnitude < 1e-4f) return false;
            toObj.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toObj);
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = r.Base + side * Random.Range(-0.9f, 0.9f) - toObj * Random.Range(0.05f, 0.35f);
                // Inside the walls, nothing between it and the rod spot, flat floor level with it, free (ItemPlacement).
                var why = ItemPlacement.Check(r.Base, r.Base.y, p, Profile, out var floor);
                if (why != ItemPlacement.Reject.None)
                {
                    ItemPlacement.Count(why, "phone spot", p);
                    continue;
                }
                spot = new Spot { Pos = floor + Vector3.up * 0.004f, Yaw = Random.Range(0f, 360f), Near = r.Near };
                return true;
            }
            return false;
        }
    }
}
