using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Where the four players of the pool table wait and where they are put when they get up. Waiting spots: team A
    /// at the long side -Z, team B at +Z, the first player of a team towards the head, the second towards the foot,
    /// <see cref="StandOut"/> from the rail (clear of the shooter's cue, which never points at a long side's waiting
    /// spot from closer than the rail). Getting up: behind the own waiting spot, or the nearest free spot around the
    /// table when furniture or another player stands there (never into a sofa).
    /// </summary>
    public sealed partial class PoolTable
    {
        /// <summary>Along-the-rail offset of a place's waiting spot (first player towards the head, second towards the foot).</summary>
        public static float PlaceX(int place) => place < 2 ? -0.55f : 0.55f;

        public override Vector3 SeatStand(int seat) => transform.TransformPoint(new Vector3(PlaceX(seat), 0.02f, Side(seat) * StoolZ));

        public override Vector3 LeavePoint(int seat)
        {
            Vector3 want = transform.TransformPoint(new Vector3(PlaceX(seat), 0.02f, Side(seat) * (StoolZ + 0.62f)));
            if (Clear(want)) return want;
            // Round the table, 1.3 m outside its edge: the nearest free spot to the wanted one.
            float a = PoolTableModel.OuterL * 0.5f + 1.3f, b = PoolTableModel.OuterW * 0.5f + 1.3f;
            Vector3 best = want;
            float bestD = float.MaxValue;
            for (int i = 0; i < 32; i++)
            {
                float t = i / 32f * Mathf.PI * 2f;
                var local = new Vector3(Mathf.Cos(t) * a, 0.02f, Mathf.Sin(t) * b);
                Vector3 p = transform.TransformPoint(local);
                float d = (p - want).sqrMagnitude;
                if (d < bestD && Clear(p)) { bestD = d; best = p; }
            }
            return best;
        }

        static readonly Collider[] Hits = new Collider[16];

        /// <summary>A standing player's capsule fits at <paramref name="feet"/> (players and triggers do not count).</summary>
        static bool Clear(Vector3 feet)
        {
            int n = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.5f, 0.32f, Hits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i];
                if (c == null || c.GetComponentInParent<NetPlayer>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
