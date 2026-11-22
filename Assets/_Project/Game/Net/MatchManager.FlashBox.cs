using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby flashlight box (server side): E at <see cref="FlashlightBox"/> takes a lamp (player becomes ready once it is
    /// clipped onto the shoulder) or puts it back (not ready once it is let go in the box). The player stands at the
    /// box (teleported to its stand point) and is busy for the length of the animation. R no longer sets ready.
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Called from Update on every instance: the box exists in the lobby scene.</summary>
        private void UpdateFlashBox()
        {
            if (Phase.Value == MatchPhase.Lobby && InLobbyScene) FlashlightBox.Ensure();
        }

        public void ServerFlashBox(NetPlayer pl, bool take)
        {
            if (!IsServer || pl == null || Phase.Value != MatchPhase.Lobby || FlashlightBox.Instance == null) return;
            if (pl.Busy.Value || pl.Life.Value != LifeState.Alive || pl.Seat.Value >= 0) return;
            if (ForkHolder.Value == pl.NetworkObjectId || ArcadePlayer.Value == pl.NetworkObjectId) return;
            if (pl.Ready.Value == take) return;
            Vector3 flat = pl.transform.position - FlashlightBox.StandPos;
            flat.y = 0f;
            if (flat.magnitude > 3.2f) { Debug.Log($"[FlashBox] {pl.Nick} too far from the box ({flat.magnitude:0.0} m)"); return; }
            pl.Busy.Value = true;
            pl.TeleportRpc(FlashlightBox.StandPos, FlashlightBox.StandYaw);
            pl.FlashBoxRpc(take);
            StartCoroutine(FlashBoxRoutine(pl, take));
            Debug.Log($"[FlashBox] {pl.Nick} {(take ? "takes a lamp" : "puts the lamp back")}");
        }

        private IEnumerator FlashBoxRoutine(NetPlayer pl, bool take)
        {
            float start = Time.time;
            float mark = take ? FlashTakeTimeline.TakeClip : FlashTakeTimeline.PutRelease;
            float end = FlashTakeTimeline.Duration(take);
            bool applied = false;
            while (Time.time - start < end)
            {
                yield return null;
                if (pl == null) yield break;
                if (Phase.Value != MatchPhase.Lobby) break;
                if (!applied && Time.time - start >= mark)
                {
                    applied = true;
                    pl.Ready.Value = take;
                    Debug.Log($"[FlashBox] {pl.Nick} ready={take}");
                }
            }
            if (pl == null) yield break;
            if (!applied && Phase.Value == MatchPhase.Lobby) pl.Ready.Value = take;
            pl.Busy.Value = false;
        }
    }
}
