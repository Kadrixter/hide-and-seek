using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    public sealed partial class DotLinkProbe
    {
        string BoardLine(MatchManager mm)
        {
            var b = DotLinkPanel.Instance != null ? DotLinkPanel.Instance.NetBoard : null;
            var u = mm.DotLinkUserPlayer;
            return $"stage={mm.Escape.Value} user={(u != null ? u.Nick : "-")} " +
                   (b != null ? $"pairs={b.CompleteCount}/{b.Puzzle.Colors} filled={b.FilledCount}/{b.Puzzle.Cells} seq={b.Seq} solved={b.SolvedFlag}" : "no board");
        }

        IEnumerator Run(MatchManager mm, NetPlayer me)
        {
            bool h1 = _role == "hider1", h2 = _role == "hider2", seeker = _role == "seeker";
            NetPlayer pH2 = Find(p => p.IsHider && !p.IsOwnedByServer), pS = Find(p => p.IsSeeker);
            Check("roles", pH2 != null && pS != null && Find(p => p.IsHider && p.IsOwnedByServer) != null, $"h2={(pH2 != null ? pH2.Nick : "-")} seeker={(pS != null ? pS.Nick : "-")}");
            DotLinkLayout.TryGet(out var lay);
            Vector3 far = lay.Centre + lay.Normal * 7f + Vector3.up * 0.5f + Vector3.Cross(Vector3.up, lay.Normal) * 2.5f;

            // ---- before the stage: shut, dark, no prompt
            yield return At(0.5f);
            if (h1) StandAtBox(me, 1.1f, 0f);
            if (h2) StandAtBox(me, 2.6f, -2.2f);
            if (seeker) me.TeleportLocal(lay.Stand + new Vector3(lay.Normal.x, 0f, lay.Normal.z).normalized * 15f, 0f);
            yield return At(1.6f);
            var lpc = LocalPlayerController.Instance;
            if (h1)
            {
                Check("no prompt before DotLink", lpc.Prompt != PromptKind.DotLink, $"prompt={lpc.Prompt} '{lpc.PromptText}' stage={mm.Escape.Value}");
                Check("panel dark before DotLink", !DotLinkPanel.Instance.Lit);
                Render("dark_before", lay.Centre + lay.Normal * 1.3f + Vector3.up * 0.15f, lay.Centre, 45f, 1.2f);
                Shot("dark_before");
            }
            yield return At(2f);
            if (h1) mm.ServerEscapeAdvance(EscapeStage.DotLink); // test: the fuse part is somebody else's
            yield return At(4f);
            Check("panel lit at DotLink", DotLinkPanel.Instance.Lit && DotLinkPanel.Instance.Puzzle != null, BoardLine(mm));
            if (h1)
            {
                Check("prompt at DotLink", lpc.Prompt == PromptKind.DotLink, $"'{lpc.PromptText}'");
                Shot("prompt");
                Render("active_far", far, lay.Centre, 40f);
                Render("active_near", lay.Centre + lay.Normal * 1.2f + Vector3.up * 0.3f + Vector3.Cross(Vector3.up, lay.Normal) * 0.5f, lay.Centre, 40f);
            }
            var sol = DotLinkPanel.Instance.Puzzle != null ? Solution(DotLinkPanel.Instance.Puzzle) : null;
            int colors = sol != null ? sol.Length : 0, part1 = colors * 4 / 10, part2 = colors * 7 / 10;
            if (!seeker) Check("solution found", sol != null, $"colors={colors}");

            // ---- hider 1: 40% of the lines, one wiped with RMB and drawn again, Esc
            if (h1 && sol != null)
            {
                me.DotLinkUseRpc(true);
                yield return new WaitForSeconds(1.4f);
                Check("hider1 at the panel", me.AtDotLink && DotLinkLocal.Active, BoardLine(mm));
                Shot("empty");
                for (int c = 0; c < part1; c++) yield return DrawLine(sol[c]);
                yield return new WaitForSeconds(0.5f);
                Check("hider1 drew part 1", DotLinkPanel.Instance.NetBoard.CompleteCount == part1, BoardLine(mm));
                yield return RightClick(sol[part1 - 1][sol[part1 - 1].Length / 2]);
                Check("RMB wiped a line", DotLinkPanel.Instance.NetBoard.Path(part1 - 1).Count == 0, BoardLine(mm));
                yield return DrawLine(sol[part1 - 1]);
                yield return new WaitForSeconds(0.4f);
                Shot("half");
                DotLinkLocal.TestEsc = true;
                yield return new WaitForSeconds(0.8f);
                Check("Esc left, lines kept", !me.AtDotLink && DotLinkPanel.Instance.NetBoard.CompleteCount == part1, BoardLine(mm));
                StandAtBox(me, 1.3f, 0.75f);
            }
            yield return At(16f);

            // ---- hider 2 carries on; hider 1 is refused meanwhile
            if (h2 && sol != null)
            {
                Check("hider2 sees the progress", DotLinkPanel.Instance.NetBoard.CompleteCount == part1, BoardLine(mm));
                StandAtBox(me, 0.62f, 0f);
                yield return new WaitForSeconds(0.3f);
                me.DotLinkUseRpc(true);
                yield return new WaitForSeconds(1.4f);
                Check("hider2 at the panel", me.AtDotLink, BoardLine(mm));
                Shot("h2_continue");
                for (int c = part1; c < part2; c++) yield return DrawLine(sol[c]);
                yield return new WaitForSeconds(0.4f);
                Check("hider2 drew part 2", DotLinkPanel.Instance.NetBoard.CompleteCount == part2, BoardLine(mm));
                Shot("h2_more");
            }
            yield return At(19f);
            if (h1)
            {
                Check("busy prompt", lpc.Prompt == PromptKind.DotLink && lpc.PromptText.StartsWith("E  panel zajęty"), $"'{lpc.PromptText}'");
                Shot("busy_prompt");
                me.DotLinkUseRpc(true); // must be refused
            }
            yield return At(20.5f);
            if (h1) Check("hider1 refused while busy", !me.AtDotLink && mm.DotLinkUserPlayer == pH2, BoardLine(mm));
            yield return At(27f);

            // ---- the seeker knifes hider 2 at the panel (killed if still alive)
            if (seeker && pH2 != null)
            {
                Vector3 back = pH2.transform.position + new Vector3(lay.Normal.x, 0f, lay.Normal.z).normalized * 1.0f;
                me.TeleportLocal(back, Mathf.Atan2(-lay.Normal.x, -lay.Normal.z) * Mathf.Rad2Deg);
                yield return new WaitForSeconds(0.5f);
                me.RequestKnifeRpc(pH2.NetworkObjectId);
                Log($"knife request on {pH2.Nick}");
            }
            yield return At(29f);
            if (h1) Check("attack frees the panel", mm.DotLinkUserPlayer == null, $"{BoardLine(mm)} h2life={pH2?.Life.Value} busy={pH2?.Busy.Value}");
            yield return At(36f);
            if (h1 && pH2 != null && pH2.Life.Value != LifeState.Dead)
            {
                Log($"hider2 still {pH2.Life.Value}: killed by the test");
                mm.TestKill(pH2);
            }
            yield return At(38f);
            if (seeker) me.TeleportLocal(lay.Stand + new Vector3(lay.Normal.x, 0f, lay.Normal.z).normalized * 15f, 0f);
            if (!seeker) Check("lines kept after the death", DotLinkPanel.Instance.NetBoard.CompleteCount >= part1 && mm.DotLinkUserPlayer == null, BoardLine(mm));

            // ---- hider 1 finishes
            if (h1 && sol != null)
            {
                StandAtBox(me, 0.62f, 0f);
                yield return new WaitForSeconds(0.3f);
                me.DotLinkUseRpc(true);
                yield return new WaitForSeconds(1.4f);
                Check("hider1 back at the panel", me.AtDotLink, BoardLine(mm));
                for (int c = 0; c < colors; c++)
                    if (!DotLinkPanel.Instance.NetBoard.Complete(c)) yield return DrawLine(sol[c]);
                yield return new WaitForSeconds(0.15f);
                Shot("solved");
                yield return new WaitForSeconds(2.4f);
                Check("solved -> GateOpening, user let go", mm.Escape.Value >= EscapeStage.GateOpening && !me.AtDotLink && DotLinkPanel.Instance.NetBoard.SolvedFlag, BoardLine(mm));
                Shot("after");
                Render("solved_side", lay.Centre + lay.Normal * 1.1f + Vector3.up * 0.2f + Vector3.Cross(Vector3.up, lay.Normal) * 0.4f, lay.Centre, 40f);
            }
            yield return At(56f);
            Log($"end {BoardLine(mm)}");
        }
    }
}
