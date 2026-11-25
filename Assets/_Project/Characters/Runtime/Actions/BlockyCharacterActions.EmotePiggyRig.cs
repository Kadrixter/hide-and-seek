using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Placement of both bodies of the piggyback ride in pair space (pure functions of the shared clock, so each body
    /// knows exactly where the other one is). The carrier (role 0) turns its back to the rider and crouches, the rider
    /// (role 1) jumps on and sits on the carrier's pelvis frame; then the carrier walks small circles in place (one
    /// path parameter for the turn and the walk drives the stepping feet), slowing down as it tires. The way out walks
    /// both bodies back onto their own spots, facing each other again.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        const float PiggyR = 0.3f, PiggyOmega = 66f, PiggyTire = 14f, PiggyStep = 0.2f;
        static readonly Vector3 PiggySeat = new Vector3(0f, 0.2f, -0.3f);

        static float PiggyHalf => EmoteTimeline.PiggyDistance * 0.5f;

        /// <summary>Walked angle round the circle (degrees): eases in, then slows down as the carrier tires.</summary>
        static float PiggyPhi(float t)
        {
            float w = t - EmoteTimeline.PiggyWalk;
            if (w <= 0f) return 0f;
            const float T = PiggyTire;
            return PiggyOmega * (0.55f * w + 0.45f * T * (1f - Mathf.Exp(-w / T)) - 0.4f * (1f - Mathf.Exp(-w / 0.4f)));
        }

        static float PiggyTired(float t) => 1f - Mathf.Exp(-Mathf.Max(0f, t - EmoteTimeline.PiggyWalk) / 12f);

        /// <summary>Carrier path in step units: first the half turn (45 degrees a unit), then the walk (<see cref="PiggyStep"/> a unit).</summary>
        static float PiggyUnits(float t) => 4f * Smoother(t, 0.05f, 0.9f) + PiggyR * PiggyPhi(t) * Mathf.Deg2Rad / PiggyStep;

        /// <summary>Carrier's ground point and yaw (pair space) at path units <paramref name="u"/>.</summary>
        static void PiggyGround(float u, out Vector3 pos, out float yaw)
        {
            float turn = Mathf.Min(u, 4f) * 45f;
            float phi = Mathf.Max(0f, u - 4f) * PiggyStep / PiggyR * Mathf.Rad2Deg;
            yaw = turn + phi;
            float psi = (180f + phi) * Mathf.Deg2Rad;
            var c = new Vector3(-PiggyR, 0f, -PiggyHalf);
            pos = c - PiggyR * new Vector3(Mathf.Cos(psi), 0f, -Mathf.Sin(psi));
        }

        static BodyState PiggyCarrier(float t)
        {
            float u = PiggyUnits(t);
            PiggyGround(u, out var g, out float yaw);
            float tired = PiggyTired(t);
            float walkS = Mathf.Max(0f, u - 4f) * PiggyStep;
            float bob = 0.018f * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * walkS / (PiggyStep * 0.5f)));
            float crouch = 0.22f * Env(t, 0.55f, 1.0f, 2.15f, 2.75f) + 0.04f * Seg(t, 2.1f, 2.8f) + 0.05f * tired + bob;
            float breath = Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(0.3f, 0.7f, tired));
            float lean = 32f * Env(t, 0.55f, 1.0f, 2.15f, 2.75f) + 15f * Seg(t, 2.0f, 2.8f) + 11f * tired + (1f + 2f * tired) * breath;
            float sway = Mathf.Sin(Mathf.PI * walkS / (PiggyStep * 0.5f)) * Seg(t, EmoteTimeline.PiggyWalk, EmoteTimeline.PiggyWalk + 0.6f);
            return new BodyState
            {
                Pos = g + Vector3.up * (BlockyCharacter.HipHeight - 0.005f - crouch),
                Rot = Quaternion.Euler(0f, yaw, 2.5f * sway * (1f + tired)),
                Torso = Quaternion.Euler(lean, 0f, 0f),
            };
        }

        static BodyState PiggyRider(float t)
        {
            float hip = BlockyCharacter.HipHeight - 0.005f;
            float prep = 0.12f * Env(t, 1.15f, 1.5f, 1.6f, 1.72f);
            var stand = new BodyState
            {
                Pos = new Vector3(0f, hip - prep, PiggyHalf),
                Rot = Quaternion.Euler(0f, 180f, 0f),
                Torso = Quaternion.Euler(10f * prep / 0.12f, 0f, 0f),
            };
            if (t <= EmoteTimeline.PiggyJump) return stand;
            var c = PiggyCarrier(t);
            Quaternion yawOnly = Quaternion.Euler(0f, c.Rot.eulerAngles.y, 0f);
            float tired = PiggyTired(t);
            var ride = new BodyState
            {
                Pos = c.Pos + yawOnly * PiggySeat + Vector3.up * 0.012f * Mathf.Sin(t * 9f) * Seg(t, 2.8f, 3.4f),
                Rot = yawOnly,
                Torso = Quaternion.Euler(-4f + 3f * tired, 0f, 3f * Mathf.Sin(t * 2.6f)),
            };
            float j = Seg(t, EmoteTimeline.PiggyJump, EmoteTimeline.PiggyLand);
            if (j >= 1f) return ride;
            return new BodyState
            {
                Pos = Vector3.Lerp(stand.Pos, ride.Pos, j) + Vector3.up * 0.3f * Hump(t, EmoteTimeline.PiggyJump, EmoteTimeline.PiggyLand),
                Rot = ride.Rot,
                Torso = Quaternion.Slerp(Quaternion.Euler(-8f, 0f, 0f), ride.Torso, j),
            };
        }

        /// <summary>Carrier sole (pair space) at time <paramref name="t"/>: stepping along the turn and the walk.</summary>
        static Vector3 PiggyCarrierFoot(int leg, float t, out float yaw)
        {
            StepPhase(PiggyUnits(t), 1f, leg, out float uf, out float lift);
            PiggyGround(uf, out var g, out yaw);
            Vector3 local = leg == 0 ? new Vector3(0.13f, 0f, 0.01f) : new Vector3(-0.13f, 0f, -0.01f);
            return g + Quaternion.Euler(0f, yaw, 0f) * local + Vector3.up * 0.06f * lift;
        }

        // ------------------------------------------------------------------ way out (from the pose frozen at the stop)

        /// <summary>
        /// Carrier on the way out, <paramref name="stopT"/> seconds after the stop at <paramref name="t0"/>: walks home
        /// with the back to the rider's spot (the rider still on), lets the rider down, turns round to the partner.
        /// </summary>
        static BodyState PiggyCarrierExit(float t0, float stopT, out float units, out float total)
        {
            var s0 = PiggyCarrier(t0);
            PiggyExitPath(t0, out var g0, out float yaw0, out float yawMid, out int n1);
            float w = Seg(stopT, 0.1f, 0.7f), turn = Seg(stopT, 0.95f, 1.35f);
            units = w * n1 + turn * 4f;
            total = n1 + 4f;
            PiggyExitPlace(g0, yaw0, yawMid, n1, units, out var g, out float yaw);
            float hip = BlockyCharacter.HipHeight - 0.005f;
            return new BodyState
            {
                Pos = g + Vector3.up * (Mathf.Lerp(s0.Pos.y, hip, Seg(stopT, 0.75f, 1.15f)) - 0.05f * Hump(stopT, 0.6f, 0.98f)),
                Rot = Quaternion.Euler(0f, yaw, 0f),
                Torso = Quaternion.Slerp(s0.Torso, Quaternion.identity, Seg(stopT, 0.75f, 1.25f)),
            };
        }

        static void PiggyExitPath(float t0, out Vector3 g0, out float yaw0, out float yawMid, out int n1)
        {
            PiggyGround(PiggyUnits(t0), out g0, out yaw0);
            yawMid = 180f + 360f * Mathf.Round((yaw0 - 180f) / 360f);
            n1 = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(g0, new Vector3(0f, 0f, -PiggyHalf)) / 0.16f + Mathf.Abs(yawMid - yaw0) / 40f));
        }

        /// <summary>Carrier ground point / yaw at exit path units (walk home in n1 units, then the half turn in 4).</summary>
        static void PiggyExitPlace(Vector3 g0, float yaw0, float yawMid, int n1, float units, out Vector3 g, out float yaw)
        {
            float w = Mathf.Clamp01(units / n1);
            g = Vector3.Lerp(g0, new Vector3(0f, 0f, -PiggyHalf), w);
            yaw = Mathf.Lerp(yaw0, yawMid, w) + 180f * Mathf.Clamp01((units - n1) / 4f);
        }

        /// <summary>Carrier sole on the way out (stepping along the exit path).</summary>
        static Vector3 PiggyCarrierExitFoot(int leg, float t0, float stopT, out float yaw)
        {
            PiggyCarrierExit(t0, stopT, out float units, out _);
            PiggyExitPath(t0, out var g0, out float yaw0, out float yawMid, out int n1);
            StepPhase(units, 1f, leg, out float uf, out float lift);
            PiggyExitPlace(g0, yaw0, yawMid, n1, uf, out var g, out yaw);
            Vector3 local = leg == 0 ? new Vector3(0.13f, 0f, 0.01f) : new Vector3(-0.13f, 0f, -0.01f);
            return g + Quaternion.Euler(0f, yaw, 0f) * local + Vector3.up * 0.06f * lift;
        }

        /// <summary>Rider on the way out: rides home on the carrier, then hops off backwards onto the own spot.</summary>
        static BodyState PiggyRiderExit(float t0, float stopT, out float hop)
        {
            var c = PiggyCarrierExit(t0, Mathf.Min(stopT, 0.7f), out _, out _);
            var ride = PiggyRider(t0);
            Quaternion yawOnly = Quaternion.Euler(0f, c.Rot.eulerAngles.y, 0f);
            ride.Pos = c.Pos + yawOnly * PiggySeat;
            ride.Rot = yawOnly;
            float j = Seg(stopT, 0.7f, 1.05f);
            hop = Hump(stopT, 0.7f, 1.05f);
            var home = new Vector3(0f, BlockyCharacter.HipHeight - 0.005f - 0.06f * Hump(stopT, 1.0f, 1.3f), PiggyHalf);
            ride.Pos = Vector3.Lerp(ride.Pos, home, j) + Vector3.up * 0.14f * hop;
            ride.Torso = Quaternion.Slerp(ride.Torso, Quaternion.identity, j);
            return ride;
        }
    }
}
