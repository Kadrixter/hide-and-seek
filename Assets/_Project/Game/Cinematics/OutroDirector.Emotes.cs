using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    public sealed partial class OutroDirector
    {
        readonly System.Collections.Generic.HashSet<int> _ownEmote = new(), _inPair = new();

        double NowS => _pack.StartAt + _t;

        CinePuppet PuppetOf(ulong id)
        {
            foreach (var p in _puppets) if (p != null && p.PlayerId == id) return p;
            return null;
        }

        /// <summary>The figure shows what its player does (solo emote, pair emote with a row neighbour), else a cheer.</summary>
        void MirrorEmote(CinePuppet p, int i, float t)
        {
            var pl = NetPlayer.Find(p.PlayerId);
            EmoteId id = EmoteId.None;
            double start = 0d;
            CinePuppet partner = null;
            byte role = 0;
            if (pl != null)
            {
                var pv = pl.PairVar.Value;
                var sv = pl.EmoteVar.Value;
                if (pv.Active && (partner = PuppetOf(pv.Partner)) != null)
                {
                    _ownEmote.Add(i);
                    PairPlace(p, i, partner, pv);
                    if (NowS >= pv.StartAt) { id = (EmoteId)pv.Id; start = pv.StartAt; role = pv.Role; }
                }
                else if (sv.Id != 0)
                {
                    _ownEmote.Add(i);
                    id = (EmoteId)sv.Id;
                    start = sv.StartAt;
                }
                if (!pv.Active && _inPair.Remove(i)) p.WalkTo(_gate.Local(Slot(i, _puppets.Count)), 0.9f, RowYaw(i, _puppets.Count));
            }
            // A cheer of its own, once, for the figures whose player does nothing (and the dead winners).
            if (id == EmoteId.None && !_ownEmote.Contains(i) && p.Arrived)
            {
                var info = Emotes.Get(_autoEmote[i]);
                double at = _pack.StartAt + _autoAt[i];
                if (info != null && NowS >= at && NowS - at < info.Duration) { id = _autoEmote[i]; start = at; }
            }
            var a = p.Actions;
            if (id != EmoteId.None)
            {
                a.SetEmotePartner(partner != null ? partner.Body : null, role);
                a.PlayEmote(id, (float)(NowS - start));
            }
            else if (a.Emote != EmoteId.None) a.StopEmote();
        }

        /// <summary>Pair emote of two row neighbours: both step to the middle between their slots and face each other.</summary>
        void PairPlace(CinePuppet p, int i, CinePuppet partner, in PairEmotePack pv)
        {
            if (_inPair.Contains(i)) return;
            _inPair.Add(i);
            int n = _puppets.Count;
            int j = _puppets.IndexOf(partner);
            if (j < 0) return;
            var info = Emotes.Get((EmoteId)pv.Id);
            float half = (info != null ? info.PairDistance : 0.9f) * 0.5f;
            Vector3 a = Slot(i, n), b = Slot(j, n);
            Vector3 mid = (a + b) * 0.5f + new Vector3(0f, 0f, 0.25f);
            Vector3 axis = (b - a).normalized;
            Vector3 spot = _gate.Local(mid - axis * half);
            Vector3 face = _gate.LocalDir(axis);
            p.WalkTo(spot, 1.1f, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
        }

        // ================================================================ rules used by the emote code during the outro

        /// <summary>Both are in the outro row next to each other: pair emotes ignore their real distance.</summary>
        public static bool Neighbours(NetPlayer a, NetPlayer b)
        {
            if (a == null || b == null || Row.Count == 0) return false;
            int i = Row.IndexOf(a.NetworkObjectId), j = Row.IndexOf(b.NetworkObjectId);
            return i >= 0 && j >= 0 && Mathf.Abs(i - j) == 1;
        }

        /// <summary>Partner for the K wheel's pair page during the outro: the living neighbour on the right, else left.</summary>
        public static NetPlayer PairTargetFor(NetPlayer me)
        {
            if (me == null || Row.Count == 0) return null;
            int i = Row.IndexOf(me.NetworkObjectId);
            if (i < 0) return null;
            foreach (int k in new[] { i + 1, i - 1 })
            {
                if (k < 0 || k >= Row.Count) continue;
                var o = NetPlayer.Find(Row[k]);
                if (o != null && o.Life.Value == HideAndSeek.Core.LifeState.Alive) return o;
            }
            return null;
        }
    }
}
