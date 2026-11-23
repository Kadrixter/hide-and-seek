using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seated poses fitted to the actual seat (<see cref="SeatShape"/>), in seat space (origin on the floor at the
    /// seat point, +Z = facing direction). Every body is built from a recipe and then solved against the seat's
    /// solids: the pelvis is lowered until the thighs rest on the cushion, moved forward until the hanging shins
    /// clear the front of the seat, slid back until the back (and the head over its whole look range) touches the
    /// backrest, and every hand is placed at the nearest spot where the arm is clear of the furniture and does not
    /// sink into the own legs / torso. The look range is solved too (<see cref="ClampLook"/>): per head yaw, how far
    /// down the eyes may go before they come too close to the own shoulders / chest (first person: the camera must
    /// never look into the own body) and how far up before the head touches the backrest. Results are cached per
    /// seat shape and variant; only the head changes per frame.
    /// <para>
    /// The blocky rig has short legs (hip 0.675 m) and arms (fist 0.51 m below the shoulder): on the lobby sofa
    /// (cushion top 0.53-0.56 m) and pouf (0.42-0.46 m) the thighs have to rest on top of the cushion, so the feet
    /// hang above the floor instead of being pushed through the seat.
    /// </para>
    /// Variants: 0 upright, leaning a little forward, hands on the knees; 1 sofa: lounging against the backrest,
    /// outer forearm on the armrest, inner hand on the hip, legs stretched out / pouf: casual, right hand on the
    /// knee, left hand on the hip, left leg out; 2 bent forward, elbows on the knees, hands together; 3 right
    /// ankle on the left knee (figure four). Pin-up poses (chosen from the pose wheel, see SitPoseCatalog and
    /// SeatPoses.Glamour.cs): 4-7 sofa only (4-6 lie along the cushions and need the neighbouring seat free),
    /// 8-11 pouf only.
    /// <para>
    /// The rig's 25 cm thick arms rule out folded arms or hands in the lap (the forearm would have to pass through
    /// the belly); hands go where the arm can reach from outside: knees, hips, armrest.
    /// </para>
    /// </summary>
    public static partial class SeatPoses
    {
        public const int Count = 12;

        /// <summary>Poses picked at random when sitting down (the rest only from the pose wheel).</summary>
        public const int RandomCount = 4;

        /// <summary>
        /// Bump when a recipe, the solver or a seat shape changes: baked results (SeatPoses.Baked.cs, written by the
        /// editor seat check) of another version are ignored and the poses are solved at runtime instead.
        /// </summary>
        public const int SolverVersion = 4;

        /// <summary>Use the baked results when they match <see cref="SolverVersion"/> (the editor check turns this off to solve afresh).</summary>
        public static bool UseBaked = true;

        /// <summary>Camera (eyes) relative to the neck pivot, in head space, seated: just behind the face.</summary>
        public static readonly Vector3 EyeOffset = new Vector3(0f, 0.255f, 0.1f);
        /// <summary>Same for a standing player (the regular first-person eye height 1.62 m, centre of the head).</summary>
        public static readonly Vector3 StandEyeOffset = new Vector3(0f, 1.62f - 24f * BlockyCharacter.PixelSize, 0f);

        /// <summary>Closest the first-person eye may come to the own (visible) body: near clip 3 cm + its half diagonal + margin.</summary>
        public const float EyeClearance = 0.08f;

        /// <summary>
        /// Look range of a pose: head yaw within <see cref="Center"/> +- <see cref="Yaw"/> (relative to the seat), pitch
        /// between -<see cref="Down"/> and <see cref="Up"/>; the neutral look is (<see cref="Center"/>, <see cref="Pitch"/>).
        /// <see cref="Roll"/>: the head is tilted by this much (degrees, + = top to the left) on top of the look (the
        /// camera does not roll, only the eye moves with the head).
        /// </summary>
        public struct LookLimits
        {
            public float Yaw, Up, Down, Center, Pitch, Roll;
            public LookLimits(float yaw, float up, float down, float center = 0f, float pitch = 0f, float roll = 0f)
            {
                Yaw = yaw; Up = up; Down = down; Center = center; Pitch = pitch; Roll = roll;
            }
        }

        const float Margin = 0.008f;      // clearance kept from the (already conservative) seat solids
        const float SelfAllow = 0.01f;    // arms may sink this much into the own clothes (both have a 1.4 cm layer)
        const float HeadAllow = 0.015f;   // the head may touch a fist / forearm this much while looking around

        const int MTorso = 1 << 0, MHead = 1 << 1, MUpperR = 1 << 2, MForeR = 1 << 3, MUpperL = 1 << 4, MForeL = 1 << 5;
        const int MThighR = 1 << 6, MShinR = 1 << 7, MThighL = 1 << 8, MShinL = 1 << 9;
        const int MThighs = MThighR | MThighL, MShins = MShinR | MShinL, MLegs = MThighs | MShins;

        const float TableStep = 5f;       // look table: one entry per 5 degrees of head yaw, -90..90
        const int TableSize = 37;

        sealed class Entry
        {
            public int Variant;
            public CharacterPose Body;
            public LookLimits Limits;
            public float[] Up, Down;
        }

        static readonly Dictionary<int, Entry> Cache = new Dictionary<int, Entry>();
        static readonly PartBox[] Boxes = new PartBox[BodyBoxes.Count];

        // ================================================================== public API

        /// <summary>Body of the pose (head straight, relative to the torso) in seat space.</summary>
        public static CharacterPose Body(int variant, SeatShape shape) => Get(variant, shape).Body;

        /// <summary>Full pose: body plus the head looking at (yaw relative to the seat, pitch up +), clamped.</summary>
        public static CharacterPose Evaluate(int variant, SeatShape shape, float yaw, float pitch)
        {
            var e = Get(variant, shape);
            var p = e.Body;
            ClampLook(variant, shape, ref yaw, ref pitch);
            p.Head = Quaternion.Inverse(p.TorsoRoot) * HeadRot(e.Limits, yaw, pitch);
            return p;
        }

        /// <summary>The pose can be used on this seat (sofa poses on sofas, pouf poses on poufs, 0-3 everywhere).</summary>
        public static bool Supports(SeatShape shape, int variant)
        {
            if (variant < 0 || variant >= Count) return false;
            if (variant < RandomCount) return true;
            if (shape == null) return false;
            if (variant <= 7) return shape.Kind == SeatKind.Sofa && shape.HasBack && shape.ArmSide != 0 && (variant == 7 || shape.Wide != null);
            return shape.Kind == SeatKind.Pouf;
        }

        /// <summary>The pose lies along the sofa over the neighbouring seat too (that seat must be free).</summary>
        public static bool SpansNeighbour(int variant) => variant >= 4 && variant <= 6;

        /// <summary>The body lies (longer blends in and out, the pelvis lifts while changing poses).</summary>
        public static bool Lying(int variant) => SpansNeighbour(variant);

        /// <summary>How long changing from one pose to another on the same seat takes (seconds).</summary>
        public static float SwitchTime(int from, int to) =>
            Lying(from) && Lying(to) ? 0.85f : Lying(from) || Lying(to) ? 1.0f : 0.6f;

        /// <summary>How far the pelvis lifts half way through a pose change (clears the cushion while the legs swing round).</summary>
        public static float SwitchLift(int from, int to) => Lying(from) || Lying(to) ? 0.07f : 0.035f;

        /// <summary>
        /// Clamps a look (head yaw relative to the seat, pitch up +) to what the pose allows: the yaw to the pose's
        /// range, the pitch to the solved range at that yaw.
        /// </summary>
        public static void ClampLook(int variant, SeatShape shape, ref float yaw, ref float pitch)
        {
            var e = Get(variant, shape);
            float c = e.Limits.Center;
            yaw = Mathf.Clamp(yaw, c - e.Limits.Yaw, c + e.Limits.Yaw);
            float f = Mathf.Clamp((yaw - c + 90f) / TableStep, 0f, TableSize - 1.001f);
            int i = (int)f;
            float t = f - i;
            float up = Mathf.Lerp(e.Up[i], e.Up[i + 1], t), down = Mathf.Lerp(e.Down[i], e.Down[i + 1], t);
            pitch = Mathf.Clamp(pitch, -down, up);
        }

        /// <summary>Outer look range of the pose (the pitch range shrinks further with the yaw, see <see cref="ClampLook"/>).</summary>
        public static LookLimits Limits(int variant, SeatShape shape) => Get(variant, shape).Limits;

        /// <summary>Neck pivot of the pose in seat space.</summary>
        public static Vector3 Neck(int variant, SeatShape shape) => Get(variant, shape).Body.TorsoPoint(BlockyCharacter.NeckRest);

        /// <summary>Eyes (first-person camera) in seat space, look clamped.</summary>
        public static Vector3 Eye(int variant, SeatShape shape, float yaw, float pitch)
        {
            ClampLook(variant, shape, ref yaw, ref pitch);
            return Neck(variant, shape) + HeadRot(Limits(variant, shape), yaw, pitch) * EyeOffset;
        }

        static Quaternion LookRot(float yaw, float pitch) => Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-pitch, 0f, 0f);

        /// <summary>Head rotation in seat space for a look: the look plus the pose's head tilt.</summary>
        static Quaternion HeadRot(in LookLimits lim, float yaw, float pitch) =>
            lim.Roll == 0f ? LookRot(yaw, pitch) : LookRot(yaw, pitch) * Quaternion.Euler(0f, 0f, lim.Roll);

        static Entry Get(int variant, SeatShape shape)
        {
            if (shape == null) shape = SeatShape.Generic();
            variant = Mathf.Clamp(variant, 0, Count - 1);
            if (!Supports(shape.MirrorSource ?? shape, variant)) variant = 0;
            int key = shape.Id * 16 + variant;
            if (Cache.TryGetValue(key, out var e)) return e;
            if (shape.MirrorSource == null && UseBaked && TryBaked(key, out e))
            {
                Cache[key] = e;
                return e;
            }
            if (shape.MirrorSource != null)
            {
                // The seat is the mirror image of another one (sofa seat with the armrest on the left): same pose, mirrored.
                var src = Get(variant, shape.MirrorSource);
                var lim = src.Limits;
                lim.Center = -lim.Center;
                lim.Roll = -lim.Roll;
                e = new Entry { Limits = lim, Body = Mirror(src.Body), Up = new float[TableSize], Down = new float[TableSize] };
                for (int i = 0; i < TableSize; i++)
                {
                    e.Up[i] = src.Up[TableSize - 1 - i];
                    e.Down[i] = src.Down[TableSize - 1 - i];
                }
                Cache[key] = e;
                return e;
            }
            e = new Entry { Variant = variant, Limits = NominalLimits(variant, shape) };
            Trace?.AppendLine($"  solving {shape.Kind} (id {shape.Id}) pose {variant}");
            // Lying along the sofa: solved against the whole sofa (the neighbouring seat is free then).
            var solve = SpansNeighbour(variant) && shape.Wide != null ? shape.Wide : shape;
            float sink = solve.Sink + (variant == 4 || variant == 7 ? 0.008f : variant == 5 ? -0.006f : variant == 6 ? 0.02f : variant == 8 || variant == 10 ? 0.008f : 0f); // measured contact gaps (editor seat check)
            PlantLift = sink;
            e.Body = Build(variant, solve, e.Limits);
            // Down onto the real cushion (the solids are conservative), see SeatShape.Sink; planted feet were placed that much higher.
            e.Body.PelvisPos.y -= sink;
            SolveLookTable(e, solve);
            Cache[key] = e;
            return e;
        }

        /// <summary>Forgets every solved pose (the editor check re-solves after changing <see cref="UseBaked"/>).</summary>
        public static void ClearCache() => Cache.Clear();

        // ------------------------------------------------------------------ baking

        const int BakedStride = 2 + 3 + 12 * 4 + 6 + 2 * TableSize;
        static Dictionary<int, Entry> _baked;

        static bool TryBaked(int key, out Entry e)
        {
            e = null;
            if (BakedVersion != SolverVersion || BakedData == null) return false;
            if (_baked == null)
            {
                _baked = new Dictionary<int, Entry>();
                for (int o = 0; o + BakedStride <= BakedData.Length; o += BakedStride) _baked[(int)BakedData[o]] = Read(BakedData, o);
            }
            return _baked.TryGetValue(key, out e);
        }

        static Entry Read(float[] d, int o)
        {
            int i = o + 2;
            Quaternion Q() { var q = new Quaternion(d[i], d[i + 1], d[i + 2], d[i + 3]); i += 4; return q; }
            var p = new CharacterPose { PelvisPos = new Vector3(d[i], d[i + 1], d[i + 2]) };
            i += 3;
            p.Pelvis = Q(); p.Torso = Q(); p.Head = Q();
            p.ShoulderR = Q(); p.ElbowR = Q(); p.ShoulderL = Q(); p.ElbowL = Q();
            p.HipR = Q(); p.KneeR = Q(); p.HipL = Q(); p.KneeL = Q(); p.PropR = Q();
            var e = new Entry { Body = p, Limits = new LookLimits(d[i], d[i + 1], d[i + 2], d[i + 3], d[i + 4], d[i + 5]), Up = new float[TableSize], Down = new float[TableSize] };
            i += 6;
            for (int k = 0; k < TableSize; k++) e.Up[k] = d[i++];
            for (int k = 0; k < TableSize; k++) e.Down[k] = d[i++];
            return e;
        }

        /// <summary>
        /// Editor: the solved result of one seat shape / variant as baked numbers (key, version, pose, limits, look table),
        /// the format SeatPoses.Baked.cs stores.
        /// </summary>
        public static float[] Bake(int variant, SeatShape shape)
        {
            var e = Get(variant, shape);
            var d = new List<float> { shape.Id * 16 + variant, SolverVersion };
            var p = e.Body;
            d.Add(p.PelvisPos.x); d.Add(p.PelvisPos.y); d.Add(p.PelvisPos.z);
            foreach (var q in new[] { p.Pelvis, p.Torso, p.Head, p.ShoulderR, p.ElbowR, p.ShoulderL, p.ElbowL, p.HipR, p.KneeR, p.HipL, p.KneeL, p.PropR })
            {
                d.Add(q.x); d.Add(q.y); d.Add(q.z); d.Add(q.w);
            }
            d.Add(e.Limits.Yaw); d.Add(e.Limits.Up); d.Add(e.Limits.Down);
            d.Add(e.Limits.Center); d.Add(e.Limits.Pitch); d.Add(e.Limits.Roll);
            d.AddRange(e.Up);
            d.AddRange(e.Down);
            return d.ToArray();
        }

        static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        /// <summary>The pose mirrored left / right (x -> -x, right and left limbs swapped).</summary>
        static CharacterPose Mirror(in CharacterPose p) => new CharacterPose
        {
            PelvisPos = new Vector3(-p.PelvisPos.x, p.PelvisPos.y, p.PelvisPos.z),
            Pelvis = Mirror(p.Pelvis), Torso = Mirror(p.Torso), Head = Mirror(p.Head),
            ShoulderR = Mirror(p.ShoulderL), ElbowR = Mirror(p.ElbowL), ShoulderL = Mirror(p.ShoulderR), ElbowL = Mirror(p.ElbowR),
            HipR = Mirror(p.HipL), KneeR = Mirror(p.KneeL), HipL = Mirror(p.HipR), KneeL = Mirror(p.KneeR),
            PropR = p.PropR
        };

        /// <summary>Outer look range per pose: the neck stays believable (bent forward: little up, lounging: little down).</summary>
        static LookLimits NominalLimits(int variant, SeatShape shape)
        {
            bool lounge = variant == 1 && shape.Kind == SeatKind.Sofa && shape.HasBack;
            switch (variant)
            {
                case 1: return lounge ? new LookLimits(70f, 30f, 45f) : new LookLimits(75f, 40f, 50f);
                case 2: return new LookLimits(65f, 22f, 55f);
                case 0: case 3: return new LookLimits(75f, 40f, 55f);
                default: return GlamourLimits(variant);
            }
        }

        // ================================================================== recipes

        static CharacterPose Build(int v, SeatShape s, LookLimits lim)
        {
            if (s.Office && v == 0) return Office(s, lim);
            switch (v)
            {
                case 1: return Lounging(s);
                case 2: return ElbowsOnKnees(s);
                case 3: return FigureFourPlanted(s);
                case 0: return Relaxed(s);
                default: return BuildGlamour(v, s, lim);
            }
        }

        delegate CharacterPose Recipe(SeatShape s, float lean, out float bad);

        /// <summary>
        /// The lean (degrees forward) nearest to <paramref name="from"/> (first) within [<paramref name="lo"/>, <paramref name="to"/>]
        /// at which the recipe's arms fit (the arms are short: leaning brings the shoulders over the knees).
        /// </summary>
        static CharacterPose BestLean(SeatShape s, Recipe recipe, float from, float to, float lo = float.NaN)
        {
            if (float.IsNaN(lo)) lo = from;
            var leans = new List<float>();
            for (float l = lo; l <= to + 0.01f; l += 2f) leans.Add(l);
            leans.Sort((a, b) => Mathf.Abs(a - from).CompareTo(Mathf.Abs(b - from)));
            CharacterPose best = default;
            float bestBad = float.MaxValue;
            foreach (float lean in leans)
            {
                Trace?.AppendLine($"   lean {lean:0}");
                var p = recipe(s, lean, out float bad);
                if (bad <= 0f) return p;
                if (bad < bestBad) { bestBad = bad; best = p; }
            }
            return best;
        }

        /// <summary>Bent forward over the (already placed) legs, elbows on the knees, hands loosely together.</summary>
        static CharacterPose ForwardArms(CharacterPose p, SeatShape s)
        {

            // Leans at which both upper arms reach the elbows' resting spots on the knees, best first.
            var elbow = new Vector3[2];
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? 1f : -1f;
                elbow[arm] = BodyBoxes.KneePos(p, arm) + new Vector3(-side * 0.03f, 0.127f + 0.118f, -0.075f);
            }
            var leans = new List<Vector2>();
            for (float lean = 10f; lean <= 55f; lean += 1f)
            {
                p.Torso = Quaternion.Euler(lean - 8f, 0f, 0f);
                float err = 0f;
                for (int arm = 0; arm < 2; arm++) err += Mathf.Abs((p.ShoulderPos(arm) - elbow[arm]).magnitude - RigIK.UpperArm);
                leans.Add(new Vector2(lean, err));
            }
            leans.Sort((a, b) => a.y.CompareTo(b.y));

            CharacterPose best = p;
            float bestBad = float.MaxValue;
            for (int n = 0; n < 10 && n < leans.Count; n++)
            {
                var q = p;
                q.Torso = Quaternion.Euler(leans[n].x - 8f, 0f, 0f);
                float bad = 0f;
                for (int arm = 0; arm < 2; arm++)
                {
                    float side = arm == 0 ? 1f : -1f;
                    Vector3 sh = q.ShoulderPos(arm);
                    Vector3 e = sh + (elbow[arm] - sh).normalized * RigIK.UpperArm;
                    // Forearms hang forward and in, the fists meet in front of the knees.
                    Vector3 hand = e + new Vector3(-side * 0.3f, -0.55f, 0.78f).normalized * RigIK.Forearm;
                    bad += PlaceHand(ref q, s, arm, hand, e - (sh + hand) * 0.5f, Vector3.forward, Vector3.down, Vector3.right * side,
                        MLegs | MTorso | (arm == 0 ? MForeL : 0));
                }
                if (bad <= 0f) return q;
                if (bad < bestBad) { bestBad = bad; best = q; }
            }
            return best;
        }

        /// <summary>Right thigh turned out to the side, the shin across to the top of the left knee.</summary>
        static void CrossRightLeg(ref CharacterPose p)
        {
            Vector3 hip = p.HipPos(0);
            Vector3 lk = BodyBoxes.KneePos(p, 1);
            // The ankle rests on top of the left knee (both 25 cm thick), a little to its outside; raised until the
            // shin is clear of the thigh under it.
            for (float lift = 0.245f; lift < 0.36f; lift += 0.01f)
            {
                Vector3 sole = lk + new Vector3(-0.04f, lift, 0.01f);
                // Knee: thigh length from the hip and shin length from the ankle, out to the right.
                Vector3 mid = (hip + sole) * 0.5f;
                Vector3 axis = (sole - hip).normalized;
                float d = Mathf.Min((sole - hip).magnitude, RigIK.Thigh + RigIK.Shin - 0.01f);
                float h = Mathf.Sqrt(Mathf.Max(0f, RigIK.Thigh * RigIK.Thigh - d * d * 0.25f));
                Vector3 outDir = Vector3.ProjectOnPlane(new Vector3(1f, 0.35f, 0.25f), axis).normalized;
                Vector3 knee = mid + outDir * h;
                p.SolveLeg(0, sole, knee - mid);
                if (LegCross(p) <= 0f) break;
            }
        }

        // ================================================================== limbs

        /// <summary>Sets a leg by directions: thigh turned out / dropped (degrees), shin forward / out from vertical.</summary>
        static void Leg(ref CharacterPose p, int leg, float outYaw, float drop, float shinFwd, float shinOut)
        {
            float side = leg == 0 ? 1f : -1f;
            Vector3 thigh = Quaternion.Euler(drop, side * outYaw, 0f) * Vector3.forward;
            float a = shinFwd * Mathf.Deg2Rad;
            Vector3 shin = Quaternion.Euler(0f, side * shinOut, 0f) * new Vector3(0f, -Mathf.Cos(a), Mathf.Sin(a));
            Vector3 hip = p.HipPos(leg);
            Vector3 knee = hip + thigh * RigIK.Thigh;
            Vector3 sole = knee + shin * RigIK.Shin;
            p.SolveLeg(leg, sole, knee - (hip + sole) * 0.5f);
        }

        static Vector3 ThighUp(in CharacterPose p, int leg)
        {
            Vector3 along = (BodyBoxes.KneePos(p, leg) - p.HipPos(leg)).normalized;
            Vector3 up = Vector3.Cross(along, Vector3.right).normalized;
            return up.y < 0f ? -up : up;
        }

        static float HandOnKnee(ref CharacterPose p, SeatShape s, int arm, int leg, float back, int selfMask)
        {
            float side = arm == 0 ? 1f : -1f;
            Vector3 hip = p.HipPos(leg), knee = BodyBoxes.KneePos(p, leg);
            Vector3 along = (knee - hip).normalized, up = ThighUp(p, leg);
            Vector3 outward = Vector3.ProjectOnPlane(Vector3.right * side, along).normalized;
            Vector3 hand = knee - along * back + up * (0.127f + 0.062f) + outward * 0.03f;
            return PlaceHand(ref p, s, arm, hand, new Vector3(side, -0.25f, -0.45f), outward, -along, up, selfMask);
        }

        /// <summary>Fist against the side of the waist, elbow out and back.</summary>
        static float HandOnHip(ref CharacterPose p, SeatShape s, int arm)
        {
            float side = arm == 0 ? 1f : -1f;
            Vector3 hand = p.TorsoPoint(new Vector3(side * 0.36f, 0.17f, 0.01f));
            return PlaceHand(ref p, s, arm, hand, TorsoDir(p, side * 0.6f, 0.15f, -1f), TorsoDir(p, side, 0f, 0f), TorsoDir(p, 0f, 0f, 1f),
                TorsoDir(p, 0f, 1f, 0f), MLegs | MTorso);
        }

        static readonly List<Vector3> Offsets = BuildOffsets();

        /// <summary>Candidate hand offsets (outward, along, up) sorted by distance: the nearest clear spot wins.</summary>
        static List<Vector3> BuildOffsets()
        {
            var list = new List<Vector3>();
            float[] o = { 0f, 0.02f, 0.04f, 0.06f, 0.08f, 0.1f, 0.12f, 0.15f, 0.18f };
            float[] a = { 0f, 0.03f, -0.03f, 0.06f, -0.06f, 0.1f, -0.1f, 0.14f, -0.14f };
            float[] u = { 0f, 0.012f, 0.025f, 0.04f, 0.06f, 0.08f, 0.11f };
            foreach (float x in o)
            foreach (float y in a)
            foreach (float z in u)
                list.Add(new Vector3(x, y, z));
            // Lifting the fist off what it rests on looks worst: prefer sliding it along / outward.
            float Cost(Vector3 v) => v.x * v.x + v.y * v.y + 6f * v.z * v.z;
            list.Sort((l, r) => Cost(l).CompareTo(Cost(r)));
            return list;
        }

        /// <summary>
        /// Puts a fist at <paramref name="hand"/>, or at the nearest offset (along the three given directions) where the
        /// arm is clear of the seat and does not sink into the own parts in <paramref name="selfMask"/>; the elbow is
        /// tried along the given pole and further out.
        /// </summary>
        static float PlaceHand(ref CharacterPose p, SeatShape s, int arm, Vector3 hand, Vector3 pole, Vector3 dirOut, Vector3 dirAlong, Vector3 dirUp, int selfMask)
        {
            int armMask = arm == 0 ? MUpperR | MForeR : MUpperL | MForeL;
            Vector3 side = Vector3.right * (arm == 0 ? 1f : -1f);
            pole = pole.normalized;
            var poles = new[] { pole, (pole + side).normalized, (pole + Vector3.forward * 0.8f).normalized };
            var best = p;
            float bestBad = float.MaxValue;
            foreach (var off in Offsets)
            {
                Vector3 h = hand + dirOut * off.x + dirAlong * off.y + dirUp * off.z;
                foreach (var pl in poles)
                {
                    var q = p;
                    q.SolveArm(arm, h, pl);
                    float bad = SeatPenetration(q, s, armMask) + Mathf.Max(0f, SelfPenetration(q, arm, selfMask) - SelfAllow);
                    if (bad <= 0f)
                    {
                        // Rest on what is below: lower the fist while it stays clear (5 mm steps).
                        float lowered = 0f;
                        for (float dz = 0.005f; dz <= off.z + 0.03f + 0.0001f; dz += 0.005f)
                        {
                            var r = p;
                            r.SolveArm(arm, h - dirUp * dz, pl);
                            if (SeatPenetration(r, s, armMask) + Mathf.Max(0f, SelfPenetration(r, arm, selfMask) - SelfAllow) > 0f) break;
                            q = r;
                            lowered = dz;
                        }
                        Trace?.AppendLine($"    {(arm == 0 ? "right" : "left")} hand: clean, offset {off.x:0.00} {off.y:0.00} {off.z - lowered:0.000}");
                        p = q;
                        return 0f;
                    }
                    if (bad < bestBad) { bestBad = bad; best = q; }
                }
            }
            if (Trace != null)
                Trace.AppendLine($"    {(arm == 0 ? "right" : "left")} hand: NO clean spot, best: seat {SeatPenetration(best, s, armMask) * 100f:0.0} cm, " +
                                 $"own body {SelfPenetration(best, arm, selfMask) * 100f:0.0} cm ({SelfWorst(best, arm, selfMask)})");
            p = best;
            return bestBad;
        }

        /// <summary>When set (editor seat check), the solver writes how every hand was placed.</summary>
        public static System.Text.StringBuilder Trace;

        /// <summary>Deepest penetration of the parts in <paramref name="mask"/> into the seat (boxes exactly; floor / upright cylinders: 5 cm when touched).</summary>
        static float SeatPenetration(in CharacterPose p, SeatShape s, int mask)
        {
            BodyBoxes.Get(p, Boxes);
            float worst = 0f;
            var solids = s.Solids;
            for (int i = 0; i < BodyBoxes.Count; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                for (int n = 0; n < solids.Length; n++)
                {
                    if (solids[n].Upright) continue;
                    worst = Mathf.Max(worst, BodyBoxes.Penetration(Boxes[i], solids[n].AsBox(Margin)));
                }
            }
            if (worst <= 0f && Hits(p, s, mask)) worst = 0.05f;
            return worst;
        }

        static string SelfWorst(in CharacterPose p, int arm, int selfMask)
        {
            BodyBoxes.Get(p, Boxes);
            int up = arm == 0 ? 2 : 4, fore = up + 1;
            string what = "-";
            float worst = 0f;
            for (int b = 0; b < BodyBoxes.Count; b++)
            {
                if ((selfMask & (1 << b)) == 0 || b == up || b == fore) continue;
                float d = BodyBoxes.Penetration(Boxes[fore], Boxes[b]);
                if (d > worst) { worst = d; what = $"forearm x {(BodyPart)b}"; }
                if (b <= 1) continue; // the upper arm meets the torso and the head at the shoulder
                d = BodyBoxes.Penetration(Boxes[up], Boxes[b]);
                if (d > worst) { worst = d; what = $"upper arm x {(BodyPart)b}"; }
            }
            return what;
        }

        /// <summary>Deepest overlap of the arm with own parts: the forearm against any of them, the upper arm against all but the torso and the head (they meet at the shoulder).</summary>
        static float SelfPenetration(in CharacterPose p, int arm, int selfMask)
        {
            BodyBoxes.Get(p, Boxes);
            // Breathing scales the torso by up to (0.8 %, 0.4 %, 3 %).
            Boxes[0].Half = Vector3.Scale(Boxes[0].Half, new Vector3(1.008f, 1.004f, 1.03f));
            int up = arm == 0 ? 2 : 4, fore = up + 1;
            float worst = 0f;
            for (int b = 0; b < BodyBoxes.Count; b++)
            {
                if ((selfMask & (1 << b)) == 0 || b == up || b == fore) continue;
                worst = Mathf.Max(worst, BodyBoxes.Penetration(Boxes[fore], Boxes[b]));
                if (b > 1) worst = Mathf.Max(worst, BodyBoxes.Penetration(Boxes[up], Boxes[b]));
            }
            return worst;
        }

        // ================================================================== look range

        static readonly float[] ProbeYaws = { -60f, -30f, 0f, 30f, 60f };

        static bool HeadHitsAnywhere(in CharacterPose p, SeatShape s, LookLimits lim)
        {
            var q = p;
            foreach (float yaw in ProbeYaws)
            foreach (float pitch in new[] { lim.Up, lim.Pitch, Mathf.Lerp(lim.Pitch, -lim.Down, 0.5f) })
            {
                q.Head = Quaternion.Inverse(q.TorsoRoot) * HeadRot(lim, lim.Center + Mathf.Clamp(yaw, -lim.Yaw, lim.Yaw), pitch);
                if (Hits(q, s, MHead)) return true;
            }
            return false;
        }

        /// <summary>
        /// Per yaw: the lowest pitch where the eyes are still <see cref="EyeClearance"/> away from the own body and the
        /// highest where the head is clear of the seat (and the eyes of the body); the head never sinks into the own
        /// arms / legs either (a hand in the hair, a chin on the fists). Scanned outwards from the pose's neutral pitch.
        /// </summary>
        static void SolveLookTable(Entry e, SeatShape s)
        {
            e.Up = new float[TableSize];
            e.Down = new float[TableSize];
            var p = e.Body;
            BodyBoxes.Get(p, Boxes);
            var body = (PartBox[])Boxes.Clone();
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
            var lim = e.Limits;
            _headVsLimbs = e.Variant >= RandomCount;
            float start = Mathf.Clamp(lim.Pitch, -lim.Down, lim.Up);
            if (_headVsLimbs)
            {
                // Neutral look not possible (the head in a fist): the nearest look that is.
                string why = LookWhy(p, s, body, neck, lim, lim.Center, start);
                if (why != null)
                {
                    bool found = false;
                    for (float r = 1f; r <= 30f && !found; r += 1f)
                    foreach (var (dy, dp) in new[] { (0f, r), (0f, -r), (r, 0f), (-r, 0f), (r, r), (-r, r), (r, -r), (-r, -r) })
                    {
                        float pp = Mathf.Clamp(start + dp, -lim.Down, lim.Up);
                        if (!LookOk(p, s, body, neck, lim, lim.Center + dy, pp)) continue;
                        Trace?.AppendLine($"   neutral look ({lim.Center:0}, {start:0}) not possible ({why}), moved to ({lim.Center + dy:0}, {pp:0})");
                        lim.Center += dy;
                        lim.Pitch = start = pp;
                        found = true;
                        break;
                    }
                    if (!found) Trace?.AppendLine($"   neutral look ({lim.Center:0}, {start:0}) not possible ({why}), nothing near it is either");
                }
                // Yaw range: as far both ways from the neutral look as the head can turn at the neutral pitch (a hand in
                // the hair stops it on that side); the range is re-centred on what is left.
                float lo = 0f, hi = 0f;
                for (float d = 1f; d <= lim.Yaw && LookOk(p, s, body, neck, lim, lim.Center + d, start); d += 1f) hi = d;
                for (float d = -1f; d >= -lim.Yaw && LookOk(p, s, body, neck, lim, lim.Center + d, start); d -= 1f) lo = d;
                if (hi - lo < 2f * lim.Yaw - 0.5f)
                {
                    Trace?.AppendLine($"   look yaw {lim.Center:0}+-{lim.Yaw:0} narrowed to {lim.Center + lo:0}..{lim.Center + hi:0}");
                    lim.Center += (lo + hi) * 0.5f;
                    lim.Yaw = (hi - lo) * 0.5f;
                }
                e.Limits = lim;
            }
            for (int i = 0; i < TableSize; i++)
            {
                float yaw = lim.Center + Mathf.Clamp(-90f + i * TableStep, -lim.Yaw, lim.Yaw);
                float down = -start, up = start;
                for (float pitch = start; pitch >= -lim.Down; pitch -= 1f)
                {
                    if (!LookOk(p, s, body, neck, lim, yaw, pitch)) break;
                    down = -pitch;
                }
                for (float pitch = start; pitch <= lim.Up; pitch += 1f)
                {
                    if (!LookOk(p, s, body, neck, lim, yaw, pitch)) break;
                    up = pitch;
                }
                e.Down[i] = down;
                e.Up[i] = up;
            }
            // The clamp interpolates between entries 5 degrees apart: keep every entry within its neighbours' range.
            var u = (float[])e.Up.Clone();
            var dn = (float[])e.Down.Clone();
            for (int i = 0; i < TableSize; i++)
            {
                int a = Mathf.Max(0, i - 1), b = Mathf.Min(TableSize - 1, i + 1);
                e.Up[i] = Mathf.Min(u[a], Mathf.Min(u[i], u[b]));
                e.Down[i] = Mathf.Min(dn[a], Mathf.Min(dn[i], dn[b]));
            }
        }

        static readonly PartBox[] LookBoxes = new PartBox[BodyBoxes.Count];

        /// <summary>Parts the head must not sink into while looking around: forearms (fists) and legs.</summary>
        public static readonly int[] HeadVs =
        {
            (int)BodyPart.ForearmR, (int)BodyPart.ForearmL, (int)BodyPart.ThighR, (int)BodyPart.ShinR, (int)BodyPart.ThighL, (int)BodyPart.ShinL
        };

        /// <summary>Pin-up poses: the head is also kept out of the own forearms / legs (the original four keep their ranges).</summary>
        static bool _headVsLimbs;

        static bool LookOk(CharacterPose p, SeatShape s, PartBox[] body, Vector3 neck, in LookLimits lim, float yaw, float pitch) =>
            LookWhy(p, s, body, neck, lim, yaw, pitch) == null;

        /// <summary>Why the look is not allowed (null = it is).</summary>
        static string LookWhy(CharacterPose p, SeatShape s, PartBox[] body, Vector3 neck, in LookLimits lim, float yaw, float pitch)
        {
            Quaternion look = HeadRot(lim, yaw, pitch);
            Vector3 eye = neck + look * EyeOffset;
            for (int b = 0; b < body.Length; b++)
                if (b != (int)BodyPart.Head && body[b].Distance(eye) < EyeClearance) return $"eye near {(BodyPart)b}";
            p.Head = Quaternion.Inverse(p.TorsoRoot) * look;
            if (Hits(p, s, MHead)) return "head in the seat";
            if (!_headVsLimbs) return null;
            // The head against the own forearms and legs (the upper arms meet it at the shoulders anyway).
            BodyBoxes.Get(p, LookBoxes);
            foreach (int b in HeadVs)
                if (BodyBoxes.Penetration(LookBoxes[(int)BodyPart.Head], body[b]) > HeadAllow) return $"head in {(BodyPart)b}";
            return null;
        }

        // ================================================================== solver

        /// <summary>Thighs onto the cushion; then forward (and down again) until the hanging shins clear the seat's front.</summary>
        static void FitSeat(ref CharacterPose p, SeatShape s, int restMask)
        {
            Settle(ref p, s, restMask);
            for (int i = 0; i < 40 && Hits(p, s, MShins); i++)
            {
                p.PelvisPos.z += 0.01f;
                Settle(ref p, s, restMask);
            }
        }

        /// <summary>Lowest pelvis height where the parts in <paramref name="mask"/> are clear of the seat.</summary>
        static void Settle(ref CharacterPose p, SeatShape s, int mask)
        {
            Vector3 basePos = p.PelvisPos;
            float lo = -0.35f, hi = 0.45f;
            for (int i = 0; i < 22; i++)
            {
                float mid = (lo + hi) * 0.5f;
                p.PelvisPos = basePos + Vector3.up * mid;
                if (Hits(p, s, mask)) lo = mid; else hi = mid;
            }
            p.PelvisPos = basePos + Vector3.up * hi;
        }

        static bool Hits(in CharacterPose p, SeatShape s, int mask)
        {
            BodyBoxes.Get(p, Boxes);
            for (int i = 0; i < BodyBoxes.Count; i++)
                if ((mask & (1 << i)) != 0 && BoxHits(Boxes[i], s)) return true;
            return false;
        }

        /// <summary>A part against the floor and every solid: boxes / lying cylinders exactly (separating axes), upright cylinders by points.</summary>
        static bool BoxHits(in PartBox b, SeatShape s)
        {
            for (int c = 0; c < 8; c++)
                if (b.Point((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f).y < Margin) return true;
            var solids = s.Solids;
            for (int n = 0; n < solids.Length; n++)
            {
                if (!solids[n].Upright)
                {
                    if (BodyBoxes.Penetration(b, solids[n].AsBox(Margin)) > 0f) return true;
                    continue;
                }
                // Quick reject: the box's bounding sphere against the cylinder's bounding box.
                var hull = solids[n].AsBox(Margin + b.Half.magnitude);
                if (hull.Distance(b.Center) > 0f) continue;
                for (int i = 0; i < 7; i++)
                for (int j = 0; j < 7; j++)
                for (int k = 0; k < 7; k++)
                {
                    if (i != 0 && i != 6 && j != 0 && j != 6 && k != 0 && k != 6) continue; // surface points only
                    if (solids[n].Contains(b.Point(i / 3f - 1f, j / 3f - 1f, k / 3f - 1f), Margin)) return true;
                }
            }
            return false;
        }

        static Vector3 TorsoDir(in CharacterPose p, float x, float y, float z) => p.TorsoRoot * new Vector3(x, y, z);
    }
}
