using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Expected contacts of the emotes (for the checks: the editor emote check and the game emote test). A contact is
    /// "at time T, this point of body A touches that point / part of body B"; the gap is measured between the surfaces
    /// of the blocky parts (hand and elbow blocks are 4 px cubes), so 0 = touching, negative = slightly pressed in.
    /// Body 0 = the one who asked (or the solo body), 1 = the partner.
    /// </summary>
    public static class EmoteContacts
    {
        /// <summary>Hand right / left, elbow right / left; anything from <see cref="Box"/> up is a body part box.</summary>
        public const int HandR = 0, HandL = 1, ElbowR = 2, ElbowL = 3, Box = 10;

        public struct Contact
        {
            public float T;
            public int BodyA, PointA, BodyB, PointB;
            public string Label;
        }

        static Contact C(float t, int ba, int pa, int bb, int pb, string label) =>
            new Contact { T = t, BodyA = ba, PointA = pa, BodyB = bb, PointB = pb, Label = label };

        static int Part(BodyPart p) => Box + (int)p;

        public static Contact[] For(EmoteId id)
        {
            float b = EmoteTimeline.PattyBeat;
            switch (id)
            {
                case EmoteId.Clap: return new[] { C(0.6f, 0, HandR, 0, HandL, "clap 1"), C(1.5f, 0, HandR, 0, HandL, "clap 4") };
                case EmoteId.Hug: return new[] { C(2.2f, 0, HandR, 1, Part(BodyPart.Torso), "hand on back"), C(2.2f, 1, HandL, 0, Part(BodyPart.Torso), "partner hand on back") };
                case EmoteId.HighFive: return new[] { C(EmoteTimeline.FiveHit, 0, HandR, 1, HandR, "slap") };
                case EmoteId.Handshake:
                    return new[]
                    {
                        C(EmoteTimeline.ShakeGrip + 0.03f, 0, HandR, 1, HandR, "grip"), C(2.1f, 0, HandR, 1, HandR, "shake"),
                        C(EmoteTimeline.ShakePats[1], 0, HandL, 1, Part(BodyPart.ForearmR), "pat forearm"),
                        C(EmoteTimeline.ShakeThumb, 0, HandR, 1, HandR, "photo grip"),
                    };
                case EmoteId.PairDance:
                    return new[]
                    {
                        C(1.0f, 0, HandR, 1, HandL, "hold A"), C(1.0f, 0, HandL, 1, HandR, "hold B"),
                        C(4.5f, 0, HandR, 1, HandL, "rock step hold"), C(6.9f, 0, HandR, 1, HandL, "circle A"), C(6.9f, 0, HandL, 1, HandR, "circle B"),
                    };
                case EmoteId.BearHug:
                    return new[]
                    {
                        C(2.0f, 0, HandR, 1, Part(BodyPart.Torso), "hug: hand on waist"), C(2.0f, 1, HandR, 0, Part(BodyPart.Torso), "hug: partner hand on back"),
                        C(EmoteTimeline.BearThumb, 0, HandR, 1, Part(BodyPart.Torso), "spin: hand on back"),
                        C(EmoteTimeline.BearThumb, 0, HandL, 1, Part(BodyPart.Torso), "spin: left hand on back"),
                    };
                case EmoteId.SecretShake:
                    return new[]
                    {
                        C(EmoteTimeline.SecretHigh, 0, HandR, 1, HandR, "high five"), C(EmoteTimeline.SecretLow, 0, HandR, 1, HandR, "low five"),
                        C(EmoteTimeline.SecretElbow, 0, ElbowR, 1, ElbowR, "elbows"),
                        C(EmoteTimeline.SecretHip, 0, Part(BodyPart.Torso), 1, Part(BodyPart.Torso), "hips"),
                        C(EmoteTimeline.SecretFist, 0, HandR, 1, HandR, "fist bump"),
                    };
                case EmoteId.PattyCake:
                    return new[]
                    {
                        C(b, 0, HandR, 0, HandL, "own clap A"), C(b, 1, HandR, 1, HandL, "own clap B"),
                        C(2f * b, 0, HandR, 1, HandR, "right hands"), C(4f * b, 0, HandL, 1, HandL, "left hands"),
                        C(EmoteTimeline.PattyPeriod + 2f * b, 0, HandR, 1, HandR, "right hands (2nd loop)"),
                    };
                case EmoteId.Piggyback:
                    return new[]
                    {
                        C(4.0f, 0, HandR, 1, Part(BodyPart.ThighR), "carrier hand under knee"),
                        C(4.0f, 1, HandL, 0, Part(BodyPart.UpperArmL), "rider hand on shoulder"),
                    };
                default: return System.Array.Empty<Contact>();
            }
        }

        /// <summary>World point of a contact point on a character (hand / elbow block centre).</summary>
        public static Vector3 PointOf(BlockyCharacter c, int point)
        {
            switch (point)
            {
                case HandR: return c.ElbowRight.TransformPoint(CharacterPose.HandLocal);
                case HandL: return c.ElbowLeft.TransformPoint(CharacterPose.HandLocal);
                case ElbowR: return c.ElbowRight.position;
                default: return c.ElbowLeft.position;
            }
        }

        /// <summary>Part box of a character in world space.</summary>
        public static PartBox BoxOf(BlockyCharacter c, BodyPart part, PartBox[] buf)
        {
            BodyBoxes.Get(CharacterPose.Capture(c), buf);
            return buf[(int)part].Transformed(c.transform.position, c.transform.rotation);
        }

        const float BlockHalf = 2f * BlockyCharacter.PixelSize;

        /// <summary>Surface gap of contact <paramref name="k"/> between body <paramref name="a"/> (A) and <paramref name="b"/> (B), metres.</summary>
        public static float Gap(in Contact k, BlockyCharacter a, BlockyCharacter b, PartBox[] buf)
        {
            var ca = k.BodyA == 0 ? a : b;
            var cb = k.BodyB == 0 ? a : b;
            if (ca == null || cb == null) return float.NaN;
            if (k.PointA >= Box && k.PointB >= Box)
                return -BodyBoxes.Penetration(BoxOf(ca, (BodyPart)(k.PointA - Box), buf), BoxOf(cb, (BodyPart)(k.PointB - Box), buf));
            Vector3 pa = PointOf(ca, k.PointA);
            if (k.PointB >= Box) return BoxOf(cb, (BodyPart)(k.PointB - Box), buf).Distance(pa) - BlockHalf;
            return Vector3.Distance(pa, PointOf(cb, k.PointB)) - 2f * BlockHalf;
        }
    }
}
