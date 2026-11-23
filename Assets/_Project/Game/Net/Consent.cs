using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>What a yes/no question between two players is about (decides only the logic on the server).</summary>
    public enum ConsentKind : byte
    {
        Emote = 1,
        Carry = 2,
    }

    public enum ConsentAnswer : byte
    {
        Yes = 0,
        No = 1,
        Timeout = 2,
        Cancelled = 3,
    }

    /// <summary>
    /// Server side of the generic "may I?" question from one player to another (pair emotes, carrying a player...).
    /// <see cref="Ask"/> shows the asked player a prompt "&lt;nick&gt; &lt;text&gt; - [Y] tak / [N] nie" with a countdown
    /// (<see cref="UI.ConsentPrompt"/>), the asking player a small "waiting for..." line; the answer (or the timeout /
    /// a cancel) comes back to the callback on the server exactly once. One open question per asked player and one
    /// per asking player: a newer one cancels the older one. Ticked from <see cref="NetPlayer"/> on the server.
    /// </summary>
    public static class Consent
    {
        public const float DefaultTimeout = 10f;

        sealed class Pending
        {
            public uint Id;
            public NetPlayer From, To;
            public ConsentKind Kind;
            public byte Arg;
            public float Until;
            public Func<bool> StillValid;
            public Action<ConsentAnswer> Done;
        }

        static readonly Dictionary<uint, Pending> open = new();
        static readonly List<uint> tmp = new();
        static uint nextId = 1;
        static int tickedFrame = -1;

        /// <summary>
        /// Server: asks <paramref name="to"/>. <paramref name="text"/> follows the asker's nick in the prompt
        /// ("chce się przytulić"). <paramref name="stillValid"/> (optional) is checked every frame; false cancels the
        /// question (walked away, died...). Returns the question id (0 = could not ask).
        /// </summary>
        public static uint Ask(NetPlayer from, NetPlayer to, ConsentKind kind, byte arg, string text, Action<ConsentAnswer> done,
            Func<bool> stillValid = null, float timeout = DefaultTimeout)
        {
            if (from == null || to == null || from == to || !from.IsServer) return 0;
            // One question at a time for both sides.
            foreach (var p in open.Values)
                if (p.To == to || p.From == from || p.To == from || p.From == to) tmp.Add(p.Id);
            foreach (var id0 in tmp) Finish(id0, ConsentAnswer.Cancelled);
            tmp.Clear();

            uint id = nextId++;
            if (nextId == 0) nextId = 1;
            open[id] = new Pending
            {
                Id = id, From = from, To = to, Kind = kind, Arg = arg, Done = done, StillValid = stillValid,
                Until = Time.unscaledTime + timeout,
            };
            to.ConsentAskRpc(id, from.NetworkObjectId, (byte)kind, arg, text ?? "", timeout);
            from.ConsentWaitRpc(id, to.NetworkObjectId, timeout);
            Debug.Log($"[Consent] #{id} {from.Nick} -> {to.Nick}: {kind}/{arg} '{text}'");
            return id;
        }

        /// <summary>Server: is there an open question between these two (either way)?</summary>
        public static bool Asking(NetPlayer a, NetPlayer b)
        {
            foreach (var p in open.Values)
                if ((p.From == a && p.To == b) || (p.From == b && p.To == a)) return true;
            return false;
        }

        /// <summary>Server: withdraws question <paramref name="id"/> (the callback gets Cancelled).</summary>
        public static void Cancel(uint id) => Finish(id, ConsentAnswer.Cancelled);

        /// <summary>Server: the asker withdraws its own question.</summary>
        internal static void Withdraw(NetPlayer from, uint id)
        {
            if (open.TryGetValue(id, out var p) && p.From == from) Finish(id, ConsentAnswer.Cancelled);
        }

        /// <summary>Server: withdraws every question this player asked or was asked (left, died, round over).</summary>
        public static void CancelFor(NetPlayer p)
        {
            foreach (var q in open.Values)
                if (q.From == p || q.To == p) tmp.Add(q.Id);
            foreach (var id in tmp) Finish(id, ConsentAnswer.Cancelled);
            tmp.Clear();
        }

        /// <summary>Server: the asked player's owner answered (RPC checked the sender).</summary>
        internal static void Answer(NetPlayer to, uint id, bool yes)
        {
            if (!open.TryGetValue(id, out var p) || p.To != to) return;
            Finish(id, yes ? ConsentAnswer.Yes : ConsentAnswer.No);
        }

        /// <summary>Server, once per frame: timeouts and validity.</summary>
        internal static void Tick()
        {
            if (tickedFrame == Time.frameCount || open.Count == 0) return;
            tickedFrame = Time.frameCount;
            float now = Time.unscaledTime;
            foreach (var p in open.Values)
            {
                bool gone = p.From == null || p.To == null || !p.From.IsSpawned || !p.To.IsSpawned;
                if (gone) tmp.Add(p.Id);
                else if (now >= p.Until) tmp.Add(p.Id);
                else
                {
                    bool valid = true;
                    try { valid = p.StillValid == null || p.StillValid(); }
                    catch (Exception e) { Debug.LogException(e); valid = false; }
                    if (!valid) tmp.Add(p.Id);
                }
            }
            foreach (var id in tmp)
                if (open.TryGetValue(id, out var p)) Finish(id, now >= p.Until ? ConsentAnswer.Timeout : ConsentAnswer.Cancelled);
            tmp.Clear();
        }

        static void Finish(uint id, ConsentAnswer answer)
        {
            if (!open.TryGetValue(id, out var p)) return;
            open.Remove(id);
            Debug.Log($"[Consent] #{id} {answer}");
            if (p.To != null && p.To.IsSpawned) p.To.ConsentCloseRpc(id, (byte)answer);
            if (p.From != null && p.From.IsSpawned) p.From.ConsentResultRpc(id, (byte)answer, p.To != null ? p.To.NetworkObjectId : 0);
            try { p.Done?.Invoke(answer); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
