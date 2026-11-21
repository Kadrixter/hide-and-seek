using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server side of the crates in front of the security rooms (<see cref="SecurityCrates"/>: 2 bits per level, see
    /// <see cref="SecurityRooms.StateOf"/>). Hiders push the crate aside or back (from outside or from the doorway);
    /// the seeker cannot move it, but cuts it apart with the chainsaw: the door then stays open until the round ends.
    /// </summary>
    public partial class MatchManager
    {
        private readonly float[] _crateBusyUntil = new float[3];

        /// <summary>A player asks for <paramref name="action"/> on the crate of level <paramref name="lv"/>.</summary>
        public void ServerCrateAction(NetPlayer pl, byte lv, byte action)
        {
            if (pl == null || lv > 2 || action > (byte)CrateAction.Saw || SecurityRooms.Instance == null) return;
            if (Phase.Value != MatchPhase.Hiding && Phase.Value != MatchPhase.Seeking) return;
            if (pl.Life.Value != LifeState.Alive || pl.Busy.Value || Time.time < _crateBusyUntil[lv]) return;
            var act = (CrateAction)action;
            bool seeker = pl.IsSeeker;
            bool sawOut = seeker && pl.HasChainsaw.Value && pl.Held.Value == HeldItem.Chainsaw && Phase.Value == MatchPhase.Seeking;
            if ((act == CrateAction.Saw) != seeker) return;   // the seeker only cuts, hiders only push / pull
            if (!SecurityRooms.Instance.ServerCanAct(lv, act, pl.transform.position, seeker, sawOut)) return;
            if (act != CrateAction.Saw && SecurityRooms.PathBlocked(lv, act, pl))
            {
                pl.ToastRpc("Ktoś stoi na drodze skrzyni.", 2.5f);
                return;
            }
            float time = act == CrateAction.Saw ? BlockyCharacterActions.CrateSawTime : SecurityRooms.CratePushTime;
            _crateBusyUntil[lv] = Time.time + time + 0.2f;
            StartCoroutine(CrateRoutine(pl, lv, act));
        }

        private IEnumerator CrateRoutine(NetPlayer pl, byte lv, CrateAction act)
        {
            pl.Busy.Value = true;
            ServerRcForcePark(pl);
            Vector3 stand = SecurityRooms.ActionStand(lv, act, out float yaw);
            pl.TeleportRpc(stand, yaw);
            yield return new WaitForSeconds(0.05f);
            if (act != CrateAction.Saw)
            {
                bool opening = act == CrateAction.OpenOutside || act == CrateAction.OpenInside;
                SecurityCrates.Value = SecurityRooms.WithState(SecurityCrates.Value, lv, opening ? CrateState.Open : CrateState.Closed);
            }
            pl.CrateActionRpc(lv, (byte)act);
            Debug.Log($"[Crate] level {lv}: {pl.name} {act}");
            if (act == CrateAction.Saw)
            {
                yield return new WaitForSeconds(BlockyCharacterActions.CrateSawBreakTime);
                SecurityCrates.Value = SecurityRooms.WithState(SecurityCrates.Value, lv, CrateState.Broken);
                yield return new WaitForSeconds(BlockyCharacterActions.CrateSawTime - BlockyCharacterActions.CrateSawBreakTime);
            }
            else yield return new WaitForSeconds(SecurityRooms.CratePushTime);
            if (pl != null && pl.IsSpawned) pl.Busy.Value = false;
        }
    }
}
