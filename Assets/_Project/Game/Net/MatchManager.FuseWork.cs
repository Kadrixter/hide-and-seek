using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server side of working on a fuse box (MatchManager.Fuses): a hider puts a fuse in (E, <see cref="FuseTimeline.InEnd"/>
    /// s, the socket is filled at <see cref="FuseTimeline.InClick"/>), anybody but a locked box takes one out (E held for
    /// <see cref="FuseTimeline.OutPop"/> s, letting go stops it): a hider gets it into the inventory (or it falls in front
    /// of the box when there is no room), the seeker throws it on the floor. A full box is checked right after the click.
    /// </summary>
    public partial class MatchManager
    {
        private readonly HashSet<ulong> _fuseCancel = new();

        /// <summary>Why <paramref name="pl"/> cannot work on box <paramref name="box"/> right now (null = can).</summary>
        public string FuseWorkBlock(NetPlayer pl, int box)
        {
            if (pl == null || !FusePhase || RaceMode || pl.Life.Value != LifeState.Alive || !(pl.IsHider || pl.IsSeeker)) return "phase";
            if (box < 0 || box >= FuseTuning.Boxes || !FuseBoxes.Value[box].Placed) return "box";
            if (pl.Busy.Value || pl.Seat.Value >= 0 || pl.RcAnimating || pl.WireHandsBusy || pl.FuseWork.Value.Active) return "busy";
            if (pl.IsSeeker && Phase.Value != MatchPhase.Seeking) return "phase";
            return null;
        }

        private bool FuseInReach(NetPlayer pl, in FuseBox b)
        {
            Vector3 stand = b.World(FuseBoxModel.StandLocal), at = pl.transform.position;
            return Mathf.Abs(at.y - b.Pos.y) < 0.8f && Vector2.Distance(new Vector2(at.x, at.z), new Vector2(stand.x, stand.z)) <= FuseTuning.Reach;
        }

        private bool SocketTaken(int box, int socket, NetPlayer except)
        {
            foreach (var o in NetPlayer.All)
                if (o != null && o != except && o.FuseWork.Value.Active && o.FuseWork.Value.Box == box && o.FuseWork.Value.Socket == socket) return true;
            return false;
        }

        // ================================================================ putting a fuse in

        public void ServerFuseInsert(NetPlayer pl, byte box, byte socket)
        {
            if (FuseWorkBlock(pl, box) != null || !pl.IsHider || socket >= FuseTuning.Sockets) return;
            var b = FuseBoxes.Value[box];
            if (b.Locked || b.Has(socket) || !FuseInReach(pl, b)) return;
            int sym = b.Symbol(socket);
            if (!pl.HasFuse(sym)) { pl.ToastRpc("Nie masz bezpiecznika z tym znaczkiem.", 2f); return; }
            if (SocketTaken(box, socket, pl)) { pl.ToastRpc("Ktoś już przy tym gnieździe.", 2f); return; }
            pl.FuseWork.Value = new FuseWorkState { Box = (sbyte)box, Socket = socket, Remove = false, Symbol = (byte)sym, Start = Now };
            StartCoroutine(FuseInsertRoutine(pl, box, socket, sym));
            Debug.Log($"[Fuses] {pl.Nick} puts fuse {FuseSymbols.Name(sym)} into box {box} socket {socket}");
        }

        private IEnumerator FuseInsertRoutine(NetPlayer pl, int box, int socket, int sym)
        {
            double start = Now;
            string why = null;
            bool done = false;
            while (Now - start < FuseTimeline.InEnd)
            {
                yield return null;
                if (pl == null || !pl.IsSpawned) yield break;
                if (!done)
                {
                    why = FuseInterrupted(pl, start);
                    if (why != null) break;
                    if (Now - start >= FuseTimeline.InClick)
                    {
                        done = true;
                        var b = FuseBoxes.Value[box];
                        if (b.Has(socket) || b.Locked || !pl.HasFuse(sym)) { why = "socket taken"; break; }
                        pl.FuseMask.Value = (byte)(pl.FuseMask.Value & ~(1 << sym));
                        b.Filled = (byte)(b.Filled | (1 << socket));
                        SetFuseBox(box, b);
                        Debug.Log($"[Fuses] {pl.Nick}: fuse {FuseSymbols.Name(sym)} in box {box} socket {socket} ({b.FilledCount()}/4)");
                        if (b.Full) FuseBoxFull(box, pl);
                    }
                }
                else if (pl.Life.Value != LifeState.Alive) break;
            }
            if (pl == null || !pl.IsSpawned) yield break;
            pl.FuseWork.Value = FuseWorkState.None;
            if (why != null)
            {
                Debug.Log($"[Fuses] {pl.Nick}: putting the fuse in interrupted ({why})");
                pl.ToastRpc(why == "socket taken" ? "Gniazdo jest już zajęte." : "Przerwane.", 1.5f);
            }
        }

        // ================================================================ taking a fuse out

        public void ServerFuseRemove(NetPlayer pl, byte box, byte socket)
        {
            if (FuseWorkBlock(pl, box) != null || socket >= FuseTuning.Sockets) return;
            var b = FuseBoxes.Value[box];
            if (!b.Has(socket) || !FuseInReach(pl, b)) return;
            if (b.Locked) { pl.ToastRpc("Skrzynka jest zablokowana - bezpieczników nie da się wyjąć.", 2.5f); return; }
            if (SocketTaken(box, socket, pl)) { pl.ToastRpc("Ktoś już przy tym gnieździe.", 2f); return; }
            pl.FuseWork.Value = new FuseWorkState { Box = (sbyte)box, Socket = socket, Remove = true, Symbol = (byte)b.Symbol(socket), Start = Now };
            _fuseCancel.Remove(pl.NetworkObjectId);
            StartCoroutine(FuseRemoveRoutine(pl, box, socket));
            Debug.Log($"[Fuses] {pl.Nick} pulls the fuse out of box {box} socket {socket}");
        }

        public void ServerFuseCancel(NetPlayer pl)
        {
            if (pl != null) _fuseCancel.Add(pl.NetworkObjectId);
        }

        private IEnumerator FuseRemoveRoutine(NetPlayer pl, int box, int socket)
        {
            ulong id = pl.NetworkObjectId;
            double start = Now;
            string why = null;
            bool done = false;
            while (Now - start < FuseTimeline.OutEnd)
            {
                yield return null;
                if (pl == null || !pl.IsSpawned) { _fuseCancel.Remove(id); yield break; }
                if (done) { if (pl.Life.Value != LifeState.Alive) break; continue; }
                why = FuseInterrupted(pl, start);
                if (why == null && _fuseCancel.Remove(id)) why = "let go";
                if (why == null && FuseBoxes.Value[box].Locked) why = "locked";
                if (why != null) break;
                if (Now - start < FuseTimeline.OutPop) continue;
                done = true;
                var b = FuseBoxes.Value[box];
                if (!b.Has(socket)) { why = "gone"; break; }
                int sym = b.Symbol(socket);
                b.Filled = (byte)(b.Filled & ~(1 << socket));
                if (b.Lit && b.Empty) b.Flags = (byte)(b.Flags & ~FuseBox.FlagLit);
                SetFuseBox(box, b);
                if (pl.IsHider && Inventory.HasRoomFor(pl, FuseItems.Of(sym))) pl.FuseMask.Value = (byte)(pl.FuseMask.Value | (1 << sym));
                else
                {
                    FuseToFloor(b, socket, sym);
                    if (pl.IsHider) pl.ToastRpc(Inventory.FullText, 2.5f);
                }
                Debug.Log($"[Fuses] {pl.Nick}: fuse {FuseSymbols.Name(sym)} out of box {box} socket {socket} ({b.FilledCount()}/4{(b.Lit ? ", lights still on" : "")})");
            }
            _fuseCancel.Remove(id);
            if (pl == null || !pl.IsSpawned) yield break;
            pl.FuseWork.Value = FuseWorkState.None;
            if (why != null) Debug.Log($"[Fuses] {pl.Nick}: pulling the fuse out stopped ({why})");
            if (why != null && why != "let go") pl.ToastRpc("Przerwane.", 1.5f);
        }

        /// <summary>A pulled-out fuse falls out of the socket in front of the box (seeker, or no room in the inventory).</summary>
        private void FuseToFloor(in FuseBox b, int socket, int sym)
        {
            Vector3 from = b.World(FuseBoxModel.FuseLocal(socket, 0.18f));
            Vector3 vel = b.Normal * Random.Range(1.2f, 1.8f) + Vector3.up * 0.8f + b.Rot * Vector3.right * Random.Range(-0.4f, 0.4f);
            Vector3 feet = b.World(FuseBoxModel.StandLocal);
            SpawnDrop(FuseItems.Of(sym), 0, from, b.Rot * Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(-40f, 40f), 0f), vel, feet);
        }

        private string FuseInterrupted(NetPlayer pl, double start)
        {
            if (pl.Life.Value != LifeState.Alive) return "downed";
            if (pl.Busy.Value) return "attacked";
            if (!FusePhase) return "round over";
            if (!pl.FuseWork.Value.Active) return "state changed";
            var b = FuseBoxes.Value[pl.FuseWork.Value.Box];
            if (Now - start > 0.45 && !FuseInReach(pl, b)) return "walked away";
            return null;
        }

        private void SetFuseBox(int box, FuseBox b)
        {
            var set = FuseBoxes.Value;
            set[box] = b;
            FuseBoxes.Value = set;
        }

        // ================================================================ a full box

        private void FuseBoxFull(int box, NetPlayer by)
        {
            var b = FuseBoxes.Value[box];
            if (box == _fuseGood)
            {
                b.Flags = (byte)(b.Flags | FuseBox.FlagLocked);
                SetFuseBox(box, b);
                Debug.Log($"[Fuses] RIGHT box {box} full ({by.Nick}) - locked, escape stage -> DotLink");
                FuseBoxEventRpc((byte)box, true);
                ServerEscapeAdvance(EscapeStage.DotLink);
            }
            else
            {
                b.Flags = (byte)(b.Flags | FuseBox.FlagLit);
                SetFuseBox(box, b);
                Debug.Log($"[Fuses] wrong box {box} full ({by.Nick}) - every light of level {b.Level} on");
                FuseBoxEventRpc((byte)box, false);
            }
        }

        /// <summary>Everybody: the effect of a full box (the lasting state - lamp, lock, lights - comes from <see cref="FuseBoxes"/>).</summary>
        [Rpc(SendTo.Everyone)]
        private void FuseBoxEventRpc(byte box, bool right)
        {
            var b = FuseBoxes.Value[box];
            FuseBoxViews.PlayEvent(box, right);
            var me = NetPlayer.Local;
            if (me == null) return;
            if (right)
            {
                if (me.IsHider) GameHud.Banner("PRĄD WRÓCIŁ", "Skrzynka działa. Idź do urwanej dźwigni przy bramie WYJŚCIE.", 5f, new Color(0.45f, 1f, 0.5f));
                else GameHud.Banner("PRĄD WRÓCIŁ", "Chowający naprawili skrzynkę z bezpiecznikami. Pilnuj bramy WYJŚCIE!", 5f, new Color(1f, 0.55f, 0.35f));
            }
            else if (HideAndSeek.Map.MapInfo.LevelOf(me.transform.position.y) == b.Level)
                GameHud.Toast("Zła skrzynka! Zapaliły się wszystkie światła na tym poziomie.", 4f);
        }
    }
}
