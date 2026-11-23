using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Cut-off legs as items. During a round every leg the chainsaw takes off a hider is a server-owned dropped item
    /// (<see cref="HotbarItem.SeveredLeg"/>, MatchManager.Legs) instead of a local loose part, so everybody sees it in
    /// the same spot and a hider can pick it up (E) and throw it. Whose leg it is travels in one byte ("code": the
    /// owner's client id in the low 7 bits, bit 7 = left leg) - in the drop's Extra and in the holder's
    /// <see cref="NetPlayer.LegCode"/> - so the leg keeps the owner's skin and trousers everywhere.
    /// </summary>
    public static class SeveredLegs
    {
        public const byte UnknownOwner = 0x7F;

        public static byte Code(NetPlayer owner, int limb)
        {
            byte id = owner != null && owner.OwnerClientId < UnknownOwner ? (byte)owner.OwnerClientId : UnknownOwner;
            return (byte)(id | (limb == Limb.LegL ? 0x80 : 0));
        }

        public static bool IsLeft(byte code) => (code & 0x80) != 0;

        public static NetPlayer OwnerOf(byte code)
        {
            int id = code & 0x7F;
            if (id == UnknownOwner) return null;
            foreach (var p in NetPlayer.All)
                if (p != null && (int)p.OwnerClientId == id) return p;
            return null;
        }

        /// <summary>The owner's skin material (null when he left or has no body).</summary>
        public static Material SkinOf(byte code)
        {
            var c = OwnerOf(code)?.Character;
            if (c == null || !c.IsBuilt) return null;
            var j = c.LimbJoint(IsLeft(code) ? Limb.LegL : Limb.LegR);
            return j != null && j.TryGetComponent<MeshRenderer>(out var r) ? r.sharedMaterial : null;
        }

        public static GameObject Model(Transform parent, byte code) =>
            SeveredLegModel.Create(parent, SkinOf(code), BloodFx.StumpMaterial, IsLeft(code));

        private static readonly Dictionary<int, HandGrip> Grips = new();

        /// <summary>How this leg is held (one grip per code, so a different leg rebuilds the model in the hand).</summary>
        public static HandGrip Grip(int code)
        {
            if (code < 0) return null;
            if (Grips.TryGetValue(code, out var g)) return g;
            byte c = (byte)code;
            g = SeveredLegModel.MakeGrip(r => Model(r, c));
            Grips[code] = g;
            return g;
        }

        /// <summary>
        /// True when a leg cut off <paramref name="victim"/> now is the server's item: a hider's leg in a round on the
        /// parking garage. The local loose part is then not made (the dropped item appears in its place).
        /// </summary>
        public static bool ServerOwned(NetPlayer victim, int limb)
        {
            if (victim == null || (limb != Limb.LegR && limb != Limb.LegL) || !victim.IsHider) return false;
            var mm = MatchManager.Instance;
            if (mm == null || mm.RaceMode || SceneManager.GetActiveScene().name != NetSession.GameScene) return false;
            var ph = mm.Phase.Value;
            return ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended;
        }

        /// <summary>
        /// Called for every new dropped-item view: a leg gets its owner's skin and, while fresh, keeps bleeding from the
        /// cut for a few seconds.
        /// </summary>
        public static void OnDropView(DroppedItemView view, DropState d)
        {
            if (view == null || d.Kind != HotbarItem.SeveredLeg) return;
            var skin = SkinOf(d.Extra);
            if (skin != null) SeveredLegModel.SetSkin(view.gameObject, skin);
            // Just cut off (or knocked off a head): still bleeding while it falls. Thrown ones fly too fast to drip.
            if (!d.Settled && d.Vel.sqrMagnitude < 16f)
            {
                var cut = view.transform.Find("Model/SeveredLeg");
                if (cut != null) BloodFx.GibBleed(cut, Random.Range(4f, 7f));
            }
        }
    }
}
