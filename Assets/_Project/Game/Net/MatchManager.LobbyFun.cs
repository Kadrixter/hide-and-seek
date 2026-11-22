using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby easter eggs: the fork on the coffee table (into a wall socket: short circuit, electrocution, respawn)
    /// and the SNAKE arcade constants (the game itself: MatchManager.Snake.cs).
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<ulong> ForkHolder = new(ulong.MaxValue);
        public readonly NetworkVariable<ulong> ArcadePlayer = new(ulong.MaxValue);

        public static readonly Vector3 ForkTablePos = new Vector3(-1.05f, 0.455f, 0.48f);
        public const float ForkTableYaw = 32f;

        /// <summary>Wall sockets in the lobby: plate centre and the direction it faces (into the room).</summary>
        public static readonly Vector3[] OutletPos =
        {
            new Vector3(-0.3f, 0.3f, 5.985f), new Vector3(-7.985f, 0.3f, -1.4f), new Vector3(7.985f, 0.3f, 1.8f), new Vector3(-2.6f, 0.3f, -5.985f),
        };
        public static readonly Vector3[] OutletNormal = { Vector3.back, Vector3.right, Vector3.left, Vector3.forward };

        public static readonly Vector3 ArcadeStand = new Vector3(6.0f, 0f, 4.6f);
        public static readonly Vector3 ArcadeStickBall = new Vector3(5.83f, 1.124f, 5.054f); // builder: MultiplayerBuilder.LobbyArcade.cs
        public static readonly Vector3 ArcadeButtons = new Vector3(6.075f, 1.049f, 5.058f);
        public static readonly Vector3 ArcadeScreen = new Vector3(6.0f, 1.35f, 5.14f);

        public static bool InLobbyScene => SceneManager.GetActiveScene().name == NetSession.LobbyScene;

        private GameObject _tableFork;

        // ================================================================ per frame (all peers)

        private void UpdateLobbyFun()
        {
            bool lobby = Phase.Value == MatchPhase.Lobby && InLobbyScene;
            if (lobby) { LobbyBricks.Ensure(); Boombox.Ensure(); }
            // Fork lying on the table while nobody holds it.
            if (lobby && _tableFork == null)
            {
                var prop = BlockyProps.Create(HeldItem.Fork, null, 0);
                _tableFork = prop.gameObject;
                _tableFork.name = "TableFork";
                _tableFork.transform.SetPositionAndRotation(ForkTablePos, Quaternion.Euler(0f, ForkTableYaw, 0f));
            }
            if (_tableFork != null)
            {
                bool show = lobby && ForkHolder.Value == ulong.MaxValue;
                if (_tableFork.activeSelf != show) _tableFork.SetActive(show);
            }

            if (!IsServer) return;
            if (!lobby)
            {
                if (ForkHolder.Value != ulong.MaxValue) ForkHolder.Value = ulong.MaxValue;
                if (ArcadePlayer.Value != ulong.MaxValue) StopArcade();
                return;
            }
            if (ForkHolder.Value != ulong.MaxValue && NetPlayer.Find(ForkHolder.Value) == null) ForkHolder.Value = ulong.MaxValue;
            if (ArcadePlayer.Value != ulong.MaxValue)
            {
                var ap = NetPlayer.Find(ArcadePlayer.Value);
                if (ap == null || Vector3.Distance(ap.transform.position, ArcadeStand) > 1.5f) StopArcade();
                else SnakeTick();
            }
        }

        // ================================================================ fork

        public void ServerPickupFork(NetPlayer pl)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || ForkHolder.Value != ulong.MaxValue || pl.Busy.Value) return;
            if (Vector3.Distance(pl.transform.position, ForkTablePos) > 2.4f) return;
            ForkHolder.Value = pl.NetworkObjectId;
            pl.Held.Value = HeldItem.Fork;
            pl.SfxRpc(GameSfx.UiClick, 0.6f);
        }

        public void ServerForkOutlet(NetPlayer pl, int idx)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || ForkHolder.Value != pl.NetworkObjectId || pl.Busy.Value) return;
            if (idx < 0 || idx >= OutletPos.Length) return;
            if (Vector3.Distance(pl.transform.position, OutletPos[idx]) > 2.2f) return;
            StartCoroutine(ShockRoutine(pl, idx));
        }

        private IEnumerator ShockRoutine(NetPlayer pl, int idx)
        {
            pl.Busy.Value = true;
            Vector3 n = OutletNormal[idx];
            Vector3 stand = OutletPos[idx] + n * 0.62f;
            stand.y = 0f;
            float yaw = Quaternion.LookRotation(-n).eulerAngles.y;
            pl.TeleportRpc(stand, yaw);
            yield return new WaitForSeconds(0.1f);
            pl.ShockRpc((byte)idx);
            yield return new WaitForSeconds(ShockTimeline.Respawn);
            if (pl == null || !pl.IsSpawned) yield break;
            ForkHolder.Value = ulong.MaxValue;
            pl.Held.Value = HeldItem.None;
            var spawns = SpawnPoints.Lobby();
            if (spawns.Count > 0)
            {
                var sp = spawns[Random.Range(0, spawns.Count)];
                pl.TeleportRpc(sp.position, sp.eulerAngles.y);
            }
            pl.Busy.Value = false;
        }

        // ================================================================ exit door

        private bool _exitBusy;

        public void ServerExitDoor(NetPlayer pl)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || !InLobbyScene || _exitBusy || pl.Busy.Value || pl.Seat.Value >= 0) return;
            if (ForkHolder.Value == pl.NetworkObjectId || ArcadePlayer.Value == pl.NetworkObjectId) return;
            var door = LobbyExitDoor.Instance;
            Vector3 handle = door != null ? door.HandleWorld : new Vector3(4.66f, 1f, -5.93f);
            if (Vector3.Distance(pl.transform.position + Vector3.up, handle) > 2.6f) return;
            StartCoroutine(ExitDoorRoutine(pl));
        }

        private IEnumerator ExitDoorRoutine(NetPlayer pl)
        {
            _exitBusy = true;
            pl.Busy.Value = true;
            pl.TeleportRpc(LobbyExitDoor.StandPos, LobbyExitDoor.StandYaw);
            yield return new WaitForSeconds(0.1f);
            if (pl != null && pl.IsSpawned) pl.ExitDoorRpc();
            yield return new WaitForSeconds(ExitTimeline.Respawn);
            _exitBusy = false;
            if (pl == null || !pl.IsSpawned) yield break;
            var spawns = SpawnPoints.Lobby();
            if (spawns.Count > 0)
            {
                var sp = spawns[Random.Range(0, spawns.Count)];
                pl.TeleportRpc(sp.position, sp.eulerAngles.y);
            }
            pl.Busy.Value = false;
        }
    }
}
