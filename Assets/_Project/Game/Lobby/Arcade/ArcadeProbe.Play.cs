using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    public sealed partial class ArcadeProbe
    {
        /// <summary>Autopilot game: turn buffer check, periodic screenshots, occluder check while the client stands in view.</summary>
        private IEnumerator Play(MatchManager mm, NetPlayer me, NetPlayer other, float seconds)
        {
            float t0 = Time.time, nextShot = t0 + 2.5f, nextLog = t0;
            int shots = 0, crashes = 0, steps = 0, maxScore = 0, dtWait = 0;
            Vector2Int lastHead = new(-1, -1), dtFrom = default;
            bool dtDone = false, watchRendered = false;
            int occStage = 0;
            float occSince = -1f, occT = 0f;
            while (Time.time - t0 < seconds)
            {
                var b = ArcadeState.Body;
                if (ArcadeState.State == ArcadeState.Over)
                {
                    if (Time.time - ArcadeState.StateSince > 1.2f)
                    {
                        crashes++;
                        Log($"natural crash #{crashes} score={ArcadeState.Score} -> restart");
                        mm.ServerArcadeDir(me, 1);
                    }
                }
                else if (ArcadeState.State == ArcadeState.Playing && b.Count > 1 && b[0] != lastHead)
                {
                    lastHead = b[0];
                    steps++;
                    maxScore = Mathf.Max(maxScore, ArcadeState.Score);
                    var head = b[0];
                    var cur = head - b[1];
                    if (dtWait > 0)
                    {
                        if (--dtWait == 0)
                        {
                            var want = dtFrom + Vector2Int.up + Vector2Int.left;
                            Log($"double turn (up+left in one frame): head={head} expected={want} -> {(head == want ? "OK" : "FAIL")}");
                        }
                    }
                    else if (!dtDone && ArcadeState.Score >= 2 && cur == Vector2Int.right && head.y < SnakeRules.H - 2 && head.x > 2
                             && !b.Contains(head + Vector2Int.up) && !b.Contains(head + Vector2Int.up + Vector2Int.left))
                    {
                        dtDone = true;
                        dtFrom = head;
                        dtWait = 2;
                        mm.ServerArcadeDir(me, 0);
                        mm.ServerArcadeDir(me, 3);
                    }
                    else
                    {
                        int d = SnakePilot.Next(b, ArcadeState.Food, ArcadeState.HasBonus, ArcadeState.Bonus);
                        if (d >= 0) mm.ServerArcadeDir(me, (byte)d);
                    }
                }

                if (Time.time >= nextShot && shots < 8)
                {
                    Shot($"fp_2_play_{shots:00}");
                    shots++;
                    nextShot = Time.time + 3.2f;
                }
                if (!watchRendered && ArcadeState.Score >= 6)
                {
                    watchRendered = true;
                    Render("play_left", new Vector3(5.0f, 1.6f, 3.8f), MatchManager.ArcadeScreen + Vector3.down * 0.25f, 50f);
                    Render("play_close", new Vector3(6.0f, 1.62f, 4.55f), MatchManager.ArcadeScreen, 48f);
                }

                if (other != null)
                {
                    bool inside = ArcadeKeepOut.Overlaps(other.transform.position, 0.3f, out _);
                    bool hidden = ArcadeKeepOut.Instance != null && ArcadeKeepOut.Instance.IsHidden(other);
                    if (Time.time >= nextLog)
                    {
                        nextLog = Time.time + 1f;
                        Log($"t={Time.time - t0:0.0} score={ArcadeState.Score} lvl={ArcadeState.Level} len={b.Count} bonus={ArcadeState.HasBonus} other={other.transform.position} inZone={inside} hidden={hidden}");
                    }
                    if (hidden && occSince < 0f) occSince = Time.time;
                    if (!hidden && occStage == 0) occSince = -1f;
                    switch (occStage)
                    {
                        case 0 when occSince > 0f && Time.time - occSince > 0.6f:
                            Log($"occluder in view: hidden={hidden}");
                            Shot("fp_5_occluder_hidden");
                            occStage = 1; occT = Time.time;
                            break;
                        case 1 when Time.time - occT > 0.2f:
                            ArcadeKeepOut.DisableHide = true;
                            occStage = 2; occT = Time.time;
                            break;
                        case 2 when Time.time - occT > 0.2f:
                            Shot("fp_6_occluder_visible_without_fix");
                            occStage = 3; occT = Time.time;
                            break;
                        case 3 when Time.time - occT > 0.2f:
                            ArcadeKeepOut.DisableHide = false;
                            occStage = 4;
                            break;
                    }
                }
                yield return null;
            }
            Log($"autopilot done steps={steps} maxScore={maxScore} crashes={crashes} level={ArcadeState.Level}");
        }
    }
}
