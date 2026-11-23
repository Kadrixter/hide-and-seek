using System;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Solo emote shared by the owner: id + server time it started (late joiners pick up a loop in step).</summary>
    public struct SoloEmotePack : INetworkSerializable, IEquatable<SoloEmotePack>
    {
        public byte Id;
        public double StartAt;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref StartAt);
        }

        public bool Equals(SoloEmotePack o) => Id == o.Id && StartAt.Equals(o.StartAt);
    }

    /// <summary>
    /// Pair emote set by the server on both players: which emote, this side's role (0 asked / leads, 1 accepted), the
    /// partner, the shared server start time (after the walk to the spots) and where this player has to stand.
    /// </summary>
    public struct PairEmotePack : INetworkSerializable, IEquatable<PairEmotePack>
    {
        public byte Id;
        public byte Role;
        public ulong Partner;
        public double StartAt;
        public Vector3 Spot;
        public float Yaw;

        public bool Active => Id != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Role);
            s.SerializeValue(ref Partner);
            s.SerializeValue(ref StartAt);
            s.SerializeValue(ref Spot);
            s.SerializeValue(ref Yaw);
        }

        public bool Equals(PairEmotePack o) => Id == o.Id && Role == o.Role && Partner == o.Partner && StartAt.Equals(o.StartAt) && Spot == o.Spot && Yaw.Equals(o.Yaw);
    }

    /// <summary>When a player may emote (same checks on the owner and the server).</summary>
    public static class EmoteRules
    {
        static int SightMask => ~((1 << PlayerAvatarBinder.LocalBodyLayer) | GameLayers.RagdollMask | (1 << 2));

        /// <summary>Free to start / keep an emote: alive, up, hands and body not taken by anything else.</summary>
        public static bool Free(NetPlayer p, out string why)
        {
            why = null;
            var mm = MatchManager.Instance;
            if (p == null || !p.IsSpawned || mm == null) { why = ""; return false; }
            var ph = mm.Phase.Value;
            if (ph == MatchPhase.Loading || ph == MatchPhase.Race || mm.RaceMode) { why = "Teraz nie można."; return false; }
            if (p.Life.Value != LifeState.Alive) { why = "Teraz nie można."; return false; }
            if (p.Seat.Value >= 0) { why = "Najpierw wstań."; return false; }
            if (p.Busy.Value || p.RcAnimating || p.HoldsPaMic || p.OverdoseActive || p.Gasping || p.Reviving.Value || p.HoldAnimVar.Value != 0)
            { why = "Masz zajęte ręce."; return false; }
            if (mm.ArcadePlayer.Value == p.NetworkObjectId || p.BrickColor.Value != 0) { why = "Masz zajęte ręce."; return false; }
            if (p.SawRev.Value || p.GlockAiming.Value) { why = "Masz zajęte ręce."; return false; }
            if (p.Motor != null && p.Motor.ProneAmount > 0.25f) { why = "Najpierw wstań."; return false; }
            return true;
        }

        /// <summary>Partner for a pair emote: free, close enough (eye to eye), on the same floor.</summary>
        public static bool PartnerOk(NetPlayer me, NetPlayer other, float slack, out string why)
        {
            why = null;
            if (other == null || other == me) { why = "Spójrz na gracza obok."; return false; }
            if (!Free(other, out _)) { why = $"{other.Nick} jest teraz zajęty."; return false; }
            if (Cinematics.OutroDirector.Neighbours(me, other)) return true; // outro row: figures side by side, bodies anywhere
            Vector3 d = other.transform.position - me.transform.position;
            if (Mathf.Abs(d.y) > 0.8f || new Vector2(d.x, d.z).magnitude > Emotes.AskRange + slack) { why = "Podejdź bliżej."; return false; }
            return true;
        }

        /// <summary>Tests: why <paramref name="o"/> is (not) the look target.</summary>
        public static string Explain(NetPlayer me, NetPlayer o)
        {
            if (me == null || o == null || me.Camera == null) return "none";
            Transform cam = me.Camera.transform;
            Vector3 eye = me.EyePosition, chest = o.transform.position + Vector3.up * 1.25f, to = chest - eye;
            float ang = Vector3.Angle(new Vector3(cam.forward.x, 0f, cam.forward.z), new Vector3(to.x, 0f, to.z));
            string hit = Physics.Linecast(eye, chest, out var h, SightMask, QueryTriggerInteraction.Ignore) ? $"{h.collider.name}/{h.collider.gameObject.layer}" : "-";
            return $"ang={ang:0} dist={new Vector2(to.x, to.z).magnitude:0.00} fwdY={cam.forward.y:0.00} hit={hit}";
        }

        /// <summary>
        /// Player the local camera points at within <see cref="Emotes.AskRange"/> (cone ~22 degrees, nothing in between),
        /// null if none.
        /// </summary>
        public static NetPlayer LookTarget(NetPlayer me)
        {
            if (me == null || me.Camera == null) return null;
            if (Cinematics.OutroDirector.PairTargetFor(me) is NetPlayer row) return row; // outro: the neighbour in the row
            Transform cam = me.Camera.transform;
            Vector3 eye = me.EyePosition;
            NetPlayer best = null;
            float bestScore = float.MaxValue;
            foreach (var o in NetPlayer.All)
            {
                if (o == null || o == me || !o.IsSpawned || o.Life.Value != LifeState.Alive) continue;
                Vector3 chest = o.transform.position + Vector3.up * 1.25f;
                Vector3 to = chest - eye;
                Vector3 flatTo = new Vector3(to.x, 0f, to.z), flatFwd = new Vector3(cam.forward.x, 0f, cam.forward.z);
                float dist = new Vector2(o.transform.position.x - me.transform.position.x, o.transform.position.z - me.transform.position.z).magnitude;
                if (dist > Emotes.AskRange || Mathf.Abs(o.transform.position.y - me.transform.position.y) > 0.8f) continue;
                // Mostly the direction on the floor plan counts (looking a bit up or down still picks the player).
                float ang = Vector3.Angle(flatFwd, flatTo);
                if (ang > 28f || cam.forward.y < -0.85f) continue;
                if (Physics.Linecast(eye, chest, out var hit, SightMask, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<NetPlayer>() != o) continue;
                float score = ang + dist * 6f;
                if (score < bestScore) { bestScore = score; best = o; }
            }
            return best;
        }
    }
}
