using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testspecinv &lt;dir&gt; (host = seeker with -forceseeker, clients "Klient" and "Klient2" = hiders, all three get the
    /// switch): the player list shows the fuse icon (1 fuse) and icon + number (3 fuses) to the hiders but not to the
    /// seeker; "Klient" is killed and, spectating "Klient2", sees his inventory on the hotbar. Also logs the round's
    /// phones (4-6) and toy guns (2-4, 13 shots). Logs: [SpecInvTest] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static void SiLog(string s) => Debug.Log("[SpecInvTest] " + s);

        private static NetPlayer ByNick(string nick)
        {
            foreach (var p in NetPlayer.All) if (p != null && p.Nick == nick) return p;
            return null;
        }

        private IEnumerator SpecInvTest(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (Manager.IsServer)
            {
                var h1 = ByNick("Klient");
                var h2 = ByNick("Klient2");
                if (h1 == null || h2 == null) { SiLog("need clients Klient and Klient2"); yield break; }
                int guns = mm.ToyGuns.Count;
                bool ammo = true;
                for (int i = 0; i < guns; i++) if (mm.ToyGuns[i].Ammo != ToyTuning.Shots) ammo = false;
                SiLog($"phones={mm.Phones.Count} (4-6) -> {Pf(mm.Phones.Count >= 4 && mm.Phones.Count <= 6)}");
                SiLog($"toy guns={guns} (2-4), shots={ToyTuning.Shots} -> {Pf(guns >= 2 && guns <= 4 && ammo && ToyTuning.Shots == 13)}");
                h1.FuseMask.Value = 1 << 2;
                h2.FuseMask.Value = (1 << 0) | (1 << 3) | (1 << 5);
                h2.MedkitCount.Value = 2;
                h2.WireCount.Value = 1;
                yield return new WaitForSeconds(2f);
                SiLog($"fuses: {h1.Nick}={h1.FuseCount} {h2.Nick}={h2.FuseCount}");
                Shot(dir, "h_list_seeker");
                yield return new WaitForSeconds(5f);
                mm.TestKill(h1);
                SiLog($"killed {h1.Nick}");
                yield break;
            }
            yield return new WaitForSeconds(1.5f);
            Shot(dir, me.Nick == "Klient2" ? "c2_list_hider" : "c1_list_hider");
            if (me.Nick != "Klient") yield break;
            float t0 = Time.time;
            while (me.Life.Value != LifeState.Dead && Time.time - t0 < 20f) yield return null;
            yield return new WaitForSeconds(3f);
            for (int k = 0; k < 4 && (GameHud.Spectated == null || GameHud.Spectated.Nick != "Klient2"); k++)
            {
                SpectatorCamera.TestNext = true;
                yield return new WaitForSeconds(0.8f);
            }
            var w = GameHud.Spectated;
            yield return new WaitForSeconds(1.5f);
            int items = 0;
            for (int s = 0; w != null && s < Hotbar.Slots; s++) if (Hotbar.ItemAt(w, s) != HotbarItem.None) items++;
            SiLog($"client dead={me.Life.Value == LifeState.Dead} spectating={(w != null ? w.Nick : "-")} items in his slots={items} -> {Pf(w != null && w.Nick == "Klient2" && items >= 3)}");
            Shot(dir, "c1_spectate_inventory");
        }
    }
}
