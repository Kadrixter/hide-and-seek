using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Sitting poses with the feet on the floor (the lobby seats are low enough for the rig's short legs, see
    /// <see cref="SeatShape.PoufTopY"/> / <see cref="SeatShape.SofaSeatTopY"/>): the feet are planted first, then the
    /// pelvis settles onto the cushion (thighs resting on it) and, on a sofa, slides back until the back leans on the
    /// back cushion; the feet are re-planted after every move so the shins' lowest edges stay on the floor.
    /// </summary>
    public static partial class SeatPoses
    {
        /// <summary>Feet that stand on the floor in a pose (bit 0 right, bit 1 left; on a mirrored seat the legs swap).</summary>
        public static int PlantedFeet(int v, SeatShape s)
        {
            int m = v == 3 || v == 8 ? 2 : v >= 4 && v <= 7 ? 0 : 3;
            if (s != null && s.MirrorSource != null) m = ((m & 1) << 1) | ((m >> 1) & 1);
            return m;
        }

        /// <summary>Lowest point of a shin (its box corners) in seat space.</summary>
        static float ShinLow(in CharacterPose p, int leg)
        {
            BodyBoxes.Get(p, Boxes);
            var b = Boxes[leg == 0 ? (int)BodyPart.ShinR : (int)BodyPart.ShinL];
            float low = float.MaxValue;
            for (int c = 0; c < 8; c++) low = Mathf.Min(low, b.Point((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f).y);
            return low;
        }

        /// <summary>Foot on the floor at (x, z): the sole is lowered / raised until the shin's lowest edge just touches the floor.</summary>
        static void Plant(ref CharacterPose p, int leg, float x, float z, Vector3 pole, float floor = 0f)
        {
            float y = floor + 0.02f;
            for (int i = 0; i < 4; i++)
            {
                p.SolveLeg(leg, new Vector3(x, y, z), pole);
                y -= ShinLow(p, leg) - (floor + Mathf.Max(0.009f, PlantLift + 0.004f)); // over the solver's floor margin; the final sink brings it down
            }
            p.SolveLeg(leg, new Vector3(x, y, z), pole);
        }

        delegate void LegsFn(ref CharacterPose p);

        /// <summary>How far the solved pose is lowered at the end (see <see cref="Get"/>): planted feet start that much higher.</summary>
        static float PlantLift;

        /// <summary>
        /// Sits the body down: plant the legs, settle the thighs (and <paramref name="restMask"/>) onto the seat, repeat;
        /// on a seat with a back (<paramref name="toBack"/>) then slide back until the torso leans on it.
        /// </summary>
        static void SitOn(ref CharacterPose p, SeatShape s, LegsFn legs, bool toBack, int restMask = MThighs)
        {
            for (int i = 0; i < 4; i++)
            {
                legs(ref p);
                Settle(ref p, s, restMask);
            }
            // Legs reaching far forward slope the thighs down over the front edge, which lifts the hips off the seat:
            // move forward (perch nearer the edge) until the hips are down on the cushion.
            float down = s.TopY + 0.127f + 0.025f;
            for (int i = 0; i < 30 && p.PelvisPos.y > down; i++)
            {
                p.PelvisPos.z += 0.01f;
                legs(ref p);
                Settle(ref p, s, restMask);
            }
            if (!toBack || !s.HasBack) { legs(ref p); return; }
            // Forward while the torso is in the back cushion, then back until it touches (never climbing onto it).
            for (int i = 0; i < 30 && Hits(p, s, MTorso); i++)
            {
                p.PelvisPos.z += 0.01f;
                legs(ref p);
                Settle(ref p, s, restMask);
            }
            for (int i = 0; i < 30; i++)
            {
                var q = p;
                q.PelvisPos.z -= 0.01f;
                legs(ref q);
                Settle(ref q, s, restMask);
                if (Hits(q, s, MTorso | MShins) || q.PelvisPos.y > p.PelvisPos.y + 0.008f) break; // the back on the cushion, or the calves at the front edge
                p = q;
            }
            legs(ref p);
        }

        /// <summary>
        /// Leans the upper body back (1 degree steps, up to <paramref name="max"/>) until the back rests on the backrest /
        /// armrest behind it (the slide back may stop early: the calves at the front edge).
        /// </summary>
        static void LeanToBack(ref CharacterPose p, SeatShape s, float max = 24f)
        {
            for (float a = 1f; a <= max; a += 1f)
            {
                var q = p;
                q.Torso = q.Torso * Quaternion.Euler(-1f, 0f, 0f);
                // The cushions' solids are puffed on every side (conservative by up to 3 cm at their edges): lean into
                // them by that much, so the back really meets the soft cushion.
                if (SeatPenetration(q, s, MTorso) > 0.03f) return;
                p = q;
            }
        }

        /// <summary>A point on top of a thigh: <paramref name="along"/> 0 = hip .. 1 = knee, the fist resting on it.</summary>
        static float HandOnThigh(ref CharacterPose p, SeatShape s, int arm, int leg, float along, float outward, int selfMask)
        {
            Vector3 hip = p.HipPos(leg), knee = BodyBoxes.KneePos(p, leg);
            Vector3 dir = (knee - hip).normalized, up = ThighUp(p, leg);
            float side = arm == 0 ? 1f : -1f;
            Vector3 outDir = Vector3.ProjectOnPlane(Vector3.right * side, dir).normalized;
            Vector3 hand = Vector3.Lerp(hip, knee, along) + up * (0.127f + 0.07f) + outDir * outward;
            return PlaceHand(ref p, s, arm, hand, new Vector3(side * 0.9f, -0.2f, -0.5f), outDir, -dir, up, selfMask);
        }

        // ================================================================== 0: relaxed upright

        /// <summary>Sitting back (sofa: leaning on the back cushion), feet flat a little apart, hands resting on the thighs.</summary>
        static CharacterPose Relaxed(SeatShape s)
        {
            bool back = s.HasBack;
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(back ? -7f : 2f, 0f, 0f);
            p.Torso = Quaternion.Euler(back ? 2f : 3f, 0f, 0f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, back ? s.BackZ + 0.16f : s.EdgeZ - 0.3f);
            float footZ = s.EdgeZ + (back ? 0.2f : 0.22f);
            LegsFn legs = (ref CharacterPose q) =>
            {
                Plant(ref q, 0, 0.17f, footZ, new Vector3(0.15f, 0.6f, 1f));
                Plant(ref q, 1, -0.15f, footZ - 0.03f, new Vector3(-0.15f, 0.6f, 1f));
            };
            SitOn(ref p, s, legs, back);
            if (back) LeanToBack(ref p, s);
            HandOnThigh(ref p, s, 0, 0, 0.62f, 0.02f, MLegs | MTorso);
            HandOnThigh(ref p, s, 1, 1, 0.5f, 0.03f, MLegs | MTorso);
            return p;
        }

        // ================================================================== 1: lounging / casual

        /// <summary>
        /// Sofa: slid forward and sunk back into the cushion, legs long with the heels on the floor, the outer forearm on
        /// the arm roll, the inner hand on the belly. Pouf: leaning back a little on the inner hand, one leg long.
        /// </summary>
        static CharacterPose Lounging(SeatShape s)
        {
            if (!s.HasBack) return Casual(s);
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(-20f, 0f, 0f);
            p.Torso = Quaternion.Euler(-4f, 0f, 0f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.BackZ + 0.24f);
            float footZ = s.EdgeZ + 0.36f;
            LegsFn legs = (ref CharacterPose q) =>
            {
                Plant(ref q, 0, 0.26f, footZ, new Vector3(0.35f, 1f, 0.6f));
                Plant(ref q, 1, -0.25f, footZ + 0.06f, new Vector3(-0.35f, 1f, 0.6f));
            };
            SitOn(ref p, s, legs, true);
            LeanToBack(ref p, s);
            int side = s.ArmSide != 0 ? s.ArmSide : 1;
            int outer = side > 0 ? 0 : 1, inner = 1 - outer;
            if (s.ArmTopY > 0f)
            {
                // Forearm along the arm roll, the fist hanging over its front.
                Vector3 hand = new Vector3(side * (s.ArmInnerX + 0.1f), s.ArmTopY + 0.095f, p.ShoulderPos(outer).z + 0.26f);
                PlaceHand(ref p, s, outer, hand, new Vector3(side, -0.6f, -0.4f), Vector3.right * side, Vector3.forward, Vector3.up, MLegs | MTorso);
            }
            else HandOnThigh(ref p, s, outer, outer, 0.6f, 0.02f, MLegs | MTorso);
            // Inner hand on the cushion beside the hip (the thick arm cannot reach the belly).
            var keep = p;
            if (HandOnThigh(ref p, s, inner, inner, 0.5f, -0.02f, MLegs | MTorso) > 0f)
            {
                p = keep;
                HandOnThigh(ref p, s, inner, inner, 0.7f, 0.02f, MLegs | MTorso);
            }
            return p;
        }

        /// <summary>Pouf: one hand on its knee, the other planted on the pouf beside the hip, leaning a little to it, the other leg long.</summary>
        static CharacterPose Casual(SeatShape s)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(-4f, -6f, 3f);
            p.Torso = Quaternion.Euler(0f, 8f, 4f);
            p.PelvisPos = new Vector3(0.02f, s.TopY + 0.13f, s.EdgeZ - 0.3f);
            LegsFn legs = (ref CharacterPose q) =>
            {
                Plant(ref q, 0, 0.2f, s.EdgeZ + 0.2f, new Vector3(0.2f, 0.6f, 1f));
                Plant(ref q, 1, -0.2f, s.EdgeZ + 0.46f, new Vector3(-0.2f, 1f, 0.5f));
            };
            SitOn(ref p, s, legs, false);
            HandOnKnee(ref p, s, 0, 0, 0.05f, MLegs | MTorso);
            Vector3 beside = new Vector3(-0.36f, s.TopY + 0.12f, p.PelvisPos.z - 0.06f);
            PlaceHand(ref p, s, 1, beside, new Vector3(-0.6f, 0.1f, -1f), Vector3.left, Vector3.back, Vector3.up, MLegs | MTorso);
            return p;
        }

        // ================================================================== 2: elbows on the knees

        static CharacterPose ElbowsOnKnees(SeatShape s)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(8f, 0f, 0f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.HasBack ? s.BackZ + 0.2f : s.EdgeZ - 0.29f);
            LegsFn legs = (ref CharacterPose q) =>
            {
                Plant(ref q, 0, 0.25f, s.EdgeZ + 0.2f, new Vector3(0.6f, 0.6f, 1f));
                Plant(ref q, 1, -0.25f, s.EdgeZ + 0.2f, new Vector3(-0.6f, 0.6f, 1f));
            };
            SitOn(ref p, s, legs, false);
            return ForwardArms(p, s);
        }

        // ================================================================== 3: figure four

        static CharacterPose FigureFourPlanted(SeatShape s)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float lean in new[] { 0f, 4f, -4f, 8f, -8f, -12f })
            {
                bool back = s.HasBack;
                var p = CharacterPose.Identity;
                p.Pelvis = Quaternion.Euler(back ? -8f : -2f, -5f, 0f);
                p.Torso = Quaternion.Euler(lean + (back ? 0f : 4f), 10f, -1f);
                p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, back ? s.BackZ + 0.17f : s.EdgeZ - 0.3f);
                LegsFn legs = (ref CharacterPose q) =>
                {
                    Plant(ref q, 1, -0.12f, s.EdgeZ + 0.18f, new Vector3(-0.1f, 0.6f, 1f));
                    CrossRightLeg(ref q);
                };
                SitOn(ref p, s, legs, back);
                if (back) LeanToBack(ref p, s);
                for (int i = 0; i < 20 && Hits(p, s, MLegs); i++)
                {
                    p.PelvisPos.z += 0.01f;
                    legs(ref p);
                    Settle(ref p, s, MThighs);
                }
                float bad = LegCross(p);
                bad += HandOnKnee(ref p, s, 0, 0, 0.04f, MLegs | MTorso);
                if (s.ArmSide < 0 && s.ArmTopY > 0f)
                {
                    Vector3 hand = new Vector3(-(s.ArmInnerX + 0.1f), s.ArmTopY + 0.08f, p.ShoulderPos(1).z + 0.24f);
                    bad += PlaceHand(ref p, s, 1, hand, new Vector3(-1f, -0.6f, -0.4f), Vector3.left, Vector3.forward, Vector3.up, MLegs | MTorso);
                }
                else
                {
                    Vector3 ankle = BodyBoxes.SolePos(p, 0);
                    Vector3 hand = ankle + new Vector3(-0.02f, 0.127f + 0.07f, -0.05f);
                    bad += PlaceHand(ref p, s, 1, hand, new Vector3(-1f, -0.3f, -0.6f), Vector3.left, Vector3.back, Vector3.up, MLegs | MTorso | MForeR);
                }
                if (bad <= 0f) return p;
                if (bad < bestBad) { bestBad = bad; best = p; }
            }
            return best;
        }
    }
}
