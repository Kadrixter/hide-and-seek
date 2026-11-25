using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server test hooks for the dropped-item physics (-testdropsafe, NetSession.DropSafeTests.cs): the same throw as Q
    /// for any item (no inventory needed) and a raw spawn with a chosen start / speed (inside a wall, very fast).
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Q path (ThrowStart, the same speeds as <see cref="ServerDropSlot"/>) for <paramref name="item"/>; returns the new drop id.</summary>
        public int TestThrowDrop(NetPlayer pl, HotbarItem item, byte extra)
        {
            int id = _nextDropId;
            Vector3 fwd = pl.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            ThrowDrop(pl, item, extra, fwd, 0.5f, 1.15f, 2.3f, 1.3f, Quaternion.LookRotation(fwd) * Quaternion.Euler(0f, 90f, 0f));
            return id;
        }

        /// <summary>Raw drop: starts at <paramref name="pos"/> with <paramref name="vel"/>, checked against <paramref name="feet"/>; returns its id.</summary>
        public int TestSpawnDropAt(HotbarItem item, Vector3 pos, Vector3 vel, Vector3 feet)
        {
            int id = _nextDropId;
            SpawnDrop(item, 0, pos, Quaternion.Euler(0f, 30f, 0f), vel, feet);
            return id;
        }

        /// <summary>Tests: where RC car <paramref name="i"/> is now (the driver's live position while driven).</summary>
        public Vector3 TestRcWhere(int i)
        {
            RcWhere(i, out var pos, out _);
            return pos;
        }

        /// <summary>Removes a drop (tests clean up after each case).</summary>
        public void TestRemoveDrop(int id)
        {
            for (int i = 0; i < Drops.Count; i++)
                if (Drops[i].Id == id) { Drops.RemoveAt(i); _dropAnchor.Remove(id); return; }
        }
    }
}
