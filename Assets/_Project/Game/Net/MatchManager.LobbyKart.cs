using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server side of the drift track's go-karts (<see cref="LobbyKart"/>, <see cref="DriftTrack"/>): one driver per
    /// kart, one kart per player. E next to a free kart (living player in the lobby, not busy / seated / at the arcade
    /// / holding the fork / at a board game, within 3 m) gets in; E again gets out and the kart stays where it stopped
    /// (on the pad, else back in its slot). A driver is put out when he leaves, dies, gets busy or sits down somewhere;
    /// a kart goes back in its slot (its epoch + 1) when a round starts, when its driver asks (R / stuck) or when it
    /// ends up off the pad.
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<KartsState> KartsVar = new(KartsState.AtSlots(0));

        private System.Action _stepKart;
        private readonly float[] _kartOutSince = { -1f, -1f, -1f, -1f, -1f };

        /// <summary>MatchManager.Update hook (every instance): the track and its karts exist in the lobby scene; server rules.</summary>
        private void UpdateLobbyKart()
        {
            if (InLobbyScene)
            {
                DriftTrack.Ensure();
                LobbyKart.EnsureAll();
            }
            if (IsServer) ServerKartTick(Phase.Value == MatchPhase.Lobby && InLobbyScene);
        }

        /// <summary>Why <paramref name="p"/> may not get into a kart now (null = he may).</summary>
        public string KartRefusal(NetPlayer p)
        {
            if (p == null) return "nobody";
            if (Phase.Value != MatchPhase.Lobby || !InLobbyScene) return "not in the lobby";
            if (p.Life.Value != LifeState.Alive) return "not alive";
            if (p.Busy.Value) return "busy";
            if (p.Seat.Value >= 0) return "on a seat";
            if (ArcadeCabOf(p.NetworkObjectId) >= 0) return "at the arcade";
            if (ForkHolder.Value == p.NetworkObjectId) return "holds the fork";
            if (MiniSeatOf(p.NetworkObjectId).game >= 0) return "at a board game";
            if (p.BodyCarryBusy) return "carrying";
            if (KartsVar.Value.KartOf(p.NetworkObjectId) >= 0) return "already driving";
            return null;
        }

        [Rpc(SendTo.Server)]
        public void KartRequestRpc(byte kart, bool enter, Vector3 pos, float yaw, RpcParams rpc = default)
        {
            var p = NetPlayer.FindByClient(rpc.Receive.SenderClientId);
            if (p == null || kart >= DriftTrackLayout.KartCount) return;
            var all = KartsVar.Value;
            var s = all.Get(kart);
            if (!enter)
            {
                if (s.Driver != p.NetworkObjectId) return;
                bool inside = LobbyKart.OnPad(pos);
                s.Driver = KartState.Free;
                if (inside) { s.Pos = pos; s.Yaw = Mathf.Repeat(yaw, 360f); }
                else s = KartState.AtSlot(kart, (byte)(s.Epoch + 1));
                KartsVar.Value = all.With(kart, s);
                Debug.Log($"[Kart] {p.Nick} got out of kart {kart} at {s.Pos} (on the pad {inside})");
                return;
            }
            string why = s.Driven ? "taken" : KartRefusal(p);
            Vector3 d = p.transform.position - s.Pos;
            if (why == null && (new Vector2(d.x, d.z).magnitude > 3f || Mathf.Abs(d.y) > 1.2f)) why = $"too far ({new Vector2(d.x, d.z).magnitude:0.0} m)";
            if (why != null) { Debug.Log($"[Kart] {p.Nick} cannot get into kart {kart}: {why}"); return; }
            s.Driver = p.NetworkObjectId;
            KartsVar.Value = all.With(kart, s);
            Debug.Log($"[Kart] {p.Nick} gets into kart {kart} at {s.Pos}");
        }

        /// <summary>The driver (or anybody, when it stands free) puts kart <paramref name="kart"/> back in its slot.</summary>
        [Rpc(SendTo.Server)]
        public void KartResetRpc(byte kart, RpcParams rpc = default)
        {
            var p = NetPlayer.FindByClient(rpc.Receive.SenderClientId);
            if (p == null || kart >= DriftTrackLayout.KartCount) return;
            var s = KartsVar.Value.Get(kart);
            if (s.Driven && s.Driver != p.NetworkObjectId) return;
            ServerKartToSlot(kart, s.Driver, "asked by " + p.Nick);
        }

        private void ServerKartToSlot(int kart, ulong driver, string why)
        {
            var all = KartsVar.Value;
            var s = KartState.AtSlot(kart, (byte)(all.Get(kart).Epoch + 1));
            s.Driver = driver;
            KartsVar.Value = all.With(kart, s);
            Debug.Log($"[Kart] kart {kart} back in its slot ({why}), epoch {s.Epoch}");
        }

        private void ServerKartTick(bool lobby)
        {
            for (int i = 0; i < DriftTrackLayout.KartCount; i++)
            {
                var s = KartsVar.Value.Get(i);
                if (!lobby)
                {
                    // Round (or another scene): nobody drives, the karts wait in their slots for the next lobby.
                    if (s.Driven || (s.Pos - DriftTrackLayout.KartSlot(i)).sqrMagnitude > 1e-4f || Mathf.Abs(Mathf.DeltaAngle(s.Yaw, DriftTrackLayout.KartSlotYaw)) > 0.1f)
                        ServerKartToSlot(i, KartState.Free, "not the lobby");
                    continue;
                }
                if (s.Driven)
                {
                    var p = NetPlayer.Find(s.Driver);
                    string why = p == null || !p.IsSpawned ? "left" : p.Life.Value != LifeState.Alive ? "not alive" : p.Busy.Value ? "busy"
                        : p.Seat.Value >= 0 ? "sat down" : ArcadeCabOf(p.NetworkObjectId) >= 0 ? "at the arcade" : null;
                    if (why != null)
                    {
                        var pk = p != null ? p.KartDrive.Value : default;
                        bool use = pk.Valid && pk.Index == i && pk.Epoch == s.Epoch && LobbyKart.OnPad(pk.Pos);
                        s.Driver = KartState.Free;
                        if (use) { s.Pos = pk.Pos; s.Yaw = pk.YawDeg; }
                        KartsVar.Value = KartsVar.Value.With(i, s);
                        Debug.Log($"[Kart] driver of kart {i} put out ({why}), kart at {s.Pos}");
                        continue;
                    }
                    // The driver's kart off the pad for half a second: back in its slot (he stays in).
                    var pack = p.KartDrive.Value;
                    bool outside = pack.Valid && pack.Index == i && pack.Epoch == s.Epoch && !LobbyKart.OnPad(pack.Pos);
                    if (!outside) _kartOutSince[i] = -1f;
                    else if (_kartOutSince[i] < 0f) _kartOutSince[i] = Time.time;
                    else if (Time.time - _kartOutSince[i] > 0.5f) { _kartOutSince[i] = -1f; ServerKartToSlot(i, s.Driver, "off the pad"); }
                    continue;
                }
                if (!LobbyKart.OnPad(s.Pos)) ServerKartToSlot(i, KartState.Free, "parked off the pad");
            }
        }
    }
}
