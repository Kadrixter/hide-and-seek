using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Pin-up poses on the pouf (8-11), the feet on the floor like the sitting poses (<see cref="SitOn"/>):
    /// 8 femme fatale: legs crossed knee over knee, the top foot hanging pointed, leaning back a little on both hands
    /// on the pouf, chest out; 9 pin-up: knees together slanted to one side, the far hand reaching
    /// across to the near knee, the other on the pouf behind, turned to the room; 10 sunbather: leaning back on both
    /// hands on the pouf, legs long, one knee up, chin up; 11 model: a hand behind the head, the other on the thigh,
    /// knees together slanted the other way.
    /// </summary>
    public static partial class SeatPoses
    {
        delegate CharacterPose LeanRecipe(SeatShape s, float lean, out float bad);

        /// <summary>The first lean (in the given order) at which the recipe fits, else the best one.</summary>
        static CharacterPose FirstFit(SeatShape s, LeanRecipe r, params float[] leans)
        {
            CharacterPose best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float lean in leans)
            {
                var p = r(s, lean, out float bad);
                Trace?.AppendLine($"   lean {lean:0}: bad {bad * 100f:0.0}");
                if (bad <= 0f) return p;
                if (bad < bestBad) { bestBad = bad; best = p; }
            }
            return best;
        }

        /// <summary>Fist on the seat top beside / behind the hip (leaning on it).</summary>
        static float HandOnSeat(ref CharacterPose p, SeatShape s, int arm, float x, float z)
        {
            float side = arm == 0 ? 1f : -1f;
            Vector3 hand = new Vector3(x, s.TopY + 0.11f, z);
            float bad = PlaceHand(ref p, s, arm, hand, new Vector3(side, 0.1f, -0.8f), Vector3.right * side, Vector3.back, Vector3.up, MLegs | MTorso);
            return bad + Reach(p, arm, hand);
        }

        static CharacterPose FemmeFatale2(SeatShape s, float lean, LookLimits lim, out float bad)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(2f, -8f, 0f);
            p.Torso = Quaternion.Euler(lean - 4f, 14f, -2f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.EdgeZ - 0.3f);
            NeutralHead(ref p, lim);
            float legBad = 0f;
            LegsFn legs = (ref CharacterPose q) =>
            {
                Plant(ref q, 1, -0.12f, s.EdgeZ + 0.3f, new Vector3(-0.05f, 0.6f, 1f));
                Plant(ref q, 0, 0.16f, s.EdgeZ + 0.16f, new Vector3(0.1f, 0.6f, 1f)); // placeholder, crossed below
            };
            SitOn(ref p, s, legs, false);
            CrossHigh(ref p, s, out legBad);
            bad = legBad;
            // Leaning back a little on both hands on the pouf behind, chest out (the crossed knee is too high to rest a hand on).
            float back = Mathf.Max(s.RearZ + 0.08f, p.PelvisPos.z - 0.2f);
            bad += HandOnSeat(ref p, s, 0, 0.27f, back);
            bad += HandOnSeat(ref p, s, 1, -0.27f, back);
            return p;
        }

        static CharacterPose PinUp2(SeatShape s, float lean, LookLimits lim, out float bad)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(0f, -10f, 0f);
            p.Torso = Quaternion.Euler(lean, 18f, 3f);
            p.PelvisPos = new Vector3(0.02f, s.TopY + 0.13f, s.EdgeZ - 0.3f);
            NeutralHead(ref p, lim);
            LegsFn legs = (ref CharacterPose q) =>
            {
                Plant(ref q, 0, 0.0f, s.EdgeZ + 0.26f, new Vector3(-0.55f, 0.7f, 1f));
                Plant(ref q, 1, -0.25f, s.EdgeZ + 0.12f, new Vector3(-0.6f, 0.6f, 1f));
            };
            SitOn(ref p, s, legs, false);
            bad = LegCross(p);
            bad += HandOnSeat(ref p, s, 0, 0.3f, p.PelvisPos.z - 0.1f);
            bad += HandOnKnee(ref p, s, 1, 1, 0.08f, MLegs | MTorso | MHead);
            return p;
        }

        static CharacterPose Sunbather2(SeatShape s, float lean, LookLimits lim, out float bad)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(-14f, 0f, 0f);
            p.Torso = Quaternion.Euler(14f - lean, 0f, 0f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.EdgeZ - 0.24f);
            NeutralHead(ref p, lim);
            LegsFn legs = (ref CharacterPose q) =>
            {
                Plant(ref q, 1, -0.1f, s.EdgeZ + 0.55f, new Vector3(-0.1f, 1f, 0.2f));
                Plant(ref q, 0, 0.16f, s.EdgeZ + 0.3f, new Vector3(0.2f, 1f, 0.5f));
            };
            SitOn(ref p, s, legs, false);
            bad = LegCross(p);
            float back = Mathf.Max(s.RearZ + 0.06f, p.PelvisPos.z - 0.3f);
            bad += HandOnSeat(ref p, s, 0, 0.22f, back);
            bad += HandOnSeat(ref p, s, 1, -0.22f, back);
            return p;
        }

        static CharacterPose Model2(SeatShape s, float lean, LookLimits lim, out float bad)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(2f, 10f, 2f);
            p.Torso = Quaternion.Euler(lean, -12f, -7f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.EdgeZ - 0.3f);
            NeutralHead(ref p, lim);
            LegsFn legs = (ref CharacterPose q) =>
            {
                Plant(ref q, 0, 0.27f, s.EdgeZ + 0.12f, new Vector3(0.6f, 0.6f, 1f));
                Plant(ref q, 1, 0.02f, s.EdgeZ + 0.2f, new Vector3(0.55f, 0.7f, 1f));
            };
            SitOn(ref p, s, legs, false);
            bad = LegCross(p);
            // Right fist behind the nape, the elbow up and out; the head turns freely above it.
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
            Quaternion tr = p.TorsoRoot;
            Vector3 nape = neck + tr * new Vector3(0.12f, -0.17f, -0.3f);
            bad += PlaceHand(ref p, s, 0, nape, tr * Dir(1f, 0.6f, -0.3f), tr * Vector3.back, tr * Vector3.right, tr * Vector3.down, MLegs | MTorso | MHead);
            bad += Reach(p, 0, nape) * 0.5f;
            bad += HandOnThigh(ref p, s, 1, 1, 0.55f, 0.06f, MLegs | MTorso);
            return p;
        }
    }
}
